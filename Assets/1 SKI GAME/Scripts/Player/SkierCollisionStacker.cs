using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkierCollisionStacker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private Rigidbody body;

    [Header("Impact Rules")]
    [SerializeField] private float relativeSpeedToStack = 5.5f;
    [SerializeField] private float minimumSelfSpeed = 2.25f;
    [SerializeField] private float cooldownSeconds = 0.4f;
    [SerializeField] private float minSeverity = 0.45f;
    [SerializeField] private float maxSeverity = 1f;
    [SerializeField] private bool stackBothSkiersOnStrongImpact = true;
    [SerializeField] private float bothStackRelativeSpeed = 8.5f;

    private float _lastImpactTime = -999f;

    private void Awake()
    {
        if (skiController == null)
            skiController = GetComponentInParent<SkiController>();

        if (body == null && skiController != null)
            body = skiController.GetComponent<Rigidbody>();

        if (body == null)
            body = GetComponentInParent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void EvaluateCollision(Collision collision)
    {
        if (collision == null || skiController == null)
            return;

        if (Time.time < _lastImpactTime + cooldownSeconds)
            return;

        if (skiController.IsStacked)
            return;

        var otherController = collision.collider != null
            ? collision.collider.GetComponentInParent<SkiController>()
            : null;

        if (otherController == null || otherController == skiController)
            return;


        Vector3 selfVelocity = body != null ? body.linearVelocity : skiController.Velocity;
        Vector3 otherVelocity = otherController.Velocity;
        Vector3 relativeVelocity = selfVelocity - otherVelocity;

        float relativeSpeed = relativeVelocity.magnitude;
        float selfPlanarSpeed = Vector3.ProjectOnPlane(selfVelocity, Vector3.up).magnitude;
        float otherPlanarSpeed = Vector3.ProjectOnPlane(otherVelocity, Vector3.up).magnitude;

        if (relativeSpeed < relativeSpeedToStack)
            return;

        if (selfPlanarSpeed < minimumSelfSpeed && otherPlanarSpeed < minimumSelfSpeed)
            return;

        ContactPoint cp = collision.GetContact(0);

        float severityT = Mathf.InverseLerp(relativeSpeedToStack, bothStackRelativeSpeed, relativeSpeed);
        float severity = Mathf.Lerp(minSeverity, maxSeverity, severityT);

        skiController.TriggerImpactStackFromPoint(cp.point, relativeVelocity, severity, "SkierCollision");
        _lastImpactTime = Time.time;

        if (!stackBothSkiersOnStrongImpact || relativeSpeed < bothStackRelativeSpeed || otherController.IsStacked)
            return;

        otherController.TriggerImpactStackFromPoint(cp.point, -relativeVelocity, severity, "SkierCollision");
    }
}