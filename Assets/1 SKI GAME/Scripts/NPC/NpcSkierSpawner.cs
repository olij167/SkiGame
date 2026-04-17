using System.Collections;
using System.Collections.Generic;
using SkiGame.Runs;
using UnityEngine;

public class NpcSkierSpawner : MonoBehaviour
{
    public static NpcSkierSpawner Instance { get; private set; }

    [Header("Pool")]
    [Tooltip("Prefab used when creating pooled NPC skiers.")]
    [SerializeField] private NpcSkierBrain npcPrefab;

    [Tooltip("Optional parent transform for spawned NPC instances. Falls back to this spawner transform if left empty.")]
    [SerializeField] private Transform npcParent;

    [Tooltip("Total number of NPC instances to keep in the pool.")]
    [SerializeField] private int poolSize = 18;

    [Tooltip("Preferred number of NPCs that should remain active around the player.")]
    [SerializeField] private int targetActiveCount = 10;

    [Tooltip("If enabled, the pool is initialized automatically on Start once the scene is ready.")]
    [SerializeField] private bool initializeOnStart = true;

    [Tooltip("How many pooled NPCs to create per frame during runtime initialization.")]
    [SerializeField] private int createBatchSize = 2;

    [Tooltip("Maximum number of inactive pooled NPCs that can be reactivated during a single population refresh.")]
    [SerializeField] private int maxNewActivationsPerRefresh = 2;

    [Header("Player")]
    [Tooltip("Player transform used for spawn distance, visibility checks, and recycling. If empty, Camera.main is used at runtime.")]
    [SerializeField] private Transform player;

    [Tooltip("Active NPCs farther than this distance from the player are returned to the pool.")]
    [SerializeField] private float recycleRadius = 1000f;

    [Tooltip("How often, in seconds, the spawner evaluates and refreshes the active NPC population.")]
    [SerializeField] private float refreshInterval = 1.25f;

    [Tooltip("NPCs within this radius remain persistent around the player even if they briefly leave the camera view.")]
    [SerializeField] private float persistenceRadius = 260f;

    [Header("Scene Sources")]
    [Tooltip("Automatically find SkiRunLine objects in the loaded scene instead of using the manual run list below.")]
    [SerializeField] private bool autoFindRunsInScene = true;

    [Tooltip("Automatically find LiftLine objects in the loaded scene instead of using the manual lift list below.")]
    [SerializeField] private bool autoFindLiftsInScene = true;

    [Tooltip("Manual list of runs used for spawning when Auto Find Runs In Scene is disabled.")]
    [SerializeField] private List<SkiRunLine> availableRuns = new();

    [Tooltip("Manual list of lifts used for spawning when Auto Find Lifts In Scene is disabled.")]
    [SerializeField] private List<LiftLine> availableLifts = new();

