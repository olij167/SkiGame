using System.Collections.Generic;
using SkiGame.Activities;
using UnityEngine;
using SkiGame.Navigation;
using SkiGame.Progression;

[DisallowMultipleComponent]
public sealed class RescueService : MonoBehaviour
{
    public enum RescueMissionRevealMode
    {
        ExactLocation = 0,
        SearchArea = 1
    }

    public enum RescueMissionTargetMode
    {
        InjuredOnly = 0,
        InjuredWithCompanion = 1,
        StrandedHealthy = 2
    }

    public struct GeneratedMission
    {
        public MedicTentActivityHub sourceHub;
        public Vector3 rescuePoint;
        public Vector3 rescueNormal;
        public Vector3 searchAreaCenter;
        public float searchAreaRadius;
        public int casualtyCount;
        public float timeLimitSeconds;
        public RescueMissionTargetMode targetMode;
        public RescueMissionRevealMode revealMode;
        public bool useSnowmobile;
        public int missionTier;
    }

    [Header("Optional Scene References")]
    [SerializeField] private GameObject casualtyPrefab;
    [SerializeField] private GameObject companionPrefab;
    [SerializeField] private RescueStretcherController stretcherPrefab;
    [SerializeField] private float passengerBoardDistance = 6f;

    [Header("Navigation")]
    [SerializeField] private Color rescueExactMarkerColor = new Color(1f, 0.35f, 0.15f, 1f);
    [SerializeField] private int rescueNavigationPriority = 60;
    [SerializeField] private float rescueArriveDistance = 8f;

    [SerializeField] private Color rescueSearchAreaColor = new Color(1f, 0.82f, 0.18f, 1f);
    [SerializeField] private Color rescueReturnColor = new Color(0.25f, 0.95f, 0.35f, 1f);
    [SerializeField] private float casualtyRevealRadius = 40f;
    [SerializeField] private float searchAreaArriveDistance = 24f;
    [SerializeField] private float returnArriveDistance = 8f;
    [SerializeField] private float searchAreaVisualHeight = 0.35f;

    [Header("Runtime")]
    [SerializeField] private float casualtySpreadRadius = 8f;
    [SerializeField] private float casualtySecureRadius = 4f;

    [Header("Spawn Placement")]
    [SerializeField] private LayerMask spawnGroundMask = ~0;
    [SerializeField] private float stretcherSpawnTerrainClearance = 0.35f;
    [SerializeField] private float spawnGroundProbeHeight = 6f;
    [SerializeField] private float spawnGroundProbeDistance = 20f;

    [Header("Snowmobile Mission Rules")]
    [SerializeField] private bool autoMountSnowmobileOnStart = true;
    [SerializeField] private float snowmobileAbandonGraceSeconds = 6f;

    [Header("Rewards")]
    [SerializeField] private int baseCompletionReward = 90;
    [SerializeField] private int rewardPerMissionTier = 30;
    [SerializeField] private int searchAreaBonus = 20;
    [SerializeField] private int injuredCasualtyBonus = 15;
    [SerializeField] private int companionBonus = 10;
    [SerializeField] private int fastResponseBonus = 25;
    [SerializeField] private float fastResponseThresholdSeconds = 120f;

    public static RescueService Instance { get; private set; }

    private GeneratedMission _activeMission;
    private bool _active;
    private float _elapsed;
    private GameObject _activePlayerRoot;
    private readonly List<Vector3> _casualtyPositions = new List<Vector3>();
    private readonly List<bool> _casualtySecured = new List<bool>();
    private readonly List<RescueCasualtyTarget> _spawnedCasualties = new List<RescueCasualtyTarget>();
    private readonly List<GameObject> _spawnedCompanions = new List<GameObject>();

    private readonly HashSet<int> _revealedCasualtyIndices = new HashSet<int>();
    private bool _searchAreaNavigationRegistered;
    private bool _returnNavigationRegistered;

    private readonly List<RescueTargetKind> _targetKinds = new List<RescueTargetKind>();
    private RescueStretcherController _activeStretcher;
    private SnowmobileController _activeSnowmobile;
    private GameObject _searchAreaVisual;
    private Material _searchAreaVisualMaterial;

