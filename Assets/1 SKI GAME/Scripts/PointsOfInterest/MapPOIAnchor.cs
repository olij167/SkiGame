using UnityEngine;

namespace SkiGame.POI
{
    [DisallowMultipleComponent]
    public sealed class MapPOIAnchor : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "POI";
        [SerializeField] private string customId = "";
        [SerializeField] private POICategory category = POICategory.Custom;
        [SerializeField] private bool alwaysShowLabel = false;

        [Header("Visuals")]
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("Anchor")]
        [SerializeField] private Transform anchorOverride;

        [Header("Metadata")]
        [TextArea]
        [SerializeField] private string meta = "";

        [Header("Bake")]
        [SerializeField] private bool includeInMapBake = true;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName.Trim();
        public string CustomId => customId;
        public POICategory Category => category;
        public bool AlwaysShowLabel => alwaysShowLabel;
        public Color Color => color;
        public string Meta => meta ?? string.Empty;
        public bool IncludeInMapBake => includeInMapBake;

        public Vector3 WorldPosition
        {
            get
            {
                if (anchorOverride != null)
                    return anchorOverride.position;

                return transform.position;
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            displayName = gameObject.name;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = color;
            Vector3 pos = WorldPosition;
            Gizmos.DrawSphere(pos, 1.0f);
            Gizmos.DrawWireSphere(pos, 1.5f);
        }
#endif
    }
}