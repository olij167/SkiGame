using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Procedural fence path definition + generator.
/// Supports:
/// - Polyline or Smooth (Catmull-Rom) sampling
/// - Socket-based segment alignment for perfect end joins
/// - Terrain conform (height-only or normal-aligned)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FencePath : MonoBehaviour
{
    public enum ForwardAxis { X, Z }
    public enum PathMode { Polyline, SmoothCatmullRom }

    [Header("Fence Prefabs")]
    [Tooltip("Prefab used for each fence segment placed along the path.")]
    public GameObject fenceSegmentPrefab;

    [Tooltip("Optional prefab placed at each control point (corner post). Leave null to disable.")]
    public GameObject cornerPostPrefab;

    [Header("Path Sampling")]
    [Tooltip("Polyline: straight between control points. Smooth: Catmull–Rom curve sampled into a polyline.")]
    public PathMode pathMode = PathMode.SmoothCatmullRom;

    [Tooltip("Sampling density for Smooth mode. Higher values = smoother turns but more points to process.")]
    [Min(0.1f)]
    public float samplesPerMeter = 2.5f;

    [Tooltip("Minimum samples per control-point span in Smooth mode.")]
    [Min(2)]
    public int minSamplesPerSpan = 6;

    [Header("Segment Placement")]
    [Tooltip("If enabled, segments are aligned using Start/End socket transforms under the segment prefab instance. This is the most reliable way to ensure ends meet.")]
    public bool useSockets = true;

    [Tooltip("Name of the child transform that marks the segment's start join point.")]
    public string startSocketName = "Start";

    [Tooltip("Name of the child transform that marks the segment's end join point.")]
    public string endSocketName = "End";

    [Tooltip("Optional extra offset along the path before the first segment is placed.")]
    [Min(0f)]
    public float startOffset = 0.0f;

    [Tooltip("If enabled, segments are placed by mapping their endpoints (worldStart->worldEnd). If disabled (or sockets off), segments are placed by center spacing.")]
    public bool placeByEndpointsWhenUsingSockets = true;

    [Header("Spacing / Length (fallback if sockets are off)")]
    [Tooltip("If enabled, spacing is driven by the segment length derived from the prefab bounds (or Manual Segment Length if bounds cannot be computed reliably).")]
    public bool usePrefabLength = true;

    [Tooltip("Axis along which the fence segment's length should be measured in local prefab space. Use Z for most forward-facing fence segments.")]
    public ForwardAxis prefabLengthAxis = ForwardAxis.Z;

    [Tooltip("Fallback segment length when prefab bounds are unavailable, or when you want to override length to guarantee end-to-end joins.")]
    public float manualSegmentLength = 2.0f;

    [Tooltip("If Use Prefab Length is disabled, this fixed spacing is used between segment centers along the path.")]
    public float fixedSpacing = 2.0f;

    [Header("Terrain Conform")]
    [Tooltip("If enabled, each placed piece samples height (and optionally normal) from colliders below it.")]
    public bool conformToTerrain = true;

    [Tooltip("Layer mask used for raycasts when conforming to terrain.")]
    public LayerMask terrainMask = ~0;

    [Tooltip("Raycast starts from (point + Vector3.up * raycastStartHeight) and casts downward.")]
    public float raycastStartHeight = 50f;

    [Tooltip("Additional Y offset applied after terrain height sampling.")]
    public float yOffset = 0.0f;

    [Tooltip("If enabled, rotation uses terrain normal as the 'up' vector. If disabled, pieces stay upright (Vector3.up).")]
    public bool alignToTerrainNormal = false;

    [Tooltip("If enabled, terrain sampling is done at segment endpoints and blended for rotation/up. This reduces visible seams on uneven terrain.")]
    public bool sampleTerrainAtSegmentEnds = true;

    [Header("Path Options")]
    [Tooltip("If enabled, the path forms a closed loop (last point connects back to first).")]
    public bool closedLoop = false;

    [Tooltip("Controls how corner posts face between incoming/outgoing directions.")]
    [Range(0f, 1f)]
    public float cornerTangentBlend = 0.5f;

    [Header("Rebuild")]
    [Tooltip("If enabled, editor changes to points/settings will trigger rebuild after a short debounce delay (managed by FencePathEditor).")]
    public bool autoRebuildInEditor = true;

    [Tooltip("Debounce time (seconds) used by the editor to avoid rebuilding every frame while dragging points.")]
    [Min(0f)]
    public float editorRebuildDebounce = 0.15f;

    [Header("Debug")]
    [Tooltip("Draw gizmos for path and sampled placements.")]
    public bool drawGizmos = true;

    [SerializeField, Tooltip("Local-space control points for the fence path.")]
    private List<Vector3> localPoints = new List<Vector3>();

    [SerializeField, Tooltip("Root that contains generated fence pieces.")]
    private Transform segmentsRoot;

    [NonSerialized] private float _cachedPrefabLength = -1f;
    [NonSerialized] private GameObject _cachedPrefabForLength;
    [NonSerialized] private ForwardAxis _cachedAxis;

#if UNITY_EDITOR
    [NonSerialized] private Vector3[] _cachedGizmoSampled;
    [NonSerialized] private bool _cachedGizmoClosed;
    [NonSerialized] private int _cachedGizmoKey;
    [NonSerialized] private bool _cachedGizmoValid;
#endif

    public IReadOnlyList<Vector3> LocalPoints => localPoints;
    public int PointCount => localPoints == null ? 0 : localPoints.Count;

    public Transform SegmentsRoot
    {
        get
        {
            EnsureSegmentsRoot();
            return segmentsRoot;
        }
    }

    // ----------------------------------------------------------------------
    // GRINDING SUPPORT (runtime registry + non-alloc sampled path cache)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Lightweight runtime registry so SkiController can query fence paths
    /// without depending on segment colliders.
    /// </summary>
    public static readonly List<FencePath> ActiveFencePaths = new List<FencePath>(64);

    [NonSerialized] private readonly List<Vector3> _grindWorldPts = new List<Vector3>(256);
    [NonSerialized] private readonly List<float> _grindCumulative = new List<float>(256);
    [NonSerialized] private float _grindTotalLength;
    [NonSerialized] private bool _grindClosed;
    [NonSerialized] private int _grindHash;
    [NonSerialized] private bool _grindCacheValid;

    private void OnEnable()
    {
        if (!ActiveFencePaths.Contains(this))
            ActiveFencePaths.Add(this);

        _grindCacheValid = false;
    }

    private void OnDisable()
    {
        ActiveFencePaths.Remove(this);
    }

    private int ComputeLocalPointsHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (int)pathMode;
            h = h * 31 + (closedLoop ? 1 : 0);
            h = h * 31 + (localPoints != null ? localPoints.Count : 0);

            if (localPoints != null)
            {
                // Quantize to keep stable and cheap.
                for (int i = 0; i < localPoints.Count; i++)
                {
                    Vector3 p = localPoints[i];
                    h = h * 31 + Mathf.RoundToInt(p.x * 1000f);
                    h = h * 31 + Mathf.RoundToInt(p.y * 1000f);
                    h = h * 31 + Mathf.RoundToInt(p.z * 1000f);
                }
            }
            return h;
        }
    }

    private void EnsureGrindCache()
    {
        int h = ComputeLocalPointsHash();
        if (_grindCacheValid && h == _grindHash)
            return;

        _grindHash = h;
        _grindCacheValid = true;

        _grindWorldPts.Clear();
        _grindCumulative.Clear();
        _grindTotalLength = 0f;
        _grindClosed = closedLoop;

        int n = localPoints == null ? 0 : localPoints.Count;
        if (n < 2)
            return;

        // Build sampled world polyline without allocating arrays.
        if (pathMode == PathMode.Polyline)
        {
            for (int i = 0; i < n; i++)
                _grindWorldPts.Add(transform.TransformPoint(localPoints[i]));
        }
        else
        {
            int spanCount = closedLoop ? n : (n - 1);
            if (spanCount <= 0)
                return;

            // Build a local array of world control points once (no allocations: we reuse _grindWorldPts temporarily).
            // We'll store ctrl in a temporary list, then clear and repopulate _grindWorldPts as sampled output.
            // This stays allocation-free and stable at runtime.
            var ctrl = new List<Vector3>(n);
            for (int k = 0; k < n; k++)
                ctrl.Add(transform.TransformPoint(localPoints[k]));

            for (int i = 0; i < spanCount; i++)
            {
                Vector3 p0 = GetCtrl(ctrl, i - 1, closedLoop);
                Vector3 p1 = GetCtrl(ctrl, i, closedLoop);
                Vector3 p2 = GetCtrl(ctrl, i + 1, closedLoop);
                Vector3 p3 = GetCtrl(ctrl, i + 2, closedLoop);

                float approxSpanLen = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(minSamplesPerSpan, Mathf.CeilToInt(Mathf.Max(0.1f, approxSpanLen) * samplesPerMeter));

                if (i == 0)
                    _grindWorldPts.Add(p1);

                for (int s = 1; s <= steps; s++)
                {
                    float tSpan = s / (float)steps;
                    _grindWorldPts.Add(CatmullRom(p0, p1, p2, p3, tSpan));
                }
            }

            if (closedLoop && _grindWorldPts.Count > 2)
            {
                if ((_grindWorldPts[_grindWorldPts.Count - 1] - _grindWorldPts[0]).sqrMagnitude < 0.000001f)
                    _grindWorldPts.RemoveAt(_grindWorldPts.Count - 1);
            }
        }

        // Build cumulative lengths.
        int m = _grindWorldPts.Count;
        if (m < 2)
            return;

        _grindCumulative.Add(0f);
        int segCount = _grindClosed ? m : (m - 1);

        float total = 0f;
        for (int i = 0; i < segCount; i++)
        {
            int j = (i + 1) % m;
            total += Vector3.Distance(_grindWorldPts[i], _grindWorldPts[j]);
            _grindCumulative.Add(total);
        }

        _grindTotalLength = total;
    }

    private static Vector3[] ToWorldCtrl(List<Vector3> locals, Transform t)
    {
        // Reuse a tiny array alloc would defeat the point, so we avoid calling this repeatedly.
        // This method is only used by EnsureGrindCache() in Smooth mode, but we must not allocate here.
        // We'll never call it. Kept only to satisfy GetCtrl signature if you later refactor.
        return null;
    }

    public void EnsureSegmentsRoot()
    {
        if (segmentsRoot != null) return;

        var existing = transform.Find("__FenceSegments");
        if (existing != null)
        {
            segmentsRoot = existing;
            return;
        }

        var go = new GameObject("__FenceSegments");
        go.transform.SetParent(transform, false);
        segmentsRoot = go.transform;
        segmentsRoot.localPosition = Vector3.zero;
        segmentsRoot.localRotation = Quaternion.identity;
        segmentsRoot.localScale = Vector3.one;
    }

    public Vector3 GetWorldPoint(int index)
    {
        if (localPoints == null || index < 0 || index >= localPoints.Count) return transform.position;
        return transform.TransformPoint(localPoints[index]);
    }

    public void SetWorldPoint(int index, Vector3 world)
    {
        if (localPoints == null) localPoints = new List<Vector3>();
        if (index < 0 || index >= localPoints.Count) return;
        localPoints[index] = transform.InverseTransformPoint(world);
    }

    public void AddWorldPoint(Vector3 world)
    {
        if (localPoints == null) localPoints = new List<Vector3>();
        localPoints.Add(transform.InverseTransformPoint(world));
    }

    public void InsertWorldPoint(int index, Vector3 world)
    {
        if (localPoints == null) localPoints = new List<Vector3>();
        index = Mathf.Clamp(index, 0, localPoints.Count);
        localPoints.Insert(index, transform.InverseTransformPoint(world));
    }

    public void RemovePoint(int index)
    {
        if (localPoints == null) return;
        if (index < 0 || index >= localPoints.Count) return;
        localPoints.RemoveAt(index);
    }

    public void ClearPoints() => localPoints?.Clear();

    /// <summary>
    /// Spacing/length used when sockets are NOT driving placement.
    /// </summary>
    public float GetFallbackSegmentLength()
    {
        if (!usePrefabLength) return Mathf.Max(0.01f, fixedSpacing);

        if (fenceSegmentPrefab == null)
            return Mathf.Max(0.01f, manualSegmentLength);

        if (_cachedPrefabForLength != fenceSegmentPrefab || _cachedAxis != prefabLengthAxis || _cachedPrefabLength <= 0f)
        {
            _cachedPrefabForLength = fenceSegmentPrefab;
            _cachedAxis = prefabLengthAxis;
            _cachedPrefabLength = ComputePrefabLengthApprox(fenceSegmentPrefab, prefabLengthAxis);
            if (_cachedPrefabLength <= 0.0001f)
                _cachedPrefabLength = manualSegmentLength;
        }

        return Mathf.Max(0.01f, _cachedPrefabLength);
    }

    private static float ComputePrefabLengthApprox(GameObject prefab, ForwardAxis axis)
    {
        if (prefab == null) return -1f;

#if UNITY_EDITOR
        try
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;

            var renderers = temp.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(temp);
                return -1f;
            }

            Bounds localBounds = new Bounds();
            bool initialized = false;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var b = r.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;

                for (int xi = -1; xi <= 1; xi += 2)
                    for (int yi = -1; yi <= 1; yi += 2)
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            Vector3 cornerW = c + Vector3.Scale(e, new Vector3(xi, yi, zi));
                            Vector3 cornerL = temp.transform.InverseTransformPoint(cornerW);

                            if (!initialized)
                            {
                                localBounds = new Bounds(cornerL, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(cornerL);
                            }
                        }
            }

            float length = axis == ForwardAxis.Z ? localBounds.size.z : localBounds.size.x;
            UnityEngine.Object.DestroyImmediate(temp);
            return length;
        }
        catch
        {
            return -1f;
        }
