using UnityEngine;

/// <summary>
/// Purely visual rope between a cable anchor and a hanger (chair/t-bar assembly).
/// Deterministic (no physics), updates every frame to match LiftCarrierHanger.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class LiftHangerRopeVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional: if assigned, we'll use this as the anchor point. Otherwise we use LiftCarrierHanger.cableAnchor.")]
    public Transform anchorPoint;

    [Tooltip("Optional: if assigned, we'll use this as the hanger point. Otherwise we use LiftCarrierHanger.hanger.")]
    public Transform hangerPoint;

    [Tooltip("If present on the same object, we can auto-wire anchor/hanger from it.")]
    public LiftCarrierHanger hangerDriver;

    [Tooltip("LineRenderer used to draw the rope. If null, one will be created as a child.")]
    public LineRenderer lineRenderer;

    [Header("Visual")]
    public float ropeWidth = 0.04f;
    public Material ropeMaterial;

    [Tooltip("Number of segments for a slight curve (1 = straight line).")]
    [Range(1, 24)]
    public int segments = 8;

    [Tooltip("How much the rope sags downward in the middle (0 = straight).")]
    [Range(0f, 2f)]
    public float sagAmount = 0.15f;

    private void Reset()
    {
        hangerDriver = GetComponent<LiftCarrierHanger>();
        lineRenderer = GetComponentInChildren<LineRenderer>();
    }

    private void OnEnable()
    {
        EnsureSetup();
        UpdateLine();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        EnsureSetup();
        UpdateLine();
    }

    private void LateUpdate()
    {
        // LateUpdate so it follows LiftCarrierHanger's LateUpdate motion.
        EnsureSetup();
        UpdateLine();
    }

    private void EnsureSetup()
    {
        if (!hangerDriver) hangerDriver = GetComponent<LiftCarrierHanger>();

        if (!anchorPoint && hangerDriver && hangerDriver.cableAnchor)
            anchorPoint = hangerDriver.cableAnchor;

        if (!hangerPoint && hangerDriver && hangerDriver.hanger)
            hangerPoint = hangerDriver.hanger;

        if (!lineRenderer)
        {
            lineRenderer = GetComponentInChildren<LineRenderer>();
            if (!lineRenderer)
            {
                var go = new GameObject("HangerRopeLine");
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                lineRenderer = go.AddComponent<LineRenderer>();
            }
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = Mathf.Max(0.0001f, ropeWidth);

        if (ropeMaterial && lineRenderer.sharedMaterial != ropeMaterial)
            lineRenderer.sharedMaterial = ropeMaterial;
    }

    private void UpdateLine()
    {
        if (!lineRenderer || !anchorPoint || !hangerPoint)
        {
            if (lineRenderer) lineRenderer.positionCount = 0;
            return;
        }

        int seg = Mathf.Max(1, segments);
        int count = seg + 1;
        lineRenderer.positionCount = count;

        Vector3 a = anchorPoint.position;
        Vector3 b = hangerPoint.position;

        // Simple sag curve: interpolate along the rope and add a downward offset peaking at the middle.
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / seg;
            Vector3 p = Vector3.Lerp(a, b, t);

            if (sagAmount > 0f && seg > 1)
            {
                // 0 at ends, 1 at middle
                float mid = 1f - Mathf.Abs(2f * t - 1f);
                p += Vector3.down * (sagAmount * mid);
            }

            lineRenderer.SetPosition(i, p);
        }
    }
}
