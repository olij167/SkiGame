using System.Collections.Generic;
using UnityEngine;
using SkiGame.Activities;

[DisallowMultipleComponent]
public sealed class RaceActivityService : MonoBehaviour
{
    public static RaceActivityService Instance { get; private set; }

    private const int ChampionshipExtraNpcOpponents = 2;
    private const int ChampionshipStartSpectatorCount = 18;
    private const int ChampionshipFinishSpectatorCount = 28;
    private const float ChampionshipSpectatorMinLateralOffset = 10f;
    private const float ChampionshipSpectatorMaxLateralOffset = 24f;
    private const float ChampionshipSpectatorAlongJitter = 24f;
    private const float ChampionshipSpectatorCleanupDelay = 8f;
    private const float ChampionshipRaceNpcCleanupDelay = 4.5f;
    private const float ChampionshipSpectatorCourseBuffer = 6f;
    private const float ChampionshipSpectatorGroundOffset = 0.16f;

    private RaceCourseLine _activeRace;
    private readonly List<RaceCourseNpcRacer> _activeRacers = new List<RaceCourseNpcRacer>();
    private readonly List<RaceCourseNpcRacer> _finishOrder = new List<RaceCourseNpcRacer>();
    private readonly List<RaceCourseNpcRacer> _activeSpectators = new List<RaceCourseNpcRacer>();
    private readonly List<NpcSkierBrain> _borrowedSpectators = new List<NpcSkierBrain>();
    private MountainActivityManager _boundActivityManager;
    private float _crowdCleanupAt = -1f;
    private float _racerCleanupAt = -1f;

    public int ActiveNpcCount => _activeRacers.Count;
    public int FinishedNpcCount => _finishOrder.Count;

    public bool TryGetNearestOpponentGap(RaceCourseNpcRacer requester, float requesterDistanceAlong, out float aheadGap, out float behindGap)
    {
        aheadGap = float.PositiveInfinity;
        behindGap = float.PositiveInfinity;

        if (requester == null)
            return false;

        bool found = false;
        for (int i = 0; i < _activeRacers.Count; i++)
        {
            RaceCourseNpcRacer racer = _activeRacers[i];
            if (racer == null || racer == requester || racer.IsFinished)
                continue;

            float delta = racer.DistanceAlong - requesterDistanceAlong;
            if (delta >= 0f)
                aheadGap = Mathf.Min(aheadGap, delta);
            else
                behindGap = Mathf.Min(behindGap, -delta);

            found = true;
        }

        return found;
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

    private void OnEnable()
    {
        TryBindActivityManager();
    }

    private void OnDisable()
    {
        UnbindActivityManager();
    }

    private void HandleActivityStarted(MountainActivityKind kind, MonoBehaviour source, string displayName, int variantNumber)
    {
        if (kind != MountainActivityKind.Race || source is not RaceCourseLine race)
            return;

        ClearActiveRace();
        _activeRace = race;
        _crowdCleanupAt = -1f;
        _racerCleanupAt = -1f;
        SpawnChampionshipSpectators(race);
        SpawnNpcRacers(race, variantNumber);
    }

    private void HandleActivityCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        if (kind != MountainActivityKind.Race || source is not RaceCourseLine race)
            return;

        int placement = _finishOrder.Count + 1;
        int entrants = _activeRacers.Count + 1;
        race.SetResolvedPlacement(placement, entrants);

        BeginRaceWrapUp(true);
    }

    private void HandleActivityFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
    {
        if (kind == MountainActivityKind.Race && source == _activeRace)
            BeginRaceWrapUp(false);
    }