    private bool _awaitingReturnToSnowmobile;
    private float _returnToSnowmobileCountdown;

    private int _lastMissionRewardGranted;
    private bool _lastMissionRankIncreased;
    private int _lastMissionNewRank;
    private float _lastMissionCompletionSeconds = -1f;

    public bool HasActiveMission => _active;
    public GeneratedMission ActiveMission => _activeMission;
    public int RemainingCasualties
    {
        get
        {
            int remaining = 0;
            for (int i = 0; i < _casualtySecured.Count; i++)
            {
                if (!_casualtySecured[i])
                    remaining++;
            }
            return remaining;
        }
    }

    public float ElapsedSeconds => _elapsed;
    public float TimeRemainingSeconds => _activeMission.timeLimitSeconds > 0f ? Mathf.Max(0f, _activeMission.timeLimitSeconds - _elapsed) : 0f;
    public bool IsExactLocationMission => _active && _activeMission.revealMode == RescueMissionRevealMode.ExactLocation;
    public bool IsSearchAreaMission => _active && _activeMission.revealMode == RescueMissionRevealMode.SearchArea;
    public Vector3 SearchAreaCenter => _activeMission.searchAreaCenter;
    public float SearchAreaRadius => _activeMission.searchAreaRadius;

    public int TotalCasualties => _casualtySecured != null ? _casualtySecured.Count : 0;
    public int SecuredCasualties => Mathf.Max(0, TotalCasualties - RemainingCasualties);

    public bool IsReturnPhase => _active && RemainingCasualties <= 0;
    public bool UsesSnowmobile => _active && _activeMission.useSnowmobile;

    public RescueMissionRevealMode MissionRevealMode => _activeMission.revealMode;
    public RescueMissionTargetMode MissionTargetMode => _activeMission.targetMode;

    public string MissionDisplayName
    {
        get
        {
            if (_activeMission.sourceHub == null)
                return "Rescue Mission";

            string tentName = string.IsNullOrWhiteSpace(_activeMission.sourceHub.TentName)
                ? "Medic Tent"
                : _activeMission.sourceHub.TentName.Trim();

            return $"{tentName} Rescue";
        }
    }

    public SnowmobileController ActiveSnowmobile => _activeSnowmobile;
    public RescueStretcherController ActiveStretcher => _activeStretcher;
    public GameObject ActivePlayerRoot => _activePlayerRoot;

    public bool IsAwaitingReturnToSnowmobile => _active && _awaitingReturnToSnowmobile;
    public float ReturnToSnowmobileSecondsRemaining => _active && _awaitingReturnToSnowmobile
        ? Mathf.Max(0f, _returnToSnowmobileCountdown)
        : 0f;

    public string ReturnToSnowmobileWarningText
    {
        get
        {
            if (!IsAwaitingReturnToSnowmobile)
                return string.Empty;

            return $"Return to the snowmobile or the rescue will be abandoned in {Mathf.CeilToInt(ReturnToSnowmobileSecondsRemaining)}s";
        }
    }

    public int LastMissionRewardGranted => _lastMissionRewardGranted;
    public bool LastMissionRankIncreased => _lastMissionRankIncreased;
    public int LastMissionNewRank => _lastMissionNewRank;
    public float LastMissionCompletionSeconds => _lastMissionCompletionSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (!_active)
            return;

        if (_activePlayerRoot == null || _activeMission.sourceHub == null)
        {
            FailMission("Player lost");
            return;
        }

        _elapsed += Time.deltaTime;

        if (_activeMission.timeLimitSeconds > 0f && _elapsed > _activeMission.timeLimitSeconds)
        {
            FailMission("Time limit exceeded");
            return;
        }

        Vector3 playerPos = _activePlayerRoot.transform.position;

        if (IsSearchAreaMission)
            UpdateSearchAreaReveal(playerPos);

        UpdateSnowmobileMissionState();
        if (!_active)
            return;

