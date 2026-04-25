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
//[ExecuteAlways]
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

    [Tooltip("Hard cap on generated collider count to prevent accidental stalls.")]
    [Min(16)]
    public int maxSegmentCount = 2048;

    [Header("Rebuild Behavior")]
    [Tooltip("Rebuild colliders continuously in edit mode (useful while moving stations).")]
    public bool rebuildInEditMode = true;

    [Tooltip("Rebuild colliders continuously in play mode (usually false; enable only if stations move at runtime).")]
    public bool rebuildInPlayMode = false;

    [Tooltip("If true, forces LiftLine.RebuildAnalyticLoop() every time proxy rebuilds. If false, only rebuilds when line/stations/config changed.")]
    public bool forceAnalyticRebuild = false;

    [Tooltip("Show the generated collider segments as gizmos.")]
    public bool drawGizmos = false;

    private readonly List<CapsuleCollider> _pool = new List<CapsuleCollider>(256);

    // --- Perf / stability caches (prevents rebuild spam + prevents duplicate child creation on domain reload) ---
    private bool _poolSynced;
    private Vector3 _lastBottomPos;
    private Vector3 _lastTopPos;
    private float _lastMaxSegmentLength;
    private float _lastRopeRadius;
    private bool _lastIsTrigger;
    private PhysicsMaterial _lastPhysMat;
    private bool _lastOverrideLayer;
    private int _lastLayer;

#if UNITY_EDITOR
    private bool _pendingEditorRebuild;
    private double _nextEditorRebuildTime;
    [SerializeField] private float editorRebuildIntervalSeconds = 0.25f;
#endif

    private void SyncPoolFromChildren()
    {
        _pool.Clear();

        if (colliderRoot == null)
        {
            _poolSynced = true;
            return;
        }

        // Reuse any existing CapsuleColliders under the root (including inactive children).
        for (int i = 0; i < colliderRoot.childCount; i++)
        {
            Transform child = colliderRoot.GetChild(i);
            if (child == null) continue;

            CapsuleCollider col = child.GetComponent<CapsuleCollider>();
            if (col != null)
                _pool.Add(col);
        }

        _poolSynced = true;
    }

    private bool HasConfigOrStationsChanged()
    {
        if (line == null) return true;

        // If LiftLine doesn't expose stations, we can only rely on config changes.
        // (If it does, this becomes a very strong “dirty” check.)
        Transform bottom = line.bottomStation;
        Transform top = line.topStation;

        if (bottom != null && top != null)
        {
            if ((_lastBottomPos - bottom.position).sqrMagnitude > 0.000001f) return true;
            if ((_lastTopPos - top.position).sqrMagnitude > 0.000001f) return true;
        }

        if (Mathf.Abs(_lastMaxSegmentLength - maxSegmentLength) > 0.0001f) return true;
        if (Mathf.Abs(_lastRopeRadius - ropeRadius) > 0.000001f) return true;

        if (_lastIsTrigger != isTrigger) return true;
        if (_lastPhysMat != physicsMaterial) return true;

        if (_lastOverrideLayer != overrideLayer) return true;
        if (_lastLayer != layer) return true;

        return false;
    }

    private void CommitDirtySnapshot()
    {
        if (line != null)
        {
            Transform bottom = line.bottomStation;
            Transform top = line.topStation;

            if (bottom != null) _lastBottomPos = bottom.position;
            if (top != null) _lastTopPos = top.position;
        }

        _lastMaxSegmentLength = maxSegmentLength;
        _lastRopeRadius = ropeRadius;
        _lastIsTrigger = isTrigger;
        _lastPhysMat = physicsMaterial;
        _lastOverrideLayer = overrideLayer;
        _lastLayer = layer;
    }

    private void Reset()
    {
        line = GetComponent<LiftLine>();
    }

    private void OnEnable()
    {
        SyncPoolFromChildren();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (rebuildInEditMode)
            {
                _pendingEditorRebuild = true;
                _nextEditorRebuildTime = UnityEditor.EditorApplication.timeSinceStartup; // earliest allowed
            }
            return;
        }
