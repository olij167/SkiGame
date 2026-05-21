using System;
using System.Collections.Generic;
using System.Text;
using SkiGame.Progression;
using UnityEditor;
using UnityEngine;

namespace SkiGame.EditorTools
{
    [CustomEditor(typeof(NpcDialogueSequenceSO))]
    public sealed class NpcDialogueSequenceSOEditor : Editor
    {
        private readonly Dictionary<object, bool> _foldouts = new Dictionary<object, bool>();
        private bool _previewFoldout;
        private string _previewText;

        public override void OnInspectorGUI()
        {
            var sequence = (NpcDialogueSequenceSO)target;
            if (sequence == null)
                return;

            serializedObject.Update();
            Undo.RecordObject(sequence, "Edit NPC Dialogue Sequence");

            DrawValidation(sequence);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField(new GUIContent("Sequence", "Top-level dialogue sequence settings."), EditorStyles.boldLabel);
            sequence.sequenceId = EditorGUILayout.TextField(new GUIContent("Sequence Id", "Stable ID used by dialogue sequence lookups and debugging. Auto-generate if empty."), sequence.sequenceId);
            sequence.displayName = EditorGUILayout.TextField(new GUIContent("Display Name", "Author-facing label for this sequence."), sequence.displayName);
            sequence.startNodeId = DrawNodeIdPopup(new GUIContent("Start Node Id", "Node ID where playback begins. If empty or missing, playback falls back to the first node."), sequence.startNodeId, sequence, true);
            sequence.canSkip = EditorGUILayout.Toggle(new GUIContent("Can Skip", "Allows player input to skip/advance this sequence where supported."), sequence.canSkip);
            sequence.autoCloseOnEnd = EditorGUILayout.Toggle(new GUIContent("Auto Close On End", "Closes the dialogue UI automatically after an End node."), sequence.autoCloseOnEnd);
            sequence.defaultImportance = (NpcDialogueImportance)EditorGUILayout.EnumPopup(new GUIContent("Default Importance", "Default importance applied by sequence playback when nodes do not override it."), sequence.defaultImportance);
            sequence.styleOverride = (NpcDialogueBubbleStyleSO)EditorGUILayout.ObjectField(new GUIContent("Style Override", "Optional dialogue bubble style override for this sequence."), sequence.styleOverride, typeof(NpcDialogueBubbleStyleSO), false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Auto-generate Missing Sequence ID", "Fills sequenceId from the asset name if it is empty.")))
                GenerateSequenceId(sequence);
            if (GUILayout.Button(new GUIContent("Auto-generate Missing Node IDs", "Fills empty node IDs with stable line/choice/action/end IDs.")))
                GenerateMissingNodeIds(sequence);
            if (GUILayout.Button(new GUIContent("Normalize Node IDs", "Renames nodes to normalized IDs and updates links.")))
                NormalizeNodeIds(sequence);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            DrawNodeToolbar(sequence);
            DrawNodes(sequence);

            _previewFoldout = EditorGUILayout.Foldout(_previewFoldout, new GUIContent("Preview Dialogue As Text", "Text dump of the sequence nodes for quick review."), true);
            if (_previewFoldout)
            {
                if (GUILayout.Button(new GUIContent("Refresh Preview", "Rebuild the preview text dump.")) || string.IsNullOrEmpty(_previewText))
                    _previewText = BuildPreview(sequence);
                EditorGUILayout.TextArea(_previewText, GUILayout.MinHeight(120f));
            }

            if (GUI.changed)
                EditorUtility.SetDirty(sequence);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNodeToolbar(NpcDialogueSequenceSO sequence)
        {
            EditorGUILayout.LabelField(new GUIContent("Nodes", "Ordered dialogue nodes. Use dropdowns for node links to avoid raw ID typos."), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Add Line", "Add a simple line node.")))
                AddNode(sequence, NpcDialogueSequenceNodeType.Line);
            if (GUILayout.Button(new GUIContent("Add Choice", "Add a choice node with two blank choices.")))
                AddNode(sequence, NpcDialogueSequenceNodeType.Choice);
            if (GUILayout.Button(new GUIContent("Add Quest Accept", "Add an action node that requests quest acceptance.")))
                AddQuestAcceptNode(sequence);
            if (GUILayout.Button(new GUIContent("Add End", "Add a sequence end node.")))
                AddNode(sequence, NpcDialogueSequenceNodeType.End);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNodes(NpcDialogueSequenceSO sequence)
        {
            sequence.nodes ??= Array.Empty<NpcDialogueSequenceNode>();
            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                var node = sequence.nodes[i] ?? new NpcDialogueSequenceNode();
                sequence.nodes[i] = node;

                bool expanded = GetFoldout(node, true);
                expanded = EditorGUILayout.Foldout(expanded, new GUIContent(GetNodeLabel(node, i), "Stable foldout state is keyed by this node object."), true);
                SetFoldout(node, expanded);
                if (!expanded)
                    continue;

                EditorGUILayout.BeginVertical("box");
                node.nodeId = EditorGUILayout.TextField(new GUIContent("Node Id", "Stable node key used by next/target/choice links. Keep unique within this sequence."), node.nodeId);
                node.nodeType = (NpcDialogueSequenceNodeType)EditorGUILayout.EnumPopup(new GUIContent("Node Type", "Controls which node fields are meaningful during playback."), node.nodeType);
                node.speakerIdentityId = EditorGUILayout.TextField(new GUIContent("Speaker Identity Id", "Optional speaker identity override for this node."), node.speakerIdentityId);
                node.speakerRole = EditorGUILayout.TextField(new GUIContent("Speaker Role", "Optional speaker role token for formatting or presentation."), node.speakerRole);
                node.importance = (NpcDialogueImportance)EditorGUILayout.EnumPopup(new GUIContent("Importance", "Importance override for this node."), node.importance);
                node.styleOverride = (NpcDialogueBubbleStyleSO)EditorGUILayout.ObjectField(new GUIContent("Style Override", "Optional style override for this node."), node.styleOverride, typeof(NpcDialogueBubbleStyleSO), false);
                DrawNodeBody(sequence, node);
                DrawNodeFooter(sequence, i);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3f);
            }
        }

        private void DrawNodeBody(NpcDialogueSequenceSO sequence, NpcDialogueSequenceNode node)
        {
            switch (node.nodeType)
            {
                case NpcDialogueSequenceNodeType.Line:
                    EditorGUILayout.LabelField(new GUIContent("Text", "Line spoken by this node. Supports dialogue tokens used by runtime formatting."));
                    node.text = EditorGUILayout.TextArea(node.text ?? string.Empty, GUILayout.MinHeight(42f));
                    node.duration = EditorGUILayout.FloatField(new GUIContent("Duration", "Suggested display duration in seconds."), Mathf.Max(0.1f, node.duration));
                    node.waitForPlayerContinue = EditorGUILayout.Toggle(new GUIContent("Wait For Player Continue", "Requires player continue input before advancing."), node.waitForPlayerContinue);
                    node.nextNodeId = DrawNodeIdPopup(new GUIContent("Next Node", "Node to play after this line."), node.nextNodeId, sequence, true);
                    break;
                case NpcDialogueSequenceNodeType.Choice:
                    EditorGUILayout.LabelField(new GUIContent("Prompt Text", "Prompt shown above this node's choices."));
                    node.promptText = EditorGUILayout.TextArea(node.promptText ?? string.Empty, GUILayout.MinHeight(32f));
                    DrawChoices(sequence, node);
                    break;
                case NpcDialogueSequenceNodeType.Action:
                    DrawActions(node);
                    node.nextNodeId = DrawNodeIdPopup(new GUIContent("Next Node", "Node to play after actions run."), node.nextNodeId, sequence, true);
                    break;
                case NpcDialogueSequenceNodeType.Jump:
                    node.targetNodeId = DrawNodeIdPopup(new GUIContent("Target Node", "Node this jump redirects to."), node.targetNodeId, sequence, false);
                    break;
                case NpcDialogueSequenceNodeType.Wait:
                    node.waitSeconds = EditorGUILayout.FloatField(new GUIContent("Wait Seconds", "Seconds to wait before advancing."), Mathf.Max(0f, node.waitSeconds));
                    node.nextNodeId = DrawNodeIdPopup(new GUIContent("Next Node", "Node to play after waiting."), node.nextNodeId, sequence, true);
                    break;
                case NpcDialogueSequenceNodeType.End:
                    EditorGUILayout.HelpBox("End nodes stop sequence playback.", MessageType.Info);
                    break;
            }

            DrawRequirements(node);
        }

        private void DrawChoices(NpcDialogueSequenceSO sequence, NpcDialogueSequenceNode node)
        {
            node.choices ??= Array.Empty<NpcDialogueSequenceChoice>();
            EditorGUILayout.LabelField(new GUIContent("Choices", "Player choices and their target nodes."), EditorStyles.boldLabel);
            for (int i = 0; i < node.choices.Length; i++)
            {
                var choice = node.choices[i] ?? new NpcDialogueSequenceChoice();
                node.choices[i] = choice;
                EditorGUILayout.BeginVertical("box");
                choice.label = EditorGUILayout.TextField(new GUIContent("Text", "Choice text shown to the player."), choice.label);
                choice.nextNodeId = DrawNodeIdPopup(new GUIContent("Target Node", "Node played when this choice is selected."), choice.nextNodeId, sequence, false);
                DrawRequirements(ref choice.requirements, "Choice Requirements", "Requirements that must be met before this choice is shown.");
                DrawActions(ref choice.actions, "Choice Actions", "Actions fired when this choice is selected.");
                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button(new GUIContent("Remove Choice", "Remove this choice.")))
                {
                    var list = new List<NpcDialogueSequenceChoice>(node.choices);
                    list.RemoveAt(i);
                    node.choices = list.ToArray();
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(new GUIContent("Add Choice", "Add another blank choice.")))
            {
                var list = new List<NpcDialogueSequenceChoice>(node.choices) { new NpcDialogueSequenceChoice() };
                node.choices = list.ToArray();
            }
        }

        private void DrawActions(NpcDialogueSequenceNode node)
        {
            DrawActions(ref node.actions, "Actions", "Runtime actions requested by this node.");
        }

        private void DrawActions(ref NpcDialogueSequenceAction[] actions, string label, string tooltip)
        {
            actions ??= Array.Empty<NpcDialogueSequenceAction>();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel);
            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i] ?? new NpcDialogueSequenceAction();
                actions[i] = action;
                EditorGUILayout.BeginVertical("box");
                action.actionType = (NpcDialogueSequenceActionType)EditorGUILayout.EnumPopup(new GUIContent("Action Type", "Runtime action requested by the sequence player."), action.actionType);
                action.questDefinition = (QuestDefinitionSO)EditorGUILayout.ObjectField(new GUIContent("Quest Definition", "Quest asset used by quest-related actions."), action.questDefinition, typeof(QuestDefinitionSO), false);
                action.questId = EditorGUILayout.TextField(new GUIContent("Quest Id", "Fallback quest ID when no quest asset is assigned."), action.questId);
                action.boolValue = EditorGUILayout.Toggle(new GUIContent("Bool Value", "Boolean payload used by toggle-style actions."), action.boolValue);
                action.signalKey = EditorGUILayout.TextField(new GUIContent("Signal Key", "Quest signal key raised by RaiseQuestSignal."), action.signalKey);
                action.signalText = EditorGUILayout.TextField(new GUIContent("Signal Text", "Optional string payload for quest signals."), action.signalText);
                action.signalNumericValue = EditorGUILayout.FloatField(new GUIContent("Signal Numeric Value", "Optional numeric payload for quest signals."), action.signalNumericValue);
                action.includeSignalNumericValue = EditorGUILayout.Toggle(new GUIContent("Include Numeric Value", "Include the numeric signal payload when raising the signal."), action.includeSignalNumericValue);
                action.requiresExplicitChoiceOrConfirm = EditorGUILayout.Toggle(new GUIContent("Requires Explicit Choice/Confirm", "Prevents sensitive actions from firing without explicit player choice/confirmation."), action.requiresExplicitChoiceOrConfirm);
                action.dialogueSequence = (NpcDialogueSequenceSO)EditorGUILayout.ObjectField(new GUIContent("Dialogue Sequence", "Sequence to start for StartDialogueSequence actions."), action.dialogueSequence, typeof(NpcDialogueSequenceSO), false);
                action.panelId = EditorGUILayout.TextField(new GUIContent("Panel Id", "Panel ID used by OpenPanel actions."), action.panelId);
                action.timelineId = EditorGUILayout.TextField(new GUIContent("Timeline Id", "Timeline placeholder ID used by timeline actions."), action.timelineId);
                action.waitForTimelineComplete = EditorGUILayout.Toggle(new GUIContent("Wait For Timeline", "Wait for timeline completion before continuing when supported."), action.waitForTimelineComplete);
                action.markerId = EditorGUILayout.TextField(new GUIContent("Marker Id", "Marker ID used by NPC movement actions."), action.markerId);
                action.animationTrigger = EditorGUILayout.TextField(new GUIContent("Animation Trigger", "Animator trigger used by animation actions."), action.animationTrigger);
                action.customEventId = EditorGUILayout.TextField(new GUIContent("Custom Event Id", "Custom event key emitted by CustomEvent actions."), action.customEventId);
                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button(new GUIContent("Remove Action", "Remove this action.")))
                {
                    var list = new List<NpcDialogueSequenceAction>(actions);
                    list.RemoveAt(i);
                    actions = list.ToArray();
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(new GUIContent("Add Action", "Add another action to this node.")))
            {
                var list = new List<NpcDialogueSequenceAction>(actions) { new NpcDialogueSequenceAction() };
                actions = list.ToArray();
            }
        }

