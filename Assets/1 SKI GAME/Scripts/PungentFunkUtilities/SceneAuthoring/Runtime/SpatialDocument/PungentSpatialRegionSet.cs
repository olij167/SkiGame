using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [Serializable]
    public struct PungentSpatialRegionVertex
    {
        public string id;
        public Vector2 normalizedPosition;

        public PungentSpatialRegionVertex(string id, Vector2 normalizedPosition)
        {
            this.id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            this.normalizedPosition = normalizedPosition;
        }
    }

    [Serializable]
    public sealed class PungentSpatialRegionFace
    {
        public string stableId;
        public string displayName = "Region";
        public Color color = new Color(0.2f, 0.7f, 1f, 0.24f);
        public List<int> outerLoop = new List<int>();
        public List<PungentSpatialRegionHole> holes = new List<PungentSpatialRegionHole>();
        public bool visible = true;
        public bool locked;

        public string StableId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(stableId))
                    stableId = Guid.NewGuid().ToString("N");
                return stableId;
            }
        }
    }

    [Serializable]
    public sealed class PungentSpatialRegionHole
    {
        public List<int> loop = new List<int>();
    }

    [Serializable]
    public sealed class PungentSpatialRegionSet
    {
        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public bool visible = true;
        public bool locked;
        public List<PungentSpatialRegionVertex> vertices = new List<PungentSpatialRegionVertex>();
        public List<PungentSpatialRegionFace> faces = new List<PungentSpatialRegionFace>();
        public int rootFaceIndex;

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
            if (vertices == null)
                vertices = new List<PungentSpatialRegionVertex>();
            if (faces == null)
                faces = new List<PungentSpatialRegionFace>();
            rootFaceIndex = Mathf.Clamp(rootFaceIndex, 0, Mathf.Max(0, faces.Count - 1));
        }

        public void ResetToSingleFace()
        {
            vertices = new List<PungentSpatialRegionVertex>
            {
                new PungentSpatialRegionVertex(null, new Vector2(0f, 0f)),
                new PungentSpatialRegionVertex(null, new Vector2(0f, 1f)),
                new PungentSpatialRegionVertex(null, new Vector2(1f, 1f)),
                new PungentSpatialRegionVertex(null, new Vector2(1f, 0f))
            };

            faces = new List<PungentSpatialRegionFace>
            {
                new PungentSpatialRegionFace
                {
                    displayName = "Region",
                    outerLoop = new List<int> { 0, 1, 2, 3 }
                }
            };
            rootFaceIndex = 0;
        }
    }
}
