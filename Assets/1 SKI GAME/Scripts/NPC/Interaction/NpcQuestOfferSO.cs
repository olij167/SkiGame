using System;
using System.Collections.Generic;
using SkiGame.Progression;
using UnityEngine;

public enum NpcQuestOfferOrderMode
{
    InspectorOrder,
    AvailableFirst,
    PriorityThenInspector
}

public enum NpcQuestActiveBlockingMode
{
    None,
    BlockIfAnyOfferedQuestActive,
    BlockIfAnyQuestFromThisGiverActive,
    BlockIfAnyQuestInSameQuestLineActive
}

[CreateAssetMenu(fileName = "NpcQuestOffer", menuName = "SkiGame/NPC/Quest Offer")]
public sealed class NpcQuestOfferSO : ScriptableObject
{
    [Header("Quest")]
    [SerializeField] private QuestDefinitionSO questDefinition;
    [SerializeField] private string offerTitle;
    [SerializeField] private string questLineId;
    [SerializeField] private int offerPriority;

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 5)] private string offerDialogue;
    [SerializeField, TextArea(2, 5)] private string acceptedDialogue;
    [SerializeField, TextArea(2, 5)] private string inProgressDialogue;
    [SerializeField, TextArea(2, 5)] private string completedDialogue;
    [SerializeField, TextArea(2, 5)] private string turnInDialogue;
    [SerializeField, TextArea(2, 5)] private string replayDialogue;

    [Header("Dialogue Sequences")]
    [SerializeField] private NpcDialogueSequenceSO offerSequence;
    [SerializeField] private NpcDialogueSequenceSO acceptedSequence;
    [SerializeField] private NpcDialogueSequenceSO inProgressSequence;
    [SerializeField] private NpcDialogueSequenceSO readyToTurnInSequence;
    [SerializeField] private NpcDialogueSequenceSO completedSequence;
    [SerializeField] private NpcDialogueSequenceSO replaySequence;
    [SerializeField] private NpcDialogueSequenceSO failedSequence;
    [SerializeField] private QuestDialogueProfileSO dialogueProfile;

    [Header("Ambient Dialogue Topics")]
    [SerializeField] private NpcDialogueBankSO ambientDialogueBankOverride;
    [SerializeField] private string availableAmbientTopic;
    [SerializeField] private string inProgressAmbientTopic;
    [SerializeField] private string readyToTurnInAmbientTopic;
    [SerializeField] private string completedAmbientTopic;
    [SerializeField] private string replayAmbientTopic;
    [SerializeField] private string blockedAmbientTopic;
    [SerializeField, Min(0f)] private float ambientWeight = 1f;
    [SerializeField, Min(0f)] private float ambientCooldown = 8f;

    [Header("Availability")]
    [SerializeField] private List<QuestDefinitionSO> requiredCompletedQuests = new();
    [SerializeField] private List<QuestDefinitionSO> blockedByCompletedQuests = new();
    [SerializeField] private bool showWhenAvailable = true;
    [SerializeField] private bool showWhenInProgress = false;
    [SerializeField] private bool showWhenReadyToTurnIn = true;
    [SerializeField] private bool showWhenCompleted = false;
    [SerializeField] private bool replayable = false;
    [SerializeField] private bool hideIfQuestAccepted = true;
    [SerializeField] private bool hideIfQuestCompleted = true;

    [Header("Guidance")]
    [SerializeField] private bool autoTrackOnAccept = true;
    [SerializeField] private bool showNavigationTarget = true;

    public QuestDefinitionSO QuestDefinition => questDefinition;
    public string OfferTitle => string.IsNullOrWhiteSpace(offerTitle) ? questDefinition != null ? questDefinition.title : name : offerTitle.Trim();
    public string QuestLineId => string.IsNullOrWhiteSpace(questLineId) ? questDefinition != null ? questDefinition.SafeId : string.Empty : questLineId.Trim();
    public int OfferPriority => offerPriority;
    public string OfferDialogue => offerDialogue;
    public string AcceptedDialogue => acceptedDialogue;
    public string InProgressDialogue => inProgressDialogue;
    public string CompletedDialogue => completedDialogue;
    public string TurnInDialogue => turnInDialogue;
    public string ReplayDialogue => replayDialogue;
    public NpcDialogueSequenceSO OfferSequence => offerSequence;
    public NpcDialogueSequenceSO AcceptedSequence => acceptedSequence;
    public NpcDialogueSequenceSO InProgressSequence => inProgressSequence;
    public NpcDialogueSequenceSO ReadyToTurnInSequence => readyToTurnInSequence;
    public NpcDialogueSequenceSO CompletedSequence => completedSequence;
    public NpcDialogueSequenceSO ReplaySequence => replaySequence;
    public NpcDialogueSequenceSO FailedSequence => failedSequence;
    public QuestDialogueProfileSO DialogueProfile => dialogueProfile;
    public NpcDialogueBankSO AmbientDialogueBankOverride => ambientDialogueBankOverride;
    public string AvailableAmbientTopic => availableAmbientTopic;
    public string InProgressAmbientTopic => inProgressAmbientTopic;
    public string ReadyToTurnInAmbientTopic => readyToTurnInAmbientTopic;
    public string CompletedAmbientTopic => completedAmbientTopic;
    public string ReplayAmbientTopic => replayAmbientTopic;
    public string BlockedAmbientTopic => blockedAmbientTopic;
    public float AmbientWeight => ambientWeight;
    public float AmbientCooldown => ambientCooldown;
    public IReadOnlyList<QuestDefinitionSO> RequiredCompletedQuests => requiredCompletedQuests;
    public IReadOnlyList<QuestDefinitionSO> BlockedByCompletedQuests => blockedByCompletedQuests;
    public bool ShowWhenAvailable => showWhenAvailable;
    public bool ShowWhenInProgress => showWhenInProgress;
    public bool ShowWhenReadyToTurnIn => showWhenReadyToTurnIn;
    public bool ShowWhenCompleted => showWhenCompleted;
    public bool Replayable => replayable;
    public bool HideIfQuestAccepted => hideIfQuestAccepted;
    public bool HideIfQuestCompleted => hideIfQuestCompleted;
    public bool AutoTrackOnAccept => autoTrackOnAccept;
    public bool ShowNavigationTarget => showNavigationTarget;
}