#endif

        if (rebuildInPlayMode)
            Rebuild();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (rebuildInEditMode)
            {
                _pendingEditorRebuild = true;
                _nextEditorRebuildTime = UnityEditor.EditorApplication.timeSinceStartup;
            }
            return;
        }
#endif

        if (rebuildInPlayMode)
            Rebuild();
    }

    private void Update()
    {
        // If we’re not allowed to rebuild in this mode, do nothing.
        bool wantsRebuild =
            (!Application.isPlaying && rebuildInEditMode) ||
            (Application.isPlaying && rebuildInPlayMode);

        if (!wantsRebuild)
            return;

#if UNITY_EDITOR
        // Edit mode: keep your throttle behavior, but don’t do any work unless pending or dirty.
        if (!Application.isPlaying)
        {
            // Mark pending if dirty (cheap check).
            if (!_pendingEditorRebuild && HasConfigOrStationsChanged())
            {
                _pendingEditorRebuild = true;
                _nextEditorRebuildTime = UnityEditor.EditorApplication.timeSinceStartup; // earliest allowed
            }

            if (_pendingEditorRebuild)
            {
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (now >= _nextEditorRebuildTime)
                {
                    _pendingEditorRebuild = false;
                    _nextEditorRebuildTime = now + editorRebuildIntervalSeconds;

                    if (!EnsureRefs() || !CanModifyHierarchyNow())
                        return;

                    if (!_poolSynced)
                        SyncPoolFromChildren();

                    Rebuild();
                }
            }

            return;
        }
#endif

        // Play mode: only rebuild if dirty.
        if (!HasConfigOrStationsChanged())
            return;

        if (!EnsureRefs() || !CanModifyHierarchyNow())
            return;

        if (!_poolSynced)
            SyncPoolFromChildren();

        Rebuild();
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

        // Only rebuild the analytic loop when needed (saves a lot in edit mode).
        if (forceAnalyticRebuild || HasConfigOrStationsChanged())
            line.RebuildAnalyticLoop();

        float total = line.BandLength;
        if (total <= 0.0001f)
        {
            DisableAll();
            return;
        }

        float segLen = Mathf.Max(0.25f, maxSegmentLength);
        int segmentCount = Mathf.Max(4, Mathf.CeilToInt(total / segLen));
        if (segmentCount > maxSegmentCount)
            segmentCount = maxSegmentCount;

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

        CommitDirtySnapshot();

    }

    private void EnsurePoolSize(int needed)
    {
        // If we domain-reloaded, _pool is empty even though child objects still exist.
        if (!_poolSynced)
            SyncPoolFromChildren();

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

        if (overrideLayer && go.layer != layer)
            go.layer = layer;

        if (col.isTrigger != isTrigger)
            col.isTrigger = isTrigger;

        if (col.sharedMaterial != physicsMaterial)
            col.sharedMaterial = physicsMaterial;

        Vector3 delta = p1 - p0;
        float sqrLen = delta.sqrMagnitude;

        // Always ensure radius; it affects height too.
        if (!Mathf.Approximately(col.radius, ropeRadius))
            col.radius = ropeRadius;

        if (sqrLen < 0.00000025f) // ~0.0005^2
        {
            float h = Mathf.Max(ropeRadius * 2f, ropeRadius * 2f);
            if (!Mathf.Approximately(col.height, h))
                col.height = h;

            // Only write transform if changed significantly
            if ((go.transform.position - p0).sqrMagnitude > 0.000001f)
                go.transform.position = p0;

            // Don’t spam rotation changes for degenerate segments
            return;
        }

        float len = Mathf.Sqrt(sqrLen);
        Vector3 mid = (p0 + p1) * 0.5f;
        Vector3 dir = delta / len;

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        // One combined call is marginally cheaper than setting both separately.
        go.transform.SetPositionAndRotation(mid, rot);

        float targetHeight = Mathf.Max(ropeRadius * 2f, len + ropeRadius * 2f);
        if (!Mathf.Approximately(col.height, targetHeight))
            col.height = targetHeight;
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
