using System;
using System.Collections.Generic;
using SkiGame.Tricks;
using UnityEngine;

namespace SkiGame.Progression
{
    public enum QuestConditionKind
    {
        EventCount = 0,
        StateEquals = 10,
        StatThreshold = 20,
        StatAccumulate = 30,
        AllOf = 40,
        AnyOf = 50,
        Sequence = 60,
        TrickRuleEventCount = 70
    }

    public enum QuestComparisonOp
    {
        GreaterOrEqual = 0,
        LessOrEqual = 10,
        Equal = 20,
        NotEqual = 30,
        Greater = 40,
        Less = 50
    }

    public enum QuestValueKind
    {
        Bool = 0,
        String = 10
    }

    public enum QuestObjectiveTemplate
    {
        UseInput = 0,
        PerformMovementSkill = 10,
        TravelDistance = 20,
        ReachThreshold = 30,
        ReachState = 40,
        Activity = 50,
        Trick = 60,
        Interaction = 70,
        OpenUiScreen = 80,
        Compound = 90,
        Advanced = 100
    }

    public enum QuestInputActionType
    {
        MoveInput = 0,
        Tuck = 10,
        PolePush = 20,
        PoleDrag = 30,
        LeanForward = 40,
        LeanBackward = 50,
        AirPose = 60
    }

    public enum QuestMovementSkillType
    {
        Wedge = 0,
        SkateSwitch = 10,
        QuickStop = 20,
        JumpAdjust = 30
    }

    public enum QuestDistanceStatType
    {
        GroundDistance = 0,
        AirDistance = 10,
        AirTime = 20,
        TuckTime = 30,
        TurnTime = 40
    }

    public enum QuestThresholdStatType
    {
        Speed = 0,
        Soreness = 10,
        AirTime = 20
    }

    public enum QuestStateTargetType
    {
        InResort = 0,
        OnLift = 10,
        SkisEquipped = 20,
        Airborne = 30,
        Grounded = 40,
        HasDefaultPass = 50,
        Stacked = 60,
        LessonActive = 70,
        LessonCompleted = 80
    }

    public enum QuestActivityTargetType
    {
        RaceStarted = 0,
        RaceCompleted = 10,
        RescueStarted = 20,
        RescueCompleted = 30
    }

    public enum QuestTrickTargetType
    {
        AnyLanding = 0,
        TuckLanding = 10,
        GrindLanding = 20,
        StyledLanding = 30,
        NamedLanding = 40,
        RuleLanding = 50
    }

    public enum QuestInteractionTargetType
    {
        EnterResort = 0,
        ExitResort = 10,
        MountLift = 20,
        DismountLift = 30,
        EquipSkis = 40,
        UnequipSkis = 50,
        ClaimDefaultPass = 60,
        Stacked = 70
    }

    public enum QuestUiScreenTargetType
    {
        OverlayOpened = 0,
        MapViewed = 10,
        StatsViewed = 20,
        TasksViewed = 30,
        QuestsViewed = 40,
        ShopOpened = 50,
        KioskOpened = 60,
        TaskRewardClaimed = 70,
        RaceKioskOpened = 80
    }

    public static class QuestSignalCatalog
    {
        public static string GetInputEventKey(QuestInputActionType inputAction)
        {
            switch (inputAction)
            {
                case QuestInputActionType.MoveInput: return "input.move.used";
                case QuestInputActionType.Tuck: return "input.tuck.used";
                case QuestInputActionType.PolePush: return "input.poles.push";
                case QuestInputActionType.PoleDrag: return "input.poles.drag";
                case QuestInputActionType.LeanForward: return "input.lean.forward";
                case QuestInputActionType.LeanBackward: return "input.lean.backward";
                case QuestInputActionType.AirPose: return "input.air_pose.used";
                default: return string.Empty;
            }
        }

        public static string GetMovementEventKey(QuestMovementSkillType movementSkill)
        {
            switch (movementSkill)
            {
                case QuestMovementSkillType.Wedge: return "movement.wedge.performed";
                case QuestMovementSkillType.SkateSwitch: return "movement.skate.switch";
                case QuestMovementSkillType.QuickStop: return "movement.quick_stop.performed";
                case QuestMovementSkillType.JumpAdjust: return "movement.jump_adjusted";
                default: return string.Empty;
            }
        }

