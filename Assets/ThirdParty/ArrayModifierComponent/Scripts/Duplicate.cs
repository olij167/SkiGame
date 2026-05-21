using UnityEngine;

namespace UnityArrayModifier
{
    //[SerializeField]
    public class Duplicate
    {
        public Vector3 position;
        public Quaternion rotation;
        public HideFlags hideFlags;
        public GameObject gameObject;

        public Duplicate(Vector3 position, Quaternion rotation, HideFlags hideFlags)
        {
            this.position = position;
            this.rotation = rotation;
            this.hideFlags = hideFlags;
        }

        public Duplicate(Duplicate rhs)
        {
            this.position = rhs.position;
            this.rotation = rhs.rotation;
            this.hideFlags = rhs.hideFlags;
        }
    }
}