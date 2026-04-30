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
