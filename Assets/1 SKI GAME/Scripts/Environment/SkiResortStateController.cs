using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TimeWeather;
using UnityEngine.LowLevel;

[DisallowMultipleComponent]
public class SkiResortStateController : MonoBehaviour
{
    public static SkiResortStateController Instance { get; private set; }

    public enum ResortState
    {
        Outside,
        ApproachingEnter,
        InResort,
        ExitingResort
    }

    [Header("Discovery")]
    [SerializeField] private SorenessMeter playerSoreness;
    [SerializeField] private TimeController timeController;

    [Header("Movement Links")]
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private SkiController skiController;
    [SerializeField] private Rigidbody playerRb;

    [Header("Approach (Enter)")]
    [Tooltip("How fast the auto-walk drives the player to the entrance (0..1 move input).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float autoWalkMoveStrength = 0.8f;

    [SerializeField] private bool autoWalkUsesSprint = true;

    [Tooltip("When farther than this from the entrance, push the auto-walk at full strength.")]
    [SerializeField] private float farApproachDistance = 8f;

    [Tooltip("Once within this distance, snap directly to the inside point to avoid a slow final shuffle.")]
    [SerializeField] private float nearEntranceSnapDistance = 2.25f;

    [Tooltip("If player provides move input above this magnitude, auto-walk cancels.")]
    [SerializeField] private float cancelMoveThreshold = 0.15f;

    [Tooltip("Distance to entrance point to consider 'arrived'.")]
    [SerializeField] private float arriveDistance = 1.2f;

    [Header("Exit (Leave)")]
    [Tooltip("Duration of the 'walk out' animation/move.")]
    [SerializeField] private float exitWalkDuration = 1.2f;

    [Header("Soreness")]
    [SerializeField] private bool toggleRecoveryEnabled = true;
    [SerializeField] private bool disableRecoveryAtStart = true;
    [SerializeField] private bool useRestRecoveryRateInResort = true;

    [Header("Time")]
    [Range(0.01f, 10f)]
    [SerializeField] private float resortSecondsPerMinuteMultiplier = 0.25f;

    [Header("Time Smoothing")]
    [SerializeField] private float timeRampSeconds = 1.25f;
    private Coroutine _timeCo;

    [Header("Cameras")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera resortCamera;

    [Tooltip("If set, will be disabled during camera blends and while in resort.")]
    [SerializeField] private CameraController gameplayCameraController;

    [Tooltip("Blend duration when transitioning into resort camera pose.")]
    [SerializeField] private float cameraBlendOutSeconds = 2.0f;

    [Tooltip("Blend duration when transitioning back to gameplay camera pose.")]
    [SerializeField] private float cameraBlendInSeconds = 1.5f;

    [Header("Camera Seamlessness")]
    [SerializeField] private bool useSingleCameraBlend = true;

    [Tooltip("How quickly the active camera pose eases into the resort pose.")]
    [SerializeField] private float cameraArriveEaseSeconds = 0.35f;

    [Tooltip("If true, we copy key camera settings from the resort camera when entering (FOV, clip planes, etc.)")]
    [SerializeField] private bool copyResortCameraSettingsOnEnter = true;

    private float _cachedGameplayFov;
    private float _cachedGameplayNear;
    private float _cachedGameplayFar;

    private ResortState _state = ResortState.Outside;
    public ResortState State => _state;
    public bool IsInResort => _state == ResortState.InResort;

    public enum ResortAutoExitMode
    {
        None,
        WhenRecovered,
        AtAbsoluteGameMinute
    }

    [Header("Auto Exit")]
    [SerializeField] private float recoveredThreshold01 = 0.02f;

    private SkiResortZone _activeZone;
    private ResortAutoExitMode _autoExitMode = ResortAutoExitMode.None;
    private int _autoExitAbsoluteGameMinute = -1;

    public SkiResortZone ActiveZone => _activeZone;
    public bool HasScheduledExit => _autoExitMode != ResortAutoExitMode.None;
    public ResortAutoExitMode ScheduledExitMode => _autoExitMode;

    public SorenessMeter PlayerSoreness => playerSoreness;
    public TimeController TimeControllerRef => timeController;
    public float RecoveredThreshold01 => recoveredThreshold01;
    public int ScheduledExitAbsoluteGameMinute => _autoExitAbsoluteGameMinute;

    private bool _prevRecoveryEnabled;
    private bool _prevResting;

    private Coroutine _flowCo;
    private Coroutine _camCo;

    private Vector3 _savedGameplayCamPos;
    private Quaternion _savedGameplayCamRot;

    private Quaternion _resortCamAuthRot;
    private Vector3 _resortCamAuthPos;

    [Header("Approach Camera Tracking")]
    [SerializeField] private Vector3 approachLookOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private float approachLookLerpSpeed = 10f;

    private bool _trackPlayerDuringApproach;
    private bool _resortFastTimeActive;
    private float _currentTimeMultiplier = 1f;

    [Header("Visible World Fast Forward")]
    [SerializeField] private bool accelerateVisibleNpcSkiers = true;
    [SerializeField] private bool accelerateVisibleLiftLines = true;
    [SerializeField] private float visibleWorldRefreshInterval = 0.2f;
    [SerializeField] private float maxVisibleWorldSpeedMultiplier = 8f;

    private float _nextVisibleWorldRefreshTime;
    private Plane[] _resortCameraPlanes = new Plane[6];

    private readonly List<NpcSkierBrain> _cachedNpcBrains = new List<NpcSkierBrain>(64);
    private readonly List<LiftLine> _cachedLiftLines = new List<LiftLine>(32);

    private readonly HashSet<NpcSkierBrain> _boostedNpcBrains = new HashSet<NpcSkierBrain>();
    private readonly HashSet<LiftLine> _boostedLiftLines = new HashSet<LiftLine>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (playerSoreness == null)
            playerSoreness = FindObjectOfType<SorenessMeter>();
        if (timeController == null)
            timeController = TimeController.instance;

        if (walkingController == null)
            walkingController = FindObjectOfType<WalkingController>();
        if (skiController == null)
            skiController = FindObjectOfType<SkiController>();
        if (playerRb == null && walkingController != null)
            playerRb = walkingController.GetComponent<Rigidbody>();

        if (gameplayCameraController == null && gameplayCamera != null)
            gameplayCameraController = gameplayCamera.GetComponent<CameraController>();

        if (disableRecoveryAtStart && toggleRecoveryEnabled && playerSoreness != null)
            playerSoreness.RecoveryEnabled = false;

        if (useRestRecoveryRateInResort && playerSoreness != null)
            playerSoreness.IsResting = false;

        timeController?.ClearExternalTimeOverride();

        SetCameraEnabled(gameplay: true, resort: false);
        CacheGameplayCameraSettings();

        if (resortCamera != null)
        {
            _resortCamAuthPos = resortCamera.transform.position;
            _resortCamAuthRot = resortCamera.transform.rotation;
        }

        SetPlayerFrozen(false);
        _activeZone = null;
        _autoExitMode = ResortAutoExitMode.None;
        _autoExitAbsoluteGameMinute = -1;
        _resortFastTimeActive = false;
        _currentTimeMultiplier = 1f;
        _state = ResortState.Outside;
    }

    private void OnDisable()
    {
        ClearVisibleWorldFastForward();
    }

    private void Update()
    {
        if (_state == ResortState.InResort)
        {
            UpdateInResortTimeMode();
            TickAutoExit();
            TickVisibleWorldFastForward();
            return;
        }

        ClearVisibleWorldFastForward();

        if (_state != ResortState.ApproachingEnter)
            return;

        if (!_trackPlayerDuringApproach)
            return;

        if (gameplayCamera == null)
            return;

        Transform target = walkingController != null ? walkingController.transform
                        : (playerRb != null ? playerRb.transform : null);

        if (target == null)
            return;

        Vector3 targetPos = target.position + approachLookOffset;
        Vector3 dir = targetPos - gameplayCamera.transform.position;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        float k = 1f - Mathf.Exp(-approachLookLerpSpeed * Time.deltaTime);
        gameplayCamera.transform.rotation = Quaternion.Slerp(gameplayCamera.transform.rotation, lookRot, k);
    }

    private void TickAutoExit()
    {
        if (_activeZone == null || _state != ResortState.InResort)
            return;

        switch (_autoExitMode)
        {
            case ResortAutoExitMode.WhenRecovered:
                {
                    if (playerSoreness != null && playerSoreness.Soreness01 <= recoveredThreshold01)
                    {
                        StartExitFlow(GetPlayerRoot(), _activeZone);
                    }
                    break;
                }

            case ResortAutoExitMode.AtAbsoluteGameMinute:
                {
                    int now = GetCurrentAbsoluteGameMinute();
                    if (now >= 0 && _autoExitAbsoluteGameMinute >= 0 && now >= _autoExitAbsoluteGameMinute)
                    {
                        StartExitFlow(GetPlayerRoot(), _activeZone);
                    }
                    break;
                }
        }
    }

    private void UpdateInResortTimeMode()
    {
        if (_state != ResortState.InResort)
            return;

        bool fullyRecovered = IsPlayerRecovered();
        bool hasSchedule = _autoExitMode != ResortAutoExitMode.None;

        // Desired behavior:
        // - Recovering with no schedule: fast time.
        // - Fully recovered with no schedule: normal time.
        // - Time-based scheduled exit: fast time, even if already recovered.
        // - Leave-when-recovered schedule: fast time until the exit triggers.
        bool wantsFastTime =
            _autoExitMode == ResortAutoExitMode.AtAbsoluteGameMinute ||
            _autoExitMode == ResortAutoExitMode.WhenRecovered ||
            !fullyRecovered;

        SetResortFastTimeActive(wantsFastTime);
    }

    private float GetVisibleWorldSpeedMultiplier()
    {
        if (!_resortFastTimeActive)
            return 1f;

        float denom = Mathf.Max(0.0001f, _currentTimeMultiplier);
        float speedUp = 1f / denom;

        return Mathf.Clamp(speedUp, 1f, Mathf.Max(1f, maxVisibleWorldSpeedMultiplier));
    }

    private Camera GetActiveResortViewCamera()
    {
        if (useSingleCameraBlend)
            return gameplayCamera != null && gameplayCamera.isActiveAndEnabled ? gameplayCamera : null;

        return resortCamera != null && resortCamera.isActiveAndEnabled ? resortCamera : null;
    }

    private void TickVisibleWorldFastForward()
    {
        if (!accelerateVisibleNpcSkiers && !accelerateVisibleLiftLines)
        {
            ClearVisibleWorldFastForward();
            return;
        }

        if (!_resortFastTimeActive)
        {
            ClearVisibleWorldFastForward();
            return;
        }

        Camera activeResortViewCamera = GetActiveResortViewCamera();
        if (activeResortViewCamera == null)
        {
            ClearVisibleWorldFastForward();
            return;
        }

        if (Time.unscaledTime < _nextVisibleWorldRefreshTime)
            return;

        _nextVisibleWorldRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, visibleWorldRefreshInterval);

        float worldSpeedMultiplier = GetVisibleWorldSpeedMultiplier();

        GeometryUtility.CalculateFrustumPlanes(activeResortViewCamera, _resortCameraPlanes);

        if (accelerateVisibleNpcSkiers)
            RefreshVisibleNpcBoosts(worldSpeedMultiplier);

        if (accelerateVisibleLiftLines)
            RefreshVisibleLiftBoosts(worldSpeedMultiplier);
    }

    private void RefreshVisibleNpcBoosts(float multiplier)
    {
        _cachedNpcBrains.Clear();
        _cachedNpcBrains.AddRange(FindObjectsOfType<NpcSkierBrain>(includeInactive: false));

        var stillVisible = new HashSet<NpcSkierBrain>();

        for (int i = 0; i < _cachedNpcBrains.Count; i++)
        {
            NpcSkierBrain npc = _cachedNpcBrains[i];
            if (npc == null || !npc.isActiveAndEnabled)
                continue;

            if (!npc.TryGetVisibilityBounds(out Bounds bounds))
                continue;

            if (!GeometryUtility.TestPlanesAABB(_resortCameraPlanes, bounds))
                continue;

            npc.SetVisibleFastForwardMultiplier(multiplier);
            Debug.Log($"[ResortFF] Boosting NPC '{npc.name}' to x{multiplier:0.00}");
            _boostedNpcBrains.Add(npc);
            stillVisible.Add(npc);
        }

        if (_boostedNpcBrains.Count == 0)
            return;

        var toClear = ListPool<NpcSkierBrain>.Get();

        foreach (NpcSkierBrain npc in _boostedNpcBrains)
        {
            if (npc == null || stillVisible.Contains(npc))
                continue;

            toClear.Add(npc);
        }

        for (int i = 0; i < toClear.Count; i++)
        {
            NpcSkierBrain npc = toClear[i];
            if (npc != null)
                npc.SetVisibleFastForwardMultiplier(1f);

            _boostedNpcBrains.Remove(npc);
        }

        ListPool<NpcSkierBrain>.Release(toClear);
    }

    private void RefreshVisibleLiftBoosts(float multiplier)
    {
        _cachedLiftLines.Clear();
        _cachedLiftLines.AddRange(FindObjectsOfType<LiftLine>(includeInactive: false));

        var stillVisible = new HashSet<LiftLine>();

        for (int i = 0; i < _cachedLiftLines.Count; i++)
        {
            LiftLine lift = _cachedLiftLines[i];
            if (lift == null || !lift.isActiveAndEnabled)
                continue;

            if (!lift.TryGetVisibilityBounds(out Bounds bounds))
                continue;

            if (!GeometryUtility.TestPlanesAABB(_resortCameraPlanes, bounds))
                continue;

            lift.SetVisibleFastForwardMultiplier(multiplier);
            Debug.Log($"[ResortFF] Boosting lift '{lift.name}' to x{multiplier:0.00}");
            _boostedLiftLines.Add(lift);
            stillVisible.Add(lift);
        }

        if (_boostedLiftLines.Count == 0)
            return;

        var toClear = ListPool<LiftLine>.Get();

        foreach (LiftLine lift in _boostedLiftLines)
        {
            if (lift == null || stillVisible.Contains(lift))
                continue;

            toClear.Add(lift);
        }

        for (int i = 0; i < toClear.Count; i++)
        {
            LiftLine lift = toClear[i];
            if (lift != null)
                lift.SetVisibleFastForwardMultiplier(1f);

            _boostedLiftLines.Remove(lift);
        }

        ListPool<LiftLine>.Release(toClear);
    }

    private void ClearVisibleWorldFastForward()
    {
        if (_boostedNpcBrains.Count > 0)
        {
            foreach (NpcSkierBrain npc in _boostedNpcBrains)
            {
                if (npc != null)
                    npc.SetVisibleFastForwardMultiplier(1f);
            }

            _boostedNpcBrains.Clear();
        }

        if (_boostedLiftLines.Count > 0)
        {
            foreach (LiftLine lift in _boostedLiftLines)
            {
                if (lift != null)
                    lift.SetVisibleFastForwardMultiplier(1f);
            }

            _boostedLiftLines.Clear();
        }
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>(8);

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>(16);
        }

        public static void Release(List<T> list)
        {
            if (list == null)
                return;

            list.Clear();
            Pool.Push(list);
        }
    }

