using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Map;
using SkiGame.POI;
using SkiGame.Navigation;
using static UnityEngine.UIElements.VisualElement;

namespace SkiGame.Map.UI
{
    /// <summary>
    /// Lightweight UI Toolkit map renderer for the Phone HUD:
    /// - Background texture from MapData
    /// - Polylines (runs/lifts) via Painter2D
    /// - Markers (POIs)
    /// - Pan/Zoom/Reset
    /// </summary>
    public sealed class PhoneMapPageUI
    {
        private MapData _mapData;
        private Camera _mapCamera;

        private VisualElement _viewport;
        private VisualElement _content;
        private VisualElement _bg;
        private VisualElement _polyHost;
        private VisualElement _markerHost;

        private Label _missingLabel;
        private VisualElement _btnReset;

        private VisualElement _btnCenterPlayer;

        private MapPolylineLayer _polyLayer;
        private MapRegionLayer _regionLayer;

        private bool _bound;
        private bool _dirty = true;

        private VisualElement _root;
        private bool _isScrollViewRoot;

        // Tracks when the viewport first gets a real (non-zero) layout rect.
        // Embedded minimaps can bind/refresh before layout resolves, which leaves the view at (0,0).
        private bool _pendingCenterOnPlayer;
        private bool _pendingCenterKeepZoom = true;
        private float _pendingCenterMinZoom = 1.0f;

        [SerializeField, Min(1f)] private float _centerOnPlayerButtonMinZoom = 1.75f;

        // -------------------------
        // Debug: projection validation overlay
        // -------------------------
        private bool _debugCompareProjections = false;
        private bool _debugLogProjectionDeltas = true;

        // Draws debug dots on top of the map to compare projections.
        private VisualElement _debugHost;

        // Small helper colors (only used for debug overlay dots).
        private static readonly Color DebugProjColor = new Color(1f, 1f, 1f, 0.95f);     // PROJ = white
        private static readonly Color DebugCamColor = new Color(1f, 0f, 1f, 0.95f);     // CAM  = magenta
        private static readonly Color DebugWarnColor = new Color(1f, 0.65f, 0f, 0.95f);  // warning = orange

        // Content size in UI local space (not texture pixels).
        private Vector2 _contentSize = new Vector2(1024f, 1024f);

        // Pan/zoom state
        private float _zoom = 1f;
        private Vector2 _pan = Vector2.zero;

        // Drag state
        private bool _dragging;
        private int _activePointerId = -1;
        private Vector2 _dragStartPointer;
        private Vector2 _dragStartPan;

        private string _lastWaypointClickedId;
        private float _lastWaypointClickTime = -10f;
        private const float WaypointDoubleClickWindowSeconds = 0.32f;

        private string _lastMarkerClickedId;
        private float _lastMarkerClickTime = -10f;

        private string _lastMarkerRightClickedId;
        private float _lastMarkerRightClickTime = -10f;

        private const float MarkerDoubleClickWindowSeconds = 0.32f;
        private const float MarkerRightDoubleClickWindowSeconds = 0.45f;

        // -------------------------
        // Player tracking + interactivity
        // -------------------------
        public event Action<Map.MapMarker> MarkerSelected;
        public event Action<Map.MapMarker> MarkerDoubleClicked;
        public event Action<Map.MapMarker> MarkerRightDoubleClicked;
        public event Action<Map.MapPolyline> PolylineSelected;
        public event Action<Vector3> BackgroundWorldClicked;
        public event Action<Vector3> BackgroundWorldDoubleClicked;
        public event Action SelectionCleared;

        public event Action<string> WaypointClicked;
        public event Action<string> WaypointDoubleClicked;
        public event Action<string> WaypointDeleteRequested;
        public event Action<string> WaypointLabelEditRequested;

        private Transform _playerTransform;
        private VisualElement _playerMarker;         // UI element (directional marker)
        private MapTrailLayer _trailLayer;           // Painter2D trail layer

        // Trail sampling (world XZ)
        private readonly List<Vector2> _trailWorldXZ = new();
        private float _nextTrailSampleTime;
        private Vector2 _lastTrailSampleWorldXZ;
        private bool _hasLastTrailSample;

        private bool _trackPlayerMarker = true;
        private bool _trackPlayerTrail = true;

        private float _trailSampleInterval = 0.20f;
        private float _trailMinDistanceMeters = 1.5f;
        private int _trailMaxSamples = 1500;

        // -------------------------
        // Minimap / embedded-map mode
        // -------------------------
        private bool _minimapFollowPlayer = false;
        private bool _lockPan = false;
        private bool _allowZoom = true;
        private bool _suppressSelection = false;
        private bool _hideMarkerLabels = false;

        // -------------------------
        // Map legend / filters
        // -------------------------
        private VisualElement _layerBar;
        private VisualElement _btnLayerRuns;
        private VisualElement _btnLayerLifts;
        private VisualElement _btnLayerPOIs;

        private readonly HashSet<POICategory> _visiblePOICategories = new();
        private int _maxVisibleRunDifficultyRank = 3; // Green..Black inclusive by default
        private bool _legendDefaultsInitialized;

        // -------------------------
        // Info panel + viewport sizing sync
        // -------------------------
        private VisualElement _mapBottomDock;   // "MapBottomDock"
        private VisualElement _mapInfoPanel;    // "MapInfoPanel"

        private bool _infoPanelRefsResolved;
        private bool _infoPanelCallbacksHooked;

        // Cached open state so we can react immediately even if no GeometryChanged fires.
        private bool _infoPanelOpenCached;
        private float _infoPanelReserveCached;

        // Centering should occur after the viewport is reduced (so the selected point is centered in reduced view).
        private bool _pendingCenterOnSelection;
        private float _pendingSelectionMinZoom = -1f;

        private bool _showRunOverlays = true;
        private bool _showLiftOverlays = true;
        private bool _showPOIOverlays = true;

        // Marker root bookkeeping so we can hide/show without rebuilding (preserves pan/zoom)
        private readonly Dictionary<string, VisualElement> _markerRoots = new();
        private readonly Dictionary<string, POIType> _markerTypes = new();

        private readonly Dictionary<string, SkiGame.Map.MapMarker> _markerById = new();
        private readonly Dictionary<string, Label> _poiOverlayLabels = new(); // markerId -> overlay label

        private readonly Dictionary<string, Label> _regionOverlayLabels = new();   // regionId -> overlay label
        private readonly Dictionary<string, Vector2> _regionAnchorLocal = new();   // regionId -> content-local anchor
        private MapRegionSet _regionSet;
        private string _selectedRegionId;
        private const float RegionOverviewZoomThreshold = 0.60f;

        private MapUIStyleSettings _style;
        private PointOfInterestRegistry _poiRegistry;

        // Cache created UI elements so we can restyle them when selection changes
        private readonly Dictionary<string, (VisualElement dot, Label label)> _markerVisuals = new();
        private readonly Dictionary<string, Label> _polylineLabelVisuals = new();
        private readonly Dictionary<string, Color> _polylineColorOverrides = new();

        // Links to unify selection for SkiRuns: selecting any linked element selects all.
        private readonly Dictionary<string, string> _markerToPolyline = new();   // markerId -> polylineId
        private readonly Dictionary<string, string> _polylineToMarker = new();   // polylineId -> markerId (first match)

        // Label accent colors (a small coloured stripe) to help disambiguate labels in dense areas.
        private readonly Dictionary<string, Color> _markerLabelAccent = new();
        private readonly Dictionary<string, Color> _polylineLabelAccent = new();

        // Lift pass requirement → map colouring
        private readonly Dictionary<string, int> _liftRequiredLevelByPolyline = new();
        private readonly Dictionary<string, Color> _liftRequiredColorByPolyline = new();

        private string _lastSelectedPolylineId;

        private const float LiftLabelOffsetPx = 10f; // above the midline (screen px)

        private const float PoiVisibleMinZoom = 1.15f;
        private const int MaxContextRunLiftLabels = 50;
        private const int MaxContextPoiLabels = 25;


        // -------------------------
        // Linked selection (Runs + Lifts)
        // -------------------------

        private enum LiftStationRole { None = 0, Bottom = 1, Top = 2 }

        // lift station marker(s) <-> lift polyline (multiple markers: top + bottom + optional “label marker”)
        private readonly Dictionary<string, string> _liftMarkerToPolyline = new();
        private readonly Dictionary<string, List<string>> _liftPolylineToMarkers = new();
        private readonly Dictionary<string, LiftStationRole> _liftMarkerRole = new();


        // If selection came from a lift station marker, we append this to the info panel title.
        private string _selectedLiftStationSuffix;
        public bool TryGetSelectedLiftStationSuffix(out string suffix)
        {
            suffix = _selectedLiftStationSuffix;
            return !string.IsNullOrEmpty(suffix);
        }
        public string SelectedLiftStationSuffix => _selectedLiftStationSuffix;

        // selection visuals need to support multiple markers (top+bottom at once)
        private readonly HashSet<string> _selectedMarkerSet = new();
        private readonly HashSet<string> _lastMarkerSet = new();

        /// <summary>
        /// Enables a "minimap" behavior:
        /// - optionally follows player (re-centers every Tick)
        /// - can lock panning
        /// - can allow/disallow zoom
        /// - can suppress selection (so clicks don't open info panels)
        /// - can hide marker labels (cleaner at small size)
        /// </summary>
        public void SetMinimapMode(
            bool enabled,
            bool followPlayer = true,
            bool lockPan = true,
            bool allowZoom = true,
            bool suppressSelection = true,
            bool hideMarkerLabels = true)
        {
            _minimapFollowPlayer = enabled && followPlayer;
            _lockPan = enabled && lockPan;
            _allowZoom = allowZoom; // allowZoom is meaningful even if not enabled, so don't gate it
            _suppressSelection = enabled && suppressSelection;
            _hideMarkerLabels = enabled && hideMarkerLabels;

            // If we hide labels, we need a rebuild to apply display toggles.
            _dirty = true;
        }

        /// <summary>
        /// Scroll-driven zoom for embedded maps (watch scroll, etc).
        /// Zooms around the viewport center.
        /// </summary>
        public void ZoomFromScroll(float scrollDeltaY)
        {
            if (!_bound) return;
            if (!_allowZoom) return;
            if (_viewport == null) return;

            if (!TryGetViewportSize(out float vw, out float vh))
                return;

            float prevZoom = _zoom;

            float zoomFactor = Mathf.Pow(1.12f, -scrollDeltaY * 0.1f);
            _zoom = Mathf.Clamp(_zoom * zoomFactor, 0.15f, 10f);

            Vector2 center = new Vector2(vw * 0.5f, vh * 0.5f);

            Vector2 contentAtCenterBefore = (center - _pan) / Mathf.Max(0.0001f, prevZoom);
            _pan = center - contentAtCenterBefore * _zoom;

            ApplyTransform();
            _polyLayer?.SetZoom(_zoom);
            _trailLayer?.SetZoom(_zoom);

        }

        // Click / drag discrimination for selection
        private bool _pointerDown;
        private bool _didDrag;
        private Vector2 _pointerDownPosViewport;
        private const float ClickDragThresholdPx = 6f;

        private float _lastLeftClickTime = -10f;
        private Vector2 _lastLeftClickViewportPos;
        private const float DoubleClickWindowSeconds = 0.32f;
        private const float DoubleClickDistancePx = 16f;

        private enum PendingMapClickKind
        {
            None = 0,
            Background = 1,
        }

        private PendingMapClickKind _pendingMapClickKind = PendingMapClickKind.None;
        private string _pendingMapClickRegionId;
        private Vector2 _pendingMapClickViewportPos;
        private Vector3 _pendingMapClickWorld;
        private float _pendingMapClickDueTime = -10f;
        private IVisualElementScheduledItem _pendingMapClickScheduledItem;

        private string _lastWaypointVisualLeftClickedId;
        private float _lastWaypointVisualLeftClickTime = -10f;

        private string _lastWaypointVisualRightClickedId;
        private float _lastWaypointVisualRightClickTime = -10f;

        // Selection state
        private string _selectedMarkerId;
        private string _selectedPolylineId;

        // Make marker hover less finicky (bigger invisible hit targets)
        private readonly Dictionary<string, VisualElement> _markerHitTargets = new();

        // Cheap lookup for label text (avoids scanning MapData every frame)
        private readonly Dictionary<string, string> _markerDisplayNames = new();

        // Explicit “always visible label” POIs (driven by marker.meta tokens, not UI toggles).
        private readonly Dictionary<string, bool> _poiAlwaysLabelById = new();

        // Fixed pixel offsets (screen-space) for labels
        private const float PoiLabelOffsetPx = 18f;  // label center sits below marker pivot
        private const float RunLabelOffsetPx = 18f;  // same; tweak independently if needed

        private readonly Dictionary<string, Vector2> _markerAnchorLocal = new();       // markerId -> content-local anchor (pre-zoom)

        // -------------------------
        // Overlay label system (stable + simple)
        // Labels live in viewport space (NOT inside scaled MapContent).
        // -------------------------
        private VisualElement _labelOverlay;

        // Anchors in content-local (same space as markers / polylines before pan+zoom)
        private readonly Dictionary<string, Vector2> _polyAnchorLocal = new();       // polylineId -> content-local
        private readonly Dictionary<string, Vector2> _polyNormalLocal = new();       // polylineId -> preferred normal dir (content-local)

        // Remember the last “slot” we successfully used to reduce jitter
        private readonly Dictionary<string, int> _labelSlotCache = new();

        // Tunables (screen px)
        private const float LabelCellSizePx = 96f;            // spatial hash cell size
        private const float LabelBaseOffsetPx = 12f;          // how far from anchor we try to place
        private const float RunLiftAlwaysVisibleMinZoom = 0.85f; // show run/lift labels once zoomed enough

        private SkiGame.Navigation.MapWaypointManager _waypointManager;
        private int _lastWaypointVersion = -1;

        private readonly Dictionary<string, VisualElement> _waypointMarkerRoots = new();
        private readonly Dictionary<string, VisualElement> _waypointMarkerDots = new();
        private readonly Dictionary<string, Label> _waypointLabels = new();
        private readonly Dictionary<string, Vector2> _waypointAnchorLocal = new();

        private Label _activeWaypointArrow;
        private int _pointerButton = -1;

        private string _lastPolylineClickedId;
        private float _lastPolylineClickTime = -10f;

        private string _lastPolylineRightClickedId;
        private float _lastPolylineRightClickTime = -10f;

        private VisualElement _navigationOverlay;
        private readonly Dictionary<string, Label> _waypointNavArrows = new();
        private WaypointConnectorOverlay _selectedWaypointConnector;

        public void SetWaypointManager(SkiGame.Navigation.MapWaypointManager waypointManager)
        {
            _waypointManager = waypointManager;
            _lastWaypointVersion = -1;
            _dirty = true;
        }

