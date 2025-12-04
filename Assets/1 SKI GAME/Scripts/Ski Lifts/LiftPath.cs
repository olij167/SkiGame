using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct LiftPathSegment
{
    public Vector3 start;
    public Vector3 end;
    public float length;
}

/// <summary>
/// Represents the sagging cable path between stations / towers.
/// Built from a set of control points (bottom station, tower tops, top station).
/// </summary>
public class LiftPath : MonoBehaviour
{
    [Header("Control Points (auto-filled by LiftLine)")]
    public Transform[] controlPoints;

    [Header("Cable Shape")]
    [Tooltip("Controls how much sag is applied along each segment (0-1).")]
    public AnimationCurve sagCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
    [Tooltip("Maximum sag (in metres) applied at the peak of sagCurve for each segment.")]
    public float maxSag = 3f;
    [Tooltip("How many samples per segment when drawing / building the cable.")]
    public int samplesPerSegment = 20;

    [Header("Debug / Gizmos")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.gray;
    public float gizmoPointRadius = 0.05f;

    private readonly List<LiftPathSegment> segments = new List<LiftPathSegment>();
    private float totalLength;

    /// <summary>
    /// Build path segments from the provided control points.
    /// Called by LiftLine whenever stations / towers change.
    /// </summary>
    public void BuildPath(Transform[] points)
    {
        controlPoints = points;
        segments.Clear();
        totalLength = 0f;

        if (controlPoints == null || controlPoints.Length < 2)
            return;

        for (int i = 0; i < controlPoints.Length - 1; i++)
        {
            Vector3 a = controlPoints[i].position;
            Vector3 b = controlPoints[i + 1].position;
            float len = Vector3.Distance(a, b);

            var seg = new LiftPathSegment
            {
                start = a,
                end = b,
                length = len
            };

            segments.Add(seg);
            totalLength += len;
        }
    }

    public float TotalLength => totalLength;

    /// <summary>
    /// Returns the world position along the cable for a given distance.
    /// distanceAlong is automatically wrapped into [0, TotalLength].
    /// </summary>
    public Vector3 GetPosition(float distanceAlong)
    {
        if (segments.Count == 0)
            return transform.position;

        if (totalLength <= 0f)
            return segments[segments.Count - 1].end;

        distanceAlong = Mathf.Repeat(distanceAlong, totalLength);

        float accum = 0f;
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            float nextAccum = accum + seg.length;

            if (distanceAlong <= nextAccum)
            {
                float localDist = distanceAlong - accum;
                float t = seg.length > 0f ? (localDist / seg.length) : 0f;

                // Base linear interpolate
                Vector3 pos = Vector3.Lerp(seg.start, seg.end, t);

                // Apply sag in world up, heavier mid-segment
                float sagT = sagCurve != null ? sagCurve.Evaluate(t) : 0f;
                pos.y -= sagT * maxSag;

                return pos;
            }

            accum = nextAccum;
        }

        // Fallback
        return segments[segments.Count - 1].end;
    }

    /// <summary>
    /// Returns the forward direction along the cable at the given distance.
    /// </summary>
    public Vector3 GetForward(float distanceAlong)
    {
        float delta = 0.2f;
        Vector3 p1 = GetPosition(distanceAlong);
        Vector3 p2 = GetPosition(distanceAlong + delta);
        Vector3 forward = (p2 - p1).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return forward;
    }

    /// <summary>
    /// Sample the path into a list of positions for rendering / gizmos.
    /// Optional lateralOffset offsets each point sideways in cable local space.
    /// </summary>
    /// <param name="buffer">List to fill with positions.</param>
    /// <param name="samplesPerSegmentOverride">If >0, overrides samplesPerSegment.</param>
    /// <param name="lateralOffset">Offset to the side (metres). Positive is to the right of travel.</param>
    public void GetSampledPositions(List<Vector3> buffer, int samplesPerSegmentOverride = -1, float lateralOffset = 0f)
    {
        buffer.Clear();

        if (segments == null || segments.Count == 0 || totalLength <= 0f)
            return;

        int segSamples = samplesPerSegmentOverride > 0 ? samplesPerSegmentOverride : samplesPerSegment;
        if (segSamples < 1) segSamples = 1;

        float distanceAccum = 0f;

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];

            // Ensure we include the end of the last segment exactly once.
            int steps = (i == segments.Count - 1) ? segSamples + 1 : segSamples;

            for (int s = 0; s < steps; s++)
            {
                float t = (float)s / segSamples;
                float distanceAlong = distanceAccum + seg.length * t;

                Vector3 pos = GetPosition(distanceAlong);

                if (Mathf.Abs(lateralOffset) > 0.0001f)
                {
                    Vector3 forward = GetForward(distanceAlong);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    pos += right * lateralOffset;
                }

                buffer.Add(pos);
            }

            distanceAccum += seg.length;
        }
    }

#if UNITY_EDITOR
    private static readonly List<Vector3> gizmoPositions = new List<Vector3>();

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        // Try to ensure we have a built path based on control points in editor
        if ((segments == null || segments.Count == 0) &&
            controlPoints != null && controlPoints.Length > 1)
        {
            BuildPath(controlPoints);
        }

        if (segments == null || segments.Count == 0 || totalLength <= 0f)
            return;

        gizmoPositions.Clear();
        GetSampledPositions(gizmoPositions);

        if (gizmoPositions.Count < 2)
            return;

        Gizmos.color = gizmoColor;

        for (int i = 0; i < gizmoPositions.Count - 1; i++)
        {
            Vector3 p0 = gizmoPositions[i];
            Vector3 p1 = gizmoPositions[i + 1];

            Gizmos.DrawLine(p0, p1);
            if (gizmoPointRadius > 0f)
            {
                Gizmos.DrawSphere(p0, gizmoPointRadius);
            }
        }

        // Draw final point sphere
        if (gizmoPointRadius > 0f)
        {
            Gizmos.DrawSphere(gizmoPositions[gizmoPositions.Count - 1], gizmoPointRadius);
        }
    }
#endif
}