        public static string GetDistanceStatKey(QuestDistanceStatType distanceStat)
        {
            switch (distanceStat)
            {
                case QuestDistanceStatType.GroundDistance: return "player.ground_distance";
                case QuestDistanceStatType.AirDistance: return "player.air_distance";
                case QuestDistanceStatType.AirTime: return "player.air_time";
                case QuestDistanceStatType.TuckTime: return "player.tuck_time";
                case QuestDistanceStatType.TurnTime: return "player.turn_time";
                default: return string.Empty;
            }
        }

        public static string GetThresholdStatKey(QuestThresholdStatType stat)
        {
            switch (stat)
            {
                case QuestThresholdStatType.Speed: return "player.speed";
                case QuestThresholdStatType.Soreness: return "player.soreness";
                case QuestThresholdStatType.AirTime: return "player.air_time";
                default: return string.Empty;
            }
        }

        public static string GetStateKey(QuestStateTargetType state)
        {
            switch (state)
            {
                case QuestStateTargetType.InResort: return "player.in_resort";
                case QuestStateTargetType.OnLift: return "player.on_lift";
                case QuestStateTargetType.SkisEquipped: return "player.skis_equipped";
                case QuestStateTargetType.Airborne: return "player.airborne";
                case QuestStateTargetType.Grounded: return "player.grounded";
                case QuestStateTargetType.HasDefaultPass: return "player.has_default_pass";
                case QuestStateTargetType.Stacked: return "player.stacked";
                case QuestStateTargetType.LessonActive: return "tutorial.lesson.active";
                case QuestStateTargetType.LessonCompleted: return "tutorial.lesson.completed";
                default: return string.Empty;
            }
        }

        public static string GetActivityEventKey(QuestActivityTargetType activity)
        {
            switch (activity)
            {
                case QuestActivityTargetType.RaceStarted: return "activity.race.started";
                case QuestActivityTargetType.RaceCompleted: return "activity.race.completed";
                case QuestActivityTargetType.RescueStarted: return "activity.rescue.started";
                case QuestActivityTargetType.RescueCompleted: return "activity.rescue.completed";
                default: return string.Empty;
            }
        }

        public static string GetTrickEventKey(QuestTrickTargetType trick)
        {
            switch (trick)
            {
                case QuestTrickTargetType.AnyLanding:
                case QuestTrickTargetType.TuckLanding:
                case QuestTrickTargetType.GrindLanding:
                case QuestTrickTargetType.StyledLanding:
                case QuestTrickTargetType.RuleLanding:
                    return "trick.landed";
                case QuestTrickTargetType.NamedLanding:
                    return "trick.named";
                default:
                    return string.Empty;
            }
        }

        public static string GetInteractionEventKey(QuestInteractionTargetType interaction)
        {
            switch (interaction)
            {
                case QuestInteractionTargetType.EnterResort: return "player.entered_resort";
                case QuestInteractionTargetType.ExitResort: return "player.exited_resort";
                case QuestInteractionTargetType.MountLift: return "lift.mounted";
                case QuestInteractionTargetType.DismountLift: return "lift.dismounted";
                case QuestInteractionTargetType.EquipSkis: return "player.skis.equipped";
                case QuestInteractionTargetType.UnequipSkis: return "player.skis.unequipped";
                case QuestInteractionTargetType.ClaimDefaultPass: return "player.default_pass.claimed";
                case QuestInteractionTargetType.Stacked: return "player.stacked";
                default: return string.Empty;
            }
        }

        public static string GetUiEventKey(QuestUiScreenTargetType uiTarget)
        {
            switch (uiTarget)
            {
                case QuestUiScreenTargetType.OverlayOpened: return "ui.overlay.opened";
                case QuestUiScreenTargetType.MapViewed: return "ui.overlay.map_viewed";
                case QuestUiScreenTargetType.StatsViewed: return "ui.overlay.stats_viewed";
                case QuestUiScreenTargetType.TasksViewed: return "ui.overlay.tasks_viewed";
                case QuestUiScreenTargetType.QuestsViewed: return "ui.overlay.quests_viewed";
                case QuestUiScreenTargetType.ShopOpened: return "ui.shop.opened";
                case QuestUiScreenTargetType.KioskOpened: return "ui.kiosk.opened";
                case QuestUiScreenTargetType.TaskRewardClaimed: return "ui.tasks.claimed";
                case QuestUiScreenTargetType.RaceKioskOpened: return "ui.race_kiosk.opened";
                default: return string.Empty;
            }
        }
    }

