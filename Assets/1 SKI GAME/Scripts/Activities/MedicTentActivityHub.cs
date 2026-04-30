using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;
using SkiGame.Activities;
using SkiGame.Map;
using SkiGame.Progression;

[DisallowMultipleComponent]
public sealed class MedicTentActivityHub : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Identity")]
    [SerializeField] private string tentName = "Medic Tent";
    [SerializeField] private string tentId;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Prompt")]
    [SerializeField] private bool requireHold = false;
    [SerializeField] private float holdSeconds = 0.15f;
    [SerializeField] private string promptText = "Start Rescue Mission";
    [SerializeField] private int promptPriority = 58;

    [Header("Vehicle Spawn")]
    [SerializeField] private float snowmobileSpawnTerrainClearance = 0.25f;
    [SerializeField] private float vehicleSpawnProbeHeight = 6f;
    [SerializeField] private float vehicleSpawnProbeDistance = 20f;

    [Header("Rescue Generation")]
    [SerializeField] private bool useBoxArea = false;
    [SerializeField] private Vector3 rescueAreaOffset = Vector3.zero;
    [SerializeField] private float rescueRadiusMeters = 300f;
    [SerializeField] private Vector3 rescueBoxSize = new Vector3(300f, 80f, 300f);
    [SerializeField] private LayerMask terrainMask = ~0;
    [SerializeField] private float sampleHeightAboveArea = 400f;
    [SerializeField] private float minDistanceFromTent = 80f;
    [SerializeField] private int maxSampleAttempts = 16;
    [SerializeField] private MapRegionSet regionSet;
    [SerializeField] private string assignedRegionId;

    [SerializeField] private int searchZoneSampleAttempts = 12;
    [SerializeField] private float searchZoneEdgePaddingMeters = 14f;
    [SerializeField] private float searchTargetMinOffsetMeters = 18f;

    [Header("Mission Tuning")]
    [SerializeField] private Vector2Int casualtyCountRange = new Vector2Int(1, 2);
    [SerializeField] private Vector2 timeLimitRangeSeconds = new Vector2(120f, 300f);
    [SerializeField] private bool revealExactLocationOnEasy = true;
    [SerializeField] private bool useSnowmobile = true;

    [SerializeField, Range(0f, 1f)] private float injuredWithCompanionChance = 0.30f;
    [SerializeField, Range(0f, 1f)] private float strandedHealthyChance = 0.20f;

    [SerializeField, Range(0f, 1f)] private float exactLocationMissionChance = 0.5f;
    [SerializeField] private float maxSpawnSlope = 38f;
    [SerializeField] private float spawnClearanceRadius = 2.25f;
    [SerializeField] private LayerMask spawnBlockingMask = ~0;
    [SerializeField] private float searchAreaRadiusMeters = 90f;
    [SerializeField] private Vector2 tier3SearchAreaRadiusRangeMeters = new Vector2(70f, 110f);
    [SerializeField] private Vector2 tier4SearchAreaRadiusRangeMeters = new Vector2(90f, 140f);

    [Header("Scene")]
    [SerializeField] private Transform snowmobileSpawnPoint;
    [SerializeField] private Transform returnPoint;
    [SerializeField] private Collider interactionAreaTrigger;
    [SerializeField] private SnowmobileController snowmobilePrefab;
    private SnowmobileController _spawnedSnowmobile;

    private GameObject _playerRootInTrigger;
    private bool _wasPressed;
    private float _held;
    private bool _enterArmed;

    public string TentName => string.IsNullOrWhiteSpace(tentName) ? name : tentName.Trim();
    public string TentId => tentId;
    public Transform SnowmobileSpawnPoint => snowmobileSpawnPoint != null ? snowmobileSpawnPoint : transform;
    public Transform ReturnPoint => returnPoint != null ? returnPoint : transform;
    public bool UseSnowmobile => useSnowmobile;
    public bool RevealExactLocationOnEasy => revealExactLocationOnEasy;
    public Vector3 RescueAreaCenter => transform.position + rescueAreaOffset;
    public float MountedStartHoldSeconds => Mathf.Max(0.35f, holdSeconds);
    public Collider InteractionAreaTrigger => interactionAreaTrigger != null ? interactionAreaTrigger : GetComponent<Collider>();

    public string AssignedRegionId => string.IsNullOrWhiteSpace(assignedRegionId) ? string.Empty : assignedRegionId.Trim();

    private void Reset()
    {
        EnsureTentId();
    }

    private void OnValidate()
    {
        EnsureTentId();

        if (interactionAreaTrigger == null)
            interactionAreaTrigger = GetComponent<Collider>();

        if (regionSet == null)
        {
            MapRegionSet[] regionSets = Resources.FindObjectsOfTypeAll<MapRegionSet>();
            regionSet = regionSets.Length > 0 ? regionSets[0] : null;
        }

        if (rescueRadiusMeters < 20f) rescueRadiusMeters = 20f;
        if (rescueBoxSize.x < 20f) rescueBoxSize.x = 20f;
        if (rescueBoxSize.z < 20f) rescueBoxSize.z = 20f;

        casualtyCountRange.x = Mathf.Max(1, casualtyCountRange.x);
        casualtyCountRange.y = Mathf.Max(casualtyCountRange.x, casualtyCountRange.y);

        timeLimitRangeSeconds.x = Mathf.Max(30f, timeLimitRangeSeconds.x);
        timeLimitRangeSeconds.y = Mathf.Max(timeLimitRangeSeconds.x, timeLimitRangeSeconds.y);

        exactLocationMissionChance = Mathf.Clamp01(exactLocationMissionChance);
        maxSpawnSlope = Mathf.Clamp(maxSpawnSlope, 5f, 85f);
        spawnClearanceRadius = Mathf.Max(0.25f, spawnClearanceRadius);
        searchAreaRadiusMeters = Mathf.Max(20f, searchAreaRadiusMeters);
        tier3SearchAreaRadiusRangeMeters.x = Mathf.Max(20f, tier3SearchAreaRadiusRangeMeters.x);
        tier3SearchAreaRadiusRangeMeters.y = Mathf.Max(tier3SearchAreaRadiusRangeMeters.x, tier3SearchAreaRadiusRangeMeters.y);
        tier4SearchAreaRadiusRangeMeters.x = Mathf.Max(20f, tier4SearchAreaRadiusRangeMeters.x);
        tier4SearchAreaRadiusRangeMeters.y = Mathf.Max(tier4SearchAreaRadiusRangeMeters.x, tier4SearchAreaRadiusRangeMeters.y);

        injuredWithCompanionChance = Mathf.Clamp01(injuredWithCompanionChance);
        strandedHealthyChance = Mathf.Clamp01(strandedHealthyChance);
    }

    private void OnEnable()
    {
        WorldInteractionPromptRegistry.Register(this);

        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        WorldInteractionPromptRegistry.Unregister(this);
    }

    private void Update()
    {
        if (_playerRootInTrigger == null) return;
        if (interactAction == null || interactAction.action == null) return;
        if (RescueService.Instance == null) return;

        var mgr = MountainActivityManager.Instance;
        if (mgr == null) return;

        if (ShouldDeferInteractionToOwnedSnowmobile())
            return;

        var action = interactAction.action;
        if (!action.enabled) action.Enable();

        bool pressed = action.IsPressed();

        if (!_enterArmed)
        {
            if (!pressed) _enterArmed = true;
            _wasPressed = false;
            _held = 0f;
            return;
        }

        if (!pressed)
        {
            _wasPressed = false;
            _held = 0f;
            return;
        }

        if (!_wasPressed)
        {
            _wasPressed = true;
            _held = 0f;

            if (!requireHold)
                Trigger();

            return;
        }

        if (requireHold)
        {
            _held += Time.unscaledDeltaTime;
            if (_held >= holdSeconds)
            {
                _held = -999f;
                Trigger();
            }
        }
    }

    private void Trigger()
    {
        if (_playerRootInTrigger == null || RescueService.Instance == null)
            return;

        RescueService.Instance.TryStartMission(this, _playerRootInTrigger);
    }

    public bool TryGenerateMission(out RescueService.GeneratedMission mission)
    {
        mission = default;

        int rescueRank = RaceRescueProgression.GetRescueCareerRank();
        int missionTier = Mathf.Clamp(rescueRank, 1, 4);

        RescueService.RescueMissionTargetMode targetMode;
        RescueService.RescueMissionRevealMode revealMode;
        int targetCount;

        switch (missionTier)
        {
            case 1:
                targetMode = RescueService.RescueMissionTargetMode.StrandedHealthy;
                revealMode = RescueService.RescueMissionRevealMode.ExactLocation;
                targetCount = 1;
                break;

            case 2:
                targetMode = RescueService.RescueMissionTargetMode.InjuredWithCompanion;
                revealMode = RescueService.RescueMissionRevealMode.ExactLocation;
                targetCount = 1;
                break;

            case 3:
                targetMode = Random.value < 0.5f
                    ? RescueService.RescueMissionTargetMode.StrandedHealthy
                    : RescueService.RescueMissionTargetMode.InjuredOnly;
                revealMode = RescueService.RescueMissionRevealMode.SearchArea;
                targetCount = 1;
                break;

            default:
                targetMode = RescueService.RescueMissionTargetMode.InjuredOnly;
                revealMode = Random.value <= exactLocationMissionChance
                    ? RescueService.RescueMissionRevealMode.ExactLocation
                    : RescueService.RescueMissionRevealMode.SearchArea;
                targetCount = Mathf.Clamp(Random.Range(casualtyCountRange.x, casualtyCountRange.y + 1), 1, 3);
                break;
        }

        Vector3 rescuePoint;
        Vector3 rescueNormal;
        Vector3 searchAreaCenter;
        float searchAreaRadius = ResolveSearchAreaRadius(missionTier);

        bool generated;

        if (revealMode == RescueService.RescueMissionRevealMode.SearchArea)
        {
            generated = TryGenerateSearchMissionPoints(
                searchAreaRadius,
                out searchAreaCenter,
                out rescuePoint,
                out rescueNormal);

            // Fallback: if we cannot build a valid search mission, degrade cleanly
            // to an exact-location mission rather than failing mission generation.
            if (!generated)
            {
                revealMode = RescueService.RescueMissionRevealMode.ExactLocation;
                generated = TryGetRandomRescuePoint(out rescuePoint, out rescueNormal);
                searchAreaCenter = rescuePoint;
            }
        }
        else
        {
            generated = TryGetRandomRescuePoint(out rescuePoint, out rescueNormal);
            searchAreaCenter = rescuePoint;
        }

        if (!generated)
            return false;

        mission.sourceHub = this;
        mission.rescuePoint = rescuePoint;
        mission.rescueNormal = rescueNormal;
        mission.searchAreaCenter = searchAreaCenter;
        mission.searchAreaRadius = searchAreaRadius;
        mission.casualtyCount = targetCount;
        mission.timeLimitSeconds = Random.Range(timeLimitRangeSeconds.x, timeLimitRangeSeconds.y + 0.001f);
        mission.revealMode = revealMode;
        mission.targetMode = targetMode;
        mission.useSnowmobile = useSnowmobile;
        mission.missionTier = missionTier;

        return true;
    }

    private float ResolveSearchAreaRadius(int missionTier)
    {
        switch (Mathf.Clamp(missionTier, 1, 4))
        {
            case 3:
                return Random.Range(tier3SearchAreaRadiusRangeMeters.x, tier3SearchAreaRadiusRangeMeters.y + 0.001f);

            case 4:
                return Random.Range(tier4SearchAreaRadiusRangeMeters.x, tier4SearchAreaRadiusRangeMeters.y + 0.001f);

            default:
                return Mathf.Max(20f, searchAreaRadiusMeters);
        }
    }

    private bool TryGenerateSearchMissionPoints(
    float searchRadius,
    out Vector3 searchCenter,
    out Vector3 casualtyPoint,
    out Vector3 casualtyNormal)
    {
        searchCenter = default;
        casualtyPoint = default;
        casualtyNormal = Vector3.up;

        int attempts = Mathf.Max(1, searchZoneSampleAttempts);
        float paddedRadius = Mathf.Max(6f, searchRadius - Mathf.Max(0f, searchZoneEdgePaddingMeters + spawnClearanceRadius));
        float minOffset = Mathf.Clamp(searchTargetMinOffsetMeters, 0f, paddedRadius * 0.85f);

        for (int i = 0; i < attempts; i++)
        {
            if (!TryGetRandomRescuePoint(out Vector3 centerPoint, out Vector3 centerNormal))
                continue;

            if (!TryGetSearchTargetPoint(centerPoint, paddedRadius, minOffset, out Vector3 targetPoint, out Vector3 targetNormal))
                continue;

            searchCenter = centerPoint;
            casualtyPoint = targetPoint;
            casualtyNormal = targetNormal;
            return true;
        }

        return false;
    }

    private bool TryGetSearchTargetPoint(
        Vector3 center,
        float maxRadius,
        float minRadius,
        out Vector3 point,
        out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;

        int attempts = Mathf.Max(maxSampleAttempts, 8);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 offset2D = SampleRingOffset(minRadius, maxRadius);
            Vector3 candidate = center + new Vector3(offset2D.x, 0f, offset2D.y);

            if (!TryProjectValidSpawn(candidate, out Vector3 resolvedPoint, out Vector3 resolvedNormal))
                continue;

            if (!IsPointInsideAssignedRegion(resolvedPoint))
                continue;

            float dist = Vector3.Distance(center, resolvedPoint);
            if (dist > maxRadius)
                continue;

            point = resolvedPoint;
            normal = resolvedNormal;
            return true;
        }

        return false;
    }

    private static Vector2 SampleRingOffset(float minRadius, float maxRadius)
    {
        minRadius = Mathf.Max(0f, minRadius);
        maxRadius = Mathf.Max(minRadius, maxRadius);

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        dir.Normalize();

        float radius = Random.Range(minRadius, maxRadius);
        return dir * radius;
    }

    public bool TryGetRandomRescuePoint(out Vector3 point, out Vector3 normal)
    {
        Vector3 areaCenter = RescueAreaCenter;

        Vector3 bestPoint = default;
        Vector3 bestNormal = Vector3.up;
        bool foundAnyTerrainPoint = false;

        for (int i = 0; i < Mathf.Max(1, maxSampleAttempts); i++)
        {
            Vector3 candidate = useBoxArea ? SampleBoxPoint(areaCenter) : SampleRadiusPoint(areaCenter);

            // First try the strict validated path.
            if (TryProjectValidSpawn(candidate, out point, out normal))
            {
                if (Vector3.Distance(transform.position, point) < minDistanceFromTent)
                    continue;

                if (!IsPointInsideAssignedRegion(point))
                    continue;

                return true;
            }

            // Fallback: remember any terrain point that at least raycasts successfully,
            // even if clearance failed, so the mission can still begin.
            Vector3 rayOrigin = candidate + Vector3.up * Mathf.Max(10f, sampleHeightAboveArea);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, sampleHeightAboveArea * 2f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope <= maxSpawnSlope && IsPointInsideAssignedRegion(hit.point))
                {
                    bestPoint = hit.point;
                    bestNormal = hit.normal;
                    foundAnyTerrainPoint = true;
                }
            }
        }

        if (foundAnyTerrainPoint)
        {
            point = bestPoint;
            normal = bestNormal;
            return true;
        }

        point = transform.position;
        normal = Vector3.up;
        return false;
    }

    private bool IsPointInsideAssignedRegion(Vector3 point)
    {
        if (regionSet == null || string.IsNullOrWhiteSpace(AssignedRegionId))
            return true;

        MapRegionFace face = regionSet.GetFaceById(AssignedRegionId);
        if (face == null || !face.IsValid)
            return true;

        MapData mapData = regionSet.MapData;
        if (mapData == null)
            return true;

        Vector2 uv = mapData.WorldToMapUV(point);
        return MapRegionUtility.ContainsFace(regionSet, face, uv);
    }

    public bool TryGetValidCasualtyPointNear(Vector3 center, float radius, out Vector3 point, out Vector3 normal)
    {
        Vector3 bestPoint = center;
        Vector3 bestNormal = Vector3.up;
        bool foundAnyTerrainPoint = false;

        for (int i = 0; i < Mathf.Max(1, maxSampleAttempts); i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

            if (TryProjectValidSpawn(candidate, out point, out normal) && IsPointInsideAssignedRegion(point))
                return true;

            Vector3 rayOrigin = candidate + Vector3.up * Mathf.Max(10f, sampleHeightAboveArea);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, sampleHeightAboveArea * 2f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope <= maxSpawnSlope)
                {
                    bestPoint = hit.point;
                    bestNormal = hit.normal;
                    foundAnyTerrainPoint = true;
                }
            }
        }

        point = bestPoint;
        normal = bestNormal;
        return foundAnyTerrainPoint;
    }

    private bool TryProjectValidSpawn(Vector3 candidate, out Vector3 point, out Vector3 normal)
    {
        Vector3 rayOrigin = candidate + Vector3.up * Mathf.Max(10f, sampleHeightAboveArea);

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, sampleHeightAboveArea * 2f, terrainMask, QueryTriggerInteraction.Ignore))
        {
            point = default;
            normal = Vector3.up;
            return false;
        }

        float slope = Vector3.Angle(hit.normal, Vector3.up);
        if (slope > maxSpawnSlope)
        {
            point = default;
            normal = Vector3.up;
            return false;
        }

        // Use a smaller, lifted clearance test so we do not reject valid terrain hits.
        Vector3 checkCenter = hit.point + hit.normal * 0.9f;

        bool blocked = false;
        if (spawnBlockingMask.value != 0)
        {
            Collider[] overlaps = Physics.OverlapSphere(
                checkCenter,
                spawnClearanceRadius,
                spawnBlockingMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider c = overlaps[i];
                if (c == null)
                    continue;

                // Ignore terrain itself and trigger volumes.
                if (c is TerrainCollider || c.isTrigger)
                    continue;

                blocked = true;
                break;
            }
        }

        if (blocked)
        {
            point = default;
            normal = Vector3.up;
            return false;
        }

        point = hit.point;
        normal = hit.normal;
        return true;
    }

    private Vector3 SampleRadiusPoint(Vector3 areaCenter)
    {
        Vector2 circle = Random.insideUnitCircle * rescueRadiusMeters;
        return areaCenter + new Vector3(circle.x, 0f, circle.y);
    }

    private Vector3 SampleBoxPoint(Vector3 areaCenter)
    {
        Vector3 half = rescueBoxSize * 0.5f;
        return areaCenter + new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z));
    }

    private void EnsureTentId()
    {
        if (string.IsNullOrWhiteSpace(tentId))
            tentId = System.Guid.NewGuid().ToString("N");
    }

    private void OnTriggerEnter(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null) return;

        _enterArmed = false;
        _playerRootInTrigger = root;
    }

    private void OnTriggerExit(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null) return;

        if (_playerRootInTrigger == root)
            _playerRootInTrigger = null;
    }

    private static GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        var t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("NPC"))
                return null;
            t = t.parent;
        }

        Transform playerTagged = null;
        t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player"))
            {
                playerTagged = t;
                break;
            }
            t = t.parent;
        }

        if (playerTagged == null)
            return null;

        var pi = other.GetComponentInParent<PlayerInput>();
        if (pi != null)
            return pi.gameObject;

        return playerTagged.gameObject;
    }

    public SnowmobileController EnsureSnowmobileSpawned()
    {
        if (!useSnowmobile || snowmobilePrefab == null)
            return null;

        Transform spawn = SnowmobileSpawnPoint;
        Vector3 spawnPos = ProjectVehicleSpawnAboveTerrain(spawn.position, snowmobileSpawnTerrainClearance);
        Quaternion spawnRot = spawn.rotation;

        if (_spawnedSnowmobile == null)
        {
            _spawnedSnowmobile = Instantiate(snowmobilePrefab, spawnPos, spawnRot);
            _spawnedSnowmobile.name = $"{TentName}_Snowmobile";
            _spawnedSnowmobile.SetOwnerTent(this);
            return _spawnedSnowmobile;
        }

        _spawnedSnowmobile.ForceResetForMissionStart(spawnPos, spawnRot);
        _spawnedSnowmobile.SetOwnerTent(this);
        return _spawnedSnowmobile;
    }

    private Vector3 ProjectVehicleSpawnAboveTerrain(Vector3 worldPos, float clearance)
    {
        Vector3 rayOrigin = worldPos + Vector3.up * Mathf.Max(1f, vehicleSpawnProbeHeight);

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            Mathf.Max(2f, vehicleSpawnProbeDistance),
            terrainMask,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point + hit.normal * Mathf.Max(0.05f, clearance);
        }

        return worldPos + Vector3.up * Mathf.Max(0.05f, clearance);
    }

    public bool CanMountedPlayerStartMission(GameObject playerRoot)
    {
        if (playerRoot == null)
            return false;

        bool playerIsEligibleAtTent =
            _playerRootInTrigger == playerRoot ||
            IsMountedPlayerInOwnedSnowmobileAtTent(playerRoot);

        if (!playerIsEligibleAtTent)
            return false;

        var mgr = MountainActivityManager.Instance;
        if (mgr == null || !mgr.CanStart(MountainActivityKind.Rescue, this))
            return false;

        return RescueService.Instance != null;
    }

    private bool IsWorldPointInsideInteractionArea(Vector3 worldPos, float tolerance = 0.75f)
    {
        Collider area = InteractionAreaTrigger;
        if (area == null)
            return false;

        Vector3 closest = area.ClosestPoint(worldPos);
        return (closest - worldPos).sqrMagnitude <= tolerance * tolerance;
    }

    private bool IsMountedPlayerInOwnedSnowmobileAtTent(GameObject playerRoot)
    {
        if (playerRoot == null || _spawnedSnowmobile == null)
            return false;

        if (!_spawnedSnowmobile.HasMountedPlayer(playerRoot))
            return false;

        return IsWorldPointInsideInteractionArea(_spawnedSnowmobile.transform.position);
    }

    public bool TryStartMissionFromMountedPlayer(GameObject playerRoot)
    {
        if (!CanMountedPlayerStartMission(playerRoot))
            return false;

        return RescueService.Instance != null && RescueService.Instance.TryStartMission(this, playerRoot);
    }

    private bool ShouldDeferInteractionToOwnedSnowmobile()
    {
        return _playerRootInTrigger != null &&
               _spawnedSnowmobile != null &&
               _spawnedSnowmobile.IsPlayerMounted(_playerRootInTrigger);
    }

    public bool IsPromptAvailable
    {
        get
        {
            var mgr = MountainActivityManager.Instance;
            return _playerRootInTrigger != null &&
                   mgr != null &&
                   mgr.CanStart(MountainActivityKind.Rescue, this);
        }
    }

    public string PromptActionText => "Interact";
    public string PromptDescriptionText => promptText;
    public bool PromptUsesHold => requireHold;
    public float PromptHoldDuration => holdSeconds;
    public Vector3 PromptWorldPosition => transform.position;
    public int PromptPriority => promptPriority;

    private void OnDrawGizmosSelected()
    {
        Vector3 areaCenter = RescueAreaCenter;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ReturnPoint.position, 2.25f);

        Gizmos.color = new Color(0.2f, 1f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, areaCenter);
        Gizmos.DrawWireSphere(areaCenter, 2f);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
        if (useBoxArea)
        {
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(areaCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, rescueBoxSize);
            Gizmos.matrix = prev;
        }
        else
        {
            Gizmos.DrawWireSphere(areaCenter, rescueRadiusMeters);
        }
    }
}
