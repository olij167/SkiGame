using System;
using System.Collections.Generic;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal sealed class PungentSpatialProviderCache
    {
        public sealed class Record
        {
            public string key;
            public string stableId;
            public string displayName;
            public PungentSpatialItemKind kind;
            public UnityEngine.Object sourceObject;
            public Component sourceComponent;
            public PungentSpatialAuthoringAsset sourceAsset;
            public int itemIndex = -1;
            public int childIndex = -1;
            public bool visible = true;
            public bool locked;
            public bool selected;
            public int warningCount;
            public int estimatedDrawOperations;
            public int pointCount;
            public string layer;
            public string category;
            public string searchText;
            public Color color = Color.white;
            public Bounds bounds;
        }

        private readonly List<Record> _records = new List<Record>();
        private bool _dirty = true;
        private double _lastRebuildTime;

        public IReadOnlyList<Record> Records => _records;
        public int WarningCount { get; private set; }
        public int DrawCost { get; private set; }
        public double LastRebuildTime => _lastRebuildTime;

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void RebuildIfDirty(PungentSpatialAuthoringAsset activeAsset)
        {
            if (!_dirty)
                return;

            Rebuild(activeAsset);
        }

        public void Rebuild(PungentSpatialAuthoringAsset activeAsset)
        {
            _dirty = false;
            _records.Clear();
            WarningCount = 0;
            DrawCost = 0;
            _lastRebuildTime = EditorApplication.timeSinceStartup;

            if (activeAsset != null)
                CollectAsset(activeAsset);

            CollectOpenSceneComponents();

            for (int i = 0; i < _records.Count; i++)
            {
                WarningCount += Mathf.Max(0, _records[i].warningCount);
                DrawCost += Mathf.Max(0, _records[i].estimatedDrawOperations);
            }
        }

        private void CollectAsset(PungentSpatialAuthoringAsset asset)
        {
            asset.Normalize();
            for (int i = 0; i < asset.SpatialItemCount; i++)
            {
                if (!asset.TryGetSpatialItem(i, out PungentSpatialItemSnapshot item))
                    continue;

                Record record = FromSnapshot(item, asset, null, i);
                record.key = $"asset:{asset.GetInstanceID()}:{i}:{item.Kind}:{item.StableId}";
                record.sourceAsset = asset;
                _records.Add(record);
            }

            if (asset.regionSets != null)
            {
                for (int setIndex = 0; setIndex < asset.regionSets.Count; setIndex++)
                {
                    PungentSpatialRegionSet set = asset.regionSets[setIndex];
                    if (set == null || set.faces == null)
                        continue;

                    for (int faceIndex = 0; faceIndex < set.faces.Count; faceIndex++)
                    {
                        PungentSpatialRegionFace face = set.faces[faceIndex];
                        if (face == null)
                            continue;

                        _records.Add(new Record
                        {
                            key = $"asset:{asset.GetInstanceID()}:region-face:{setIndex}:{faceIndex}:{face.StableId}",
                            stableId = face.StableId,
                            displayName = string.IsNullOrWhiteSpace(face.displayName) ? $"Region Face {faceIndex + 1}" : face.displayName,
                            kind = PungentSpatialItemKind.RegionFace,
                            sourceObject = asset,
                            sourceAsset = asset,
                            itemIndex = setIndex,
                            childIndex = faceIndex,
                            visible = face.visible,
                            locked = face.locked,
                            warningCount = face.outerLoop == null || face.outerLoop.Count < 3 ? 1 : 0,
                            estimatedDrawOperations = face.outerLoop == null ? 0 : face.outerLoop.Count * 2,
                            pointCount = face.outerLoop == null ? 0 : face.outerLoop.Count,
                            layer = set.Metadata.layer.Name,
                            category = "Region Face",
                            color = face.color,
                            bounds = asset.projection.GetWorldBounds(),
                            searchText = BuildSearchText(face.StableId, face.displayName, "Region Face", set.Metadata.layer.Name)
                        });
                    }
                }
            }
        }

        private void CollectOpenSceneComponents()
        {
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (!IsOpenSceneComponent(behaviour))
                    continue;

                if (behaviour is PungentModularSpatialOutput output)
                    AddGeneratedOutputRecord(output);

                IPungentPathPointProvider pathProvider = behaviour as IPungentPathPointProvider;
                if (pathProvider != null || behaviour is IPungentPathQueryProvider)
                    AddPathRecord(behaviour, pathProvider);

                if (behaviour is IPungentAreaShapeProvider || behaviour is IPungentAreaVolumeProvider)
                    AddAreaRecord(behaviour);
            }
        }

        private void AddPathRecord(Component component, IPungentPathPointProvider pathProvider)
        {
            string id = component.GetInstanceID().ToString();
            string displayName = component.name;
            Color color = Color.cyan;
            string layer = "Scene";
            string category = "Path";
            int points = pathProvider == null ? 0 : pathProvider.PointCount;
            Bounds bounds = new Bounds(component.transform.position, Vector3.one);
            int warnings = points < 2 ? 1 : 0;

            if (component is IPungentPathMetadataProvider metadataProvider && metadataProvider.TryGetPathMetadata(out PungentPathMetadata metadata))
            {
                id = string.IsNullOrWhiteSpace(metadata.PathId) ? id : metadata.PathId;
                displayName = string.IsNullOrWhiteSpace(metadata.DisplayName) ? displayName : metadata.DisplayName;
                color = metadata.Color;
                category = metadata.ClosedLoop ? "Closed Path" : "Path";
                points = metadata.ControlPointCount;
            }

            if (component is IPungentSpatialLabelProvider labels)
            {
                id = string.IsNullOrWhiteSpace(labels.SpatialId) ? id : labels.SpatialId;
                displayName = string.IsNullOrWhiteSpace(labels.SpatialDisplayName) ? displayName : labels.SpatialDisplayName;
                color = labels.SpatialColor;
            }

            if (component is IPungentSpatialBoundsProvider boundsProvider)
                boundsProvider.TryGetSpatialBounds(out bounds);

            _records.Add(new Record
            {
                key = $"component:{component.GetInstanceID()}:path",
                stableId = id,
                displayName = displayName,
                kind = PungentSpatialItemKind.Path,
                sourceObject = component,
                sourceComponent = component,
                visible = true,
                locked = false,
                warningCount = warnings,
                estimatedDrawOperations = Mathf.Max(1, points * 2),
                pointCount = points,
                layer = layer,
                category = category,
                color = color,
                bounds = bounds,
                searchText = BuildSearchText(id, displayName, category, layer)
            });
        }

        private void AddAreaRecord(Component component)
        {
            string id = component.GetInstanceID().ToString();
            string displayName = component.name;
            Color color = new Color(0.2f, 0.75f, 1f, 0.8f);
            int points = 0;
            int warnings = 0;
            Bounds bounds = new Bounds(component.transform.position, Vector3.one);

            if (component is IPungentSpatialLabelProvider labels)
            {
                id = string.IsNullOrWhiteSpace(labels.SpatialId) ? id : labels.SpatialId;
                displayName = string.IsNullOrWhiteSpace(labels.SpatialDisplayName) ? displayName : labels.SpatialDisplayName;
                color = labels.SpatialColor;
            }

            if (component is IPungentAreaShapeProvider shapeProvider && shapeProvider.TryGetAreaShape(out PungentAreaShape shape))
            {
                points = shape.WorldPolygon == null ? 0 : shape.WorldPolygon.Length;
                warnings = shape.Kind == PungentAreaShapeKind.PolygonXZ && points < 3 ? 1 : 0;
            }

            if (component is IPungentSpatialBoundsProvider boundsProvider)
                boundsProvider.TryGetSpatialBounds(out bounds);

            _records.Add(new Record
            {
                key = $"component:{component.GetInstanceID()}:area",
                stableId = id,
                displayName = displayName,
                kind = PungentSpatialItemKind.Area,
                sourceObject = component,
                sourceComponent = component,
                visible = true,
                locked = false,
                warningCount = warnings,
                estimatedDrawOperations = Mathf.Max(1, points * 2),
                pointCount = points,
                layer = "Scene",
                category = "Area",
                color = color,
                bounds = bounds,
                searchText = BuildSearchText(id, displayName, "Area", "Scene")
            });
        }

        private void AddGeneratedOutputRecord(PungentModularSpatialOutput output)
        {
            if (output == null)
                return;

            _records.Add(new Record
            {
                key = $"component:{output.GetInstanceID()}:generated",
                stableId = output.name,
                displayName = output.name,
                kind = PungentSpatialItemKind.GeneratedOutput,
                sourceObject = output,
                sourceComponent = output,
                visible = true,
                locked = false,
                warningCount = output.source != null ? 0 : 1,
                estimatedDrawOperations = Mathf.Max(1, output.ExistingGeneratedChildCount),
                pointCount = output.ExistingGeneratedChildCount,
                layer = "Generated",
                category = "Spatial Output Recipe",
                color = new Color(1f, 0.8f, 0.25f, 1f),
                bounds = new Bounds(output.transform.position, Vector3.one),
                searchText = BuildSearchText(output.name, output.name, "Spatial Output Recipe", "Generated")
            });
        }

        private static Record FromSnapshot(PungentSpatialItemSnapshot item, PungentSpatialAuthoringAsset asset, Component component, int index)
        {
            return new Record
            {
                stableId = item.StableId,
                displayName = item.DisplayName,
                kind = item.Kind,
                sourceObject = item.SourceObject != null ? item.SourceObject : asset,
                sourceAsset = asset,
                sourceComponent = component,
                itemIndex = index,
                visible = item.Visible,
                locked = item.Locked,
                warningCount = item.WarningCount,
                estimatedDrawOperations = item.EstimatedDrawOperations,
                pointCount = item.PointCount,
                layer = item.Layer,
                category = item.Category,
                color = item.Color,
                bounds = item.Bounds,
                searchText = BuildSearchText(item.StableId, item.DisplayName, item.Category, item.Layer)
            };
        }

        private static bool IsOpenSceneComponent(Component component)
        {
            if (component == null || component.gameObject == null)
                return false;
            if (EditorUtility.IsPersistent(component) || EditorUtility.IsPersistent(component.gameObject))
                return false;

            Scene scene = component.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static string BuildSearchText(string id, string name, string category, string layer)
        {
            return string.Join(" ", id, name, category, layer).ToLowerInvariant();
        }
    }
#endif
}
