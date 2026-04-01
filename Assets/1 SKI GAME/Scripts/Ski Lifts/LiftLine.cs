using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("Minimum Ski Pass level required to use this lift (0 = default/basic).")]
    [Min(0)]
    [SerializeField] private int requiredPassLevel = 0;
    public int RequiredPassLevel => requiredPassLevel;

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
    }

    private void OnDisable()
    {
        ActiveLiftLines.Remove(this);
    }

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


    #region Public API for visuals

    /// <summary>
    /// Rebuilds the analytic loop using current station positions/settings.
    /// Used by LiftRopeVisual to keep the visual band in sync.
    /// </summary>
    public void RebuildAnalyticLoop()
    {
        if (!ValidateStations())
            return;

        SnapStationsToTerrainIfAvailable();
        BuildAnalyticLoop();
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

        // Travel direction projected onto horizontal plane for side separation.
        Vector3 dir = b - a;
        Vector3 horizDir = dir;
        horizDir.y = 0f;
        if (horizDir.sqrMagnitude < 0.0001f)
            horizDir = Vector3.forward;
        horizDir.Normalize();

        // Left/right axis relative to travel direction (for up/down lines).
        Vector3 right = Vector3.Cross(Vector3.up, horizDir);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        right.Normalize();

        float rA = GetWorldRadius(_bottomCollider) + ropeClearance;
        float rB = GetWorldRadius(_topCollider) + ropeClearance;
        float halfSep = horizontalSeparation * 0.5f;

        Vector3 upOffset = Vector3.up * verticalOffset;

        // Contact points on either side of each sphere
        Vector3 aLeft = a + right * (rA + halfSep) + upOffset;   // Uphill side at bottom
        Vector3 aRight = a - right * (rA + halfSep) + upOffset;   // Downhill side at bottom

        Vector3 bLeft = b + right * (rB + halfSep) + upOffset;   // Uphill side at top
        Vector3 bRight = b - right * (rB + halfSep) + upOffset;   // Downhill side at top

        // Arc midpoints: move along travel direction to place control points "outside" the stations.
        Vector3 cA = a + upOffset;
        Vector3 cB = b + upOffset;

        float arcDepthA = rA + halfSep;
        float arcDepthB = rB + halfSep;

        // Station A loop at the "bottom" end (behind A along -dir)
        Vector3 aMid = cA - horizDir * arcDepthA;

        // Station B loop at the "top" end (ahead of B along +dir)
        Vector3 bMid = cB + horizDir * arcDepthB;

        // Start at uphill side near bottom station
        _bandPoints.Add(aLeft);

        // Uphill side A_left -> B_left with analytic sag
        AddSideSegmentWithSag(_bandPoints, aLeft, bLeft, includeStartPoint: false);

        // Rounded arc around top station: B_left -> B_right
        AddArcInterior(_bandPoints, bLeft, bMid, bRight);

        // Downhill side B_right -> A_right with analytic sag
        AddSideSegmentWithSag(_bandPoints, bRight, aRight, includeStartPoint: true);

        // Rounded arc around bottom station: A_right -> A_left (closing loop)
        AddArcInterior(_bandPoints, aRight, aMid, aLeft);
        // We do not re-add A_left here; the loop conceptually closes back to the first point.

        // Build cumulative lengths (including last->first segment)
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
        {
            Debug.LogWarning($"[{nameof(LiftLine)}] Analytic band length is zero after setup.", this);
        }
    }

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

        if (cfg != null)
        {
            var p = cfg.Get(requiredPassLevel);
            if (p != null) return $"{p.displayName} (L{requiredPassLevel})";
        }

        return $"Pass Level {requiredPassLevel}";
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