        private void DrawRequirements(NpcDialogueSequenceNode node)
        {
            DrawRequirements(ref node.requirements, "Requirements", "Requirements that must be met before this node can play.");
        }

        private void DrawRequirements(ref DialogueRequirement[] requirements, string label, string tooltip)
        {
            requirements ??= Array.Empty<DialogueRequirement>();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel);
            for (int i = 0; i < requirements.Length; i++)
            {
                var requirement = requirements[i] ?? new DialogueRequirement();
                requirements[i] = requirement;
                EditorGUILayout.BeginVertical("box");
                requirement.requirementType = (DialogueRequirementType)EditorGUILayout.EnumPopup(new GUIContent("Requirement Type", "Runtime requirement checked before this dialogue element is available."), requirement.requirementType);
                requirement.key = EditorGUILayout.TextField(new GUIContent("Key", "Quest ID, context key, or requirement-specific lookup key."), requirement.key);
                requirement.comparison = (DialogueRequirementComparison)EditorGUILayout.EnumPopup(new GUIContent("Comparison", "How the runtime value is compared to the expected value."), requirement.comparison);
                requirement.value = EditorGUILayout.TextField(new GUIContent("Value", "Expected value. QuestStage accepts a stage index or stage ID."), requirement.value);
                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button(new GUIContent("Remove Requirement", "Remove this requirement.")))
                {
                    var list = new List<DialogueRequirement>(requirements);
                    list.RemoveAt(i);
                    requirements = list.ToArray();
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(new GUIContent("Add Requirement", "Add another requirement.")))
            {
                var list = new List<DialogueRequirement>(requirements) { new DialogueRequirement() };
                requirements = list.ToArray();
            }
        }

        private void DrawNodeFooter(NpcDialogueSequenceSO sequence, int index)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Move Up", "Move this node earlier in the array.")) && index > 0)
            {
                Swap(sequence.nodes, index, index - 1);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(new GUIContent("Move Down", "Move this node later in the array.")) && index < sequence.nodes.Length - 1)
            {
                Swap(sequence.nodes, index, index + 1);
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
            if (GUILayout.Button(new GUIContent("Remove Node", "Remove this node. Links pointing to it are not automatically deleted.")))
            {
                RemoveNode(sequence, index);
                GUI.backgroundColor = Color.white;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private string DrawNodeIdPopup(GUIContent label, string current, NpcDialogueSequenceSO sequence, bool allowEmpty)
        {
            var ids = GetNodeIds(sequence, allowEmpty);
            int index = Mathf.Max(0, ids.IndexOf(current ?? string.Empty));
            if (index == 0 && !string.IsNullOrWhiteSpace(current) && !ids.Contains(current))
                ids.Add(current);

            index = EditorGUILayout.Popup(label, index, ids.ToArray());
            return ids[Mathf.Clamp(index, 0, ids.Count - 1)];
        }

        private static List<string> GetNodeIds(NpcDialogueSequenceSO sequence, bool allowEmpty)
        {
            var ids = new List<string>();
            if (allowEmpty)
                ids.Add(string.Empty);

            if (sequence?.nodes == null)
                return ids;

            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                string id = sequence.nodes[i]?.nodeId;
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                    ids.Add(id);
            }

            return ids;
        }

        private void DrawValidation(NpcDialogueSequenceSO sequence)
        {
            var messages = CollectValidation(sequence);
            EditorGUILayout.LabelField(new GUIContent("Validation", "Non-destructive checks for node IDs, links, choices, and quest actions."), EditorStyles.boldLabel);
            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("No sequence authoring issues detected.", MessageType.Info);
                return;
            }

            for (int i = 0; i < messages.Count; i++)
                EditorGUILayout.HelpBox(messages[i].Item2, messages[i].Item1);
        }

        private static List<Tuple<MessageType, string>> CollectValidation(NpcDialogueSequenceSO sequence)
        {
            var messages = new List<Tuple<MessageType, string>>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(sequence.sequenceId))
                messages.Add(Tuple.Create(MessageType.Warning, "Sequence ID is empty."));
            if (sequence.nodes == null || sequence.nodes.Length == 0)
            {
                messages.Add(Tuple.Create(MessageType.Error, "Sequence has no nodes."));
                return messages;
            }

            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                var node = sequence.nodes[i];
                if (node == null)
                    continue;

                if (string.IsNullOrWhiteSpace(node.nodeId))
                    messages.Add(Tuple.Create(MessageType.Error, $"Node {i + 1} has an empty ID."));
                else if (!ids.Add(node.nodeId.Trim()))
                    messages.Add(Tuple.Create(MessageType.Error, $"Duplicate node ID '{node.nodeId}'."));
            }

            if (!string.IsNullOrWhiteSpace(sequence.startNodeId) && !ids.Contains(sequence.startNodeId.Trim()))
                messages.Add(Tuple.Create(MessageType.Error, $"Start node '{sequence.startNodeId}' does not exist."));
            if (string.IsNullOrWhiteSpace(sequence.startNodeId) && sequence.GetStartNode() == null)
                messages.Add(Tuple.Create(MessageType.Error, "Missing start node."));

            for (int i = 0; i < sequence.nodes.Length; i++)
                ValidateNode(sequence.nodes[i], ids, messages);

            return messages;
        }

        private static void ValidateNode(NpcDialogueSequenceNode node, HashSet<string> ids, List<Tuple<MessageType, string>> messages)
        {
            if (node == null)
                return;

            if (node.nodeType == NpcDialogueSequenceNodeType.Line && string.IsNullOrWhiteSpace(node.text))
                messages.Add(Tuple.Create(MessageType.Warning, $"Line node '{node.nodeId}' has empty text."));

            WarnMissingLink(node.nextNodeId, "nextNodeId", node.nodeId, ids, messages);
            WarnMissingLink(node.targetNodeId, "targetNodeId", node.nodeId, ids, messages);

            if (node.choices != null)
            {
                for (int i = 0; i < node.choices.Length; i++)
                {
                    var choice = node.choices[i];
                    if (choice == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(choice.label))
                        messages.Add(Tuple.Create(MessageType.Warning, $"Choice {i + 1} on node '{node.nodeId}' has empty text."));
                    if (string.IsNullOrWhiteSpace(choice.nextNodeId))
                        messages.Add(Tuple.Create(MessageType.Warning, $"Choice '{choice.label}' on node '{node.nodeId}' has no target."));
                    WarnMissingLink(choice.nextNodeId, $"choice '{choice.label}'", node.nodeId, ids, messages);
                }
            }

            if (node.actions != null)
            {
                for (int i = 0; i < node.actions.Length; i++)
                    ValidateAction(node.actions[i], node.nodeId, messages);
            }
        }

        private static void ValidateAction(NpcDialogueSequenceAction action, string nodeId, List<Tuple<MessageType, string>> messages)
        {
            if (action == null)
                return;

            bool needsQuest = action.actionType == NpcDialogueSequenceActionType.AcceptQuest ||
                              action.actionType == NpcDialogueSequenceActionType.TrackQuest ||
                              action.actionType == NpcDialogueSequenceActionType.CompleteQuest ||
                              action.actionType == NpcDialogueSequenceActionType.SetQuestTracked;
            if (needsQuest && action.questDefinition == null && string.IsNullOrWhiteSpace(action.questId))
                messages.Add(Tuple.Create(MessageType.Warning, $"Action node '{nodeId}' has a quest action with no quest reference or quest ID."));
            if (action.actionType == NpcDialogueSequenceActionType.StartDialogueSequence && action.dialogueSequence == null)
                messages.Add(Tuple.Create(MessageType.Warning, $"Action node '{nodeId}' starts a dialogue sequence but none is assigned."));
            if (action.actionType == NpcDialogueSequenceActionType.RaiseQuestSignal && string.IsNullOrWhiteSpace(action.signalKey))
                messages.Add(Tuple.Create(MessageType.Warning, $"Action node '{nodeId}' raises a quest signal but Signal Key is empty."));
        }

        private static void WarnMissingLink(string target, string field, string owner, HashSet<string> ids, List<Tuple<MessageType, string>> messages)
        {
            if (!string.IsNullOrWhiteSpace(target) && !ids.Contains(target.Trim()))
                messages.Add(Tuple.Create(MessageType.Error, $"Node '{owner}' has {field} pointing to missing node '{target}'."));
        }

        private static void GenerateSequenceId(NpcDialogueSequenceSO sequence)
        {
            if (!string.IsNullOrWhiteSpace(sequence.sequenceId))
                return;

            sequence.sequenceId = QuestDefinitionSOEditor.SanitizeAssetToken(sequence.name);
            EditorUtility.SetDirty(sequence);
        }

        private static void GenerateMissingNodeIds(NpcDialogueSequenceSO sequence)
        {
            if (sequence.nodes == null)
                return;

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(sequence.nodes[i]?.nodeId))
                    used.Add(sequence.nodes[i].nodeId.Trim());
            }

            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                var node = sequence.nodes[i];
                if (node == null || !string.IsNullOrWhiteSpace(node.nodeId))
                    continue;

                node.nodeId = UniqueNodeId(GetPrefix(node.nodeType), used);
                used.Add(node.nodeId);
            }

            if (string.IsNullOrWhiteSpace(sequence.startNodeId) && sequence.nodes.Length > 0)
                sequence.startNodeId = sequence.nodes[0]?.nodeId;
            EditorUtility.SetDirty(sequence);
        }

        private static void NormalizeNodeIds(NpcDialogueSequenceSO sequence)
        {
            if (sequence.nodes == null)
                return;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                var node = sequence.nodes[i];
                if (node == null)
                    continue;

                string oldId = node.nodeId;
                string newId = UniqueNodeId(GetPrefix(node.nodeType), used);
                node.nodeId = newId;
                used.Add(newId);
                if (!string.IsNullOrWhiteSpace(oldId))
                    map[oldId.Trim()] = newId;
            }

            if (!string.IsNullOrWhiteSpace(sequence.startNodeId) && map.TryGetValue(sequence.startNodeId.Trim(), out string newStart))
                sequence.startNodeId = newStart;
            UpdateLinks(sequence, map);
            EditorUtility.SetDirty(sequence);
        }

        private static void UpdateLinks(NpcDialogueSequenceSO sequence, Dictionary<string, string> map)
        {
            if (sequence.nodes == null)
                return;

            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                var node = sequence.nodes[i];
                if (node == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(node.nextNodeId) && map.TryGetValue(node.nextNodeId.Trim(), out string next))
                    node.nextNodeId = next;
                if (!string.IsNullOrWhiteSpace(node.targetNodeId) && map.TryGetValue(node.targetNodeId.Trim(), out string target))
                    node.targetNodeId = target;
                if (node.choices == null)
                    continue;
                for (int c = 0; c < node.choices.Length; c++)
                {
                    var choice = node.choices[c];
                    if (!string.IsNullOrWhiteSpace(choice?.nextNodeId) && map.TryGetValue(choice.nextNodeId.Trim(), out string choiceTarget))
                        choice.nextNodeId = choiceTarget;
                }
            }
        }

        private static string UniqueNodeId(string prefix, HashSet<string> used)
        {
            for (int i = 1; i < 10000; i++)
            {
                string candidate = $"{prefix}_{i:00}";
                if (!used.Contains(candidate))
                    return candidate;
            }

            return Guid.NewGuid().ToString("N");
        }

        private static string GetPrefix(NpcDialogueSequenceNodeType type)
        {
            return type switch
            {
                NpcDialogueSequenceNodeType.Choice => "choice",
                NpcDialogueSequenceNodeType.Action => "action",
                NpcDialogueSequenceNodeType.Jump => "jump",
                NpcDialogueSequenceNodeType.Wait => "wait",
                NpcDialogueSequenceNodeType.End => "end",
                _ => "line"
            };
        }

        private static void AddNode(NpcDialogueSequenceSO sequence, NpcDialogueSequenceNodeType type)
        {
            var list = new List<NpcDialogueSequenceNode>(sequence.nodes ?? Array.Empty<NpcDialogueSequenceNode>());
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(list[i]?.nodeId))
                    used.Add(list[i].nodeId.Trim());
            }

            var node = new NpcDialogueSequenceNode
            {
                nodeId = UniqueNodeId(GetPrefix(type), used),
                nodeType = type,
                text = type == NpcDialogueSequenceNodeType.Line ? string.Empty : null,
                choices = type == NpcDialogueSequenceNodeType.Choice
                    ? new[] { new NpcDialogueSequenceChoice(), new NpcDialogueSequenceChoice() }
                    : null
            };
            list.Add(node);
            sequence.nodes = list.ToArray();
            if (string.IsNullOrWhiteSpace(sequence.startNodeId))
                sequence.startNodeId = node.nodeId;
            EditorUtility.SetDirty(sequence);
        }

        private static void AddQuestAcceptNode(NpcDialogueSequenceSO sequence)
        {
            AddNode(sequence, NpcDialogueSequenceNodeType.Action);
            var node = sequence.nodes[sequence.nodes.Length - 1];
            node.actions = new[]
            {
                new NpcDialogueSequenceAction
                {
                    actionType = NpcDialogueSequenceActionType.AcceptQuest,
                    requiresExplicitChoiceOrConfirm = true
                }
            };
        }

        private static void RemoveNode(NpcDialogueSequenceSO sequence, int index)
        {
            var list = new List<NpcDialogueSequenceNode>(sequence.nodes ?? Array.Empty<NpcDialogueSequenceNode>());
            list.RemoveAt(index);
            sequence.nodes = list.ToArray();
        }

        private static void Swap<T>(T[] array, int a, int b)
        {
            T temp = array[a];
            array[a] = array[b];
            array[b] = temp;
        }

        private bool GetFoldout(object key, bool defaultExpanded)
        {
            if (key == null)
                return defaultExpanded;
            if (!_foldouts.TryGetValue(key, out bool value))
            {
                value = defaultExpanded;
                _foldouts[key] = value;
            }
            return value;
        }

        private void SetFoldout(object key, bool value)
        {
            if (key != null)
                _foldouts[key] = value;
        }

        private static string GetNodeLabel(NpcDialogueSequenceNode node, int index)
        {
            if (node == null)
                return $"Node {index + 1}";

            string id = string.IsNullOrWhiteSpace(node.nodeId) ? $"Node {index + 1}" : node.nodeId.Trim();
            return $"{id}  [{node.nodeType}]";
        }

        private static string BuildPreview(NpcDialogueSequenceSO sequence)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{sequence.displayName} ({sequence.SafeId})");
            if (sequence.nodes == null)
                return builder.ToString();

            for (int i = 0; i < sequence.nodes.Length; i++)
            {
                var node = sequence.nodes[i];
                if (node == null)
                    continue;

                builder.Append(node.nodeId).Append(" [").Append(node.nodeType).Append("] ");
                builder.Append(node.nodeType == NpcDialogueSequenceNodeType.Choice ? node.promptText : node.text);
                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