    private bool IsPlayerRecovered()
    {
        if (playerSoreness == null)
            return true;

        return playerSoreness.Soreness01 <= recoveredThreshold01;
    }

    private void SetResortFastTimeActive(bool active)
    {
        if (_resortFastTimeActive == active)
            return;

        _resortFastTimeActive = active;

        if (active)
        {
            StartTimeRamp(_currentTimeMultiplier, resortSecondsPerMinuteMultiplier, timeRampSeconds, clearOverrideAtEnd: false);
        }
        else
        {
            StartTimeRamp(_currentTimeMultiplier, 1f, timeRampSeconds, clearOverrideAtEnd: true);
        }
    }

    private GameObject GetPlayerRoot()
    {
        if (walkingController != null)
            return walkingController.transform.root.gameObject;

        if (playerRb != null)
            return playerRb.transform.root.gameObject;

        if (skiController != null)
            return skiController.transform.root.gameObject;

        return null;
    }

    private int GetCurrentAbsoluteGameMinute()
    {
        if (timeController == null)
            timeController = TimeController.instance;

        if (timeController == null)
            return -1;

        int day = Mathf.Max(0, timeController.dayCount);
        int hour = Mathf.Clamp(timeController.timeHours, 0, 23);
        int minute = Mathf.Clamp(Mathf.RoundToInt((float)timeController.timeMinutes), 0, 59);

        return day * 1440 + hour * 60 + minute;
    }

