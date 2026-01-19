using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Generates a lightweight physics proxy (pooled CapsuleColliders) along the LiftLine analytic band.
/// This makes the cable collide-able even when the visual is only a LineRenderer.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(LiftLine))]
public class LiftRopeColliderProxy : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Lift line providing the analytic band. If null, auto-resolves from this GameObject.")]
    public LiftLine line;

    [Tooltip("Optional parent for generated colliders. If null, an internal child root is created.")]
    public Transform colliderRoot;

    [Header("Collider Shape")]
    [Tooltip("Approximate max distance between collider segments along the band (world units). Smaller = smoother, more colliders.")]
    [Min(0.25f)] public float maxSegmentLength = 2.0f;

    [Tooltip("Cable collision radius (world units).")]
    [Min(0.001f)] public float ropeRadius = 0.06f;

    [Tooltip("If true, colliders are triggers (good for 'grind detection' without physical bump). If false, they physically collide.")]
    public bool isTrigger = false;

    [Tooltip("Optional PhysicMaterial for the rope proxy colliders.")]
    public PhysicsMaterial physicsMaterial;

    [Header("Layering")]
    [Tooltip("If set, all generated colliders will be forced to this layer. Use this to control collision matrix.")]
    public bool overrideLayer = false;

    [Tooltip("Layer to assign when Override Layer is enabled.")]
    public int layer = 0;

    [Header("Rebuild Behavior")]
    [Tooltip("Rebuild colliders continuously in edit mode (useful while moving stations).")]
    public bool rebuildInEditMode = true;

    [Tooltip("Rebuild colliders continuously in play mode (usually false; enable only if stations move at runtime).")]
    public bool rebuildInPlayMode = false;

    [Tooltip("Show the generated collider segments as gizmos.")]
    public bool drawGizmos = false;

    private readonly List<CapsuleCollider> _pool = new List<CapsuleCollider>(256);

    private void Reset()
    {
        line = GetComponent<LiftLine>();
    }

    private void OnEnable()
    {
        EnsureRefs();

        if (CanModifyHierarchyNow())
            Rebuild();
    }

    private void OnValidate()
    {
        EnsureRefs();

        // Avoid modifying prefab assets in Project view; only rebuild when it's safe.
        if (CanModifyHierarchyNow())
            Rebuild();
    }

    private void Update()
    {
        if (!EnsureRefs())
            return;

        if (!CanModifyHierarchyNow())
            return;

        if (!Application.isPlaying)
        {
            if (rebuildInEditMode)
                Rebuild();
        }
        else
        {
            if (rebuildInPlayMode)
                Rebuild();
        }

    }

    private bool CanModifyHierarchyNow()
    {
        // Unity "fake null" safety
        if (this == null || gameObject == null) return false;

#if UNITY_EDITOR
        // If this component exists on a Prefab Asset in the Project window,
        // Unity forbids parenting/creating children to prevent asset corruption.
        // Allow modifications only if we are editing the prefab in Prefab Mode.
        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(gameObject))
                return true;

            return false;
        }
#endif

        return true;
    }

    private bool EnsureRefs()
    {
        if (line == null)
            line = GetComponent<LiftLine>();

        if (line == null)
            return false;

        if (colliderRoot == null)
        {
            Transform existing = transform.Find("RopeColliderRoot");
            if (existing != null) colliderRoot = existing;
            else
            {
                // If Unity is validating a Prefab Asset, do not create children.
                if (!CanModifyHierarchyNow())
                    return false;

                GameObject root = new GameObject("RopeColliderRoot");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                colliderRoot = root.transform;
            }
        }

        return true;
    }

    public void Rebuild()
    {
        if (!EnsureRefs())
            return;

        // Ensure analytic path is current (matches how LiftRopeVisual rebuilds).【LiftRopeVisual calls RebuildAnalyticLoop + BandLength】
        line.RebuildAnalyticLoop();

        float total = line.BandLength;
        if (total <= 0.0001f)
        {
            DisableAll();
            return;
        }

        float segLen = Mathf.Max(0.25f, maxSegmentLength);
        int segmentCount = Mathf.Max(4, Mathf.CeilToInt(total / segLen));

        // We need one capsule per segment (i -> i+1). Loop is implicitly closed by LiftLine.
        EnsurePoolSize(segmentCount);

        float step = total / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float d0 = i * step;
            float d1 = (i + 1) * step;

            Vector3 p0 = line.GetBandPosition(d0);
            Vector3 p1 = line.GetBandPosition(d1);

            SetupCapsule(_pool[i], p0, p1);
        }

        // Disable unused pooled colliders
        for (int i = segmentCount; i < _pool.Count; i++)
        {
            if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    private void EnsurePoolSize(int needed)
    {
        while (_pool.Count < needed)
        {
            GameObject go = new GameObject($"RopeCol_{_pool.Count:D3}");
            go.transform.SetParent(colliderRoot, false);

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();

            // We'll align segments along local Z axis.
            col.direction = 2;
            col.center = Vector3.zero;

            _pool.Add(col);
        }
    }

    private void SetupCapsule(CapsuleCollider col, Vector3 p0, Vector3 p1)
    {
        if (col == null) return;

        GameObject go = col.gameObject;
        if (!go.activeSelf) go.SetActive(true);

        if (overrideLayer)
            go.layer = layer;

        col.isTrigger = isTrigger;
        col.sharedMaterial = physicsMaterial;

        Vector3 delta = p1 - p0;
        float len = delta.magnitude;

        if (len < 0.0005f)
        {
            // Degenerate segment: make a small capsule.
            col.radius = ropeRadius;
            col.height = Mathf.Max(ropeRadius * 2f, ropeRadius * 2f);
            go.transform.position = p0;
            go.transform.rotation = Quaternion.identity;
            return;
        }

        Vector3 mid = (p0 + p1) * 0.5f;
        Vector3 dir = delta / len;

        // Orient local Z to segment direction.
        go.transform.position = mid;
        go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        col.radius = ropeRadius;
        // Capsule height includes hemispheres; give it len + 2r so it covers endpoints cleanly.
        col.height = Mathf.Max(ropeRadius * 2f, len + ropeRadius * 2f);
    }

    private void DisableAll()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || _pool == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < _pool.Count; i++)
        {
            CapsuleCollider c = _pool[i];
            if (c == null || !c.gameObject.activeInHierarchy) continue;

            Gizmos.matrix = c.transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, c.radius);
        }
    }
}
