using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public sealed class PungentBoardComponentSelectionProjectionAdapter : IPungentBoardProjectionAdapter
    {
        public const string Id = "unity-component-selection-import";

        public string AdapterId => Id;
        public string DisplayName => "Unity Selection / Component Import";
        public string Description => "Creates board nodes, reference edges, and optional groups from explicitly selected Unity objects using SerializedObject data only.";

        private sealed class SourceRecord
        {
            public Object unityObject;
            public GameObject gameObject;
            public Component component;
            public string sourceKey = string.Empty;
            public string title = string.Empty;
            public string summary = string.Empty;
            public string styleKey = "freeform";
            public string nodeTypeKey = string.Empty;
            public string groupGameObjectKey = string.Empty;
            public string folderPath = string.Empty;
            public PungentBoardNodeKind nodeKind = PungentBoardNodeKind.FreeformCard;
            public Vector2 position;
            public readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<ObjectReferenceRecord> objectReferences = new List<ObjectReferenceRecord>();
        }

        private sealed class ObjectReferenceRecord
        {
            public Object target;
            public string propertyPath = string.Empty;
            public string displayName = string.Empty;
        }

        public bool CanPreview(PungentBoardProjectionRequest request, out string reason)
        {
            if (request == null || request.document == null)
            {
                reason = "Select a board before previewing Unity selection import.";
                return false;
            }

            if (request.profile == null)
            {
                reason = "Select an integration profile.";
                return false;
            }

            bool hasScope = request.profile.HasScope(PungentBoardProjectionSourceScope.CurrentSelection) ||
                            request.profile.HasScope(PungentBoardProjectionSourceScope.SelectedGameObjectsAndComponents) ||
                            request.profile.HasScope(PungentBoardProjectionSourceScope.ExplicitAssetOrFolderRoots);
            if (!hasScope)
            {
                reason = string.Empty;
                return false;
            }

            bool hasSelection = request.selectionObjects != null && request.selectionObjects.Any(item => item != null);
            bool hasRoots = request.profile.explicitRootPaths != null && request.profile.explicitRootPaths.Any(path => !string.IsNullOrWhiteSpace(path));
            if (!hasSelection && !hasRoots)
            {
                reason = "Unity selection/component import has no selected objects or explicit roots to preview.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public PungentBoardProjectionPreview BuildPreview(PungentBoardProjectionRequest request)
        {
            PungentBoardProjectionPreview preview = new PungentBoardProjectionPreview
            {
                adapterId = AdapterId,
                displayName = DisplayName,
                generatedUtc = DateTime.UtcNow.ToString("o")
            };

            string reason;
            if (!CanPreview(request, out reason))
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    preview.messages.Add(reason);
                return preview;
            }

            PungentBoardDocument document = request.document;
            PungentBoardIntegrationProfile profile = request.profile;
            Dictionary<string, PungentBoardNode> existingNodes = PungentBoardProjectionApplier.BuildExistingNodeSourceLookup(document);
            Dictionary<string, PungentBoardEdge> existingEdges = PungentBoardProjectionApplier.BuildExistingEdgeSourceLookup(document);
            Dictionary<string, PungentBoardGroup> existingGroups = PungentBoardProjectionApplier.BuildExistingGroupSourceLookup(document);
            List<SourceRecord> sources = CollectSources(request, preview);
            Dictionary<Object, string> sourceKeysByObject = sources
                .Where(source => source.unityObject != null && !string.IsNullOrWhiteSpace(source.sourceKey))
                .GroupBy(source => source.unityObject)
                .ToDictionary(group => group.Key, group => group.First().sourceKey);

            Vector2 nextPosition = request.origin;
            int createdOrUpdatedNodes = 0;
            foreach (SourceRecord source in sources)
            {
                PungentBoardNode existing;
                bool hasExisting = existingNodes.TryGetValue(source.sourceKey, out existing);
                if (hasExisting && !profile.updateExistingNodes)
                    continue;
                if (!hasExisting && !profile.createMissingNodes)
                    continue;

                source.position = hasExisting ? existing.position : nextPosition;
                PungentBoardProjectionChange change = BuildNodeChange(document, profile, source, hasExisting, existing);
                preview.changes.Add(change);
                createdOrUpdatedNodes++;
                if (!hasExisting)
                    nextPosition = Advance(nextPosition, request.origin, createdOrUpdatedNodes, profile);
            }

            BuildGroupChanges(profile, sources, existingGroups, preview);
            BuildEdgeChanges(document, profile, sources, existingNodes, existingEdges, sourceKeysByObject, preview);
            BuildStaleChanges(profile, existingNodes, sources, preview);

            preview.messages.Add("Previewed " + createdOrUpdatedNodes + " Unity selection/component node change(s), " +
                                 preview.changes.Count(change => change != null && (change.changeKind == PungentBoardProjectionChangeKind.AddEdge || change.changeKind == PungentBoardProjectionChangeKind.UpdateEdge)) +
                                 " edge change(s), and " +
                                 preview.changes.Count(change => change != null && (change.changeKind == PungentBoardProjectionChangeKind.AddGroup || change.changeKind == PungentBoardProjectionChangeKind.UpdateGroup)) +
                                 " group change(s). No board data has been changed yet.");
            return preview;
        }

        private static List<SourceRecord> CollectSources(PungentBoardProjectionRequest request, PungentBoardProjectionPreview preview)
        {
            PungentBoardIntegrationProfile profile = request.profile;
            List<SourceRecord> sources = new List<SourceRecord>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool includeSelectionObjects = profile.HasScope(PungentBoardProjectionSourceScope.CurrentSelection);
            bool includeComponents = profile.HasScope(PungentBoardProjectionSourceScope.SelectedGameObjectsAndComponents);
            foreach (Object item in request.selectionObjects ?? new Object[0])
                AddObjectSources(item, includeSelectionObjects, includeComponents, profile, sources, seen, preview);

            if (profile.HasScope(PungentBoardProjectionSourceScope.ExplicitAssetOrFolderRoots))
                AddExplicitRootSources(profile, sources, seen, preview);

            return sources.Take(profile.maxItemsPerRefresh).ToList();
        }

        private static void AddObjectSources(Object item, bool includeSelectionObject, bool includeComponents, PungentBoardIntegrationProfile profile, List<SourceRecord> sources, HashSet<string> seen, PungentBoardProjectionPreview preview)
        {
            if (item == null || sources.Count >= profile.maxItemsPerRefresh)
                return;

            GameObject gameObject = item as GameObject;
            Component component = item as Component;
            if (gameObject != null)
            {
                if (includeSelectionObject)
                    AddSource(gameObject, profile, sources, seen, preview);
                if (includeComponents)
                {
                    foreach (Component childComponent in gameObject.GetComponents<Component>())
                    {
                        if (childComponent != null)
                            AddSource(childComponent, profile, sources, seen, preview);
                        if (sources.Count >= profile.maxItemsPerRefresh)
                            break;
                    }
                }

                return;
            }

            if (component != null)
            {
                if (includeSelectionObject)
                    AddSource(component.gameObject, profile, sources, seen, preview);
                AddSource(component, profile, sources, seen, preview);
                return;
            }

            if (includeSelectionObject)
                AddSource(item, profile, sources, seen, preview);
        }

        private static void AddExplicitRootSources(PungentBoardIntegrationProfile profile, List<SourceRecord> sources, HashSet<string> seen, PungentBoardProjectionPreview preview)
        {
            foreach (string rawRoot in profile.explicitRootPaths ?? new List<string>())
            {
                if (sources.Count >= profile.maxItemsPerRefresh)
                    break;

                string root = ToAssetPath(rawRoot);
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                if (AssetDatabase.IsValidFolder(root))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Object", new[] { root });
                    foreach (string guid in guids)
                    {
                        if (sources.Count >= profile.maxItemsPerRefresh)
                            break;
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                        AddSource(asset, profile, sources, seen, preview);
                    }

                    continue;
                }

                Object rootAsset = AssetDatabase.LoadMainAssetAtPath(root);
                AddSource(rootAsset, profile, sources, seen, preview);
            }
        }

        private static void AddSource(Object unityObject, PungentBoardIntegrationProfile profile, List<SourceRecord> sources, HashSet<string> seen, PungentBoardProjectionPreview preview)
        {
            if (unityObject == null || sources.Count >= profile.maxItemsPerRefresh)
                return;
            if (!profile.IncludesSourceType(unityObject.GetType()))
                return;

            string sourceKey = SourceKey(unityObject);
            if (string.IsNullOrWhiteSpace(sourceKey) || !seen.Add(sourceKey))
                return;

            SourceRecord source = BuildSourceRecord(unityObject, sourceKey);
            ApplyFieldMappings(profile, source);
            sources.Add(source);
        }

        private static SourceRecord BuildSourceRecord(Object unityObject, string sourceKey)
        {
            Component component = unityObject as Component;
            GameObject gameObject = unityObject as GameObject;
            GameObject owner = gameObject != null ? gameObject : component != null ? component.gameObject : null;
            string assetPath = AssetDatabase.GetAssetPath(unityObject);
            string assetGuid = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            Type type = unityObject.GetType();

            SourceRecord source = new SourceRecord
            {
                unityObject = unityObject,
                gameObject = owner,
                component = component,
                sourceKey = sourceKey,
                title = component != null ? ObjectNames.NicifyVariableName(type.Name) + " | " + owner.name : unityObject.name,
                summary = component != null ? "Component on " + owner.name + "." : gameObject != null ? "Selected GameObject." : "Selected asset: " + assetPath,
                styleKey = component != null ? "utility" : gameObject != null ? "system" : "external",
                nodeKind = component != null ? PungentBoardNodeKind.UtilityReference : PungentBoardNodeKind.FreeformCard,
                groupGameObjectKey = owner != null ? SourceKey(owner) : string.Empty,
                folderPath = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty
            };

            source.values["name"] = unityObject.name;
            source.values["source.name"] = unityObject.name;
            source.values["source.type"] = type.Name;
            source.values["source.fullType"] = type.FullName ?? type.Name;
            source.values["source.key"] = sourceKey;
            source.values["asset.path"] = assetPath;
            source.values["asset.guid"] = assetGuid;
            if (owner != null)
                source.values["gameObject.name"] = owner.name;
            if (component != null)
            {
                source.values["component.type"] = type.Name;
                source.values["component.fullType"] = type.FullName ?? type.Name;
                ReadSerializedObject(source, component);
            }

            return source;
        }

        private static void ReadSerializedObject(SourceRecord source, Object unityObject)
        {
            try
            {
                SerializedObject serialized = new SerializedObject(unityObject);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    string value = SerializedPropertyValue(property);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        source.values[property.propertyPath] = value;
                        source.values["property:" + property.propertyPath] = value;
                        source.values[property.displayName] = value;
                    }

                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                    {
                        source.objectReferences.Add(new ObjectReferenceRecord
                        {
                            target = property.objectReferenceValue,
                            propertyPath = property.propertyPath,
                            displayName = property.displayName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                source.values["serialized.warning"] = ex.Message;
            }
        }

        private static string SerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Integer: return property.intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Float: return property.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.Enum: return property.enumDisplayNames != null && property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length ? property.enumDisplayNames[property.enumValueIndex] : property.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue != null ? property.objectReferenceValue.name : string.Empty;
                case SerializedPropertyType.Color: return property.colorValue.ToString();
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString();
                default: return string.Empty;
            }
        }

        private static PungentBoardProjectionChange BuildNodeChange(PungentBoardDocument document, PungentBoardIntegrationProfile profile, SourceRecord source, bool hasExisting, PungentBoardNode existing)
        {
            string nodeTypeKey = ResolveNodeTypeKey(document, source.component != null ? new[] { "utility", "system", "freeform-card" } : source.gameObject != null ? new[] { "system", "freeform-card" } : new[] { "external", "document-reference", "freeform-card" });
            PungentBoardProjectionChange change = new PungentBoardProjectionChange
            {
                adapterId = Id,
                sourceKey = source.sourceKey,
                sourceLabel = profile.sourceLabel,
                changeKind = hasExisting ? PungentBoardProjectionChangeKind.UpdateNode : PungentBoardProjectionChangeKind.AddNode,
                existingNodeId = hasExisting ? existing.id : string.Empty,
                title = source.title,
                summary = source.summary,
                body = source.summary,
                nodeKind = source.nodeKind,
                nodeTypeKey = nodeTypeKey,
                styleKey = source.styleKey,
                position = source.position
            };
            change.targets.AddRange(BuildTargets(source));
            AddProperty(change.properties, "sourceType", source.values.ContainsKey("source.fullType") ? source.values["source.fullType"] : string.Empty);
            AddProperty(change.properties, "sourceKey", source.sourceKey);
            if (source.gameObject != null)
                AddProperty(change.properties, "gameObject", source.gameObject.name);
            if (!string.IsNullOrWhiteSpace(source.folderPath))
                AddProperty(change.properties, "folder", source.folderPath);
            ApplyFieldMappings(profile, source, change);
            return change;
        }

        private static void ApplyFieldMappings(PungentBoardIntegrationProfile profile, SourceRecord source)
        {
            foreach (PungentBoardFieldMappingRule rule in profile.fieldMappings ?? new List<PungentBoardFieldMappingRule>())
            {
                if (rule == null || !rule.enabled)
                    continue;

                string value = ResolveValue(source, rule);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (rule.target == PungentBoardFieldMappingTarget.Title)
                    source.title = value;
                else if (rule.target == PungentBoardFieldMappingTarget.Summary || rule.target == PungentBoardFieldMappingTarget.Body)
                    source.summary = value;
                else if (rule.target == PungentBoardFieldMappingTarget.StyleKey)
                    source.styleKey = value;
                else if (rule.target == PungentBoardFieldMappingTarget.NodeTypeKey)
                    source.nodeTypeKey = value;
            }
        }

        private static void ApplyFieldMappings(PungentBoardIntegrationProfile profile, SourceRecord source, PungentBoardProjectionChange change)
        {
            foreach (PungentBoardFieldMappingRule rule in profile.fieldMappings ?? new List<PungentBoardFieldMappingRule>())
            {
                if (rule == null || !rule.enabled)
                    continue;

                string value = ResolveValue(source, rule);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                switch (rule.target)
                {
                    case PungentBoardFieldMappingTarget.Title:
                        change.title = value;
                        break;
                    case PungentBoardFieldMappingTarget.Summary:
                        change.summary = value;
                        break;
                    case PungentBoardFieldMappingTarget.Body:
                        change.body = value;
                        break;
                    case PungentBoardFieldMappingTarget.StatusProperty:
                        change.status = value;
                        break;
                    case PungentBoardFieldMappingTarget.Tag:
                        change.tags.Add(value);
                        break;
                    case PungentBoardFieldMappingTarget.NodeProperty:
                        AddProperty(change.properties, string.IsNullOrWhiteSpace(rule.targetKey) ? rule.sourcePath : rule.targetKey, value);
                        break;
                    case PungentBoardFieldMappingTarget.NodeTypeKey:
                        change.nodeTypeKey = value;
                        break;
                    case PungentBoardFieldMappingTarget.StyleKey:
                        change.styleKey = value;
                        break;
                }
            }
        }

        private static string ResolveValue(SourceRecord source, PungentBoardFieldMappingRule rule)
        {
            string value;
            if (!string.IsNullOrWhiteSpace(rule.sourcePath) && source.values.TryGetValue(rule.sourcePath, out value))
                return value;
            if (!string.IsNullOrWhiteSpace(rule.sourcePath) && source.values.TryGetValue("property:" + rule.sourcePath, out value))
                return value;
            return rule.fallbackValue ?? string.Empty;
        }

        private static void BuildEdgeChanges(PungentBoardDocument document, PungentBoardIntegrationProfile profile, List<SourceRecord> sources, Dictionary<string, PungentBoardNode> existingNodes, Dictionary<string, PungentBoardEdge> existingEdges, Dictionary<Object, string> sourceKeysByObject, PungentBoardProjectionPreview preview)
        {
            Dictionary<string, SourceRecord> sourcesByKey = sources.ToDictionary(source => source.sourceKey, source => source, StringComparer.OrdinalIgnoreCase);
            foreach (SourceRecord source in sources)
            {
                foreach (ObjectReferenceRecord reference in source.objectReferences)
                {
                    if (!RelationMappingsAllow(profile, reference))
                        continue;

                    string targetKey;
                    if (reference.target == null || !sourceKeysByObject.TryGetValue(reference.target, out targetKey))
                    {
                        preview.changes.Add(new PungentBoardProjectionChange
                        {
                            selected = false,
                            changeKind = PungentBoardProjectionChangeKind.Skip,
                            adapterId = Id,
                            sourceKey = source.sourceKey + ":skip:" + reference.propertyPath,
                            title = source.title,
                            skipReason = "Object reference '" + reference.displayName + "' points outside the selected/imported source set."
                        });
                        continue;
                    }

                    if (!sourcesByKey.ContainsKey(targetKey) && !existingNodes.ContainsKey(targetKey))
                        continue;

                    string edgeSourceKey = Id + ":edge:" + source.sourceKey + ":" + reference.propertyPath + ":" + targetKey;
                    PungentBoardEdge existing;
                    bool hasExisting = existingEdges.TryGetValue(edgeSourceKey, out existing);
                    if (hasExisting && !profile.updateExistingEdges)
                        continue;
                    if (!hasExisting && !profile.createMissingEdges)
                        continue;

                    PungentBoardRelationMappingRule mapping = FirstEnabledRelationMapping(profile);
                    string labelPrefix = mapping != null && !string.IsNullOrWhiteSpace(mapping.labelPrefix) ? mapping.labelPrefix + " " : string.Empty;
                    preview.changes.Add(new PungentBoardProjectionChange
                    {
                        adapterId = Id,
                        sourceKey = edgeSourceKey,
                        sourceLabel = profile.sourceLabel,
                        changeKind = hasExisting ? PungentBoardProjectionChangeKind.UpdateEdge : PungentBoardProjectionChangeKind.AddEdge,
                        existingEdgeId = hasExisting ? existing.id : string.Empty,
                        fromSourceKey = source.sourceKey,
                        toSourceKey = targetKey,
                        label = labelPrefix + reference.displayName,
                        title = labelPrefix + reference.displayName,
                        edgeKind = PungentBoardEdgeKind.References,
                        edgeTypeKey = mapping != null && !string.IsNullOrWhiteSpace(mapping.edgeTypeKey) ? mapping.edgeTypeKey : DefaultEdgeType(document),
                        styleKey = "curve",
                        directed = true
                    });
                }
            }
        }

        private static void BuildGroupChanges(PungentBoardIntegrationProfile profile, List<SourceRecord> sources, Dictionary<string, PungentBoardGroup> existingGroups, PungentBoardProjectionPreview preview)
        {
            foreach (PungentBoardGroupMappingRule mapping in profile.groupMappings ?? new List<PungentBoardGroupMappingRule>())
            {
                if (mapping == null || !mapping.enabled || mapping.groupBy == PungentBoardGroupBy.None)
                    continue;
                if (!profile.createMissingGroups && !profile.updateExistingGroups)
                    continue;

                foreach (IGrouping<string, SourceRecord> group in sources.GroupBy(source => GroupKey(mapping.groupBy, source)).Where(group => !string.IsNullOrWhiteSpace(group.Key)))
                {
                    string sourceKey = Id + ":group:" + mapping.groupBy + ":" + group.Key;
                    PungentBoardGroup existing;
                    bool hasExisting = existingGroups.TryGetValue(sourceKey, out existing);
                    if (hasExisting && !profile.updateExistingGroups)
                        continue;
                    if (!hasExisting && !profile.createMissingGroups)
                        continue;

                    Rect rect = BoundsFor(group.ToList());
                    string title = (string.IsNullOrWhiteSpace(mapping.titlePrefix) ? mapping.groupBy.ToString() : mapping.titlePrefix) + ": " + group.Key;
                    preview.changes.Add(new PungentBoardProjectionChange
                    {
                        adapterId = Id,
                        sourceKey = sourceKey,
                        sourceLabel = profile.sourceLabel,
                        changeKind = hasExisting ? PungentBoardProjectionChangeKind.UpdateGroup : PungentBoardProjectionChangeKind.AddGroup,
                        existingGroupId = hasExisting ? existing.id : string.Empty,
                        title = title,
                        groupTitle = title,
                        groupRect = rect,
                        styleKey = string.IsNullOrWhiteSpace(mapping.styleKey) ? "group" : mapping.styleKey
                    });
                }
            }
        }

        private static void BuildStaleChanges(PungentBoardIntegrationProfile profile, Dictionary<string, PungentBoardNode> existingNodes, List<SourceRecord> sources, PungentBoardProjectionPreview preview)
        {
            if (!profile.markStaleMissingSources)
                return;

            HashSet<string> currentKeys = new HashSet<string>(sources.Select(source => source.sourceKey), StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, PungentBoardNode> pair in existingNodes)
            {
                if (pair.Value == null || !string.Equals(pair.Value.integrationSourceAdapterId, Id, StringComparison.OrdinalIgnoreCase) || currentKeys.Contains(pair.Key))
                    continue;

                preview.changes.Add(new PungentBoardProjectionChange
                {
                    adapterId = Id,
                    sourceKey = pair.Key,
                    sourceLabel = profile.sourceLabel,
                    changeKind = PungentBoardProjectionChangeKind.MarkStale,
                    existingNodeId = pair.Value.id,
                    title = pair.Value.title,
                    selected = false
                });
            }
        }

        private static IEnumerable<PungentAuthoringTarget> BuildTargets(SourceRecord source)
        {
            string path = AssetDatabase.GetAssetPath(source.unityObject);
            if (!string.IsNullOrWhiteSpace(path))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrWhiteSpace(guid))
                    yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.AssetGuid, guid, source.unityObject.name, Id);
            }

            if (source.gameObject != null && string.IsNullOrWhiteSpace(path))
            {
                string gameObjectId = ResolvableGlobalId(source.gameObject);
                if (!string.IsNullOrWhiteSpace(gameObjectId))
                    yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.SceneObjectGlobalId, gameObjectId, source.gameObject.name, Id);
            }
            if (source.component != null && string.IsNullOrWhiteSpace(path))
            {
                string componentId = ResolvableGlobalId(source.component);
                if (!string.IsNullOrWhiteSpace(componentId))
                    yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.ComponentInstanceId, componentId, ObjectNames.NicifyVariableName(source.component.GetType().Name), Id);
            }
            if (source.component != null)
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.ComponentType, source.component.GetType().FullName, ObjectNames.NicifyVariableName(source.component.GetType().Name), Id);
        }

        private static bool RelationMappingsAllow(PungentBoardIntegrationProfile profile, ObjectReferenceRecord reference)
        {
            PungentBoardRelationMappingRule mapping = FirstEnabledRelationMapping(profile);
            if (mapping == null)
                return true;
            if (!mapping.includeObjectReferenceFields)
                return false;
            return string.IsNullOrWhiteSpace(mapping.sourcePropertyPathContains) ||
                   reference.propertyPath.IndexOf(mapping.sourcePropertyPathContains, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reference.displayName.IndexOf(mapping.sourcePropertyPathContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static PungentBoardRelationMappingRule FirstEnabledRelationMapping(PungentBoardIntegrationProfile profile)
        {
            return (profile.relationMappings ?? new List<PungentBoardRelationMappingRule>()).FirstOrDefault(rule => rule != null && rule.enabled);
        }

        private static string GroupKey(PungentBoardGroupBy groupBy, SourceRecord source)
        {
            switch (groupBy)
            {
                case PungentBoardGroupBy.GameObject: return source.gameObject != null ? source.gameObject.name : string.Empty;
                case PungentBoardGroupBy.ComponentType: return source.component != null ? ObjectNames.NicifyVariableName(source.component.GetType().Name) : string.Empty;
                case PungentBoardGroupBy.Folder: return source.folderPath;
                case PungentBoardGroupBy.SourceLabel: return source.values.ContainsKey("source.type") ? source.values["source.type"] : string.Empty;
                default: return string.Empty;
            }
        }

        private static Rect BoundsFor(List<SourceRecord> sources)
        {
            if (sources == null || sources.Count == 0)
                return new Rect(-40f, -40f, 360f, 240f);

            Rect rect = new Rect(sources[0].position.x - 28f, sources[0].position.y - 36f, 276f, 192f);
            for (int i = 1; i < sources.Count; i++)
            {
                Rect nodeRect = new Rect(sources[i].position.x - 28f, sources[i].position.y - 36f, 276f, 192f);
                rect = Union(rect, nodeRect);
            }

            rect.xMin -= 24f;
            rect.yMin -= 28f;
            rect.xMax += 24f;
            rect.yMax += 24f;
            return rect;
        }

        private static Rect Union(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Vector2 Advance(Vector2 current, Vector2 origin, int index, PungentBoardIntegrationProfile profile)
        {
            int column = index % 4;
            int row = index / 4;
            return origin + new Vector2(column * profile.nodeSpacingX, row * profile.nodeSpacingY);
        }

        private static string ResolveNodeTypeKey(PungentBoardDocument document, IEnumerable<string> candidates)
        {
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            if (graphType != null)
            {
                foreach (string candidate in candidates ?? new string[0])
                {
                    PungentBoardNodeTypeDefinition type = graphType.FindNodeType(candidate);
                    if (type != null)
                        return type.typeKey;
                }

                PungentBoardNodeTypeDefinition defaultType = graphType.GetDefaultNodeType();
                if (defaultType != null)
                    return defaultType.typeKey;
            }

            return string.Empty;
        }

        private static string DefaultEdgeType(PungentBoardDocument document)
        {
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            return graphType != null ? graphType.defaultEdgeTypeKey : string.Empty;
        }

        private static void AddProperty(List<PungentBoardGraphPropertyValue> properties, string key, string value)
        {
            if (properties == null || string.IsNullOrWhiteSpace(key))
                return;

            properties.Add(new PungentBoardGraphPropertyValue
            {
                key = key,
                value = value ?? string.Empty
            });
        }

        private static string SourceKey(Object unityObject)
        {
            if (unityObject == null)
                return string.Empty;

            try
            {
                GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
                string value = globalId.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return "unity:" + value;
            }
            catch
            {
                // Some transient editor objects cannot produce a global id.
            }

            string path = AssetDatabase.GetAssetPath(unityObject);
            if (!string.IsNullOrWhiteSpace(path))
                return "unity-asset:" + AssetDatabase.AssetPathToGUID(path) + ":" + unityObject.name;
            return "unity-instance:" + unityObject.GetInstanceID();
        }

        private static string ResolvableGlobalId(Object unityObject)
        {
            if (unityObject == null)
                return string.Empty;

            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ToAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string clean = path.Trim().Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (clean.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + clean.Substring(dataPath.Length);
            return clean;
        }
    }
#endif
}
