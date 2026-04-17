using System;
using System.Collections.Generic;
using SkiGame.Activities;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.POI;
using SkiGame.Runs;
using SkiGame.UI;
using TimeWeather;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private InputPromptIconLibrary iconLibrary;

        [Header("Map")]
        [SerializeField] private MapData mapData;
        [SerializeField] private MapRegionSet regionSet;
        [SerializeField] private Camera mapReferenceCamera;
        [SerializeField] private MapUIStyleSettings mapStyle;

        [Header("References")]
        [SerializeField] private SkiController skiController;
        [SerializeField] private RunProgressTracker runProgressTracker;
        [SerializeField] private PlayerMapRegionTracker playerRegionTracker;
        [SerializeField] private PlayerStatsManager statsManager;
        [SerializeField] private TimeController timeController;
        [SerializeField] private WeatherController weatherController;
        [SerializeField] private MountainHudOverlayController overlayController;
        [SerializeField] private QuestDirector questDirector;
        [SerializeField] private ActivityHudPresenter activityHudPresenter;

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
        private VisualElement _questTrackerPanel;
        private VisualElement _questPrimaryCardHost;
        private VisualElement _questTrackerList;
        private Button _btnQuestSummaryToggle;
        private Label _lblQuestTrackerHint;

        private VisualElement _runBanner;
        private VisualElement _runBannerSegments;
        private Label _lblRunBannerName;
        private Label _lblRunBannerTime;
        private Label _lblRunBannerPercent;
        private VisualElement _miniRunPanel;

        private float _nextRefreshTime;
        private bool _visible = true;
        private Rigidbody _rb;
        private bool _miniMapGeometryHooked;

        private bool _pendingInitializeAfterPreview;
        private readonly System.Collections.Generic.List<Vector3> _raceCheckpointMarkerBuffer = new();
        private readonly System.Collections.Generic.List<QuestRuntimeState> _trackedQuestBuffer = new();
        private readonly System.Collections.Generic.List<QuestRuntimeState> _summaryQuestBuffer = new();
        private readonly System.Collections.Generic.List<VisualElement> _secondaryQuestCardRoots = new();

        private string _selectedQuestId;
        private bool _questSummaryExpanded = false;
        private VisualElement _questTrackerHintHost;

        private VisualElement _primaryQuestCardRoot;
        private string _cachedPrimaryStructureKey = string.Empty;
        private string _cachedSecondaryStructureKey = string.Empty;

        private VisualElement _activityModalBlocker;
        private VisualElement _activityFailModal;
        private Label _lblActivityFailTitle;
        private Label _lblActivityFailBody;
        private Button _btnActivityFailRetry;
        private Button _btnActivityFailQuit;

        private RaceCourseLine _activeFailedRace;
        private string _activeFailedRaceReason;
        private bool _showRaceFailModal;

        [Header("Quest HUD")]
        [SerializeField, Min(0.01f)] private float questScrollThreshold = 0.1f;

        private void Reset()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (skiController == null) skiController = FindObjectOfType<SkiController>();
            if (runProgressTracker == null) runProgressTracker = FindObjectOfType<RunProgressTracker>();
            if (playerRegionTracker == null) playerRegionTracker = FindObjectOfType<PlayerMapRegionTracker>();
            if (statsManager == null) statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();
            if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (weatherController == null) weatherController = FindObjectOfType<WeatherController>();
            if (overlayController == null) overlayController = FindObjectOfType<MountainHudOverlayController>();
            if (questDirector == null) questDirector = QuestDirector.Instance != null ? QuestDirector.Instance : FindObjectOfType<QuestDirector>();
            if (activityHudPresenter == null) activityHudPresenter = FindObjectOfType<ActivityHudPresenter>();
        }

        private void Awake()
        {
            ResolveBootstrapReferences();
        }

        private void ResolveBootstrapReferences()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (inputActions == null)
                inputActions = new InputSystem_Actions().asset;

            if (iconLibrary == null)
                iconLibrary = InputPromptResolver.LoadDefaultLibrary();

            if (mapData == null)
                mapData = AutoFindMapData();

            if (mapReferenceCamera == null && mapData != null && mapData.PreferCameraProjection)
            {
                Debug.LogWarning(
                    "[MiniMountainHudController] MapData prefers camera projection, but no explicit mapReferenceCamera is assigned. " +
                    "Falling back to baked MapProjection is recommended for stable alignment.");
            }
        }

        private static bool IsUsablePlayerSkiController(SkiController candidate)
        {
            if (candidate == null)
                return false;

            if (!candidate.isActiveAndEnabled)
                return false;

            var go = candidate.gameObject;
            if (!go.activeInHierarchy)
                return false;

            if (candidate.CompareTag("Player") || candidate.transform.root.CompareTag("Player"))
                return true;

            return false;
        }

        private SkiController FindBestPlayerSkiController()
        {
            SkiController taggedPlayer = null;
            var all = FindObjectsOfType<SkiController>(true);

            for (int i = 0; i < all.Length; i++)
            {
                var ski = all[i];
                if (!IsUsablePlayerSkiController(ski))
                    continue;

                if (ski.CompareTag("Player") || ski.transform.root.CompareTag("Player"))
                    return ski;

                if (taggedPlayer == null)
                    taggedPlayer = ski;
            }

            return taggedPlayer != null ? taggedPlayer : FindObjectOfType<SkiController>();
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

            HandleQuestHudInput();
            PollActivityFailureModal();

            float dt = Time.unscaledDeltaTime;
            _miniMapUI?.Tick(dt);

            SyncRaceCheckpointMarkersOnMiniMap();

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshAll(force: false);
        }

        private bool IsQuestListInteractionSuppressed()
        {
            if (activityHudPresenter == null)
                return false;

            return activityHudPresenter.TryGetCurrentSnapshot(out var snapshot) &&
                   snapshot != null &&
                   (!snapshot.allowQuestListExpansion || snapshot.suppressQuestList);
        }

        private void HandleQuestHudInput()
        {
            if (!_visible || questDirector == null)
                return;

            if (IsQuestListInteractionSuppressed())
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            questDirector.GetTrackedQuestStates(_trackedQuestBuffer);
            bool hasMultipleTracked = _trackedQuestBuffer.Count > 1;

            if (hasMultipleTracked && mouse.middleButton.wasPressedThisFrame)
                ToggleQuestSummary();

            float scrollY = mouse.scroll.ReadValue().y;
            if (hasMultipleTracked && scrollY >= questScrollThreshold)
                CycleFocusedQuest(-1);
            else if (hasMultipleTracked && scrollY <= -questScrollThreshold)
                CycleFocusedQuest(1);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                if (_showRaceFailModal)
                    GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 400);
                else
                    GameCursorService.Request(this, GameCursorMode.HiddenLocked, priority: 100);

                RefreshAll(force: true);
            }
            else
            {
                GameCursorService.Release(this);
            }
        }

        public string GetFocusedQuestId()
        {
            return _selectedQuestId;
        }

        public void SetFocusedQuest(string questId, bool expandSummary = false)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            _selectedQuestId = questId;

            if (expandSummary)
                _questSummaryExpanded = true;

            RefreshQuestTracker(forceStructureRebuild: true);
        }

        private void ResolveReferences()
        {
            ResolveBootstrapReferences();

            var resolvedPlayer = FindBestPlayerSkiController();
            if (resolvedPlayer != null && resolvedPlayer != skiController)
                skiController = resolvedPlayer;
            else if (skiController == null)
                skiController = FindObjectOfType<SkiController>();

            if (runProgressTracker == null) runProgressTracker = FindObjectOfType<RunProgressTracker>();
            if (playerRegionTracker == null) playerRegionTracker = FindObjectOfType<PlayerMapRegionTracker>();
            if (statsManager == null) statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();

            var resolvedRegionSet = ResolveRegionSet();
            if (playerRegionTracker != null)
                playerRegionTracker.Configure(
                    resolvedRegionSet,
                    skiController != null ? skiController.transform : null,
                    mapData);

            if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (weatherController == null) weatherController = FindObjectOfType<WeatherController>();
            if (overlayController == null) overlayController = FindObjectOfType<MountainHudOverlayController>();

            if (activityHudPresenter == null) activityHudPresenter = FindObjectOfType<ActivityHudPresenter>();

            _rb = skiController != null ? skiController.GetComponent<Rigidbody>() : null;

            if (_miniMapUI != null)
            {
                _miniMapUI.SetPlayer(skiController != null ? skiController.transform : null);
                _miniMapUI.SetPlayerTracking(showMarker: true, drawTrail: true);
            }

        }

        private void SyncRaceCheckpointMarkersOnMiniMap()
        {
            if (_miniMapUI == null)
                return;

            var mgr = MountainActivityManager.Instance;
            if (mgr != null &&
                mgr.ActiveKind == MountainActivityKind.Race &&
                mgr.ActiveSource is RaceCourseLine race &&
                race.GeneratedCheckpoints != null &&
                race.GeneratedCheckpoints.Count > 0)
            {
                _raceCheckpointMarkerBuffer.Clear();

                for (int i = 0; i < race.GeneratedCheckpoints.Count; i++)
                    _raceCheckpointMarkerBuffer.Add(race.GeneratedCheckpoints[i].worldPos);

                _miniMapUI.SetActivityCheckpointMarkers(_raceCheckpointMarkerBuffer, race.CurrentCheckpointIndex, hideBaseMarkers: true);
            }
            else
            {
                _miniMapUI.ClearActivityCheckpointMarkers();
            }
        }

        private MapRegionSet ResolveRegionSet()
        {
            if (regionSet != null)
            {
                if (regionSet.MapData == null && mapData != null)
                    regionSet.SetMapData(mapData);

                return regionSet;
            }

            var auto = AutoFindRegionSetForCurrentMap();
            if (auto != null && auto.MapData == null && mapData != null)
                auto.SetMapData(mapData);

            return auto;
        }

        private MapRegionSet AutoFindRegionSetForCurrentMap()
        {
            var all = Resources.FindObjectsOfTypeAll<MapRegionSet>();
            if (all == null || all.Length == 0)
                return null;

            MapRegionSet firstMatchingMap = null;
            MapRegionSet firstAny = null;

            bool havePlayerUv = false;
            Vector2 playerUv = Vector2.zero;

            if (mapData != null && skiController != null)
            {
                playerUv = mapData.WorldToMapUV(skiController.transform.position);
                havePlayerUv = true;
            }

            for (int i = 0; i < all.Length; i++)
            {
                var set = all[i];
                if (set == null)
                    continue;

                if (firstAny == null)
                    firstAny = set;

                if (mapData != null && set.MapData != mapData)
                    continue;

                if (firstMatchingMap == null)
                    firstMatchingMap = set;

                if (havePlayerUv)
                {
                    set.EnsureInitialized();
                    string regionId = MapRegionUtility.ResolveRegionId(set, playerUv);
                    if (!string.IsNullOrWhiteSpace(regionId))
                        return set;
                }
            }

            return firstMatchingMap != null ? firstMatchingMap : firstAny;
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
            _questTrackerPanel = _root.Q<VisualElement>("QuestTrackerPanel");
            _questPrimaryCardHost = _root.Q<VisualElement>("QuestPrimaryCardHost");
            _questTrackerList = _root.Q<VisualElement>("QuestTrackerList");
            _btnQuestSummaryToggle = _root.Q<Button>("Btn_QuestSummaryToggle");
            _lblQuestTrackerHint = _root.Q<Label>("Lbl_QuestTrackerHint");
            if (_lblQuestTrackerHint != null && _lblQuestTrackerHint.parent != null)
            {
                var parent = _lblQuestTrackerHint.parent;
                _questTrackerHintHost = InputPromptVisualBuilder.CreatePrompt(InputPromptTokens.Text(_lblQuestTrackerHint.text), "quest-footer-prompt");
                _questTrackerHintHost.name = "QuestTrackerHintHost";
                parent.Insert(parent.IndexOf(_lblQuestTrackerHint), _questTrackerHintHost);
                parent.Remove(_lblQuestTrackerHint);
                _lblQuestTrackerHint = null;
            }

            _miniRunPanel = _root.Q<VisualElement>("MiniRunPanel");

            _runBanner = _root.Q<VisualElement>("RunBanner");
            _runBannerSegments = _root.Q<VisualElement>("RunBannerSegments");
            _lblRunBannerName = _root.Q<Label>("Lbl_RunBannerName");
            _lblRunBannerTime = _root.Q<Label>("Lbl_RunBannerTime");
            _lblRunBannerPercent = _root.Q<Label>("Lbl_RunBannerPercent");

            if (_btnOpenOverlay != null)
            {
                _btnOpenOverlay.clicked += () =>
                {
                    if (overlayController != null)
                        overlayController.SetOverlayOpen(true);
                };
            }

            if (_miniRunPanel != null)
                _miniRunPanel.style.display = DisplayStyle.None;

            if (_btnQuestSummaryToggle != null)
            {
                _btnQuestSummaryToggle.clicked -= ToggleQuestSummary;
                _btnQuestSummaryToggle.clicked += ToggleQuestSummary;
            }

            EnsureActivityFailModal();
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

            var waypointManager = SkiGame.Navigation.MapWaypointManager.Instance != null
    ? SkiGame.Navigation.MapWaypointManager.Instance
    : SkiGame.Navigation.MapWaypointManager.EnsureInstance();

            _miniMapUI.SetWaypointManager(waypointManager);

            var resolvedRegionSet = ResolveRegionSet();
            if (resolvedRegionSet != null)
                _miniMapUI.SetRegionSet(resolvedRegionSet);

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
            RefreshRegionPanel();
            RefreshStatsPanel();
            RefreshQuestTracker(forceStructureRebuild: force);

            if (_miniMapUI != null)
            {
                _miniMapUI.SetPlayer(skiController != null ? skiController.transform : null);

                if (force)
                {
                    _miniMapUI.Refresh();
                    _miniMapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.25f);
                }
            }
        }

        private void RefreshQuestTracker(bool forceStructureRebuild = false)
        {
            if (_questTrackerPanel == null || _questPrimaryCardHost == null || _questTrackerList == null)
                return;

            ActivityHudSnapshot activitySnapshot = null;
            bool hasActivitySnapshot = activityHudPresenter != null &&
                                       activityHudPresenter.TryGetCurrentSnapshot(out activitySnapshot) &&
                                       activitySnapshot != null;

            _trackedQuestBuffer.Clear();
            if (questDirector != null)
                questDirector.GetTrackedQuestStates(_trackedQuestBuffer);

            QuestRuntimeState focusedState = _trackedQuestBuffer.Count > 0
                ? ResolveFocusedQuestState(_trackedQuestBuffer)
                : null;

            QuestDefinitionSO focusedDefinition = focusedState != null && questDirector != null
                ? questDirector.GetQuestDefinition(focusedState.questId)
                : null;

            bool canShowQuestPrimary = focusedState != null && focusedDefinition != null;
            bool canShowActivityPrimary = hasActivitySnapshot;

            if (!canShowQuestPrimary && !canShowActivityPrimary)
            {
                _questPrimaryCardHost.Clear();
                _questTrackerList.Clear();
                _secondaryQuestCardRoots.Clear();
                _primaryQuestCardRoot = null;
                _cachedPrimaryStructureKey = string.Empty;
                _cachedSecondaryStructureKey = string.Empty;

                _questTrackerPanel.style.display = DisplayStyle.None;
                if (_btnQuestSummaryToggle != null)
                    _btnQuestSummaryToggle.style.display = DisplayStyle.None;
                if (_questTrackerHintHost != null)
                    _questTrackerHintHost.style.display = DisplayStyle.None;
                return;
            }

            string primaryStructureKey;
            if (canShowActivityPrimary)
            {
                primaryStructureKey = BuildPrimaryStructureKeyForActivity(activitySnapshot);

                if (forceStructureRebuild || _primaryQuestCardRoot == null || _cachedPrimaryStructureKey != primaryStructureKey)
                {
                    _questPrimaryCardHost.Clear();
                    _primaryQuestCardRoot = BuildFocusedActivityCard(activitySnapshot);
                    _questPrimaryCardHost.Add(_primaryQuestCardRoot);
                    _cachedPrimaryStructureKey = primaryStructureKey;
                }

                UpdatePrimaryActivityCard(_primaryQuestCardRoot, activitySnapshot);
            }
            else
            {
                _selectedQuestId = focusedDefinition.SafeId;

                QuestObjectiveDefinition objectiveDefinition = null;
                QuestObjectiveRuntimeState objectiveState = null;

                bool hasObjective = TryResolveFocusedObjective(
                    focusedDefinition,
                    focusedState,
                    out objectiveDefinition,
                    out objectiveState);

                primaryStructureKey = hasObjective
                    ? BuildPrimaryStructureKeyForQuest(focusedDefinition, objectiveDefinition)
                    : $"quest-card|{focusedDefinition.SafeId}|fallback";

                if (forceStructureRebuild || _primaryQuestCardRoot == null || _cachedPrimaryStructureKey != primaryStructureKey)
                {
                    _questPrimaryCardHost.Clear();

                    _primaryQuestCardRoot = hasObjective
                        ? BuildFocusedQuestCard(focusedDefinition, focusedState, objectiveDefinition, objectiveState)
                        : BuildQuestTrackerCard(focusedDefinition, focusedState, isSelected: true, compact: false);

                    _questPrimaryCardHost.Add(_primaryQuestCardRoot);
                    _cachedPrimaryStructureKey = primaryStructureKey;
                }

                if (hasObjective)
                    UpdatePrimaryQuestCard(_primaryQuestCardRoot, focusedDefinition, objectiveDefinition, objectiveState);
            }

            _summaryQuestBuffer.Clear();

            if (_trackedQuestBuffer.Count > 0)
            {
                bool displaceFocusedToSecondary = hasActivitySnapshot &&
                                                 activitySnapshot.displaceTrackedQuestToSecondary &&
                                                 focusedState != null;

                if (displaceFocusedToSecondary)
                    _summaryQuestBuffer.Add(focusedState);

                for (int i = 0; i < _trackedQuestBuffer.Count; i++)
                {
                    var state = _trackedQuestBuffer[i];
                    if (state == null)
                        continue;

                    if (focusedState != null &&
                        string.Equals(state.questId, focusedState.questId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!displaceFocusedToSecondary)
                            continue;

                        if (displaceFocusedToSecondary)
                            continue;
                    }

                    _summaryQuestBuffer.Add(state);
                }
            }

            bool suppressQuestList = hasActivitySnapshot && activitySnapshot.suppressQuestList;
            bool allowQuestListExpansion = !hasActivitySnapshot || activitySnapshot.allowQuestListExpansion;
            bool hasSecondary = !suppressQuestList && _summaryQuestBuffer.Count > 0;

            if (!hasSecondary)
                _questSummaryExpanded = false;

            if (_btnQuestSummaryToggle != null)
            {
                _btnQuestSummaryToggle.style.display = hasSecondary && allowQuestListExpansion
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                if (hasSecondary)
                {
                    _btnQuestSummaryToggle.text = _questSummaryExpanded
                        ? $"Hide Other Quests ({_summaryQuestBuffer.Count})"
                        : $"+{_summaryQuestBuffer.Count} More Tracked Quest{(_summaryQuestBuffer.Count == 1 ? string.Empty : "s")}";
                }
            }

            bool showExpandedSecondary = hasSecondary && allowQuestListExpansion && _questSummaryExpanded;
            _questTrackerList.style.display = showExpandedSecondary ? DisplayStyle.Flex : DisplayStyle.None;

            string secondaryStructureKey = showExpandedSecondary
                ? BuildSecondaryStructureKey(_summaryQuestBuffer)
                : string.Empty;

            if (!showExpandedSecondary)
            {
                _questTrackerList.Clear();
                _secondaryQuestCardRoots.Clear();
                _cachedSecondaryStructureKey = string.Empty;
            }
            else
            {
                if (forceStructureRebuild ||
                    _cachedSecondaryStructureKey != secondaryStructureKey ||
                    _secondaryQuestCardRoots.Count != _summaryQuestBuffer.Count)
                {
                    RebuildSecondaryQuestCards();
                    _cachedSecondaryStructureKey = secondaryStructureKey;
                }
                else
                {
                    UpdateSecondaryQuestCards();
                }
            }

            if (_questTrackerHintHost != null)
            {
                _questTrackerHintHost.style.display = hasSecondary && allowQuestListExpansion
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                if (hasSecondary && allowQuestListExpansion)
                    InputPromptVisualBuilder.Populate(_questTrackerHintHost, BuildQuestFooterHintTokens());
            }

            _questTrackerPanel.style.display = DisplayStyle.Flex;
        }

        private string BuildPrimaryStructureKeyForQuest(QuestDefinitionSO definition, QuestObjectiveDefinition objectiveDefinition)
        {
            return $"quest|{definition.SafeId}|{objectiveDefinition?.id}";
        }

        private string BuildPrimaryStructureKeyForActivity(ActivityHudSnapshot snapshot)
        {
            int sourceId = snapshot != null && snapshot.source != null ? snapshot.source.GetInstanceID() : 0;
            bool isChampionship = snapshot?.source is RaceCourseLine race && race.IsRegionalChampionship;

            return $"activity|{snapshot?.mode}|{snapshot?.activityKind}|{sourceId}|champ:{isChampionship}|progress:{snapshot?.showProgressBar}";
        }

        private string BuildSecondaryStructureKey(System.Collections.Generic.List<QuestRuntimeState> states)
        {
            if (states == null || states.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder(128);
            sb.Append(states.Count).Append('|');

            for (int i = 0; i < states.Count; i++)
            {
                if (i > 0)
                    sb.Append(';');

                sb.Append(states[i]?.questId ?? "null");
            }

            return sb.ToString();
        }

        private void RebuildSecondaryQuestCards()
        {
            _questTrackerList.Clear();
            _secondaryQuestCardRoots.Clear();

            if (questDirector == null)
                return;

            for (int i = 0; i < _summaryQuestBuffer.Count; i++)
            {
                var state = _summaryQuestBuffer[i];
                if (state == null)
                    continue;

                var definition = questDirector.GetQuestDefinition(state.questId);
                if (definition == null)
                    continue;

                var card = BuildQuestTrackerCard(definition, state, isSelected: false, compact: true);
                _secondaryQuestCardRoots.Add(card);
                _questTrackerList.Add(card);
            }

            UpdateSecondaryQuestCards();
        }

        private void UpdateSecondaryQuestCards()
        {
            if (questDirector == null)
                return;

            int count = Mathf.Min(_secondaryQuestCardRoots.Count, _summaryQuestBuffer.Count);
            for (int i = 0; i < count; i++)
            {
                var card = _secondaryQuestCardRoots[i];
                var state = _summaryQuestBuffer[i];
                if (card == null || state == null)
                    continue;

                var definition = questDirector.GetQuestDefinition(state.questId);
                if (definition == null)
                    continue;

                UpdateSecondaryQuestCard(card, definition, state);
            }
        }

        private void UpdatePrimaryQuestCard(
            VisualElement card,
            QuestDefinitionSO definition,
            QuestObjectiveDefinition objectiveDefinition,
            QuestObjectiveRuntimeState objectiveState)
        {
            if (card == null || definition == null || objectiveDefinition == null || objectiveState == null)
                return;

            SetLabelText(card, "PrimaryKicker", string.IsNullOrWhiteSpace(definition.title) ? definition.SafeId : definition.title.Trim());
            SetLabelText(card, "PrimaryTitle", string.IsNullOrWhiteSpace(objectiveDefinition.title) ? objectiveDefinition.BuildAuthoringSummary() : objectiveDefinition.title.Trim());
            SetLabelText(card, "PrimaryGlyph", "◎");

            string promptText = BuildDetailedPrompt(objectiveDefinition);
            bool showDirective = !string.IsNullOrWhiteSpace(promptText) && promptText != "-";
            SetElementVisible(card.Q<VisualElement>("PrimaryDirectiveRow"), showDirective);
            if (showDirective)
            {
                SetLabelText(card, "PrimaryDirectiveKicker", "NEXT");
                SetLabelText(card, "PrimaryDirectiveText", promptText);
            }

            SetElementVisible(card.Q<VisualElement>("PrimaryProgressWrap"), true);
            SetProgressFillWidth(card, "PrimaryProgressFill", objectiveState.progress01);
            SetLabelText(card, "PrimaryProgressLabel", objectiveState.progressText);
        }

        private void UpdatePrimaryActivityCard(VisualElement card, ActivityHudSnapshot snapshot)
        {
            if (card == null || snapshot == null)
                return;

            SetLabelText(card, "PrimaryKicker", string.IsNullOrWhiteSpace(snapshot.title) ? "Activity" : snapshot.title.Trim());

            string titleText = (!string.IsNullOrWhiteSpace(snapshot.subtitle) && snapshot.mode != ActivityHudMode.RacePreStart)
                ? snapshot.subtitle.Trim()
                : string.Empty;
            SetLabelText(card, "PrimaryTitle", titleText);
            SetElementVisible(card.Q<Label>("PrimaryTitle"), !string.IsNullOrWhiteSpace(titleText));

            SetLabelText(card, "PrimaryGlyph", ResolveActivityGlyph(snapshot));

            var statusLabel = card.Q<Label>("PrimaryStatus");
            bool showStatus = statusLabel != null && !string.IsNullOrWhiteSpace(snapshot.statusText);
            SetElementVisible(statusLabel, showStatus);
            if (showStatus)
                statusLabel.text = snapshot.statusText.Trim();

            var metaLine = card.Q<Label>("PrimaryMetaLine");
            var leagueTitle = card.Q<Label>("PrimaryLeagueTitle");

            if (snapshot.mode == ActivityHudMode.RacePreStart)
            {
                SetElementVisible(metaLine, false);
                SetElementVisible(leagueTitle, true);
                if (leagueTitle != null)
                    leagueTitle.text = string.IsNullOrWhiteSpace(snapshot.subtitle) ? "League" : snapshot.subtitle.Trim();
            }
            else
            {
                SetElementVisible(leagueTitle, false);
                bool showMeta = metaLine != null && !string.IsNullOrWhiteSpace(snapshot.subtitle);
                SetElementVisible(metaLine, showMeta);
                if (showMeta)
                    metaLine.text = snapshot.subtitle.Trim();
            }

            bool showDirective = !string.IsNullOrWhiteSpace(snapshot.objectiveText);
            var directiveRow = card.Q<VisualElement>("PrimaryDirectiveRow");
            SetElementVisible(directiveRow, showDirective);

            if (showDirective)
            {
                bool urgentRescueReturn =
                    snapshot.activityKind == MountainActivityKind.Rescue &&
                    snapshot.source is RescueService rescue &&
                    rescue.IsAwaitingReturnToSnowmobile;

                directiveRow?.EnableInClassList("is-urgent", urgentRescueReturn);

                SetLabelText(card, "PrimaryDirectiveKicker",
                    urgentRescueReturn ? "RETURN" : ResolveDirectiveLabel(snapshot)?.ToUpperInvariant());

                SetLabelText(card, "PrimaryDirectiveText", snapshot.objectiveText.Trim());
            }
            else
            {
                directiveRow?.EnableInClassList("is-urgent", false);
            }

            bool showProgress = snapshot.showProgressBar;
            SetElementVisible(card.Q<VisualElement>("PrimaryProgressWrap"), showProgress);
            if (showProgress)
            {
                SetProgressFillWidth(card, "PrimaryProgressFill", snapshot.progress01);
                SetLabelText(card, "PrimaryProgressLabel", snapshot.progressText);
            }

            UpdateMetricSlot(card, 0, snapshot.statLineA);
            UpdateMetricSlot(card, 1, snapshot.statLineB);
            UpdateMetricSlot(card, 2, snapshot.statLineC);

            var controlRow = card.Q<VisualElement>("PrimaryControlRow");
            bool showControls = snapshot.mode == ActivityHudMode.RacePreStart;
            SetElementVisible(controlRow, showControls);
            if (showControls)
            {
                SetLabelText(card, "PrimaryStartBinding", ResolveRaceBindingText(GetBindingDisplay("Interact"), null, "-"));
                SetLabelText(card, "PrimaryStartText", "Hold to Start");
            }
        }

        private void UpdateSecondaryQuestCard(VisualElement card, QuestDefinitionSO definition, QuestRuntimeState state)
        {
            if (card == null || definition == null || state == null)
                return;

            SetLabelText(card, "QuestSecondaryTitle", string.IsNullOrWhiteSpace(definition.title) ? definition.SafeId : definition.title);

            string objectiveTitle = string.Empty;
            QuestObjectiveRuntimeState nextObjective = null;
            var stage = definition.GetStage(state.currentStageIndex);

            if (state.objectiveStates != null)
            {
                for (int i = 0; i < state.objectiveStates.Count; i++)
                {
                    var objective = state.objectiveStates[i];
                    if (objective != null && !objective.completed)
                    {
                        nextObjective = objective;
                        break;
                    }
                }
            }

            if (nextObjective != null && stage != null && stage.objectives != null)
            {
                for (int i = 0; i < stage.objectives.Count; i++)
                {
                    var candidate = stage.objectives[i];
                    if (candidate != null && string.Equals(candidate.id, nextObjective.objectiveId, StringComparison.OrdinalIgnoreCase))
                    {
                        objectiveTitle = !string.IsNullOrWhiteSpace(candidate.title)
                            ? candidate.title
                            : candidate.BuildAuthoringSummary();
                        break;
                    }
                }
            }

            var subtitleLabel = card.Q<Label>("QuestSecondarySubtitle");
            bool showSubtitle = subtitleLabel != null && !string.IsNullOrWhiteSpace(objectiveTitle);
            SetElementVisible(subtitleLabel, showSubtitle);
            if (showSubtitle)
                subtitleLabel.text = objectiveTitle;

            UpdateSecondaryQuestPips(card.Q<VisualElement>("QuestSecondaryPips"), GetQuestProgress01(state));
        }

        private void UpdateMetricSlot(VisualElement card, int index, string rawText)
        {
            var slot = card.Q<VisualElement>($"MetricCard_{index}");
            if (slot == null)
                return;

            bool show = !string.IsNullOrWhiteSpace(rawText);
            SetElementVisible(slot, show);
            if (!show)
                return;

            SplitMetricText(rawText, out string label, out string value);
            SetLabelText(card, $"MetricGlyph_{index}", ResolveMetricGlyph(label));
            SetLabelText(card, $"MetricLabel_{index}", string.IsNullOrWhiteSpace(label) ? "Info" : label);
            SetLabelText(card, $"MetricValue_{index}", value);
        }

        private void UpdateSecondaryQuestPips(VisualElement host, float progress01)
        {
            if (host == null)
                return;

            const int count = 4;
            int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress01) * count), 0, count);

            for (int i = 0; i < host.childCount; i++)
            {
                var pip = host[i];
                if (pip == null)
                    continue;

                pip.style.opacity = i < filled ? 1f : 0.35f;
            }
        }

        private void SetLabelText(VisualElement root, string elementName, string value)
        {
            var label = root != null ? root.Q<Label>(elementName) : null;
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private void SetProgressFillWidth(VisualElement root, string elementName, float progress01)
        {
            var fill = root != null ? root.Q<VisualElement>(elementName) : null;
            if (fill != null)
                fill.style.width = Length.Percent(Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f));
        }

        private void SetElementVisible(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private QuestRuntimeState ResolveFocusedQuestState(System.Collections.Generic.List<QuestRuntimeState> states)
        {
            if (states == null || states.Count == 0)
                return null;

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && string.Equals(state.questId, _selectedQuestId, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var definition = state != null ? questDirector.GetQuestDefinition(state.questId) : null;
                if (state != null && definition != null && definition.tutorialQuest && !state.completed)
                    return state;
            }

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && !state.completed)
                    return state;
            }

            return states[0];
        }

        private void CycleFocusedQuest(int direction)
        {
            if (questDirector == null)
                return;

            questDirector.GetTrackedQuestStates(_trackedQuestBuffer);
            if (_trackedQuestBuffer.Count <= 1)
                return;

            if (direction == 0)
                direction = 1;

            int currentIndex = -1;
            for (int i = 0; i < _trackedQuestBuffer.Count; i++)
            {
                if (_trackedQuestBuffer[i] != null && string.Equals(_trackedQuestBuffer[i].questId, _selectedQuestId, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = currentIndex < 0
                ? 0
                : (currentIndex + direction + _trackedQuestBuffer.Count) % _trackedQuestBuffer.Count;
            var next = _trackedQuestBuffer[nextIndex];
            if (next != null)
            {
                _selectedQuestId = next.questId;
                RefreshQuestTracker(forceStructureRebuild: true); 
            }
        }

        private void ToggleQuestSummary()
        {
            _questSummaryExpanded = !_questSummaryExpanded;
            RefreshQuestTracker(forceStructureRebuild: true);
        }

        private VisualElement BuildFocusedQuestCard(
    QuestDefinitionSO definition,
    QuestRuntimeState state,
    QuestObjectiveDefinition objectiveDefinition,
    QuestObjectiveRuntimeState objectiveState)
        {
            Color accent = QuestVisualUtility.GetQuestAccent(definition.SafeId);

            var card = new VisualElement();
            card.name = "PrimaryQuestCard";
            card.AddToClassList("quest-focus-card");
            card.AddToClassList("quest-focus-card--calm");

            var accentBar = new VisualElement();
            accentBar.AddToClassList("quest-focus-accent");
            accentBar.style.backgroundColor = new StyleColor(accent);
            card.Add(accentBar);

            var headerRow = new VisualElement();
            headerRow.AddToClassList("objective-card-header");

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("objective-card-title-block");

            var questTitle = new Label();
            questTitle.name = "PrimaryKicker";
            questTitle.AddToClassList("quest-focus-quest-title");
            questTitle.AddToClassList("objective-card-kicker");
            titleBlock.Add(questTitle);

            var objectiveTitle = new Label();
            objectiveTitle.name = "PrimaryTitle";
            objectiveTitle.AddToClassList("quest-focus-title");
            objectiveTitle.AddToClassList("objective-card-title");
            titleBlock.Add(objectiveTitle);

            headerRow.Add(titleBlock);

            var headerSide = new VisualElement();
            headerSide.AddToClassList("objective-card-header-side");

            var glyph = new Label();
            glyph.name = "PrimaryGlyph";
            glyph.AddToClassList("objective-card-glyph");
            headerSide.Add(glyph);

            headerRow.Add(headerSide);

            card.Add(headerRow);
            card.Add(BuildObjectiveDivider());

            var directive = BuildDirectiveLine(null, null);
            directive.name = "PrimaryDirectiveRow";
            card.Add(directive);

            var progressWrap = new VisualElement();
            progressWrap.name = "PrimaryProgressWrap";
            progressWrap.AddToClassList("activity-progress-wrap");
            progressWrap.AddToClassList("objective-progress-wrap");

            var progressTrack = new VisualElement();
            progressTrack.AddToClassList("quest-focus-progress-track");
            progressTrack.AddToClassList("activity-progress-track");

            var progressFill = new VisualElement();
            progressFill.name = "PrimaryProgressFill";
            progressFill.AddToClassList("quest-focus-progress-fill");
            progressFill.AddToClassList("activity-progress-fill");
            progressFill.style.backgroundColor = new StyleColor(accent);

            progressTrack.Add(progressFill);
            progressWrap.Add(progressTrack);

            var progress = new Label();
            progress.name = "PrimaryProgressLabel";
            progress.AddToClassList("quest-focus-progress");
            progress.AddToClassList("activity-progress-text");
            progressWrap.Add(progress);

            card.Add(progressWrap);

            return card;
        }

        private VisualElement BuildFocusedActivityCard(ActivityHudSnapshot snapshot)
        {
            var card = new VisualElement();
            card.name = "PrimaryActivityCard";
            card.AddToClassList("quest-focus-card");
            card.AddToClassList("activity-focus-card");
            card.AddToClassList("quest-focus-card--calm");

            if (snapshot != null)
            {
                switch (snapshot.mode)
                {
                    case ActivityHudMode.RacePreStart:
                        card.AddToClassList("is-prestart");
                        break;
                    case ActivityHudMode.RaceActive:
                    case ActivityHudMode.RescueActive:
                        card.AddToClassList("is-active");
                        break;
                    case ActivityHudMode.Result:
                        card.AddToClassList("is-result");
                        break;
                }

                switch (snapshot.activityKind)
                {
                    case MountainActivityKind.Race:
                        card.AddToClassList("is-race");
                        break;
                    case MountainActivityKind.Rescue:
                        card.AddToClassList("is-rescue");
                        break;
                }
            }

            Color accent = snapshot != null ? snapshot.accentColor : new Color(0.36f, 0.78f, 1f, 1f);

            var accentBar = new VisualElement();
            accentBar.AddToClassList("quest-focus-accent");
            accentBar.style.backgroundColor = new StyleColor(accent);
            card.Add(accentBar);

            var headerRow = new VisualElement();
            headerRow.AddToClassList("objective-card-header");

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("objective-card-title-block");

            var title = new Label();
            title.name = "PrimaryKicker";
            title.AddToClassList("quest-focus-quest-title");
            title.AddToClassList("objective-card-kicker");
            titleBlock.Add(title);

            var subtitle = new Label();
            subtitle.name = "PrimaryTitle";
            subtitle.AddToClassList("quest-focus-title");
            subtitle.AddToClassList("objective-card-title");
            titleBlock.Add(subtitle);

            headerRow.Add(titleBlock);

            var headerSide = new VisualElement();
            headerSide.AddToClassList("objective-card-header-side");

            var status = new Label();
            status.name = "PrimaryStatus";
            status.AddToClassList("activity-status-chip");
            status.AddToClassList("activity-status-chip--quiet");
            headerSide.Add(status);

            var glyph = new Label();
            glyph.name = "PrimaryGlyph";
            glyph.AddToClassList("objective-card-glyph");
            glyph.AddToClassList("is-activity");
            headerSide.Add(glyph);

            headerRow.Add(headerSide);

            card.Add(headerRow);
            card.Add(BuildObjectiveDivider());

            bool isChampionship = snapshot?.source is RaceCourseLine race && race.IsRegionalChampionship;
            if (snapshot != null && snapshot.mode == ActivityHudMode.RacePreStart)
            {
                card.Add(isChampionship
                    ? BuildChampionshipSelectorRow(snapshot)
                    : BuildRaceLeagueSelectorRow(snapshot));
            }
            else
            {
                var meta = BuildActivityMetaLine(string.Empty);
                meta.name = "PrimaryMetaLine";
                card.Add(meta);
            }

            var directive = BuildDirectiveLine(null, null);
            directive.name = "PrimaryDirectiveRow";
            card.Add(directive);

            var progressWrap = new VisualElement();
            progressWrap.name = "PrimaryProgressWrap";
            progressWrap.AddToClassList("activity-progress-wrap");
            progressWrap.AddToClassList("objective-progress-wrap");

            var progressTrack = new VisualElement();
            progressTrack.AddToClassList("quest-focus-progress-track");
            progressTrack.AddToClassList("activity-progress-track");

            var progressFill = new VisualElement();
            progressFill.name = "PrimaryProgressFill";
            progressFill.AddToClassList("quest-focus-progress-fill");
            progressFill.AddToClassList("activity-progress-fill");
            progressFill.style.backgroundColor = new StyleColor(accent);

            progressTrack.Add(progressFill);
            progressWrap.Add(progressTrack);

            var progress = new Label();
            progress.name = "PrimaryProgressLabel";
            progress.AddToClassList("quest-focus-progress");
            progress.AddToClassList("activity-progress-text");
            progressWrap.Add(progress);

            card.Add(progressWrap);
            card.Add(BuildActivityMetricGrid(snapshot));
            card.Add(BuildActivityControlRow(snapshot));

            return card;
        }

        private VisualElement BuildRaceLeagueSelectorRow(ActivityHudSnapshot snapshot)
        {
            var row = new VisualElement();
            row.AddToClassList("activity-league-row");

            row.Add(BuildRaceBindingCard(
                GetRaceCycleLeftBindingDisplay(),
                null,
                "Q"));

            var leagueTitle = new Label(string.IsNullOrWhiteSpace(snapshot?.subtitle) ? "League" : snapshot.subtitle.Trim());
            leagueTitle.name = "PrimaryLeagueTitle";
            leagueTitle.AddToClassList("activity-league-title");
            row.Add(leagueTitle);

            row.Add(BuildRaceBindingCard(
                GetRaceCycleRightBindingDisplay(),
                null,
                "E"));

            return row;
        }

        private VisualElement BuildChampionshipSelectorRow(ActivityHudSnapshot snapshot)
        {
            var row = new VisualElement();
            row.AddToClassList("activity-league-row");
            row.AddToClassList("is-championship");

            var title = new Label(string.IsNullOrWhiteSpace(snapshot?.subtitle) ? "Championship" : snapshot.subtitle.Trim());
            title.name = "PrimaryLeagueTitle";
            title.AddToClassList("activity-league-title");
            title.AddToClassList("is-championship");
            row.Add(title);

            return row;
        }

        private VisualElement BuildActivityMetaLine(string text)
        {
            var meta = new Label(text ?? string.Empty);
            meta.AddToClassList("activity-meta-line");
            return meta;
        }

        private VisualElement BuildActivityMetricGrid(ActivityHudSnapshot snapshot)
        {
            var grid = new VisualElement();
            grid.AddToClassList("activity-metric-grid");

            if (snapshot != null)
            {
                switch (snapshot.mode)
                {
                    case ActivityHudMode.RacePreStart:
                        grid.AddToClassList("is-prestart");
                        break;
                    case ActivityHudMode.RaceActive:
                    case ActivityHudMode.RescueActive:
                        grid.AddToClassList("is-active");
                        break;
                    case ActivityHudMode.Result:
                        grid.AddToClassList("is-result");
                        break;
                }

                if (snapshot.activityKind == MountainActivityKind.Rescue)
                    grid.AddToClassList("is-rescue");
                else if (snapshot.activityKind == MountainActivityKind.Race)
                    grid.AddToClassList("is-race");

                bool verticalStack =
                    snapshot.mode == ActivityHudMode.RacePreStart ||
                    snapshot.mode == ActivityHudMode.RescueActive ||
                    snapshot.mode == ActivityHudMode.Result;

                if (verticalStack)
                    grid.AddToClassList("is-vertical");
            }

            for (int i = 0; i < 3; i++)
                grid.Add(BuildMetricCardSlot(i));

            return grid;
        }

        private VisualElement BuildMetricCardSlot(int index)
        {
            var chip = new VisualElement();
            chip.name = $"MetricCard_{index}";
            chip.AddToClassList("activity-metric-chip");
            chip.AddToClassList("activity-metric-chip--flat");

            var line = new VisualElement();
            line.AddToClassList("activity-metric-line");

            var icon = new Label();
            icon.name = $"MetricGlyph_{index}";
            icon.AddToClassList("activity-metric-glyph");
            line.Add(icon);

            var labelEl = new Label();
            labelEl.name = $"MetricLabel_{index}";
            labelEl.AddToClassList("activity-metric-line-label");
            line.Add(labelEl);

            var separator = new Label("—");
            separator.AddToClassList("activity-metric-line-separator");
            line.Add(separator);

            var valueEl = new Label();
            valueEl.name = $"MetricValue_{index}";
            valueEl.AddToClassList("activity-metric-line-value");
            line.Add(valueEl);

            chip.Add(line);
            return chip;
        }

        private VisualElement BuildActivityControlRow(ActivityHudSnapshot snapshot)
        {
            var row = new VisualElement();
            row.name = "PrimaryControlRow";
            row.AddToClassList("activity-control-row");
            row.AddToClassList("is-race-prestart");
            row.AddToClassList("is-minimal");

            row.Add(BuildRaceBindingActionChip(
                GetBindingDisplay("Interact"),
                "Hold to Start"));

            return row;
        }

        private VisualElement BuildActivityTextChip(string text, string className)
        {
            var chip = new Label(text.Trim());
            chip.AddToClassList(className);
            return chip;
        }

        private VisualElement BuildRaceBindingCard(string primaryBinding, string secondaryBinding, string fallback)
        {
            string bindingText = ResolveRaceBindingText(primaryBinding, secondaryBinding, fallback);

            var card = new VisualElement();
            card.AddToClassList("activity-input-card");
            card.AddToClassList("activity-binding-card");

            var label = new Label(bindingText);
            label.AddToClassList("activity-binding-card-label");
            card.Add(label);

            return card;
        }

        private VisualElement BuildRaceBindingActionChip(string binding, string actionLabel)
        {
            var chip = new VisualElement();
            chip.AddToClassList("activity-control-chip");
            chip.AddToClassList("activity-binding-action-chip");

            var bindingCard = new VisualElement();
            bindingCard.AddToClassList("activity-binding-inline-card");

            var bindingLabel = new Label(ResolveRaceBindingText(binding, null, "-"));
            bindingLabel.name = "PrimaryStartBinding";
            bindingLabel.AddToClassList("activity-binding-inline-label");
            bindingCard.Add(bindingLabel);

            chip.Add(bindingCard);

            var text = new Label(actionLabel ?? string.Empty);
            text.name = "PrimaryStartText";
            text.AddToClassList("activity-control-text");
            chip.Add(text);

            return chip;
        }

        private VisualElement BuildDirectiveLine(string text, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("objective-directive-row");

            var kicker = new Label(label ?? string.Empty);
            kicker.name = "PrimaryDirectiveKicker";
            kicker.AddToClassList("objective-directive-kicker");
            row.Add(kicker);

            var body = new Label(text ?? string.Empty);
            body.name = "PrimaryDirectiveText";
            body.AddToClassList("objective-directive-text");
            row.Add(body);

            return row;
        }

        private VisualElement BuildObjectiveDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("objective-divider");
            return divider;
        }

        private static string ResolveDirectiveLabel(ActivityHudSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            switch (snapshot.mode)
            {
                case ActivityHudMode.RacePreStart:
                    return "Event";
                case ActivityHudMode.RaceActive:
                    return "Now";
                case ActivityHudMode.RescueActive:
                    return "Task";
                case ActivityHudMode.Result:
                    return "Result";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveActivityGlyph(ActivityHudSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            switch (snapshot.mode)
            {
                case ActivityHudMode.RacePreStart:
                case ActivityHudMode.RaceActive:
                    return "⚑";
                case ActivityHudMode.RescueActive:
                    return "✚";
                case ActivityHudMode.Result:
                    switch (snapshot.resultType)
                    {
                        case ActivityHudResultType.Success: return "★";
                        case ActivityHudResultType.Failure: return "!";
                        case ActivityHudResultType.Cancelled: return "–";
                        default: return "•";
                    }
                default:
                    return string.Empty;
            }
        }

        private static string ResolveRaceBindingText(string primaryBinding, string secondaryBinding, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(primaryBinding) && primaryBinding != "-")
                return primaryBinding.Trim();

            if (!string.IsNullOrWhiteSpace(secondaryBinding) && secondaryBinding != "-")
                return secondaryBinding.Trim();

            return string.IsNullOrWhiteSpace(fallback) ? "-" : fallback.Trim();
        }

        private static void SplitMetricText(string text, out string label, out string value)
        {
            label = string.Empty;
            value = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            int colon = text.IndexOf(':');
            if (colon <= 0 || colon >= text.Length - 1)
                return;

            label = text.Substring(0, colon).Trim();
            value = text.Substring(colon + 1).Trim();
        }

        private static string ResolveMetricGlyph(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "•";

            string lower = label.Trim().ToLowerInvariant();
            if (lower.Contains("time"))
                return "◷";
            if (lower.Contains("checkpoint"))
                return "⚑";
            if (lower.Contains("placement"))
                return "№";
            if (lower.Contains("reward"))
                return "$";
            if (lower.Contains("league"))
                return "◈";
            if (lower.Contains("recovered"))
                return "✚";
            if (lower.Contains("transport"))
                return "➜";
            if (lower.Contains("rank"))
                return "★";
            if (lower.Contains("search"))
                return "⌖";
            if (lower.Contains("npc"))
                return "◎";
            if (lower.Contains("start"))
                return "⏱";

            return "•";
        }

        private static string ResolveMetricSymbol(string label, string fallback)
        {
            if (string.IsNullOrWhiteSpace(label))
                return fallback;

            string lower = label.Trim().ToLowerInvariant();
            if (lower.Contains("time"))
                return "◷";
            if (lower.Contains("checkpoint"))
                return "⚑";
            if (lower.Contains("placement"))
                return "№";
            if (lower.Contains("reward"))
                return "$";
            if (lower.Contains("league"))
                return "◈";
            if (lower.Contains("recovered"))
                return "✚";
            if (lower.Contains("transport"))
                return "➜";
            if (lower.Contains("rank"))
                return "★";
            if (lower.Contains("search"))
                return "⌖";
            if (lower.Contains("npc"))
                return "◎";
            if (lower.Contains("start"))
                return "⏱";

            return fallback;
        }

        private void EnsureActivityFailModal()
        {
            if (_root == null || _activityFailModal != null)
                return;

            _activityModalBlocker = new VisualElement();
            _activityModalBlocker.name = "ActivityFailModalBlocker";
            _activityModalBlocker.AddToClassList("activity-modal-blocker");

            _activityFailModal = new VisualElement();
            _activityFailModal.name = "ActivityFailModal";
            _activityFailModal.AddToClassList("activity-fail-modal");

            var accent = new VisualElement();
            accent.AddToClassList("activity-fail-modal-accent");
            _activityFailModal.Add(accent);

            _lblActivityFailTitle = new Label("Race Failed");
            _lblActivityFailTitle.AddToClassList("activity-fail-modal-title");
            _activityFailModal.Add(_lblActivityFailTitle);

            _lblActivityFailBody = new Label("You missed the race objective.");
            _lblActivityFailBody.AddToClassList("activity-fail-modal-body");
            _activityFailModal.Add(_lblActivityFailBody);

            var actionsRow = new VisualElement();
            actionsRow.AddToClassList("activity-fail-modal-actions");

            _btnActivityFailRetry = new Button(OnRaceFailRetryPressed) { text = "Retry" };
            _btnActivityFailRetry.AddToClassList("activity-fail-modal-button");
            _btnActivityFailRetry.AddToClassList("is-primary");

            _btnActivityFailQuit = new Button(OnRaceFailQuitPressed) { text = "Quit" };
            _btnActivityFailQuit.AddToClassList("activity-fail-modal-button");

            actionsRow.Add(_btnActivityFailRetry);
            actionsRow.Add(_btnActivityFailQuit);
            _activityFailModal.Add(actionsRow);

            _activityModalBlocker.Add(_activityFailModal);
            _root.Add(_activityModalBlocker);

            SetRaceFailModalVisible(false);
        }

        private void SetRaceFailModalVisible(bool visible)
        {
            _showRaceFailModal = visible;

            if (_activityModalBlocker != null)
                _activityModalBlocker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_activityFailModal != null)
                _activityFailModal.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
                GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 400);
            else if (_visible)
                GameCursorService.Request(this, GameCursorMode.HiddenLocked, priority: 100);
        }

        private void ShowRaceFailModal(RaceCourseLine race, string reason)
        {
            _activeFailedRace = race;
            _activeFailedRaceReason = string.IsNullOrWhiteSpace(reason) ? "Race failed." : reason.Trim();

            if (_lblActivityFailTitle != null)
                _lblActivityFailTitle.text = race != null && !string.IsNullOrWhiteSpace(race.RaceName)
                    ? $"{race.RaceName} Failed"
                    : "Race Failed";

            if (_lblActivityFailBody != null)
                _lblActivityFailBody.text = _activeFailedRaceReason;

            SetRaceFailModalVisible(true);
        }

        private void HideRaceFailModal()
        {
            _activeFailedRace = null;
            _activeFailedRaceReason = null;
            SetRaceFailModalVisible(false);
        }

        private void PollActivityFailureModal()
        {
            if (_showRaceFailModal || activityHudPresenter == null)
                return;

            if (activityHudPresenter.TryConsumeRaceFailureModal(out var failedRace, out var reason))
                ShowRaceFailModal(failedRace, reason);
        }

        private void OnRaceFailRetryPressed()
        {
            var failedRace = _activeFailedRace;
            HideRaceFailModal();

            if (failedRace == null)
                return;

            if (activityHudPresenter != null)
                activityHudPresenter.ClearPendingRaceFailureModal();

            if (!TryRestartRaceFromModal(failedRace))
            {
                Debug.LogWarning(
                    $"[MiniMountainHudController] Failed to restart race '{failedRace.name}' from fail modal. " +
                    "No supported public retry/start entrypoint was found on RaceCourseLine.");
            }
        }

        private void OnRaceFailQuitPressed()
        {
            HideRaceFailModal();

            if (activityHudPresenter != null)
                activityHudPresenter.ClearPendingRaceFailureModal();

            // Grounded behavior:
            // - MountainActivityManager has already cleared the failed activity.
            // - RaceActivityService has already started wrap-up / cleanup.
            // So quitting the fail modal only needs to dismiss the popup and return the
            // player to normal exploration / pre-start HUD behavior.
        }

        private bool TryRestartRaceFromModal(RaceCourseLine race)
        {
            if (race == null)
                return false;

            var mgr = MountainActivityManager.Instance;
            if (mgr != null && mgr.HasActiveActivity)
            {
                Debug.LogWarning(
                    $"[MiniMountainHudController] Cannot restart race '{race.name}' because another activity is currently active.");
                return false;
            }

            int selectedLeague = Mathf.Max(1, race.SelectedLeagueNumber);

            // Try the race's own public restart/start entrypoints first.
            if (TryInvokeRaceRestartMethod(race, "RetryRaceFromFailure"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "RetryRace"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "RestartRace"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "RestartSelectedLeagueRace"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "BeginSelectedLeagueRace"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "StartSelectedLeagueRace"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "BeginRaceFromSelection"))
                return true;

            if (TryInvokeRaceRestartMethod(race, "TryBeginRaceFromSelection"))
                return true;

            // Then try common signatures that take the selected league / variant number.
            if (TryInvokeRaceRestartMethod(race, "BeginRace", selectedLeague))
                return true;

            if (TryInvokeRaceRestartMethod(race, "StartRace", selectedLeague))
                return true;

            if (TryInvokeRaceRestartMethod(race, "TryStartRace", selectedLeague))
                return true;

            // Last-resort fallback: if your RaceCourseLine exposes the same UI flow as the
            // world start prompt, SendMessage can still re-enter it without hard compile coupling.
            if (TrySendRaceRestartMessage(race, "RetryRaceFromFailure"))
                return true;

            if (TrySendRaceRestartMessage(race, "RetryRace"))
                return true;

            if (TrySendRaceRestartMessage(race, "RestartRace"))
                return true;

            if (TrySendRaceRestartMessage(race, "RestartSelectedLeagueRace"))
                return true;

            if (TrySendRaceRestartMessage(race, "BeginSelectedLeagueRace"))
                return true;

            if (TrySendRaceRestartMessage(race, "StartSelectedLeagueRace"))
                return true;

            return false;
        }

        private static bool TryInvokeRaceRestartMethod(RaceCourseLine race, string methodName)
        {
            if (race == null || string.IsNullOrWhiteSpace(methodName))
                return false;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;

            var method = race.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
            if (method == null)
                return false;

            try
            {
                object result = method.Invoke(race, null);

                if (method.ReturnType == typeof(bool))
                    return result is bool ok && ok;

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[MiniMountainHudController] Exception invoking RaceCourseLine.{methodName}() on '{race.name}'.\n{ex}");
                return false;
            }
        }

        private static bool TryInvokeRaceRestartMethod(RaceCourseLine race, string methodName, int leagueNumber)
        {
            if (race == null || string.IsNullOrWhiteSpace(methodName))
                return false;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;

            var method = race.GetType().GetMethod(methodName, flags, null, new[] { typeof(int) }, null);
            if (method == null)
                return false;

            try
            {
                object result = method.Invoke(race, new object[] { leagueNumber });

                if (method.ReturnType == typeof(bool))
                    return result is bool ok && ok;

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[MiniMountainHudController] Exception invoking RaceCourseLine.{methodName}(int) on '{race.name}'.\n{ex}");
                return false;
            }
        }

        private static bool TrySendRaceRestartMessage(RaceCourseLine race, string methodName)
        {
            if (race == null || string.IsNullOrWhiteSpace(methodName))
                return false;

            try
            {
                race.gameObject.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[MiniMountainHudController] Exception sending '{methodName}' to race '{race.name}'.\n{ex}");
                return false;
            }
        }

        private VisualElement BuildQuestTrackerCard(QuestDefinitionSO definition, QuestRuntimeState state, bool isSelected, bool compact)
        {
            var card = new VisualElement();
            card.AddToClassList("quest-tracker-card");
            if (compact)
                card.AddToClassList("is-secondary");
            if (isSelected)
                card.AddToClassList("is-selected");

            Color accent = QuestVisualUtility.GetQuestAccent(definition.SafeId);

            var accentBar = new VisualElement();
            accentBar.AddToClassList("quest-tracker-accent");
            accentBar.style.backgroundColor = new StyleColor(accent);
            card.Add(accentBar);

            if (compact)
            {
                var row = new VisualElement();
                row.AddToClassList("quest-secondary-row");

                var textBlock = new VisualElement();
                textBlock.AddToClassList("quest-secondary-text-block");

                var title = new Label(string.IsNullOrWhiteSpace(definition.title) ? definition.SafeId : definition.title);
                title.name = "QuestSecondaryTitle";
                title.AddToClassList("quest-tracker-card-title");
                title.AddToClassList("quest-secondary-title");
                textBlock.Add(title);

                var subtitle = new Label();
                subtitle.name = "QuestSecondarySubtitle";
                subtitle.AddToClassList("quest-secondary-subtitle");
                textBlock.Add(subtitle);

                row.Add(textBlock);

                var pipBar = BuildSecondaryQuestPips(GetQuestProgress01(state), accent);
                pipBar.name = "QuestSecondaryPips";
                row.Add(pipBar);

                card.Add(row);
            }
            else
            {
                var title = new Label(string.IsNullOrWhiteSpace(definition.title) ? definition.SafeId : definition.title);
                title.AddToClassList("quest-tracker-card-title");
                card.Add(title);

                QuestObjectiveRuntimeState nextObjective = null;
                var stage = definition.GetStage(state.currentStageIndex);
                if (state.objectiveStates != null)
                {
                    for (int i = 0; i < state.objectiveStates.Count; i++)
                    {
                        var objective = state.objectiveStates[i];
                        if (objective != null && !objective.completed)
                        {
                            nextObjective = objective;
                            break;
                        }
                    }
                }

                if (nextObjective != null && stage != null && stage.objectives != null)
                {
                    QuestObjectiveDefinition objectiveDefinition = null;
                    for (int i = 0; i < stage.objectives.Count; i++)
                    {
                        var candidate = stage.objectives[i];
                        if (candidate != null && string.Equals(candidate.id, nextObjective.objectiveId, StringComparison.OrdinalIgnoreCase))
                        {
                            objectiveDefinition = candidate;
                            break;
                        }
                    }

                    string objectiveTitle = objectiveDefinition != null && !string.IsNullOrWhiteSpace(objectiveDefinition.title)
                        ? objectiveDefinition.title
                        : objectiveDefinition != null
                            ? objectiveDefinition.BuildAuthoringSummary()
                            : string.Empty;

                    if (!string.IsNullOrWhiteSpace(objectiveTitle))
                    {
                        var objective = new Label(objectiveTitle);
                        objective.AddToClassList("quest-tracker-objective");
                        card.Add(objective);
                    }

                    var objectiveTrack = new VisualElement();
                    objectiveTrack.AddToClassList("quest-tracker-objective-track");

                    var objectiveFill = new VisualElement();
                    objectiveFill.AddToClassList("quest-tracker-objective-fill");
                    objectiveFill.style.width = Length.Percent(Mathf.RoundToInt(Mathf.Clamp01(nextObjective.progress01) * 100f));
                    objectiveFill.style.backgroundColor = new StyleColor(accent);
                    objectiveTrack.Add(objectiveFill);
                    card.Add(objectiveTrack);

                    if (!string.IsNullOrWhiteSpace(nextObjective.progressText))
                    {
                        var progress = new Label(nextObjective.progressText);
                        progress.AddToClassList("quest-tracker-progress");
                        card.Add(progress);
                    }
                }
            }

            card.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedQuestId = definition.SafeId;
                _questSummaryExpanded = true;
                RefreshQuestTracker(forceStructureRebuild: true);
            });

            return card;
        }

        private static VisualElement BuildPromptChip(string text, string className)
        {
            var chip = new Label(string.IsNullOrWhiteSpace(text) ? "-" : text.Trim());
            chip.AddToClassList(className);
            return chip;
        }

        private VisualElement BuildSecondaryQuestPips(float progress01, Color accent)
        {
            var host = new VisualElement();
            host.AddToClassList("quest-secondary-pips");

            const int count = 4;
            int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress01) * count), 0, count);

            for (int i = 0; i < count; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("quest-secondary-pip");
                pip.style.backgroundColor = new StyleColor(i < filled
                    ? accent
                    : new Color(1f, 1f, 1f, 0.10f));
                host.Add(pip);
            }

            return host;
        }

        private bool TryResolveFocusedObjective(
            QuestDefinitionSO definition,
            QuestRuntimeState state,
            out QuestObjectiveDefinition objectiveDefinition,
            out QuestObjectiveRuntimeState objectiveState)
        {
            objectiveDefinition = null;
            objectiveState = null;

            var stage = definition != null ? definition.GetStage(state.currentStageIndex) : null;
            if (stage?.objectives == null || state?.objectiveStates == null)
                return false;

            for (int i = 0; i < state.objectiveStates.Count; i++)
            {
                var candidateState = state.objectiveStates[i];
                if (candidateState == null || candidateState.completed)
                    continue;

                for (int j = 0; j < stage.objectives.Count; j++)
                {
                    var candidateDefinition = stage.objectives[j];
                    if (candidateDefinition != null && string.Equals(candidateDefinition.id, candidateState.objectiveId, StringComparison.OrdinalIgnoreCase))
                    {
                        objectiveDefinition = candidateDefinition;
                        objectiveState = candidateState;
                        return true;
                    }
                }
            }

            return false;
        }

        private readonly struct QuestSupportRow
        {
            public readonly string label;
            public readonly string value;

            public QuestSupportRow(string label, string value)
            {
                this.label = label ?? string.Empty;
                this.value = value ?? string.Empty;
            }
        }

        private System.Collections.Generic.List<QuestSupportRow> BuildSupportRows(QuestObjectiveDefinition objectiveDefinition)
        {
            var rows = new System.Collections.Generic.List<QuestSupportRow>();
            if (objectiveDefinition == null)
                return rows;

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.JumpAdjust:
                            rows.Add(new QuestSupportRow("Jump", $"{GetBindingDisplay("Jump")} (hold for more pop)"));
                            rows.Add(new QuestSupportRow("Air adjust", $"{GetBindingDisplay("Lean")} + {CombineBindings("LeftSki", "RightSki")}"));
                            break;
                        case QuestMovementSkillType.SkateSwitch:
                            rows.Add(new QuestSupportRow("Lean forward", GetBindingDisplay("Lean", "positive", "up", "forward")));
                            rows.Add(new QuestSupportRow("Alternate skis", $"{GetBindingDisplay("LeftSki")} then {GetBindingDisplay("RightSki")}"));
                            break;
                        case QuestMovementSkillType.QuickStop:
                            rows.Add(new QuestSupportRow("Lean back", GetBindingDisplay("Lean", "negative", "down", "back", "backward")));
                            rows.Add(new QuestSupportRow("Turn sharply", $"{GetBindingDisplay("LeftSki")} or {GetBindingDisplay("RightSki")}"));
                            break;
                    }
                    break;

                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.AirPose:
                            rows.Add(new QuestSupportRow("Get airborne", GetBindingDisplay("Jump")));
                            break;
                        case QuestInputActionType.PolePush:
                            rows.Add(new QuestSupportRow("Best used", "On flat ground or low speed sections"));
                            break;
                        case QuestInputActionType.PoleDrag:
                            rows.Add(new QuestSupportRow("Best used", "While moving when you want gentle braking"));
                            break;
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    if (objectiveDefinition.uiTarget == QuestUiScreenTargetType.TasksViewed ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.QuestsViewed ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.MapViewed ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.StatsViewed)
                    {
                        rows.Add(new QuestSupportRow("Open overlay", GetOverlayBindingDisplay()));
                    }
                    else if (objectiveDefinition.uiTarget == QuestUiScreenTargetType.ShopOpened ||
                             objectiveDefinition.uiTarget == QuestUiScreenTargetType.KioskOpened ||
                             objectiveDefinition.uiTarget == QuestUiScreenTargetType.RaceKioskOpened)
                    {
                        rows.Add(new QuestSupportRow("Interact", GetBindingDisplay("Interact")));
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked:
                            rows.Add(new QuestSupportRow("What happens next", $"If you are stuck on your side, hold {GetBindingDisplay("EquipSkis")}"));
                            break;
                        case QuestInteractionTargetType.UnequipSkis:
                            rows.Add(new QuestSupportRow("Goal", "Switch to walking mode so you can reset upright"));
                            rows.Add(new QuestSupportRow("Input", $"{GetBindingDisplay("EquipSkis")} (hold)"));
                            break;
                        case QuestInteractionTargetType.EquipSkis:
                            rows.Add(new QuestSupportRow("Goal", "Once upright, put the skis back on and keep riding"));
                            rows.Add(new QuestSupportRow("Input", $"{GetBindingDisplay("EquipSkis")} (hold)"));
                            break;
                        case QuestInteractionTargetType.EnterResort:
                            rows.Add(new QuestSupportRow("Goal", "Step inside the resort to regroup and access services"));
                            rows.Add(new QuestSupportRow("Interact", GetBindingDisplay("Interact")));
                            break;
                        case QuestInteractionTargetType.ExitResort:
                            rows.Add(new QuestSupportRow("Goal", "Head back out when you are ready to ski again"));
                            break;
                        case QuestInteractionTargetType.ClaimDefaultPass:
                            rows.Add(new QuestSupportRow("Interact", GetBindingDisplay("Interact")));
                            break;
                    }
                    break;
            }

            return rows;
        }

        private string BuildDetailedPrompt(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return "-";

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.LeanForward: return InputPromptResolver.GetBindingDisplay(inputActions, "Lean", "positive", "up", "forward");
                        case QuestInputActionType.LeanBackward: return InputPromptResolver.GetBindingDisplay(inputActions, "Lean", "negative", "down", "back", "backward");
                        case QuestInputActionType.Tuck: return $"Hold {InputPromptResolver.GetBindingDisplay(inputActions, "Sprint")}";
                        case QuestInputActionType.PolePush: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "Poles")} on flats";
                        case QuestInputActionType.PoleDrag: return $"Hold {InputPromptResolver.GetBindingDisplay(inputActions, "Poles")} while moving";
                        case QuestInputActionType.AirPose: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "Jump")} + {InputPromptResolver.GetBindingDisplay(inputActions, "Poles")}";
                    }
                    break;

                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.Wedge: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "LeftSki")} + {InputPromptResolver.GetBindingDisplay(inputActions, "RightSki")}";
                        case QuestMovementSkillType.SkateSwitch: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "Lean", "positive", "up", "forward")} + {InputPromptResolver.CombineBindings(inputActions, "LeftSki", "RightSki")}";
                        case QuestMovementSkillType.QuickStop: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "Lean", "negative", "down", "back", "backward")} + {InputPromptResolver.CombineBindings(inputActions, "LeftSki", "RightSki")}";
                        case QuestMovementSkillType.JumpAdjust: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "Jump")} + {InputPromptResolver.GetBindingDisplay(inputActions, "Lean")} + {InputPromptResolver.CombineBindings(inputActions, "LeftSki", "RightSki")}";
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    switch (objectiveDefinition.uiTarget)
                    {
                        case QuestUiScreenTargetType.OverlayOpened: return GetOverlayBindingDisplay();
                        case QuestUiScreenTargetType.MapViewed: return BuildOverlayRoutePrompt("Map");
                        case QuestUiScreenTargetType.StatsViewed: return BuildOverlayRoutePrompt("Stats");
                        case QuestUiScreenTargetType.TasksViewed: return BuildOverlayRoutePrompt("Tasks");
                        case QuestUiScreenTargetType.QuestsViewed: return BuildOverlayRoutePrompt("Quests");
                        case QuestUiScreenTargetType.TaskRewardClaimed: return $"{BuildOverlayRoutePrompt("Tasks")} + Claim";
                        case QuestUiScreenTargetType.ShopOpened:
                        case QuestUiScreenTargetType.KioskOpened:
                        case QuestUiScreenTargetType.RaceKioskOpened:
                            return $"Beacon + {InputPromptResolver.GetBindingDisplay(inputActions, "Interact")}";
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked: return $"Hold {InputPromptResolver.GetBindingDisplay(inputActions, "EquipSkis")} to stand";
                        case QuestInteractionTargetType.EquipSkis:
                        case QuestInteractionTargetType.UnequipSkis: return $"Hold {InputPromptResolver.GetBindingDisplay(inputActions, "EquipSkis")}";
                        case QuestInteractionTargetType.ClaimDefaultPass: return $"{InputPromptResolver.GetBindingDisplay(inputActions, "Interact")} + Claim";
                        case QuestInteractionTargetType.ExitResort: return "Leave resort";
                        default: return $"Beacon + {InputPromptResolver.GetBindingDisplay(inputActions, "Interact")}";
                    }

                case QuestObjectiveTemplate.TravelDistance:
                    return BuildTravelPrompt(objectiveDefinition);

                case QuestObjectiveTemplate.Activity:
                    switch (objectiveDefinition.activityTarget)
                    {
                        case QuestActivityTargetType.RaceStarted: return "Kiosk -> Start gate";
                        case QuestActivityTargetType.RaceCompleted: return "Finish the race";
                        case QuestActivityTargetType.RescueStarted: return "Medic tent -> Accept";
                        case QuestActivityTargetType.RescueCompleted: return "Finish rescue";
                    }
                    break;
            }

            return objectiveDefinition.BuildPromptLabel();
        }

        private string BuildOverlayRoutePrompt(string tabName)
        {
            string toggle = GetOverlayBindingDisplay();
            return string.IsNullOrWhiteSpace(tabName) ? toggle : $"{toggle} + {tabName}";
        }

        private string BuildTravelPrompt(QuestObjectiveDefinition objectiveDefinition)
        {
            string summary = $"{objectiveDefinition.title} {objectiveDefinition.description} {objectiveDefinition.BuildAuthoringSummary()}";
            if (summary.IndexOf("carve", StringComparison.OrdinalIgnoreCase) >= 0)
                return $"Hold {InputPromptResolver.CombineBindings(inputActions, "LeftSki", "RightSki")} while moving";

            return "Keep skiing";
        }

        private InputPromptToken[] BuildDetailedPromptTokens(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return InputPromptTokens.Text("-");

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.LeanForward: return Action("Lean", "positive", "up", "forward");
                        case QuestInputActionType.LeanBackward: return Action("Lean", "negative", "down", "back", "backward");
                        case QuestInputActionType.Tuck: return HoldAction("Sprint");
                        case QuestInputActionType.PolePush: return Join(Action("Poles"), "on flats");
                        case QuestInputActionType.PoleDrag: return Join(Action("Poles"), "while moving");
                        case QuestInputActionType.AirPose: return Join(Action("Jump"), "+", Action("Poles"));
                    }
                    break;

                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.Wedge: return Join(Action("LeftSki"), "+", Action("RightSki"));
                        case QuestMovementSkillType.SkateSwitch: return Join(Action("Lean", "positive", "up", "forward"), "+", Join(Action("LeftSki"), "/", Action("RightSki")));
                        case QuestMovementSkillType.QuickStop: return Join(Action("Lean", "negative", "down", "back", "backward"), "+", Join(Action("LeftSki"), "/", Action("RightSki")));
                        case QuestMovementSkillType.JumpAdjust: return Join(Join(Action("Jump"), "+", LeanAxis()), "+", TurnAxis());
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    switch (objectiveDefinition.uiTarget)
                    {
                        case QuestUiScreenTargetType.OverlayOpened: return Action("Toggle");
                        case QuestUiScreenTargetType.MapViewed: return Join(Action("Toggle"), "->", InputPromptTokens.Text("Map"));
                        case QuestUiScreenTargetType.StatsViewed: return Join(Action("Toggle"), "->", InputPromptTokens.Text("Stats"));
                        case QuestUiScreenTargetType.TasksViewed: return Join(Action("Toggle"), "->", InputPromptTokens.Text("Goals"), "->", InputPromptTokens.Text("Tasks"));
                        case QuestUiScreenTargetType.QuestsViewed: return Join(Action("Toggle"), "->", InputPromptTokens.Text("Goals"), "->", InputPromptTokens.Text("Quests"));
                        case QuestUiScreenTargetType.TaskRewardClaimed: return Join(Action("Toggle"), "->", InputPromptTokens.Text("Goals"), "->", InputPromptTokens.Text("Tasks"), "->", InputPromptTokens.Text("Claim"));
                        case QuestUiScreenTargetType.ShopOpened:
                        case QuestUiScreenTargetType.KioskOpened:
                        case QuestUiScreenTargetType.RaceKioskOpened:
                            return Join(InputPromptTokens.Text("Beacon"), "+", Action("Interact"));
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked: return InputPromptTokens.Text("Stack / topple");
                        case QuestInteractionTargetType.EquipSkis:
                        case QuestInteractionTargetType.UnequipSkis: return HoldAction("EquipSkis");
                        case QuestInteractionTargetType.ClaimDefaultPass: return Join(Action("Interact"), "+", InputPromptTokens.Text("Claim"));
                        case QuestInteractionTargetType.ExitResort: return InputPromptTokens.Text("Leave resort");
                        default: return Join(InputPromptTokens.Text("Beacon"), "+", Action("Interact"));
                    }

                case QuestObjectiveTemplate.TravelDistance:
                    return InputPromptTokens.Text("Keep moving");

                case QuestObjectiveTemplate.Activity:
                    switch (objectiveDefinition.activityTarget)
                    {
                        case QuestActivityTargetType.RaceStarted: return InputPromptTokens.Text("Kiosk -> Start gate");
                        case QuestActivityTargetType.RaceCompleted: return InputPromptTokens.Text("Finish the race");
                        case QuestActivityTargetType.RescueStarted: return InputPromptTokens.Text("Medic tent -> Accept");
                        case QuestActivityTargetType.RescueCompleted: return InputPromptTokens.Text("Finish rescue");
                    }
                    break;
            }

            return InputPromptTokens.Text(objectiveDefinition.BuildPromptLabel());
        }

        private InputPromptToken[] Action(string actionName, params string[] compositePartNames)
        {
            return InputPromptResolver.ResolveActionTokens(inputActions, actionName, iconLibrary, compositePartNames);
        }

        private InputPromptToken[] HoldAction(string actionName, params string[] compositePartNames)
        {
            return InputPromptTokens.Phrase("Hold", Action(actionName, compositePartNames));
        }

        private InputPromptToken[] LeanAxis()
        {
            return Join(Action("Lean", "positive", "up", "forward"), "/", Action("Lean", "negative", "down", "back", "backward"));
        }

        private InputPromptToken[] TurnAxis()
        {
            return Join(Action("LeftSki"), "/", Action("RightSki"));
        }

        private static InputPromptToken[] Join(IReadOnlyList<InputPromptToken> left, string separator, IReadOnlyList<InputPromptToken> right)
        {
            return InputPromptTokens.Concat(left, new[] { InputPromptToken.Separator(separator) }, right);
        }

        private static InputPromptToken[] Join(
    IReadOnlyList<InputPromptToken> first,
    string separator1,
    IReadOnlyList<InputPromptToken> second,
    string separator2,
    IReadOnlyList<InputPromptToken> third,
    string separator3,
    IReadOnlyList<InputPromptToken> fourth)
        {
            return InputPromptTokens.Concat(
                first,
                new[] { InputPromptToken.Separator(separator1) },
                second,
                new[] { InputPromptToken.Separator(separator2) },
                third,
                new[] { InputPromptToken.Separator(separator3) },
                fourth);
        }

        private static InputPromptToken[] Join(IReadOnlyList<InputPromptToken> left, string separator1, IReadOnlyList<InputPromptToken> middle, string separator2, IReadOnlyList<InputPromptToken> right)
        {
            return InputPromptTokens.Concat(left, new[] { InputPromptToken.Separator(separator1) }, middle, new[] { InputPromptToken.Separator(separator2) }, right);
        }

        private static InputPromptToken[] Join(IReadOnlyList<InputPromptToken> left, string trailingText)
        {
            return InputPromptTokens.Concat(left, InputPromptTokens.Text(trailingText));
        }

        private string CombineBindings(params string[] actionNames)
        {
            return InputPromptResolver.CombineBindings(inputActions, actionNames);
        }

        private string GetBindingDisplay(string actionName, params string[] compositePartNames)
        {
            return InputPromptResolver.GetBindingDisplay(inputActions, actionName, compositePartNames);
        }

        private string GetRaceCycleLeftBindingDisplay()
        {
            return FirstNonEmptyBindingDisplay("CycleUILeft", "NavigateLeft", "Previous", "Q");
        }

        private string GetRaceCycleRightBindingDisplay()
        {
            return FirstNonEmptyBindingDisplay("CycleUIRight", "NavigateRight", "Next", "E");
        }

        private string FirstNonEmptyBindingDisplay(string primaryActionName, string secondaryActionName, string tertiaryActionName, string fallback)
        {
            string value = GetBindingDisplay(primaryActionName);
            if (!string.IsNullOrWhiteSpace(value) && value != "-")
                return value;

            value = GetBindingDisplay(secondaryActionName);
            if (!string.IsNullOrWhiteSpace(value) && value != "-")
                return value;

            value = GetBindingDisplay(tertiaryActionName);
            if (!string.IsNullOrWhiteSpace(value) && value != "-")
                return value;

            return fallback;
        }

        private string GetOverlayBindingDisplay()
        {
            return overlayController != null
                ? overlayController.GetOverlayToggleBindingDisplay(inputActions)
                : InputPromptResolver.GetBindingDisplay(inputActions, "Toggle");
        }

        private InputPromptToken[] BuildQuestFooterHintTokens()
        {
            return InputPromptTokens.Text("Scroll: Cycle  •  MMB: List");
        }

        private static float GetQuestProgress01(QuestRuntimeState state)
        {
            if (state == null)
                return 0f;

            if (state.completed)
                return 1f;

            if (state.objectiveStates == null || state.objectiveStates.Count == 0)
                return 0f;

            float total = 0f;
            int count = 0;
            for (int i = 0; i < state.objectiveStates.Count; i++)
            {
                var objective = state.objectiveStates[i];
                if (objective == null)
                    continue;

                total += Mathf.Clamp01(objective.progress01);
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private void RefreshTimeWeather()
        {
            if (_lblMiniTime != null)
                _lblMiniTime.text = FormatTime();

            if (_lblMiniWeather != null)
                _lblMiniWeather.text = FormatWeather();
        }

        private MapRegionFace ResolveCurrentRegionDirect()
        {
            var set = ResolveRegionSet();
            if (set == null || skiController == null)
                return null;

            if (set.MapData == null && mapData != null)
                set.SetMapData(mapData);

            if (set.MapData == null)
                return null;

            set.EnsureInitialized();

            Vector2 uv = set.MapData.WorldToMapUV(skiController.transform.position);
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);

            return MapRegionUtility.ResolveRegion(set, uv);
        }

        private string ResolveCurrentRunName()
        {
            if (runProgressTracker != null && runProgressTracker.TryGetActiveProgress(out var active))
            {
                if (!string.IsNullOrWhiteSpace(active.runName))
                    return active.runName;
            }

            if (mapData != null && skiController != null &&
                mapData.TryResolveRunAtWorldPosition(skiController.transform.position, out var corridor))
            {
                if (!string.IsNullOrWhiteSpace(corridor.displayName))
                    return corridor.displayName;

                if (!string.IsNullOrWhiteSpace(corridor.id))
                    return corridor.id;
            }

            return null;
        }

        private void RefreshRegionPanel()
        {
            if (_lblRunTitle == null || _lblRunBody == null)
                return;

            if (_miniRunPanel != null)
                _miniRunPanel.style.display = DisplayStyle.Flex;

            string regionName = null;
            string runName = ResolveCurrentRunName();

            var directRegion = ResolveCurrentRegionDirect();
            if (directRegion != null)
            {
                regionName = directRegion.displayName;
            }
            else if (playerRegionTracker != null && playerRegionTracker.CurrentRegion != null)
            {
                regionName = playerRegionTracker.CurrentRegion.displayName;
            }

            if (!string.IsNullOrWhiteSpace(runName) && !string.IsNullOrWhiteSpace(regionName))
            {
                _lblRunTitle.text = $"{runName}, {regionName}";
                _lblRunBody.text = BuildRegionSubtitle(regionName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(runName))
            {
                _lblRunTitle.text = runName;
                _lblRunBody.text = "On a mapped ski run";
                return;
            }

            if (!string.IsNullOrWhiteSpace(regionName))
            {
                _lblRunTitle.text = regionName;
                _lblRunBody.text = BuildRegionSubtitle(regionName);
                return;
            }

            _lblRunTitle.text = "Unknown Region";
            _lblRunBody.text = "Move around the mountain to discover named areas";
        }

        private void DrawRunBannerSegments(RunProgressTracker.ActiveRunProgress active)
        {
            _runBannerSegments.Clear();

            var intervals = active.coverageIntervals;
            if (intervals == null || intervals.Length == 0)
            {
                AddRunBannerSegment(active.entryFraction01, active.currentFraction01);
                return;
            }

            for (int i = 0; i < intervals.Length; i++)
                AddRunBannerSegment(intervals[i].startFraction01, intervals[i].endFraction01);
        }

        private void AddRunBannerSegment(float start01, float end01)
        {
            float lo = Mathf.Clamp01(Mathf.Min(start01, end01));
            float hi = Mathf.Clamp01(Mathf.Max(start01, end01));

            if (hi - lo <= 0.0005f)
                return;

            var seg = new VisualElement();
            seg.AddToClassList("run-banner-segment");
            seg.style.left = Length.Percent(lo * 100f);
            seg.style.width = Length.Percent((hi - lo) * 100f);

            _runBannerSegments.Add(seg);
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

        private string BuildRegionSubtitle(string currentRegionName)
        {
            if (runProgressTracker != null && runProgressTracker.TryGetActiveProgress(out var active))
            {
                float pct = Mathf.Clamp01(active.currentFraction01) * 100f;
                return $"On {active.runName} · {Mathf.RoundToInt(pct)}% complete";
            }

            string nearestRun = FindNearestRunName();
            if (!string.IsNullOrWhiteSpace(nearestRun))
                return $"Nearest run · {nearestRun}";

            return "Explore the mountain";
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

            return $"{bestName} · {Mathf.Sqrt(bestSq):0} m";
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
