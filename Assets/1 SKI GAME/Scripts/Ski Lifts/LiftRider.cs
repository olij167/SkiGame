using UnityEngine;
using UnityEngine.InputSystem;

public class LiftRider : MonoBehaviour
{
    [Header("Dependencies")]
    public Rigidbody rb;
    public SkiController skiController;
    public WalkingController walkingController;

    [Header("Detach settings")]
    public float detachForwardImpulse = 3f;
    public float detachDownOffset = 0.2f;

    [Header("Lift detection")]
    [SerializeField] private LayerMask liftCarrierLayers = ~0;

    [Tooltip("Radius around the player used to search for nearby lift carriers.")]
    [SerializeField] private float liftDetectionRadius = 3.5f;

    [Tooltip("Optional point used for lift detection (e.g. hips/chest). If null, uses this transform.position.")]
    [SerializeField] private Transform liftDetectPoint;

    [Tooltip("If true, while the button is held we keep trying to attach every physics frame.")]
    [SerializeField] private bool holdToAttach = true;

    [Tooltip("If you tap slightly early, we still try to attach for this long (seconds).")]
    [SerializeField] private float attachInputBuffer = 0.20f;

    [Tooltip("How often (seconds) we attempt to attach while held/buffered.")]
    [SerializeField] private float attachAttemptInterval = 0.05f;

    [Header("Input")]
    [Tooltip("Input action used to attach/detach from lifts (e.g. Player/Interact).")]
    [SerializeField] private InputActionReference liftInput;

    private LiftCarrier currentCarrier;
    private bool isAttached;
    private bool isChairMode; // true if attached to chair, false if T-bar
    private bool liftInputHeld;

    private float _attachBufferUntilTime;
    private float _nextAttachAttemptTime;

    // Non-alloc overlap cache
    private readonly Collider[] _overlapHits = new Collider[32];

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // Hook into the assigned InputActionReference
        if (liftInput != null && liftInput.action != null)
        {
            liftInput.action.started += OnLiftInput;
            liftInput.action.performed += OnLiftInput;
            liftInput.action.canceled += OnLiftInput;
            liftInput.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (liftInput != null && liftInput.action != null)
        {
            liftInput.action.started -= OnLiftInput;
            liftInput.action.performed -= OnLiftInput;
            liftInput.action.canceled -= OnLiftInput;
            liftInput.action.Disable();
        }
    }

    /// <summary>
    /// Callback from the Input System for the configured lift input action.
    /// This maps the action phases onto the existing SetLiftInput logic.
    /// </summary>
    private void OnLiftInput(InputAction.CallbackContext context)
    {
        bool isPressed = context.ReadValueAsButton();
        bool wasPressedThisFrame = context.started;

        if (wasPressedThisFrame)
        {
            // Allow "press slightly early" attachment
            _attachBufferUntilTime = Time.time + attachInputBuffer;
        }

        SetLiftInput(isPressed, wasPressedThisFrame);
    }

    /// <summary>
    /// Core logic for how lift input affects attachment/detachment.
    /// Now usually driven by OnLiftInput, but you can still call it externally if needed.
    /// </summary>
    public void SetLiftInput(bool isPressed, bool wasPressedThisFrame)
    {
        liftInputHeld = isPressed;

        // Chair logic: toggle on press
        if (isAttached && isChairMode)
        {
            if (wasPressedThisFrame)
            {
                RequestDetach();
            }
        }
        else if (!isAttached)
        {
            // Not attached: trying to attach to closest carrier within range
            if (wasPressedThisFrame)
            {
                TryAttachToNearbyCarrier();
            }
        }

        // T-bar logic: must hold
        if (isAttached && !isChairMode && !liftInputHeld)
        {
            // Released grip
            RequestDetach();
        }
    }

