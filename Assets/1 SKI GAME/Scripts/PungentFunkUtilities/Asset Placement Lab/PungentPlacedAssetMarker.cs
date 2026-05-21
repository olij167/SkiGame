using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    [DisallowMultipleComponent]
    public sealed class PungentPlacedAssetMarker : MonoBehaviour
    {
        public string groupId;
        public string moduleId;
        public string presetId;
        public int seed;
        public GameObject sourcePrefab;
        public Bounds originalEstimatedBounds;
        public bool locked;
        public string generatedAtUtc;
        public string notes;
    }

}