using System;
using System.Collections.Generic;
using System.Text;
using SkiGame.Progression;
using UnityEngine;

public enum NpcDialogueSequenceNodeType
{
    Line,
    Choice,
    Action,
    Jump,
    Wait,
    End
}

public enum NpcDialogueSequenceActionType
{
    None,
    AcceptQuest,
    TrackQuest,
    CompleteQuest,
    RaiseQuestSignal,
    SetQuestTracked,
    StartDialogueSequence,
    OpenPanel,
    PlayTimelinePlaceholder,
    MoveNpcToMarker,
    TriggerNpcAnimation,
    PerformNpcAction,
    CustomEvent
}

[Serializable]
public sealed class NpcDialogueSequenceChoice
{
    public string label;
    public string nextNodeId;
    public DialogueRequirement[] requirements;
    public NpcDialogueSequenceAction[] actions;
}

[Serializable]
public sealed class NpcDialogueSequenceAction
{
    public NpcDialogueSequenceActionType actionType;
    public QuestDefinitionSO questDefinition;
    public string questId;
    public bool boolValue = true;
    public string signalKey;
    public string signalText;
    public float signalNumericValue;
    public bool includeSignalNumericValue;
    public bool requiresExplicitChoiceOrConfirm = true;
    public NpcDialogueSequenceSO dialogueSequence;
    public string panelId;
    public string timelineId;
    public bool waitForTimelineComplete;
    public string markerId;
    public string animationTrigger;
    public string customEventId;
}

[Serializable]
public sealed class NpcDialogueSequenceNode
{
    public string nodeId;
    public NpcDialogueSequenceNodeType nodeType = NpcDialogueSequenceNodeType.Line;
    public string speakerIdentityId;
    public string speakerRole;
    [TextArea(2, 5)] public string text;
    [Min(0.1f)] public float duration = 3f;
    public bool waitForPlayerContinue;
    public NpcDialogueImportance importance;
    public NpcDialogueBubbleStyleSO styleOverride;
    public DialogueRequirement[] requirements;
    [TextArea(1, 3)] public string promptText;
    public NpcDialogueSequenceChoice[] choices;
    public NpcDialogueSequenceAction[] actions;
    public string nextNodeId;
    public string targetNodeId;
    [Min(0f)] public float waitSeconds = 0.5f;
}

[CreateAssetMenu(fileName = "NpcDialogueSequence", menuName = "SkiGame/NPC/Dialogue Sequence")]
public sealed class NpcDialogueSequenceSO : ScriptableObject
{
    public string sequenceId;
    public string displayName;
    public NpcDialogueSequenceNode[] nodes;
    public string startNodeId;
    public bool canSkip = true;
    public bool autoCloseOnEnd = true;
    public NpcDialogueImportance defaultImportance = NpcDialogueImportance.Quest;
    public NpcDialogueBubbleStyleSO styleOverride;

    public string SafeId => string.IsNullOrWhiteSpace(sequenceId) ? name : sequenceId.Trim();

    public bool TryGetNode(string nodeId, out NpcDialogueSequenceNode node)
    {
        node = null;
        if (nodes == null || string.IsNullOrWhiteSpace(nodeId))
            return false;

        for (int i = 0; i < nodes.Length; i++)
        {
            var candidate = nodes[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.nodeId))
                continue;

            if (string.Equals(candidate.nodeId.Trim(), nodeId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                node = candidate;
                return true;
            }
        }

        return false;
    }

    public NpcDialogueSequenceNode GetStartNode()
    {
        if (!string.IsNullOrWhiteSpace(startNodeId) && TryGetNode(startNodeId, out var explicitStart))
            return explicitStart;

        return nodes != null && nodes.Length > 0 ? nodes[0] : null;
    }

    [ContextMenu("Validate Sequence")]
    public void ValidateSequence()
    {
        foreach (string warning in CollectValidationWarnings())
            Debug.LogWarning($"[{name}] {warning}", this);
    }

    [ContextMenu("Preview Text Dump")]
    public void PreviewTextDump()
    {
        Debug.Log(BuildPreviewTextDump(), this);
    }

    public List<string> CollectValidationWarnings()
    {
        var warnings = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (nodes == null || nodes.Length == 0)
        {
            warnings.Add("Sequence has no nodes.");
            return warnings;
        }

        if (GetStartNode() == null)
            warnings.Add("Missing start node.");

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null)
                continue;

            if (string.IsNullOrWhiteSpace(node.nodeId))
                warnings.Add($"Node at index {i} has no node id.");
            else if (!ids.Add(node.nodeId.Trim()))
                warnings.Add($"Duplicate node id '{node.nodeId}'.");

            if (node.nodeType == NpcDialogueSequenceNodeType.Line && string.IsNullOrWhiteSpace(node.text))
                warnings.Add($"Line node '{node.nodeId}' has no text.");
        }

        for (int i = 0; i < nodes.Length; i++)
            ValidateNodeLinks(nodes[i], ids, warnings);

        return warnings;
    }

    private static void ValidateNodeLinks(NpcDialogueSequenceNode node, HashSet<string> ids, List<string> warnings)
    {
        if (node == null)
            return;

        WarnIfMissing(node.nextNodeId, "nextNodeId", node.nodeId, ids, warnings);
        WarnIfMissing(node.targetNodeId, "targetNodeId", node.nodeId, ids, warnings);

        if (node.choices == null)
            return;

        for (int i = 0; i < node.choices.Length; i++)
        {
            var choice = node.choices[i];
            if (choice == null)
                continue;

            WarnIfMissing(choice.nextNodeId, $"choice '{choice.label}'", node.nodeId, ids, warnings);
        }
    }

    private static void WarnIfMissing(string nodeId, string field, string ownerNodeId, HashSet<string> ids, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        if (!ids.Contains(nodeId.Trim()))
            warnings.Add($"Node '{ownerNodeId}' has {field} pointing to missing node '{nodeId}'.");
    }

    private string BuildPreviewTextDump()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{displayName} ({SafeId})");
        if (nodes == null)
            return builder.ToString();

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null)
                continue;

            builder.Append(node.nodeId).Append(" [").Append(node.nodeType).Append("] ");
            if (node.nodeType == NpcDialogueSequenceNodeType.Choice)
                builder.Append(node.promptText);
            else
                builder.Append(node.text);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
