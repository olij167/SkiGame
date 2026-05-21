using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentAreaPrimitiveKind
    {
        PolygonXZ,
        RectangleXZ,
        CircleXZ,
        Box,
        Sphere
    }

    public enum PungentSpatialVerticalBehavior
    {
        HeightFromTransform,
        ExplicitMinMax,
        Infinite
    }

    public interface IPungentAreaPriorityProvider
    {
        int AreaPriority { get; }
        float EdgeFalloff { get; }
    }

    [CreateAssetMenu(menuName = "PungentFunk Utilities/Scene Authoring/Area Asset", fileName = "PungentAreaAsset")]
    public class PungentAreaAsset : ScriptableObject
    {
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public PungentAreaPrimitiveKind shape = PungentAreaPrimitiveKind.PolygonXZ;
        public List<Vector3> polygonPoints = new List<Vector3>();
        [Min(0.01f)] public Vector2 rectangleSize = new Vector2(4f, 4f);
        [Min(0.01f)] public float circleRadius = 2f;
        [Min(0.01f)] public Vector3 boxSize = new Vector3(4f, 2f, 4f);
        [Min(0.01f)] public float sphereRadius = 2f;
        public PungentSpatialVerticalBehavior verticalBehavior = PungentSpatialVerticalBehavior.HeightFromTransform;
        [Min(0.01f)] public float height = 2f;
        public float minY;
        public float maxY = 2f;
        [Min(0f)] public float edgeFalloff;
        public int priority;
        public bool runtimeQueryable = true;

        private void OnValidate()
        {
            Normalize(name);
        }

        public void Normalize(string fallbackName)
        {
            if (metadata == null)
                metadata = new PungentSpatialObjectMetadata();
            metadata.Normalize(fallbackName);
            if (polygonPoints == null)
                polygonPoints = new List<Vector3>();
            rectangleSize.x = Mathf.Max(0.01f, rectangleSize.x);
            rectangleSize.y = Mathf.Max(0.01f, rectangleSize.y);
            circleRadius = Mathf.Max(0.01f, circleRadius);
            boxSize.x = Mathf.Max(0.01f, boxSize.x);
            boxSize.y = Mathf.Max(0.01f, boxSize.y);
            boxSize.z = Mathf.Max(0.01f, boxSize.z);
            sphereRadius = Mathf.Max(0.01f, sphereRadius);
            height = Mathf.Max(0.01f, height);
            edgeFalloff = Mathf.Max(0f, edgeFalloff);
            if (maxY < minY)
            {
                float swap = minY;
                minY = maxY;
                maxY = swap;
            }
        }
    }

    [ExecuteAlways]
    [AddComponentMenu("Utilities/Spatial/Area Instance")]
    public class PungentAreaInstance : MonoBehaviour,
        IPungentAreaShapeProvider,
        IPungentAreaVolumeProvider,
        IPungentAreaPriorityProvider,
        IPungentSpatialLabelProvider,
        IPungentSpatialBoundsProvider,
        IPungentSpatialVisualizationProvider,
        IPungentSpatialObjectMetadataProvider,
        IPungentSpatialValidationProvider
    {
        private const int CircleSegments = 32;

        public PungentAreaAsset asset;
        public bool useAssetShape;
        public bool useAssetMetadata;
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public PungentAreaPrimitiveKind shape = PungentAreaPrimitiveKind.PolygonXZ;
        public List<Vector3> polygonPoints = new List<Vector3>();
        [Min(0.01f)] public Vector2 rectangleSize = new Vector2(4f, 4f);
        [Min(0.01f)] public float circleRadius = 2f;
        [Min(0.01f)] public Vector3 boxSize = new Vector3(4f, 2f, 4f);
        [Min(0.01f)] public float sphereRadius = 2f;
        public PungentSpatialVerticalBehavior verticalBehavior = PungentSpatialVerticalBehavior.HeightFromTransform;
        [Min(0.01f)] public float height = 2f;
        public float minY;
        public float maxY = 2f;
        [Min(0f)] public float edgeFalloff;
        public int priority;
        public bool runtimeQueryable = true;
        public bool drawGizmo = true;
        public bool drawUnselectedGizmo;

        public string SpatialId => ActiveMetadata.StableId;
        public string SpatialDisplayName => ActiveMetadata.DisplayNameOrFallback(name);
        public IReadOnlyList<string> SpatialTags => ActiveMetadata.TagNames;
        public Color SpatialColor => ActiveMetadata.color;
        public int AreaPriority => ActiveAssetOrInstancePriority;
        public float EdgeFalloff => ActiveEdgeFalloff;
        public int PolygonPointCount => ActivePolygon.Count;

        public PungentSpatialObjectMetadata ActiveMetadata
        {
            get
            {
                if (useAssetMetadata && asset != null && asset.metadata != null)
                    return asset.metadata;
                if (metadata == null)
                    metadata = new PungentSpatialObjectMetadata();
                return metadata;
            }
        }

        public PungentAreaPrimitiveKind ActiveShape => useAssetShape && asset != null ? asset.shape : shape;
        public IReadOnlyList<Vector3> ActivePolygon => useAssetShape && asset != null && asset.polygonPoints != null ? asset.polygonPoints : polygonPoints;
        public Vector2 ActiveRectangleSize => useAssetShape && asset != null ? asset.rectangleSize : rectangleSize;
        public float ActiveCircleRadius => useAssetShape && asset != null ? asset.circleRadius : circleRadius;
        public Vector3 ActiveBoxSize => useAssetShape && asset != null ? asset.boxSize : boxSize;
        public float ActiveSphereRadius => useAssetShape && asset != null ? asset.sphereRadius : sphereRadius;
        public PungentSpatialVerticalBehavior ActiveVerticalBehavior => useAssetShape && asset != null ? asset.verticalBehavior : verticalBehavior;
        public float ActiveHeight => useAssetShape && asset != null ? asset.height : height;
        public float ActiveMinY => useAssetShape && asset != null ? asset.minY : minY;
        public float ActiveMaxY => useAssetShape && asset != null ? asset.maxY : maxY;
        public bool ActiveRuntimeQueryable => useAssetShape && asset != null ? asset.runtimeQueryable : runtimeQueryable;
        public float ActiveEdgeFalloff => useAssetShape && asset != null ? asset.edgeFalloff : edgeFalloff;
        public int ActiveAssetOrInstancePriority => useAssetShape && asset != null ? asset.priority : priority;

        private void Reset()
        {
            EnsureDefaultData();
        }

        private void OnEnable()
        {
            EnsureDefaultData();
            PungentSpatialRuntimeRegistry.RegisterGlobal(this);
        }

        private void OnDisable()
        {
            PungentSpatialRuntimeRegistry.UnregisterGlobal(this);
        }

        private void OnValidate()
        {
            Normalize();
        }

        public void EnsureDefaultData()
        {
            if (metadata == null)
                metadata = new PungentSpatialObjectMetadata();
            metadata.Normalize(name);
            if (polygonPoints == null)
                polygonPoints = new List<Vector3>();
            if (polygonPoints.Count == 0)
            {
                polygonPoints.Add(new Vector3(-2f, 0f, -2f));
                polygonPoints.Add(new Vector3(-2f, 0f, 2f));
                polygonPoints.Add(new Vector3(2f, 0f, 2f));
                polygonPoints.Add(new Vector3(2f, 0f, -2f));
            }

            rectangleSize.x = Mathf.Max(0.01f, rectangleSize.x);
            rectangleSize.y = Mathf.Max(0.01f, rectangleSize.y);
            circleRadius = Mathf.Max(0.01f, circleRadius);
            boxSize.x = Mathf.Max(0.01f, boxSize.x);
            boxSize.y = Mathf.Max(0.01f, boxSize.y);
            boxSize.z = Mathf.Max(0.01f, boxSize.z);
            sphereRadius = Mathf.Max(0.01f, sphereRadius);
            height = Mathf.Max(0.01f, height);
            edgeFalloff = Mathf.Max(0f, edgeFalloff);
            if (maxY < minY)
            {
                float swap = minY;
                minY = maxY;
                maxY = swap;
            }
        }

        public void Normalize()
        {
            EnsureDefaultData();
            asset?.Normalize(asset.name);
        }

        public bool TryGetAreaShape(out PungentAreaShape areaShape)
        {
            areaShape = new PungentAreaShape
            {
                Projection = PungentSpatialProjectionMode.XZ,
                Center = transform.position,
                Rotation = transform.rotation,
                Radius = ActiveCircleRadius,
                Size = ActiveRectangleSize
            };

            if (TryGetSpatialBounds(out Bounds bounds))
                areaShape.Bounds = bounds;

            switch (ActiveShape)
            {
                case PungentAreaPrimitiveKind.PolygonXZ:
                    areaShape.Kind = PungentAreaShapeKind.PolygonXZ;
                    areaShape.WorldPolygon = GetWorldPolygonArray();
                    return areaShape.WorldPolygon != null && areaShape.WorldPolygon.Length >= 3;
                case PungentAreaPrimitiveKind.RectangleXZ:
                    areaShape.Kind = PungentAreaShapeKind.RectangleXZ;
                    areaShape.WorldPolygon = GetWorldPolygonArray();
                    areaShape.Size = ActiveRectangleSize;
                    return true;
                case PungentAreaPrimitiveKind.CircleXZ:
                    areaShape.Kind = PungentAreaShapeKind.CircleXZ;
                    areaShape.WorldPolygon = GetWorldPolygonArray();
                    areaShape.Radius = ActiveCircleRadius;
                    return true;
                case PungentAreaPrimitiveKind.Box:
                    areaShape.Kind = PungentAreaShapeKind.Bounds;
                    areaShape.WorldPolygon = GetWorldPolygonArray();
                    areaShape.Size = new Vector2(ActiveBoxSize.x, ActiveBoxSize.z);
                    return true;
                case PungentAreaPrimitiveKind.Sphere:
                    areaShape.Kind = PungentAreaShapeKind.CircleXZ;
                    areaShape.WorldPolygon = GetWorldPolygonArray();
                    areaShape.Radius = ActiveSphereRadius;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetAreaVolume(out PungentAreaVolume volume)
        {
            volume = default;
            if (!TryGetAreaShape(out PungentAreaShape areaShape) || !TryGetSpatialBounds(out Bounds bounds))
                return false;

            GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite);
            volume = new PungentAreaVolume
            {
                Shape = areaShape,
                Bounds = bounds,
                MinY = worldMinY,
                MaxY = worldMaxY,
                HasFiniteHeight = finite,
                RuntimeQueryable = ActiveRuntimeQueryable
            };
            return true;
        }

        public bool TryGetSpatialBounds(out Bounds bounds)
        {
            List<Vector3> polygon = GetWorldPolygon();
            if (polygon.Count == 0)
            {
                bounds = new Bounds(transform.position, Vector3.zero);
                return false;
            }

            GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite);
            if (!finite)
            {
                worldMinY = transform.position.y - 0.05f;
                worldMaxY = transform.position.y + 0.05f;
            }

            Vector3 first = polygon[0];
            first.y = worldMinY;
            bounds = new Bounds(first, Vector3.zero);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 low = polygon[i];
                Vector3 high = polygon[i];
                low.y = worldMinY;
                high.y = worldMaxY;
                bounds.Encapsulate(low);
                bounds.Encapsulate(high);
            }

            return true;
        }

        public bool TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetSpatialBounds(out Bounds bounds))
                return false;

            int pointCount = ActiveShape == PungentAreaPrimitiveKind.CircleXZ || ActiveShape == PungentAreaPrimitiveKind.Sphere
                ? CircleSegments
                : GetWorldPolygon().Count;
            snapshot = new PungentSpatialVisualizationSnapshot
            {
                Kind = PungentSpatialVisualizationKind.Area,
                Owner = this,
                DisplayName = SpatialDisplayName,
                Color = SpatialColor,
                Bounds = bounds,
                PointCount = pointCount,
                EstimatedDrawCost = Mathf.Max(4, pointCount),
                HasWarnings = ActiveShape == PungentAreaPrimitiveKind.PolygonXZ && pointCount >= 128
            };
            return true;
        }

        public bool TryGetSpatialMetadata(out PungentSpatialObjectMetadata spatialMetadata)
        {
            spatialMetadata = ActiveMetadata;
            return spatialMetadata != null;
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            return TryGetAreaVolume(out PungentAreaVolume volume) &&
                   PungentSpatialRuntimeQueryUtility.ContainsPoint(volume, worldPoint);
        }

        public List<Vector3> GetWorldPolygon()
        {
            List<Vector3> results = new List<Vector3>();
            AddWorldPolygon(results);
            return results;
        }

        public Vector3[] GetWorldPolygonArray()
        {
            List<Vector3> polygon = GetWorldPolygon();
            return polygon.Count == 0 ? Array.Empty<Vector3>() : polygon.ToArray();
        }

        public int GetSpatialValidationIssues(IList<PungentSpatialValidationIssue> results)
        {
            int before = results == null ? 0 : results.Count;
            PungentSpatialValidationUtility.ValidateArea(this, results);
            return results == null ? 0 : results.Count - before;
        }

        private void AddWorldPolygon(List<Vector3> results)
        {
            if (results == null)
                return;

            switch (ActiveShape)
            {
                case PungentAreaPrimitiveKind.PolygonXZ:
                {
                    IReadOnlyList<Vector3> activePolygon = ActivePolygon;
                    if (activePolygon == null)
                        return;
                    for (int i = 0; i < activePolygon.Count; i++)
                        results.Add(transform.TransformPoint(activePolygon[i]));
                    break;
                }
                case PungentAreaPrimitiveKind.RectangleXZ:
                    AddRectangle(results, ActiveRectangleSize);
                    break;
                case PungentAreaPrimitiveKind.CircleXZ:
                    AddCircle(results, ActiveCircleRadius);
                    break;
                case PungentAreaPrimitiveKind.Box:
                    AddRectangle(results, new Vector2(ActiveBoxSize.x, ActiveBoxSize.z));
                    break;
                case PungentAreaPrimitiveKind.Sphere:
                    AddCircle(results, ActiveSphereRadius);
                    break;
            }
        }

        private void AddRectangle(List<Vector3> results, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            results.Add(transform.TransformPoint(new Vector3(-half.x, 0f, -half.y)));
            results.Add(transform.TransformPoint(new Vector3(-half.x, 0f, half.y)));
            results.Add(transform.TransformPoint(new Vector3(half.x, 0f, half.y)));
            results.Add(transform.TransformPoint(new Vector3(half.x, 0f, -half.y)));
        }

        private void AddCircle(List<Vector3> results, float radius)
        {
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                results.Add(transform.TransformPoint(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius)));
            }
        }

        private void GetVerticalRange(out float worldMinY, out float worldMaxY, out bool finite)
        {
            if (ActiveShape == PungentAreaPrimitiveKind.Sphere)
            {
                float radius = ActiveSphereRadius;
                worldMinY = transform.position.y - radius;
                worldMaxY = transform.position.y + radius;
                finite = true;
                return;
            }

            if (ActiveShape == PungentAreaPrimitiveKind.Box)
            {
                float half = ActiveBoxSize.y * 0.5f;
                worldMinY = transform.position.y - half;
                worldMaxY = transform.position.y + half;
                finite = true;
                return;
            }

            switch (ActiveVerticalBehavior)
            {
                case PungentSpatialVerticalBehavior.ExplicitMinMax:
                    worldMinY = Mathf.Min(ActiveMinY, ActiveMaxY);
                    worldMaxY = Mathf.Max(ActiveMinY, ActiveMaxY);
                    finite = true;
                    break;
                case PungentSpatialVerticalBehavior.Infinite:
                    worldMinY = float.NegativeInfinity;
                    worldMaxY = float.PositiveInfinity;
                    finite = false;
                    break;
                default:
                    worldMinY = transform.position.y;
                    worldMaxY = transform.position.y + Mathf.Max(0.01f, ActiveHeight);
                    finite = true;
                    break;
            }
        }
    }
}