    [Serializable]
    public sealed class QuestConditionFilter
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class QuestObjectiveCondition
    {
        public QuestConditionKind kind = QuestConditionKind.EventCount;
        public string key;
        public QuestComparisonOp comparison = QuestComparisonOp.GreaterOrEqual;
        public int targetCount = 1;
        public float targetValue = 1f;
        public QuestValueKind stateValueKind = QuestValueKind.Bool;
        public bool boolValue = true;
        public string stringValue;
        public List<QuestConditionFilter> filters = new List<QuestConditionFilter>();
        public TrickRequirementDefinition trickRequirement = new TrickRequirementDefinition();
        public List<QuestObjectiveCondition> children = new List<QuestObjectiveCondition>();
    }

    [Serializable]
    public sealed class QuestObjectiveDefinition
    {
        public string id;
        public string title;

        [TextArea] public string description;

        public bool optional;
        public bool hiddenUntilAvailable;
        public bool autoPinWhenAvailable = true;
        public QuestObjectiveTemplate template = QuestObjectiveTemplate.UseInput;
        public int requiredCount = 1;
        public float targetValue = 1f;
        public QuestComparisonOp comparison = QuestComparisonOp.GreaterOrEqual;
        public QuestInputActionType inputAction;
        public QuestMovementSkillType movementSkill;
        public QuestDistanceStatType distanceStat;
        public QuestThresholdStatType thresholdStat;
        public QuestStateTargetType stateTarget;
        public QuestActivityTargetType activityTarget;
        public QuestTrickTargetType trickTarget;
        public QuestInteractionTargetType interactionTarget;
        public QuestUiScreenTargetType uiTarget;
        public string namedTrick;
        public TrickRequirementDefinition trickRequirement = new TrickRequirementDefinition();
        public QuestConditionKind compoundMode = QuestConditionKind.AllOf;
        public List<QuestObjectiveDefinition> compoundObjectives = new List<QuestObjectiveDefinition>();
        public QuestObjectiveCondition advancedCondition = new QuestObjectiveCondition();

