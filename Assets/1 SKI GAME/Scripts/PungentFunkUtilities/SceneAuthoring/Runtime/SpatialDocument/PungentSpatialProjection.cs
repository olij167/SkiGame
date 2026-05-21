using System;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSpatialProjectionKind
    {
        TopDownXZ,
        TopDownXY,
        TopDownYZ,
        IsometricPreview,
        SideElevation,
        CustomPlane
    }

    [Serializable]
    public struct PungentSpatialProjection
    {
        public PungentSpatialProjectionKind mode;
        public Vector3 origin;
        public Vector2 worldMin;
        public Vector2 worldMax;
        public float yawDegrees;

        public static PungentSpatialProjection DefaultTopDownXZ => new PungentSpatialProjection
        {
            mode = PungentSpatialProjectionKind.TopDownXZ,
            origin = Vector3.zero,
            worldMin = new Vector2(-50f, -50f),
            worldMax = new Vector2(50f, 50f),
            yawDegrees = 0f
        };

        public bool IsImplemented => mode == PungentSpatialProjectionKind.TopDownXZ;
        public Vector2 Size => new Vector2(Mathf.Abs(worldMax.x - worldMin.x), Mathf.Abs(worldMax.y - worldMin.y));
        public Vector2 Center2D => (worldMin + worldMax) * 0.5f;
        public bool IsValid => IsImplemented && Size.x > 0.001f && Size.y > 0.001f;

        public Quaternion Rotation
        {
            get
            {
                switch (mode)
                {
                    case PungentSpatialProjectionKind.TopDownXZ:
                        return Quaternion.Euler(0f, yawDegrees, 0f);
                    default:
                        return Quaternion.identity;
                }
            }
        }

        public void Normalize()
        {
            if (mode != PungentSpatialProjectionKind.TopDownXZ)
                return;

            if (worldMax.x < worldMin.x)
            {
                float swap = worldMin.x;
                worldMin.x = worldMax.x;
                worldMax.x = swap;
            }

            if (worldMax.y < worldMin.y)
            {
                float swap = worldMin.y;
                worldMin.y = worldMax.y;
                worldMax.y = swap;
            }

            if (Size.x <= 0.001f)
                worldMax.x = worldMin.x + 1f;
            if (Size.y <= 0.001f)
                worldMax.y = worldMin.y + 1f;
        }

        public bool TryWorldToNormalized(Vector3 world, out Vector2 normalized)
        {
            normalized = default;
            if (!IsValid)
                return false;

            Vector3 local = Quaternion.Inverse(Rotation) * (world - origin);
            Vector2 size = Size;
            normalized = new Vector2(
                Mathf.InverseLerp(worldMin.x, worldMax.x, local.x),
                Mathf.InverseLerp(worldMin.y, worldMax.y, local.z));
            return true;
        }

        public bool TryNormalizedToWorld(Vector2 normalized, float worldY, out Vector3 world)
        {
            world = default;
            if (!IsValid)
                return false;

            Vector3 local = new Vector3(
                Mathf.Lerp(worldMin.x, worldMax.x, normalized.x),
                worldY - origin.y,
                Mathf.Lerp(worldMin.y, worldMax.y, normalized.y));
            world = origin + Rotation * local;
            return true;
        }

        public Vector3 NormalizedToWorldClamped(Vector2 normalized, float worldY = 0f)
        {
            TryNormalizedToWorld(new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y)), worldY, out Vector3 world);
            return world;
        }

        public Bounds GetWorldBounds(float minY = 0f, float maxY = 0f)
        {
            Normalize();
            if (!IsValid)
                return new Bounds(origin, Vector3.zero);

            Vector3 a = NormalizedToWorldClamped(Vector2.zero, minY);
            Vector3 b = NormalizedToWorldClamped(Vector2.right, minY);
            Vector3 c = NormalizedToWorldClamped(Vector2.one, maxY);
            Vector3 d = NormalizedToWorldClamped(Vector2.up, maxY);
            Bounds bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(b);
            bounds.Encapsulate(c);
            bounds.Encapsulate(d);
            return bounds;
        }
    }
}
