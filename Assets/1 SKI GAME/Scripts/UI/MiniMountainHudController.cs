using System;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.POI;
using SkiGame.Runs;
using SkiGame.UI;
using TimeWeather;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkiGame.Progression
{
    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class MiniMountainHudController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;

        [Header("Map")]
        [SerializeField] private MapData mapData;
        [SerializeField] private Camera mapReferenceCamera;
        [SerializeField] private MapUIStyleSettings mapStyle;

        [Header("References")]
        [SerializeField] private SkiController skiController;
        [SerializeField] private RunProgressTracker runProgressTracker;
        [SerializeField] private PlayerStatsManager statsManager;
        [SerializeField] private TimeController timeController;
        [SerializeField] private WeatherController weatherController;
        [SerializeField] private MountainHudOverlayController overlayController;

        [Header("Refresh")]
        [SerializeField, Range(0.1f, 1.0f)] private float refreshIntervalSeconds = 0.2f;

        private PointOfInterestRegistry _appliedPoiRegistry;
        private MapUIStyleSettings _appliedMapStyle;

        private VisualElement _root;
        private PhoneMapPageUI _miniMapUI;

        private Label _lblMiniTime;
        private Label _lblMiniWeather;
        private Label _lblRunTitle;
        private Label _lblRunBody;
        private Label _lblStatSpeed;
        private Label _lblStatDistance;
        private Label _lblStatRuns;
        private Label _lblStatVertical;
        private Button _btnOpenOverlay;

        private float _nextRefreshTime;
        private bool _visible = true;
        private Rigidbody _rb;
        private bool _miniMapGeometryHooked;

        private bool _pendingInitializeAfterPreview;

        private void Reset()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (skiController == null) skiController = FindObjectOfType<SkiController>();
            if (runProgressTracker == null) runProgressTracker = FindObjectOfType<RunProgressTracker>();
            if (statsManager == null) statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();
            if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (weatherController == null) weatherController = FindObjectOfType<WeatherController>();
            if (overlayController == null) overlayController = FindObjectOfType<MountainHudOverlayController>();
        }

        private void Awake()
        {
            ResolveBootstrapReferences();
        }

        private void ResolveBootstrapReferences()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (mapData == null)
                mapData = AutoFindMapData();

            if (mapReferenceCamera == null && mapData != null && mapData.PreferCameraProjection)
            {
                Debug.LogWarning(
                    "[MiniMountainHudController] MapData prefers camera projection, but no explicit mapReferenceCamera is assigned. " +
                    "Falling back to baked MapProjection is recommended for stable alignment.");
            }
        }

        private void OnEnable()
        {
            if (RuntimeSceneLoadContext.IsMenuBackgroundPreview)
            {
                _pendingInitializeAfterPreview = true;
                RuntimeSceneLoadContext.MenuBackgroundPreviewEnded += HandleMenuPreviewEnded;
                return;
            }

            ResolveReferences();
            TryInitializeOrRetry();
        }

        private void HandleMenuPreviewEnded()
        {
            RuntimeSceneLoadContext.MenuBackgroundPreviewEnded -= HandleMenuPreviewEnded;

            if (!this || !isActiveAndEnabled)
                return;

            if (!_pendingInitializeAfterPreview)
                return;

            _pendingInitializeAfterPreview = false;
            ResolveReferences();
            TryInitializeOrRetry();
        }

        private void TryInitializeOrRetry()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null)
            {
                Debug.LogWarning("[MiniMountainHudController] Missing UIDocument.");
                return;
            }

            _root = document.rootVisualElement;
            if (_root == null)
            {
                // UI Toolkit root not ready yet; retry next frame instead of disabling forever.
                StartCoroutine(RetryInitializeNextFrame());
                return;
            }

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            BindUI();
            BindMap();
            SetVisible(true);
            RefreshAll(force: true);
        }

        private System.Collections.IEnumerator RetryInitializeNextFrame()
        {
            yield return null;

            if (!this || !isActiveAndEnabled)
                yield break;

            ResolveReferences();

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[MiniMountainHudController] UIDocument root still not ready after retry.");
                yield break;
            }

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            BindUI();
            BindMap();
            SetVisible(true);
            RefreshAll(force: true);
        }
        private void OnDisable()
        {
            GameCursorService.Release(this);

            RuntimeSceneLoadContext.MenuBackgroundPreviewEnded -= HandleMenuPreviewEnded;
            _pendingInitializeAfterPreview = false;
        }

        private void Update()
        {
            if (!_visible)
                return;

            float dt = Time.unscaledDeltaTime;
            _miniMapUI?.Tick(dt);

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshAll(force: false);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                GameCursorService.Request(this, GameCursorMode.HiddenLocked, priority: 100);
                RefreshAll(force: true);
            }
            else
            {
                GameCursorService.Release(this);
            }
        }

        private void ResolveReferences()
        {
            ResolveBootstrapReferences();

            if (skiController == null) skiController = FindObjectOfType<SkiController>();
            if (runProgressTracker == null) runProgressTracker = FindObjectOfType<RunProgressTracker>();
            if (statsManager == null) statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();
            if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (weatherController == null) weatherController = FindObjectOfType<WeatherController>();
            if (overlayController == null) overlayController = FindObjectOfType<MountainHudOverlayController>();

            _rb = skiController != null ? skiController.GetComponent<Rigidbody>() : null;

            if (_miniMapUI != null)
            {
                if (skiController != null)
                    _miniMapUI.SetPlayer(skiController.transform);

                _miniMapUI.SetPlayerTracking(showMarker: true, drawTrail: true);
            }
        }

        private void BindUI()
        {
            _lblMiniTime = _root.Q<Label>("Lbl_MiniTime");
            _lblMiniWeather = _root.Q<Label>("Lbl_MiniWeather");
            _lblRunTitle = _root.Q<Label>("Lbl_MiniRunTitle");
            _lblRunBody = _root.Q<Label>("Lbl_MiniRunBody");
            _lblStatSpeed = _root.Q<Label>("Lbl_MiniStatSpeed");
            _lblStatDistance = _root.Q<Label>("Lbl_MiniStatDistance");
            _lblStatRuns = _root.Q<Label>("Lbl_MiniStatRuns");
            _lblStatVertical = _root.Q<Label>("Lbl_MiniStatVertical");
            _btnOpenOverlay = _root.Q<Button>("Btn_OpenOverlay");

            if (_btnOpenOverlay != null)
            {
                _btnOpenOverlay.clicked += () =>
                {
                    if (overlayController != null)
                        overlayController.SetOverlayOpen(true);
                };
            }
        }

        private void BindMap()
        {
            if (mapData == null)
            {
                Debug.LogWarning("[MiniMountainHudController] Missing MapData.");
                return;
            }

            var host = _root.Q<VisualElement>("MiniMapHost");
            if (host == null)
            {
                Debug.LogWarning("[MiniMountainHudController] MiniMapHost placeholder not found.");
                return;
            }

            host.Clear();
            //host.style.width = 250f;
            //host.style.height = 250f;
            host.style.minWidth = 250f;
            host.style.minHeight = 250f;
            host.style.overflow = Overflow.Hidden;

            var miniRoot = BuildLegacyEmbeddedMiniMapRoot("MiniLegacyMapRoot");
            //miniRoot.style.width = 250f;
            //miniRoot.style.height = 250f;
            miniRoot.style.minWidth = 250f;
            miniRoot.style.minHeight = 250f;

            host.Add(miniRoot);

            _miniMapUI = new PhoneMapPageUI();
            _miniMapUI.Bind(miniRoot, mapData, mapReferenceCamera);

            if (skiController != null)
                _miniMapUI.SetPlayer(skiController.transform);

            _miniMapUI.SetPlayerTracking(showMarker: true, drawTrail: true);
            _miniMapUI.SetTrailSamplingEnabled(false);
            _miniMapUI.SetMinimapMode(
                enabled: true,
                followPlayer: true,
                lockPan: true,
                allowZoom: false,
                suppressSelection: true,
                hideMarkerLabels: true);

            miniRoot.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                _miniMapUI?.Refresh();
                _miniMapUI?.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.25f);
            });

            RefreshMapDependencies(forceRefresh: true);

            _miniMapUI.Refresh();
            _miniMapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.25f);
        }

        private void RefreshMapDependencies(bool forceRefresh)
        {
            if (_miniMapUI == null)
                return;

            if (mapStyle == null)
                mapStyle = AutoFindMapStyle();

            if (mapStyle != null && (_appliedMapStyle != mapStyle || forceRefresh))
            {
                _appliedMapStyle = mapStyle;
                _miniMapUI.ApplyStyle(mapStyle);
            }

            var reg = PointOfInterestRegistry.Instance;
            if (reg != null)
            {
                if (_appliedPoiRegistry != reg || forceRefresh)
                {
                    _appliedPoiRegistry = reg;
                    reg.Refresh();
                    _miniMapUI.SetPOIRegistry(reg);
                    _miniMapUI.Refresh();
                }
            }
        }

        private void RefreshAll(bool force)
        {
            ResolveReferences();
            RefreshMapDependencies(forceRefresh: false);
            RefreshTimeWeather();
            RefreshRunPanel();
            RefreshStatsPanel();

            if (_miniMapUI != null)
            {
                if (force)
                {
                    _miniMapUI.Refresh();
                    _miniMapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.25f);
                }
                else
                {
                    _miniMapUI.RequestCenterOnPlayer(keepZoom: true, minZoom: 1.25f);
                }
            }
        }

        private void RefreshTimeWeather()
        {
            if (_lblMiniTime != null)
                _lblMiniTime.text = FormatTime();

            if (_lblMiniWeather != null)
                _lblMiniWeather.text = FormatWeather();
        }

        private void RefreshRunPanel()
        {
            if (_lblRunTitle == null || _lblRunBody == null)
                return;

            if (runProgressTracker != null && runProgressTracker.TryGetActiveProgress(out var active))
            {
                _lblRunTitle.text = active.runName;
                _lblRunBody.text =
                    $"{Mathf.RoundToInt(active.completion01 * 100f)}% � {FormatSeconds(active.elapsedSeconds)}\n" +
                    $"{FormatSpeed(active.topSpeedMps)} � Stacks {active.stacks}";
                return;
            }

            string nearestRun = FindNearestRunName();
            _lblRunTitle.text = "No Active Run";
            _lblRunBody.text = string.IsNullOrWhiteSpace(nearestRun)
                ? "Explore the mountain"
                : $"Nearest: {nearestRun}";
        }

        private void RefreshStatsPanel()
        {
            var profile = statsManager != null ? statsManager.Profile : null;

            float currentSpeedMps = 0f;
            if (_rb != null)
            {
                Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
                currentSpeedMps = planar.magnitude;
            }

            if (_lblStatSpeed != null)
                _lblStatSpeed.text = $"Speed\n{FormatSpeed(currentSpeedMps)}";

            if (profile == null)
            {
                if (_lblStatDistance != null) _lblStatDistance.text = "Distance\n--";
                if (_lblStatRuns != null) _lblStatRuns.text = "Runs\n--";
                if (_lblStatVertical != null) _lblStatVertical.text = "Vertical\n--";
                return;
            }

            if (_lblStatDistance != null)
                _lblStatDistance.text = $"Distance\n{FormatMeters(profile.session.distanceMeters)}";

            if (_lblStatRuns != null)
                _lblStatRuns.text = $"Runs\n{profile.session.runsCompleted}";

            if (_lblStatVertical != null)
                _lblStatVertical.text = $"Vertical\n{FormatMeters(profile.session.verticalDescentMeters)}";
        }

        private string FindNearestRunName()
        {
            if (skiController == null || PointOfInterestRegistry.Instance == null)
                return null;

            float bestSq = float.MaxValue;
            string bestName = null;
            Vector3 p = skiController.transform.position;

            var list = PointOfInterestRegistry.Instance.Current;
            for (int i = 0; i < list.Count; i++)
            {
                var poi = list[i];
                if (poi.type != POIType.SkiRun) continue;

                float d = (poi.position - p).sqrMagnitude;
                if (d < bestSq)
                {
                    bestSq = d;
                    bestName = string.IsNullOrWhiteSpace(poi.displayName) ? poi.id : poi.displayName;
                }
            }

            if (string.IsNullOrWhiteSpace(bestName))
                return null;

            return $"{bestName} � {Mathf.Sqrt(bestSq):0} m";
        }

        private string FormatTime()
        {
            if (timeController == null)
                return "--:--";

            return $"{Mathf.Clamp(timeController.timeHours, 0, 23):00}:{Mathf.Clamp((int)timeController.timeMinutes, 0, 59):00}";
        }

        private string FormatWeather()
        {
            if (weatherController == null || weatherController.currentWeatherPreset == null)
                return "Weather";

            string cond = weatherController.currentWeatherPreset.weatherCondition;
            return string.IsNullOrWhiteSpace(cond) ? "Weather" : cond;
        }

        private static string FormatSeconds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.RoundToInt(seconds);
            int mins = total / 60;
            int secs = total % 60;
            return $"{mins:00}:{secs:00}";
        }

        private static string FormatMeters(float meters)
        {
            if (meters >= 1000f)
                return $"{meters / 1000f:0.0} km";
            return $"{meters:0} m";
        }

        private static string FormatSpeed(float mps)
        {
            return $"{(mps * 3.6f):0} km/h";
        }

        private static MapData AutoFindMapData()
        {
            var all = Resources.FindObjectsOfTypeAll<MapData>();
            if (all == null || all.Length == 0)
                return null;

            MapData fallback = null;

            for (int i = 0; i < all.Length; i++)
            {
                var data = all[i];
                if (data == null) continue;

                if (fallback == null)
                    fallback = data;

                if (string.Equals(data.name, "MapData", StringComparison.OrdinalIgnoreCase))
                    return data;
            }

            return fallback;
        }

        private static Camera AutoFindMapReferenceCamera()
        {
            var cameras = Resources.FindObjectsOfTypeAll<Camera>();
            if (cameras == null || cameras.Length == 0)
                return null;

            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null) continue;
                if (!cam.gameObject.scene.IsValid()) continue;

                string n = cam.name ?? string.Empty;
                if (n.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return cam;
                }
            }

            return null;
        }

        private static VisualElement BuildLegacyEmbeddedMiniMapRoot(string rootName)
        {
            var root = new VisualElement { name = rootName };
            root.style.position = Position.Relative;
            root.style.flexGrow = 1;
            root.style.overflow = Overflow.Hidden;
            root.style.borderTopLeftRadius = 10;
            root.style.borderTopRightRadius = 10;
            root.style.borderBottomLeftRadius = 10;
            root.style.borderBottomRightRadius = 10;

            var viewport = new VisualElement { name = "MapViewport" };
            viewport.style.position = Position.Absolute;
            viewport.style.left = 0;
            viewport.style.top = 0;
            viewport.style.right = 0;
            viewport.style.bottom = 0;
            viewport.style.overflow = Overflow.Hidden;

            var content = new VisualElement { name = "MapContent" };
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.top = 0;
            content.style.right = StyleKeyword.Auto;
            content.style.bottom = StyleKeyword.Auto;

            var bg = new VisualElement { name = "MapBackground" };
            var polys = new VisualElement { name = "MapPolylines" };
            var markers = new VisualElement { name = "MapMarkers" };

            bg.style.position = Position.Absolute;
            polys.style.position = Position.Absolute;
            markers.style.position = Position.Absolute;

            bg.style.left = polys.style.left = markers.style.left = 0;
            bg.style.top = polys.style.top = markers.style.top = 0;
            bg.style.right = polys.style.right = markers.style.right = StyleKeyword.Auto;
            bg.style.bottom = polys.style.bottom = markers.style.bottom = StyleKeyword.Auto;

            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;

            root.pickingMode = PickingMode.Ignore;
            viewport.pickingMode = PickingMode.Ignore;
            content.pickingMode = PickingMode.Ignore;
            bg.pickingMode = PickingMode.Ignore;
            polys.pickingMode = PickingMode.Ignore;
            markers.pickingMode = PickingMode.Ignore;

            content.Add(bg);
            content.Add(polys);
            content.Add(markers);
            viewport.Add(content);
            root.Add(viewport);

            return root;
        }

        private static MapUIStyleSettings AutoFindMapStyle()
        {
            var all = Resources.FindObjectsOfTypeAll<MapUIStyleSettings>();
            if (all == null || all.Length == 0)
                return null;

            MapUIStyleSettings fallback = null;

            for (int i = 0; i < all.Length; i++)
            {
                var style = all[i];
                if (style == null) continue;

                if (fallback == null)
                    fallback = style;

                if (string.Equals(style.name, "MapUIStyleSettings", StringComparison.OrdinalIgnoreCase))
                    return style;
            }

            return fallback;
        }
    }
}