using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;

[DisallowMultipleComponent]
public class SkiResortHoldInteractor : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Input")]
    [SerializeField] private InputActionReference resortAction;
    [SerializeField] private WalkingController walkingController;

    [SerializeField] private bool requireHold = true;
    [SerializeField] private float holdSeconds = 0.6f;

    [Header("Links")]
    [SerializeField] private SkiResortStateController controller;
    [SerializeField] private SkiResortZone currentZone;

    [Header("Cancel")]
    [SerializeField] private float cancelMoveThreshold = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool logEvents;

    private float _holdT;
    private bool _armed = true;

    public bool IsPromptAvailable
    {
        get
        {
            if (controller == null)
                controller = SkiResortStateController.Instance;

            var zone = ResolveInteractZone();
            if (controller == null || zone == null)
                return false;

            if (controller.State == SkiResortStateController.ResortState.InResort)
                return true;

            return zone.IsPlayerInZone(gameObject) &&
                   controller.State == SkiResortStateController.ResortState.Outside;
        }
    }

    public string PromptActionText => "Interact";

    public string PromptDescriptionText
    {
        get
        {
            if (controller == null)
                controller = SkiResortStateController.Instance;

            if (controller != null && controller.State == SkiResortStateController.ResortState.InResort)
                return "Leave Resort";

            SkiResortZone zone = ResolveInteractZone();
            if (zone == null)
                return string.Empty;

            SkiResortAccessManager resortAccessManager = SkiResortAccessManager.Instance != null
                ? SkiResortAccessManager.Instance
                : FindObjectOfType<SkiResortAccessManager>();

            if (resortAccessManager == null || string.IsNullOrWhiteSpace(zone.ResortId))
                return "Enter Resort";

            if (resortAccessManager.IsResortAccessible(zone.ResortId))
                return "Enter Resort";

            if (resortAccessManager.TryQuoteRental(zone.ResortId, out SkiResortAccessManager.RentalQuote quote, out _))
                return $"Rent Resort ({quote.cost})";

            return "Resort Locked";
        }
    }

    public bool PromptUsesHold => requireHold;
    public float PromptHoldDuration => holdSeconds;
    public Vector3 PromptWorldPosition => ResolveInteractZone() != null ? ResolveInteractZone().transform.position : transform.position;
    public int PromptPriority => 40;

    public bool IsPlayerInsideResort =>
        controller != null &&
        controller.State == SkiResortStateController.ResortState.InResort;

    private void Awake()
    {
        if (controller == null)
            controller = SkiResortStateController.Instance;

        if (walkingController == null)
            walkingController = GetComponent<WalkingController>();
    }

    private void OnEnable()
    {
        if (resortAction?.action != null)
            resortAction.action.Enable();
    }

    private void OnDisable()
    {
        if (resortAction?.action != null)
            resortAction.action.Disable();
    }

    private void Update()
    {
        if (controller == null)
            controller = SkiResortStateController.Instance;

        if (controller != null &&
            controller.State == SkiResortStateController.ResortState.ApproachingEnter &&
            walkingController != null &&
            walkingController.IsUserTryingToMove(cancelMoveThreshold))
        {
            if (logEvents) Debug.Log("[Resort] Cancel enter (player moved)");
            controller.CancelEnter();

            _holdT = 0f;
            _armed = true;
            return;
        }

        var interactZone = ResolveInteractZone();

        bool validForInteraction =
            interactZone != null &&
            controller != null &&
            (
                controller.State == SkiResortStateController.ResortState.InResort ||
                interactZone.IsPlayerInZone(gameObject)
            );

        if (!validForInteraction)
        {
            _holdT = 0f;
            _armed = true;
            return;
        }

        var action = resortAction != null ? resortAction.action : null;
        if (action == null)
            return;

        bool isHeld = action.IsPressed();

        if (!requireHold)
        {
            if (!isHeld)
            {
                _holdT = 0f;
                _armed = true;
                return;
            }

            if (!_armed)
                return;

            _armed = false;

            if (controller != null)
            {
                if (logEvents) Debug.Log("[Resort] Press detected → Toggle request");
                controller.RequestToggleResort(gameObject, interactZone);
            }

            return;
        }

        if (!isHeld)
        {
            _holdT = 0f;
            _armed = true;
            return;
        }

        if (!_armed)
            return;

        _holdT += Time.deltaTime;

        if (_holdT >= holdSeconds)
        {
            _holdT = 0f;
            _armed = false;

            if (controller != null)
            {
                if (logEvents) Debug.Log("[Resort] Hold complete → Toggle request");
                controller.RequestToggleResort(gameObject, interactZone);
            }
        }
    }

    private SkiResortZone ResolveInteractZone()
    {
        if (controller == null)
            controller = SkiResortStateController.Instance;

        if (controller != null &&
            controller.State == SkiResortStateController.ResortState.InResort &&
            controller.ActiveZone != null)
        {
            return controller.ActiveZone;
        }

        if (currentZone != null && currentZone.IsPlayerInZone(gameObject))
            return currentZone;

        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var zone = other != null ? other.GetComponentInParent<SkiResortZone>() : null;
        if (zone == null) return;

        currentZone = zone;
    }

    private void OnTriggerExit(Collider other)
    {
        var zone = other != null ? other.GetComponentInParent<SkiResortZone>() : null;
        if (zone == null) return;

        if (currentZone == zone)
            currentZone = null;
    }
}
