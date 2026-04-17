using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcRescueCasualtyState : MonoBehaviour
{
    [SerializeField] private SkiController skiController;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Behaviour[] behavioursToDisableAfterInit;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float spawnHeightAboveGround = 1.25f;
    [SerializeField] private float settleSeconds = 0.75f;
    [SerializeField] private float maxGroundSnapDistance = 6f;
    [SerializeField] private Vector2 proneRollRange = new Vector2(70f, 110f);
    [SerializeField] private bool freezeRotationAfterSettle = true;

    public void InitializeAt(Vector3 groundPoint, Vector3 groundNormal)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 spawnPos = groundPoint + groundNormal.normalized * spawnHeightAboveGround;
        transform.position = spawnPos;

        Vector3 forward = Vector3.ProjectOnPlane(Random.insideUnitSphere, groundNormal);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(groundNormal, Vector3.right);

        forward.Normalize();

        Quaternion align = Quaternion.LookRotation(forward, groundNormal.normalized);
        float roll = Random.Range(proneRollRange.x, proneRollRange.y);
        transform.rotation = align * Quaternion.AngleAxis(roll, Vector3.forward);

        StopAllCoroutines();
        StartCoroutine(SettleThenDisable());
    }

    private IEnumerator SettleThenDisable()
    {
        yield return null;
        yield return new WaitForSeconds(settleSeconds);

        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, maxGroundSnapDistance, groundMask, QueryTriggerInteraction.Ignore))
            transform.position = hit.point + hit.normal * 0.05f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (freezeRotationAfterSettle)
                rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (skiController != null)
            skiController.enabled = false;

        if (behavioursToDisableAfterInit != null)
        {
            for (int i = 0; i < behavioursToDisableAfterInit.Length; i++)
            {
                if (behavioursToDisableAfterInit[i] != null)
                    behavioursToDisableAfterInit[i].enabled = false;
            }
        }
    }

    public void PrepareForTransport()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        StopAllCoroutines();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (skiController != null)
            skiController.enabled = false;
    }
}