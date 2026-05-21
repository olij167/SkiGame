using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PungentFunk.Utilities.BoardGraph
{
    public static class PungentBoardBuiltInGraphTypes
    {
        public const string FreeformWhiteboard = "freeform-whiteboard";
        public const string DialogueQuestFlow = "dialogue-quest-flow";
        public const string BehaviourTree = "behaviour-tree";
        public const string AiStateMachine = "ai-state-machine";
        public const string EventSequence = "event-sequence";
        public const string SkillTree = "skill-tree";
        public const string DependencyMap = "dependency-map";

        public static string NormalizeGraphTypeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? FreeformWhiteboard : value.Trim();
        }
    }

    public enum PungentBoardPortDirection
    {
        Input = 0,
        Output = 10,
        Both = 20
    }

    public enum PungentBoardExecutionMode
    {
        None = 0,
        Flow = 10,
        BehaviourTree = 20,
        StateMachine = 30,
        Sequence = 40,
        SkillTree = 50,
        Dependency = 60
    }

    public enum PungentBoardGraphPropertyValueKind
    {
        Text = 0,
        LongText = 10,
        Number = 20,
        Boolean = 30,
        Enum = 40,
        Json = 50
    }

    [Serializable]
    public sealed class PungentBoardGraphPropertyDefinition
    {
        public string key = string.Empty;
        public string displayName = "Property";
        public string description = string.Empty;
        public PungentBoardGraphPropertyValueKind valueKind = PungentBoardGraphPropertyValueKind.Text;
        public string defaultValue = string.Empty;
        public bool required;
        public List<string> options = new List<string>();

        public void NormalizeInPlace()
        {
            key = string.IsNullOrWhiteSpace(key) ? "property" : key.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim();
            description = description == null ? string.Empty : description.Trim();
            defaultValue = defaultValue == null ? string.Empty : defaultValue.Trim();
            options = PungentBoardPortDefinition.NormalizeKeys(options);
        }
    }

    [Serializable]
    public sealed class PungentBoardGraphPropertyValue
    {
        public string key = string.Empty;
        public string value = string.Empty;

        public void NormalizeInPlace()
        {
            key = key == null ? string.Empty : key.Trim();
            value = value == null ? string.Empty : value;
        }

        public static PungentBoardGraphPropertyValue FindOrCreate(List<PungentBoardGraphPropertyValue> values, PungentBoardGraphPropertyDefinition definition)
        {
            if (values == null || definition == null || string.IsNullOrWhiteSpace(definition.key))
                return null;

            string key = definition.key.Trim();
            PungentBoardGraphPropertyValue value = values.FirstOrDefault(item => item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            if (value != null)
                return value;

            value = new PungentBoardGraphPropertyValue
            {
                key = key,
                value = definition.defaultValue ?? string.Empty
            };
            value.NormalizeInPlace();
            values.Add(value);
            return value;
        }
    }

    [Serializable]
    public sealed class PungentBoardExecutionMetadata
    {
        public PungentBoardExecutionMode executionMode = PungentBoardExecutionMode.None;
        public string entryNodeTypeKey = string.Empty;
        public string successNodeTypeKey = string.Empty;
        public string failureNodeTypeKey = string.Empty;
        public bool tickDriven;
        public bool allowRuntimeExecution;
        public string notes = string.Empty;

        public void NormalizeInPlace()
        {
            entryNodeTypeKey = entryNodeTypeKey == null ? string.Empty : entryNodeTypeKey.Trim();
            successNodeTypeKey = successNodeTypeKey == null ? string.Empty : successNodeTypeKey.Trim();
            failureNodeTypeKey = failureNodeTypeKey == null ? string.Empty : failureNodeTypeKey.Trim();
            notes = notes == null ? string.Empty : notes.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBoardPortDefinition
    {
        public string key = string.Empty;
        public string displayName = "Port";
        public PungentBoardPortDirection direction = PungentBoardPortDirection.Both;
        public bool required;
        public bool allowMultipleConnections = true;
        public List<string> acceptsNodeTypeKeys = new List<string>();

        public void NormalizeInPlace()
        {
            key = string.IsNullOrWhiteSpace(key) ? "port" : key.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim();
            acceptsNodeTypeKeys = NormalizeKeys(acceptsNodeTypeKeys);
        }

        public bool AcceptsNodeType(string nodeTypeKey)
        {
            if (acceptsNodeTypeKeys == null || acceptsNodeTypeKeys.Count == 0)
                return true;

            string clean = string.IsNullOrWhiteSpace(nodeTypeKey) ? string.Empty : nodeTypeKey.Trim();
            return acceptsNodeTypeKeys.Any(key => string.Equals(key, clean, StringComparison.OrdinalIgnoreCase));
        }

        internal static List<string> NormalizeKeys(IEnumerable<string> values)
        {
            return (values ?? new string[0])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    [Serializable]
    public sealed class PungentBoardNodeTypeDefinition
    {
        public string typeKey = string.Empty;
        public string displayName = "Node";
        public string description = string.Empty;
        public string defaultTitle = string.Empty;
        public PungentBoardNodeKind nodeKind = PungentBoardNodeKind.FreeformCard;
        public string styleKey = "freeform";
        public Vector2 defaultSize = new Vector2(220f, 120f);
        public bool canBeRoot;
        public bool isExecutionNode;
        public bool isTerminalNode;
        public bool allowMultiple = true;
        public List<string> inspectorFieldKeys = new List<string>();
        public List<PungentBoardGraphPropertyDefinition> propertyDefinitions = new List<PungentBoardGraphPropertyDefinition>();
        public List<PungentBoardPortDefinition> ports = new List<PungentBoardPortDefinition>();

        public void NormalizeInPlace()
        {
            typeKey = string.IsNullOrWhiteSpace(typeKey) ? nodeKind.ToString() : typeKey.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? typeKey : displayName.Trim();
            description = description == null ? string.Empty : description.Trim();
            defaultTitle = string.IsNullOrWhiteSpace(defaultTitle) ? displayName : defaultTitle.Trim();
            styleKey = string.IsNullOrWhiteSpace(styleKey) ? PungentBoardNode.GetDefaultStyleKey(nodeKind) : styleKey.Trim();
            defaultSize.x = Mathf.Clamp(float.IsNaN(defaultSize.x) || float.IsInfinity(defaultSize.x) ? 220f : defaultSize.x, 120f, 720f);
            defaultSize.y = Mathf.Clamp(float.IsNaN(defaultSize.y) || float.IsInfinity(defaultSize.y) ? 120f : defaultSize.y, 64f, 520f);
            inspectorFieldKeys = PungentBoardPortDefinition.NormalizeKeys(inspectorFieldKeys);
            propertyDefinitions = propertyDefinitions ?? new List<PungentBoardGraphPropertyDefinition>();
            ports = ports ?? new List<PungentBoardPortDefinition>();
            for (int i = propertyDefinitions.Count - 1; i >= 0; i--)
            {
                if (propertyDefinitions[i] == null)
                {
                    propertyDefinitions.RemoveAt(i);
                    continue;
                }

                propertyDefinitions[i].NormalizeInPlace();
            }

            for (int i = ports.Count - 1; i >= 0; i--)
            {
                if (ports[i] == null)
                {
                    ports.RemoveAt(i);
                    continue;
                }

                ports[i].NormalizeInPlace();
            }
        }

        public IEnumerable<PungentBoardPortDefinition> InputPorts()
        {
            return (ports ?? new List<PungentBoardPortDefinition>()).Where(port => port != null && (port.direction == PungentBoardPortDirection.Input || port.direction == PungentBoardPortDirection.Both));
        }

        public IEnumerable<PungentBoardPortDefinition> OutputPorts()
        {
            return (ports ?? new List<PungentBoardPortDefinition>()).Where(port => port != null && (port.direction == PungentBoardPortDirection.Output || port.direction == PungentBoardPortDirection.Both));
        }
    }

    [Serializable]
    public sealed class PungentBoardEdgeRuleDefinition
    {
        public string typeKey = "default";
        public string displayName = "Connection";
        public string description = string.Empty;
        public PungentBoardEdgeKind edgeKind = PungentBoardEdgeKind.LeadsTo;
        public string styleKey = "curve";
        public bool directed = true;
        public List<string> fromNodeTypeKeys = new List<string>();
        public List<string> toNodeTypeKeys = new List<string>();
        public string fromPortKey = string.Empty;
        public string toPortKey = string.Empty;
        public bool allowCycles = true;
        public int executionOrderWeight;
        public List<PungentBoardGraphPropertyDefinition> propertyDefinitions = new List<PungentBoardGraphPropertyDefinition>();

        public void NormalizeInPlace()
        {
            typeKey = string.IsNullOrWhiteSpace(typeKey) ? edgeKind.ToString() : typeKey.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? typeKey : displayName.Trim();
            description = description == null ? string.Empty : description.Trim();
            styleKey = string.IsNullOrWhiteSpace(styleKey) ? "curve" : styleKey.Trim();
            fromNodeTypeKeys = PungentBoardPortDefinition.NormalizeKeys(fromNodeTypeKeys);
            toNodeTypeKeys = PungentBoardPortDefinition.NormalizeKeys(toNodeTypeKeys);
            fromPortKey = fromPortKey == null ? string.Empty : fromPortKey.Trim();
            toPortKey = toPortKey == null ? string.Empty : toPortKey.Trim();
            propertyDefinitions = propertyDefinitions ?? new List<PungentBoardGraphPropertyDefinition>();
            for (int i = propertyDefinitions.Count - 1; i >= 0; i--)
            {
                if (propertyDefinitions[i] == null)
                {
                    propertyDefinitions.RemoveAt(i);
                    continue;
                }

                propertyDefinitions[i].NormalizeInPlace();
            }
        }

        public bool AllowsConnection(string fromNodeTypeKey, string toNodeTypeKey)
        {
            return AllowsFrom(fromNodeTypeKey) && AllowsTo(toNodeTypeKey);
        }

        public bool AllowsFrom(string nodeTypeKey)
        {
            return fromNodeTypeKeys == null || fromNodeTypeKeys.Count == 0 || fromNodeTypeKeys.Any(key => string.Equals(key, nodeTypeKey, StringComparison.OrdinalIgnoreCase));
        }

        public bool AllowsTo(string nodeTypeKey)
        {
            return toNodeTypeKeys == null || toNodeTypeKeys.Count == 0 || toNodeTypeKeys.Any(key => string.Equals(key, nodeTypeKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    public sealed class PungentBoardGraphTypeDefinition
    {
        public string id = PungentBoardBuiltInGraphTypes.FreeformWhiteboard;
        public string displayName = "Freeform Whiteboard";
        public string description = "Flexible spatial whiteboard for notes, references, groups, and loose relationships.";
        public List<string> tags = new List<string>();
        public bool allowCycles = true;
        public bool directedEdgesDefault = true;
        public bool requiresRootNode;
        public string rootNodeTypeKey = string.Empty;
        public string defaultNodeTypeKey = string.Empty;
        public string defaultEdgeTypeKey = string.Empty;
        public int schemaVersion = 1;
        public List<PungentBoardNodeTypeDefinition> nodeTypes = new List<PungentBoardNodeTypeDefinition>();
        public List<PungentBoardEdgeRuleDefinition> edgeRules = new List<PungentBoardEdgeRuleDefinition>();
        public PungentBoardExecutionMetadata executionMetadata = new PungentBoardExecutionMetadata();

        public void NormalizeInPlace()
        {
            id = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(id);
            displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim();
            description = description == null ? string.Empty : description.Trim();
            tags = PungentBoardPortDefinition.NormalizeKeys(tags);
            rootNodeTypeKey = rootNodeTypeKey == null ? string.Empty : rootNodeTypeKey.Trim();
            defaultNodeTypeKey = defaultNodeTypeKey == null ? string.Empty : defaultNodeTypeKey.Trim();
            defaultEdgeTypeKey = defaultEdgeTypeKey == null ? string.Empty : defaultEdgeTypeKey.Trim();
            schemaVersion = Mathf.Max(1, schemaVersion);
            nodeTypes = nodeTypes ?? new List<PungentBoardNodeTypeDefinition>();
            edgeRules = edgeRules ?? new List<PungentBoardEdgeRuleDefinition>();
            executionMetadata = executionMetadata ?? new PungentBoardExecutionMetadata();

            for (int i = nodeTypes.Count - 1; i >= 0; i--)
            {
                if (nodeTypes[i] == null)
                {
                    nodeTypes.RemoveAt(i);
                    continue;
                }

                nodeTypes[i].NormalizeInPlace();
            }

            for (int i = edgeRules.Count - 1; i >= 0; i--)
            {
                if (edgeRules[i] == null)
                {
                    edgeRules.RemoveAt(i);
                    continue;
                }

                edgeRules[i].NormalizeInPlace();
            }

            executionMetadata.NormalizeInPlace();
            if (string.IsNullOrWhiteSpace(defaultNodeTypeKey) && nodeTypes.Count > 0)
                defaultNodeTypeKey = nodeTypes[0].typeKey;
            if (string.IsNullOrWhiteSpace(rootNodeTypeKey) && nodeTypes.Count > 0)
            {
                PungentBoardNodeTypeDefinition root = nodeTypes.FirstOrDefault(type => type != null && type.canBeRoot);
                rootNodeTypeKey = root != null ? root.typeKey : string.Empty;
            }
            if (string.IsNullOrWhiteSpace(defaultEdgeTypeKey) && edgeRules.Count > 0)
                defaultEdgeTypeKey = edgeRules[0].typeKey;
        }

        public PungentBoardNodeTypeDefinition FindNodeType(string typeKey)
        {
            string clean = string.IsNullOrWhiteSpace(typeKey) ? defaultNodeTypeKey : typeKey.Trim();
            return nodeTypes == null ? null : nodeTypes.FirstOrDefault(type => type != null && string.Equals(type.typeKey, clean, StringComparison.OrdinalIgnoreCase));
        }

        public PungentBoardNodeTypeDefinition GetDefaultNodeType()
        {
            return FindNodeType(defaultNodeTypeKey) ?? (nodeTypes != null && nodeTypes.Count > 0 ? nodeTypes[0] : null);
        }

        public PungentBoardEdgeRuleDefinition FindEdgeRule(string typeKey)
        {
            string clean = string.IsNullOrWhiteSpace(typeKey) ? defaultEdgeTypeKey : typeKey.Trim();
            return edgeRules == null ? null : edgeRules.FirstOrDefault(rule => rule != null && string.Equals(rule.typeKey, clean, StringComparison.OrdinalIgnoreCase));
        }

        public PungentBoardEdgeRuleDefinition FindAllowedEdgeRule(string fromNodeTypeKey, string toNodeTypeKey, string preferredEdgeTypeKey = "")
        {
            PungentBoardEdgeRuleDefinition preferred = FindEdgeRule(preferredEdgeTypeKey);
            if (preferred != null && preferred.AllowsConnection(fromNodeTypeKey, toNodeTypeKey))
                return preferred;

            return edgeRules == null ? null : edgeRules.FirstOrDefault(rule => rule != null && rule.AllowsConnection(fromNodeTypeKey, toNodeTypeKey));
        }
    }
}
