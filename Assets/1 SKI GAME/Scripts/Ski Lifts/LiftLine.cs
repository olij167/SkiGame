using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum LiftCarrierMode
{
    Chair,
    TBar
}

[AddComponentMenu("Ski Lifts/Lift Line")]
public class LiftLine : MonoBehaviour
{
    [Header("Stations")]
    [Tooltip("Bottom/first station transform. Should have a SphereCollider on the same GameObject.")]
    public Transform bottomStation;

    [Tooltip("Top/second station transform. Should have a SphereCollider on the same GameObject.")]
    public Transform topStation;

    [Header("Ski Pass")]
    [Tooltip("Legacy fallback: minimum Ski Pass level required to use this lift (0 = default/basic).")]
    [Min(0)]
    [SerializeField] private int requiredPassLevel = 0;
    public int RequiredPassLevel => requiredPassLevel;

    [Tooltip("Preferred non-sequential requirement. If set, this pass ID is used instead of the legacy numeric level.")]
    [SerializeField] private string requiredPassId = "";
    public string RequiredPassId => requiredPassId;

    [Header("Curve & Sag")]
    [Tooltip("Horizontal distance between the uphill and downhill sides of the loop.")]
    public float horizontalSeparation = 4f;

    [Tooltip("Vertical offset from station center for the rope contacts (0 = through center).")]
    public float verticalOffset = 0f;

    [Tooltip("Extra clearance added to the sphere radius when placing rope contacts.")]
    public float ropeClearance = 0.2f;

    [Tooltip("Approximate max distance between control points along each straight side in world units.")]
    public float sideMaxSegmentLength = 8f;

    [Tooltip("Fraction of the side span used as initial sag depth (0 = no sag, 0.1 = 10% of span).")]
    [Range(0f, 0.5f)]
    public float sideSagFraction = 0.1f;

    [Tooltip("Number of Bezier samples per station arc (more = rounder wrap around the sphere).")]
    [Range(1, 8)]
    public int arcSamplesPerStation = 3;

    [Header("Motion")]
    [Tooltip("Speed of the lift along the band in metres per second (outside stations).")]
    public float bandSpeed = 5f;

    [Header("Station Slowdown")]
    [Tooltip("If enabled, carriers slow down near either station and return to full speed after leaving.")]
    public bool stationSlowdownEnabled = true;

    [Tooltip("Extra distance beyond the station SphereCollider radius where slowdown begins (metres).")]
    [Min(0f)]
    public float stationSlowdownDistance = 8f;

    [Tooltip("Speed multiplier at the station (0.25 = 25% of bandSpeed).")]
    [Range(0.05f, 1f)]
    public float stationMinSpeedMultiplier = 0.25f;

    [Tooltip("If true, distance-to-station ignores vertical difference (XZ only). Helps if stations are at different heights.")]
    public bool stationSlowdownIgnoreY = true;

    [Tooltip("Curve mapping normalized distance from station surface (0) to edge of slow zone (1). Value is slowdown strength (1=full slowdown, 0=no slowdown).")]
    public AnimationCurve stationSlowdownCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Terrain Clearance")]
    [Tooltip("Enable adjusting the cable so it maintains a minimum clearance to terrain between stations.")]
    public bool terrainClearanceEnabled = false;

    [Tooltip("Layers considered as terrain for cable clearance (e.g. Terrain, ground meshes).")]
    public LayerMask terrainLayers = ~0;

    [Tooltip("Desired vertical clearance between cable and terrain (in metres).")]
    public float terrainClearanceHeight = 4f;

    [Tooltip("Height above the sample point to start the downward raycast.")]
    public float terrainRaycastStartHeight = 50f;

    [Tooltip("Maximum additional distance we will raycast downward from the start height.")]
    public float terrainRaycastMaxDistance = 200f;

    [Tooltip("Also apply terrain clearance while editing (can be a bit heavier due to frequent rebuilds).")]
    public bool terrainClearanceInEditMode = false;

    [Header("Carriers")]
    [Tooltip("Prefab for a lift carrier (chair / T-bar). Must have a LiftCarrier component.")]
    public LiftCarrier carrierPrefab;

    [Tooltip("Number of carriers to distribute around the loop.")]
    public int carrierCount = 8;

    [Tooltip("Runtime list of all spawned carriers on this line.")]
    public List<LiftCarrier> carriers = new List<LiftCarrier>();

    [Header("Loading Gates")]
    [FormerlySerializedAs("loadingGate")]
    public LiftBoardGate legacyLoadingGate;

    [Tooltip("Boarding gate at the bottom station.")]
    public LiftBoardGate bottomLoadingGate;

    [Tooltip("Boarding gate at the top station.")]
    public LiftBoardGate topLoadingGate;

    [Header("Intermediate Supports")]
    [Tooltip("If enabled, support towers are inserted between bottom and top stations to break the side spans into smaller sag segments.")]
    public bool useIntermediateSupports = true;

    [Tooltip("Prefab used when auto-generating support towers.")]
    public LiftSupportTower supportTowerPrefab;

