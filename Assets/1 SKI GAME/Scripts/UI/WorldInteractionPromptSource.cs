using UnityEngine;

namespace SkiGame.UI
{
    public interface IWorldInteractionPromptSource
    {
        bool IsPromptAvailable { get; }
        string PromptActionText { get; }
        string PromptDescriptionText { get; }
        bool PromptUsesHold { get; }
        float PromptHoldDuration { get; }
        Vector3 PromptWorldPosition { get; }
        int PromptPriority { get; }
    }
}