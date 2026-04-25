using UnityEngine;

public sealed class WeatheradeCoverageLeadTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform cameraTransform;

    [Header("Lead")]
    [SerializeField] private float minLeadDistance = 12f;
    [SerializeField] private float maxLeadDistance = 45f;
    [SerializeField] private float speedToMaxLead = 35f;
    [SerializeField] private float verticalOffset = 30f;

    [Header("Smoothing")]
    [SerializeField] private float followSharpness = 18f;

    private void LateUpdate()
    {
        if (!playerRoot)
            return;

        Vector3 moveDir = GetPreferredDirection();
        float speed = playerRigidbody ? playerRigidbody.linearVelocity.magnitude : 0f;

        float t = speedToMaxLead > 0f ? Mathf.Clamp01(speed / speedToMaxLead) : 0f;
        float leadDistance = Mathf.Lerp(minLeadDistance, maxLeadDistance, t);

        Vector3 targetPosition = playerRoot.position + moveDir * leadDistance;
        targetPosition.y = playerRoot.position.y + verticalOffset;

        float lerp = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerp);
    }

    private Vector3 GetPreferredDirection()
    {
        if (playerRigidbody && playerRigidbody.linearVelocity.sqrMagnitude > 4f)
        {
            Vector3 velocity = playerRigidbody.linearVelocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > 0.01f)
                return velocity.normalized;
        }

        if (cameraTransform)
        {
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude > 0.01f)
                return cameraForward.normalized;
        }

        Vector3 forward = playerRoot.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
    }

    private void Reset()
    {
        transform.name = "WeatheradeCoverageLeadTarget";
    }
}