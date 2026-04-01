using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Map
{
    [Serializable]
    public sealed class SharedMapVertex
    {
        public string id;
        public Vector2 uv;
    }

    [Serializable]
    public sealed class MapRegionLoop
    {
        public List<string> vertexIds = new List<string>();

        public bool IsValid => vertexIds != null && vertexIds.Count >= 3;
    }

    [Serializable]
    public sealed class MapRegionFace
    {
        public string id;
        public string displayName;
        public Color fillColor = new Color(0.2f, 0.7f, 1f, 0.18f);
        public Color borderColor = new Color(0.2f, 0.7f, 1f, 0.9f);
        public Vector2 labelAnchorUv = new Vector2(0.5f, 0.5f);

        [Tooltip("Outer boundary loop of this face.")]
        public List<string> outerVertexIds = new List<string>();

        [Tooltip("Optional hole loops carved out of this face.")]
        public List<MapRegionLoop> holeLoops = new List<MapRegionLoop>();

        public bool IsValid => !string.IsNullOrWhiteSpace(id) && outerVertexIds != null && outerVertexIds.Count >= 3;
    }

    [CreateAssetMenu(menuName = "SkiGame/Map/Map Region Set", fileName = "MapRegionSet")]
    public sealed class MapRegionSet : ScriptableObject
    {
        public const string RootFaceId = "root_face";

        [SerializeField] private MapData mapData;
        [SerializeField] private List<SharedMapVertex> vertices = new List<SharedMapVertex>();
        [SerializeField] private List<MapRegionFace> faces = new List<MapRegionFace>();

        public MapData MapData => mapData;
        public List<SharedMapVertex> Vertices => vertices;
        public List<MapRegionFace> Faces => faces;

        public void SetMapData(MapData value) => mapData = value;

        public void EnsureInitialized()
        {
            if (vertices == null)
                vertices = new List<SharedMapVertex>();

            if (faces == null)
                faces = new List<MapRegionFace>();

            if (faces.Count > 0 && vertices.Count > 0)
                return;

            ResetToSingleFace();
        }

        public void ResetToSingleFace()
        {
            vertices = new List<SharedMapVertex>();
            faces = new List<MapRegionFace>();

            string v0 = CreateVertex(new Vector2(0f, 0f));
            string v1 = CreateVertex(new Vector2(1f, 0f));
            string v2 = CreateVertex(new Vector2(1f, 1f));
            string v3 = CreateVertex(new Vector2(0f, 1f));

            MapRegionFace root = new MapRegionFace
            {
                id = RootFaceId,
                displayName = "Mountain",
                fillColor = new Color(0.15f, 0.45f, 1f, 0.12f),
                borderColor = new Color(0.85f, 0.92f, 1f, 1f),
                labelAnchorUv = new Vector2(0.5f, 0.5f),
                outerVertexIds = new List<string> { v0, v1, v2, v3 },
                holeLoops = new List<MapRegionLoop>()
            };

            faces.Add(root);
        }

        public SharedMapVertex GetVertexById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (v != null && string.Equals(v.id, id, StringComparison.Ordinal))
                    return v;
            }

            return null;
        }

        public bool TryGetVertexUv(string id, out Vector2 uv)
        {
            var v = GetVertexById(id);
            if (v != null)
            {
                uv = v.uv;
                return true;
            }

            uv = Vector2.zero;
            return false;
        }

        public MapRegionFace GetFaceById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < faces.Count; i++)
            {
                var f = faces[i];
                if (f != null && string.Equals(f.id, id, StringComparison.Ordinal))
                    return f;
            }

            return null;
        }

        public string CreateVertex(Vector2 uv)
        {
            SharedMapVertex v = new SharedMapVertex
            {
                id = Guid.NewGuid().ToString("N"),
                uv = Clamp01(uv)
            };

            vertices.Add(v);
            return v.id;
        }

        public MapRegionFace CreateFace(string name, Color fill, Color border)
        {
            MapRegionFace face = new MapRegionFace
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = string.IsNullOrWhiteSpace(name) ? $"Region {faces.Count + 1}" : name,
                fillColor = fill,
                borderColor = border,
                labelAnchorUv = new Vector2(0.5f, 0.5f),
                outerVertexIds = new List<string>(),
                holeLoops = new List<MapRegionLoop>()
            };

            faces.Add(face);
            return face;
        }

        public void RemoveFaceById(string faceId)
        {
            for (int i = faces.Count - 1; i >= 0; i--)
            {
                if (faces[i] != null && string.Equals(faces[i].id, faceId, StringComparison.Ordinal))
                {
                    faces.RemoveAt(i);
                    break;
                }
            }
        }

        public void RemoveUnusedVertices()
        {
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                if (face == null)
                    continue;

                CollectUsed(face.outerVertexIds, used);

                if (face.holeLoops != null)
                {
                    for (int h = 0; h < face.holeLoops.Count; h++)
                    {
                        var hole = face.holeLoops[h];
                        if (hole == null)
                            continue;

                        CollectUsed(hole.vertexIds, used);
                    }
                }
            }

            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                var v = vertices[i];
                if (v == null || !used.Contains(v.id))
                    vertices.RemoveAt(i);
            }
        }

        private static void CollectUsed(List<string> ids, HashSet<string> used)
        {
            if (ids == null)
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (!string.IsNullOrWhiteSpace(id))
                    used.Add(id);
            }
        }

        private static Vector2 Clamp01(Vector2 v)
        {
            v.x = Mathf.Clamp01(v.x);
            v.y = Mathf.Clamp01(v.y);
            return v;
        }
    }
}