    public void ScheduleExitWhenRecovered()
    {
        _autoExitMode = ResortAutoExitMode.WhenRecovered;
        _autoExitAbsoluteGameMinute = -1;
    }

    public void ScheduleExitAtNextOccurrence(int hour24, int minute = 0)
    {
        int now = GetCurrentAbsoluteGameMinute();
        if (now < 0)
            return;

        hour24 = Mathf.Clamp(hour24, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);

        int currentTimeOfDay = now % 1440;
        int targetTimeOfDay = hour24 * 60 + minute;

        int delta = targetTimeOfDay - currentTimeOfDay;
        if (delta <= 0)
            delta += 1440;

        _autoExitAbsoluteGameMinute = now + delta;
        _autoExitMode = ResortAutoExitMode.AtAbsoluteGameMinute;

        if (_state == ResortState.InResort)
            SetResortFastTimeActive(true);
    }

    public void CancelScheduledExit()
    {
        _autoExitMode = ResortAutoExitMode.None;
        _autoExitAbsoluteGameMinute = -1;

        if (_state == ResortState.InResort)
            UpdateInResortTimeMode();
    }

    public void LeaveResortNow()
    {
        if (_state == ResortState.InResort && _activeZone != null)
            StartExitFlow(GetPlayerRoot(), _activeZone);
    }

