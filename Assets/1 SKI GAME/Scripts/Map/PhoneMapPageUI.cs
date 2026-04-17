using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Map;
using SkiGame.Runs;
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

        [SerializeField, Min(1f)] private float _centerOnPlayerButtonMinZoom = 2.4f;

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

        // Trail samples are stored in map UV space so they stay aligned with both
        // bounds-based and camera-projected maps when shared across views.
        private readonly List<Vector2> _trailWorldXZ = new();
        private readonly List<float> _trailSampleTimes = new();
        private readonly List<float> _trailSampleDistances = new();
        private float _nextTrailSampleTime;
        private Vector2 _lastTrailSampleWorldXZ;
        private bool _hasLastTrailSample;
        private float _trailDistanceTravelledMeters;

        private bool _trackPlayerMarker = true;
        private bool _trackPlayerTrail = true;

        private float _trailSampleInterval = 0.20f;
        private float _trailMinDistanceMeters = 1.5f;
        private int _trailMaxSamples = 1500;
        private float _playerMarkerAngleDeg;
        private bool _hasPlayerMarkerAngle;
        private const float PlayerMarkerSmoothingPerSecond = 14f;
        private const float PlayerMarkerSnapAngleDelta = 90f;
        private const float PlayerMarkerIgnoreAngleDelta = 0.6f;
        private const float PlayerMarkerForwardProbeMeters = 6f;

        // -------------------------
        // Minimap / embedded-map mode
        // -------------------------
        private bool _minimapFollowPlayer = false;
        private bool _lockPan = false;
        private bool _allowZoom = true;
        private bool _suppressSelection = false;
        private bool _hideMarkerLabels = false;

        private Func<SkiGame.Map.MapMarker, bool> _markerSelectionFilter;
        private Func<SkiGame.Map.MapPolyline, bool> _polylineSelectionFilter;

        // -------------------------
        // Map legend / filters
        // -------------------------
        private VisualElement _layerBar;
        private VisualElement _btnLayerRuns;
        private VisualElement _btnLayerLifts;
        private VisualElement _btnLayerPOIs;

        private readonly HashSet<POICategory> _visiblePOICategories = new();
        private readonly HashSet<int> _visibleRunDifficultyRanks = new() { 0, 1, 2, 3 };

        private bool _legendDefaultsInitialized;

        private VisualElement _btnLayerRaces;
        private VisualElement _btnLayerMedicTents;
        private VisualElement _btnLayerSnowmobiles;

        private bool _showRaceStartOverlays = true;
        private bool _showMedicTentOverlays = true;
        private bool _showSnowmobileOverlays = true;

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
        private bool _showSkiRunOverlays = true;
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

        private MapUIStyleSettings _style;
        private PointOfInterestRegistry _poiRegistry;
        private int _appliedStyleRevision = -1;

        // Cache created UI elements so we can restyle them when selection changes
        private readonly Dictionary<string, (VisualElement dot, VisualElement spriteOutline, VisualElement spriteBody, Label glyph, Label label)> _markerVisuals = new();
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
        private readonly Dictionary<string, string> _liftRequiredPassIdByPolyline = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> _liftRequiredColorByPolyline = new();

        private string _lastSelectedPolylineId;

        private const float DefaultRegionOverviewFadeStartZoom = 1.10f;
        private const float DefaultRegionOverviewFadeEndZoom = 2.20f;
        private const float DefaultPoiLabelRevealZoom = 2.10f;
        private const float DefaultRunLiftLabelRevealZoom = 0.72f;
        private const int DefaultMaxContextRunLiftLabels = 30;
        private const int DefaultMaxContextPoiLabels = 12;
        private const float DefaultMarkerFadeInStartZoom = 0.95f;
        private const float DefaultMarkerFadeInEndZoom = 2.10f;

        private const float DefaultTrailMaxAgeSeconds = 45f;
        private const float DefaultTrailMaxDistanceMeters = 450f;
        private const float DefaultTrailBreakDistanceMeters = 60f;
        private const float DefaultTrailOutlierWorldDistanceMeters = 45f;
        private const float DefaultTrailOutlierUvDistance = 0.12f;
        private const float DefaultTrailFadePower = 1.35f;
        private const float DefaultTrailMinAlpha = 0.08f;
        private static readonly Color DefaultTrailColor = new Color(0.5176471f, 0.4392157f, 1f, 0.92f);
        private static readonly Color DefaultTrailOutlineColor = new Color(0f, 0f, 0f, 0.40f);

        private float GetRegionOverviewFadeStartZoom() => DefaultRegionOverviewFadeStartZoom;
        private float GetRegionOverviewFadeEndZoom() => DefaultRegionOverviewFadeEndZoom;
        private float GetPoiLabelRevealZoom() => DefaultPoiLabelRevealZoom;
        private float GetRunLiftLabelRevealZoom() => DefaultRunLiftLabelRevealZoom;
        private int GetMaxContextRunLiftLabels() => DefaultMaxContextRunLiftLabels;
        private int GetMaxContextPoiLabels() => DefaultMaxContextPoiLabels;
        private float GetMarkerFadeInStartZoom() => DefaultMarkerFadeInStartZoom;
        private float GetMarkerFadeInEndZoom() => DefaultMarkerFadeInEndZoom;

        private struct ResolvedMapLabelRule
        {
            public MapUIStyleSettings.MapElementSemantic semantic;
            public MapUIStyleSettings.MapLabelDisplayMode markerDisplayMode;
            public string labelStyleId;
            public float labelSizeMultiplier;
        }

        private struct ResolvedMarkerStyleRule
        {
            public bool hasMarkerSprite;
            public Sprite markerSprite;
            public float markerSizeMultiplier;
            public bool hasDefaultColor;
            public Color defaultColor;
            public bool colorIsFallbackOnly;
        }

        private struct ResolvedMarkerRule
        {
            public Sprite sprite;
            public float sizeMultiplier;
            public bool hasDefaultColor;
            public Color defaultColor;
            public bool colorIsFallbackOnly;
        }

        // -------------------------
        // Linked selection (Runs + Lifts)
        // -------------------------

        private enum LiftStationRole { None = 0, Bottom = 1, Top = 2 }

        // lift station marker(s) <-> lift polyline (multiple markers: top + bottom + optional “label marker”)
        private readonly Dictionary<string, string> _liftMarkerToPolyline = new();
        private readonly Dictionary<string, List<string>> _liftPolylineToMarkers = new();
        private readonly Dictionary<string, LiftStationRole> _liftMarkerRole = new();
        private readonly Dictionary<string, LiftLine> _liveLiftByPolylineId = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _forcedLiftRequiredLevelByPolyline = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _forcedLiftRequiredPassIdByPolyline = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> _forcedLiftColorByPolyline = new(StringComparer.OrdinalIgnoreCase);

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
        private readonly Dictionary<string, MapUIStyleSettings.MapLabelDisplayMode> _poiLabelDisplayOverrides = new();

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

        private readonly Dictionary<string, Rect> _waypointLabelViewportRects = new();
        private readonly Dictionary<string, Vector2> _waypointLabelScreenOffsets = new();

        private float _lastWaypointLayoutZoom = -1f;
        private Vector2 _lastWaypointLayoutPan = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        private float _lastWaypointLayoutTime = -10f;
        private bool _waypointLabelLayoutDirty = true;

        private TextField _activeWaypointRenameField;
        private string _activeWaypointRenameId;
        private Vector2 _activeWaypointRenameScreenOffset = new Vector2(0f, -28f);

        private const float WaypointLayoutZoomThreshold = 0.10f;
        private const float WaypointLayoutPanThresholdPx = 18f;
        private const float WaypointLayoutSettleDelay = 0.10f;

        private Label _activeWaypointArrow;
        private int _pointerButton = -1;

        private string _lastPolylineClickedId;
        private float _lastPolylineClickTime = -10f;

        private string _lastPolylineRightClickedId;
        private float _lastPolylineRightClickTime = -10f;

        private VisualElement _navigationOverlay;
        private readonly Dictionary<string, VisualElement> _waypointNavArrows = new();
        private WaypointConnectorOverlay _selectedWaypointConnector;

        private VisualElement _activeNavigationArrow;
        private VisualElement _activityCheckpointArrow;

        private readonly List<Vector3> _activityCheckpointWorldPositions = new();
        private readonly List<VisualElement> _activityCheckpointMarkerRoots = new();
        private readonly List<VisualElement> _activityCheckpointMarkerDots = new();
        private readonly List<Label> _activityCheckpointMarkerLabels = new();

        private bool _hideBaseMarkersForActivity;
        private int _activityCurrentCheckpointIndex = -1;

        private bool _legendVisible = true;

        private bool _hasActiveActivityMarker;
        private Vector3 _activeActivityMarkerWorldPosition;
        private Color _activeActivityMarkerColor = Color.white;
        private string _activeActivityMarkerLabel = string.Empty;

        private bool _regionsSelectable = true;
        private bool _showRegionOverlayLabels = true;
        private bool _showNonRegionLabels = true;

        private const string MinorLabelStyleId = "minor";
        private const string StandardLabelStyleId = "standard";
        private const string MajorLabelStyleId = "major";
        private const string RegionMajorLabelStyleId = "region-major";
        private const string WaypointEditableLabelStyleId = "waypoint-editable";

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

        private void RegisterOverlayLabelCallbacks(Label label)
        {
            if (label == null)
                return;

            label.RegisterCallback<GeometryChangedEvent>(_ => LayoutOverlayLabels());
        }

        private void SelectPolylineInternal(string polylineId, Map.MapPolyline polyline, bool fireEvent)
        {
            _selectedPolylineId = polylineId;
            _selectedMarkerId = null;

            if (polyline.lineType == MapLineType.SkiRun && _polylineToMarker.TryGetValue(polylineId, out var runMarkerId))
            {
                _selectedMarkerId = runMarkerId;
            }
            else if (polyline.lineType == MapLineType.RaceCourse && TryFindRaceActivityMarkerForPolyline(polylineId, out var raceMarker))
            {
                _selectedMarkerId = raceMarker.id;
            }

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
                _trailLayer.SetStyle(_style);
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
                    _trailLayer.SetStyle(_style);
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

                var spriteVisual = new VisualElement();
                spriteVisual.name = "MapPlayerMarkerSprite";
                spriteVisual.style.position = Position.Absolute;
                spriteVisual.style.left = 0f;
                spriteVisual.style.top = 0f;
                spriteVisual.style.right = 0f;
                spriteVisual.style.bottom = 0f;
                spriteVisual.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                spriteVisual.style.display = DisplayStyle.None;
                spriteVisual.pickingMode = PickingMode.Ignore;

                var spriteOutlineVisual = new VisualElement();
                spriteOutlineVisual.name = "MapPlayerMarkerSpriteOutline";
                spriteOutlineVisual.style.position = Position.Absolute;
                spriteOutlineVisual.style.left = 0f;
                spriteOutlineVisual.style.top = 0f;
                spriteOutlineVisual.style.right = 0f;
                spriteOutlineVisual.style.bottom = 0f;
                spriteOutlineVisual.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                spriteOutlineVisual.style.display = DisplayStyle.None;
                spriteOutlineVisual.pickingMode = PickingMode.Ignore;

                _playerMarker.Add(spriteOutlineVisual);
                _playerMarker.Add(arrowOutline);
                _playerMarker.Add(arrowBody);
                _playerMarker.Add(baseCircle);
                _playerMarker.Add(centerDot);
                _playerMarker.Add(spriteVisual);

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

            // Strip legacy toolbar classes so they don't fight the legend layout.
            _layerBar.RemoveFromClassList("map-layerbar");
            _layerBar.RemoveFromClassList("map-layer-bar");

            _layerBar.pickingMode = PickingMode.Position;

            _layerBar.style.position = Position.Absolute;
            _layerBar.style.right = 14f;
            _layerBar.style.top = 14f;
            _layerBar.style.left = StyleKeyword.Auto;
            _layerBar.style.bottom = StyleKeyword.Auto;

            _layerBar.style.width = StyleKeyword.Auto;
            _layerBar.style.height = StyleKeyword.Auto;
            _layerBar.style.maxHeight = Length.Percent(72);
            _layerBar.style.flexGrow = 0f;
            _layerBar.style.flexShrink = 0f;
            _layerBar.style.overflow = Overflow.Hidden;
            _layerBar.style.unityOverflowClipBox = OverflowClipBox.PaddingBox;

            _layerBar.BringToFront();

            EnsureLegendDefaults();
            RebuildLegendUI();
        }

        private bool IsEventInsideLegend(EventBase evtBase)
        {
            if (_layerBar == null || evtBase == null)
                return false;

            if (evtBase.target is VisualElement ve)
                return ve == _layerBar || _layerBar.Contains(ve);

            return false;
        }

        private void EnsureLegendDefaults()
        {
            if (_legendDefaultsInitialized)
            {
                SanitizeLegendCategoryVisibilityState();
                return;
            }

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

                    var normalized = NormalizeCategoryForLegend(m.category);
                    if (IsDedicatedActivityLegendCategory(normalized))
                        continue;

                    _visiblePOICategories.Add(normalized);
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

            SanitizeLegendCategoryVisibilityState();

            _visibleRunDifficultyRanks.Clear();
            for (int i = 0; i < DefaultRunDifficultyRanks.Length; i++)
                _visibleRunDifficultyRanks.Add(DefaultRunDifficultyRanks[i]);

            _legendDefaultsInitialized = true;
        }

        private static readonly int[] DefaultRunDifficultyRanks = { 0, 1, 2, 3 };

        private void EnsureRunDifficultyDefaults()
        {
            if (_visibleRunDifficultyRanks.Count > 0)
                return;

            for (int i = 0; i < DefaultRunDifficultyRanks.Length; i++)
                _visibleRunDifficultyRanks.Add(DefaultRunDifficultyRanks[i]);
        }

        private bool IsRunDifficultyVisible(int rank)
        {
            EnsureRunDifficultyDefaults();
            return _visibleRunDifficultyRanks.Contains(rank);
        }

        private bool AreAllRunDifficultiesVisible()
        {
            return _visibleRunDifficultyRanks.Count >= DefaultRunDifficultyRanks.Length;
        }

        private void SetAllRunDifficultiesVisible()
        {
            _visibleRunDifficultyRanks.Clear();
            for (int i = 0; i < DefaultRunDifficultyRanks.Length; i++)
                _visibleRunDifficultyRanks.Add(DefaultRunDifficultyRanks[i]);
        }

        private void ToggleRunOverlays()
        {
            if (_showRunOverlays)
            {
                _showRunOverlays = false;
                _visibleRunDifficultyRanks.Clear();
                return;
            }

            _showRunOverlays = true;
            SetAllRunDifficultiesVisible();
        }

        private void ToggleRunDifficulty(int rank)
        {
            EnsureRunDifficultyDefaults();

            // If the main Ski Runs toggle is off, pressing a difficulty should
            // turn Ski Runs back on and enable only that difficulty.
            if (!_showRunOverlays)
            {
                _showRunOverlays = true;
                _visibleRunDifficultyRanks.Clear();
                _visibleRunDifficultyRanks.Add(rank);
                return;
            }

            if (_visibleRunDifficultyRanks.Contains(rank))
                _visibleRunDifficultyRanks.Remove(rank);
            else
                _visibleRunDifficultyRanks.Add(rank);

            // If all difficulties are off, the main Ski Runs toggle should also go off.
            if (_visibleRunDifficultyRanks.Count == 0)
                _showRunOverlays = false;
            else
                _showRunOverlays = true;
        }

        private static string FormatRunDifficultyLabel(int rank)
        {
            switch (rank)
            {
                case 0: return "Green";
                case 1: return "Blue";
                case 2: return "Red";
                case 3: return "Black";
                default: return $"Difficulty {rank}";
            }
        }

        private static string GetRunDifficultyClass(int rank)
        {
            switch (rank)
            {
                case 0: return "legend-chip-difficulty-green";
                case 1: return "legend-chip-difficulty-blue";
                case 2: return "legend-chip-difficulty-red";
                case 3: return "legend-chip-difficulty-black";
                default: return "legend-chip-difficulty-generic";
            }
        }

        public void SetOverlayVisibility(bool showRuns, bool showLifts, bool showPOIs)
        {
            _showRunOverlays = showRuns;
            _showSkiRunOverlays = showRuns;
            _showLiftOverlays = showLifts;
            _showPOIOverlays = showPOIs;

            ApplyLayerVisibility();
        }

        public void SetShowStandardPOIOverlays(bool show)
        {
            _showPOIOverlays = show;
            _dirty = true;
            Refresh();
        }

        public void SetActivityOverlayVisibility(bool showRaceStarts, bool showMedicTents, bool showSnowmobiles)
        {
            _showRaceStartOverlays = showRaceStarts;
            _showMedicTentOverlays = showMedicTents;
            _showSnowmobileOverlays = showSnowmobiles;
            _dirty = true;
            Refresh();
        }

        public void SetSelectionFilters(
            Func<SkiGame.Map.MapMarker, bool> markerFilter,
            Func<SkiGame.Map.MapPolyline, bool> polylineFilter)
        {
            _markerSelectionFilter = markerFilter;
            _polylineSelectionFilter = polylineFilter;
        }

        public void SetSkiRunOverlayVisible(bool visible)
        {
            _showSkiRunOverlays = visible;
            ApplyLayerVisibility();
        }

        public void SetPolylineDisplayColorOverride(string polylineId, Color color)
        {
            if (string.IsNullOrWhiteSpace(polylineId))
                return;

            Color opaque = new Color(color.r, color.g, color.b, 1f);
            _polylineColorOverrides[polylineId] = opaque;
            _polylineLabelAccent[polylineId] = opaque;
            _polyLayer?.SetColorOverrides(_polylineColorOverrides);
            ApplyPolylineLabelVisual(polylineId, selected: string.Equals(_selectedPolylineId, polylineId, StringComparison.Ordinal));
        }

        public void ClearSelection()
        {
            ClearSelectionInternal(fireEvent: true);
        }

        public void SetLegendVisible(bool visible)
        {
            _legendVisible = visible;

            if (_layerBar != null)
                _layerBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RebuildLegendUI()
        {
            if (_layerBar == null)
                return;

            EnsureLegendDefaults();
            EnsureRunDifficultyDefaults();

            _layerBar.Clear();

            _layerBar.RemoveFromClassList("map-layerbar");
            _layerBar.RemoveFromClassList("map-layer-bar");
            _layerBar.AddToClassList("map-legend-panel");

            _layerBar.style.display = _legendVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _layerBar.style.flexDirection = FlexDirection.Column;
            _layerBar.style.alignSelf = Align.FlexStart;

            if (!_legendVisible)
                return;

            var header = new Label("Map Filters");
            header.AddToClassList("map-legend-title");
            _layerBar.Add(header);

            var legendMarkerStyle = GetLegendMarkerStyle();
            Sprite labelLegendSprite = legendMarkerStyle.sprite;

            var labelChip = CreateLegendToggleChip(
                "Labels",
                _showNonRegionLabels,
                () =>
                {
                    _showNonRegionLabels = !_showNonRegionLabels;
                    ApplyGlobalLabelVisibility();
                    UpdateLayerBarVisuals();
                },
                labelLegendSprite,
                Color.white);

            labelChip.AddToClassList("legend-chip-labels");
            _layerBar.Add(labelChip);

            var typeLabel = new Label("Map Markers");
            typeLabel.AddToClassList("map-legend-section-label");
            _layerBar.Add(typeLabel);


            _btnLayerLifts = CreateLegendToggleChip("Ski Lifts", _showLiftOverlays, () =>
            {
                _showLiftOverlays = !_showLiftOverlays;
                ApplyLayerVisibility();
            }, ResolveLegendSprite(POIType.SkiLift, POICategory.None), new Color(0.40f, 0.85f, 1f, 1f));
            _btnLayerLifts.AddToClassList("legend-chip-lift");
            _layerBar.Add(_btnLayerLifts);


            foreach (var category in GetLegendCategoriesSorted())
            {
                if (IsDedicatedActivityLegendCategory(category))
                    continue;

                var chip = CreateLegendToggleChip(
                    FormatPOICategoryLabel(category),
                    IsPOICategoryVisible(category),
                    () =>
                    {
                        TogglePOICategory(category);
                        ApplyLayerVisibility();
                    },
                    ResolveLegendSprite(POIType.Custom, category),
                    GetLegendCategoryTint(category));

                chip.AddToClassList(GetPOICategoryClass(category));
                _layerBar.Add(chip);
            }

            var activityLabel = new Label("Activities");
            activityLabel.AddToClassList("map-legend-section-label");
            _layerBar.Add(activityLabel);

            _btnLayerRaces = CreateLegendToggleChip("Races", _showRaceStartOverlays, () =>
            {
                _showRaceStartOverlays = !_showRaceStartOverlays;
                ApplyLayerVisibility();
            }, ResolveLegendSprite(POIType.Custom, POICategory.Race), GetLegendCategoryTint(POICategory.Race));
            _btnLayerRaces.AddToClassList("legend-chip-race");
            _layerBar.Add(_btnLayerRaces);

            _btnLayerMedicTents = CreateLegendToggleChip("Medic Tents", _showMedicTentOverlays, () =>
            {
                _showMedicTentOverlays = !_showMedicTentOverlays;
                ApplyLayerVisibility();
            }, ResolveLegendSprite(POIType.Custom, POICategory.Medical), GetLegendCategoryTint(POICategory.Medical));
            _btnLayerMedicTents.AddToClassList("legend-chip-medic");
            _layerBar.Add(_btnLayerMedicTents);

            var diffLabel = new Label("Run Difficulties");
            diffLabel.AddToClassList("map-legend-section-label");
            _layerBar.Add(diffLabel);

            _btnLayerRuns = CreateLegendToggleChip("All Ski Runs", _showRunOverlays, () =>
            {
                ToggleRunOverlays();
                ApplyLayerVisibility();
            }, ResolveLegendSprite(POIType.SkiRun, POICategory.None));
            _btnLayerRuns.AddToClassList("legend-chip-run");
            _layerBar.Add(_btnLayerRuns);

            for (int i = 0; i < DefaultRunDifficultyRanks.Length; i++)
            {
                int rank = DefaultRunDifficultyRanks[i];
                bool isOn = _showRunOverlays && IsRunDifficultyVisible(rank);

                var chip = CreateLegendToggleChip(
                    FormatRunDifficultyLabel(rank),
                    isOn,
                    () =>
                    {
                        ToggleRunDifficulty(rank);
                        ApplyLayerVisibility();
                    },
                    ResolveLegendSprite(POIType.SkiRun, POICategory.None),
                    GetRunDifficultyTint(rank));

                chip.AddToClassList(GetRunDifficultyClass(rank));
                _layerBar.Add(chip);
            }
        }

        private VisualElement CreateLegendToggleChip(string text, bool on, Action onClick, Sprite iconSprite = null, Color? iconTint = null)
        {
            var chip = new VisualElement();
            chip.AddToClassList("map-legend-chip");
            chip.EnableInClassList("is-on", on);
            chip.pickingMode = PickingMode.Position;
            chip.focusable = false;

            var iconRoot = new VisualElement();
            iconRoot.AddToClassList("map-legend-chip-dot");
            iconRoot.pickingMode = PickingMode.Ignore;

            float iconSize = Mathf.Max(1f, GetLegendMarkerStyle().size);
            iconRoot.style.width = iconSize;
            iconRoot.style.height = iconSize;
            iconRoot.style.position = Position.Relative;
            iconRoot.style.justifyContent = Justify.Center;
            iconRoot.style.alignItems = Align.Center;

            var iconOutline = new VisualElement();
            iconOutline.name = "LegendIconOutline";
            iconOutline.style.position = Position.Absolute;
            iconOutline.style.left = 0f;
            iconOutline.style.top = 0f;
            iconOutline.style.right = 0f;
            iconOutline.style.bottom = 0f;
            iconOutline.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            iconOutline.pickingMode = PickingMode.Ignore;

            var iconBody = new VisualElement();
            iconBody.name = "LegendIconBody";
            iconBody.style.position = Position.Absolute;
            iconBody.style.left = 0f;
            iconBody.style.top = 0f;
            iconBody.style.right = 0f;
            iconBody.style.bottom = 0f;
            iconBody.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            iconBody.pickingMode = PickingMode.Ignore;

            iconRoot.Add(iconOutline);
            iconRoot.Add(iconBody);
            ApplyLegendChipVisual(iconRoot, iconOutline, iconBody, iconSprite, iconTint ?? Color.white);
            chip.Add(iconRoot);

            var label = new Label(text);
            label.AddToClassList("map-legend-chip-text");
            label.pickingMode = PickingMode.Ignore;
            chip.Add(label);

            HookLayerBtn(chip, onClick);
            return chip;
        }

        private void ApplyLegendChipVisual(VisualElement iconRoot, VisualElement iconOutline, VisualElement iconBody, Sprite iconSprite, Color tint)
        {
            if (iconRoot == null || iconOutline == null || iconBody == null)
                return;

            bool useSprite = iconSprite != null;
            var legendStyle = GetLegendMarkerStyle();
            float borderWidth = Mathf.Max(0f, legendStyle.borderWidth);
            Color borderColor = legendStyle.borderColor;

            iconRoot.style.backgroundImage = StyleKeyword.None;
            iconRoot.style.backgroundColor = useSprite ? Color.clear : tint;
            iconRoot.style.borderLeftWidth = 0f;
            iconRoot.style.borderRightWidth = 0f;
            iconRoot.style.borderTopWidth = 0f;
            iconRoot.style.borderBottomWidth = 0f;
            iconRoot.style.borderTopLeftRadius = useSprite ? 0f : 999f;
            iconRoot.style.borderTopRightRadius = useSprite ? 0f : 999f;
            iconRoot.style.borderBottomLeftRadius = useSprite ? 0f : 999f;
            iconRoot.style.borderBottomRightRadius = useSprite ? 0f : 999f;

            if (useSprite)
            {
                iconOutline.style.display = DisplayStyle.Flex;
                iconOutline.style.backgroundImage = new StyleBackground(iconSprite);
                iconOutline.style.unityBackgroundImageTintColor = borderColor;

                iconBody.style.display = DisplayStyle.Flex;
                iconBody.style.backgroundImage = new StyleBackground(iconSprite);
                iconBody.style.unityBackgroundImageTintColor = tint;
                iconBody.style.left = borderWidth;
                iconBody.style.top = borderWidth;
                iconBody.style.right = borderWidth;
                iconBody.style.bottom = borderWidth;
            }
            else
            {
                iconOutline.style.display = DisplayStyle.None;
                iconOutline.style.backgroundImage = StyleKeyword.None;

                iconBody.style.display = DisplayStyle.None;
                iconBody.style.backgroundImage = StyleKeyword.None;
                iconBody.style.left = 0f;
                iconBody.style.top = 0f;
                iconBody.style.right = 0f;
                iconBody.style.bottom = 0f;

                iconRoot.style.borderTopLeftRadius = 999f;
                iconRoot.style.borderTopRightRadius = 999f;
                iconRoot.style.borderBottomLeftRadius = 999f;
                iconRoot.style.borderBottomRightRadius = 999f;
            }
        }

        private Sprite ResolveLegendSprite(POIType type, POICategory category)
        {
            ResolvedMarkerStyleRule styleRule = ResolveMarkerStyleRuleForPoi(type, category);
            if (styleRule.hasMarkerSprite)
                return styleRule.markerSprite;

            return GetPoiMarkerStyle().sprite;
        }

        private static Color GetLegendCategoryTint(POICategory category)
        {
            switch (category)
            {
                case POICategory.Resort: return new Color(0.25f, 0.75f, 1f, 1f);
                case POICategory.Shop: return new Color(1f, 0.45f, 0.75f, 1f);
                case POICategory.Kiosk: return new Color(1f, 0.75f, 0.2f, 1f);
                case POICategory.Service: return new Color(0.45f, 1f, 0.45f, 1f);
                case POICategory.Landmark: return new Color(0.8f, 0.8f, 1f, 1f);
                case POICategory.Race: return new Color(1.00f, 0.55f, 0.20f, 1f);
                case POICategory.Medical: return new Color(0.20f, 1.00f, 1.00f, 1f);
                case POICategory.Vehicle: return new Color(1.00f, 0.90f, 0.25f, 1f);
                default: return Color.white;
            }
        }

        private static Color GetRunDifficultyTint(int rank)
        {
            switch (rank)
            {
                case 0: return new Color(0.45f, 0.92f, 0.48f, 1f);
                case 1: return new Color(0.35f, 0.72f, 1f, 1f);
                case 2: return new Color(1f, 0.35f, 0.35f, 1f);
                case 3: return new Color(0.18f, 0.18f, 0.22f, 1f);
                default: return Color.white;
            }
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
                    if (IsDedicatedActivityLegendCategory(normalized))
                        continue;

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
                case POICategory.Race: return 5;
                case POICategory.Medical: return 6;
                case POICategory.Vehicle: return 7;
                case POICategory.Custom: return 8;
                default: return 99;
            }
        }


        private static POICategory NormalizeCategoryForLegend(POICategory category)
        {
            return category == POICategory.None ? POICategory.Custom : category;
        }

        private static bool IsDedicatedActivityLegendCategory(POICategory category)
        {
            switch (NormalizeCategoryForLegend(category))
            {
                case POICategory.Race:
                case POICategory.Medical:
                    return true;

                default:
                    return false;
            }
        }

        private void SanitizeLegendCategoryVisibilityState()
        {
            if (_visiblePOICategories.Count == 0)
                return;

            var sanitized = new HashSet<POICategory>();

            foreach (var category in _visiblePOICategories)
            {
                var normalized = NormalizeCategoryForLegend(category);
                if (IsDedicatedActivityLegendCategory(normalized))
                    continue;

                sanitized.Add(normalized);
            }

            if (sanitized.SetEquals(_visiblePOICategories))
                return;

            _visiblePOICategories.Clear();
            foreach (var category in sanitized)
                _visiblePOICategories.Add(category);
        }

        private bool IsPOICategoryVisible(POICategory category)
        {
            return _visiblePOICategories.Contains(NormalizeCategoryForLegend(category));
        }

        private void TogglePOICategory(POICategory category)
        {
            category = NormalizeCategoryForLegend(category);

            if (IsDedicatedActivityLegendCategory(category))
                return;

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
                case POICategory.Race: return "Races";
                case POICategory.Medical: return "Medical";
                case POICategory.Vehicle: return "Vehicles";
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
                case POICategory.Race: return "legend-chip-custom";
                case POICategory.Medical: return "legend-chip-service";
                case POICategory.Vehicle: return "legend-chip-custom";
                case POICategory.Custom: return "legend-chip-custom";
                default: return "legend-chip-custom";
            }
        }

        private static void HookLayerBtn(VisualElement btn, Action onClick)
        {
            if (btn == null) return;

            btn.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                    e.StopPropagation();
            });

            btn.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0)
                    return;

                onClick?.Invoke();
                e.StopPropagation();
            });
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
            _polyLayer?.SetFilterState(
                _showRunOverlays,
                _showLiftOverlays,
                _showRaceStartOverlays,
                _visibleRunDifficultyRanks);

            foreach (var kv in _markerRoots)
            {
                string id = kv.Key;
                var root = kv.Value;
                if (root == null) continue;

                root.style.display = IsMarkerVisibleById(id) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RebuildLegendUI();
            ClearSelectionIfHidden();
            UpdateLayerBarVisuals();
            ApplyGlobalLabelVisibility();
        }

        private void ApplyGlobalLabelVisibility()
        {
            for (int i = 0; i < _activityCheckpointMarkerLabels.Count; i++)
            {
                var label = _activityCheckpointMarkerLabels[i];
                if (label == null)
                    continue;

                label.style.display = _showNonRegionLabels ? DisplayStyle.Flex : DisplayStyle.None;
            }

            LayoutOverlayLabels();

            // Waypoint labels are solved separately.
            // Mark dirty once and let the dedicated solver decide visibility/placement.
            MarkWaypointLabelLayoutDirty();
            LayoutWaypointLabels();
        }

        private void RefreshActivityCheckpointMarkers()
        {
            if (!_bound || _markerHost == null)
                return;

            EnsureActivityCheckpointMarkerVisualCount(_activityCheckpointWorldPositions.Count);

            for (int i = 0; i < _activityCheckpointMarkerRoots.Count; i++)
            {
                bool active = i < _activityCheckpointWorldPositions.Count && i >= _activityCurrentCheckpointIndex;

                var root = _activityCheckpointMarkerRoots[i];
                var dot = _activityCheckpointMarkerDots[i];
                var label = _activityCheckpointMarkerLabels[i];

                if (!active || _mapData == null || !_mapData.Projection.IsValid)
                {
                    root.style.display = DisplayStyle.None;
                    continue;
                }

                if (!TryProjectWorldToUV(_activityCheckpointWorldPositions[i], out Vector2 uv))
                {
                    root.style.display = DisplayStyle.None;
                    continue;
                }

                Vector2 local = UVToLocal(uv);

                root.style.display = DisplayStyle.Flex;
                root.style.left = local.x;
                root.style.top = local.y;

                bool isCurrent = i == _activityCurrentCheckpointIndex;
                bool isFinal = i == _activityCheckpointWorldPositions.Count - 1;

                Color dotColor = isFinal
                    ? new Color(1f, 0.15f, 0.15f, 1f)
                    : (isCurrent ? new Color(0.12f, 0.95f, 0.25f, 1f) : new Color(1f, 0.92f, 0.16f, 1f));

                dot.style.backgroundColor = dotColor;
                label.text = $"{i + 1}";
            }
        }

        private void RefreshActiveActivityMarker()
        {
            _hasActiveActivityMarker = false;
            _activeActivityMarkerLabel = string.Empty;

            if (!NavigationTargetController.TryGetActiveState(out var state))
                return;

            if (state.request.kind == NavigationTargetKind.Waypoint)
                return;

            // Race checkpoints use the dedicated checkpoint marker path.
            if (_activityCurrentCheckpointIndex >= 0 && _activityCheckpointWorldPositions.Count > 0)
                return;

            _hasActiveActivityMarker = true;
            _activeActivityMarkerWorldPosition = state.beaconWorldPosition;
            _activeActivityMarkerColor = state.request.accentColor.a > 0f
                ? state.request.accentColor
                : new Color(0.36f, 0.74f, 1f, 1f);
            _activeActivityMarkerLabel = state.request.displayName;
        }

        private void EnsureActivityCheckpointMarkerVisualCount(int required)
        {
            if (_markerHost == null)
                return;

            while (_activityCheckpointMarkerRoots.Count < required)
            {
                var root = new VisualElement();
                root.style.position = Position.Absolute;
                root.style.width = 0f;
                root.style.height = 0f;
                root.pickingMode = PickingMode.Ignore;

                var dot = new VisualElement();
                dot.style.position = Position.Absolute;
                dot.style.width = 18f;
                dot.style.height = 18f;
                dot.style.translate = new Translate(-9f, -9f, 0f);
                dot.style.borderTopLeftRadius = 9f;
                dot.style.borderTopRightRadius = 9f;
                dot.style.borderBottomLeftRadius = 9f;
                dot.style.borderBottomRightRadius = 9f;
                dot.style.borderLeftWidth = 2f;
                dot.style.borderRightWidth = 2f;
                dot.style.borderTopWidth = 2f;
                dot.style.borderBottomWidth = 2f;
                dot.style.borderLeftColor = Color.white;
                dot.style.borderRightColor = Color.white;
                dot.style.borderTopColor = Color.white;
                dot.style.borderBottomColor = Color.white;

                var label = new Label();
                label.style.position = Position.Absolute;
                label.style.left = 12f;
                label.style.top = -12f;
                label.style.fontSize = 12f;
                label.style.color = Color.white;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.pickingMode = PickingMode.Ignore;

                root.Add(dot);
                root.Add(label);
                _markerHost.Add(root);

                _activityCheckpointMarkerRoots.Add(root);
                _activityCheckpointMarkerDots.Add(dot);
                _activityCheckpointMarkerLabels.Add(label);
            }

            for (int i = 0; i < _activityCheckpointMarkerRoots.Count; i++)
                _activityCheckpointMarkerRoots[i].style.display = i < required ? DisplayStyle.Flex : DisplayStyle.None;
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

            // Activity POIs are normal custom POIs with activity meta tokens.
            if (IsRaceActivityMarker(marker) || IsMedicActivityMarker(marker) || IsSnowmobileActivityMarker(marker))
                return IsActivityMarkerVisible(marker);

            return _showPOIOverlays && IsPOICategoryVisible(marker.category);
        }

        private bool IsPolylineVisible(MapPolyline polyline)
        {
            switch (polyline.lineType)
            {
                case MapLineType.SkiRun:
                    return IsRunVisibleById(polyline.id);

                case MapLineType.SkiLift:
                    return _showLiftOverlays;

                case MapLineType.RaceCourse:
                    return IsRaceCourseVisible(polyline.id);

                default:
                    return true;
            }
        }

        private bool IsRunVisibleById(string runId)
        {
            if (!_showRunOverlays || !_showSkiRunOverlays)
                return false;

            if (TryGetRunDifficultyRank(runId, out int rank))
                return IsRunDifficultyVisible(rank);

            // If the run difficulty cannot be resolved, only show it when all
            // difficulties are effectively enabled.
            return AreAllRunDifficultiesVisible();
        }

        private bool TryGetRunDifficultyRank(string runId, out int rank)
        {
            rank = -1;

            if (string.IsNullOrWhiteSpace(runId))
                return false;

            string normalizedRunId = runId;
            int suffixIndex = normalizedRunId.LastIndexOf("__", StringComparison.Ordinal);
            if (suffixIndex > 0)
                normalizedRunId = normalizedRunId.Substring(0, suffixIndex);

            // Prefer live authored run data from the POI registry when available.
            if (_poiRegistry != null && _poiRegistry.TryGetById(normalizedRunId, out var poi) && poi.source is SkiRunLine line)
            {
                if (TryMapRunDifficultyName(line.Difficulty.ToString(), out rank))
                    return true;
            }

            if (_mapData != null)
            {
                var corridors = _mapData.RunCorridors;
                if (corridors != null)
                {
                    for (int i = 0; i < corridors.Count; i++)
                    {
                        if (string.Equals(corridors[i].id, normalizedRunId, StringComparison.Ordinal))
                        {
                            rank = NormalizeRunDifficultyRank(corridors[i].difficultyRank);
                            return rank >= 0;
                        }
                    }
                }

                var polys = _mapData.Polylines;
                if (polys != null)
                {
                    for (int i = 0; i < polys.Count; i++)
                    {
                        if (string.Equals(polys[i].id, normalizedRunId, StringComparison.Ordinal))
                        {
                            rank = NormalizeRunDifficultyRank(polys[i].difficultyRank);
                            return rank >= 0;
                        }
                    }
                }
            }

            return false;
        }

        private static int NormalizeRunDifficultyRank(int rawRank)
        {
            if (rawRank >= 0 && rawRank <= 3)
                return rawRank;

            // Some data paths may serialize green/blue/red/black as 1..4.
            if (rawRank >= 1 && rawRank <= 4)
                return rawRank - 1;

            return -1;
        }

        private static bool TryMapRunDifficultyName(string raw, out int rank)
        {
            rank = -1;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string s = raw.Replace(" ", string.Empty).ToLowerInvariant();

            if (s.Contains("green"))
            {
                rank = 0;
                return true;
            }

            if (s.Contains("blue"))
            {
                rank = 1;
                return true;
            }

            if (s.Contains("red"))
            {
                rank = 2;
                return true;
            }

            if (s.Contains("black"))
            {
                rank = 3;
                return true;
            }

            return false;
        }

        private static bool HasMetaToken(string meta, string token)
        {
            if (string.IsNullOrWhiteSpace(meta) || string.IsNullOrWhiteSpace(token))
                return false;

            return meta.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRaceActivityMarker(SkiGame.Map.MapMarker marker)
        {
            return HasMetaToken(marker.meta, "activity:race");
        }

        private static bool IsMedicActivityMarker(SkiGame.Map.MapMarker marker)
        {
            return HasMetaToken(marker.meta, "activity:medic");
        }

        private static bool IsSnowmobileActivityMarker(SkiGame.Map.MapMarker marker)
        {
            return HasMetaToken(marker.meta, "activity:snowmobile");
        }

        public bool CenterOnPolylineById(string polylineId, float minZoom = -1f)
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

                if (p.Has3DPoints && p.pointsWorld != null && p.pointsWorld.Count > 0)
                {
                    int mid = p.pointsWorld.Count / 2;
                    return CenterOnWorldPosition(p.pointsWorld[mid], minZoom);
                }

                if (p.HasXZPoints && p.pointsWorldXZ != null && p.pointsWorldXZ.Count > 0)
                {
                    int mid = p.pointsWorldXZ.Count / 2;
                    var w = p.pointsWorldXZ[mid];
                    return CenterOnWorldPosition(new Vector3(w.x, 0f, w.y), minZoom);
                }

                return false;
            }

            return false;
        }

        public bool FramePolylineById(string polylineId, float paddingPx = 28f, float minZoom = 0.85f)
        {
            if (!_bound) return false;
            if (_mapData == null || string.IsNullOrWhiteSpace(polylineId)) return false;
            if (!TryGetViewportSize(out float vw, out float vh)) return false;

            var list = _mapData.Polylines;
            if (list == null) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (!p.IsValid) continue;
                if (!string.Equals(p.id, polylineId, StringComparison.Ordinal)) continue;

                bool foundAny = false;
                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;

                if (p.Has3DPoints && p.pointsWorld != null)
                {
                    for (int k = 0; k < p.pointsWorld.Count; k++)
                    {
                        if (!TryProjectWorldToLocal(p.pointsWorld[k], out var local))
                            continue;

                        foundAny = true;
                        minX = Mathf.Min(minX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxX = Mathf.Max(maxX, local.x);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }
                else if (p.HasXZPoints && p.pointsWorldXZ != null)
                {
                    for (int k = 0; k < p.pointsWorldXZ.Count; k++)
                    {
                        if (!TryProjectWorldXZToLocal(p.pointsWorldXZ[k], out var local))
                            continue;

                        foundAny = true;
                        minX = Mathf.Min(minX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxX = Mathf.Max(maxX, local.x);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }

                if (!foundAny)
                    return false;

                float width = Mathf.Max(8f, maxX - minX);
                float height = Mathf.Max(8f, maxY - minY);

                float usableW = Mathf.Max(32f, vw - paddingPx * 2f);
                float usableH = Mathf.Max(32f, vh - paddingPx * 2f);

                _zoom = Mathf.Max(minZoom, Mathf.Min(usableW / width, usableH / height));

                Vector2 centerLocal = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                CenterViewOnContentPoint(centerLocal);
                return true;
            }

            return false;
        }

        private bool IsActivityMarkerVisible(SkiGame.Map.MapMarker marker)
        {
            if (IsRaceActivityMarker(marker))
                return _showRaceStartOverlays;

            if (IsMedicActivityMarker(marker))
                return _showMedicTentOverlays;

            if (IsSnowmobileActivityMarker(marker))
                return _showSnowmobileOverlays;

            return false;
        }

        private bool IsRaceCourseVisible(string polylineId)
        {
            if (!_showRaceStartOverlays)
                return false;

            return TryFindRaceActivityMarkerForPolyline(polylineId, out _);
        }

        private bool TryFindRaceActivityMarkerForPolyline(string polylineId, out SkiGame.Map.MapMarker marker)
        {
            marker = default;

            if (_mapData == null || _mapData.Markers == null || string.IsNullOrWhiteSpace(polylineId))
                return false;

            for (int i = 0; i < _mapData.Markers.Count; i++)
            {
                var m = _mapData.Markers[i];
                if (!m.IsValid)
                    continue;

                if (!IsRaceActivityMarker(m))
                    continue;

                // Prefer exact marker-source identity when the POI was baked from the same RaceCourseLine.
                if (m.source is RaceCourseLine race && race != null)
                {
                    string raceId = race.RaceId;
                    string raceName = race.RaceName;
                    string raceObjectName = race.name; // safer than race.gameObject.name

                    if ((!string.IsNullOrWhiteSpace(raceId) && string.Equals(raceId, polylineId, StringComparison.Ordinal)) ||
                        string.Equals(raceName, polylineId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(raceObjectName, polylineId, StringComparison.Ordinal))
                    {
                        marker = m;
                        return true;
                    }
                }

                // Fallback to marker id conventions and display-name matching.
                if (string.Equals(m.id, polylineId, StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(m.id) && m.id.EndsWith(polylineId, StringComparison.Ordinal)) ||
                    string.Equals(m.displayName, polylineId, StringComparison.OrdinalIgnoreCase))
                {
                    marker = m;
                    return true;
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

            RefreshStyleIfNeeded();

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

        private bool TryFindLiveLiftLineForPolylineId(string polylineId, out LiftLine lift)
        {
            lift = null;
            if (string.IsNullOrWhiteSpace(polylineId))
                return false;

            LiftLine[] lifts = UnityEngine.Object.FindObjectsOfType<LiftLine>(includeInactive: false);
            if (lifts == null || lifts.Length == 0)
                return false;

            for (int i = 0; i < lifts.Length; i++)
            {
                LiftLine candidate = lifts[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.name, polylineId, StringComparison.Ordinal) ||
                    string.Equals(candidate.gameObject.name, polylineId, StringComparison.OrdinalIgnoreCase))
                {
                    lift = candidate;
                    return true;
                }
            }

            return false;
        }

        private void ResolveLiftRequirementForPolyline(
    string polylineId,
    out int requiredLevel,
    out string requiredPassId,
    out Color baseColor)
        {
            requiredLevel = 0;
            requiredPassId = string.Empty;
            baseColor = new Color(0.75f, 0.85f, 1f, 0.70f);

            if (string.IsNullOrWhiteSpace(polylineId))
                return;

            if (_forcedLiftRequiredLevelByPolyline.TryGetValue(polylineId, out int forcedLevel))
            {
                requiredLevel = forcedLevel;

                if (_forcedLiftRequiredPassIdByPolyline.TryGetValue(polylineId, out string forcedPassId))
                    requiredPassId = forcedPassId;
                else
                    requiredPassId = string.Empty;

                if (_forcedLiftColorByPolyline.TryGetValue(polylineId, out Color forcedColor))
                    baseColor = forcedColor;
                else
                    baseColor = GetPassLevelMapColor(requiredLevel);

                return;
            }

            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;

            LiftLine liveLift = null;

            // First prefer the direct mapping captured during marker rebuild.
            if (!_liveLiftByPolylineId.TryGetValue(polylineId, out liveLift) || liveLift == null)
                TryFindLiveLiftLineForPolylineId(polylineId, out liveLift);

            if (liveLift != null)
            {
                requiredLevel = Mathf.Max(0, liveLift.RequiredPassLevel);

                if (cfg != null && !string.IsNullOrWhiteSpace(liveLift.RequiredPassId))
                {
                    string normalized = liveLift.RequiredPassId.Trim();
                    int byId = cfg.GetLevelIndexByPassId(normalized);
                    if (byId >= 0)
                    {
                        requiredPassId = normalized;
                        requiredLevel = byId;
                    }
                }

                if (string.IsNullOrWhiteSpace(requiredPassId) && cfg != null)
                {
                    string fallbackPassId = cfg.GetPassIdForLevel(requiredLevel);
                    if (!string.IsNullOrWhiteSpace(fallbackPassId))
                        requiredPassId = fallbackPassId.Trim();
                }

                baseColor = GetPassLevelMapColor(requiredLevel);

                _liftRequiredLevelByPolyline[polylineId] = requiredLevel;

                if (!string.IsNullOrWhiteSpace(requiredPassId))
                    _liftRequiredPassIdByPolyline[polylineId] = requiredPassId;
                else
                    _liftRequiredPassIdByPolyline.Remove(polylineId);

                _liftRequiredColorByPolyline[polylineId] = baseColor;
                return;
            }

            if (_liftRequiredLevelByPolyline.TryGetValue(polylineId, out int cachedLevel))
                requiredLevel = cachedLevel;

            if (_liftRequiredPassIdByPolyline.TryGetValue(polylineId, out string cachedPassId))
                requiredPassId = cachedPassId;

            // If the cached pass id is valid, let it correct the cached level before deriving colour.
            if (cfg != null && !string.IsNullOrWhiteSpace(requiredPassId))
            {
                int byId = cfg.GetLevelIndexByPassId(requiredPassId.Trim());
                if (byId >= 0)
                    requiredLevel = byId;
            }

            if (_liftRequiredColorByPolyline.TryGetValue(polylineId, out Color cachedColor))
            {
                // Recompute from corrected level when the cached colour may have been built from a stale level.
                baseColor = GetPassLevelMapColor(requiredLevel);
                _liftRequiredColorByPolyline[polylineId] = baseColor;
            }
            else
            {
                baseColor = GetPassLevelMapColor(requiredLevel);
            }
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

        private string GetLiftRequiredPassId(SkiGame.Map.MapMarker m)
        {
            if (m.source is LiftLine ll && ll && !string.IsNullOrWhiteSpace(ll.RequiredPassId))
                return ll.RequiredPassId.Trim();

            int requiredLevel = GetLiftRequiredPassLevel(m);
            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;
            if (cfg == null)
                return string.Empty;

            return cfg.GetPassIdForLevel(requiredLevel);
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

        private static Color DimLiftPassColor(Color baseColor, float factor = 0.62f, float alpha = 0.56f)
        {
            factor = Mathf.Clamp01(factor);
            alpha = Mathf.Clamp01(alpha);

            float grey = (baseColor.r + baseColor.g + baseColor.b) / 3f;
            Color desaturated = new Color(
                Mathf.Lerp(grey, baseColor.r, 0.55f),
                Mathf.Lerp(grey, baseColor.g, 0.55f),
                Mathf.Lerp(grey, baseColor.b, 0.55f),
                1f);

            return new Color(
                desaturated.r * factor,
                desaturated.g * factor,
                desaturated.b * factor,
                alpha);
        }

        private static Color VisibleLiftPassColor(Color baseColor, float alpha = 0.82f)
        {
            alpha = Mathf.Clamp01(alpha);
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        private void CacheLiftRequirementColour(string liftPolylineId, int requiredLevel, string requiredPassId)
        {
            if (string.IsNullOrWhiteSpace(liftPolylineId))
                return;

            requiredLevel = Mathf.Max(0, requiredLevel);

            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;

            // Prefer pass-id resolution for colour selection when possible.
            if (cfg != null && !string.IsNullOrWhiteSpace(requiredPassId))
            {
                int resolvedById = cfg.GetLevelIndexByPassId(requiredPassId.Trim());
                if (resolvedById >= 0)
                    requiredLevel = resolvedById;
            }

            // If multiple stations report something, keep the most restrictive resolved level.
            if (_liftRequiredLevelByPolyline.TryGetValue(liftPolylineId, out var existing))
                requiredLevel = Mathf.Max(existing, requiredLevel);

            _liftRequiredLevelByPolyline[liftPolylineId] = requiredLevel;

            if (!string.IsNullOrWhiteSpace(requiredPassId))
                _liftRequiredPassIdByPolyline[liftPolylineId] = requiredPassId.Trim();
            else
                _liftRequiredPassIdByPolyline.Remove(liftPolylineId);

            Color c = GetPassLevelMapColor(requiredLevel);
            _liftRequiredColorByPolyline[liftPolylineId] = c;

            _polylineColorOverrides[liftPolylineId] = new Color(c.r, c.g, c.b, 0.70f);
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

            foreach (var kv in _waypointLabels)
                kv.Value?.RemoveFromHierarchy();

            if (_activeWaypointRenameField != null)
            {
                if (_activeWaypointRenameField.parent != null)
                    _activeWaypointRenameField.RemoveFromHierarchy();

                _activeWaypointRenameField = null;
                _activeWaypointRenameId = null;
            }

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
            _liftRequiredPassIdByPolyline.Clear();
            _liftRequiredColorByPolyline.Clear();
            _liveLiftByPolylineId.Clear();

            _markerHitTargets.Clear();
            _markerDisplayNames.Clear();
            _poiLabelDisplayOverrides.Clear();

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

                            if (m.source is LiftLine liveLift && liveLift != null)
                                _liveLiftByPolylineId[liftPolyId] = liveLift;

                            int req = GetLiftRequiredPassLevel(m);
                            string requiredPassId = GetLiftRequiredPassId(m);

                            // Prefer the live lift's authored values when available.
                            if (m.source is LiftLine sourceLift && sourceLift != null)
                            {
                                req = Mathf.Max(0, sourceLift.RequiredPassLevel);

                                if (!string.IsNullOrWhiteSpace(sourceLift.RequiredPassId))
                                    requiredPassId = sourceLift.RequiredPassId.Trim();
                            }

                            CacheLiftRequirementColour(liftPolyId, req, requiredPassId);
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
                dot.style.justifyContent = Justify.Center;
                dot.style.alignItems = Align.Center;

                var spriteOutline = new VisualElement();
                spriteOutline.name = "MapMarkerSpriteOutline";
                spriteOutline.style.position = Position.Absolute;
                spriteOutline.style.left = 0f;
                spriteOutline.style.top = 0f;
                spriteOutline.style.right = 0f;
                spriteOutline.style.bottom = 0f;
                spriteOutline.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                spriteOutline.style.display = DisplayStyle.None;
                spriteOutline.pickingMode = PickingMode.Ignore;

                var spriteBody = new VisualElement();
                spriteBody.name = "MapMarkerSpriteBody";
                spriteBody.style.position = Position.Absolute;
                spriteBody.style.left = 0f;
                spriteBody.style.top = 0f;
                spriteBody.style.right = 0f;
                spriteBody.style.bottom = 0f;
                spriteBody.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                spriteBody.style.display = DisplayStyle.None;
                spriteBody.pickingMode = PickingMode.Ignore;

                ResolvedMarkerStyleRule markerStyleRule = ResolveMarkerStyleRuleForPoi(m.type, ResolvePoiCategoryForMarkerId(m.id, m.type));
                Color c = ResolveMapElementColor(m.color, markerStyleRule, GetPoiMarkerStyle().fillColor.a > 0f ? GetPoiMarkerStyle().fillColor : GetFallbackCategoryColor(m));

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
                dot.Add(spriteOutline);
                dot.Add(spriteBody);

                // Cache accent colour for this marker's label (used for the left stripe).
                _markerLabelAccent[m.id] = new Color(c.r, c.g, c.b, 1f);

                // Lift station markers: overlay ^ or v (use resolved role from prepass)
                Label glyphLabel = null;
                if (_liftMarkerRole.TryGetValue(m.id, out var role) && role != LiftStationRole.None)
                {
                    glyphLabel = new Label(role == LiftStationRole.Top ? "↑" : "↓");
                    glyphLabel.pickingMode = PickingMode.Ignore;
                    glyphLabel.style.position = Position.Absolute;
                    glyphLabel.style.left = 0f;
                    glyphLabel.style.top = 0f;
                    glyphLabel.style.right = 0f;
                    glyphLabel.style.bottom = 0f;
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
                    MapUIStyleSettings.MapLabelDisplayMode mode = MapUIStyleSettings.MapLabelDisplayMode.Contextual;

                    if (_poiRegistry != null && _poiRegistry.TryGetById(m.id, out var poiInfo) && poiInfo.hasLabelDisplayOverride)
                        mode = poiInfo.labelDisplayOverride;
                    else if (HasAlwaysLabelFlag(m.meta))
                        mode = MapUIStyleSettings.MapLabelDisplayMode.Always;

                    _poiLabelDisplayOverrides[m.id] = mode;
                }
                else
                {
                    _poiLabelDisplayOverrides[m.id] = MapUIStyleSettings.MapLabelDisplayMode.Contextual;
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

                _markerVisuals[m.id] = (dot, spriteOutline, spriteBody, glyphLabel, label);
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

            RefreshRacePolylineVisualOverrides();
            _polyLayer?.SetColorOverrides(_polylineColorOverrides);

        }

        private void RefreshRacePolylineVisualOverrides()
        {
            if (_mapData == null || _mapData.Polylines == null)
                return;

            for (int i = 0; i < _mapData.Polylines.Count; i++)
            {
                var polyline = _mapData.Polylines[i];
                if (!polyline.IsValid || polyline.lineType != MapLineType.RaceCourse)
                    continue;

                if (!TryGetRaceCourseLineForPolyline(polyline.id, out var race) || race == null)
                    continue;

                Color fallback = polyline.color.a > 0.001f
                    ? polyline.color
                    : new Color(1f, 1f, 1f, 0.70f);

                Color resolved = race.GetResolvedMapLineColor(fallback);
                Color opaque = new Color(resolved.r, resolved.g, resolved.b, 1f);
                _polylineColorOverrides[polyline.id] = opaque;
                _polylineLabelAccent[polyline.id] = opaque;
            }
        }

        public void ForceOverlayLabelRefresh()
        {
            RebuildRegionOverlayLabels();
            LayoutOverlayLabels();
            ApplyGlobalLabelVisibility();
            _polyHost?.MarkDirtyRepaint();
            _markerHost?.MarkDirtyRepaint();
        }

        private bool TryGetRaceCourseLineForPolyline(string polylineId, out RaceCourseLine race)
        {
            race = null;

            if (string.IsNullOrWhiteSpace(polylineId))
                return false;

            if (TryFindRaceActivityMarkerForPolyline(polylineId, out var marker) &&
                marker.source is RaceCourseLine markerRace &&
                markerRace != null)
            {
                race = markerRace;
                return true;
            }

            return false;
        }

        private void RebuildWaypointMarkers()
        {
            if (_markerHost == null || _waypointManager == null)
                return;

            EnsureLabelOverlay();

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
                dot.style.justifyContent = Justify.Center;
                dot.style.alignItems = Align.Center;
                dot.pickingMode = PickingMode.Position;

                var spriteOutline = new VisualElement();
                spriteOutline.name = "WaypointSpriteOutline";
                spriteOutline.style.position = Position.Absolute;
                spriteOutline.style.left = 0f;
                spriteOutline.style.top = 0f;
                spriteOutline.style.right = 0f;
                spriteOutline.style.bottom = 0f;
                spriteOutline.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                spriteOutline.style.display = DisplayStyle.None;
                spriteOutline.pickingMode = PickingMode.Ignore;

                var spriteBody = new VisualElement();
                spriteBody.name = "WaypointSpriteBody";
                spriteBody.style.position = Position.Absolute;
                spriteBody.style.left = 0f;
                spriteBody.style.top = 0f;
                spriteBody.style.right = 0f;
                spriteBody.style.bottom = 0f;
                spriteBody.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                spriteBody.style.display = DisplayStyle.None;
                spriteBody.pickingMode = PickingMode.Ignore;

                dot.Add(spriteOutline);
                dot.Add(spriteBody);

                bool selected = _waypointManager.IsWaypointSelected(wp.id);
                bool active = string.Equals(_waypointManager.ActiveWaypointId, wp.id, StringComparison.Ordinal);

                bool labelEditable = string.IsNullOrWhiteSpace(wp.sourceKey);

                var label = new Label(wp.displayName);
                label.AddToClassList("map-waypoint-label");
                label.style.position = Position.Absolute;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.left = 0f;
                label.style.top = 0f;
                label.style.width = StyleKeyword.Auto;
                label.style.minWidth = StyleKeyword.Auto;
                label.style.maxWidth = StyleKeyword.None;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.pickingMode = PickingMode.Position;
                label.style.visibility = Visibility.Hidden;

                // Never let a newly-built waypoint label become visible until it has a valid viewport rect.
                label.style.display = DisplayStyle.None;

                // If this waypoint already had a valid on-screen placement, restore it immediately.
                if (_waypointLabelViewportRects.TryGetValue(wp.id, out var cachedRect) &&
                    float.IsFinite(cachedRect.x) && float.IsFinite(cachedRect.y) &&
                    float.IsFinite(cachedRect.width) && float.IsFinite(cachedRect.height) &&
                    cachedRect.width > 0f && cachedRect.height > 0f)
                {
                    label.style.left = cachedRect.x;
                    label.style.top = cachedRect.y;
                    label.style.width = cachedRect.width;
                    label.style.height = cachedRect.height;
                    label.style.display = DisplayStyle.Flex;
                    label.style.visibility = Visibility.Visible;
                }

                void OpenRenameEditor()
                {
                    if (!labelEditable)
                    {
                        WaypointClicked?.Invoke(wp.id);
                        return;
                    }

                    bool selectedNow = _waypointManager != null && _waypointManager.IsWaypointSelected(wp.id);
                    float renameLabelMultiplier = wp.labelSizeMultiplier > 0f ? Mathf.Max(0.1f, wp.labelSizeMultiplier) : 1f;

                    OpenWaypointRenameEditor(
                        wp.id,
                        wp.color,
                        wp.displayName,
                        selectedNow,
                        renameLabelMultiplier);
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
                _markerHost.Add(root);

                _waypointMarkerRoots[wp.id] = root;
                _waypointMarkerDots[wp.id] = dot;
                _waypointLabels[wp.id] = label;
                _waypointAnchorLocal[wp.id] = local;

                // Fully style and normalize the label before it enters the live overlay tree.
                ApplyWaypointVisual(wp.id, selected, active);
                ApplyWaypointLabelVisual(wp.id, selected);

                // If we already have a committed rect, restore it now.
                // Otherwise keep the label hidden until LayoutWaypointLabels() solves one.
                if (_waypointLabelViewportRects.TryGetValue(wp.id, out var restoredRect) &&
                    float.IsFinite(restoredRect.x) && float.IsFinite(restoredRect.y) &&
                    float.IsFinite(restoredRect.width) && float.IsFinite(restoredRect.height) &&
                    restoredRect.width > 0f && restoredRect.height > 0f)
                {
                    SetWaypointLabelViewportRect(wp.id, restoredRect);
                }
                else
                {
                    label.style.display = DisplayStyle.None;
                    label.style.visibility = Visibility.Hidden;
                    SyncWaypointLabelTransformImmediate(label);
                }

                _labelOverlay.Add(label);
            }

            // Restore the last known-good placement immediately so rebuilds do not flash labels
            // back to an origin/default position before the next authoritative solve.
            ApplyCachedWaypointLabelPlacement();

            MarkWaypointLabelLayoutDirty();
            LayoutWaypointLabels();
        }

        private void RefreshWaypointVisuals()
        {
            foreach (var kv in _waypointMarkerRoots)
                kv.Value?.RemoveFromHierarchy();

            foreach (var kv in _waypointLabels)
                kv.Value?.RemoveFromHierarchy();

            if (_activeWaypointRenameField != null)
            {
                if (_activeWaypointRenameField.parent != null)
                    _activeWaypointRenameField.RemoveFromHierarchy();

                _activeWaypointRenameField = null;
                _activeWaypointRenameId = null;
            }

            _waypointMarkerRoots.Clear();
            _waypointMarkerDots.Clear();
            _waypointLabels.Clear();
            _waypointAnchorLocal.Clear();

            PruneWaypointLayoutCaches();

            MarkWaypointLabelLayoutDirty();

            RebuildWaypointMarkers();
            ApplyLinkedSourceWaypointVisuals();

            // Selection visuals can restyle waypoint labels after rebuild.
            UpdateSelectionVisuals();

            // Rebuild must always end with one authoritative waypoint layout pass.
            MarkWaypointLabelLayoutDirty();
            LayoutWaypointLabels();
            UpdateWaypointRenameEditorPosition();
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

            // Waypoint labels are viewport-overlay labels.
            // During pan/zoom, only replay already-valid cached placement.
            ApplyCachedWaypointLabelPlacement();

            // Keep the rename field anchored without forcing any second layout path.
            UpdateWaypointRenameEditorPosition();

            // Overlay labels live in viewport space, so they should not receive any extra zoom transform.
            // Their effective size should come only from ComputeZoomedLabelFontSize(...).
            void ResetOverlayLabelTransform(Label lab)
            {
                if (lab == null)
                    return;

                lab.style.scale = new Scale(Vector3.one);
                lab.style.translate = new Translate(0f, 0f, 0f);
            }

            foreach (var kv in _polylineLabelVisuals)
                ResetOverlayLabelTransform(kv.Value);

            foreach (var kv in _poiOverlayLabels)
                ResetOverlayLabelTransform(kv.Value);

            foreach (var kv in _regionOverlayLabels)
                ResetOverlayLabelTransform(kv.Value);

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

                case SkiGame.POI.POICategory.Race:
                    return new Color(1.00f, 0.55f, 0.20f, 1f);

                case SkiGame.POI.POICategory.Medical:
                    return new Color(0.20f, 1.00f, 1.00f, 1f);

                case SkiGame.POI.POICategory.Vehicle:
                    return new Color(1.00f, 0.90f, 0.25f, 1f);

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
                RegisterOverlayLabelCallbacks(label);

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

            if (_style != null &&
                _style.runLabelAnchorMode == MapUIStyleSettings.RunLabelAnchorMode.LinkedMarker &&
                p.lineType == MapLineType.SkiRun &&
                _polylineToMarker.TryGetValue(p.id, out var markerId) &&
                _markerAnchorLocal.TryGetValue(markerId, out var markerAnchor))
            {
                if (TryFindNearestPolylineAnchorAndNormal(pts, markerAnchor, out anchor, out normal))
                    return true;

                anchor = markerAnchor;
                normal = Vector2.down;
                return true;
            }

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
                RegisterOverlayLabelCallbacks(label);

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

        public void SetRegionInteractivity(bool selectable, bool showLabels = true)
        {
            _regionsSelectable = selectable;
            _showRegionOverlayLabels = showLabels;

            if (!_showRegionOverlayLabels)
            {
                foreach (var kv in _regionOverlayLabels)
                {
                    if (kv.Value != null)
                        kv.Value.style.display = DisplayStyle.None;
                }
            }
            else
            {
                LayoutOverlayLabels();
            }
        }

        public bool FrameLiftPolylinesForPassLevel(int passLevel, float paddingPx = 36f, float minZoom = 0.85f)
        {
            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;
            string selectedPassId = cfg != null ? cfg.GetPassIdForLevel(passLevel) : string.Empty;
            return FrameLiftPolylinesForPassId(selectedPassId, passLevel, paddingPx, minZoom);
        }

        public bool FrameLiftPolylinesForPassId(string selectedPassId, int selectedLevel, float paddingPx = 36f, float minZoom = 0.85f)
        {
            if (_mapData == null || _mapData.Polylines == null || !_bound)
                return false;

            if (!TryGetViewportSize(out float vw, out float vh))
                return false;

            selectedLevel = Mathf.Max(0, selectedLevel);
            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;

            bool foundAny = false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < _mapData.Polylines.Count; i++)
            {
                var poly = _mapData.Polylines[i];
                if (!poly.IsValid || poly.lineType != MapLineType.SkiLift)
                    continue;

                int requiredLevel = 0;
                _liftRequiredLevelByPolyline.TryGetValue(poly.id, out requiredLevel);

                bool accessible = selectedLevel >= requiredLevel;
                if (cfg != null &&
                    !string.IsNullOrWhiteSpace(selectedPassId) &&
                    _liftRequiredPassIdByPolyline.TryGetValue(poly.id, out string requiredPassId) &&
                    !string.IsNullOrWhiteSpace(requiredPassId))
                {
                    accessible = cfg.PassGrantsAccessTo(selectedPassId, requiredPassId);
                }

                if (!accessible)
                    continue;

                if (poly.Has3DPoints)
                {
                    for (int k = 0; k < poly.pointsWorld.Count; k++)
                    {
                        if (!TryProjectWorldToLocal(poly.pointsWorld[k], out var local))
                            continue;

                        foundAny = true;
                        minX = Mathf.Min(minX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxX = Mathf.Max(maxX, local.x);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }
                else if (poly.HasXZPoints)
                {
                    for (int k = 0; k < poly.pointsWorldXZ.Count; k++)
                    {
                        if (!TryProjectWorldXZToLocal(poly.pointsWorldXZ[k], out var local))
                            continue;

                        foundAny = true;
                        minX = Mathf.Min(minX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxX = Mathf.Max(maxX, local.x);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }
            }

            if (!foundAny)
                return false;

            float width = Mathf.Max(8f, maxX - minX);
            float height = Mathf.Max(8f, maxY - minY);

            float usableW = Mathf.Max(32f, vw - paddingPx * 2f);
            float usableH = Mathf.Max(32f, vh - paddingPx * 2f);

            _zoom = Mathf.Max(minZoom, Mathf.Min(usableW / width, usableH / height));

            Vector2 centerLocal = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            CenterViewOnContentPoint(centerLocal);
            return true;
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
                RegisterOverlayLabelCallbacks(label);

                string regionId = id;
                if (_regionsSelectable)
                    MakeOverlayLabelInteractive(label, () => SelectRegion(regionId, zoomToRegion: true));

                _regionOverlayLabels[id] = label;
                _labelOverlay.Add(label);

                // Important: region labels must start hidden.
                // LayoutOverlayLabels() is the only place that should decide when and where they appear.
                // If they start visible and the current layout path returns early (for example when a lift is selected),
                // they can remain at their default 0,0 position in the top-left.
                label.style.display = DisplayStyle.None;
                label.style.visibility = Visibility.Hidden;

                ApplyRegionOverlayLabelVisual(id, selected: _regionsSelectable && string.Equals(_selectedRegionId, id, StringComparison.Ordinal));
            }

            LayoutOverlayLabels();
        }

        private void ApplyPOIOverlayLabelVisual(string markerId, bool selected)
        {
            if (!_poiOverlayLabels.TryGetValue(markerId, out var label) || label == null)
                return;

            ResolvedMapLabelRule rule = ResolveLabelRuleForMarker(markerId);
            var style = ResolveStandardLabelStyleForRule(rule);

            Color accent = _markerLabelAccent.TryGetValue(markerId, out var accentColor)
                ? accentColor
                : Color.white;

            Color textColor = selected ? style.selectedColor : style.color;
            if (TryGetLinkedWaypointForMarker(markerId, out var linkedWp))
                textColor = linkedWp.color;

            ApplySharedOverlayLabelVisual(
                label,
                style,
                selected,
                accent,
                rule.labelSizeMultiplier,
                textColor);
        }

        private void ApplyRegionOverlayLabelVisual(string regionId, bool selected)
        {
            if (!_regionOverlayLabels.TryGetValue(regionId, out var label) || label == null)
                return;
            if (_regionSet == null)
                return;

            var style = GetLabelStyleById(RegionMajorLabelStyleId);

            Color accent = Color.white;
            var face = _regionSet.GetFaceById(regionId);
            if (face != null && face.borderColor.a > 0.001f)
                accent = face.borderColor;

            ApplySharedOverlayLabelVisual(
                label,
                style,
                selected,
                accent,
                1f,
                selected ? style.selectedColor : style.color);
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

            ResolvedMapLabelRule regionRule = ResolveRegionLabelRule();
            float regionOverviewAlpha = EvaluateRevealAlpha(regionRule, relativeZoom);
            bool showRegionOverview = regionOverviewAlpha > 0.02f;

            // ---- Helpers ----
            int ComputeFont(bool selected)
            {
                return ComputeZoomedLabelFontSize(GetStandardLabelStyle(), selected);
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
            bool suppressNonRegionLabels = _hideMarkerLabels || !_showNonRegionLabels;
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
                    ResolvedMapLabelRule selectedPolyRule = TryGetPolylineById(_selectedPolylineId, out var selectedPolyline)
                        ? ResolveLabelRuleForPolyline(selectedPolyline)
                        : ResolveFallbackRule(MapUIStyleSettings.MapElementSemantic.SkiRun, POIType.SkiRun, POICategory.None);

                    polyLabel.style.fontSize = ComputeZoomedLabelFontSize(ResolveStandardLabelStyleForRule(selectedPolyRule), true, Mathf.Max(0.75f, selectedPolyRule.labelSizeMultiplier));
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
                    ResolvedMapLabelRule selectedMarkerRule = ResolveLabelRuleForMarker(_selectedMarkerId);
                    poiLabel.style.fontSize = ComputeZoomedLabelFontSize(ResolveStandardLabelStyleForRule(selectedMarkerRule), true, Mathf.Max(0.75f, selectedMarkerRule.labelSizeMultiplier));
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

                if (_regionOverlayLabels.Count > 0)
                {
                    foreach (var kv in _regionOverlayLabels)
                    {
                        var regionLabel = kv.Value;
                        if (regionLabel == null)
                            continue;

                        regionLabel.style.display = DisplayStyle.None;
                        regionLabel.style.visibility = Visibility.Hidden;
                    }
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
                        ? Mathf.Max(ComputeZoomedLabelFontSize(GetRegionLabelStyle(), true, regionRule.labelSizeMultiplier), 16)
                        : Mathf.Max(ComputeZoomedLabelFontSize(GetRegionLabelStyle(), false, regionRule.labelSizeMultiplier), isRoot ? 13 : 12);

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
            var polyContext = new List<(string id, Label label, Vector2 anchor, Vector2 n, int priority, int maxCount, float dist)>(128);

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

                bool linkedWaypoint = TryGetLinkedWaypointForPolyline(id, out _);
                ResolvedMapLabelRule rule = ResolveLabelRuleForPolyline(p);
                float revealAlpha = EvaluateRevealAlpha(rule, relativeZoom);
                bool show = !showRegionOverview &&
                            (isSelected || (layerOn && ShouldDisplayLabelByMode(rule, isSelected, linkedWaypoint) && revealAlpha > 0.02f));

                if (!show)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                _polyAnchorLocal.TryGetValue(id, out var anchor);
                _polyNormalLocal.TryGetValue(id, out var n);

                float dist = (anchor - focusContent).sqrMagnitude;

                // Style
                label.style.fontSize = ComputeZoomedLabelFontSize(ResolveStandardLabelStyleForRule(rule), isSelected, Mathf.Max(0.75f, rule.labelSizeMultiplier));
                label.style.opacity = isSelected ? 1f : revealAlpha;
                ApplyPolylineLabelVisual(id, selected: isSelected);

                if (isSelected)
                {
                    work.Add((id, label, anchor, n, priority: 0, mustShow: true, distKey: dist));
                }
                else
                {
                    var profile = ResolveLabelProfileForRule(rule);
                    polyContext.Add((id, label, anchor, n, profile.labelPriority, profile.maxContextCount, dist));
                }
            }

            polyContext.Sort((a, b) =>
            {
                int c = a.priority.CompareTo(b.priority);
                return c != 0 ? c : a.dist.CompareTo(b.dist);
            });
            var polyCounts = new Dictionary<int, int>();
            for (int i = 0; i < polyContext.Count; i++)
            {
                var it = polyContext[i];
                int used = polyCounts.TryGetValue(it.priority, out var count) ? count : 0;
                int maxCount = it.maxCount > 0 ? it.maxCount : DefaultMaxContextRunLiftLabels;
                if (used >= maxCount)
                {
                    polyContext[i].label.style.display = DisplayStyle.None;
                    continue;
                }
                polyCounts[it.priority] = used + 1;
                work.Add((it.id, it.label, it.anchor, it.n, priority: it.priority, mustShow: false, distKey: it.dist));
            }

            // --- POIs ---
            var poiContext = new List<(string id, Label label, Vector2 anchor, int priority, int maxCount, float dist)>(128);

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
                bool linkedWaypoint = TryGetLinkedWaypointForMarker(id, out _);
                ResolvedMapLabelRule rule = ResolveLabelRuleForMarker(id);
                float revealAlpha = EvaluateRevealAlpha(rule, relativeZoom);
                bool show = !showRegionOverview &&
                            IsMarkerVisibleById(id) &&
                            (isSelected || (ShouldDisplayLabelByMode(rule, isSelected, linkedWaypoint) && revealAlpha > 0.02f));
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

                label.style.fontSize = ComputeZoomedLabelFontSize(ResolveStandardLabelStyleForRule(rule), isSelected, Mathf.Max(0.75f, rule.labelSizeMultiplier));
                ApplyPOIOverlayLabelVisual(id, isSelected);
                label.style.opacity = isSelected ? 1f : revealAlpha;

                if (isSelected)
                {
                    work.Add((id, label, anchor, Vector2.down, priority: 1, mustShow: true, distKey: dist));
                }
                else
                {
                    var profile = ResolveLabelProfileForRule(rule);
                    poiContext.Add((id, label, anchor, profile.labelPriority, profile.maxContextCount, dist));
                }
            }

            poiContext.Sort((a, b) =>
            {
                int c = a.priority.CompareTo(b.priority);
                return c != 0 ? c : a.dist.CompareTo(b.dist);
            });

            var poiCounts = new Dictionary<int, int>();
            for (int i = 0; i < poiContext.Count; i++)
            {
                var it = poiContext[i];
                int used = poiCounts.TryGetValue(it.priority, out var count) ? count : 0;
                int maxCount = it.maxCount > 0 ? it.maxCount : DefaultMaxContextPoiLabels;
                if (used >= maxCount)
                {
                    poiContext[i].label.style.display = DisplayStyle.None;
                    continue;
                }
                poiCounts[it.priority] = used + 1;
                work.Add((it.id, it.label, it.anchor, Vector2.down, priority: it.priority, mustShow: false, distKey: it.dist));
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

        public void SetActivityCheckpointMarkers(IReadOnlyList<Vector3> worldPositions, int currentCheckpointIndex, bool hideBaseMarkers)
        {
            _activityCheckpointWorldPositions.Clear();

            if (worldPositions != null)
            {
                for (int i = 0; i < worldPositions.Count; i++)
                    _activityCheckpointWorldPositions.Add(worldPositions[i]);
            }

            _activityCurrentCheckpointIndex = currentCheckpointIndex;
            _hideBaseMarkersForActivity = hideBaseMarkers;

            RefreshActivityCheckpointMarkers();
            ApplyLayerVisibility();
        }

        public void ClearActivityCheckpointMarkers()
        {
            _activityCheckpointWorldPositions.Clear();
            _activityCurrentCheckpointIndex = -1;
            _hideBaseMarkersForActivity = false;

            RefreshActivityCheckpointMarkers();
            ApplyLayerVisibility();
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
            if (_content == null)
                return;

            _content.transform.position = new Vector3(_pan.x, _pan.y, 0f);
            _content.transform.scale = new Vector3(_zoom, _zoom, 1f);

            _regionLayer?.SetZoom(_zoom);
            _polyLayer?.SetZoom(_zoom);
            _trailLayer?.SetZoom(_zoom);

            // Keep marker visuals screen-locked.
            UpdateAllLabelTransformsForZoom();

            // Regular overlay labels live in viewport space, so they must always
            // reflow when the map pan/zoom transform changes.
            LayoutOverlayLabels();

            // Waypoint labels use a more conservative path to avoid visible jitter.
            // Reapply the last valid placement immediately, and only run a fresh solve
            // when some other path has explicitly dirtied their layout.
            ApplyCachedWaypointLabelPlacement();

            if (_waypointLabelLayoutDirty)
                LayoutWaypointLabels();
            else
                UpdateWaypointRenameEditorPosition();

            _navigationOverlay?.MarkDirtyRepaint();
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

        private MapUIStyleSettings.MapMarkerVisualStyle GetPoiMarkerStyle() => _style != null ? _style.GetPoiMarkerStyle() : new MapUIStyleSettings.MapMarkerVisualStyle { size = 14f, selectedSize = 18f, borderWidth = 2f, activeBorderWidth = 2f, borderColor = new Color(0f, 0f, 0f, 0.65f), activeBorderColor = new Color(0f, 0f, 0f, 0.65f), fillColor = Color.white };
        private MapUIStyleSettings.MapMarkerVisualStyle GetWaypointMarkerStyle() => _style != null ? _style.GetWaypointMarkerStyle() : new MapUIStyleSettings.MapMarkerVisualStyle { size = 16f, selectedSize = 20f, borderWidth = 2f, activeBorderWidth = 2f, borderColor = new Color(0f, 0f, 0f, 0.70f), activeBorderColor = Color.white, fillColor = Color.white };
        private MapUIStyleSettings.MapMarkerVisualStyle GetPlayerMarkerStyle() => _style != null ? _style.GetPlayerMarkerStyle() : new MapUIStyleSettings.MapMarkerVisualStyle { size = 20f, selectedSize = 20f, borderWidth = 2f, activeBorderWidth = 2f, borderColor = new Color(0f, 0f, 0f, 0.75f), activeBorderColor = new Color(0f, 0f, 0f, 0.75f), fillColor = Color.cyan };
        private MapUIStyleSettings.MapMarkerVisualStyle GetLegendMarkerStyle() => _style != null ? _style.GetLegendMarkerStyle() : new MapUIStyleSettings.MapMarkerVisualStyle { size = 14f, selectedSize = 14f, borderWidth = 2f, activeBorderWidth = 2f, borderColor = new Color(0f, 0f, 0f, 0.65f), activeBorderColor = new Color(0f, 0f, 0f, 0.65f), fillColor = Color.white };
        private MapUIStyleSettings.MapArrowVisualStyle GetNavigationArrowStyle() => _style != null ? _style.GetNavigationArrowStyle() : new MapUIStyleSettings.MapArrowVisualStyle { size = 22f };
        private MapUIStyleSettings.MapLabelVisualStyle GetStandardLabelStyle() => _style != null
            ? _style.GetFallbackStandardLabelStyle()
            : new MapUIStyleSettings.MapLabelVisualStyle
            {
                fontSize = 12,
                selectedFontSize = 13,
                fontStyle = FontStyle.Bold,
                selectedFontStyle = FontStyle.Bold,
                color = Color.white,
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
            };

        private MapUIStyleSettings.MapLabelVisualStyle GetWaypointLabelStyle() =>
            GetLabelStyleById(WaypointEditableLabelStyleId);

        private MapUIStyleSettings.MapLabelVisualStyle GetRegionLabelStyle() =>
            GetLabelStyleById(RegionMajorLabelStyleId);
        private MapUIStyleSettings.MapLineVisualStyle GetPoiPolylineStyle() => _style != null ? _style.GetPoiPolylineStyle() : new MapUIStyleSettings.MapLineVisualStyle { widthPx = 2.8f, selectedWidthMultiplier = 1.35f, outlineExtraPx = 2f, outlineExtraSelectedPx = 3f, outlineColor = new Color(0f, 0f, 0f, 0.35f), outlineColorSelected = new Color(0f, 0f, 0f, 0.55f), widthZoomExponent = 0.35f, widthZoomMinMul = 0.70f, widthZoomMaxMul = 1.60f };
        private MapUIStyleSettings.MapLineVisualStyle GetTrailLineStyle() => _style != null ? _style.GetTrailLineStyle() : new MapUIStyleSettings.MapLineVisualStyle { widthPx = 2f, outlineColor = new Color(0f, 0f, 0f, 0.40f), outlineColorSelected = new Color(0f, 0f, 0f, 0.40f), widthZoomMinMul = 1f, widthZoomMaxMul = 1f };

        private int ComputeZoomedLabelFontSize(bool selected) => ComputeZoomedLabelFontSize(GetStandardLabelStyle(), selected, 1f);

        private int ComputeZoomedLabelFontSize(MapUIStyleSettings.MapLabelVisualStyle style, bool selected, float sizeMultiplier = 1f)
        {
            int baseSize = selected ? style.selectedFontSize : style.fontSize;
            float z = Mathf.Max(0.0001f, _zoom);
            float comp = Mathf.Clamp01(style.zoomCompensation);
            float exp = 1f - comp;
            float scaled = baseSize * Mathf.Pow(z, exp) * Mathf.Max(0f, sizeMultiplier);
            int size = Mathf.RoundToInt(scaled);
            int min = Mathf.Max(6, style.fontMin);
            int max = Mathf.Max(min, style.fontMax);
            return Mathf.Clamp(size, min, max);
        }

        private void ApplySharedOverlayLabelVisual(
    Label label,
    MapUIStyleSettings.MapLabelVisualStyle style,
    bool selected,
    Color accentColor,
    float sizeMultiplier = 1f,
    Color? textColorOverride = null)
        {
            if (label == null)
                return;

            int fontSize = ComputeZoomedLabelFontSize(style, selected, sizeMultiplier);

            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = selected ? style.selectedFontStyle : style.fontStyle;
            label.style.color = textColorOverride ?? (selected ? style.selectedColor : style.color);

            float stripeW = Mathf.Max(0f, style.accentStripeWidth);
            float padX = Mathf.Max(0f, style.paddingX);
            float padY = Mathf.Max(0f, style.paddingY);
            float radius = Mathf.Max(0f, style.cornerRadius);
            float plateAlpha = Mathf.Clamp01(style.plateAlpha);

            label.style.borderLeftWidth = stripeW;
            label.style.borderLeftColor = accentColor;
            label.style.backgroundColor = new Color(accentColor.r, accentColor.g, accentColor.b, plateAlpha);
            label.style.paddingLeft = padX;
            label.style.paddingRight = padX;
            label.style.paddingTop = padY;
            label.style.paddingBottom = padY;
            label.style.minHeight = 0f;

            label.style.borderTopLeftRadius = radius;
            label.style.borderTopRightRadius = radius;
            label.style.borderBottomLeftRadius = radius;
            label.style.borderBottomRightRadius = radius;

            label.style.width = StyleKeyword.Auto;
            label.style.minWidth = StyleKeyword.Auto;
            label.style.maxWidth = StyleKeyword.None;

            label.style.opacity = selected ? 1f : 0.95f;
        }
        private float GetWaypointCompactFontSize(bool selected)
        {
            var style = GetLabelStyleById(WaypointEditableLabelStyleId);
            return selected ? style.selectedFontSize : style.fontSize;
        }

        private float GetRelativeZoom()
        {
            if (_viewport == null || !TryGetViewportSize(out float vw, out float vh))
                return _zoom;

            float fitZoom = Mathf.Min(
                vw / Mathf.Max(1f, _contentSize.x),
                vh / Mathf.Max(1f, _contentSize.y));

            return _zoom / Mathf.Max(0.0001f, fitZoom);
        }

        private static MapUIStyleSettings.MapElementSemantic ResolveSemantic(POIType poiType, POICategory poiCategory)
        {
            if (poiType == POIType.SkiRun)
                return MapUIStyleSettings.MapElementSemantic.SkiRun;
            if (poiType == POIType.SkiLift)
                return MapUIStyleSettings.MapElementSemantic.SkiLift;

            return poiCategory switch
            {
                POICategory.Resort => MapUIStyleSettings.MapElementSemantic.Resort,
                POICategory.Shop => MapUIStyleSettings.MapElementSemantic.Shop,
                POICategory.Kiosk => MapUIStyleSettings.MapElementSemantic.Kiosk,
                POICategory.Service => MapUIStyleSettings.MapElementSemantic.Service,
                POICategory.Landmark => MapUIStyleSettings.MapElementSemantic.Landmark,
                POICategory.Race => MapUIStyleSettings.MapElementSemantic.Race,
                POICategory.Medical => MapUIStyleSettings.MapElementSemantic.Medical,
                POICategory.Vehicle => MapUIStyleSettings.MapElementSemantic.Vehicle,
                POICategory.Custom => MapUIStyleSettings.MapElementSemantic.CustomPoi,
                _ => MapUIStyleSettings.MapElementSemantic.GenericPoi
            };
        }

        private ResolvedMapLabelRule ResolveLabelRuleForMarker(string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId) || !_markerById.TryGetValue(markerId, out var marker))
                return ResolveFallbackRule(MapUIStyleSettings.MapElementSemantic.GenericPoi, POIType.Unknown, POICategory.None);

            POIType poiType = marker.type;
            POICategory poiCategory = ResolvePoiCategoryForMarkerId(markerId, poiType);
            var semantic = MapUIStyleSettings.ResolveSemantic(poiType, poiCategory);

            if (_style != null && _style.TryResolveVisibilityRule(semantic, poiType, poiCategory, out var rule))
            {
                return new ResolvedMapLabelRule
                {
                    semantic = semantic,
                    markerDisplayMode = rule.markerDisplayMode,
                    labelStyleId = string.IsNullOrWhiteSpace(rule.labelStyleId) ? StandardLabelStyleId : rule.labelStyleId,
                    labelSizeMultiplier = rule.labelSizeMultiplier > 0f ? rule.labelSizeMultiplier : 1f
                };
            }

            return ResolveFallbackRule(semantic, poiType, poiCategory);
        }

        private ResolvedMapLabelRule ResolveLabelRuleForPolyline(MapPolyline polyline)
        {
            if (!polyline.IsValid)
                return ResolveFallbackRule(MapUIStyleSettings.MapElementSemantic.GenericPoi, POIType.Unknown, POICategory.None);

            POIType poiType = POIType.Unknown;
            POICategory poiCategory = POICategory.None;

            switch (polyline.lineType)
            {
                case MapLineType.SkiRun:
                    poiType = POIType.SkiRun;
                    break;
                case MapLineType.SkiLift:
                    poiType = POIType.SkiLift;
                    break;
                case MapLineType.RaceCourse:
                    poiType = POIType.Custom;
                    poiCategory = POICategory.Race;
                    break;
            }

            var semantic = MapUIStyleSettings.ResolveSemantic(poiType, poiCategory);

            if (_style != null && _style.TryResolveVisibilityRule(semantic, poiType, poiCategory, out var rule))
            {
                return new ResolvedMapLabelRule
                {
                    semantic = semantic,
                    markerDisplayMode = rule.markerDisplayMode,
                    labelStyleId = string.IsNullOrWhiteSpace(rule.labelStyleId) ? StandardLabelStyleId : rule.labelStyleId,
                    labelSizeMultiplier = rule.labelSizeMultiplier > 0f ? rule.labelSizeMultiplier : 1f
                };
            }

            return ResolveFallbackRule(semantic, poiType, poiCategory);
        }

        private ResolvedMapLabelRule ResolveRegionLabelRule()
        {
            return new ResolvedMapLabelRule
            {
                semantic = MapUIStyleSettings.MapElementSemantic.Region,
                markerDisplayMode = MapUIStyleSettings.MapLabelDisplayMode.Contextual,
                labelStyleId = RegionMajorLabelStyleId,
                labelSizeMultiplier = 1f
            };
        }

        private POICategory ResolvePoiCategoryForMarkerId(string markerId, POIType poiType)
        {
            if (_poiRegistry != null && _poiRegistry.TryGetById(markerId, out var poiInfo))
                return poiInfo.category;

            return poiType == POIType.Custom ? POICategory.Custom : POICategory.None;
        }

        private ResolvedMapLabelRule ResolveFallbackRule(MapUIStyleSettings.MapElementSemantic semantic, POIType poiType, POICategory poiCategory)
        {
            string styleId = StandardLabelStyleId;

            if (semantic == MapUIStyleSettings.MapElementSemantic.Region)
                styleId = RegionMajorLabelStyleId;
            else if (poiCategory == POICategory.Resort)
                styleId = MajorLabelStyleId;
            else if (poiType == POIType.Custom && (
                         poiCategory == POICategory.Shop ||
                         poiCategory == POICategory.Kiosk ||
                         poiCategory == POICategory.Service ||
                         poiCategory == POICategory.Vehicle ||
                         poiCategory == POICategory.Custom))
                styleId = MinorLabelStyleId;

            return new ResolvedMapLabelRule
            {
                semantic = semantic,
                markerDisplayMode = MapUIStyleSettings.MapLabelDisplayMode.Contextual,
                labelStyleId = styleId,
                labelSizeMultiplier = 1f
            };
        }

        private ResolvedMarkerStyleRule ResolveMarkerStyleRuleForPoi(POIType type, POICategory category)
        {
            var resolved = new ResolvedMarkerStyleRule
            {
                markerSizeMultiplier = 1f,
                colorIsFallbackOnly = true
            };

            if (_style != null && _style.TryFindBestVisibilityRule(type, category, out var rule))
            {
                resolved.hasMarkerSprite = rule.overrideMarkerSprite && rule.markerSprite != null;
                resolved.markerSprite = rule.markerSprite;
                resolved.markerSizeMultiplier = rule.overrideMarkerSizeMultiplier ? Mathf.Max(0.1f, rule.markerSizeMultiplier) : 1f;
                resolved.hasDefaultColor = rule.overrideMarkerDefaultColor;
                resolved.defaultColor = rule.markerDefaultColor;
                resolved.colorIsFallbackOnly = rule.markerColorIsFallbackOnly;
            }

            return resolved;
        }

        private ResolvedMarkerStyleRule ResolveMarkerStyleRuleForPolyline(MapPolyline polyline)
        {
            if (polyline.lineType == MapLineType.SkiLift)
                return ResolveMarkerStyleRuleForPoi(POIType.SkiLift, POICategory.None);

            if (polyline.lineType == MapLineType.SkiRun)
                return ResolveMarkerStyleRuleForPoi(POIType.SkiRun, POICategory.None);

            return ResolveMarkerStyleRuleForPoi(POIType.Unknown, POICategory.None);
        }

        private ResolvedMarkerRule ResolveMarkerRule(POIType poiType, POICategory poiCategory)
        {
            var result = new ResolvedMarkerRule
            {
                sprite = null,
                sizeMultiplier = 1f,
                hasDefaultColor = false,
                defaultColor = Color.white,
                colorIsFallbackOnly = true
            };

            if (_style == null)
                return result;

            if (_style.TryResolveVisibilityRule(
                    MapUIStyleSettings.ResolveSemantic(poiType, poiCategory),
                    poiType,
                    poiCategory,
                    out var rule))
            {
                if (rule.overrideMarkerSprite)
                    result.sprite = rule.markerSprite;

                if (rule.overrideMarkerSizeMultiplier && rule.markerSizeMultiplier > 0f)
                    result.sizeMultiplier = Mathf.Max(0.1f, rule.markerSizeMultiplier);

                if (rule.overrideMarkerDefaultColor)
                {
                    result.hasDefaultColor = true;
                    result.defaultColor = rule.markerDefaultColor;
                    result.colorIsFallbackOnly = rule.markerColorIsFallbackOnly;
                }
            }

            return result;
        }

        private MapUIStyleSettings.MapLabelVisualStyle ResolveStandardLabelStyleForRule(ResolvedMapLabelRule rule)
        {
            return GetLabelStyleById(rule.labelStyleId);
        }

        private Color ResolveMapElementColor(Color? sourceColor, ResolvedMarkerStyleRule styleRule, Color familyFallbackColor)
        {
            bool hasSourceColor = sourceColor.HasValue && sourceColor.Value.a > 0.001f;

            if (styleRule.hasDefaultColor && !styleRule.colorIsFallbackOnly)
                return styleRule.defaultColor;

            if (hasSourceColor)
                return sourceColor.Value;

            if (styleRule.hasDefaultColor)
                return styleRule.defaultColor;

            return familyFallbackColor;
        }

        private float EvaluateRevealAlpha(ResolvedMapLabelRule rule, float relativeZoom)
        {
            var profile = ResolveLabelProfileForRule(rule);

            switch (profile.revealDirection)
            {
                case MapUIStyleSettings.MapRevealDirection.Always:
                    return 1f;

                case MapUIStyleSettings.MapRevealDirection.ZoomOutReveal:
                    {
                        float t = Mathf.InverseLerp(profile.fadeStartZoom, profile.fadeEndZoom, relativeZoom);
                        return 1f - Mathf.Clamp01(t);
                    }

                case MapUIStyleSettings.MapRevealDirection.ZoomInReveal:
                default:
                    {
                        float t = Mathf.InverseLerp(profile.fadeStartZoom, profile.fadeEndZoom, relativeZoom);
                        return Mathf.Clamp01(t);
                    }
            }
        }

        private bool ShouldDisplayLabelByMode(ResolvedMapLabelRule rule, bool isSelected, bool linkedWaypoint)
        {
            var profile = ResolveLabelProfileForRule(rule);

            switch (profile.labelDisplayMode)
            {
                case MapUIStyleSettings.MapLabelDisplayMode.Always:
                    return true;

                case MapUIStyleSettings.MapLabelDisplayMode.SelectedOnly:
                    return isSelected || linkedWaypoint;

                default:
                    return true;
            }
        }

        private static bool ShouldDisplayMarkerByMode(ResolvedMapLabelRule rule, bool selected, bool linkedWaypoint)
        {
            if (selected || linkedWaypoint)
                return true;

            return rule.markerDisplayMode != MapUIStyleSettings.MapLabelDisplayMode.SelectedOnly;
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

            if (IsEventInsideLegend(evt))
            {
                _pointerDown = false;
                _dragging = false;
                _activePointerId = -1;
                evt.StopPropagation();
                return;
            }

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
            if (IsEventInsideLegend(evt))
            {
                evt.StopPropagation();
                return;
            }

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
            if (IsEventInsideLegend(evtBase))
            {
                _pointerDown = false;
                _dragging = false;

                if (_viewport != null && _activePointerId != -1 && _viewport.HasPointerCapture(_activePointerId))
                    _viewport.ReleasePointer(_activePointerId);

                _activePointerId = -1;
                evtBase.StopPropagation();
                return;
            }

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

                if (TryPickMarker(contentLocal, pickDistContent, out var pickedMarker) &&
                    (_markerSelectionFilter == null || _markerSelectionFilter(pickedMarker)))
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

                if (_polyLayer != null &&
                    _polyLayer.TryPick(contentLocal, pickDistContent, out var pickedLine) &&
                    (_polylineSelectionFilter == null || _polylineSelectionFilter(pickedLine)))
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
            _trailSampleTimes.Clear();
            _trailSampleDistances.Clear();
            _hasLastTrailSample = false;
            _trailDistanceTravelledMeters = 0f;
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

            RefreshStyleIfNeeded();

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
                    UpdatePlayerMarkerVisual(playerLocal, unscaledDeltaTime);
                }
                else
                {
                    _playerMarker.style.display = DisplayStyle.None;
                    _hasPlayerMarkerAngle = false;
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

            RefreshActivityCheckpointMarkers();
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
            return Mathf.Max(8f, GetPlayerMarkerStyle().size);
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

            if (!TryProjectWorldToUV(_playerTransform.position + flatForward.normalized * PlayerMarkerForwardProbeMeters, out Vector2 uvB))
                return false;

            Vector2 a = UVToLocal(uvA);
            Vector2 b = UVToLocal(uvB);
            Vector2 dir = b - a;

            if (dir.sqrMagnitude < 0.0001f)
                return false;

            angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            return true;
        }

        private void UpdatePlayerMarkerVisual(Vector2 playerLocal, float unscaledDeltaTime)
        {
            if (_playerMarker == null)
                return;

            float markerSize = GetPlayerMarkerRenderSize();

            _playerMarker.style.width = markerSize;
            _playerMarker.style.height = markerSize;

            // Important:
            // The marker already gets centered by the shared translate/scale pass.
            // So this position should be the actual anchor point, not anchor-minus-half-size.
            _playerMarker.style.left = playerLocal.x;
            _playerMarker.style.top = playerLocal.y;

            if (TryGetPlayerFacingAngle(out float targetAngleDeg))
            {
                if (!_hasPlayerMarkerAngle)
                {
                    _playerMarkerAngleDeg = targetAngleDeg;
                    _hasPlayerMarkerAngle = true;
                }
                else
                {
                    float delta = Mathf.Abs(Mathf.DeltaAngle(_playerMarkerAngleDeg, targetAngleDeg));
                    if (delta >= PlayerMarkerIgnoreAngleDelta)
                    {
                        if (delta >= PlayerMarkerSnapAngleDelta)
                        {
                            _playerMarkerAngleDeg = targetAngleDeg;
                        }
                        else
                        {
                            float t = 1f - Mathf.Exp(-Mathf.Max(0f, unscaledDeltaTime) * PlayerMarkerSmoothingPerSecond);
                            _playerMarkerAngleDeg = Mathf.LerpAngle(_playerMarkerAngleDeg, targetAngleDeg, t);
                        }
                    }
                }

                float spriteOffset = GetPlayerMarkerStyle().spriteRotationOffsetDegrees;
                _playerMarker.transform.rotation = Quaternion.Euler(0f, 0f, _playerMarkerAngleDeg + spriteOffset);
            }
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

            // Important: overlay labels live in viewport space, not content space.
            // Programmatic selection/frame paths must explicitly refresh them after pan/zoom changes.
            LayoutOverlayLabels();
            LayoutWaypointLabels();
            _regionLayer?.MarkDirtyRepaint();
            _polyHost?.MarkDirtyRepaint();
            _markerHost?.MarkDirtyRepaint();
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
            float worldDistanceFromLast = 0f;

            if (_hasLastTrailSample)
            {
                worldDistanceFromLast = Vector2.Distance(worldXZ, _lastTrailSampleWorldXZ);

                float breakDistance = _style != null ? Mathf.Max(0f, DefaultTrailBreakDistanceMeters) : 60f;
                if (breakDistance > 0f && worldDistanceFromLast >= breakDistance)
                {
                    _hasLastTrailSample = false;
                    _lastTrailSampleWorldXZ = worldXZ;
                    return;
                }

                if (worldDistanceFromLast < _trailMinDistanceMeters)
                    return;
            }

            if (!TryProjectWorldToUV(_playerTransform.position, out Vector2 uv))
                return;

            if (!float.IsFinite(uv.x) || !float.IsFinite(uv.y))
                return;

            if (uv.x < -0.25f || uv.x > 1.25f || uv.y < -0.25f || uv.y > 1.25f)
                return;

            if (_trailWorldXZ.Count > 0)
            {
                float uvDistance = Vector2.Distance(uv, _trailWorldXZ[_trailWorldXZ.Count - 1]);
                float outlierWorldDistance = _style != null ? Mathf.Max(0f, DefaultTrailOutlierWorldDistanceMeters) : 45f;
                float outlierUvDistance = _style != null ? Mathf.Max(0f, DefaultTrailOutlierUvDistance) : 0.12f;
                if (worldDistanceFromLast > 0f &&
                    worldDistanceFromLast <= outlierWorldDistance &&
                    uvDistance >= outlierUvDistance)
                {
                    return;
                }
            }

            _hasLastTrailSample = true;
            _lastTrailSampleWorldXZ = worldXZ;
            _trailDistanceTravelledMeters += worldDistanceFromLast;
            _trailWorldXZ.Add(uv);
            _trailSampleTimes.Add(now);
            _trailSampleDistances.Add(_trailDistanceTravelledMeters);

            TrimTrailHistory(now);

            // Cap memory
            if (_trailWorldXZ.Count > _trailMaxSamples)
            {
                int trimCount = _trailWorldXZ.Count - _trailMaxSamples;
                _trailWorldXZ.RemoveRange(0, trimCount);
                _trailSampleTimes.RemoveRange(0, trimCount);
                _trailSampleDistances.RemoveRange(0, trimCount);
            }

            _trailLayer.SetWorldTrail(_trailWorldXZ);
        }

        private void TrimTrailHistory(float now)
        {
            float maxAge = _style != null ? Mathf.Max(0f, DefaultTrailMaxAgeSeconds) : 45f;
            float maxDistance = _style != null ? Mathf.Max(0f, DefaultTrailMaxDistanceMeters) : 450f;

            while (_trailWorldXZ.Count > 1)
            {
                bool trimForAge = maxAge > 0f && _trailSampleTimes.Count > 0 && (now - _trailSampleTimes[0]) > maxAge;
                bool trimForDistance = maxDistance > 0f && _trailSampleDistances.Count > 0 && (_trailDistanceTravelledMeters - _trailSampleDistances[0]) > maxDistance;

                if (!trimForAge && !trimForDistance)
                    break;

                _trailWorldXZ.RemoveAt(0);
                if (_trailSampleTimes.Count > 0)
                    _trailSampleTimes.RemoveAt(0);
                if (_trailSampleDistances.Count > 0)
                    _trailSampleDistances.RemoveAt(0);
            }
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

        public void ClearForcedLiftRequirementOverrides()
        {
            _forcedLiftRequiredLevelByPolyline.Clear();
            _forcedLiftRequiredPassIdByPolyline.Clear();
            _forcedLiftColorByPolyline.Clear();
        }

        public void SetForcedLiftRequirementOverride(string polylineId, int requiredLevel, string requiredPassId, Color color)
        {
            if (string.IsNullOrWhiteSpace(polylineId))
                return;

            _forcedLiftRequiredLevelByPolyline[polylineId] = Mathf.Max(0, requiredLevel);

            if (!string.IsNullOrWhiteSpace(requiredPassId))
                _forcedLiftRequiredPassIdByPolyline[polylineId] = requiredPassId.Trim();
            else
                _forcedLiftRequiredPassIdByPolyline.Remove(polylineId);

            _forcedLiftColorByPolyline[polylineId] = color;
        }

        public void PreviewLiftAccessForPassLevel(int passLevel)
        {
            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;
            string selectedPassId = cfg != null ? cfg.GetPassIdForLevel(passLevel) : string.Empty;
            PreviewLiftAccessForPassId(selectedPassId, passLevel);
        }

        public void PreviewLiftAccessibilityByOwnedPassIds(ISet<string> ownedAccessPassIds)
        {
            if (!_bound || _mapData == null || _mapData.Polylines == null)
                return;

            for (int i = 0; i < _mapData.Polylines.Count; i++)
            {
                var poly = _mapData.Polylines[i];
                if (!poly.IsValid || poly.lineType != MapLineType.SkiLift)
                    continue;

                ResolveLiftRequirementForPolyline(
                    poly.id,
                    out int requiredLevel,
                    out string requiredPassId,
                    out Color baseColor);

                bool unlocked = DoesAccessSetIncludeLiftRequirement(ownedAccessPassIds, requiredPassId, requiredLevel);

                Color displayColor = unlocked
                    ? VisibleLiftPassColor(baseColor, 0.82f)
                    : DimLiftPassColor(baseColor, 0.78f, 0.66f);

                _polylineColorOverrides[poly.id] = displayColor;
                ApplyLiftPreviewMarkerState(poly.id, displayColor);
            }

            _polyLayer?.SetColorOverrides(_polylineColorOverrides);
            _polyHost?.MarkDirtyRepaint();
        }

        public bool FrameLiftPolylinesByOwnedPassIds(ISet<string> ownedAccessPassIds, float paddingPx = 36f, float minZoom = 0.85f)
        {
            if (_mapData == null || _mapData.Polylines == null || !_bound)
                return false;

            if (!TryGetViewportSize(out float vw, out float vh))
                return false;

            bool foundAny = false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < _mapData.Polylines.Count; i++)
            {
                var poly = _mapData.Polylines[i];
                if (!poly.IsValid || poly.lineType != MapLineType.SkiLift)
                    continue;

                ResolveLiftRequirementForPolyline(
                    poly.id,
                    out int requiredLevel,
                    out string requiredPassId,
                    out _);

                if (!DoesAccessSetIncludeLiftRequirement(ownedAccessPassIds, requiredPassId, requiredLevel))
                    continue;

                if (poly.Has3DPoints)
                {
                    for (int k = 0; k < poly.pointsWorld.Count; k++)
                    {
                        if (!TryProjectWorldToLocal(poly.pointsWorld[k], out var local))
                            continue;

                        foundAny = true;
                        minX = Mathf.Min(minX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxX = Mathf.Max(maxX, local.x);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }
                else if (poly.HasXZPoints)
                {
                    for (int k = 0; k < poly.pointsWorldXZ.Count; k++)
                    {
                        if (!TryProjectWorldXZToLocal(poly.pointsWorldXZ[k], out var local))
                            continue;

                        foundAny = true;
                        minX = Mathf.Min(minX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxX = Mathf.Max(maxX, local.x);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }
            }

            if (!foundAny)
                return false;

            float width = Mathf.Max(8f, maxX - minX);
            float height = Mathf.Max(8f, maxY - minY);

            float usableW = Mathf.Max(32f, vw - paddingPx * 2f);
            float usableH = Mathf.Max(32f, vh - paddingPx * 2f);

            _zoom = Mathf.Max(minZoom, Mathf.Min(usableW / width, usableH / height));

            Vector2 centerLocal = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            CenterViewOnContentPoint(centerLocal);
            return true;
        }

        private bool DoesAccessSetIncludeLiftRequirement(ISet<string> ownedAccessPassIds, string requiredPassId, int requiredLevel)
        {
            if (ownedAccessPassIds != null && ownedAccessPassIds.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(requiredPassId))
                    return ownedAccessPassIds.Contains(requiredPassId.Trim());

                var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;
                if (cfg != null)
                {
                    string fallbackPassId = cfg.GetPassIdForLevel(requiredLevel);
                    if (!string.IsNullOrWhiteSpace(fallbackPassId))
                        return ownedAccessPassIds.Contains(fallbackPassId.Trim());
                }

                return false;
            }

            var manager = SkiPassManager.Instance;
            var config = manager != null ? manager.Config : null;

            if (config != null && !string.IsNullOrWhiteSpace(requiredPassId))
            {
                string currentPassId = config.GetPassIdForLevel(manager != null ? manager.CurrentLevel : 0);
                return !string.IsNullOrWhiteSpace(currentPassId) &&
                       config.PassGrantsAccessTo(currentPassId, requiredPassId);
            }

            return manager != null
                ? manager.CurrentLevel >= Mathf.Max(0, requiredLevel)
                : Mathf.Max(0, requiredLevel) <= 0;
        }

        public void PreviewLiftAccessForPassId(string selectedPassId, int selectedLevel)
        {
            if (!_bound || _mapData == null || _mapData.Polylines == null)
                return;

            selectedLevel = Mathf.Max(0, selectedLevel);
            var cfg = SkiPassManager.Instance != null ? SkiPassManager.Instance.Config : null;

            for (int i = 0; i < _mapData.Polylines.Count; i++)
            {
                var poly = _mapData.Polylines[i];
                if (!poly.IsValid || poly.lineType != MapLineType.SkiLift)
                    continue;

                ResolveLiftRequirementForPolyline(
                    poly.id,
                    out int requiredLevel,
                    out string requiredPassId,
                    out Color baseColor);

                bool unlocked = selectedLevel >= requiredLevel;

                if (cfg != null &&
                    !string.IsNullOrWhiteSpace(selectedPassId) &&
                    !string.IsNullOrWhiteSpace(requiredPassId))
                {
                    unlocked = cfg.PassGrantsAccessTo(selectedPassId, requiredPassId);
                }

                Color displayColor = unlocked
                    ? VisibleLiftPassColor(baseColor, 0.82f)
                    : DimLiftPassColor(baseColor, 0.62f, 0.56f);

                _polylineColorOverrides[poly.id] = displayColor;
                ApplyLiftPreviewMarkerState(poly.id, displayColor);
            }

            _polyLayer?.SetColorOverrides(_polylineColorOverrides);
            _polyHost?.MarkDirtyRepaint();
        }

        private void ApplyLiftPreviewMarkerState(string polyId, Color previewColor)
        {
            if (string.IsNullOrWhiteSpace(polyId))
                return;

            if (_liftPolylineToMarkers.TryGetValue(polyId, out var markerIds))
            {
                for (int i = 0; i < markerIds.Count; i++)
                {
                    string markerId = markerIds[i];
                    _markerLabelAccent[markerId] = new Color(previewColor.r, previewColor.g, previewColor.b, 1f);
                    ApplyMarkerVisual(markerId, _selectedMarkerSet.Contains(markerId));
                }
            }

            _polylineLabelAccent[polyId] = new Color(previewColor.r, previewColor.g, previewColor.b, 1f);

            if (TryGetPolylineById(polyId, out var poly))
                ApplyPolylineLabelVisual(polyId, selected: IsPolylineSelected(poly));
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
            private bool _showRaceCourses = true;

            private readonly HashSet<int> _visibleRunDifficultyRanks = new() { 0, 1, 2, 3 };

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

            private PointOfInterestRegistry _poiRegistry;

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

            public void SetFilterState(bool showRuns, bool showLifts, bool showRaceCourses, IReadOnlyCollection<int> visibleRunDifficultyRanks)
            {
                _showRuns = showRuns;
                _showLifts = showLifts;
                _showRaceCourses = showRaceCourses;

                _visibleRunDifficultyRanks.Clear();
                if (visibleRunDifficultyRanks != null)
                {
                    foreach (var rank in visibleRunDifficultyRanks)
                        _visibleRunDifficultyRanks.Add(rank);
                }

                MarkDirtyRepaint();
            }

            public void SetPOIRegistry(PointOfInterestRegistry registry)
            {
                _poiRegistry = registry;
                MarkDirtyRepaint();
            }

            public void SetColorOverrides(IReadOnlyDictionary<string, Color> overrides)
            {
                _colorOverrides = overrides;
                MarkDirtyRepaint();
            }

            private bool IsVisible(MapPolyline line)
            {
                switch (line.lineType)
                {
                    case MapLineType.SkiRun:
                        {
                            if (!_showRuns)
                                return false;

                            int rank = ResolveRunDifficultyRank(line.id, line.difficultyRank);
                            if (rank < 0)
                                return _visibleRunDifficultyRanks.Count >= DefaultRunDifficultyRanks.Length;

                            return _visibleRunDifficultyRanks.Contains(rank);
                        }

                    case MapLineType.SkiLift:
                        return _showLifts;

                    case MapLineType.RaceCourse:
                        return _showRaceCourses;

                    default:
                        return true;
                }
            }

            private bool IsVisible(MapRunCorridor corridor)
            {
                if (!_showRuns)
                    return false;

                int rank = ResolveRunDifficultyRank(corridor.id, corridor.difficultyRank);
                if (rank < 0)
                    return _visibleRunDifficultyRanks.Count >= DefaultRunDifficultyRanks.Length;

                return _visibleRunDifficultyRanks.Contains(rank);
            }

            private int ResolveRunDifficultyRank(string runId, int bakedRank)
            {
                string normalizedRunId = runId;
                int suffixIndex = normalizedRunId.LastIndexOf("__", StringComparison.Ordinal);
                if (suffixIndex > 0)
                    normalizedRunId = normalizedRunId.Substring(0, suffixIndex);

                if (_poiRegistry != null && _poiRegistry.TryGetById(normalizedRunId, out var poi) && poi.source is SkiRunLine line)
                {
                    if (TryMapRunDifficultyName(line.Difficulty.ToString(), out int authoredRank))
                        return authoredRank;
                }

                return NormalizeRunDifficultyRank(bakedRank);
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

                    float pickDistancePx = c.src.lineType == MapLineType.RaceCourse
                        ? maxDistancePx * 1.75f
                        : maxDistancePx;

                    Rect b = c.bounds;
                    b.xMin -= pickDistancePx;
                    b.yMin -= pickDistancePx;
                    b.xMax += pickDistancePx;
                    b.yMax += pickDistancePx;

                    if (!b.Contains(contentLocal))
                        continue;

                    var pts = c.localPts;
                    if (pts == null || pts.Count < 2)
                        continue;

                    for (int k = 0; k < pts.Count - 1; k++)
                    {
                        float dSqr = DistPointToSegmentSqr(contentLocal, pts[k], pts[k + 1]);
                        float allowedSqr = pickDistancePx * pickDistancePx;
                        if (dSqr <= allowedSqr && dSqr < bestSqr)
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

                float zoomMul = 1f;
                if (_style != null)
                {
                    var lineStyle = _style.GetPoiPolylineStyle();
                    float exp = Mathf.Clamp01(lineStyle.widthZoomExponent);
                    zoomMul = Mathf.Pow(Mathf.Max(0.0001f, _zoom), exp);
                    zoomMul = Mathf.Clamp(zoomMul, lineStyle.widthZoomMinMul, lineStyle.widthZoomMaxMul);
                }

                var runAreaStyle = _style != null
                    ? _style.GetRunAreaStyle()
                    : new MapUIStyleSettings.MapAreaVisualStyle
                    {
                        outlineWidthPx = 1.8f,
                        outlineWidthSelectedPx = 3.1f,
                        fillOpacity = 0.24f,
                        fillOpacitySelected = 0.42f,
                        outlineOpacity = 0.92f,
                        outlineOpacitySelected = 1f
                    };

                float outlineWidth = (runAreaStyle.outlineWidthPx * zoomMul) / Mathf.Max(0.0001f, _zoom);
                float selectedOutlineWidth = (runAreaStyle.outlineWidthSelectedPx * zoomMul) / Mathf.Max(0.0001f, _zoom);
                float fillOpacity = runAreaStyle.fillOpacity;
                float fillOpacitySelected = runAreaStyle.fillOpacitySelected;
                float outlineOpacity = runAreaStyle.outlineOpacity;
                float outlineOpacitySelected = runAreaStyle.outlineOpacitySelected;

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
                        selected ? fillOpacitySelected : fillOpacity);

                    Color outline = new Color(
                        baseColor.r,
                        baseColor.g,
                        baseColor.b,
                        selected ? outlineOpacitySelected : outlineOpacity);

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

                    var lineStyle = _style != null ? _style.GetPoiPolylineStyle() : new MapUIStyleSettings.MapLineVisualStyle
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
                    float screenW = lineStyle.widthPx;
                    float zoomMul = 1f;
                    if (_style != null)
                    {
                        float exp = Mathf.Clamp01(lineStyle.widthZoomExponent);
                        zoomMul = Mathf.Pow(Mathf.Max(0.0001f, _zoom), exp);
                        zoomMul = Mathf.Clamp(zoomMul, lineStyle.widthZoomMinMul, lineStyle.widthZoomMaxMul);
                    }

                    float w = (screenW * zoomMul) / _zoom;
                    bool selected = (!string.IsNullOrEmpty(_selectedId) && line.id == _selectedId);
                    float selMul = lineStyle.selectedWidthMultiplier;

                    var polylineStyle = _style != null
                         ? _style.GetPoiPolylineStyle()
                         : new MapUIStyleSettings.MapLineVisualStyle
                         {
                             outlineColor = new Color(0f, 0f, 0f, 0.35f),
                             outlineColorSelected = new Color(0f, 0f, 0f, 0.55f)
                         };

                    Color outlineCol = selected
                        ? polylineStyle.outlineColorSelected
                        : polylineStyle.outlineColor;

                    float outlineExtra = selected ? lineStyle.outlineExtraSelectedPx : lineStyle.outlineExtraPx;

                    p.strokeColor = outlineCol;
                    p.lineWidth = ((screenW + outlineExtra) * zoomMul) / _zoom;
                    Stroke(p, pts);

                    Color baseColor = (line.color.a <= 0.001f) ? new Color(1f, 1f, 1f, 0.70f) : line.color;
                    if (_colorOverrides != null && _colorOverrides.TryGetValue(line.id, out var ov))
                        baseColor = ov;

                    p.strokeColor = baseColor;
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
            private MapUIStyleSettings _style;

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

            public void SetStyle(MapUIStyleSettings style)
            {
                _style = style;
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
                    Vector2 uv = ApplyInset(_worldTrail[i], _data.BackgroundUvMin, _data.BackgroundUvMax);
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

                var trailStyle = _style != null
                    ? _style.GetTrailLineStyle()
                    : new MapUIStyleSettings.MapLineVisualStyle
                    {
                        widthPx = 2.0f,
                        outlineColor = new Color(0f, 0f, 0f, 0.40f)
                    };

                float screenW = Mathf.Max(0.1f, trailStyle.widthPx);
                float w = screenW / _zoom;
                float outlineWidth = (screenW + 2.0f) / _zoom;
                Color mainColor = DefaultTrailColor;
                Color outlineColor = trailStyle.outlineColor.a > 0.001f
                    ? trailStyle.outlineColor
                    : DefaultTrailOutlineColor;
                float fadePower = DefaultTrailFadePower;
                float minAlpha = DefaultTrailMinAlpha;
                int segmentCount = _localPts.Count - 1;

                for (int i = 1; i < _localPts.Count; i++)
                {
                    float t = segmentCount <= 1 ? 1f : i / (float)segmentCount;
                    float alpha = Mathf.Lerp(minAlpha, 1f, Mathf.Pow(t, fadePower));

                    p.strokeColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a * alpha);
                    p.lineWidth = outlineWidth;
                    Stroke(p, _localPts[i - 1], _localPts[i]);

                    p.strokeColor = new Color(mainColor.r, mainColor.g, mainColor.b, mainColor.a * alpha);
                    p.lineWidth = w;
                    Stroke(p, _localPts[i - 1], _localPts[i]);
                }
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

            private static void Stroke(Painter2D p, Vector2 a, Vector2 b)
            {
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
        }

        public void ApplyStyle(MapUIStyleSettings style)
        {
            if (!ReferenceEquals(_style, style))
            {
                if (_style != null)
                    _style.RuntimeStyleChanged -= HandleRuntimeStyleChanged;

                _style = style;

                if (_style != null)
                    _style.RuntimeStyleChanged += HandleRuntimeStyleChanged;
            }

            ReapplyStyleState();
        }

        public void SetPOIRegistry(PointOfInterestRegistry registry)
        {
            if (ReferenceEquals(_poiRegistry, registry))
            {
                _polyLayer?.SetPOIRegistry(registry);
                RefreshRacePolylineVisualOverrides();
                RebuildLegendUI();
                _dirty = true;
                return;
            }

            if (_poiRegistry != null)
                _poiRegistry.OnChanged -= HandlePoiRegistryChanged;

            _poiRegistry = registry;

            if (_poiRegistry != null)
                _poiRegistry.OnChanged += HandlePoiRegistryChanged;

            _polyLayer?.SetPOIRegistry(registry);
            RefreshRacePolylineVisualOverrides();
            RebuildLegendUI();
            _dirty = true;
        }

        private void HandleRuntimeStyleChanged(MapUIStyleSettings changedStyle)
        {
            if (!ReferenceEquals(_style, changedStyle))
                return;

            ReapplyStyleState();
        }

        private void HandlePoiRegistryChanged(IReadOnlyList<POIInfo> _)
        {
            RefreshRacePolylineVisualOverrides();
            RebuildLegendUI();
            _dirty = true;

            if (_bound)
                Refresh();
        }

        private void RefreshStyleIfNeeded()
        {
            if (_style == null)
            {
                _appliedStyleRevision = -1;
                return;
            }

            if (_appliedStyleRevision == _style.Revision)
                return;

            ReapplyStyleState();
        }

        private void ReapplyStyleState()
        {
            _appliedStyleRevision = _style != null ? _style.Revision : -1;

            _regionLayer?.SetStyle(_style);
            _polyLayer?.SetStyle(_style);
            _trailLayer?.SetStyle(_style);
            ApplyPlayerMarkerStyle();

            foreach (var kv in _markerVisuals)
                ApplyMarkerVisual(kv.Key, _selectedMarkerSet.Contains(kv.Key));

            foreach (var kv in _polylineLabelVisuals)
            {
                bool selected = TryGetPolylineById(kv.Key, out var polyline) && IsPolylineSelected(polyline);
                ApplyPolylineLabelVisual(kv.Key, selected);
            }

            foreach (var kv in _poiOverlayLabels)
                ApplyPOIOverlayLabelVisual(kv.Key, _selectedMarkerSet.Contains(kv.Key));

            foreach (var kv in _regionOverlayLabels)
                ApplyRegionOverlayLabelVisual(kv.Key, _regionsSelectable && string.Equals(_selectedRegionId, kv.Key, StringComparison.Ordinal));

            if (_waypointManager != null)
            {
                foreach (var kv in _waypointMarkerDots)
                {
                    bool selected = _waypointManager.IsWaypointSelected(kv.Key);
                    bool active = string.Equals(_waypointManager.ActiveWaypointId, kv.Key, StringComparison.Ordinal);
                    ApplyWaypointVisual(kv.Key, selected, active);
                    ApplyWaypointLabelVisual(kv.Key, selected);
                }

                MarkWaypointLabelLayoutDirty();
            }

            RebuildLegendUI();
            UpdateAllLabelTransformsForZoom();
            _polyLayer?.SetColorOverrides(_polylineColorOverrides);
            _polyHost?.MarkDirtyRepaint();
            _markerHost?.MarkDirtyRepaint();
            _navigationOverlay?.MarkDirtyRepaint();
            LayoutOverlayLabels();
            LayoutWaypointLabels();
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

            var playerStyle = GetPlayerMarkerStyle();
            bool useSprite = playerStyle.sprite != null;
            float s = Mathf.Max(16f, playerStyle.size);
            _playerMarker.style.width = s;
            _playerMarker.style.height = s;
            _playerMarker.style.backgroundColor = Color.clear;

            _playerMarker.style.transformOrigin = useSprite
                ? new TransformOrigin(new Length(50, LengthUnit.Percent), new Length(50, LengthUnit.Percent), 0f)
                : new TransformOrigin(new Length(50, LengthUnit.Percent), new Length(75, LengthUnit.Percent), 0f);

            Color bodyColor = playerStyle.fillColor;
            Color outlineColor = playerStyle.borderColor;

            var baseCircle = _playerMarker.Q<VisualElement>("MapPlayerMarkerBase");
            var arrowBody = _playerMarker.Q<VisualElement>("MapPlayerMarkerArrow");
            var arrowOutline = _playerMarker.Q<VisualElement>("MapPlayerMarkerArrowOutline");
            var centerDot = _playerMarker.Q<VisualElement>("MapPlayerMarkerCenter");
            var spriteOutlineVisual = _playerMarker.Q<VisualElement>("MapPlayerMarkerSpriteOutline");
            var spriteVisual = _playerMarker.Q<VisualElement>("MapPlayerMarkerSprite");
            float border = Mathf.Max(1f, playerStyle.borderWidth);

            if (spriteVisual != null)
            {
                spriteVisual.style.display = useSprite ? DisplayStyle.Flex : DisplayStyle.None;
                if (useSprite)
                {
                    spriteVisual.style.backgroundImage = new StyleBackground(playerStyle.sprite);
                    spriteVisual.style.unityBackgroundImageTintColor = bodyColor;
                    float inset = border;
                    spriteVisual.style.left = inset;
                    spriteVisual.style.top = inset;
                    spriteVisual.style.right = inset;
                    spriteVisual.style.bottom = inset;
                }
                else
                {
                    spriteVisual.style.backgroundImage = StyleKeyword.None;
                    spriteVisual.style.left = 0f;
                    spriteVisual.style.top = 0f;
                    spriteVisual.style.right = 0f;
                    spriteVisual.style.bottom = 0f;
                }
            }

            if (spriteOutlineVisual != null)
            {
                spriteOutlineVisual.style.display = useSprite ? DisplayStyle.Flex : DisplayStyle.None;
                if (useSprite)
                {
                    spriteOutlineVisual.style.backgroundImage = new StyleBackground(playerStyle.sprite);
                    spriteOutlineVisual.style.unityBackgroundImageTintColor = outlineColor;
                }
                else
                {
                    spriteOutlineVisual.style.backgroundImage = StyleKeyword.None;
                }
            }

            if (baseCircle != null) baseCircle.style.display = useSprite ? DisplayStyle.None : DisplayStyle.Flex;
            if (arrowBody != null) arrowBody.style.display = useSprite ? DisplayStyle.None : DisplayStyle.Flex;
            if (arrowOutline != null) arrowOutline.style.display = useSprite ? DisplayStyle.None : DisplayStyle.Flex;
            if (centerDot != null) centerDot.style.display = useSprite ? DisplayStyle.None : DisplayStyle.Flex;

            if (useSprite)
                return;

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
            if (!_markerVisuals.TryGetValue(id, out var v))
                return;

            MapUIStyleSettings.MapMarkerVisualStyle markerStyle = _style != null
                ? _style.GetPoiMarkerStyle()
                : default;

            float markerSize = _style != null
                ? (selected ? markerStyle.selectedSize : markerStyle.size)
                : (selected ? 16f : 12f);

            Sprite explicitSprite = null;
            float explicitSizeMultiplier = 1f;
            POIType poiType = POIType.Unknown;
            POICategory poiCategory = POICategory.None;
            Color poiColor = Color.clear;

            if (_poiRegistry != null && _poiRegistry.TryGetById(id, out var poiInfo))
            {
                explicitSprite = poiInfo.markerSprite;
                explicitSizeMultiplier = poiInfo.markerSizeMultiplier > 0f ? Mathf.Max(0.1f, poiInfo.markerSizeMultiplier) : 1f;
                poiType = poiInfo.type;
                poiCategory = poiInfo.category;
                poiColor = poiInfo.color;
            }

            ResolvedMarkerRule rule = ResolveMarkerRule(poiType, poiCategory);

            Sprite markerSprite = explicitSprite != null
                ? explicitSprite
                : (rule.sprite != null ? rule.sprite : (_style != null ? markerStyle.sprite : null));

            float sizeMultiplier = !Mathf.Approximately(explicitSizeMultiplier, 1f)
                ? explicitSizeMultiplier
                : rule.sizeMultiplier;

            bool useSprite = markerSprite != null;

            bool linkedWaypoint = TryGetLinkedWaypointForMarker(id, out _);
            bool explicitAlways =
                _poiLabelDisplayOverrides.TryGetValue(id, out var displayMode) &&
                displayMode == MapUIStyleSettings.MapLabelDisplayMode.Always;
            bool isCoreNavigation = poiType == POIType.SkiRun || poiType == POIType.SkiLift;
            bool isCustomUserPoi = poiType == POIType.Custom && poiCategory == POICategory.Custom;

            float zoomT = Mathf.InverseLerp(GetMarkerFadeInStartZoom(), GetMarkerFadeInEndZoom(), _zoom);

            float contextOpacity;
            if (selected)
            {
                contextOpacity = 1f;
            }
            else if (linkedWaypoint || explicitAlways || isCustomUserPoi)
            {
                contextOpacity = Mathf.Lerp(0.82f, 1f, zoomT);
            }
            else if (isCoreNavigation)
            {
                contextOpacity = Mathf.Lerp(0.72f, 0.95f, zoomT);
            }
            else
            {
                contextOpacity = Mathf.Lerp(0.45f, 0.92f, zoomT);
            }

            float contextScale;
            if (selected)
                contextScale = 1f;
            else if (linkedWaypoint || isCustomUserPoi)
                contextScale = Mathf.Lerp(0.96f, 1f, zoomT);
            else if (isCoreNavigation)
                contextScale = Mathf.Lerp(0.93f, 1f, zoomT);
            else
                contextScale = Mathf.Lerp(0.88f, 1f, zoomT);

            float s = Mathf.Max(1f, markerSize * Mathf.Max(0.1f, sizeMultiplier) * contextScale);
            v.dot.style.width = s;
            v.dot.style.height = s;
            v.dot.style.opacity = contextOpacity;

            float bw = _style != null ? Mathf.Max(0f, markerStyle.borderWidth) : 1f;
            Color borderColor = _style != null ? markerStyle.borderColor : new Color(0f, 0f, 0f, 0.65f);

            Color fillColor = _markerLabelAccent.TryGetValue(id, out var accentColor) ? accentColor : Color.white;
            if (rule.hasDefaultColor && (!rule.colorIsFallbackOnly || fillColor == Color.white || fillColor.a <= 0f))
                fillColor = rule.defaultColor;
            if (poiColor.a > 0f)
                fillColor = poiColor;

            if (useSprite)
            {
                v.dot.style.backgroundColor = Color.clear;
                v.dot.style.borderLeftWidth = 0f;
                v.dot.style.borderRightWidth = 0f;
                v.dot.style.borderTopWidth = 0f;
                v.dot.style.borderBottomWidth = 0f;
                v.dot.style.borderTopLeftRadius = 0f;
                v.dot.style.borderTopRightRadius = 0f;
                v.dot.style.borderBottomLeftRadius = 0f;
                v.dot.style.borderBottomRightRadius = 0f;

                if (v.spriteOutline != null)
                {
                    v.spriteOutline.style.display = DisplayStyle.Flex;
                    v.spriteOutline.style.backgroundImage = new StyleBackground(markerSprite);
                    v.spriteOutline.style.unityBackgroundImageTintColor = borderColor;
                    v.spriteOutline.style.left = 0f;
                    v.spriteOutline.style.top = 0f;
                    v.spriteOutline.style.right = 0f;
                    v.spriteOutline.style.bottom = 0f;
                }

                if (v.spriteBody != null)
                {
                    v.spriteBody.style.display = DisplayStyle.Flex;
                    v.spriteBody.style.backgroundImage = new StyleBackground(markerSprite);
                    v.spriteBody.style.unityBackgroundImageTintColor = fillColor;
                    v.spriteBody.style.left = bw;
                    v.spriteBody.style.top = bw;
                    v.spriteBody.style.right = bw;
                    v.spriteBody.style.bottom = bw;
                }
            }
            else
            {
                if (v.spriteOutline != null)
                {
                    v.spriteOutline.style.display = DisplayStyle.None;
                    v.spriteOutline.style.backgroundImage = StyleKeyword.None;
                }

                if (v.spriteBody != null)
                {
                    v.spriteBody.style.display = DisplayStyle.None;
                    v.spriteBody.style.backgroundImage = StyleKeyword.None;
                    v.spriteBody.style.left = 0f;
                    v.spriteBody.style.top = 0f;
                    v.spriteBody.style.right = 0f;
                    v.spriteBody.style.bottom = 0f;
                }

                v.dot.style.backgroundColor = fillColor;
                v.dot.style.borderLeftWidth = bw;
                v.dot.style.borderRightWidth = bw;
                v.dot.style.borderTopWidth = bw;
                v.dot.style.borderBottomWidth = bw;
                v.dot.style.borderLeftColor = borderColor;
                v.dot.style.borderRightColor = borderColor;
                v.dot.style.borderTopColor = borderColor;
                v.dot.style.borderBottomColor = borderColor;
                v.dot.style.borderTopLeftRadius = 999f;
                v.dot.style.borderTopRightRadius = 999f;
                v.dot.style.borderBottomLeftRadius = 999f;
                v.dot.style.borderBottomRightRadius = 999f;
            }

            MapUIStyleSettings.MapLabelVisualStyle labelStyle = _style != null
                ? _style.GetFallbackStandardLabelStyle()
                : default;

            int labelFontSize = _style != null ? (selected ? labelStyle.selectedFontSize : labelStyle.fontSize) : (selected ? 13 : 12);
            FontStyle labelFontStyle = _style != null ? (selected ? labelStyle.selectedFontStyle : labelStyle.fontStyle) : FontStyle.Bold;
            Color labelColor = _style != null ? (selected ? labelStyle.selectedColor : labelStyle.color) : Color.white;

            v.label.style.fontSize = labelFontSize;
            v.label.style.unityFontStyleAndWeight = labelFontStyle;
            v.label.style.color = labelColor;

            float stripeW = Mathf.Max(1f, _style != null ? labelStyle.accentStripeWidth : 3f);
            v.label.style.borderLeftWidth = stripeW;
            v.label.style.borderLeftColor = fillColor;

            float plateAlpha = Mathf.Clamp01(_style != null ? labelStyle.plateAlpha : 0.78f) * 0.22f;
            v.label.style.backgroundColor = new Color(fillColor.r, fillColor.g, fillColor.b, plateAlpha);

            if (v.glyph != null)
                v.glyph.style.display = useSprite ? DisplayStyle.None : DisplayStyle.Flex;

            float invZoom = 1f / Mathf.Max(0.0001f, _zoom);
            v.dot.style.scale = new Scale(new Vector3(invZoom, invZoom, 1f));
            v.dot.style.translate = new Translate(-s * 0.5f, -s * 0.5f, 0f);
        }

        private void ApplyWaypointVisual(string id, bool selected, bool active)
        {
            if (!_waypointMarkerDots.TryGetValue(id, out var dot) || dot == null)
                return;

            var markerStyle = GetWaypointMarkerStyle();
            float size = selected ? markerStyle.selectedSize : markerStyle.size;
            float borderWidth = active ? markerStyle.activeBorderWidth : markerStyle.borderWidth;
            Color borderColor = active ? markerStyle.activeBorderColor : markerStyle.borderColor;

            Sprite markerSprite = markerStyle.sprite;
            float sizeMultiplier = 1f;
            Color bodyColor = markerStyle.fillColor.a > 0.001f ? markerStyle.fillColor : Color.white;

            if (_waypointManager != null && _waypointManager.TryGetWaypoint(id, out var waypoint))
            {
                if (waypoint.markerSprite != null)
                    markerSprite = waypoint.markerSprite;

                sizeMultiplier = waypoint.markerSizeMultiplier > 0f ? Mathf.Max(0.1f, waypoint.markerSizeMultiplier) : 1f;
                bodyColor = waypoint.color.a > 0.001f ? waypoint.color : bodyColor;
            }

            size = Mathf.Max(1f, size * sizeMultiplier);
            borderWidth = Mathf.Max(0f, borderWidth);

            dot.style.width = size;
            dot.style.height = size;

            var spriteOutline = dot.Q<VisualElement>("WaypointSpriteOutline");
            var spriteBody = dot.Q<VisualElement>("WaypointSpriteBody");
            bool useSprite = markerSprite != null;

            if (useSprite)
            {
                dot.style.backgroundColor = Color.clear;
                dot.style.borderLeftWidth = 0f;
                dot.style.borderRightWidth = 0f;
                dot.style.borderTopWidth = 0f;
                dot.style.borderBottomWidth = 0f;
                dot.style.borderTopLeftRadius = 0f;
                dot.style.borderTopRightRadius = 0f;
                dot.style.borderBottomLeftRadius = 0f;
                dot.style.borderBottomRightRadius = 0f;

                if (spriteOutline != null)
                {
                    spriteOutline.style.display = DisplayStyle.Flex;
                    spriteOutline.style.backgroundImage = new StyleBackground(markerSprite);
                    spriteOutline.style.unityBackgroundImageTintColor = borderColor;
                    spriteOutline.style.left = 0f;
                    spriteOutline.style.top = 0f;
                    spriteOutline.style.right = 0f;
                    spriteOutline.style.bottom = 0f;
                }

                if (spriteBody != null)
                {
                    spriteBody.style.display = DisplayStyle.Flex;
                    spriteBody.style.backgroundImage = new StyleBackground(markerSprite);
                    spriteBody.style.unityBackgroundImageTintColor = bodyColor;
                    spriteBody.style.left = borderWidth;
                    spriteBody.style.top = borderWidth;
                    spriteBody.style.right = borderWidth;
                    spriteBody.style.bottom = borderWidth;
                }
            }
            else
            {
                if (spriteOutline != null)
                {
                    spriteOutline.style.display = DisplayStyle.None;
                    spriteOutline.style.backgroundImage = StyleKeyword.None;
                }

                if (spriteBody != null)
                {
                    spriteBody.style.display = DisplayStyle.None;
                    spriteBody.style.backgroundImage = StyleKeyword.None;
                    spriteBody.style.left = 0f;
                    spriteBody.style.top = 0f;
                    spriteBody.style.right = 0f;
                    spriteBody.style.bottom = 0f;
                }

                dot.style.backgroundColor = bodyColor;
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

            float invZoom = 1f / Mathf.Max(0.0001f, _zoom);
            dot.style.scale = new Scale(new Vector3(invZoom, invZoom, 1f));
            dot.style.translate = new Translate(-size * 0.5f, -size * 0.5f, 0f);
        }

        private Vector2 MeasureWaypointLabelSize(Label label)
        {
            if (label == null)
                return new Vector2(40f, 18f);

            var measured = label.MeasureTextSize(label.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined);

            float w = measured.x;
            float h = measured.y;

            if (w <= 0f)
                w = label.resolvedStyle.width > 0f ? label.resolvedStyle.width : 40f;

            if (h <= 0f)
                h = label.resolvedStyle.height > 0f ? label.resolvedStyle.height : 18f;

            return new Vector2(
                Mathf.Ceil(w + 8f),
                Mathf.Ceil(h + 6f));
        }

        private void SyncWaypointLabelTransformImmediate(Label label)
        {
            if (label == null)
                return;

            // Waypoint labels now live in _labelOverlay, so they are already in viewport space.
            // They must not be inverse-scaled or anchor-translated like marker-local labels.
            label.style.scale = new Scale(Vector3.one);
            label.style.translate = new Translate(0f, 0f, 0f);
        }

        private void SetWaypointLabelViewportRect(string waypointId, Rect rect)
        {
            if (!_waypointLabels.TryGetValue(waypointId, out var label) || label == null)
                return;

            if (!float.IsFinite(rect.x) || !float.IsFinite(rect.y) ||
                !float.IsFinite(rect.width) || !float.IsFinite(rect.height) ||
                rect.width <= 0f || rect.height <= 0f)
            {
                label.style.display = DisplayStyle.None;
                label.style.visibility = Visibility.Hidden;
                return;
            }

            // Waypoint labels are viewport overlay labels.
            // Their position should be the final viewport rect directly.
            label.style.visibility = Visibility.Hidden;
            label.style.display = DisplayStyle.Flex;

            label.style.left = Mathf.Round(rect.x);
            label.style.top = Mathf.Round(rect.y);
            label.style.width = Mathf.Ceil(rect.width);
            label.style.height = Mathf.Ceil(rect.height);

            SyncWaypointLabelTransformImmediate(label);
            label.style.visibility = Visibility.Visible;
        }

        private void MarkWaypointLabelLayoutDirty()
        {
            _waypointLabelLayoutDirty = true;
        }

        //private void ScheduleWaypointLayoutRefresh()
        //{
        //    // Intentionally disabled.
        //    //
        //    // Waypoint labels now have one authoritative placement path:
        //    // LayoutWaypointLabels().
        //    //
        //    // A deferred scheduled pass after rename open/close was still causing
        //    // transient placement churn and brief incorrect positions.
        //}

        private void PruneWaypointLayoutCaches()
        {
            if (_waypointManager == null)
            {
                _waypointLabelViewportRects.Clear();
                _waypointLabelScreenOffsets.Clear();
                return;
            }

            var validIds = new HashSet<string>();
            var waypoints = _waypointManager.Waypoints;
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    var wp = waypoints[i];
                    if (!string.IsNullOrWhiteSpace(wp.id))
                        validIds.Add(wp.id);
                }
            }

            var rectKeys = new List<string>(_waypointLabelViewportRects.Keys);
            for (int i = 0; i < rectKeys.Count; i++)
            {
                if (!validIds.Contains(rectKeys[i]))
                    _waypointLabelViewportRects.Remove(rectKeys[i]);
            }

            var offsetKeys = new List<string>(_waypointLabelScreenOffsets.Keys);
            for (int i = 0; i < offsetKeys.Count; i++)
            {
                if (!validIds.Contains(offsetKeys[i]))
                    _waypointLabelScreenOffsets.Remove(offsetKeys[i]);
            }
        }

        private void ApplyCachedWaypointLabelPlacement()
        {
            if (_waypointLabels.Count == 0)
                return;

            bool showLabels = _showNonRegionLabels && !_hideMarkerLabels;
            if (!showLabels)
            {
                foreach (var kv in _waypointLabels)
                {
                    if (kv.Value != null)
                    {
                        kv.Value.style.display = DisplayStyle.None;
                        kv.Value.style.visibility = Visibility.Hidden;
                    }
                }

                return;
            }

            foreach (var kv in _waypointLabels)
            {
                string id = kv.Key;
                Label label = kv.Value;
                if (label == null)
                    continue;

                bool selected = _waypointManager != null && _waypointManager.IsWaypointSelected(id);
                ApplyWaypointLabelVisual(id, selected);

                if (!ShouldShowWaypointLabel(id, selected))
                {
                    label.style.display = DisplayStyle.None;
                    label.style.visibility = Visibility.Hidden;
                    continue;
                }

                if (_waypointMarkerRoots.TryGetValue(id, out var root) &&
                    root != null &&
                    root.resolvedStyle.display == DisplayStyle.None)
                {
                    label.style.display = DisplayStyle.None;
                    label.style.visibility = Visibility.Hidden;
                    continue;
                }

                if (_waypointLabelViewportRects.TryGetValue(id, out var rect) &&
                    float.IsFinite(rect.x) && float.IsFinite(rect.y) &&
                    float.IsFinite(rect.width) && float.IsFinite(rect.height) &&
                    rect.width > 0f && rect.height > 0f)
                {
                    SetWaypointLabelViewportRect(id, rect);
                }
                else
                {
                    // Critical: uncached labels stay hidden until the authoritative solver places them.
                    label.style.display = DisplayStyle.None;
                    label.style.visibility = Visibility.Hidden;
                }
            }
        }

        private bool ShouldRelayoutWaypointLabels()
        {
            if (_waypointLabelLayoutDirty)
                return true;

            if (_lastWaypointLayoutZoom < 0f)
                return true;

            float zoomDelta = Mathf.Abs(_zoom - _lastWaypointLayoutZoom);
            if (zoomDelta >= WaypointLayoutZoomThreshold)
                return true;

            if ((_pan - _lastWaypointLayoutPan).sqrMagnitude >= WaypointLayoutPanThresholdPx * WaypointLayoutPanThresholdPx)
                return true;

            return false;
        }

        private bool CanRunSettledWaypointLayout()
        {
            return (Time.unscaledTime - _lastWaypointLayoutTime) >= WaypointLayoutSettleDelay;
        }

        private void CommitWaypointLayoutState()
        {
            _lastWaypointLayoutZoom = _zoom;
            _lastWaypointLayoutPan = _pan;
            _lastWaypointLayoutTime = Time.unscaledTime;
            _waypointLabelLayoutDirty = false;
        }

        private MapUIStyleSettings.MapLabelStyleProfile GetWaypointLabelProfile()
        {
            return _style != null
                ? _style.GetStandardLabelProfileById(WaypointEditableLabelStyleId)
                : _style.GetFallbackStandardLabelProfile();
        }

        private float EvaluateWaypointRevealAlpha(bool selected)
        {
            var profile = GetWaypointLabelProfile();
            if (selected)
                return 1f;

            float relativeZoom = GetRelativeZoom();

            switch (profile.revealDirection)
            {
                case MapUIStyleSettings.MapRevealDirection.Always:
                    return 1f;

                case MapUIStyleSettings.MapRevealDirection.ZoomOutReveal:
                    {
                        float t = Mathf.InverseLerp(profile.fadeStartZoom, profile.fadeEndZoom, relativeZoom);
                        return 1f - Mathf.Clamp01(t);
                    }

                case MapUIStyleSettings.MapRevealDirection.ZoomInReveal:
                default:
                    {
                        float t = Mathf.InverseLerp(profile.fadeStartZoom, profile.fadeEndZoom, relativeZoom);
                        return Mathf.Clamp01(t);
                    }
            }
        }

        private bool ShouldShowWaypointLabel(string waypointId, bool selected)
        {
            if (!_showNonRegionLabels || _hideMarkerLabels)
                return false;

            if (_waypointManager == null || string.IsNullOrWhiteSpace(waypointId))
                return false;

            if (!_waypointManager.TryGetWaypoint(waypointId, out var wp))
                return false;

            if (!string.IsNullOrWhiteSpace(_activeWaypointRenameId) &&
                string.Equals(_activeWaypointRenameId, waypointId, StringComparison.Ordinal))
            {
                return false;
            }

            var profile = GetWaypointLabelProfile();

            if (selected)
                return true;

            switch (profile.labelDisplayMode)
            {
                case MapUIStyleSettings.MapLabelDisplayMode.Always:
                    return EvaluateWaypointRevealAlpha(selected) > 0.02f;

                case MapUIStyleSettings.MapLabelDisplayMode.SelectedOnly:
                    return false;

                case MapUIStyleSettings.MapLabelDisplayMode.Contextual:
                default:
                    return EvaluateWaypointRevealAlpha(selected) > 0.02f;
            }
        }

        private void CloseWaypointRenameEditor(bool commit)
        {
            if (_activeWaypointRenameField == null)
                return;

            string waypointId = _activeWaypointRenameId;
            string newValue = _activeWaypointRenameField.value;

            if (commit && !string.IsNullOrWhiteSpace(waypointId))
                _waypointManager?.RenameWaypoint(waypointId, newValue);

            if (_activeWaypointRenameField.parent != null)
                _activeWaypointRenameField.RemoveFromHierarchy();

            _activeWaypointRenameField = null;
            _activeWaypointRenameId = null;

            MarkWaypointLabelLayoutDirty();
            LayoutWaypointLabels();
            UpdateWaypointRenameEditorPosition();
        }

        private void UpdateWaypointRenameEditorPosition()
        {
            if (_activeWaypointRenameField == null || string.IsNullOrWhiteSpace(_activeWaypointRenameId))
                return;

            if (!_waypointAnchorLocal.TryGetValue(_activeWaypointRenameId, out var anchorLocal))
                return;

            Vector2 anchorViewport = _pan + anchorLocal * _zoom;

            // Use authored dimensions first so first-frame placement is deterministic.
            float width = _activeWaypointRenameField.resolvedStyle.width;
            if (width <= 1f)
                width = _activeWaypointRenameField.layout.width;
            if (width <= 1f)
                width = 112f;

            float height = _activeWaypointRenameField.resolvedStyle.height;
            if (height <= 1f)
                height = _activeWaypointRenameField.layout.height;
            if (height <= 1f)
                height = 20f;

            Vector2 center = anchorViewport + _activeWaypointRenameScreenOffset;

            if (TryGetViewportSize(out float vw, out float vh))
            {
                center.x = Mathf.Clamp(center.x, 2f + width * 0.5f, vw - 2f - width * 0.5f);
                center.y = Mathf.Clamp(center.y, 2f + height * 0.5f, vh - 2f - height * 0.5f);
            }

            float newLeft = Mathf.Round(center.x - width * 0.5f);
            float newTop = Mathf.Round(center.y - height * 0.5f);

            float currentLeft = _activeWaypointRenameField.resolvedStyle.left;
            float currentTop = _activeWaypointRenameField.resolvedStyle.top;

            if (!float.IsFinite(currentLeft) || Mathf.Abs(currentLeft - newLeft) > 0.25f)
                _activeWaypointRenameField.style.left = newLeft;

            if (!float.IsFinite(currentTop) || Mathf.Abs(currentTop - newTop) > 0.25f)
                _activeWaypointRenameField.style.top = newTop;
        }

        private void OpenWaypointRenameEditor(string waypointId, Color accent, string initialValue, bool selectedNow, float labelSizeMultiplier)
        {
            EnsureLabelOverlay();

            if (_labelOverlay == null || string.IsNullOrWhiteSpace(waypointId))
                return;

            if (_activeWaypointRenameField != null)
            {
                if (string.Equals(_activeWaypointRenameId, waypointId, StringComparison.Ordinal))
                {
                    _activeWaypointRenameField.Focus();
                    _activeWaypointRenameField.SelectAll();
                    return;
                }

                CloseWaypointRenameEditor(commit: true);
            }

            int renameFontSize = Mathf.RoundToInt(GetWaypointCompactFontSize(selectedNow) * Mathf.Max(0.1f, labelSizeMultiplier));

            var textField = new TextField
            {
                name = "WaypointRenameField",
                value = initialValue
            };

            StyleWaypointRenameField(textField, accent, 1f, renameFontSize);

            textField.RegisterCallback<KeyDownEvent>(keyEvt =>
            {
                if (keyEvt.keyCode == KeyCode.Return || keyEvt.keyCode == KeyCode.KeypadEnter)
                {
                    CloseWaypointRenameEditor(commit: true);
                    keyEvt.StopPropagation();
                }
                else if (keyEvt.keyCode == KeyCode.Escape)
                {
                    CloseWaypointRenameEditor(commit: false);
                    keyEvt.StopPropagation();
                }
            });

            textField.RegisterCallback<FocusOutEvent>(_ => CloseWaypointRenameEditor(commit: true));

            _activeWaypointRenameField = textField;
            _activeWaypointRenameId = waypointId;

            // Hide the underlying waypoint label immediately so there is no pre-layout frame
            // where it can briefly render from a stale/default state.
            if (_waypointLabels.TryGetValue(waypointId, out var renameSourceLabel) && renameSourceLabel != null)
            {
                renameSourceLabel.style.display = DisplayStyle.None;
                renameSourceLabel.style.visibility = Visibility.Hidden;
                SyncWaypointLabelTransformImmediate(renameSourceLabel);
            }

            // Keep the field in the live overlay tree but invisible until its first
            // deterministic viewport rect has been written.
            textField.style.display = DisplayStyle.Flex;
            textField.style.visibility = Visibility.Hidden;

            _labelOverlay.Add(textField);
            UpdateWaypointRenameEditorPosition();
            textField.style.visibility = Visibility.Visible;

            textField.Focus();
            textField.SelectAll();

            MarkWaypointLabelLayoutDirty();
            LayoutWaypointLabels();
            UpdateWaypointRenameEditorPosition();
        }

        private List<Rect> CollectVisibleOverlayLabelRects(bool includeWaypointLabels = false)
        {
            var rects = new List<Rect>(64);

            void CollectFrom(Dictionary<string, Label> labels)
            {
                foreach (var kv in labels)
                {
                    Label label = kv.Value;
                    if (label == null || label.resolvedStyle.display == DisplayStyle.None)
                        continue;

                    Rect r = label.worldBound;
                    if (r.width <= 0f || r.height <= 0f)
                        continue;

                    rects.Add(new Rect(
                        r.x - _viewport.worldBound.x,
                        r.y - _viewport.worldBound.y,
                        r.width,
                        r.height));
                }
            }

            // These are true external blockers for waypoint labels.
            CollectFrom(_polylineLabelVisuals);
            CollectFrom(_poiOverlayLabels);
            CollectFrom(_regionOverlayLabels);

            // IMPORTANT:
            // Do NOT include existing waypoint label rects when solving waypoint labels.
            // Doing so causes the solver to treat last frame's waypoint placement as occupied,
            // which makes labels bounce between alternate slots across repeated layout passes.
            if (includeWaypointLabels)
                CollectFrom(_waypointLabels);

            // The active rename field is still a real blocker.
            if (_activeWaypointRenameField != null &&
                _activeWaypointRenameField.resolvedStyle.display != DisplayStyle.None)
            {
                Rect r = _activeWaypointRenameField.worldBound;
                if (r.width > 0f && r.height > 0f)
                {
                    rects.Add(new Rect(
                        r.x - _viewport.worldBound.x,
                        r.y - _viewport.worldBound.y,
                        r.width,
                        r.height));
                }
            }

            return rects;
        }

        private void StyleWaypointRenameField(TextField textField, Color accent, float invZoom, int fontSize)
        {
            if (textField == null)
                return;

            var style = GetLabelStyleById(WaypointEditableLabelStyleId);

            float stripeWidth = Mathf.Max(2f, style.accentStripeWidth);
            float padX = Mathf.Max(4f, style.paddingX);
            float padY = Mathf.Max(2f, style.paddingY);
            float radius = Mathf.Max(4f, style.cornerRadius);

            int compactFontSize = Mathf.Clamp(
                Mathf.RoundToInt(GetWaypointCompactFontSize(selected: false)),
                style.fontMin,
                style.fontMax);

            float width = 112f;
            float height = 20f;

            Color plate = new Color(0.08f, 0.11f, 0.16f, Mathf.Clamp01(style.plateAlpha + 0.10f));

            textField.style.position = Position.Absolute;
            textField.style.width = width;
            textField.style.minWidth = width;
            textField.style.maxWidth = width;
            textField.style.height = height;
            textField.style.minHeight = height;
            textField.style.maxHeight = height;

            textField.style.marginLeft = 0f;
            textField.style.marginRight = 0f;
            textField.style.marginTop = 0f;
            textField.style.marginBottom = 0f;

            textField.style.paddingLeft = stripeWidth + padX;
            textField.style.paddingRight = padX;
            textField.style.paddingTop = padY;
            textField.style.paddingBottom = padY;

            textField.style.backgroundColor = plate;
            textField.style.color = Color.white;
            textField.style.unityTextAlign = TextAnchor.MiddleLeft;
            textField.style.fontSize = compactFontSize;
            textField.style.unityFontStyleAndWeight = FontStyle.Bold;

            textField.style.borderLeftWidth = stripeWidth;
            textField.style.borderRightWidth = 0f;
            textField.style.borderTopWidth = 0f;
            textField.style.borderBottomWidth = 0f;

            textField.style.borderLeftColor = accent;
            textField.style.borderRightColor = Color.clear;
            textField.style.borderTopColor = Color.clear;
            textField.style.borderBottomColor = Color.clear;

            textField.style.borderTopLeftRadius = radius;
            textField.style.borderTopRightRadius = radius;
            textField.style.borderBottomLeftRadius = radius;
            textField.style.borderBottomRightRadius = radius;

            void StyleInputElement(VisualElement input)
            {
                if (input == null)
                    return;

                input.style.backgroundColor = plate;
                input.style.color = Color.white;
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                input.style.marginLeft = 0f;
                input.style.marginRight = 0f;
                input.style.marginTop = 0f;
                input.style.marginBottom = 0f;
                input.style.paddingLeft = stripeWidth + padX;
                input.style.paddingRight = padX;
                input.style.paddingTop = padY;
                input.style.paddingBottom = padY;
                input.style.borderLeftWidth = 0f;
                input.style.borderRightWidth = 0f;
                input.style.borderTopWidth = 0f;
                input.style.borderBottomWidth = 0f;
                input.style.height = height;
                input.style.minHeight = height;
                input.style.maxHeight = height;
                input.style.fontSize = compactFontSize;
                input.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            StyleInputElement(textField.Q(className: "unity-text-input"));
            StyleInputElement(textField.Q(TextField.textInputUssName));
            StyleInputElement(textField.Q("unity-text-input"));

            foreach (var child in textField.Children())
                StyleInputElement(child);
        }

        private void LayoutWaypointLabels()
        {
            if (_labelOverlay == null || _viewport == null || _waypointManager == null)
                return;

            if (_waypointLabels.Count == 0)
                return;

            if (!TryGetViewportSize(out float vw, out float vh))
                return;

            bool showLabels = _showNonRegionLabels && !_hideMarkerLabels;

            if (!showLabels)
            {
                foreach (var kv in _waypointLabels)
                {
                    if (kv.Value != null)
                    {
                        kv.Value.style.display = DisplayStyle.None;
                        kv.Value.style.visibility = Visibility.Hidden;
                    }
                }

                UpdateWaypointRenameEditorPosition();
                return;
            }

            float now = Time.unscaledTime;
            bool zoomChangedEnough =
                _lastWaypointLayoutZoom < 0f ||
                Mathf.Abs(_zoom - _lastWaypointLayoutZoom) >= WaypointLayoutZoomThreshold;

            bool panChangedEnough =
                float.IsInfinity(_lastWaypointLayoutPan.x) ||
                Vector2.Distance(_pan, _lastWaypointLayoutPan) >= WaypointLayoutPanThresholdPx;

            bool settleElapsed =
                (now - _lastWaypointLayoutTime) >= WaypointLayoutSettleDelay;

            // During light transform churn, keep cached placement stable instead of constantly re-solving.
            if (!_waypointLabelLayoutDirty && !zoomChangedEnough && !panChangedEnough && !settleElapsed)
            {
                ApplyCachedWaypointLabelPlacement();
                UpdateWaypointRenameEditorPosition();
                return;
            }

            // Only collect non-waypoint overlay blockers here.
            // Waypoint labels should block one another only within THIS solve pass.
            var occupied = CollectVisibleOverlayLabelRects(includeWaypointLabels: false);

            const float margin = 2f;
            const float baseDistance = 18f;
            const float diagonalDistance = 14f;

            Vector2[] slotDirections =
            {
        new Vector2(0f, -1f),   // above
        new Vector2(1f, -1f),   // upper-right
        new Vector2(-1f, -1f),  // upper-left
        new Vector2(1f, 0f),    // right
        new Vector2(-1f, 0f),   // left
        new Vector2(0f, 1f),    // below
        new Vector2(1f, 1f),    // lower-right
        new Vector2(-1f, 1f),   // lower-left
    };

            float[] slotDistances =
            {
        baseDistance,
        diagonalDistance,
        diagonalDistance,
        baseDistance,
        baseDistance,
        baseDistance,
        diagonalDistance,
        diagonalDistance,
    };

            bool RectOverlapsAny(Rect rect, List<Rect> others)
            {
                for (int i = 0; i < others.Count; i++)
                {
                    if (others[i].Overlaps(rect))
                        return true;
                }

                return false;
            }

            for (int i = 0; i < _waypointManager.Waypoints.Count; i++)
            {
                var wp = _waypointManager.Waypoints[i];
                if (string.IsNullOrWhiteSpace(wp.id))
                    continue;

                if (!_waypointLabels.TryGetValue(wp.id, out var label) || label == null)
                    continue;

                bool selected = _waypointManager.IsWaypointSelected(wp.id);

                ApplyWaypointLabelVisual(wp.id, selected);

                if (!ShouldShowWaypointLabel(wp.id, selected))
                {
                    label.style.display = DisplayStyle.None;
                    label.style.visibility = Visibility.Hidden;
                    continue;
                }

                if (!_waypointAnchorLocal.TryGetValue(wp.id, out var anchorLocal))
                {
                    label.style.display = DisplayStyle.None;
                    label.style.visibility = Visibility.Hidden;
                    continue;
                }

                Vector2 anchorViewport = _pan + anchorLocal * _zoom;
                Vector2 size = MeasureWaypointLabel(label);

                int cachedSlot = 0;
                if (_waypointLabelScreenOffsets.TryGetValue(wp.id, out var cachedOffset) && cachedOffset.sqrMagnitude > 0.0001f)
                {
                    float bestDot = float.NegativeInfinity;
                    for (int s = 0; s < slotDirections.Length; s++)
                    {
                        Vector2 dir = slotDirections[s].normalized;
                        float dot = Vector2.Dot(cachedOffset.normalized, dir);
                        if (dot > bestDot)
                        {
                            bestDot = dot;
                            cachedSlot = s;
                        }
                    }
                }

                Rect bestRect = default;
                Vector2 bestOffset = default;
                bool found = false;

                for (int attempt = 0; attempt < slotDirections.Length; attempt++)
                {
                    int slot = (cachedSlot + attempt) % slotDirections.Length;
                    Vector2 dir = slotDirections[slot].normalized;
                    Vector2 center = anchorViewport + dir * slotDistances[slot];

                    Rect rect = new Rect(
                        Mathf.Round(center.x - size.x * 0.5f),
                        Mathf.Round(center.y - size.y * 0.5f),
                        size.x,
                        size.y);

                    rect.x = Mathf.Clamp(rect.x, margin, vw - margin - rect.width);
                    rect.y = Mathf.Clamp(rect.y, margin, vh - margin - rect.height);

                    if (RectOverlapsAny(rect, occupied))
                        continue;

                    bestRect = rect;
                    bestOffset = new Vector2(
                        rect.center.x - anchorViewport.x,
                        rect.center.y - anchorViewport.y);

                    found = true;
                    break;
                }

                if (!found)
                {
                    // Fall back to cached rect if it is still sane.
                    if (_waypointLabelViewportRects.TryGetValue(wp.id, out var cachedRect))
                    {
                        bestRect = cachedRect;
                        bestOffset = new Vector2(
                            cachedRect.center.x - anchorViewport.x,
                            cachedRect.center.y - anchorViewport.y);
                        found = true;
                    }
                }

                if (!found)
                {
                    // Last-resort default: stable position above.
                    Vector2 center = anchorViewport + Vector2.up * -baseDistance;
                    bestRect = new Rect(
                        Mathf.Clamp(Mathf.Round(center.x - size.x * 0.5f), margin, vw - margin - size.x),
                        Mathf.Clamp(Mathf.Round(center.y - size.y * 0.5f), margin, vh - margin - size.y),
                        size.x,
                        size.y);

                    bestOffset = new Vector2(
                        bestRect.center.x - anchorViewport.x,
                        bestRect.center.y - anchorViewport.y);
                }

                SetWaypointLabelViewportRect(wp.id, bestRect);
                label.style.display = DisplayStyle.Flex;
                label.style.visibility = Visibility.Visible;

                _waypointLabelViewportRects[wp.id] = bestRect;
                _waypointLabelScreenOffsets[wp.id] = bestOffset;

                // Only now should waypoint labels block later waypoint labels in this same solve.
                occupied.Add(bestRect);
            }

            _lastWaypointLayoutZoom = _zoom;
            _lastWaypointLayoutPan = _pan;
            _lastWaypointLayoutTime = now;
            _waypointLabelLayoutDirty = false;

            UpdateWaypointRenameEditorPosition();
        }

        private Vector2 MeasureWaypointLabel(Label label)
        {
            if (label == null)
                return new Vector2(48f, 20f);

            var measured = label.MeasureTextSize(
                label.text ?? string.Empty,
                0f,
                MeasureMode.Undefined,
                0f,
                MeasureMode.Undefined);

            float width = measured.x;
            float height = measured.y;

            var resolved = label.resolvedStyle;

            float paddingLeft = float.IsNaN(resolved.paddingLeft) ? 0f : resolved.paddingLeft;
            float paddingRight = float.IsNaN(resolved.paddingRight) ? 0f : resolved.paddingRight;
            float paddingTop = float.IsNaN(resolved.paddingTop) ? 0f : resolved.paddingTop;
            float paddingBottom = float.IsNaN(resolved.paddingBottom) ? 0f : resolved.paddingBottom;

            float borderLeft = float.IsNaN(resolved.borderLeftWidth) ? 0f : resolved.borderLeftWidth;
            float borderRight = float.IsNaN(resolved.borderRightWidth) ? 0f : resolved.borderRightWidth;
            float borderTop = float.IsNaN(resolved.borderTopWidth) ? 0f : resolved.borderTopWidth;
            float borderBottom = float.IsNaN(resolved.borderBottomWidth) ? 0f : resolved.borderBottomWidth;

            width += paddingLeft + paddingRight + borderLeft + borderRight;
            height += paddingTop + paddingBottom + borderTop + borderBottom;

            // Small safety margin so collision tests are a touch conservative and stable.
            width += 6f;
            height += 4f;

            width = Mathf.Max(24f, Mathf.Ceil(width));
            height = Mathf.Max(16f, Mathf.Ceil(height));

            return new Vector2(width, height);
        }

        private void ApplyWaypointLabelVisual(string id, bool selected)
        {
            if (!_waypointLabels.TryGetValue(id, out var label) || label == null)
                return;

            var profile = GetWaypointLabelProfile();
            var style = profile.style;

            Color accent = Color.white;
            float labelSizeMultiplier = 1f;

            if (_waypointManager != null && _waypointManager.TryGetWaypoint(id, out var waypoint))
            {
                accent = waypoint.color;
                labelSizeMultiplier = waypoint.labelSizeMultiplier > 0f ? Mathf.Max(0.1f, waypoint.labelSizeMultiplier) : 1f;
                label.text = waypoint.displayName;
            }

            ApplySharedOverlayLabelVisual(
                label,
                style,
                selected,
                accent,
                labelSizeMultiplier,
                selected ? style.selectedColor : style.color);

            float revealAlpha = EvaluateWaypointRevealAlpha(selected);
            label.style.opacity = selected ? 1f : revealAlpha;

            // Do not touch display here.
            // LayoutWaypointLabels() owns show/hide, while rebuild paths already keep
            // genuinely unplaced labels hidden until they have a rect.
            SyncWaypointLabelTransformImmediate(label);
        }

        private void ApplyPolylineLabelVisual(string id, bool selected)
        {
            if (!_polylineLabelVisuals.TryGetValue(id, out var label) || label == null)
                return;

            ResolvedMapLabelRule rule = _mapData != null && TryGetPolylineById(id, out var polyline)
                ? ResolveLabelRuleForPolyline(polyline)
                : ResolveFallbackRule(MapUIStyleSettings.MapElementSemantic.SkiRun, POIType.SkiRun, POICategory.None);

            var style = ResolveStandardLabelStyleForRule(rule);

            Color accent = _polylineLabelAccent != null && _polylineLabelAccent.TryGetValue(id, out var accentColor)
                ? accentColor
                : Color.white;

            Color textColor = selected ? style.selectedColor : style.color;
            if (TryGetLinkedWaypointForPolyline(id, out var linkedWp))
                textColor = linkedWp.color;

            ApplySharedOverlayLabelVisual(
                label,
                style,
                selected,
                accent,
                rule.labelSizeMultiplier,
                textColor);
        }

        private MapUIStyleSettings.MapLabelStyleProfile GetLabelProfileById(string styleId)
        {
            if (_style != null)
                return _style.GetStandardLabelProfileById(styleId);

            return new MapUIStyleSettings.MapLabelStyleProfile
            {
                id = StandardLabelStyleId,
                displayName = "Standard",
                labelDisplayMode = MapUIStyleSettings.MapLabelDisplayMode.Contextual,
                revealDirection = MapUIStyleSettings.MapRevealDirection.ZoomInReveal,
                fadeStartZoom = DefaultPoiLabelRevealZoom,
                fadeEndZoom = 2.40f,
                labelPriority = 8,
                maxContextCount = DefaultMaxContextPoiLabels,
                style = new MapUIStyleSettings.MapLabelVisualStyle
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
            };
        }

        private MapUIStyleSettings.MapLabelVisualStyle GetLabelStyleById(string styleId)
        {
            return GetLabelProfileById(styleId).style;
        }

        private MapUIStyleSettings.MapLabelStyleProfile ResolveLabelProfileForRule(ResolvedMapLabelRule rule)
        {
            return GetLabelProfileById(rule.labelStyleId);
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

            // Re-run overlay placement so selected label wins.
            LayoutOverlayLabels();

            if (_waypointLabelLayoutDirty)
                LayoutWaypointLabels();
            else
                ApplyCachedWaypointLabelPlacement();
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

            if (poly.lineType == MapLineType.RaceCourse)
            {
                return TryFindRaceActivityMarkerForPolyline(polylineId, out marker);
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

        private VisualElement EnsureSingleNavigationArrow(ref VisualElement arrowField, string name, float fontSize = 18f)
        {
            if (_navigationOverlay == null)
                return null;

            if (arrowField != null)
                return arrowField;

            var arrow = new VisualElement();
            arrow.name = name;
            arrow.pickingMode = PickingMode.Ignore;
            arrow.style.position = Position.Absolute;
            arrow.style.justifyContent = Justify.Center;
            arrow.style.alignItems = Align.Center;
            arrow.style.display = DisplayStyle.None;

            var glyph = new Label("▲");
            glyph.name = $"{name}_Glyph";
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.fontSize = fontSize;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.Add(glyph);

            _navigationOverlay.Add(arrow);
            arrowField = arrow;
            return arrow;
        }

        private void HideSingleNavigationArrow(VisualElement arrow)
        {
            if (arrow != null)
                arrow.style.display = DisplayStyle.None;
        }

        private void ShowDirectionalArrow(VisualElement arrow, Vector2 playerViewport, Vector2 targetViewport, Color color, float orbitRadius = 26f, float edgePadding = 14f)
        {
            if (arrow == null)
                return;

            Label glyph = arrow.Q<Label>($"{arrow.name}_Glyph");

            float viewportWidth = Mathf.Max(1f, _viewport.resolvedStyle.width);
            float viewportHeight = Mathf.Max(1f, _viewport.resolvedStyle.height);

            float minX = edgePadding;
            float maxX = viewportWidth - edgePadding;
            float minY = edgePadding;
            float maxY = viewportHeight - edgePadding;

            Vector2 dir = targetViewport - playerViewport;
            float sqr = dir.sqrMagnitude;
            if (sqr < 4f)
            {
                arrow.style.display = DisplayStyle.None;
                return;
            }

            Vector2 n = dir.normalized;

            bool targetVisible =
                targetViewport.x >= minX && targetViewport.x <= maxX &&
                targetViewport.y >= minY && targetViewport.y <= maxY;

            Vector2 arrowPos = targetVisible
                ? playerViewport + n * orbitRadius
                : GetRayRectIntersection(playerViewport, n, minX, maxX, minY, maxY);

            var arrowStyle = GetNavigationArrowStyle();
            bool useSprite = arrowStyle.sprite != null;
            if (useSprite)
            {
                float size = Mathf.Max(8f, arrowStyle.size);
                float angle = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg + arrowStyle.spriteRotationOffsetDegrees;

                arrow.style.width = size;
                arrow.style.height = size;
                arrow.style.backgroundImage = new StyleBackground(arrowStyle.sprite);
                arrow.style.unityBackgroundImageTintColor = color;
                arrow.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                arrow.style.left = arrowPos.x - (size * 0.5f);
                arrow.style.top = arrowPos.y - (size * 0.5f);
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                if (glyph != null)
                    glyph.style.display = DisplayStyle.None;
            }
            else
            {
                arrow.style.width = StyleKeyword.Auto;
                arrow.style.height = StyleKeyword.Auto;
                arrow.style.backgroundImage = StyleKeyword.None;
                arrow.style.left = arrowPos.x - 8f;
                arrow.style.top = arrowPos.y - 10f;
                arrow.transform.rotation = Quaternion.identity;
                if (glyph != null)
                {
                    glyph.text = GetArrowGlyph(n);
                    glyph.style.color = color;
                    glyph.style.display = DisplayStyle.Flex;
                }
            }

            arrow.style.display = DisplayStyle.Flex;
        }

        private void UpdateActiveNavigationTargetArrow(bool havePlayerLocal, Vector2 playerLocal)
        {
            HideSingleNavigationArrow(_activeNavigationArrow);

            if (!havePlayerLocal || _navigationOverlay == null)
                return;

            if (!NavigationTargetController.TryGetActiveState(out var state))
                return;

            // Waypoints are already handled by the waypoint arrow pool.
            if (state.request.kind == NavigationTargetKind.Waypoint)
                return;

            if (!state.request.showWorldBeacon && !state.request.showHud)
                return;

            if (!TryProjectWorldToLocal(state.beaconWorldPosition, out var targetLocal))
                return;

            Vector2 playerViewport = ContentToViewport(playerLocal);
            Vector2 targetViewport = ContentToViewport(targetLocal);

            var arrow = EnsureSingleNavigationArrow(ref _activeNavigationArrow, "ActiveNavigationArrow");

            Color color = state.request.accentColor.a > 0f
                ? state.request.accentColor
                : new Color(0.36f, 0.74f, 1f, 1f);

            ShowDirectionalArrow(arrow, playerViewport, targetViewport, color);
        }

        private void UpdateActivityCheckpointArrow(bool havePlayerLocal, Vector2 playerLocal)
        {
            HideSingleNavigationArrow(_activityCheckpointArrow);

            if (!havePlayerLocal || _navigationOverlay == null)
                return;

            if (_activityCurrentCheckpointIndex < 0 || _activityCurrentCheckpointIndex >= _activityCheckpointWorldPositions.Count)
                return;

            if (!TryProjectWorldToLocal(_activityCheckpointWorldPositions[_activityCurrentCheckpointIndex], out var checkpointLocal))
                return;

            Vector2 playerViewport = ContentToViewport(playerLocal);
            Vector2 checkpointViewport = ContentToViewport(checkpointLocal);

            var arrow = EnsureSingleNavigationArrow(ref _activityCheckpointArrow, "ActivityCheckpointArrow");

            Color color = new Color(0.12f, 0.95f, 0.25f, 1f); // current checkpoint green
            ShowDirectionalArrow(arrow, playerViewport, checkpointViewport, color);
        }

        private void EnsureWaypointArrowElement(string waypointId)
        {
            if (_navigationOverlay == null || string.IsNullOrWhiteSpace(waypointId) || _waypointNavArrows.ContainsKey(waypointId))
                return;

            var arrow = new VisualElement();
            arrow.name = $"WaypointNavArrow_{waypointId}";
            arrow.pickingMode = PickingMode.Ignore;
            arrow.style.position = Position.Absolute;
            arrow.style.justifyContent = Justify.Center;
            arrow.style.alignItems = Align.Center;
            arrow.style.display = DisplayStyle.None;

            var glyph = new Label("▲");
            glyph.name = $"{arrow.name}_Glyph";
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.fontSize = 18f;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.Add(glyph);

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

            HideSingleNavigationArrow(_activeNavigationArrow);
            HideSingleNavigationArrow(_activityCheckpointArrow);

            _selectedWaypointConnector?.Hide();

            if (!havePlayerLocal)
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

                if (_waypointManager != null && _waypointManager.Waypoints != null)
                {
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
                        var arrowStyle = GetNavigationArrowStyle();
                        bool useSprite = arrowStyle.sprite != null;
                        var glyph = arrow.Q<Label>($"{arrow.name}_Glyph");

                        if (useSprite)
                        {
                            float size = Mathf.Max(8f, arrowStyle.size);
                            float angle = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg + arrowStyle.spriteRotationOffsetDegrees;
                            arrow.style.width = size;
                            arrow.style.height = size;
                            arrow.style.backgroundImage = new StyleBackground(arrowStyle.sprite);
                            arrow.style.unityBackgroundImageTintColor = wp.color;
                            arrow.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                            arrow.style.left = arrowPos.x - (size * 0.5f);
                            arrow.style.top = arrowPos.y - (size * 0.5f);
                            arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                            if (glyph != null)
                                glyph.style.display = DisplayStyle.None;
                        }
                        else
                        {
                            arrow.style.width = StyleKeyword.Auto;
                            arrow.style.height = StyleKeyword.Auto;
                            arrow.style.backgroundImage = StyleKeyword.None;
                            arrow.style.left = arrowPos.x - 8f;
                            arrow.style.top = arrowPos.y - 10f;
                            arrow.transform.rotation = Quaternion.identity;
                            if (glyph != null)
                            {
                                glyph.text = GetArrowGlyph(n);
                                glyph.style.color = wp.color;
                                glyph.style.display = DisplayStyle.Flex;
                            }
                        }

                        arrow.style.display = DisplayStyle.Flex;
                    }
                }

                UpdateActiveNavigationTargetArrow(havePlayerLocal, playerLocal);
                UpdateActivityCheckpointArrow(havePlayerLocal, playerLocal);

                return;
            }

            if (_waypointManager == null || _waypointManager.Waypoints == null)
                return;

            if (!string.IsNullOrWhiteSpace(_waypointManager.SelectedWaypointId) &&
                _waypointManager.TryGetWaypoint(_waypointManager.SelectedWaypointId, out var selectedWp) &&
                TryProjectWorldToLocal(selectedWp.worldPosition, out var selectedLocal))
            {
                Vector2 targetViewport = ContentToViewport(selectedLocal);
                _selectedWaypointConnector?.Show(playerViewport, targetViewport, selectedWp.color);
            }

            UpdateActiveNavigationTargetArrow(havePlayerLocal, playerLocal);
            UpdateActivityCheckpointArrow(havePlayerLocal, playerLocal);
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
