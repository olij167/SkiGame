using System.Collections.Generic;
using SkiGame.Activities;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class RescueService : MonoBehaviour
{
    public struct GeneratedMission
    {
        public MedicTentActivityHub sourceHub;
        public Vector3 rescuePoint;
        public int casualtyCount;
        public float timeLimitSeconds;
        public bool revealExactLocation;
        public bool useSnowmobile;
    }

    [Header("Optional Scene References")]
    [SerializeField] private GameObject casualtyMarkerPrefab;

    [SerializeField] private InputActionReference casualtyInteractAction;

    [Header("Runtime")]
    [SerializeField] private float casualtySpreadRadius = 8f;
    [SerializeField] private float casualtySecureRadius = 4f;

    public static RescueService Instance { get; private set; }

    private GeneratedMission _activeMission;
    private bool _active;
    private float _elapsed;
    private GameObject _activePlayerRoot;
    private readonly List<Vector3> _casualtyPositions = new List<Vector3>();
    private readonly List<bool> _casualtySecured = new List<bool>();
    private readonly List<GameObject> _spawnedMarkers = new List<GameObject>();

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

        if (RemainingCasualties <= 0)
        {
            float returnDistance = Vector3.Distance(playerPos, _activeMission.sourceHub.ReturnPoint.position);
            if (returnDistance <= 8f)
            {
                CompleteMission();
            }
        }
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

        BuildCasualties();
        return true;
    }

    private void BuildCasualties()
    {
        ClearRuntimeObjects();

        _casualtyPositions.Clear();
        _casualtySecured.Clear();

        int count = Mathf.Max(1, _activeMission.casualtyCount);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset2 = Random.insideUnitCircle * casualtySpreadRadius;
            Vector3 pos = _activeMission.rescuePoint + new Vector3(offset2.x, 0f, offset2.y);
            _casualtyPositions.Add(pos);
            _casualtySecured.Add(false);

            if (casualtyMarkerPrefab != null)
            {
                var go = Instantiate(casualtyMarkerPrefab, pos, Quaternion.identity);
                go.name = $"RescueCasualty_{i + 1:00}";

                var collider = go.GetComponent<Collider>();
                if (collider == null)
                {
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.isTrigger = true;
                    sphere.radius = 2f;
                }
                else
                {
                    collider.isTrigger = true;
                }

                var target = go.GetComponent<RescueCasualtyTarget>();
                if (target == null)
                    target = go.AddComponent<RescueCasualtyTarget>();

                target.SetInteractAction(casualtyInteractAction);
                target.Initialize(this, i);

                if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var casualtyState = go.GetComponent<NpcRescueCasualtyState>();
                    if (casualtyState != null)
                        casualtyState.InitializeAt(hit.point, hit.normal);
                    else
                        go.transform.position = hit.point + Vector3.up * 0.25f;
                }

                _spawnedMarkers.Add(go);
            }
        }
    }

    public bool IsCasualtySecureable(int casualtyIndex)
    {
        return _active &&
               casualtyIndex >= 0 &&
               casualtyIndex < _casualtySecured.Count &&
               !_casualtySecured[casualtyIndex];
    }

    public bool TrySecureCasualty(int casualtyIndex)
    {
        if (!IsCasualtySecureable(casualtyIndex))
            return false;

        _casualtySecured[casualtyIndex] = true;

        if (casualtyIndex >= 0 && casualtyIndex < _spawnedMarkers.Count && _spawnedMarkers[casualtyIndex] != null)
            _spawnedMarkers[casualtyIndex].SetActive(false);

        return true;
    }

    private void CompleteMission()
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr != null && _activeMission.sourceHub != null)
            mgr.Complete(MountainActivityKind.Rescue, _activeMission.sourceHub, "Completed");

        ClearMission();
    }

    private void FailMission(string reason)
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr != null && _activeMission.sourceHub != null)
            mgr.Fail(MountainActivityKind.Rescue, _activeMission.sourceHub, reason);

        ClearMission();
    }

    private void ClearMission()
    {
        _active = false;
        _elapsed = 0f;
        _activePlayerRoot = null;
        _activeMission = default;
        _casualtyPositions.Clear();
        _casualtySecured.Clear();
        ClearRuntimeObjects();
    }

    private void ClearRuntimeObjects()
    {
        for (int i = 0; i < _spawnedMarkers.Count; i++)
        {
            if (_spawnedMarkers[i] != null)
                Destroy(_spawnedMarkers[i]);
        }

        _spawnedMarkers.Clear();
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
}