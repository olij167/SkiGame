using System;
using System.Collections.Generic;
using System.IO;
using SkiGame.Progression;
using UnityEditor;
using UnityEngine;

namespace SkiGame.EditorTools
{
    [CustomEditor(typeof(QuestDialogueProfileSO))]
    public sealed class QuestDialogueProfileSOEditor : Editor
    {
        private bool _eventsFoldout = true;
        private bool _stagesFoldout = true;
        private bool _objectivesFoldout;
        private bool _ambientFoldout;

        public override void OnInspectorGUI()
        {
            var profile = (QuestDialogueProfileSO)target;
            if (profile == null)
                return;

            serializedObject.Update();
            Undo.RecordObject(profile, "Edit Quest Dialogue Profile");

            DrawValidation(profile);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField(new GUIContent("Quest", "Quest asset and speaker defaults used when this profile is played."), EditorStyles.boldLabel);
            profile.questDefinition = (QuestDefinitionSO)EditorGUILayout.ObjectField(new GUIContent("Quest Definition", "Quest this dialogue profile belongs to. QuestDialogueBridge matches profiles by this reference."), profile.questDefinition, typeof(QuestDefinitionSO), false);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(new GUIContent("Quest Id", "Resolved stable quest ID from the linked quest."), profile.questDefinition != null ? profile.questDefinition.SafeId : string.Empty);
            profile.defaultSpeakerIdentityId = EditorGUILayout.TextField(new GUIContent("Default Speaker Identity Id", "Fallback speaker identity used by authored dialogue systems when no runtime NPC supplies one."), profile.defaultSpeakerIdentityId);

            EditorGUILayout.HelpBox("Runtime check: QuestDialogueBridge currently consumes onAccepted, onReadyToTurnIn, onCompleted, stage onStageStarted, and stageReminderTopic. NPC quest offers consume their own direct sequence fields for offer/track/turn-in interactions.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(profile.questDefinition == null))
            {
                if (GUILayout.Button(new GUIContent("Sync From Quest", "Creates missing stage bindings for each linked quest stage.")))
                    QuestDefinitionSOEditor.SyncStageBindings(profile, profile.questDefinition);

                if (GUILayout.Button(new GUIContent("Sync Objective Bindings From Quest", "Creates missing objective binding rows. Runtime objective dialogue events are not currently wired.")))
                    SyncObjectiveBindings(profile);
            }
            EditorGUILayout.EndHorizontal();

            _eventsFoldout = EditorGUILayout.Foldout(_eventsFoldout, new GUIContent("Quest Event Sequences", "Major quest lifecycle sequence slots."), true);
            if (_eventsFoldout)
            {
                EditorGUILayout.BeginVertical("box");
                DrawSequenceField(profile, "onAvailableSequence", "Available", "Authoring slot for quest availability dialogue. Not consumed by QuestDialogueBridge today.");
                DrawSequenceField(profile, "onOfferedSequence", "Offered", "Authoring slot for offer dialogue. NpcQuestOfferSO direct offer sequence is currently authoritative for offers.");
                DrawSequenceField(profile, "onAcceptedSequence", "Accepted", "Played by QuestDialogueBridge when the quest is accepted.");
                profile.reminderTopic = EditorGUILayout.TextField(new GUIContent("Reminder Topic", "General reminder topic. Stage reminder topics are consumed by QuestDialogueBridge when no stage-start sequence plays."), profile.reminderTopic);
                DrawSequenceField(profile, "onObjectivesCompleteSequence", "Objectives Complete", "Authoring slot for objective-complete dialogue. Not consumed by QuestDialogueBridge today.");
                DrawSequenceField(profile, "onReadyToTurnInSequence", "Ready To Turn In", "Played by QuestDialogueBridge when the quest enters ready-to-turn-in state.");
                DrawSequenceField(profile, "onTurnedInSequence", "Turned In", "Authoring slot for the turn-in moment. Not consumed by QuestDialogueBridge today.");
                DrawSequenceField(profile, "onCompletedSequence", "Completed", "Played by QuestDialogueBridge when the quest completes.");
                EditorGUILayout.EndVertical();
            }

            _stagesFoldout = EditorGUILayout.Foldout(_stagesFoldout, new GUIContent("Stage Bindings", "Dialogue linked to quest stage IDs and stage advancement."), true);
            if (_stagesFoldout)
                DrawStageBindings(profile);

            _objectivesFoldout = EditorGUILayout.Foldout(_objectivesFoldout, new GUIContent("Objective Bindings", "Dialogue linked to objective IDs. Current runtime does not consume these bindings."), true);
            if (_objectivesFoldout)
            {
                EditorGUILayout.HelpBox("Objective bindings are stored but QuestDialogueBridge does not currently subscribe to objective progress/completion events.", MessageType.Info);
                DrawDefaultProperty("objectiveBindings", "Objective dialogue bindings stored for future objective event integration.");
            }

            _ambientFoldout = EditorGUILayout.Foldout(_ambientFoldout, new GUIContent("Ambient Topics", "Topic IDs used by contextual NPC dialogue when sequences are not played."), true);
            if (_ambientFoldout)
            {
                profile.availableAmbientTopic = EditorGUILayout.TextField(new GUIContent("Available Ambient Topic", "Topic used for available quest ambient dialogue."), profile.availableAmbientTopic);
                profile.inProgressAmbientTopic = EditorGUILayout.TextField(new GUIContent("In Progress Ambient Topic", "Topic used for in-progress quest ambient dialogue."), profile.inProgressAmbientTopic);
                profile.readyToTurnInAmbientTopic = EditorGUILayout.TextField(new GUIContent("Ready To Turn In Ambient Topic", "Topic used when the quest is ready for turn-in and no sequence plays."), profile.readyToTurnInAmbientTopic);
                profile.completedAmbientTopic = EditorGUILayout.TextField(new GUIContent("Completed Ambient Topic", "Topic used after quest completion and no sequence plays."), profile.completedAmbientTopic);
            }

            if (GUI.changed)
                EditorUtility.SetDirty(profile);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefaultProperty(string propertyName, string tooltip)
        {
            serializedObject.Update();
            var prop = serializedObject.FindProperty(propertyName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, new GUIContent(ObjectNames.NicifyVariableName(propertyName), tooltip), true);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSequenceField(QuestDialogueProfileSO profile, string fieldName, string label, string tooltip)
        {
            var prop = serializedObject.FindProperty(fieldName);
            if (prop == null)
                return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));
            var sequence = prop.objectReferenceValue as NpcDialogueSequenceSO;
            if (GUILayout.Button(new GUIContent("Create", "Create and assign a new NPC dialogue sequence."), GUILayout.Width(58f)))
            {
                prop.objectReferenceValue = CreateProfileSequence(profile, label);
                serializedObject.ApplyModifiedProperties();
            }

            using (new EditorGUI.DisabledScope(sequence == null))
            {
                if (GUILayout.Button(new GUIContent("Select", "Select the assigned sequence."), GUILayout.Width(54f)))
                    Selection.activeObject = sequence;
                if (GUILayout.Button(new GUIContent("Ping", "Ping the assigned sequence."), GUILayout.Width(44f)))
                    EditorGUIUtility.PingObject(sequence);
                if (GUILayout.Button(new GUIContent("Clear", "Clear this sequence reference."), GUILayout.Width(46f)))
                {
                    prop.objectReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStageBindings(QuestDialogueProfileSO profile)
        {
            profile.stageBindings ??= Array.Empty<StageDialogueBinding>();
            EditorGUILayout.BeginVertical("box");
            for (int i = 0; i < profile.stageBindings.Length; i++)
            {
                var binding = profile.stageBindings[i] ?? new StageDialogueBinding();
                profile.stageBindings[i] = binding;

                EditorGUILayout.LabelField(new GUIContent($"Binding {i + 1}", "Stage dialogue binding. Stage ID is preferred for stability; index is a fallback."), EditorStyles.boldLabel);
                binding.stageId = EditorGUILayout.TextField(new GUIContent("Stage Id", "Stable quest stage ID this binding targets."), binding.stageId);
                binding.stageIndex = EditorGUILayout.IntField(new GUIContent("Stage Index", "Fallback quest stage index. Prefer Stage Id when possible."), binding.stageIndex);
                binding.stageReminderTopic = EditorGUILayout.TextField(new GUIContent("Stage Reminder Topic", "Topic spoken when the stage starts and no stage-start sequence plays."), binding.stageReminderTopic);
                DrawStageSequence(profile, binding, true);
                DrawStageSequence(profile, binding, false);

                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button(new GUIContent("Remove Binding", "Remove this stage binding. Does not delete assigned sequence assets.")))
                {
                    var list = new List<StageDialogueBinding>(profile.stageBindings);
                    list.RemoveAt(i);
                    profile.stageBindings = list.ToArray();
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(4f);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Add Stage Binding", "Add a blank stage dialogue binding.")))
            {
                var list = new List<StageDialogueBinding>(profile.stageBindings) { new StageDialogueBinding() };
                profile.stageBindings = list.ToArray();
            }
            using (new EditorGUI.DisabledScope(profile.questDefinition == null))
            {
                if (GUILayout.Button(new GUIContent("Create Missing Stage Sequences", "Create missing started/completed sequence assets for every stage binding.")))
                    CreateMissingStageSequences(profile);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawStageSequence(QuestDialogueProfileSO profile, StageDialogueBinding binding, bool started)
        {
            EditorGUILayout.BeginHorizontal();
            var current = started ? binding.onStageStarted : binding.onStageCompleted;
            current = (NpcDialogueSequenceSO)EditorGUILayout.ObjectField(new GUIContent(started ? "On Stage Started" : "On Stage Completed", started ? "Sequence played by QuestDialogueBridge when this stage becomes current." : "Stored stage-completion sequence. Current bridge does not play stage-completed events."), current, typeof(NpcDialogueSequenceSO), false);
            if (started)
                binding.onStageStarted = current;
            else
                binding.onStageCompleted = current;

            if (GUILayout.Button(new GUIContent("Create", "Create and assign a sequence asset."), GUILayout.Width(58f)))
            {
                var created = CreateProfileSequence(profile, $"{binding.stageId}_{(started ? "Started" : "Completed")}");
                if (started)
                    binding.onStageStarted = created;
                else
                    binding.onStageCompleted = created;
            }

            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUILayout.Button(new GUIContent("Select", "Select the assigned sequence."), GUILayout.Width(54f)))
                    Selection.activeObject = current;
                if (GUILayout.Button(new GUIContent("Ping", "Ping the assigned sequence."), GUILayout.Width(44f)))
                    EditorGUIUtility.PingObject(current);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidation(QuestDialogueProfileSO profile)
        {
            var messages = CollectValidation(profile);
            EditorGUILayout.LabelField(new GUIContent("Validation", "Non-destructive checks for profile/quest binding issues."), EditorStyles.boldLabel);
            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("No profile authoring issues detected.", MessageType.Info);
                return;
            }

            for (int i = 0; i < messages.Count; i++)
                EditorGUILayout.HelpBox(messages[i].Item2, messages[i].Item1);
        }

        private static List<Tuple<MessageType, string>> CollectValidation(QuestDialogueProfileSO profile)
        {
            var messages = new List<Tuple<MessageType, string>>();
            if (profile.questDefinition == null)
            {
                messages.Add(Tuple.Create(MessageType.Error, "Missing linked quest definition."));
                return messages;
            }

            var stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (profile.questDefinition.stages != null)
            {
                for (int i = 0; i < profile.questDefinition.stages.Count; i++)
                {
                    var stage = profile.questDefinition.stages[i];
                    if (!string.IsNullOrWhiteSpace(stage?.id))
                        stageIds.Add(stage.id.Trim());
                }
            }

            var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (profile.stageBindings != null)
            {
                for (int i = 0; i < profile.stageBindings.Length; i++)
                {
                    var binding = profile.stageBindings[i];
                    if (binding == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(binding.stageId))
                    {
                        string id = binding.stageId.Trim();
                        bound.Add(id);
                        if (!stageIds.Contains(id))
                            messages.Add(Tuple.Create(MessageType.Warning, $"Binding {i + 1} references missing quest stage ID '{id}'."));
                    }
                }
            }

            foreach (string id in stageIds)
            {
                if (!bound.Contains(id))
                    messages.Add(Tuple.Create(MessageType.Info, $"Quest stage '{id}' has no dialogue binding."));
            }

            if (profile.onAcceptedSequence == null)
                messages.Add(Tuple.Create(MessageType.Info, "No accepted sequence assigned."));
            if (profile.questDefinition.completionMode == QuestCompletionMode.RequireTurnIn && profile.onReadyToTurnInSequence == null)
                messages.Add(Tuple.Create(MessageType.Warning, "RequireTurnIn quest has no ready-to-turn-in sequence."));
            if (profile.onCompletedSequence == null)
                messages.Add(Tuple.Create(MessageType.Info, "No completed sequence assigned."));
            if (profile.objectiveBindings != null && profile.objectiveBindings.Length > 0)
                messages.Add(Tuple.Create(MessageType.Info, "Objective bindings exist, but runtime objective dialogue events are not currently consumed."));

            return messages;
        }

        private static void SyncObjectiveBindings(QuestDialogueProfileSO profile)
        {
            if (profile?.questDefinition == null)
                return;

            Undo.RecordObject(profile, "Sync Quest Dialogue Objective Bindings");
            var bindings = new List<ObjectiveDialogueBinding>();
            if (profile.objectiveBindings != null)
                bindings.AddRange(profile.objectiveBindings);

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < bindings.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(bindings[i]?.objectiveId))
                    existing.Add(bindings[i].objectiveId.Trim());
            }

            AddObjectiveBindings(profile.questDefinition.stages, bindings, existing);
            profile.objectiveBindings = bindings.ToArray();
            EditorUtility.SetDirty(profile);
        }

        private static void AddObjectiveBindings(List<QuestStageDefinition> stages, List<ObjectiveDialogueBinding> bindings, HashSet<string> existing)
        {
            if (stages == null)
                return;

            for (int i = 0; i < stages.Count; i++)
                AddObjectiveBindings(stages[i]?.objectives, bindings, existing);
        }

        private static void AddObjectiveBindings(List<QuestObjectiveDefinition> objectives, List<ObjectiveDialogueBinding> bindings, HashSet<string> existing)
        {
            if (objectives == null)
                return;

            for (int i = 0; i < objectives.Count; i++)
            {
                var objective = objectives[i];
                if (objective == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(objective.id) && existing.Add(objective.id.Trim()))
                    bindings.Add(new ObjectiveDialogueBinding { objectiveId = objective.id.Trim() });

                AddObjectiveBindings(objective.compoundObjectives, bindings, existing);
            }
        }

        private static void CreateMissingStageSequences(QuestDialogueProfileSO profile)
        {
            if (profile == null)
                return;

            string directory = GetAssetDirectory(profile);
            Undo.RecordObject(profile, "Create Missing Stage Dialogue Sequences");
            if (profile.stageBindings == null)
                return;

            for (int i = 0; i < profile.stageBindings.Length; i++)
            {
                var binding = profile.stageBindings[i];
                if (binding == null)
                    continue;

                string stage = QuestDefinitionSOEditor.SanitizeAssetToken(string.IsNullOrWhiteSpace(binding.stageId) ? $"Stage{i + 1}" : binding.stageId);
                if (binding.onStageStarted == null)
                    binding.onStageStarted = QuestDefinitionSOEditor.CreateSequenceAsset(directory, $"{profile.name}_{stage}_StartedSequence");
                if (binding.onStageCompleted == null)
                    binding.onStageCompleted = QuestDefinitionSOEditor.CreateSequenceAsset(directory, $"{profile.name}_{stage}_CompletedSequence");
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static NpcDialogueSequenceSO CreateProfileSequence(QuestDialogueProfileSO profile, string suffix)
        {
            string directory = GetAssetDirectory(profile);
            string questName = profile.questDefinition != null ? profile.questDefinition.name : profile.name;
            var sequence = QuestDefinitionSOEditor.CreateSequenceAsset(directory, $"{questName}_{QuestDefinitionSOEditor.SanitizeAssetToken(suffix)}Sequence");
            AssetDatabase.SaveAssets();
            return sequence;
        }

        private static string GetAssetDirectory(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string directory = string.IsNullOrWhiteSpace(path) ? "Assets" : Path.GetDirectoryName(path)?.Replace("\\", "/");
            return string.IsNullOrWhiteSpace(directory) ? "Assets" : directory;
        }
    }
}
