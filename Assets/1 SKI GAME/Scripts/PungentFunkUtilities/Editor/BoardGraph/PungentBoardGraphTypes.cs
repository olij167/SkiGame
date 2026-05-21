using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.BoardGraph;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public interface IPungentBoardGraphTypeProvider
    {
        string ProviderId { get; }
        IEnumerable<PungentBoardGraphTypeDefinition> GetGraphTypes();
    }

    [Serializable]
    public sealed class PungentBoardGraphTypeDatabase
    {
        public int schemaVersion = 1;
        public string lastSavedUtc = string.Empty;
        public List<PungentBoardGraphTypeDefinition> customGraphTypes = new List<PungentBoardGraphTypeDefinition>();

        public void NormalizeInPlace()
        {
            customGraphTypes = customGraphTypes ?? new List<PungentBoardGraphTypeDefinition>();
            for (int i = customGraphTypes.Count - 1; i >= 0; i--)
            {
                if (customGraphTypes[i] == null)
                {
                    customGraphTypes.RemoveAt(i);
                    continue;
                }

                customGraphTypes[i].NormalizeInPlace();
            }
        }
    }

    public static class PungentBoardGraphTypeStorage
    {
        private const string StorageFileName = "BoardGraphTypes.json";

        private static PungentBoardGraphTypeDatabase _database;
        private static string _loadError = string.Empty;

        public static string StoragePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "PungentFunkUtilities", StorageFileName);
        public static string LoadError => _loadError;

        public static PungentBoardGraphTypeDatabase Database
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
            if (!File.Exists(StoragePath))
            {
                _database = new PungentBoardGraphTypeDatabase();
                _database.NormalizeInPlace();
                PungentBoardGraphTypeRegistry.MarkDirty();
                return;
            }

            try
            {
                string json = File.ReadAllText(StoragePath);
                _database = string.IsNullOrWhiteSpace(json)
                    ? new PungentBoardGraphTypeDatabase()
                    : JsonUtility.FromJson<PungentBoardGraphTypeDatabase>(json);
                if (_database == null)
                    _database = new PungentBoardGraphTypeDatabase();
                _database.NormalizeInPlace();
                PungentBoardGraphTypeRegistry.MarkDirty();
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                _database = new PungentBoardGraphTypeDatabase();
                _database.NormalizeInPlace();
                Debug.LogWarning("PungentFunk Board graph type definitions could not be loaded. Built-in graph types remain available. " + _loadError);
                PungentBoardGraphTypeRegistry.MarkDirty();
            }
        }

        public static bool Save(out string error)
        {
            error = string.Empty;
            try
            {
                PungentBoardGraphTypeDatabase database = Database;
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
                PungentBoardGraphTypeRegistry.MarkDirty();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public static class PungentBoardGraphTypeRegistry
    {
        private static readonly List<IPungentBoardGraphTypeProvider> Providers = new List<IPungentBoardGraphTypeProvider>();
        private static readonly Dictionary<string, PungentBoardGraphTypeDefinition> Cache = new Dictionary<string, PungentBoardGraphTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        private static bool _builtInsRegistered;
        private static bool _cacheDirty = true;

        public static IReadOnlyList<PungentBoardGraphTypeDefinition> GraphTypes
        {
            get
            {
                EnsureCache();
                return Cache.Values.OrderBy(type => type.displayName).ToList();
            }
        }

        public static void EnsureBuiltInsRegistered()
        {
            if (_builtInsRegistered)
                return;

            Register(new PungentBoardBuiltInGraphTypeProvider());
            _builtInsRegistered = true;
        }

        public static void Register(IPungentBoardGraphTypeProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;

            if (Providers.Any(item => item != null && string.Equals(item.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase)))
                return;

            Providers.Add(provider);
            _cacheDirty = true;
        }

        public static PungentBoardGraphTypeDefinition Find(string graphTypeId)
        {
            EnsureCache();
            PungentBoardGraphTypeDefinition definition;
            return Cache.TryGetValue(PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(graphTypeId), out definition) ? definition : null;
        }

        public static PungentBoardGraphTypeDefinition FindOrFreeform(string graphTypeId)
        {
            return Find(graphTypeId) ?? Find(PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
        }

        public static bool IsMissing(string graphTypeId)
        {
            return Find(graphTypeId) == null;
        }

        public static void MarkDirty()
        {
            _cacheDirty = true;
        }

        private static void EnsureCache()
        {
            EnsureBuiltInsRegistered();
            PungentBoardGraphTypeStorage.EnsureLoaded();
            if (!_cacheDirty)
                return;

            Cache.Clear();
            foreach (IPungentBoardGraphTypeProvider provider in Providers.ToArray())
            {
                if (provider == null)
                    continue;

                IEnumerable<PungentBoardGraphTypeDefinition> types;
                try
                {
                    types = provider.GetGraphTypes();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Board graph type provider '" + provider.ProviderId + "' failed: " + ex.Message);
                    continue;
                }

                AddDefinitions(types);
            }

            AddDefinitions(PungentBoardGraphTypeStorage.Database.customGraphTypes);
            _cacheDirty = false;
        }

        private static void AddDefinitions(IEnumerable<PungentBoardGraphTypeDefinition> definitions)
        {
            foreach (PungentBoardGraphTypeDefinition definition in definitions ?? new PungentBoardGraphTypeDefinition[0])
            {
                if (definition == null)
                    continue;

                definition.NormalizeInPlace();
                Cache[definition.id] = definition;
            }
        }
    }

    public sealed class PungentBoardBuiltInGraphTypeProvider : IPungentBoardGraphTypeProvider
    {
        public string ProviderId => "pungent-board-built-in-graph-types";

        public IEnumerable<PungentBoardGraphTypeDefinition> GetGraphTypes()
        {
            yield return FreeformWhiteboard();
            yield return DialogueQuestFlow();
            yield return BehaviourTree();
            yield return AiStateMachine();
            yield return EventSequence();
            yield return SkillTree();
            yield return DependencyMap();
        }

        private static PungentBoardGraphTypeDefinition FreeformWhiteboard()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.FreeformWhiteboard,
                "Freeform Whiteboard",
                "Loose planning board for cards, references, groups, and informal relationships.",
                PungentBoardExecutionMode.None,
                true,
                false,
                "note-card");

            graph.nodeTypes.Add(Node("note-card", "Note Card", PungentBoardNodeKind.NoteCard, "note", "Note Card", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("document-reference", "Document Reference", PungentBoardNodeKind.DocumentReference, "document", "Document Reference", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("utility-reference", "Utility Reference", PungentBoardNodeKind.UtilityReference, "utility", "Utility Reference", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("token-reference", "Token Reference", PungentBoardNodeKind.TokenReference, "token", "Token Reference", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("documentation-link", "Documentation Link", PungentBoardNodeKind.DocumentationLink, "documentation", "Documentation Link", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("audit-finding", "Audit Finding", PungentBoardNodeKind.AuditFinding, "audit", "Audit Finding", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("task", "Task", PungentBoardNodeKind.Task, "task", "Task", false, false, false, Ports()));
            graph.nodeTypes.Add(Node("freeform-card", "Freeform Card", PungentBoardNodeKind.FreeformCard, "freeform", "Freeform Card", false, false, false, Ports()));
            graph.edgeRules.Add(Rule("related", "Related", PungentBoardEdgeKind.Related, "curve", false, true));
            graph.edgeRules.Add(Rule("leads-to", "Leads To", PungentBoardEdgeKind.LeadsTo, "curve", true, true));
            graph.edgeRules.Add(Rule("depends-on", "Depends On", PungentBoardEdgeKind.DependsOn, "elbow", true, true));
            graph.defaultEdgeTypeKey = "related";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition DialogueQuestFlow()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.DialogueQuestFlow,
                "Dialogue / Quest Flow",
                "Branching narrative, quest beats, player choices, and follow-up tasks.",
                PungentBoardExecutionMode.Flow,
                false,
                true,
                "start");
            graph.rootNodeTypeKey = "start";

            graph.nodeTypes.Add(Node("start", "Start", PungentBoardNodeKind.NoteCard, "start", "Start", true, true, false, Ports(null, Output("next", true))));
            graph.nodeTypes.Add(Node("dialogue-beat", "Dialogue Beat", PungentBoardNodeKind.FreeformCard, "dialogue", "Dialogue Beat", false, true, false, Ports(Input("in", true), Output("next", false))));
            graph.nodeTypes.Add(Node("choice", "Choice", PungentBoardNodeKind.FreeformCard, "choice", "Choice", false, true, false, Ports(Input("in", true), Output("option", true))));
            graph.nodeTypes.Add(Node("quest-task", "Quest Task", PungentBoardNodeKind.Task, "task", "Quest Task", false, true, false, Ports(Input("in", true), Output("complete", false))));
            graph.nodeTypes.Add(Node("outcome", "Outcome", PungentBoardNodeKind.NoteCard, "outcome", "Outcome", false, true, true, Ports(Input("in", true), null)));
            graph.edgeRules.Add(Rule("flow", "Flow", PungentBoardEdgeKind.Sequence, "curve", true, false, Any(), Any(), "next", "in"));
            graph.edgeRules.Add(Rule("choice", "Choice", PungentBoardEdgeKind.LeadsTo, "curve", true, false, Keys("choice"), Keys("dialogue-beat", "quest-task", "outcome"), "option", "in"));
            graph.defaultEdgeTypeKey = "flow";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition BehaviourTree()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.BehaviourTree,
                "Behaviour Tree",
                "Planning schema for root, composite, decorator, condition, and action behaviour nodes.",
                PungentBoardExecutionMode.BehaviourTree,
                false,
                true,
                "root");
            graph.rootNodeTypeKey = "root";
            graph.executionMetadata.tickDriven = true;

            graph.nodeTypes.Add(Node("root", "Root", PungentBoardNodeKind.FreeformCard, "root", "Root", true, true, false, Ports(null, Output("child", true))));
            graph.nodeTypes.Add(Node("selector", "Selector", PungentBoardNodeKind.FreeformCard, "selector", "Selector", false, true, false, Ports(Input("parent", true), Output("child", true))));
            graph.nodeTypes.Add(Node("sequence", "Sequence", PungentBoardNodeKind.FreeformCard, "sequence", "Sequence", false, true, false, Ports(Input("parent", true), Output("child", true))));
            graph.nodeTypes.Add(Node("condition", "Condition", PungentBoardNodeKind.FreeformCard, "condition", "Condition", false, true, true, Ports(Input("parent", true), null)));
            graph.nodeTypes.Add(Node("action", "Action", PungentBoardNodeKind.Task, "action", "Action", false, true, true, Ports(Input("parent", true), null)));
            graph.nodeTypes.Add(Node("decorator", "Decorator", PungentBoardNodeKind.FreeformCard, "decorator", "Decorator", false, true, false, Ports(Input("parent", true), Output("child", true))));
            graph.edgeRules.Add(Rule("child", "Child", PungentBoardEdgeKind.ParentChild, "elbow", true, false, Keys("root", "selector", "sequence", "decorator"), Keys("selector", "sequence", "condition", "action", "decorator"), "child", "parent"));
            graph.defaultEdgeTypeKey = "child";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition AiStateMachine()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.AiStateMachine,
                "AI State Machine",
                "State, decision, transition, entry, and exit planning schema for AI behaviour.",
                PungentBoardExecutionMode.StateMachine,
                false,
                true,
                "state");
            graph.rootNodeTypeKey = "entry";

            graph.nodeTypes.Add(Node("entry", "Entry", PungentBoardNodeKind.FreeformCard, "entry", "Entry", true, true, false, Ports(null, Output("transition", true))));
            graph.nodeTypes.Add(Node("state", "State", PungentBoardNodeKind.FreeformCard, "state", "State", false, true, false, Ports(Input("in", true), Output("transition", false))));
            graph.nodeTypes.Add(Node("transition", "Transition", PungentBoardNodeKind.FreeformCard, "transition", "Transition", false, true, false, Ports(Input("from", true), Output("to", true))));
            graph.nodeTypes.Add(Node("decision", "Decision", PungentBoardNodeKind.FreeformCard, "decision", "Decision", false, true, false, Ports(Input("in", true), Output("transition", true))));
            graph.nodeTypes.Add(Node("exit", "Exit", PungentBoardNodeKind.FreeformCard, "exit", "Exit", false, true, true, Ports(Input("in", true), null)));
            graph.edgeRules.Add(Rule("transition", "Transition", PungentBoardEdgeKind.LeadsTo, "curve", true, false, Keys("entry", "state", "decision", "transition"), Keys("state", "decision", "transition", "exit"), "transition", "in"));
            graph.defaultEdgeTypeKey = "transition";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition EventSequence()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.EventSequence,
                "Event Sequence",
                "Event, timing, camera, trigger, wait, and branch schema for authored sequences.",
                PungentBoardExecutionMode.Sequence,
                false,
                false,
                "event");

            graph.nodeTypes.Add(Node("event", "Event", PungentBoardNodeKind.FreeformCard, "event", "Event", true, true, false, Ports(Input("in", false), Output("next", false))));
            graph.nodeTypes.Add(Node("delay", "Delay", PungentBoardNodeKind.FreeformCard, "delay", "Delay", false, true, false, Ports(Input("in", true), Output("next", true))));
            graph.nodeTypes.Add(Node("camera-move", "Camera Move", PungentBoardNodeKind.FreeformCard, "camera", "Camera Move", false, true, false, Ports(Input("in", true), Output("done", true))));
            graph.nodeTypes.Add(Node("trigger", "Trigger", PungentBoardNodeKind.FreeformCard, "trigger", "Trigger", false, true, false, Ports(Input("in", true), Output("done", true))));
            graph.nodeTypes.Add(Node("wait", "Wait", PungentBoardNodeKind.FreeformCard, "wait", "Wait", false, true, false, Ports(Input("in", true), Output("done", true))));
            graph.nodeTypes.Add(Node("branch", "Branch", PungentBoardNodeKind.FreeformCard, "branch", "Branch", false, true, false, Ports(Input("in", true), Output("true", false), Output("false", false))));
            graph.edgeRules.Add(Rule("sequence", "Sequence", PungentBoardEdgeKind.Sequence, "curve", true, false, Any(), Any(), string.Empty, "in"));
            graph.edgeRules.Add(Rule("branch", "Branch", PungentBoardEdgeKind.LeadsTo, "curve", true, false, Keys("branch"), Any(), string.Empty, "in"));
            graph.defaultEdgeTypeKey = "sequence";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition SkillTree()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.SkillTree,
                "Skill Tree",
                "Skill, requirement, gate, unlock, and modifier schema for progression graphs.",
                PungentBoardExecutionMode.SkillTree,
                false,
                false,
                "skill");

            graph.nodeTypes.Add(Node("skill", "Skill", PungentBoardNodeKind.FreeformCard, "skill", "Skill", true, true, false, Ports(Input("requires", false), Output("unlocks", false))));
            graph.nodeTypes.Add(Node("requirement", "Requirement", PungentBoardNodeKind.FreeformCard, "requirement", "Requirement", false, true, false, Ports(Input("in", false), Output("unlocks", true))));
            graph.nodeTypes.Add(Node("unlock", "Unlock", PungentBoardNodeKind.FreeformCard, "unlock", "Unlock", false, true, false, Ports(Input("requires", true), Output("unlocks", false))));
            graph.nodeTypes.Add(Node("modifier", "Modifier", PungentBoardNodeKind.FreeformCard, "modifier", "Modifier", false, true, true, Ports(Input("requires", true), null)));
            graph.nodeTypes.Add(Node("gate", "Gate", PungentBoardNodeKind.FreeformCard, "gate", "Gate", false, true, false, Ports(Input("in", true), Output("unlocks", true))));
            graph.edgeRules.Add(Rule("unlocks", "Unlocks", PungentBoardEdgeKind.LeadsTo, "curve", true, false, Keys("skill", "requirement", "unlock", "gate"), Keys("skill", "unlock", "modifier", "gate"), "unlocks", "requires"));
            graph.edgeRules.Add(Rule("requires", "Requires", PungentBoardEdgeKind.DependsOn, "elbow", true, false, Keys("skill", "modifier", "gate"), Keys("skill", "requirement", "gate"), "requires", "unlocks"));
            graph.defaultEdgeTypeKey = "unlocks";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition DependencyMap()
        {
            PungentBoardGraphTypeDefinition graph = Graph(
                PungentBoardBuiltInGraphTypes.DependencyMap,
                "Dependency Map",
                "System, utility, package, provider, and external dependency mapping.",
                PungentBoardExecutionMode.Dependency,
                false,
                false,
                "system");

            graph.nodeTypes.Add(Node("system", "System", PungentBoardNodeKind.FreeformCard, "system", "System", true, false, false, Ports(Input("in", false), Output("requires", false))));
            graph.nodeTypes.Add(Node("utility", "Utility", PungentBoardNodeKind.UtilityReference, "utility", "Utility", false, false, false, Ports(Input("used-by", false), Output("requires", false))));
            graph.nodeTypes.Add(Node("provider", "Authoring Provider", PungentBoardNodeKind.UtilityReference, "provider", "Authoring Provider", false, false, false, Ports(Input("used-by", false), Output("provides", false))));
            graph.nodeTypes.Add(Node("package", "Package", PungentBoardNodeKind.FreeformCard, "package", "Package", false, false, false, Ports(Input("used-by", false), Output("requires", false))));
            graph.nodeTypes.Add(Node("external", "External Source", PungentBoardNodeKind.FreeformCard, "external", "External Source", false, false, false, Ports(Input("in", false), Output("out", false))));
            graph.edgeRules.Add(Rule("depends-on", "Depends On", PungentBoardEdgeKind.DependsOn, "elbow", true, false, Any(), Any(), "requires", "used-by"));
            graph.edgeRules.Add(Rule("provides", "Provides", PungentBoardEdgeKind.References, "curve", true, true, Any(), Any(), "provides", "in"));
            graph.defaultEdgeTypeKey = "depends-on";
            graph.NormalizeInPlace();
            return graph;
        }

        private static PungentBoardGraphTypeDefinition Graph(string id, string displayName, string description, PungentBoardExecutionMode mode, bool allowCycles, bool requiresRoot, string defaultNodeType)
        {
            PungentBoardGraphTypeDefinition graph = new PungentBoardGraphTypeDefinition
            {
                id = id,
                displayName = displayName,
                description = description,
                allowCycles = allowCycles,
                requiresRootNode = requiresRoot,
                defaultNodeTypeKey = defaultNodeType,
                directedEdgesDefault = true,
                executionMetadata = new PungentBoardExecutionMetadata
                {
                    executionMode = mode,
                    entryNodeTypeKey = defaultNodeType,
                    allowRuntimeExecution = false
                }
            };
            return graph;
        }

        private static PungentBoardNodeTypeDefinition Node(string typeKey, string displayName, PungentBoardNodeKind kind, string styleKey, string defaultTitle, bool root, bool execution, bool terminal, IEnumerable<PungentBoardPortDefinition> ports)
        {
            PungentBoardNodeTypeDefinition node = new PungentBoardNodeTypeDefinition
            {
                typeKey = typeKey,
                displayName = displayName,
                defaultTitle = defaultTitle,
                nodeKind = kind,
                styleKey = styleKey,
                canBeRoot = root,
                isExecutionNode = execution,
                isTerminalNode = terminal,
                ports = (ports ?? new PungentBoardPortDefinition[0]).Where(port => port != null).ToList(),
                propertyDefinitions = DefaultNodeProperties(typeKey),
                inspectorFieldKeys = new List<string> { "title", "summary", "body", "status", "tags", "linkedAuthoringRef" }
            };
            node.NormalizeInPlace();
            return node;
        }

        private static PungentBoardPortDefinition Input(string key, bool required)
        {
            return Port(key, key, PungentBoardPortDirection.Input, required);
        }

        private static PungentBoardPortDefinition Output(string key, bool required)
        {
            return Port(key, key, PungentBoardPortDirection.Output, required);
        }

        private static PungentBoardPortDefinition Port(string key, string displayName, PungentBoardPortDirection direction, bool required)
        {
            PungentBoardPortDefinition port = new PungentBoardPortDefinition
            {
                key = key,
                displayName = displayName,
                direction = direction,
                required = required,
                allowMultipleConnections = !required
            };
            port.NormalizeInPlace();
            return port;
        }

        private static IEnumerable<PungentBoardPortDefinition> Ports(params PungentBoardPortDefinition[] ports)
        {
            if (ports == null || ports.Length == 0)
            {
                yield return Input("in", false);
                yield return Output("out", false);
                yield break;
            }

            foreach (PungentBoardPortDefinition port in ports)
                if (port != null)
                    yield return port;
        }

        private static PungentBoardEdgeRuleDefinition Rule(string typeKey, string displayName, PungentBoardEdgeKind kind, string styleKey, bool directed, bool allowCycles)
        {
            return Rule(typeKey, displayName, kind, styleKey, directed, allowCycles, Any(), Any(), string.Empty, string.Empty);
        }

        private static PungentBoardEdgeRuleDefinition Rule(string typeKey, string displayName, PungentBoardEdgeKind kind, string styleKey, bool directed, bool allowCycles, List<string> fromTypes, List<string> toTypes, string fromPort, string toPort)
        {
            PungentBoardEdgeRuleDefinition rule = new PungentBoardEdgeRuleDefinition
            {
                typeKey = typeKey,
                displayName = displayName,
                edgeKind = kind,
                styleKey = styleKey,
                directed = directed,
                allowCycles = allowCycles,
                fromNodeTypeKeys = fromTypes,
                toNodeTypeKeys = toTypes,
                fromPortKey = fromPort,
                toPortKey = toPort,
                propertyDefinitions = DefaultEdgeProperties(typeKey)
            };
            rule.NormalizeInPlace();
            return rule;
        }

        private static List<PungentBoardGraphPropertyDefinition> DefaultNodeProperties(string typeKey)
        {
            switch ((typeKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "note-card":
                    return Props(Prop("noteRole", "Note Role", PungentBoardGraphPropertyValueKind.Enum, "Context", "Question", "Decision", "Risk"), Prop("owner", "Owner"));
                case "document-reference":
                    return Props(Prop("documentKey", "Document Key"), Prop("sectionAnchor", "Section Anchor"));
                case "utility-reference":
                    return Props(Prop("utilityId", "Utility ID"), Prop("packageId", "Package ID"));
                case "token-reference":
                    return Props(Prop("tokenKey", "Token Key"), Prop("tokenScope", "Token Scope"));
                case "documentation-link":
                    return Props(Prop("urlOrTopic", "URL Or Topic"), Prop("sectionAnchor", "Section Anchor"));
                case "audit-finding":
                    return Props(Prop("findingId", "Finding ID"), Prop("severity", "Severity", PungentBoardGraphPropertyValueKind.Enum, "Info", "Warning", "Error", "Critical"), Prop("fixStatus", "Fix Status", PungentBoardGraphPropertyValueKind.Enum, "Open", "Accepted", "Fixed", "Deferred"));
                case "task":
                    return Props(Prop("assignee", "Assignee"), Prop("dueDate", "Due Date"), Prop("estimate", "Estimate"));
                case "freeform-card":
                    return Props(Prop("cardRole", "Card Role", PungentBoardGraphPropertyValueKind.Enum, "Idea", "Constraint", "Decision", "Reference"), Prop("sourceKey", "Source Key"));
                case "start":
                    return Props(Prop("entryKey", "Entry Key"), Prop("autoStart", "Auto Start", PungentBoardGraphPropertyValueKind.Boolean, "true"));
                case "outcome":
                    return Props(Prop("outcomeKey", "Outcome Key"), Prop("resultState", "Result State"));
                case "root":
                    return Props(Prop("rootKey", "Root Key"), Prop("entryPolicy", "Entry Policy", PungentBoardGraphPropertyValueKind.Enum, "SingleChild", "FirstChild"), Prop("tickInterval", "Tick Interval", PungentBoardGraphPropertyValueKind.Number, "0"));
                case "selector":
                    return Props(Prop("selectionMode", "Selection Mode", PungentBoardGraphPropertyValueKind.Enum, "Priority", "Random", "Weighted"), Prop("abortPolicy", "Abort Policy", PungentBoardGraphPropertyValueKind.Enum, "None", "Self", "LowerPriority", "Both"));
                case "sequence":
                    return Props(Prop("failurePolicy", "Failure Policy", PungentBoardGraphPropertyValueKind.Enum, "StopOnFailure", "Continue"), Prop("successPolicy", "Success Policy", PungentBoardGraphPropertyValueKind.Enum, "AllChildren", "AnyChild"));
                case "dialogue-beat":
                    return Props(Prop("speakerId", "Speaker ID"), Prop("lineKey", "Line Key"), Prop("voiceCue", "Voice Cue"));
                case "choice":
                    return Props(Prop("choiceText", "Choice Text", PungentBoardGraphPropertyValueKind.LongText), Prop("conditionKey", "Condition Key"));
                case "quest-task":
                    return Props(Prop("taskKey", "Task Key"), Prop("completionCondition", "Completion Condition", PungentBoardGraphPropertyValueKind.LongText));
                case "condition":
                    return Props(Prop("conditionKey", "Condition Key"), Prop("invert", "Invert", PungentBoardGraphPropertyValueKind.Boolean));
                case "action":
                    return Props(Prop("actionKey", "Action Key"), Prop("parametersJson", "Parameters JSON", PungentBoardGraphPropertyValueKind.Json));
                case "decorator":
                    return Props(Prop("decoratorKey", "Decorator Key"), Prop("policy", "Policy", PungentBoardGraphPropertyValueKind.Enum, "Once", "Always", "UntilSuccess", "UntilFailure"));
                case "entry":
                    return Props(Prop("entryKey", "Entry Key"), Prop("autoEnter", "Auto Enter", PungentBoardGraphPropertyValueKind.Boolean, "true"));
                case "exit":
                    return Props(Prop("exitKey", "Exit Key"), Prop("exitResult", "Exit Result"));
                case "state":
                    return Props(Prop("stateKey", "State Key"), Prop("enterAction", "Enter Action"), Prop("exitAction", "Exit Action"));
                case "transition":
                    return Props(Prop("conditionKey", "Condition Key"), Prop("priority", "Priority", PungentBoardGraphPropertyValueKind.Number));
                case "decision":
                    return Props(Prop("decisionKey", "Decision Key"), Prop("expression", "Expression", PungentBoardGraphPropertyValueKind.LongText));
                case "event":
                    return Props(Prop("eventKey", "Event Key"), Prop("payloadJson", "Payload JSON", PungentBoardGraphPropertyValueKind.Json));
                case "delay":
                    return Props(Prop("durationSeconds", "Duration Seconds", PungentBoardGraphPropertyValueKind.Number));
                case "camera-move":
                    return Props(Prop("cameraTarget", "Camera Target"), Prop("durationSeconds", "Duration Seconds", PungentBoardGraphPropertyValueKind.Number), Prop("easing", "Easing", PungentBoardGraphPropertyValueKind.Enum, "Linear", "EaseIn", "EaseOut", "EaseInOut"));
                case "trigger":
                    return Props(Prop("triggerKey", "Trigger Key"), Prop("payloadJson", "Payload JSON", PungentBoardGraphPropertyValueKind.Json));
                case "wait":
                    return Props(Prop("waitFor", "Wait For"), Prop("timeoutSeconds", "Timeout Seconds", PungentBoardGraphPropertyValueKind.Number));
                case "branch":
                    return Props(Prop("conditionKey", "Condition Key"), Prop("falseFallback", "False Fallback"));
                case "skill":
                    return Props(Prop("skillId", "Skill ID"), Prop("cost", "Cost", PungentBoardGraphPropertyValueKind.Number), Prop("maxRank", "Max Rank", PungentBoardGraphPropertyValueKind.Number, "1"));
                case "requirement":
                    return Props(Prop("requirementKey", "Requirement Key"), Prop("requiredValue", "Required Value"));
                case "unlock":
                    return Props(Prop("unlockKey", "Unlock Key"), Prop("unlockMessage", "Unlock Message", PungentBoardGraphPropertyValueKind.LongText));
                case "modifier":
                    return Props(Prop("modifierKey", "Modifier Key"), Prop("amount", "Amount", PungentBoardGraphPropertyValueKind.Number));
                case "gate":
                    return Props(Prop("gateKey", "Gate Key"), Prop("mode", "Mode", PungentBoardGraphPropertyValueKind.Enum, "All", "Any"));
                case "system":
                case "utility":
                case "provider":
                case "package":
                case "external":
                    return Props(Prop("sourceKey", "Source Key"), Prop("owner", "Owner"));
                default:
                    return new List<PungentBoardGraphPropertyDefinition>();
            }
        }

        private static List<PungentBoardGraphPropertyDefinition> DefaultEdgeProperties(string typeKey)
        {
            switch ((typeKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "choice":
                case "branch":
                    return Props(Prop("conditionKey", "Condition Key"), Prop("labelOverride", "Label Override"));
                case "transition":
                    return Props(Prop("conditionKey", "Condition Key"), Prop("priority", "Priority", PungentBoardGraphPropertyValueKind.Number));
                case "child":
                    return Props(Prop("order", "Order", PungentBoardGraphPropertyValueKind.Number));
                case "sequence":
                    return Props(Prop("order", "Order", PungentBoardGraphPropertyValueKind.Number), Prop("delaySeconds", "Delay Seconds", PungentBoardGraphPropertyValueKind.Number));
                case "unlocks":
                case "requires":
                    return Props(Prop("requirementNote", "Requirement Note", PungentBoardGraphPropertyValueKind.LongText));
                default:
                    return new List<PungentBoardGraphPropertyDefinition>();
            }
        }

        private static PungentBoardGraphPropertyDefinition Prop(string key, string displayName, PungentBoardGraphPropertyValueKind kind = PungentBoardGraphPropertyValueKind.Text, string defaultValue = "")
        {
            PungentBoardGraphPropertyDefinition property = new PungentBoardGraphPropertyDefinition
            {
                key = key,
                displayName = displayName,
                valueKind = kind,
                defaultValue = defaultValue
            };
            property.NormalizeInPlace();
            return property;
        }

        private static PungentBoardGraphPropertyDefinition Prop(string key, string displayName, PungentBoardGraphPropertyValueKind kind, params string[] options)
        {
            PungentBoardGraphPropertyDefinition property = Prop(key, displayName, kind, options != null && options.Length > 0 ? options[0] : string.Empty);
            property.options = (options ?? new string[0]).ToList();
            property.NormalizeInPlace();
            return property;
        }

        private static List<PungentBoardGraphPropertyDefinition> Props(params PungentBoardGraphPropertyDefinition[] properties)
        {
            return (properties ?? new PungentBoardGraphPropertyDefinition[0]).Where(property => property != null).ToList();
        }

        private static List<string> Any()
        {
            return new List<string>();
        }

        private static List<string> Keys(params string[] values)
        {
            return (values ?? new string[0]).ToList();
        }
    }

    [InitializeOnLoad]
    public static class PungentBoardGraphTypeBootstrap
    {
        static PungentBoardGraphTypeBootstrap()
        {
            PungentBoardGraphTypeRegistry.EnsureBuiltInsRegistered();
        }
    }
#endif
}
