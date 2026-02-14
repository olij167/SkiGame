using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SkiResortHoldInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference resortAction;
    [SerializeField] private WalkingController walkingController;

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

    private void Awake()
    {
        if (controller == null)
            controller = SkiResortStateController.Instance;

        if (walkingController == null)
            walkingController = GetComponent<WalkingController>();

    }

    private void OnEnable()
    {
        if (resortAction?.action != null) resortAction.action.Enable();
    }

    private void OnDisable()
    {
        if (resortAction?.action != null) resortAction.action.Disable();
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

        // Must be in a resort interaction zone.
        if (currentZone == null || !currentZone.IsPlayerInZone(gameObject))
        {
            _holdT = 0f;
            _armed = true;
            return;
        }

        var action = resortAction != null ? resortAction.action : null;
        if (action == null)
            return;

        bool isHeld = action.IsPressed();

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
                controller.RequestToggleResort(gameObject, currentZone);
            }
        }
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
