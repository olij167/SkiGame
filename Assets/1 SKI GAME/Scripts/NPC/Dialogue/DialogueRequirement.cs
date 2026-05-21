using System;
using SkiGame.Progression;
using UnityEngine;

public enum DialogueRequirementType
{
    AlwaysTrue,
    QuestActive,
    QuestCompleted,
    QuestStage,
    PlayerSorenessGreaterThan,
    AnchorType,
    AnchorTag,
    Region,
    NearbyRunDifficulty,
    Weather,
    TimeOfDay,
    PlayerHasPass,
    ReputationAtLeast
}

public enum DialogueRequirementComparison
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual
}

[Serializable]
public sealed class DialogueRequirement
{
    public DialogueRequirementType requirementType = DialogueRequirementType.AlwaysTrue;
    public string key;
    public DialogueRequirementComparison comparison = DialogueRequirementComparison.Equals;
    public string value;
}

public static class DialogueRequirementEvaluator
{
    public static bool AreMet(DialogueRequirement[] requirements, DialogueContext context)
    {
        if (requirements == null || requirements.Length == 0)
            return true;

        for (int i = 0; i < requirements.Length; i++)
        {
            if (!IsMet(requirements[i], context))
                return false;
        }

        return true;
    }

    public static bool IsMet(DialogueRequirement requirement, DialogueContext context)
    {
        if (requirement == null || requirement.requirementType == DialogueRequirementType.AlwaysTrue)
            return true;

        var director = QuestDirector.Instance;
        string questId = FirstNonEmpty(requirement.key, context.questId, context.questDefinition != null ? context.questDefinition.SafeId : null);

        switch (requirement.requirementType)
        {
            case DialogueRequirementType.QuestActive:
                return director != null && !string.IsNullOrWhiteSpace(questId) && director.IsQuestAccepted(questId) && !director.IsQuestCompleted(questId);

            case DialogueRequirementType.QuestCompleted:
                return director != null && !string.IsNullOrWhiteSpace(questId) && director.IsQuestCompleted(questId);

            case DialogueRequirementType.QuestStage:
            {
                var state = context.questState ?? (director != null ? director.GetQuestState(questId) : null);
                if (state == null)
                    return false;

                if (int.TryParse(requirement.value, out int expectedIndex))
                    return Compare(state.currentStageIndex, expectedIndex, requirement.comparison);

                var definition = context.questDefinition ?? (director != null ? director.GetQuestDefinition(questId) : null);
                var stage = definition != null ? definition.GetStage(state.currentStageIndex) : null;
                return stage != null && string.Equals(stage.id, requirement.value, StringComparison.OrdinalIgnoreCase);
            }

            case DialogueRequirementType.PlayerSorenessGreaterThan:
            {
                var provider = UnityEngine.Object.FindObjectOfType<QuestContextProvider>();
                float soreness = provider != null ? provider.ReadFloat("player.soreness") : 0f;
                float threshold = float.TryParse(requirement.value, out float parsed) ? parsed : 0f;
                return soreness > threshold;
            }

            case DialogueRequirementType.AnchorType:
                return CompareString(context.anchorType, requirement.value, requirement.comparison);

            case DialogueRequirementType.AnchorTag:
                return ContextValueContains(context, "anchorTags", requirement.value);

            case DialogueRequirementType.Region:
                return CompareString(context.regionName, requirement.value, requirement.comparison);

            case DialogueRequirementType.NearbyRunDifficulty:
                return CompareString(context.nearbyRunDifficulty, requirement.value, requirement.comparison);

            case DialogueRequirementType.Weather:
                return CompareString(context.weather, requirement.value, requirement.comparison);

            case DialogueRequirementType.TimeOfDay:
                return CompareString(context.timeOfDay, requirement.value, requirement.comparison);

            case DialogueRequirementType.PlayerHasPass:
            {
                var passManager = SkiPassManager.Instance;
                if (passManager == null)
                    return false;

                string passId = FirstNonEmpty(requirement.key, requirement.value, context.requiredPassId, context.currentPassId);
                bool hasPass = string.IsNullOrWhiteSpace(passId)
                    ? passManager.CurrentLevel >= 0 || passManager.HasTimedPass
                    : passManager.HasAccessToPass(passId);
                return requirement.comparison == DialogueRequirementComparison.NotEquals ? !hasPass : hasPass;
            }

            case DialogueRequirementType.ReputationAtLeast:
                return true;

            default:
                return true;
        }
    }

    private static bool Compare(int actual, int expected, DialogueRequirementComparison comparison)
    {
        return comparison switch
        {
            DialogueRequirementComparison.NotEquals => actual != expected,
            DialogueRequirementComparison.GreaterThan => actual > expected,
            DialogueRequirementComparison.GreaterOrEqual => actual >= expected,
            DialogueRequirementComparison.LessThan => actual < expected,
            DialogueRequirementComparison.LessOrEqual => actual <= expected,
            _ => actual == expected
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private static bool CompareString(string actual, string expected, DialogueRequirementComparison comparison)
    {
        bool equal = string.Equals(actual?.Trim(), expected?.Trim(), StringComparison.OrdinalIgnoreCase);
        return comparison == DialogueRequirementComparison.NotEquals ? !equal : equal;
    }

    private static bool ContextValueContains(DialogueContext context, string key, string expected)
    {
        if (context.values == null || string.IsNullOrWhiteSpace(expected))
            return false;

        if (!context.values.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
            return false;

        string[] parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i].Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