    [Tooltip("Optional parent for generated support towers.")]
    public Transform supportTowerContainer;

    [Tooltip("Toggle on to regenerate support towers immediately in the editor, then auto-reset to false.")]
    public bool regenerateSupportsNow = false;

    [Tooltip("Extra horizontal inset from the usable span ends when distributing supports evenly.")]
    [Min(0f)]
    public float supportEdgePadding = 2f;

    [Tooltip("Generated placement spacing. Towers can still be manually fine-tuned after generation.")]
    [Min(5f)]
    public float generateSupportEveryXMeters = 35f;

    [Tooltip("Distance from the bottom station before the first generated tower is allowed.")]
    [Min(0f)]
    public float supportStartOffset = 18f;

    [Tooltip("Distance from the top station before the last generated tower is allowed.")]
    [Min(0f)]
    public float supportEndOffset = 18f;

    [Tooltip("Guide height assigned to newly generated towers.")]
    public float generatedSupportGuideHeight = 8f;

    [Tooltip("Crossarm width assigned to newly generated towers.")]
    public float generatedSupportCrossarmWidth = 5f;

    [Tooltip("Layers used when terrain-snapping generated supports.")]
    public LayerMask supportPlacementLayers = ~0;

    [Tooltip("Downward raycast start height for generated support placement.")]
    public float supportPlacementRayStartHeight = 200f;

    [Tooltip("Downward raycast distance for generated support placement.")]
    public float supportPlacementRayDistance = 500f;

    [Tooltip("Current support tower list used by this lift.")]
    public List<LiftSupportTower> supportTowers = new List<LiftSupportTower>();

    [Header("Manual Support Authoring")]
    [Tooltip("If enabled, manually placed support towers keep their XZ positions and can be snapped vertically to terrain.")]
    public bool keepManualTowerXZWhenAligning = true;

    [Tooltip("Prefab used when manually placing support towers in the scene view. Falls back to supportTowerPrefab if null.")]
    public LiftSupportTower manualSupportTowerPrefab;

    [Tooltip("Optional vertical offset applied after terrain alignment.")]
    public float manualTowerTerrainYOffset = 0f;


    // ---- internal analytic loop ----

    private class CarrierRuntime
    {
        public LiftCarrier carrier;
        public Transform anchor;      // follows the band loop
        public float distanceAlong;   // distance (m) along the loop
    }

    private readonly List<CarrierRuntime> _carrierRuntime = new List<CarrierRuntime>();
    private float _visibleFastForwardMultiplier = 1f;
    private Renderer[] _cachedRenderers;

    // World-space polyline describing the band loop
    private readonly List<Vector3> _bandPoints = new List<Vector3>();
    // Cumulative length at each point index (0..Count)
    private readonly List<float> _segmentCumulative = new List<float>();

    private float _bandLength;
    public float BandLength => _bandLength;

#if UNITY_EDITOR
    private bool _editorQueuedSupportRegeneration;
    private bool _editorQueuedRebuild;
#endif

    private LiftRopeVisual[] _cachedRopeVisuals;
    private bool _supportTowerCacheDirty = true;
    private bool _ropeVisualCacheDirty = true;

    // ----------------------------------------------------------------------
    // GRINDING SUPPORT (lightweight runtime registry + closest point queries)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Lightweight runtime registry so SkiController can query lift cables
    /// without requiring extra colliders along the rope.
    /// </summary>
    public static readonly List<LiftLine> ActiveLiftLines = new List<LiftLine>(16);

    private void OnEnable()
    {
        if (!ActiveLiftLines.Contains(this))
            ActiveLiftLines.Add(this);

        _supportTowerCacheDirty = true;
        _ropeVisualCacheDirty = true;
    }

    private void OnDisable()
    {
        ActiveLiftLines.Remove(this);
        _cachedRopeVisuals = null;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

#if UNITY_EDITOR
        if (regenerateSupportsNow)
        {
            regenerateSupportsNow = false;
            QueueEditorSupportRegeneration();
            return;
        }

        QueueEditorRebuild();
#endif
    }

#if UNITY_EDITOR
    private void QueueEditorSupportRegeneration()
    {
        if (_editorQueuedSupportRegeneration)
            return;

        _editorQueuedSupportRegeneration = true;
        UnityEditor.EditorApplication.delayCall += ProcessQueuedEditorSupportRegeneration;
    }

    private void ProcessQueuedEditorSupportRegeneration()
    {
        _editorQueuedSupportRegeneration = false;

        if (this == null)
            return;

        RegenerateIntermediateSupports();
    }

    private void QueueEditorRebuild()
    {
        if (_editorQueuedRebuild)
            return;

        _editorQueuedRebuild = true;
        UnityEditor.EditorApplication.delayCall += ProcessQueuedEditorRebuild;
    }

    private void ProcessQueuedEditorRebuild()
    {
        _editorQueuedRebuild = false;

        if (this == null)
            return;

        RebuildNow();
    }

#endif

