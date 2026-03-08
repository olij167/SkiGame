using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.Progression;

/// <summary>
/// Generic third-person orbit camera using the new Input System.
/// - Assign a target Transform (or tag something "Player" and leave empty).
/// - Assign an InputActionReference (Vector2) for look input (e.g. Player/Look).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("What the camera orbits around. If left null, will try to find an object tagged 'Player' at Start.")]
    [SerializeField] private Transform target;

    [Header("Input (New Input System)")]
    [Tooltip("InputActionReference providing a Vector2 look input (e.g. from a 'Look' action).")]
    [SerializeField] private InputActionReference lookAction;

    [Header("Orbit")]
    public float distance = 5f;
    public float height = 2f;
    public float sensitivityX = 150f;
    public float sensitivityY = 100f;

    [Tooltip("Hard clamp on how fast yaw/pitch can change (degrees/second). Helps prevent crazy orbit spikes.")]
    public float maxOrbitDegreesPerSecond = 360f;

    public float minPitch = -30f;
    public float maxPitch = 70f;
    public float smoothTime = 0.05f;

    [Header("Auto Follow")]
    [Tooltip("When the player moves faster than this speed, the camera will softly align behind them.")]
    public float autoFollowSpeedThreshold = 10f;

    [Tooltip("How quickly the camera yaws to behind the player when auto-follow is active (deg/sec).")]
    public float autoFollowYawSpeed = 120f;

    [Tooltip("Ignore auto-follow for a short time after the player manually looks around.")]
    public float manualLookGraceSeconds = 0.35f;

    [Tooltip("Deadzone for treating look input as manual control.")]
    public float manualLookDeadzone = 0.05f;

    [Tooltip("If true, auto-follow aligns behind travel direction (velocity heading) instead of target facing.")]
    public bool followTravelDirection = true;

    [Tooltip("Camera won't try to auto-align unless the yaw drift exceeds this many degrees.")]
    [Range(0f, 180f)]
    public float autoFollowAngleThreshold = 25f;

    [Tooltip("When following travel direction, ignore tiny planar velocity to avoid jitter.")]
    public float travelDirMinPlanarSpeed = 1.5f;

    [Header("UI Lock (Phone)")]
    [Tooltip("When the phone UI is open, camera look input is ignored and the camera locks behind the player.")]
    public bool lockBehindWhenPhoneOpen = true;

    [Tooltip("Pitch to use while phone is open (degrees).")]
    public float phoneLockPitch = 12f;

    [Tooltip("How quickly the camera snaps behind while phone is open (deg/sec).")]
    public float phoneLockYawSpeed = 360f;

    [Header("Collision")]
    public LayerMask collisionLayers = ~0;
    public float collisionRadius = 0.2f;

    public enum CameraMode { Normal, ShopOrbit }

    [Header("Shop Orbit")]
    [SerializeField] private CameraMode mode = CameraMode.Normal;
    [SerializeField] private Transform shopOrbitTarget;
    [SerializeField] private float shopOrbitDistance = 3.0f;
    [SerializeField] private float shopOrbitHeight = 1.35f;
    [SerializeField] private float shopOrbitYaw = 180f;
    [SerializeField] private float shopOrbitRotateSpeed = 140f;

    [SerializeField] private InputActionReference shopOrbitPressAction; // <Pointer>/press
    [SerializeField] private InputActionReference shopOrbitDeltaAction; // <Pointer>/delta
    [SerializeField] private bool shopOrbitRequiresPress = true;

    private bool _shopOrbitHeld;

    private float _yaw;
    private float _pitch;
    private Vector3 _camVelocity;

    private Rigidbody _targetRb;
    private PhoneHUDController _phoneHUD;
    private float _lastManualLookTime;


    [Header("Settings")]
    [SerializeField] private bool applyFovFromSettings = true;

    private Camera _cam;
    private float _baseFov;

    // --- Settings integration ---
    private float _baseSensitivityX;
    private float _baseSensitivityY;
    private float _baseSmoothTime;
    private float _baseMaxOrbitDps;
    private float _baseAutoFollowYawSpeed;
    private float _basePhoneLockYawSpeed;

    private bool _settingsHooked;

    /// <summary>
    /// Allows other scripts to set the camera target at runtime.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
            _yaw = target.eulerAngles.y;
    }

    private void OnEnable()
    {
        if (lookAction != null && lookAction.action != null)
            lookAction.action.Enable();

        if (shopOrbitPressAction != null && shopOrbitPressAction.action != null)
        {
            shopOrbitPressAction.action.Enable();
            shopOrbitPressAction.action.performed += OnShopOrbitPressPerformed;
            shopOrbitPressAction.action.canceled += OnShopOrbitPressCanceled;
        }

        if (shopOrbitDeltaAction != null)
            shopOrbitDeltaAction.action.Enable();

        HookSettingsIfNeeded();

    }

    private void OnDisable()
    {
        if (lookAction != null && lookAction.action != null)
            lookAction.action.Disable();

        if (shopOrbitPressAction != null && shopOrbitPressAction.action != null)
        {
            shopOrbitPressAction.action.performed -= OnShopOrbitPressPerformed;
            shopOrbitPressAction.action.canceled -= OnShopOrbitPressCanceled;
        }

        UnhookSettings();

    }

    private void Start()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }

        if (target != null)
        {
            _yaw = target.eulerAngles.y;
            _targetRb = target.GetComponentInParent<Rigidbody>();
        }

        // Optional: phone HUD may live elsewhere in the scene.
        _phoneHUD = FindObjectOfType<PhoneHUDController>();

        _cam = GetComponent<Camera>();
        if (_cam != null)
            _baseFov = _cam.fieldOfView;

    }

    private float GetAutoFollowYaw()
    {
        if (target == null)
            return _yaw;

        // Default: follow facing
        float desiredYaw = target.eulerAngles.y;

        if (!followTravelDirection)
            return desiredYaw;

        // Travel-direction follow: use planar velocity heading when meaningful.
        if (_targetRb == null)
            _targetRb = target.GetComponentInParent<Rigidbody>();

        if (_targetRb == null)
            return desiredYaw;

        Vector3 v = _targetRb.linearVelocity; // (Your project uses linearVelocity)
        v.y = 0f;

        float planarSpeed = v.magnitude;
        if (planarSpeed < travelDirMinPlanarSpeed)
            return desiredYaw;

        // Heading of travel in world space
        desiredYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        return desiredYaw;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Resolve phone HUD lazily (scene reloads).
        if (_phoneHUD == null)
            _phoneHUD = FindObjectOfType<PhoneHUDController>();

        bool phoneOpen = (_phoneHUD != null) && _phoneHUD.IsPhoneOpen;

        // While phone is open, suppress manual look and keep the camera stable.
        if (phoneOpen && lockBehindWhenPhoneOpen)
        {
            float targetYaw = (target != null) ? target.eulerAngles.y : _yaw;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, phoneLockYawSpeed * dt);

            _pitch = Mathf.MoveTowards(_pitch, phoneLockPitch, phoneLockYawSpeed * dt);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            return;
        }

        if (mode == CameraMode.ShopOrbit)
        {
            // In shop orbit, we orbit around shopOrbitTarget (or fallback to target)
            Transform t = shopOrbitTarget != null ? shopOrbitTarget : target;
            if (t == null) return;

            Vector2 look = Vector2.zero;
            if (lookAction != null && lookAction.action != null)
                look = lookAction.action.ReadValue<Vector2>();

            bool allow = !shopOrbitRequiresPress || _shopOrbitHeld;

            if (allow)
            {
                float dyaw = look.x * sensitivityX * dt;
                float dpitch = -look.y * sensitivityY * dt;

                float maxDelta = Mathf.Max(0f, maxOrbitDegreesPerSecond) * dt;
                if (maxDelta > 0f)
                {
                    dyaw = Mathf.Clamp(dyaw, -maxDelta, maxDelta);
                    dpitch = Mathf.Clamp(dpitch, -maxDelta, maxDelta);
                }

                _yaw += dyaw;
                _pitch += dpitch;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            // IMPORTANT: in shop mode, do NOT auto-follow.
            return;
        }
        else
        {
            Vector2 look = Vector2.zero;
            if (lookAction != null && lookAction.action != null)
                look = lookAction.action.ReadValue<Vector2>();

            bool manualLooking = look.sqrMagnitude > (manualLookDeadzone * manualLookDeadzone);
            if (manualLooking)
                _lastManualLookTime = Time.unscaledTime;

            // Orbit input, with per-frame clamp to prevent spikes.
            float dyaw = look.x * sensitivityX * dt;
            float dpitch = -look.y * sensitivityY * dt;

            float maxDelta = Mathf.Max(0f, maxOrbitDegreesPerSecond) * dt;
            if (maxDelta > 0f)
            {
                dyaw = Mathf.Clamp(dyaw, -maxDelta, maxDelta);
                dpitch = Mathf.Clamp(dpitch, -maxDelta, maxDelta);
            }

            _yaw += dyaw;
            _pitch += dpitch;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            // Soft auto-follow behind at speed, unless the player has recently looked around.
            if (target != null)
            {
                if (_targetRb == null)
                    _targetRb = target.GetComponentInParent<Rigidbody>();

                float speed = (_targetRb != null) ? _targetRb.linearVelocity.magnitude : 0f;
                bool graceActive = (Time.unscaledTime - _lastManualLookTime) < manualLookGraceSeconds;

                if (!graceActive && speed >= autoFollowSpeedThreshold)
                {
                    float desiredYaw = GetAutoFollowYaw();

                    // Let the player deviate a bit before the camera tries to re-center.
                    float yawError = Mathf.Abs(Mathf.DeltaAngle(_yaw, desiredYaw));
                    if (yawError > autoFollowAngleThreshold)
                    {
                        _yaw = Mathf.MoveTowardsAngle(_yaw, desiredYaw, autoFollowYawSpeed * dt);
                    }
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (mode == CameraMode.ShopOrbit)
        {
            TickShopOrbit();
            return;
        }
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 targetPos = target.position + Vector3.up * height;
        Vector3 desiredPos = targetPos - rot * Vector3.forward * distance;

        // Simple collision: pull camera closer if blocked
        Vector3 toCam = desiredPos - targetPos;
        float distanceToCam = toCam.magnitude;

        if (distanceToCam > 0.01f)
        {
            if (Physics.SphereCast(
                    targetPos, collisionRadius,
                    toCam.normalized,
                    out RaycastHit hit,
                    distanceToCam,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                desiredPos = targetPos + toCam.normalized * Mathf.Max(hit.distance - 0.05f, 0f);
            }
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _camVelocity, smoothTime);
        transform.rotation = rot;
    }

    public void EnterShopOrbit(Transform targetLookAt)
    {
        mode = CameraMode.ShopOrbit;
        shopOrbitTarget = targetLookAt;
    }

    public void ExitShopOrbit()
    {
        mode = CameraMode.Normal;
        shopOrbitTarget = null;
    }

    private void TickShopOrbit()
    {
        if (shopOrbitTarget == null) return;

        if (_shopOrbitHeld && shopOrbitDeltaAction != null)
        {
            Vector2 delta = shopOrbitDeltaAction.action.ReadValue<Vector2>();
            if (delta.sqrMagnitude > 0.0001f)
                shopOrbitYaw += -delta.x * shopOrbitRotateSpeed * Time.unscaledDeltaTime;
        }

        ApplyShopOrbitPose();
    }

    private void ApplyShopOrbitPose()
    {
        if (shopOrbitTarget == null) return;

        Vector3 target = shopOrbitTarget.position;
        float yawRad = shopOrbitYaw * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad)) * shopOrbitDistance;
        offset.y = shopOrbitHeight;

        transform.position = target + offset;
        transform.LookAt(target);
    }

    private void OnShopOrbitPressPerformed(InputAction.CallbackContext ctx) => _shopOrbitHeld = true;
    private void OnShopOrbitPressCanceled(InputAction.CallbackContext ctx) => _shopOrbitHeld = false;

    private void HookSettingsIfNeeded()
    {
        if (_settingsHooked) return;
        _settingsHooked = true;

        // Cache "designer defaults" from inspector so settings act as multipliers.
        _baseSensitivityX = sensitivityX;
        _baseSensitivityY = sensitivityY;
        _baseSmoothTime = smoothTime;
        _baseMaxOrbitDps = maxOrbitDegreesPerSecond;
        _baseAutoFollowYawSpeed = autoFollowYawSpeed;
        _basePhoneLockYawSpeed = phoneLockYawSpeed;

        GameSettingsService.EnsureLoaded();
        GameSettingsService.OnChanged += ApplySettings;
        ApplySettings(GameSettingsService.Current);
    }

    private void UnhookSettings()
    {
        if (!_settingsHooked) return;
        _settingsHooked = false;
        GameSettingsService.OnChanged -= ApplySettings;
    }

    private void ApplySettings(GameSettingsProfile s)
    {
        if (s == null) return;

        // Controls
        float sensMul = Mathf.Clamp(s.lookSensitivity, 0.25f, 3f);
        sensitivityX = _baseSensitivityX * sensMul;

        // Your pitch input is dpitch = -look.y * sensitivityY, so inverting Y is done by flipping sensitivityY sign.
        float ySign = s.invertLookY ? -1f : 1f;
        sensitivityY = _baseSensitivityY * sensMul * ySign;

        // Reduce motion: conservative damping knobs that won’t break behaviour.
        if (s.reduceMotion)
        {
            smoothTime = Mathf.Max(_baseSmoothTime, _baseSmoothTime * 1.35f);
            maxOrbitDegreesPerSecond = _baseMaxOrbitDps * 0.75f;
            autoFollowYawSpeed = _baseAutoFollowYawSpeed * 0.65f;
            phoneLockYawSpeed = _basePhoneLockYawSpeed * 0.85f;
        }
        else
        {
            smoothTime = _baseSmoothTime;
            maxOrbitDegreesPerSecond = _baseMaxOrbitDps;
            autoFollowYawSpeed = _baseAutoFollowYawSpeed;
            phoneLockYawSpeed = _basePhoneLockYawSpeed;
        }

        if (applyFovFromSettings && _cam != null)
        {
            _cam.fieldOfView = Mathf.Clamp(s.cameraFov, 60f, 110f);
        }

    }


}
