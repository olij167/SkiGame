using System.Collections.Generic;
using UnityEngine;

public enum LiftCarrierMode
{
    Chair,
    TBar
}

/// <summary>
/// Controls a single lift line:
/// - Defines stations and path (via LiftPath)
/// - (Optionally) auto-generates towers along the line
/// - Spawns and advances carriers (chairs / T-bars) along the cable
/// </summary>
public class LiftLine : MonoBehaviour
{
    [Header("Stations")]
    public LiftStation bottomStation;
    public LiftStation topStation;

    [Tooltip("Optional manually defined control points between stations (e.g. custom towers). Ignored if auto-generate is enabled.")]
    public Transform[] extraControlPoints;

    [Header("Path")]
    public LiftPath path;
    [Tooltip("Cable speed in metres per second.")]
    public float speed = 5f;

    [Header("Carriers")]
    public LiftCarrier carrierPrefab;
    public int carrierCount = 10;

    [Header("Tower Generation")]
    [Tooltip("Automatically generate towers between bottom and top stations.")]
    public bool autoGenerateTowers = true;
    [Tooltip("Prefab for a tower. Its origin is placed at ground level; a child named 'CableTop' is created at the specified height.")]
    public GameObject towerPrefab;
    [Tooltip("Approximate spacing between towers along the straight line between stations.")]
    public float towerSpacing = 30f;
    [Tooltip("Height of the cable above the tower base (in metres).")]
    public float towerHeight = 8f;
    [Tooltip("Maximum height above terrain to start ground raycasts.")]
    public float towerRaycastHeight = 50f;
    [Tooltip("Layers considered as 'ground' when placing towers.")]
    public LayerMask groundMask = ~0;
    [Tooltip("Parent transform for auto-generated towers. If null, one will be created.")]
    public Transform towersParent;

    private readonly List<LiftCarrier> carriers = new List<LiftCarrier>();

    // Internal bookkeeping for generated towers so we can safely clear them
    [SerializeField, HideInInspector]
    private List<Transform> generatedTowerInstances = new List<Transform>();
    [SerializeField, HideInInspector]
    private List<Transform> generatedTowerPoints = new List<Transform>();

    // Reusable buffer for building path control points
    private readonly List<Transform> controlPointsBuffer = new List<Transform>();

    private void Awake()
    {
        if (!path) path = GetComponent<LiftPath>();

        BuildPath();
        SpawnCarriers();
    }