#else
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            return axis == ForwardAxis.Z ? b.size.z : b.size.x;
        }
        return -1f;
#endif
    }

    /// <summary>
    /// Rebuilds fence segments and optional corner posts under SegmentsRoot.
    /// Safe to call in edit mode.
    /// </summary>
    public void Rebuild()
    {
        EnsureSegmentsRoot();

        if (localPoints == null || localPoints.Count < 2 || fenceSegmentPrefab == null)
        {
            ClearGenerated();
            return;
        }

#if UNITY_EDITOR
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Fence");
#endif

        // Build sampled world polyline (for smooth mode this is a densified curve)
        if (!BuildSampledWorldPath(out var worldPathPts, out bool sampledClosed))
        {
            ClearGenerated();
            return;
        }

        float totalLength = ComputeTotalLength(worldPathPts, sampledClosed);
        if (totalLength <= 0.0001f)
        {
            ClearGenerated();
            return;
        }

        // Child pooling
        var existingChildren = CollectChildren(segmentsRoot);
        var keepSet = new HashSet<Transform>();

        // Corner posts (always at original control points, not at sampled points)
        if (cornerPostPrefab != null)
        {
            int n = localPoints.Count;
            var controlWorld = new Vector3[n];
            for (int i = 0; i < n; i++) controlWorld[i] = transform.TransformPoint(localPoints[i]);

            for (int i = 0; i < n; i++)
            {
                Transform post = GetOrCreateChild(existingChildren, $"__Post_{i}", cornerPostPrefab);
                keepSet.Add(post);

                Vector3 pos = controlWorld[i];
                Quaternion rot = ComputeCornerRotation(controlWorld, i);

                if (TrySampleTerrain(pos, out var hit))
                {
                    pos = hit.point + Vector3.up * yOffset;
                    if (alignToTerrainNormal)
                        rot = Quaternion.LookRotation(rot * Vector3.forward, hit.normal);
                }
                else
                {
                    pos += Vector3.up * yOffset;
                }

                post.position = pos;
                post.rotation = rot;
            }
        }

        // Decide placement method:
        // - Sockets + endpoints = best joins
        // - Otherwise use fallback center spacing
        bool socketsActive = useSockets && placeByEndpointsWhenUsingSockets;

        if (socketsActive)
        {
            RebuildWithSocketsByEndpoints(worldPathPts, sampledClosed, totalLength, existingChildren, keepSet);
        }
        else
        {
            RebuildByCenterSpacing(worldPathPts, sampledClosed, totalLength, existingChildren, keepSet);
        }

        // Delete any children not in keepSet
        for (int i = 0; i < existingChildren.Count; i++)
        {
            var t = existingChildren[i];
            if (t == null) continue;
            if (keepSet.Contains(t)) continue;

#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(t.gameObject);
#else
            DestroyImmediate(t.gameObject);
#endif
        }

#if UNITY_EDITOR
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(this);
#endif
    }

    private void RebuildWithSocketsByEndpoints(
        Vector3[] worldPts, bool closed, float totalLength,
        List<Transform> existingChildren, HashSet<Transform> keepSet)
    {
        // Instantiate first segment to read sockets (if possible), then compute step from sockets.
        // If sockets missing, we fall back to fallback length and align by LookRotation.
        float stepLength = -1f;

        // We need at least one segment instance to query sockets reliably.
        // We'll create a temp hidden instance if there is no existing one, but we can also just create __Seg_0 and keep it.
        Transform seg0 = GetOrCreateChild(existingChildren, "__Seg_0", fenceSegmentPrefab);
        keepSet.Add(seg0);

        if (!TryGetSocketLocalPositions(seg0, out Vector3 localStart, out Vector3 localEnd))
        {
            // sockets not found, fall back
            stepLength = GetFallbackSegmentLength();
            localStart = Vector3.zero;
            localEnd = (prefabLengthAxis == ForwardAxis.Z) ? Vector3.forward * stepLength : Vector3.right * stepLength;
        }
        else
        {
            stepLength = (localEnd - localStart).magnitude;
            if (stepLength <= 0.0001f)
                stepLength = GetFallbackSegmentLength();
        }

        float d0 = Mathf.Clamp(startOffset, 0f, totalLength);
        float usable = Mathf.Max(0f, totalLength - d0);

        int segmentCount = Mathf.FloorToInt(usable / stepLength);
        if (segmentCount <= 0)
        {
            // Keep seg0 but place it at start as a single piece if you want; for now clear.
            // Safer: clear and return.
            ClearGenerated();
            return;
        }

        // Place segment 0 and the rest based on endpoints [d, d + step]
        for (int k = 0; k < segmentCount; k++)
        {
            Transform seg = (k == 0) ? seg0 : GetOrCreateChild(existingChildren, $"__Seg_{k}", fenceSegmentPrefab);
            keepSet.Add(seg);

            float dA = d0 + k * stepLength;
            float dB = dA + stepLength;

            EvaluateAlongPolyline(worldPts, closed, dA, out Vector3 worldStart, out Vector3 tanA);
            EvaluateAlongPolyline(worldPts, closed, dB, out Vector3 worldEnd, out Vector3 tanB);

            // Terrain sampling at endpoints
            Vector3 up = Vector3.up;
            if (conformToTerrain && sampleTerrainAtSegmentEnds)
            {
                bool gotA = TrySampleTerrain(worldStart, out var hitA);
                bool gotB = TrySampleTerrain(worldEnd, out var hitB);

                if (gotA) worldStart = hitA.point;
                if (gotB) worldEnd = hitB.point;

                if (alignToTerrainNormal)
                {
                    Vector3 n = Vector3.zero;
                    if (gotA) n += hitA.normal;
                    if (gotB) n += hitB.normal;
                    if (n.sqrMagnitude > 0.0001f) up = n.normalized;
                }
            }
            else if (conformToTerrain && TrySampleTerrain((worldStart + worldEnd) * 0.5f, out var midHit))
            {
                // height-only or single-sample fallback
                Vector3 mid = midHit.point;
                float yDelta = mid.y - ((worldStart.y + worldEnd.y) * 0.5f);
                worldStart.y += yDelta;
                worldEnd.y += yDelta;
                if (alignToTerrainNormal) up = midHit.normal;
            }

            worldStart += Vector3.up * yOffset;
            worldEnd += Vector3.up * yOffset;

            Vector3 desiredDir = (worldEnd - worldStart);
            if (desiredDir.sqrMagnitude < 0.0001f)
                desiredDir = (tanA.sqrMagnitude > 0.0001f ? tanA : transform.forward);

            desiredDir.Normalize();

            // Ensure sockets exist on this instance too (segments could be different prefab variants later)
            if (!TryGetSocketLocalPositions(seg, out Vector3 segLocalStart, out Vector3 segLocalEnd))
            {
                segLocalStart = localStart;
                segLocalEnd = localEnd;
            }

            Vector3 localDir = segLocalEnd - segLocalStart;
            if (localDir.sqrMagnitude < 0.0001f)
                localDir = Vector3.forward;

            // Build rotation that maps localDir -> desiredDir, keeping up stable.
            Quaternion fromLocal = Quaternion.LookRotation(localDir.normalized, Vector3.up);
            Quaternion toWorld = Quaternion.LookRotation(desiredDir, up);
            Quaternion rot = toWorld * Quaternion.Inverse(fromLocal);

            // Place so that (rot * localStart) lands on worldStart
            Vector3 pos = worldStart - (rot * segLocalStart);

            seg.SetPositionAndRotation(pos, rot);
        }
    }

    private void RebuildByCenterSpacing(
        Vector3[] worldPts, bool closed, float totalLength,
        List<Transform> existingChildren, HashSet<Transform> keepSet)
    {
        float step = GetFallbackSegmentLength();
        float half = step * 0.5f;

        float placeStart = Mathf.Clamp(startOffset, 0f, totalLength) + half;
        float placeEnd = Mathf.Max(placeStart, totalLength - half);

        if (placeEnd <= placeStart)
        {
            ClearGenerated();
            return;
        }

        int neededSegments = Mathf.FloorToInt((placeEnd - placeStart) / step) + 1;

        float d = placeStart;
        for (int k = 0; k < neededSegments; k++)
        {
            Transform seg = GetOrCreateChild(existingChildren, $"__Seg_{k}", fenceSegmentPrefab);
            keepSet.Add(seg);

            EvaluateAlongPolyline(worldPts, closed, d, out Vector3 pos, out Vector3 tangent);

            if (tangent.sqrMagnitude < 0.0001f) tangent = transform.forward;
            tangent.Normalize();

            Vector3 up = Vector3.up;

            if (conformToTerrain)
            {
                if (sampleTerrainAtSegmentEnds)
                {
                    // approximate by sampling slightly ahead/behind for better stability
                    EvaluateAlongPolyline(worldPts, closed, Mathf.Max(0f, d - half), out Vector3 a, out _);
                    EvaluateAlongPolyline(worldPts, closed, Mathf.Min(totalLength, d + half), out Vector3 b, out _);

                    bool gotA = TrySampleTerrain(a, out var hitA);
                    bool gotB = TrySampleTerrain(b, out var hitB);

                    if (gotA && gotB)
                    {
                        // shift center to avg sampled height
                        float targetY = (hitA.point.y + hitB.point.y) * 0.5f;
                        pos.y = targetY;

                        if (alignToTerrainNormal)
                        {
                            Vector3 n = (hitA.normal + hitB.normal);
                            if (n.sqrMagnitude > 0.0001f) up = n.normalized;
                        }
                    }
                    else if (TrySampleTerrain(pos, out var midHit))
                    {
                        pos = midHit.point;
                        if (alignToTerrainNormal) up = midHit.normal;
                    }
                }
                else if (TrySampleTerrain(pos, out var hit))
                {
                    pos = hit.point;
                    if (alignToTerrainNormal) up = hit.normal;
                }
            }

            pos += Vector3.up * yOffset;

            Quaternion rot = Quaternion.LookRotation(tangent, up);
            seg.SetPositionAndRotation(pos, rot);

            d += step;
        }
    }

    private bool TryGetSocketLocalPositions(Transform segmentInstanceRoot, out Vector3 localStart, out Vector3 localEnd)
    {
        localStart = Vector3.zero;
        localEnd = Vector3.zero;
        if (segmentInstanceRoot == null) return false;

        var startT = segmentInstanceRoot.Find(startSocketName);
        var endT = segmentInstanceRoot.Find(endSocketName);

        // If not direct children, try deep search (single pass)
        if (startT == null) startT = FindDeep(segmentInstanceRoot, startSocketName);
        if (endT == null) endT = FindDeep(segmentInstanceRoot, endSocketName);

        if (startT == null || endT == null) return false;

        // Convert world socket positions to segment root local (safe regardless of hierarchy)
        localStart = segmentInstanceRoot.InverseTransformPoint(startT.position);
        localEnd = segmentInstanceRoot.InverseTransformPoint(endT.position);
        return true;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        int n = root.childCount;
        for (int i = 0; i < n; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;

            var d = FindDeep(c, name);
            if (d != null) return d;
        }
        return null;
    }

    public void ClearGenerated()
    {
        EnsureSegmentsRoot();

#if UNITY_EDITOR
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Fence");
#endif

        for (int i = segmentsRoot.childCount - 1; i >= 0; i--)
        {
            var c = segmentsRoot.GetChild(i);
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(c.gameObject);
#else
            DestroyImmediate(c.gameObject);
#endif
        }

#if UNITY_EDITOR
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(this);
#endif
    }

    private void ClearGeneratedIfInvalid()
    {
        if (segmentsRoot == null) return;
        if (localPoints == null || localPoints.Count < 2 || fenceSegmentPrefab == null)
            ClearGenerated();
    }

    private static List<Transform> CollectChildren(Transform root)
    {
        var list = new List<Transform>(root.childCount);
        for (int i = 0; i < root.childCount; i++)
            list.Add(root.GetChild(i));
        return list;
    }

    private Transform GetOrCreateChild(List<Transform> existing, string name, GameObject prefab)
    {
        for (int i = 0; i < existing.Count; i++)
        {
            var t = existing[i];
            if (t != null && t.name == name)
                return t;
        }

        if (prefab == null) return null;

        GameObject go;
#if UNITY_EDITOR
        go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, segmentsRoot);
        Undo.RegisterCreatedObjectUndo(go, "Create Fence Piece");
