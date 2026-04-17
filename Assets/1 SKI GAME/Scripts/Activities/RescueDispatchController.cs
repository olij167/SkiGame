using System.Collections;
using SkiGame.Navigation;
using SkiGame.Progression;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RescueDispatchController : MonoBehaviour
{
    [SerializeField] private float medicalDropHeight = 24f;
    [SerializeField] private float medicalDropFallSeconds = 2.6f;
    [SerializeField] private float snowmobileDispatchRadius = 10f;
    [SerializeField] private float snowmobileDispatchProbeHeight = 14f;
    [SerializeField] private float snowmobileDispatchProbeDistance = 40f;
    [SerializeField] private LayerMask snowmobileGroundMask = ~0;
    [SerializeField] private Color rescueBeaconColor = new Color(1f, 0.82f, 0.18f, 1f);

    private GameObject _activeMedicalDrop;
    private string _activeBeaconWaypointId;

    public static RescueDispatchController Instance { get; private set; }

    public static RescueDispatchController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RescueDispatchController existing = FindObjectOfType<RescueDispatchController>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("RescueDispatchController");
        return go.AddComponent<RescueDispatchController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SyncActiveBeaconWaypoint();
    }

    private void Update()
    {
        SyncActiveBeaconWaypoint();
    }

    public bool TryRequestMedicalSupplyDrop(out string message)
    {
        if (!TryResolvePlayer(out SkiController skiController))
        {
            message = "Player not found.";
            return false;
        }

        SorenessMeter soreness = skiController.GetComponent<SorenessMeter>();
        if (soreness == null)
        {
            message = "No soreness meter found on the player.";
            return false;
        }

        if (soreness.Soreness01 <= 0.001f)
        {
            message = "Condition is already fully recovered.";
            return false;
        }

        if (!RaceRescueProgression.TryConsumeRescueUtility(
            RaceRescueProgression.RescueDispatchUtility.MedicalSupplyDrop,
            out message))
        {
            return false;
        }

        if (_activeMedicalDrop != null)
            Destroy(_activeMedicalDrop);

        _activeMedicalDrop = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        _activeMedicalDrop.name = "MedicalSupplyDrop";
        _activeMedicalDrop.transform.position = skiController.transform.position + Vector3.up * Mathf.Max(6f, medicalDropHeight);
        _activeMedicalDrop.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);

        Renderer renderer = _activeMedicalDrop.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.95f, 0.25f, 0.25f, 1f);

        Collider collider = _activeMedicalDrop.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        StartCoroutine(CompleteMedicalDrop(_activeMedicalDrop, skiController, soreness));
        message = "Medical supply drop inbound.";
        return true;
    }

    public bool TryPlaceRespawnBeacon(out string message)
    {
        if (!TryResolvePlayer(out SkiController skiController))
        {
            message = "Player not found.";
            return false;
        }

        if (RaceRescueProgression.TryGetActiveRescueBeacon(out _, out _))
        {
            message = "A rescue beacon is already active.";
            return false;
        }

        if (!RaceRescueProgression.TryConsumeRescueUtility(
            RaceRescueProgression.RescueDispatchUtility.RespawnBeacon,
            out message))
        {
            return false;
        }

        Vector3 beaconPosition = ResolveGroundPointNear(skiController.transform.position, 0f);
        RaceRescueProgression.SetActiveRescueBeacon(beaconPosition);
        SyncActiveBeaconWaypoint(forceRebuild: true);

        message = "Respawn beacon placed.";
        return true;
    }

    public bool TryRecallToRespawnBeacon(out string message)
    {
        if (!TryResolvePlayer(out SkiController skiController))
        {
            message = "Player not found.";
            return false;
        }

        if (!RaceRescueProgression.TryGetActiveRescueBeacon(out Vector3 beaconPosition, out _))
        {
            message = "No active rescue beacon.";
            return false;
        }

        Vector3 grounded = ResolveGroundPointNear(beaconPosition, 0f) + Vector3.up * 0.2f;
        Vector3 forward = Vector3.ProjectOnPlane(skiController.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        skiController.TeleportToSpawn(grounded, Quaternion.LookRotation(forward.normalized, Vector3.up), snapToGround: true);
        message = "Returned to the rescue beacon.";
        return true;
    }

    public bool TryRequestSnowmobileDispatch(out string message)
    {
        if (RescueService.Instance != null && RescueService.Instance.HasActiveMission)
        {
            message = "Snowmobile dispatch is only available during free roam.";
            return false;
        }

        if (!TryResolvePlayer(out SkiController skiController))
        {
            message = "Player not found.";
            return false;
        }

        MedicTentActivityHub[] tents = FindObjectsOfType<MedicTentActivityHub>();
        MedicTentActivityHub bestTent = null;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < tents.Length; i++)
        {
            MedicTentActivityHub tent = tents[i];
            if (tent == null || !tent.UseSnowmobile)
                continue;

            float distanceSqr = (tent.transform.position - skiController.transform.position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTent = tent;
            }
        }

        if (bestTent == null)
        {
            message = "No medic tent snowmobile dispatch source was found.";
            return false;
        }

        if (!RaceRescueProgression.TryConsumeRescueUtility(
            RaceRescueProgression.RescueDispatchUtility.SnowmobileDispatch,
            out message))
        {
            return false;
        }

        SnowmobileController snowmobile = bestTent.EnsureSnowmobileSpawned();
        if (snowmobile == null)
        {
            message = "Snowmobile dispatch failed.";
            return false;
        }

        Vector3 spawnPosition = ResolveGroundPointNear(skiController.transform.position, snowmobileDispatchRadius);
        Vector3 facing = Vector3.ProjectOnPlane(skiController.transform.forward, Vector3.up);
        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.forward;

        snowmobile.transform.SetPositionAndRotation(
            spawnPosition + Vector3.up * 0.2f,
            Quaternion.LookRotation(facing.normalized, Vector3.up));

        Rigidbody rb = snowmobile.VehicleRigidbody;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        message = "Rescue snowmobile dispatched nearby.";
        return true;
    }

    private IEnumerator CompleteMedicalDrop(GameObject dropObject, SkiController skiController, SorenessMeter soreness)
    {
        if (dropObject == null)
            yield break;

        Vector3 start = dropObject.transform.position;
        Vector3 end = ResolveGroundPointNear(skiController != null ? skiController.transform.position : start, 0f) + Vector3.up * 0.5f;
        float duration = Mathf.Max(0.75f, medicalDropFallSeconds);
        float elapsed = 0f;

        while (dropObject != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            dropObject.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        if (soreness != null)
            soreness.ResetSoreness();

        if (dropObject != null)
            Destroy(dropObject);

        _activeMedicalDrop = null;
    }

    private void SyncActiveBeaconWaypoint(bool forceRebuild = false)
    {
        MapWaypointManager waypointManager = MapWaypointManager.Instance != null
            ? MapWaypointManager.Instance
            : MapWaypointManager.EnsureInstance();

        if (waypointManager == null)
            return;

        if (!RaceRescueProgression.TryGetActiveRescueBeacon(out Vector3 beaconPosition, out _))
        {
            if (!string.IsNullOrWhiteSpace(_activeBeaconWaypointId))
            {
                waypointManager.RemoveWaypoint(_activeBeaconWaypointId);
                _activeBeaconWaypointId = null;
            }

            return;
        }

        if (forceRebuild && !string.IsNullOrWhiteSpace(_activeBeaconWaypointId))
        {
            waypointManager.RemoveWaypoint(_activeBeaconWaypointId);
            _activeBeaconWaypointId = null;
        }

        if (string.IsNullOrWhiteSpace(_activeBeaconWaypointId))
        {
            _activeBeaconWaypointId = waypointManager.AddCustomWaypoint(beaconPosition, "Rescue Beacon", setActive: false);
            waypointManager.SetWaypointColour(_activeBeaconWaypointId, rescueBeaconColor);
            return;
        }

        if (waypointManager.TryGetWaypoint(_activeBeaconWaypointId, out _))
        {
            waypointManager.RenameWaypoint(_activeBeaconWaypointId, "Rescue Beacon");
            waypointManager.SetWaypointColour(_activeBeaconWaypointId, rescueBeaconColor);
        }
    }

    private bool TryResolvePlayer(out SkiController skiController)
    {
        skiController = FindObjectOfType<SkiController>();
        return skiController != null;
    }

    private Vector3 ResolveGroundPointNear(Vector3 center, float radius)
    {
        if (radius > 0.01f)
        {
            Vector2 offset2D = Random.insideUnitCircle.normalized * Mathf.Max(3f, radius);
            center += new Vector3(offset2D.x, 0f, offset2D.y);
        }

        Vector3 rayOrigin = center + Vector3.up * Mathf.Max(1f, snowmobileDispatchProbeHeight);
        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            Mathf.Max(4f, snowmobileDispatchProbeDistance),
            snowmobileGroundMask,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return center;
    }
}
