using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LiftStationTerrainSnap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional. If assigned, this point is treated as the station base that should sit on terrain.")]
    [SerializeField] private Transform basePoint;

    [Header("Terrain Snap")]
    [SerializeField] private LayerMask terrainLayers = ~0;
    [SerializeField] private float raycastStartHeight = 50f;
    [SerializeField] private float raycastMaxDistance = 200f;
    [SerializeField] private float baseOffsetAboveGround = 0f;
    [SerializeField] private bool snapInEditMode = true;
    [SerializeField] private bool snapOnPlayStart = true;

    [Header("Orientation")]
    [SerializeField] private bool alignToTerrainNormal = false;
    [SerializeField, Range(0f, 45f)] private float maxTiltAngle = 10f;

    private void Reset()
    {
        basePoint = transform;
    }

    private void Awake()
    {
        if (Application.isPlaying && snapOnPlayStart)
            SnapToTerrain();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && snapInEditMode)
            SnapToTerrain();
    }
#endif

    [ContextMenu("Snap To Terrain")]
    public void SnapToTerrain()
    {
        Transform baseTf = basePoint != null ? basePoint : transform;

        Vector3 origin = baseTf.position + Vector3.up * raycastStartHeight;
        float distance = raycastStartHeight + raycastMaxDistance;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, terrainLayers, QueryTriggerInteraction.Ignore))
            return;

        float deltaY = (hit.point.y + baseOffsetAboveGround) - baseTf.position.y;
        if (Mathf.Abs(deltaY) > 0.001f)
            transform.position += Vector3.up * deltaY;

        if (alignToTerrainNormal)
        {
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            if (projectedForward.sqrMagnitude < 0.0001f)
                projectedForward = Vector3.ProjectOnPlane(transform.up, hit.normal).normalized;

            Quaternion target = Quaternion.LookRotation(projectedForward, hit.normal);

            Vector3 axis;
            float angle;
            Quaternion.FromToRotation(Vector3.up, hit.normal).ToAngleAxis(out angle, out axis);
            angle = Mathf.Min(angle, maxTiltAngle);

            Quaternion limitedTilt = Quaternion.AngleAxis(angle, axis.normalized);
            Quaternion yawOnly = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized, Vector3.up);

            Quaternion desiredRotation = yawOnly * limitedTilt;
            if (Quaternion.Angle(transform.rotation, desiredRotation) > 0.05f)
                transform.rotation = desiredRotation;
        }
    }
}
