using System.Collections;
using SkiGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;

public class CustomizationPortal : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Hold")]
    [SerializeField] private bool requireHold = false;
    [SerializeField] private float holdSeconds = 0.15f;

    [Header("Entry")]
    [SerializeField] private CustomizationShopEntryPoint entryPoint;

    [Header("Approach")]
    [SerializeField] private float autoWalkMoveStrength = 0.85f;
    [SerializeField] private bool autoWalkUsesSprint = false;
    [SerializeField] private float farApproachDistance = 6f;
    [SerializeField] private float arriveDistance = 1f;
    [SerializeField] private float cancelMoveThreshold = 0.15f;

    private GameObject _playerRootInTrigger;
    private bool _busy;
    private bool _wasPressed;
    private float _held;
    private bool _enterArmed;
    private Coroutine _approachRoutine;

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();
    }

    private void Update()
    {
        if (_busy || _playerRootInTrigger == null)
            return;

        if (interactAction == null)
            return;

        var action = interactAction.action;
        if (action != null && !action.enabled)
            action.Enable();

        bool pressed = action.IsPressed();

        if (!_enterArmed)
        {
            if (!pressed)
                _enterArmed = true;

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
        if (CustomizationShopRuntime.IsOpen)
        {
            CustomizationShopRuntime.RequestExit(false);
            return;
        }

        if (_playerRootInTrigger == null || entryPoint == null)
            return;

        if (_approachRoutine != null)
            StopCoroutine(_approachRoutine);

        _approachRoutine = StartCoroutine(CoApproachEntryPoint());
    }

    private IEnumerator CoApproachEntryPoint()
    {
        _busy = true;

        GameObject playerRoot = _playerRootInTrigger;
        if (playerRoot == null || entryPoint == null)
        {
            _busy = false;
            yield break;
        }

        WalkingController walking = playerRoot.GetComponentInChildren<WalkingController>();
        SkiController skiController = playerRoot.GetComponentInChildren<SkiController>();
        AutoSkiApproachDriver skiApproach = playerRoot.GetComponent<AutoSkiApproachDriver>();
        Rigidbody playerRb = playerRoot.GetComponentInChildren<Rigidbody>();

        entryPoint.ArmForPlayer(playerRoot);

        bool restoreSkisOnCancel = false;
        bool useSkiApproach = false;

        if (walking != null)
        {
            restoreSkisOnCancel = walking.SkisOn;
            walking.ClearExternalMove();
            walking.ControlsEnabled = true;

            useSkiApproach = restoreSkisOnCancel &&
                             skiController != null &&
                             skiController.enabled &&
                             skiApproach != null;

            Vector3 to = entryPoint.transform.position - skiController.transform.position;
            float distance = to.magnitude;

            if (useSkiApproach)
            {
                walking.ForceEnterSkiMode();
                skiApproach.BeginApproach(this, entryPoint.ApproachPosition, autoWalkMoveStrength, Mathf.Max(0.05f, arriveDistance), allowPoles: distance > 2.5f);
            }
            else
            {
                walking.ForceEnterWalkMode();
                walking.SetWalkPresentationKeepsSkisEquipped(true);
            }
        }

        if (playerRb != null)
        {
            Vector3 v = playerRb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            playerRb.linearVelocity = v;
            playerRb.angularVelocity = Vector3.zero;
        }

        while (playerRoot != null && entryPoint != null)
        {
            if (walking == null)
                break;

            if ((useSkiApproach && skiApproach != null && skiApproach.CancelRequested) ||
                walking.IsUserTryingToMove(cancelMoveThreshold))
            {
                CancelApproach(walking, skiApproach, restoreSkisOnCancel);

                entryPoint.DisarmPlayer(playerRoot);
                _busy = false;
                _approachRoutine = null;
                yield break;
            }

            if (entryPoint.HasReached(playerRoot))
            {
                PrepareForShopEntry(walking, skiController, skiApproach, playerRb);
                yield return entryPoint.LoadForPlayer(playerRoot);
                break;
            }

            Vector3 target = entryPoint.ApproachPosition;
            Vector3 to = target - walking.transform.position;
            to.y = 0f;

            float distance = to.magnitude;
            if (distance <= Mathf.Max(0.05f, arriveDistance))
            {
                PrepareForShopEntry(walking, skiController, skiApproach, playerRb);
                yield return entryPoint.LoadForPlayer(playerRoot);
                break;
            }

            Vector3 dir = to / Mathf.Max(0.001f, distance);
            float distanceT = Mathf.InverseLerp(arriveDistance, farApproachDistance, distance);
            float strength = Mathf.Lerp(autoWalkMoveStrength, 1f, distanceT);

            if (useSkiApproach && skiApproach != null)
            {
                skiApproach.UpdateApproachTarget(entryPoint.ApproachPosition, strength, Mathf.Max(0.05f, arriveDistance), allowPoles: true);
            }
            else
            {
                walking.SetExternalMoveToward(dir, strength, sprint: autoWalkUsesSprint);
            }

            yield return null;
        }

        if (skiApproach != null)
            skiApproach.StopApproach(this);

        if (walking != null)
            walking.ClearExternalMove();

        _busy = false;
        _approachRoutine = null;
    }

    private void CancelApproach(WalkingController walking, AutoSkiApproachDriver skiApproach, bool restoreSkisOnCancel)
    {
        if (skiApproach != null)
            skiApproach.StopApproach(this);

        if (walking == null)
            return;

        walking.ClearExternalMove();
        walking.SetWalkPresentationKeepsSkisEquipped(false);

        if (restoreSkisOnCancel)
            walking.ForceEnterSkiMode();
        else
            walking.RefreshEquipmentPresentation();
    }

    private static void PrepareForShopEntry(WalkingController walking, SkiController skiController, AutoSkiApproachDriver skiApproach, Rigidbody playerRb)
    {
        if (skiApproach != null)
            skiApproach.StopApproach(null);

        if (walking != null)
        {
            walking.ClearExternalMove();
            walking.SetWalkPresentationKeepsSkisEquipped(false);
            walking.ForceEnterSkiMode();
        }

        if (skiController != null)
        {
            skiController.ClearExternalInputSource();
            skiController.ResetStackStateSilently(snapUpright: true, forwardHint: skiController.transform.forward);
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        NpcSkierBrain npc = ResolveNpcBrain(other);
        if (npc != null)
        {
            npc.DespawnToPool("EnteredShopPortal");
            return;
        }

        GameObject root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        _playerRootInTrigger = root;

        bool pressed = interactAction != null &&
                       interactAction.action != null &&
                       interactAction.action.IsPressed();

        _enterArmed = !pressed;
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        if (_playerRootInTrigger == root)
            _playerRootInTrigger = null;
    }

    private GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("NPC"))
                return null;
            t = t.parent;
        }

        t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player"))
                return t.gameObject;
            t = t.parent;
        }

        return null;
    }

    private static NpcSkierBrain ResolveNpcBrain(Collider other)
    {
        if (other == null)
            return null;

        return other.GetComponentInParent<NpcSkierBrain>();
    }

    public bool IsPromptAvailable => _playerRootInTrigger != null && !_busy;
    public string PromptActionText => "Interact";
    public string PromptDescriptionText => CustomizationShopRuntime.IsOpen ? "Close Shop" : "Enter Shop";
    public bool PromptUsesHold => requireHold;
    public float PromptHoldDuration => holdSeconds;
    public Vector3 PromptWorldPosition => transform.position;
    public int PromptPriority => 50;
}
