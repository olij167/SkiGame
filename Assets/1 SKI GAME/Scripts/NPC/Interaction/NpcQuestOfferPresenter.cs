using System;
using UnityEngine;

[Obsolete("NpcQuestOfferPresenter is retained only as a compatibility wrapper. Use NpcDialogueBubblePresenter quest-offer APIs instead.")]
[DisallowMultipleComponent]
public sealed class NpcQuestOfferPresenter : MonoBehaviour
{
    private bool _loggedObsoleteWarning;

    [Serializable]
    public sealed class ViewData
    {
        public string npcName;
        public string questTitle;
        public string bodyText;
        public string indexText;
        public string holdText;
        public string cycleText;
    }

    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private Transform anchor;

    public bool IsVisible => dialogueAgent != null && dialogueAgent.Presenter.IsShowingQuestOffer;

    private void Awake()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();

        if (dialogueAgent != null && anchor != null)
            dialogueAgent.Presenter.SetAnchor(anchor);

        if (!_loggedObsoleteWarning)
        {
            Debug.LogWarning("NpcQuestOfferPresenter is obsolete; remove it from this prefab. Quest offers now render through NpcDialogueBubblePresenter.", this);
            _loggedObsoleteWarning = true;
        }
    }

    public void SetAnchor(Transform value)
    {
        anchor = value;
        if (dialogueAgent != null && anchor != null)
            dialogueAgent.Presenter.SetAnchor(anchor);
    }

    public void Show(ViewData data)
    {
        if (dialogueAgent == null || data == null)
            return;

        dialogueAgent.Presenter.ShowContent(new NpcDialogueBubbleContent
        {
            kind = NpcDialogueBubbleContentKind.Card,
            lifetimeMode = NpcDialogueBubbleLifetimeMode.PersistentUntilHidden,
            speakerName = data.npcName,
            titleText = data.questTitle,
            bodyText = data.bodyText,
            metaText = data.indexText,
            controlsText = string.IsNullOrWhiteSpace(data.cycleText)
                ? data.holdText
                : $"{data.holdText}    {data.cycleText}",
            showSpeaker = !string.IsNullOrWhiteSpace(data.npcName),
            importance = NpcDialogueImportance.Quest,
            priority = 1000
        }, forceInterrupt: true);
    }

    public void Hide()
    {
        dialogueAgent?.Presenter.HideContent(immediate: true);
    }
}
