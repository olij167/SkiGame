using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public static class PungentBoardCustomNodeTemplateStorage
    {
        private const string StorageFileName = "BoardCustomNodeTemplates.json";

        private static PungentBoardCustomNodeTemplateDatabase _database;
        private static string _loadError = string.Empty;

        public static string StoragePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "PungentFunkUtilities", StorageFileName);
        public static string LoadError => _loadError;

        public static PungentBoardCustomNodeTemplateDatabase Database
        {
            get
            {
                EnsureLoaded();
                return _database;
            }
        }

        public static void EnsureLoaded()
        {
            if (_database != null)
                return;

            Reload();
        }

        public static void Reload()
        {
            _loadError = string.Empty;
            try
            {
                if (!File.Exists(StoragePath))
                {
                    _database = new PungentBoardCustomNodeTemplateDatabase();
                    _database.NormalizeInPlace();
                    return;
                }

                string json = File.ReadAllText(StoragePath);
                _database = string.IsNullOrWhiteSpace(json)
                    ? new PungentBoardCustomNodeTemplateDatabase()
                    : JsonUtility.FromJson<PungentBoardCustomNodeTemplateDatabase>(json);
                if (_database == null)
                    _database = new PungentBoardCustomNodeTemplateDatabase();
                _database.NormalizeInPlace();
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                _database = new PungentBoardCustomNodeTemplateDatabase();
                _database.NormalizeInPlace();
                Debug.LogWarning("PungentFunk Board custom node templates could not be loaded. A safe empty library is being used. " + _loadError);
            }
        }

        public static bool Save(out string error)
        {
            error = string.Empty;
            try
            {
                PungentBoardCustomNodeTemplateDatabase database = Database;
                database.lastSavedUtc = DateTime.UtcNow.ToString("o");
                database.NormalizeInPlace();

                string directory = Path.GetDirectoryName(StoragePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(database, true);
                string tempPath = StoragePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(StoragePath))
                    File.Copy(StoragePath, StoragePath + ".bak", true);
                File.Copy(tempPath, StoragePath, true);
                File.Delete(tempPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool SaveGroupAsTemplate(PungentBoardDocument document, PungentBoardGroup group, out PungentBoardCustomNodeTemplate template, out string message)
        {
            template = null;
            message = string.Empty;
            if (document == null || group == null)
            {
                message = "Select a board group before saving a custom node template.";
                return false;
            }

            PungentBoardCanvasGUI.UpdateGroupMemberships(document);
            group.customNodeEnabled = true;
            group.collapsedAsNode = true;
            if (string.IsNullOrWhiteSpace(group.customNodeTemplateId))
                group.customNodeTemplateId = PungentAuthoringId.NewValue();
            group.NormalizeInPlace();

            template = PungentBoardCustomNodeTemplate.FromGroup(document, group);
            template.id = group.customNodeTemplateId;
            template.displayName = string.IsNullOrWhiteSpace(group.title) ? "Custom Node" : group.title;
            template.NormalizeInPlace();
            Database.AddOrUpdate(template);

            string error;
            if (!Save(out error))
            {
                message = error;
                return false;
            }

            message = "Saved custom node template '" + template.displayName + "'.";
            return true;
        }

        public static bool InstantiateTemplate(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardCustomNodeTemplate template, Vector2 canvasPoint, out PungentBoardGroup group, out string message)
        {
            group = null;
            message = string.Empty;
            if (document == null || template == null)
            {
                message = "Cannot instantiate custom node: board or template is missing.";
                return false;
            }

            template.NormalizeInPlace();
            if ((template.nodes?.Count ?? 0) == 0)
            {
                message = "Custom node template has no stored node snapshot.";
                return false;
            }

            PungentBoardCanvasGUI.BeginExternalHistory(document, state, "Add Custom Group Node");
            Dictionary<string, string> idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Rect sourceBounds = template.sourceBounds.width > 0f && template.sourceBounds.height > 0f
                ? template.sourceBounds
                : CalculateTemplateBounds(template);
            Rect groupRect = new Rect(canvasPoint.x - sourceBounds.width * 0.5f, canvasPoint.y - sourceBounds.height * 0.5f, sourceBounds.width, sourceBounds.height);

            group = PungentBoardGroup.Create(template.displayName, groupRect);
            group.colorStyleKey = string.IsNullOrWhiteSpace(template.styleKey) ? "group" : template.styleKey;
            group.customNodeEnabled = true;
            group.customNodeSourceTemplateId = template.id;
            group.collapsedAsNode = template.collapsedAsNode;
            group.collapsed = template.collapsedAsNode;
            group.exposedPorts = CloneList(template.ports);
            group.exposedBindingSlots = CloneList(template.exposedBindingSlots);
            group.exposedPortMappings = CloneList(template.portMappings);
            group.referenceSlots = CloneList(template.referenceSlots);

            foreach (PungentBoardNode sourceNode in template.nodes ?? new List<PungentBoardNode>())
            {
                if (sourceNode == null)
                    continue;

                PungentBoardNode node = Clone(sourceNode);
                string oldId = node.id;
                node.id = PungentAuthoringId.NewValue();
                idMap[oldId] = node.id;
                node.position = groupRect.position + (sourceNode.position - sourceBounds.position);
                node.integrationSourceAdapterId = "custom-node-template";
                node.integrationSourceKey = template.id;
                node.integrationSourceLabel = template.displayName;
                RemapNodeBindingIds(node, oldId, node.id);
                node.NormalizeInPlace();
                document.nodes.Add(node);
                group.containedNodeIds.Add(node.id);
            }

            foreach (PungentBoardEdge sourceEdge in template.edges ?? new List<PungentBoardEdge>())
            {
                if (sourceEdge == null)
                    continue;

                string fromId;
                string toId;
                if (!idMap.TryGetValue(sourceEdge.fromNodeId, out fromId) || !idMap.TryGetValue(sourceEdge.toNodeId, out toId))
                    continue;

                PungentBoardEdge edge = Clone(sourceEdge);
                edge.id = PungentAuthoringId.NewValue();
                edge.fromNodeId = fromId;
                edge.toNodeId = toId;
                edge.integrationSourceAdapterId = "custom-node-template";
                edge.integrationSourceKey = template.id;
                edge.integrationSourceLabel = template.displayName;
                edge.NormalizeInPlace();
                document.edges.Add(edge);
            }

            foreach (PungentBoardCustomNodePortMapping mapping in group.exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>())
            {
                if (mapping == null)
                    continue;

                string mapped;
                if (!string.IsNullOrWhiteSpace(mapping.internalNodeId) && idMap.TryGetValue(mapping.internalNodeId, out mapped))
                    mapping.internalNodeId = mapped;
                mapping.NormalizeInPlace();
            }

            NormalizeGroupSlots(group);
            group.NormalizeInPlace();
            document.groups.Add(group);
            PungentBoardCanvasGUI.UpdateGroupMemberships(document);

            if (state != null)
            {
                state.SelectGroup(group.id);
                state.contentChanged = true;
                state.repaintRequested = true;
            }

            message = "Added custom group node '" + group.title + "'.";
            return true;
        }

        private static Rect CalculateTemplateBounds(PungentBoardCustomNodeTemplate template)
        {
            bool hasBounds = false;
            Rect bounds = new Rect(0f, 0f, 360f, 240f);
            foreach (PungentBoardNode node in template.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = node.Rect;
                    hasBounds = true;
                }
                else
                {
                    float xMin = Mathf.Min(bounds.xMin, node.Rect.xMin);
                    float yMin = Mathf.Min(bounds.yMin, node.Rect.yMin);
                    float xMax = Mathf.Max(bounds.xMax, node.Rect.xMax);
                    float yMax = Mathf.Max(bounds.yMax, node.Rect.yMax);
                    bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                }
            }

            if (!hasBounds)
                return new Rect(0f, 0f, 360f, 240f);
            bounds.xMin -= 24f;
            bounds.yMin -= 24f;
            bounds.xMax += 24f;
            bounds.yMax += 24f;
            return bounds;
        }

        private static void RemapNodeBindingIds(PungentBoardNode node, string oldId, string newId)
        {
            if (node == null)
                return;

            node.projectBindingLinkId = string.Empty;
            if (node.projectBindingSlot != null)
            {
                if (PungentAuthoringId.EqualsId(node.projectBindingSlot.elementId, oldId))
                    node.projectBindingSlot.elementId = newId;
                node.projectBindingSlot.bindingLinkId = string.Empty;
                node.projectBindingSlot.NormalizeInPlace();
            }
        }

        private static void NormalizeGroupSlots(PungentBoardGroup group)
        {
            if (group == null)
                return;

            foreach (PungentAuthoringBindingSlot slot in group.exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>())
            {
                if (slot == null)
                    continue;
                if (slot.role == PungentAuthoringBindingSlotRole.Unknown)
                    slot.role = PungentAuthoringBindingSlotRole.BoardGroup;
                slot.elementId = group.id;
                slot.NormalizeInPlace();
            }

            foreach (PungentBoardCustomNodeReferenceSlot slot in group.referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>())
            {
                if (slot == null)
                    continue;
                if (slot.bindingSlot != null)
                    slot.bindingSlot.elementId = group.id;
                slot.NormalizeInPlace();
            }
        }

        private static T Clone<T>(T value)
        {
            return value == null ? default(T) : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        }

        private static List<T> CloneList<T>(IEnumerable<T> values)
        {
            List<T> clone = new List<T>();
            foreach (T value in values ?? new T[0])
            {
                T item = Clone(value);
                if (item != null)
                    clone.Add(item);
            }
            return clone;
        }
    }
#endif
}
