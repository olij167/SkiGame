using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcRescueCasualtyState : MonoBehaviour
{
    [SerializeField] private SkiController skiController;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Behaviour[] behavioursToDisableAfterInit;
    [SerializeField] private float spawnHeightAboveGround = 1.5f;
    [SerializeField] private float settleSeconds = 0.75f;
    [SerializeField] private Vector2 proneRollRange = new Vector2(70f, 110f);

    public void InitializeAt(Vector3 groundPoint, Vector3 groundNormal)
    {
        Vector3 spawnPos = groundPoint + groundNormal.normalized * spawnHeightAboveGround;
        transform.position = spawnPos;

        Vector3 forward = Vector3.ProjectOnPlane(Random.insideUnitSphere, groundNormal);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(groundNormal, Vector3.right);
        forward.Normalize();

        Quaternion align = Quaternion.LookRotation(forward, groundNormal.normalized);
        float roll = Random.Range(proneRollRange.x, proneRollRange.y);
        transform.rotation = align * Quaternion.AngleAxis(roll, Vector3.forward);

        StartCoroutine(SettleThenDisable());
    }

    private IEnumerator SettleThenDisable()
    {
        yield return null; // let appearance init run first
        yield return new WaitForSeconds(settleSeconds);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
}