    public string GetCurrentClockText()
    {
        if (timeController == null)
            timeController = TimeController.instance;

        if (timeController == null)
            return "--:--";

        return $"{Mathf.Clamp(timeController.timeHours, 0, 23):00}:{Mathf.Clamp((int)timeController.timeMinutes, 0, 59):00}";
    }

    public string GetCurrentDayText()
    {
        if (timeController == null)
            timeController = TimeController.instance;

        if (timeController == null)
            return "Day -";

        int day = Mathf.Max(1, timeController.dayCount + 1);
        int week = Mathf.Max(1, ((day - 1) / 7) + 1);
        int dayOfWeek = ((day - 1) % 7) + 1;

        return $"Week {week} - Day {dayOfWeek}";
    }

    public int GetMinutesUntilScheduledExit()
    {
        if (_autoExitMode != ResortAutoExitMode.AtAbsoluteGameMinute || _autoExitAbsoluteGameMinute < 0)
            return -1;

        int now = GetCurrentAbsoluteGameMinute();
        if (now < 0)
            return -1;

        return Mathf.Max(0, _autoExitAbsoluteGameMinute - now);
    }

    public string GetScheduledExitSummary()
    {
        switch (_autoExitMode)
        {
            case ResortAutoExitMode.WhenRecovered:
                return "Leaving when fully recovered";

            case ResortAutoExitMode.AtAbsoluteGameMinute:
                {
                    int mins = GetMinutesUntilScheduledExit();
                    if (mins < 0)
                        return "Scheduled exit";

                    int hours = mins / 60;
                    int minutes = mins % 60;

                    if (hours > 0)
                        return $"Leaving in {hours}h {minutes:00}m";

                    return $"Leaving in {minutes}m";
                }

            default:
                return "No scheduled exit";
        }
    }

