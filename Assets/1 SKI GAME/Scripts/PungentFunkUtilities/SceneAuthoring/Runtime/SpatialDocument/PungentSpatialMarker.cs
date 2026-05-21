using System;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [Serializable]
    public sealed class PungentSpatialMarker
    {
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public bool visible = true;
        public bool locked;
        public Vector3 worldPosition;
        public Vector2 normalizedPosition;
        public bool useProjectedPosition;
        public Color color = new Color(1f, 0.85f, 0.2f, 1f);

        public PungentSpatialObjectMetadata Metadata
        {
            get
            {
                if (metadata == null)
                    metadata = new PungentSpatialObjectMetadata();
                return metadata;
            }
        }

        public void Normalize(string fallbackName)
        {
            Metadata.Normalize(fallbackName);
        }
    }
}
