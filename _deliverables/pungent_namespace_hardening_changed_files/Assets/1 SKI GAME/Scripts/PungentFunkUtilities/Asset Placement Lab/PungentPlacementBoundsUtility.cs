using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    public static class PungentPlacementBoundsUtility
    {
        public static Bounds CalculateHierarchyBounds(GameObject root, bool includeInactive = true)
        {
            if (root == null)
                return new Bounds(Vector3.zero, Vector3.one);

            bool found = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!found)
                bounds = new Bounds(root.transform.position, Vector3.one);

            return bounds;
        }

        public static Bounds EstimatePrefabWorldBounds(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, PungentPlacementFootprint footprint)
        {
            Bounds local = CalculatePrefabLocalBounds(prefab);
            Vector3 extents = Vector3.Scale(local.extents, Abs(scale));

            if (footprint != null && footprint.useFootprint)
                return footprint.GetWorldBounds(position, rotation, extents);

            Vector3 size = extents * 2f;
            if (footprint != null)
                size += footprint.boundsPadding;

            return new Bounds(position + rotation * Vector3.Scale(local.center, scale), size);
        }

        public static Bounds CalculatePrefabLocalBounds(GameObject prefab)
        {
            if (prefab == null)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds world = CalculateHierarchyBounds(prefab, true);
            Vector3 center = prefab.transform.InverseTransformPoint(world.center);
            Vector3 size = world.size;
            return new Bounds(center, size);
        }

        public static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        public static Bounds EncapsulateSelection(GameObject[] selection)
        {
            bool found = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            if (selection == null || selection.Length == 0)
                return bounds;

            for (int i = 0; i < selection.Length; i++)
            {
                GameObject go = selection[i];
                if (go == null)
                    continue;

                Bounds item = CalculateHierarchyBounds(go, true);
                if (!found)
                {
                    bounds = item;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(item);
                }
            }

            return found ? bounds : bounds;
        }
    }

}