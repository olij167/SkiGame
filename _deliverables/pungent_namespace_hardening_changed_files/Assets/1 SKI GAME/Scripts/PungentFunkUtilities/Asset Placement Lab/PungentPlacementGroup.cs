using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    [DisallowMultipleComponent]
    public sealed class PungentPlacementGroup : MonoBehaviour
    {
        public string groupId;
        public string displayName = "Placement Group";
        public string moduleId;
        public int seed;
        public PungentPlacementAssetSetSO assetSet;
        public PungentPlacementRuleSetSO ruleSet;
        public bool locked;

        public int CountChildren()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).GetComponent<PungentPlacedAssetMarker>() != null)
                    count++;
            }

            return count;
        }
    }

}