    // Called by hold interactor
    public void RequestToggleResort(GameObject playerRoot, SkiResortZone zone)
    {
        if (_state == ResortState.Outside)
        {
            if (zone == null)
                return;

            if (zone.entrancePoint == null || resortCamera == null)
                return;

            StartEnterFlow(playerRoot, zone);
        }
        else if (_state == ResortState.InResort)
        {
            if (zone == null)
                zone = _activeZone;

            if (zone == null || zone.exitPoint == null)
                return;

            StartExitFlow(playerRoot != null ? playerRoot : GetPlayerRoot(), zone);
        }
    }

    private void StartEnterFlow(GameObject playerRoot, SkiResortZone zone)
    {
        StopAllFlows();

        _activeZone = zone;
        CancelScheduledExit();

        CachePlayerRefs(playerRoot);

        walkingController?.ClearExternalMove();

        if (playerRb != null)
        {
            var v = playerRb.linearVelocity;
            v.x = 0f; v.z = 0f;
            playerRb.linearVelocity = v;
            playerRb.angularVelocity = Vector3.zero;
        }

        walkingController?.ForceEnterWalkMode();

        if (gameplayCamera != null)
        {
            _savedGameplayCamPos = gameplayCamera.transform.position;
            _savedGameplayCamRot = gameplayCamera.transform.rotation;
        }

        _trackPlayerDuringApproach = true;
        _flowCo = StartCoroutine(CoEnterResort(playerRoot, zone));
    }

    private void StartExitFlow(GameObject playerRoot, SkiResortZone zone)
    {
        StopAllFlows();

        if (zone != null)
            _activeZone = zone;

        CancelScheduledExit();
        CachePlayerRefs(playerRoot);

        _flowCo = StartCoroutine(CoExitResort(playerRoot, _activeZone));
    }

