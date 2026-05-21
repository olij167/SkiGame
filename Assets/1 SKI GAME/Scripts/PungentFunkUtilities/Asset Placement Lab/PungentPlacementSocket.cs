using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    [DisallowMultipleComponent]
    public sealed class PungentPlacementSocket : MonoBehaviour
    {
        public string socketId = "Socket";
        public string category = "Default";
        public string[] acceptsCategories = { "Default" };
        public Vector2 size = Vector2.one;
        public bool occupied;
        public bool allowRotationMatching = true;
        public bool allowMirroring;
        [Range(0f, 10f)] public float priority = 1f;
        public Color gizmoColor = new Color(0.2f, 0.85f, 1f, 0.75f);

        public bool Accepts(PungentPlacementSocket other)
        {
            if (other == null)
                return false;

            if (acceptsCategories == null || acceptsCategories.Length == 0)
                return string.Equals(category, other.category, System.StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < acceptsCategories.Length; i++)
            {
                if (string.Equals(acceptsCategories[i], other.category, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 0.04f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.75f);
            Gizmos.matrix = previous;
        }
    }

}