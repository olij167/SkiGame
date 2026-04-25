using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SkiGame.Progression;
using SkiGame.UI;

public class CustomizationSceneBootstrap : MonoBehaviour
{
    [Header("Showcase")]
    [SerializeField] private Transform showcasePoint;
    [SerializeField] private Transform shopOrbitTargetOverride;

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
    private CameraController _cameraController;

    private bool _isExiting;
    private bool _exitArmed; // becomes true only after we observe a full release

    // Snapshot of shop-entry customization state so cancel cleanly restores everything.
    private string _entryCustomizationStateJson;

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

        // Put the live gameplay camera into shop orbit mode.
        _cameraController = FindActiveCameraController();
        if (_cameraController != null)
            _cameraController.EnterShopOrbit(GetShopOrbitTarget(), FindUiDocumentInThisScene());

        // UI (optional)
        var mgr = PlayerStatsManager.Instance;
        var profile = mgr != null ? mgr.Profile : null;

        // Snapshot the full customization state so cancel fully restores entry state.
        if (profile != null && profile.customization != null)
        {
            _entryCustomizationStateJson = JsonUtility.ToJson(profile.customization);
        }

        // Ensure the player visuals match the saved profile when we enter the shop
        if (profile != null && _player != null)
        {
            var applier = _player.GetComponent<PlayerCustomizationApplier>();
            if (applier != null)
                applier.ApplyFromProfile(profile);
        }

        if (uiController != null)
            uiController.SetTimeController(timeController);

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
        if (_player == null) return;

        HandleExitHold();
    }

    private void FreezePlayer()
    {
        _rb = _player.GetComponent<Rigidbody>();
        _ski = _player.GetComponent<SkiController>();
        _walk = _player.GetComponent<WalkingController>();
        _playerInput = _player.GetComponent<PlayerInput>();

        if (_walk != null)
        {
            _walk.ClearExternalMove();
            _walk.SetWalkPresentationKeepsSkisEquipped(false);
            _walk.ForceEnterSkiMode();
        }

        if (_ski != null)
        {
            _ski.ClearExternalInputSource();
            _ski.ResetStackStateSilently(snapUpright: true, forwardHint: _player.transform.forward);
        }

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

    private CameraController FindActiveCameraController()
    {
        if (Camera.main != null)
        {
            CameraController mainController = Camera.main.GetComponent<CameraController>();
            if (mainController != null)
                return mainController;
        }

        CameraController anyController = FindFirstObjectByType<CameraController>();
        if (anyController != null)
            return anyController;

        Camera sceneCam = FindCameraInThisScene();
        if (sceneCam != null)
            return sceneCam.GetComponent<CameraController>();

        return null;
    }

    private Transform GetShopOrbitTarget()
    {
        if (shopOrbitTargetOverride != null)
            return shopOrbitTargetOverride;

        if (_player == null)
            return null;

        Transform previewLookAt = FindChildTransformByName(_player.transform, "PreviewLookAt");
        return previewLookAt != null ? previewLookAt : _player.transform;
    }

    private static Transform FindChildTransformByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (root.name == targetName)
            return root;

        var children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform current = children[i];
            if (current != null && current.name == targetName)
                return current;
        }

        return null;
    }

    private UIDocument FindUiDocumentInThisScene()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var docs = roots[i].GetComponentsInChildren<UIDocument>(true);
            for (int d = 0; d < docs.Length; d++)
            {
                if (docs[d] != null && docs[d].gameObject.scene == scene)
                    return docs[d];
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

        // If we are NOT applying, restore the complete entry snapshot so no shop changes leak out.
        if (!apply && profile != null && profile.customization != null)
        {
            if (!string.IsNullOrEmpty(_entryCustomizationStateJson))
                JsonUtility.FromJsonOverwrite(_entryCustomizationStateJson, profile.customization);
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
