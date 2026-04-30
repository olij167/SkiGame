using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcGenericInteractionProfile", menuName = "SkiGame/NPC/Generic Interaction Profile")]
public sealed class NpcGenericInteractionProfileSO : ScriptableObject
{
    [SerializeField] private string archetypeId;
    [SerializeField] private string fallbackDisplayName = "Skier";
    [SerializeField] private NpcDialogueBankSO dialogueBank;
    [SerializeField] private List<string> allowedTopics = new();
    [SerializeField, Range(0f, 1f)] private float talkPromptChance = 1f;
    [SerializeField, Range(0f, 1f)] private float tipChance = 0.35f;
    [SerializeField] private bool allowInteractionPrompt = true;
    [SerializeField] private bool allowAmbientAutoDialogue = false;
    [SerializeField, Min(0f)] private float interactionCooldown = 2f;
    [SerializeField] private string promptVerbOverride = "Talk to";

    public string ArchetypeId => archetypeId;
    public string FallbackDisplayName => string.IsNullOrWhiteSpace(fallbackDisplayName) ? "Skier" : fallbackDisplayName.Trim();
    public NpcDialogueBankSO DialogueBank => dialogueBank;
    public IReadOnlyList<string> AllowedTopics => allowedTopics;
    public float TalkPromptChance => talkPromptChance;
    public float TipChance => tipChance;
    public bool AllowInteractionPrompt => allowInteractionPrompt;
    public bool AllowAmbientAutoDialogue => allowAmbientAutoDialogue;
    public float InteractionCooldown => interactionCooldown;
    public string PromptVerbOverride => promptVerbOverride;
}