    private IEnumerator CoEnterResort(GameObject playerRoot, SkiResortZone zone)
    {
        _state = ResortState.ApproachingEnter;

        // Camera: blend gameplay camera outward toward resort camera pose (no hard switch yet).
        StartCameraBlendToResortPose(zone);

        // Allow player controls; drive external auto-walk input unless player cancels.
        if (walkingController != null)
            walkingController.ControlsEnabled = true;

        // During approach, we keep skis off, and we keep ski controller disabled.
        if (skiController != null)
            skiController.enabled = false;

        while (_state == ResortState.ApproachingEnter)
        {
            if (walkingController == null || zone == null || zone.entrancePoint == null)
            {
                CancelEnter();
                yield break;
            }

            Vector3 to = zone.entrancePoint.position - walkingController.transform.position;
            to.y = 0f;

            float distance = to.magnitude;

            // If we're close enough, skip the slow last few steps and complete the entry cleanly.
            if (distance <= nearEntranceSnapDistance)
                break;

            if (distance <= arriveDistance)
                break;

            Vector3 dir = to / Mathf.Max(0.001f, distance);

            walkingController.GetMoveBasis(out Vector3 fwd, out Vector3 right);

            float x = Vector3.Dot(dir, right);
            float y = Vector3.Dot(dir, fwd);

            Vector2 auto = new Vector2(x, y);
            auto = Vector2.ClampMagnitude(auto, 1f);

            float distanceT = Mathf.InverseLerp(arriveDistance, farApproachDistance, distance);
            float strength = Mathf.Lerp(autoWalkMoveStrength, 1f, distanceT);

            auto *= strength;

            walkingController.SetExternalMove(auto, sprint: autoWalkUsesSprint);

            yield return null;
        }

        // Arrived: clear external drive.
        walkingController?.ClearExternalMove();

        // Freeze player and snap/hold position optionally.
        SetPlayerFrozen(true);

        if (zone.insidePoint != null)
        {
            if (playerRb != null)
            {
                playerRb.position = zone.insidePoint.position;
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
            else if (walkingController != null)
            {
                walkingController.transform.position = zone.insidePoint.position;
            }
        }

        // Now we are "in resort": apply recovery/time + switch camera fully.
        ApplyResortState(playerRoot);

        _trackPlayerDuringApproach = false;

        // --- Seamless arrival: NO camera swap ---
        // We keep gameplayCamera rendering, and we ease it into the resort's authored pose.
        if (useSingleCameraBlend && gameplayCamera != null && resortCamera != null)
        {
            // Stop any in-flight cam coroutine to avoid blending conflicts
            if (_camCo != null) { StopCoroutine(_camCo); _camCo = null; }

            // Ensure resort camera never renders (pose target only)
            SetCameraEnabled(gameplay: true, resort: false);

            // Ease gameplay camera from its current pose to resort authored pose
            _camCo = StartCoroutine(CoBlendCameraToPose(
                gameplayCamera,
                resortCamera.transform.position,
                _resortCamAuthRot,
                cameraArriveEaseSeconds,
                reenableController: false));

            // Smoothly match lens settings too (avoids subtle "pop")
            if (copyResortCameraSettingsOnEnter)
                StartCoroutine(CoLerpGameplayLensTo(resortCamera, cameraArriveEaseSeconds));
        }
        else
        {
            // Fallback: if not using single camera blend, do a handoff swap to reduce popping.
            if (gameplayCamera != null && resortCamera != null)
            {
                resortCamera.transform.position = gameplayCamera.transform.position;
                resortCamera.transform.rotation = gameplayCamera.transform.rotation;
                ApplyCameraSettings(gameplayCamera, resortCamera);
            }

            SetCameraEnabled(gameplay: false, resort: true);

            // Optional: keep your authored-rotation lerp for the resort camera.
            StartCoroutine(CoLerpResortCamBack(1.2f));
        }

        _state = ResortState.InResort;

    }

    private IEnumerator CoExitResort(GameObject playerRoot, SkiResortZone zone)
    {
        _state = ResortState.ExitingResort;

        // Stop recovery/time accel as soon as exit begins.
        ClearResortState(playerRoot);

        // Start blending back: enable gameplay cam, set it to current resort cam pose to avoid a cut.
        if (gameplayCamera != null && resortCamera != null)
        {
            SetCameraEnabled(gameplay: true, resort: false);
            gameplayCamera.transform.position = resortCamera.transform.position;
            gameplayCamera.transform.rotation = resortCamera.transform.rotation;
        }

        StartCameraBlendBackToGameplayPose();

        // Keep controls frozen during the short walk-out.
        SetPlayerFrozen(true);

        // Scripted walk-out to exit point.
        if (walkingController != null && zone != null && zone.exitPoint != null)
        {
            // Ensure walking mode visuals.
            walkingController.ForceEnterWalkMode();

            Vector3 start = walkingController.transform.position;
            Vector3 end = zone.exitPoint.position;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, exitWalkDuration);
                float s = Smooth01(t);

                Vector3 p = Vector3.Lerp(start, end, s);
                if (playerRb != null)
                    playerRb.MovePosition(p);
                else
                    walkingController.transform.position = p;

                // Face movement direction.
                Vector3 d = (end - start); d.y = 0f;
                if (d.sqrMagnitude > 0.001f)
                {
                    Quaternion tr = Quaternion.LookRotation(d.normalized, Vector3.up);
                    walkingController.transform.rotation = Quaternion.Slerp(walkingController.transform.rotation, tr, 12f * Time.deltaTime);
                }

                yield return null;
            }
        }

        if (useSingleCameraBlend && gameplayCamera != null)
        {
            // Restore gameplay camera lens settings (optional).
            gameplayCamera.fieldOfView = _cachedGameplayFov;
            gameplayCamera.nearClipPlane = _cachedGameplayNear;
            gameplayCamera.farClipPlane = _cachedGameplayFar;
        }

        // Restore gameplay camera controller.
        else if (gameplayCamera != null && resortCamera != null)
        {
            // Handoff back: make gameplay camera identical to resort camera right before switching.
            gameplayCamera.transform.position = resortCamera.transform.position;
            gameplayCamera.transform.rotation = resortCamera.transform.rotation;

            ApplyCameraSettings(resortCamera, gameplayCamera);

            SetCameraEnabled(gameplay: true, resort: false);
        }
        else
        {
            SetCameraEnabled(gameplay: true, resort: false);
        }

        // Re-enable controls.
        SetPlayerFrozen(false);

