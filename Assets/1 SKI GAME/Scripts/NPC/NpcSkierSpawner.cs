using System.Collections;
using System.Collections.Generic;
using SkiGame.Runs;
using UnityEngine;

public class NpcSkierSpawner : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private NpcSkierBrain npcPrefab;
    [SerializeField] private Transform npcParent;
    [SerializeField] private int poolSize = 18;
    [SerializeField] private int targetActiveCount = 10;
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private int createBatchSize = 2;
    [SerializeField] private int maxNewActivationsPerRefresh = 2;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private float recycleRadius = 1000f;
    [SerializeField] private float refreshInterval = 1.25f;

    [Header("Scene Sources")]
    [SerializeField] private bool autoFindRunsInScene = true;
    [SerializeField] private bool autoFindLiftsInScene = true;
    [SerializeField] private List<SkiRunLine> availableRuns = new();
    [SerializeField] private List<LiftLine> availableLifts = new();

    [Header("Spawn Placement")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 120f;
    [SerializeField] private float maxSpawnDistanceFromPlayer = 500f;
    [SerializeField] private float spawnHeightOffset = 0.35f;
    [SerializeField] private float hiddenSpawnDotThreshold = 0.2f;
    [SerializeField] private int maxSpawnAttempts = 24;

    [Header("Contextual Density")]
    [SerializeField] private float runHotspotRadius = 450f;
    [SerializeField] private float liftHotspotRadius = 550f;
    [SerializeField] private float liftBottomWeight = 0.45f;
    [SerializeField] private float liftTopWeight = 0.20f;
    [SerializeField] private float runWeight = 0.35f;

    [Header("Cluster Illusion")]
    [SerializeField] private bool useSpawnClusters = true;
    [SerializeField] private int maxClusterSize = 3;
    [SerializeField] private float clusterChance = 0.55f;
    [SerializeField] private float clusterSpacingMin = 3f;
    [SerializeField] private float clusterSpacingMax = 7f;

    private readonly List<NpcSkierBrain> _pool = new();
    private readonly List<SkiRunLine> _runs = new();
    private readonly List<LiftLine> _lifts = new();

    private float _nextRefreshTime;
    private Coroutine _initializeRoutine;
    private bool _deferredInitializationPending;

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

        // Do not force-fill everything at once; let refresh populate gradually.
        RefreshPopulation(forceActivateToTarget: false);
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

        RefreshPopulation(forceActivateToTarget);
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

            float dist = Vector3.Distance(player.position, npc.transform.position);

            if (dist > recycleRadius)
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
                NpcSkierBrain npc = GetNextInactiveNpc();
                if (npc == null)
                    return;

                var pose = poses[i];

                npc.gameObject.SetActive(true);
                npc.SetSpawnContextHint(ToHint(pose.context), pose.position);
                npc.WakeFromPool(pose.position, pose.rotation);
                needed--;
            }
        }
    }

    private NpcSkierBrain GetNextInactiveNpc()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null && !_pool[i].gameObject.activeSelf)
                return _pool[i];
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

        Camera cam = Camera.main;
        Plane[] frustumPlanes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

        float totalWeight = Mathf.Max(0.01f, liftBottomWeight + liftTopWeight + runWeight);

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            SpawnContext context = ChooseContext(totalWeight);

            if (TryResolveContextPose(context, out Vector3 candidatePos, out Vector3 forward))
            {
                float dist = Vector3.Distance(player.position, candidatePos);
                if (dist < minSpawnDistanceFromPlayer || dist > maxSpawnDistanceFromPlayer)
                    continue;

                Vector3 toSpawn = (candidatePos - player.position).normalized;
                float facingDot = Vector3.Dot(player.forward, toSpawn);
                if (facingDot > hiddenSpawnDotThreshold)
                    continue;

                if (frustumPlanes != null)
                {
                    Bounds b = new Bounds(candidatePos, Vector3.one * 4f);
                    if (GeometryUtility.TestPlanesAABB(frustumPlanes, b))
                        continue;
                }

                pose.position = candidatePos + Vector3.up * spawnHeightOffset;
                pose.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up).normalized, Vector3.up);
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

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toSpawn = (candidate - player.position).normalized;
            if (Vector3.Dot(player.forward, toSpawn) > hiddenSpawnDotThreshold)
                return false;
        }

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
        return context switch
        {
            SpawnContext.LiftBottom => NpcSkierBrain.SpawnContextHint.LiftBottom,
            SpawnContext.LiftTop => NpcSkierBrain.SpawnContextHint.LiftTop,
            _ => NpcSkierBrain.SpawnContextHint.Run
        };
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
}