    private void HandleActivityCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        if (kind == MountainActivityKind.Race && source == _activeRace)
            BeginRaceWrapUp(false);
    }

    private void Update()
    {
        TryBindActivityManager();

        if (_racerCleanupAt > 0f && Time.time >= _racerCleanupAt)
        {
            DestroyActiveRacers();
            _racerCleanupAt = -1f;
        }

        if (_crowdCleanupAt > 0f && Time.time >= _crowdCleanupAt)
        {
            DestroyActiveSpectators();
            _crowdCleanupAt = -1f;
        }
    }

    private void TryBindActivityManager()
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr == null || _boundActivityManager == mgr)
            return;

        UnbindActivityManager();

        _boundActivityManager = mgr;
        _boundActivityManager.OnActivityStarted += HandleActivityStarted;
        _boundActivityManager.OnActivityCompleted += HandleActivityCompleted;
        _boundActivityManager.OnActivityFailed += HandleActivityFailed;
        _boundActivityManager.OnActivityCancelled += HandleActivityCancelled;
    }

    private void UnbindActivityManager()
    {
        if (_boundActivityManager == null)
            return;

        _boundActivityManager.OnActivityStarted -= HandleActivityStarted;
        _boundActivityManager.OnActivityCompleted -= HandleActivityCompleted;
        _boundActivityManager.OnActivityFailed -= HandleActivityFailed;
        _boundActivityManager.OnActivityCancelled -= HandleActivityCancelled;
        _boundActivityManager = null;
    }

    private void SpawnNpcRacers(RaceCourseLine race, int leagueNumber)
    {
        if (race == null || !race.HasNpcOpponents || race.NpcOpponentCount <= 0 || race.NpcRacerPrefab == null)
            return;

        Quaternion rot = race.GetNpcStartSlotRotation();
        int npcCount = Mathf.Max(0, race.NpcOpponentCount);
        if (race.IsRegionalChampionship)
            npcCount += ChampionshipExtraNpcOpponents;

        for (int i = 0; i < npcCount; i++)
        {
            Vector3 pos = race.GetNpcStartSlotWorldPosition(i);
            var npc = Instantiate(race.NpcRacerPrefab, pos, rot);
            npc.name = $"RaceNpc_{i + 1:00}_{race.RaceName}";

            var bootstrap = npc.GetComponent<NpcAppearanceBootstrap>();
            if (bootstrap != null)
                bootstrap.Apply();

            npc.BeginRace(race, leagueNumber);
            _activeRacers.Add(npc);
        }
    }

    private void SpawnChampionshipSpectators(RaceCourseLine race)
    {
        if (race == null || !race.IsRegionalChampionship || race.NpcRacerPrefab == null || race.TotalLengthMeters <= 1f)
            return;

        float startDistance = Mathf.Max(4f, GetStartSpectatorDistance(race));
        float finishDistance = Mathf.Max(startDistance + 12f, GetFinishSpectatorDistance(race));

        SpawnSpectatorCluster(race, startDistance, ChampionshipStartSpectatorCount, "Start");
        SpawnSpectatorCluster(race, finishDistance, ChampionshipFinishSpectatorCount, "Finish");
    }

    private void SpawnSpectatorCluster(RaceCourseLine race, float distanceAlongCourse, int count, string clusterName)
    {
        if (count <= 0 || race == null || race.NpcRacerPrefab == null)
            return;

        Vector3 center = race.SamplePointAtDistance(distanceAlongCourse);
        Vector3 tangent = race.SampleTangentAtDistance(distanceAlongCourse);
        Vector3 forward = Vector3.ProjectOnPlane(tangent, Vector3.up);
        if (forward.sqrMagnitude <= 0.001f)
            forward = race.StartForward;
        if (forward.sqrMagnitude <= 0.001f)
            forward = Vector3.forward;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        int leftCount = (count + 1) / 2;
        int rightCount = count - leftCount;

        SpawnSpectatorSide(race, center, forward, -right, leftCount, $"{clusterName}Left");
        SpawnSpectatorSide(race, center, forward, right, rightCount, $"{clusterName}Right");
    }

    private void SpawnSpectatorSide(RaceCourseLine race, Vector3 center, Vector3 forward, Vector3 side, int count, string sideName)
    {
        if (count <= 0)
            return;

        Vector3 lookDirection = -side;
        Quaternion baseRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

        for (int i = 0; i < count; i++)
        {
            float spreadT = count > 1 ? (i / (float)(count - 1)) : 0.5f;
            float alongOffset = Mathf.Lerp(-ChampionshipSpectatorAlongJitter, ChampionshipSpectatorAlongJitter, spreadT) + Random.Range(-2f, 2f);
            float sideOffset = Mathf.Max(
                ChampionshipSpectatorMinLateralOffset,
                race.CourseWidthMeters * 0.5f + ChampionshipSpectatorCourseBuffer + Random.Range(1.5f, ChampionshipSpectatorMaxLateralOffset - ChampionshipSpectatorMinLateralOffset));

            Vector3 rawSpawnPos = center + forward * alongOffset + side * sideOffset;
            if (!TryResolveSpectatorSpawnPosition(race, rawSpawnPos, out Vector3 spawnPos))
                continue;

            Quaternion rotation = baseRotation * Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f);
            NpcSkierSpawner spawner = NpcSkierSpawner.Instance;
            if (spawner != null && spawner.TryBorrowNpc(spawnPos, rotation, out NpcSkierBrain pooledSpectator))
            {
                pooledSpectator.BeginSpectatorCrowd(spawnPos, rotation, lookDirection);
                _borrowedSpectators.Add(pooledSpectator);
                continue;
            }

            RaceCourseNpcRacer spectator = Instantiate(race.NpcRacerPrefab, spawnPos, rotation);
            spectator.name = $"ChampionshipSpectator_{sideName}_{i + 1:00}_{race.RaceName}";

            NpcAppearanceBootstrap bootstrap = spectator.GetComponent<NpcAppearanceBootstrap>();
            if (bootstrap != null)
                bootstrap.Apply();

            spectator.ConfigureAsCrowd(spawnPos, rotation, side + forward * Random.Range(-0.35f, 0.35f));
            _activeSpectators.Add(spectator);
        }
    }

    private float GetStartSpectatorDistance(RaceCourseLine race)
    {
        if (race == null || race.GeneratedCheckpoints == null || race.GeneratedCheckpoints.Count == 0)
            return 10f;

        return Mathf.Clamp(race.GeneratedCheckpoints[0].distance - 9f, 4f, Mathf.Max(4f, race.GeneratedCheckpoints[0].distance - 2f));
    }

    private float GetFinishSpectatorDistance(RaceCourseLine race)
    {
        if (race == null)
            return 18f;

        if (race.GeneratedCheckpoints != null && race.GeneratedCheckpoints.Count > 0)
        {
            float lastCheckpointDistance = race.GeneratedCheckpoints[race.GeneratedCheckpoints.Count - 1].distance;
            return Mathf.Clamp(lastCheckpointDistance + 10f, lastCheckpointDistance + 4f, Mathf.Max(lastCheckpointDistance + 4f, race.TotalLengthMeters - 6f));
        }

        return Mathf.Max(18f, race.TotalLengthMeters - 12f);
    }

    private static bool TryResolveSpectatorSpawnPosition(RaceCourseLine race, Vector3 desiredPosition, out Vector3 resolvedPosition)
    {
        resolvedPosition = desiredPosition;
        if (race == null)
            return false;

        if (!race.TryProjectPointOntoCourse(desiredPosition, out _, out float lateralDistance, out Vector3 projectedPoint))
            return false;

        float minLateral = race.CourseWidthMeters * 0.5f + ChampionshipSpectatorCourseBuffer;
        if (lateralDistance < minLateral)
        {
            Vector3 away = Vector3.ProjectOnPlane(desiredPosition - projectedPoint, Vector3.up);
            if (away.sqrMagnitude < 0.001f)
                away = Vector3.right;

            away.Normalize();
            desiredPosition = projectedPoint + away * minLateral;
        }

        Vector3 sampleOrigin = desiredPosition + Vector3.up * 8f;
        if (!Physics.Raycast(sampleOrigin, Vector3.down, out RaycastHit hit, 24f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        resolvedPosition = hit.point + hit.normal * ChampionshipSpectatorGroundOffset;
        return true;
    }

    public void NotifyNpcFinished(RaceCourseNpcRacer npc)
    {
        if (npc == null || _finishOrder.Contains(npc))
            return;

        _finishOrder.Add(npc);
    }

    private void BeginRaceWrapUp(bool completed)
    {
        for (int i = 0; i < _activeRacers.Count; i++)
        {
            if (_activeRacers[i] != null)
                _activeRacers[i].StopRace();
        }

        for (int i = 0; i < _activeSpectators.Count; i++)
        {
            if (_activeSpectators[i] != null)
                _activeSpectators[i].BeginCrowdDispersal(completed ? ChampionshipSpectatorCleanupDelay : ChampionshipSpectatorCleanupDelay * 0.65f);
        }

        _racerCleanupAt = Time.time + (completed ? ChampionshipRaceNpcCleanupDelay : 1.5f);
        _crowdCleanupAt = Time.time + (completed ? ChampionshipSpectatorCleanupDelay : ChampionshipSpectatorCleanupDelay * 0.65f);
        _finishOrder.Clear();
        _activeRace = null;
    }

    private void ClearActiveRace()
    {
        DestroyActiveRacers();
        DestroyActiveSpectators();
        _finishOrder.Clear();
        _activeRace = null;
        _crowdCleanupAt = -1f;
        _racerCleanupAt = -1f;
    }

    private void DestroyActiveRacers()
    {
        for (int i = 0; i < _activeRacers.Count; i++)
        {
            if (_activeRacers[i] != null)
            {
                _activeRacers[i].StopRace();
                Destroy(_activeRacers[i].gameObject);
            }
        }
        _activeRacers.Clear();
    }

    private void DestroyActiveSpectators()
    {
        NpcSkierSpawner spawner = NpcSkierSpawner.Instance;
        for (int i = 0; i < _borrowedSpectators.Count; i++)
        {
            if (_borrowedSpectators[i] == null)
                continue;

            if (spawner != null)
                spawner.ReleaseBorrowedNpc(_borrowedSpectators[i], returnToNormalBehaviour: true);
            else
                _borrowedSpectators[i].ResumeFromSpectatorCrowd(immediateIntent: true);
        }
        _borrowedSpectators.Clear();

        for (int i = 0; i < _activeSpectators.Count; i++)
        {
            if (_activeSpectators[i] == null)
                continue;

            NpcSkierBrain spectatorBrain = _activeSpectators[i].GetComponent<NpcSkierBrain>();
            if (spectatorBrain != null)
            {
                spectatorBrain.ResumeFromSpectatorCrowd(immediateIntent: true);
                continue;
            }

            Destroy(_activeSpectators[i].gameObject);
        }
        _activeSpectators.Clear();
    }
}
