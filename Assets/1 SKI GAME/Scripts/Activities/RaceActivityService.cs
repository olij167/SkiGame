using System.Collections.Generic;
using UnityEngine;
using SkiGame.Activities;

[DisallowMultipleComponent]
public sealed class RaceActivityService : MonoBehaviour
{
    public static RaceActivityService Instance { get; private set; }

    private RaceCourseLine _activeRace;
    private readonly List<RaceCourseNpcRacer> _activeRacers = new List<RaceCourseNpcRacer>();
    private readonly List<RaceCourseNpcRacer> _finishOrder = new List<RaceCourseNpcRacer>();
    private MountainActivityManager _boundActivityManager;

    public int ActiveNpcCount => _activeRacers.Count;
    public int FinishedNpcCount => _finishOrder.Count;

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
        SpawnNpcRacers(race, variantNumber);
    }

    private void HandleActivityCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        if (kind != MountainActivityKind.Race || source is not RaceCourseLine race)
            return;

        int placement = _finishOrder.Count + 1;
        int entrants = _activeRacers.Count + 1;
        race.SetResolvedPlacement(placement, entrants);

        ClearActiveRace();
    }

    private void HandleActivityFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
    {
        if (kind == MountainActivityKind.Race && source == _activeRace)
            ClearActiveRace();
    }

    private void HandleActivityCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        if (kind == MountainActivityKind.Race && source == _activeRace)
            ClearActiveRace();
    }

    private void Update()
    {
        TryBindActivityManager();
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

        for (int i = 0; i < race.NpcOpponentCount; i++)
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

    public void NotifyNpcFinished(RaceCourseNpcRacer npc)
    {
        if (npc == null || _finishOrder.Contains(npc))
            return;

        _finishOrder.Add(npc);
    }

    private void ClearActiveRace()
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
        _finishOrder.Clear();
        _activeRace = null;
    }
}