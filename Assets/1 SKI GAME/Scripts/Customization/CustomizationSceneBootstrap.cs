using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SkiGame.Progression;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using SkiGame.UI;

public class CustomizationSceneBootstrap : MonoBehaviour
{
    [Header("Showcase")]
    [SerializeField] private Transform showcasePoint;

    [Header("Camera Orbit (Main Camera)")]
    [SerializeField] private float orbitDistance = 3.0f;
    [SerializeField] private float orbitHeight = 1.35f;
    [SerializeField] private float orbitYawDegrees = 180f;
    [SerializeField] private float orbitPitchDegrees = 10f;
    [SerializeField] private float rotateSpeed = 140f;

    [Header("Input System")]
    [SerializeField] private InputActionReference rotatePressAction; // (currently unused)
    [SerializeField] private InputActionReference rotateDeltaAction; // <Pointer>/delta
    [Tooltip("Optional: right stick for controller orbit (Vector2).")]
    [SerializeField] private InputActionReference rotateStickAction; // <Gamepad>/rightStick

    [Header("UI")]
    [SerializeField] private CustomizationUIController uiController;
    [SerializeField] private CustomizationCatalogSO catalog;

    [Header("Freeze")]
    [SerializeField] private bool disablePlayerInputComponent = true;

    [Header("Exit Shop (Hold)")]
    [SerializeField] private InputActionReference exitHoldAction; // reuse same action as interact
    [SerializeField] private float exitHoldSeconds = 0.2f;

    [Header("Hidden UI")]
    [SerializeField] private MiniMountainHudController miniMountainHud;
    [SerializeField] private MountainHudOverlayController mountainHudOverlay;
    [SerializeField] private MovementInputOverlayUI movementInputOverlay;
    [SerializeField] private WorldInteractionPromptUI worldInteractionPromptUI;
    //[SerializeField] private SkiLessonOverlayUI skiLessonOverlayUI;
    [SerializeField] private TimeWeather.TimeController timeController;
    [SerializeField] private int shopCursorPriority = 950;

    private bool _exitWasPressed;
    private float _exitHeld;

    private GameObject _player;
    private Rigidbody _rb;
    private SkiController _ski;
    private WalkingController _walk;
    private PlayerInput _playerInput;

    private Vector3 _savedPos;
    private Quaternion _savedRot;
    private bool _rotateHeld;

    private Camera _mainCam;
    private CameraController _cameraController;

    private bool _isExiting;
    private bool _exitArmed; // becomes true only after we observe a full release

    private UIDocument _uiDoc;

    // Snapshot of equipped IDs on entry (guarantees previews never persist)
    private string _entrySkinPatternId;
    private string _entryEyeIconId;
    private string _entryHatId;
    private string _entryCloakId;
    private string _entrySkisId;
    private string _entryPolesId;

    private void OnEnable()
    {
        if (rotatePressAction != null)
        {
            rotatePressAction.action.Enable();
            rotatePressAction.action.performed += OnRotatePressed;
            rotatePressAction.action.canceled += OnRotateReleased;
        }

        if (rotateDeltaAction != null) rotateDeltaAction.action.Enable();
        if (rotateStickAction != null) rotateStickAction.action.Enable();
    }

    private void OnDisable()
    {
        if (rotatePressAction != null)
        {
            rotatePressAction.action.performed -= OnRotatePressed;
            rotatePressAction.action.canceled -= OnRotateReleased;
        }
    }

    private void OnRotatePressed(UnityEngine.InputSystem.InputAction.CallbackContext _)
        => _rotateHeld = true;

    private void OnRotateReleased(UnityEngine.InputSystem.InputAction.CallbackContext _)
        => _rotateHeld = false;

    private void Start()
    {
        CustomizationShopRuntime.Register(this);
        GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 950);

        _player = CustomizationShopRuntime.PendingPlayerRoot;
        if (_player == null)
            _player = FindPlayerAnywhere();

        if (_player == null)
        {
            Debug.LogWarning("[CustomizationSceneBootstrap] No player available for customization bootstrap.");
            return;
        }

        ResolveUiReferences();

        _exitArmed = false;
        _exitWasPressed = false;
        _exitHeld = 0f;

        _savedPos = _player.transform.position;
        _savedRot = _player.transform.rotation;

        FreezePlayer();

        if (showcasePoint != null)
        {
            _player.transform.position = showcasePoint.position;
            _player.transform.rotation = showcasePoint.rotation;
        }

