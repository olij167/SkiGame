using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SkiGame.UI;

public class CustomizationPortal : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Scene")]
    [SerializeField] private string customizationSceneName = "CharacterCustomization";

    [Header("Input (use Player/SkiLift by default)")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Hold")]
    [SerializeField] private bool requireHold = true;
    [SerializeField] private float holdSeconds = 0.15f;


    private GameObject _playerRootInTrigger;
    private bool _busy;
    private bool _wasPressed;
    private float _held;
    private bool _enterArmed;

    private void OnEnable()
    {
        // Optional: keep enable if you want, but do NOT disable on OnDisable.
        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        // Intentionally do nothing: don't disable shared actions.
    }

    private void Update()
    {
        if (_busy) return;
        if (_playerRootInTrigger == null) return;
        if (interactAction == null) return;

        var a = interactAction.action;
        if (a != null && !a.enabled) a.Enable();

        bool pressed = a.IsPressed();

        if (!_enterArmed)
        {
            if (!pressed) _enterArmed = true;   // arm after release
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
                _held = -999f; // consume
                Trigger();
            }
        }
    }

    private void Trigger()
    {
        if (CustomizationShopRuntime.IsOpen)
        {
            Debug.Log("[CustomizationPortal] Shop already open -> requesting exit");
            CustomizationShopRuntime.RequestExit(false);
            return;
        }

        if (IsShopSceneLoaded())
        {
            Debug.Log("[CustomizationPortal] Shop scene still loaded -> not loading again");
            return;
        }

        Debug.Log("[CustomizationPortal] Loading shop scene additively...");
        _busy = true;
        StartCoroutine(LoadCustomizationAdditive());
    }

    private bool IsShopSceneLoaded()
    {
        var s = SceneManager.GetSceneByName(customizationSceneName);
        return s.IsValid() && s.isLoaded;
    }

    private System.Collections.IEnumerator LoadCustomizationAdditive()
    {
        var op = SceneManager.LoadSceneAsync(customizationSceneName, LoadSceneMode.Additive);
        while (op != null && !op.isDone) yield return null;
        _busy = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null) return;

        _enterArmed = false; // require release before allowing enter again

        _playerRootInTrigger = root;
    }

    private void OnTriggerExit(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null) return;

        if (_playerRootInTrigger == root)
        {
            _playerRootInTrigger = null;
        }
    }

    private GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null) return null;

        // Prefer components
        var ski = other.GetComponentInParent<SkiController>();
        if (ski != null) return ski.gameObject;

        var walk = other.GetComponentInParent<WalkingController>();
        if (walk != null) return walk.gameObject;

        var pi = other.GetComponentInParent<PlayerInput>();
        if (pi != null) return pi.gameObject;

        // Fallback to tag on any parent
        var t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player")) return t.gameObject;
            t = t.parent;
        }

        return null;
    }

    public bool IsPromptAvailable => _playerRootInTrigger != null && !_busy;

    public string PromptActionText => "Interact";

    public string PromptDescriptionText
    {
        get
        {
            if (CustomizationShopRuntime.IsOpen)
                return "Close Shop";

            return "Open Shop";
        }
    }

    public bool PromptUsesHold => requireHold;

    public float PromptHoldDuration => holdSeconds;

    public Vector3 PromptWorldPosition => transform.position;

    public int PromptPriority => 50;
}