        public QuestObjectiveCondition BuildCondition()
        {
            switch (template)
            {
                case QuestObjectiveTemplate.UseInput:
                    return BuildEventCondition(QuestSignalCatalog.GetInputEventKey(inputAction), Mathf.Max(1, requiredCount));

                case QuestObjectiveTemplate.PerformMovementSkill:
                    return BuildEventCondition(QuestSignalCatalog.GetMovementEventKey(movementSkill), Mathf.Max(1, requiredCount));

                case QuestObjectiveTemplate.TravelDistance:
                    return new QuestObjectiveCondition
                    {
                        kind = QuestConditionKind.StatAccumulate,
                        key = QuestSignalCatalog.GetDistanceStatKey(distanceStat),
                        comparison = comparison,
                        targetValue = Mathf.Max(0.01f, targetValue)
                    };

                case QuestObjectiveTemplate.ReachThreshold:
                    return new QuestObjectiveCondition
                    {
                        kind = QuestConditionKind.StatThreshold,
                        key = QuestSignalCatalog.GetThresholdStatKey(thresholdStat),
                        comparison = comparison,
                        targetValue = targetValue
                    };

                case QuestObjectiveTemplate.ReachState:
                    return new QuestObjectiveCondition
                    {
                        kind = QuestConditionKind.StateEquals,
                        key = QuestSignalCatalog.GetStateKey(stateTarget),
                        stateValueKind = QuestValueKind.Bool,
                        boolValue = true
                    };

                case QuestObjectiveTemplate.Activity:
                    return BuildEventCondition(QuestSignalCatalog.GetActivityEventKey(activityTarget), Mathf.Max(1, requiredCount));

                case QuestObjectiveTemplate.Trick:
                    var trickCondition = BuildEventCondition(QuestSignalCatalog.GetTrickEventKey(trickTarget), Mathf.Max(1, requiredCount));
                    if (trickTarget == QuestTrickTargetType.TuckLanding)
                        trickCondition.filters.Add(new QuestConditionFilter { key = "usedTuck", value = "true" });
                    else if (trickTarget == QuestTrickTargetType.GrindLanding)
                        trickCondition.filters.Add(new QuestConditionFilter { key = "usedGrind", value = "true" });
                    else if (trickTarget == QuestTrickTargetType.StyledLanding)
                        trickCondition.filters.Add(new QuestConditionFilter { key = "usedStyle", value = "true" });
                    else
                    {
                        var rule = CloneTrickRequirement(trickRequirement);
                        if (trickTarget == QuestTrickTargetType.NamedLanding && !string.IsNullOrWhiteSpace(namedTrick))
                            rule.explicitPoseLabel = namedTrick.Trim();

                        if (trickTarget == QuestTrickTargetType.RuleLanding || !rule.IsEmpty())
                        {
                            trickCondition.kind = QuestConditionKind.TrickRuleEventCount;
                            trickCondition.trickRequirement = rule;
                        }
                    }
                    return trickCondition;

                case QuestObjectiveTemplate.Interaction:
                    return BuildInteractionCondition(interactionTarget, Mathf.Max(1, requiredCount));

                case QuestObjectiveTemplate.OpenUiScreen:
                    return BuildEventCondition(QuestSignalCatalog.GetUiEventKey(uiTarget), Mathf.Max(1, requiredCount));

                case QuestObjectiveTemplate.Compound:
                    var compound = new QuestObjectiveCondition
                    {
                        kind = compoundMode == QuestConditionKind.AnyOf || compoundMode == QuestConditionKind.Sequence
                            ? compoundMode
                            : QuestConditionKind.AllOf,
                        children = new List<QuestObjectiveCondition>()
                    };

                    if (compoundObjectives != null)
                    {
                        for (int i = 0; i < compoundObjectives.Count; i++)
                        {
                            var child = compoundObjectives[i];
                            if (child == null)
                                continue;

                            compound.children.Add(child.BuildCondition());
                        }
                    }

                    return compound;

                case QuestObjectiveTemplate.Advanced:
                    return advancedCondition ?? new QuestObjectiveCondition();

                default:
                    return advancedCondition ?? new QuestObjectiveCondition();
            }
        }

        public string BuildAuthoringSummary()
        {
            switch (template)
            {
                case QuestObjectiveTemplate.UseInput:
                    return $"Use {inputAction} x{Mathf.Max(1, requiredCount)}";
                case QuestObjectiveTemplate.PerformMovementSkill:
                    return $"{movementSkill} x{Mathf.Max(1, requiredCount)}";
                case QuestObjectiveTemplate.TravelDistance:
                    return $"{distanceStat} {comparison} {targetValue:0.#}";
                case QuestObjectiveTemplate.ReachThreshold:
                    return $"{thresholdStat} {comparison} {targetValue:0.#}";
                case QuestObjectiveTemplate.ReachState:
                    return $"Reach state {stateTarget}";
                case QuestObjectiveTemplate.Activity:
                    return $"{activityTarget} x{Mathf.Max(1, requiredCount)}";
                case QuestObjectiveTemplate.Trick:
                    if (trickTarget == QuestTrickTargetType.NamedLanding && !string.IsNullOrWhiteSpace(namedTrick))
                        return $"Land pose \"{namedTrick}\"";

                    if ((trickTarget == QuestTrickTargetType.RuleLanding || (trickRequirement != null && !trickRequirement.IsEmpty())))
                        return $"{(trickRequirement != null ? trickRequirement.BuildSummary() : "Rule trick")} x{Mathf.Max(1, requiredCount)}";

                    return $"{trickTarget} x{Mathf.Max(1, requiredCount)}";
                case QuestObjectiveTemplate.Interaction:
                    return $"{interactionTarget} x{Mathf.Max(1, requiredCount)}";
                case QuestObjectiveTemplate.OpenUiScreen:
                    return $"Open {uiTarget} x{Mathf.Max(1, requiredCount)}";
                case QuestObjectiveTemplate.Compound:
                    return $"{compoundMode} ({(compoundObjectives != null ? compoundObjectives.Count : 0)} goals)";
                case QuestObjectiveTemplate.Advanced:
                    return "Advanced condition";
                default:
                    return string.Empty;
            }
        }

