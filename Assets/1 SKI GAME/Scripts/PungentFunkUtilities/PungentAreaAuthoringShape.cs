using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Spatial/Area Authoring Shape")]
    public class PungentAreaAuthoringShape : MonoBehaviour,
        IPungentAreaShapeProvider,
        IPungentAreaVolumeProvider,
        IPungentSpatialLabelProvider,
        IPungentSpatialBoundsProvider,
        IPungentSpatialVisualizationProvider
    {
        public enum ShapeMode
        {
            PolygonXZ,
            RectangleXZ,
            CircleXZ,
            BoundsFromCollider,
            BoundsFromRenderers
        }

        public enum VerticalMode
        {
            HeightFromTransform,
            ExplicitMinMax,
            Infinite
        }

        private const int CirclePolygonSegments = 32;

        [Header("Identity")]
        [Tooltip("Stable generic identifier used by future optional map, placement, or interaction bridges.")]
        public string stableAreaId;

        [Tooltip("Human-readable area name shown in authoring tools.")]
        public string displayName;

        [Tooltip("Generic category/tag list. Keep project-specific meaning in optional adapters.")]
        public List<string> categories = new List<string>();

        [Tooltip("Preview colour used by editor handles and runtime gizmos.")]
        public Color color = new Color(0.2f, 0.75f, 1f, 0.35f);

        [Header("Shape")]
        public ShapeMode shapeMode = ShapeMode.PolygonXZ;

        [Tooltip("Local-space XZ polygon points. Y is ignored for containment and kept only for authored offsets.")]
        public List<Vector3> localPolygonPoints = new List<Vector3>
        {
            new Vector3(-2f, 0f, -2f),
            new Vector3(-2f, 0f, 2f),
            new Vector3(2f, 0f, 2f),
            new Vector3(2f, 0f, -2f)
        };

        [Min(0.01f)]
        public Vector2 rectangleSize = new Vector2(4f, 4f);

        [Min(0.01f)]
        public float circleRadius = 2f;

        [Header("Vertical")]
        public VerticalMode verticalMode = VerticalMode.HeightFromTransform;

        [Min(0.01f)]
        public float height = 2f;

        public float minY;
        public float maxY = 2f;

        [Header("Runtime & Preview")]
        [Tooltip("If disabled, ContainsWorldPoint returns false while shape data remains available for authoring previews.")]
        public bool runtimeQueryable = true;

        [Tooltip("Draw runtime gizmo outlines for this area.")]
        public bool drawGizmo = true;

        [Tooltip("Draw lightweight gizmos even when this area is not selected. Disabled by default for responsive spatial-authoring scenes.")]
        public bool drawUnselectedGizmo = false;

        public string SpatialId => stableAreaId;
        public string SpatialDisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public IReadOnlyList<string> SpatialTags => categories;
        public Color SpatialColor => color;
        public int PolygonPointCount => localPolygonPoints == null ? 0 : localPolygonPoints.Count;

        private void Reset()
        {
            EnsureStableAreaId();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }

        private void OnValidate()
        {
            EnsureStableAreaId();
            rectangleSize.x = Mathf.Max(0.01f, rectangleSize.x);
            rectangleSize.y = Mathf.Max(0.01f, rectangleSize.y);
            circleRadius = Mathf.Max(0.01f, circleRadius);
            height = Mathf.Max(0.01f, height);
            if (maxY < minY)
            {
                float swap = minY;
                minY = maxY;
                maxY = swap;
            }
        }

        public bool TryGetAreaShape(out PungentAreaShape shape)
        {
            shape = new PungentAreaShape
            {
                Projection = PungentSpatialProjectionMode.XZ,
                Center = transform.position,
                Rotation = transform.rotation,
                Radius = circleRadius,
                Size = rectangleSize
            };

            if (TryGetSpatialBounds(out Bounds bounds))
                shape.Bounds = bounds;

            switch (shapeMode)
            {
                case ShapeMode.PolygonXZ:
                    shape.Kind = PungentAreaShapeKind.PolygonXZ;
                    shape.WorldPolygon = GetWorldPolygonArray();
                    return shape.WorldPolygon != null && shape.WorldPolygon.Length >= 3;
                case ShapeMode.RectangleXZ:
                    shape.Kind = PungentAreaShapeKind.RectangleXZ;
                    shape.WorldPolygon = GetWorldPolygonArray();
                    return true;
                case ShapeMode.CircleXZ:
                    shape.Kind = PungentAreaShapeKind.CircleXZ;
                    shape.WorldPolygon = GetWorldPolygonArray();
                    return true;
                case ShapeMode.BoundsFromCollider:
                case ShapeMode.BoundsFromRenderers:
                    shape.Kind = PungentAreaShapeKind.Bounds;
                    shape.Size = new Vector2(shape.Bounds.size.x, shape.Bounds.size.z);
                    return shape.Bounds.size.sqrMagnitude > 0.0001f;
                default:
                    return false;
            }
        }

        public bool TryGetAreaVolume(out PungentAreaVolume volume)
        {
            volume = new PungentAreaVolume();
            if (!TryGetAreaShape(out PungentAreaShape shape) || !TryGetSpatialBounds(out Bounds bounds))
                return false;

            GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite);
            volume = new PungentAreaVolume
            {
                Shape = shape,
                Bounds = bounds,
                MinY = worldMinY,
                MaxY = worldMaxY,
                HasFiniteHeight = finite,
                RuntimeQueryable = runtimeQueryable
            };
            return true;
        }

        public bool TryGetSpatialBounds(out Bounds bounds)
        {
            switch (shapeMode)
            {
                case ShapeMode.BoundsFromCollider:
                    return TryGetColliderBounds(out bounds);
                case ShapeMode.BoundsFromRenderers:
                    return TryGetRendererBounds(out bounds);
                default:
                    return TryGetFootprintBounds(out bounds);
            }
        }

        public bool TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetSpatialBounds(out Bounds bounds))
                return false;

            snapshot = new PungentSpatialVisualizationSnapshot
            {
                Kind = PungentSpatialVisualizationKind.Area,
                Owner = this,
                DisplayName = SpatialDisplayName,
                Color = color,
                Bounds = bounds,
                PointCount = PolygonPointCount,
                EstimatedDrawCost = Mathf.Max(PolygonPointCount, shapeMode == ShapeMode.CircleXZ ? CirclePolygonSegments : 4),
                HasWarnings = shapeMode == ShapeMode.PolygonXZ && PolygonPointCount >= 128
            };
            return true;
        }

        public List<Vector3> GetWorldPolygon()
        {
            List<Vector3> points = new List<Vector3>();
            GetWorldPolygon(points);
            return points;
        }

        public void GetWorldPolygon(List<Vector3> results)
        {
            if (results == null)
                return;

            results.Clear();
            switch (shapeMode)
            {
                case ShapeMode.PolygonXZ:
                    if (localPolygonPoints == null)
                        return;
                    for (int i = 0; i < localPolygonPoints.Count; i++)
                        results.Add(transform.TransformPoint(localPolygonPoints[i]));
                    break;
                case ShapeMode.RectangleXZ:
                    AddRectanglePoints(results);
                    break;
                case ShapeMode.CircleXZ:
                    AddCirclePoints(results, CirclePolygonSegments);
                    break;
                case ShapeMode.BoundsFromCollider:
                case ShapeMode.BoundsFromRenderers:
                    if (TryGetSpatialBounds(out Bounds bounds))
                        AddBoundsFootprintPoints(results, bounds);
                    break;
            }
        }

        public Vector3[] GetWorldPolygonArray()
        {
            List<Vector3> points = GetWorldPolygon();
            return points.Count == 0 ? Array.Empty<Vector3>() : points.ToArray();
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (!runtimeQueryable)
                return false;

            switch (shapeMode)
            {
                case ShapeMode.PolygonXZ:
                    return PassesVerticalRange(worldPoint.y) && ContainsLocalPolygonPoint(transform.InverseTransformPoint(worldPoint));
                case ShapeMode.RectangleXZ:
                    return PassesVerticalRange(worldPoint.y) && ContainsLocalRectanglePoint(transform.InverseTransformPoint(worldPoint));
                case ShapeMode.CircleXZ:
                    return PassesVerticalRange(worldPoint.y) && ContainsLocalCirclePoint(transform.InverseTransformPoint(worldPoint));
                case ShapeMode.BoundsFromCollider:
                case ShapeMode.BoundsFromRenderers:
                    return TryGetSpatialBounds(out Bounds bounds) && bounds.Contains(worldPoint);
                default:
                    return false;
            }
        }

        private void AddRectanglePoints(List<Vector3> results)
        {
            Vector2 half = rectangleSize * 0.5f;
            results.Add(transform.TransformPoint(new Vector3(-half.x, 0f, -half.y)));
            results.Add(transform.TransformPoint(new Vector3(-half.x, 0f, half.y)));
            results.Add(transform.TransformPoint(new Vector3(half.x, 0f, half.y)));
            results.Add(transform.TransformPoint(new Vector3(half.x, 0f, -half.y)));
        }

        private void AddCirclePoints(List<Vector3> results, int segments)
        {
            int count = Mathf.Max(8, segments);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
                results.Add(transform.TransformPoint(local));
            }
        }

        private static void AddBoundsFootprintPoints(List<Vector3> results, Bounds bounds)
        {
            float y = bounds.center.y;
            results.Add(new Vector3(bounds.min.x, y, bounds.min.z));
            results.Add(new Vector3(bounds.min.x, y, bounds.max.z));
            results.Add(new Vector3(bounds.max.x, y, bounds.max.z));
            results.Add(new Vector3(bounds.max.x, y, bounds.min.z));
        }

        private bool ContainsLocalRectanglePoint(Vector3 local)
        {
            Vector2 half = rectangleSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
        }

        private bool ContainsLocalCirclePoint(Vector3 local)
        {
            return new Vector2(local.x, local.z).sqrMagnitude <= circleRadius * circleRadius;
        }

        private bool ContainsLocalPolygonPoint(Vector3 local)
        {
            if (localPolygonPoints == null || localPolygonPoints.Count < 3)
                return false;

            bool inside = false;
            int count = localPolygonPoints.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector3 pi = localPolygonPoints[i];
                Vector3 pj = localPolygonPoints[j];
                float denominator = pj.z - pi.z;
                if (Mathf.Abs(denominator) < 0.000001f)
                    continue;

                bool crosses = ((pi.z > local.z) != (pj.z > local.z)) &&
                               (local.x < (pj.x - pi.x) * (local.z - pi.z) / denominator + pi.x);
                if (crosses)
                    inside = !inside;
            }

            return inside;
        }

        private bool TryGetFootprintBounds(out Bounds bounds)
        {
            List<Vector3> points = GetWorldPolygon();
            if (points.Count == 0)
            {
                bounds = new Bounds(transform.position, Vector3.zero);
                return false;
            }

            GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite);
            float fallbackY = transform.position.y;
            if (!finite)
            {
                worldMinY = fallbackY - 0.05f;
                worldMaxY = fallbackY + 0.05f;
            }

            Vector3 first = points[0];
            first.y = worldMinY;
            bounds = new Bounds(first, Vector3.zero);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 low = points[i];
                Vector3 high = points[i];
                low.y = worldMinY;
                high.y = worldMaxY;
                bounds.Encapsulate(low);
                bounds.Encapsulate(high);
            }

            return true;
        }

        private bool TryGetColliderBounds(out Bounds bounds)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(false);
            return TryCombineBounds(colliders, out bounds);
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            return TryCombineBounds(renderers, out bounds);
        }

        private static bool TryCombineBounds(Component[] components, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool initialized = false;
            if (components == null)
                return false;

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Bounds componentBounds;
                if (component is Collider collider)
                    componentBounds = collider.bounds;
                else if (component is Renderer renderer)
                    componentBounds = renderer.bounds;
                else
                    continue;

                if (!initialized)
                {
                    bounds = componentBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(componentBounds);
                }
            }

            return initialized;
        }

        private bool PassesVerticalRange(float worldY)
        {
            GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite);
            return !finite || (worldY >= worldMinY && worldY <= worldMaxY);
        }

        private void GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite)
        {
            switch (verticalMode)
            {
                case VerticalMode.ExplicitMinMax:
                    worldMinY = Mathf.Min(minY, maxY);
                    worldMaxY = Mathf.Max(minY, maxY);
                    finite = true;
                    break;
                case VerticalMode.Infinite:
                    worldMinY = float.NegativeInfinity;
                    worldMaxY = float.PositiveInfinity;
                    finite = false;
                    break;
                default:
                    worldMinY = transform.position.y;
                    worldMaxY = transform.position.y + Mathf.Max(0.01f, height);
                    finite = true;
                    break;
            }
        }

        private void EnsureStableAreaId()
        {
            if (!string.IsNullOrWhiteSpace(stableAreaId))
                return;

            stableAreaId = Guid.NewGuid().ToString("N");
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmo || !drawUnselectedGizmo)
                return;

            DrawAreaGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
                return;

            DrawAreaGizmo(true);
        }

        private void DrawAreaGizmo(bool selected)
        {
            Color previous = Gizmos.color;
            Color outline = color;
            outline.a = selected ? 0.95f : 0.55f;
            Gizmos.color = outline;

            if ((shapeMode == ShapeMode.BoundsFromCollider || shapeMode == ShapeMode.BoundsFromRenderers) &&
                TryGetSpatialBounds(out Bounds bounds))
            {
                Gizmos.DrawWireCube(bounds.center, bounds.size);
                Gizmos.color = previous;
                return;
            }

            List<Vector3> points = GetWorldPolygon();
            if (points.Count >= 2)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    int j = (i + 1) % points.Count;
                    Gizmos.DrawLine(points[i], points[j]);
                }
            }

            if (selected && TryGetSpatialBounds(out Bounds selectedBounds))
                Gizmos.DrawWireCube(selectedBounds.center, selectedBounds.size);

            Gizmos.color = previous;
        }
    }
}
