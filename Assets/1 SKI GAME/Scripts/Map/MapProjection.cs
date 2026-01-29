using System;
using UnityEngine;

namespace SkiGame.Map
{
    /// <summary>
    /// Defines how world-space positions are projected into 2D map-space.
    /// Convention: map plane is XZ. Y is ignored for projection.
    /// </summary>
    [Serializable]
    public struct MapProjection
    {
        [Header("World Bounds (XZ)")]
        public Vector2 worldMinXZ;
        public Vector2 worldMaxXZ;

        [Header("Optional Rotation (Yaw)")]
        [Tooltip("If enabled, world positions are rotated around worldOrigin before projection.")]
        public bool useRotation;

        [Tooltip("Yaw degrees applied when projecting world -> map. Useful to align the mountain in UI.")]
        public float rotationYawDeg;

        [Tooltip("World-space origin used as pivot for rotation. If not set, bounds center is used.")]
        public Vector3 worldOrigin;

        public Vector2 WorldSizeXZ
        {
            get
            {
                Vector2 s = worldMaxXZ - worldMinXZ;
                // Prevent divide-by-zero / invalid projections.
                s.x = Mathf.Max(0.0001f, s.x);
                s.y = Mathf.Max(0.0001f, s.y);
                return s;
            }
        }

        public Vector2 WorldCenterXZ => 0.5f * (worldMinXZ + worldMaxXZ);

        /// <summary>
        /// True if bounds are sane (min < max).
        /// </summary>
        public bool IsValid
        {
            get
            {
                return worldMaxXZ.x > worldMinXZ.x + 0.0001f
                    && worldMaxXZ.y > worldMinXZ.y + 0.0001f;
            }
        }

        /// <summary>
        /// Projects a world position into normalized map coordinates (0..1).
        /// </summary>
        public Vector2 WorldToNormalized(Vector3 worldPos)
        {
            Vector3 p = worldPos;

            if (useRotation)
            {
                Vector3 pivot = (worldOrigin != Vector3.zero) ? worldOrigin : new Vector3(WorldCenterXZ.x, 0f, WorldCenterXZ.y);
                p = RotateAroundPivotYaw(p, pivot, rotationYawDeg);
            }

            Vector2 xz = new Vector2(p.x, p.z);
            Vector2 size = WorldSizeXZ;

            float u = (xz.x - worldMinXZ.x) / size.x;
            float v = (xz.y - worldMinXZ.y) / size.y;

            return new Vector2(u, v);
        }

        /// <summary>
        /// Converts a normalized map coordinate (0..1) back to a world position on the XZ plane.
        /// Y is passed through as a parameter.
        /// </summary>
        public Vector3 NormalizedToWorld(Vector2 uv, float y = 0f)
        {
            Vector2 size = WorldSizeXZ;

            Vector2 xz = new Vector2(
                worldMinXZ.x + uv.x * size.x,
                worldMinXZ.y + uv.y * size.y
            );

            Vector3 p = new Vector3(xz.x, y, xz.y);

            if (useRotation)
            {
                // Inverse rotation for map->world.
                Vector3 pivot = (worldOrigin != Vector3.zero) ? worldOrigin : new Vector3(WorldCenterXZ.x, 0f, WorldCenterXZ.y);
                p = RotateAroundPivotYaw(p, pivot, -rotationYawDeg);
            }

            return p;
        }

        private static Vector3 RotateAroundPivotYaw(Vector3 point, Vector3 pivot, float yawDeg)
        {
            Quaternion r = Quaternion.Euler(0f, yawDeg, 0f);
            Vector3 dir = point - pivot;
            dir = r * dir;
            return pivot + dir;
        }
    }
}