        public string BuildPromptLabel()
        {
            switch (template)
            {
                case QuestObjectiveTemplate.UseInput:
                    switch (inputAction)
                    {
                        case QuestInputActionType.LeanForward: return "PRESS W";
                        case QuestInputActionType.LeanBackward: return "PRESS S";
                        case QuestInputActionType.Tuck: return "PRESS LEFT SHIFT";
                        case QuestInputActionType.PolePush: return "PRESS LEFT CTRL ON FLATS";
                        case QuestInputActionType.PoleDrag: return "PRESS LEFT CTRL WHILE MOVING";
                        case QuestInputActionType.AirPose: return "SPACE, THEN LEFT CTRL";
                        case QuestInputActionType.MoveInput: return "MOVE";
                        default: return "USE INPUT";
                    }

                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (movementSkill)
                    {
                        case QuestMovementSkillType.Wedge: return "PRESS A + D";
                        case QuestMovementSkillType.SkateSwitch: return "W + A / D IN RHYTHM";
                        case QuestMovementSkillType.QuickStop: return "PRESS S + SHARP A OR D";
                        case QuestMovementSkillType.JumpAdjust: return "SPACE, THEN W/S + A/D";
                        default: return "USE SKILL";
                    }

                case QuestObjectiveTemplate.TravelDistance:
                    return "KEEP MOVING";

                case QuestObjectiveTemplate.ReachThreshold:
                    switch (thresholdStat)
                    {
                        case QuestThresholdStatType.Speed: return "BUILD SPEED";
                        case QuestThresholdStatType.Soreness: return comparison == QuestComparisonOp.LessOrEqual ? "RECOVER" : "TAKE A SPILL";
                        case QuestThresholdStatType.AirTime: return "STAY AIRBORNE";
                        default: return "REACH TARGET";
                    }

                case QuestObjectiveTemplate.ReachState:
                    switch (stateTarget)
                    {
                        case QuestStateTargetType.InResort: return "GO TO THE RESORT";
                        case QuestStateTargetType.OnLift: return "BOARD THE LIFT";
                        case QuestStateTargetType.SkisEquipped: return "HOLD Q";
                        case QuestStateTargetType.Airborne: return "GET AIRBORNE";
                        case QuestStateTargetType.Grounded: return "GET BACK ON SNOW";
                        case QuestStateTargetType.HasDefaultPass: return "CLAIM YOUR PASS";
                        case QuestStateTargetType.Stacked: return "YOU STACKED";
                        default: return "REACH STATE";
                    }

                case QuestObjectiveTemplate.Activity:
                    switch (activityTarget)
                    {
                        case QuestActivityTargetType.RaceStarted: return "START RACE";
                        case QuestActivityTargetType.RaceCompleted: return "FINISH RACE";
                        case QuestActivityTargetType.RescueStarted: return "START RESCUE";
                        case QuestActivityTargetType.RescueCompleted: return "COMPLETE RESCUE";
                        default: return "COMPLETE ACTIVITY";
                    }

                case QuestObjectiveTemplate.Trick:
                    return "LAND A TRICK";

                case QuestObjectiveTemplate.Interaction:
                    switch (interactionTarget)
                    {
                        case QuestInteractionTargetType.EnterResort: return "GO TO RESORT, PRESS E";
                        case QuestInteractionTargetType.ExitResort: return "LEAVE THE RESORT";
                        case QuestInteractionTargetType.MountLift: return "PRESS E";
                        case QuestInteractionTargetType.DismountLift: return "PRESS E";
                        case QuestInteractionTargetType.EquipSkis: return "HOLD Q";
                        case QuestInteractionTargetType.UnequipSkis: return "HOLD Q";
                        case QuestInteractionTargetType.ClaimDefaultPass: return "CLAIM PASS";
                        case QuestInteractionTargetType.Stacked: return "TAKE A SPILL";
                        default: return "INTERACT";
                    }

                case QuestObjectiveTemplate.OpenUiScreen:
                    switch (uiTarget)
                    {
                        case QuestUiScreenTargetType.OverlayOpened: return "OPEN OVERLAY";
                        case QuestUiScreenTargetType.MapViewed: return "OPEN MAP";
                        case QuestUiScreenTargetType.StatsViewed: return "OPEN STATS";
                        case QuestUiScreenTargetType.TasksViewed: return "OPEN GOALS > TASKS";
                        case QuestUiScreenTargetType.QuestsViewed: return "OPEN GOALS > QUESTS";
                        case QuestUiScreenTargetType.ShopOpened: return "GO TO SHOP, PRESS E";
                        case QuestUiScreenTargetType.KioskOpened: return "GO TO SKI PASS KIOSK, PRESS E";
                        case QuestUiScreenTargetType.TaskRewardClaimed: return "GOALS > TASKS > CLAIM";
                        case QuestUiScreenTargetType.RaceKioskOpened: return "GO TO RACE KIOSK, PRESS E";
                        default: return "OPEN UI";
                    }

                case QuestObjectiveTemplate.Compound:
                    return BuildCompoundPromptLabel();

                case QuestObjectiveTemplate.Advanced:
                    return "FOLLOW OBJECTIVE";

                default:
                    return string.Empty;
            }
        }

