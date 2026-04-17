using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Audio;
using SkiGame.Progression;
using SkiGame.UI;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.POI;

[RequireComponent(typeof(UIDocument))]
[DisallowMultipleComponent]
public class SkiPassKioskUI : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private StyleSheet styleSheet;

    [Header("References")]
    [SerializeField] private SkiPassManager skiPassManager;
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private MiniMountainHudController miniHudController;

    [Header("Map")]
    [SerializeField] private MapData mapData;
    [SerializeField] private Camera mapReferenceCamera;
    [SerializeField] private MapUIStyleSettings mapStyle;
    [SerializeField] private PointOfInterestRegistry poiRegistry;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MapRegionSet regionSet;

    private VisualElement _root;
    private VisualElement _panel;
    private Button _btnClose;
    private Label _lblCurrentPass;
    private Label _lblExpiry;
    private Label _lblOwnedPasses;
    private Label _lblQuote;
    private Label _lblResult;
    private VisualElement _passList;
    private VisualElement _durationList;
    private ScrollView _liftList;
    private Button _btnPurchase;
    private Label _lblCurrentSelection;
    private VisualElement _currentPassPanel;
    private VisualElement _currentPassList;

    private VisualElement _mapHost;
    private Label _lblSelectedLiftName;
    private Label _lblSelectedLiftStatus;
    private Label _lblSelectedLiftPassRequirement;
    private VisualElement _selectedLiftPassList;
    private VisualElement _selectedLiftOverlay;

    private VisualElement _claimOverlay;
    private Label _lblClaimOverlayTitle;
    private Label _lblClaimOverlayBody;
    private Button _btnClaimOverlay;

    private PhoneMapPageUI _mapUI;
    private LiftLine _selectedLift;

    private int _selectedLevel = -1;
    private int _selectedDurationIndex = 0;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool IsBlocked => false;

    private void Reset()
    {
        if (document == null)
            document = GetComponent<UIDocument>();
    }

    private void Awake()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (skiPassManager == null)
            skiPassManager = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();

        if (statsManager == null)
            statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();

        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        if (mapData == null)
            mapData = FindObjectOfType<MapData>();

        if (regionSet == null)
            regionSet = ResolveRegionSet();

        if (mapReferenceCamera == null)
            mapReferenceCamera = AutoFindMapReferenceCamera();

        if (poiRegistry == null)
            poiRegistry = FindObjectOfType<PointOfInterestRegistry>();

        if (playerTransform == null)
            playerTransform = ResolvePlayerTransform();

        _root = document != null ? document.rootVisualElement : null;
        if (_root == null)
            return;

        if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
            _root.styleSheets.Add(styleSheet);

        _panel = _root.Q<VisualElement>("SkiPassKioskPanel");
        _btnClose = _root.Q<Button>("Btn_CloseSkiPassKiosk");
        _lblCurrentPass = _root.Q<Label>("Lbl_CurrentPass");
        _lblExpiry = _root.Q<Label>("Lbl_PassExpiry");
        _lblOwnedPasses = _root.Q<Label>("Lbl_PermanentPasses");
        _lblQuote = _root.Q<Label>("Lbl_PassQuote");
        _lblResult = _root.Q<Label>("Lbl_PassResult");
        _passList = _root.Q<VisualElement>("PassLevelList");
        _durationList = _root.Q<VisualElement>("DurationList");
        _liftList = _root.Q<ScrollView>("UnlockedLiftList");
        _btnPurchase = _root.Q<Button>("Btn_PurchasePass");
        _lblCurrentSelection = _root.Q<Label>("Lbl_CurrentSelection");
        _currentPassPanel = _root.Q<VisualElement>("CurrentPassPanel");

        _mapHost = _root.Q<VisualElement>("LiftMapHost");
        _lblSelectedLiftName = _root.Q<Label>("Lbl_SelectedLiftName");
        _lblSelectedLiftStatus = _root.Q<Label>("Lbl_SelectedLiftStatus");
        _lblSelectedLiftPassRequirement = _root.Q<Label>("Lbl_SelectedLiftPassRequirement");
        _selectedLiftPassList = _root.Q<VisualElement>("SelectedLiftPassList");

        _claimOverlay = _root.Q<VisualElement>("ClaimPassOverlay");
        _lblClaimOverlayTitle = _root.Q<Label>("Lbl_ClaimPassTitle");
        _lblClaimOverlayBody = _root.Q<Label>("Lbl_ClaimPassBody");
        _btnClaimOverlay = _root.Q<Button>("Btn_ClaimPassOverlay");

        if (_btnClaimOverlay != null)
        {
            _btnClaimOverlay.clicked += () =>
            {
                if (skiPassManager == null || skiPassManager.Config == null)
                    return;

                _selectedLevel = skiPassManager.Config.defaultLevelIndex;
                PurchaseSelected();
            };
        }

        if (_currentPassPanel != null)
        {
            _currentPassList = _currentPassPanel.Q<VisualElement>("CurrentPassList");
            if (_currentPassList == null)
            {
                _currentPassList = new VisualElement();
                _currentPassList.name = "CurrentPassList";
                _currentPassList.AddToClassList("ski-pass-current-list");
                _currentPassPanel.Add(_currentPassList);
            }
        }

        if (_btnClose != null)
            _btnClose.clicked += Close;

        if (_btnPurchase != null)
            _btnPurchase.clicked += PurchaseSelected;

        HideImmediate();
    }

    private void OnEnable()
    {
        if (skiPassManager != null)
            skiPassManager.OnPassChanged += HandlePassChanged;
    }

    private void OnDisable()
    {
        if (skiPassManager != null)
            skiPassManager.OnPassChanged -= HandlePassChanged;

        if (_mapUI != null)
            _mapUI.PolylineSelected -= HandleMapPolylineSelected;

        ApplyCursorAndCameraState(false);
        GameplayModalMovementLock.SetLocked(this, false);
    }

    private void Update()
    {
        if (!_isOpen || _mapUI == null)
            return;

        _mapUI.Tick(Time.unscaledDeltaTime);
    }

    private void HandlePassChanged()
    {
        if (!_isOpen)
            return;

        SyncSelectedPassToCurrent();
        RefreshAll();
    }

    public void Open()
    {
        if (_root == null)
            return;

        bool wasOpen = _isOpen;
        _isOpen = true;
        SyncSelectedPassToCurrent();
        _selectedDurationIndex = 0;

        _root.style.display = DisplayStyle.Flex;
        _root.style.visibility = Visibility.Visible;
        _root.style.opacity = 1f;
        _root.pickingMode = PickingMode.Position;

        ApplyCursorAndCameraState(true);
        GameplayModalMovementLock.SetLocked(this, true);

        if (miniHudController != null)
            miniHudController.SetVisible(false);

        if (!wasOpen)
        {
            GameAudio.PlayUi(GameAudioCueId.UiOpen);
            RaiseQuestUiEvent("ui.kiosk.opened");
        }

        EnsureMapBound();
        RefreshAll();
    }

    public void Close()
    {
        bool wasOpen = _isOpen;
        _isOpen = false;
        HideImmediate();

        ApplyCursorAndCameraState(false);
        GameplayModalMovementLock.SetLocked(this, false);

        if (miniHudController != null)
            miniHudController.SetVisible(true);

        if (wasOpen)
            GameAudio.PlayUi(GameAudioCueId.UiClose);
    }

    private static void RaiseQuestUiEvent(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var signalBus = FindObjectOfType<SkiGame.Progression.QuestSignalBus>();
        signalBus?.RaiseEvent(key);
    }

    private void HideImmediate()
    {
        if (_root == null)
            return;

        _root.style.display = DisplayStyle.None;
        _root.style.visibility = Visibility.Hidden;
        _root.style.opacity = 0f;
        _root.pickingMode = PickingMode.Ignore;
    }

    private void ApplyCursorAndCameraState(bool open)
    {
        if (open)
            GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 850);
        else
            GameCursorService.Release(this);

        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        if (cameraController != null)
            cameraController.SetExternalUiLookLock(open);
    }

    private void EnsureMapBound()
    {
        if (_mapHost == null || mapData == null)
            return;

        if (_mapUI != null)
        {
            _mapUI.PolylineSelected -= HandleMapPolylineSelected;

            _mapUI.SetPlayer(playerTransform);
            _mapUI.SetOverlayVisibility(showRuns: true, showLifts: true, showPOIs: false);
            _mapUI.SetShowStandardPOIOverlays(false);
            _mapUI.SetActivityOverlayVisibility(showRaceStarts: true, showMedicTents: false, showSnowmobiles: false);
            _mapUI.SetSelectionFilters(
                marker => marker.type == POIType.SkiLift,
                polyline => polyline.IsValid && polyline.lineType == MapLineType.SkiLift);
            _mapUI.SetLegendVisible(false);
            _mapUI.SetRegionInteractivity(selectable: false, showLabels: true);

            if (regionSet != null)
                _mapUI.SetRegionSet(regionSet);

            _mapUI.PolylineSelected += HandleMapPolylineSelected;
            _mapUI.Refresh();
            _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.15f);
            return;
        }

        _mapHost.Clear();

        VisualElement runtimeMapRoot = MountainHudMapRootFactory.BuildOverlayMapRoot("SkiPassKioskMapRoot");
        _mapHost.Add(runtimeMapRoot);

        _mapUI = new PhoneMapPageUI();
        _mapUI.Bind(runtimeMapRoot, mapData, mapReferenceCamera);

        if (regionSet != null)
            _mapUI.SetRegionSet(regionSet);

        if (mapStyle != null)
            _mapUI.ApplyStyle(mapStyle);

        if (poiRegistry != null)
            _mapUI.SetPOIRegistry(poiRegistry);

        _mapUI.SetPlayer(playerTransform);
        _mapUI.SetPlayerTracking(showMarker: true, drawTrail: false);
        _mapUI.SetMinimapMode(false, followPlayer: false, lockPan: false, allowZoom: true, suppressSelection: false, hideMarkerLabels: false);
        _mapUI.SetOverlayVisibility(showRuns: true, showLifts: true, showPOIs: false);
        _mapUI.SetShowStandardPOIOverlays(false);
        _mapUI.SetActivityOverlayVisibility(showRaceStarts: true, showMedicTents: false, showSnowmobiles: false);
        _mapUI.SetSelectionFilters(
            marker => marker.type == POIType.SkiLift,
            polyline => polyline.IsValid && polyline.lineType == MapLineType.SkiLift);
        _mapUI.SetLegendVisible(false);
        _mapUI.SetRegionInteractivity(selectable: false, showLabels: true);

        _mapUI.PolylineSelected -= HandleMapPolylineSelected;
        _mapUI.PolylineSelected += HandleMapPolylineSelected;
    }

    private bool _handlingMapPolylineSelection;

    private void HandleMapPolylineSelected(MapPolyline polyline)
    {
        if (!polyline.IsValid || polyline.lineType != MapLineType.SkiLift)
            return;

        if (_handlingMapPolylineSelection)
            return;

        try
        {
            _handlingMapPolylineSelection = true;
            SelectLiftById(polyline.id, centerMap: true);
        }
        finally
        {
            _handlingMapPolylineSelection = false;
        }
    }

    private void RefreshAll()
    {
        EnsureValidSelectedPass();
        RebuildPassButtons();
        RebuildDurationButtons();
        RefreshQuoteLiftListAndMap();
        RefreshCurrentPass();
        RefreshSelectedLiftDetails();
        RefreshClaimOverlay();
    }

    private void EnsureValidSelectedPass()
    {
        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null || cfg.levels == null || cfg.levels.Length == 0)
        {
            _selectedLevel = -1;
            return;
        }

        if (_selectedLevel >= 0 && _selectedLevel < cfg.levels.Length && cfg.Get(_selectedLevel) != null)
            return;

        SyncSelectedPassToCurrent();
    }

    private void SyncSelectedPassToCurrent()
    {
        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null || cfg.levels == null || cfg.levels.Length == 0)
        {
            _selectedLevel = -1;
            return;
        }

        int currentLevel = skiPassManager != null ? skiPassManager.CurrentLevel : cfg.defaultLevelIndex;
        _selectedLevel = cfg.ClampLevel(currentLevel);
    }

    private SkiPassConfigSO.PassLevel GetSelectedPass()
    {
        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null || _selectedLevel < 0)
            return null;

        return cfg.Get(_selectedLevel);
    }

    private string GetSelectedPassId()
    {
        return skiPassManager != null && skiPassManager.Config != null && _selectedLevel >= 0
            ? skiPassManager.Config.GetPassIdForLevel(_selectedLevel)
            : string.Empty;
    }

    private string ResolvePreviewPassId(string selectedPassId, int selectedLevel)
    {
        if (!string.IsNullOrWhiteSpace(selectedPassId))
            return selectedPassId.Trim();

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return string.Empty;

        if (selectedLevel < 0)
            return string.Empty;

        return cfg.GetPassIdForLevel(cfg.ClampLevel(selectedLevel)) ?? string.Empty;
    }

    private bool IsLiftUnlockedForPreview(LiftLine lift, string selectedPassId, int selectedLevel)
    {
        if (lift == null)
            return false;

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return selectedLevel >= Mathf.Max(0, lift.RequiredPassLevel);

        string resolvedPreviewPassId = ResolvePreviewPassId(selectedPassId, selectedLevel);

        if (!string.IsNullOrWhiteSpace(resolvedPreviewPassId) &&
            TryResolveLiftRequiredPassId(lift, out string resolvedLiftPassId))
        {
            return cfg.PassGrantsAccessTo(resolvedPreviewPassId, resolvedLiftPassId);
        }

        return selectedLevel >= Mathf.Max(0, lift.RequiredPassLevel);
    }

    private string GetPassDisplayName(int levelIndex)
    {
        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        var pass = cfg != null ? cfg.Get(levelIndex) : null;
        if (pass == null)
            return "Unknown Pass";

        return string.IsNullOrWhiteSpace(pass.displayName) ? $"Pass {levelIndex}" : pass.displayName;
    }

    private string BuildPermanentOwnedSummary()
    {
        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return "Permanent ownership: unavailable";

        HashSet<string> owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> displayNames = new List<string>();

        List<string> idUnlocks = RaceRescueProgression.GetPermanentlyUnlockedPassIds();
        if (idUnlocks != null)
        {
            for (int i = 0; i < idUnlocks.Count; i++)
            {
                string passId = idUnlocks[i];
                if (string.IsNullOrWhiteSpace(passId))
                    continue;

                string normalized = passId.Trim();
                if (!owned.Add(normalized))
                    continue;

                displayNames.Add(cfg.GetDisplayNameForPassId(normalized));
            }
        }

        var profile = statsManager != null ? statsManager.Profile : null;
        List<int> legacyLevels = profile != null ? profile.permanentlyUnlockedPassLevels : null;
        if (legacyLevels != null)
        {
            for (int i = 0; i < legacyLevels.Count; i++)
            {
                string passId = cfg.GetPassIdForLevel(legacyLevels[i]);
                if (string.IsNullOrWhiteSpace(passId))
                    continue;

                string normalized = passId.Trim();
                if (!owned.Add(normalized))
                    continue;

                displayNames.Add(cfg.GetDisplayNameForPassId(normalized));
            }
        }

        if (displayNames.Count == 0)
            return "Permanent ownership: none";

        return $"Permanent ownership: {string.Join(", ", displayNames)}";
    }

    private void AddGrantedPassIds(HashSet<string> results, string sourcePassId)
    {
        if (results == null || string.IsNullOrWhiteSpace(sourcePassId))
            return;

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        string normalized = sourcePassId.Trim();

        if (cfg == null)
        {
            results.Add(normalized);
            return;
        }

        HashSet<string> granted = cfg.ResolveGrantedAccessPassIds(normalized);
        if (granted == null || granted.Count == 0)
        {
            results.Add(normalized);
            return;
        }

        foreach (string passId in granted)
        {
            if (!string.IsNullOrWhiteSpace(passId))
                results.Add(passId.Trim());
        }
    }

    private HashSet<string> BuildOwnedAccessPassIds()
    {
        if (skiPassManager != null)
            return skiPassManager.BuildAccessiblePassIdSet();

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private bool TryResolveLiftRequiredPassId(LiftLine lift, out string requiredPassId)
    {
        requiredPassId = string.Empty;

        if (lift == null)
            return false;

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return false;

        if (!string.IsNullOrWhiteSpace(lift.RequiredPassId))
        {
            string normalized = lift.RequiredPassId.Trim();
            int index = cfg.GetLevelIndexByPassId(normalized);
            if (index >= 0)
            {
                requiredPassId = normalized;
                return true;
            }

            Debug.LogWarning(
                $"[SkiPassKioskUI] Lift '{lift.name}' has RequiredPassId='{normalized}' but it was not found in SkiPassManager.Config. " +
                $"Falling back to RequiredPassLevel={lift.RequiredPassLevel}. Active config asset: '{cfg.name}'.",
                lift);
        }

        string fallback = cfg.GetPassIdForLevel(Mathf.Max(0, lift.RequiredPassLevel));
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            requiredPassId = fallback.Trim();
            return true;
        }

        Debug.LogWarning(
            $"[SkiPassKioskUI] Lift '{lift.name}' could not resolve a required pass from either RequiredPassId or RequiredPassLevel.",
            lift);

        return false;
    }

    private int ResolveRequiredPassLevelIndex(LiftLine lift)
    {
        if (lift == null)
            return 0;

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return Mathf.Max(0, lift.RequiredPassLevel);

        if (TryResolveLiftRequiredPassId(lift, out string resolvedPassId))
        {
            int byId = cfg.GetLevelIndexByPassId(resolvedPassId);
            if (byId >= 0)
                return byId;
        }

        return Mathf.Max(0, lift.RequiredPassLevel);
    }

    private Color GetLiftRequiredPassColor(LiftLine lift)
    {
        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return new Color(0.36f, 0.77f, 1f, 1f);

        int requiredLevel = ResolveRequiredPassLevelIndex(lift);
        var pass = cfg.Get(requiredLevel);
        if (pass != null && pass.mapColor.a > 0.001f)
            return pass.mapColor;

        return new Color(0.36f, 0.77f, 1f, 1f);
    }

    private void ApplyKioskLiftRequirementOverridesToMap()
    {
        if (_mapUI == null || mapData == null || mapData.Polylines == null)
            return;

        _mapUI.ClearForcedLiftRequirementOverrides();

        List<LiftLine> lifts = GetAllLiftsSorted();
        if (lifts == null || lifts.Count == 0)
            return;

        for (int i = 0; i < lifts.Count; i++)
        {
            LiftLine lift = lifts[i];
            if (lift == null)
                continue;

            if (!TryGetLiftPolylineId(lift, out string polylineId) || string.IsNullOrWhiteSpace(polylineId))
                continue;

            int requiredLevel = ResolveRequiredPassLevelIndex(lift);

            string requiredPassId = string.Empty;
            if (TryResolveLiftRequiredPassId(lift, out string resolvedPassId))
                requiredPassId = resolvedPassId;

            Color requiredColor = GetLiftRequiredPassColor(lift);

            _mapUI.SetForcedLiftRequirementOverride(polylineId, requiredLevel, requiredPassId, requiredColor);
        }
    }

    private bool IsLiftAccessibleWithOwnedPassIds(LiftLine lift, HashSet<string> ownedAccessPassIds)
    {
        if (lift == null)
            return false;

        if (ownedAccessPassIds != null && ownedAccessPassIds.Count > 0)
        {
            if (TryResolveLiftRequiredPassId(lift, out string resolvedPassId))
                return ownedAccessPassIds.Contains(resolvedPassId);
        }

        return skiPassManager != null && skiPassManager.CanUseLift(lift);
    }

    private bool TryGetNowGameHours(out double nowHours)
    {
        nowHours = 0.0;

        var t = TimeWeather.TimeController.instance != null
            ? TimeWeather.TimeController.instance
            : FindObjectOfType<TimeWeather.TimeController>();

        if (t == null)
            return false;

        int day = Mathf.Max(0, t.dayCount);
        int hh = Mathf.Clamp(t.timeHours, 0, 23);
        float mm = Mathf.Clamp((float)t.timeMinutes, 0f, 59f);

        nowHours = (day * 24.0) + hh + (mm / 60.0);
        return true;
    }

    private string FormatGameHoursTimestamp(double gameHours)
    {
        if (double.IsNaN(gameHours) || double.IsInfinity(gameHours))
            return string.Empty;

        int totalMinutes = Mathf.Max(0, Mathf.RoundToInt((float)(gameHours * 60.0)));
        int day = totalMinutes / (24 * 60);
        int minutesIntoDay = totalMinutes % (24 * 60);
        int hour = minutesIntoDay / 60;
        int minute = minutesIntoDay % 60;

        return $"Day {day + 1} • {hour:00}:{minute:00}";
    }

    private VisualElement BuildCurrentPassRow(
        string displayName,
        Color passColor,
        string statusText,
        bool showProgress,
        float progress01,
        string progressText,
        string tooltipText)
    {
        var row = new VisualElement();
        row.AddToClassList("ski-pass-current-row");
        row.tooltip = tooltipText ?? string.Empty;

        var left = new VisualElement();
        left.AddToClassList("ski-pass-current-row-left");

        var chip = new Label(displayName);
        chip.AddToClassList("ski-pass-current-chip");
        chip.style.backgroundColor = new Color(passColor.r, passColor.g, passColor.b, 0.18f);
        chip.style.borderLeftColor = new Color(passColor.r, passColor.g, passColor.b, 0.70f);
        chip.style.borderRightColor = new Color(passColor.r, passColor.g, passColor.b, 0.70f);
        chip.style.borderTopColor = new Color(passColor.r, passColor.g, passColor.b, 0.70f);
        chip.style.borderBottomColor = new Color(passColor.r, passColor.g, passColor.b, 0.70f);

        left.Add(chip);
        row.Add(left);

        var right = new VisualElement();
        right.AddToClassList("ski-pass-current-row-right");

        if (showProgress)
        {
            var progressWrap = new VisualElement();
            progressWrap.AddToClassList("ski-pass-current-progress-wrap");
            progressWrap.tooltip = tooltipText ?? string.Empty;

            var progressTrack = new VisualElement();
            progressTrack.AddToClassList("ski-pass-current-progress-track");

            var progressFill = new VisualElement();
            progressFill.AddToClassList("ski-pass-current-progress-fill");
            progressFill.style.width = Length.Percent(Mathf.Clamp01(progress01) * 100f);
            progressFill.style.backgroundColor = new Color(passColor.r, passColor.g, passColor.b, 0.92f);

            var progressLabel = new Label(progressText);
            progressLabel.AddToClassList("ski-pass-current-progress-label");

            progressTrack.Add(progressFill);
            progressWrap.Add(progressTrack);
            progressWrap.Add(progressLabel);
            right.Add(progressWrap);
        }
        else
        {
            var status = new Label(statusText);
            status.AddToClassList("ski-pass-current-status");
            right.Add(status);
        }

        row.Add(right);
        return row;
    }

    private void RebuildCurrentPassList()
    {
        if (_currentPassList == null || skiPassManager == null || skiPassManager.Config == null)
            return;

        _currentPassList.Clear();

        var cfg = skiPassManager.Config;
        HashSet<string> addedPassIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var activePasses = skiPassManager.ActivePasses;
        if (activePasses != null)
        {
            List<SkiPassManager.ActivePassRecord> sortedActive = new List<SkiPassManager.ActivePassRecord>();
            for (int i = 0; i < activePasses.Count; i++)
            {
                var record = activePasses[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.passId))
                    sortedActive.Add(record);
            }

            sortedActive.Sort((a, b) =>
            {
                int levelA = cfg.GetLevelIndexByPassId(a.passId);
                int levelB = cfg.GetLevelIndexByPassId(b.passId);

                int levelCmp = levelB.CompareTo(levelA);
                if (levelCmp != 0)
                    return levelCmp;

                return a.expiryGameHours.CompareTo(b.expiryGameHours);
            });

            for (int i = 0; i < sortedActive.Count; i++)
            {
                var record = sortedActive[i];
                string passId = record.passId.Trim();
                if (!addedPassIds.Add(passId))
                    continue;

                var pass = cfg.GetByPassId(passId);
                if (pass == null)
                    continue;

                double nowHours = 0.0;
                TryGetNowGameHours(out nowHours);

                double totalHours = Math.Max(0.0001, record.totalHoursPurchased);
                double remainingHours = Math.Max(0.0, record.expiryGameHours - nowHours);
                float progress01 = Mathf.Clamp01((float)(remainingHours / totalHours));

                string tooltip = $"Started: {FormatGameHoursTimestamp(record.startGameHours)}\nExpires: {FormatGameHoursTimestamp(record.expiryGameHours)}";

                _currentPassList.Add(BuildCurrentPassRow(
                    pass.displayName,
                    GetPassUiColor(pass),
                    statusText: string.Empty,
                    showProgress: true,
                    progress01: progress01,
                    progressText: $" Expires in {Mathf.CeilToInt((float)remainingHours)}h",
                    tooltipText: tooltip));
            }
        }

        List<string> permanentIds = RaceRescueProgression.GetPermanentlyUnlockedPassIds();
        if (permanentIds != null)
        {
            for (int i = 0; i < permanentIds.Count; i++)
            {
                string passId = permanentIds[i];
                if (string.IsNullOrWhiteSpace(passId))
                    continue;

                string normalized = passId.Trim();
                if (!addedPassIds.Add(normalized))
                    continue;

                var pass = cfg.GetByPassId(normalized);
                if (pass == null)
                    continue;

                _currentPassList.Add(BuildCurrentPassRow(
                    pass.displayName,
                    GetPassUiColor(pass),
                    "Permanent",
                    showProgress: false,
                    progress01: 1f,
                    progressText: string.Empty,
                    tooltipText: string.Empty));
            }
        }

        var profile = statsManager != null ? statsManager.Profile : null;
        List<int> legacyLevels = profile != null ? profile.permanentlyUnlockedPassLevels : null;
        if (legacyLevels != null)
        {
            for (int i = 0; i < legacyLevels.Count; i++)
            {
                string passId = cfg.GetPassIdForLevel(legacyLevels[i]);
                if (string.IsNullOrWhiteSpace(passId))
                    continue;

                string normalized = passId.Trim();
                if (!addedPassIds.Add(normalized))
                    continue;

                var pass = cfg.GetByPassId(normalized);
                if (pass == null)
                    continue;

                _currentPassList.Add(BuildCurrentPassRow(
                    pass.displayName,
                    GetPassUiColor(pass),
                    "Permanent",
                    showProgress: false,
                    progress01: 1f,
                    progressText: string.Empty,
                    tooltipText: string.Empty));
            }
        }

        if (skiPassManager.HasClaimedDefaultPass)
        {
            string defaultPassId = cfg.GetDefaultPassId();
            if (!string.IsNullOrWhiteSpace(defaultPassId) && addedPassIds.Add(defaultPassId.Trim()))
            {
                var pass = cfg.GetByPassId(defaultPassId);
                if (pass != null)
                {
                    _currentPassList.Add(BuildCurrentPassRow(
                        pass.displayName,
                        GetPassUiColor(pass),
                        "Permanent",
                        showProgress: false,
                        progress01: 1f,
                        progressText: string.Empty,
                        tooltipText: string.Empty));
                }
            }
        }

        if (_currentPassList.childCount == 0)
        {
            var empty = new Label("No active or owned passes");
            empty.AddToClassList("ski-pass-current-empty");
            _currentPassList.Add(empty);
        }
    }

    private void RefreshCurrentPass()
    {
        if (skiPassManager == null)
        {
            if (_lblCurrentSelection != null)
                _lblCurrentSelection.text = "No ski pass manager found.";

            if (_lblCurrentPass != null)
                _lblCurrentPass.style.display = DisplayStyle.None;

            if (_lblExpiry != null)
                _lblExpiry.style.display = DisplayStyle.None;

            if (_lblOwnedPasses != null)
                _lblOwnedPasses.style.display = DisplayStyle.None;

            if (_currentPassList != null)
                _currentPassList.Clear();

            return;
        }

        var previewPass = GetSelectedPass();
        string previewName = previewPass != null ? previewPass.displayName : skiPassManager.GetCurrentPassDisplayName();

        if (_lblCurrentSelection != null)
            _lblCurrentSelection.text = $"Previewing: {previewName}";

        // Hide the old single-line fields; the panel now renders through CurrentPassList.
        if (_lblCurrentPass != null)
            _lblCurrentPass.style.display = DisplayStyle.None;

        if (_lblExpiry != null)
            _lblExpiry.style.display = DisplayStyle.None;

        if (_lblOwnedPasses != null)
            _lblOwnedPasses.style.display = DisplayStyle.None;

        RebuildCurrentPassList();
    }

    private static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    private static Color Darken(Color c, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color(
            Mathf.Lerp(c.r, 0f, amount),
            Mathf.Lerp(c.g, 0f, amount),
            Mathf.Lerp(c.b, 0f, amount),
            c.a);
    }

    private Color GetPassUiColor(SkiPassConfigSO.PassLevel pass)
    {
        if (pass != null && pass.mapColor.a > 0.001f)
            return pass.mapColor;

        return new Color(0.36f, 0.77f, 1f, 1f);
    }

    private void ApplyPassButtonVisualState(
        Button btn,
        SkiPassConfigSO.PassLevel pass,
        bool isCurrent,
        bool isSelected,
        bool permanentlyUnlocked)
    {
        if (btn == null)
            return;

        Color passColor = GetPassUiColor(pass);

        btn.style.borderLeftColor = WithAlpha(passColor, isCurrent ? 0.95f : 0.55f);
        btn.style.borderRightColor = WithAlpha(passColor, isCurrent ? 0.95f : 0.55f);
        btn.style.borderTopColor = WithAlpha(passColor, isCurrent ? 0.95f : 0.55f);
        btn.style.borderBottomColor = WithAlpha(passColor, isCurrent ? 0.95f : 0.55f);

        btn.style.borderLeftWidth = isSelected || isCurrent ? 2f : 1f;
        btn.style.borderRightWidth = isSelected || isCurrent ? 2f : 1f;
        btn.style.borderTopWidth = isSelected || isCurrent ? 2f : 1f;
        btn.style.borderBottomWidth = isSelected || isCurrent ? 2f : 1f;

        if (isSelected)
        {
            btn.style.backgroundColor = WithAlpha(passColor, 0.88f);
            btn.style.color = Darken(passColor, 0.92f);
        }
        else if (permanentlyUnlocked)
        {
            btn.style.backgroundColor = WithAlpha(passColor, 0.24f);
            btn.style.color = Color.white;
        }
        else if (isCurrent)
        {
            float remaining = skiPassManager != null && skiPassManager.HasTimedPass
                ? skiPassManager.GetRemainingFraction01()
                : 1f;

            btn.style.backgroundColor = WithAlpha(passColor, Mathf.Lerp(0.18f, 0.42f, remaining));
            btn.style.color = Color.white;
        }
        else
        {
            btn.style.backgroundColor = WithAlpha(passColor, 0.12f);
            btn.style.color = new Color(0.92f, 0.96f, 1f, 1f);
        }
    }

    private void RebuildPassButtons()
    {
        if (_passList == null || skiPassManager == null || skiPassManager.Config == null)
            return;

        _passList.Clear();

        var cfg = skiPassManager.Config;
        HashSet<string> ownedAccessPassIds = BuildOwnedAccessPassIds();
        List<LiftLine> lifts = GetAllLiftsSorted();

        for (int i = 0; i < cfg.levels.Length; i++)
        {
            var pass = cfg.levels[i];
            if (pass == null)
                continue;

            int levelIndex = i;
            bool isCurrent = skiPassManager.IsPassActive(levelIndex);
            bool isSelected = levelIndex == _selectedLevel;
            bool permanentlyUnlocked = skiPassManager.IsPassPermanentlyUnlocked(levelIndex);
            bool isDefault = levelIndex == cfg.defaultLevelIndex;

            var block = new VisualElement();
            block.AddToClassList("ski-pass-pass-block");

            var btn = new Button(() =>
            {
                _selectedLift = null;
                _selectedLevel = levelIndex;
                RefreshAll();
            });

            string statusTag;
            if (isDefault && !skiPassManager.HasClaimedDefaultPass)
                statusTag = "FREE";
            else if (isCurrent)
                statusTag = skiPassManager.HasTimedPass ? skiPassManager.GetRemainingTimeString().ToUpperInvariant() : "ACTIVE";
            else if (permanentlyUnlocked)
                statusTag = "PERMANENT";
            else if (isSelected)
                statusTag = "PREVIEW";
            else
                statusTag = "LOCKED";

            btn.text = $"{pass.displayName}\n{statusTag}";
            btn.AddToClassList("ski-pass-option-button");

            if (isSelected)
                btn.AddToClassList("is-selected");

            if (isCurrent)
                btn.AddToClassList("is-current");

            ApplyPassButtonVisualState(btn, pass, isCurrent, isSelected, permanentlyUnlocked);
            block.Add(btn);

            string passId = cfg.GetPassIdForLevel(levelIndex);
            Debug.Log($"[SkiPassKioskUI] Pass card '{pass.displayName}' resolved passId='{passId}' at levelIndex={levelIndex} from config '{cfg.name}'.");

            var liftRow = new VisualElement();
            liftRow.AddToClassList("ski-pass-pass-lift-row");

            int chipCount = 0;
            for (int l = 0; l < lifts.Count; l++)
            {
                LiftLine lift = lifts[l];
                if (!LiftRequiresPassExactly(lift, passId, levelIndex))
                    continue;

                chipCount++;
                liftRow.Add(CreateLiftChipButton(lift, ownedAccessPassIds));
            }

            if (chipCount > 0)
                block.Add(liftRow);

            _passList.Add(block);
        }
    }

    private List<LiftLine> GetAllLiftsSorted()
    {
        var lifts = FindObjectsOfType<LiftLine>(includeInactive: false);
        List<LiftLine> results = new List<LiftLine>();

        if (lifts != null)
        {
            for (int i = 0; i < lifts.Length; i++)
            {
                if (lifts[i] != null)
                    results.Add(lifts[i]);
            }
        }

        results.Sort((a, b) =>
        {
            string ar = a != null ? a.GetRequiredPassDisplayName() : string.Empty;
            string br = b != null ? b.GetRequiredPassDisplayName() : string.Empty;
            int cmp = string.Compare(ar, br, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;

            return string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }

    private bool LiftRequiresPassExactly(LiftLine lift, string passId, int levelIndex)
    {
        if (lift == null)
            return false;

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null)
            return false;

        if (TryResolveLiftRequiredPassId(lift, out string resolvedLiftPassId))
        {
            string normalizedPassId = string.IsNullOrWhiteSpace(passId) ? string.Empty : passId.Trim();
            return string.Equals(resolvedLiftPassId, normalizedPassId, StringComparison.OrdinalIgnoreCase);
        }

        return Mathf.Max(0, lift.RequiredPassLevel) == Mathf.Max(0, levelIndex);
    }

    private Button CreateLiftChipButton(LiftLine lift, HashSet<string> ownedAccessPassIds)
    {
        var btn = new Button(() => SelectLift(lift, centerMap: true))
        {
            text = lift != null ? lift.name : "Lift"
        };

        btn.AddToClassList("ski-pass-lift-button");
        btn.AddToClassList("ski-pass-pass-lift-chip");

        if (lift == null)
            return btn;

        bool isSelected = _selectedLift == lift;
        bool accessibleNow = IsLiftAccessibleWithOwnedPassIds(lift, ownedAccessPassIds);
        Color requiredColor = GetLiftRequiredPassColor(lift);

        if (isSelected)
            btn.AddToClassList("is-selected");

        if (!accessibleNow)
            btn.AddToClassList("is-locked");

        float borderAlpha = isSelected ? 0.95f : (accessibleNow ? 0.78f : 0.64f);
        btn.style.borderLeftColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);
        btn.style.borderRightColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);
        btn.style.borderTopColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);
        btn.style.borderBottomColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);

        btn.style.borderLeftWidth = isSelected ? 2f : 1f;
        btn.style.borderRightWidth = isSelected ? 2f : 1f;
        btn.style.borderTopWidth = isSelected ? 2f : 1f;
        btn.style.borderBottomWidth = isSelected ? 2f : 1f;

        btn.style.backgroundColor = accessibleNow
            ? new Color(requiredColor.r, requiredColor.g, requiredColor.b, isSelected ? 0.24f : 0.14f)
            : new Color(requiredColor.r, requiredColor.g, requiredColor.b, isSelected ? 0.18f : 0.10f);

        return btn;
    }

    private void RebuildDurationButtons()
    {
        if (_durationList == null || skiPassManager == null || skiPassManager.Config == null)
            return;

        _durationList.Clear();

        var cfg = skiPassManager.Config;
        for (int i = 0; i < cfg.durations.Length; i++)
        {
            var dur = cfg.durations[i];
            if (dur == null)
                continue;

            int idx = i;
            var btn = new Button(() =>
            {
                _selectedDurationIndex = idx;
                RefreshAll();
            });

            btn.text = dur.label;
            btn.AddToClassList("ski-pass-option-button");

            if (idx == _selectedDurationIndex)
                btn.AddToClassList("is-selected");

            _durationList.Add(btn);
        }
    }

    private void RefreshQuoteLiftListAndMap()
    {
        if (skiPassManager == null || skiPassManager.Config == null)
            return;

        HashSet<string> ownedAccessPassIds = BuildOwnedAccessPassIds();
        if (_liftList != null)
            _liftList.style.display = DisplayStyle.None;

        if (_mapUI != null)
        {
            ApplyKioskLiftRequirementOverridesToMap();
            _mapUI.PreviewLiftAccessibilityByOwnedPassIds(ownedAccessPassIds);
            _mapUI.Refresh();

            if (_selectedLift != null && TryGetLiftPolylineId(_selectedLift, out string selectedPolyId))
            {
                _mapUI.SelectPolylineById(selectedPolyId, center: false, minZoom: -1f);
                _mapUI.FramePolylineById(selectedPolyId, paddingPx: 28f, minZoom: 0.85f);
            }
            else
            {
                _mapUI.FrameLiftPolylinesByOwnedPassIds(ownedAccessPassIds, paddingPx: 36f, minZoom: 0.85f);
            }

            _mapUI.ForceOverlayLabelRefresh();
        }

        if (_lblResult != null)
            _lblResult.text = string.Empty;

        if (_btnPurchase != null)
            _btnPurchase.SetEnabled(false);

        int freeLevel = skiPassManager.Config.defaultLevelIndex;
        if (_selectedLevel == freeLevel)
        {
            bool alreadyClaimed = skiPassManager.HasClaimedDefaultPass;

            if (_lblQuote != null)
                _lblQuote.text = alreadyClaimed
                    ? "The default pass is already available and never expires."
                    : "Claim the default pass for free. It never expires.";

            if (_btnPurchase != null)
            {
                _btnPurchase.text = alreadyClaimed ? "Owned" : "Claim Free Pass";
                _btnPurchase.SetEnabled(!alreadyClaimed);
            }

            return;
        }

        if (skiPassManager.IsPassPermanentlyUnlocked(_selectedLevel))
        {
            if (_lblQuote != null)
                _lblQuote.text = "This regional pass is already permanently owned.";

            if (_btnPurchase != null)
            {
                _btnPurchase.text = "Permanently Owned";
                _btnPurchase.SetEnabled(false);
            }

            return;
        }

        if (skiPassManager.TryQuotePurchase(_selectedLevel, _selectedDurationIndex, out var quote, out string reason))
        {
            string mode = quote.isUpgrade ? "Upgrade" : (quote.isExtend ? "Extend" : "Purchase");
            string credit = quote.credit > 0 ? $" • Credit {quote.credit}" : string.Empty;

            if (_lblQuote != null)
                _lblQuote.text = $"{mode}: {quote.finalCost}{credit}";

            if (_btnPurchase != null)
            {
                _btnPurchase.text = mode;
                _btnPurchase.SetEnabled(true);
            }
        }
        else
        {
            if (_lblQuote != null)
                _lblQuote.text = reason;

            if (_btnPurchase != null)
                _btnPurchase.text = "Unavailable";
        }
    }

    private void PurchaseSelected()
    {
        if (skiPassManager == null)
            return;

        int freeLevel = skiPassManager.Config != null ? skiPassManager.Config.defaultLevelIndex : 0;
        if (_selectedLevel == freeLevel)
        {
            bool claimed = skiPassManager.TryClaimDefaultPass(out string claimReason);

            if (_lblResult != null)
                _lblResult.text = claimed ? "Default pass unlocked!" : claimReason;

            GameAudio.PlayUi(claimed ? GameAudioCueId.UiPurchaseSuccess : GameAudioCueId.UiPurchaseFail);

            if (claimed)
            {
                if (statsManager != null)
                    statsManager.Save();

                SyncSelectedPassToCurrent();
                RefreshAll();
            }

            return;
        }

        if (skiPassManager.IsPassPermanentlyUnlocked(_selectedLevel))
        {
            if (_lblResult != null)
                _lblResult.text = "This pass is already permanently owned.";
            GameAudio.PlayUi(GameAudioCueId.UiPurchaseFail, 0.8f);
            RefreshAll();
            return;
        }

        bool ok = skiPassManager.TryPurchase(
            _selectedLevel,
            _selectedDurationIndex,
            TrySpendCurrency,
            out _,
            out string failReason);

        if (_lblResult != null)
            _lblResult.text = ok ? "Purchased!" : failReason;

        GameAudio.PlayUi(ok ? GameAudioCueId.UiPurchaseSuccess : GameAudioCueId.UiPurchaseFail);

        if (ok)
        {
            if (statsManager != null)
                statsManager.Save();

            SyncSelectedPassToCurrent();
            RefreshAll();
        }
    }

    private bool TrySpendCurrency(int amount)
    {
        var mgr = statsManager != null ? statsManager : PlayerStatsManager.Instance;
        if (mgr == null || mgr.Profile == null)
            return false;
        if (amount < 0)
            return false;
        if (!ProgressionEventRecorder.TrySpendCurrency(mgr.Profile, amount))
            return false;

        mgr.Save();
        return true;
    }

    private void RefreshClaimOverlay()
    {
        if (_claimOverlay == null || skiPassManager == null || skiPassManager.Config == null)
            return;

        bool showDefaultClaim = !skiPassManager.HasClaimedDefaultPass;
        _claimOverlay.style.display = showDefaultClaim ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showDefaultClaim)
            return;

        int defaultLevel = skiPassManager.Config.defaultLevelIndex;
        var pass = skiPassManager.Config.Get(defaultLevel);
        string passName = pass != null && !string.IsNullOrWhiteSpace(pass.displayName)
            ? pass.displayName
            : "Default Pass";

        if (_lblClaimOverlayTitle != null)
            _lblClaimOverlayTitle.text = $"{passName} Ready To Claim";

        if (_lblClaimOverlayBody != null)
            _lblClaimOverlayBody.text = "Claim the free permanent pass now to unlock ski lift access across the mountain.";

        if (_btnClaimOverlay != null)
            _btnClaimOverlay.text = "Claim Free Pass";
    }

    private void RebuildLiftList(HashSet<string> ownedAccessPassIds)
    {
        if (_liftList == null)
            return;

        _liftList.Clear();

        var lifts = FindObjectsOfType<LiftLine>(includeInactive: false);
        if (lifts == null || lifts.Length == 0)
        {
            _liftList.Add(new Label("No lifts found."));
            return;
        }

        List<LiftLine> sortedLifts = new List<LiftLine>();
        for (int i = 0; i < lifts.Length; i++)
        {
            if (lifts[i] != null)
                sortedLifts.Add(lifts[i]);
        }

        sortedLifts.Sort((a, b) =>
        {
            string ar = a != null ? a.GetRequiredPassDisplayName() : string.Empty;
            string br = b != null ? b.GetRequiredPassDisplayName() : string.Empty;
            int cmp = string.Compare(ar, br, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;

            return string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < sortedLifts.Count; i++)
        {
            LiftLine lift = sortedLifts[i];
            if (lift == null)
                continue;

            bool isSelected = _selectedLift == lift;
            bool accessibleNow = IsLiftAccessibleWithOwnedPassIds(lift, ownedAccessPassIds);
            Color requiredColor = GetLiftRequiredPassColor(lift);

            var btn = new Button(() => SelectLift(lift, centerMap: true))
            {
                text = $"{lift.name}\n{lift.GetRequiredPassDisplayName()}"
            };

            btn.AddToClassList("ski-pass-lift-button");

            if (isSelected)
                btn.AddToClassList("is-selected");

            if (!accessibleNow)
                btn.AddToClassList("is-locked");

            float borderAlpha = isSelected ? 0.95f : (accessibleNow ? 0.78f : 0.64f);
            btn.style.borderLeftColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);
            btn.style.borderRightColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);
            btn.style.borderTopColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);
            btn.style.borderBottomColor = new Color(requiredColor.r, requiredColor.g, requiredColor.b, borderAlpha);

            btn.style.borderLeftWidth = isSelected ? 2f : 1f;
            btn.style.borderRightWidth = isSelected ? 2f : 1f;
            btn.style.borderTopWidth = isSelected ? 2f : 1f;
            btn.style.borderBottomWidth = isSelected ? 2f : 1f;

            btn.style.backgroundColor = accessibleNow
                ? new Color(requiredColor.r, requiredColor.g, requiredColor.b, isSelected ? 0.24f : 0.14f)
                : new Color(requiredColor.r, requiredColor.g, requiredColor.b, isSelected ? 0.18f : 0.10f);

            _liftList.Add(btn);
        }
    }

    private bool PreviewPassGrantsLiftAccess(LiftLine lift, string selectedPassId, int selectedLevel)
    {
        return IsLiftUnlockedForPreview(lift, selectedPassId, selectedLevel);
    }

    private void SelectLift(LiftLine lift, bool centerMap)
    {
        _selectedLift = lift;
        RefreshSelectedLiftDetails();
        RebuildPassButtons();

        if (_mapUI == null || lift == null || !TryGetLiftPolylineId(lift, out string polylineId))
            return;

        _mapUI.SelectPolylineById(polylineId, center: false, minZoom: -1f);

        if (centerMap)
            _mapUI.FramePolylineById(polylineId, paddingPx: 28f, minZoom: 0.85f);

        _mapUI?.ForceOverlayLabelRefresh();
    }

    private bool TryGetLiftPolylineId(LiftLine lift, out string polylineId)
    {
        polylineId = null;
        if (lift == null)
            return false;

        string[] candidates =
        {
        lift.name,
        lift.gameObject != null ? lift.gameObject.name : string.Empty
    };

        if (mapData != null && mapData.Polylines != null)
        {
            for (int i = 0; i < mapData.Polylines.Count; i++)
            {
                var poly = mapData.Polylines[i];
                if (!poly.IsValid || poly.lineType != MapLineType.SkiLift)
                    continue;

                for (int c = 0; c < candidates.Length; c++)
                {
                    string candidate = candidates[c];
                    if (string.IsNullOrWhiteSpace(candidate))
                        continue;

                    if (string.Equals(poly.id, candidate, StringComparison.Ordinal) ||
                        string.Equals(poly.id, candidate, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(poly.displayName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        polylineId = poly.id;
                        return true;
                    }
                }
            }
        }

        polylineId = !string.IsNullOrWhiteSpace(lift.name) ? lift.name : lift.gameObject.name;
        return !string.IsNullOrWhiteSpace(polylineId);
    }

    private void SelectLiftById(string liftId, bool centerMap)
    {
        if (string.IsNullOrWhiteSpace(liftId))
            return;

        LiftLine lift = FindLiftById(liftId);
        if (lift != null)
        {
            SelectLift(lift, centerMap);
            return;
        }

        if (_mapUI != null)
            _mapUI.SelectPolylineById(liftId, center: centerMap, minZoom: centerMap ? 1.25f : -1f);

        _selectedLift = null;
        RefreshSelectedLiftDetails();
        RebuildPassButtons();
    }

    private LiftLine FindLiftById(string liftId)
    {
        if (string.IsNullOrWhiteSpace(liftId))
            return null;

        LiftLine[] lifts = FindObjectsOfType<LiftLine>(includeInactive: false);
        if (lifts == null || lifts.Length == 0)
            return null;

        for (int i = 0; i < lifts.Length; i++)
        {
            LiftLine lift = lifts[i];
            if (lift == null)
                continue;

            if (string.Equals(lift.gameObject.name, liftId, StringComparison.Ordinal) ||
                string.Equals(lift.gameObject.name, liftId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lift.name, liftId, StringComparison.Ordinal) ||
                string.Equals(lift.name, liftId, StringComparison.OrdinalIgnoreCase))
                return lift;

            if (TryGetLiftPolylineId(lift, out string polylineId) &&
                string.Equals(polylineId, liftId, StringComparison.OrdinalIgnoreCase))
                return lift;
        }

        return null;
    }

    private void RefreshSelectedLiftDetails()
    {
        if (_lblSelectedLiftName == null || _lblSelectedLiftStatus == null || _lblSelectedLiftPassRequirement == null || _selectedLiftPassList == null)
            return;

        _selectedLiftPassList.Clear();

        if (_selectedLift == null)
        {
            if (_selectedLiftOverlay != null)
                _selectedLiftOverlay.style.display = DisplayStyle.None;

            _lblSelectedLiftName.text = "Select a ski lift";
            _lblSelectedLiftStatus.text = "Choose a lift from the map or list to inspect its access requirements.";
            _lblSelectedLiftPassRequirement.text = string.Empty;
            return;
        }

        if (_selectedLiftOverlay != null)
            _selectedLiftOverlay.style.display = DisplayStyle.Flex;

        HashSet<string> ownedAccessPassIds = BuildOwnedAccessPassIds();
        bool accessibleNow = IsLiftAccessibleWithOwnedPassIds(_selectedLift, ownedAccessPassIds);

        string selectedPassId = GetSelectedPassId();
        string selectedPassLabel = GetPassDisplayName(_selectedLevel);
        bool previewAccessible = PreviewPassGrantsLiftAccess(_selectedLift, selectedPassId, _selectedLevel);
        string requiredPassLabel = _selectedLift.GetRequiredPassDisplayName();

        _lblSelectedLiftName.text = _selectedLift.name;
        _lblSelectedLiftStatus.text = accessibleNow
            ? "Rideable now."
            : "Locked right now.";

        _lblSelectedLiftPassRequirement.text = previewAccessible
            ? $"Required pass: {requiredPassLabel} • Previewed pass: {selectedPassLabel} includes this lift."
            : $"Required pass: {requiredPassLabel} • Previewed pass: {selectedPassLabel} does not include this lift.";

        var cfg = skiPassManager != null ? skiPassManager.Config : null;
        if (cfg == null || cfg.levels == null)
            return;

        bool foundAny = false;
        for (int i = 0; i < cfg.levels.Length; i++)
        {
            var pass = cfg.levels[i];
            if (pass == null)
                continue;

            string passId = cfg.GetPassIdForLevel(i);
            if (!PreviewPassGrantsLiftAccess(_selectedLift, passId, i))
                continue;

            foundAny = true;

            var chip = new Label(pass.displayName);
            chip.AddToClassList("ski-pass-supported-pass-chip");

            if (string.Equals(passId, _selectedLift.RequiredPassId, StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(_selectedLift.RequiredPassId) && i == Mathf.Max(0, _selectedLift.RequiredPassLevel)))
            {
                chip.AddToClassList("is-required");
            }

            if (i == _selectedLevel)
                chip.AddToClassList("is-selected");

            _selectedLiftPassList.Add(chip);
        }

        if (!foundAny)
            _selectedLiftPassList.Add(new Label(requiredPassLabel));
    }

    private MapRegionSet ResolveRegionSet()
    {
        if (regionSet != null)
        {
            if (regionSet.MapData == null && mapData != null)
                regionSet.SetMapData(mapData);

            return regionSet;
        }

        var all = Resources.FindObjectsOfTypeAll<MapRegionSet>();
        if (all == null || all.Length == 0)
            return null;

        for (int i = 0; i < all.Length; i++)
        {
            var set = all[i];
            if (set == null)
                continue;

            if (mapData == null || set.MapData == mapData)
            {
                if (set.MapData == null && mapData != null)
                    set.SetMapData(mapData);

                return set;
            }
        }

        return all[0];
    }

    private Transform ResolvePlayerTransform()
    {
        var ski = FindObjectOfType<SkiController>();
        if (ski != null)
            return ski.transform;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    private static Camera AutoFindMapReferenceCamera()
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        if (cameras == null || cameras.Length == 0)
            return null;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null)
                continue;
            if (!cam.gameObject.scene.IsValid())
                continue;

            string n = cam.name ?? string.Empty;
            if (n.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 &&
                n.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cam;
            }
        }

        return null;
    }
}