    /// <summary>
    /// Public tangent accessor (wrapper around internal tangent method).
    /// </summary>
    public Vector3 GetBandTangentPublic(float distance) => GetBandTangent(distance);

    /// <summary>
    /// Finds the closest point on the analytic band polyline to a world position.
    /// Returns:
    /// - distanceAlong: distance (m) along the loop
    /// - closestPoint: closest world point on the polyline
    /// - tangent: forward tangent (unit) along the polyline at that point
    /// </summary>
    public bool TryGetClosestPointOnBand(
        Vector3 worldPos,
        out float distanceAlong,
        out Vector3 closestPoint,
        out Vector3 tangent)
    {
        distanceAlong = 0f;
        closestPoint = transform.position;
        tangent = Vector3.forward;

        if (_bandPoints == null || _bandPoints.Count < 2 || _segmentCumulative == null || _segmentCumulative.Count < 2 || _bandLength <= 0f)
            return false;

        float bestSq = float.PositiveInfinity;
        float bestAlong = 0f;
        Vector3 bestPoint = closestPoint;
        Vector3 bestTan = tangent;

        int n = _bandPoints.Count;
        // The loop is closed implicitly (segment i goes to (i+1)%n).
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;

            Vector3 a = _bandPoints[i];
            Vector3 b = _bandPoints[j];
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
                float segStart = (i < _segmentCumulative.Count) ? _segmentCumulative[i] : 0f;
                bestAlong = segStart + t * segLen;

                Vector3 dir = ab / segLen;
                bestTan = (dir.sqrMagnitude > 0.0001f) ? dir : GetBandTangent(bestAlong);
            }
        }

        distanceAlong = Mathf.Repeat(bestAlong, _bandLength);
        closestPoint = bestPoint;
        tangent = (bestTan.sqrMagnitude > 0.0001f) ? bestTan.normalized : GetBandTangent(distanceAlong);

        return true;
    }

    private SphereCollider _bottomCollider;
    private SphereCollider _topCollider;

    private void Reset()
    {
        // Try to auto-assign stations from children
        if (!bottomStation && transform.childCount > 0)
            bottomStation = transform.GetChild(0);

        if (!topStation && transform.childCount > 1)
            topStation = transform.GetChild(1);
    }

    private void Awake()
    {
        if (!ValidateStations())
        {
            enabled = false;
            return;
        }

        SnapStationsToTerrainIfAvailable();
        CacheSupportTowers();
        BuildAnalyticLoop();
        SpawnCarriers();
    }

    private void FixedUpdate()
    {
        if (_bandLength <= 0f || _carrierRuntime.Count == 0 || Mathf.Approximately(bandSpeed, 0f))
            return;

        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < _carrierRuntime.Count; i++)
        {
            CarrierRuntime c = _carrierRuntime[i];
            if (c == null || c.anchor == null)
                continue;

            float speedMult = GetStationSpeedMultiplierAtDistance(c.distanceAlong);
            float step = bandSpeed * _visibleFastForwardMultiplier * speedMult * dt;

            c.distanceAlong = Mathf.Repeat(c.distanceAlong + step, _bandLength);

            c.carrier.distanceAlong = c.distanceAlong;

            Vector3 pos = GetBandPosition(c.distanceAlong);
            Vector3 fwd = GetBandTangent(c.distanceAlong);

            c.anchor.position = pos;
            c.anchor.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    private float GetStationSpeedMultiplierAtDistance(float distanceAlong)
    {
        if (!stationSlowdownEnabled || stationSlowdownDistance <= 0f)
            return 1f;

        if (!ValidateStations())
            return 1f;

        Vector3 pos = GetBandPosition(distanceAlong);

        float halfSep = horizontalSeparation * 0.5f;

        float Proximity01(Vector3 stationPos, SphereCollider col)
        {
            // IMPORTANT: rope orbits at radius + ropeClearance + halfSep (not just collider radius)
            float orbitRadius = GetWorldRadius(col) + ropeClearance + halfSep;

            float inner = orbitRadius;
            float outer = orbitRadius + stationSlowdownDistance;

            // Use XZ distance by default (matches how your loop is built around stations)
            Vector2 a = new Vector2(pos.x, pos.z);
            Vector2 b = new Vector2(stationPos.x, stationPos.z);
            float d = Vector2.Distance(a, b);

            if (d >= outer) return 0f;
            if (d <= inner) return 1f;

            float t = Mathf.InverseLerp(inner, outer, d); // 0 at inner -> 1 at outer
            return Mathf.Clamp01(stationSlowdownCurve.Evaluate(t));
        }

        float pBottom = Proximity01(bottomStation.position, _bottomCollider);
        float pTop = Proximity01(topStation.position, _topCollider);

        float proximity = Mathf.Max(pBottom, pTop);
        return Mathf.Lerp(1f, stationMinSpeedMultiplier, proximity);
    }

    public void SetVisibleFastForwardMultiplier(float multiplier)
    {
        _visibleFastForwardMultiplier = Mathf.Max(1f, multiplier);
    }

    public bool TryGetVisibilityBounds(out Bounds bounds)
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            _cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);

        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            bounds = new Bounds(transform.position, Vector3.one * 8f);
            return true;
        }

        bool found = false;
        bounds = default;

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer r = _cachedRenderers[i];
            if (r == null || !r.enabled)
                continue;

            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!found)
        {
            bounds = new Bounds(transform.position, Vector3.one * 8f);
            return true;
        }

        return true;
    }

    public bool HasPlayerBoardingGates
    {
        get
        {
            return GateRequiresPlayers(legacyLoadingGate) ||
                   GateRequiresPlayers(bottomLoadingGate) ||
                   GateRequiresPlayers(topLoadingGate);
        }
    }

    public void GetBoardGates(List<LiftBoardGate> results)
    {
        if (results == null)
            return;

        results.Clear();

        AddGateIfValid(results, legacyLoadingGate);
        AddGateIfValid(results, bottomLoadingGate);
        AddGateIfValid(results, topLoadingGate);
    }

    private static bool GateRequiresPlayers(LiftBoardGate gate)
    {
        return gate != null && gate.requireGateForPlayers;
    }

    private static void AddGateIfValid(List<LiftBoardGate> results, LiftBoardGate gate)
    {
        if (gate != null && !results.Contains(gate))
            results.Add(gate);
    }

    #region Public API for visuals

    /// <summary>
    /// Rebuilds the analytic loop using current station positions/settings.
    /// Used by LiftRopeVisual to keep the visual band in sync.
    /// </summary>
    public void RebuildAnalyticLoop()
    {
        if (!ValidateStations())
            return;

        RebuildNow();
    }

    /// <summary>
    /// Returns world-space position along the analytic loop at a given distance.
    /// </summary>
    public Vector3 GetBandPosition(float distance)
    {
        if (_bandLength <= 0f || _bandPoints.Count == 0)
            return transform.position;

        float s = Mathf.Repeat(distance, _bandLength);

        // Find segment where s falls
        int segIndex = 0;
        for (int i = 0; i < _segmentCumulative.Count - 1; i++)
        {
            if (s <= _segmentCumulative[i + 1])
            {
                segIndex = i;
                break;
            }
        }

        float segStart = _segmentCumulative[segIndex];
        float segEnd = _segmentCumulative[segIndex + 1];
        float t = (segEnd > segStart) ? (s - segStart) / (segEnd - segStart) : 0f;

        Vector3 p0 = _bandPoints[segIndex];
        Vector3 p1 = _bandPoints[(segIndex + 1) % _bandPoints.Count];

        return Vector3.Lerp(p0, p1, t);
    }

    #endregion

    private void SnapStationsToTerrainIfAvailable()
    {
        if (bottomStation != null)
        {
            var snap = bottomStation.GetComponent<LiftStationTerrainSnap>();
            if (snap != null)
                snap.SnapToTerrain();
        }

        if (topStation != null)
        {
            var snap = topStation.GetComponent<LiftStationTerrainSnap>();
            if (snap != null)
                snap.SnapToTerrain();
        }
    }

    #region Setup & Validation

    private bool ValidateStations()
    {
        if (bottomStation == null || topStation == null)
        {
            Debug.LogError($"[{nameof(LiftLine)}] Bottom and Top stations must be assigned.", this);
            return false;
        }

        if (_bottomCollider == null)
            _bottomCollider = bottomStation.GetComponent<SphereCollider>();
        if (_topCollider == null)
            _topCollider = topStation.GetComponent<SphereCollider>();

        if (_bottomCollider == null || _topCollider == null)
        {
            Debug.LogError($"[{nameof(LiftLine)}] Both stations must have SphereColliders on the same GameObject.", this);
            return false;
        }

        return true;
    }

    private void MarkSupportCacheDirty()
    {
        _supportTowerCacheDirty = true;
    }

    private void CacheSupportTowers(bool force = false)
    {
        if (!force && !_supportTowerCacheDirty)
            return;

        _supportTowerCacheDirty = false;

        supportTowers.RemoveAll(t => t == null);

        if (supportTowerContainer == null)
            return;

        LiftSupportTower[] found = supportTowerContainer.GetComponentsInChildren<LiftSupportTower>(true);

        supportTowers.Clear();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                supportTowers.Add(found[i]);
        }
    }

    private void MarkRopeVisualCacheDirty()
    {
        _ropeVisualCacheDirty = true;
    }

    private LiftRopeVisual[] GetRopeVisuals(bool force = false)
    {
        if (!force && !_ropeVisualCacheDirty && _cachedRopeVisuals != null)
            return _cachedRopeVisuals;

        _ropeVisualCacheDirty = false;
        _cachedRopeVisuals = GetComponentsInChildren<LiftRopeVisual>(true);
        return _cachedRopeVisuals;
    }

    public void RefreshLiftVisualsImmediate()
    {
        LiftRopeVisual[] ropeVisuals = GetRopeVisuals();
        for (int i = 0; i < ropeVisuals.Length; i++)
        {
            if (ropeVisuals[i] == null)
                continue;

            ropeVisuals[i].RebuildRopeFromLiftLine();
        }
    }

    public void RebuildNow(bool refreshRopeVisuals = true)
    {
        if (!ValidateStations())
            return;

        SnapStationsToTerrainIfAvailable();
        CacheSupportTowers();
        BuildAnalyticLoop();

        for (int i = 0; i < _carrierRuntime.Count; i++)
        {
            CarrierRuntime runtime = _carrierRuntime[i];
            if (runtime == null || runtime.anchor == null || runtime.carrier == null)
                continue;

            runtime.distanceAlong = Mathf.Repeat(runtime.distanceAlong, Mathf.Max(0.0001f, _bandLength));
            runtime.carrier.distanceAlong = runtime.distanceAlong;

            Vector3 pos = GetBandPosition(runtime.distanceAlong);
            Vector3 fwd = GetBandTangent(runtime.distanceAlong);

            runtime.anchor.position = pos;
            runtime.anchor.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        if (refreshRopeVisuals)
            RefreshLiftVisualsImmediate();
    }

    [ContextMenu("Regenerate Intermediate Supports")]
    public void RegenerateIntermediateSupports()
    {
        if (!ValidateStations())
            return;

        if (supportTowerPrefab == null)
        {
            Debug.LogWarning($"[{nameof(LiftLine)}] No support tower prefab assigned.", this);
            return;
        }

        if (supportTowerContainer == null)
        {
            GameObject root = new GameObject("GeneratedSupports");
            root.transform.SetParent(transform, false);
            supportTowerContainer = root.transform;
        }

        ClearGeneratedIntermediateSupports();

        Vector3 a = bottomStation.position;
        Vector3 b = topStation.position;
        Vector3 full = b - a;

        float fullLength = full.magnitude;
        if (fullLength < 0.01f)
        {
            RebuildNow();
            return;
        }

        Vector3 dir = full / fullLength;

        Vector3 horizDir = full;
        horizDir.y = 0f;
        if (horizDir.sqrMagnitude < 0.0001f)
            horizDir = Vector3.forward;
        horizDir.Normalize();

        float usableStart = Mathf.Max(0f, supportStartOffset);
        float usableEnd = Mathf.Max(usableStart, fullLength - supportEndOffset);

        float paddedStart = Mathf.Min(usableEnd, usableStart + supportEdgePadding);
        float paddedEnd = Mathf.Max(paddedStart, usableEnd - supportEdgePadding);
        float paddedLength = paddedEnd - paddedStart;

        supportTowers.Clear();

        float desiredSpacing = Mathf.Max(5f, generateSupportEveryXMeters);

        if (paddedLength > desiredSpacing)
        {
            int supportCount = Mathf.Max(1, Mathf.RoundToInt(paddedLength / desiredSpacing) - 1);
            float actualSpacing = paddedLength / (supportCount + 1);

            for (int i = 0; i < supportCount; i++)
            {
                float d = paddedStart + actualSpacing * (i + 1);
                CreateGeneratedSupportAtDistance(a, dir, horizDir, d);
            }
        }
        else if (paddedLength > 1f)
        {
            float d = paddedStart + paddedLength * 0.5f;
            CreateGeneratedSupportAtDistance(a, dir, horizDir, d);
        }

        MarkSupportCacheDirty();
        MarkRopeVisualCacheDirty();
        RebuildNow();
    }

    private void CreateGeneratedSupportAtDistance(Vector3 spanStart, Vector3 dir, Vector3 horizDir, float distanceAlongSpan)
    {
        Vector3 pos = spanStart + dir * distanceAlongSpan;

        Vector3 rayOrigin = pos + Vector3.up * supportPlacementRayStartHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, supportPlacementRayDistance, supportPlacementLayers, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        LiftSupportTower tower = Instantiate(
            supportTowerPrefab,
            pos,
            Quaternion.LookRotation(horizDir, Vector3.up),
            supportTowerContainer
        );

        tower.name = $"SupportTower_{supportTowers.Count:00}";
        tower.generatedByLiftLine = true;
        tower.guideHeight = generatedSupportGuideHeight;
        tower.crossarmWidth = generatedSupportCrossarmWidth;

        supportTowers.Add(tower);

        MarkSupportCacheDirty();
    }

    [ContextMenu("Clear Generated Intermediate Supports")]
    public void ClearGeneratedIntermediateSupports()
    {
        if (supportTowerContainer == null)
            return;

        List<Transform> toRemove = new List<Transform>();
        for (int i = 0; i < supportTowerContainer.childCount; i++)
        {
            Transform child = supportTowerContainer.GetChild(i);
            LiftSupportTower tower = child.GetComponent<LiftSupportTower>();
            if (tower != null && tower.generatedByLiftLine)
                toRemove.Add(child);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            if (Application.isPlaying)
                Destroy(toRemove[i].gameObject);
            else
                DestroyImmediate(toRemove[i].gameObject);
        }

        supportTowers.RemoveAll(t => t == null || t.generatedByLiftLine);
        MarkSupportCacheDirty();
        MarkRopeVisualCacheDirty();
    }

    private List<LiftSupportTower> GetOrderedSupportTowers(Vector3 origin, Vector3 axis)
    {
        CacheSupportTowers();

        List<LiftSupportTower> ordered = new List<LiftSupportTower>();
        for (int i = 0; i < supportTowers.Count; i++)
        {
            if (supportTowers[i] != null)
                ordered.Add(supportTowers[i]);
        }

        axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;

        ordered.Sort((lhs, rhs) =>
        {
            float a = Vector3.Dot(lhs.transform.position - origin, axis);
            float b = Vector3.Dot(rhs.transform.position - origin, axis);
            return a.CompareTo(b);
        });

        return ordered;
    }

    /// <summary>
    /// Build an analytic closed loop around the two station spheres.
    /// - Two straight sides (up and down) with static sag.
    /// - Rounded arcs around each sphere using quadratic Bezier curves.
    /// </summary>
    private void BuildAnalyticLoop()
    {
        _bandPoints.Clear();
        _segmentCumulative.Clear();
        _bandLength = 0f;

        Vector3 a = bottomStation.position;
        Vector3 b = topStation.position;

        Vector3 dir = b - a;
        Vector3 horizDir = dir;
        horizDir.y = 0f;
        if (horizDir.sqrMagnitude < 0.0001f)
            horizDir = Vector3.forward;
        horizDir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, horizDir);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        right.Normalize();

        float rA = GetWorldRadius(_bottomCollider) + ropeClearance;
        float rB = GetWorldRadius(_topCollider) + ropeClearance;
        float halfSep = horizontalSeparation * 0.5f;

        Vector3 upOffset = Vector3.up * verticalOffset;

        Vector3 aLeft = a + right * (rA + halfSep) + upOffset;
        Vector3 aRight = a - right * (rA + halfSep) + upOffset;

        Vector3 bLeft = b + right * (rB + halfSep) + upOffset;
        Vector3 bRight = b - right * (rB + halfSep) + upOffset;

        Vector3 cA = a + upOffset;
        Vector3 cB = b + upOffset;

        float arcDepthA = rA + halfSep;
        float arcDepthB = rB + halfSep;

        Vector3 aMid = cA - horizDir * arcDepthA;
        Vector3 bMid = cB + horizDir * arcDepthB;

        List<LiftSupportTower> orderedSupports = useIntermediateSupports
            ? GetOrderedSupportTowers(a, b - a)
            : new List<LiftSupportTower>();

        _bandPoints.Add(aLeft);

        Vector3 previous = aLeft;
        for (int i = 0; i < orderedSupports.Count; i++)
        {
            Vector3 next = orderedSupports[i].GetGuidePoint(right, true, ropeClearance, verticalOffset);
            AddSideSegmentWithSag(_bandPoints, previous, next, includeStartPoint: false);
            previous = next;
        }

        AddSideSegmentWithSag(_bandPoints, previous, bLeft, includeStartPoint: false);

        AddArcInterior(_bandPoints, bLeft, bMid, bRight);

        previous = bRight;
        bool includeStart = true;

        for (int i = orderedSupports.Count - 1; i >= 0; i--)
        {
            Vector3 next = orderedSupports[i].GetGuidePoint(right, false, ropeClearance, verticalOffset);
            AddSideSegmentWithSag(_bandPoints, previous, next, includeStartPoint: includeStart);
            previous = next;
            includeStart = false;
        }

        AddSideSegmentWithSag(_bandPoints, previous, aRight, includeStartPoint: includeStart);

        AddArcInterior(_bandPoints, aRight, aMid, aLeft);

        _segmentCumulative.Add(0f);
        for (int i = 0; i < _bandPoints.Count; i++)
        {
            Vector3 p0 = _bandPoints[i];
            Vector3 p1 = _bandPoints[(i + 1) % _bandPoints.Count];
            float segLen = Vector3.Distance(p0, p1);
            _bandLength += segLen;
            _segmentCumulative.Add(_bandLength);
        }

        if (_bandLength <= 0f)
            Debug.LogWarning($"[{nameof(LiftLine)}] Analytic band length is zero after setup.", this);
    }

    public LiftSupportTower GetManualSupportPrefab()
    {
        return manualSupportTowerPrefab != null ? manualSupportTowerPrefab : supportTowerPrefab;
    }

    public LiftSupportTower AddManualSupport(Vector3 worldPosition, Quaternion rotation)
    {
        LiftSupportTower prefab = GetManualSupportPrefab();
        if (prefab == null)
        {
            Debug.LogWarning($"[{nameof(LiftLine)}] No manual support prefab assigned.", this);
            return null;
        }

        if (supportTowerContainer == null)
        {
            GameObject root = new GameObject("GeneratedSupports");
            root.transform.SetParent(transform, false);
            supportTowerContainer = root.transform;
        }

        LiftSupportTower tower = Instantiate(prefab, worldPosition, rotation, supportTowerContainer);
        tower.generatedByLiftLine = false;

        supportTowers.Add(tower);
        MarkSupportCacheDirty();
        MarkRopeVisualCacheDirty();
        RebuildNow();

        return tower;
    }

    public void RemoveSupport(LiftSupportTower tower)
    {
        if (tower == null)
            return;

        supportTowers.Remove(tower);

        if (Application.isPlaying)
            Destroy(tower.gameObject);
        else
            DestroyImmediate(tower.gameObject);

        MarkSupportCacheDirty();
        MarkRopeVisualCacheDirty();
        RebuildNow();
    }

    [ContextMenu("Align Manual Supports To Terrain")]
    public void AlignManualSupportsToTerrain()
    {
        CacheSupportTowers(force: true);

        for (int i = 0; i < supportTowers.Count; i++)
        {
            LiftSupportTower tower = supportTowers[i];
            if (tower == null || tower.generatedByLiftLine)
                continue;

            AlignSupportToTerrain(tower, preserveXZ: keepManualTowerXZWhenAligning);
        }

        MarkSupportCacheDirty();
        MarkRopeVisualCacheDirty();
        RebuildNow();
    }

    [ContextMenu("Align All Supports To Terrain")]
    public void AlignAllSupportsToTerrain()
    {
        CacheSupportTowers(force: true);

        for (int i = 0; i < supportTowers.Count; i++)
        {
            LiftSupportTower tower = supportTowers[i];
            if (tower == null)
                continue;

            AlignSupportToTerrain(tower, preserveXZ: true);
        }

        MarkSupportCacheDirty();
        MarkRopeVisualCacheDirty();
        RebuildNow();
    }

    public void AlignSupportToTerrain(LiftSupportTower tower, bool preserveXZ)
    {
        if (tower == null)
            return;

        Vector3 pos = tower.transform.position;
        Vector3 rayOrigin = pos + Vector3.up * supportPlacementRayStartHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, supportPlacementRayDistance, supportPlacementLayers, QueryTriggerInteraction.Ignore))
        {
            if (preserveXZ)
            {
                tower.transform.position = new Vector3(
                    pos.x,
                    hit.point.y + manualTowerTerrainYOffset,
                    pos.z
                );
            }
            else
            {
                tower.transform.position = hit.point + Vector3.up * manualTowerTerrainYOffset;
            }
        }
    }