        // We are now outside.
        _state = ResortState.Outside;
    }

    public void CancelEnter()
    {
        if (_state != ResortState.ApproachingEnter)
            return;

        StopAllFlows();

        _trackPlayerDuringApproach = false;
        _activeZone = null;
        CancelScheduledExit();

        walkingController?.ClearExternalMove();

        if (playerRb != null)
        {
            var v = playerRb.linearVelocity;
            v.x = 0f; v.z = 0f;
            playerRb.linearVelocity = v;
            playerRb.angularVelocity = Vector3.zero;
        }

        StartCameraBlendBackToGameplayPose(quick: true);

        SetPlayerFrozen(false);
        _activeZone = null;
        CancelScheduledExit();
        _state = ResortState.Outside;
    }

    private void StopAllFlows()
    {
        if (_flowCo != null) StopCoroutine(_flowCo);
        _flowCo = null;

        if (_camCo != null) StopCoroutine(_camCo);
        _camCo = null;

        StopTimeRamp();

        _trackPlayerDuringApproach = false;

        // Make sure external drive is off.
        walkingController?.ClearExternalMove();
    }

    private void CachePlayerRefs(GameObject playerRoot)
    {
        if (playerRoot == null) return;

        if (walkingController == null)
            walkingController = playerRoot.GetComponentInChildren<WalkingController>();
        if (skiController == null)
            skiController = playerRoot.GetComponentInChildren<SkiController>();
        if (playerRb == null && walkingController != null)
            playerRb = walkingController.GetComponent<Rigidbody>();

        if (playerSoreness == null)
            playerSoreness = playerRoot.GetComponentInChildren<SorenessMeter>();
    }

    private void ApplyResortState(GameObject playerRoot)
    {
        if (playerSoreness != null)
        {
            _prevRecoveryEnabled = playerSoreness.RecoveryEnabled;
            _prevResting = playerSoreness.IsResting;

            if (toggleRecoveryEnabled)
                playerSoreness.RecoveryEnabled = true;

            if (useRestRecoveryRateInResort)
                playerSoreness.IsResting = true;
        }

        if (timeController == null)
            timeController = TimeController.instance;

        _resortFastTimeActive = false;
        _currentTimeMultiplier = 1f;

        // On entry, start with fast recovery time.
        SetResortFastTimeActive(true);
    }

    private void ClearResortState(GameObject playerRoot)
    {
        ClearVisibleWorldFastForward();

        if (playerSoreness == null && playerRoot != null)
            playerSoreness = playerRoot.GetComponentInChildren<SorenessMeter>();

        if (playerSoreness != null)
        {
            if (toggleRecoveryEnabled)
                playerSoreness.RecoveryEnabled = _prevRecoveryEnabled;

            if (useRestRecoveryRateInResort)
                playerSoreness.IsResting = _prevResting;
        }

        if (timeController == null)
            timeController = TimeController.instance;

        _resortFastTimeActive = false;

        // Smoothly ramp back to default, then clear override to restore exact baseline.
        StartTimeRamp(_currentTimeMultiplier, 1f, timeRampSeconds, clearOverrideAtEnd: true);
    }

    private void SetPlayerFrozen(bool frozen)
    {
        // Walking controller input/movement
        if (walkingController != null)
            walkingController.ControlsEnabled = !frozen;

        // Ski controller safety off
        if (skiController != null)
            skiController.enabled = !frozen && (walkingController == null || walkingController.SkisOn);

        // Kill velocity to avoid drifting while frozen
        if (frozen && playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }
    }

    private void SetCameraEnabled(bool gameplay, bool resort)
    {
        if (gameplayCamera != null) gameplayCamera.enabled = gameplay;
        if (resortCamera != null) resortCamera.enabled = resort;
    }

    private void StartCameraBlendToResortPose(SkiResortZone zone)
    {
        if (gameplayCamera == null || resortCamera == null)
            return;

        // Disable orbit controller during blend.
        if (gameplayCameraController != null)
            gameplayCameraController.enabled = false;

        // Ensure gameplay camera is active while blending.
        SetCameraEnabled(gameplay: true, resort: false);

        _camCo = StartCoroutine(CoBlendCamera(gameplayCamera, resortCamera.transform, cameraBlendOutSeconds));
    }

    private void StartCameraBlendBackToGameplayPose(bool quick = false)
    {
        if (gameplayCamera == null)
            return;

        if (gameplayCameraController != null)
            gameplayCameraController.enabled = false;

        float dur = quick ? 0.6f : cameraBlendInSeconds;

        // Blend back to saved pose then re-enable orbit.
        _camCo = StartCoroutine(CoBlendCameraToPose(gameplayCamera, _savedGameplayCamPos, _savedGameplayCamRot, dur, reenableController: true));
    }

    private IEnumerator CoBlendCamera(Camera cam, Transform targetPose, float duration)
    {
        if (cam == null || targetPose == null) yield break;

        Vector3 p0 = cam.transform.position;
        Quaternion r0 = cam.transform.rotation;

        Vector3 p1 = targetPose.position;
        Quaternion r1 = targetPose.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float s = Smooth01(t);

            cam.transform.position = Vector3.Lerp(p0, p1, s);

            // Look at player during approach (current position).
            Transform target = walkingController != null ? walkingController.transform
                            : (playerRb != null ? playerRb.transform : null);

            if (target != null)
            {
                Vector3 targetPos = target.position + approachLookOffset;
                Vector3 lookDir = targetPos - cam.transform.position;

                if (lookDir.sqrMagnitude < 0.001f)
                    lookDir = cam.transform.forward;

                Quaternion lookRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                cam.transform.rotation = Quaternion.Slerp(r0, lookRot, s);
            }
            else
            {
                cam.transform.rotation = Quaternion.Slerp(r0, r1, s);
            }

            yield return null;
        }


    }

    private IEnumerator CoBlendCameraToPose(Camera cam, Vector3 pos, Quaternion rot, float duration, bool reenableController)
    {
        if (cam == null) yield break;

        Vector3 p0 = cam.transform.position;
        Quaternion r0 = cam.transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float s = Smooth01(t);

            cam.transform.position = Vector3.Lerp(p0, pos, s);
            cam.transform.rotation = Quaternion.Slerp(r0, rot, s);

            yield return null;
        }

        if (reenableController && gameplayCameraController != null)
            gameplayCameraController.enabled = true;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private System.Collections.IEnumerator CoLerpResortCamBack(float seconds)
    {
        if (resortCamera == null) yield break;

        Quaternion from = resortCamera.transform.rotation;
        Quaternion to = _resortCamAuthRot;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, seconds);
            float s = t * t * (3f - 2f * t); // smoothstep
            resortCamera.transform.rotation = Quaternion.Slerp(from, to, s);
            yield return null;
        }
    }

    private void CacheGameplayCameraSettings()
    {
        if (gameplayCamera == null) return;
        _cachedGameplayFov = gameplayCamera.fieldOfView;
        _cachedGameplayNear = gameplayCamera.nearClipPlane;
        _cachedGameplayFar = gameplayCamera.farClipPlane;
    }

    private void ApplyCameraSettings(Camera from, Camera to)
    {
        if (from == null || to == null) return;
        to.fieldOfView = from.fieldOfView;
        to.nearClipPlane = from.nearClipPlane;
        to.farClipPlane = from.farClipPlane;
    }

    private IEnumerator CoLerpGameplayLensTo(Camera sourceLens, float seconds)
    {
        if (gameplayCamera == null || sourceLens == null) yield break;

        float f0 = gameplayCamera.fieldOfView;
        float n0 = gameplayCamera.nearClipPlane;
        float fz0 = gameplayCamera.farClipPlane;

        float f1 = sourceLens.fieldOfView;
        float n1 = sourceLens.nearClipPlane;
        float fz1 = sourceLens.farClipPlane;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, seconds);
            float s = Smooth01(t);

            gameplayCamera.fieldOfView = Mathf.Lerp(f0, f1, s);
            gameplayCamera.nearClipPlane = Mathf.Lerp(n0, n1, s);
            gameplayCamera.farClipPlane = Mathf.Lerp(fz0, fz1, s);

            yield return null;
        }
    }

    private void StopTimeRamp()
    {
        if (_timeCo != null)
        {
            StopCoroutine(_timeCo);
            _timeCo = null;
        }
    }

    private void StartTimeRamp(float fromMultiplier, float toMultiplier, float seconds, bool clearOverrideAtEnd)
    {
        StopTimeRamp();
        _timeCo = StartCoroutine(CoTimeRamp(fromMultiplier, toMultiplier, seconds, clearOverrideAtEnd));
    }

    private System.Collections.IEnumerator CoTimeRamp(float from, float to, float seconds, bool clearOverrideAtEnd)
    {
        if (timeController == null)
            timeController = TimeController.instance;

        if (timeController == null)
            yield break;

        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);

        _currentTimeMultiplier = from;

        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            float s = t * t * (3f - 2f * t);

            float m = Mathf.Lerp(from, to, s);
            _currentTimeMultiplier = m;
            timeController.SetExternalTimeSpeedMultiplier(m);

            yield return null;
        }

        _currentTimeMultiplier = to;
        timeController.SetExternalTimeSpeedMultiplier(to);

        if (clearOverrideAtEnd)
            timeController.ClearExternalTimeOverride();

        _timeCo = null;
    }
}
