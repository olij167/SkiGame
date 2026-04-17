using UnityEngine;
using SkiGame.Map.UI;

namespace SkiGame.POI
{
    [DisallowMultipleComponent]
    public sealed class MapPOIAnchor : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "POI";
        [SerializeField] private string customId = "";
        [SerializeField] private POICategory category = POICategory.Custom;
        [SerializeField] private bool overrideLabelDisplayMode = false;
        [SerializeField] private MapUIStyleSettings.MapLabelDisplayMode labelDisplayModeOverride = MapUIStyleSettings.MapLabelDisplayMode.Contextual;

        [Header("Visuals")]
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Sprite markerSprite;
        [SerializeField, Min(0.1f)] private float markerSizeMultiplier = 1f;

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
        public bool OverrideLabelDisplayMode => overrideLabelDisplayMode;
        public MapUIStyleSettings.MapLabelDisplayMode LabelDisplayModeOverride => labelDisplayModeOverride;
        public Color Color => color;
        public Sprite MarkerSprite => markerSprite;
        public float MarkerSizeMultiplier => Mathf.Max(0.1f, markerSizeMultiplier);
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