    [Header("Spawn Placement")]
    [Tooltip("Minimum allowed distance from the player for a new spawn pose.")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 120f;

    [Tooltip("Maximum allowed distance from the player for a new spawn pose.")]
    [SerializeField] private float maxSpawnDistanceFromPlayer = 500f;

    [Tooltip("Vertical offset applied to the final spawn position to keep NPCs slightly above the sampled point.")]
    [SerializeField] private float spawnHeightOffset = 0.35f;

    [Tooltip("Maximum forward-facing dot product allowed for hidden spawns. Lower values force spawns further behind the player.")]
    [SerializeField] private float hiddenSpawnDotThreshold = 0.2f;

    [Tooltip("Maximum number of attempts made to find a valid contextual spawn pose before giving up.")]
    [SerializeField] private int maxSpawnAttempts = 24;

    [Header("Spawn Illusion")]
    [Tooltip("Minimum time after being recycled before the same pooled NPC can be reactivated again.")]
    [SerializeField] private float npcReuseCooldownSeconds = 8f;

    [Tooltip("Do not respawn an NPC too close to where it was last recycled unless enough time has passed.")]
    [SerializeField] private float minRespawnDistanceFromLastSleep = 80f;

    [Tooltip("Recently used spawn hotspots are temporarily suppressed to reduce visible repetition.")]
    [SerializeField] private float hotspotCooldownSeconds = 10f;

    [Tooltip("Minimum distance between a new spawn and a recently used spawn hotspot before it is considered distinct.")]
    [SerializeField] private float hotspotRepeatRadius = 40f;

    [Header("Contextual Density")]
    [Tooltip("Runs within this distance of the player are considered valid run spawn hotspots.")]
    [SerializeField] private float runHotspotRadius = 450f;

    [Tooltip("Lift stations within this distance of the player are considered valid lift spawn hotspots.")]
    [SerializeField] private float liftHotspotRadius = 550f;

    [Tooltip("Relative spawn selection weight for lift-bottom spawn contexts.")]
    [SerializeField] private float liftBottomWeight = 0.45f;

    [Tooltip("Relative spawn selection weight for lift-top spawn contexts.")]
    [SerializeField] private float liftTopWeight = 0.20f;

    [Tooltip("Relative spawn selection weight for run spawn contexts.")]
    [SerializeField] private float runWeight = 0.35f;

    [Header("Cluster Illusion")]
    [Tooltip("If enabled, newly spawned NPCs may appear in small groups instead of only one at a time.")]
    [SerializeField] private bool useSpawnClusters = true;

    [Tooltip("Maximum number of NPCs that can be included in a single spawn cluster.")]
    [SerializeField] private int maxClusterSize = 3;

    [Tooltip("Chance that a spawn request will attempt to build a multi-NPC cluster when enough NPCs are needed.")]
    [SerializeField] private float clusterChance = 0.55f;

    [Tooltip("Minimum lateral spacing between NPCs within a spawn cluster.")]
    [SerializeField] private float clusterSpacingMin = 3f;

    [Tooltip("Maximum lateral spacing between NPCs within a spawn cluster.")]
    [SerializeField] private float clusterSpacingMax = 7f;

    private readonly List<NpcSkierBrain> _pool = new();
    private readonly List<SkiRunLine> _runs = new();
    private readonly List<LiftLine> _lifts = new();
    private readonly HashSet<NpcSkierBrain> _borrowedNpcs = new();

    private float _nextRefreshTime;
    private Coroutine _initializeRoutine;
    private bool _deferredInitializationPending;
    private readonly List<SpawnMemoryEntry> _recentSpawnMemory = new();

    private enum SpawnContext
    {
        Run,
        LiftBottom,
        LiftTop
    }

    private struct SpawnPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public SpawnContext context;
    }

    private struct SpawnMemoryEntry
    {
        public Vector3 position;
        public float expiresAt;
        public SpawnContext context;
    }