        // Overlay labels should behave like any other selectable map element.
        // (They live in viewport space so we intercept clicks to prevent the viewport drag handler from capturing.)
        private void MakeOverlayLabelInteractive(
     Label label,
     Action onClick,
     Action onDoubleClick = null,
     Action onRightDoubleClick = null)
        {
            if (label == null)
                return;

            label.pickingMode = PickingMode.Position;

            label.RegisterCallback<PointerEnterEvent>(_ => label.AddToClassList("is-hovered"));
            label.RegisterCallback<PointerLeaveEvent>(_ => label.RemoveFromClassList("is-hovered"));

            float lastLeftClickTime = -10f;
            float lastRightClickTime = -10f;

            label.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 || evt.button == 1)
                    evt.StopPropagation();
            });

            label.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_suppressSelection)
                {
                    evt.StopPropagation();
                    return;
                }

                float now = Time.unscaledTime;

                if (evt.button == 0)
                {
                    bool isDouble =
                        (now - lastLeftClickTime) <= MarkerDoubleClickWindowSeconds;

                    lastLeftClickTime = isDouble ? -10f : now;

                    if (isDouble && onDoubleClick != null)
                        onDoubleClick.Invoke();
                    else
                        onClick?.Invoke();

                    evt.StopPropagation();
                }
                else if (evt.button == 1)
                {
                    bool isDouble =
                        (now - lastRightClickTime) <= MarkerRightDoubleClickWindowSeconds;

                    lastRightClickTime = isDouble ? -10f : now;

                    if (isDouble)
                        onRightDoubleClick?.Invoke();

                    evt.StopPropagation();
                }
            });
        }

        private void SelectPOIMarkerOnly(string markerId, bool fireEvent)
        {
            if (string.IsNullOrEmpty(markerId)) return;

            _selectedLiftStationSuffix = null;
            _selectedPolylineId = null;
            _selectedMarkerId = markerId;

            UpdateSelectionVisuals();
            _polyLayer?.SetSelected(null);

            if (fireEvent && _markerById.TryGetValue(markerId, out var m))
            {
                RequestCenterOnSelectionWhenInfoPanelOpen(minZoom: -1f);
                MarkerSelected?.Invoke(m);
            }
        }

        private void EnsureLabelOverlay()
        {
            if (_viewport == null) return;

            if (_labelOverlay == null)
            {
                _labelOverlay = new VisualElement();
                _labelOverlay.name = "MapLabelOverlay";
                _labelOverlay.style.position = Position.Absolute;
                _labelOverlay.style.left = 0;
                _labelOverlay.style.top = 0;
                _labelOverlay.style.right = 0;
                _labelOverlay.style.bottom = 0;

                // Overlay itself ignores input; labels inside can still receive input.
                _labelOverlay.pickingMode = PickingMode.Ignore;

                _viewport.Add(_labelOverlay);
            }

            _labelOverlay.BringToFront();
        }

        private void SelectPolylineInternal(string polylineId, Map.MapPolyline polyline, bool fireEvent)
        {
            _selectedPolylineId = polylineId;

            // If this is a SkiRun, also select its linked marker (if present).
            if (polyline.lineType == MapLineType.SkiRun && _polylineToMarker.TryGetValue(polylineId, out var markerId))
                _selectedMarkerId = markerId;
            else
                _selectedMarkerId = null;

            UpdateSelectionVisuals();

            if (fireEvent)
                PolylineSelected?.Invoke(polyline);
        }

        private void ClearSelectionInternal(bool fireEvent)
        {
            _selectedLiftStationSuffix = null;
            _selectedPolylineId = null;
            _selectedMarkerId = null;
            _selectedRegionId = null;
            _pendingCenterOnSelection = false;
            _pendingSelectionMinZoom = -1f;

            _regionLayer?.SetSelected(null);

            UpdateSelectionVisuals();

            foreach (var kv in _regionOverlayLabels)
                ApplyRegionOverlayLabelVisual(kv.Key, selected: false);

            LayoutOverlayLabels();

            if (fireEvent)
                SelectionCleared?.Invoke();
        }

        private sealed class WaypointConnectorOverlay : VisualElement
        {
            public bool Visible { get; private set; }
            public Vector2 Start { get; private set; }
            public Vector2 End { get; private set; }
            public Color LineColor { get; private set; } = Color.white;

            public WaypointConnectorOverlay()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.right = 0f;
                style.bottom = 0f;
                style.display = DisplayStyle.None;

                generateVisualContent += OnGenerateVisualContent;
            }

            public void Show(Vector2 start, Vector2 end, Color color)
            {
                Visible = true;
                Start = start;
                End = end;
                LineColor = color;
                style.display = DisplayStyle.Flex;
                MarkDirtyRepaint();
            }

            public void Hide()
            {
                Visible = false;
                style.display = DisplayStyle.None;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext mgc)
            {
                if (!Visible)
                    return;

                Vector2 dir = End - Start;
                float len = dir.magnitude;
                if (len < 6f)
                    return;

                Vector2 n = dir / len;
                var p = mgc.painter2D;

                const float dashLength = 10f;
                const float gapLength = 7f;
                const float lineWidth = 2.5f;

                p.strokeColor = LineColor;
                p.lineWidth = lineWidth;

                float traveled = 0f;
                while (traveled < len)
                {
                    float segStart = traveled;
                    float segEnd = Mathf.Min(traveled + dashLength, len);

                    Vector2 a = Start + n * segStart;
                    Vector2 b = Start + n * segEnd;

                    p.BeginPath();
                    p.MoveTo(a);
                    p.LineTo(b);
                    p.Stroke();

                    traveled += dashLength + gapLength;
                }
            }
        }

        public void Bind(VisualElement root, MapData mapData, Camera mapCamera)
        {
            if (root == null) return;

            _root = root;
            _isScrollViewRoot = root is ScrollView;

            _mapData = mapData;

            // Keep the reference camera always. PreferCameraProjection controls whether we USE it for placement,
            // but we still want it available for debug comparison and for deriving a better MapProjection.
            _mapCamera = mapCamera;

            // IMPORTANT: do NOT reconfigure the camera from MapProjection here.
            // The whole point is to let the camera remain an independent reference that matches the background image.
            ConfigureReferenceCamera(); // now becomes a lightweight validator (see replacement below)

            _viewport = root.Q<VisualElement>("MapViewport");
            EnsureLabelOverlay();

            _content = root.Q<VisualElement>("MapContent");
            _bg = root.Q<VisualElement>("MapBackground");
            _polyHost = root.Q<VisualElement>("MapPolylines");
            _markerHost = root.Q<VisualElement>("MapMarkers");

            // Reassert the same runtime invariants that the old PhoneHUD embedded maps used.
            if (_viewport != null)
            {
                _viewport.style.overflow = Overflow.Hidden;
            }

            if (_content != null)
            {
                _content.style.position = Position.Absolute;
                _content.style.left = 0;
                _content.style.top = 0;
                _content.style.right = StyleKeyword.Auto;
                _content.style.bottom = StyleKeyword.Auto;
                _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            }

            // Ensure absolute positioning for map layers.
            if (_bg != null)
            {
                _bg.style.position = Position.Absolute;
                _bg.style.left = 0;
                _bg.style.top = 0;
                _bg.style.right = StyleKeyword.Auto;
                _bg.style.bottom = StyleKeyword.Auto;
            }

            if (_polyHost != null)
            {
                _polyHost.Clear();

                _regionLayer = new MapRegionLayer();
                _polyHost.Add(_regionLayer);   // draw above background, below runs/lifts

                _polyLayer = new MapPolylineLayer();
                _polyHost.Add(_polyLayer);

                _trailLayer = new MapTrailLayer();
                _polyHost.Add(_trailLayer); // draw above base polylines, below markers
            }

            if (_markerHost != null)
            {
                _markerHost.style.position = Position.Absolute;
                _markerHost.style.left = 0;
                _markerHost.style.top = 0;
                _markerHost.style.right = StyleKeyword.Auto;
                _markerHost.style.bottom = StyleKeyword.Auto;
            }

            // Enforce draw order by hierarchy (works across UI Toolkit versions).
            // Background at the back, then polylines, then markers on top.
            _bg?.SendToBack();
            _polyHost?.BringToFront();
            _markerHost?.BringToFront();

            _missingLabel = root.Q<Label>("Lbl_MapMissing");
            _btnReset = root.Q<VisualElement>("Btn_MapReset");

            if (_btnReset != null)
            {
                _btnReset.RegisterCallback<ClickEvent>(_ =>
                {
                    CancelPendingMapClick();
                    ClearSelectionInternal(fireEvent: true);
                    ResetViewToFit();
                });
            }

            _btnCenterPlayer = root.Q<VisualElement>("Btn_MapCenterPlayer");
            if (_btnCenterPlayer != null)
            {
                _btnCenterPlayer.tooltip = "Center on player";
                _btnCenterPlayer.RegisterCallback<ClickEvent>(_ =>
                    RequestCenterOnPlayer(keepZoom: false, minZoom: _centerOnPlayerButtonMinZoom));
            }

            BindLayerBar(root);

            // Resolve dock + info panel globally (they are not children of Page_Map in UXML)
            ResolveInfoPanelRefs();

            if (_viewport != null)
            {
                // Pan/Zoom handlers
                _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
                _viewport.RegisterCallback<PointerCancelEvent>(OnPointerUp);

                // Capture wheel to prevent ScrollView from scrolling
                _viewport.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            }

            // The layer stack was already created above.
            // Do not clear/recreate it here, otherwise the region layer gets removed.
            if (_polyHost != null)
            {
                if (_regionLayer == null || _regionLayer.parent != _polyHost)
                {
                    _regionLayer = new MapRegionLayer();
                    _polyHost.Add(_regionLayer);
                }

                if (_polyLayer == null || _polyLayer.parent != _polyHost)
                {
                    _polyLayer = new MapPolylineLayer();
                    _polyHost.Add(_polyLayer);
                }

                if (_trailLayer == null || _trailLayer.parent != _polyHost)
                {
                    _trailLayer = new MapTrailLayer();
                    _polyHost.Add(_trailLayer); // draw above base polylines, below markers
                }
            }

            // Install player marker (persistent; not cleared when rebuilding POI markers)
            if (_markerHost != null && _playerMarker == null)
            {
                _playerMarker = new VisualElement();
                _playerMarker.name = "MapPlayerMarker";
                _playerMarker.AddToClassList("map-player-marker");
                _playerMarker.style.position = Position.Absolute;
                _playerMarker.style.width = 26f;
                _playerMarker.style.height = 26f;
                _playerMarker.style.justifyContent = Justify.Center;
                _playerMarker.style.alignItems = Align.Center;
                _playerMarker.style.backgroundColor = Color.clear;

                // Base circle
                var baseCircle = new VisualElement();
                baseCircle.name = "MapPlayerMarkerBase";
                baseCircle.style.position = Position.Absolute;
                baseCircle.style.width = 11f;
                baseCircle.style.height = 11f;
                baseCircle.style.left = 7.5f;
                baseCircle.style.top = 13.5f;
                baseCircle.style.borderTopLeftRadius = 999f;
                baseCircle.style.borderTopRightRadius = 999f;
                baseCircle.style.borderBottomLeftRadius = 999f;
                baseCircle.style.borderBottomRightRadius = 999f;
                baseCircle.style.backgroundColor = Color.mediumSlateBlue;
                baseCircle.style.borderLeftWidth = 2f;
                baseCircle.style.borderRightWidth = 2f;
                baseCircle.style.borderTopWidth = 2f;
                baseCircle.style.borderBottomWidth = 2f;
                baseCircle.style.borderLeftColor = new Color(0f, 0f, 0f, 0.75f);
                baseCircle.style.borderRightColor = new Color(0f, 0f, 0f, 0.75f);
                baseCircle.style.borderTopColor = new Color(0f, 0f, 0f, 0.75f);
                baseCircle.style.borderBottomColor = new Color(0f, 0f, 0f, 0.75f);
                baseCircle.pickingMode = PickingMode.Ignore;

                // Arrow body
                var arrowBody = new VisualElement();
                arrowBody.name = "MapPlayerMarkerArrow";
                arrowBody.style.position = Position.Absolute;
                arrowBody.style.width = 0f;
                arrowBody.style.height = 0f;
                arrowBody.style.left = 5f;
                arrowBody.style.top = 1f;
                arrowBody.style.borderLeftWidth = 8f;
                arrowBody.style.borderRightWidth = 8f;
                arrowBody.style.borderBottomWidth = 15f;
                arrowBody.style.borderLeftColor = Color.clear;
                arrowBody.style.borderRightColor = Color.clear;
                arrowBody.style.borderBottomColor = Color.mediumSlateBlue;
                arrowBody.pickingMode = PickingMode.Ignore;

                // Arrow outline behind the body
                var arrowOutline = new VisualElement();
                arrowOutline.name = "MapPlayerMarkerArrowOutline";
                arrowOutline.style.position = Position.Absolute;
                arrowOutline.style.width = 0f;
                arrowOutline.style.height = 0f;
                arrowOutline.style.left = 3f;
                arrowOutline.style.top = -1f;
                arrowOutline.style.borderLeftWidth = 10f;
                arrowOutline.style.borderRightWidth = 10f;
                arrowOutline.style.borderBottomWidth = 18f;
                arrowOutline.style.borderLeftColor = Color.clear;
                arrowOutline.style.borderRightColor = Color.clear;
                arrowOutline.style.borderBottomColor = new Color(0f, 0f, 0f, 0.75f);
                arrowOutline.pickingMode = PickingMode.Ignore;

                // Small white center dot for readability
                var centerDot = new VisualElement();
                centerDot.name = "MapPlayerMarkerCenter";
                centerDot.style.position = Position.Absolute;
                centerDot.style.width = 4f;
                centerDot.style.height = 4f;
                centerDot.style.left = 11f;
                centerDot.style.top = 17f;
                centerDot.style.borderTopLeftRadius = 999f;
                centerDot.style.borderTopRightRadius = 999f;
                centerDot.style.borderBottomLeftRadius = 999f;
                centerDot.style.borderBottomRightRadius = 999f;
                centerDot.style.backgroundColor = Color.white;
                centerDot.pickingMode = PickingMode.Ignore;

                _playerMarker.Add(arrowOutline);
                _playerMarker.Add(arrowBody);
                _playerMarker.Add(baseCircle);
                _playerMarker.Add(centerDot);

                _playerMarker.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

                _markerHost.Add(_playerMarker);
            }

            ApplyPlayerMarkerStyle();

            if (_markerHost != null && _activeWaypointArrow == null)
            {
                _activeWaypointArrow = new Label("▲");
                _activeWaypointArrow.name = "MapActiveWaypointArrow";
                _activeWaypointArrow.style.position = Position.Absolute;
                _activeWaypointArrow.style.color = Color.white;
                _activeWaypointArrow.style.fontSize = 18f;
                _activeWaypointArrow.style.unityFontStyleAndWeight = FontStyle.Bold;
                _activeWaypointArrow.style.display = DisplayStyle.None;
                _activeWaypointArrow.pickingMode = PickingMode.Ignore;
                _markerHost.Add(_activeWaypointArrow);
            }

            if (_viewport != null && _navigationOverlay == null)
            {
                _navigationOverlay = new VisualElement();
                _navigationOverlay.name = "MapNavigationOverlay";
                _navigationOverlay.pickingMode = PickingMode.Ignore;
                _navigationOverlay.style.position = Position.Absolute;
                _navigationOverlay.style.left = 0f;
                _navigationOverlay.style.top = 0f;
                _navigationOverlay.style.right = 0f;
                _navigationOverlay.style.bottom = 0f;

                _selectedWaypointConnector = new WaypointConnectorOverlay();
                _navigationOverlay.Add(_selectedWaypointConnector);

                _viewport.Add(_navigationOverlay);
                _navigationOverlay.BringToFront();
            }

            // Install debug overlay layer (above polylines and markers).
            // We attach it to _content so it inherits pan/zoom transforms.
            if (_content != null)
            {
                _debugHost = new VisualElement { name = "MapDebugOverlay" };
                _debugHost.pickingMode = PickingMode.Ignore;

                // Absolute so we can place dots with left/top in local content space.
                _debugHost.style.position = Position.Absolute;
                _debugHost.style.left = 0;
                _debugHost.style.top = 0;
                _debugHost.style.right = 0;
                _debugHost.style.bottom = 0;

                // Add last so it draws above other children in the visual tree.
                _content.Add(_debugHost);
            }

            _bound = (_viewport != null && _content != null && _bg != null && _polyHost != null && _markerHost != null);
            _dirty = true;

            // Geometry init:
            // - For ScrollView root (Page_Map), force a stable viewport height so the map is visible.
            // - For embedded minimaps, this simply triggers Refresh once sizes resolve.
            if (_viewport != null)
            {
                _viewport.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    if (_isScrollViewRoot)
                        EnsureViewportHeightForScrollViewRoot();

                    if (_dirty)
                    {
                        Refresh();
                    }
                    else
                    {
                        UpdateLayerSizes();
                        RebuildRegionOverlayLabels();
                    }
                });
            }

            if (_isScrollViewRoot)
            {
                // Also listen to root size changes (phone open/close, orientation, etc.)
                _root.RegisterCallback<GeometryChangedEvent>(_ => EnsureViewportHeightForScrollViewRoot());
                EnsureViewportHeightForScrollViewRoot();
            }
        }

        private void BindLayerBar(VisualElement root)
        {
            _layerBar = root.Q<VisualElement>("MapLayerBar");
            if (_layerBar == null)
                return;

            // Always float the legend inside the viewport so it behaves consistently
            // for both Page_Map and MountainHudMapRootFactory roots.
            if (_viewport != null && _layerBar.parent != _viewport)
            {
                _layerBar.RemoveFromHierarchy();
                _viewport.Add(_layerBar);
            }

            _layerBar.pickingMode = PickingMode.Position;
            _layerBar.style.position = Position.Absolute;
            _layerBar.style.right = 14f;
            _layerBar.style.top = 14f;
            _layerBar.style.left = StyleKeyword.Auto;
            _layerBar.style.bottom = StyleKeyword.Auto;

            EnsureLegendDefaults();
            RebuildLegendUI();
        }

        private void EnsureLegendDefaults()
        {
            if (_legendDefaultsInitialized)
                return;

            _visiblePOICategories.Clear();

            if (_mapData != null && _mapData.Markers != null)
            {
                for (int i = 0; i < _mapData.Markers.Count; i++)
                {
                    var m = _mapData.Markers[i];
                    if (!m.IsValid)
                        continue;

                    if (m.type == POIType.SkiRun || m.type == POIType.SkiLift)
                        continue;

                    _visiblePOICategories.Add(NormalizeCategoryForLegend(m.category));
                }
            }

            if (_visiblePOICategories.Count == 0)
            {
                _visiblePOICategories.Add(POICategory.Resort);
                _visiblePOICategories.Add(POICategory.Shop);
                _visiblePOICategories.Add(POICategory.Kiosk);
                _visiblePOICategories.Add(POICategory.Service);
                _visiblePOICategories.Add(POICategory.Landmark);
                _visiblePOICategories.Add(POICategory.Custom);
            }

            _maxVisibleRunDifficultyRank = 3;
            _legendDefaultsInitialized = true;
        }

        private void RebuildLegendUI()
        {
            if (_layerBar == null)
                return;

            EnsureLegendDefaults();

            _layerBar.Clear();
            _layerBar.AddToClassList("map-legend-panel");

            var header = new Label("Map Filters");
            header.AddToClassList("map-legend-title");
            _layerBar.Add(header);

            var topRow = new VisualElement();
            topRow.AddToClassList("map-legend-row");

            _btnLayerRuns = CreateLegendToggleChip("Runs", _showRunOverlays, () =>
            {
                _showRunOverlays = !_showRunOverlays;
                ApplyLayerVisibility();
            });
            _btnLayerRuns.AddToClassList("legend-chip-run");

            _btnLayerLifts = CreateLegendToggleChip("Lifts", _showLiftOverlays, () =>
            {
                _showLiftOverlays = !_showLiftOverlays;
                ApplyLayerVisibility();
            });
            _btnLayerLifts.AddToClassList("legend-chip-lift");

            _btnLayerPOIs = CreateLegendToggleChip("POIs", _showPOIOverlays, () =>
            {
                _showPOIOverlays = !_showPOIOverlays;
                ApplyLayerVisibility();
            });
            _btnLayerPOIs.AddToClassList("legend-chip-poi");

            topRow.Add(_btnLayerRuns);
            topRow.Add(_btnLayerLifts);
            topRow.Add(_btnLayerPOIs);
            _layerBar.Add(topRow);

            var diffLabel = new Label("Max run difficulty");
            diffLabel.AddToClassList("map-legend-section-label");
            _layerBar.Add(diffLabel);

            var diffRow = new VisualElement();
            diffRow.AddToClassList("map-legend-row");

            diffRow.Add(CreateDifficultyChip("Green", 0));
            diffRow.Add(CreateDifficultyChip("Blue", 1));
            diffRow.Add(CreateDifficultyChip("Red", 2));
            diffRow.Add(CreateDifficultyChip("Black", 3));

            _layerBar.Add(diffRow);

            var poiLabel = new Label("POIs");
            poiLabel.AddToClassList("map-legend-section-label");
            _layerBar.Add(poiLabel);

            var poiWrap = new VisualElement();
            poiWrap.AddToClassList("map-legend-wrap");

            foreach (var category in GetLegendCategoriesSorted())
            {
                var chip = CreateLegendToggleChip(
                    FormatPOICategoryLabel(category),
                    IsPOICategoryVisible(category),
                    () =>
                    {
                        TogglePOICategory(category);
                        ApplyLayerVisibility();
                    });

                chip.AddToClassList(GetPOICategoryClass(category));
                poiWrap.Add(chip);
            }

            _layerBar.Add(poiWrap);
        }

        private VisualElement CreateLegendToggleChip(string text, bool on, Action onClick)
        {
            var chip = new VisualElement();
            chip.AddToClassList("map-legend-chip");
            chip.EnableInClassList("is-on", on);

            var dot = new VisualElement();
            dot.AddToClassList("map-legend-chip-dot");
            chip.Add(dot);

            var label = new Label(text);
            label.AddToClassList("map-legend-chip-text");
            chip.Add(label);

            HookLayerBtn(chip, onClick);
            return chip;
        }

        private VisualElement CreateDifficultyChip(string text, int rank)
        {
            bool on = _maxVisibleRunDifficultyRank == rank;

            var chip = new VisualElement();
            chip.AddToClassList("map-legend-chip");
            chip.AddToClassList("map-legend-chip-compact");
            chip.EnableInClassList("is-on", on);
            chip.EnableInClassList($"is-difficulty-{text.ToLowerInvariant()}", true);

            var dot = new VisualElement();
            dot.AddToClassList("map-legend-chip-dot");
            chip.Add(dot);

            var label = new Label(text);
            label.AddToClassList("map-legend-chip-text");
            chip.Add(label);

            HookLayerBtn(chip, () =>
            {
                _maxVisibleRunDifficultyRank = rank;
                ApplyLayerVisibility();
            });

            return chip;
        }

        private IEnumerable<POICategory> GetLegendCategoriesSorted()
        {
            var list = new List<POICategory>(_visiblePOICategories);

            if (_mapData != null && _mapData.Markers != null)
            {
                for (int i = 0; i < _mapData.Markers.Count; i++)
                {
                    var m = _mapData.Markers[i];
                    if (!m.IsValid)
                        continue;

                    if (m.type == POIType.SkiRun || m.type == POIType.SkiLift)
                        continue;

                    var normalized = NormalizeCategoryForLegend(m.category);
                    if (!list.Contains(normalized))
                        list.Add(normalized);
                }
            }

            list.Sort((a, b) => GetLegendCategorySortKey(a).CompareTo(GetLegendCategorySortKey(b)));
            return list;
        }

        private static int GetLegendCategorySortKey(POICategory category)
        {
            switch (category)
            {
                case POICategory.Resort: return 0;
                case POICategory.Kiosk: return 1;
                case POICategory.Shop: return 2;
                case POICategory.Service: return 3;
                case POICategory.Landmark: return 4;
                case POICategory.Custom: return 5;
                default: return 99;
            }
        }

        private static POICategory NormalizeCategoryForLegend(POICategory category)
        {
            return category == POICategory.None ? POICategory.Custom : category;
        }

        private bool IsPOICategoryVisible(POICategory category)
        {
            return _visiblePOICategories.Contains(NormalizeCategoryForLegend(category));
        }

        private void TogglePOICategory(POICategory category)
        {
            category = NormalizeCategoryForLegend(category);

            if (!_visiblePOICategories.Add(category))
                _visiblePOICategories.Remove(category);
        }

        private static string FormatPOICategoryLabel(POICategory category)
        {
            switch (NormalizeCategoryForLegend(category))
            {
                case POICategory.Resort: return "Resorts";
                case POICategory.Kiosk: return "Kiosks";
                case POICategory.Shop: return "Shops";
                case POICategory.Service: return "Services";
                case POICategory.Landmark: return "Landmarks";
                case POICategory.Custom: return "Other";
                default: return category.ToString();
            }
        }

        private static string GetPOICategoryClass(POICategory category)
        {
            switch (NormalizeCategoryForLegend(category))
            {
                case POICategory.Resort: return "legend-chip-resort";
                case POICategory.Kiosk: return "legend-chip-kiosk";
                case POICategory.Shop: return "legend-chip-shop";
                case POICategory.Service: return "legend-chip-service";
                case POICategory.Landmark: return "legend-chip-landmark";
                case POICategory.Custom: return "legend-chip-custom";
                default: return "legend-chip-custom";
            }
        }

        private static void HookLayerBtn(VisualElement btn, Action onClick)
        {
            if (btn == null) return;

            btn.RegisterCallback<ClickEvent>(e =>
            {
                onClick?.Invoke();
                e.StopPropagation();
            });

            // Prevent accidental map drag start if this ends up within the viewport area on some layouts.
            btn.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
        }

        private void UpdateLayerBarVisuals()
        {
            SetOn(_btnLayerRuns, _showRunOverlays);
            SetOn(_btnLayerLifts, _showLiftOverlays);
            SetOn(_btnLayerPOIs, _showPOIOverlays);

            static void SetOn(VisualElement ve, bool on)
            {
                if (ve == null) return;
                ve.EnableInClassList("is-on", on);
            }
        }

        private void ApplyLayerVisibility()
        {
            _polyLayer?.SetFilterState(_showRunOverlays, _showLiftOverlays, _maxVisibleRunDifficultyRank);

            foreach (var kv in _markerRoots)
            {
                string id = kv.Key;
                var root = kv.Value;
                if (root == null) continue;

                root.style.display = IsMarkerVisibleById(id) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RebuildLegendUI();
            ClearSelectionIfHidden();
        }

        private bool IsMarkerVisibleById(string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId) || !_markerById.TryGetValue(markerId, out var marker))
                return false;

            if (marker.type == POIType.SkiRun)
            {
                string runId = marker.id;
                if (_markerToPolyline.TryGetValue(marker.id, out var linkedRunId))
                    runId = linkedRunId;

                return IsRunVisibleById(runId);
            }

            if (marker.type == POIType.SkiLift)
                return _showLiftOverlays;

            return _showPOIOverlays && IsPOICategoryVisible(marker.category);
        }

        private bool IsPolylineVisible(MapPolyline polyline)
        {
            if (polyline.lineType == MapLineType.SkiRun)
                return IsRunVisibleById(polyline.id);

            if (polyline.lineType == MapLineType.SkiLift)
                return _showLiftOverlays;

            return true;
        }

        private bool IsRunVisibleById(string runId)
        {
            if (!_showRunOverlays)
                return false;

            if (TryGetRunDifficultyRank(runId, out int rank))
                return rank <= _maxVisibleRunDifficultyRank;

            return true;
        }

        private bool TryGetRunDifficultyRank(string runId, out int rank)
        {
            rank = 0;

            if (_mapData == null || string.IsNullOrWhiteSpace(runId))
                return false;

            var corridors = _mapData.RunCorridors;
            if (corridors != null)
            {
                for (int i = 0; i < corridors.Count; i++)
                {
                    if (string.Equals(corridors[i].id, runId, StringComparison.Ordinal))
                    {
                        rank = corridors[i].difficultyRank;
                        return true;
                    }
                }
            }

            var polys = _mapData.Polylines;
            if (polys != null)
            {
                for (int i = 0; i < polys.Count; i++)
                {
                    if (string.Equals(polys[i].id, runId, StringComparison.Ordinal))
                    {
                        rank = polys[i].difficultyRank;
                        return true;
                    }
                }
            }

            return false;
        }

        private void ClearSelectionIfHidden()
        {
            if (!string.IsNullOrEmpty(_selectedPolylineId) && TryGetPolylineById(_selectedPolylineId, out var p))
            {
                if (!IsPolylineVisible(p))
                {
                    ClearSelectionInternal(fireEvent: true);
                    _polyLayer?.SetSelected(null);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(_selectedMarkerId))
            {
                if (!IsMarkerVisibleById(_selectedMarkerId))
                {
                    ClearSelectionInternal(fireEvent: true);
                    _polyLayer?.SetSelected(null);
                }
            }
        }

        public void RequestViewportRecalc()
        {
            EnsureViewportHeightForScrollViewRoot();
        }

        /// <summary>
        /// Call when the map page is shown (or when data changes).
        /// Safe to call frequently; rebuilds are throttled by _dirty.
        /// </summary>
        public void Refresh()
        {
            if (!_bound) return;

            bool hasData = _mapData != null && _mapData.Projection.IsValid;
            if (_missingLabel != null)
                _missingLabel.style.display = hasData ? DisplayStyle.None : DisplayStyle.Flex;

            Camera activeProjectionCamera = (_mapData != null && _mapData.PreferCameraProjection && _mapCamera != null) ? _mapCamera : null;

            if (!hasData)
            {
                // Clear visuals when missing
                _bg.style.backgroundImage = StyleKeyword.None;
                _markerHost.Clear();


                _regionLayer?.SetData(_regionSet, _contentSize);
                _polyLayer?.SetData(_mapData, _contentSize, activeProjectionCamera);
                _trailLayer?.SetData(_mapData, _contentSize, activeProjectionCamera);

                _regionLayer?.SetZoom(_zoom);
                _polyLayer?.SetZoom(_zoom);
                _trailLayer?.SetZoom(_zoom);

                return;
            }

            if (!_dirty)
            {
                // Still ensure painter layer gets the latest transform sizing
                UpdateLayerSizes();
                return;
            }

            // Compute content size: preserve aspect ratio of world bounds (XZ)
            // Prefer the actual background texture aspect, because the UI is drawing OVER the texture.
            float aspect;
            var bg = _mapData?.BackgroundTexture; // or however you access it

            if (bg != null && bg.width > 0 && bg.height > 0)
            {
                aspect = (float)bg.height / (float)bg.width;
            }
            else
            {
                // Fallback to world aspect if no texture
                var worldSize = _mapData.Projection.WorldSizeXZ; // Vector2 (x,z)
                aspect = (worldSize.x > 0.0001f) ? (worldSize.y / worldSize.x) : 1f;
            }

            float baseWidth = 1024f; // keep your existing base width
            _contentSize = new Vector2(baseWidth, baseWidth * aspect);

            // Keep ScaleToFit (fine once aspect matches) OR StretchToFill (forces fit but distorts).
            _bg.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

            ApplyContentSizing();

            // Background texture (optional)
            if (_mapData.BackgroundTexture != null)
            {
                _bg.style.backgroundImage = new StyleBackground(_mapData.BackgroundTexture);

                // Scale-to-fit is the compatible way across UI Toolkit versions.
                // (Avoid BackgroundSize.Contain which is not available in some Unity versions.)
                _bg.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

            }
            else
            {
                _bg.style.backgroundImage = StyleKeyword.None;
            }

            _regionLayer?.SetData(_regionSet, _contentSize);
            _polyLayer?.SetData(_mapData, _contentSize, activeProjectionCamera);
            _trailLayer?.SetData(_mapData, _contentSize, activeProjectionCamera);

            // Markers
            RebuildMarkers();
            EnsureLegendDefaults();
            RebuildLegendUI();
            ApplyLayerVisibility();

            RebuildRegionOverlayLabels();

            _dirty = false;

            _regionLayer?.SetZoom(_zoom);
            _regionLayer?.SetSelected(_selectedRegionId);
            _regionLayer?.SetStyle(_style);

            // After rebuild: map page fits, minimap centers on player (once viewport size is valid).
            if (_minimapFollowPlayer && TryGetPlayerLocal(out Vector2 playerLocal))
            {
                // Ensure we have a usable zoom for a minimap (otherwise you can end up "fit" and clamped).
                if (!TryGetViewportSize(out _, out _))
                    return; // Tick() will center once geometry is valid

                // Pick a stable minimap zoom floor so we actually have room to pan.
                _zoom = Mathf.Max(_zoom, 2.25f);

                CenterViewOnContentPoint(playerLocal);
            }
            else
            {
                ResetViewToFit();
            }

            // Keep debug overlay on top (child order defines draw order in most UI Toolkit versions)
            if (_debugHost != null && _content != null && _debugCompareProjections)
            {
                _debugHost.RemoveFromHierarchy();
                _content.Add(_debugHost);
            }

        }

        /// <summary>
        /// Enables/disables the dual-projection debug overlay.
        /// PROJ (white) = MapProjection mapping, CAM (magenta) = reference camera viewport mapping.
        /// </summary>
        public void SetDebugCompareProjections(bool enabled, bool logDeltas = true)
        {
            _debugCompareProjections = enabled;
            _debugLogProjectionDeltas = logDeltas;

            // Force a rebuild so the overlay reflects current data.
            _dirty = true;
        }

        private void ConfigureReferenceCamera()
        {
            if (_mapCamera == null) return;
            if (_mapData == null) return;

            // Reference only.
            _mapCamera.enabled = false;

            // Critical: match camera aspect to the baked background texture aspect,
            // so WorldToViewportPoint aligns with the PNG regardless of current screen aspect.
            var bg = _mapData.BackgroundTexture;
            if (bg != null && bg.width > 0 && bg.height > 0)
            {
                float texAspect = bg.width / (float)bg.height; // Camera.aspect is width/height
                if (Mathf.Abs(_mapCamera.aspect - texAspect) > 0.0001f)
                    _mapCamera.aspect = texAspect;
            }

            if (!_mapCamera.orthographic)
            {
                Debug.LogWarning(
                    $"[PhoneMapPageUI] Reference camera '{_mapCamera.name}' is not orthographic. " +
                    $"For a top-down map image, an orthographic camera is strongly recommended.");
            }
        }

        private void ApplyContentSizing()
        {
            if (_content == null) return;

            // Do NOT override anchors here; UXML/USS already defines correct absolute layout.
            // Only ensure explicit content sizing so pan/zoom math has a stable space.
            _content.style.width = _contentSize.x;
            _content.style.height = _contentSize.y;

            UpdateLayerSizes();
        }

        private void EnsureViewportHeightForScrollViewRoot()
        {
            if (_viewport == null) return;
            if (_root == null) return;

            float h = _root.resolvedStyle.height;
            if (h <= 1f) h = _root.layout.height;
            if (h <= 1f) return;

            float chromeBudget = 120f;

            // Reserve space only when the info panel is open.
            float reserve = GetInfoPanelReserveHeight();

            float target = Mathf.Max(220f, h - chromeBudget - reserve);

            _viewport.style.height = target;
            _viewport.style.marginBottom = reserve > 0f ? (reserve + 8f) : 0f;
        }

        private void SyncViewportToInfoPanelState()
        {
            bool isOpen = IsInfoPanelOpen();
            float reserve = GetInfoPanelReserveHeight();

            bool changed =
                (isOpen != _infoPanelOpenCached) ||
                (Mathf.Abs(reserve - _infoPanelReserveCached) > 0.5f);

            if (!changed) return;

            _infoPanelOpenCached = isOpen;
            _infoPanelReserveCached = reserve;

            EnsureViewportHeightForScrollViewRoot();

            if (isOpen && HasSelection())
                _pendingCenterOnSelection = true;
        }

        private bool HasSelection()
        {
            return !string.IsNullOrEmpty(_selectedMarkerId) || !string.IsNullOrEmpty(_selectedPolylineId);
        }

        private bool TryGetSelectedAnchorLocal(out Vector2 anchorLocal)
        {
            // Prefer marker anchor when available (user expectation: selected marker centers).
            anchorLocal = default;

            if (!string.IsNullOrEmpty(_selectedMarkerId) && _markerAnchorLocal.TryGetValue(_selectedMarkerId, out var m))
            {
                anchorLocal = m;
                return true;
            }

            if (!string.IsNullOrEmpty(_selectedPolylineId) && _polyAnchorLocal.TryGetValue(_selectedPolylineId, out var p))
            {
                anchorLocal = p;
                return true;
            }

            return false;
        }

        private void RequestCenterOnSelectionWhenInfoPanelOpen(float minZoom = -1f)
        {
            _pendingCenterOnSelection = true;
            _pendingSelectionMinZoom = minZoom;
        }

        private Vector3 ResolveEffectiveMarkerWorldPosition(SkiGame.Map.MapMarker marker)
        {
            Vector3 baked = marker.worldPosition;

            switch (marker.type)
            {
                case SkiGame.POI.POIType.SkiRun:
                    {
                        // For runs, prefer an anchor derived from the actual baked run polyline.
                        // This keeps the marker visually attached to the line the player sees on the map.
                        if (TryResolveRunAnchorFromPolyline(marker, out var runAnchor))
                            return runAnchor;

                        if (IsWorldMarkerPositionUsable(baked))
                            return baked;

                        return ResolveSourceAnchor(marker, baked);
                    }

                case SkiGame.POI.POIType.SkiLift:
                    {
                        // Lift station markers are usually already accurate from baked/world positions.
                        if (IsWorldMarkerPositionUsable(baked))
                            return baked;

                        if (TryResolveLiftAnchorFromPolyline(marker, out var liftAnchor))
                            return liftAnchor;

                        return ResolveSourceAnchor(marker, baked);
                    }

                default:
                    {
                        // General/custom POIs benefit most from smarter scene anchors.
                        Vector3 sourceAnchor = ResolveSourceAnchor(marker, baked);
                        if (IsWorldMarkerPositionUsable(sourceAnchor))
                            return sourceAnchor;

                        return baked;
                    }
            }
        }


        private bool TryProjectWorldToUV(Vector3 worldPos, out Vector2 uv)
        {
            // Use camera projection ONLY when the map asset explicitly says so.
            if (_mapCamera != null && _mapData != null && _mapData.PreferCameraProjection)
            {
                // IMPORTANT: use the real 3D world position.
                Vector3 vp = _mapCamera.WorldToViewportPoint(worldPos);

                if (vp.z >= 0f)
                {
                    Vector2 camUV = new Vector2(vp.x, vp.y);
                    const float tol = 0.05f;
                    if (camUV.x >= -tol && camUV.x <= 1f + tol && camUV.y >= -tol && camUV.y <= 1f + tol)
                    {
                        uv = camUV;
                        return true;
                    }
                }
            }

            // Default: baked projection (bounds-based).
            if (_mapData != null)
            {
                uv = _mapData.WorldToMapUV(worldPos);
                return true;
            }

            uv = default;
            return false;
        }

        private void UpdateLayerSizes()
        {
            SetLayerSize(_bg);
            SetLayerSize(_polyHost);

            // NOTE: Painter2D rendering requires the element itself to have a non-zero layout rect.
            // Some UI Toolkit versions will not reliably size a child that only uses right/bottom anchors,
            // so we explicitly size the custom painter layers as well.
            SetLayerSize(_regionLayer);
            SetLayerSize(_polyLayer);
            SetLayerSize(_trailLayer);

            SetLayerSize(_markerHost);
            SetLayerSize(_debugHost);
        }

        private void SetLayerSize(VisualElement ve)
        {
            if (ve == null) return;

            // Respect UXML/USS anchoring. We only guarantee the layer has explicit size.
            ve.style.position = Position.Absolute;
            ve.style.left = 0;
            ve.style.top = 0;
            ve.style.width = _contentSize.x;
            ve.style.height = _contentSize.y;
        }

        private bool TryGetPolylineById(string id, out SkiGame.Map.MapPolyline poly)
        {
            poly = default;
            if (_mapData == null || string.IsNullOrEmpty(id)) return false;

            var lines = _mapData.Polylines;
            if (lines == null) return false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].id == id)
                {
                    poly = lines[i];
                    return true;
                }
            }
            return false;
        }

        private static float DistSqXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private LiftStationRole DetermineLiftStationRole(SkiGame.Map.MapMarker m)
        {
            // If this marker ID directly matches a lift polyline ID, treat it as a "line label marker" (not a station).
            if (TryGetPolylineById(m.id, out var p) && p.lineType == SkiGame.Map.MapLineType.SkiLift)
                return LiftStationRole.None;

            // Prefer LiftLine station transforms when available.
            if (m.source is LiftLine ll && ll) // important: Unity "fake null" guard
            {

                Vector3 bot = ll.bottomStation ? ll.bottomStation.position : ll.transform.position;
                Vector3 top = ll.topStation ? ll.topStation.position : ll.transform.position;

                float db = DistSqXZ(m.worldPosition, bot);
                float dt = DistSqXZ(m.worldPosition, top);

                // within ~6m on XZ counts as a station marker
                const float tolSq = 36f;

                if (db <= tolSq && db <= dt) return LiftStationRole.Bottom;
                if (dt <= tolSq && dt < db) return LiftStationRole.Top;

                // If ambiguous but still nearer, assign anyway (helps authoring slightly off).
                if (db < dt) return LiftStationRole.Bottom;
                if (dt < db) return LiftStationRole.Top;
            }

            // Heuristic fallback: marker id/name contains station hints.
            string s = ((m.id ?? "") + " " + (m.displayName ?? "")).ToLowerInvariant();
            if (s.Contains("top") || s.Contains("upper")) return LiftStationRole.Top;
            if (s.Contains("bottom") || s.Contains("lower")) return LiftStationRole.Bottom;

            return LiftStationRole.None;
        }

        private bool TryGetLiftPolylineIdForMarker(SkiGame.Map.MapMarker m, out string polylineId)
        {
            polylineId = null;
            if (_mapData == null) return false;

            // If marker ID is itself a polyline ID, that's the link.
            if (TryGetPolylineById(m.id, out var byId) && byId.lineType == SkiGame.Map.MapLineType.SkiLift)
            {
                polylineId = byId.id;
                return true;
            }

            // Prefer LiftLine source object name as polyline id.
            if (m.source is LiftLine ll && ll) // important: Unity "fake null" guard
            {
                // Avoid ll.gameObject on destroyed objects; ll.name is enough and cheaper.
                string candidate = ll.name;

                if (TryGetPolylineById(candidate, out var p) && p.lineType == SkiGame.Map.MapLineType.SkiLift)
                {
                    polylineId = p.id;
                    return true;
                }

                // Also try matching by displayName.
                var lines = _mapData.Polylines;
                if (lines != null)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].lineType != SkiGame.Map.MapLineType.SkiLift) continue;
                        if (string.Equals(lines[i].displayName, candidate, System.StringComparison.OrdinalIgnoreCase))
                        {
                            polylineId = lines[i].id;
                            return true;
                        }
                    }
                }
            }

            // Fallback: prefix match (e.g. LiftA_Top, LiftA_Bottom)
            {
                var lines = _mapData.Polylines;
                if (lines != null)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].lineType != SkiGame.Map.MapLineType.SkiLift) continue;
                        string id = lines[i].id;
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(m.id) &&
                            (m.id.StartsWith(id + "_") || m.id.StartsWith(id + ":") || m.id.StartsWith(id + "-")))
                        {
                            polylineId = id;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void AddLiftMarkerLink(string polylineId, string markerId, LiftStationRole role)
        {
            if (string.IsNullOrEmpty(polylineId) || string.IsNullOrEmpty(markerId)) return;

            _liftMarkerToPolyline[markerId] = polylineId;

            if (!_liftPolylineToMarkers.TryGetValue(polylineId, out var list))
            {
                list = new List<string>(2);
                _liftPolylineToMarkers[polylineId] = list;
            }

            if (!list.Contains(markerId))
                list.Add(markerId);

            if (role != LiftStationRole.None)
                _liftMarkerRole[markerId] = role;
        }

        private int GetLiftRequiredPassLevel(SkiGame.Map.MapMarker m)
        {
            // Best case: runtime LiftLine reference exists.
            if (m.source is LiftLine ll && ll)
            {
                // Try common field/property names via reflection so we don't hard-bind to a specific implementation.
                // (Keeps this resilient if your LiftLine changes.)
                var t = ll.GetType();

                // Properties
                string[] propNames =
                {
            "RequiredPassLevel", "requiredPassLevel",
            "RequiredSkiPassLevel", "requiredSkiPassLevel",
            "PassLevelRequired", "passLevelRequired",
            "MinPassLevel", "minPassLevel",
            "RequiredLevel", "requiredLevel",
        };

                for (int i = 0; i < propNames.Length; i++)
                {
                    var p = t.GetProperty(propNames[i]);
                    if (p != null && p.PropertyType == typeof(int) && p.CanRead)
                    {
                        try { return Mathf.Max(0, (int)p.GetValue(ll)); }
                        catch { /* ignore */ }
                    }
                }

                // Fields
                string[] fieldNames =
                {
            "requiredPassLevel", "RequiredPassLevel",
            "requiredSkiPassLevel", "RequiredSkiPassLevel",
            "passLevelRequired", "PassLevelRequired",
            "minPassLevel", "MinPassLevel",
            "requiredLevel", "RequiredLevel",
        };

                for (int i = 0; i < fieldNames.Length; i++)
                {
                    var f = t.GetField(fieldNames[i]);
                    if (f != null && f.FieldType == typeof(int))
                    {
                        try { return Mathf.Max(0, (int)f.GetValue(ll)); }
                        catch { /* ignore */ }
                    }
                }
            }

            // Fallback: parse marker meta / name for something like "L2", "Level 2", "Pass:2", etc.
            // (Only used if you don't have LiftLine source objects wired in MapData.)
            string s = ((m.meta ?? "") + " " + (m.displayName ?? "") + " " + (m.id ?? "")).ToLowerInvariant();

            // Common patterns
            // "level 2", "pass 2", "pass:2"
            for (int lvl = 0; lvl <= 10; lvl++)
            {
                if (s.Contains($"level {lvl}") || s.Contains($"pass {lvl}") || s.Contains($"pass:{lvl}") || s.Contains($"l{lvl}"))
                    return lvl;
            }

            return 0;
        }

        private Color GetPassLevelMapColor(int level)
        {
            level = Mathf.Max(0, level);

            // Prefer your SkiPassConfigSO tier colour.
            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;
            if (cfg != null)
            {
                var p = cfg.Get(level);
                if (p != null)
                {
                    var col = p.mapColor;
                    // If someone left it as default white, that's still a valid choice.
                    return col;
                }
            }

            // Fallback palette if config is missing / not set.
            // (0=grey, 1=green, 2=blue, 3=purple, 4=gold, 5=red)
            Color[] fallback =
            {
        new Color(0.80f, 0.80f, 0.80f, 1f),
        new Color(0.35f, 0.95f, 0.55f, 1f),
        new Color(0.35f, 0.65f, 1.00f, 1f),
        new Color(0.75f, 0.45f, 1.00f, 1f),
        new Color(1.00f, 0.85f, 0.35f, 1f),
        new Color(1.00f, 0.35f, 0.35f, 1f),
    };

            return fallback[Mathf.Clamp(level, 0, fallback.Length - 1)];
        }

        private void CacheLiftRequirementColour(string liftPolylineId, int requiredLevel)
        {
            requiredLevel = Mathf.Max(0, requiredLevel);

            // If multiple stations report something, keep the max (most restrictive).
            if (_liftRequiredLevelByPolyline.TryGetValue(liftPolylineId, out var existing))
                requiredLevel = Mathf.Max(existing, requiredLevel);

            _liftRequiredLevelByPolyline[liftPolylineId] = requiredLevel;

            Color c = GetPassLevelMapColor(requiredLevel);
            _liftRequiredColorByPolyline[liftPolylineId] = c;

            // Apply to polyline overrides with your usual alpha feel.
            _polylineColorOverrides[liftPolylineId] = new Color(c.r, c.g, c.b, 0.70f);

            // Also cache accent colour for polyline labels.
            _polylineLabelAccent[liftPolylineId] = new Color(c.r, c.g, c.b, 1f);
        }

        private void RebuildMarkers()
        {
            // Preserve the persistent player marker while rebuilding POI markers.
            if (_playerMarker != null)
                _playerMarker.RemoveFromHierarchy();

            EnsureLabelOverlay();

            // Remove old overlay labels BEFORE clearing the dictionaries that track them.
            // Otherwise stale labels survive rebuilds and can pile up in viewport corners.
            foreach (var kv in _polylineLabelVisuals)
                kv.Value?.RemoveFromHierarchy();

            foreach (var kv in _poiOverlayLabels)
                kv.Value?.RemoveFromHierarchy();

            _polylineLabelVisuals.Clear();
            _poiOverlayLabels.Clear();

            _markerHost.Clear();

            _markerVisuals.Clear();

            _markerRoots.Clear();
            _markerTypes.Clear();

            // Clear link maps each rebuild (prevents stale selection links)
            _markerToPolyline.Clear();
            _polylineToMarker.Clear();

            _liftMarkerToPolyline.Clear();
            _liftPolylineToMarkers.Clear();
            _liftMarkerRole.Clear();

            _markerLabelAccent.Clear();
            _polylineLabelAccent.Clear();

            _polylineColorOverrides.Clear();

            _liftRequiredLevelByPolyline.Clear();
            _liftRequiredColorByPolyline.Clear();

            _markerHitTargets.Clear();
            _markerDisplayNames.Clear();
            _poiAlwaysLabelById.Clear();

            _markerAnchorLocal.Clear();

            _markerById.Clear();

            _polyAnchorLocal.Clear();

            _waypointMarkerRoots.Clear();
            _waypointMarkerDots.Clear();
            _waypointLabels.Clear();
            _waypointAnchorLocal.Clear();

            _polyNormalLocal.Clear();
            _labelSlotCache.Clear();

            if (_playerMarker != null)
                _markerHost.Add(_playerMarker);

            // Debug overlay cleanup
            if (_debugHost != null)
                _debugHost.Clear();

            var list = _mapData.Markers;
            if (list == null) return;

            // Prepass: build run/lift marker linkage so selection/label logic can be cohesive.
            var polylines = _mapData.Polylines;

            // Runs: marker id == polyline id
            if (polylines != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (!m.IsValid) continue;

                    _markerById[m.id] = m;

                    // Run markers usually have ids like "{runId}__top", while the polyline id is "{runId}".
                    // Link them explicitly so labels can anchor beside the visible run marker.
                    if (m.type == SkiGame.POI.POIType.SkiRun)
                    {
                        string runPolyId = null;

                        if (!string.IsNullOrWhiteSpace(m.id))
                        {
                            int idx = m.id.LastIndexOf("__", StringComparison.Ordinal);
                            runPolyId = idx > 0 ? m.id.Substring(0, idx) : m.id;
                        }

                        if (!string.IsNullOrWhiteSpace(runPolyId) &&
                            TryGetPolylineById(runPolyId, out var p) &&
                            p.lineType == SkiGame.Map.MapLineType.SkiRun)
                        {
                            _markerToPolyline[m.id] = p.id;

                            // Prefer the first linked run marker we encounter for label anchoring.
                            if (!_polylineToMarker.ContainsKey(p.id))
                                _polylineToMarker[p.id] = m.id;
                        }
                    }

                    // Lift station markers: link to lift polyline via LiftLine or heuristics.
                    else if (m.type == SkiGame.POI.POIType.SkiLift)
                    {
                        if (TryGetLiftPolylineIdForMarker(m, out string liftPolyId))
                        {
                            var role = DetermineLiftStationRole(m);
                            AddLiftMarkerLink(liftPolyId, m.id, role);

                            // Pass requirement colouring
                            int req = GetLiftRequiredPassLevel(m);
                            CacheLiftRequirementColour(liftPolyId, req);
                        }
                    }

                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.IsValid) continue;

                Vector3 markerWorldPos = ResolveEffectiveMarkerWorldPosition(m);

                // Compute both UV solutions for validation:
                // - PROJ: MapProjection (bounds-based)
                // - CAM:  reference camera viewport mapping (if available)
                Vector2 uvProj = (_mapData != null) ? _mapData.WorldToMapUV(markerWorldPos) : default;

                bool camValid = false;
                Vector2 uvCam = default;

                if (_mapCamera != null)
                {
                    Vector3 vp = _mapCamera.WorldToViewportPoint(markerWorldPos);

                    // vp.z < 0 means behind camera
                    if (vp.z >= 0f)
                    {
                        uvCam = new Vector2(vp.x, vp.y);
                        camValid = true;
                    }
                }

                // Your actual placement UV remains whatever TryProjectWorldToUV decides (based on PreferCameraProjection).
                if (!TryProjectWorldToUV(markerWorldPos, out Vector2 uv))
                    continue;

                Vector2 uvRaw = uv;
                Vector2 uvInset = ApplyBackgroundInset(uvRaw);
                Vector2 localRaw = new Vector2(uvInset.x * _contentSize.x, (1f - uvInset.y) * _contentSize.y);

                //Debug.Log($"MAP MARKER '{m.displayName}' world={m.worldPosition} uvRaw={uvRaw} uvInset={uvInset} localRaw={localRaw} bgMin={_mapData.BackgroundUvMin} bgMax={_mapData.BackgroundUvMax}");

                Vector2 local = UVToLocal(uv);

                bool isRunOrLiftLabel =
                    m.type == SkiGame.POI.POIType.SkiRun ||
                    m.type == SkiGame.POI.POIType.SkiLift;

                // For lifts: we only want station markers on the map (top/bottom).
                // Any SkiLift marker that is NOT a station marker is treated as a legacy "lift label marker" and should not be drawn.
                bool isLiftStationMarker =
                    m.type == SkiGame.POI.POIType.SkiLift &&
                    (m.id.EndsWith("__top", StringComparison.Ordinal) || m.id.EndsWith("__bottom", StringComparison.Ordinal));

                bool isLiftLineMarker = (m.type == SkiGame.POI.POIType.SkiLift) && !isLiftStationMarker;
                if (isLiftLineMarker)
                {
                    // Skip drawing this marker entirely (we use the lift polyline label instead).
                    continue;
                }

                if (_debugCompareProjections && _debugHost != null)
                {
                    // Convert both UVs to local (including inset + Y flip)
                    Vector2 localProj = UVToLocal(uvProj);
                    Vector2 localCam = camValid ? UVToLocal(uvCam) : localProj;

                    // Dot size in UI units (scales with zoom because it's inside _content)
                    const float dotSize = 8f;

                    // Draw PROJ dot (white)
                    var projDot = new VisualElement();
                    projDot.style.position = Position.Absolute;
                    projDot.style.width = dotSize;
                    projDot.style.height = dotSize;
                    projDot.style.left = localProj.x - dotSize * 0.5f;
                    projDot.style.top = localProj.y - dotSize * 0.5f;
                    projDot.style.borderTopLeftRadius = dotSize;
                    projDot.style.borderTopRightRadius = dotSize;
                    projDot.style.borderBottomLeftRadius = dotSize;
                    projDot.style.borderBottomRightRadius = dotSize;
                    projDot.style.backgroundColor = DebugProjColor;

                    _debugHost.Add(projDot);

                    // Draw CAM dot (magenta) if camera is available, otherwise draw a warning dot
                    var camDot = new VisualElement();
                    camDot.style.position = Position.Absolute;
                    camDot.style.width = dotSize;
                    camDot.style.height = dotSize;
                    camDot.style.left = localCam.x - dotSize * 0.5f;
                    camDot.style.top = localCam.y - dotSize * 0.5f;
                    camDot.style.borderTopLeftRadius = dotSize;
                    camDot.style.borderTopRightRadius = dotSize;
                    camDot.style.borderBottomLeftRadius = dotSize;
                    camDot.style.borderBottomRightRadius = dotSize;
                    camDot.style.backgroundColor = camValid ? DebugCamColor : DebugWarnColor;

                    _debugHost.Add(camDot);

                    if (_debugLogProjectionDeltas && camValid)
                    {
                        Vector2 duv = uvCam - uvProj;
                        float duvMag = duv.magnitude;

                        // Only log meaningful differences so the console stays usable.
                        if (duvMag > 0.0025f) // ~0.25% of map span
                        {
                            Debug.Log(
                                $"MAP UV DELTA '{m.displayName}' " +
                                $"uvProj={uvProj:F3} uvCam={uvCam:F3} duv={duv:F3} |duv|={duvMag:F4} " +
                                $"world={m.worldPosition}"
                            );
                        }
                    }
                }

                var marker = new VisualElement();
                marker.AddToClassList("map-marker");
                marker.style.position = Position.Absolute;

                _markerRoots[m.id] = marker;
                _markerTypes[m.id] = m.type;

                // Apply current legend visibility immediately
                marker.style.display = IsMarkerVisibleById(m.id) ? DisplayStyle.Flex : DisplayStyle.None;

                // Bigger invisible hit target so hover/click isn’t finicky (marker root is 0x0 pivot).
                var hit = new VisualElement();
                hit.AddToClassList("map-marker-hit");
                hit.style.position = Position.Absolute;
                const float hitSize = 20f;
                hit.style.width = hitSize;
                hit.style.height = hitSize;

                // Center hit zone around the marker pivot (0,0)
                hit.style.left = -hitSize * 0.5f;
                hit.style.top = -hitSize * 0.5f;

                hit.style.backgroundColor = new Color(0, 0, 0, 0); // invisible but still pickable
                hit.pickingMode = PickingMode.Ignore;

                _markerHitTargets[m.id] = hit;
                marker.Add(hit);

                var dot = new VisualElement();
                dot.AddToClassList("map-marker-dot");

                Color c = GetFallbackCategoryColor(m);

                if (_style != null && _style.usePOIRegistryMarkerColors && _poiRegistry != null && _poiRegistry.TryGetById(m.id, out var info))
                    c = info.color;

                // Lift station markers: colour should reflect the pass tier required by the lift.
                if (isLiftStationMarker)
                {
                    int idx = m.id.LastIndexOf("__", StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        string liftId = m.id.Substring(0, idx);

                        // If we cached a required colour during the prepass, use it.
                        if (_liftRequiredColorByPolyline.TryGetValue(liftId, out var passCol))
                        {
                            c = passCol;

                            // Ensure the lift polyline matches too.
                            _polylineColorOverrides[liftId] = new Color(c.r, c.g, c.b, 0.70f);
                            _polylineLabelAccent[liftId] = new Color(c.r, c.g, c.b, 1f);
                        }
                    }
                }

                dot.style.backgroundColor = c;

                // Cache accent colour for this marker's label (used for the left stripe).
                _markerLabelAccent[m.id] = new Color(c.r, c.g, c.b, 1f);

                // Lift station markers: overlay ^ or v (use resolved role from prepass)
                if (_liftMarkerRole.TryGetValue(m.id, out var role) && role != LiftStationRole.None)
                {
                    dot.style.alignItems = Align.Center;
                    dot.style.justifyContent = Justify.Center;

                    var glyphLabel = new Label(role == LiftStationRole.Top ? "↑" : "↓");
                    glyphLabel.pickingMode = PickingMode.Ignore;
                    glyphLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    glyphLabel.style.color = Color.white;
                    glyphLabel.style.fontSize = 12;
                    glyphLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                    dot.Add(glyphLabel);
                }

                var labelText = ResolveMarkerDisplayName(m);

                // Cache explicit POI “always label” flag
                if (m.type != POIType.SkiRun && m.type != POIType.SkiLift)
                {
                    bool always = false;

                    // Prefer explicit registry flag (editor-visible)
                    if (_poiRegistry != null && _poiRegistry.TryGetById(m.id, out var poiInfo))
                        always = poiInfo.alwaysShowLabel;

                    // Fallback to meta token if you still want it (optional)
                    if (!always)
                        always = HasAlwaysLabelFlag(m.meta);

                    _poiAlwaysLabelById[m.id] = always;
                }
                else
                {
                    _poiAlwaysLabelById[m.id] = false;
                }

                var label = new Label(labelText);
                label.AddToClassList("map-marker-label");
                label.style.display = DisplayStyle.None; // POI labels are now overlay labels only

                _markerDisplayNames[m.id] = labelText;

                bool isRun = (m.type == SkiGame.POI.POIType.SkiRun);
                bool isLift = (m.type == SkiGame.POI.POIType.SkiLift);

                // Position labels in SCREEN px below the marker (stable across zoom).
                float z = Mathf.Max(0.0001f, _zoom);
                label.style.left = 0f;
                label.style.top = (isRun ? RunLabelOffsetPx : PoiLabelOffsetPx) / z;
                // We now render labels in the viewport overlay for stability.
                // Keep the old label element around (minimal churn), but never show it.
                label.style.display = DisplayStyle.None;

                // Visibility rules (simplified):
                // - Runs/Lifts: NEVER show marker labels (their labels live on polylines)
                // - POIs: hover/selected, plus explicit always-flag
                if (_hideMarkerLabels)
                {
                    label.style.display = DisplayStyle.None;
                }
                else if (isLift || isRun)
                {
                    label.style.display = DisplayStyle.None;
                }
                else
                {
                    // POI labels are now handled by the overlay "cling to sides" system.
                    // Never show the marker-attached label (prevents duplicates).
                    label.style.display = DisplayStyle.None;
                }

                // Global minimap suppression still applies
                if (_hideMarkerLabels)
                    label.style.display = DisplayStyle.None;

                marker.Add(dot);
                marker.Add(label);

                _markerVisuals[m.id] = (dot, label);
                ApplyMarkerVisual(m.id, selected: _selectedMarkerId == m.id);

                // Anchor at the projected coordinate (this element becomes a 0,0 "pivot")
                marker.style.left = local.x;
                marker.style.top = local.y;
                _markerAnchorLocal[m.id] = local;

                // Make the marker itself a pivot container; children are positioned relative to (0,0)
                marker.style.width = 0;
                marker.style.height = 0;

                // Ensure absolute positioning for children so label size never shifts the dot's anchor
                dot.style.position = Position.Absolute;
                dot.style.left = 0;
                dot.style.top = 0;

                // Center the dot on the pivot once we know its size
                dot.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float z = Mathf.Max(0.0001f, _zoom);
                    float inv = 1f / z;

                    float w = dot.resolvedStyle.width > 0 ? dot.resolvedStyle.width : dot.layout.width;
                    float h = dot.resolvedStyle.height > 0 ? dot.resolvedStyle.height : dot.layout.height;

                    dot.style.scale = new Scale(new Vector3(inv, inv, 1f));
                    dot.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                });

                // Label is positioned by our solver (marker-local offsets). We only center it on its anchor.
                label.style.position = Position.Absolute;
                // NOTE: do NOT reset left/top here — the placement solver above computed an attached offset
                // (relative to the marker pivot). Resetting would snap the label onto the marker and ruin
                // disambiguation/overlap avoidance.

                label.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float z = Mathf.Max(0.0001f, _zoom);
                    float inv = 1f / z;

                    float w = label.resolvedStyle.width > 0 ? label.resolvedStyle.width : label.layout.width;
                    float h = label.resolvedStyle.height > 0 ? label.resolvedStyle.height : label.layout.height;

                    label.style.scale = new Scale(new Vector3(inv, inv, 1f));
                    label.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                });

                // Marker visuals must stay purely visual.
                // All marker interaction is handled centrally in OnPointerUp(...)
                // so single-click / double-click / right-double-click all share one authoritative path.
                marker.pickingMode = PickingMode.Ignore;
                dot.pickingMode = PickingMode.Ignore;
                label.pickingMode = PickingMode.Ignore;

                _markerHost.Add(marker);

            }

            if (_playerMarker != null)
            {
                _markerHost.Add(_playerMarker);
                _playerMarker.style.display = (_trackPlayerMarker) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RebuildLiftPolylineLabels();   // will now only create overlay poly labels + anchor caches
            RebuildPOIOverlayLabels();     // new
            LayoutOverlayLabels();         // new

            RebuildWaypointMarkers();
            ApplyLinkedSourceWaypointVisuals();

            _polyLayer?.SetColorOverrides(_polylineColorOverrides);

        }

        private void RebuildWaypointMarkers()
        {
            if (_markerHost == null || _waypointManager == null)
                return;

            var waypoints = _waypointManager.Waypoints;
            if (waypoints == null || waypoints.Count == 0)
                return;

            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];

                if (!TryProjectWorldToLocal(wp.worldPosition, out Vector2 local))
                    continue;

                bool linkedToExistingMarker =
                    !string.IsNullOrWhiteSpace(wp.sourceKey) &&
                    wp.sourceKey.StartsWith("marker:", StringComparison.Ordinal);

                // Waypoints linked to existing markers should not spawn their own
                // standalone waypoint visual. The existing marker itself is styled
                // via ApplyLinkedSourceWaypointVisuals().
                if (linkedToExistingMarker)
                    continue;

                var root = new VisualElement();
                root.name = $"WaypointMarker_{wp.id}";
                root.style.position = Position.Absolute;
                root.style.left = local.x;
                root.style.top = local.y;
                root.style.width = 0f;
                root.style.height = 0f;
                root.pickingMode = PickingMode.Ignore;

                var dot = new VisualElement();
                dot.AddToClassList("map-waypoint-marker");
                dot.style.position = Position.Absolute;
                dot.style.backgroundColor = wp.color;
                dot.pickingMode = PickingMode.Position;

                bool selected = _waypointManager.IsWaypointSelected(wp.id);
                bool active = string.Equals(_waypointManager.ActiveWaypointId, wp.id, StringComparison.Ordinal);

                var label = new Label(wp.displayName);
                label.AddToClassList("map-waypoint-label");
                label.style.position = Position.Absolute;

                float invZoom = 1f / Mathf.Max(0.0001f, _zoom);
                float waypointFontSizeScreen = Mathf.Max(9f, ComputeZoomedLabelFontSize(false) - 2f);
                float waypointFontSizeLocal = waypointFontSizeScreen * invZoom;

                label.style.width = 92f * invZoom;
                label.style.minWidth = 92f * invZoom;
                label.style.left = -46f * invZoom;
                label.style.top = -30f * invZoom;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.fontSize = waypointFontSizeLocal;
                label.style.paddingLeft = 4f * invZoom;
                label.style.paddingRight = 4f * invZoom;
                label.style.paddingTop = 1.5f * invZoom;
                label.style.paddingBottom = 1.5f * invZoom;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
                label.style.color = wp.color;
                label.style.borderTopLeftRadius = 5f * invZoom;
                label.style.borderTopRightRadius = 5f * invZoom;
                label.style.borderBottomLeftRadius = 5f * invZoom;
                label.style.borderBottomRightRadius = 5f * invZoom;
                label.pickingMode = PickingMode.Position;

                bool labelEditable = string.IsNullOrWhiteSpace(wp.sourceKey);

                void OpenRenameEditor()
                {
                    if (!labelEditable)
                    {
                        WaypointClicked?.Invoke(wp.id);
                        return;
                    }

                    TextField existingEditor = root.Q<TextField>("WaypointRenameField");
                    if (existingEditor != null)
                    {
                        existingEditor.Focus();
                        existingEditor.SelectAll();
                        return;
                    }

                    var textField = new TextField
                    {
                        name = "WaypointRenameField",
                        value = wp.displayName
                    };

                    textField.style.position = Position.Absolute;
                    textField.style.width = 110f * invZoom;
                    textField.style.minWidth = 110f * invZoom;
                    textField.style.left = -55f * invZoom;
                    textField.style.top = -30f * invZoom;
                    textField.style.fontSize = waypointFontSizeLocal;
                    textField.style.paddingLeft = 5f * invZoom;
                    textField.style.paddingRight = 5f * invZoom;
                    textField.style.backgroundColor = new Color(0f, 0f, 0f, 0.90f);
                    textField.style.color = Color.white;
                    textField.style.unityTextAlign = TextAnchor.MiddleLeft;

                    void CloseEditor(bool commit)
                    {
                        if (commit)
                            _waypointManager?.RenameWaypoint(wp.id, textField.value);

                        if (textField.parent != null)
                            root.Remove(textField);
                    }

                    textField.RegisterCallback<KeyDownEvent>(keyEvt =>
                    {
                        if (keyEvt.keyCode == KeyCode.Return || keyEvt.keyCode == KeyCode.KeypadEnter)
                        {
                            CloseEditor(commit: true);
                            keyEvt.StopPropagation();
                        }
                        else if (keyEvt.keyCode == KeyCode.Escape)
                        {
                            CloseEditor(commit: false);
                            keyEvt.StopPropagation();
                        }
                    });

                    textField.RegisterCallback<FocusOutEvent>(_ => CloseEditor(commit: true));

                    root.Add(textField);
                    textField.Focus();
                    textField.SelectAll();
                }

                void HandleWaypointPointer(PointerUpEvent evt, bool fromLabel)
                {
                    float now = Time.unscaledTime;

                    if (evt.button == 0)
                    {
                        bool isDouble =
                            string.Equals(_lastWaypointVisualLeftClickedId, wp.id, StringComparison.Ordinal) &&
                            (now - _lastWaypointVisualLeftClickTime) <= WaypointDoubleClickWindowSeconds;

                        if (isDouble)
                        {
                            _lastWaypointVisualLeftClickedId = null;
                            _lastWaypointVisualLeftClickTime = -10f;

                            if (fromLabel && labelEditable)
                                OpenRenameEditor();
                            else
                                WaypointDoubleClicked?.Invoke(wp.id);
                        }
                        else
                        {
                            _lastWaypointVisualLeftClickedId = wp.id;
                            _lastWaypointVisualLeftClickTime = now;
                            WaypointClicked?.Invoke(wp.id);
                        }

                        evt.StopImmediatePropagation();
                    }
                    else if (evt.button == 1)
                    {
                        bool isDouble =
                            string.Equals(_lastWaypointVisualRightClickedId, wp.id, StringComparison.Ordinal) &&
                            (now - _lastWaypointVisualRightClickTime) <= MarkerRightDoubleClickWindowSeconds;

                        if (isDouble)
                        {
                            _lastWaypointVisualRightClickedId = null;
                            _lastWaypointVisualRightClickTime = -10f;
                            WaypointDeleteRequested?.Invoke(wp.id);
                        }
                        else
                        {
                            _lastWaypointVisualRightClickedId = wp.id;
                            _lastWaypointVisualRightClickTime = now;
                        }

                        evt.StopImmediatePropagation();
                    }
                }

                dot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0 || evt.button == 1)
                        evt.StopImmediatePropagation();
                });

                dot.RegisterCallback<PointerUpEvent>(evt => HandleWaypointPointer(evt, fromLabel: false));

                label.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0 || evt.button == 1)
                        evt.StopImmediatePropagation();
                });

                label.RegisterCallback<PointerUpEvent>(evt => HandleWaypointPointer(evt, fromLabel: true));


                root.Add(dot);
                root.Add(label);
                _markerHost.Add(root);

                _waypointMarkerRoots[wp.id] = root;
                _waypointMarkerDots[wp.id] = dot;
                _waypointLabels[wp.id] = label;
                _waypointAnchorLocal[wp.id] = local;

                ApplyWaypointVisual(wp.id, selected, active);
            }
        }

        private void RefreshWaypointVisuals()
        {
            // Remove only standalone waypoint visuals.
            foreach (var kv in _waypointMarkerRoots)
                kv.Value?.RemoveFromHierarchy();

            _waypointMarkerRoots.Clear();
            _waypointMarkerDots.Clear();
            _waypointLabels.Clear();
            _waypointAnchorLocal.Clear();

            RebuildWaypointMarkers();
            ApplyLinkedSourceWaypointVisuals();

            // Re-apply current selection styling and re-run overlay placement
            // without tearing down all POI/polyline labels.
            UpdateSelectionVisuals();
        }

        private void ApplyLinkedSourceWaypointVisuals()
        {
            if (_waypointManager == null)
                return;

            foreach (var kv in _markerRoots)
            {
                string markerId = kv.Key;
                VisualElement root = kv.Value;
                if (root == null)
                    continue;

                string sourceKey = $"marker:{markerId}";
                VisualElement underlay = root.Q<VisualElement>("LinkedWaypointUnderlay");

                if (_waypointManager.TryGetWaypointBySourceKey(sourceKey, out var linkedWp))
                {
                    if (underlay == null)
                    {
                        underlay = new VisualElement();
                        underlay.name = "LinkedWaypointUnderlay";
                        underlay.style.position = Position.Absolute;
                        underlay.style.width = 24f;
                        underlay.style.height = 24f;
                        underlay.style.borderTopLeftRadius = 999f;
                        underlay.style.borderTopRightRadius = 999f;
                        underlay.style.borderBottomLeftRadius = 999f;
                        underlay.style.borderBottomRightRadius = 999f;
                        underlay.style.borderLeftWidth = 2f;
                        underlay.style.borderRightWidth = 2f;
                        underlay.style.borderTopWidth = 2f;
                        underlay.style.borderBottomWidth = 2f;
                        underlay.style.borderLeftColor = new Color(0f, 0f, 0f, 0.55f);
                        underlay.style.borderRightColor = new Color(0f, 0f, 0f, 0.55f);
                        underlay.style.borderTopColor = new Color(0f, 0f, 0f, 0.55f);
                        underlay.style.borderBottomColor = new Color(0f, 0f, 0f, 0.55f);
                        underlay.pickingMode = PickingMode.Ignore;

                        underlay.RegisterCallback<GeometryChangedEvent>(_ =>
                        {
                            float z = Mathf.Max(0.0001f, _zoom);
                            float inv = 1f / z;

                            float w = underlay.resolvedStyle.width > 0 ? underlay.resolvedStyle.width : underlay.layout.width;
                            float h = underlay.resolvedStyle.height > 0 ? underlay.resolvedStyle.height : underlay.layout.height;

                            underlay.style.scale = new Scale(new Vector3(inv, inv, 1f));
                            underlay.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0f);
                        });

                        root.Insert(0, underlay);
                    }

                    underlay.style.display = DisplayStyle.Flex;
                    underlay.style.backgroundColor = linkedWp.color;
                }
                else
                {
                    if (underlay != null)
                        underlay.style.display = DisplayStyle.None;
                }

                if (_poiOverlayLabels.ContainsKey(markerId))
                    ApplyPOIOverlayLabelVisual(markerId, string.Equals(_selectedMarkerId, markerId, StringComparison.Ordinal));
            }
        }

        private bool TryProjectWorldToLocal(Vector3 worldPos, out Vector2 local)
        {
            local = default;

            if (!TryProjectWorldToUV(worldPos, out var uv))
                return false;

            Vector2 uvInset = ApplyBackgroundInset(uv);
            local = new Vector2(uvInset.x * _contentSize.x, (1f - uvInset.y) * _contentSize.y);
            return true;
        }

        private bool TryProjectWorldXZToLocal(Vector2 worldXZ, out Vector2 local)
        {
            local = default;
            if (_mapData == null) return false;

            // Fallback projection for legacy XZ-only data.
            Vector2 uv = _mapData.WorldXZToMapUV(worldXZ);
            Vector2 uvInset = ApplyBackgroundInset(uv);
            local = new Vector2(uvInset.x * _contentSize.x, (1f - uvInset.y) * _contentSize.y);
            return true;
        }

        private static string GetLiftPolylineIdFromStationMarkerId(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return null;

            // Registry emits: "{entry.id}__top" / "{entry.id}__bottom"
            int idx = markerId.LastIndexOf("__", StringComparison.Ordinal);
            if (idx <= 0) return null;

            return markerId.Substring(0, idx);
        }

        private bool IsLiftSelected(string liftPolylineId)
        {
            // Selection is lift-wide if the selected polyline is the lift line,
            // OR if either station marker is selected (shares base id).
            if (string.IsNullOrEmpty(liftPolylineId)) return false;

            if (string.Equals(_selectedPolylineId, liftPolylineId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrEmpty(_selectedMarkerId))
            {
                string selectedBase = GetLiftPolylineIdFromStationMarkerId(_selectedMarkerId);
                if (!string.IsNullOrEmpty(selectedBase) && string.Equals(selectedBase, liftPolylineId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool IsPolylineSelected(MapPolyline p)
        {
            if (!p.IsValid) return false;

            if (string.Equals(_selectedPolylineId, p.id, StringComparison.Ordinal))
                return true;

            if (p.lineType == MapLineType.SkiLift)
                return IsLiftSelected(p.id);

            // Run selection: marker + polyline are linked.
            if (p.lineType == MapLineType.SkiRun && _polylineToMarker.TryGetValue(p.id, out var markerId))
                return string.Equals(_selectedMarkerId, markerId, StringComparison.Ordinal);

            return false;
        }

        private void UpdateAllLabelTransformsForZoom()
        {
            float z = Mathf.Max(0.0001f, _zoom);
            float inv = 1f / z;

            // Markers: dot + label should be screen-locked (inverse-scaled).
            foreach (var kv in _markerVisuals)
            {
                var dot = kv.Value.dot;
                var lab = kv.Value.label;

                if (dot != null)
                {
                    dot.style.scale = new Scale(new Vector3(inv, inv, 1f));

                    float w = dot.resolvedStyle.width > 0 ? dot.resolvedStyle.width : dot.layout.width;
                    float h = dot.resolvedStyle.height > 0 ? dot.resolvedStyle.height : dot.layout.height;
                    if (w > 0f && h > 0f)
                    {
                        // IMPORTANT: translate is in pre-scale space; do NOT multiply by inv.
                        dot.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                    }
                }

                if (lab != null)
                {
                    lab.style.scale = new Scale(new Vector3(inv, inv, 1f));

                    float w = lab.resolvedStyle.width > 0 ? lab.resolvedStyle.width : lab.layout.width;
                    float h = lab.resolvedStyle.height > 0 ? lab.resolvedStyle.height : lab.layout.height;
                    if (w > 0f && h > 0f)
                    {
                        // IMPORTANT: translate is in pre-scale space; do NOT multiply by inv.
                        lab.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                    }
                }
            }

            // Standalone waypoint markers: dot + label should be screen-locked too.
            foreach (var kv in _waypointMarkerDots)
            {
                var dot = kv.Value;
                if (dot != null)
                {
                    dot.style.scale = new Scale(new Vector3(inv, inv, 1f));

                    float w = dot.resolvedStyle.width > 0 ? dot.resolvedStyle.width : dot.layout.width;
                    float h = dot.resolvedStyle.height > 0 ? dot.resolvedStyle.height : dot.layout.height;
                    if (w > 0f && h > 0f)
                        dot.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                }
            }

            foreach (var kv in _waypointLabels)
            {
                var lab = kv.Value;
                if (lab != null)
                {
                    lab.style.scale = new Scale(new Vector3(inv, inv, 1f));

                    float w = lab.resolvedStyle.width > 0 ? lab.resolvedStyle.width : lab.layout.width;
                    float h = lab.resolvedStyle.height > 0 ? lab.resolvedStyle.height : lab.layout.height;
                    if (w > 0f && h > 0f)
                        lab.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                }
            }

            // Polyline labels: screen-locked, but allow controlled growth when zooming in.
            float comp = (_style != null) ? Mathf.Clamp01(_style.labelZoomCompensation) : 1f;
            float invPoly = 1f / Mathf.Pow(z, Mathf.Max(0.0001f, comp));

            foreach (var kv in _polylineLabelVisuals)
            {
                var lab = kv.Value;
                if (lab == null) continue;

                lab.style.scale = new Scale(new Vector3(invPoly, invPoly, 1f));

                float w = lab.resolvedStyle.width > 0 ? lab.resolvedStyle.width : lab.layout.width;
                float h = lab.resolvedStyle.height > 0 ? lab.resolvedStyle.height : lab.layout.height;
                if (w > 0f && h > 0f)
                {
                    lab.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                }
            }

            // Player marker (screen-locked)
            if (_playerMarker != null)
            {
                _playerMarker.style.scale = new Scale(new Vector3(inv, inv, 1f));

                float w = _playerMarker.resolvedStyle.width > 0 ? _playerMarker.resolvedStyle.width : _playerMarker.layout.width;
                float h = _playerMarker.resolvedStyle.height > 0 ? _playerMarker.resolvedStyle.height : _playerMarker.layout.height;
                if (w > 0f && h > 0f)
                {
                    // IMPORTANT: translate is in pre-scale space; do NOT multiply by inv.
                    _playerMarker.style.translate = new Translate(-w * 0.5f, -h * 0.5f, 0);
                }
            }
        }

        private void ResolveInfoPanelRefs()
        {
            if (_root == null) return;

            // Search from the top of the UI tree, not the map page.
            VisualElement searchRoot = _root.panel?.visualTree;
            if (searchRoot == null)
            {
                searchRoot = _root;
                while (searchRoot.parent != null)
                    searchRoot = searchRoot.parent;
            }

            _mapBottomDock = searchRoot.Q<VisualElement>("MapBottomDock");
            _mapInfoPanel = searchRoot.Q<VisualElement>("MapInfoPanel");

            _infoPanelRefsResolved = (_mapBottomDock != null || _mapInfoPanel != null);

            // Hook callbacks once, when we first successfully find them.
            if (!_infoPanelCallbacksHooked && _infoPanelRefsResolved)
            {
                if (_mapBottomDock != null)
                    _mapBottomDock.RegisterCallback<GeometryChangedEvent>(_ => EnsureViewportHeightForScrollViewRoot());

                if (_mapInfoPanel != null)
                    _mapInfoPanel.RegisterCallback<GeometryChangedEvent>(_ => EnsureViewportHeightForScrollViewRoot());

                _infoPanelCallbacksHooked = true;
            }
        }

        private bool IsInfoPanelOpen()
        {
            // Prefer dock visibility (most implementations toggle dock on/off).
            if (_mapBottomDock != null)
                return _mapBottomDock.resolvedStyle.display != DisplayStyle.None;

            // Fallback to info panel visibility.
            if (_mapInfoPanel != null)
                return _mapInfoPanel.resolvedStyle.display != DisplayStyle.None;

            return false;
        }

        private float GetInfoPanelReserveHeight()
        {
            // If panel isn’t open, reserve nothing (expanded map).
            if (!IsInfoPanelOpen()) return 0f;

            // Prefer reserving dock height.
            if (_mapBottomDock != null)
            {
                float dockH = _mapBottomDock.resolvedStyle.height;
                if (dockH <= 1f) dockH = _mapBottomDock.layout.height;
                if (dockH > 1f) return dockH;
            }

            // Fallback to reserving info panel height.
            if (_mapInfoPanel != null)
            {
                float panelH = _mapInfoPanel.resolvedStyle.height;
                if (panelH <= 1f) panelH = _mapInfoPanel.layout.height;
                if (panelH > 1f) return panelH;
            }

            return 0f;
        }

        private string ResolveMarkerDisplayName(SkiGame.Map.MapMarker m)
        {
            // Prefer the baked marker display name.
            if (!string.IsNullOrWhiteSpace(m.displayName))
                return m.displayName;

            // Fallback: POI registry display name (this is usually what you want for runs).
            if (_poiRegistry != null && _poiRegistry.TryGetById(m.id, out var info) && !string.IsNullOrWhiteSpace(info.displayName))
                return info.displayName;

            // Last resort: id (should be rare after this change).
            return m.id;
        }

        private bool TryGetLinkedWaypointForMarker(string markerId, out MapWaypointRecord waypoint)
        {
            waypoint = default;

            if (_waypointManager == null || string.IsNullOrWhiteSpace(markerId))
                return false;

            string sourceKey = $"marker:{markerId}";
            return _waypointManager.TryGetWaypointBySourceKey(sourceKey, out waypoint);
        }

        private bool TryGetLinkedWaypointIdForMarker(string markerId, out string waypointId)
        {
            waypointId = null;

            if (!TryGetLinkedWaypointForMarker(markerId, out var waypoint))
                return false;

            if (string.IsNullOrWhiteSpace(waypoint.id))
                return false;

            waypointId = waypoint.id;
            return true;
        }

        private bool IsWaypointLinkedToExistingMarker(string waypointId)
        {
            if (_waypointManager == null || string.IsNullOrWhiteSpace(waypointId))
                return false;

            if (!_waypointManager.TryGetWaypoint(waypointId, out var waypoint))
                return false;

            return !string.IsNullOrWhiteSpace(waypoint.sourceKey) &&
                   waypoint.sourceKey.StartsWith("marker:", StringComparison.Ordinal);
        }

        private bool TryGetLinkedWaypointForPolyline(string polylineId, out MapWaypointRecord waypoint)
        {
            waypoint = default;

            if (_waypointManager == null || string.IsNullOrWhiteSpace(polylineId))
                return false;

            if (!TryResolveSourceMarkerForPolyline(polylineId, out var marker))
                return false;

            return TryGetLinkedWaypointForMarker(marker.id, out waypoint);
        }

        private static bool HasAlwaysLabelFlag(string meta)
        {
            if (string.IsNullOrWhiteSpace(meta)) return false;

            // Keep this intentionally simple + robust. You can author baked POIs with:
            // "label:always"  or  "alwaysLabel"  or  "[label=always]" etc.
            var m = meta.ToLowerInvariant();
            return m.Contains("label:always") || m.Contains("label=always") || m.Contains("alwayslabel");
        }

        private static Color GetFallbackCategoryColor(SkiGame.Map.MapMarker marker)
        {
            switch (marker.category)
            {
                case SkiGame.POI.POICategory.Resort:
                    return new Color(0.25f, 0.75f, 1f, 1f);

                case SkiGame.POI.POICategory.Shop:
                    return new Color(1f, 0.45f, 0.75f, 1f);

                case SkiGame.POI.POICategory.Kiosk:
                    return new Color(1f, 0.75f, 0.2f, 1f);

                case SkiGame.POI.POICategory.Service:
                    return new Color(0.45f, 1f, 0.45f, 1f);

                case SkiGame.POI.POICategory.Landmark:
                    return new Color(0.8f, 0.8f, 1f, 1f);

                default:
                    return marker.color;
            }
        }

        private string ResolvePolylineDisplayName(SkiGame.Map.MapPolyline p)
        {
            if (!string.IsNullOrWhiteSpace(p.displayName))
                return p.displayName;

            // If run polyline id maps to a marker, reuse that marker's resolved display name.
            if (_polylineToMarker != null && _polylineToMarker.TryGetValue(p.id, out var markerId) &&
                _markerDisplayNames != null && _markerDisplayNames.TryGetValue(markerId, out var name) &&
                !string.IsNullOrWhiteSpace(name))
                return name;

            if (_poiRegistry != null && _poiRegistry.TryGetById(p.id, out var info) && !string.IsNullOrWhiteSpace(info.displayName))
                return info.displayName;

            return p.id;
        }

        private void RebuildLiftPolylineLabels()
        {
            EnsureLabelOverlay();
            if (_labelOverlay == null) return;

            // Builds overlay labels for runs + lifts.
            // Prefer anchoring each label beside its linked marker when one exists,
            // otherwise anchor to the nearest point on the polyline itself.
            _polyAnchorLocal.Clear();
            _polyNormalLocal.Clear();

            foreach (var kv in _polylineLabelVisuals)
                kv.Value?.RemoveFromHierarchy();
            _polylineLabelVisuals.Clear();

            var lines = _mapData != null ? _mapData.Polylines : null;
            if (lines == null || lines.Count == 0) return;

            for (int i = 0; i < lines.Count; i++)
            {
                var p = lines[i];
                if (!p.IsValid) continue;
                if (p.lineType != MapLineType.SkiRun && p.lineType != MapLineType.SkiLift) continue;
                var pts = new List<Vector2>();

                if (p.Has3DPoints)
                {
                    pts.Capacity = p.pointsWorld.Count;
                    for (int k = 0; k < p.pointsWorld.Count; k++)
                    {
                        if (TryProjectWorldToLocal(p.pointsWorld[k], out var loc))
                            pts.Add(loc);
                    }
                }
                else if (p.HasXZPoints)
                {
                    pts.Capacity = p.pointsWorldXZ.Count;
                    for (int k = 0; k < p.pointsWorldXZ.Count; k++)
                    {
                        if (TryProjectWorldXZToLocal(p.pointsWorldXZ[k], out var loc))
                            pts.Add(loc);
                    }
                }

                if (pts.Count < 2) continue;

                Vector2 anchor;
                Vector2 n;

                if (!TryGetPreferredPolylineLabelAnchor(p, pts, out anchor, out n))
                    continue;

                string labelText = ResolvePolylineDisplayName(p);

                var label = new Label(labelText);
                label.AddToClassList("map-polyline-label");
                label.style.position = Position.Absolute;

                string polyId = p.id;
                MakeOverlayLabelInteractive(
                    label,
                    onClick: () =>
                    {
                        if (TryGetPolylineById(polyId, out var poly))
                        {
                            _selectedLiftStationSuffix = null;
                            SelectPolylineInternal(polyId, poly, fireEvent: true);
                            _polyLayer?.SetSelected(polyId);
                        }
                    },
                    onDoubleClick: () =>
                    {
                        if (TryResolveSourceMarkerForPolyline(polyId, out var sourceMarker))
                            MarkerDoubleClicked?.Invoke(sourceMarker);
                    },
                    onRightDoubleClick: () =>
                    {
                        if (TryResolveSourceMarkerForPolyline(polyId, out var sourceMarker))
                            MarkerRightDoubleClicked?.Invoke(sourceMarker);
                    });

                _polylineLabelVisuals[p.id] = label;
                _polyAnchorLocal[p.id] = anchor;
                _polyNormalLocal[p.id] = n;

                _polylineLabelAccent[p.id] = GetPolylineDisplayColor(p);
                ApplyPolylineLabelVisual(p.id, selected: IsPolylineSelected(p));

                _labelOverlay.Add(label);
            }

            LayoutOverlayLabels();
        }

        private bool TryGetPreferredPolylineLabelAnchor(SkiGame.Map.MapPolyline p, List<Vector2> pts, out Vector2 anchor, out Vector2 normal)
        {
            anchor = default;
            normal = Vector2.down; // UI y is down, so Vector2.down places label above the anchor.

            if (pts == null || pts.Count < 2)
                return false;

            anchor = ComputePolylineMidpoint(pts, out _);
            normal = Vector2.down;
            return true;
        }

        private static Vector2 ComputePolylineMidpoint(List<Vector2> pts, out Vector2 dirAtMid)
        {
            dirAtMid = Vector2.right;
            if (pts == null || pts.Count < 2)
                return default;

            float total = 0f;
            for (int i = 1; i < pts.Count; i++)
                total += Vector2.Distance(pts[i - 1], pts[i]);

            if (total <= 0.001f)
            {
                dirAtMid = (pts[1] - pts[0]).normalized;
                if (dirAtMid.sqrMagnitude < 0.0001f)
                    dirAtMid = Vector2.right;
                return pts[0];
            }

            float half = total * 0.5f;
            float acc = 0f;

            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1];
                Vector2 b = pts[i];
                float seg = Vector2.Distance(a, b);
                if (acc + seg >= half)
                {
                    float t = (half - acc) / Mathf.Max(0.0001f, seg);
                    dirAtMid = (b - a).normalized;
                    if (dirAtMid.sqrMagnitude < 0.0001f)
                        dirAtMid = Vector2.right;
                    return Vector2.Lerp(a, b, t);
                }
                acc += seg;
            }

            dirAtMid = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
            if (dirAtMid.sqrMagnitude < 0.0001f)
                dirAtMid = Vector2.right;
            return pts[pts.Count - 1];
        }

        private bool TryFindNearestPolylineAnchorAndNormal(List<Vector2> pts, Vector2 target, out Vector2 anchor, out Vector2 normal)
        {
            anchor = default;
            normal = Vector2.up;
            if (pts == null || pts.Count < 2)
                return false;

            float bestDist = float.PositiveInfinity;
            Vector2 bestPoint = default;
            Vector2 bestDir = Vector2.right;
            bool found = false;

            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1];
                Vector2 b = pts[i];
                Vector2 ab = b - a;
                float lenSq = ab.sqrMagnitude;
                if (lenSq <= 0.0001f)
                    continue;

                float t = Mathf.Clamp01(Vector2.Dot(target - a, ab) / lenSq);
                Vector2 point = a + ab * t;
                float dist = (target - point).sqrMagnitude;
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                bestPoint = point;
                bestDir = ab.normalized;
                found = true;
            }

            if (!found)
                return false;

            anchor = bestPoint;
            normal = ComputePreferredLabelNormal(bestDir);
            return true;
        }

        private static Vector2 ComputePreferredLabelNormal(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;

            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x);
            if (n.sqrMagnitude < 0.0001f)
                n = Vector2.up;
            n.Normalize();

            // UI space is y-down. Prefer the screen-up side so labels visually hug the feature.
            if (n.y > 0f)
                n = -n;

            return n;
        }

        private void RebuildPOIOverlayLabels()
        {
            EnsureLabelOverlay();
            if (_labelOverlay == null) return;
            if (_mapData == null || _mapData.Markers == null) return;

            // Remove existing POI labels (we’ll keep using _poiOverlayLabels name if you want,
            // but it now lives inside _labelOverlay only, not a separate overlay element).
            foreach (var kv in _poiOverlayLabels)
                kv.Value?.RemoveFromHierarchy();
            _poiOverlayLabels.Clear();

            var list = _mapData.Markers;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.IsValid) continue;

                // POIs only (runs/lifts are labeled via polyline)
                if (m.type == POIType.SkiRun || m.type == POIType.SkiLift)
                    continue;

                if (!_markerAnchorLocal.ContainsKey(m.id))
                    continue;

                string text = ResolveMarkerDisplayName(m);
                var label = new Label(text);
                label.AddToClassList("map-marker-label");
                label.style.position = Position.Absolute;

                // Make overlay POI labels selectable.
                string markerId = m.id;
                MakeOverlayLabelInteractive(
                    label,
                    onClick: () => SelectPOIMarkerOnly(markerId, fireEvent: true),
                    onDoubleClick: () =>
                    {
                        if (_markerById.TryGetValue(markerId, out var marker))
                            MarkerDoubleClicked?.Invoke(marker);
                    },
                    onRightDoubleClick: () =>
                    {
                        if (_markerById.TryGetValue(markerId, out var marker))
                            MarkerRightDoubleClicked?.Invoke(marker);
                    });

                _poiOverlayLabels[m.id] = label;
                _labelOverlay.Add(label);

                ApplyPOIOverlayLabelVisual(m.id, selected: string.Equals(_selectedMarkerId, m.id, StringComparison.Ordinal));
            }

            LayoutOverlayLabels();
        }

        private Vector2 ResolveRegionLabelAnchorLocal(MapRegionFace face)
        {
            if (_regionSet == null || face == null)
                return new Vector2(_contentSize.x * 0.5f, _contentSize.y * 0.5f);

            Vector2 anchorUv = face.labelAnchorUv;
            bool anchorLooksValid =
                anchorUv.x >= 0f && anchorUv.x <= 1f &&
                anchorUv.y >= 0f && anchorUv.y <= 1f &&
                MapRegionUtility.ContainsFace(_regionSet, face, anchorUv);

            if (!anchorLooksValid)
            {
                var ptsUv = MapRegionUtility.ResolveLoopUv(_regionSet, face.outerVertexIds);
                if (ptsUv != null && ptsUv.Count > 0)
                {
                    // Try simple centroid first.
                    Vector2 centroidUv = MapRegionUtility.ComputeCentroid(ptsUv, new Vector2(0.5f, 0.5f));

                    // If centroid falls outside the actual face, fall back to the bounds center.
                    if (MapRegionUtility.ContainsFace(_regionSet, face, centroidUv))
                    {
                        anchorUv = centroidUv;
                    }
                    else
                    {
                        float minX = float.MaxValue, minY = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue;

                        for (int i = 0; i < ptsUv.Count; i++)
                        {
                            var p = ptsUv[i];
                            if (p.x < minX) minX = p.x;
                            if (p.y < minY) minY = p.y;
                            if (p.x > maxX) maxX = p.x;
                            if (p.y > maxY) maxY = p.y;
                        }

                        Vector2 boundsCenterUv = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

                        anchorUv = MapRegionUtility.ContainsFace(_regionSet, face, boundsCenterUv)
                            ? boundsCenterUv
                            : centroidUv;
                    }
                }
                else
                {
                    anchorUv = new Vector2(0.5f, 0.5f);
                }
            }

            anchorUv.x = Mathf.Clamp01(anchorUv.x);
            anchorUv.y = Mathf.Clamp01(anchorUv.y);

            return new Vector2(anchorUv.x * _contentSize.x, (1f - anchorUv.y) * _contentSize.y);
        }

        private void RebuildRegionOverlayLabels()
        {
            EnsureLabelOverlay();
            if (_labelOverlay == null) return;

            foreach (var kv in _regionOverlayLabels)
                kv.Value?.RemoveFromHierarchy();

            _regionOverlayLabels.Clear();
            _regionAnchorLocal.Clear();
            _labelSlotCache.Clear();

            if (_regionSet == null || _regionSet.Faces == null)
                return;

            for (int i = 0; i < _regionSet.Faces.Count; i++)
            {
                var face = _regionSet.Faces[i];
                if (face == null || !face.IsValid)
                    continue;

                string id = face.id;
                string text = string.IsNullOrWhiteSpace(face.displayName) ? id : face.displayName;

                Vector2 anchorLocal = ResolveRegionLabelAnchorLocal(face);
                _regionAnchorLocal[id] = anchorLocal;

                var label = new Label(text);
                label.AddToClassList("map-polyline-label");
                label.style.position = Position.Absolute;

                // Region labels are now the primary selector, so make them a bit easier to hit.
                label.style.paddingLeft = 8f;
                label.style.paddingRight = 8f;
                label.style.paddingTop = 4f;
                label.style.paddingBottom = 4f;
                label.style.minHeight = 22f;

                string regionId = id;
                MakeOverlayLabelInteractive(label, () => SelectRegion(regionId, zoomToRegion: true));

                _regionOverlayLabels[id] = label;
                _labelOverlay.Add(label);

                ApplyRegionOverlayLabelVisual(id, selected: string.Equals(_selectedRegionId, id, StringComparison.Ordinal));
            }

            LayoutOverlayLabels();
        }

        private void ApplyPOIOverlayLabelVisual(string markerId, bool selected)
        {
            if (_style == null) return;
            if (!_poiOverlayLabels.TryGetValue(markerId, out var label) || label == null) return;

            label.style.unityFontStyleAndWeight = selected ? _style.labelFontStyleSelected : _style.labelFontStyle;

            Color textColor = selected ? _style.labelColorSelected : _style.labelColor;
            if (TryGetLinkedWaypointForMarker(markerId, out var linkedWp))
                textColor = linkedWp.color;

            label.style.color = textColor;

            if (_markerLabelAccent.TryGetValue(markerId, out var accent))
            {
                float stripeW = Mathf.Max(1f, _style.labelAccentStripeWidth);
                label.style.borderLeftWidth = stripeW;
                label.style.borderLeftColor = accent;

                float a = Mathf.Clamp01(_style.labelPlateAlpha) * 0.22f;
                label.style.backgroundColor = new Color(accent.r, accent.g, accent.b, a);
            }
        }

        private void ApplyRegionOverlayLabelVisual(string regionId, bool selected)
        {
            if (_style == null) return;
            if (!_regionOverlayLabels.TryGetValue(regionId, out var label) || label == null) return;
            if (_regionSet == null) return;

            var face = _regionSet.GetFaceById(regionId);
            if (face == null) return;

            label.style.unityFontStyleAndWeight = selected ? _style.labelFontStyleSelected : _style.labelFontStyle;
            label.style.color = selected ? _style.labelColorSelected : _style.labelColor;

            Color accent = face.borderColor;
            label.style.borderLeftWidth = Mathf.Max(1f, _style.labelAccentStripeWidth);
            label.style.borderLeftColor = accent;

            float a = Mathf.Clamp01(_style.labelPlateAlpha) * 0.28f;
            label.style.backgroundColor = new Color(accent.r, accent.g, accent.b, a);
        }

        private void LayoutOverlayLabels()
        {
            if (_labelOverlay == null) return;
            if (_style == null) return;
            if (_viewport == null) return;

            if (!TryGetViewportSize(out float vw, out float vh))
                return;

            float fitZoom = Mathf.Min(
                vw / Mathf.Max(1f, _contentSize.x),
                vh / Mathf.Max(1f, _contentSize.y));

            fitZoom = Mathf.Max(0.0001f, fitZoom);

            float relativeZoom = _zoom / fitZoom;

            // 1.0 = fully zoomed out / fit-to-viewport.
            // Fade labels out gradually as the user zooms in past overview.
            float regionOverviewAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(1.10f, 2.20f, relativeZoom));
            bool showRegionOverview = regionOverviewAlpha > 0.02f;

            // ---- Helpers ----
            int ComputeFont(bool selected)
            {
                int baseSize = selected ? _style.labelFontSizeSelected : _style.labelFontSize;

                float z = Mathf.Max(0.0001f, _zoom);
                float comp = Mathf.Clamp01(_style.labelZoomCompensation);
                float scaled = baseSize / Mathf.Pow(z, comp);

                int sz = Mathf.RoundToInt(scaled);
                sz = Mathf.Clamp(sz, _style.labelFontMin, _style.labelFontMax);
                return sz;
            }

            Vector2 Measure(Label l)
            {
                // MeasureTextSize is stable even before a layout pass.
                var s = l.MeasureTextSize(l.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined);
                // Add a small safety margin for padding/borders so collisions feel nicer.
                return new Vector2(s.x + 6f, s.y + 4f);
            }

            Rect MakeRect(Vector2 topLeft, Vector2 size) => new Rect(topLeft.x, topLeft.y, size.x, size.y);

            int CellKey(int cx, int cy) => (cy << 16) ^ (cx & 0xFFFF);

            const float MarkerAvoidRadiusPx = 14f;
            const float PolylineAvoidInsetPx = 4f;
            const float PolylineLabelSlotStepPenalty = 24f;
            const float MarkerOverlapPenalty = 3000f;
            const float PolylineOverlapPenalty = 2200f;
            const float OffAxisPenalty = 120f;

            Rect ExpandRect(Rect r, float pad)
            {
                return new Rect(r.xMin - pad, r.yMin - pad, r.width + pad * 2f, r.height + pad * 2f);
            }

            bool RectContainsPoint(Rect r, Vector2 p)
            {
                return p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax;
            }

            bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
            {
                float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

                Vector2 r = a2 - a1;
                Vector2 s = b2 - b1;
                float denom = Cross(r, s);

                if (Mathf.Abs(denom) < 0.0001f)
                    return false;

                Vector2 diff = b1 - a1;
                float t = Cross(diff, s) / denom;
                float uVal = Cross(diff, r) / denom;

                return t >= 0f && t <= 1f && uVal >= 0f && uVal <= 1f;
            }

            bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect r)
            {
                if (RectContainsPoint(r, a) || RectContainsPoint(r, b))
                    return true;

                Vector2 tl = new Vector2(r.xMin, r.yMin);
                Vector2 tr = new Vector2(r.xMax, r.yMin);
                Vector2 br = new Vector2(r.xMax, r.yMax);
                Vector2 bl = new Vector2(r.xMin, r.yMax);

                return
                    SegmentsIntersect(a, b, tl, tr) ||
                    SegmentsIntersect(a, b, tr, br) ||
                    SegmentsIntersect(a, b, br, bl) ||
                    SegmentsIntersect(a, b, bl, tl);
            }

            void AddRectToGrid(Dictionary<int, List<Rect>> grid, Rect r)
            {
                int x0 = Mathf.FloorToInt(r.xMin / LabelCellSizePx);
                int x1 = Mathf.FloorToInt(r.xMax / LabelCellSizePx);
                int y0 = Mathf.FloorToInt(r.yMin / LabelCellSizePx);
                int y1 = Mathf.FloorToInt(r.yMax / LabelCellSizePx);

                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int key = CellKey(x, y);
                        if (!grid.TryGetValue(key, out var bucket))
                        {
                            bucket = new List<Rect>(8);
                            grid[key] = bucket;
                        }
                        bucket.Add(r);
                    }
            }

            bool Overlaps(Dictionary<int, List<Rect>> grid, Rect r)
            {
                int x0 = Mathf.FloorToInt(r.xMin / LabelCellSizePx);
                int x1 = Mathf.FloorToInt(r.xMax / LabelCellSizePx);
                int y0 = Mathf.FloorToInt(r.yMin / LabelCellSizePx);
                int y1 = Mathf.FloorToInt(r.yMax / LabelCellSizePx);

                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int key = CellKey(x, y);
                        if (!grid.TryGetValue(key, out var bucket)) continue;

                        for (int i = 0; i < bucket.Count; i++)
                            if (bucket[i].Overlaps(r))
                                return true;
                    }
                return false;
            }

            var visibleMarkerAvoidRects = new List<Rect>(128);
            foreach (var kv in _markerAnchorLocal)
            {
                string markerId = kv.Key;

                if (!IsMarkerVisibleById(markerId))
                    continue;

                Vector2 anchorVp = _pan + kv.Value * _zoom;

                // Skip obviously offscreen markers.
                if (anchorVp.x < -32f || anchorVp.x > vw + 32f || anchorVp.y < -32f || anchorVp.y > vh + 32f)
                    continue;

                visibleMarkerAvoidRects.Add(new Rect(
                    anchorVp.x - MarkerAvoidRadiusPx,
                    anchorVp.y - MarkerAvoidRadiusPx,
                    MarkerAvoidRadiusPx * 2f,
                    MarkerAvoidRadiusPx * 2f));
            }

            var visiblePolylineSegments = new Dictionary<string, List<(Vector2 a, Vector2 b)>>(64);

            if (_mapData != null && _mapData.Polylines != null)
            {
                for (int i = 0; i < _mapData.Polylines.Count; i++)
                {
                    var poly = _mapData.Polylines[i];
                    if (!poly.IsValid) continue;
                    if (poly.lineType != MapLineType.SkiRun && poly.lineType != MapLineType.SkiLift) continue;
                    int estimatedCount = poly.Has3DPoints ? poly.pointsWorld.Count : (poly.HasXZPoints ? poly.pointsWorldXZ.Count : 0);
                    if (estimatedCount < 2) continue;

                    var segs = new List<(Vector2 a, Vector2 b)>(Mathf.Max(1, estimatedCount - 1));
                    bool havePrev = false;
                    Vector2 prev = default;

                    if (poly.Has3DPoints)
                    {
                        for (int k = 0; k < poly.pointsWorld.Count; k++)
                        {
                            if (!TryProjectWorldToLocal(poly.pointsWorld[k], out var local))
                                continue;

                            Vector2 vp = _pan + local * _zoom;

                            if (havePrev)
                                segs.Add((prev, vp));

                            prev = vp;
                            havePrev = true;
                        }
                    }
                    else if (poly.HasXZPoints)
                    {
                        for (int k = 0; k < poly.pointsWorldXZ.Count; k++)
                        {
                            if (!TryProjectWorldXZToLocal(poly.pointsWorldXZ[k], out var local))
                                continue;

                            Vector2 vp = _pan + local * _zoom;

                            if (havePrev)
                                segs.Add((prev, vp));

                            prev = vp;
                            havePrev = true;
                        }
                    }

                    if (segs.Count > 0)
                        visiblePolylineSegments[poly.id] = segs;
                }
            }

            float ComputePolylineGeometryPenalty(string polyId, Rect candidateRect)
            {
                float penalty = 0f;

                Rect expanded = ExpandRect(candidateRect, PolylineAvoidInsetPx);

                for (int i = 0; i < visibleMarkerAvoidRects.Count; i++)
                {
                    if (expanded.Overlaps(visibleMarkerAvoidRects[i]))
                        penalty += MarkerOverlapPenalty;
                }

                foreach (var kv in visiblePolylineSegments)
                {
                    bool isOwnPolyline = string.Equals(kv.Key, polyId, StringComparison.Ordinal);
                    float polyPenalty = isOwnPolyline ? (PolylineOverlapPenalty * 0.35f) : PolylineOverlapPenalty;

                    var segs = kv.Value;
                    for (int i = 0; i < segs.Count; i++)
                    {
                        if (SegmentIntersectsRect(segs[i].a, segs[i].b, expanded))
                            penalty += polyPenalty;
                    }
                }

                return penalty;
            }

            // ---- Build a prioritized list of labels to place ----
            // Priority lower = placed first (wins).
            var work = new List<(string id, Label label, Vector2 anchorLocal, Vector2 preferDir, int priority, bool mustShow, float distKey)>(256);

            // Minimap mode hides POI/run/lift labels, but region labels should still be allowed.
            bool suppressNonRegionLabels = _hideMarkerLabels;
            if (suppressNonRegionLabels)
            {
                foreach (var kv in _polylineLabelVisuals)
                    if (kv.Value != null) kv.Value.style.display = DisplayStyle.None;

                foreach (var kv in _poiOverlayLabels)
                    if (kv.Value != null) kv.Value.style.display = DisplayStyle.None;
            }

            // Use viewport center as the “focus point” for prioritizing context labels.
            Vector2 focusContent = ((new Vector2(vw * 0.5f, vh * 0.5f) - _pan) / Mathf.Max(0.0001f, _zoom));

            // If something is selected, hide all other overlay labels (focus mode).
            // We keep only the label that corresponds to the selected marker/polyline.
            bool hasSelection =
                !string.IsNullOrEmpty(_selectedPolylineId) ||
                !string.IsNullOrEmpty(_selectedMarkerId);

            if (hasSelection)
            {
                // Hide everything by default.
                foreach (var kv in _polylineLabelVisuals)
                    if (kv.Value != null) kv.Value.style.display = DisplayStyle.None;

                foreach (var kv in _poiOverlayLabels)
                    if (kv.Value != null) kv.Value.style.display = DisplayStyle.None;

                // Build a tiny placement list (usually 1 item).
                var workSel = new List<(string id, Label label, Vector2 anchorLocal, Vector2 preferDir, int priority, bool mustShow, float distKey)>(2);

                // Selected polyline label (runs/lifts).
                if (!string.IsNullOrEmpty(_selectedPolylineId) &&
                    _polylineLabelVisuals.TryGetValue(_selectedPolylineId, out var polyLabel) &&
                    polyLabel != null &&
                    _polyAnchorLocal.TryGetValue(_selectedPolylineId, out var polyAnchor))
                {
                    _polyNormalLocal.TryGetValue(_selectedPolylineId, out var polyN);

                    polyLabel.style.fontSize = ComputeFont(true);
                    polyLabel.style.opacity = 1f;
                    ApplyPolylineLabelVisual(_selectedPolylineId, selected: true);

                    workSel.Add((_selectedPolylineId, polyLabel, polyAnchor, polyN, priority: 0, mustShow: true, distKey: 0f));
                }

                // Selected POI label (only applies to non-run/non-lift POIs because those are what _poiOverlayLabels contains).
                if (!string.IsNullOrEmpty(_selectedMarkerId) &&
                    _poiOverlayLabels.TryGetValue(_selectedMarkerId, out var poiLabel) &&
                    poiLabel != null &&
                    _markerAnchorLocal.TryGetValue(_selectedMarkerId, out var poiAnchor))
                {
                    poiLabel.style.fontSize = ComputeFont(true);
                    poiLabel.style.opacity = 1f;
                    ApplyPOIOverlayLabelVisual(_selectedMarkerId, selected: true);

                    workSel.Add((_selectedMarkerId, poiLabel, poiAnchor, Vector2.down, priority: 1, mustShow: true, distKey: 0f));
                }

                // If nothing matches (eg. selected lift station marker -> label is the polyline label above),
                // we still just return after hiding everything else.
                if (workSel.Count == 0)
                    return;

                // Place selected label(s) using the same collision grid (handles rare case where both exist).
                workSel.Sort((a, b) => a.priority.CompareTo(b.priority));

                var gridSel = new Dictionary<int, List<Rect>>(16);

                for (int i = 0; i < workSel.Count; i++)
                {
                    var item = workSel[i];

                    Vector2 anchorVp = _pan + item.anchorLocal * _zoom;
                    Vector2 size = Measure(item.label);

                    Vector2 dir = item.preferDir;
                    if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
                    dir.Normalize();

                    Vector2[] dirs =
                    {
            dir,
            -dir,
            Vector2.up,
            Vector2.down,
            Vector2.right,
            Vector2.left,
            (dir + Vector2.right).normalized,
            (dir + Vector2.left).normalized,
        };

                    int startSlot = 0;
                    if (_labelSlotCache.TryGetValue(item.id, out var cached))
                        startSlot = Mathf.Clamp(cached, 0, dirs.Length - 1);

                    bool placed = false;
                    Rect placedRect = default;
                    Vector2 placedPos = default;

                    for (int attempt = 0; attempt < dirs.Length; attempt++)
                    {
                        int slot = (startSlot + attempt) % dirs.Length;
                        Vector2 pos = anchorVp + dirs[slot] * LabelBaseOffsetPx;

                        Vector2 tl = new Vector2(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f);

                        tl.x = Mathf.Clamp(tl.x, 2f, vw - size.x - 2f);
                        tl.y = Mathf.Clamp(tl.y, 2f, vh - size.y - 2f);

                        Rect r = MakeRect(tl, size);

                        if (!Overlaps(gridSel, r) || item.mustShow)
                        {
                            placed = true;
                            placedRect = r;
                            placedPos = tl;
                            _labelSlotCache[item.id] = slot;
                            break;
                        }
                    }

                    if (!placed)
                    {
                        item.label.style.display = DisplayStyle.None;
                        continue;
                    }

                    item.label.style.display = DisplayStyle.Flex;
                    item.label.style.left = placedPos.x;
                    item.label.style.top = placedPos.y;

                    AddRectToGrid(gridSel, placedRect);
                }

                return;
            }

            // --- Regions overview ---
            if (_regionOverlayLabels.Count > 0)
            {
                foreach (var kv in _regionOverlayLabels)
                {
                    string id = kv.Key;
                    var label = kv.Value;
                    if (label == null) continue;

                    if (!_regionAnchorLocal.TryGetValue(id, out var anchor))
                    {
                        label.style.display = DisplayStyle.None;
                        continue;
                    }

                    bool selected = string.Equals(_selectedRegionId, id, StringComparison.Ordinal);

                    if (!showRegionOverview && !selected)
                    {
                        label.style.display = DisplayStyle.None;
                        continue;
                    }

                    bool isRoot = string.Equals(id, MapRegionSet.RootFaceId, StringComparison.Ordinal);

                    label.style.fontSize = selected
                        ? Mathf.Max(ComputeFont(true), 16)
                        : Mathf.Max(ComputeFont(false), isRoot ? 13 : 12);

                    label.style.opacity = selected
                        ? 1f
                        : Mathf.Clamp01(Mathf.Lerp(isRoot ? 0.28f : 0.18f, 1f, regionOverviewAlpha));

                    ApplyRegionOverlayLabelVisual(id, selected);

                    Vector2 size = Measure(label);
                    Vector2 anchorVp = _pan + anchor * _zoom;

                    // Do not clamp off-screen region labels into the viewport corners.
                    // That was causing multiple labels to pile up in the top-left.
                    const float offscreenMargin = 24f;
                    bool anchorOffscreen =
                        anchorVp.x < -offscreenMargin ||
                        anchorVp.y < -offscreenMargin ||
                        anchorVp.x > vw + offscreenMargin ||
                        anchorVp.y > vh + offscreenMargin;

                    if (anchorOffscreen)
                    {
                        label.style.display = DisplayStyle.None;
                        continue;
                    }

                    Vector2 tl = new Vector2(
                        Mathf.Clamp(anchorVp.x - size.x * 0.5f, 2f, vw - size.x - 2f),
                        Mathf.Clamp(anchorVp.y - size.y * 0.5f, 2f, vh - size.y - 2f));

                    label.style.display = DisplayStyle.Flex;
                    label.style.left = tl.x;
                    label.style.top = tl.y;
                }
            }

            if (suppressNonRegionLabels)
                return;

            // --- Polylines: runs + lifts ---
            var polyContext = new List<(string id, Label label, Vector2 anchor, Vector2 n, bool selected, float dist)>(128);

            foreach (var kv in _polylineLabelVisuals)
            {
                string id = kv.Key;
                var label = kv.Value;
                if (label == null) continue;

                if (!TryGetPolylineById(id, out var p))
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                bool layerOn = IsPolylineVisible(p);

                bool isSelected = IsPolylineSelected(p);

                // Simple visibility rule: selected always; otherwise only when zoomed in and layer enabled.
                bool linkedWaypoint = TryGetLinkedWaypointForPolyline(id, out _);
                bool show = !showRegionOverview && (isSelected || linkedWaypoint || (layerOn && _zoom >= RunLiftAlwaysVisibleMinZoom));

                if (!show)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                _polyAnchorLocal.TryGetValue(id, out var anchor);
                _polyNormalLocal.TryGetValue(id, out var n);

                float dist = (anchor - focusContent).sqrMagnitude;

                // Style
                label.style.fontSize = ComputeFont(isSelected);
                label.style.opacity = 1f;
                ApplyPolylineLabelVisual(id, selected: isSelected);

                if (isSelected)
                {
                    work.Add((id, label, anchor, n, priority: 0, mustShow: true, distKey: dist));
                }
                else
                {
                    polyContext.Add((id, label, anchor, n, selected: false, dist: dist));
                }
            }

            // Cap context polylines by distance to focus
            polyContext.Sort((a, b) => a.dist.CompareTo(b.dist));
            for (int i = 0; i < polyContext.Count; i++)
            {
                if (i >= MaxContextRunLiftLabels)
                {
                    polyContext[i].label.style.display = DisplayStyle.None;
                    continue;
                }

                var it = polyContext[i];
                work.Add((it.id, it.label, it.anchor, it.n, priority: 5, mustShow: false, distKey: it.dist));
            }

            // --- POIs ---
            var poiContext = new List<(string id, Label label, Vector2 anchor, bool selected, bool always, float dist)>(128);

            foreach (var kv in _poiOverlayLabels)
            {
                string id = kv.Key;
                var label = kv.Value;
                if (label == null) continue;

                if (!_markerTypes.TryGetValue(id, out var type))
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                bool isSelected = string.Equals(_selectedMarkerId, id, StringComparison.Ordinal);
                bool always = _poiAlwaysLabelById.TryGetValue(id, out var a) && a;

                // Simple visibility: selected always; otherwise only when zoomed in.
                bool linkedWaypoint = TryGetLinkedWaypointForMarker(id, out _);
                bool show = !showRegionOverview && IsMarkerVisibleById(id) && (isSelected || linkedWaypoint || always || _zoom >= PoiVisibleMinZoom);
                if (!show)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                if (!_markerAnchorLocal.TryGetValue(id, out var anchor))
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                float dist = (anchor - focusContent).sqrMagnitude;

                label.style.fontSize = ComputeFont(isSelected);
                ApplyPOIOverlayLabelVisual(id, isSelected);
                label.style.opacity = 1f;

                if (isSelected)
                {
                    work.Add((id, label, anchor, Vector2.down, priority: 1, mustShow: true, distKey: dist));
                }
                else
                {
                    poiContext.Add((id, label, anchor, selected: false, always: always, dist: dist));
                }
            }

            // Prefer always-label POIs over ordinary ones, then distance
            poiContext.Sort((a, b) =>
            {
                int c = b.always.CompareTo(a.always);
                if (c != 0) return c;
                return a.dist.CompareTo(b.dist);
            });

            for (int i = 0; i < poiContext.Count; i++)
            {
                if (i >= MaxContextPoiLabels)
                {
                    poiContext[i].label.style.display = DisplayStyle.None;
                    continue;
                }

                var it = poiContext[i];
                work.Add((it.id, it.label, it.anchor, Vector2.down, priority: it.always ? 6 : 7, mustShow: false, distKey: it.dist));
            }

            // Sort by priority, then distance, then id for determinism
            work.Sort((a, b) =>
            {
                int c = a.priority.CompareTo(b.priority);
                if (c != 0) return c;
                c = a.distKey.CompareTo(b.distKey);
                if (c != 0) return c;
                return string.CompareOrdinal(a.id, b.id);
            });

            // ---- Place labels without overlap ----
            var grid = new Dictionary<int, List<Rect>>(256);

            // If a label's anchor is far outside the viewport, clamping would pin it to an edge,
            // which reads as "label detached from its feature". Hide those (except for selected).
            const float OffscreenAnchorMarginPx = 48f;

            for (int i = 0; i < work.Count; i++)
            {
                var item = work[i];

                // Convert content-local anchor to viewport-local anchor
                Vector2 anchorVp = _pan + item.anchorLocal * _zoom;

                if (!item.mustShow)
                {
                    if (anchorVp.x < -OffscreenAnchorMarginPx || anchorVp.x > vw + OffscreenAnchorMarginPx ||
                        anchorVp.y < -OffscreenAnchorMarginPx || anchorVp.y > vh + OffscreenAnchorMarginPx)
                    {
                        item.label.style.display = DisplayStyle.None;
                        continue;
                    }
                }

                Vector2 size = Measure(item.label);

                // Candidate directions (in viewport space)
                Vector2 dir = item.preferDir;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
                dir.Normalize();

                bool isPolylineLabel = _polylineLabelVisuals.ContainsKey(item.id);

                Vector2 dirRight = new Vector2(dir.y, -dir.x).normalized;
                Vector2 dirLeft = -dirRight;

                Vector2[] dirs;

                if (isPolylineLabel)
                {
                    // Strongly prefer "above midpoint" placements first.
                    dirs = item.mustShow
                        ? new Vector2[]
                        {
            Vector2.down,
            (Vector2.down + Vector2.right * 0.35f).normalized,
            (Vector2.down + Vector2.left * 0.35f).normalized,
            Vector2.right,
            Vector2.left,
            dir,
            (dir + dirRight * 0.5f).normalized,
            (dir + dirLeft * 0.5f).normalized,
            -dir,
                        }
                        : new Vector2[]
                        {
            Vector2.down,
            (Vector2.down + Vector2.right * 0.35f).normalized,
            (Vector2.down + Vector2.left * 0.35f).normalized,
            Vector2.right,
            Vector2.left,
                        };
                }
                else
                {
                    dirs = item.mustShow
                        ? new Vector2[]
                        {
            dir,
            (dir + dirRight * 0.5f).normalized,
            (dir + dirLeft * 0.5f).normalized,
            -dir,
            Vector2.up,
            Vector2.down,
            Vector2.right,
            Vector2.left,
                        }
                        : new Vector2[]
                        {
            dir,
            (dir + dirRight * 0.5f).normalized,
            (dir + dirLeft * 0.5f).normalized,
            dirRight,
            dirLeft,
                        };
                }

                int startSlot = 0;
                if (_labelSlotCache.TryGetValue(item.id, out var cached))
                    startSlot = Mathf.Clamp(cached, 0, dirs.Length - 1);

                bool placed = false;
                Rect placedRect = default;
                Vector2 placedPos = default;
                int placedSlot = -1;
                float bestScore = float.PositiveInfinity;

                for (int attempt = 0; attempt < dirs.Length; attempt++)
                {
                    int slot = (startSlot + attempt) % dirs.Length;

                    Vector2 pos = anchorVp + dirs[slot] * LabelBaseOffsetPx;

                    Vector2 tlDesired = new Vector2(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f);

                    bool inBounds =
                        (tlDesired.x >= 2f && tlDesired.x <= (vw - size.x - 2f)) &&
                        (tlDesired.y >= 2f && tlDesired.y <= (vh - size.y - 2f));

                    Vector2 tl = tlDesired;
                    if (!inBounds)
                    {
                        if (!item.mustShow)
                            continue;

                        tl.x = Mathf.Clamp(tl.x, 2f, vw - size.x - 2f);
                        tl.y = Mathf.Clamp(tl.y, 2f, vh - size.y - 2f);
                    }

                    Rect r = MakeRect(tl, size);

                    bool overlapsPlacedLabel = Overlaps(grid, r);
                    if (overlapsPlacedLabel && !item.mustShow)
                        continue;

                    float score = attempt * PolylineLabelSlotStepPenalty;

                    if (isPolylineLabel)
                    {
                        score += ComputePolylineGeometryPenalty(item.id, r);

                        // Mildly prefer placements that remain "above" rather than drifting sideways.
                        Vector2 slotDir = dirs[slot];
                        score += Mathf.Abs(slotDir.x) * OffAxisPenalty;
                        if (slotDir.y > -0.25f)
                            score += OffAxisPenalty;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        placed = true;
                        placedRect = r;
                        placedPos = tl;
                        placedSlot = slot;

                        if (score <= 0.001f)
                            break;
                    }
                }

                if (!placed)
                {
                    item.label.style.display = DisplayStyle.None;
                    continue;
                }

                item.label.style.display = DisplayStyle.Flex;
                item.label.style.left = placedPos.x;
                item.label.style.top = placedPos.y;

                _labelSlotCache[item.id] = Mathf.Max(0, placedSlot);

                // Add to collision grid
                AddRectToGrid(grid, placedRect);
            }

        }

        private Vector2 UVToLocal(Vector2 uv)
        {
            uv = ApplyBackgroundInset(uv);

            // Hard clamp to prevent clipping artifacts when data/camera is slightly off.
            //uv.x = Mathf.Clamp01(uv.x);
            //uv.y = Mathf.Clamp01(uv.y);

            return new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y);
        }


        private void ApplyTransform()
        {
            if (_content == null) return;

            _content.transform.position = new Vector3(_pan.x, _pan.y, 0f);
            _content.transform.scale = new Vector3(_zoom, _zoom, 1f);

            _regionLayer?.SetZoom(_zoom);
            _polyLayer?.SetZoom(_zoom);
            _trailLayer?.SetZoom(_zoom);

            // Keep markers/player marker screen-locked if you still want that behavior
            UpdateAllLabelTransformsForZoom();

            // Overlay labels reposition from pan/zoom (no inverse scaling required)
            LayoutOverlayLabels();
        }

        private static string GetArrowGlyph(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            if (angle >= -22.5f && angle < 22.5f) return "▶";
            if (angle >= 22.5f && angle < 67.5f) return "◥";
            if (angle >= 67.5f && angle < 112.5f) return "▲";
            if (angle >= 112.5f && angle < 157.5f) return "◤";
            if (angle >= 157.5f || angle < -157.5f) return "◀";
            if (angle >= -157.5f && angle < -112.5f) return "◣";
            if (angle >= -112.5f && angle < -67.5f) return "▼";
            return "◢";
        }

        private bool TryGetViewportSize(out float width, out float height)
        {
            width = 0f;
            height = 0f;

            if (_viewport == null)
                return false;

            width = _viewport.resolvedStyle.width;
            height = _viewport.resolvedStyle.height;

            if (width <= 1f) width = _viewport.layout.width;
            if (height <= 1f) height = _viewport.layout.height;

            return width > 1f && height > 1f;
        }

        private int ComputeZoomedLabelFontSize(bool selected)
        {
            if (_style == null)
                return selected ? 13 : 12;

            int baseSize = selected ? _style.labelFontSizeSelected : _style.labelFontSize;

            float z = Mathf.Max(0.0001f, _zoom);

            // Use existing setting, but in the way you actually want:
            // comp = 1 => constant size, comp = 0 => scale with zoom.
            float comp = Mathf.Clamp01(_style.labelZoomCompensation);
            float exp = 1f - comp;

            float scaled = baseSize * Mathf.Pow(z, exp);
            int size = Mathf.RoundToInt(scaled);

            int min = Mathf.Max(6, _style.labelFontMin);
            int max = Mathf.Max(min, _style.labelFontMax);
            return Mathf.Clamp(size, min, max);
        }


        private Vector2 ApplyBackgroundInset(Vector2 uv)
        {
            if (_mapData == null) return uv;

            Vector2 mn = _mapData.BackgroundUvMin;
            Vector2 mx = _mapData.BackgroundUvMax;

            // If defaults, do nothing.
            if (mn == Vector2.zero && mx == Vector2.one) return uv;

            // HARD GUARD:
            // BackgroundUvMin/Max are defined as normalized UVs (0..1).
            // If the asset contains pixel-like values (e.g. 500), treating them as UVs collapses markers to an edge.
            // In that case, ignore inset entirely.
            const float saneMin = -0.01f;
            const float saneMax = 1.01f;

            bool mnSane = (mn.x >= saneMin && mn.x <= saneMax && mn.y >= saneMin && mn.y <= saneMax);
            bool mxSane = (mx.x >= saneMin && mx.x <= saneMax && mx.y >= saneMin && mx.y <= saneMax);

            if (!mnSane || !mxSane)
                return uv;

            // Clamp & order
            mn = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mn));
            mx = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mx));

            if (mx.x < mn.x) (mn.x, mx.x) = (mx.x, mn.x);
            if (mx.y < mn.y) (mn.y, mx.y) = (mx.y, mn.y);

            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);

            return new Vector2(
                Mathf.Lerp(mn.x, mx.x, uv.x),
                Mathf.Lerp(mn.y, mx.y, uv.y)
            );
        }

        private Vector2 RemoveBackgroundInset(Vector2 uvInset)
        {
            if (_mapData == null)
                return uvInset;

            Vector2 mn = _mapData.BackgroundUvMin;
            Vector2 mx = _mapData.BackgroundUvMax;

            if (mn == Vector2.zero && mx == Vector2.one)
                return uvInset;

            const float saneMin = -0.01f;
            const float saneMax = 1.01f;

            bool mnSane = (mn.x >= saneMin && mn.x <= saneMax && mn.y >= saneMin && mn.y <= saneMax);
            bool mxSane = (mx.x >= saneMin && mx.x <= saneMax && mx.y >= saneMin && mx.y <= saneMax);

            if (!mnSane || !mxSane)
                return uvInset;

            mn = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mn));
            mx = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mx));

            if (mx.x < mn.x) (mn.x, mx.x) = (mx.x, mn.x);
            if (mx.y < mn.y) (mn.y, mx.y) = (mx.y, mn.y);

            float width = Mathf.Max(0.0001f, mx.x - mn.x);
            float height = Mathf.Max(0.0001f, mx.y - mn.y);

            return new Vector2(
                Mathf.Clamp01((uvInset.x - mn.x) / width),
                Mathf.Clamp01((uvInset.y - mn.y) / height)
            );
        }

        private bool TryContentLocalToWorld(Vector2 contentLocal, out Vector3 world)
        {
            world = default;

            if (_mapData == null || !_mapData.Projection.IsValid)
                return false;

            float w = Mathf.Max(1f, _contentSize.x);
            float h = Mathf.Max(1f, _contentSize.y);

            Vector2 uvInset = new Vector2(
                Mathf.Clamp01(contentLocal.x / w),
                Mathf.Clamp01(1f - (contentLocal.y / h))
            );

            Vector2 uv = RemoveBackgroundInset(uvInset);
            world = _mapData.Projection.NormalizedToWorld(uv, 0f);
            return true;
        }

        private void ResetViewToFit()
        {
            if (_viewport == null || _content == null) return;

            if (!TryGetViewportSize(out float vw, out float vh))
                return;

            float fit = Mathf.Min(vw / Mathf.Max(1f, _contentSize.x), vh / Mathf.Max(1f, _contentSize.y));
            fit *= 0.96f;

            _zoom = Mathf.Clamp(fit, 0.15f, 8f);

            float cw = _contentSize.x * _zoom;
            float ch = _contentSize.y * _zoom;

            _pan = new Vector2((vw - cw) * 0.5f, (vh - ch) * 0.5f);

            ApplyTransform();
            // Keep polyline widths stable in screen space.
            _polyLayer?.SetZoom(_zoom);
            _trailLayer?.SetZoom(_zoom);
        }

        private bool TryPickMarker(Vector2 contentLocal, float maxDistanceContentPx, out SkiGame.Map.MapMarker picked)
        {
            picked = default;

            float bestSqr = maxDistanceContentPx * maxDistanceContentPx;
            bool found = false;

            foreach (var kv in _markerAnchorLocal)
            {
                string id = kv.Key;

                if (!_markerById.TryGetValue(id, out var m))
                    continue;

                // Respect legend visibility.
                if (!IsMarkerVisibleById(id))
                    continue;

                // Skip legacy lift "line markers" (only allow station markers if they exist).
                if (m.type == SkiGame.POI.POIType.SkiLift)
                {
                    bool isStation = id.EndsWith("__top", StringComparison.Ordinal) || id.EndsWith("__bottom", StringComparison.Ordinal);
                    if (!isStation)
                        continue;
                }

                float dSqr = (kv.Value - contentLocal).sqrMagnitude;
                if (dSqr < bestSqr)
                {
                    bestSqr = dSqr;
                    picked = m;
                    found = true;
                }
            }

            return found;
        }

        private bool TryPickWaypointMarker(Vector2 contentLocal, float pickDistContent, out string waypointId)
        {
            waypointId = null;

            if (_waypointManager == null || _waypointAnchorLocal.Count == 0)
                return false;

            float bestSqr = pickDistContent * pickDistContent;
            bool found = false;

            foreach (var kv in _waypointAnchorLocal)
            {
                string candidateId = kv.Key;

                // Critical:
                // Waypoints linked to existing markers should NOT intercept marker clicks.
                // Those markers must remain handled by the normal marker interaction path.
                if (IsWaypointLinkedToExistingMarker(candidateId))
                    continue;

                float dSqr = (kv.Value - contentLocal).sqrMagnitude;
                if (dSqr < bestSqr)
                {
                    bestSqr = dSqr;
                    waypointId = candidateId;
                    found = true;
                }
            }

            return found;
        }

        private static float ComputePolygonAreaLocal(IReadOnlyList<Vector2> pts)
        {
            if (pts == null || pts.Count < 3)
                return 0f;

            float area = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % pts.Count];
                area += (a.x * b.y) - (b.x * a.y);
            }

            return Mathf.Abs(area * 0.5f);
        }

        private static bool ContainsPointLocal(IReadOnlyList<Vector2> polygon, Vector2 point, float edgeEpsilonLocal)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            float edgeEpsilonSq = edgeEpsilonLocal * edgeEpsilonLocal;

            bool inside = false;
            int count = polygon.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = polygon[j];
                Vector2 b = polygon[i];

                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                if (abLenSq > 0.0001f)
                {
                    float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLenSq);
                    Vector2 closest = a + ab * t;
                    if ((point - closest).sqrMagnitude <= edgeEpsilonSq)
                        return true;
                }

                bool intersect =
                    ((a.y > point.y) != (b.y > point.y)) &&
                    (point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.000001f, (b.y - a.y)) + a.x);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private static float DistanceToPolygonEdgeSqLocal(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon == null || polygon.Count < 2)
                return float.MaxValue;

            float best = float.MaxValue;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];

                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                if (abLenSq <= 0.0001f)
                {
                    float d = (point - a).sqrMagnitude;
                    if (d < best) best = d;
                    continue;
                }

                float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLenSq);
                Vector2 closest = a + ab * t;
                float distSq = (point - closest).sqrMagnitude;
                if (distSq < best) best = distSq;
            }

            return best;
        }

        private Vector2 RegionUvToLocal(Vector2 uv)
        {
            // Regions are currently drawn in raw authored UV space inside MapRegionLayer.
            // Region picking and fit-to-region must use the same space to stay accurate.
            return new Vector2(
                uv.x * _contentSize.x,
                (1f - uv.y) * _contentSize.y);
        }

        private bool TryResolveFaceLocalGeometry(MapRegionFace face, out List<Vector2> outerLocal, out List<List<Vector2>> holeLocals)
        {
            outerLocal = null;
            holeLocals = null;

            if (_regionSet == null || face == null || !face.IsValid)
                return false;

            var outerUv = MapRegionUtility.ResolveLoopUv(_regionSet, face.outerVertexIds);
            if (outerUv == null || outerUv.Count < 3)
                return false;

            outerLocal = new List<Vector2>(outerUv.Count);
            for (int i = 0; i < outerUv.Count; i++)
                outerLocal.Add(RegionUvToLocal(outerUv[i]));

            holeLocals = new List<List<Vector2>>();
            if (face.holeLoops != null)
            {
                for (int i = 0; i < face.holeLoops.Count; i++)
                {
                    var hole = face.holeLoops[i];
                    if (hole == null || !hole.IsValid)
                        continue;

                    var holeUv = MapRegionUtility.ResolveLoopUv(_regionSet, hole.vertexIds);
                    if (holeUv == null || holeUv.Count < 3)
                        continue;

                    var holeLocal = new List<Vector2>(holeUv.Count);
                    for (int h = 0; h < holeUv.Count; h++)
                        holeLocal.Add(RegionUvToLocal(holeUv[h]));

                    holeLocals.Add(holeLocal);
                }
            }

            return true;
        }

        private bool TryPickRegion(Vector2 contentLocal, out string pickedRegionId)
        {
            pickedRegionId = null;

            if (_regionLayer == null)
                return false;

            return _regionLayer.TryPickRenderedFace(contentLocal, out pickedRegionId);
        }

        private void CancelPendingMapClick()
        {
            _pendingMapClickKind = PendingMapClickKind.None;
            _pendingMapClickRegionId = null;
            _pendingMapClickDueTime = -10f;

            if (_pendingMapClickScheduledItem != null)
            {
                _pendingMapClickScheduledItem.Pause();
                _pendingMapClickScheduledItem = null;
            }
        }

        private void SchedulePendingMapClick()
        {
            if (_root == null)
                return;

            if (_pendingMapClickScheduledItem != null)
            {
                _pendingMapClickScheduledItem.Pause();
                _pendingMapClickScheduledItem = null;
            }

            int delayMs = Mathf.Max(1, Mathf.CeilToInt(DoubleClickWindowSeconds * 1000f) + 8);
            _pendingMapClickScheduledItem = _root.schedule.Execute(FlushPendingMapClickIfDue).StartingIn(delayMs);
        }

        private void QueuePendingBackgroundClick(Vector2 viewportPos, Vector3 worldPos)
        {
            _pendingMapClickKind = PendingMapClickKind.Background;
            _pendingMapClickRegionId = null;
            _pendingMapClickViewportPos = viewportPos;
            _pendingMapClickWorld = worldPos;
            _pendingMapClickDueTime = Time.unscaledTime + DoubleClickWindowSeconds;
            SchedulePendingMapClick();
        }

        private bool TryConsumePendingBackgroundDoubleClick(Vector2 viewportPos, out Vector3 worldPos)
        {
            worldPos = default;

            if (_pendingMapClickKind == PendingMapClickKind.None)
                return false;

            bool withinWindow = (Time.unscaledTime - (_pendingMapClickDueTime - DoubleClickWindowSeconds)) <= DoubleClickWindowSeconds;
            bool withinDistance = Vector2.Distance(viewportPos, _pendingMapClickViewportPos) <= DoubleClickDistancePx;

            if (!withinWindow || !withinDistance)
                return false;

            worldPos = _pendingMapClickWorld;
            CancelPendingMapClick();
            return true;
        }

        private void FlushPendingMapClickIfDue()
        {
            if (_pendingMapClickKind == PendingMapClickKind.None)
                return;

            if (Time.unscaledTime + 0.0001f < _pendingMapClickDueTime)
            {
                SchedulePendingMapClick();
                return;
            }

            var kind = _pendingMapClickKind;
            string regionId = _pendingMapClickRegionId;
            Vector3 worldPos = _pendingMapClickWorld;

            CancelPendingMapClick();

            switch (kind)
            {
                case PendingMapClickKind.Background:
                    {
                        BackgroundWorldClicked?.Invoke(worldPos);
                        ClearSelectionInternal(fireEvent: true);
                        break;
                    }
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_viewport == null || _content == null)
                return;

            if (evt.button != 0 && evt.button != 1)
                return;

            _pointerButton = evt.button;
            _pointerDown = true;
            _didDrag = false;

            Vector2 worldPos = new Vector2(evt.position.x, evt.position.y);
            _pointerDownPosViewport = _viewport.WorldToLocal(worldPos);

            _activePointerId = evt.pointerId;

            bool allowDrag = evt.button == 0 && !_lockPan;
            if (allowDrag)
            {
                _dragging = true;
                _dragStartPointer = _pointerDownPosViewport;
                _dragStartPan = _pan;
                _viewport.CapturePointer(_activePointerId);
            }
            else
            {
                _dragging = false;
            }

            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            // Dragging: existing pan logic (keep first so it wins)
            if (_dragging)
            {
                if (evt.pointerId != _activePointerId) return;

                Vector2 worldPos = new Vector2(evt.position.x, evt.position.y);
                Vector2 pos = _viewport.WorldToLocal(worldPos);
                Vector2 delta = pos - _dragStartPointer;

                if (!_didDrag && delta.magnitude > ClickDragThresholdPx)
                    _didDrag = true;

                _pan = _dragStartPan + delta;

                ApplyTransform();
                evt.StopPropagation();
                return;
            }
        }

        private void OnPointerUp(EventBase evtBase)
        {
            if (!_pointerDown && !_dragging)
                return;

            var evt = evtBase as IPointerEvent;
            if (evt != null && evt.pointerId != _activePointerId)
                return;

            if (_dragging)
            {
                _dragging = false;
                if (_viewport != null && _activePointerId != -1 && _viewport.HasPointerCapture(_activePointerId))
                    _viewport.ReleasePointer(_activePointerId);
            }

            _activePointerId = -1;

            if (_pointerDown && !_didDrag && evtBase is PointerUpEvent pu)
            {
                if (_suppressSelection)
                {
                    CancelPendingMapClick();
                    _pointerDown = false;
                    _pointerButton = -1;
                    evtBase.StopPropagation();
                    return;
                }

                Vector2 worldPos = new Vector2(pu.position.x, pu.position.y);
                Vector2 viewportPos = _viewport.WorldToLocal(worldPos);
                Vector2 contentLocal = (viewportPos - _pan) / Mathf.Max(0.0001f, _zoom);
                bool haveWorldAtClick = TryContentLocalToWorld(contentLocal, out var clickedWorld);

                const float hitDistScreenPx = 24f;
                float pickDistContent = hitDistScreenPx / Mathf.Max(0.0001f, _zoom);

                if (pu.button == 1)
                {
                    // Marker interactions must win over waypoint-marker hit tests.
                    // This ensures existing markers remain the primary interaction target.
                    if (TryPickMarker(contentLocal, pickDistContent, out var pickedMarkerRight))
                    {
                        if (TryGetLinkedWaypointIdForMarker(pickedMarkerRight.id, out var linkedWaypointIdRight))
                        {
                            float waypointRightNow = Time.unscaledTime;
                            bool waypointRightIsDouble =
                                string.Equals(_lastWaypointVisualRightClickedId, linkedWaypointIdRight, StringComparison.Ordinal) &&
                                (waypointRightNow - _lastWaypointVisualRightClickTime) <= MarkerRightDoubleClickWindowSeconds;

                            if (waypointRightIsDouble)
                            {
                                WaypointDeleteRequested?.Invoke(linkedWaypointIdRight);
                                _lastWaypointVisualRightClickedId = null;
                                _lastWaypointVisualRightClickTime = -10f;
                            }
                            else
                            {
                                WaypointClicked?.Invoke(linkedWaypointIdRight);
                                _lastWaypointVisualRightClickedId = linkedWaypointIdRight;
                                _lastWaypointVisualRightClickTime = waypointRightNow;
                            }

                            _pointerDown = false;
                            _pointerButton = -1;
                            evtBase.StopPropagation();
                            return;
                        }

                        float markerRightNow = Time.unscaledTime;
                        bool markerRightIsDouble =
                            string.Equals(_lastMarkerRightClickedId, pickedMarkerRight.id, StringComparison.Ordinal) &&
                            (markerRightNow - _lastMarkerRightClickTime) <= MarkerRightDoubleClickWindowSeconds;

                        if (markerRightIsDouble)
                        {
                            MarkerRightDoubleClicked?.Invoke(pickedMarkerRight);
                            _lastMarkerRightClickedId = null;
                            _lastMarkerRightClickTime = -10f;
                        }
                        else
                        {
                            _lastMarkerRightClickedId = pickedMarkerRight.id;
                            _lastMarkerRightClickTime = markerRightNow;
                        }

                        _pointerDown = false;
                        _pointerButton = -1;
                        evtBase.StopPropagation();
                        return;
                    }

                    if (TryPickWaypointMarker(contentLocal, pickDistContent, out var pickedWaypointIdRight))
                    {
                        float waypointNow = Time.unscaledTime;
                        bool waypointIsDouble =
                            string.Equals(_lastWaypointClickedId, pickedWaypointIdRight, StringComparison.Ordinal) &&
                            (waypointNow - _lastWaypointClickTime) <= WaypointDoubleClickWindowSeconds;

                        if (waypointIsDouble)
                        {
                            WaypointDoubleClicked?.Invoke(pickedWaypointIdRight);
                            _lastWaypointClickedId = null;
                            _lastWaypointClickTime = -10f;
                        }
                        else
                        {
                            WaypointClicked?.Invoke(pickedWaypointIdRight);
                            _lastWaypointClickedId = pickedWaypointIdRight;
                            _lastWaypointClickTime = waypointNow;
                        }

                        _pointerDown = false;
                        _pointerButton = -1;
                        evtBase.StopPropagation();
                        return;
                    }

                    if (_polyLayer != null && _polyLayer.TryPick(contentLocal, pickDistContent, out var pickedRightLine))
                    {
                        float now = Time.unscaledTime;
                        bool isDouble =
                            string.Equals(_lastPolylineRightClickedId, pickedRightLine.id, StringComparison.Ordinal) &&
                            (now - _lastPolylineRightClickTime) <= MarkerRightDoubleClickWindowSeconds;

                        if (isDouble && TryResolveSourceMarkerForPolyline(pickedRightLine.id, out var sourceMarker))
                        {
                            MarkerRightDoubleClicked?.Invoke(sourceMarker);
                            _lastPolylineRightClickedId = null;
                            _lastPolylineRightClickTime = -10f;
                        }
                        else
                        {
                            _lastPolylineRightClickedId = pickedRightLine.id;
                            _lastPolylineRightClickTime = now;
                        }

                        _pointerDown = false;
                        _pointerButton = -1;
                        evtBase.StopPropagation();
                        return;
                    }

                    _pointerDown = false;
                    _pointerButton = -1;
                    evtBase.StopPropagation();
                    return;
                }

                if (TryPickMarker(contentLocal, pickDistContent, out var pickedMarker))
                {
                    float markerNow = Time.unscaledTime;
                    bool markerIsDouble =
                        string.Equals(_lastMarkerClickedId, pickedMarker.id, StringComparison.Ordinal) &&
                        (markerNow - _lastMarkerClickTime) <= MarkerDoubleClickWindowSeconds;

                    _selectedLiftStationSuffix = null;

                    if (pickedMarker.type == SkiGame.POI.POIType.SkiRun)
                    {
                        string runPolyId = pickedMarker.id;
                        if (_markerToPolyline.TryGetValue(pickedMarker.id, out var linked))
                            runPolyId = linked;

                        if (TryGetPolylineById(runPolyId, out var runPoly))
                        {
                            _selectedMarkerId = pickedMarker.id;
                            _selectedPolylineId = runPolyId;

                            UpdateSelectionVisuals();
                            _polyLayer?.SetSelected(runPolyId);
                            PolylineSelected?.Invoke(runPoly);

                            if (markerIsDouble)
                            {
                                if (TryGetLinkedWaypointIdForMarker(pickedMarker.id, out var linkedWaypointIdLeft))
                                    WaypointDoubleClicked?.Invoke(linkedWaypointIdLeft);
                                else
                                    MarkerDoubleClicked?.Invoke(pickedMarker);
                            }

                            _lastMarkerClickedId = markerIsDouble ? null : pickedMarker.id;
                            _lastMarkerClickTime = markerIsDouble ? -10f : markerNow;

                            _pointerDown = false;
                            _pointerButton = -1;
                            evtBase.StopPropagation();
                            return;
                        }
                    }
                    else if (pickedMarker.type == SkiGame.POI.POIType.SkiLift)
                    {
                        string liftPolyId = pickedMarker.id;
                        int idx = liftPolyId.LastIndexOf("__", StringComparison.Ordinal);
                        if (idx > 0)
                            liftPolyId = liftPolyId.Substring(0, idx);

                        if (TryGetPolylineById(liftPolyId, out var liftPoly))
                        {
                            _selectedMarkerId = pickedMarker.id;
                            _selectedPolylineId = liftPolyId;

                            if (_liftMarkerRole.TryGetValue(pickedMarker.id, out var role) && role != LiftStationRole.None)
                                _selectedLiftStationSuffix = role == LiftStationRole.Top ? " (Top)" : " (Bottom)";

                            UpdateSelectionVisuals();
                            _polyLayer?.SetSelected(liftPolyId);
                            PolylineSelected?.Invoke(liftPoly);

                            if (markerIsDouble)
                            {
                                if (TryGetLinkedWaypointIdForMarker(pickedMarker.id, out var linkedWaypointIdLeft))
                                    WaypointDoubleClicked?.Invoke(linkedWaypointIdLeft);
                                else
                                    MarkerDoubleClicked?.Invoke(pickedMarker);
                            }

                            _lastMarkerClickedId = markerIsDouble ? null : pickedMarker.id;
                            _lastMarkerClickTime = markerIsDouble ? -10f : markerNow;

                            _pointerDown = false;
                            _pointerButton = -1;
                            evtBase.StopPropagation();
                            return;
                        }
                    }
                    else
                    {
                        _selectedMarkerId = pickedMarker.id;
                        _selectedPolylineId = null;

                        UpdateSelectionVisuals();
                        _polyLayer?.SetSelected(null);
                        MarkerSelected?.Invoke(pickedMarker);

                        if (markerIsDouble)
                        {
                            if (TryGetLinkedWaypointIdForMarker(pickedMarker.id, out var linkedWaypointIdLeft))
                                WaypointDoubleClicked?.Invoke(linkedWaypointIdLeft);
                            else
                                MarkerDoubleClicked?.Invoke(pickedMarker);
                        }

                        _lastMarkerClickedId = markerIsDouble ? null : pickedMarker.id;
                        _lastMarkerClickTime = markerIsDouble ? -10f : markerNow;

                        _pointerDown = false;
                        _pointerButton = -1;
                        evtBase.StopPropagation();
                        return;
                    }
                }

                if (TryPickWaypointMarker(contentLocal, pickDistContent, out var pickedWaypointId))
                {
                    float now = Time.unscaledTime;
                    bool isDouble =
                        string.Equals(_lastWaypointClickedId, pickedWaypointId, StringComparison.Ordinal) &&
                        (now - _lastWaypointClickTime) <= WaypointDoubleClickWindowSeconds;

                    if (isDouble)
                    {
                        WaypointDoubleClicked?.Invoke(pickedWaypointId);
                        _lastWaypointClickedId = null;
                        _lastWaypointClickTime = -10f;
                    }
                    else
                    {
                        WaypointClicked?.Invoke(pickedWaypointId);
                        _lastWaypointClickedId = pickedWaypointId;
                        _lastWaypointClickTime = now;
                    }

                    _pointerDown = false;
                    _pointerButton = -1;
                    evtBase.StopPropagation();
                    return;
                }

                if (_polyLayer != null && _polyLayer.TryPick(contentLocal, pickDistContent, out var pickedLine))
                {
                    float now = Time.unscaledTime;
                    bool isDouble =
                        string.Equals(_lastPolylineClickedId, pickedLine.id, StringComparison.Ordinal) &&
                        (now - _lastPolylineClickTime) <= MarkerDoubleClickWindowSeconds;

                    _selectedLiftStationSuffix = null;
                    _selectedPolylineId = pickedLine.id;
                    _selectedMarkerId = null;

                    UpdateSelectionVisuals();
                    _polyLayer.SetSelected(pickedLine.id);
                    PolylineSelected?.Invoke(pickedLine);

                    if (isDouble)
                    {
                        // Double-clicking inside a ski run corridor should place a free custom waypoint
                        // at the clicked point within the corridor, not toggle the run-start marker waypoint.
                        if (pickedLine.lineType == MapLineType.SkiRun && haveWorldAtClick)
                        {
                            BackgroundWorldDoubleClicked?.Invoke(clickedWorld);
                        }
                        else if (TryResolveSourceMarkerForPolyline(pickedLine.id, out var sourceMarker))
                        {
                            MarkerDoubleClicked?.Invoke(sourceMarker);
                        }
                    }

                    _lastPolylineClickedId = isDouble ? null : pickedLine.id;
                    _lastPolylineClickTime = isDouble ? -10f : now;

                    _pointerDown = false;
                    _pointerButton = -1;
                    evtBase.StopPropagation();
                    return;
                }

                if (haveWorldAtClick)
                {
                    if (TryConsumePendingBackgroundDoubleClick(viewportPos, out var firstClickWorld))
                    {
                        BackgroundWorldDoubleClicked?.Invoke(firstClickWorld);

                        _pointerDown = false;
                        _pointerButton = -1;
                        evtBase.StopPropagation();
                        return;
                    }

                    QueuePendingBackgroundClick(viewportPos, clickedWorld);

                    _pointerDown = false;
                    _pointerButton = -1;
                    evtBase.StopPropagation();
                    return;
                }

                CancelPendingMapClick();
                ClearSelectionInternal(fireEvent: true);
            }

            _pointerDown = false;
            _pointerButton = -1;
            evtBase.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (_viewport == null || _content == null) return;

            // Always consume wheel so the ScrollView doesn't also scroll (which looks like panning).
            evt.StopPropagation();
            evt.PreventDefault();

            if (!_allowZoom) return;

            if (!TryGetViewportSize(out float vw, out float vh))
                return;

            float prevZoom = _zoom;

            // Zoom factor (invert wheel Y for natural feel)
            float zoomFactor = Mathf.Pow(1.12f, -evt.delta.y * 0.1f);
            _zoom = Mathf.Clamp(_zoom * zoomFactor, 0.15f, 10f);

            // Zoom around the viewport center (stable "camera zoom").
            Vector2 center = new Vector2(vw * 0.5f, vh * 0.5f);

            // Keep the content point currently under the viewport center fixed in viewport space.
            Vector2 contentAtCenterBefore = (center - _pan) / Mathf.Max(0.0001f, prevZoom);
            _pan = center - contentAtCenterBefore * _zoom;

            ApplyTransform();


            // Keep polyline/trail widths stable in screen space.
            _polyLayer?.SetZoom(_zoom);
            _trailLayer?.SetZoom(_zoom);

        }

        public void SetPlayer(Transform playerTransform)
        {
            _playerTransform = playerTransform;
        }

        public void SetPlayerTracking(bool showMarker, bool drawTrail)
        {
            _trackPlayerMarker = showMarker;
            _trackPlayerTrail = drawTrail;

            if (_playerMarker != null)
                _playerMarker.style.display = (_trackPlayerMarker) ? DisplayStyle.Flex : DisplayStyle.None;

            if (_trailLayer != null)
                _trailLayer.SetEnabled(_trackPlayerTrail);
        }

        public IReadOnlyList<Vector2> GetWorldTrailXZ() => _trailWorldXZ;

        public void SetWorldTrailXZ(IReadOnlyList<Vector2> worldTrailXZ)
        {
            // Used by minimaps to display a shared breadcrumb trail.
            _trailLayer?.SetWorldTrail(worldTrailXZ);
        }


        public void SetTrailSamplingEnabled(bool enabled)
        {
            _trackPlayerTrail = enabled;

            if (!enabled)
            {
                // Prevent internal sampler state from fighting shared trails.
                _hasLastTrailSample = false;
            }
        }

        public void ClearPlayerTrail()
        {
            _trailWorldXZ.Clear();
            _hasLastTrailSample = false;
            _trailLayer?.SetWorldTrail(_trailWorldXZ);
        }

        /// <summary>
        /// Call this periodically while the map page is open.
        /// Uses unscaled time so it stays responsive while the phone is open.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!_bound) return;
            if (_mapData == null || !_mapData.Projection.IsValid) return;

            // Keep viewport sizing in sync with info panel open/close even if geometry events don't fire.
            if (_isScrollViewRoot)
            {
                if (!_infoPanelRefsResolved || (_mapBottomDock == null && _mapInfoPanel == null))
                    ResolveInfoPanelRefs();

                SyncViewportToInfoPanelState();
            }

            if (_waypointManager != null && _lastWaypointVersion != _waypointManager.Version)
            {
                _lastWaypointVersion = _waypointManager.Version;
                RefreshWaypointVisuals();
                ApplyTransform();
            }

            // Compute once; follow-centering should not be gated on marker visibility.
            bool havePlayerLocal = TryGetPlayerLocal(out Vector2 playerLocal);

            // One-shot centering request (e.g., when the map page opens).
            // This retries until the viewport has a valid size and we can project the player.
            if (_pendingCenterOnPlayer && havePlayerLocal)
            {
                if (TryGetViewportSize(out _, out _))
                {
                    if (!_pendingCenterKeepZoom)
                        ResetViewToFit();

                    _zoom = Mathf.Max(_zoom, _pendingCenterMinZoom);
                    CenterViewOnContentPoint(playerLocal);

                    _pendingCenterOnPlayer = false;
                }
            }

            // Update player marker every tick (cheap).
            if (_playerMarker != null)
            {
                if (_trackPlayerMarker && havePlayerLocal)
                {
                    _playerMarker.style.display = DisplayStyle.Flex;
                    UpdatePlayerMarkerVisual(playerLocal);
                }
                else
                {
                    _playerMarker.style.display = DisplayStyle.None;
                }
            }

            UpdateWaypointNavigationOverlay(havePlayerLocal, playerLocal);

            // Follow-player mode should always re-center (watch/tile minimaps rely on this).
            if (_minimapFollowPlayer && havePlayerLocal)
                CenterViewOnContentPoint(playerLocal);

            // If selection happened while the viewport was still expanded, wait until the info panel is open
            // (and the viewport has been reduced) before centering.
            if (_pendingCenterOnSelection)
            {
                bool panelOpen = (_mapInfoPanel != null &&
                                  _mapInfoPanel.resolvedStyle.display != DisplayStyle.None);

                if (panelOpen && TryGetSelectedAnchorLocal(out var anchorLocal))
                {
                    if (_pendingSelectionMinZoom > 0f)
                        _zoom = Mathf.Max(_zoom, _pendingSelectionMinZoom);

                    CenterViewOnContentPoint(anchorLocal);

                    _pendingCenterOnSelection = false;
                    _pendingSelectionMinZoom = -1f;
                }
            }

            // Sample trail at a throttled rate.
            if (_trackPlayerTrail)
                SamplePlayerTrail();
        }


        private bool TryGetPlayerLocal(out Vector2 playerLocal)
        {
            playerLocal = default;

            if (_playerTransform == null) return false;
            if (!TryProjectWorldToUV(_playerTransform.position, out Vector2 uv))
                return false;

            playerLocal = UVToLocal(uv);
            return true;
        }

        private float GetPlayerMarkerRenderSize()
        {
            if (_style != null)
                return Mathf.Max(8f, _style.playerMarkerSize * 1.6f);

            return 20f;
        }

        private bool TryGetPlayerFacingAngle(out float angleDeg)
        {
            angleDeg = 0f;

            if (_playerTransform == null)
                return false;

            Vector3 flatForward = Vector3.ProjectOnPlane(_playerTransform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f)
                return false;

            if (!TryProjectWorldToUV(_playerTransform.position, out Vector2 uvA))
                return false;

            if (!TryProjectWorldToUV(_playerTransform.position + flatForward.normalized * 2f, out Vector2 uvB))
                return false;

            Vector2 a = UVToLocal(uvA);
            Vector2 b = UVToLocal(uvB);
            Vector2 dir = b - a;

            if (dir.sqrMagnitude < 0.0001f)
                return false;

            angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            return true;
        }

        private void UpdatePlayerMarkerVisual(Vector2 playerLocal)
        {
            if (_playerMarker == null)
                return;

            float markerSize = GetPlayerMarkerRenderSize();
            float half = markerSize * 0.5f;

            _playerMarker.style.width = markerSize;
            _playerMarker.style.height = markerSize;
            _playerMarker.style.left = playerLocal.x - half;
            _playerMarker.style.top = playerLocal.y - half;

            if (TryGetPlayerFacingAngle(out float angleDeg))
                _playerMarker.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        public void CenterOnPlayerIfPossible(bool keepZoom = true, float minZoom = 1.0f)
        {
            if (!_bound) return;
            if (!TryGetViewportSize(out _, out _)) return;

            if (!TryGetPlayerLocal(out Vector2 playerLocal))
                return;

            if (!keepZoom)
                ResetViewToFit();

            _zoom = Mathf.Max(_zoom, minZoom);
            CenterViewOnContentPoint(playerLocal);
        }

        public void RequestCenterOnPlayer(bool keepZoom = true, float minZoom = 1.0f)
        {
            _pendingCenterOnPlayer = true;
            _pendingCenterKeepZoom = keepZoom;
            _pendingCenterMinZoom = minZoom;

            // Try immediately in case we're already laid out.
            CenterOnPlayerIfPossible(keepZoom, minZoom);
        }

        public bool SelectRegion(string regionId, bool zoomToRegion = true)
        {
            if (_regionSet == null || string.IsNullOrWhiteSpace(regionId))
                return false;

            var face = _regionSet.GetFaceById(regionId);
            if (face == null || !face.IsValid)
                return false;

            _selectedRegionId = regionId;
            _regionLayer?.SetSelected(regionId);

            foreach (var kv in _regionOverlayLabels)
                ApplyRegionOverlayLabelVisual(kv.Key, string.Equals(kv.Key, regionId, StringComparison.Ordinal));

            LayoutOverlayLabels();

            if (!zoomToRegion)
            {
                LayoutOverlayLabels();
                return true;
            }

            if (!TryGetViewportSize(out float vw, out float vh))
                return true;

            var ptsUv = MapRegionUtility.ResolveLoopUv(_regionSet, face.outerVertexIds);
            if (ptsUv == null || ptsUv.Count == 0)
                return true;

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < ptsUv.Count; i++)
            {
                Vector2 p = RegionUvToLocal(ptsUv[i]);
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }

            float w = Mathf.Max(1f, maxX - minX);
            float h = Mathf.Max(1f, maxY - minY);

            float pad = 40f;
            float zoomX = (vw - pad * 2f) / w;
            float zoomY = (vh - pad * 2f) / h;
            _zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), 0.15f, 10f);

            Vector2 centerLocal = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            CenterViewOnContentPoint(centerLocal);

            LayoutOverlayLabels();
            return true;
        }

        private void CenterViewOnContentPoint(Vector2 contentLocal)
        {
            // In this implementation, _viewport is the only stable "view rect" we can trust.
            // Embedded minimaps still provide MapViewport (it just lives under a different root).
            if (_viewport == null) return;

            if (!TryGetViewportSize(out float vw, out float vh))
                return;

            Vector2 center = new Vector2(vw * 0.5f, vh * 0.5f);

            // Pan so the content point lands at the viewport center.
            _pan = center - contentLocal * _zoom;

            // Clamp only when content exceeds viewport (prevents zoom drift due to forced centering)
            ClampPanToBounds(vw, vh);

            ApplyTransform();
        }

        /// <summary>
        /// Centers the viewport on a world-space position (using the same projection as markers).
        /// This does NOT change zoom by default; it only pans so the target is centered.
        /// </summary>
        public bool CenterOnWorldPosition(Vector3 worldPosition, float minZoom = -1f)
        {
            if (!_bound) return false;
            if (_viewport == null) return false;

            // Ensure we have a valid viewport rect; otherwise centering will noop.
            float vw = _viewport.resolvedStyle.width;
            float vh = _viewport.resolvedStyle.height;
            if (vw <= 1f || vh <= 1f) return false;

            if (!TryProjectWorldToUV(worldPosition, out Vector2 uv))
                return false;

            Vector2 contentLocal = UVToLocal(uv);

            // Optional: enforce a minimum zoom (useful if you want a marker selection to "snap in").
            if (minZoom > 0f)
                _zoom = Mathf.Max(_zoom, minZoom);

            CenterViewOnContentPoint(contentLocal);
            return true;
        }

        private void ClampPanToBounds(float vw, float vh)
        {
            float scaledW = _contentSize.x * _zoom;
            float scaledH = _contentSize.y * _zoom;

            // IMPORTANT:
            // Only clamp axes where the content is larger than the viewport.
            // If the content is smaller, clamping would "recenter" it and cause drift when zooming.
            if (scaledW > vw)
            {
                float minX = vw - scaledW; // leftmost
                float maxX = 0f;          // rightmost
                _pan.x = Mathf.Clamp(_pan.x, minX, maxX);
            }

            if (scaledH > vh)
            {
                float minY = vh - scaledH; // topmost
                float maxY = 0f;           // bottommost
                _pan.y = Mathf.Clamp(_pan.y, minY, maxY);
            }
        }

        private void SamplePlayerTrail()
        {
            if (_playerTransform == null) return;
            if (_trailLayer == null) return;

            float now = Time.unscaledTime;
            if (now < _nextTrailSampleTime) return;

            _nextTrailSampleTime = now + Mathf.Max(0.02f, _trailSampleInterval);

            Vector2 worldXZ = new Vector2(_playerTransform.position.x, _playerTransform.position.z);

            if (_hasLastTrailSample)
            {
                float d = Vector2.Distance(worldXZ, _lastTrailSampleWorldXZ);
                if (d < _trailMinDistanceMeters)
                    return;
            }

            _hasLastTrailSample = true;
            _lastTrailSampleWorldXZ = worldXZ;

            _trailWorldXZ.Add(worldXZ);

            // Cap memory
            if (_trailWorldXZ.Count > _trailMaxSamples)
                _trailWorldXZ.RemoveRange(0, _trailWorldXZ.Count - _trailMaxSamples);

            _trailLayer.SetWorldTrail(_trailWorldXZ);
        }

        /// <summary>
        /// Programmatically selects a polyline by id and raises the same selection callback
        /// used for user clicks. Useful when navigating from the Runs page.
        /// </summary>
        public bool SelectPolylineById(string polylineId, bool center = true, float minZoom = 1.25f)
        {
            if (!_bound) return false;
            if (_mapData == null || string.IsNullOrWhiteSpace(polylineId)) return false;

            var list = _mapData.Polylines;
            if (list == null) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (!p.IsValid) continue;
                if (p.id != polylineId) continue;

                if (center)
                {
                    if (p.Has3DPoints && p.pointsWorld.Count > 0)
                    {
                        int mid = p.pointsWorld.Count / 2;
                        CenterOnWorldPosition(p.pointsWorld[mid], minZoom);
                    }
                    else if (p.HasXZPoints && p.pointsWorldXZ.Count > 0)
                    {
                        int mid = p.pointsWorldXZ.Count / 2;
                        var w = p.pointsWorldXZ[mid];
                        CenterOnWorldPosition(new Vector3(w.x, 0f, w.y), minZoom);
                    }
                }

                SelectPolylineInternal(polylineId, p, fireEvent: true);
                return true;
            }

            return false;
        }

        public bool SelectMarkerById(string markerId, bool center = true, float minZoom = 1.25f)
        {
            if (!_bound) return false;
            if (_mapData == null || string.IsNullOrWhiteSpace(markerId)) return false;

            var list = _mapData.Markers;
            if (list == null) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.IsValid) continue;
                if (!string.Equals(m.id, markerId, StringComparison.Ordinal)) continue;

                _selectedLiftStationSuffix = null;

                // Runs: selecting the marker should behave like selecting the linked polyline.
                if (m.type == SkiGame.POI.POIType.SkiRun)
                {
                    string runPolyId = markerId;
                    if (_markerToPolyline.TryGetValue(markerId, out var linked))
                        runPolyId = linked;

                    if (TryGetPolylineById(runPolyId, out var runPoly))
                    {
                        _selectedMarkerId = markerId;
                        _selectedPolylineId = runPolyId;

                        UpdateSelectionVisuals();
                        _polyLayer?.SetSelected(runPolyId);

                        if (center)
                            CenterOnWorldPosition(ResolveEffectiveMarkerWorldPosition(m), minZoom);

                        PolylineSelected?.Invoke(runPoly);
                        return true;
                    }
                }

                // Lifts: selecting the station marker should behave like selecting the lift polyline.
                if (m.type == SkiGame.POI.POIType.SkiLift)
                {
                    string liftPolyId = markerId;
                    if (!_liftMarkerToPolyline.TryGetValue(markerId, out liftPolyId))
                    {
                        int idx = markerId.LastIndexOf("__", StringComparison.Ordinal);
                        liftPolyId = idx > 0 ? markerId.Substring(0, idx) : markerId;
                    }

                    if (TryGetPolylineById(liftPolyId, out var liftPoly))
                    {
                        _selectedMarkerId = markerId;
                        _selectedPolylineId = liftPolyId;

                        if (_liftMarkerRole.TryGetValue(markerId, out var role) && role != LiftStationRole.None)
                            _selectedLiftStationSuffix = role == LiftStationRole.Top ? " (Top)" : " (Bottom)";

                        UpdateSelectionVisuals();
                        _polyLayer?.SetSelected(liftPolyId);

                        if (center)
                            CenterOnWorldPosition(ResolveEffectiveMarkerWorldPosition(m), minZoom);

                        PolylineSelected?.Invoke(liftPoly);
                        return true;
                    }
                }

                // General POIs stay as marker selection.
                _selectedMarkerId = markerId;
                _selectedPolylineId = null;

                UpdateSelectionVisuals();
                _polyLayer?.SetSelected(null);

                if (center)
                    CenterOnWorldPosition(ResolveEffectiveMarkerWorldPosition(m), minZoom);

                MarkerSelected?.Invoke(m);
                return true;
            }

            return false;
        }

        public void PreviewLiftAccessForPassLevel(int passLevel)
        {
            if (!_bound)
                return;

            passLevel = Mathf.Max(0, passLevel);

            // Reapply only lift overrides for preview.
            foreach (var kvp in _liftRequiredLevelByPolyline)
            {
                string polyId = kvp.Key;
                int required = kvp.Value;

                Color baseColor = new Color(0.75f, 0.85f, 1f, 0.70f);
                if (_liftRequiredColorByPolyline.TryGetValue(polyId, out var c))
                    baseColor = c;

                bool accessible = passLevel >= required;
                _polylineColorOverrides[polyId] = accessible
                    ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f)
                    : new Color(0.28f, 0.32f, 0.38f, 0.22f);
            }

            _polyLayer?.SetColorOverrides(_polylineColorOverrides);
            _polyHost?.MarkDirtyRepaint();
        }

        /// <summary>
        /// Painter2D layer for region outlines + fills.
        /// Regions are most visible when zoomed out and fade toward transparent as zoom increases.
        /// </summary>
        private sealed class MapRegionLayer : VisualElement
        {
            private MapRegionSet _regionSet;
            private Vector2 _contentSize;
            private float _zoom = 1f;
            private string _selectedRegionId;
            private MapUIStyleSettings _style;

            private struct CachedFace
            {
                public string id;
                public Color fill;
                public Color border;
                public List<Vector2> outerLocalPts;
                public List<List<Vector2>> holeLocalPts;
                public float area;
            }

            private readonly List<CachedFace> _cache = new();

            public MapRegionLayer()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0;
                style.top = 0;
                style.right = 0;
                style.bottom = 0;
                generateVisualContent += OnGenerate;
            }

            public void SetData(MapRegionSet regionSet, Vector2 contentSize)
            {
                _regionSet = regionSet;
                _contentSize = contentSize;
                RebuildCache();
                MarkDirtyRepaint();
            }

            public void SetZoom(float zoom)
            {
                _zoom = Mathf.Max(0.0001f, zoom);
                MarkDirtyRepaint();
            }

            public void SetSelected(string regionId)
            {
                _selectedRegionId = regionId;
                MarkDirtyRepaint();
            }

            public void SetStyle(MapUIStyleSettings style)
            {
                _style = style;
                MarkDirtyRepaint();
            }

            private static bool ContainsPointLocal(IReadOnlyList<Vector2> polygon, Vector2 point, float edgeEpsilonLocal)
            {
                if (polygon == null || polygon.Count < 3)
                    return false;

                float edgeEpsilonSq = edgeEpsilonLocal * edgeEpsilonLocal;

                bool inside = false;
                int count = polygon.Count;

                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    Vector2 a = polygon[j];
                    Vector2 b = polygon[i];

                    Vector2 ab = b - a;
                    float abLenSq = ab.sqrMagnitude;
                    if (abLenSq > 0.0001f)
                    {
                        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLenSq);
                        Vector2 closest = a + ab * t;
                        if ((point - closest).sqrMagnitude <= edgeEpsilonSq)
                            return true;
                    }

                    bool intersect =
                        ((a.y > point.y) != (b.y > point.y)) &&
                        (point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.000001f, (b.y - a.y)) + a.x);

                    if (intersect)
                        inside = !inside;
                }

                return inside;
            }

            private static float ComputePolygonAreaLocal(IReadOnlyList<Vector2> pts)
            {
                if (pts == null || pts.Count < 3)
                    return 0f;

                float area = 0f;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[(i + 1) % pts.Count];
                    area += (a.x * b.y) - (b.x * a.y);
                }

                return Mathf.Abs(area * 0.5f);
            }

            public bool TryPickRenderedFace(Vector2 contentLocal, out string regionId)
            {
                regionId = null;

                if (_cache.Count == 0)
                    return false;

                // Keep the edge tolerance stable in screen space.
                float edgeEpsilonLocal = Mathf.Max(0.35f, 6f / Mathf.Max(0.0001f, _zoom));

                // IMPORTANT:
                // Pick against the same geometry that is actually rendered on screen.
                // Since this Unity version fills only the outer polygon (no hole subtraction),
                // area picking must also use the rendered outer polygons to avoid "random" picks.
                //
                // Iterate from the end because _cache is sorted large->small and later faces draw on top.
                for (int i = _cache.Count - 1; i >= 0; i--)
                {
                    var face = _cache[i];
                    if (face.outerLocalPts == null || face.outerLocalPts.Count < 3)
                        continue;

                    if (ContainsPointLocal(face.outerLocalPts, contentLocal, edgeEpsilonLocal))
                    {
                        regionId = face.id;
                        return true;
                    }
                }

                return false;
            }

            private void RebuildCache()
            {
                _cache.Clear();

                if (_regionSet == null || _regionSet.Faces == null)
                    return;

                for (int i = 0; i < _regionSet.Faces.Count; i++)
                {
                    var face = _regionSet.Faces[i];
                    if (face == null || !face.IsValid)
                        continue;

                    var uvPts = MapRegionUtility.ResolveLoopUv(_regionSet, face.outerVertexIds);
                    if (uvPts == null || uvPts.Count < 3)
                        continue;

                    List<Vector2> outerLocalPts = new List<Vector2>(uvPts.Count);
                    for (int p = 0; p < uvPts.Count; p++)
                    {
                        Vector2 uv = uvPts[p];
                        outerLocalPts.Add(new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y));
                    }

                    List<List<Vector2>> holeLocalPts = new List<List<Vector2>>();
                    if (face.holeLoops != null)
                    {
                        for (int h = 0; h < face.holeLoops.Count; h++)
                        {
                            var hole = face.holeLoops[h];
                            if (hole == null || !hole.IsValid)
                                continue;

                            var holeUv = MapRegionUtility.ResolveLoopUv(_regionSet, hole.vertexIds);
                            if (holeUv == null || holeUv.Count < 3)
                                continue;

                            List<Vector2> holeLocal = new List<Vector2>(holeUv.Count);
                            for (int p = 0; p < holeUv.Count; p++)
                            {
                                Vector2 uv = holeUv[p];
                                holeLocal.Add(new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y));
                            }

                            holeLocalPts.Add(holeLocal);
                        }
                    }

                    _cache.Add(new CachedFace
                    {
                        id = face.id,
                        fill = face.fillColor,
                        border = face.borderColor,
                        outerLocalPts = outerLocalPts,
                        holeLocalPts = holeLocalPts,
                        area = Mathf.Abs(MapRegionUtility.ComputeSignedArea(uvPts))
                    });
                }

                // Draw larger regions first, smaller regions on top.
                _cache.Sort((a, b) => b.area.CompareTo(a.area));
            }

            private void OnGenerate(MeshGenerationContext ctx)
            {
                if (_cache.Count == 0)
                    return;

                float overviewAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.60f, 1.15f, _zoom));
                float fillAlphaMul = overviewAlpha;
                float lineAlphaMul = overviewAlpha;

                float baseOutlineWidth = 1.6f / Mathf.Max(0.0001f, _zoom);
                float selectedOutlineWidth = 3.0f / Mathf.Max(0.0001f, _zoom);

                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;

                for (int i = 0; i < _cache.Count; i++)
                {
                    var face = _cache[i];
                    var outer = face.outerLocalPts;
                    if (outer == null || outer.Count < 3)
                        continue;

                    bool selected = !string.IsNullOrEmpty(_selectedRegionId) &&
                                    string.Equals(_selectedRegionId, face.id, StringComparison.Ordinal);

                    Color fill = face.fill;
                    fill.a *= selected ? Mathf.Max(0.22f, fillAlphaMul) : fillAlphaMul;

                    // Fallback rendering path for Unity versions without Painter2D.fillRule:
                    // fill only the outer polygon. Holes are still respected by picking,
                    // and selected hole borders are still stroked below.
                    if (fill.a > 0.001f)
                    {
                        p.fillColor = fill;
                        p.BeginPath();
                        AppendClosedPath(p, outer);
                        p.Fill();
                    }

                    Color border = face.border;
                    border.a *= selected ? Mathf.Max(0.90f, lineAlphaMul) : lineAlphaMul;

                    p.strokeColor = border;
                    p.lineWidth = selected ? selectedOutlineWidth : baseOutlineWidth;

                    StrokeClosed(p, outer);

                    if (selected && face.holeLocalPts != null)
                    {
                        for (int h = 0; h < face.holeLocalPts.Count; h++)
                        {
                            var hole = face.holeLocalPts[h];
                            if (hole == null || hole.Count < 3)
                                continue;

                            StrokeClosed(p, hole);
                        }
                    }
                }
            }

            private static void AppendClosedPath(Painter2D p, List<Vector2> pts)
            {
                if (pts == null || pts.Count < 2)
                    return;

                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Count; i++)
                    p.LineTo(pts[i]);
                p.ClosePath();
            }

            private static void StrokeClosed(Painter2D p, List<Vector2> pts)
            {
                if (pts == null || pts.Count < 2)
                    return;

                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Count; i++)
                    p.LineTo(pts[i]);
                p.ClosePath();
                p.Stroke();
            }

            private static List<int> TriangulatePolygon(List<Vector2> polygon)
            {
                List<int> result = new List<int>();
                if (polygon == null || polygon.Count < 3)
                    return result;

                List<int> indices = new List<int>(polygon.Count);
                for (int i = 0; i < polygon.Count; i++)
                    indices.Add(i);

                bool isClockwise = MapRegionUtility.ComputeSignedArea(polygon) < 0f;

                int guard = 0;
                while (indices.Count > 3 && guard < 4096)
                {
                    guard++;
                    bool earFound = false;

                    for (int i = 0; i < indices.Count; i++)
                    {
                        int prev = indices[(i - 1 + indices.Count) % indices.Count];
                        int curr = indices[i];
                        int next = indices[(i + 1) % indices.Count];

                        Vector2 a = polygon[prev];
                        Vector2 b = polygon[curr];
                        Vector2 c = polygon[next];

                        if (!IsEar(a, b, c, polygon, indices, prev, curr, next, isClockwise))
                            continue;

                        result.Add(prev);
                        result.Add(curr);
                        result.Add(next);
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }

                    if (!earFound)
                        break;
                }

                if (indices.Count == 3)
                {
                    result.Add(indices[0]);
                    result.Add(indices[1]);
                    result.Add(indices[2]);
                }

                return result;
            }

            private static bool IsEar(
                Vector2 a,
                Vector2 b,
                Vector2 c,
                List<Vector2> polygon,
                List<int> activeIndices,
                int prev,
                int curr,
                int next,
                bool isClockwise)
            {
                float cross = Cross(b - a, c - b);
                if (isClockwise ? cross > 0f : cross < 0f)
                    return false;

                for (int i = 0; i < activeIndices.Count; i++)
                {
                    int idx = activeIndices[i];
                    if (idx == prev || idx == curr || idx == next)
                        continue;

                    if (PointInTriangle(polygon[idx], a, b, c))
                        return false;
                }

                return true;
            }

            private static float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }

            private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = Sign(p, a, b);
                float d2 = Sign(p, b, c);
                float d3 = Sign(p, c, a);

                bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
                bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

                return !(hasNeg && hasPos);
            }

            private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            }
        }

        /// <summary>
        /// Painter2D polyline renderer + hit testing.
        /// </summary>
        private sealed class MapPolylineLayer : VisualElement
        {
            private MapData _data;
            private Vector2 _contentSize;
            private Camera _cam;
            private float _zoom = 1f;

            private string _selectedId;
            private MapUIStyleSettings _style;
            private bool _showRuns = true;
            private bool _showLifts = true;
            private int _maxRunDifficultyRank = 3;

            private struct CachedLine
            {
                public MapPolyline src;
                public List<Vector2> localPts;
                public Rect bounds;
            }

            private struct CachedRunCorridor
            {
                public MapRunCorridor src;
                public List<Vector2> localPolygon;
                public Rect bounds;
            }

            private readonly List<CachedLine> _cache = new();
            private readonly List<CachedRunCorridor> _runCache = new();

            private IReadOnlyDictionary<string, Color> _colorOverrides;

            public MapPolylineLayer()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0;
                style.top = 0;
                style.right = 0;
                style.bottom = 0;
                generateVisualContent += OnGenerate;
            }

            public void SetData(MapData data, Vector2 contentSize, Camera cam)
            {
                _data = data;
                _contentSize = contentSize;
                _cam = cam;
                RebuildCache();
                MarkDirtyRepaint();
            }

            public void SetZoom(float zoom)
            {
                _zoom = Mathf.Max(0.0001f, zoom);
                MarkDirtyRepaint();
            }

            public void SetSelected(string id)
            {
                _selectedId = id;
                MarkDirtyRepaint();
            }

            public void SetFilterState(bool showRuns, bool showLifts, int maxRunDifficultyRank)
            {
                _showRuns = showRuns;
                _showLifts = showLifts;
                _maxRunDifficultyRank = Mathf.Max(0, maxRunDifficultyRank);
                MarkDirtyRepaint();
            }

            public void SetColorOverrides(IReadOnlyDictionary<string, Color> overrides)
            {
                _colorOverrides = overrides;
                MarkDirtyRepaint();
            }

            private bool IsVisible(MapPolyline line)
            {
                if (line.lineType == MapLineType.SkiRun)
                    return _showRuns && line.difficultyRank <= _maxRunDifficultyRank;

                if (line.lineType == MapLineType.SkiLift)
                    return _showLifts;

                return true;
            }

            private bool IsVisible(MapRunCorridor corridor)
            {
                return _showRuns && corridor.difficultyRank <= _maxRunDifficultyRank;
            }

            public bool TryPick(Vector2 contentLocal, float maxDistancePx, out MapPolyline picked)
            {
                picked = default;

                // Runs: pick against baked corridor fills first.
                if (_showRuns && TryPickRunCorridor(contentLocal, out string runId))
                {
                    if (TryGetPolylineById(runId, out picked))
                        return true;
                }

                // Lifts / fallback polylines: pick by segment distance.
                float bestSqr = maxDistancePx * maxDistancePx;
                int bestIdx = -1;

                for (int i = 0; i < _cache.Count; i++)
                {
                    var c = _cache[i];
                    if (!IsVisible(c.src))
                        continue;

                    if (c.src.lineType == MapLineType.SkiRun && HasRunCorridor(c.src.id))
                        continue;

                    Rect b = c.bounds;
                    b.xMin -= maxDistancePx;
                    b.yMin -= maxDistancePx;
                    b.xMax += maxDistancePx;
                    b.yMax += maxDistancePx;

                    if (!b.Contains(contentLocal))
                        continue;

                    var pts = c.localPts;
                    if (pts == null || pts.Count < 2)
                        continue;

                    for (int k = 0; k < pts.Count - 1; k++)
                    {
                        float dSqr = DistPointToSegmentSqr(contentLocal, pts[k], pts[k + 1]);
                        if (dSqr < bestSqr)
                        {
                            bestSqr = dSqr;
                            bestIdx = i;
                        }
                    }
                }

                if (bestIdx >= 0)
                {
                    picked = _cache[bestIdx].src;
                    return true;
                }

                return false;
            }

            private void OnGenerate(MeshGenerationContext ctx)
            {
                if (_data == null)
                    return;

                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;

                DrawRunCorridors(p);
                DrawRemainingPolylines(p);
            }

            private void DrawRunCorridors(Painter2D p)
            {
                if (!_showRuns || _runCache.Count == 0)
                    return;

                float outlineWidth = 1.8f / Mathf.Max(0.0001f, _zoom);
                float selectedOutlineWidth = 3.1f / Mathf.Max(0.0001f, _zoom);

                for (int i = 0; i < _runCache.Count; i++)
                {
                    var c = _runCache[i];
                    if (!c.src.IsValid || c.localPolygon == null || c.localPolygon.Count < 3)
                        continue;

                    if (!IsVisible(c.src))
                        continue;

                    bool selected = !string.IsNullOrEmpty(_selectedId) &&
                                    string.Equals(_selectedId, c.src.id, StringComparison.Ordinal);

                    Color baseColor = (c.src.color.a <= 0.001f)
                        ? new Color(1f, 1f, 1f, 0.30f)
                        : c.src.color;

                    Color fill = new Color(
                        baseColor.r,
                        baseColor.g,
                        baseColor.b,
                        selected ? 0.42f : 0.24f);

                    Color outline = _style != null
                        ? (selected ? _style.outlineColorSelected : new Color(baseColor.r, baseColor.g, baseColor.b, 0.92f))
                        : (selected ? new Color(1f, 1f, 1f, 0.95f) : new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f));

                    AppendClosedPath(p, c.localPolygon);
                    p.fillColor = fill;
                    p.Fill();

                    p.strokeColor = outline;
                    p.lineWidth = selected ? selectedOutlineWidth : outlineWidth;
                    StrokeClosed(p, c.localPolygon);
                }
            }

            private void DrawRemainingPolylines(Painter2D p)
            {
                for (int i = 0; i < _cache.Count; i++)
                {
                    var c = _cache[i];
                    var line = c.src;

                    if (!IsVisible(line))
                        continue;

                    if (line.lineType == MapLineType.SkiRun && HasRunCorridor(line.id))
                        continue;

                    var pts = c.localPts;
                    if (pts == null || pts.Count < 2)
                        continue;

                    float runW = _style != null ? _style.runWidthPx : 2.8f;
                    float liftW = _style != null ? _style.liftWidthPx : 2.1f;

                    float screenW = (line.lineType == MapLineType.SkiLift) ? liftW : runW;
                    float zoomMul = 1f;
                    if (_style != null)
                    {
                        float exp = Mathf.Clamp01(_style.polylineWidthZoomExponent);
                        zoomMul = Mathf.Pow(Mathf.Max(0.0001f, _zoom), exp);
                        zoomMul = Mathf.Clamp(zoomMul, _style.polylineWidthZoomMinMul, _style.polylineWidthZoomMaxMul);
                    }

                    float w = (screenW * zoomMul) / _zoom;
                    bool selected = (!string.IsNullOrEmpty(_selectedId) && line.id == _selectedId);
                    float selMul = _style != null ? _style.selectedWidthMultiplier : 1.35f;

                    Color outlineCol = _style != null
                        ? (selected ? _style.outlineColorSelected : _style.outlineColor)
                        : new Color(0f, 0f, 0f, selected ? 0.55f : 0.35f);

                    float outlineExtra = _style != null
                        ? (selected ? _style.outlineExtraSelectedPx : _style.outlineExtraPx)
                        : (selected ? 3.0f : 2.0f);

                    p.strokeColor = outlineCol;
                    p.lineWidth = ((screenW + outlineExtra) * zoomMul) / _zoom;
                    Stroke(p, pts);

                    Color baseColor = (line.color.a <= 0.001f) ? new Color(1f, 1f, 1f, 0.70f) : line.color;
                    if (_colorOverrides != null && _colorOverrides.TryGetValue(line.id, out var ov))
                        baseColor = ov;

                    p.strokeColor = selected ? new Color(baseColor.r, baseColor.g, baseColor.b, 1f) : baseColor;
                    p.lineWidth = selected ? (w * selMul) : w;
                    Stroke(p, pts);
                }
            }

            private void RebuildCache()
            {
                _cache.Clear();
                _runCache.Clear();

                if (_data == null)
                    return;

                var lines = _data.Polylines;
                if (lines != null)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (!line.IsValid)
                            continue;

                        int estimatedCount = line.Has3DPoints ? line.pointsWorld.Count : (line.HasXZPoints ? line.pointsWorldXZ.Count : 0);
                        if (estimatedCount < 2)
                            continue;

                        var localPts = new List<Vector2>(estimatedCount);
                        Rect bounds = MakeEmptyBounds();

                        if (line.Has3DPoints)
                        {
                            for (int k = 0; k < line.pointsWorld.Count; k++)
                            {
                                if (!TryProjectWorldToUV(line.pointsWorld[k], out Vector2 uv))
                                    continue;

                                uv = ApplyInset(uv, _data.BackgroundUvMin, _data.BackgroundUvMax);
                                Vector2 local = new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y);
                                localPts.Add(local);
                                ExpandBounds(ref bounds, local);
                            }
                        }
                        else
                        {
                            for (int k = 0; k < line.pointsWorldXZ.Count; k++)
                            {
                                if (!TryProjectWorldXZToUV(line.pointsWorldXZ[k], out Vector2 uv))
                                    continue;

                                uv = ApplyInset(uv, _data.BackgroundUvMin, _data.BackgroundUvMax);
                                Vector2 local = new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y);
                                localPts.Add(local);
                                ExpandBounds(ref bounds, local);
                            }
                        }

                        if (localPts.Count < 2)
                            continue;

                        _cache.Add(new CachedLine
                        {
                            src = line,
                            localPts = localPts,
                            bounds = bounds
                        });
                    }
                }

                var corridors = _data.RunCorridors;
                if (corridors != null)
                {
                    for (int i = 0; i < corridors.Count; i++)
                    {
                        var corridor = corridors[i];
                        if (!corridor.IsValid || corridor.polygonWorldXZ == null || corridor.polygonWorldXZ.Count < 3)
                            continue;

                        List<Vector2> localPoly = new List<Vector2>(corridor.polygonWorldXZ.Count);
                        Rect bounds = MakeEmptyBounds();

                        for (int p = 0; p < corridor.polygonWorldXZ.Count; p++)
                        {
                            if (!TryProjectWorldXZToUV(corridor.polygonWorldXZ[p], out Vector2 uv))
                                continue;

                            uv = ApplyInset(uv, _data.BackgroundUvMin, _data.BackgroundUvMax);
                            Vector2 local = new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y);
                            localPoly.Add(local);
                            ExpandBounds(ref bounds, local);
                        }

                        if (localPoly.Count < 3)
                            continue;

                        _runCache.Add(new CachedRunCorridor
                        {
                            src = corridor,
                            localPolygon = localPoly,
                            bounds = bounds
                        });
                    }
                }
            }

            private bool TryPickRunCorridor(Vector2 contentLocal, out string runId)
            {
                runId = null;

                for (int i = _runCache.Count - 1; i >= 0; i--)
                {
                    var c = _runCache[i];

                    if (!IsVisible(c.src))
                        continue;

                    Rect b = c.bounds;

                    if (!b.Contains(contentLocal))
                        continue;

                    if (ContainsPointLocal(c.localPolygon, contentLocal))
                    {
                        runId = c.src.id;
                        return true;
                    }
                }

                return false;
            }

            private bool HasRunCorridor(string runId)
            {
                for (int i = 0; i < _runCache.Count; i++)
                {
                    if (string.Equals(_runCache[i].src.id, runId, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            private bool TryGetPolylineById(string id, out MapPolyline poly)
            {
                poly = default;

                if (_data == null || string.IsNullOrWhiteSpace(id) || _data.Polylines == null)
                    return false;

                var lines = _data.Polylines;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (string.Equals(lines[i].id, id, StringComparison.Ordinal))
                    {
                        poly = lines[i];
                        return true;
                    }
                }

                return false;
            }

            private bool TryProjectWorldToUV(Vector3 worldPos, out Vector2 uv)
            {
                if (_cam != null && _data != null && _data.PreferCameraProjection)
                {
                    Vector3 vp = _cam.WorldToViewportPoint(worldPos);
                    if (vp.z >= 0f)
                    {
                        Vector2 camUV = new Vector2(vp.x, vp.y);
                        const float tol = 0.05f;
                        if (camUV.x >= -tol && camUV.x <= 1f + tol && camUV.y >= -tol && camUV.y <= 1f + tol)
                        {
                            uv = camUV;
                            return true;
                        }
                    }
                }

                if (_data != null)
                {
                    uv = _data.WorldToMapUV(worldPos);
                    return true;
                }

                uv = default;
                return false;
            }

            private bool TryProjectWorldXZToUV(Vector2 worldXZ, out Vector2 uv)
            {
                if (_cam != null && _data != null && _data.PreferCameraProjection)
                {
                    Vector3 vp = _cam.WorldToViewportPoint(new Vector3(worldXZ.x, 0f, worldXZ.y));
                    if (vp.z >= 0f)
                    {
                        Vector2 camUV = new Vector2(vp.x, vp.y);
                        const float tol = 0.05f;
                        if (camUV.x >= -tol && camUV.x <= 1f + tol && camUV.y >= -tol && camUV.y <= 1f + tol)
                        {
                            uv = camUV;
                            return true;
                        }
                    }
                }

                if (_data != null)
                {
                    uv = _data.WorldXZToMapUV(worldXZ);
                    return true;
                }

                uv = default;
                return false;
            }

            private static Vector2 ApplyInset(Vector2 uv, Vector2 mn, Vector2 mx)
            {
                if (mn == Vector2.zero && mx == Vector2.one) return uv;

                const float saneMin = -0.01f;
                const float saneMax = 1.01f;

                bool mnSane = (mn.x >= saneMin && mn.x <= saneMax && mn.y >= saneMin && mn.y <= saneMax);
                bool mxSane = (mx.x >= saneMin && mx.x <= saneMax && mx.y >= saneMin && mx.y <= saneMax);
                if (!mnSane || !mxSane) return uv;

                mn = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mn));
                mx = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mx));
                if (mx.x < mn.x) (mn.x, mx.x) = (mx.x, mn.x);
                if (mx.y < mn.y) (mn.y, mx.y) = (mx.y, mn.y);

                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);

                return new Vector2(
                    Mathf.Lerp(mn.x, mx.x, uv.x),
                    Mathf.Lerp(mn.y, mx.y, uv.y)
                );
            }

            private static void AppendClosedPath(Painter2D p, List<Vector2> pts)
            {
                if (pts == null || pts.Count < 3)
                    return;

                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Count; i++)
                    p.LineTo(pts[i]);
                p.ClosePath();
            }

            private static void StrokeClosed(Painter2D p, List<Vector2> pts)
            {
                AppendClosedPath(p, pts);
                p.Stroke();
            }

            private static void Stroke(Painter2D p, List<Vector2> pts)
            {
                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Count; i++)
                    p.LineTo(pts[i]);
                p.Stroke();
            }

            private static Rect MakeEmptyBounds()
            {
                return new Rect(float.PositiveInfinity, float.PositiveInfinity, 0f, 0f);
            }

            private static void ExpandBounds(ref Rect bounds, Vector2 point)
            {
                if (bounds.xMin == float.PositiveInfinity)
                {
                    bounds = new Rect(point.x, point.y, 0f, 0f);
                    return;
                }

                bounds.xMin = Mathf.Min(bounds.xMin, point.x);
                bounds.yMin = Mathf.Min(bounds.yMin, point.y);
                bounds.xMax = Mathf.Max(bounds.xMax, point.x);
                bounds.yMax = Mathf.Max(bounds.yMax, point.y);
            }

            private static bool ContainsPointLocal(IReadOnlyList<Vector2> polygon, Vector2 point)
            {
                if (polygon == null || polygon.Count < 3)
                    return false;

                bool inside = false;
                int count = polygon.Count;

                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    Vector2 a = polygon[j];
                    Vector2 b = polygon[i];

                    bool intersect =
                        ((a.y > point.y) != (b.y > point.y)) &&
                        (point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.000001f, (b.y - a.y)) + a.x);

                    if (intersect)
                        inside = !inside;
                }

                return inside;
            }

            private static float DistPointToSegmentSqr(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float denom = ab.sqrMagnitude;
                if (denom < 1e-6f) return (p - a).sqrMagnitude;

                float t = Vector2.Dot(p - a, ab) / denom;
                t = Mathf.Clamp01(t);
                Vector2 proj = a + ab * t;
                return (p - proj).sqrMagnitude;
            }

            public void SetStyle(MapUIStyleSettings style)
            {
                _style = style;
                MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Painter2D layer for drawing the player's recent movement trail.
        /// </summary>
        private sealed class MapTrailLayer : VisualElement
        {
            private MapData _data;
            private Vector2 _contentSize;
            private Camera _cam;
            private float _zoom = 1f;

            private bool _enabled = true;

            private IReadOnlyList<Vector2> _worldTrail;
            private readonly List<Vector2> _localPts = new();

            public MapTrailLayer()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0;
                style.top = 0;
                style.right = 0;
                style.bottom = 0;
                generateVisualContent += OnGenerate;
            }

            public void SetData(MapData data, Vector2 contentSize, Camera cam)
            {
                _data = data;
                _contentSize = contentSize;
                _cam = cam;
                RebuildLocal();
                MarkDirtyRepaint();
            }

            public void SetZoom(float zoom)
            {
                _zoom = Mathf.Max(0.0001f, zoom);
                MarkDirtyRepaint();
            }

            public void SetEnabled(bool enabled)
            {
                _enabled = enabled;
                MarkDirtyRepaint();
            }

            public void SetWorldTrail(IReadOnlyList<Vector2> worldXZ)
            {
                _worldTrail = worldXZ;
                RebuildLocal();
                MarkDirtyRepaint();
            }

            private void RebuildLocal()
            {
                _localPts.Clear();
                if (_data == null) return;
                if (_worldTrail == null) return;
                if (_worldTrail.Count < 2) return;

                for (int i = 0; i < _worldTrail.Count; i++)
                {
                    if (!TryProjectWorldToUV(new Vector3(_worldTrail[i].x, 0f, _worldTrail[i].y), out Vector2 uv))
                        continue;

                    uv = ApplyInset(uv, _data.BackgroundUvMin, _data.BackgroundUvMax);
                    _localPts.Add(new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y));
                }
            }

            private void OnGenerate(MeshGenerationContext ctx)
            {
                if (!_enabled) return;
                if (_localPts.Count < 2) return;

                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;

                // Slightly thinner than runs; stable in screen space.
                float screenW = 2.0f;
                float w = screenW / _zoom;

                // Outline
                p.strokeColor = new Color(0f, 0f, 0f, 0.40f);
                p.lineWidth = (screenW + 2.0f) / _zoom;
                Stroke(p, _localPts);

                // Main (cyan-ish, matches player dot)
                p.strokeColor = Color.mediumSlateBlue;
                p.lineWidth = w;
                Stroke(p, _localPts);
            }

            private bool TryProjectWorldToUV(Vector3 worldPos, out Vector2 uv)
            {
                if (_cam != null && _data != null && _data.PreferCameraProjection)
                {
                    Vector3 vp = _cam.WorldToViewportPoint(worldPos);
                    if (vp.z >= 0f)
                    {
                        Vector2 camUV = new Vector2(vp.x, vp.y);
                        const float tol = 0.05f;
                        if (camUV.x >= -tol && camUV.x <= 1f + tol && camUV.y >= -tol && camUV.y <= 1f + tol)
                        {
                            uv = camUV;
                            return true;
                        }
                    }
                }

                if (_data != null)
                {
                    uv = _data.WorldToMapUV(worldPos);
                    return true;
                }

                uv = default;
                return false;
            }

            private bool TryProjectWorldXZToUV(Vector2 worldXZ, out Vector2 uv)
            {
                if (_cam != null && _data != null && _data.PreferCameraProjection)
                {
                    Vector3 vp = _cam.WorldToViewportPoint(new Vector3(worldXZ.x, 0f, worldXZ.y));
                    if (vp.z >= 0f)
                    {
                        Vector2 camUV = new Vector2(vp.x, vp.y);
                        const float tol = 0.05f;
                        if (camUV.x >= -tol && camUV.x <= 1f + tol && camUV.y >= -tol && camUV.y <= 1f + tol)
                        {
                            uv = camUV;
                            return true;
                        }
                    }
                }

                if (_data != null)
                {
                    uv = _data.WorldXZToMapUV(worldXZ);
                    return true;
                }

                uv = default;
                return false;
            }

            private static Vector2 ApplyInset(Vector2 uv, Vector2 mn, Vector2 mx)
            {
                if (mn == Vector2.zero && mx == Vector2.one) return uv;

                const float saneMin = -0.01f;
                const float saneMax = 1.01f;

                bool mnSane = (mn.x >= saneMin && mn.x <= saneMax && mn.y >= saneMin && mn.y <= saneMax);
                bool mxSane = (mx.x >= saneMin && mx.x <= saneMax && mx.y >= saneMin && mx.y <= saneMax);
                if (!mnSane || !mxSane) return uv;

                mn = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mn));
                mx = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, mx));
                if (mx.x < mn.x) (mn.x, mx.x) = (mx.x, mn.x);
                if (mx.y < mn.y) (mn.y, mx.y) = (mx.y, mn.y);

                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);

                return new Vector2(
                    Mathf.Lerp(mn.x, mx.x, uv.x),
                    Mathf.Lerp(mn.y, mx.y, uv.y)
                );
            }

            private static void Stroke(Painter2D p, List<Vector2> pts)
            {
                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Count; i++)
                    p.LineTo(pts[i]);
                p.Stroke();
            }
        }

        public void ApplyStyle(MapUIStyleSettings style)
        {
            _style = style;
            _regionLayer?.SetStyle(style);
            _polyLayer?.SetStyle(style);
            ApplyPlayerMarkerStyle();
            _dirty = true;
        }

        public void SetPOIRegistry(PointOfInterestRegistry registry)
        {
            _poiRegistry = registry;
            _dirty = true;
        }

        public void SetRegionSet(MapRegionSet regionSet)
        {
            _regionSet = regionSet;

            if (_regionSet != null)
            {
                Debug.Log(
                    $"[PhoneMapPageUI] Region set applied: {_regionSet.name}, faces={(_regionSet.Faces != null ? _regionSet.Faces.Count : 0)}, " +
                    $"mapData={(_regionSet.MapData != null ? _regionSet.MapData.name : "null")}");
            }
            else
            {
                Debug.LogWarning("[PhoneMapPageUI] SetRegionSet called with null.");
            }

            _regionLayer?.SetData(_regionSet, _contentSize);
            RebuildRegionOverlayLabels();
            _dirty = true;
            LayoutOverlayLabels();
        }

        private void ApplyPlayerMarkerStyle()
        {
            if (_playerMarker == null)
                return;

            float s = Mathf.Max(16f, (_style != null ? _style.playerMarkerSize * 2.0f : 26f));
            _playerMarker.style.width = s;
            _playerMarker.style.height = s;
            _playerMarker.style.backgroundColor = Color.clear;

            Color bodyColor = _style != null ? _style.playerMarkerColor : Color.mediumSlateBlue;
            Color outlineColor = _style != null ? _style.playerBorderColor : new Color(0f, 0f, 0f, 0.75f);

            var baseCircle = _playerMarker.Q<VisualElement>("MapPlayerMarkerBase");
            var arrowBody = _playerMarker.Q<VisualElement>("MapPlayerMarkerArrow");
            var arrowOutline = _playerMarker.Q<VisualElement>("MapPlayerMarkerArrowOutline");
            var centerDot = _playerMarker.Q<VisualElement>("MapPlayerMarkerCenter");

            if (baseCircle != null)
            {
                float circleSize = s * 0.42f;
                float circleLeft = (s - circleSize) * 0.5f;
                float circleTop = s * 0.54f;

                baseCircle.style.width = circleSize;
                baseCircle.style.height = circleSize;
                baseCircle.style.left = circleLeft;
                baseCircle.style.top = circleTop;
                baseCircle.style.backgroundColor = bodyColor;

                float border = Mathf.Max(1.5f, (_style != null ? _style.playerBorderWidth : 2f));
                baseCircle.style.borderLeftWidth = border;
                baseCircle.style.borderRightWidth = border;
                baseCircle.style.borderTopWidth = border;
                baseCircle.style.borderBottomWidth = border;
                baseCircle.style.borderLeftColor = outlineColor;
                baseCircle.style.borderRightColor = outlineColor;
                baseCircle.style.borderTopColor = outlineColor;
                baseCircle.style.borderBottomColor = outlineColor;
            }

            if (arrowOutline != null)
            {
                float outlineHalfWidth = s * 0.38f;
                float outlineHeight = s * 0.62f;

                arrowOutline.style.left = (s * 0.5f) - outlineHalfWidth;
                arrowOutline.style.top = -s * 0.04f;
                arrowOutline.style.borderLeftWidth = outlineHalfWidth;
                arrowOutline.style.borderRightWidth = outlineHalfWidth;
                arrowOutline.style.borderBottomWidth = outlineHeight;
                arrowOutline.style.borderBottomColor = outlineColor;
            }

            if (arrowBody != null)
            {
                float bodyHalfWidth = s * 0.30f;
                float bodyHeight = s * 0.52f;

                arrowBody.style.left = (s * 0.5f) - bodyHalfWidth;
                arrowBody.style.top = s * 0.04f;
                arrowBody.style.borderLeftWidth = bodyHalfWidth;
                arrowBody.style.borderRightWidth = bodyHalfWidth;
                arrowBody.style.borderBottomWidth = bodyHeight;
                arrowBody.style.borderBottomColor = bodyColor;
            }

            if (centerDot != null)
            {
                float dotSize = Mathf.Max(3f, s * 0.14f);
                centerDot.style.width = dotSize;
                centerDot.style.height = dotSize;
                centerDot.style.left = (s - dotSize) * 0.5f;
                centerDot.style.top = s * 0.68f;
                centerDot.style.backgroundColor = Color.white;
            }
        }

        private void ApplyMarkerVisual(string id, bool selected)
        {
            if (_style == null) return;
            if (!_markerVisuals.TryGetValue(id, out var v)) return;

            float s = Mathf.Max(1f, selected ? _style.markerSizeSelected : _style.markerSize);
            v.dot.style.width = s;
            v.dot.style.height = s;

            float bw = Mathf.Max(0f, _style.markerBorderWidth);
            v.dot.style.borderLeftWidth = bw;
            v.dot.style.borderRightWidth = bw;
            v.dot.style.borderTopWidth = bw;
            v.dot.style.borderBottomWidth = bw;

            v.dot.style.borderLeftColor = _style.markerBorderColor;
            v.dot.style.borderRightColor = _style.markerBorderColor;
            v.dot.style.borderTopColor = _style.markerBorderColor;
            v.dot.style.borderBottomColor = _style.markerBorderColor;

            v.dot.style.borderTopLeftRadius = 999;
            v.dot.style.borderTopRightRadius = 999;
            v.dot.style.borderBottomLeftRadius = 999;
            v.dot.style.borderBottomRightRadius = 999;

            v.label.style.fontSize = selected ? _style.labelFontSizeSelected : _style.labelFontSize;

            v.label.style.unityFontStyleAndWeight = selected ? _style.labelFontStyleSelected : _style.labelFontStyle;
            v.label.style.color = selected ? _style.labelColorSelected : _style.labelColor;

            // Accent stripe + plate tint to visually link label to marker.
            if (_markerLabelAccent.TryGetValue(id, out var accent))
            {
                float stripeW = Mathf.Max(1f, _style.labelAccentStripeWidth);
                v.label.style.borderLeftWidth = stripeW;
                v.label.style.borderLeftColor = accent;

                float a = Mathf.Clamp01(_style.labelPlateAlpha) * 0.22f; // subtle tint
                v.label.style.backgroundColor = new Color(accent.r, accent.g, accent.b, a);
            }

        }

        private void ApplyWaypointVisual(string id, bool selected, bool active)
        {
            if (!_waypointMarkerDots.TryGetValue(id, out var dot) || dot == null)
                return;

            float size = 16f;
            float borderWidth = 2f;
            Color borderColor = new Color(0f, 0f, 0f, 0.70f);

            if (_style != null)
            {
                size = selected ? _style.waypointMarkerSizeSelected : _style.waypointMarkerSize;
                borderWidth = active
                    ? _style.waypointMarkerBorderWidthActive
                    : _style.waypointMarkerBorderWidth;
                borderColor = active
                    ? _style.waypointMarkerBorderColorActive
                    : _style.waypointMarkerBorderColor;
            }
            else
            {
                if (selected)
                    size = 20f;

                if (active)
                    borderColor = Color.white;
            }

            size = Mathf.Max(1f, size);
            borderWidth = Mathf.Max(0f, borderWidth);

            dot.style.width = size;
            dot.style.height = size;

            dot.style.borderLeftWidth = borderWidth;
            dot.style.borderRightWidth = borderWidth;
            dot.style.borderTopWidth = borderWidth;
            dot.style.borderBottomWidth = borderWidth;

            dot.style.borderLeftColor = borderColor;
            dot.style.borderRightColor = borderColor;
            dot.style.borderTopColor = borderColor;
            dot.style.borderBottomColor = borderColor;

            dot.style.borderTopLeftRadius = 999f;
            dot.style.borderTopRightRadius = 999f;
            dot.style.borderBottomLeftRadius = 999f;
            dot.style.borderBottomRightRadius = 999f;
        }

        private void ApplyPolylineLabelVisual(string id, bool selected)
        {
            if (_style == null) return;
            if (!_polylineLabelVisuals.TryGetValue(id, out var label)) return;

            label.style.fontSize = ComputeZoomedLabelFontSize(selected);
            label.style.unityFontStyleAndWeight = selected ? _style.labelFontStyleSelected : _style.labelFontStyle;

            Color textColor = selected ? _style.labelColorSelected : _style.labelColor;

            if (TryGetLinkedWaypointForPolyline(id, out var linkedWp))
                textColor = linkedWp.color;

            label.style.color = textColor;

            if (_polylineLabelAccent != null && _polylineLabelAccent.TryGetValue(id, out var accent))
            {
                float stripeW = Mathf.Max(1f, _style.labelAccentStripeWidth);
                label.style.borderLeftWidth = stripeW;
                label.style.borderLeftColor = accent;

                float a = Mathf.Clamp01(_style.labelPlateAlpha) * 0.22f;
                label.style.backgroundColor = new Color(accent.r, accent.g, accent.b, a);
            }

            label.style.opacity = 1f;
        }

        private Color GetPolylineDisplayColor(SkiGame.Map.MapPolyline p)
        {
            // Prefer any runtime override (eg liftline colour derived from station markers / registry).
            if (_polylineColorOverrides != null && _polylineColorOverrides.TryGetValue(p.id, out var ov))
                return new Color(ov.r, ov.g, ov.b, 1f);

            // Otherwise use the polyline's baked color. If none provided, fall back to your drawing default.
            Color c = p.color;
            if (c.a <= 0.001f)
                c = new Color(1f, 1f, 1f, 0.70f);

            // Accent/label stripe should be opaque even if the line is semi-transparent.
            return new Color(c.r, c.g, c.b, 1f);
        }

        private void UpdateSelectionVisuals()
        {
            _selectedMarkerSet.Clear();

            if (!string.IsNullOrEmpty(_selectedMarkerId))
                _selectedMarkerSet.Add(_selectedMarkerId);

            if (!string.IsNullOrEmpty(_selectedPolylineId) && TryGetPolylineById(_selectedPolylineId, out var p))
            {
                if (p.lineType == MapLineType.SkiRun)
                {
                    if (_polylineToMarker.TryGetValue(_selectedPolylineId, out var mid))
                        _selectedMarkerSet.Add(mid);
                }
                else if (p.lineType == MapLineType.SkiLift)
                {
                    if (_liftPolylineToMarkers.TryGetValue(_selectedPolylineId, out var mids))
                        for (int i = 0; i < mids.Count; i++)
                            _selectedMarkerSet.Add(mids[i]);
                }
            }

            if (!string.IsNullOrEmpty(_selectedMarkerId) && _liftMarkerToPolyline.TryGetValue(_selectedMarkerId, out var liftPid))
            {
                if (_liftPolylineToMarkers.TryGetValue(liftPid, out var mids))
                    for (int i = 0; i < mids.Count; i++)
                        _selectedMarkerSet.Add(mids[i]);
            }

            foreach (var id in _lastMarkerSet)
                if (!_selectedMarkerSet.Contains(id))
                    ApplyMarkerVisual(id, selected: false);

            foreach (var id in _selectedMarkerSet)
                ApplyMarkerVisual(id, selected: true);

            if (_lastSelectedPolylineId != null && _lastSelectedPolylineId != _selectedPolylineId)
                ApplyPolylineLabelVisual(_lastSelectedPolylineId, selected: false);

            if (_selectedPolylineId != null)
                ApplyPolylineLabelVisual(_selectedPolylineId, selected: true);

            // Style POI overlay labels (selected vs not)
            foreach (var kv in _poiOverlayLabels)
                ApplyPOIOverlayLabelVisual(kv.Key, selected: _selectedMarkerSet.Contains(kv.Key));

            _lastMarkerSet.Clear();
            foreach (var id in _selectedMarkerSet) _lastMarkerSet.Add(id);

            _lastSelectedPolylineId = _selectedPolylineId;

            _polyLayer?.SetSelected(_selectedPolylineId);

            // Re-run overlay placement so selected label wins
            LayoutOverlayLabels();
        }

        private bool IsWorldMarkerPositionUsable(Vector3 p)
        {
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                return false;

            if (_mapData != null)
            {
                Vector2 uv = _mapData.WorldToMapUV(p);
                // allow a little margin outside bounds, but reject extreme nonsense
                if (uv.x < -0.25f || uv.x > 1.25f || uv.y < -0.25f || uv.y > 1.25f)
                    return false;
            }

            return true;
        }

        private bool TryResolveRunAnchorFromPolyline(SkiGame.Map.MapMarker marker, out Vector3 world)
        {
            world = default;

            string polyId = null;

            // Run marker IDs are typically "{runId}__top", while the polyline ID is the runId.
            if (!string.IsNullOrWhiteSpace(marker.id))
            {
                int idx = marker.id.LastIndexOf("__", StringComparison.Ordinal);
                polyId = idx > 0 ? marker.id.Substring(0, idx) : marker.id;
            }

            if (string.IsNullOrWhiteSpace(polyId) || !TryGetPolylineById(polyId, out var poly) || !poly.IsValid)
                return false;

            if (poly.Has3DPoints && poly.pointsWorld.Count > 0)
            {
                world = poly.pointsWorld[0];
                return true;
            }

            var pts = poly.pointsWorldXZ;
            if (pts == null || pts.Count == 0)
                return false;

            Vector2 p = pts[0];
            world = new Vector3(p.x, marker.worldPosition.y, p.y);
            return true;
        }

        private bool TryResolveLiftAnchorFromPolyline(SkiGame.Map.MapMarker marker, out Vector3 world)
        {
            world = default;

            string polyId = null;

            if (_liftMarkerToPolyline.TryGetValue(marker.id, out var linked))
                polyId = linked;
            else if (!string.IsNullOrWhiteSpace(marker.id))
            {
                int idx = marker.id.LastIndexOf("__", StringComparison.Ordinal);
                polyId = idx > 0 ? marker.id.Substring(0, idx) : marker.id;
            }

            if (string.IsNullOrWhiteSpace(polyId) || !TryGetPolylineById(polyId, out var poly) || !poly.IsValid)
                return false;

            if (poly.Has3DPoints && poly.pointsWorld.Count >= 2)
            {
                if (_liftMarkerRole.TryGetValue(marker.id, out var role))
                {
                    world = role == LiftStationRole.Top ? poly.pointsWorld[poly.pointsWorld.Count - 1] : poly.pointsWorld[0];
                    return true;
                }

                Vector3 a3 = poly.pointsWorld[0];
                Vector3 b3 = poly.pointsWorld[poly.pointsWorld.Count - 1];
                world = (a3 + b3) * 0.5f;
                return true;
            }

            var pts = poly.pointsWorldXZ;
            if (pts == null || pts.Count < 2)
                return false;

            if (_liftMarkerRole.TryGetValue(marker.id, out var role2))
            {
                Vector2 p = role2 == LiftStationRole.Top ? pts[pts.Count - 1] : pts[0];
                world = new Vector3(p.x, marker.worldPosition.y, p.y);
                return true;
            }

            Vector2 a = pts[0];
            Vector2 b = pts[pts.Count - 1];
            Vector2 mid = (a + b) * 0.5f;
            world = new Vector3(mid.x, marker.worldPosition.y, mid.y);
            return true;
        }

        private bool TryResolveSourceMarkerForPolyline(string polylineId, out MapMarker marker)
        {
            marker = default;

            if (string.IsNullOrWhiteSpace(polylineId) || !TryGetPolylineById(polylineId, out var poly))
                return false;

            if (poly.lineType == MapLineType.SkiRun)
            {
                if (_polylineToMarker.TryGetValue(polylineId, out var runMarkerId) &&
                    _markerById.TryGetValue(runMarkerId, out marker) &&
                    marker.IsValid)
                {
                    return true;
                }

                return false;
            }

            if (poly.lineType == MapLineType.SkiLift)
            {
                if (_liftPolylineToMarkers.TryGetValue(polylineId, out var liftMarkerIds) &&
                    liftMarkerIds != null &&
                    liftMarkerIds.Count > 0)
                {
                    string preferredId = null;

                    for (int i = 0; i < liftMarkerIds.Count; i++)
                    {
                        string id = liftMarkerIds[i];
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        if (_liftMarkerRole.TryGetValue(id, out var role) && role == LiftStationRole.Bottom)
                        {
                            preferredId = id;
                            break;
                        }

                        if (preferredId == null)
                            preferredId = id;
                    }

                    if (!string.IsNullOrWhiteSpace(preferredId) &&
                        _markerById.TryGetValue(preferredId, out marker) &&
                        marker.IsValid)
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private Vector2 ContentToViewport(Vector2 contentLocal)
        {
            return contentLocal * _zoom + _pan;
        }

        private static Vector2 GetRayRectIntersection(Vector2 center, Vector2 dir, float minX, float maxX, float minY, float maxY)
        {
            float tx = float.MaxValue;
            float ty = float.MaxValue;

            if (Mathf.Abs(dir.x) > 0.0001f)
            {
                float targetX = dir.x > 0f ? maxX : minX;
                tx = (targetX - center.x) / dir.x;
            }

            if (Mathf.Abs(dir.y) > 0.0001f)
            {
                float targetY = dir.y > 0f ? maxY : minY;
                ty = (targetY - center.y) / dir.y;
            }

            float t = Mathf.Min(tx, ty);
            if (float.IsInfinity(t) || float.IsNaN(t) || t < 0f)
                t = 0f;

            Vector2 hit = center + dir * t;
            hit.x = Mathf.Clamp(hit.x, minX, maxX);
            hit.y = Mathf.Clamp(hit.y, minY, maxY);
            return hit;
        }

        private void EnsureWaypointArrowElement(string waypointId)
        {
            if (_navigationOverlay == null || string.IsNullOrWhiteSpace(waypointId) || _waypointNavArrows.ContainsKey(waypointId))
                return;

            var arrow = new Label("▲");
            arrow.name = $"WaypointNavArrow_{waypointId}";
            arrow.pickingMode = PickingMode.Ignore;
            arrow.style.position = Position.Absolute;
            arrow.style.fontSize = 18f;
            arrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            arrow.style.display = DisplayStyle.None;

            _waypointNavArrows[waypointId] = arrow;
            _navigationOverlay.Add(arrow);
        }

        private void SyncWaypointArrowPool()
        {
            if (_navigationOverlay == null)
                return;

            var valid = new HashSet<string>();

            if (_waypointManager != null && _waypointManager.Waypoints != null)
            {
                for (int i = 0; i < _waypointManager.Waypoints.Count; i++)
                {
                    string id = _waypointManager.Waypoints[i].id;
                    valid.Add(id);
                    EnsureWaypointArrowElement(id);
                }
            }

            List<string> stale = null;
            foreach (var kv in _waypointNavArrows)
            {
                if (!valid.Contains(kv.Key))
                {
                    stale ??= new List<string>();
                    stale.Add(kv.Key);
                }
            }

            if (stale == null)
                return;

            for (int i = 0; i < stale.Count; i++)
            {
                string id = stale[i];
                if (_waypointNavArrows.TryGetValue(id, out var arrow) && arrow != null)
                    arrow.RemoveFromHierarchy();

                _waypointNavArrows.Remove(id);
            }
        }

        private void UpdateWaypointNavigationOverlay(bool havePlayerLocal, Vector2 playerLocal)
        {
            if (_navigationOverlay == null)
                return;

            SyncWaypointArrowPool();

            foreach (var kv in _waypointNavArrows)
            {
                if (kv.Value != null)
                    kv.Value.style.display = DisplayStyle.None;
            }

            _selectedWaypointConnector?.Hide();

            if (!havePlayerLocal || _waypointManager == null || _waypointManager.Waypoints == null)
                return;

            float viewportWidth = Mathf.Max(1f, _viewport.resolvedStyle.width);
            float viewportHeight = Mathf.Max(1f, _viewport.resolvedStyle.height);

            Vector2 playerViewport = ContentToViewport(playerLocal);

            if (_minimapFollowPlayer)
            {
                const float orbitRadius = 26f;
                const float edgePadding = 14f;

                float minX = edgePadding;
                float maxX = viewportWidth - edgePadding;
                float minY = edgePadding;
                float maxY = viewportHeight - edgePadding;

                for (int i = 0; i < _waypointManager.Waypoints.Count; i++)
                {
                    var wp = _waypointManager.Waypoints[i];
                    if (!_waypointNavArrows.TryGetValue(wp.id, out var arrow) || arrow == null)
                        continue;

                    if (!TryProjectWorldToLocal(wp.worldPosition, out var waypointLocal))
                        continue;

                    Vector2 waypointViewport = ContentToViewport(waypointLocal);
                    Vector2 dir = waypointViewport - playerViewport;
                    float sqr = dir.sqrMagnitude;
                    if (sqr < 4f)
                        continue;

                    Vector2 n = dir.normalized;

                    bool waypointVisible =
                        waypointViewport.x >= minX && waypointViewport.x <= maxX &&
                        waypointViewport.y >= minY && waypointViewport.y <= maxY;

                    Vector2 arrowPos = waypointVisible
                        ? playerViewport + n * orbitRadius
                        : GetRayRectIntersection(playerViewport, n, minX, maxX, minY, maxY);

                    arrow.text = GetArrowGlyph(n);
                    arrow.style.left = arrowPos.x - 8f;
                    arrow.style.top = arrowPos.y - 10f;
                    arrow.style.color = wp.color;
                    arrow.style.display = DisplayStyle.Flex;
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(_waypointManager.SelectedWaypointId) &&
                _waypointManager.TryGetWaypoint(_waypointManager.SelectedWaypointId, out var selectedWp) &&
                TryProjectWorldToLocal(selectedWp.worldPosition, out var selectedLocal))
            {
                Vector2 targetViewport = ContentToViewport(selectedLocal);
                _selectedWaypointConnector?.Show(playerViewport, targetViewport, selectedWp.color);
            }
        }

        private Vector3 ResolveSourceAnchor(SkiGame.Map.MapMarker marker, Vector3 fallback)
        {
            if (_poiRegistry != null && _poiRegistry.TryGetById(marker.id, out var liveInfo))
            {
                if (liveInfo.source is Component src && src != null)
                {
                    var renderer = src.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                        return renderer.bounds.center;

                    var collider = src.GetComponentInChildren<Collider>();
                    if (collider != null)
                        return collider.bounds.center;
                }

                return liveInfo.position;
            }

            return fallback;
        }
    }
}