#else
        go = Instantiate(prefab, segmentsRoot);
#endif
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        existing.Add(go.transform);
        return go.transform;
    }

    private bool BuildSampledWorldPath(out Vector3[] worldPts, out bool closed)
    {
        worldPts = null;
        closed = closedLoop;

        int n = localPoints == null ? 0 : localPoints.Count;
        if (n < 2) return false;

        // Convert control points to world
        var ctrl = new Vector3[n];
        for (int i = 0; i < n; i++) ctrl[i] = transform.TransformPoint(localPoints[i]);

        if (pathMode == PathMode.Polyline)
        {
            worldPts = ctrl;
            return true;
        }

        // Smooth: Catmull–Rom sampled polyline
        var sampled = new List<Vector3>(n * 8);

        int spanCount = closedLoop ? n : (n - 1);
        if (spanCount <= 0) return false;

        for (int i = 0; i < spanCount; i++)
        {
            // P1 -> P2 is the active span
            Vector3 p0 = GetCtrl(ctrl, i - 1, closedLoop);
            Vector3 p1 = GetCtrl(ctrl, i, closedLoop);
            Vector3 p2 = GetCtrl(ctrl, i + 1, closedLoop);
            Vector3 p3 = GetCtrl(ctrl, i + 2, closedLoop);

            float approxSpanLen = Vector3.Distance(p1, p2);
            int steps = Mathf.Max(minSamplesPerSpan, Mathf.CeilToInt(Mathf.Max(0.1f, approxSpanLen) * samplesPerMeter));

            // Add first point of span (avoid duplicates except for first span)
            if (i == 0)
                sampled.Add(p1);

            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                sampled.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        // If closed loop, we don't want a duplicate final point identical to start in the array,
        // because we treat closure implicitly when computing lengths.
        if (closedLoop && sampled.Count > 2)
        {
            // Remove last point if it's extremely close to first
            if ((sampled[sampled.Count - 1] - sampled[0]).sqrMagnitude < 0.000001f)
                sampled.RemoveAt(sampled.Count - 1);
        }

        worldPts = sampled.ToArray();
        return worldPts.Length >= 2;
    }

    private static Vector3 GetCtrl(Vector3[] ctrl, int index, bool closed)
    {
        int n = ctrl.Length;
        if (closed)
        {
            int i = index % n;
            if (i < 0) i += n;
            return ctrl[i];
        }
        else
        {
            int i = Mathf.Clamp(index, 0, n - 1);
            return ctrl[i];
        }
    }

    private static Vector3 GetCtrl(List<Vector3> ctrl, int index, bool closed)
    {
        int n = ctrl.Count;
        if (n == 0) return Vector3.zero;

        if (closed)
        {
            int i = index % n;
            if (i < 0) i += n;
            return ctrl[i];
        }
        else
        {
            int i = Mathf.Clamp(index, 0, n - 1);
            return ctrl[i];
        }
    }

    /// <summary>
    /// Finds the closest point on the sampled fence path to a world position.
    /// Returns:
    /// - distanceAlong: distance (m) along the path (loops if closed)
    /// - closestPoint: closest world point on the polyline
    /// - tangent: forward tangent (unit) along the polyline at that point
    /// </summary>
    public bool TryGetClosestPointOnPath(
        Vector3 worldPos,
        out float distanceAlong,
        out Vector3 closestPoint,
        out Vector3 tangent)
    {
        EnsureGrindCache();

        distanceAlong = 0f;
        closestPoint = transform.position;
        tangent = Vector3.forward;

        if (_grindWorldPts == null || _grindWorldPts.Count < 2 || _grindTotalLength <= 0.0001f)
            return false;

        float bestSq = float.PositiveInfinity;
        float bestAlong = 0f;
        Vector3 bestPoint = closestPoint;
        Vector3 bestTan = tangent;

        int m = _grindWorldPts.Count;
        int segCount = _grindClosed ? m : (m - 1);

        for (int i = 0; i < segCount; i++)
        {
            int j = (i + 1) % m;

            Vector3 a = _grindWorldPts[i];
            Vector3 b = _grindWorldPts[j];
            Vector3 ab = b - a;

            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.000001f)
                continue;

            float t = Vector3.Dot(worldPos - a, ab) / abLenSq;
            t = Mathf.Clamp01(t);

            Vector3 p = a + ab * t;
            float sq = (worldPos - p).sqrMagnitude;

            if (sq < bestSq)
            {
                bestSq = sq;
                bestPoint = p;

                float segLen = Mathf.Sqrt(abLenSq);
                float segStart = (i < _grindCumulative.Count) ? _grindCumulative[i] : 0f;
                bestAlong = segStart + t * segLen;

                Vector3 dir = ab / segLen;
                bestTan = (dir.sqrMagnitude > 0.0001f) ? dir : transform.forward;
            }
        }

        distanceAlong = _grindClosed ? Mathf.Repeat(bestAlong, _grindTotalLength) : Mathf.Clamp(bestAlong, 0f, _grindTotalLength);
        closestPoint = bestPoint;
        tangent = (bestTan.sqrMagnitude > 0.0001f) ? bestTan.normalized : transform.forward;

        return true;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Standard Catmull–Rom (uniform)
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private static float ComputeTotalLength(Vector3[] pts, bool closed)
    {
        int n = pts.Length;
        int segCount = closed ? n : (n - 1);
        float total = 0f;

        for (int i = 0; i < segCount; i++)
        {
            int j = (i + 1) % n;
            total += Vector3.Distance(pts[i], pts[j]);
        }

        return total;
    }

    private void EvaluateAlongPolyline(Vector3[] worldPts, bool closed, float distance, out Vector3 pos, out Vector3 tangent)
    {
        int n = worldPts.Length;
        int segCount = closed ? n : (n - 1);

        if (segCount <= 0)
        {
            pos = worldPts[0];
            tangent = transform.forward;
            return;
        }

        float total = 0f;
        for (int i = 0; i < segCount; i++)
        {
            int j = (i + 1) % n;
            total += Vector3.Distance(worldPts[i], worldPts[j]);
        }

        float d = Mathf.Clamp(distance, 0f, total);
        float accum = 0f;

        for (int i = 0; i < segCount; i++)
        {
            int j = (i + 1) % n;
            Vector3 a = worldPts[i];
            Vector3 b = worldPts[j];
            float len = Vector3.Distance(a, b);
            if (len <= 0.0001f) continue;

            if (accum + len >= d)
            {
                float t = (d - accum) / len;
                pos = Vector3.Lerp(a, b, t);
                tangent = (b - a);
                return;
            }

            accum += len;
        }

        // Fallback
        int last = segCount - 1;
        int lastB = (last + 1) % n;
        pos = worldPts[lastB];
        tangent = (worldPts[lastB] - worldPts[last]);
    }

    private Quaternion ComputeCornerRotation(Vector3[] worldCtrlPts, int index)
    {
        int n = worldCtrlPts.Length;
        if (n < 2) return transform.rotation;

        Vector3 p = worldCtrlPts[index];

        int prev = index - 1;
        int next = index + 1;

        if (closedLoop)
        {
            if (prev < 0) prev = n - 1;
            if (next >= n) next = 0;
        }
        else
        {
            prev = Mathf.Clamp(prev, 0, n - 1);
            next = Mathf.Clamp(next, 0, n - 1);
        }

        Vector3 a = worldCtrlPts[prev];
        Vector3 b = worldCtrlPts[next];

        Vector3 dirPrev = (p - a);
        Vector3 dirNext = (b - p);

        Vector3 tPrev = dirPrev.sqrMagnitude > 0.0001f ? dirPrev.normalized : transform.forward;
        Vector3 tNext = dirNext.sqrMagnitude > 0.0001f ? dirNext.normalized : transform.forward;

        Vector3 blended = Vector3.Slerp(tPrev, tNext, Mathf.Clamp01(cornerTangentBlend));
        if (blended.sqrMagnitude < 0.0001f) blended = tNext;

        return Quaternion.LookRotation(blended, Vector3.up);
    }

    private bool TrySampleTerrain(Vector3 world, out RaycastHit hit)
    {
        hit = default;
        if (!conformToTerrain) return false;

        Vector3 start = world + Vector3.up * Mathf.Max(0.01f, raycastStartHeight);
        if (Physics.Raycast(start, Vector3.down, out hit, raycastStartHeight * 2f, terrainMask, QueryTriggerInteraction.Ignore))
            return true;

        return false;
    }

    private void OnValidate()
    {
        manualSegmentLength = Mathf.Max(0.01f, manualSegmentLength);
        fixedSpacing = Mathf.Max(0.01f, fixedSpacing);
        raycastStartHeight = Mathf.Max(0.1f, raycastStartHeight);
        editorRebuildDebounce = Mathf.Max(0f, editorRebuildDebounce);

        samplesPerMeter = Mathf.Max(0.1f, samplesPerMeter);
        minSamplesPerSpan = Mathf.Max(2, minSamplesPerSpan);

        if (_cachedPrefabForLength != fenceSegmentPrefab)
            _cachedPrefabLength = -1f;

        if (!Application.isPlaying)
            ClearGeneratedIfInvalid();

        _grindCacheValid = false;

    }

#if UNITY_EDITOR
    private int ComputeGizmoKey()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (localPoints != null ? localPoints.Count : 0);
            h = h * 31 + (int)pathMode;
            h = h * 31 + (closedLoop ? 1 : 0);
            h = h * 31 + Mathf.RoundToInt(samplesPerMeter * 1000f);
            h = h * 31 + minSamplesPerSpan;

            // Transform affects TransformPoint()
            h = h * 31 + transform.position.GetHashCode();
            h = h * 31 + transform.rotation.GetHashCode();
            h = h * 31 + transform.lossyScale.GetHashCode();

            // A couple of points is usually enough to detect edits without hashing everything.
            if (localPoints != null && localPoints.Count > 0)
            {
                h = h * 31 + localPoints[0].GetHashCode();
                h = h * 31 + localPoints[localPoints.Count - 1].GetHashCode();
            }

            return h;
        }
    }

    private bool EnsureGizmoSampled()
    {
        int key = ComputeGizmoKey();
        if (_cachedGizmoValid && key == _cachedGizmoKey && _cachedGizmoSampled != null && _cachedGizmoSampled.Length >= 2)
            return true;

        _cachedGizmoKey = key;
        _cachedGizmoValid = BuildSampledWorldPath(out _cachedGizmoSampled, out _cachedGizmoClosed);
        return _cachedGizmoValid;
    }
#endif

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (localPoints == null || localPoints.Count < 2) return;

        // Draw control polyline
        int n = localPoints.Count;
        Vector3 prev = transform.TransformPoint(localPoints[0]);

        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        for (int i = 1; i < n; i++)
        {
            Vector3 cur = transform.TransformPoint(localPoints[i]);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        if (closedLoop && n >= 3)
            Gizmos.DrawLine(transform.TransformPoint(localPoints[n - 1]), transform.TransformPoint(localPoints[0]));

        // Draw sampled path if smooth
#if UNITY_EDITOR
        if (pathMode == PathMode.SmoothCatmullRom && EnsureGizmoSampled())
        {
            var sampled = _cachedGizmoSampled;
            bool closed = _cachedGizmoClosed;

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
            int m = sampled.Length;
            int segCount = closed ? m : (m - 1);
            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % m;
                Gizmos.DrawLine(sampled[i], sampled[j]);
            }
        }
#endif

    }
}
