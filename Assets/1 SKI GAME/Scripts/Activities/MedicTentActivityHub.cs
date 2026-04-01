using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;
using SkiGame.Activities;

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

    [Header("Rescue Generation")]
    [SerializeField] private bool useBoxArea = false;
    [SerializeField] private Vector3 rescueAreaOffset = Vector3.zero;
    [SerializeField] private float rescueRadiusMeters = 300f;
    [SerializeField] private Vector3 rescueBoxSize = new Vector3(300f, 80f, 300f);
    [SerializeField] private LayerMask terrainMask = ~0;
    [SerializeField] private float sampleHeightAboveArea = 400f;
    [SerializeField] private float minDistanceFromTent = 80f;
    [SerializeField] private int maxSampleAttempts = 16;

    [Header("Mission Tuning")]
    [SerializeField] private Vector2Int casualtyCountRange = new Vector2Int(1, 2);
    [SerializeField] private Vector2 timeLimitRangeSeconds = new Vector2(120f, 300f);
    [SerializeField] private bool revealExactLocationOnEasy = true;
    [SerializeField] private bool useSnowmobile = true;

    [Header("Scene")]
    [SerializeField] private Transform snowmobileSpawnPoint;
    [SerializeField] private Transform returnPoint;
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

    private void Reset()
    {
        EnsureTentId();
    }

    private void OnValidate()
    {
        EnsureTentId();

        if (rescueRadiusMeters < 20f) rescueRadiusMeters = 20f;
        if (rescueBoxSize.x < 20f) rescueBoxSize.x = 20f;
        if (rescueBoxSize.z < 20f) rescueBoxSize.z = 20f;

        casualtyCountRange.x = Mathf.Max(1, casualtyCountRange.x);
        casualtyCountRange.y = Mathf.Max(casualtyCountRange.x, casualtyCountRange.y);

        timeLimitRangeSeconds.x = Mathf.Max(30f, timeLimitRangeSeconds.x);
        timeLimitRangeSeconds.y = Mathf.Max(timeLimitRangeSeconds.x, timeLimitRangeSeconds.y);
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();
    }

    private void Update()
    {
        if (_playerRootInTrigger == null) return;
        if (interactAction == null || interactAction.action == null) return;
        if (RescueService.Instance == null) return;

        var mgr = MountainActivityManager.Instance;
        if (mgr == null) return;

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

        if (useSnowmobile)
            EnsureSnowmobileSpawned();

        RescueService.Instance.TryStartMission(this, _playerRootInTrigger);
    }

    public bool TryGenerateMission(out RescueService.GeneratedMission mission)
    {
        mission = default;

        Vector3 spawnPoint;
        if (!TryGetRandomRescuePoint(out spawnPoint))
            return false;

        mission.sourceHub = this;
        mission.rescuePoint = spawnPoint;
        mission.casualtyCount = Random.Range(casualtyCountRange.x, casualtyCountRange.y + 1);
        mission.timeLimitSeconds = Random.Range(timeLimitRangeSeconds.x, timeLimitRangeSeconds.y + 0.001f);
        mission.revealExactLocation = revealExactLocationOnEasy;
        mission.useSnowmobile = useSnowmobile;
        return true;
    }

    public bool TryGetRandomRescuePoint(out Vector3 point)
    {
        Vector3 areaCenter = RescueAreaCenter;

        for (int i = 0; i < Mathf.Max(1, maxSampleAttempts); i++)
        {
            Vector3 candidate = useBoxArea ? SampleBoxPoint(areaCenter) : SampleRadiusPoint(areaCenter);

            Vector3 rayOrigin = candidate + Vector3.up * Mathf.Max(10f, sampleHeightAboveArea);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, sampleHeightAboveArea * 2f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Distance(transform.position, hit.point) < minDistanceFromTent)
                    continue;

                point = hit.point;
                return true;
            }
        }

        point = transform.position;
        return false;
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

        if (_spawnedSnowmobile != null)
            return _spawnedSnowmobile;

        Transform spawn = SnowmobileSpawnPoint;
        _spawnedSnowmobile = Instantiate(snowmobilePrefab, spawn.position, spawn.rotation);
        _spawnedSnowmobile.name = $"{TentName}_Snowmobile";
        return _spawnedSnowmobile;
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