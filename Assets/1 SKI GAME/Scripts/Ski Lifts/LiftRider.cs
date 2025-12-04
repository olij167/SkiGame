using UnityEngine;

public class LiftRider : MonoBehaviour
{
    [Header("Dependencies")]
    public Rigidbody rb;
    public SkiController skiController;
    public WalkingController walkingController;

    [Header("Detach settings")]
    public float detachForwardImpulse = 3f;
    public float detachDownOffset = 0.2f;

    private LiftCarrier currentCarrier;
    private bool isAttached;
    private bool isChairMode; // true if attached to chair, false if T-bar
    private bool liftInputHeld;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    // Call this from your InputSystem wrapper
    public void SetLiftInput(bool isPressed, bool wasPressedThisFrame)
    {
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
        liftInputHeld = isPressed;
        if (isAttached && !isChairMode && !liftInputHeld)
        {
            // Released grip
            RequestDetach();
        }
    }

    private void FixedUpdate()
    {
        // For T-bar, we don't parent; we apply a pull along cable while attached
        if (isAttached && !isChairMode && currentCarrier != null)
        {
            Vector3 forward = currentCarrier.transform.forward;
            // Pull uphill along cable, but let SkiController do terrain work
            Vector3 targetVel = forward * (currentCarrier.line.speed * 0.9f);
            Vector3 vel = rb.linearVelocity;
            Vector3 velChange = targetVel - vel;

            // Only project along cable direction on XZ to avoid fighting vertical
            velChange = Vector3.ProjectOnPlane(velChange, Vector3.up);
            rb.AddForce(velChange, ForceMode.Acceleration);
        }
    }

    private void TryAttachToNearbyCarrier()
    {
        // Simple approach: sphere overlap, find closest LiftCarrier
        float radius = 3f;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        LiftCarrier best = null;
        float bestDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            var carrier = hit.GetComponentInParent<LiftCarrier>();
            if (!carrier) continue;
            if (!carrier.CanAttach(this)) continue;

            float dist = Vector3.Distance(transform.position, carrier.attachPoint.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = carrier;
            }
        }

        if (best != null)
        {
            best.AttachRider(this);
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
            if (skiController) skiController.enabled = true; // we assume we�re skiing

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
}