        // Put main camera into shop orbit mode
        _mainCam = FindCameraInThisScene();
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) _mainCam = FindFirstObjectByType<Camera>();

        if (_mainCam != null)
        {
            _cameraController = _mainCam.GetComponent<CameraController>();
            if (_cameraController != null)
                _cameraController.EnterShopOrbit(_player.transform);
        }

        SnapCamera();

        // UI (optional)
        var mgr = PlayerStatsManager.Instance;
        var profile = mgr != null ? mgr.Profile : null;

        // Cache a UI document for UITK pointer hit-testing
        if (_uiDoc == null)
            _uiDoc = FindFirstObjectByType<UIDocument>();

        // Snapshot equipped state so we can always restore on exit (previews never persist)
        if (profile != null && profile.customization != null)
        {
            var s = profile.customization;
            _entrySkinPatternId = s.equippedSkinPatternId;
            _entryEyeIconId = s.equippedEyeIconId;
            _entryHatId = s.equippedHatId;
            _entryCloakId = s.equippedJacketId;
            _entrySkisId = s.equippedSkisId;
            _entryPolesId = s.equippedPolesId;
        }

        // Ensure the player visuals match the saved profile when we enter the shop
        if (profile != null && _player != null)
        {
            var applier = _player.GetComponent<PlayerCustomizationApplier>();
            if (applier != null)
                applier.ApplyFromProfile(profile);
        }

        if (uiController != null)
        {
            uiController.Open(
                playerRoot: _player,
                previewRoot: _player,
                catalog: catalog,
                profile: profile,
                onRequestExit: RequestExit);
        }

        SetGameplayUiVisible(false);

        if (timeController != null)
            timeController.PushExternalPause();

        GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, shopCursorPriority);
    }

    private void Update()
    {
        if (_isExiting) return;
        if (_mainCam == null || _player == null) return;

        HandleExitHold();

        if (IsPointerOverAnyUI())
            return;

        float deltaYaw = 0f;

        if (_rotateHeld && rotateDeltaAction != null)
        {
            Vector2 d = rotateDeltaAction.action.ReadValue<Vector2>();
            deltaYaw += -d.x * rotateSpeed * Time.unscaledDeltaTime;
        }

        if (rotateStickAction != null)
        {
            Vector2 stick = rotateStickAction.action.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.001f)
                deltaYaw += stick.x * rotateSpeed * Time.unscaledDeltaTime;
        }

        if (Mathf.Abs(deltaYaw) > 0.0001f)
        {
            orbitYawDegrees += deltaYaw;
            SnapCamera();
        }
    }

    private bool IsPointerOverAnyUI()
    {
        // UI Toolkit hit test
        if (_uiDoc != null && _uiDoc.rootVisualElement != null)
        {
            var panel = _uiDoc.rootVisualElement.panel;
            if (panel != null && Mouse.current != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screen);

                var picked = panel.Pick(panelPos);
                if (picked != null)
                    return true;
            }
        }

        // Fallback for any uGUI
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SnapCamera()
    {
        if (_mainCam == null || _player == null) return;

        Vector3 target = GetLookAtTarget();
        float yaw = orbitYawDegrees * Mathf.Deg2Rad;
        float pitch = orbitPitchDegrees * Mathf.Deg2Rad;

        Vector3 dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 offset = dir * orbitDistance;
        offset.y = orbitHeight + Mathf.Sin(pitch) * 0.25f;

        _mainCam.transform.position = target + offset;
        _mainCam.transform.LookAt(target);
    }

    private Vector3 GetLookAtTarget()
    {
        var t = _player.transform.Find("PreviewLookAt");
        if (t != null) return t.position;
        return _player.transform.position + Vector3.up * 1.35f;
    }

    private void FreezePlayer()
    {
        _rb = _player.GetComponent<Rigidbody>();
        _ski = _player.GetComponent<SkiController>();
        _walk = _player.GetComponent<WalkingController>();
        _playerInput = _player.GetComponent<PlayerInput>();

        if (_ski != null) _ski.enabled = false;
        if (_walk != null) _walk.enabled = false;

        if (disablePlayerInputComponent && _playerInput != null)
            _playerInput.enabled = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }
    }

    private void UnfreezePlayer()
    {
        if (_rb != null) _rb.isKinematic = false;
        if (_ski != null) _ski.enabled = true;
        if (_walk != null) _walk.enabled = true;
        if (disablePlayerInputComponent && _playerInput != null) _playerInput.enabled = true;
    }

    private GameObject FindPlayerInThisScene()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            if (root.CompareTag("Player"))
                return root;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < children.Length; t++)
            {
                var tr = children[t];
                if (tr != null && tr.CompareTag("Player"))
                    return tr.gameObject;
            }
        }

        return null;
    }

    private GameObject FindPlayerAnywhere()
    {
        var ski = FindFirstObjectByType<SkiController>();
        if (ski != null && (ski.CompareTag("Player") || ski.transform.root.CompareTag("Player")))
            return ski.gameObject;

        var walk = FindFirstObjectByType<WalkingController>();
        if (walk != null && (walk.CompareTag("Player") || walk.transform.root.CompareTag("Player")))
            return walk.gameObject;

        var input = FindFirstObjectByType<PlayerInput>();
        if (input != null && (input.CompareTag("Player") || input.transform.root.CompareTag("Player")))
            return input.gameObject;

        var tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged;
    }
    private Camera FindCameraInThisScene()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var cams = roots[i].GetComponentsInChildren<Camera>(true);
            for (int c = 0; c < cams.Length; c++)
            {
                var cam = cams[c];
                if (cam != null && cam.gameObject.scene == scene)
                    return cam;
            }
        }

        return null;
    }

    public void RequestExit(bool applyChangesToLivePlayer)
    {
        if (_isExiting) return;
        _isExiting = true;
        StartCoroutine(ExitRoutine(applyChangesToLivePlayer));
    }

    private void HandleExitHold()
    {
        if (exitHoldAction == null) return;

        var a = exitHoldAction.action;
        if (a != null && !a.enabled) a.Enable();

        bool pressed = a != null && a.IsPressed();

        // Arm only after we've seen the button released while in the shop.
        if (!_exitArmed)
        {
            if (!pressed)
                _exitArmed = true;

            // While not armed, ignore any held state carried from "enter"
            _exitWasPressed = false;
            _exitHeld = 0f;
            return;
        }

        if (!pressed)
        {
            _exitWasPressed = false;
            _exitHeld = 0f;
            return;
        }

        if (!_exitWasPressed)
        {
            _exitWasPressed = true;
            _exitHeld = 0f;
            return;
        }

        _exitHeld += Time.unscaledDeltaTime;
        if (_exitHeld >= exitHoldSeconds)
        {
            _exitHeld = -999f; // consume
            RequestExit(applyChangesToLivePlayer: false);
        }
    }

    private System.Collections.IEnumerator ExitRoutine(bool apply)
    {
        // 1) Re-apply the *equipped* loadout from the profile so any preview-only
        //    selections are cleared before we return to gameplay.
        var mgr = PlayerStatsManager.Instance;
        var profile = mgr != null ? mgr.Profile : null;

        // If we are NOT applying, force-restore the equipped IDs from when we entered the shop.
        // This guarantees previews can never "leak" into saved equip state.
        if (!apply && profile != null && profile.customization != null)
        {
            var s = profile.customization;
            s.equippedSkinPatternId = _entrySkinPatternId;
            s.equippedEyeIconId = _entryEyeIconId;
            s.equippedHatId = _entryHatId;
            s.equippedJacketId = _entryCloakId;
            s.equippedSkisId = _entrySkisId;
            s.equippedPolesId = _entryPolesId;
        }

        if (profile != null && _player != null)
        {
            var applier = _player.GetComponent<PlayerCustomizationApplier>();
            if (applier != null)
            {
                applier.ApplyFromProfile(profile);
            }
        }

        // 2) Optionally save profile if exiting via the "Apply" path.
        if (apply)
            PlayerStatsManager.Instance?.Save();

        // 3) Restore camera controller mode
        if (_cameraController != null)
            _cameraController.ExitShopOrbit();

        // 4) Restore player transform + gameplay
        if (_player != null)
        {
            _player.transform.position = _savedPos;
            _player.transform.rotation = _savedRot;
        }

        SetGameplayUiVisible(true);

        if (timeController != null)
            timeController.PopExternalPause();

        GameCursorService.Release(this);

        UnfreezePlayer();

        // 5) IMPORTANT: clear runtime state before unloading
        CustomizationShopRuntime.Unregister(this);
        GameCursorService.Release(this);
        CustomizationShopRuntime.ClearPendingPlayerRoot(_player);

        // 6) IMPORTANT: unload the additive shop scene so it can be entered again
        Scene shopScene = gameObject.scene;
        if (shopScene.IsValid() && shopScene.isLoaded)
        {
            var op = SceneManager.UnloadSceneAsync(shopScene);
            while (op != null && !op.isDone) yield return null;
        }

        // Done
    }

    private void ResolveUiReferences()
    {
        if (miniMountainHud == null)
            miniMountainHud = FindFirstObjectByType<MiniMountainHudController>();

        if (mountainHudOverlay == null)
            mountainHudOverlay = FindFirstObjectByType<MountainHudOverlayController>();

        if (movementInputOverlay == null)
            movementInputOverlay = FindFirstObjectByType<MovementInputOverlayUI>();

        if (worldInteractionPromptUI == null)
            worldInteractionPromptUI = FindFirstObjectByType<WorldInteractionPromptUI>();

        //if (skiLessonOverlayUI == null)
        //    skiLessonOverlayUI = FindFirstObjectByType<SkiLessonOverlayUI>();

        if (timeController == null)
            timeController = FindFirstObjectByType<TimeWeather.TimeController>();
    }

    private void SetGameplayUiVisible(bool visible)
    {
        if (miniMountainHud != null)
            miniMountainHud.gameObject.SetActive(visible);

        if (mountainHudOverlay != null)
            mountainHudOverlay.gameObject.SetActive(visible);

        if (movementInputOverlay != null)
            movementInputOverlay.gameObject.SetActive(visible);

        if (worldInteractionPromptUI != null)
            worldInteractionPromptUI.gameObject.SetActive(visible);

        //if (skiLessonOverlayUI != null)
        //    skiLessonOverlayUI.gameObject.SetActive(visible);
    }

    private void OnDestroy()
    {
        GameCursorService.Release(this);
        CustomizationShopRuntime.ClearPendingPlayerRoot(_player);
        CustomizationShopRuntime.Unregister(this);
    }
}
