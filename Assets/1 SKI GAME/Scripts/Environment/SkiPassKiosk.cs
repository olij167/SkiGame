using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;

[DisallowMultipleComponent]
public class SkiPassKiosk : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Prompt")]
    [SerializeField] private bool requireHold = false;
    [SerializeField] private float holdSeconds = 0.15f;
    [SerializeField] private string promptText = "Open Ski Pass Kiosk";

    [Header("UI")]
    [SerializeField] private SkiPassKioskUI kioskUI;

    private GameObject _playerRootInTrigger;
    private bool _busy;
    private bool _wasPressed;
    private float _held;
    private bool _enterArmed;

    private void Awake()
    {
        if (kioskUI == null)
            kioskUI = FindObjectOfType<SkiPassKioskUI>(true);
    }

    private void OnEnable()
    {
        WorldInteractionPromptRegistry.Register(this);

        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        WorldInteractionPromptRegistry.Unregister(this);
    }

    private void Update()
    {
        if (_busy) return;
        if (_playerRootInTrigger == null) return;
        if (kioskUI == null) return;
        if (interactAction == null || interactAction.action == null) return;

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
        if (_playerRootInTrigger != null && _playerRootInTrigger.CompareTag("NPC"))
        {
            _playerRootInTrigger = null;
            return;
        }

        if (kioskUI == null)
            return;

        if (kioskUI.IsOpen)
        {
            kioskUI.Close();
            return;
        }

        kioskUI.Open();
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

    public bool IsPromptAvailable => _playerRootInTrigger != null && !_busy && kioskUI != null && !kioskUI.IsBlocked;
    public string PromptActionText => "Interact";
    public string PromptDescriptionText => kioskUI != null && kioskUI.IsOpen ? "Close Ski Pass Kiosk" : promptText;
    public bool PromptUsesHold => requireHold;
    public float PromptHoldDuration => holdSeconds;
    public Vector3 PromptWorldPosition => transform.position;
    public int PromptPriority => 55;
}
