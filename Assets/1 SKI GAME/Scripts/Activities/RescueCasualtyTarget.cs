using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;

[DisallowMultipleComponent]
public sealed class RescueCasualtyTarget : MonoBehaviour, IWorldInteractionPromptSource
{
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private string promptText = "Secure Casualty";
    [SerializeField] private int promptPriority = 65;

    private RescueService _service;
    private int _casualtyIndex = -1;
    private GameObject _playerRootInTrigger;
    private bool _wasPressed;

    public void Initialize(RescueService service, int casualtyIndex)
    {
        _service = service;
        _casualtyIndex = casualtyIndex;
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();
    }

    private void Update()
    {
        if (_playerRootInTrigger == null || _service == null || _casualtyIndex < 0)
            return;

        if (interactAction == null || interactAction.action == null)
            return;

        bool pressed = interactAction.action.IsPressed();

        if (!pressed)
        {
            _wasPressed = false;
            return;
        }

        if (!_wasPressed)
        {
            _wasPressed = true;
            _service.TrySecureCasualty(_casualtyIndex);
        }
    }

    public void SetInteractAction(InputActionReference action)
    {
        interactAction = action;
    }

    private void OnTriggerEnter(Collider other)
    {
        var pi = other.GetComponentInParent<PlayerInput>();
        if (pi != null && !pi.CompareTag("NPC"))
            _playerRootInTrigger = pi.gameObject;
    }

    private void OnTriggerExit(Collider other)
    {
        var pi = other.GetComponentInParent<PlayerInput>();
        if (pi != null && pi.gameObject == _playerRootInTrigger)
            _playerRootInTrigger = null;
    }

    public bool IsPromptAvailable => _playerRootInTrigger != null && _service != null && _service.IsCasualtySecureable(_casualtyIndex);
    public string PromptActionText => "Interact";
    public string PromptDescriptionText => promptText;
    public bool PromptUsesHold => false;
    public float PromptHoldDuration => 0f;
    public Vector3 PromptWorldPosition => transform.position;
    public int PromptPriority => promptPriority;
}