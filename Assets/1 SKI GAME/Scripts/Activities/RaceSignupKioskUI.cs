using System;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.POI;
using SkiGame.Progression;
using SkiGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DisallowMultipleComponent]
public sealed class RaceSignupKioskUI : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private StyleSheet styleSheet;

    [Header("References")]
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

    private VisualElement _mapHost;

    private VisualElement _browseState;
    private VisualElement _selectedState;
    private Label _lblBrowseHint;

    private ScrollView _raceList;
    private Button _btnBackToRaceList;

    private VisualElement _leagueList;
    private Button _btnSignUp;
    private Label _lblResult;

    private VisualElement _championshipBanner;
    private VisualElement _championshipGoalPanel;
    private Label _lblChampionshipTitle;
    private Label _lblChampionshipStatus;
    private Label _lblChampionshipSummary;
    private Label _lblChampionshipReward;
    private Label _lblRaceBoardSummary;
    private VisualElement _championshipProgressMatrix;
    private VisualElement _championshipGoalLane;
    private Button _btnChampionshipAction;

    private Label _lblSelectedRaceName;
    private Label _lblSelectedRaceStatus;
    private Label _lblSelectedRaceRequirement;
    private Label _lblSelectedRaceDescription;
    private Label _lblSelectedRaceBestPlacement;
    private Label _lblSelectedRaceBestTime;
    private Label _lblSelectedRaceBaseReward;
    private Label _lblSelectedRaceReward;
    private VisualElement _selectedRaceRewardList;

    private PhoneMapPageUI _mapUI;
    private RaceCourseLine _selectedRace;
    private int _selectedLeagueNumber = 1;
    private bool _isOpen;
    private bool _handlingMapPolylineSelection;
    private bool _uiBound;

    private static readonly Color SelectedRaceDesaturatedColor = new Color(0.50f, 0.50f, 0.50f, 0.42f);

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

        ResolveSceneReferences();
        EnsureUiBound();
        HideImmediate();
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        EnsureUiBound();
        if (!_isOpen)
            HideImmediate();
    }

    private void OnDisable()
    {
        if (_mapUI != null)
        {
            _mapUI.PolylineSelected -= HandleMapPolylineSelected;
            _mapUI.MarkerSelected -= HandleMapMarkerSelected;
            _mapUI.SelectionCleared -= HandleMapSelectionCleared;
            _mapUI.BackgroundWorldClicked -= HandleMapBackgroundClicked;
        }

        ApplyCursorAndCameraState(false);
        GameplayModalMovementLock.SetLocked(this, false);
        _isOpen = false;
    }

    private void Update()
    {
        if (!_isOpen || _mapUI == null)
            return;

        _mapUI.Tick(Time.unscaledDeltaTime);
    }

    private bool EnsureUiBound()
    {
        if (_uiBound && _root != null)
            return true;

        if (document == null)
            document = GetComponent<UIDocument>();

        if (document == null)
            return false;

        _root = document.rootVisualElement;
        if (_root == null)
            return false;

        if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
            _root.styleSheets.Add(styleSheet);

        _panel = _root.Q<VisualElement>("RaceSignupKioskPanel");
        _btnClose = _root.Q<Button>("Btn_CloseRaceSignupKiosk");

        _mapHost = _root.Q<VisualElement>("RaceMapHost");

        _championshipBanner = _root.Q<VisualElement>("ChampionshipBanner");
        _championshipGoalPanel = _root.Q<VisualElement>("ChampionshipGoalPanel");
        _lblChampionshipTitle = _root.Q<Label>("Lbl_ChampionshipTitle");
        _lblChampionshipStatus = _root.Q<Label>("Lbl_ChampionshipStatus");
        _lblChampionshipSummary = _root.Q<Label>("Lbl_ChampionshipSummary");
        _lblChampionshipReward = _root.Q<Label>("Lbl_ChampionshipReward");
        _lblRaceBoardSummary = _root.Q<Label>("Lbl_RaceBoardSummary");
        _championshipProgressMatrix = _root.Q<VisualElement>("ChampionshipProgressMatrix");
        _championshipGoalLane = _root.Q<VisualElement>("ChampionshipGoalLane");
        _btnChampionshipAction = _root.Q<Button>("Btn_ChampionshipAction");

        _browseState = _root.Q<VisualElement>("BrowseState");
        _selectedState = _root.Q<VisualElement>("SelectedState");
        _lblBrowseHint = _root.Q<Label>("Lbl_BrowseHint");

        _raceList = _root.Q<ScrollView>("RaceList");
        _btnBackToRaceList = _root.Q<Button>("Btn_BackToRaceList");

        _leagueList = _root.Q<VisualElement>("RaceLeagueList");
        _btnSignUp = _root.Q<Button>("Btn_SignUpRace");
        _lblResult = _root.Q<Label>("Lbl_RaceSignupResult");

        _lblSelectedRaceName = _root.Q<Label>("Lbl_SelectedRaceName");
        _lblSelectedRaceStatus = _root.Q<Label>("Lbl_SelectedRaceStatus");
        _lblSelectedRaceRequirement = _root.Q<Label>("Lbl_SelectedRaceRequirement");
        _lblSelectedRaceDescription = _root.Q<Label>("Lbl_SelectedRaceDescription");
        _lblSelectedRaceBestPlacement = _root.Q<Label>("Lbl_SelectedRaceBestPlacement");
        _lblSelectedRaceBestTime = _root.Q<Label>("Lbl_SelectedRaceBestTime");
        _lblSelectedRaceBaseReward = _root.Q<Label>("Lbl_SelectedRaceBaseReward");
        _lblSelectedRaceReward = _root.Q<Label>("Lbl_SelectedRaceReward");
        _selectedRaceRewardList = _root.Q<VisualElement>("SelectedRaceRewardList");

        if (_btnClose != null)
        {
            _btnClose.clicked -= Close;
            _btnClose.clicked += Close;
        }

        if (_btnBackToRaceList != null)
        {
            _btnBackToRaceList.clicked -= HandleBackToRaceList;
            _btnBackToRaceList.clicked += HandleBackToRaceList;
        }

        if (_btnSignUp != null)
        {
            _btnSignUp.clicked -= SignUpSelectedRace;
            _btnSignUp.clicked += SignUpSelectedRace;
        }

        if (_btnChampionshipAction != null)
        {
            _btnChampionshipAction.clicked -= HandleChampionshipAction;
            _btnChampionshipAction.clicked += HandleChampionshipAction;
        }

        _uiBound = true;
        return true;
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

    public void Open()
    {
        ResolveSceneReferences();

        if (!EnsureUiBound())
            return;

        bool wasOpen = _isOpen;
        _isOpen = true;
        _selectedLeagueNumber = Mathf.Max(1, _selectedLeagueNumber);

        _root.style.display = DisplayStyle.Flex;
        _root.style.visibility = Visibility.Visible;
        _root.style.opacity = 1f;
        _root.pickingMode = PickingMode.Position;

        if (_panel != null)
            _panel.style.display = DisplayStyle.Flex;

        ApplyCursorAndCameraState(true);

        GameObject playerRoot = ResolvePlayerRootObject();
        GameplayModalMovementLock.SetLocked(this, true, playerRoot);

        if (miniHudController != null)
            miniHudController.SetVisible(false);

        if (!wasOpen)
            RaiseQuestUiEvent("ui.race_kiosk.opened");

        EnsureMapBound();
        EnsureSelectedLeagueIsValid();
        RefreshAll();
    }

    public void Close()
    {
        _isOpen = false;
        HideImmediate();

        ApplyCursorAndCameraState(false);
        GameplayModalMovementLock.SetLocked(this, false);

        if (miniHudController != null)
            miniHudController.SetVisible(true);
    }

    private void ResolveSceneReferences()
    {
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        if (mapData == null)
            mapData = FindObjectOfType<MapData>();

        if (regionSet == null)
            regionSet = FindObjectOfType<MapRegionSet>();

        if (mapReferenceCamera == null)
            mapReferenceCamera = FindObjectOfType<Camera>();

        if (poiRegistry == null)
            poiRegistry = PointOfInterestRegistry.Instance != null
                ? PointOfInterestRegistry.Instance
                : FindObjectOfType<PointOfInterestRegistry>();

        if (playerTransform == null)
            playerTransform = ResolvePlayerTransform();
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

    private static void RaiseQuestUiEvent(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var signalBus = UnityEngine.Object.FindObjectOfType<QuestSignalBus>();
        signalBus?.RaiseEvent(key);
    }

    private void EnsureMapBound()
    {
        if (_mapHost == null || mapData == null)
            return;

        if (_mapUI != null)
        {
            _mapUI.SetPlayer(playerTransform);
            _mapUI.SetPlayerTracking(showMarker: true, drawTrail: false);
            _mapUI.SetMinimapMode(false, followPlayer: false, lockPan: false, allowZoom: true, suppressSelection: false, hideMarkerLabels: false);
            _mapUI.SetOverlayVisibility(showRuns: true, showLifts: true, showPOIs: true);
            _mapUI.SetSkiRunOverlayVisible(false);
            _mapUI.SetActivityOverlayVisibility(showRaceStarts: true, showMedicTents: false, showSnowmobiles: false);
            _mapUI.SetLegendVisible(false);
            _mapUI.SetRegionInteractivity(selectable: false, showLabels: true);
            _mapUI.SetSelectionFilters(
                marker => marker.IsValid && marker.source is RaceCourseLine,
                polyline => polyline.IsValid && polyline.lineType == MapLineType.RaceCourse);

            if (regionSet != null)
                _mapUI.SetRegionSet(regionSet);

            ApplyRacePolylineColors();
            _mapUI.Refresh();
            _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.0f);
            return;
        }

        _mapHost.Clear();

        VisualElement runtimeMapRoot = MountainHudMapRootFactory.BuildOverlayMapRoot("RaceSignupKioskMapRoot");
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
        _mapUI.SetOverlayVisibility(showRuns: true, showLifts: true, showPOIs: true);
        _mapUI.SetSkiRunOverlayVisible(false);
        _mapUI.SetActivityOverlayVisibility(showRaceStarts: true, showMedicTents: false, showSnowmobiles: false);
        _mapUI.SetLegendVisible(false);
        _mapUI.SetRegionInteractivity(selectable: false, showLabels: true);
        _mapUI.SetSelectionFilters(
            marker => marker.IsValid && marker.source is RaceCourseLine,
            polyline => polyline.IsValid && polyline.lineType == MapLineType.RaceCourse);

        _mapUI.PolylineSelected += HandleMapPolylineSelected;
        _mapUI.MarkerSelected += HandleMapMarkerSelected;
        _mapUI.SelectionCleared += HandleMapSelectionCleared;
        _mapUI.BackgroundWorldClicked += HandleMapBackgroundClicked;

        ApplyRacePolylineColors();
        _mapUI.Refresh();
        _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.0f);
    }

    private void HandleMapPolylineSelected(MapPolyline polyline)
    {
        if (!polyline.IsValid || _handlingMapPolylineSelection)
            return;

        RaceCourseLine race = FindRaceById(polyline.id);
        if (race == null)
        {
            _mapUI?.ClearSelection();
            return;
        }

        try
        {
            _handlingMapPolylineSelection = true;
            SelectRace(race, _selectedLeagueNumber, centerMap: false);
        }
        finally
        {
            _handlingMapPolylineSelection = false;
        }
    }

    private void HandleMapMarkerSelected(MapMarker marker)
    {
        if (!marker.IsValid)
            return;

        RaceCourseLine race = marker.source as RaceCourseLine;
        if (race == null)
            race = FindRaceById(marker.id);

        if (race == null)
            return;

        SelectRace(race, _selectedLeagueNumber, centerMap: false);
    }

    private void HandleMapSelectionCleared()
    {
        if (_selectedRace == null)
            return;

        _selectedRace = null;
        RefreshAll();
    }

    private void HandleMapBackgroundClicked(Vector3 _)
    {
        if (_selectedRace == null)
            return;

        _selectedRace = null;
        RefreshAll();
    }

    private void HandleBackToRaceList()
    {
        if (_mapUI != null)
            _mapUI.ClearSelection();
        else
        {
            _selectedRace = null;
            RefreshAll();
        }
    }

    private void HandleChampionshipAction()
    {
        RaceCourseLine championshipRace = GetChampionshipRace();
        if (championshipRace == null)
            return;

        if (!championshipRace.IsChampionshipUnlocked())
        {
            SelectRace(championshipRace, GetPreferredLeagueForRace(championshipRace), centerMap: true);
            return;
        }

        SelectRace(championshipRace, GetPreferredLeagueForRace(championshipRace), centerMap: true);
        SignUpSelectedRace();
    }

    private void RefreshAll()
    {
        EnsureSelectedLeagueIsValid();
        RebuildChampionshipBanner();
        RebuildRaceList();
        RebuildLeagueButtons();
        RefreshSelectedRaceDetails();
        RefreshMapSelection();
        UpdateSidebarState();
    }

    private void UpdateSidebarState()
    {
        bool hasSelection = _selectedRace != null;

        if (_browseState != null)
            _browseState.style.display = hasSelection ? DisplayStyle.None : DisplayStyle.Flex;

        if (_selectedState != null)
            _selectedState.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;

        if (_lblBrowseHint != null)
            _lblBrowseHint.text = "Select a race from the board, the map, or the list.";
    }

    private void RebuildChampionshipBanner()
    {
        RaceCourseLine championshipRace = GetChampionshipRace();
        RaceCourseLine[] standardRaces = GetStandardRaces();

        if (_championshipBanner == null)
            return;

        if (championshipRace == null)
        {
            _championshipBanner.style.display = DisplayStyle.None;
            return;
        }

        _championshipBanner.style.display = DisplayStyle.Flex;

        bool unlocked = championshipRace.IsChampionshipUnlocked();
        bool completed = RaceRescueProgression.IsRaceChampionshipCompleted(championshipRace.RaceId);
        int completedCount = championshipRace.GetChampionshipCompletedStandardRaceCount();
        int totalCount = Mathf.Max(1, championshipRace.GetChampionshipTotalStandardRaceCount());
        string rewardSummary = championshipRace.GetChampionshipRewardSummary();

        if (_championshipGoalPanel != null)
        {
            _championshipGoalPanel.EnableInClassList("is-unlocked", unlocked && !completed);
            _championshipGoalPanel.EnableInClassList("is-completed", completed);
        }

        if (_lblRaceBoardSummary != null)
            _lblRaceBoardSummary.text = $"{standardRaces.Length} races";

        if (_lblChampionshipTitle != null)
            _lblChampionshipTitle.text = $"{championshipRace.RaceName}";

        if (_lblChampionshipReward != null)
            _lblChampionshipReward.text = string.IsNullOrWhiteSpace(rewardSummary)
                ? "Regional championship reward"
                : rewardSummary;

        if (_lblChampionshipStatus != null)
        {
            if (completed)
                _lblChampionshipStatus.text = "Completed";
            else if (unlocked)
                _lblChampionshipStatus.text = "Unlocked";
            else
                _lblChampionshipStatus.text = "Qualifying";
        }

        if (_lblChampionshipSummary != null)
            _lblChampionshipSummary.text = $"{completedCount}/{totalCount}";

        if (_btnChampionshipAction != null)
        {
            _btnChampionshipAction.style.display = DisplayStyle.Flex;
            _btnChampionshipAction.text = completed
                ? "Replay Championship"
                : (unlocked ? "Start" : "View");
            _btnChampionshipAction.SetEnabled(true);
        }

        RebuildRaceSelectionBoard(standardRaces);
        RebuildChampionshipGoalLane(championshipRace, standardRaces, completedCount, totalCount);
    }

    private void RebuildRaceSelectionBoard(RaceCourseLine[] standardRaces)
    {
        if (_championshipProgressMatrix == null)
            return;

        _championshipProgressMatrix.Clear();

        for (int i = 0; i < standardRaces.Length; i++)
        {
            RaceCourseLine race = standardRaces[i];
            if (race == null)
                continue;

            Color accent = GetRaceDisplayColor(race, i, desaturateIfUnselected: false);

            int previewLeague = GetPreviewLeagueForRace(race);
            bool isSelectedRace = _selectedRace == race;
            bool previewUnlocked = race.IsLeagueUnlocked(previewLeague);
            bool previewCompleted = race.HasCompletedLeague(previewLeague);

            var block = new VisualElement();
            block.AddToClassList("race-selection-pass-block");

            var raceButton = new Button(() =>
            {
                SelectRace(race, previewLeague, centerMap: true);
            });

            raceButton.text = $"{race.RaceName}\n{BuildRacePreviewTag(race, previewLeague, previewUnlocked, previewCompleted)}";
            raceButton.AddToClassList("race-selection-pass-button");

            if (isSelectedRace)
                raceButton.AddToClassList("is-selected");

            ApplyRacePassButtonVisualState(raceButton, accent, isSelectedRace, previewUnlocked, previewCompleted);
            block.Add(raceButton);

            var leagueRow = new VisualElement();
            leagueRow.AddToClassList("race-selection-pass-league-row");

            int chipCount = 0;
            int leagueCount = race.Leagues != null ? race.Leagues.Count : 0;
            for (int l = 0; l < leagueCount; l++)
            {
                var league = race.Leagues[l];
                if (league == null)
                    continue;

                chipCount++;
                int leagueNumber = Mathf.Max(1, league.leagueNumber);
                leagueRow.Add(CreateRaceLeagueChipButton(race, leagueNumber, accent));
            }

            if (chipCount > 0)
                block.Add(leagueRow);

            _championshipProgressMatrix.Add(block);
        }
    }

    private void ApplyRacePassButtonVisualState(
    Button btn,
    Color accent,
    bool isSelected,
    bool isUnlocked,
    bool isCompleted)
    {
        if (btn == null)
            return;

        Color borderColor = new Color(accent.r, accent.g, accent.b, isSelected ? 0.92f : 0.58f);

        btn.style.borderLeftColor = new StyleColor(borderColor);
        btn.style.borderRightColor = new StyleColor(borderColor);
        btn.style.borderTopColor = new StyleColor(borderColor);
        btn.style.borderBottomColor = new StyleColor(borderColor);

        btn.style.borderLeftWidth = isSelected ? 2f : 1f;
        btn.style.borderRightWidth = isSelected ? 2f : 1f;
        btn.style.borderTopWidth = isSelected ? 2f : 1f;
        btn.style.borderBottomWidth = isSelected ? 2f : 1f;

        if (isSelected)
        {
            btn.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.88f));
            btn.style.color = new StyleColor(new Color(0.08f, 0.11f, 0.16f, 1f));
        }
        else if (isCompleted)
        {
            btn.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.24f));
            btn.style.color = new StyleColor(Color.white);
        }
        else if (isUnlocked)
        {
            btn.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.14f));
            btn.style.color = new StyleColor(new Color(0.92f, 0.96f, 1f, 1f));
        }
        else
        {
            btn.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.08f));
            btn.style.color = new StyleColor(new Color(0.82f, 0.88f, 0.95f, 0.86f));
        }
    }

    private Button CreateRaceLeagueChipButton(RaceCourseLine race, int leagueNumber, Color accent)
    {
        bool isSelected = _selectedRace == race && _selectedLeagueNumber == leagueNumber;
        bool isUnlocked = race != null && race.IsLeagueUnlocked(leagueNumber);
        bool isCompleted = race != null && race.HasCompletedLeague(leagueNumber);

        var btn = new Button(() => SelectRace(race, leagueNumber, centerMap: true))
        {
            text = race != null ? $"L{leagueNumber}" : "L?"
        };

        btn.AddToClassList("race-selection-league-chip");

        if (isSelected)
            btn.AddToClassList("is-selected");

        if (!isUnlocked)
            btn.AddToClassList("is-locked");

        if (isCompleted)
            btn.AddToClassList("is-completed");

        float borderAlpha = isSelected ? 0.95f : (isUnlocked ? 0.78f : 0.54f);

        btn.style.borderLeftColor = new StyleColor(new Color(accent.r, accent.g, accent.b, borderAlpha));
        btn.style.borderRightColor = new StyleColor(new Color(accent.r, accent.g, accent.b, borderAlpha));
        btn.style.borderTopColor = new StyleColor(new Color(accent.r, accent.g, accent.b, borderAlpha));
        btn.style.borderBottomColor = new StyleColor(new Color(accent.r, accent.g, accent.b, borderAlpha));

        btn.style.borderLeftWidth = isSelected ? 2f : 1f;
        btn.style.borderRightWidth = isSelected ? 2f : 1f;
        btn.style.borderTopWidth = isSelected ? 2f : 1f;
        btn.style.borderBottomWidth = isSelected ? 2f : 1f;

        btn.style.backgroundColor = new StyleColor(
            isUnlocked
                ? new Color(accent.r, accent.g, accent.b, isSelected ? 0.28f : 0.14f)
                : new Color(accent.r, accent.g, accent.b, isSelected ? 0.18f : 0.08f));

        string stateText = isCompleted ? "Completed" : (isUnlocked ? "Available" : "Locked");
        btn.tooltip = $"{race.RaceName} • {race.GetLeagueDisplayName(leagueNumber)} • {stateText}";

        return btn;
    }

    private string BuildRacePreviewTag(RaceCourseLine race, int previewLeague, bool previewUnlocked, bool previewCompleted)
    {
        if (race == null)
            return "LOCKED";

        if (previewCompleted)
            return "COMPLETED";

        if (previewUnlocked)
            return race.GetLeagueDisplayName(previewLeague).ToUpperInvariant();

        return "LOCKED";
    }

    private void RebuildChampionshipGoalLane(RaceCourseLine championshipRace, RaceCourseLine[] standardRaces, int completedCount, int totalCount)
    {
        if (_championshipGoalLane == null)
            return;

        _championshipGoalLane.Clear();

        int requiredLeagueNumber = championshipRace != null
            ? Mathf.Max(1, championshipRace.ChampionshipRequiredLeagueNumber)
            : 1;

        var segmentColors = new System.Collections.Generic.List<Color>();
        var segmentCompletedStates = new System.Collections.Generic.List<bool>();

        for (int i = 0; i < standardRaces.Length; i++)
        {
            RaceCourseLine race = standardRaces[i];
            if (race == null)
                continue;

            Color accent = GetRaceDisplayColor(race, i, desaturateIfUnselected: false);

            int leagueCount = race.Leagues != null ? race.Leagues.Count : 0;
            for (int l = 0; l < leagueCount; l++)
            {
                var league = race.Leagues[l];
                if (league == null)
                    continue;

                int leagueNumber = Mathf.Max(1, league.leagueNumber);
                if (leagueNumber > requiredLeagueNumber)
                    continue;

                segmentColors.Add(accent);
                segmentCompletedStates.Add(race.HasCompletedLeague(leagueNumber));
            }
        }

        if (segmentColors.Count == 0)
        {
            segmentColors.Add(new Color(1f, 1f, 1f, 0.2f));
            segmentCompletedStates.Add(false);
        }

        int firstIncompleteIndex = segmentCompletedStates.FindIndex(state => !state);
        if (firstIncompleteIndex < 0)
            firstIncompleteIndex = int.MaxValue;

        for (int i = 0; i < segmentColors.Count; i++)
        {
            var seg = new VisualElement();
            seg.AddToClassList("race-signup-goal-segment");

            Color c = segmentColors[i];
            bool isCompleted = segmentCompletedStates[i];
            bool isCurrent = i == firstIncompleteIndex && completedCount < totalCount;

            if (isCompleted)
            {
                seg.style.backgroundColor = new StyleColor(new Color(c.r, c.g, c.b, 0.96f));
            }
            else
            {
                seg.AddToClassList("is-empty");
                seg.style.backgroundColor = new StyleColor(new Color(c.r, c.g, c.b, 0.18f));
            }

            if (isCurrent)
                seg.AddToClassList("is-current");

            _championshipGoalLane.Add(seg);
        }
    }

    private void RebuildLeagueButtons()
    {
        if (_leagueList == null)
            return;

        _leagueList.Clear();

        if (_selectedRace == null || _selectedRace.IsRegionalChampionship)
        {
            _leagueList.style.display = DisplayStyle.None;
            return;
        }

        _leagueList.style.display = DisplayStyle.Flex;

        int leagueCount = _selectedRace.Leagues != null ? _selectedRace.Leagues.Count : 0;
        for (int i = 0; i < leagueCount; i++)
        {
            var league = _selectedRace.Leagues[i];
            if (league == null)
                continue;

            int leagueNumber = Mathf.Max(1, league.leagueNumber);
            bool unlocked = _selectedRace.IsLeagueUnlocked(leagueNumber);
            bool completed = _selectedRace.HasCompletedLeague(leagueNumber);

            var btn = new Button(() =>
            {
                _selectedLeagueNumber = leagueNumber;
                RefreshAll();
            });

            btn.text = $"L{leagueNumber}";
            btn.AddToClassList("race-signup-option-button");

            if (leagueNumber == _selectedLeagueNumber)
                btn.AddToClassList("is-selected");

            if (!unlocked)
                btn.AddToClassList("is-locked");

            if (completed)
                btn.tooltip = $"{_selectedRace.GetLeagueDisplayName(leagueNumber)} • Completed";
            else if (unlocked)
                btn.tooltip = $"{_selectedRace.GetLeagueDisplayName(leagueNumber)} • Available";
            else
                btn.tooltip = BuildLockedLeagueStatus(_selectedRace, leagueNumber);

            _leagueList.Add(btn);
        }
    }

    private void RebuildRaceList()
    {
        if (_raceList == null)
            return;

        _raceList.Clear();

        RaceCourseLine[] races = GetStandardRaces();
        if (races == null || races.Length == 0)
        {
            _raceList.Add(new Label("No races found."));
            return;
        }

        for (int i = 0; i < races.Length; i++)
        {
            RaceCourseLine race = races[i];
            if (race == null)
                continue;

            int previewLeague = GetPreviewLeagueForRace(race);
            bool unlocked = race.IsLeagueUnlocked(previewLeague);
            bool completed = race.HasCompletedLeague(previewLeague);

            int bestPlacement = race.GetBestPlacement(previewLeague);
            float bestTime = race.GetBestTimeSeconds(previewLeague);

            Color accent = GetRaceDisplayColor(race, i, desaturateIfUnselected: false);

            var row = new Button(() => SelectRace(race, previewLeague, centerMap: true));
            row.AddToClassList("race-signup-race-card");
            row.text = string.Empty;

            if (_selectedRace == race)
                row.AddToClassList("is-selected");

            if (!unlocked)
                row.AddToClassList("is-locked");

            if (completed)
                row.AddToClassList("is-completed");

            ApplyRaceBrowseCardAccent(row, accent, _selectedRace == race);

            var header = new VisualElement();
            header.AddToClassList("race-signup-race-card-header");

            var titleWrap = new VisualElement();
            titleWrap.AddToClassList("race-signup-race-card-title-wrap");

            var title = new Label(race.RaceName);
            title.AddToClassList("race-signup-race-card-title");
            titleWrap.Add(title);

            var tier = new Label(race.GetLeagueDisplayName(previewLeague));
            tier.AddToClassList("race-signup-race-card-tier");
            titleWrap.Add(tier);

            header.Add(titleWrap);

            if (!unlocked || completed)
            {
                var status = new Label(completed ? "Completed" : "Locked");
                status.AddToClassList("race-signup-race-card-status");
                if (completed)
                    status.AddToClassList("is-completed");
                else
                    status.AddToClassList("is-locked");

                header.Add(status);
            }

            row.Add(header);

            var statsRow = new VisualElement();
            statsRow.AddToClassList("race-signup-race-card-stats");

            statsRow.Add(BuildBrowseStatChip("Best", bestPlacement > 0 ? $"{bestPlacement}" : "--"));
            statsRow.Add(BuildBrowseStatChip("Time", bestTime >= 0f ? $"{bestTime:0.00}s" : "--"));
            statsRow.Add(BuildBrowseStatChip("Base", $"${race.CompletionReward}"));

            row.Add(statsRow);

            row.tooltip = !unlocked
                ? $"{race.RaceName} • {BuildLockedLeagueStatus(race, previewLeague)}"
                : $"{race.RaceName} • {race.GetLeagueDisplayName(previewLeague)}";

            _raceList.Add(row);
        }
    }

    private static void ApplyRaceBrowseCardAccent(VisualElement row, Color accent, bool isSelected)
    {
        if (row == null)
            return;

        Color fill = isSelected
            ? new Color(accent.r, accent.g, accent.b, 0.88f)
            : new Color(accent.r, accent.g, accent.b, 0.18f);

        Color border = isSelected
            ? new Color(accent.r, accent.g, accent.b, 0.96f)
            : new Color(accent.r, accent.g, accent.b, 0.52f);

        row.style.backgroundColor = new StyleColor(fill);

        row.style.borderLeftWidth = isSelected ? 2f : 1f;
        row.style.borderRightWidth = isSelected ? 2f : 1f;
        row.style.borderTopWidth = isSelected ? 2f : 1f;
        row.style.borderBottomWidth = isSelected ? 2f : 1f;

        row.style.borderLeftColor = new StyleColor(border);
        row.style.borderRightColor = new StyleColor(border);
        row.style.borderTopColor = new StyleColor(border);
        row.style.borderBottomColor = new StyleColor(border);
    }

    private VisualElement BuildBrowseStatChip(string labelText, string valueText)
    {
        var chip = new VisualElement();
        chip.AddToClassList("race-signup-race-card-stat-chip");

        var label = new Label(labelText);
        label.AddToClassList("race-signup-race-card-stat-label");
        chip.Add(label);

        var value = new Label(valueText);
        value.AddToClassList("race-signup-race-card-stat-value");
        chip.Add(value);

        return chip;
    }

    private static string BuildLockedLeagueStatus(RaceCourseLine race, int leagueNumber)
    {
        if (race == null)
            return "Locked";

        if (race.IsRegionalChampionship)
            return race.BuildChampionshipLockReason();

        int required = race.GetPreviousLeagueNumber(leagueNumber);
        return required > 0
            ? $"Locked until {race.GetLeagueDisplayName(required)}"
            : "Locked";
    }

    private void SelectRace(RaceCourseLine race, int leagueNumber, bool centerMap)
    {
        _selectedRace = race;
        _selectedLeagueNumber = Mathf.Max(1, leagueNumber);
        EnsureSelectedLeagueIsValid();
        RefreshAll();

        if (_mapUI == null || race == null || !TryGetRacePolylineId(race, out string polylineId))
            return;

        _mapUI.SelectPolylineById(polylineId, center: false, minZoom: -1f);

        if (centerMap)
            _mapUI.FramePolylineById(polylineId, paddingPx: 28f, minZoom: 0.85f);
    }

    private void RefreshSelectedRaceDetails()
    {
        if (_lblSelectedRaceName == null ||
            _lblSelectedRaceStatus == null ||
            _lblSelectedRaceRequirement == null ||
            _lblSelectedRaceDescription == null ||
            _lblSelectedRaceBestPlacement == null ||
            _lblSelectedRaceBestTime == null ||
            _lblSelectedRaceBaseReward == null ||
            _lblSelectedRaceReward == null ||
            _btnSignUp == null)
        {
            return;
        }

        if (_selectedRace == null)
        {
            _lblSelectedRaceName.text = "Select a race";
            _lblSelectedRaceStatus.text = "Choose a race from the board, map, or list.";
            _lblSelectedRaceRequirement.text = string.Empty;
            _lblSelectedRaceDescription.text = string.Empty;
            _lblSelectedRaceBestPlacement.text = "--";
            _lblSelectedRaceBestTime.text = "--";
            _lblSelectedRaceBaseReward.text = "--";
            _lblSelectedRaceReward.text = string.Empty;

            if (_selectedRaceRewardList != null)
                _selectedRaceRewardList.Clear();

            _btnSignUp.text = "Start Race";
            _btnSignUp.SetEnabled(false);
            return;
        }

        bool unlocked = _selectedRace.IsRegionalChampionship
            ? _selectedRace.IsChampionshipUnlocked()
            : _selectedRace.IsLeagueUnlocked(_selectedLeagueNumber);

        bool completed = _selectedRace.IsRegionalChampionship
            ? RaceRescueProgression.IsRaceChampionshipCompleted(_selectedRace.RaceId)
            : _selectedRace.HasCompletedLeague(_selectedLeagueNumber);

        int bestPlacement = _selectedRace.GetBestPlacement(_selectedLeagueNumber);
        float bestTime = _selectedRace.GetBestTimeSeconds(_selectedLeagueNumber);
        var league = _selectedRace.GetLeague(_selectedLeagueNumber);

        _lblSelectedRaceName.text = _selectedRace.IsRegionalChampionship
            ? $"{_selectedRace.RaceName}"
            : _selectedRace.RaceName;

        _lblSelectedRaceStatus.text = completed
            ? "Completed"
            : (unlocked ? "Available" : "Locked");

        if (_selectedRace.IsRegionalChampionship)
        {
            int completedCount = _selectedRace.GetChampionshipCompletedStandardRaceCount();
            int totalCount = Mathf.Max(1, _selectedRace.GetChampionshipTotalStandardRaceCount());

            _lblSelectedRaceRequirement.text = unlocked
                ? $"Championship ready • {completedCount}/{totalCount} qualifying clears complete"
                : _selectedRace.BuildChampionshipLockReason();

            _lblSelectedRaceDescription.text = string.IsNullOrWhiteSpace(_selectedRace.GetChampionshipRewardSummary())
                ? "The regional capstone event."
                : _selectedRace.GetChampionshipRewardSummary();

            _lblSelectedRaceBestPlacement.text = "--";
            _lblSelectedRaceBestTime.text = "--";
            _lblSelectedRaceBaseReward.text = "--";
            _lblSelectedRaceReward.text = "Championship Reward";

            if (_selectedRaceRewardList != null)
            {
                _selectedRaceRewardList.Clear();
                _selectedRaceRewardList.Add(BuildRewardChip($"Need L{_selectedRace.ChampionshipRequiredLeagueNumber}"));
                if (_selectedRace.ChampionshipPermanentPassLevelReward >= 0)
                    _selectedRaceRewardList.Add(BuildRewardChip($"Pass L{_selectedRace.ChampionshipPermanentPassLevelReward}"));
            }

            _btnSignUp.text = unlocked ? "Start Championship" : "Locked";
            _btnSignUp.SetEnabled(unlocked);
            return;
        }

        _lblSelectedRaceRequirement.text = unlocked
            ? _selectedRace.GetLeagueDisplayName(_selectedLeagueNumber)
            : BuildLockedLeagueStatus(_selectedRace, _selectedLeagueNumber);

            string objectiveSummary = _selectedRace.BuildLeagueObjectiveSummary(_selectedLeagueNumber);
            string description = league != null && !string.IsNullOrWhiteSpace(league.description)
                ? league.description
                : "Race the course against the current league field.";
            if (!string.IsNullOrWhiteSpace(objectiveSummary))
                description = $"{description}\n{objectiveSummary}";

            _lblSelectedRaceDescription.text = description;

        _lblSelectedRaceBestPlacement.text = bestPlacement > 0 ? $"{bestPlacement}" : "--";
        _lblSelectedRaceBestTime.text = bestTime >= 0f ? $"{bestTime:0.00}s" : "--";
        _lblSelectedRaceBaseReward.text = $"${_selectedRace.CompletionReward}";
        _lblSelectedRaceReward.text = "Podium Rewards";

        if (_selectedRaceRewardList != null)
        {
            _selectedRaceRewardList.Clear();
            _selectedRaceRewardList.Add(BuildRewardChip($"1st\n+${_selectedRace.FirstPlaceBonus}"));
            _selectedRaceRewardList.Add(BuildRewardChip($"2nd\n+${_selectedRace.SecondPlaceBonus}"));
            _selectedRaceRewardList.Add(BuildRewardChip($"3rd\n+${_selectedRace.ThirdPlaceBonus}"));

            if (!completed && _selectedRace.FirstTimeLeagueCompletionBonus > 0)
                _selectedRaceRewardList.Add(BuildRewardChip($"First Clear\n+${_selectedRace.FirstTimeLeagueCompletionBonus}"));
        }

        _btnSignUp.text = unlocked ? "Start Race" : "Locked";
        _btnSignUp.SetEnabled(unlocked);
    }

    private VisualElement BuildRewardChip(string text)
    {
        var chip = new Label(text);
        chip.AddToClassList("race-signup-reward-chip");
        return chip;
    }

    private void RefreshMapSelection()
    {
        if (_mapUI == null)
            return;

        ApplyRacePolylineColors();
        _mapUI.Refresh();

        if (_selectedRace != null && TryGetRacePolylineId(_selectedRace, out string polylineId))
            _mapUI.SelectPolylineById(polylineId, center: false, minZoom: -1f);
        else
            _mapUI.ClearSelection();
    }

    private void SignUpSelectedRace()
    {
        if (_selectedRace == null)
            return;

        GameObject playerRoot = ResolvePlayerRootObject();
        if (playerRoot == null)
        {
            if (_lblResult != null)
                _lblResult.text = "Player not found.";

            return;
        }

        // Important: release the kiosk/modal movement lock BEFORE starting the race.
        // MountainActivityManager.TryStart invokes RaceCourseLine.BeginAttempt synchronously,
        // so starting while the kiosk still owns movement can restore stale disabled state after BeginAttempt.
        ApplyCursorAndCameraState(false);
        GameplayModalMovementLock.SetLocked(this, false);

        bool ok = _selectedRace.TryStartLeagueExternal(playerRoot, _selectedLeagueNumber);

        if (!ok)
        {
            // Race did not start, so restore kiosk interaction state.
            ApplyCursorAndCameraState(true);
            GameplayModalMovementLock.SetLocked(this, true, playerRoot);

            if (_lblResult != null)
            {
                bool raceUnlocked = _selectedRace.IsRegionalChampionship
                    ? _selectedRace.IsChampionshipUnlocked()
                    : _selectedRace.IsLeagueUnlocked(_selectedLeagueNumber);

                if (!raceUnlocked)
                {
                    if (_selectedRace.IsRegionalChampionship)
                    {
                        _lblResult.text = _selectedRace.BuildChampionshipLockReason();
                    }
                    else
                    {
                        int prev = _selectedRace.GetPreviousLeagueNumber(_selectedLeagueNumber);
                        _lblResult.text = prev > 0
                            ? $"League locked - win {_selectedRace.GetLeagueDisplayName(prev)} first."
                            : "League locked.";
                    }
                }
                else
                {
                    _lblResult.text = "Could not start race.";
                }
            }

            return;
        }

        if (_lblResult != null)
        {
            _lblResult.text = _selectedRace.IsRegionalChampionship
                ? $"Signed up for {_selectedRace.RaceName} Championship"
                : $"Signed up for {_selectedRace.RaceName} - {_selectedRace.GetLeagueDisplayName(_selectedLeagueNumber)}";
        }

        // Close after the successful start, but movement is already released above.
        Close();
    }

    private void EnsureSelectedLeagueIsValid()
    {
        if (_selectedRace != null)
        {
            int requestedLeague = Mathf.Max(1, _selectedLeagueNumber);

            if (_selectedRace.GetLeague(requestedLeague) != null)
            {
                _selectedLeagueNumber = requestedLeague;
                return;
            }

            if (_selectedRace.Leagues != null && _selectedRace.Leagues.Count > 0)
            {
                _selectedLeagueNumber = Mathf.Max(1, _selectedRace.Leagues[0].leagueNumber);
                return;
            }
        }

        _selectedLeagueNumber = Mathf.Max(1, _selectedLeagueNumber);
    }

    private static void ApplyRaceAccent(VisualElement row, Color accent)
    {
        if (row == null)
            return;

        row.style.borderLeftWidth = 4f;
        row.style.borderLeftColor = new StyleColor(accent);
    }

    private static Color GetRaceAccentColor(int index)
    {
        Color[] accents =
        {
            new Color(0.27f, 0.76f, 1f, 0.95f),
            new Color(0.34f, 0.96f, 0.60f, 0.95f),
            new Color(1f, 0.80f, 0.31f, 0.95f),
            new Color(1f, 0.48f, 0.48f, 0.95f),
            new Color(0.86f, 0.54f, 1f, 0.95f),
        };

        return accents[Mathf.Abs(index) % accents.Length];
    }

    private RaceCourseLine[] GetSortedRaces()
    {
        var races = FindObjectsOfType<RaceCourseLine>(includeInactive: false);
        if (races == null || races.Length == 0)
            return Array.Empty<RaceCourseLine>();

        Array.Sort(races, (a, b) =>
        {
            bool aChamp = a != null && a.IsRegionalChampionship;
            bool bChamp = b != null && b.IsRegionalChampionship;
            if (aChamp != bChamp)
                return aChamp ? 1 : -1;

            return string.Compare(
                a != null ? a.RaceName : string.Empty,
                b != null ? b.RaceName : string.Empty,
                StringComparison.OrdinalIgnoreCase);
        });

        return races;
    }

    private void ApplyRacePolylineColors()
    {
        if (_mapUI == null)
            return;

        var races = GetSortedRaces();
        for (int i = 0; i < races.Length; i++)
        {
            RaceCourseLine race = races[i];
            if (race == null || !TryGetRacePolylineId(race, out string polylineId))
                continue;

            _mapUI.SetPolylineDisplayColorOverride(polylineId, GetRaceDisplayColor(race, i, desaturateIfUnselected: true));
        }
    }

    private Color GetRaceDisplayColor(RaceCourseLine race, int index, bool desaturateIfUnselected)
    {
        if (race == null)
            return GetRaceAccentColor(index);

        Color resolved = race.GetResolvedMapLineColor(GetRaceAccentColor(index));
        if (!desaturateIfUnselected || _selectedRace == null || _selectedRace == race)
            return resolved;

        return Color.Lerp(SelectedRaceDesaturatedColor, resolved, 0.22f);
    }

    private RaceCourseLine FindRaceById(string raceId)
    {
        if (string.IsNullOrWhiteSpace(raceId))
            return null;

        var races = FindObjectsOfType<RaceCourseLine>(includeInactive: false);
        for (int i = 0; i < races.Length; i++)
        {
            RaceCourseLine race = races[i];
            if (race == null)
                continue;

            if (string.Equals(race.RaceId, raceId, StringComparison.Ordinal) ||
                string.Equals(race.RaceName, raceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(race.gameObject.name, raceId, StringComparison.Ordinal))
            {
                return race;
            }

            if (TryGetRacePolylineId(race, out string polylineId) &&
                string.Equals(polylineId, raceId, StringComparison.Ordinal))
            {
                return race;
            }
        }

        return null;
    }

    private int GetMaxLeagueCountAcrossRaces()
    {
        int maxLeague = 0;

        var races = FindObjectsOfType<RaceCourseLine>(includeInactive: false);
        for (int i = 0; i < races.Length; i++)
        {
            RaceCourseLine race = races[i];
            if (race == null || race.Leagues == null)
                continue;

            maxLeague = Mathf.Max(maxLeague, race.Leagues.Count);
        }

        return Mathf.Max(1, maxLeague);
    }

    private GameObject ResolvePlayerRootObject()
    {
        if (playerTransform != null)
            return playerTransform.gameObject;

        Transform resolved = ResolvePlayerTransform();
        playerTransform = resolved;

        return playerTransform != null ? playerTransform.gameObject : null;
    }

    private Transform ResolvePlayerTransform()
    {
        SkiController ski = FindObjectOfType<SkiController>();
        if (ski != null)
            return ski.transform;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            return tagged.transform;

        return null;
    }

    private RaceCourseLine[] GetStandardRaces()
    {
        var races = GetSortedRaces();
        if (races == null || races.Length == 0)
            return Array.Empty<RaceCourseLine>();

        var list = new System.Collections.Generic.List<RaceCourseLine>();
        for (int i = 0; i < races.Length; i++)
        {
            if (races[i] != null && !races[i].IsRegionalChampionship)
                list.Add(races[i]);
        }

        return list.ToArray();
    }

    private RaceCourseLine GetChampionshipRace()
    {
        var races = GetSortedRaces();
        for (int i = 0; i < races.Length; i++)
        {
            if (races[i] != null && races[i].IsRegionalChampionship)
                return races[i];
        }

        return null;
    }

    private int GetPreviewLeagueForRace(RaceCourseLine race)
    {
        if (race == null)
            return 1;

        if (_selectedRace == race && race.GetLeague(_selectedLeagueNumber) != null)
            return _selectedLeagueNumber;

        return Mathf.Max(1, race.GetHighestUnlockedLeagueNumber());
    }

    private int GetPreferredLeagueForRace(RaceCourseLine race)
    {
        if (race == null)
            return 1;

        if (race.IsRegionalChampionship)
        {
            int required = Mathf.Max(1, race.ChampionshipRequiredLeagueNumber);
            if (race.GetLeague(required) != null)
                return required;
        }

        return Mathf.Max(1, race.GetHighestUnlockedLeagueNumber());
    }
    private bool TryGetRacePolylineId(RaceCourseLine race, out string polylineId)
    {
        polylineId = null;
        if (race == null || mapData == null || mapData.Polylines == null)
            return false;

        string raceName = race.RaceName;
        string raceId = race.RaceId;
        string objectName = race.gameObject.name;

        for (int i = 0; i < mapData.Polylines.Count; i++)
        {
            var poly = mapData.Polylines[i];
            if (!poly.IsValid || poly.lineType != MapLineType.RaceCourse)
                continue;

            if ((!string.IsNullOrWhiteSpace(raceId) && string.Equals(poly.id, raceId, StringComparison.Ordinal)) ||
                string.Equals(poly.id, objectName, StringComparison.Ordinal) ||
                string.Equals(poly.id, raceName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(poly.displayName, raceName, StringComparison.OrdinalIgnoreCase))
            {
                polylineId = poly.id;
                return true;
            }
        }

        return false;
    }
}
