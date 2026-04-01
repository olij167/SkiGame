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

        if (_playerRootInTrigger == null)
        {
            Debug.LogWarning("[CustomizationPortal] No player root in trigger; cannot open customization shop.");
            return;
        }

        if (IsShopSceneLoaded())
        {
            Debug.Log("[CustomizationPortal] Shop scene still loaded -> not loading again");
            return;
        }

        if (_playerRootInTrigger != null && _playerRootInTrigger.CompareTag("NPC"))
        {
            Debug.LogWarning("[CustomizationPortal] Ignoring trigger because the stored root is an NPC.");
            _playerRootInTrigger = null;
            return;
        }

        Debug.Log("[CustomizationPortal] Loading shop scene additively...");
        CustomizationShopRuntime.SetPendingPlayerRoot(_playerRootInTrigger);
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
        if (root == null)
            return;

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
        if (other == null)
            return null;

        // Never allow NPCs to claim player interactions.
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
