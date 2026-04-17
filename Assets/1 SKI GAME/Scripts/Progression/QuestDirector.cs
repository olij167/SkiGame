using System;
using System.Collections.Generic;
using SkiGame.Navigation;
using SkiGame.Tricks;
using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestDirector : MonoBehaviour
    {
        private readonly struct ConditionEvaluation
        {
            public readonly bool Completed;
            public readonly float Progress01;
            public readonly string ProgressText;
            public readonly float RawValue;

            public ConditionEvaluation(bool completed, float progress01, string progressText, float rawValue)
            {
                Completed = completed;
                Progress01 = progress01;
                ProgressText = progressText ?? string.Empty;
                RawValue = rawValue;
            }
        }

        public static QuestDirector Instance { get; private set; }

        [SerializeField] private QuestCatalogSO catalog;
        [SerializeField] private PlayerStatsManager playerStatsManager;
        [SerializeField] private QuestSignalBus signalBus;
        [SerializeField] private QuestContextProvider contextProvider;
        [SerializeField, Range(0.05f, 1f)] private float evaluateIntervalSeconds = 0.2f;
        [Header("Guidance")]
        [SerializeField] private bool enableTrackedQuestGuidance = true;
        [SerializeField] private int maxTrackedQuestWaypoints = 3;
        [SerializeField] private float guidanceArriveDistance = 9f;

        public event Action<QuestDefinitionSO, QuestRuntimeState> OnQuestAccepted;
        public event Action<QuestDefinitionSO, QuestRuntimeState> OnQuestCompleted;
        public event Action<QuestDefinitionSO, QuestRuntimeState> OnQuestStageAdvanced;
        public event Action OnQuestTrackingChanged;

        private readonly Dictionary<string, QuestDefinitionSO> _questById = new Dictionary<string, QuestDefinitionSO>(StringComparer.OrdinalIgnoreCase);
        private readonly List<QuestRuntimeState> _trackedQuestBuffer = new List<QuestRuntimeState>();
        private readonly HashSet<string> _activeGuidanceSourceKeys = new HashSet<string>(StringComparer.Ordinal);
        private float _nextEvaluateTime;
        private bool _dirty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildLookup();
            ResolveReferences();
            EnsureTutorialQuestFlowController();
        }

        private void Start()
        {
            ResolveReferences();
            EnsureProfileState();
            AutoAcceptConfiguredQuests();
            _nextEvaluateTime = Time.unscaledTime + evaluateIntervalSeconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextEvaluateTime)
                return;

            _nextEvaluateTime = Time.unscaledTime + evaluateIntervalSeconds;

            ResolveReferences();
            var profile = EnsureProfileState();
            if (profile == null)
                return;

            bool changed = false;

            for (int i = 0; i < profile.quests.questStates.Count; i++)
            {
                var runtimeState = profile.quests.questStates[i];
                if (runtimeState == null || !runtimeState.accepted || runtimeState.completed)
                    continue;

                if (!_questById.TryGetValue(runtimeState.questId ?? string.Empty, out var definition) || definition == null)
                    continue;

                if (EvaluateQuest(definition, runtimeState))
                    changed = true;
            }

            if (changed)
                MarkDirty();

            RefreshTrackedQuestGuidance(profile);
            FlushDirtySaveIfNeeded();
        }

        public bool TryAcceptQuest(string questId)
        {
            if (!_questById.TryGetValue(questId ?? string.Empty, out var definition) || definition == null)
                return false;

            var profile = EnsureProfileState();
            if (profile == null)
                return false;

            var runtimeState = GetOrCreateQuestState(profile.quests, definition);
            if (runtimeState.completed)
                return false;

            if (!runtimeState.accepted)
            {
                runtimeState.accepted = true;
                SyncObjectiveStates(definition, runtimeState);
                EnsureQuestTracked(profile.quests, definition.SafeId);
                ProgressionEventRecorder.RecordQuestAccepted(profile);
                MarkDirty();
                RefreshTrackedQuestGuidance(profile);
                OnQuestAccepted?.Invoke(definition, runtimeState);
            }

            FlushDirtySaveIfNeeded(force: true);
            return true;
        }

        public QuestRuntimeState GetQuestState(string questId)
        {
            var profile = EnsureProfileState();
            if (profile?.quests?.questStates == null || string.IsNullOrWhiteSpace(questId))
                return null;

            for (int i = 0; i < profile.quests.questStates.Count; i++)
            {
                var state = profile.quests.questStates[i];
                if (state == null)
                    continue;

                if (string.Equals(state.questId, questId, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            return null;
        }

        public QuestDefinitionSO GetQuestDefinition(string questId)
        {
            return _questById.TryGetValue(questId ?? string.Empty, out var definition) ? definition : null;
        }

        public bool IsQuestAccepted(string questId)
        {
            var state = GetQuestState(questId);
            return state != null && state.accepted;
        }

        public bool IsQuestCompleted(string questId)
        {
            var state = GetQuestState(questId);
            return state != null && state.completed;
        }

        public void GetQuestDefinitions(List<QuestDefinitionSO> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            if (catalog?.quests == null)
                return;

            for (int i = 0; i < catalog.quests.Count; i++)
            {
                var quest = catalog.quests[i];
                if (quest != null)
                    buffer.Add(quest);
            }
        }

        public bool IsQuestTracked(string questId)
        {
            var profile = EnsureProfileState();
            var tracked = profile?.quests?.trackedQuestIds;
            return tracked != null && tracked.Contains(questId);
        }

        public void SetQuestTracked(string questId, bool tracked)
        {
            var profile = EnsureProfileState();
            if (profile?.quests == null || string.IsNullOrWhiteSpace(questId))
                return;

            profile.quests.trackedQuestIds ??= new List<string>();
            bool changed = false;

            if (tracked)
            {
                if (!profile.quests.trackedQuestIds.Contains(questId))
                {
                    profile.quests.trackedQuestIds.Add(questId);
                    changed = true;
                }
            }
            else
            {
                changed = profile.quests.trackedQuestIds.Remove(questId);
            }

            if (changed)
            {
                MarkDirty();
                RefreshTrackedQuestGuidance(profile);
                FlushDirtySaveIfNeeded(force: true);
                OnQuestTrackingChanged?.Invoke();
            }
        }

        public void GetTrackedQuestStates(List<QuestRuntimeState> buffer, bool includeCompleted = false)
        {
            if (buffer == null)
                return;

            buffer.Clear();

            var profile = EnsureProfileState();
            if (profile?.quests?.trackedQuestIds == null || profile.quests.questStates == null)
                return;

            for (int i = 0; i < profile.quests.trackedQuestIds.Count; i++)
            {
                string questId = profile.quests.trackedQuestIds[i];
                var state = GetQuestState(questId);
                if (state == null)
                    continue;

                if (!includeCompleted && state.completed)
                    continue;

                buffer.Add(state);
            }
        }

        public void GetActiveQuestStates(List<QuestRuntimeState> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            var profile = EnsureProfileState();
            if (profile?.quests?.questStates == null)
                return;

            for (int i = 0; i < profile.quests.questStates.Count; i++)
            {
                var state = profile.quests.questStates[i];
                if (state != null && state.accepted && !state.completed)
                    buffer.Add(state);
            }
        }

        public void GetCompletedQuestStates(List<QuestRuntimeState> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            var profile = EnsureProfileState();
            if (profile?.quests?.questStates == null)
                return;

            for (int i = 0; i < profile.quests.questStates.Count; i++)
            {
                var state = profile.quests.questStates[i];
                if (state != null && state.completed)
                    buffer.Add(state);
            }
        }

        private void ResolveReferences()
        {
            if (playerStatsManager == null)
                playerStatsManager = PlayerStatsManager.Instance != null
                    ? PlayerStatsManager.Instance
                    : FindObjectOfType<PlayerStatsManager>();

            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();

            if (contextProvider == null)
                contextProvider = FindObjectOfType<QuestContextProvider>();
        }

        private PlayerStatsProfile EnsureProfileState()
        {
            if (playerStatsManager == null)
                return null;

            if (playerStatsManager.Profile == null)
                playerStatsManager.Load();

            var profile = playerStatsManager.Profile;
            profile?.Sanitize();
            return profile;
        }

        private void BuildLookup()
        {
            _questById.Clear();
            if (catalog == null || catalog.quests == null)
                return;

            for (int i = 0; i < catalog.quests.Count; i++)
            {
                var quest = catalog.quests[i];
                if (quest == null)
                    continue;

                string id = quest.SafeId;
                if (!string.IsNullOrWhiteSpace(id))
                    _questById[id] = quest;
            }
        }

        private void EnsureTutorialQuestFlowController()
        {
            if (!CatalogContainsTutorialQuest())
                return;

            if (FindObjectOfType<TutorialQuestFlowController>() != null)
                return;

            var go = new GameObject("TutorialQuestFlowController");
            go.AddComponent<TutorialQuestFlowController>();
        }

        private bool CatalogContainsTutorialQuest()
        {
            if (catalog?.quests == null)
                return false;

            for (int i = 0; i < catalog.quests.Count; i++)
            {
                var quest = catalog.quests[i];
                if (quest != null && quest.tutorialQuest)
                    return true;
            }

            return false;
        }

        private void AutoAcceptConfiguredQuests()
        {
            if (catalog == null || catalog.quests == null)
                return;

            for (int i = 0; i < catalog.quests.Count; i++)
            {
                var quest = catalog.quests[i];
                if (quest != null && quest.autoAccept)
                    TryAcceptQuest(quest.SafeId);
            }
        }

        private QuestRuntimeState GetOrCreateQuestState(QuestLogState questLog, QuestDefinitionSO definition)
        {
            questLog.questStates ??= new List<QuestRuntimeState>();

            for (int i = 0; i < questLog.questStates.Count; i++)
            {
                var state = questLog.questStates[i];
                if (state == null)
                    continue;

                if (string.Equals(state.questId, definition.SafeId, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            var created = new QuestRuntimeState
            {
                questId = definition.SafeId,
                accepted = false,
                completed = false,
                currentStageIndex = 0,
                objectiveStates = new List<QuestObjectiveRuntimeState>()
            };

            questLog.questStates.Add(created);
            return created;
        }

        private void SyncObjectiveStates(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            runtimeState.objectiveStates ??= new List<QuestObjectiveRuntimeState>();
            var stage = definition.GetStage(runtimeState.currentStageIndex);
            if (stage == null)
            {
                runtimeState.objectiveStates.Clear();
                return;
            }

            for (int i = runtimeState.objectiveStates.Count - 1; i >= 0; i--)
            {
                var existing = runtimeState.objectiveStates[i];
                if (existing == null || !StageContainsObjective(stage, existing.objectiveId))
                    runtimeState.objectiveStates.RemoveAt(i);
            }

            if (stage.objectives == null)
                return;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                if (objective == null || string.IsNullOrWhiteSpace(objective.id))
                    continue;

                if (GetObjectiveState(runtimeState, objective.id) != null)
                    continue;

                runtimeState.objectiveStates.Add(new QuestObjectiveRuntimeState
                {
                    objectiveId = objective.id,
                    available = !objective.hiddenUntilAvailable,
                    completed = false,
                    progress01 = 0f,
                    progressValue = 0f,
                    progressText = string.Empty,
                    conditionStates = new List<QuestConditionRuntimeState>()
                });
            }
        }

        private bool EvaluateQuest(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            bool changed = false;
            var stage = definition.GetStage(runtimeState.currentStageIndex);
            if (stage == null)
            {
                CompleteQuest(definition, runtimeState);
                return true;
            }

            SyncObjectiveStates(definition, runtimeState);

            int requiredCount = 0;
            int completedRequiredCount = 0;

            if (stage.objectives != null)
            {
                for (int i = 0; i < stage.objectives.Count; i++)
                {
                    var objective = stage.objectives[i];
                    if (objective == null || string.IsNullOrWhiteSpace(objective.id))
                        continue;

                    var objectiveState = GetObjectiveState(runtimeState, objective.id);
                    if (objectiveState == null)
                        continue;

                    var eval = EvaluateCondition(objectiveState, objective.BuildCondition(), "0");
                    bool wasCompleted = objectiveState.completed;

                    objectiveState.progress01 = eval.Progress01;
                    objectiveState.progressValue = eval.RawValue;
                    objectiveState.progressText = eval.ProgressText;
                    objectiveState.completed = eval.Completed;
                    objectiveState.available = true;

                    if (wasCompleted != objectiveState.completed)
                        changed = true;

                    if (!objective.optional)
                    {
                        requiredCount++;
                        if (objectiveState.completed)
                            completedRequiredCount++;
                    }
                }
            }

            if (requiredCount > 0 && completedRequiredCount >= requiredCount)
            {
                runtimeState.currentStageIndex++;
                runtimeState.objectiveStates.Clear();
                SyncObjectiveStates(definition, runtimeState);
                EnsureQuestTracked(EnsureProfileState()?.quests, definition.SafeId);
                var profile = EnsureProfileState();
                if (profile != null)
                    ProgressionEventRecorder.RecordQuestStageCompleted(profile);
                changed = true;

                if (definition.GetStage(runtimeState.currentStageIndex) == null)
                {
                    CompleteQuest(definition, runtimeState);
                    changed = true;
                }
                else
                {
                    OnQuestStageAdvanced?.Invoke(definition, runtimeState);
                }
            }

            return changed;
        }

        private ConditionEvaluation EvaluateCondition(QuestObjectiveRuntimeState objectiveState, QuestObjectiveCondition condition, string path)
        {
            if (condition == null)
                return new ConditionEvaluation(true, 1f, string.Empty, 1f);

            var runtime = GetOrCreateConditionState(objectiveState, path);
            int currentSequence = signalBus != null ? signalBus.CurrentSequence : 0;
            if (runtime.lastSeenEventSequence > currentSequence)
                runtime.lastSeenEventSequence = 0;

            switch (condition.kind)
            {
                case QuestConditionKind.EventCount:
                    if (!runtime.initialized)
                    {
                        runtime.initialized = true;
                        runtime.lastSeenEventSequence = currentSequence;
                    }

                    int deltaEvents = signalBus != null
                        ? signalBus.CountEvents(condition.key, runtime.lastSeenEventSequence, condition.filters)
                        : 0;
                    runtime.accumulatedValue += deltaEvents;
                    runtime.lastSeenEventSequence = currentSequence;

                    float eventCurrent = runtime.accumulatedValue;
                    int eventTarget = Mathf.Max(1, condition.targetCount);
                    bool eventComplete = eventCurrent >= eventTarget;
                    runtime.completed = eventComplete;
                    return new ConditionEvaluation(eventComplete, Mathf.Clamp01(eventCurrent / eventTarget), $"{eventCurrent:0}/{eventTarget}", eventCurrent);

                case QuestConditionKind.TrickRuleEventCount:
                    if (!runtime.initialized)
                    {
                        runtime.initialized = true;
                        runtime.lastSeenEventSequence = currentSequence;
                    }

                    int matchedEvents = signalBus != null
                        ? signalBus.CountEvents(
                            condition.key,
                            runtime.lastSeenEventSequence,
                            condition.filters,
                            data => TrickActivityRules.Matches(condition.trickRequirement, TrickActivityRules.CreateDescriptorFromSignalData(data)))
                        : 0;
                    runtime.accumulatedValue += matchedEvents;
                    runtime.lastSeenEventSequence = currentSequence;

                    float trickCurrent = runtime.accumulatedValue;
                    int trickTarget = Mathf.Max(1, condition.targetCount);
                    bool trickComplete = trickCurrent >= trickTarget;
                    runtime.completed = trickComplete;
                    return new ConditionEvaluation(trickComplete, Mathf.Clamp01(trickCurrent / trickTarget), $"{trickCurrent:0}/{trickTarget}", trickCurrent);

                case QuestConditionKind.StatAccumulate:
                    float currentValue = contextProvider != null ? contextProvider.ReadFloat(condition.key) : 0f;
                    if (!runtime.initialized)
                    {
                        runtime.initialized = true;
                        runtime.lastSeenStatValue = currentValue;
                    }

                    float delta = Mathf.Max(0f, currentValue - runtime.lastSeenStatValue);
                    runtime.accumulatedValue += delta;
                    runtime.lastSeenStatValue = currentValue;

                    float targetValue = Mathf.Max(0.0001f, condition.targetValue);
                    bool accumulateComplete = EvaluateComparison(runtime.accumulatedValue, targetValue, condition.comparison);
                    runtime.completed = accumulateComplete;
                    return new ConditionEvaluation(
                        accumulateComplete,
                        Mathf.Clamp01(runtime.accumulatedValue / targetValue),
                        $"{FormatMetricValue(runtime.accumulatedValue)}/{FormatMetricValue(targetValue)}",
                        runtime.accumulatedValue);

                case QuestConditionKind.StatThreshold:
                    float statValue = contextProvider != null ? contextProvider.ReadFloat(condition.key) : 0f;
                    bool statComplete = EvaluateComparison(statValue, condition.targetValue, condition.comparison);
                    runtime.completed = statComplete;
                    return new ConditionEvaluation(
                        statComplete,
                        CalculateThresholdProgress01(statValue, condition.targetValue, condition.comparison, statComplete),
                        $"{FormatMetricValue(statValue)}/{FormatMetricValue(condition.targetValue)}",
                        statValue);

                case QuestConditionKind.StateEquals:
                    bool stateComplete;
                    if (condition.stateValueKind == QuestValueKind.String)
                    {
                        string currentText = contextProvider != null ? contextProvider.ReadString(condition.key) : string.Empty;
                        stateComplete = string.Equals(currentText ?? string.Empty, condition.stringValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        bool currentState = contextProvider != null && contextProvider.ReadBool(condition.key);
                        stateComplete = currentState == condition.boolValue;
                    }

                    runtime.completed = stateComplete;
                    return new ConditionEvaluation(stateComplete, stateComplete ? 1f : 0f, stateComplete ? "Ready" : "Waiting", stateComplete ? 1f : 0f);

                case QuestConditionKind.AllOf:
                    return EvaluateAllOf(objectiveState, condition, path, runtime);

                case QuestConditionKind.AnyOf:
                    return EvaluateAnyOf(objectiveState, condition, path, runtime);

                case QuestConditionKind.Sequence:
                    return EvaluateSequence(objectiveState, condition, path, runtime);
            }

            return new ConditionEvaluation(false, 0f, string.Empty, 0f);
        }

        private ConditionEvaluation EvaluateAllOf(QuestObjectiveRuntimeState objectiveState, QuestObjectiveCondition condition, string path, QuestConditionRuntimeState runtime)
        {
            int childCount = condition.children != null ? condition.children.Count : 0;
            if (childCount <= 0)
                return new ConditionEvaluation(true, 1f, string.Empty, 1f);

            int completedChildren = 0;
            float progressTotal = 0f;
            for (int i = 0; i < childCount; i++)
            {
                var childEval = EvaluateCondition(objectiveState, condition.children[i], path + "/" + i);
                if (childEval.Completed)
                    completedChildren++;

                progressTotal += childEval.Progress01;
            }

            bool complete = completedChildren >= childCount;
            runtime.completed = complete;
            return new ConditionEvaluation(complete, progressTotal / childCount, $"{completedChildren}/{childCount}", completedChildren);
        }

        private ConditionEvaluation EvaluateAnyOf(QuestObjectiveRuntimeState objectiveState, QuestObjectiveCondition condition, string path, QuestConditionRuntimeState runtime)
        {
            int childCount = condition.children != null ? condition.children.Count : 0;
            if (childCount <= 0)
                return new ConditionEvaluation(true, 1f, string.Empty, 1f);

            int completedChildren = 0;
            float bestProgress = 0f;
            for (int i = 0; i < childCount; i++)
            {
                var childEval = EvaluateCondition(objectiveState, condition.children[i], path + "/" + i);
                if (childEval.Completed)
                    completedChildren++;

                bestProgress = Mathf.Max(bestProgress, childEval.Progress01);
            }

            bool complete = completedChildren > 0;
            runtime.completed = complete;
            return new ConditionEvaluation(complete, complete ? 1f : bestProgress, $"{completedChildren}/{childCount}", completedChildren);
        }

        private ConditionEvaluation EvaluateSequence(QuestObjectiveRuntimeState objectiveState, QuestObjectiveCondition condition, string path, QuestConditionRuntimeState runtime)
        {
            int childCount = condition.children != null ? condition.children.Count : 0;
            if (childCount <= 0)
                return new ConditionEvaluation(true, 1f, string.Empty, 1f);

            int index = Mathf.Clamp(runtime.sequenceChildIndex, 0, childCount);
            while (index < childCount)
            {
                var childEval = EvaluateCondition(objectiveState, condition.children[index], path + "/" + index);
                if (!childEval.Completed)
                {
                    runtime.sequenceChildIndex = index;
                    runtime.completed = false;
                    float progress = (index + childEval.Progress01) / childCount;
                    return new ConditionEvaluation(false, progress, $"{index}/{childCount}", index);
                }

                index++;
            }

            runtime.sequenceChildIndex = childCount;
            runtime.completed = true;
            return new ConditionEvaluation(true, 1f, $"{childCount}/{childCount}", childCount);
        }

        private static bool EvaluateComparison(float current, float target, QuestComparisonOp comparison)
        {
            switch (comparison)
            {
                case QuestComparisonOp.GreaterOrEqual:
                    return current >= target;
                case QuestComparisonOp.LessOrEqual:
                    return current <= target;
                case QuestComparisonOp.Equal:
                    return Mathf.Abs(current - target) <= 0.0001f;
                case QuestComparisonOp.NotEqual:
                    return Mathf.Abs(current - target) > 0.0001f;
                case QuestComparisonOp.Greater:
                    return current > target;
                case QuestComparisonOp.Less:
                    return current < target;
                default:
                    return false;
            }
        }

        private static float CalculateThresholdProgress01(float current, float target, QuestComparisonOp comparison, bool complete)
        {
            if (complete)
                return 1f;

            float safeTarget = Mathf.Max(Mathf.Abs(target), 0.0001f);
            float safeCurrent = Mathf.Max(Mathf.Abs(current), 0.0001f);

            switch (comparison)
            {
                case QuestComparisonOp.GreaterOrEqual:
                case QuestComparisonOp.Greater:
                    return Mathf.Clamp01(current / safeTarget);

                case QuestComparisonOp.LessOrEqual:
                case QuestComparisonOp.Less:
                    return Mathf.Clamp01(safeTarget / safeCurrent);

                case QuestComparisonOp.Equal:
                    return Mathf.Clamp01(1f - (Mathf.Abs(current - target) / safeTarget));

                case QuestComparisonOp.NotEqual:
                    return 0f;

                default:
                    return 0f;
            }
        }

        private static string FormatMetricValue(float value)
        {
            float abs = Mathf.Abs(value);
            if (abs <= 0.1f)
                return $"{value * 100f:0.#}%";

            if (abs < 1f)
                return $"{value:0.00}";

            if (abs < 10f)
                return $"{value:0.0}";

            return $"{value:0}";
        }

        private static bool StageContainsObjective(QuestStageDefinition stage, string objectiveId)
        {
            if (stage?.objectives == null || string.IsNullOrWhiteSpace(objectiveId))
                return false;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                if (objective == null)
                    continue;

                if (string.Equals(objective.id, objectiveId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static QuestObjectiveRuntimeState GetObjectiveState(QuestRuntimeState runtimeState, string objectiveId)
        {
            if (runtimeState?.objectiveStates == null || string.IsNullOrWhiteSpace(objectiveId))
                return null;

            for (int i = 0; i < runtimeState.objectiveStates.Count; i++)
            {
                var state = runtimeState.objectiveStates[i];
                if (state == null)
                    continue;

                if (string.Equals(state.objectiveId, objectiveId, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            return null;
        }

        private void RefreshTrackedQuestGuidance(PlayerStatsProfile profile)
        {
            if (!enableTrackedQuestGuidance || profile?.quests == null)
                return;

            var waypointManager = MapWaypointManager.Instance != null
                ? MapWaypointManager.Instance
                : MapWaypointManager.EnsureInstance();

            if (waypointManager == null)
                return;

            GetTrackedQuestStates(_trackedQuestBuffer);
            _activeGuidanceSourceKeys.Clear();

            int visibleCount = Mathf.Min(Mathf.Max(0, maxTrackedQuestWaypoints), _trackedQuestBuffer.Count);
            string activeGuidanceId = null;

            for (int i = 0; i < visibleCount; i++)
            {
                var state = _trackedQuestBuffer[i];
                var definition = state != null ? GetQuestDefinition(state.questId) : null;
                if (definition == null || state == null || state.completed)
                    continue;

                if (!TryResolveGuidanceTarget(definition, state, out var displayName, out var worldPosition, out var kind))
                    continue;

                string sourceKey = $"quest-guidance::{definition.SafeId}";
                _activeGuidanceSourceKeys.Add(sourceKey);

                Color accent = QuestVisualUtility.GetQuestAccent(definition.SafeId);
                UpsertGuidanceWaypoint(waypointManager, sourceKey, displayName, worldPosition, kind, accent, setActive: i == 0);

                if (i == 0)
                    activeGuidanceId = sourceKey;
            }

            var existingWaypoints = waypointManager.Waypoints;
            for (int i = existingWaypoints.Count - 1; i >= 0; i--)
            {
                var waypoint = existingWaypoints[i];
                if (string.IsNullOrWhiteSpace(waypoint.sourceKey) || !waypoint.sourceKey.StartsWith("quest-guidance::", StringComparison.Ordinal))
                    continue;

                if (!_activeGuidanceSourceKeys.Contains(waypoint.sourceKey))
                    waypointManager.RemoveWaypoint(waypoint.id);
            }

            if (!string.IsNullOrWhiteSpace(activeGuidanceId))
                waypointManager.SetActiveWaypoint(activeGuidanceId);
        }

        private static void UpsertGuidanceWaypoint(
            MapWaypointManager waypointManager,
            string sourceKey,
            string displayName,
            Vector3 worldPosition,
            NavigationTargetKind kind,
            Color accent,
            bool setActive)
        {
            if (waypointManager == null || string.IsNullOrWhiteSpace(sourceKey))
                return;

            if (waypointManager.TryGetWaypointBySourceKey(sourceKey, out var existing))
            {
                bool moved = (existing.worldPosition - worldPosition).sqrMagnitude > 1f;
                if (moved)
                {
                    waypointManager.RemoveWaypoint(existing.id);
                    waypointManager.ToggleSourceWaypoint(sourceKey, displayName, worldPosition, kind, accent, setActive);
                    return;
                }

                if (!string.Equals(existing.displayName, displayName, StringComparison.Ordinal))
                    waypointManager.RenameWaypoint(existing.id, displayName);

                if (existing.color != accent)
                    waypointManager.SetWaypointColour(existing.id, accent);

                if (setActive)
                    waypointManager.SetActiveWaypoint(existing.id);

                return;
            }

            waypointManager.ToggleSourceWaypoint(sourceKey, displayName, worldPosition, kind, accent, setActive);
        }

        private bool TryResolveGuidanceTarget(
            QuestDefinitionSO definition,
            QuestRuntimeState runtimeState,
            out string displayName,
            out Vector3 worldPosition,
            out NavigationTargetKind kind)
        {
            displayName = string.Empty;
            worldPosition = Vector3.zero;
            kind = NavigationTargetKind.Custom;

            if (definition == null || runtimeState?.objectiveStates == null)
                return false;

            var stage = definition.GetStage(runtimeState.currentStageIndex);
            if (stage?.objectives == null)
                return false;

            for (int i = 0; i < runtimeState.objectiveStates.Count; i++)
            {
                var objectiveState = runtimeState.objectiveStates[i];
                if (objectiveState == null || objectiveState.completed)
                    continue;

                QuestObjectiveDefinition objective = FindObjectiveDefinition(stage, objectiveState.objectiveId);
                if (objective == null)
                    continue;

                if (TryResolveObjectiveGuidance(objective, out worldPosition, out kind))
                {
                    string questTitle = string.IsNullOrWhiteSpace(definition.title) ? definition.SafeId : definition.title.Trim();
                    string objectiveTitle = string.IsNullOrWhiteSpace(objective.title) ? objective.BuildAuthoringSummary() : objective.title.Trim();
                    displayName = $"{questTitle}: {objectiveTitle}";
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveObjectiveGuidance(QuestObjectiveDefinition objective, out Vector3 worldPosition, out NavigationTargetKind kind)
        {
            worldPosition = Vector3.zero;
            kind = NavigationTargetKind.Custom;

            if (objective == null)
                return false;

            switch (objective.template)
            {
                case QuestObjectiveTemplate.OpenUiScreen:
                    switch (objective.uiTarget)
                    {
                        case QuestUiScreenTargetType.ShopOpened:
                            if (TryResolveWorldTarget(FindObjectOfType<CustomizationPortal>(), out worldPosition))
                            {
                                kind = NavigationTargetKind.Shop;
                                return true;
                            }
                            break;

                        case QuestUiScreenTargetType.KioskOpened:
                            if (TryResolveWorldTarget(FindObjectOfType<SkiPassKiosk>(), out worldPosition))
                            {
                                kind = NavigationTargetKind.PointOfInterest;
                                return true;
                            }
                            break;

                        case QuestUiScreenTargetType.RaceKioskOpened:
                            if (TryResolveWorldTarget(FindObjectOfType<RaceKiosk>(), out worldPosition))
                            {
                                kind = NavigationTargetKind.PointOfInterest;
                                return true;
                            }
                            break;
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    switch (objective.interactionTarget)
                    {
                        case QuestInteractionTargetType.EnterResort:
                            if (TryResolveResortTarget(out worldPosition))
                            {
                                kind = NavigationTargetKind.Resort;
                                return true;
                            }
                            break;

                        case QuestInteractionTargetType.ExitResort:
                            if (TryResolveResortTarget(out worldPosition))
                            {
                                kind = NavigationTargetKind.Resort;
                                return true;
                            }
                            break;

                        case QuestInteractionTargetType.ClaimDefaultPass:
                            if (TryResolveWorldTarget(FindObjectOfType<SkiPassKiosk>(), out worldPosition))
                            {
                                kind = NavigationTargetKind.PointOfInterest;
                                return true;
                            }
                            break;

                        case QuestInteractionTargetType.MountLift:
                            if (TryResolveLiftTarget(topStation: false, out worldPosition))
                            {
                                kind = NavigationTargetKind.Lift;
                                return true;
                            }
                            break;

                        case QuestInteractionTargetType.DismountLift:
                            if (TryResolveLiftTarget(topStation: true, out worldPosition))
                            {
                                kind = NavigationTargetKind.Lift;
                                return true;
                            }
                            break;
                    }
                    break;

                case QuestObjectiveTemplate.Activity:
                    switch (objective.activityTarget)
                    {
                        case QuestActivityTargetType.RescueStarted:
                            if (TryResolveWorldTarget(FindObjectOfType<MedicTentActivityHub>(), out worldPosition))
                            {
                                kind = NavigationTargetKind.PointOfInterest;
                                return true;
                            }
                            break;

                        case QuestActivityTargetType.RaceStarted:
                            if (TryResolveWorldTarget(FindObjectOfType<RaceKiosk>(), out worldPosition))
                            {
                                kind = NavigationTargetKind.PointOfInterest;
                                return true;
                            }
                            break;
                    }
                    break;
            }

            return false;
        }

        private static QuestObjectiveDefinition FindObjectiveDefinition(QuestStageDefinition stage, string objectiveId)
        {
            if (stage?.objectives == null || string.IsNullOrWhiteSpace(objectiveId))
                return null;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                if (objective != null && string.Equals(objective.id, objectiveId, StringComparison.OrdinalIgnoreCase))
                    return objective;
            }

            return null;
        }

        private static bool TryResolveWorldTarget(Component component, out Vector3 worldPosition)
        {
            if (component != null)
            {
                worldPosition = component.transform.position;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        private static bool TryResolveResortTarget(out Vector3 worldPosition)
        {
            var zone = FindObjectOfType<SkiResortZone>();
            if (zone != null)
            {
                if (zone.entrancePoint != null)
                {
                    worldPosition = zone.entrancePoint.position;
                    return true;
                }

                worldPosition = zone.transform.position;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        private static bool TryResolveLiftTarget(bool topStation, out Vector3 worldPosition)
        {
#if UNITY_2023_1_OR_NEWER
            var lifts = FindObjectsByType<LiftLine>(FindObjectsSortMode.None);
#else
            var lifts = FindObjectsOfType<LiftLine>();
#endif
            if (lifts == null || lifts.Length == 0)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            Vector3 playerPosition = Vector3.zero;
            var ski = FindObjectOfType<SkiController>();
            if (ski != null)
                playerPosition = ski.transform.position;

            LiftLine best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < lifts.Length; i++)
            {
                var lift = lifts[i];
                if (lift == null)
                    continue;

                Transform target = topStation ? lift.topStation : lift.bottomStation;
                Vector3 anchor = target != null ? target.position : lift.transform.position;
                float sqr = (anchor - playerPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = lift;
                    bestSqr = sqr;
                }
            }

            if (best != null)
            {
                Transform target = topStation ? best.topStation : best.bottomStation;
                worldPosition = target != null ? target.position : best.transform.position;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        private static QuestConditionRuntimeState GetOrCreateConditionState(QuestObjectiveRuntimeState objectiveState, string path)
        {
            objectiveState.conditionStates ??= new List<QuestConditionRuntimeState>();

            for (int i = 0; i < objectiveState.conditionStates.Count; i++)
            {
                var existing = objectiveState.conditionStates[i];
                if (existing == null)
                    continue;

                if (string.Equals(existing.path, path, StringComparison.Ordinal))
                    return existing;
            }

            var created = new QuestConditionRuntimeState
            {
                path = path,
                initialized = false,
                completed = false,
                accumulatedValue = 0f,
                lastSeenStatValue = 0f,
                lastSeenEventSequence = 0,
                sequenceChildIndex = 0
            };

            objectiveState.conditionStates.Add(created);
            return created;
        }

        private void CompleteQuest(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            runtimeState.completed = true;

            var profile = EnsureProfileState();
            if (profile?.quests?.completedQuestIds != null && !profile.quests.completedQuestIds.Contains(definition.SafeId))
                profile.quests.completedQuestIds.Add(definition.SafeId);

            ProgressionEventRecorder.RecordQuestCompleted(profile, definition != null && definition.tutorialQuest);
            signalBus?.RaiseEvent(
                "quest.completed",
                QuestSignalData.Create()
                    .WithTag("questId", definition != null ? definition.SafeId : string.Empty)
                    .WithTag("tutorialQuest", definition != null && definition.tutorialQuest ? "true" : "false"));

            MarkDirty();
            OnQuestCompleted?.Invoke(definition, runtimeState);
        }

        private void EnsureQuestTracked(QuestLogState questLog, string questId)
        {
            if (questLog == null || string.IsNullOrWhiteSpace(questId))
                return;

            questLog.trackedQuestIds ??= new List<string>();
            if (!questLog.trackedQuestIds.Contains(questId))
            {
                questLog.trackedQuestIds.Add(questId);
                OnQuestTrackingChanged?.Invoke();
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void FlushDirtySaveIfNeeded(bool force = false)
        {
            if (!_dirty || playerStatsManager == null)
                return;

            if (!force && Time.unscaledTime < _nextEvaluateTime)
                return;

            playerStatsManager.Save();
            _dirty = false;
        }
    }
}