        private string BuildCompoundPromptLabel()
        {
            if (compoundObjectives == null || compoundObjectives.Count == 0)
                return compoundMode == QuestConditionKind.AnyOf ? "COMPLETE ANY" : "COMPLETE ALL";

            string first = string.Empty;
            string second = string.Empty;

            for (int i = 0; i < compoundObjectives.Count; i++)
            {
                var child = compoundObjectives[i];
                if (child == null)
                    continue;

                string childPrompt = child.BuildPromptLabel();
                if (string.IsNullOrWhiteSpace(childPrompt))
                    continue;

                if (string.IsNullOrWhiteSpace(first))
                {
                    first = childPrompt;
                    continue;
                }

                second = childPrompt;
                break;
            }

            if (string.IsNullOrWhiteSpace(first))
                return compoundMode == QuestConditionKind.AnyOf ? "COMPLETE ANY" : "COMPLETE ALL";

            if (string.IsNullOrWhiteSpace(second))
                return first;

            return JoinPromptLabels(first, second);
        }

        private static string JoinPromptLabels(string first, string second)
        {
            first = first?.Trim() ?? string.Empty;
            second = second?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(first))
                return second;

            if (string.IsNullOrWhiteSpace(second))
                return first;

            var secondFragments = SplitPromptFragments(second);
            if (secondFragments.Count > 0)
            {
                string trailingFirst = GetTrailingPromptFragment(first);
                if (!string.IsNullOrWhiteSpace(trailingFirst) &&
                    string.Equals(trailingFirst, secondFragments[0], StringComparison.OrdinalIgnoreCase))
                {
                    secondFragments.RemoveAt(0);
                    if (secondFragments.Count == 0)
                        return first;

                    second = string.Join(", THEN ", secondFragments);
                }
            }

            return $"{first}, THEN {second}";
        }

        private static string GetTrailingPromptFragment(string prompt)
        {
            var fragments = SplitPromptFragments(prompt);
            return fragments.Count > 0 ? fragments[fragments.Count - 1] : string.Empty;
        }

        private static List<string> SplitPromptFragments(string prompt)
        {
            var fragments = new List<string>();
            if (string.IsNullOrWhiteSpace(prompt))
                return fragments;

            string normalized = prompt.Replace(", THEN ", ",");
            string[] parts = normalized.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i]?.Trim() ?? string.Empty;
                if (part.StartsWith("THEN ", StringComparison.OrdinalIgnoreCase))
                    part = part.Substring(5).Trim();

