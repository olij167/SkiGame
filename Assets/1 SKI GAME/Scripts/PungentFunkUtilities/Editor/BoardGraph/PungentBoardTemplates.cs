using PungentFunk.Utilities.BoardGraph;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public enum PungentBoardTemplateKind
    {
        BlankBoard = 0,
        FeaturePlanningBoard = 10,
        DialogueQuestFlowBoard = 20,
        UtilityDependencyMap = 30,
        AuditFollowUpBoard = 40,
        TokenDocumentationMap = 50,
        ProjectComponentMap = 60,
        BehaviourTreeBoard = 70,
        AiStateMachineBoard = 80,
        EventSequenceBoard = 90,
        SkillTreeBoard = 100
    }

    public static class PungentBoardTemplates
    {
        public static readonly PungentBoardTemplateKind[] All =
        {
            PungentBoardTemplateKind.BlankBoard,
            PungentBoardTemplateKind.FeaturePlanningBoard,
            PungentBoardTemplateKind.DialogueQuestFlowBoard,
            PungentBoardTemplateKind.UtilityDependencyMap,
            PungentBoardTemplateKind.AuditFollowUpBoard,
            PungentBoardTemplateKind.TokenDocumentationMap,
            PungentBoardTemplateKind.ProjectComponentMap,
            PungentBoardTemplateKind.BehaviourTreeBoard,
            PungentBoardTemplateKind.AiStateMachineBoard,
            PungentBoardTemplateKind.EventSequenceBoard,
            PungentBoardTemplateKind.SkillTreeBoard
        };

        public static string GetDisplayName(PungentBoardTemplateKind kind)
        {
            switch (kind)
            {
                case PungentBoardTemplateKind.FeaturePlanningBoard: return "Feature Planning Board";
                case PungentBoardTemplateKind.DialogueQuestFlowBoard: return "Dialogue / Quest Flow Board";
                case PungentBoardTemplateKind.UtilityDependencyMap: return "Utility Dependency Map";
                case PungentBoardTemplateKind.AuditFollowUpBoard: return "Audit Follow-up Board";
                case PungentBoardTemplateKind.TokenDocumentationMap: return "Token / Documentation Map";
                case PungentBoardTemplateKind.ProjectComponentMap: return "Project Component Map";
                case PungentBoardTemplateKind.BehaviourTreeBoard: return "Behaviour Tree";
                case PungentBoardTemplateKind.AiStateMachineBoard: return "AI State Machine";
                case PungentBoardTemplateKind.EventSequenceBoard: return "Event Sequence";
                case PungentBoardTemplateKind.SkillTreeBoard: return "Skill Tree";
                default: return "Blank Board";
            }
        }

        public static PungentBoardDocument Create(PungentBoardTemplateKind kind)
        {
            PungentBoardDocument document = PungentBoardDocument.Create(GetDisplayName(kind));
            document.templateKey = kind.ToString();
            document.templateDisplayName = GetDisplayName(kind);
            ApplyGraphType(document, GraphTypeForTemplate(kind));
            switch (kind)
            {
                case PungentBoardTemplateKind.FeaturePlanningBoard:
                    BuildFeaturePlanning(document);
                    break;
                case PungentBoardTemplateKind.DialogueQuestFlowBoard:
                    BuildDialogueQuestFlow(document);
                    break;
                case PungentBoardTemplateKind.UtilityDependencyMap:
                    BuildUtilityDependencyMap(document);
                    break;
                case PungentBoardTemplateKind.AuditFollowUpBoard:
                    BuildAuditFollowUp(document);
                    break;
                case PungentBoardTemplateKind.TokenDocumentationMap:
                    BuildTokenDocumentationMap(document);
                    break;
                case PungentBoardTemplateKind.ProjectComponentMap:
                    BuildProjectComponentMap(document);
                    break;
                case PungentBoardTemplateKind.BehaviourTreeBoard:
                    BuildBehaviourTree(document);
                    break;
                case PungentBoardTemplateKind.AiStateMachineBoard:
                    BuildAiStateMachine(document);
                    break;
                case PungentBoardTemplateKind.EventSequenceBoard:
                    BuildEventSequence(document);
                    break;
                case PungentBoardTemplateKind.SkillTreeBoard:
                    BuildSkillTree(document);
                    break;
            }

            document.NormalizeInPlace();
            return document;
        }

        private static void BuildFeaturePlanning(PungentBoardDocument document)
        {
            document.summary = "Plan feature intent, constraints, implementation, validation, and follow-up tasks.";
            PungentBoardNode goal = AddNode(document, PungentBoardNodeKind.NoteCard, "Goal", "What user problem does this board solve?", new Vector2(-420f, -140f));
            PungentBoardNode scope = AddNode(document, PungentBoardNodeKind.FreeformCard, "Scope", "Boundaries, dependencies, and non-goals.", new Vector2(-140f, -140f));
            PungentBoardNode tasks = AddNode(document, PungentBoardNodeKind.Task, "Tasks", "Small implementation steps and owner notes.", new Vector2(160f, -140f));
            PungentBoardNode risks = AddNode(document, PungentBoardNodeKind.FreeformCard, "Risks", "Failure modes, package boundaries, and missing optional integrations.", new Vector2(-140f, 90f));
            PungentBoardNode validation = AddNode(document, PungentBoardNodeKind.Task, "Validation", "Unity checks, missing-provider checks, persistence checks.", new Vector2(160f, 90f));
            AddEdge(document, goal, scope, "shapes", PungentBoardEdgeKind.LeadsTo);
            AddEdge(document, scope, tasks, "guides", PungentBoardEdgeKind.DependsOn);
            AddEdge(document, scope, risks, "exposes", PungentBoardEdgeKind.Related, false);
            AddEdge(document, tasks, validation, "verify", PungentBoardEdgeKind.Sequence);
            document.groups.Add(PungentBoardGroup.Create("Goal / Scope", new Rect(-470f, -190f, 600f, 400f)));
            document.groups.Add(PungentBoardGroup.Create("Tasks / Validation", new Rect(110f, -190f, 340f, 400f)));
        }

        private static void BuildDialogueQuestFlow(PungentBoardDocument document)
        {
            document.summary = "Sketch branching dialogue, quest beats, and task follow-ups without requiring runtime graph execution.";
            PungentBoardNode start = AddNode(document, PungentBoardNodeKind.NoteCard, "Opening Beat", "Conversation or quest entry point.", new Vector2(-360f, -70f), "start");
            PungentBoardNode choice = AddNode(document, PungentBoardNodeKind.FreeformCard, "Choice", "Player-facing branch or authored decision.", new Vector2(-60f, -70f), "choice");
            PungentBoardNode branchA = AddNode(document, PungentBoardNodeKind.FreeformCard, "Branch A", "Outcome or next beat.", new Vector2(240f, -170f), "dialogue-beat");
            PungentBoardNode branchB = AddNode(document, PungentBoardNodeKind.FreeformCard, "Branch B", "Alternate outcome or failure beat.", new Vector2(240f, 40f), "outcome");
            AddEdge(document, start, choice, "leads to", PungentBoardEdgeKind.Sequence, true, "flow", "next", "in");
            AddEdge(document, choice, branchA, "choice", PungentBoardEdgeKind.LeadsTo, true, "choice", "option", "in");
            AddEdge(document, choice, branchB, "choice", PungentBoardEdgeKind.LeadsTo, true, "choice", "option", "in");
        }

        private static void BuildUtilityDependencyMap(PungentBoardDocument document)
        {
            document.summary = "Map utility dependencies, optional bridges, and provider-safe fallbacks.";
            PungentBoardNode core = AddNode(document, PungentBoardNodeKind.UtilityReference, "Core / Foundation", "Shared IDs, metadata, refs, targets, and provider contracts.", new Vector2(-320f, -60f), "utility");
            PungentBoardNode board = AddNode(document, PungentBoardNodeKind.UtilityReference, "Board Extension", "Spatial cards, edges, groups, and board inspection.", new Vector2(0f, -60f), "utility");
            PungentBoardNode optional = AddNode(document, PungentBoardNodeKind.FreeformCard, "Optional Bridges", "Docs, audit, data sheet, and generation links remain optional.", new Vector2(320f, -60f), "external");
            AddEdge(document, board, core, "requires", PungentBoardEdgeKind.DependsOn, true, "depends-on", "requires", "used-by");
            AddEdge(document, board, optional, "optional", PungentBoardEdgeKind.References, true, "provides", "provides", "in");
        }

        private static void BuildAuditFollowUp(PungentBoardDocument document)
        {
            document.summary = "Track audit findings, follow-up tasks, affected utilities, and validation closure.";
            PungentBoardNode finding = AddNode(document, PungentBoardNodeKind.AuditFinding, "Finding", "Paste or link the audit finding.", new Vector2(-320f, -120f));
            PungentBoardNode cause = AddNode(document, PungentBoardNodeKind.NoteCard, "Cause", "Likely cause and affected package boundary.", new Vector2(-20f, -120f));
            PungentBoardNode blocker = AddNode(document, PungentBoardNodeKind.FreeformCard, "Blocker", "Dependency or missing context preventing closure.", new Vector2(-20f, 90f));
            PungentBoardNode task = AddNode(document, PungentBoardNodeKind.Task, "Fix Task", "Concrete implementation step.", new Vector2(280f, -120f));
            PungentBoardNode verify = AddNode(document, PungentBoardNodeKind.Task, "Verify", "Local validation and Unity console checks.", new Vector2(280f, 90f));
            AddEdge(document, finding, cause, "explains", PungentBoardEdgeKind.LeadsTo);
            AddEdge(document, cause, task, "drives", PungentBoardEdgeKind.LeadsTo);
            AddEdge(document, blocker, task, "blocks", PungentBoardEdgeKind.Blocks);
            AddEdge(document, task, verify, "checks", PungentBoardEdgeKind.Sequence);
        }

        private static void BuildTokenDocumentationMap(PungentBoardDocument document)
        {
            document.summary = "Connect token definitions, documentation links, notes, and utility references.";
            PungentBoardNode token = AddNode(document, PungentBoardNodeKind.TokenReference, "Token", "Paste a token key or linked authoring reference.", new Vector2(-320f, -80f));
            PungentBoardNode docs = AddNode(document, PungentBoardNodeKind.DocumentationLink, "Documentation", "Optional documentation link reference.", new Vector2(0f, -80f));
            PungentBoardNode utility = AddNode(document, PungentBoardNodeKind.UtilityReference, "Utility", "Utility that consumes or exposes this token.", new Vector2(320f, -80f));
            AddEdge(document, token, docs, "documented by", PungentBoardEdgeKind.References);
            AddEdge(document, utility, token, "uses", PungentBoardEdgeKind.References);
        }

        private static void BuildProjectComponentMap(PungentBoardDocument document)
        {
            document.summary = "Map project-specific components, adapters, ownership, and validation notes without hardcoding game classes into BoardGraph.";
            PungentBoardNode profile = AddNode(document, PungentBoardNodeKind.FreeformCard, "Integration Profile", "Choose which providers, adapters, and mapping rules can feed this board.", new Vector2(-420f, -110f), "system");
            PungentBoardNode adapter = AddNode(document, PungentBoardNodeKind.UtilityReference, "Projection Adapter", "Editor-only adapter that previews project data as board nodes and edges.", new Vector2(-110f, -110f), "utility");
            PungentBoardNode component = AddNode(document, PungentBoardNodeKind.FreeformCard, "Custom Component", "A user/project component represented through adapter metadata, not a compile-time BoardGraph dependency.", new Vector2(220f, -110f), "external");
            PungentBoardNode validation = AddNode(document, PungentBoardNodeKind.Task, "Review / Apply", "Preview updates, apply selected changes, and keep stale source nodes visible.", new Vector2(-110f, 120f), "system");
            AddEdge(document, profile, adapter, "configures", PungentBoardEdgeKind.DependsOn, true, "depends-on", "requires", "used-by");
            AddEdge(document, adapter, component, "projects", PungentBoardEdgeKind.References, true, "provides", "provides", "in");
            AddEdge(document, adapter, validation, "previews", PungentBoardEdgeKind.DependsOn, true, "depends-on", "requires", "used-by");
            document.groups.Add(PungentBoardGroup.Create("Configurable Project Mapping", new Rect(-470f, -165f, 930f, 410f)));
        }

        private static void BuildBehaviourTree(PungentBoardDocument document)
        {
            document.summary = "Sketch AI behaviour tree flow with root, composite, condition, and action nodes. This is authoring schema only; no runtime execution is generated.";
            PungentBoardNode root = AddNode(document, PungentBoardNodeKind.FreeformCard, "Root", "Tree entry point.", new Vector2(-420f, -60f), "root");
            PungentBoardNode selector = AddNode(document, PungentBoardNodeKind.FreeformCard, "Selector", "Try child behaviours in priority order.", new Vector2(-120f, -60f), "selector");
            PungentBoardNode condition = AddNode(document, PungentBoardNodeKind.FreeformCard, "Condition", "Check whether the action can run.", new Vector2(180f, -160f), "condition");
            PungentBoardNode action = AddNode(document, PungentBoardNodeKind.Task, "Action", "Perform an authored behaviour step.", new Vector2(180f, 40f), "action");
            AddEdge(document, root, selector, "child", PungentBoardEdgeKind.ParentChild, true, "child", "child", "parent");
            AddEdge(document, selector, condition, "child", PungentBoardEdgeKind.ParentChild, true, "child", "child", "parent");
            AddEdge(document, selector, action, "child", PungentBoardEdgeKind.ParentChild, true, "child", "child", "parent");
        }

        private static void BuildAiStateMachine(PungentBoardDocument document)
        {
            document.summary = "Sketch AI states, decisions, transitions, and terminal flow without binding to runtime components yet.";
            PungentBoardNode entry = AddNode(document, PungentBoardNodeKind.FreeformCard, "Entry", "State machine entry.", new Vector2(-420f, -80f), "entry");
            PungentBoardNode patrol = AddNode(document, PungentBoardNodeKind.FreeformCard, "Patrol State", "Default idle or patrol behaviour.", new Vector2(-120f, -80f), "state");
            PungentBoardNode decision = AddNode(document, PungentBoardNodeKind.FreeformCard, "Decision", "Branch when a condition changes.", new Vector2(180f, -80f), "decision");
            PungentBoardNode exit = AddNode(document, PungentBoardNodeKind.FreeformCard, "Exit", "Terminal or handoff state.", new Vector2(480f, -80f), "exit");
            AddEdge(document, entry, patrol, "transition", PungentBoardEdgeKind.LeadsTo, true, "transition", "transition", "in");
            AddEdge(document, patrol, decision, "transition", PungentBoardEdgeKind.LeadsTo, true, "transition", "transition", "in");
            AddEdge(document, decision, exit, "transition", PungentBoardEdgeKind.LeadsTo, true, "transition", "transition", "in");
        }

        private static void BuildEventSequence(PungentBoardDocument document)
        {
            document.summary = "Sketch event sequencing, waits, camera moves, triggers, and branches. Runtime runners come in a later adapter/executor pass.";
            PungentBoardNode start = AddNode(document, PungentBoardNodeKind.FreeformCard, "Event", "Sequence entry event.", new Vector2(-420f, -90f), "event");
            PungentBoardNode delay = AddNode(document, PungentBoardNodeKind.FreeformCard, "Delay", "Wait before the next authored event.", new Vector2(-120f, -90f), "delay");
            PungentBoardNode camera = AddNode(document, PungentBoardNodeKind.FreeformCard, "Camera Move", "Planned camera movement or framing.", new Vector2(180f, -170f), "camera-move");
            PungentBoardNode trigger = AddNode(document, PungentBoardNodeKind.FreeformCard, "Trigger", "Trigger a custom event through a later adapter.", new Vector2(180f, 20f), "trigger");
            PungentBoardNode branch = AddNode(document, PungentBoardNodeKind.FreeformCard, "Branch", "Conditional sequence branch.", new Vector2(480f, -90f), "branch");
            AddEdge(document, start, delay, "sequence", PungentBoardEdgeKind.Sequence, true, "sequence", "next", "in");
            AddEdge(document, delay, camera, "sequence", PungentBoardEdgeKind.Sequence, true, "sequence", "next", "in");
            AddEdge(document, delay, trigger, "sequence", PungentBoardEdgeKind.Sequence, true, "sequence", "next", "in");
            AddEdge(document, camera, branch, "sequence", PungentBoardEdgeKind.Sequence, true, "sequence", "done", "in");
            AddEdge(document, trigger, branch, "sequence", PungentBoardEdgeKind.Sequence, true, "sequence", "done", "in");
        }

        private static void BuildSkillTree(PungentBoardDocument document)
        {
            document.summary = "Sketch skill unlocks, requirements, gates, and modifiers without creating runtime progression assets yet.";
            PungentBoardNode starter = AddNode(document, PungentBoardNodeKind.FreeformCard, "Starter Skill", "Unlocked by default or initial progression.", new Vector2(-420f, -90f), "skill");
            PungentBoardNode requirement = AddNode(document, PungentBoardNodeKind.FreeformCard, "Requirement", "Currency, level, quest, or achievement requirement.", new Vector2(-120f, -90f), "requirement");
            PungentBoardNode gate = AddNode(document, PungentBoardNodeKind.FreeformCard, "Gate", "Combines requirements before unlock.", new Vector2(180f, -90f), "gate");
            PungentBoardNode skill = AddNode(document, PungentBoardNodeKind.FreeformCard, "Unlocked Skill", "New ability or passive unlock.", new Vector2(480f, -160f), "skill");
            PungentBoardNode modifier = AddNode(document, PungentBoardNodeKind.FreeformCard, "Modifier", "Upgrade, stat modifier, or passive effect.", new Vector2(480f, 40f), "modifier");
            AddEdge(document, starter, requirement, "unlocks", PungentBoardEdgeKind.LeadsTo, true, "unlocks", "unlocks", "requires");
            AddEdge(document, requirement, gate, "unlocks", PungentBoardEdgeKind.LeadsTo, true, "unlocks", "unlocks", "requires");
            AddEdge(document, gate, skill, "unlocks", PungentBoardEdgeKind.LeadsTo, true, "unlocks", "unlocks", "requires");
            AddEdge(document, gate, modifier, "unlocks", PungentBoardEdgeKind.LeadsTo, true, "unlocks", "unlocks", "requires");
        }

        private static PungentBoardNode AddNode(PungentBoardDocument document, PungentBoardNodeKind kind, string title, string body, Vector2 position, string nodeTypeKey = "")
        {
            PungentBoardNode node = PungentBoardNode.Create(kind, position);
            node.title = title;
            node.body = body;
            node.nodeTypeKey = string.IsNullOrWhiteSpace(nodeTypeKey) ? DefaultNodeTypeKey(kind) : nodeTypeKey;
            document.nodes.Add(node);
            return node;
        }

        private static void AddEdge(PungentBoardDocument document, PungentBoardNode from, PungentBoardNode to, string label, PungentBoardEdgeKind kind = PungentBoardEdgeKind.LeadsTo, bool directed = true, string edgeTypeKey = "", string fromPortKey = "", string toPortKey = "")
        {
            PungentBoardEdge edge = PungentBoardEdge.Create(from != null ? from.id : string.Empty, to != null ? to.id : string.Empty);
            edge.label = label;
            edge.edgeKind = kind;
            edge.directed = directed;
            edge.edgeTypeKey = edgeTypeKey;
            edge.fromPortKey = fromPortKey;
            edge.toPortKey = toPortKey;
            document.edges.Add(edge);
        }

        private static string GraphTypeForTemplate(PungentBoardTemplateKind kind)
        {
            switch (kind)
            {
                case PungentBoardTemplateKind.DialogueQuestFlowBoard:
                    return PungentBoardBuiltInGraphTypes.DialogueQuestFlow;
                case PungentBoardTemplateKind.UtilityDependencyMap:
                case PungentBoardTemplateKind.ProjectComponentMap:
                    return PungentBoardBuiltInGraphTypes.DependencyMap;
                case PungentBoardTemplateKind.BehaviourTreeBoard:
                    return PungentBoardBuiltInGraphTypes.BehaviourTree;
                case PungentBoardTemplateKind.AiStateMachineBoard:
                    return PungentBoardBuiltInGraphTypes.AiStateMachine;
                case PungentBoardTemplateKind.EventSequenceBoard:
                    return PungentBoardBuiltInGraphTypes.EventSequence;
                case PungentBoardTemplateKind.SkillTreeBoard:
                    return PungentBoardBuiltInGraphTypes.SkillTree;
                default:
                    return PungentBoardBuiltInGraphTypes.FreeformWhiteboard;
            }
        }

        private static void ApplyGraphType(PungentBoardDocument document, string graphTypeId)
        {
            document.graphTypeId = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(graphTypeId);
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId);
            document.graphTypeDisplayName = graphType != null ? graphType.displayName : document.graphTypeId;
            document.graphSchemaVersion = graphType != null ? graphType.schemaVersion : 1;
        }

        private static string DefaultNodeTypeKey(PungentBoardNodeKind kind)
        {
            switch (kind)
            {
                case PungentBoardNodeKind.DocumentReference: return "document-reference";
                case PungentBoardNodeKind.UtilityReference: return "utility-reference";
                case PungentBoardNodeKind.TokenReference: return "token-reference";
                case PungentBoardNodeKind.DocumentationLink: return "documentation-link";
                case PungentBoardNodeKind.AuditFinding: return "audit-finding";
                case PungentBoardNodeKind.Task: return "task";
                case PungentBoardNodeKind.FreeformCard: return "freeform-card";
                default: return "note-card";
            }
        }
    }
#endif
}