#if UNITY_EDITOR
    public void CacheSupportTowerEditorOnly()
    {
        CacheSupportTowers(force: true);
    }
#endif

    /// <summary>
    /// Approximate world radius of a SphereCollider accounting for non-uniform scale.
    /// </summary>
    private float GetWorldRadius(SphereCollider col)
    {
        if (!col) return 1f;
        float maxScale = Mathf.Max(
            Mathf.Abs(col.transform.lossyScale.x),
            Mathf.Abs(col.transform.lossyScale.y),
            Mathf.Abs(col.transform.lossyScale.z));
        return col.radius * maxScale;
    }

    #endregion

    #region Analytic loop helpers

    /// <summary>
    /// Adds a straight side between two points, subdivided with a static parabolic sag profile.
    /// Number of interior points is based on the distance and sideMaxSegmentLength.
    /// Terrain clearance is applied to every generated point if enabled.
    /// </summary>
    private void AddSideSegmentWithSag(List<Vector3> list, Vector3 from, Vector3 to, bool includeStartPoint)
    {
        // First, apply terrain clearance to the endpoints themselves
        Vector3 adjFrom = ApplyTerrainClearance(from);
        Vector3 adjTo = ApplyTerrainClearance(to);

        float length = Vector3.Distance(adjFrom, adjTo);
        if (length <= Mathf.Epsilon)
        {
            if (includeStartPoint)
                list.Add(adjFrom);

            list.Add(adjTo);
            return;
        }

        float maxSeg = Mathf.Max(0.5f, sideMaxSegmentLength);
        int segments = Mathf.Max(1, Mathf.RoundToInt(length / maxSeg));

        if (includeStartPoint)
            list.Add(adjFrom);

        float sagDepth = sideSagFraction * length;

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / (segments + 1);

            // Base straight-line interpolation
            Vector3 pos = Vector3.Lerp(adjFrom, adjTo, t);

            // Simple vertical sag: 0 at ends, -sagDepth at the center.
            float sag = -sagDepth * 4f * t * (1f - t);
            pos += Vector3.up * sag;

            // Make sure this sagged point still respects terrain clearance
            pos = ApplyTerrainClearance(pos);

            list.Add(pos);
        }

        list.Add(adjTo);
    }

    /// <summary>
    /// Adds interior points of a quadratic Bezier arc from 'from' to 'to',
    /// using 'mid' as the control point, but does NOT add the endpoints.
    /// Terrain clearance is applied if enabled.
    /// </summary>
    private void AddArcInterior(List<Vector3> list, Vector3 from, Vector3 mid, Vector3 to)
    {
        int samples = Mathf.Max(1, arcSamplesPerStation);

        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / (samples + 1);
            Vector3 pt = QuadraticBezier(from, mid, to, t);

            pt = ApplyTerrainClearance(pt);

            list.Add(pt);
        }
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * a +
               2f * oneMinusT * t * b +
               t * t * c;
    }

    /// <summary>
    /// Returns an approximate forward direction along the loop at a given distance.
    /// </summary>
    private Vector3 GetBandTangent(float distance)
    {
        if (_bandLength <= 0f || _bandPoints.Count == 0)
            return Vector3.forward;

        float lookAhead = 0.5f;
        Vector3 p0 = GetBandPosition(distance);
        Vector3 p1 = GetBandPosition(distance + lookAhead);

        Vector3 dir = (p1 - p0);
        if (dir.sqrMagnitude < 0.0001f)
        {
            Vector3 fallback = topStation.position - bottomStation.position;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        return dir.normalized;
    }

    /// <summary>
    /// If terrain clearance is enabled, lifts this point up so it stays at least
    /// terrainClearanceHeight units above any terrain hit directly beneath it.
    /// Never pushes the point down, only up.
    /// </summary>
    private Vector3 ApplyTerrainClearance(Vector3 worldPos)
    {
        if (!terrainClearanceEnabled)
            return worldPos;

        // Optionally skip in edit mode to avoid extra raycasts while moving stations
        if (!Application.isPlaying && !terrainClearanceInEditMode)
            return worldPos;

        Vector3 origin = worldPos + Vector3.up * terrainRaycastStartHeight;
        float maxDistance = terrainRaycastStartHeight + terrainRaycastMaxDistance;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, terrainLayers, QueryTriggerInteraction.Ignore))
        {
            float targetY = hit.point.y + terrainClearanceHeight;

            // Only lift the cable if it's below our desired clearance height
            if (worldPos.y < targetY)
            {
                worldPos.y = targetY;
            }
        }

        return worldPos;
    }

    #endregion

    #region Carriers

    private void SpawnCarriers()
    {
        carriers.Clear();
        _carrierRuntime.Clear();

        if (carrierPrefab == null)
        {
            Debug.LogWarning($"[{nameof(LiftLine)}] No carrier prefab assigned. Carriers will not be spawned.", this);
            return;
        }

        if (_bandLength <= 0f || carrierCount <= 0)
        {
            Debug.LogWarning($"[{nameof(LiftLine)}] Analytic band not ready or invalid parameters for carriers.", this);
            return;
        }

        float spacing = _bandLength / carrierCount;

        for (int i = 0; i < carrierCount; i++)
        {
            float dist = spacing * i;

            // Create an anchor transform that follows the band
            GameObject anchorGO = new GameObject($"CarrierAnchor_{i}");
            anchorGO.transform.SetParent(transform, worldPositionStays: false);

            CarrierRuntime runtime = new CarrierRuntime();
            runtime.anchor = anchorGO.transform;
            runtime.distanceAlong = dist;

            // Initial position and orientation
            Vector3 pos = GetBandPosition(dist);
            Vector3 fwd = GetBandTangent(dist);
            runtime.anchor.position = pos;
            runtime.anchor.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            // Spawn carrier as child of anchor (so prefab's own rope/seat hangs from this root)
            LiftCarrier carrierInstance = Instantiate(carrierPrefab, runtime.anchor);
            carrierInstance.transform.localPosition = Vector3.zero;
            carrierInstance.transform.localRotation = Quaternion.identity;

            runtime.carrier = carrierInstance;
            carrierInstance.line = this;
            carrierInstance.distanceAlong = dist;

            _carrierRuntime.Add(runtime);
            carriers.Add(carrierInstance);
        }
    }

    #endregion

    public string GetRequiredPassDisplayName()
    {
        var mgr = SkiPassManager.Instance;
        var cfg = mgr != null ? mgr.Config : null;

        if (cfg != null && !string.IsNullOrWhiteSpace(requiredPassId))
        {
            var byId = cfg.GetByPassId(requiredPassId);
            if (byId != null)
                return byId.displayName;

            return requiredPassId;
        }

        if (cfg != null)
        {
            var p = cfg.Get(requiredPassLevel);
            if (p != null)
                return p.displayName;
        }

        return "Ski pass required";
    }

    public bool HasValidRequiredPassId(SkiPassConfigSO config)
    {
        if (config == null || string.IsNullOrWhiteSpace(requiredPassId))
            return false;

        return config.GetLevelIndexByPassId(requiredPassId) >= 0;
    }

    public string GetResolvedRequiredPassId(SkiPassConfigSO config)
    {
        if (config == null)
            return string.Empty;

        if (HasValidRequiredPassId(config))
            return requiredPassId.Trim();

        return config.GetPassIdForLevel(Mathf.Max(0, requiredPassLevel));
    }

    private void OnDrawGizmosSelected()
    {
        if (_bandPoints == null || _bandPoints.Count < 2)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _bandPoints.Count; i++)
        {
            Vector3 p0 = _bandPoints[i];
            Vector3 p1 = _bandPoints[(i + 1) % _bandPoints.Count];
            Gizmos.DrawLine(p0, p1);
        }
    }
}