                if (!string.IsNullOrWhiteSpace(part))
                    fragments.Add(part);
            }

            return fragments;
        }

        private static QuestObjectiveCondition BuildInteractionCondition(QuestInteractionTargetType interaction, int count)
        {
            switch (interaction)
            {
                case QuestInteractionTargetType.EnterResort:
                    return BuildAnyOfCondition(
                        BuildEventCondition(QuestSignalCatalog.GetInteractionEventKey(interaction), count),
                        BuildBoolStateCondition(QuestSignalCatalog.GetStateKey(QuestStateTargetType.InResort), true));

                case QuestInteractionTargetType.EquipSkis:
                    return BuildAnyOfCondition(
                        BuildEventCondition(QuestSignalCatalog.GetInteractionEventKey(interaction), count),
                        BuildBoolStateCondition(QuestSignalCatalog.GetStateKey(QuestStateTargetType.SkisEquipped), true));

                case QuestInteractionTargetType.ClaimDefaultPass:
                    return BuildAnyOfCondition(
                        BuildEventCondition(QuestSignalCatalog.GetInteractionEventKey(interaction), count),
                        BuildBoolStateCondition(QuestSignalCatalog.GetStateKey(QuestStateTargetType.HasDefaultPass), true));

                case QuestInteractionTargetType.Stacked:
                    return BuildAnyOfCondition(
                        BuildEventCondition(QuestSignalCatalog.GetInteractionEventKey(interaction), count),
                        BuildBoolStateCondition(QuestSignalCatalog.GetStateKey(QuestStateTargetType.Stacked), true),
                        BuildBoolStateCondition("player.ever_stacked", true));

                default:
                    return BuildEventCondition(QuestSignalCatalog.GetInteractionEventKey(interaction), count);
            }
        }

        private static QuestObjectiveCondition BuildAnyOfCondition(params QuestObjectiveCondition[] children)
        {
            var condition = new QuestObjectiveCondition
            {
                kind = QuestConditionKind.AnyOf,
                children = new List<QuestObjectiveCondition>()
            };

            if (children == null)
                return condition;

            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child != null)
                    condition.children.Add(child);
            }

            return condition;
        }

        private static QuestObjectiveCondition BuildBoolStateCondition(string key, bool value)
        {
            return new QuestObjectiveCondition
            {
                kind = QuestConditionKind.StateEquals,
                key = key,
                stateValueKind = QuestValueKind.Bool,
                boolValue = value
            };
        }

        private static QuestObjectiveCondition BuildEventCondition(string key, int count)
        {
            return new QuestObjectiveCondition
            {
                kind = QuestConditionKind.EventCount,
                key = key,
                targetCount = Mathf.Max(1, count),
                filters = new List<QuestConditionFilter>()
            };
        }

        private static TrickRequirementDefinition CloneTrickRequirement(TrickRequirementDefinition source)
        {
            source ??= new TrickRequirementDefinition();

            return new TrickRequirementDefinition
            {
                flavor = source.flavor,
                minimumSpinDegrees = source.minimumSpinDegrees,
                requiredSpinDirection = source.requiredSpinDirection,
                minimumFlipCount = source.minimumFlipCount,
                requiredFlipDirection = source.requiredFlipDirection,
                requiresGrind = source.requiresGrind,
                requiresSlide = source.requiresSlide,
                requiresValidAuthoredPose = source.requiresValidAuthoredPose,
                requiresPoseRotationCombo = source.requiresPoseRotationCombo,
                requiredPoseFamily = source.requiredPoseFamily,
                requiredPoseShape = source.requiredPoseShape,
                requiredOrientationModifier = source.requiredOrientationModifier,
                explicitPoseLabel = source.explicitPoseLabel,
                requiresSwitchLanding = source.requiresSwitchLanding,
                requiresNoseLanding = source.requiresNoseLanding,
                requiresTailLanding = source.requiresTailLanding,
                requiredZoneId = source.requiredZoneId
            };
        }
    }

    [Serializable]
    public sealed class QuestStageDefinition
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>();
    }

    [CreateAssetMenu(menuName = "SkiGame/Progression/Quest Definition", fileName = "Quest_")]
    public sealed class QuestDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string title;

        [TextArea] public string description;

        [Header("Flow")]
        public bool autoAccept;
        public bool tutorialQuest;
        public List<QuestStageDefinition> stages = new List<QuestStageDefinition>();

        public string SafeId => string.IsNullOrWhiteSpace(id) ? name : id.Trim();

        public QuestStageDefinition GetStage(int index)
        {
            if (stages == null || index < 0 || index >= stages.Count)
                return null;

            return stages[index];
        }
    }
}
