using UnityEngine;

namespace UnityArrayModifier
{
    [AddComponentMenu("")]
    public class DuplicateBehaviour : MonoBehaviour
    {
        public int id;
        public ArrayModifier arrayModifier;

        public bool IsOld => id != arrayModifier.Id;

        public void Initialize(int id, ArrayModifier arrayModifier)
        {
            this.id = id;
            this.arrayModifier = arrayModifier;
        }
    }
}