    private void Start()
    {
        if (RuntimeSceneLoadContext.IsMenuBackgroundPreview)
        {
            _deferredInitializationPending = initializeOnStart;
            RuntimeSceneLoadContext.MenuBackgroundPreviewEnded += HandleMenuPreviewEnded;
            return;
        }

        if (initializeOnStart)
            InitializePool();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void HandleMenuPreviewEnded()
    {
        RuntimeSceneLoadContext.MenuBackgroundPreviewEnded -= HandleMenuPreviewEnded;

        if (!this || !gameObject.scene.isLoaded)
            return;

        if (_deferredInitializationPending)
        {
            _deferredInitializationPending = false;
            InitializePool();
        }
    }

    private void Update()
    {
        if (RuntimeSceneLoadContext.IsMenuBackgroundPreview)
            return;

        if (Time.time < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.time + refreshInterval;

        if (player == null && Camera.main != null)
            player = Camera.main.transform;

        if (player == null)
            return;

        RefreshPopulation();
    }

    private void OnDestroy()
    {
        RuntimeSceneLoadContext.MenuBackgroundPreviewEnded -= HandleMenuPreviewEnded;

        if (Instance == this)
            Instance = null;
    }

    public bool TryBorrowNpc(Vector3 position, Quaternion rotation, out NpcSkierBrain npc)
    {
        npc = null;

        if (player == null && Camera.main != null)
            player = Camera.main.transform;

        NpcSkierBrain candidate = GetNextInactiveNpc(position);
        if (candidate == null)
            return false;

        candidate.gameObject.SetActive(true);
        candidate.ConfigurePlayerFocus(player);
        candidate.WakeFromPool(position, rotation);
        _borrowedNpcs.Add(candidate);
        npc = candidate;
        return true;
    }

    public void ReleaseBorrowedNpc(NpcSkierBrain npc, bool returnToNormalBehaviour = true)
    {
        if (npc == null)
            return;

        _borrowedNpcs.Remove(npc);

        if (!npc.gameObject.activeSelf)
            return;

        npc.ConfigurePlayerFocus(player);

        if (returnToNormalBehaviour)
        {
            npc.ResumeFromSpectatorCrowd(immediateIntent: true);
        }
        else
        {
            npc.PrepareForPoolSleep();
            npc.gameObject.SetActive(false);
        }
    }

    [ContextMenu("Initialize Pool")]
    public void InitializePool()
    {
        if (!Application.isPlaying)
        {
            InitializePoolImmediate(forceActivateToTarget: true);
            return;
        }

        if (_initializeRoutine != null)
            StopCoroutine(_initializeRoutine);

        _initializeRoutine = StartCoroutine(InitializePoolRoutine());
    }

    private IEnumerator InitializePoolRoutine()
    {
        ClearPool();

        if (npcPrefab == null)
        {
            Debug.LogWarning($"[{nameof(NpcSkierSpawner)}] No NPC prefab assigned.", this);
            _initializeRoutine = null;
            yield break;
        }

        CollectSourcesIfNeeded();

        int count = Mathf.Max(poolSize, targetActiveCount);
        int batchSize = Mathf.Max(1, createBatchSize);

        for (int i = 0; i < count; i++)
        {
            CreatePooledNpc();

            if ((i + 1) % batchSize == 0)
            {
                // Let the frame breathe before creating more.
                yield return null;
            }
        }

        _initializeRoutine = null;

        // Seed the first visible-area population immediately using the hidden spawn rules.
        RefreshPopulation(forceActivateToTarget: true);
    }

    private void InitializePoolImmediate(bool forceActivateToTarget)
    {
        ClearPool();

        if (npcPrefab == null)
        {
            Debug.LogWarning($"[{nameof(NpcSkierSpawner)}] No NPC prefab assigned.", this);
            return;
        }

        CollectSourcesIfNeeded();

        int count = Mathf.Max(poolSize, targetActiveCount);
        for (int i = 0; i < count; i++)
            CreatePooledNpc();

        RefreshPopulation(forceActivateToTarget: true);
    }

    private void CreatePooledNpc()
    {
        NpcSkierBrain npc = Instantiate(
            npcPrefab,
            transform.position,
            transform.rotation,
            npcParent != null ? npcParent : transform);

        npc.ConfigureRuns(_runs);
        npc.ConfigureLifts(_lifts);
        npc.ConfigurePlayerFocus(player);

        // Important: do not call InitializeNow() here.
        // Let the NPC lazily initialize when it is actually activated.
        npc.PrepareForPoolSleep();
        npc.gameObject.SetActive(false);

        _pool.Add(npc);
    }

    [ContextMenu("Clear Pool")]
    public void ClearPool()
    {
        for (int i = _pool.Count - 1; i >= 0; i--)
        {
            if (_pool[i] == null)
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_pool[i].gameObject);
            else
                Destroy(_pool[i].gameObject);
#else
            Destroy(_pool[i].gameObject);
#endif
        }

        _pool.Clear();
    }

    private void RefreshPopulation(bool forceActivateToTarget = false)
    {
        if (player == null)
            return;

        int activeCount = 0;

        for (int i = 0; i < _pool.Count; i++)
        {
            var npc = _pool[i];
            if (npc == null)
                continue;

            if (!npc.gameObject.activeSelf)
                continue;

            if (_borrowedNpcs.Contains(npc))
                continue;

            float dist = Vector3.Distance(player.position, npc.transform.position);

            if (dist > recycleRadius && !ShouldKeepNpcPersistent(npc, dist))
            {
                npc.PrepareForPoolSleep();
                npc.gameObject.SetActive(false);
                continue;
            }

            activeCount++;
        }

        int desired = Mathf.Min(targetActiveCount, _pool.Count);
        if (!forceActivateToTarget && activeCount >= desired)
            return;

        int needed = desired - activeCount;

        if (!forceActivateToTarget)
            needed = Mathf.Min(needed, Mathf.Max(1, maxNewActivationsPerRefresh));

        while (needed > 0)
        {
            int clusterCount = 1;
            if (useSpawnClusters && needed > 1 && Random.value <= clusterChance)
                clusterCount = Random.Range(2, Mathf.Min(maxClusterSize, needed) + 1);

            if (!TryBuildSpawnCluster(clusterCount, out List<SpawnPose> poses))
                break;

            for (int i = 0; i < poses.Count && needed > 0; i++)
            {
                NpcSkierBrain npc = GetNextInactiveNpc(poses[i].position);
                if (npc == null)
                    return;

                var pose = poses[i];

                npc.gameObject.SetActive(true);
                npc.ConfigurePlayerFocus(player);
                npc.SetSpawnContextHint(ToHint(pose.context), pose.position);
                npc.WakeFromPool(pose.position, pose.rotation);
                RememberSpawnPose(pose);
                needed--;
            }
        }
    }

    private NpcSkierBrain GetNextInactiveNpc(Vector3 desiredSpawnPosition)
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            NpcSkierBrain npc = _pool[i];
            if (npc == null || npc.gameObject.activeSelf)
                continue;

            if (Time.time - npc.LastPoolSleepTime < npcReuseCooldownSeconds)
                continue;

            if (npc.LastPoolSleepTime > 0f &&
                Vector3.Distance(npc.LastPoolSleepPosition, desiredSpawnPosition) < minRespawnDistanceFromLastSleep)
            {
                continue;
            }

            return npc;
        }