    private void FixedUpdate()
    {
        // Attachment assist: while held or buffered, keep attempting to attach.
        if (!isAttached)
        {
            bool shouldTryAttach =
                (holdToAttach && liftInputHeld) ||
                (Time.time <= _attachBufferUntilTime);

            if (shouldTryAttach && Time.time >= _nextAttachAttemptTime)
            {
                _nextAttachAttemptTime = Time.time + attachAttemptInterval;
                TryAttachToNearbyCarrier();
            }
        }

        // For T-bar, we don't parent; we apply a pull along cable while attached
        if (isAttached && !isChairMode && currentCarrier != null)
        {
            Vector3 forward = currentCarrier.transform.forward;
            // Pull uphill along cable, but let SkiController do terrain work
            Vector3 targetVel = forward * (currentCarrier.line.bandSpeed * 0.9f);
            Vector3 vel = rb.linearVelocity;
            Vector3 velChange = targetVel - vel;

            // Only project along cable direction on XZ to avoid fighting vertical
            velChange = Vector3.ProjectOnPlane(velChange, Vector3.up);
            rb.AddForce(velChange, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Searches for a LiftCarrier within liftDetectionRadius and attaches if possible.
    /// This is only called when the lift input is pressed and the rider is not already attached.
    /// </summary>
    private void TryAttachToNearbyCarrier()
    {
        Vector3 center = liftDetectPoint ? liftDetectPoint.position : transform.position;

        // Non-alloc overlap (fast, no GC). Explicitly include triggers.
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            liftDetectionRadius,
            _overlapHits,
            liftCarrierLayers,
            QueryTriggerInteraction.Collide
        );

        if (hitCount <= 0)
            return;

        LiftCarrier best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _overlapHits[i];
            if (!c) continue;

            var carrier = c.GetComponentInParent<LiftCarrier>();
            if (!carrier) continue;

            if (!carrier.CanAttach(this))
                continue;

            // Score: prefer closer + in front (reduces attaching to carriers behind you)
            Vector3 toAttach = carrier.attachPoint.position - center;
            float dist = toAttach.magnitude;
            if (dist < 0.001f) dist = 0.001f;

            Vector3 toAttachDir = toAttach / dist;
            float facing = Vector3.Dot(transform.forward, toAttachDir); // -1..1
            float score = (facing * 2f) - dist; // tune weights as needed

            if (score > bestScore)
            {
                bestScore = score;
                best = carrier;
            }
        }

        if (best != null)
        {
            best.AttachRider(this);

            // Clear buffer once we attach
            _attachBufferUntilTime = 0f;
        }
    }

    public void OnAttachedToCarrier(LiftCarrier carrier)
    {
        currentCarrier = carrier;
        isAttached = true;
        isChairMode = (carrier.mode == LiftCarrierMode.Chair);

        if (isChairMode)
        {
            // Chair: fully parent + disable ski/walk controllers
            rb.isKinematic = true;
            transform.SetParent(carrier.attachPoint, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (skiController) skiController.enabled = false;
            if (walkingController) walkingController.enabled = false;
        }
        else
        {
            // T-bar: keep physics, just gently snap horizontally behind bar
            rb.isKinematic = false;
            transform.SetParent(null);

            if (walkingController) walkingController.enabled = false;
            if (skiController) skiController.enabled = true; // we assume we're skiing

            // Optionally align facing direction to cable
            Vector3 fwd = carrier.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }
    }

    public void OnDetachedFromCarrier(LiftCarrier carrier)
    {
        if (carrier != currentCarrier) return;

        // Common detach behaviour
        transform.SetParent(null);
        rb.isKinematic = false;
        if (skiController) skiController.enabled = true;

        // Small forward nudge along cable direction
        Vector3 forward = carrier.transform.forward;
        rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        rb.AddForce(forward * detachForwardImpulse, ForceMode.VelocityChange);

        // Nudge down a bit so we don't hover
        transform.position += Vector3.down * detachDownOffset;

        isAttached = false;
        currentCarrier = null;
    }

    private void RequestDetach()
    {
        if (currentCarrier != null)
        {
            currentCarrier.DetachRider(this);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualise the detection radius in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, liftDetectionRadius);
    }
#endif
}
