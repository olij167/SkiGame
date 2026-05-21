using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public struct PungentAreaBlendWeight
    {
        public Component Area;
        public float Weight;
        public int Priority;
        public string LayerOrProfile;
    }

    public struct PungentNearestPathPointResult
    {
        public Component Path;
        public PungentPathProjectionResult Projection;
    }

    public static class PungentSpatialQueryService
    {
        private static readonly List<Component> ScratchAreas = new List<Component>();
        private static readonly List<Component> ScratchPaths = new List<Component>();

        public static List<Component> GetAreasAtPosition(Vector3 worldPosition)
        {
            List<Component> results = new List<Component>();
            GetAreasAtPositionNonAlloc(worldPosition, results, null);
            return results;
        }

        public static int GetAreasAtPositionNonAlloc(Vector3 worldPosition, IList<Component> results, string layerOrType = null)
        {
            if (results == null)
                return 0;

            results.Clear();
            ScratchAreas.Clear();
            PungentSpatialRuntimeRegistry.CollectAreaProviders(ScratchAreas);
            for (int i = 0; i < ScratchAreas.Count; i++)
            {
                Component area = ScratchAreas[i];
                if (!IsRuntimeEnabled(area) || !MatchesLayerOrProfile(area, layerOrType))
                    continue;

                if (PungentSpatialRuntimeQueryUtility.TryContainsAreaPoint(area, worldPosition, out bool contains) && contains)
                    results.Add(area);
            }

            return results.Count;
        }

        public static Component GetHighestPriorityArea(Vector3 worldPosition, string layerOrType = null)
        {
            ScratchAreas.Clear();
            GetAreasAtPositionNonAlloc(worldPosition, ScratchAreas, layerOrType);
            Component best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < ScratchAreas.Count; i++)
            {
                Component area = ScratchAreas[i];
                int priority = GetAreaPriority(area);
                if (best != null && priority < bestPriority)
                    continue;

                best = area;
                bestPriority = priority;
            }

            return best;
        }

        public static List<PungentAreaBlendWeight> GetAreaBlendWeights(Vector3 worldPosition, string layerOrType = null)
        {
            List<PungentAreaBlendWeight> results = new List<PungentAreaBlendWeight>();
            GetAreaBlendWeightsNonAlloc(worldPosition, layerOrType, results);
            return results;
        }

        public static int GetAreaBlendWeightsNonAlloc(Vector3 worldPosition, string layerOrType, IList<PungentAreaBlendWeight> results)
        {
            if (results == null)
                return 0;

            results.Clear();
            ScratchAreas.Clear();
            GetAreasAtPositionNonAlloc(worldPosition, ScratchAreas, layerOrType);

            float total = 0f;
            for (int i = 0; i < ScratchAreas.Count; i++)
            {
                Component area = ScratchAreas[i];
                int priority = GetAreaPriority(area);
                float rawWeight = Mathf.Max(1f, priority + 1f);
                total += rawWeight;
                results.Add(new PungentAreaBlendWeight
                {
                    Area = area,
                    Priority = priority,
                    Weight = rawWeight,
                    LayerOrProfile = GetLayerOrProfileLabel(area)
                });
            }

            if (total > 0.0001f)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    PungentAreaBlendWeight weight = results[i];
                    weight.Weight /= total;
                    results[i] = weight;
                }
            }

            return results.Count;
        }

        public static List<Component> FindAreasByTag(string tag)
        {
            List<Component> results = new List<Component>();
            FindAreasByTagNonAlloc(tag, results);
            return results;
        }

        public static int FindAreasByTagNonAlloc(string tag, IList<Component> results)
        {
            return FindByTag(tag, results, wantAreas: true);
        }

        public static List<Component> FindPathsByTag(string tag)
        {
            List<Component> results = new List<Component>();
            FindPathsByTagNonAlloc(tag, results);
            return results;
        }

        public static int FindPathsByTagNonAlloc(string tag, IList<Component> results)
        {
            return FindByTag(tag, results, wantAreas: false);
        }

        public static bool GetNearestPathPoint(Vector3 worldPosition, out PungentNearestPathPointResult result)
        {
            result = default;
            ScratchPaths.Clear();
            PungentSpatialRuntimeRegistry.CollectPathProviders(ScratchPaths);

            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < ScratchPaths.Count; i++)
            {
                Component path = ScratchPaths[i];
                if (!IsRuntimeEnabled(path) || !(path is IPungentPathQueryProvider queryProvider))
                    continue;

                if (!queryProvider.TryProjectPoint(worldPosition, out PungentPathProjectionResult projection))
                    continue;

                if (projection.SqrDistanceToInput >= bestSqr)
                    continue;

                bestSqr = projection.SqrDistanceToInput;
                result = new PungentNearestPathPointResult
                {
                    Path = path,
                    Projection = projection
                };
            }

            return result.Path != null;
        }

        public static bool SamplePathByDistance(string pathId, float distance, out PungentPathSample sample)
        {
            sample = default;
            Component path = ResolvePath(pathId);
            if (path == null || !(path is IPungentPathQueryProvider queryProvider))
                return false;

            float normalized = DistanceToNormalized(path, distance);
            return queryProvider.TrySamplePath(normalized, out sample);
        }

        public static bool SamplePathByNormalizedDistance(string pathId, float t, out PungentPathSample sample)
        {
            sample = default;
            Component path = ResolvePath(pathId);
            return path is IPungentPathQueryProvider queryProvider &&
                   queryProvider.TrySamplePath(Mathf.Clamp01(t), out sample);
        }

        public static bool GetPathTangent(string pathId, float distanceOrT, out Vector3 tangent)
        {
            tangent = Vector3.forward;
            Component path = ResolvePath(pathId);
            if (path == null || !(path is IPungentPathQueryProvider queryProvider))
                return false;

            float normalized = distanceOrT <= 1f ? Mathf.Clamp01(distanceOrT) : DistanceToNormalized(path, distanceOrT);
            if (!queryProvider.TrySamplePath(normalized, out PungentPathSample sample))
                return false;

            tangent = sample.Tangent;
            return true;
        }

        public static bool GetPathWidth(string pathId, float distanceOrT, out float width)
        {
            width = 0f;
            Component path = ResolvePath(pathId);
            if (path == null || !(path is IPungentCorridorWidthProvider widthProvider))
                return false;

            float normalized = distanceOrT <= 1f ? Mathf.Clamp01(distanceOrT) : DistanceToNormalized(path, distanceOrT);
            return widthProvider.TryGetWidthAt(normalized, out width);
        }

        private static int FindByTag(string tag, IList<Component> results, bool wantAreas)
        {
            if (results == null)
                return 0;

            results.Clear();
            string normalized = PungentSpatialTag.Normalize(tag);
            if (string.IsNullOrWhiteSpace(normalized))
                return 0;

            List<Component> scratch = wantAreas ? ScratchAreas : ScratchPaths;
            scratch.Clear();
            if (wantAreas)
                PungentSpatialRuntimeRegistry.CollectAreaProviders(scratch);
            else
                PungentSpatialRuntimeRegistry.CollectPathProviders(scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                Component component = scratch[i];
                if (!IsRuntimeEnabled(component))
                    continue;
                if (HasTag(component, normalized))
                    results.Add(component);
            }

            return results.Count;
        }

        private static Component ResolvePath(string pathId)
        {
            if (string.IsNullOrWhiteSpace(pathId))
                return null;

            ScratchPaths.Clear();
            PungentSpatialRuntimeRegistry.CollectPathProviders(ScratchPaths);
            for (int i = 0; i < ScratchPaths.Count; i++)
            {
                Component path = ScratchPaths[i];
                if (!IsRuntimeEnabled(path))
                    continue;
                if (MatchesSpatialId(path, pathId))
                    return path;
            }

            return null;
        }

        private static bool MatchesSpatialId(Component component, string id)
        {
            if (component == null || string.IsNullOrWhiteSpace(id))
                return false;

            if (component is IPungentSpatialObjectMetadataProvider metadataProvider &&
                metadataProvider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) &&
                metadata != null &&
                string.Equals(metadata.StableId, id, StringComparison.OrdinalIgnoreCase))
                return true;

            if (component is IPungentPathMetadataProvider pathProvider &&
                pathProvider.TryGetPathMetadata(out PungentPathMetadata pathMetadata) &&
                string.Equals(pathMetadata.PathId, id, StringComparison.OrdinalIgnoreCase))
                return true;

            if (component is IPungentSpatialLabelProvider labelProvider &&
                string.Equals(labelProvider.SpatialId, id, StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(component.name, id, StringComparison.OrdinalIgnoreCase);
        }

        private static float DistanceToNormalized(Component path, float distance)
        {
            if (path is IPungentPathMetadataProvider metadataProvider &&
                metadataProvider.TryGetPathMetadata(out PungentPathMetadata metadata) &&
                metadata.TotalLength > 0.0001f)
                return Mathf.Clamp01(distance / metadata.TotalLength);

            return Mathf.Clamp01(distance);
        }

        private static bool IsRuntimeEnabled(Component component)
        {
            if (component is IPungentSpatialObjectMetadataProvider provider &&
                provider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) &&
                metadata != null)
                return metadata.runtimeEnabled;

            return true;
        }

        private static bool MatchesLayerOrProfile(Component component, string layerOrProfile)
        {
            if (string.IsNullOrWhiteSpace(layerOrProfile))
                return true;

            if (component is IPungentSpatialObjectMetadataProvider provider &&
                provider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) &&
                metadata != null &&
                metadata.MatchesLayerOrProfile(layerOrProfile))
                return true;

            return HasTag(component, layerOrProfile);
        }

        private static bool HasTag(Component component, string tag)
        {
            if (component is IPungentSpatialObjectMetadataProvider provider &&
                provider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) &&
                metadata != null &&
                metadata.HasTag(tag))
                return true;

            if (component is IPungentSpatialLabelProvider labelProvider && labelProvider.SpatialTags != null)
            {
                IReadOnlyList<string> tags = labelProvider.SpatialTags;
                for (int i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static int GetAreaPriority(Component area)
        {
            return area is IPungentAreaPriorityProvider priorityProvider ? priorityProvider.AreaPriority : 0;
        }

        private static string GetLayerOrProfileLabel(Component component)
        {
            if (component is IPungentSpatialObjectMetadataProvider provider &&
                provider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) &&
                metadata != null)
            {
                if (!string.IsNullOrWhiteSpace(metadata.layer.Name))
                    return metadata.layer.Name;
                return metadata.semanticProfile.ToString();
            }

            return string.Empty;
        }
    }
}