        return null;
    }

    private bool TryBuildSpawnCluster(int count, out List<SpawnPose> poses)
    {
        poses = new List<SpawnPose>();

        if (!TryFindContextualSpawnPose(out SpawnPose anchor))
            return false;

        poses.Add(anchor);

        for (int i = 1; i < count; i++)
        {
            if (TryOffsetClusterPose(anchor, i, out SpawnPose sibling))
                poses.Add(sibling);
        }

        return poses.Count > 0;
    }

    private bool TryFindContextualSpawnPose(out SpawnPose pose)
    {
        pose = default;

        if (player == null)
            return false;

        float totalWeight = Mathf.Max(0.01f, liftBottomWeight + liftTopWeight + runWeight);

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            SpawnContext context = ChooseContext(totalWeight);

            if (TryResolveContextPose(context, out Vector3 candidatePos, out Vector3 forward))
            {
                float dist = Vector3.Distance(player.position, candidatePos);
                if (dist < minSpawnDistanceFromPlayer || dist > maxSpawnDistanceFromPlayer)
                    continue;

                if (!IsSpawnHiddenFromCamera(candidatePos))
                    continue;

                if (IsNearRecentHotspot(candidatePos, context))
                    continue;

                pose.position = candidatePos + Vector3.up * spawnHeightOffset;
                Vector3 planarForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                if (planarForward.sqrMagnitude <= 0.0001f)
                    planarForward = Vector3.forward;
                pose.rotation = Quaternion.LookRotation(planarForward, Vector3.up);
                pose.context = context;
                return true;
            }
        }

        return false;
    }

    private bool TryOffsetClusterPose(SpawnPose anchor, int index, out SpawnPose pose)
    {
        pose = default;

        Vector3 forward = anchor.rotation * Vector3.forward;
        Vector3 right = anchor.rotation * Vector3.right;

        float spacing = Random.Range(clusterSpacingMin, clusterSpacingMax);
        float side = (index % 2 == 0) ? -1f : 1f;
        float backOffset = Random.Range(0.5f, 2.5f) * index;
        float sideOffset = spacing * side * Mathf.Ceil(index * 0.5f);

        Vector3 candidate = anchor.position - forward * backOffset + right * sideOffset;

        if (!IsSpawnHiddenFromCamera(candidate))
            return false;

        if (IsNearRecentHotspot(candidate, anchor.context))
            return false;

        pose.position = candidate;
        pose.rotation = anchor.rotation;
        pose.context = anchor.context;
        return true;
    }

    private SpawnContext ChooseContext(float totalWeight)
    {
        float roll = Random.value * totalWeight;
        if (roll < liftBottomWeight) return SpawnContext.LiftBottom;
        roll -= liftBottomWeight;
        if (roll < liftTopWeight) return SpawnContext.LiftTop;
        return SpawnContext.Run;
    }

    private bool TryResolveContextPose(SpawnContext context, out Vector3 pos, out Vector3 forward)
    {
        pos = transform.position;
        forward = Vector3.forward;

        switch (context)
        {
            case SpawnContext.LiftBottom:
                return TryFindLiftPose(bottom: true, out pos, out forward);

            case SpawnContext.LiftTop:
                return TryFindLiftPose(bottom: false, out pos, out forward);

            default:
                return TryFindRunPose(out pos, out forward);
        }
    }

    private bool TryFindLiftPose(bool bottom, out Vector3 pos, out Vector3 forward)
    {
        pos = transform.position;
        forward = Vector3.forward;

        if (_lifts.Count == 0)
            return false;

        List<LiftLine> nearby = new List<LiftLine>();
        for (int i = 0; i < _lifts.Count; i++)
        {
            var lift = _lifts[i];
            if (lift == null) continue;

            Transform station = bottom ? lift.bottomStation : lift.topStation;
            if (station == null) continue;

            float dist = Vector3.Distance(player.position, station.position);
            if (dist <= liftHotspotRadius)
                nearby.Add(lift);
        }

        if (nearby.Count == 0)
            return false;

        LiftLine chosen = nearby[Random.Range(0, nearby.Count)];
        Transform chosenStation = bottom ? chosen.bottomStation : chosen.topStation;
        if (chosenStation == null)
            return false;

        Vector3 stationPos = chosenStation.position;

        if (chosen.TryGetClosestPointOnBand(stationPos, out _, out Vector3 bandPoint, out Vector3 tangent))
        {
            if (bottom)
            {
                pos = bandPoint - tangent.normalized * 3f;
                forward = tangent;
            }
            else
            {
                pos = bandPoint + tangent.normalized * 4f;
                forward = tangent;
            }

            return true;
        }

        pos = stationPos;
        forward = bottom && chosen.topStation != null
            ? (chosen.topStation.position - chosenStation.position).normalized
            : Vector3.forward;
        return true;
    }

    private bool TryFindRunPose(out Vector3 pos, out Vector3 forward)
    {
        pos = transform.position;
        forward = Vector3.forward;

        if (_runs.Count == 0)
            return false;

        List<SkiRunLine> nearby = new List<SkiRunLine>();
        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run == null || run.PointsWorld == null || run.PointsWorld.Count < 2)
                continue;

            float dist = DistanceToRun(run, player.position);
            if (dist <= runHotspotRadius)
                nearby.Add(run);
        }

        if (nearby.Count == 0)
            return false;

        SkiRunLine chosenRun = nearby[Random.Range(0, nearby.Count)];
        float runLength = chosenRun.LengthMeters > 0.001f ? chosenRun.LengthMeters : chosenRun.GetTotalLengthMeters();
        float along = Random.Range(5f, Mathf.Max(6f, runLength - 5f));

        pos = SamplePointAtDistance(chosenRun.PointsWorld, along);
        forward = SampleTangentAtDistance(chosenRun.PointsWorld, along);
        return true;
    }

    private void CollectSourcesIfNeeded()
    {
        _runs.Clear();
        _lifts.Clear();
        PruneExpiredSpawnMemory();

        if (!autoFindRunsInScene && availableRuns != null && availableRuns.Count > 0)
            _runs.AddRange(availableRuns);
        else
            _runs.AddRange(FindObjectsOfType<SkiRunLine>(includeInactive: false));

        if (!autoFindLiftsInScene && availableLifts != null && availableLifts.Count > 0)
            _lifts.AddRange(availableLifts);
        else
            _lifts.AddRange(FindObjectsOfType<LiftLine>(includeInactive: false));
    }

    private static NpcSkierBrain.SpawnContextHint ToHint(SpawnContext context)
    {
        switch (context)
        {
            case SpawnContext.LiftBottom:
                return NpcSkierBrain.SpawnContextHint.LiftBottom;
            case SpawnContext.LiftTop:
                return NpcSkierBrain.SpawnContextHint.LiftTop;
            default:
                return NpcSkierBrain.SpawnContextHint.Run;
        }
    }

    private static float DistanceToRun(SkiRunLine run, Vector3 worldPos)
    {
        if (run == null)
            return float.PositiveInfinity;

        if (!run.TryGetClosestPointOnCenterlineXZ(worldPos, out _, out float distXZ, out _, out _))
            return float.PositiveInfinity;

        return distXZ;
    }

    private static Vector3 SamplePointAtDistance(IReadOnlyList<Vector3> points, float distanceMeters)
    {
        if (points == null || points.Count == 0)
            return Vector3.zero;

        if (points.Count == 1)
            return points[0];

        float remaining = Mathf.Max(0f, distanceMeters);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float segLen = Vector3.Distance(a, b);

            if (segLen <= 0.0001f)
                continue;

            if (remaining <= segLen)
                return Vector3.LerpUnclamped(a, b, remaining / segLen);

            remaining -= segLen;
        }

        return points[points.Count - 1];
    }

    private static Vector3 SampleTangentAtDistance(IReadOnlyList<Vector3> points, float distanceMeters)
    {
        const float delta = 2f;
        Vector3 a = SamplePointAtDistance(points, Mathf.Max(0f, distanceMeters - delta));
        Vector3 b = SamplePointAtDistance(points, distanceMeters + delta);
        Vector3 tangent = Vector3.ProjectOnPlane(b - a, Vector3.up);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.forward;
        return tangent.normalized;
    }

    private bool IsSpawnHiddenFromCamera(Vector3 candidatePos)
    {
        if (player == null)
            return false;

        Camera cam = Camera.main;
        Vector3 referenceForward = cam != null ? cam.transform.forward : player.forward;
        Vector3 referencePosition = cam != null ? cam.transform.position : player.position;

        Vector3 toSpawn = (candidatePos - referencePosition).normalized;
        if (Vector3.Dot(referenceForward, toSpawn) > hiddenSpawnDotThreshold)
            return false;

        if (cam == null)
            return true;

        Vector3 viewport = cam.WorldToViewportPoint(candidatePos);
        if (viewport.z <= 0f)
            return true;

        bool inViewport = viewport.x >= -0.08f && viewport.x <= 1.08f &&
                          viewport.y >= -0.08f && viewport.y <= 1.08f;
        if (!inViewport)
            return true;

        Bounds bounds = new Bounds(candidatePos, Vector3.one * 4f);
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        return !GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    private bool ShouldKeepNpcPersistent(NpcSkierBrain npc, float playerDistance)
    {
        if (npc == null)
            return false;

        if (playerDistance <= persistenceRadius)
            return true;

        return IsNpcVisibleToCamera(npc);
    }

    private bool IsNpcVisibleToCamera(NpcSkierBrain npc)
    {
        if (npc == null || !npc.TryGetVisibilityBounds(out Bounds bounds))
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    private void RememberSpawnPose(SpawnPose pose)
    {
        SpawnMemoryEntry entry = new SpawnMemoryEntry
        {
            position = pose.position,
            expiresAt = Time.time + hotspotCooldownSeconds,
            context = pose.context
        };

        _recentSpawnMemory.Add(entry);
        PruneExpiredSpawnMemory();
    }

    private void PruneExpiredSpawnMemory()
    {
        for (int i = _recentSpawnMemory.Count - 1; i >= 0; i--)
        {
            if (_recentSpawnMemory[i].expiresAt <= Time.time)
                _recentSpawnMemory.RemoveAt(i);
        }
    }

    private bool IsNearRecentHotspot(Vector3 position, SpawnContext context)
    {
        PruneExpiredSpawnMemory();

        for (int i = 0; i < _recentSpawnMemory.Count; i++)
        {
            SpawnMemoryEntry entry = _recentSpawnMemory[i];
            if (entry.context != context)
                continue;

            if (Vector3.Distance(entry.position, position) <= hotspotRepeatRadius)
                return true;
        }

        return false;
    }
}
