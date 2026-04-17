using UnityEngine;

[DisallowMultipleComponent]
public class LiftSupportTower : MonoBehaviour
{
    [Header("Guide Geometry")]
    [Tooltip("Optional explicit guide root. If null, this transform is used.")]
    public Transform guideRoot;

    [Tooltip("Height from the tower root to the cable guide point.")]
    public float guideHeight = 8f;

    [Tooltip("Distance between uphill and downhill guide points.")]
    public float crossarmWidth = 5f;

    [Tooltip("Optional extra vertical offset for the uphill side.")]
    public float uphillAdditionalVerticalOffset = 0f;

    [Tooltip("Optional extra vertical offset for the downhill side.")]
    public float downhillAdditionalVerticalOffset = 0f;

    [HideInInspector] public bool generatedByLiftLine = true;

    public Vector3 GetGuidePoint(Vector3 lineRight, bool uphillSide, float ropeClearance, float sharedVerticalOffset)
    {
        Transform root = guideRoot != null ? guideRoot : transform;

        Vector3 basePos = root.position + Vector3.up * guideHeight;
        float sideSign = uphillSide ? 1f : -1f;
        float sideVertical = uphillSide ? uphillAdditionalVerticalOffset : downhillAdditionalVerticalOffset;

        return basePos
             + lineRight.normalized * ((crossarmWidth * 0.5f) + ropeClearance) * sideSign
             + Vector3.up * (sharedVerticalOffset + sideVertical);
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = guideRoot != null ? guideRoot : transform;

        Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.9f);
        Vector3 top = root.position + Vector3.up * guideHeight;
        Gizmos.DrawLine(root.position, top);

        Vector3 right = transform.right;
        Vector3 a = top + right * (crossarmWidth * 0.5f);
        Vector3 b = top - right * (crossarmWidth * 0.5f);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.2f);
        Gizmos.DrawWireSphere(b, 0.2f);
    }
}