        if (RemainingCasualties <= 0)
        {
            if (!_returnNavigationRegistered)
                RegisterReturnNavigation();

            if (IsPlayerInsideReturnArea(playerPos))
                CompleteMission();
        }
    }

    private void UpdateSnowmobileMissionState()
    {
        if (!_active || !_activeMission.useSnowmobile)
        {
            _awaitingReturnToSnowmobile = false;
            _returnToSnowmobileCountdown = 0f;
            return;
        }

        if (_activeSnowmobile == null || _activePlayerRoot == null)
        {
            FailMission("Rescue snowmobile unavailable");
            return;
        }

        bool isMounted = _activeSnowmobile.IsPlayerMounted(_activePlayerRoot);

        if (isMounted)
        {
            _awaitingReturnToSnowmobile = false;
            _returnToSnowmobileCountdown = snowmobileAbandonGraceSeconds;
            return;
        }

        if (!_awaitingReturnToSnowmobile)
        {
            _awaitingReturnToSnowmobile = true;
            _returnToSnowmobileCountdown = snowmobileAbandonGraceSeconds;
        }
        else
        {
            _returnToSnowmobileCountdown -= Time.deltaTime;
        }

        if (_returnToSnowmobileCountdown <= 0f)
            FailMission("Rescue mission abandoned");
    }

    public bool TryStartMission(MedicTentActivityHub hub, GameObject playerRoot)
    {
        if (hub == null || playerRoot == null)
            return false;

        var mgr = MountainActivityManager.Instance;
        if (mgr == null)
            return false;

        if (!mgr.TryStart(MountainActivityKind.Rescue, hub, $"{hub.TentName} Rescue", 1))
            return false;

        if (!hub.TryGenerateMission(out _activeMission))
        {
            mgr.Fail(MountainActivityKind.Rescue, hub, "Could not generate mission");
            return false;
        }

        _active = true;
        _elapsed = 0f;
        _activePlayerRoot = playerRoot;
        _activeSnowmobile = _activeMission.useSnowmobile && hub != null ? hub.EnsureSnowmobileSpawned() : null;

        _awaitingReturnToSnowmobile = false;
        _returnToSnowmobileCountdown = 0f;
        _lastMissionRewardGranted = 0;
        _lastMissionRankIncreased = false;
        _lastMissionNewRank = RaceRescueProgression.GetRescueCareerRank();
        _lastMissionCompletionSeconds = -1f;

        if (PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Profile != null)
        {
            ProgressionEventRecorder.RecordRescueStart(PlayerStatsManager.Instance.Profile);
            PlayerStatsManager.Instance.Save();
        }

        if (autoMountSnowmobileOnStart && _activeMission.useSnowmobile && _activeSnowmobile != null)
            _activeSnowmobile.TryMountPlayer(playerRoot);

        _revealedCasualtyIndices.Clear();
        _searchAreaNavigationRegistered = false;
        _returnNavigationRegistered = false;

        BuildCasualties();

        if (_activeMission.targetMode != RescueMissionTargetMode.StrandedHealthy)
            SpawnMissionStretcher();

        RegisterInitialMissionNavigation();
        return true;
    }

    private void SpawnMissionStretcher()
    {
        if (stretcherPrefab == null || _activeMission.sourceHub == null)
            return;

        if (_activeStretcher != null)
            Destroy(_activeStretcher.gameObject);

        Vector3 spawnPos;
        Quaternion spawnRot;

        if (_activeSnowmobile != null && _activeSnowmobile.TowHitchTransform != null)
        {
            Transform hitch = _activeSnowmobile.TowHitchTransform;
            Vector3 backDir = -hitch.forward;
            backDir = Vector3.ProjectOnPlane(backDir, Vector3.up);

            if (backDir.sqrMagnitude < 0.001f)
                backDir = -_activeSnowmobile.transform.forward;

            if (backDir.sqrMagnitude < 0.001f)
                backDir = Vector3.back;

            backDir.Normalize();

            Vector3 rawSpawnPos = hitch.position + backDir * 3.0f;
            spawnPos = ProjectSpawnAboveTerrain(rawSpawnPos, stretcherSpawnTerrainClearance);
            spawnRot = Quaternion.LookRotation(hitch.position - spawnPos, Vector3.up);
        }
        else
        {
            Vector3 rawSpawnPos = _activeMission.sourceHub.ReturnPoint.position + _activeMission.sourceHub.ReturnPoint.right * 2f;
            spawnPos = ProjectSpawnAboveTerrain(rawSpawnPos, stretcherSpawnTerrainClearance);
            spawnRot = _activeMission.sourceHub.ReturnPoint.rotation;
        }

        _activeStretcher = Instantiate(stretcherPrefab, spawnPos, spawnRot);
        _activeStretcher.transform.SetParent(null, true);
        _activeStretcher.name = $"{_activeMission.sourceHub.TentName}_Stretcher";
        _activeStretcher.Initialize(this, _activeSnowmobile);
    }

    private Vector3 ProjectSpawnAboveTerrain(Vector3 worldPos, float clearance)
    {
        Vector3 rayOrigin = worldPos + Vector3.up * Mathf.Max(1f, spawnGroundProbeHeight);

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            Mathf.Max(2f, spawnGroundProbeDistance),
            spawnGroundMask,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point + hit.normal * Mathf.Max(0.05f, clearance);
        }

        return worldPos + Vector3.up * Mathf.Max(0.05f, clearance);
    }

    private void BuildCasualties()
    {
        ClearRuntimeObjects();

        _casualtyPositions.Clear();
        _casualtySecured.Clear();
        _targetKinds.Clear();

        switch (_activeMission.targetMode)
        {
            case RescueMissionTargetMode.StrandedHealthy:
                BuildSingleHealthyTarget();
                break;

            case RescueMissionTargetMode.InjuredWithCompanion:
                BuildInjuredTarget();
                BuildHealthyCompanionTarget();
                break;

            default:
                int count = Mathf.Max(1, _activeMission.casualtyCount);
                for (int i = 0; i < count; i++)
                    BuildInjuredTarget();
                break;
        }
    }

    private void BuildInjuredTarget()
    {
        Vector3 pos = _activeMission.rescuePoint;
        Vector3 normal = _activeMission.rescueNormal;

        if (_activeMission.sourceHub != null)
            _activeMission.sourceHub.TryGetValidCasualtyPointNear(_activeMission.rescuePoint, casualtySpreadRadius, out pos, out normal);

        SpawnTarget(pos, normal, RescueTargetKind.InjuredCasualty, casualtyPrefab, addCompanionSignal: false);
    }

    private void BuildHealthyCompanionTarget()
    {
        Vector3 pos = _activeMission.rescuePoint;
        Vector3 normal = _activeMission.rescueNormal;

        if (_activeMission.sourceHub != null)
            _activeMission.sourceHub.TryGetValidCasualtyPointNear(_activeMission.rescuePoint, 5f, out pos, out normal);

        SpawnTarget(pos, normal, RescueTargetKind.CompanionPassenger, companionPrefab != null ? companionPrefab : casualtyPrefab, addCompanionSignal: true);
    }

    private void BuildSingleHealthyTarget()
    {
        Vector3 pos = _activeMission.rescuePoint;
        Vector3 normal = _activeMission.rescueNormal;

        if (_activeMission.sourceHub != null)
            _activeMission.sourceHub.TryGetValidCasualtyPointNear(_activeMission.rescuePoint, casualtySpreadRadius, out pos, out normal);

        SpawnTarget(pos, normal, RescueTargetKind.StrandedPassenger, companionPrefab != null ? companionPrefab : casualtyPrefab, addCompanionSignal: IsExactLocationMission);
    }

    private void SpawnTarget(Vector3 pos, Vector3 normal, RescueTargetKind kind, GameObject prefab, bool addCompanionSignal)
    {
        int index = _casualtyPositions.Count;

        _casualtyPositions.Add(pos);
        _casualtySecured.Add(false);
        _targetKinds.Add(kind);

        if (prefab == null)
            return;

        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        go.name = $"RescueTarget_{index + 1:00}_{kind}";

        RescueCasualtyTarget target = go.GetComponent<RescueCasualtyTarget>();
        if (target == null)
            target = go.AddComponent<RescueCasualtyTarget>();

        target.Initialize(this, index, kind);

        if (kind == RescueTargetKind.InjuredCasualty)
        {
            NpcRescueCasualtyState casualtyState = go.GetComponent<NpcRescueCasualtyState>();
            if (casualtyState != null)
                casualtyState.InitializeAt(pos, normal);
            else
                go.transform.position = pos + normal * 0.05f;
        }
        else
        {
            go.transform.position = pos + normal * 0.05f;

            if (kind != RescueTargetKind.InjuredCasualty)
            {
                RescueCompanionSignal signal = go.GetComponent<RescueCompanionSignal>();
                if (signal == null)
                    signal = go.AddComponent<RescueCompanionSignal>();

                signal.Initialize(target);
            }
        }

        _spawnedCasualties.Add(target);

        if (IsExactLocationMission)
        {
            if (kind == RescueTargetKind.InjuredCasualty || kind == RescueTargetKind.StrandedPassenger)
                RegisterExactLocationNavigation(target, pos, index);

            _revealedCasualtyIndices.Add(index);
        }
    }

    private void RegisterInitialMissionNavigation()
    {
        if (!_active || _activeMission.sourceHub == null)
            return;

        if (IsExactLocationMission)
            return;

        EnsureSearchAreaVisual();

        NavigationTargetController.SetTarget(this, new NavigationTargetRequest
        {
            owner = this,
            id = "rescue-search-area",
            displayName = "Search Area",
            kind = NavigationTargetKind.Custom,
            targetTransform = null,
            worldPosition = _activeMission.searchAreaCenter,
            priority = rescueNavigationPriority,
            showHud = true,
            showWorldBeacon = true,
            clearWhenReached = false,
            arriveDistance = searchAreaArriveDistance,
            accentColor = rescueSearchAreaColor,
            preferMiniMapIndicator = false
        });

        _searchAreaNavigationRegistered = true;
    }

    private void UpdateSearchAreaReveal(Vector3 playerPos)
    {
        float distToCenter = Vector3.Distance(playerPos, _activeMission.searchAreaCenter);

        if (_searchAreaNavigationRegistered && distToCenter <= _activeMission.searchAreaRadius)
        {
            NavigationTargetController.ClearTarget(this);
            _searchAreaNavigationRegistered = false;
        }

        for (int i = 0; i < _casualtyPositions.Count; i++)
        {
            if (_casualtySecured[i])
                continue;

            if (_revealedCasualtyIndices.Contains(i))
                continue;

            float dist = Vector3.Distance(playerPos, _casualtyPositions[i]);
            if (dist > casualtyRevealRadius)
                continue;

            _revealedCasualtyIndices.Add(i);

            if (i >= 0 && i < _spawnedCasualties.Count && _spawnedCasualties[i] != null)
                RegisterExactLocationNavigation(_spawnedCasualties[i], _casualtyPositions[i], i);
        }
    }

    private void RegisterReturnNavigation()
    {
        if (_activeMission.sourceHub == null)
            return;

        NavigationTargetController.ClearTarget(this);

        Collider returnTrigger = _activeMission.sourceHub.InteractionAreaTrigger;
        Vector3 returnPos = returnTrigger != null
            ? returnTrigger.bounds.center
            : _activeMission.sourceHub.ReturnPoint.position;

        NavigationTargetController.SetTarget(this, new NavigationTargetRequest
        {
            owner = this,
            id = "rescue-return",
            displayName = $"{_activeMission.sourceHub.TentName}",
            kind = NavigationTargetKind.Custom,
            targetTransform = _activeMission.sourceHub.ReturnPoint,
            worldPosition = returnPos,
            priority = rescueNavigationPriority + 5,
            showHud = true,
            showWorldBeacon = true,
            clearWhenReached = false,
            arriveDistance = returnArriveDistance,
            accentColor = rescueReturnColor,
            preferMiniMapIndicator = false
        });

        _returnNavigationRegistered = true;
    }

    private void RegisterExactLocationNavigation(RescueCasualtyTarget target, Vector3 worldPosition, int casualtyIndex)
    {
        if (target == null)
            return;

        NavigationTargetController.SetTarget(target, new NavigationTargetRequest
        {
            owner = target,
            id = $"rescue-casualty-{casualtyIndex}",
            displayName = $"Rescue Target {casualtyIndex + 1}",
            kind = NavigationTargetKind.Custom,
            targetTransform = target.transform,
            worldPosition = worldPosition,
            priority = rescueNavigationPriority,
            showHud = true,
            showWorldBeacon = true,
            clearWhenReached = false,
            arriveDistance = rescueArriveDistance,
            accentColor = rescueExactMarkerColor,
            preferMiniMapIndicator = true
        });
    }

    private void SpawnCompanionNear(Vector3 casualtyPos, int casualtyIndex)
    {
        if (companionPrefab == null || _activeMission.sourceHub == null)
            return;

        if (!_activeMission.sourceHub.TryGetValidCasualtyPointNear(casualtyPos, 5f, out Vector3 companionPos, out Vector3 companionNormal))
            return;

        GameObject companion = Instantiate(companionPrefab, companionPos, Quaternion.identity);
        companion.name = $"RescueCompanion_{casualtyIndex + 1:00}";

        if (companion.GetComponent<RescueCompanionSignal>() == null)
            companion.AddComponent<RescueCompanionSignal>();

        _spawnedCompanions.Add(companion);
    }

    public bool IsCasualtySecureable(int casualtyIndex)
    {
        return _active &&
               casualtyIndex >= 0 &&
               casualtyIndex < _casualtySecured.Count &&
               !_casualtySecured[casualtyIndex];
    }

    public bool IsTargetInteractable(int targetIndex)
    {
        return _active &&
               targetIndex >= 0 &&
               targetIndex < _casualtySecured.Count &&
               !_casualtySecured[targetIndex];
    }

    public void NotifyTargetDroppedFromTransport(int targetIndex, Vector3 worldPos)
    {
        if (targetIndex < 0 || targetIndex >= _casualtySecured.Count)
            return;

        _casualtySecured[targetIndex] = false;

        if (targetIndex >= 0 && targetIndex < _spawnedCasualties.Count && _spawnedCasualties[targetIndex] != null)
        {
            RescueCasualtyTarget target = _spawnedCasualties[targetIndex];
            target.SetTransportLocked(false);
            target.transform.position = worldPos;

            if (_revealedCasualtyIndices.Contains(targetIndex))
                RegisterExactLocationNavigation(target, worldPos, targetIndex);
        }

        _returnNavigationRegistered = false;
        NavigationTargetController.ClearTarget(this);
    }

    public bool TrySecureCasualty(int casualtyIndex)
    {
        if (!IsCasualtySecureable(casualtyIndex))
            return false;

        _casualtySecured[casualtyIndex] = true;

        if (casualtyIndex >= 0 && casualtyIndex < _spawnedCasualties.Count && _spawnedCasualties[casualtyIndex] != null)
        {
            NavigationTargetController.ClearTarget(_spawnedCasualties[casualtyIndex]);
            _spawnedCasualties[casualtyIndex].gameObject.SetActive(false);
        }

        if (casualtyIndex >= 0 && casualtyIndex < _spawnedCompanions.Count && _spawnedCompanions[casualtyIndex] != null)
            _spawnedCompanions[casualtyIndex].SetActive(false);

        if (RemainingCasualties <= 0)
            RegisterReturnNavigation();

        return true;
    }

    public bool TryAutoCollectTarget(SnowmobileController snowmobile, RescueCasualtyTarget target)
    {
        if (!_active || snowmobile == null || target == null)
            return false;

        if (snowmobile != _activeSnowmobile)
            return false;

        int targetIndex = target.TargetIndex;
        if (!IsTargetInteractable(targetIndex))
            return false;

        RescueTargetKind kind = _targetKinds[targetIndex];
        if (kind != RescueTargetKind.CompanionPassenger && kind != RescueTargetKind.StrandedPassenger)
            return false;

        if (!snowmobile.IsMounted || !snowmobile.CanBoardNpcPassenger())
            return false;

        if (!snowmobile.TryBoardNpcPassenger(target.gameObject))
            return false;

        target.SetTransportLocked(true);
        _casualtySecured[targetIndex] = true;
        NavigationTargetController.ClearTarget(target);

        if (RemainingCasualties <= 0)
            RegisterReturnNavigation();

        return true;
    }

    public bool TryAutoCollectTarget(RescueStretcherController stretcher, RescueCasualtyTarget target)
    {
        if (!_active || stretcher == null || target == null)
            return false;

        if (stretcher != _activeStretcher)
            return false;

        int targetIndex = target.TargetIndex;
        if (!IsTargetInteractable(targetIndex))
            return false;

        RescueTargetKind kind = _targetKinds[targetIndex];
        if (kind != RescueTargetKind.InjuredCasualty)
            return false;

        if (_activeSnowmobile == null || !_activeSnowmobile.IsMounted)
            return false;

        if (!stretcher.LoadCasualty(target, targetIndex) &&
    !stretcher.LoadCasualtyFromSnowmobileAssist(target, targetIndex))
            return false;

        _casualtySecured[targetIndex] = true;
        NavigationTargetController.ClearTarget(target);

        if (RemainingCasualties <= 0)
            RegisterReturnNavigation();

        return true;
    }

    private bool IsPlayerInsideReturnArea(Vector3 playerPos)
    {
        if (_activeMission.sourceHub == null)
            return false;

        Collider returnTrigger = _activeMission.sourceHub.InteractionAreaTrigger;
        if (returnTrigger != null)
        {
            Vector3 closest = returnTrigger.ClosestPoint(playerPos);
            float sqr = (closest - playerPos).sqrMagnitude;
            if (sqr <= 0.25f)
                return true;
        }

        return Vector3.Distance(playerPos, _activeMission.sourceHub.ReturnPoint.position) <= returnArriveDistance;
    }

    private void CompleteMission()
    {
        _lastMissionCompletionSeconds = _elapsed;
        _lastMissionRewardGranted = CalculateCompletionReward();
        _lastMissionRankIncreased = false;
        _lastMissionNewRank = RaceRescueProgression.GetRescueCareerRank();

        RaceRescueProgression.RecordRescueMissionCompleted(
            _lastMissionRewardGranted,
            _elapsed,
            out _lastMissionRankIncreased,
            out _lastMissionNewRank);

        var mgr = MountainActivityManager.Instance;
        if (mgr != null && _activeMission.sourceHub != null)
            mgr.Complete(MountainActivityKind.Rescue, _activeMission.sourceHub, "Completed");

        ClearMission();
    }

    private void FailMission(string reason)
    {
        _lastMissionRewardGranted = 0;
        _lastMissionRankIncreased = false;
        _lastMissionNewRank = RaceRescueProgression.GetRescueCareerRank();
        _lastMissionCompletionSeconds = -1f;
        RaceRescueProgression.RecordRescueMissionFailed();

        var mgr = MountainActivityManager.Instance;
        if (mgr != null && _activeMission.sourceHub != null)
            mgr.Fail(MountainActivityKind.Rescue, _activeMission.sourceHub, reason);

        ClearMission();
    }

    private void ClearMission()
    {
        MedicTentActivityHub sourceHub = _activeMission.sourceHub;

        _active = false;
        _elapsed = 0f;
        _activePlayerRoot = null;

        NavigationTargetController.ClearTarget(this);
        _revealedCasualtyIndices.Clear();
        _searchAreaNavigationRegistered = false;
        _returnNavigationRegistered = false;

        if (_activeSnowmobile != null && sourceHub != null && _activeSnowmobile.HasNpcPassenger)
            _activeSnowmobile.ReleaseNpcPassenger(sourceHub.ReturnPoint.position, sourceHub.ReturnPoint.rotation);

        if (_activeStretcher != null)
            Destroy(_activeStretcher.gameObject);

        _activeStretcher = null;
        _activeSnowmobile = null;

        _activeMission = default;
        _casualtyPositions.Clear();
        _casualtySecured.Clear();
        _targetKinds.Clear();

        _awaitingReturnToSnowmobile = false;
        _returnToSnowmobileCountdown = 0f;

        ClearRuntimeObjects();
    }

    private void ClearRuntimeObjects()
    {
        DestroySearchAreaVisual();

        for (int i = 0; i < _spawnedCasualties.Count; i++)
        {
            if (_spawnedCasualties[i] != null)
            {
                NavigationTargetController.ClearTarget(_spawnedCasualties[i]);
                Destroy(_spawnedCasualties[i].gameObject);
            }
        }

        for (int i = 0; i < _spawnedCompanions.Count; i++)
        {
            if (_spawnedCompanions[i] != null)
                Destroy(_spawnedCompanions[i]);
        }

        _spawnedCasualties.Clear();
        _spawnedCompanions.Clear();
    }

    private int CalculateCompletionReward()
    {
        int reward = Mathf.Max(0, baseCompletionReward);
        reward += Mathf.Max(0, rewardPerMissionTier) * Mathf.Max(1, _activeMission.missionTier);

        if (_activeMission.revealMode == RescueMissionRevealMode.SearchArea)
            reward += Mathf.Max(0, searchAreaBonus);

        switch (_activeMission.targetMode)
        {
            case RescueMissionTargetMode.InjuredOnly:
                reward += Mathf.Max(0, injuredCasualtyBonus) * Mathf.Max(1, _activeMission.casualtyCount);
                break;

            case RescueMissionTargetMode.InjuredWithCompanion:
                reward += Mathf.Max(0, injuredCasualtyBonus);
                reward += Mathf.Max(0, companionBonus);
                break;

            case RescueMissionTargetMode.StrandedHealthy:
                reward += Mathf.Max(0, companionBonus);
                break;
        }

        if (_elapsed > 0f && _elapsed <= Mathf.Max(1f, fastResponseThresholdSeconds))
            reward += Mathf.Max(0, fastResponseBonus);

        return reward;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_active)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_activeMission.rescuePoint, casualtySpreadRadius);

        Gizmos.color = Color.green;
        for (int i = 0; i < _casualtyPositions.Count; i++)
        {
            Gizmos.DrawWireSphere(_casualtyPositions[i], 1.5f);
        }
    }

    private void EnsureSearchAreaVisual()
    {
        if (!IsSearchAreaMission)
        {
            DestroySearchAreaVisual();
            return;
        }

        if (_searchAreaVisual == null)
        {
            _searchAreaVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _searchAreaVisual.name = "RescueSearchAreaVisual";

            Collider collider = _searchAreaVisual.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            Renderer renderer = _searchAreaVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader flowingShader = Shader.Find("Custom/URP/InteractionAreaFlowingVolume");
                if (flowingShader != null)
                {
                    _searchAreaVisualMaterial = new Material(flowingShader);
                    renderer.material = _searchAreaVisualMaterial;
                }

                Color visualColor = rescueSearchAreaColor;
                visualColor.a = 0.45f;
                renderer.material.color = visualColor;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        float radius = Mathf.Max(2f, _activeMission.searchAreaRadius);
        _searchAreaVisual.transform.position = _activeMission.searchAreaCenter + Vector3.up * (searchAreaVisualHeight * 0.5f);
        _searchAreaVisual.transform.localScale = new Vector3(radius * 2f, Mathf.Max(0.05f, searchAreaVisualHeight * 0.5f), radius * 2f);
    }

    private void DestroySearchAreaVisual()
    {
        if (_searchAreaVisual != null)
            Destroy(_searchAreaVisual);

        if (_searchAreaVisualMaterial != null)
            Destroy(_searchAreaVisualMaterial);

        _searchAreaVisual = null;
        _searchAreaVisualMaterial = null;
    }
}