    private void FixedUpdate()
    {
        if (path == null || path.TotalLength <= 0f)
            return;

        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < carriers.Count; i++)
        {
            var carrier = carriers[i];
            if (carrier == null) continue;

            carrier.distanceAlong += speed * dt;
            Vector3 pos = path.GetPosition(carrier.distanceAlong);
            Vector3 fwd = path.GetForward(carrier.distanceAlong);

            carrier.transform.position = pos;
            carrier.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    /// <summary>
    /// Build the LiftPath from stations and either generated or manual towers.
    /// </summary>
    public void BuildPath()
    {
        if (!path)
            return;

        controlPointsBuffer.Clear();

        if (bottomStation)
        {
            controlPointsBuffer.Add(bottomStation.transform);
            bottomStation.line = this;
        }

        // Remove previously generated towers (editor or runtime)
        ClearGeneratedTowers();

        if (autoGenerateTowers && bottomStation && topStation)
        {
            GenerateTowersAlongLine(controlPointsBuffer);
        }
        else if (extraControlPoints != null && extraControlPoints.Length > 0)
        {
            controlPointsBuffer.AddRange(extraControlPoints);
        }

        if (topStation)
        {
            controlPointsBuffer.Add(topStation.transform);
            topStation.line = this;
        }

        path.BuildPath(controlPointsBuffer.ToArray());
    }

    /// <summary>
    /// Generates tower instances along the straight line between bottom and top stations,
    /// raycasting down to terrain and adding a 'CableTop' child used as a control point.
    /// </summary>
    private void GenerateTowersAlongLine(List<Transform> controlPointsAccum)
    {
        if (!bottomStation || !topStation)
            return;

        Vector3 start = bottomStation.transform.position;
        Vector3 end = topStation.transform.position;
        float totalDist = Vector3.Distance(start, end);

        if (totalDist <= Mathf.Epsilon || towerSpacing <= 0.1f)
            return;

        int towerCount = Mathf.FloorToInt(totalDist / towerSpacing);
        if (towerCount <= 0)
            return;

        if (!towersParent)
        {
            GameObject parentObj = new GameObject("GeneratedTowers");
            parentObj.transform.SetParent(transform, false);
            towersParent = parentObj.transform;
        }

        Vector3 lineDir = (end - start).normalized;

        for (int i = 1; i <= towerCount; i++)
        {
            float t = (float)i / (towerCount + 1);
            Vector3 approxPos = Vector3.Lerp(start, end, t);

            // Raycast down to terrain to find tower base
            Vector3 rayOrigin = approxPos + Vector3.up * towerRaycastHeight;
            Vector3 basePos = approxPos;
            RaycastHit hit;

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, towerRaycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                basePos = hit.point;
            }

            Transform towerInstance;

            if (towerPrefab != null)
            {
                GameObject go = Instantiate(towerPrefab, basePos, Quaternion.LookRotation(lineDir, Vector3.up), towersParent);
                towerInstance = go.transform;
            }
            else
            {
                // Fallback primitive tower
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Tower_{i}";
                go.transform.SetParent(towersParent, false);
                go.transform.position = basePos;
                go.transform.rotation = Quaternion.LookRotation(lineDir, Vector3.up);
                go.transform.localScale = new Vector3(0.5f, towerHeight, 0.5f);
                towerInstance = go.transform;
            }

            // Create a child representing the cable attachment point at the top of the tower
            GameObject topGO = new GameObject("CableTop");
            topGO.transform.SetParent(towerInstance, false);
            topGO.transform.localPosition = Vector3.up * towerHeight;

            generatedTowerInstances.Add(towerInstance);
            generatedTowerPoints.Add(topGO.transform);

            controlPointsAccum.Add(topGO.transform);
        }
    }

    /// <summary>
    /// Clears previously generated towers and their control points.
    /// </summary>
    private void ClearGeneratedTowers()
    {
        if (generatedTowerInstances != null)
        {
            for (int i = 0; i < generatedTowerInstances.Count; i++)
            {
                Transform instance = generatedTowerInstances[i];
                if (instance == null) continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(instance.gameObject);
                }
                else
#endif
                {
                    Destroy(instance.gameObject);
                }
            }
            generatedTowerInstances.Clear();
        }

        if (generatedTowerPoints != null)
        {
            generatedTowerPoints.Clear();
        }

        // Clean up parent if it is now empty and looks like an auto-generated container
        if (towersParent != null && towersParent.childCount == 0 && towersParent.name == "GeneratedTowers")
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(towersParent.gameObject);
            }
            else
#endif
            {
                Destroy(towersParent.gameObject);
            }

            towersParent = null;
        }
    }

    /// <summary>
    /// Spawn carriers evenly spaced along the LiftPath.
    /// </summary>
    private void SpawnCarriers()
    {
        carriers.Clear();

        if (!carrierPrefab || path == null || path.TotalLength <= 0f || carrierCount <= 0)
            return;

        float spacing = path.TotalLength / carrierCount;
        for (int i = 0; i < carrierCount; i++)
        {
            LiftCarrier instance = Instantiate(carrierPrefab, transform);
            instance.line = this;
            instance.distanceAlong = spacing * i;

            instance.transform.position = path.GetPosition(instance.distanceAlong);
            instance.transform.rotation = Quaternion.LookRotation(path.GetForward(instance.distanceAlong), Vector3.up);

            carriers.Add(instance);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (!path) path = GetComponent<LiftPath>();
            BuildPath();

            // Note: we do NOT spawn carriers in edit mode to avoid clutter.
        }
    }
#endif
}
