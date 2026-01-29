using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Map;

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

        private MapPolylineLayer _polyLayer;

        private bool _bound;
        private bool _dirty = true;

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

        // -------------------------
        // Player tracking + interactivity
        // -------------------------
        public event Action<Map.MapMarker> MarkerSelected;
        public event Action<Map.MapPolyline> PolylineSelected;
        public event Action SelectionCleared;

        private Transform _playerTransform;
        private VisualElement _playerMarker;         // UI element (dot)
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

            float vw = _viewport.resolvedStyle.width;
            float vh = _viewport.resolvedStyle.height;
            if (vw <= 1f || vh <= 1f) return;

            // Match WheelEvent behavior (positive scrollDeltaY usually means "scroll up/down" depending on binding).
            float prevZoom = _zoom;

            // We treat positive delta as "zoom out" similar to wheel's evt.delta.y.
            float zoomFactor = Mathf.Pow(1.12f, -scrollDeltaY * 0.1f);
            _zoom = Mathf.Clamp(_zoom * zoomFactor, 0.15f, 10f);

            Vector2 center = new Vector2(vw * 0.5f, vh * 0.5f);

            Vector2 contentBefore = (center - _pan) / prevZoom;
            _pan = center - contentBefore * _zoom;

            ApplyTransform();
            _polyLayer?.SetZoom(_zoom);
            _trailLayer?.SetZoom(_zoom);
        }

        // Click / drag discrimination for selection
        private bool _pointerDown;
        private bool _didDrag;
        private Vector2 _pointerDownPosViewport;
        private const float ClickDragThresholdPx = 6f;

        // Selection state
        private string _selectedMarkerId;
        private string _selectedPolylineId;

        public void Bind(VisualElement root, MapData mapData, Camera mapCamera)
        {
            if (root == null) return;

            _mapData = mapData;

            // Keep the reference camera always. PreferCameraProjection controls whether we USE it for placement,
            // but we still want it available for debug comparison and for deriving a better MapProjection.
            _mapCamera = mapCamera;

            // IMPORTANT: do NOT reconfigure the camera from MapProjection here.
            // The whole point is to let the camera remain an independent reference that matches the background image.
            ConfigureReferenceCamera(); // now becomes a lightweight validator (see replacement below)

            _viewport = root.Q<VisualElement>("MapViewport");
            _content = root.Q<VisualElement>("MapContent");
            _bg = root.Q<VisualElement>("MapBackground");
            _polyHost = root.Q<VisualElement>("MapPolylines");
            _markerHost = root.Q<VisualElement>("MapMarkers");

            // Ensure absolute positioning for map layers.
            if (_bg != null) _bg.style.position = Position.Absolute;
            if (_polyHost != null) _polyHost.style.position = Position.Absolute;
            if (_markerHost != null) _markerHost.style.position = Position.Absolute;

            // Enforce draw order by hierarchy (works across UI Toolkit versions).
            // Background at the back, then polylines, then markers on top.
            _bg?.SendToBack();
            _polyHost?.BringToFront();
            _markerHost?.BringToFront();

            _missingLabel = root.Q<Label>("Lbl_MapMissing");
            _btnReset = root.Q<VisualElement>("Btn_MapReset");

            if (_btnReset != null)
                _btnReset.RegisterCallback<ClickEvent>(_ => ResetViewToFit());

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

            // Install polyline + trail layers
            if (_polyHost != null)
            {
                _polyHost.Clear();

                _polyLayer = new MapPolylineLayer();
                _polyHost.Add(_polyLayer);

                _trailLayer = new MapTrailLayer();
                _polyHost.Add(_trailLayer); // draw above base polylines, below markers
            }

            // Install player marker (persistent; not cleared when rebuilding POI markers)
            if (_markerHost != null && _playerMarker == null)
            {
                _playerMarker = new VisualElement();
                _playerMarker.name = "MapPlayerMarker";
                _playerMarker.AddToClassList("map-player-marker");
                _playerMarker.style.position = Position.Absolute;
                _playerMarker.style.width = 12;
                _playerMarker.style.height = 12;
                _playerMarker.style.borderTopLeftRadius = 999;
                _playerMarker.style.borderTopRightRadius = 999;
                _playerMarker.style.borderBottomLeftRadius = 999;
                _playerMarker.style.borderBottomRightRadius = 999;
                _playerMarker.style.backgroundColor = new Color(0.25f, 0.9f, 1f, 0.95f);
                _playerMarker.style.borderLeftWidth = 2;
                _playerMarker.style.borderRightWidth = 2;
                _playerMarker.style.borderTopWidth = 2;
                _playerMarker.style.borderBottomWidth = 2;
                _playerMarker.style.borderLeftColor = new Color(0f, 0f, 0f, 0.65f);
                _playerMarker.style.borderRightColor = new Color(0f, 0f, 0f, 0.65f);
                _playerMarker.style.borderTopColor = new Color(0f, 0f, 0f, 0.65f);
                _playerMarker.style.borderBottomColor = new Color(0f, 0f, 0f, 0.65f);

                // Make it clickable (later we can open an info panel for the player).
                _playerMarker.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

                _markerHost.Add(_playerMarker);
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

            // First layout pass: when geometry exists, fit the map.
            if (_viewport != null)
            {
                _viewport.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    // Embedded minimaps (watch + home tile) don't always get an explicit Refresh() call.
                    // Once we have a real layout rect, build visuals + fit.
                    if (_dirty)
                        Refresh();
                });
            }
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

            if (!hasData)
            {
                // Clear visuals when missing
                _bg.style.backgroundImage = StyleKeyword.None;
                _markerHost.Clear();
                _polyLayer?.SetData(_mapData,_contentSize,(_mapData != null && _mapData.PreferCameraProjection) ? _mapCamera : null);

                _trailLayer?.SetData(_mapData, _contentSize,(_mapData != null && _mapData.PreferCameraProjection) ? _mapCamera : null);

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

            // Polylines
            _polyLayer?.SetData(_mapData,_contentSize,(_mapData != null && _mapData.PreferCameraProjection) ? _mapCamera : null);

            // Player trail (must be initialized with same projection inputs as polylines)
            _trailLayer?.SetData(_mapData, _contentSize, (_mapData != null && _mapData.PreferCameraProjection) ? _mapCamera : null);

            // Markers
            RebuildMarkers();

            _dirty = false;

            // Fit view after rebuild (only if user hasn't already moved it)
            ResetViewToFit();

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

        public void DebugLogSuggestedProjectionFromReferenceCamera(float planeY = 0f)
        {
            if (_mapCamera == null)
            {
                Debug.LogWarning("[PhoneMapPageUI] No reference camera assigned.");
                return;
            }

            // Intersect viewport corner rays with the plane y = planeY.
            Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

            Vector3 Intersect(float vx, float vy)
            {
                Ray r = _mapCamera.ViewportPointToRay(new Vector3(vx, vy, 0f));
                if (plane.Raycast(r, out float enter))
                    return r.GetPoint(enter);

                // Fallback: if no intersection, still return something stable.
                return r.origin;
            }

            Vector3 p00 = Intersect(0f, 0f);
            Vector3 p10 = Intersect(1f, 0f);
            Vector3 p01 = Intersect(0f, 1f);
            Vector3 p11 = Intersect(1f, 1f);

            float minX = Mathf.Min(p00.x, p10.x, p01.x, p11.x);
            float maxX = Mathf.Max(p00.x, p10.x, p01.x, p11.x);
            float minZ = Mathf.Min(p00.z, p10.z, p01.z, p11.z);
            float maxZ = Mathf.Max(p00.z, p10.z, p01.z, p11.z);

            Vector2 worldMinXZ = new Vector2(minX, minZ);
            Vector2 worldMaxXZ = new Vector2(maxX, maxZ);

            Debug.Log(
                $"[PhoneMapPageUI] Suggested MapProjection from reference camera '{_mapCamera.name}' at planeY={planeY}: " +
                $"worldMinXZ={worldMinXZ:F2} worldMaxXZ={worldMaxXZ:F2} " +
                $"camPos={_mapCamera.transform.position:F2} camRot={_mapCamera.transform.rotation.eulerAngles:F2} " +
                $"ortho={_mapCamera.orthographic} orthoSize={_mapCamera.orthographicSize:F2} aspect={_mapCamera.aspect:F4}"
            );
        }

        public void MarkDirty() => _dirty = true;

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

            _content.style.width = _contentSize.x;
            _content.style.height = _contentSize.y;

            // Ensure each layer matches content bounds
            UpdateLayerSizes();
        }

        private bool TryProjectWorldToUV(Vector3 worldPos, out Vector2 uv)
        {
            // Use camera projection ONLY when the map asset explicitly says so.
            if (_mapCamera != null && _mapData != null && _mapData.PreferCameraProjection)
            {
                // Always project on the XZ plane so terrain height doesn't skew the viewport mapping.
                Vector3 flat = new Vector3(worldPos.x, 0f, worldPos.z);
                Vector3 vp = _mapCamera.WorldToViewportPoint(flat);

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
            // so we explicitly size the MapPolylineLayer as well.
            SetLayerSize(_polyLayer);
            SetLayerSize(_trailLayer);

            SetLayerSize(_markerHost);
            SetLayerSize(_debugHost);
        }

        private void SetLayerSize(VisualElement ve)
        {
            if (ve == null) return;

            ve.style.position = Position.Absolute;
            ve.style.width = _contentSize.x;
            ve.style.height = _contentSize.y;
            ve.style.left = 0;
            ve.style.top = 0;

            // Ensure we don't end up with ambiguous layouts (left+right, top+bottom) across Unity versions.
            ve.style.right = StyleKeyword.Auto;
            ve.style.bottom = StyleKeyword.Auto;
        }

        private void RebuildMarkers()
        {
            // Preserve the persistent player marker while rebuilding POI markers.
            if (_playerMarker != null)
                _playerMarker.RemoveFromHierarchy();

            _markerHost.Clear();

            if (_playerMarker != null)
                _markerHost.Add(_playerMarker);


            // Debug overlay cleanup
            if (_debugHost != null)
                _debugHost.Clear();

            var list = _mapData.Markers;
            if (list == null) return;

            // Used to keep run/lift name tags from overlapping each other (local content space).
            var occupiedLabelRects = new List<Rect>(64);

            bool IntersectsAny(Rect r)
            {
                for (int i = 0; i < occupiedLabelRects.Count; i++)
                    if (occupiedLabelRects[i].Overlaps(r))
                        return true;
                return false;
            }

            Rect EstimateLabelRect(Vector2 pivotLocal, string text)
            {
                // Rough estimate: good enough to prevent obvious overlaps.
                int len = string.IsNullOrEmpty(text) ? 6 : Mathf.Clamp(text.Length, 4, 28);
                float w = Mathf.Clamp(22f + len * 7.5f, 70f, 220f);
                float h = 20f;

                // Centered on pivot
                return new Rect(pivotLocal.x - w * 0.5f, pivotLocal.y - h * 0.5f, w, h);
            }

            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.IsValid) continue;

                // Compute both UV solutions for validation:
                // - PROJ: MapProjection (bounds-based)
                // - CAM:  reference camera viewport mapping (if available)
                Vector2 uvProj = (_mapData != null) ? _mapData.WorldToMapUV(m.worldPosition) : default;

                bool camValid = false;
                Vector2 uvCam = default;

                if (_mapCamera != null)
                {
                    Vector3 flat = new Vector3(m.worldPosition.x, 0f, m.worldPosition.z);
                    Vector3 vp = _mapCamera.WorldToViewportPoint(flat);

                    // vp.z < 0 means behind camera
                    if (vp.z >= 0f)
                    {
                        uvCam = new Vector2(vp.x, vp.y);
                        camValid = true;
                    }
                }

                // Your actual placement UV remains whatever TryProjectWorldToUV decides (based on PreferCameraProjection).
                if (!TryProjectWorldToUV(m.worldPosition, out Vector2 uv))
                    continue;

                Vector2 uvRaw = uv;
                Vector2 uvInset = ApplyBackgroundInset(uvRaw);
                Vector2 localRaw = new Vector2(uvInset.x * _contentSize.x, (1f - uvInset.y) * _contentSize.y);

                //Debug.Log($"MAP MARKER '{m.displayName}' world={m.worldPosition} uvRaw={uvRaw} uvInset={uvInset} localRaw={localRaw} bgMin={_mapData.BackgroundUvMin} bgMax={_mapData.BackgroundUvMax}");

                Vector2 local = UVToLocal(uv);

                bool isRunOrLiftLabel =
    m.type == SkiGame.POI.POIType.SkiRun ||
    m.type == SkiGame.POI.POIType.SkiLift;

                // For run/lift markers: re-place to midline (and try nearby candidates), and avoid overlaps.
                if (isRunOrLiftLabel && _polyLayer != null)
                {
                    Vector2 chosen = local;
                    bool found = false;

                    // Candidate positions along the polyline (mid, then small offsets).
                    float[] candidates = { 0.50f, 0.46f, 0.54f, 0.42f, 0.58f, 0.38f, 0.62f };

                    for (int ci = 0; ci < candidates.Length; ci++)
                    {
                        if (_polyLayer.TryGetPointAlong(m.id, candidates[ci], out var candidateLocal))
                        {
                            var r = EstimateLabelRect(candidateLocal, m.displayName);
                            if (!IntersectsAny(r))
                            {
                                occupiedLabelRects.Add(r);
                                chosen = candidateLocal;
                                found = true;
                                break;
                            }
                        }
                    }

                    // If we couldn't place without overlap, we still allow selection via polyline,
                    // but we hide the label marker to reduce clutter.
                    if (!found)
                    {
                        // Keep local as-is but we'll hide the label later.
                        // (Polyline selection remains available.)
                    }
                    else
                    {
                        local = chosen;
                    }
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

                var dot = new VisualElement();
                dot.AddToClassList("map-marker-dot");
                dot.style.backgroundColor = m.color;

                var labelText = string.IsNullOrWhiteSpace(m.displayName) ? "POI" : m.displayName;
                var label = new Label(labelText);
                label.AddToClassList("map-marker-label");

                // Global minimap suppression still applies
                if (_hideMarkerLabels)
                    label.style.display = DisplayStyle.None;

                marker.Add(dot);
                marker.Add(label);

                // For run/lift: label-only style (centered) and optionally hidden if we failed overlap placement.
                if (isRunOrLiftLabel)
                {
                    // Dot is optional; remove to make it look like a nametag rather than a POI.
                    dot.style.display = DisplayStyle.None;

                    // Center label on the pivot
                    label.style.position = Position.Absolute;
                    label.style.left = 0;
                    label.style.top = 0;

                    // Basic overlap-aware placement for non run/lift labels.
                    // (Run/lift labels use the centered nametag style above.)
                    int side = 1;           // +1 right, -1 left
                    float yOffset = 0f;     // vertical nudge

                    if (!_hideMarkerLabels && !isRunOrLiftLabel)
                    {
                        float approxW = 12f + (labelText.Length * 7.2f);
                        float approxH = 20f;

                        // Assume a small dot radius + padding; good enough for overlap testing.
                        float dotR = 8f;
                        float xPad = 6f;

                        float[] yOptions = { 0f, 18f, -18f, 34f, -34f };
                        int[] sideOptions = { 1, -1 };

                        bool found = false;
                        for (int s = 0; s < sideOptions.Length && !found; s++)
                        {
                            for (int yy = 0; yy < yOptions.Length; yy++)
                            {
                                int sgn = sideOptions[s];
                                float y = yOptions[yy];

                                Vector2 center = local + new Vector2(sgn * (dotR + xPad + approxW * 0.5f), y);
                                Rect r = new Rect(center.x - approxW * 0.5f, center.y - approxH * 0.5f, approxW, approxH);

                                if (!IntersectsAny(r))
                                {
                                    occupiedLabelRects.Add(r);
                                    side = sgn;
                                    yOffset = y;
                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (!found)
                            label.style.display = DisplayStyle.None;
                    }

                    label.RegisterCallback<GeometryChangedEvent>(_ =>
                    {
                        float xPad = 6f;
                        float dotRight = (dot.resolvedStyle.width * 0.5f) + xPad;

                        // Place label either right or left of the dot, plus a small vertical offset.
                        if (side > 0)
                            label.style.left = dotRight;
                        else
                            label.style.left = -(dotRight + label.resolvedStyle.width);

                        label.style.top = yOffset;

                        // Vertically center around the pivot line.
                        label.style.translate = new Translate(0, -label.resolvedStyle.height * 0.5f, 0);
                    });

                    // If we didn’t successfully reserve a spot (overlap avoidance), hide it.
                    // We detect this by checking whether this label’s estimated rect exists in the occupied list.
                    // (Conservative: if it overlaps now, hide it.)
                    if (!_hideMarkerLabels)
                    {
                        var est = EstimateLabelRect(local, labelText);
                        if (IntersectsAny(est))
                            label.style.display = DisplayStyle.None;
                    }
                }

                // Anchor at the projected coordinate (this element becomes a 0,0 "pivot")
                marker.style.left = local.x;
                marker.style.top = local.y;

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
                    dot.style.translate = new Translate(
                        -dot.resolvedStyle.width * 0.5f,
                        -dot.resolvedStyle.height * 0.5f,
                        0
                    );
                });

                // Position label to the right of the dot, vertically centered on the pivot
                label.style.position = Position.Absolute;
                label.style.left = 10;   // small default; refined after layout below
                label.style.top = 0;

                label.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    // place label to the right of the dot with padding
                    float xPad = 6f;
                    float dotRight = (dot.resolvedStyle.width * 0.5f) + xPad;
                    label.style.left = dotRight;

                    // vertically center label on the pivot
                    label.style.translate = new Translate(0, -label.resolvedStyle.height * 0.5f, 0);
                });

                marker.pickingMode = PickingMode.Position;

                string markerId = m.id; // capture for closure safety
                marker.RegisterCallback<PointerDownEvent>(evt =>
                {
                    _selectedMarkerId = markerId;
                    _selectedPolylineId = null;
                    _polyLayer?.SetSelected(null);

                    MarkerSelected?.Invoke(m);

                    evt.StopPropagation(); // prevent pan handler from stealing this click
                });

                _markerHost.Add(marker);
            }

            // Now that marker labels have reserved space, place mid-polyline labels with overlap avoidance.
            RebuildPolylineLabelsBasic(occupiedLabelRects);

        }

        void RebuildPolylineLabelsBasic(List<Rect> occupiedLabelRects)
        {
            var lines = _mapData != null ? _mapData.Polylines : null;
            if (lines == null || lines.Count == 0) return;

            // Only show these labels when zoomed in enough to reduce clutter.
            if (_zoom < 0.65f) return;

            // Track placed rects (seed with marker-label occupied rects).
            var placed = new List<Rect>(64);
            if (occupiedLabelRects != null && occupiedLabelRects.Count > 0)
                placed.AddRange(occupiedLabelRects);

            for (int i = 0; i < lines.Count; i++)
            {
                var p = lines[i];
                if (!p.IsValid) continue;

                // Only label runs/lifts here.
                if (p.lineType != MapLineType.SkiRun && p.lineType != MapLineType.SkiLift)
                    continue;

                if (p.pointsWorldXZ == null || p.pointsWorldXZ.Count < 2)
                    continue;

                // Project polyline into content-local space and compute midpoint along length.
                float total = 0f;
                Vector2 prev;
                if (!TryProjectWorldXZToLocal(p.pointsWorldXZ[0], out prev))
                    continue;

                // quick length pass
                for (int k = 1; k < p.pointsWorldXZ.Count; k++)
                {
                    if (!TryProjectWorldXZToLocal(p.pointsWorldXZ[k], out var cur))
                        continue;
                    total += Vector2.Distance(prev, cur);
                    prev = cur;
                }

                if (total <= 0.001f) continue;

                // Require a minimum on-screen length
                if (total * _zoom < 140f) continue;

                float half = total * 0.5f;
                float accum = 0f;

                if (!TryProjectWorldXZToLocal(p.pointsWorldXZ[0], out var a0))
                    continue;

                Vector2 mid = a0;
                Vector2 tangent = Vector2.right;

                for (int k = 1; k < p.pointsWorldXZ.Count; k++)
                {
                    if (!TryProjectWorldXZToLocal(p.pointsWorldXZ[k], out var a1))
                        continue;

                    float seg = Vector2.Distance(a0, a1);
                    if (seg > 0.0001f && accum + seg >= half)
                    {
                        float t = (half - accum) / seg;
                        mid = Vector2.Lerp(a0, a1, t);
                        tangent = (a1 - a0).normalized;
                        break;
                    }

                    accum += seg;
                    a0 = a1;
                }

                string labelText = string.IsNullOrWhiteSpace(p.displayName) ? p.id : p.displayName;

                // Approximate label size for overlap testing (cheap + stable)
                float approxW = 12f + (labelText.Length * 7.2f);
                float approxH = 20f;

                Vector2 n = new Vector2(-tangent.y, tangent.x);
                Vector2 tdir = tangent;

                // More robust candidate offsets: normal, tangent, and diagonals.
                Vector2[] offsets =
                {
            Vector2.zero,

            n * 18f,  n * -18f,
            tdir * 18f, tdir * -18f,

            n * 34f,  n * -34f,
            tdir * 34f, tdir * -34f,

            (n + tdir).normalized * 26f,
            (n - tdir).normalized * 26f,
            (-n + tdir).normalized * 26f,
            (-n - tdir).normalized * 26f,
        };

                bool placedOk = false;
                Vector2 finalPos = mid;
                Rect finalRect = default;

                for (int o = 0; o < offsets.Length; o++)
                {
                    Vector2 pos = mid + offsets[o];
                    Rect r = new Rect(pos.x - approxW * 0.5f, pos.y - approxH * 0.5f, approxW, approxH);

                    bool overlaps = false;
                    for (int rr = 0; rr < placed.Count; rr++)
                    {
                        if (placed[rr].Overlaps(r))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        placed.Add(r);
                        finalPos = pos;
                        finalRect = r;
                        placedOk = true;
                        break;
                    }
                }

                if (!placedOk)
                    continue;

                // Reserve for anything that comes after (debug/extra labels, etc.)
                occupiedLabelRects?.Add(finalRect);

                var label = new Label(labelText);
                label.AddToClassList("map-polyline-label");
                label.style.position = Position.Absolute;
                label.style.left = finalPos.x;
                label.style.top = finalPos.y;

                // center around anchor
                label.style.translate = new Translate(-approxW * 0.5f, -approxH * 0.5f, 0);

                // Click selects the polyline (so label + line behave the same)
                label.pickingMode = PickingMode.Position;
                var capture = p; // closure safety
                label.RegisterCallback<PointerDownEvent>(evt =>
                {
                    _selectedPolylineId = capture.id;
                    _selectedMarkerId = null;

                    _polyLayer?.SetSelected(capture.id);
                    PolylineSelected?.Invoke(capture);

                    evt.StopPropagation();
                });

                _markerHost.Add(label);
            }
        }

        private bool TryProjectWorldXZToLocal(Vector2 worldXZ, out Vector2 local)
        {
            local = default;
            if (_mapData == null) return false;

            // Convert XZ -> UV, apply inset, then to content local.
            Vector2 uv = _mapData.WorldXZToMapUV(worldXZ);
            Vector2 uvInset = ApplyBackgroundInset(uv);
            local = new Vector2(uvInset.x * _contentSize.x, (1f - uvInset.y) * _contentSize.y);
            return true;
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

        private void ResetViewToFit()
        {
            if (_viewport == null || _content == null) return;

            float vw = _viewport.resolvedStyle.width;
            float vh = _viewport.resolvedStyle.height;

            if (vw <= 1f || vh <= 1f) return;

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

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_viewport == null || _content == null) return;
            if (evt.button != 0) return;

            // Minimap can lock panning entirely.
            if (_lockPan)
                return;

            // If clicking a marker/player element, let it handle the event (no pan capture).
            if (evt.target is VisualElement veTarget)
            {
                if (veTarget.ClassListContains("map-marker") || veTarget.ClassListContains("map-player-marker"))
                    return;
            }

            _pointerDown = true;
            _didDrag = false;
            _pointerDownPosViewport = new Vector2(evt.position.x, evt.position.y);

            _dragging = true;
            _activePointerId = evt.pointerId;
            _dragStartPointer = _pointerDownPosViewport;
            _dragStartPan = _pan;

            _viewport.CapturePointer(_activePointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            if (evt.pointerId != _activePointerId) return;

            Vector2 pos = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = pos - _dragStartPointer;

            if (!_didDrag && delta.magnitude > ClickDragThresholdPx)
                _didDrag = true;

            _pan = _dragStartPan + delta;

            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnPointerUp(EventBase evtBase)
        {
            if (!_dragging) return;

            var evt = evtBase as IPointerEvent;
            if (evt != null && evt.pointerId != _activePointerId) return;

            // Release pan capture
            _dragging = false;
            if (_viewport != null && _activePointerId != -1 && _viewport.HasPointerCapture(_activePointerId))
                _viewport.ReleasePointer(_activePointerId);

            _activePointerId = -1;

            // If this was a click (not a drag), attempt to pick a polyline.
            if (_pointerDown && !_didDrag && evtBase is PointerUpEvent pu)
            {
                // In minimap mode we can suppress selection entirely.
                if (_suppressSelection)
                {
                    _pointerDown = false;
                    evtBase.StopPropagation();
                    return;
                }

                Vector2 viewportPos = new Vector2(pu.position.x, pu.position.y);
                Vector2 contentLocal = (viewportPos - _pan) / Mathf.Max(0.0001f, _zoom);

                // Try pick polyline (runs/lifts).
                // Convert an on-screen pixel tolerance into content-space by dividing by zoom.
                const float hitDistScreenPx = 16f; // more forgiving, especially when zoomed out
                float pickDistContent = hitDistScreenPx / Mathf.Max(0.0001f, _zoom);

                if (_polyLayer != null && _polyLayer.TryPick(contentLocal, maxDistancePx: pickDistContent, out var picked))
                {
                    _selectedPolylineId = picked.id;
                    _selectedMarkerId = null;

                    _polyLayer.SetSelected(picked.id);
                    PolylineSelected?.Invoke(picked);
                    return;
                }
                else
                {
                    // Clicked empty space: clear selection
                    _selectedPolylineId = null;
                    _selectedMarkerId = null;
                    _polyLayer?.SetSelected(null);
                    SelectionCleared?.Invoke();
                }
            }

            _pointerDown = false;
            evtBase.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (_viewport == null || _content == null) return;

            if (!_allowZoom)
            {
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            float vw = _viewport.resolvedStyle.width;
            float vh = _viewport.resolvedStyle.height;
            if (vw <= 1f || vh <= 1f) return;

            float prevZoom = _zoom;

            // Wheel delta: positive is usually scroll down; invert for natural zoom
            float zoomFactor = Mathf.Pow(1.12f, -evt.delta.y * 0.1f);
            _zoom = Mathf.Clamp(_zoom * zoomFactor, 0.15f, 10f);

            // Zoom around cursor position (keep point under cursor stable)
            Vector2 cursor = evt.localMousePosition;

            Vector2 contentBefore = (cursor - _pan) / prevZoom;
            _pan = cursor - contentBefore * _zoom;

            ApplyTransform();
            // Keep polyline widths stable in screen space.
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
                _playerMarker.style.display = _trackPlayerMarker ? DisplayStyle.Flex : DisplayStyle.None;

            if (_trailLayer != null)
                _trailLayer.SetEnabled(_trackPlayerTrail);
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

            // Compute once; follow-centering should not be gated on marker visibility.
            bool havePlayerLocal = TryGetPlayerLocal(out Vector2 playerLocal);

            // Update player marker every tick (cheap).
            if (_trackPlayerMarker && _playerMarker != null && havePlayerLocal)
            {
                // Center the marker on the location.
                float w = _playerMarker.resolvedStyle.width > 0 ? _playerMarker.resolvedStyle.width : 12f;
                float h = _playerMarker.resolvedStyle.height > 0 ? _playerMarker.resolvedStyle.height : 12f;

                _playerMarker.style.left = playerLocal.x - w * 0.5f;
                _playerMarker.style.top = playerLocal.y - h * 0.5f;
            }

            // Follow-player mode should always re-center (watch/tile minimaps rely on this).
            if (_minimapFollowPlayer && havePlayerLocal)
                CenterViewOnContentPoint(playerLocal);

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

        private bool UpdatePlayerMarker(out Vector2 playerLocal)
        {
            playerLocal = default;

            if (_playerTransform == null || _playerMarker == null) return false;

            if (!TryProjectWorldToUV(_playerTransform.position, out Vector2 uv))
                return false;

            playerLocal = UVToLocal(uv);

            // Center the marker on the location.
            float w = _playerMarker.resolvedStyle.width > 0 ? _playerMarker.resolvedStyle.width : 12f;
            float h = _playerMarker.resolvedStyle.height > 0 ? _playerMarker.resolvedStyle.height : 12f;

            _playerMarker.style.left = playerLocal.x - w * 0.5f;
            _playerMarker.style.top = playerLocal.y - h * 0.5f;
            return true;
        }

        private void CenterViewOnContentPoint(Vector2 contentLocal)
        {
            // In this implementation, _viewport is the only stable "view rect" we can trust.
            // Embedded minimaps still provide MapViewport (it just lives under a different root).
            if (_viewport == null) return;

            float vw = _viewport.resolvedStyle.width;
            float vh = _viewport.resolvedStyle.height;

            // If layout hasn't resolved yet, we can't center reliably this frame.
            // Tick() will call again once geometry exists (and Bind() already registers a GeometryChangedEvent -> Refresh()).
            if (vw <= 1f || vh <= 1f) return;

            Vector2 center = new Vector2(vw * 0.5f, vh * 0.5f);

            // Pan so the content point lands at the viewport center.
            _pan = center - contentLocal * _zoom;

            // Clamp pan so we don't drift past edges.
            float scaledW = _contentSize.x * _zoom;
            float scaledH = _contentSize.y * _zoom;

            float minX, maxX, minY, maxY;

            // If content is smaller than viewport, keep it centered.
            if (scaledW <= vw)
                minX = maxX = (vw - scaledW) * 0.5f;
            else
            {
                minX = vw - scaledW;
                maxX = 0f;
            }

            if (scaledH <= vh)
                minY = maxY = (vh - scaledH) * 0.5f;
            else
            {
                minY = vh - scaledH;
                maxY = 0f;
            }

            _pan.x = Mathf.Clamp(_pan.x, minX, maxX);
            _pan.y = Mathf.Clamp(_pan.y, minY, maxY);

            ApplyTransform();
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
        /// Painter2D polyline renderer + hit testing.
        /// </summary>
        private sealed class MapPolylineLayer : VisualElement
        {
            private MapData _data;
            private Vector2 _contentSize;
            private Camera _cam;
            private float _zoom = 1f;

            private string _selectedId;

            private struct CachedLine
            {
                public MapPolyline src;
                public List<Vector2> localPts;
                public Rect bounds;
            }

            private readonly List<CachedLine> _cache = new();

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

            public bool TryPick(Vector2 contentLocal, float maxDistancePx, out MapPolyline picked)
            {
                picked = default;

                float bestSqr = maxDistancePx * maxDistancePx;
                int bestIdx = -1;

                for (int i = 0; i < _cache.Count; i++)
                {
                    var c = _cache[i];

                    // quick bounds reject (expand bounds)
                    Rect b = c.bounds;
                    b.xMin -= maxDistancePx;
                    b.yMin -= maxDistancePx;
                    b.xMax += maxDistancePx;
                    b.yMax += maxDistancePx;

                    if (!b.Contains(contentLocal))
                        continue;

                    var pts = c.localPts;
                    if (pts == null || pts.Count < 2) continue;

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

            public bool TryGetPointAlong(string id, float t01, out Vector2 local)
            {
                local = default;
                if (string.IsNullOrEmpty(id)) return false;

                t01 = Mathf.Clamp01(t01);

                for (int i = 0; i < _cache.Count; i++)
                {
                    var c = _cache[i];
                    if (!string.Equals(c.src.id, id, StringComparison.Ordinal))
                        continue;

                    var pts = c.localPts;
                    if (pts == null || pts.Count < 2) return false;

                    // total length
                    float total = 0f;
                    for (int k = 0; k < pts.Count - 1; k++)
                        total += Vector2.Distance(pts[k], pts[k + 1]);

                    if (total <= 0.0001f)
                    {
                        local = pts[0];
                        return true;
                    }

                    float target = total * t01;
                    float accum = 0f;

                    for (int k = 0; k < pts.Count - 1; k++)
                    {
                        float seg = Vector2.Distance(pts[k], pts[k + 1]);
                        if (seg <= 0.00001f) continue;

                        if (accum + seg >= target)
                        {
                            float u = (target - accum) / seg;
                            local = Vector2.Lerp(pts[k], pts[k + 1], u);
                            return true;
                        }

                        accum += seg;
                    }

                    local = pts[pts.Count - 1];
                    return true;
                }

                return false;
            }

            private void OnGenerate(MeshGenerationContext ctx)
            {
                if (_data == null) return;

                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;

                for (int i = 0; i < _cache.Count; i++)
                {
                    var c = _cache[i];
                    var line = c.src;
                    var pts = c.localPts;
                    if (pts == null || pts.Count < 2) continue;

                    // Stable screen-space width: divide by zoom because MapContent is scaled.
                    float screenW = (line.lineType == MapLineType.SkiLift) ? 2.1f : 2.8f;
                    float w = screenW / _zoom;

                    Color baseColor = (line.color.a <= 0.001f) ? new Color(1f, 1f, 1f, 0.70f) : line.color;
                    bool selected = (!string.IsNullOrEmpty(_selectedId) && line.id == _selectedId);

                    // Outline for legibility
                    p.strokeColor = new Color(0f, 0f, 0f, selected ? 0.55f : 0.35f);
                    p.lineWidth = (screenW + (selected ? 3.0f : 2.0f)) / _zoom;
                    Stroke(p, pts);

                    // Main stroke
                    p.strokeColor = selected ? new Color(baseColor.r, baseColor.g, baseColor.b, 1f) : baseColor;
                    p.lineWidth = selected ? (w * 1.35f) : w;
                    Stroke(p, pts);
                }
            }

            private void RebuildCache()
            {
                _cache.Clear();
                if (_data == null) return;
                var lines = _data.Polylines;
                if (lines == null) return;

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (!line.IsValid) continue;

                    var localPts = new List<Vector2>(line.pointsWorldXZ.Count);
                    Rect bounds = new Rect(float.PositiveInfinity, float.PositiveInfinity, 0, 0);

                    for (int k = 0; k < line.pointsWorldXZ.Count; k++)
                    {
                        if (!TryProjectWorldXZToUV(line.pointsWorldXZ[k], out Vector2 uv))
                            continue;

                        uv = ApplyInset(uv, _data.BackgroundUvMin, _data.BackgroundUvMax);
                        Vector2 local = new Vector2(uv.x * _contentSize.x, (1f - uv.y) * _contentSize.y);
                        localPts.Add(local);

                        if (bounds.xMin == float.PositiveInfinity)
                        {
                            bounds = new Rect(local.x, local.y, 0, 0);
                        }
                        else
                        {
                            bounds.xMin = Mathf.Min(bounds.xMin, local.x);
                            bounds.yMin = Mathf.Min(bounds.yMin, local.y);
                            bounds.xMax = Mathf.Max(bounds.xMax, local.x);
                            bounds.yMax = Mathf.Max(bounds.yMax, local.y);
                        }
                    }

                    if (localPts.Count < 2)
                        continue;

                    _cache.Add(new CachedLine { src = line, localPts = localPts, bounds = bounds });
                }
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
                    if (!TryProjectWorldXZToUV(_worldTrail[i], out Vector2 uv))
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
                p.strokeColor = new Color(0.25f, 0.9f, 1f, 0.85f);
                p.lineWidth = w;
                Stroke(p, _localPts);
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

    }
}
