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

        dialogueAgent.Presenter.ShowQuestOffer(new NpcDialogueBubblePresenter.NpcQuestOfferBubbleViewData
        {
            npcName = data.npcName,
            questTitle = data.questTitle,
            bodyText = data.bodyText,
            indexText = data.indexText,
            confirmText = data.holdText,
            confirmBindingText = data.holdText,
            stateText = string.Empty,
            canCycle = !string.IsNullOrWhiteSpace(data.cycleText)
        });
    }

    public void Hide()
    {
        dialogueAgent?.Presenter.HideQuestOffer(immediate: true);
    }
}
