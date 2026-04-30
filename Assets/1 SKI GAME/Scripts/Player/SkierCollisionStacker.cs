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
    [SerializeField] private float maxUpwardVelocityAfterNpcImpact = 1f;
    [SerializeField, Range(0f, 1f)] private float npcImpactSlideRetention = 0.35f;

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

        if (otherController.IsStacked)
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

        bool strongImpact = relativeSpeed >= bothStackRelativeSpeed;
        if (!strongImpact && selfPlanarSpeed < otherPlanarSpeed)
            return;

        ContactPoint cp = collision.GetContact(0);
        Vector3 impactNormal = relativeSpeed > 0.0001f
            ? -relativeVelocity / relativeSpeed
            : cp.normal;
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = transform.position - otherController.transform.position;
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = transform.forward;
        impactNormal.Normalize();

        float severityT = Mathf.InverseLerp(relativeSpeedToStack, bothStackRelativeSpeed, relativeSpeed);
        float severity = Mathf.Lerp(minSeverity, maxSeverity, severityT);

        if (body != null)
            body.linearVelocity = ClampNpcImpactVelocity(body.linearVelocity, impactNormal);

        skiController.TriggerImpactStackFromPointAndNormal(cp.point, impactNormal, relativeVelocity, severity, "SkierCollision");
        _lastImpactTime = Time.time;

        if (body != null)
            body.linearVelocity = ClampNpcImpactVelocity(body.linearVelocity, impactNormal);

        SkierCollisionStacker otherStacker = otherController.GetComponent<SkierCollisionStacker>();
        if (otherStacker != null)
            otherStacker.NotifyImpactCooldown(Time.time);

        if (!stackBothSkiersOnStrongImpact || !strongImpact || otherController.IsStacked)
            return;

        Rigidbody otherBody = otherStacker != null ? otherStacker.body : otherController.GetComponent<Rigidbody>();
        if (otherBody != null)
            otherBody.linearVelocity = ClampNpcImpactVelocity(otherBody.linearVelocity, -impactNormal);

        otherController.TriggerImpactStackFromPointAndNormal(cp.point, -impactNormal, -relativeVelocity, severity, "SkierCollision");

        if (otherBody != null)
            otherBody.linearVelocity = ClampNpcImpactVelocity(otherBody.linearVelocity, -impactNormal);

        if (otherStacker != null)
            otherStacker.NotifyImpactCooldown(Time.time);
    }

    private Vector3 ClampNpcImpactVelocity(Vector3 velocity, Vector3 impactNormal)
    {
        Vector3 slide = Vector3.ProjectOnPlane(velocity, impactNormal);
        velocity = slide * Mathf.Clamp01(npcImpactSlideRetention);

        if (velocity.y > maxUpwardVelocityAfterNpcImpact)
            velocity.y = maxUpwardVelocityAfterNpcImpact;

        return velocity;
    }

    private void NotifyImpactCooldown(float timeStamp)
    {
        _lastImpactTime = timeStamp;
    }
}
