using System.Collections;
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
        _state = ResortState.Outside;
    }

    private void Update()
    {
        if (_state != ResortState.ApproachingEnter)
            return;

        if (!_trackPlayerDuringApproach)
            return;

        if (gameplayCamera == null)
            return;

        // Track the *current* player position every frame while approaching.
        Transform target = walkingController != null ? walkingController.transform
                        : (playerRb != null ? playerRb.transform : null);

        if (target == null)
            return;

        Vector3 targetPos = target.position + approachLookOffset;
        Vector3 dir = targetPos - gameplayCamera.transform.position;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // Exponential smoothing (stable across frame rates)
        float k = 1f - Mathf.Exp(-approachLookLerpSpeed * Time.deltaTime);
        gameplayCamera.transform.rotation = Quaternion.Slerp(gameplayCamera.transform.rotation, lookRot, k);
    }


    // Called by hold interactor
    public void RequestToggleResort(GameObject playerRoot, SkiResortZone zone)
    {
        if (zone == null) return;

        if (_state == ResortState.Outside)
        {
            if (zone.entrancePoint == null || resortCamera == null)
                return;

            StartEnterFlow(playerRoot, zone);
        }
        else if (_state == ResortState.InResort)
        {
            if (zone.exitPoint == null)
                return;

            StartExitFlow(playerRoot, zone);
        }
    }

    private void StartEnterFlow(GameObject playerRoot, SkiResortZone zone)
    {
        StopAllFlows();

        CachePlayerRefs(playerRoot);

        // Reset any prior auto-drive / drift before starting a fresh approach.
        walkingController?.ClearExternalMove();

        if (playerRb != null)
        {
            var v = playerRb.linearVelocity;
            v.x = 0f; v.z = 0f;
            playerRb.linearVelocity = v;
            playerRb.angularVelocity = Vector3.zero;
        }

        // Ensure walking mode.
        walkingController?.ForceEnterWalkMode();

        // Save gameplay camera pose for blending back later.
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
        CachePlayerRefs(playerRoot);

        _flowCo = StartCoroutine(CoExitResort(playerRoot, zone));
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

            // Cancel if player provides movement input
            // (we detect it by reading the walking controller's own input via its internal actions,
            // so we use the hold interactor's move input instead — see interactor updates below).
            // Here, we simply keep applying external move; cancellation happens before we get called again.
            Vector3 to = zone.entrancePoint.position - walkingController.transform.position;
            to.y = 0f;

            if (to.sqrMagnitude <= arriveDistance * arriveDistance)
                break;

            Vector3 dir = to.normalized;

            // Convert world direction into the SAME movement basis WalkingController uses (camera-relative).
            walkingController.GetMoveBasis(out Vector3 fwd, out Vector3 right);

            float x = Vector3.Dot(dir, right);
            float y = Vector3.Dot(dir, fwd);

            // Stronger forward bias helps convergence + reduces orbiting.
            Vector2 auto = new Vector2(x, y);
            auto = Vector2.ClampMagnitude(auto, 1f);

            auto *= autoWalkMoveStrength;

            walkingController.SetExternalMove(auto, sprint: false);

            yield return null;
        }

        // Arrived: clear external drive.
        walkingController?.ClearExternalMove();

        // Freeze player and snap/hold position optionally.
        SetPlayerFrozen(true);

        if (zone.insidePoint != null && playerRb != null)
        {
            playerRb.position = zone.insidePoint.position;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
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

        // Clear external movement + restore camera.
        walkingController?.ClearExternalMove();

        if (playerRb != null)
        {
            var v = playerRb.linearVelocity;
            v.x = 0f; v.z = 0f;
            playerRb.linearVelocity = v;
            playerRb.angularVelocity = Vector3.zero;
        }

        // Blend back quickly.
        StartCameraBlendBackToGameplayPose(quick: true);

        SetPlayerFrozen(false);
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

        // Smoothly ramp time into resort speed.
        // 1.0 = default; resortSecondsPerMinuteMultiplier < 1 = faster time.
        StartTimeRamp(1f, resortSecondsPerMinuteMultiplier, timeRampSeconds, clearOverrideAtEnd: false);
    }

    private void ClearResortState(GameObject playerRoot)
    {
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

        // Smoothly ramp back to default, then clear override to restore exact baseline.
        StartTimeRamp(resortSecondsPerMinuteMultiplier, 1f, timeRampSeconds, clearOverrideAtEnd: true);
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

        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            float s = t * t * (3f - 2f * t); // smoothstep

            float m = Mathf.Lerp(from, to, s);
            timeController.SetExternalTimeSpeedMultiplier(m);

            yield return null;
        }

        timeController.SetExternalTimeSpeedMultiplier(to);

        if (clearOverrideAtEnd)
            timeController.ClearExternalTimeOverride();
    }


}
