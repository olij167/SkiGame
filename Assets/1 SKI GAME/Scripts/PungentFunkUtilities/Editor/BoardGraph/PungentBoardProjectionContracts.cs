using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public enum PungentBoardProjectionChangeKind
    {
        AddNode = 0,
        UpdateNode = 10,
        AddEdge = 20,
        UpdateEdge = 30,
        AddGroup = 40,
        UpdateGroup = 50,
        MarkStale = 80,
        Skip = 100
    }

    public sealed class PungentBoardProjectionRequest
    {
        public PungentBoardDocument document;
        public PungentBoardIntegrationProfile profile;
        public Vector2 origin;
        public UnityEngine.Object[] selectionObjects = new UnityEngine.Object[0];
        public List<string> explicitRootPaths = new List<string>();
    }

    [Serializable]
    public sealed class PungentBoardProjectionChange
    {
        public bool selected = true;
        public PungentBoardProjectionChangeKind changeKind = PungentBoardProjectionChangeKind.AddNode;
        public string adapterId = string.Empty;
        public string sourceKey = string.Empty;
        public string sourceLabel = string.Empty;
        public string title = string.Empty;
        public string label = string.Empty;
        public string summary = string.Empty;
        public string body = string.Empty;
        public string status = string.Empty;
        public string styleKey = string.Empty;
        public string nodeTypeKey = string.Empty;
        public string edgeTypeKey = string.Empty;
        public string groupKey = string.Empty;
        public string groupTitle = string.Empty;
        public string existingEdgeId = string.Empty;
        public string existingGroupId = string.Empty;
        public string existingNodeId = string.Empty;
        public string fromSourceKey = string.Empty;
        public string toSourceKey = string.Empty;
        public string fromNodeId = string.Empty;
        public string toNodeId = string.Empty;
        public string fromPortKey = string.Empty;
        public string toPortKey = string.Empty;
        public string skipReason = string.Empty;
        public PungentBoardNodeKind nodeKind = PungentBoardNodeKind.NoteCard;
        public PungentBoardEdgeKind edgeKind = PungentBoardEdgeKind.Related;
        public bool directed = true;
        public int executionOrder;
        public PungentAuthoringReference reference = new PungentAuthoringReference();
        public List<PungentAuthoringReference> references = new List<PungentAuthoringReference>();
        public List<PungentAuthoringTarget> targets = new List<PungentAuthoringTarget>();
        public List<PungentBoardGraphPropertyValue> properties = new List<PungentBoardGraphPropertyValue>();
        public List<string> tags = new List<string>();
        public Vector2 position;
        public Rect groupRect;
    }

    public sealed class PungentBoardProjectionPreview
    {
        public string adapterId = string.Empty;
        public string displayName = string.Empty;
        public string generatedUtc = string.Empty;
        public readonly List<PungentBoardProjectionChange> changes = new List<PungentBoardProjectionChange>();
        public readonly List<string> messages = new List<string>();

        public int SelectedCount => changes.Count(change => change != null && change.selected && change.changeKind != PungentBoardProjectionChangeKind.Skip);
    }

    public interface IPungentBoardProjectionAdapter
    {
        string AdapterId { get; }
        string DisplayName { get; }
        string Description { get; }
        bool CanPreview(PungentBoardProjectionRequest request, out string reason);
        PungentBoardProjectionPreview BuildPreview(PungentBoardProjectionRequest request);
    }

    public static class PungentBoardProjectionRegistry
    {
        // SHARED-BINDING NOTE: BoardGraph owns adapter/profile projection data, while
        // Editor/Authoring/PungentAuthoringBindingServices.cs owns guided endpoint picking,
        // preview, and explicit apply for shared Authoring targets.
        private static readonly List<IPungentBoardProjectionAdapter> Adapters = new List<IPungentBoardProjectionAdapter>();

        public static IReadOnlyList<IPungentBoardProjectionAdapter> RegisteredAdapters
        {
            get
            {
                EnsureBuiltInsRegistered();
                return Adapters.Where(adapter => adapter != null).ToArray();
            }
        }

        public static void Register(IPungentBoardProjectionAdapter adapter)
        {
            if (adapter == null || string.IsNullOrWhiteSpace(adapter.AdapterId))
                return;

            Adapters.RemoveAll(item => item == null || string.Equals(item.AdapterId, adapter.AdapterId, StringComparison.OrdinalIgnoreCase));
            Adapters.Add(adapter);
        }

        public static void EnsureBuiltInsRegistered()
        {
            if (!Adapters.Any(adapter => adapter is PungentAuthoringProviderBoardProjectionAdapter))
                Register(new PungentAuthoringProviderBoardProjectionAdapter());
            if (!Adapters.Any(adapter => adapter is PungentBoardComponentSelectionProjectionAdapter))
                Register(new PungentBoardComponentSelectionProjectionAdapter());
        }
    }

    public static class PungentBoardProjectionApplier
    {
        public static int Apply(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardProjectionPreview preview)
        {
            if (document == null || preview == null)
                return 0;

            int applied = 0;
            Dictionary<string, PungentBoardNode> nodesBySource = BuildExistingNodeSourceLookup(document);
            Dictionary<string, PungentBoardEdge> edgesBySource = BuildExistingEdgeSourceLookup(document);
            Dictionary<string, PungentBoardGroup> groupsBySource = BuildExistingGroupSourceLookup(document);

            foreach (PungentBoardProjectionChange change in preview.changes)
            {
                if (change == null || !change.selected || change.changeKind == PungentBoardProjectionChangeKind.Skip)
                    continue;

                if (change.changeKind == PungentBoardProjectionChangeKind.MarkStale)
                {
                    PungentBoardNode staleNode = PungentBoardCanvasGUI.FindNode(document, change.existingNodeId);
                    if (staleNode != null)
                    {
                        staleNode.syncState = "Missing";
                        staleNode.NormalizeInPlace();
                        applied++;
                    }

                    continue;
                }

                if (change.changeKind == PungentBoardProjectionChangeKind.UpdateNode)
                {
                    PungentBoardNode existing = PungentBoardCanvasGUI.FindNode(document, change.existingNodeId);
                    if (existing == null)
                        continue;

                    ApplyNodeFields(document, existing, change, false);
                    if (!string.IsNullOrWhiteSpace(change.sourceKey))
                        nodesBySource[change.sourceKey] = existing;
                    applied++;
                    continue;
                }

                if (change.changeKind == PungentBoardProjectionChangeKind.AddNode)
                {
                    PungentBoardNode node = PungentBoardNode.Create(change.nodeKind, change.position);
                    ApplyNodeFields(document, node, change, true);
                    document.nodes.Add(node);
                    if (!string.IsNullOrWhiteSpace(change.sourceKey))
                        nodesBySource[change.sourceKey] = node;
                    if (state != null)
                        state.SelectNode(node.id);
                    applied++;
                    continue;
                }

                if (change.changeKind == PungentBoardProjectionChangeKind.UpdateGroup)
                {
                    PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(document, change.existingGroupId);
                    if (group == null)
                        continue;

                    ApplyGroupFields(group, change);
                    if (!string.IsNullOrWhiteSpace(change.sourceKey))
                        groupsBySource[change.sourceKey] = group;
                    applied++;
                    continue;
                }

                if (change.changeKind == PungentBoardProjectionChangeKind.AddGroup)
                {
                    PungentBoardGroup group = PungentBoardGroup.Create(string.IsNullOrWhiteSpace(change.groupTitle) ? change.title : change.groupTitle, change.groupRect);
                    ApplyGroupFields(group, change);
                    document.groups.Add(group);
                    if (!string.IsNullOrWhiteSpace(change.sourceKey))
                        groupsBySource[change.sourceKey] = group;
                    applied++;
                    continue;
                }

                if (change.changeKind == PungentBoardProjectionChangeKind.UpdateEdge)
                {
                    PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(document, change.existingEdgeId);
                    if (edge == null)
                        continue;

                    if (ApplyEdgeFields(document, edge, change, nodesBySource))
                    {
                        if (!string.IsNullOrWhiteSpace(change.sourceKey))
                            edgesBySource[change.sourceKey] = edge;
                        applied++;
                    }

                    continue;
                }

                if (change.changeKind == PungentBoardProjectionChangeKind.AddEdge)
                {
                    string fromNodeId;
                    string toNodeId;
                    if (!ResolveEdgeEndpoints(change, nodesBySource, out fromNodeId, out toNodeId))
                        continue;

                    PungentBoardEdge edge = PungentBoardEdge.Create(fromNodeId, toNodeId);
                    if (ApplyEdgeFields(document, edge, change, nodesBySource))
                    {
                        document.edges.Add(edge);
                        if (!string.IsNullOrWhiteSpace(change.sourceKey))
                            edgesBySource[change.sourceKey] = edge;
                        applied++;
                    }
                }
            }

            PungentBoardCanvasGUI.UpdateGroupMemberships(document);
            if (state != null)
            {
                state.contentChanged = applied > 0;
                state.repaintRequested = applied > 0;
            }

            return applied;
        }

        public static Dictionary<string, PungentBoardNode> BuildExistingNodeSourceLookup(PungentBoardDocument document)
        {
            Dictionary<string, PungentBoardNode> lookup = new Dictionary<string, PungentBoardNode>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardNode node in document?.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                string key = !string.IsNullOrWhiteSpace(node.integrationSourceKey)
                    ? node.integrationSourceKey
                    : node.linkedAuthoringRef != null && node.linkedAuthoringRef.HasItemId
                        ? PungentAuthoringProviderBoardProjectionAdapter.BuildSourceKey(node.linkedAuthoringRef.providerId, node.linkedAuthoringRef.itemId)
                        : string.Empty;
                if (!string.IsNullOrWhiteSpace(key) && !lookup.ContainsKey(key))
                    lookup.Add(key, node);
            }

            return lookup;
        }

        public static Dictionary<string, PungentBoardEdge> BuildExistingEdgeSourceLookup(PungentBoardDocument document)
        {
            Dictionary<string, PungentBoardEdge> lookup = new Dictionary<string, PungentBoardEdge>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardEdge edge in document?.edges ?? new List<PungentBoardEdge>())
            {
                if (edge != null && !string.IsNullOrWhiteSpace(edge.integrationSourceKey) && !lookup.ContainsKey(edge.integrationSourceKey))
                    lookup.Add(edge.integrationSourceKey, edge);
            }

            return lookup;
        }

        public static Dictionary<string, PungentBoardGroup> BuildExistingGroupSourceLookup(PungentBoardDocument document)
        {
            Dictionary<string, PungentBoardGroup> lookup = new Dictionary<string, PungentBoardGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardGroup group in document?.groups ?? new List<PungentBoardGroup>())
            {
                if (group != null && !string.IsNullOrWhiteSpace(group.integrationSourceKey) && !lookup.ContainsKey(group.integrationSourceKey))
                    lookup.Add(group.integrationSourceKey, group);
            }

            return lookup;
        }

        private static void ApplyNodeFields(PungentBoardDocument document, PungentBoardNode node, PungentBoardProjectionChange change, bool includePosition)
        {
            if (!string.IsNullOrWhiteSpace(change.nodeTypeKey))
                PungentBoardCanvasGUI.ApplyNodeTypeDefaults(document, node, change.nodeTypeKey);
            node.title = string.IsNullOrWhiteSpace(change.title) ? node.title : change.title;
            node.summary = change.summary ?? string.Empty;
            node.body = !string.IsNullOrWhiteSpace(change.body) ? change.body : string.IsNullOrWhiteSpace(change.summary) ? node.body : change.summary;
            node.nodeKind = change.nodeKind;
            node.nodeTypeKey = string.IsNullOrWhiteSpace(change.nodeTypeKey) ? node.nodeTypeKey : change.nodeTypeKey;
            if (includePosition)
                node.position = change.position;
            node.colorStyleKey = string.IsNullOrWhiteSpace(change.styleKey) ? PungentBoardNode.GetDefaultStyleKey(change.nodeKind) : change.styleKey;
            node.linkedAuthoringRef = change.reference ?? new PungentAuthoringReference();
            if (change.references != null && change.references.Count > 0)
                node.references = CopyReferences(change.references);
            if (change.targets != null && change.targets.Count > 0)
                node.targets = CopyTargets(change.targets);
            MergeProperties(node.properties, change.properties);
            if (!string.IsNullOrWhiteSpace(change.status))
                SetProperty(node.properties, "status", change.status);
            if (change.tags != null && change.tags.Count > 0)
                SetProperty(node.properties, "tags", string.Join(", ", change.tags.ToArray()));
            node.integrationSourceAdapterId = change.adapterId;
            node.integrationSourceKey = change.sourceKey;
            node.integrationSourceLabel = change.sourceLabel;
            node.syncState = "Synced";
            node.NormalizeInPlace();
        }

        private static bool ApplyEdgeFields(PungentBoardDocument document, PungentBoardEdge edge, PungentBoardProjectionChange change, Dictionary<string, PungentBoardNode> nodesBySource)
        {
            string fromNodeId;
            string toNodeId;
            if (!ResolveEdgeEndpoints(change, nodesBySource, out fromNodeId, out toNodeId))
                return false;

            edge.fromNodeId = fromNodeId;
            edge.toNodeId = toNodeId;
            edge.label = !string.IsNullOrWhiteSpace(change.label)
                ? change.label
                : !string.IsNullOrWhiteSpace(change.title)
                    ? change.title
                    : "Reference";
            edge.edgeKind = change.edgeKind;
            edge.edgeTypeKey = change.edgeTypeKey ?? string.Empty;
            edge.styleKey = string.IsNullOrWhiteSpace(change.styleKey) ? "curve" : change.styleKey;
            edge.directed = change.directed;
            edge.fromPortKey = change.fromPortKey ?? string.Empty;
            edge.toPortKey = change.toPortKey ?? string.Empty;
            edge.executionOrder = change.executionOrder;
            MergeProperties(edge.properties, change.properties);
            edge.integrationSourceAdapterId = change.adapterId;
            edge.integrationSourceKey = change.sourceKey;
            edge.integrationSourceLabel = change.sourceLabel;
            edge.syncState = "Synced";
            if (!string.IsNullOrWhiteSpace(edge.edgeTypeKey))
                PungentBoardCanvasGUI.ApplyBestEdgeRule(document, edge, edge.edgeTypeKey);
            edge.NormalizeInPlace();
            return true;
        }

        private static void ApplyGroupFields(PungentBoardGroup group, PungentBoardProjectionChange change)
        {
            group.title = string.IsNullOrWhiteSpace(change.groupTitle) ? string.IsNullOrWhiteSpace(change.title) ? group.title : change.title : change.groupTitle;
            if (change.groupRect.width > 0f && change.groupRect.height > 0f)
                group.rect = change.groupRect;
            group.colorStyleKey = string.IsNullOrWhiteSpace(change.styleKey) ? "group" : change.styleKey;
            group.integrationSourceAdapterId = change.adapterId;
            group.integrationSourceKey = change.sourceKey;
            group.integrationSourceLabel = change.sourceLabel;
            group.syncState = "Synced";
            group.NormalizeInPlace();
        }

        private static bool ResolveEdgeEndpoints(PungentBoardProjectionChange change, Dictionary<string, PungentBoardNode> nodesBySource, out string fromNodeId, out string toNodeId)
        {
            fromNodeId = PungentAuthoringId.Normalize(change.fromNodeId);
            toNodeId = PungentAuthoringId.Normalize(change.toNodeId);

            PungentBoardNode node;
            if (string.IsNullOrWhiteSpace(fromNodeId) && !string.IsNullOrWhiteSpace(change.fromSourceKey) && nodesBySource.TryGetValue(change.fromSourceKey, out node))
                fromNodeId = node.id;
            if (string.IsNullOrWhiteSpace(toNodeId) && !string.IsNullOrWhiteSpace(change.toSourceKey) && nodesBySource.TryGetValue(change.toSourceKey, out node))
                toNodeId = node.id;

            return !string.IsNullOrWhiteSpace(fromNodeId) && !string.IsNullOrWhiteSpace(toNodeId);
        }

        private static List<PungentAuthoringReference> CopyReferences(IEnumerable<PungentAuthoringReference> source)
        {
            List<PungentAuthoringReference> result = new List<PungentAuthoringReference>();
            foreach (PungentAuthoringReference reference in source ?? new PungentAuthoringReference[0])
            {
                if (reference == null)
                    continue;
                PungentAuthoringReference copy = JsonUtility.FromJson<PungentAuthoringReference>(JsonUtility.ToJson(reference));
                copy.NormalizeInPlace();
                result.Add(copy);
            }

            return result;
        }

        private static List<PungentAuthoringTarget> CopyTargets(IEnumerable<PungentAuthoringTarget> source)
        {
            List<PungentAuthoringTarget> result = new List<PungentAuthoringTarget>();
            foreach (PungentAuthoringTarget target in source ?? new PungentAuthoringTarget[0])
            {
                if (target == null)
                    continue;
                PungentAuthoringTarget copy = JsonUtility.FromJson<PungentAuthoringTarget>(JsonUtility.ToJson(target));
                copy.NormalizeInPlace();
                result.Add(copy);
            }

            return result;
        }

        private static void MergeProperties(List<PungentBoardGraphPropertyValue> target, IEnumerable<PungentBoardGraphPropertyValue> source)
        {
            if (target == null)
                return;

            foreach (PungentBoardGraphPropertyValue property in source ?? new PungentBoardGraphPropertyValue[0])
            {
                if (property == null || string.IsNullOrWhiteSpace(property.key))
                    continue;

                SetProperty(target, property.key, property.value);
            }
        }

        private static void SetProperty(List<PungentBoardGraphPropertyValue> values, string key, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(key))
                return;

            PungentBoardGraphPropertyValue existing = values.FirstOrDefault(item => item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new PungentBoardGraphPropertyValue { key = key };
                values.Add(existing);
            }

            existing.value = value ?? string.Empty;
            existing.NormalizeInPlace();
        }
    }

    public sealed class PungentAuthoringProviderBoardProjectionAdapter : IPungentBoardProjectionAdapter
    {
        public const string Id = "authoring-provider-import";

        public string AdapterId => Id;
        public string DisplayName => "Authoring Provider Import";
        public string Description => "Creates or updates board nodes from registered Authoring providers when explicitly previewed.";

        public bool CanPreview(PungentBoardProjectionRequest request, out string reason)
        {
            if (request == null || request.document == null)
            {
                reason = "Select a board before previewing an import.";
                return false;
            }

            if (request.profile == null)
            {
                reason = "Select an integration profile.";
                return false;
            }

            if (!request.profile.HasScope(PungentBoardProjectionSourceScope.AuthoringProviders))
            {
                reason = string.Empty;
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
                preview.messages.Add(reason);
                return preview;
            }

            PungentBoardDocument document = request.document;
            PungentBoardIntegrationProfile profile = request.profile;
            Dictionary<string, PungentBoardNode> existingBySource = PungentBoardProjectionApplier.BuildExistingNodeSourceLookup(document);
            Dictionary<string, PungentBoardGroup> existingGroups = PungentBoardProjectionApplier.BuildExistingGroupSourceLookup(document);
            HashSet<string> currentSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<PungentBoardProjectionChange>> changesByProvider = new Dictionary<string, List<PungentBoardProjectionChange>>(StringComparer.OrdinalIgnoreCase);
            int itemCount = 0;
            int providerCount = 0;
            Vector2 nextPosition = request.origin;

            foreach (IPungentAuthoringProvider provider in PungentAuthoringProviderRegistry.GetProviders())
            {
                if (provider == null || string.Equals(provider.ProviderId, "board-documents", StringComparison.OrdinalIgnoreCase))
                    continue;
                if ((provider.Capabilities & PungentAuthoringProviderCapabilities.EnumerateItems) == 0)
                    continue;

                bool providerContributed = false;
                IEnumerable<PungentAuthoringMetadata> items;
                try
                {
                    items = provider.EnumerateItems() ?? Enumerable.Empty<PungentAuthoringMetadata>();
                }
                catch (Exception ex)
                {
                    preview.messages.Add(provider.DisplayName + " failed to enumerate: " + ex.Message);
                    continue;
                }

                foreach (PungentAuthoringMetadata metadata in items)
                {
                    if (metadata == null)
                        continue;
                    metadata.NormalizeInPlace();
                    if (!profile.Includes(provider, metadata))
                        continue;

                    string sourceKey = BuildSourceKey(provider.ProviderId, metadata.id);
                    currentSourceKeys.Add(sourceKey);
                    PungentBoardNode existing;
                    bool hasExisting = existingBySource.TryGetValue(sourceKey, out existing);
                    if (hasExisting && !profile.updateExistingNodes)
                        continue;
                    if (!hasExisting && !profile.createMissingNodes)
                        continue;

                    PungentBoardProjectionChange change = new PungentBoardProjectionChange
                    {
                        adapterId = AdapterId,
                        sourceKey = sourceKey,
                        sourceLabel = profile.sourceLabel,
                        changeKind = hasExisting ? PungentBoardProjectionChangeKind.UpdateNode : PungentBoardProjectionChangeKind.AddNode,
                        existingNodeId = hasExisting ? existing.id : string.Empty,
                        title = metadata.title,
                        summary = metadata.summary,
                        body = metadata.summary,
                        status = metadata.status,
                        nodeKind = MapNodeKind(metadata.kind),
                        nodeTypeKey = ResolveAuthoringNodeTypeKey(document, metadata.kind),
                        styleKey = MapStyleKey(metadata.kind),
                        reference = metadata.ToReference(),
                        position = hasExisting ? existing.position : nextPosition,
                        tags = metadata.tags ?? new List<string>()
                    };
                    change.references.Add(metadata.ToReference());
                    preview.changes.Add(change);
                    List<PungentBoardProjectionChange> providerChanges;
                    if (!changesByProvider.TryGetValue(provider.ProviderId, out providerChanges))
                    {
                        providerChanges = new List<PungentBoardProjectionChange>();
                        changesByProvider.Add(provider.ProviderId, providerChanges);
                    }

                    providerChanges.Add(change);
                    providerContributed = true;
                    itemCount++;

                    if (!hasExisting)
                        nextPosition = Advance(nextPosition, request.origin, itemCount, profile);

                    if (itemCount >= profile.maxItemsPerRefresh)
                        break;
                }

                if (providerContributed)
                    providerCount++;
                if (itemCount >= profile.maxItemsPerRefresh)
                    break;
            }

            if (profile.createProviderGroups)
                AddProviderGroups(profile, existingGroups, changesByProvider, preview);
            if (profile.markStaleMissingSources)
                AddStaleAuthoringNodes(profile, existingBySource, currentSourceKeys, preview);

            preview.messages.Add("Previewed " + itemCount + " item(s) from " + providerCount + " provider(s). No board data has been changed yet.");
            if (itemCount >= profile.maxItemsPerRefresh)
                preview.messages.Add("Preview capped at " + profile.maxItemsPerRefresh + " item(s). Narrow the profile or increase the cap for more.");
            return preview;
        }

        public static string BuildSourceKey(string providerId, string itemId)
        {
            return (providerId ?? string.Empty).Trim() + ":" + PungentAuthoringId.Normalize(itemId);
        }

        private static void AddProviderGroups(PungentBoardIntegrationProfile profile, Dictionary<string, PungentBoardGroup> existingGroups, Dictionary<string, List<PungentBoardProjectionChange>> changesByProvider, PungentBoardProjectionPreview preview)
        {
            foreach (KeyValuePair<string, List<PungentBoardProjectionChange>> pair in changesByProvider)
            {
                if (pair.Value == null || pair.Value.Count == 0)
                    continue;

                string sourceKey = Id + ":group:provider:" + pair.Key;
                PungentBoardGroup existing;
                bool hasExisting = existingGroups.TryGetValue(sourceKey, out existing);
                if (hasExisting && !profile.updateExistingGroups)
                    continue;
                if (!hasExisting && !profile.createMissingGroups)
                    continue;

                Rect rect = BoundsFor(pair.Value);
                string title = "Provider: " + pair.Key;
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
                    styleKey = "group"
                });
            }
        }

        private static void AddStaleAuthoringNodes(PungentBoardIntegrationProfile profile, Dictionary<string, PungentBoardNode> existingBySource, HashSet<string> currentSourceKeys, PungentBoardProjectionPreview preview)
        {
            foreach (KeyValuePair<string, PungentBoardNode> pair in existingBySource)
            {
                if (pair.Value == null || !string.Equals(pair.Value.integrationSourceAdapterId, Id, StringComparison.OrdinalIgnoreCase) || currentSourceKeys.Contains(pair.Key))
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

        private static Rect BoundsFor(List<PungentBoardProjectionChange> changes)
        {
            Rect rect = new Rect(changes[0].position.x - 40f, changes[0].position.y - 48f, 300f, 210f);
            for (int i = 1; i < changes.Count; i++)
            {
                Rect item = new Rect(changes[i].position.x - 40f, changes[i].position.y - 48f, 300f, 210f);
                float xMin = Mathf.Min(rect.xMin, item.xMin);
                float yMin = Mathf.Min(rect.yMin, item.yMin);
                float xMax = Mathf.Max(rect.xMax, item.xMax);
                float yMax = Mathf.Max(rect.yMax, item.yMax);
                rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }

            rect.xMin -= 24f;
            rect.yMin -= 28f;
            rect.xMax += 24f;
            rect.yMax += 24f;
            return rect;
        }

        private static Vector2 Advance(Vector2 current, Vector2 origin, int index, PungentBoardIntegrationProfile profile)
        {
            int column = index % 4;
            int row = index / 4;
            return origin + new Vector2(column * profile.nodeSpacingX, row * profile.nodeSpacingY);
        }

        private static PungentBoardNodeKind MapNodeKind(PungentAuthoringItemKind kind)
        {
            switch (kind)
            {
                case PungentAuthoringItemKind.RichDocument: return PungentBoardNodeKind.DocumentReference;
                case PungentAuthoringItemKind.DataSheet: return PungentBoardNodeKind.DocumentReference;
                case PungentAuthoringItemKind.DocumentationLink: return PungentBoardNodeKind.DocumentationLink;
                case PungentAuthoringItemKind.Utility:
                case PungentAuthoringItemKind.FutureUtility: return PungentBoardNodeKind.UtilityReference;
                case PungentAuthoringItemKind.TokenDefinition: return PungentBoardNodeKind.TokenReference;
                case PungentAuthoringItemKind.AuditIssue: return PungentBoardNodeKind.AuditFinding;
                case PungentAuthoringItemKind.Task: return PungentBoardNodeKind.Task;
                default: return PungentBoardNodeKind.NoteCard;
            }
        }

        private static string MapStyleKey(PungentAuthoringItemKind kind)
        {
            return PungentBoardNode.GetDefaultStyleKey(MapNodeKind(kind));
        }

        private static string ResolveAuthoringNodeTypeKey(PungentBoardDocument document, PungentAuthoringItemKind kind)
        {
            string[] candidates;
            switch (kind)
            {
                case PungentAuthoringItemKind.Utility:
                case PungentAuthoringItemKind.FutureUtility:
                    candidates = new[] { "utility-reference", "utility", "system", "freeform-card" };
                    break;
                case PungentAuthoringItemKind.TokenDefinition:
                    candidates = new[] { "token-reference", "external", "freeform-card" };
                    break;
                case PungentAuthoringItemKind.DocumentationLink:
                case PungentAuthoringItemKind.HelpTopic:
                    candidates = new[] { "documentation-link", "document-reference", "freeform-card" };
                    break;
                case PungentAuthoringItemKind.AuditIssue:
                    candidates = new[] { "audit-finding", "task", "freeform-card" };
                    break;
                case PungentAuthoringItemKind.Task:
                    candidates = new[] { "task", "quest-task", "freeform-card" };
                    break;
                default:
                    candidates = new[] { "document-reference", "note-card", "freeform-card" };
                    break;
            }

            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            if (graphType != null)
            {
                foreach (string candidate in candidates)
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
    }

    [InitializeOnLoad]
    public static class PungentBoardProjectionBootstrap
    {
        static PungentBoardProjectionBootstrap()
        {
            PungentBoardProjectionRegistry.EnsureBuiltInsRegistered();
        }
    }
#endif
}
