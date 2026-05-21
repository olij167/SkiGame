using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NpcQuestIndicatorPresenter : MonoBehaviour
{
    [SerializeField] private NpcQuestGiver questGiver;
    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcQuestIndicatorStyleSO style;
    [SerializeField] private Transform indicatorAnchor;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _rootRect;
    private Image _iconImage;

    private void Awake()
    {
        if (questGiver == null)
            questGiver = GetComponent<NpcQuestGiver>();
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();

        EnsureVisuals();
    }

    private void LateUpdate()
    {
        if (questGiver == null || style == null)
            return;

        EnsureVisuals();
        UpdateIndicator();
    }

    private void EnsureVisuals()
    {
        if (_canvas != null)
            return;

        var root = new GameObject("QuestIndicator", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        _rootRect = root.GetComponent<RectTransform>();
        _canvas = root.GetComponent<Canvas>();
        _canvasGroup = root.GetComponent<CanvasGroup>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 5;
        _canvasGroup.alpha = 0f;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        _iconImage = icon.GetComponent<Image>();
        _iconImage.preserveAspect = true;
        var iconRect = _iconImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(48f, 48f);
    }

    private void UpdateIndicator()
    {
        var indicatorState = questGiver.GetQuestIndicatorState();
        bool shouldShow = TryResolveVisuals(indicatorState, out var icon, out var color);
        shouldShow &= !ShouldHideForDialogue();

        Transform anchor = indicatorAnchor != null ? indicatorAnchor : transform;
        var cam = Camera.main != null ? Camera.main : Camera.current;
        if (cam == null)
            return;

        Vector3 offset = style.worldOffset;
        if (style.bobAnimation)
            offset.y += Mathf.Sin(Time.unscaledTime * style.bobSpeed) * style.bobAmplitude;

        Vector3 worldPosition = anchor.position + offset;
        _rootRect.position = worldPosition;

        Vector3 toCamera = cam.transform.position - worldPosition;
        if (toCamera.sqrMagnitude > 0.0001f)
            _rootRect.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);

        float distance = toCamera.magnitude;
        if (distance > style.visibleDistance)
            shouldShow = false;

        _iconImage.sprite = icon;
        _iconImage.color = color;
        float scale = Mathf.Lerp(style.minScale, style.maxScale, Mathf.InverseLerp(style.visibleDistance, 2f, distance));
        _rootRect.localScale = Vector3.one * scale;
        _canvas.enabled = shouldShow;

        float targetAlpha = shouldShow ? 1f : 0f;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * style.fadeSpeed);
    }

    private bool TryResolveVisuals(NpcQuestGiver.NpcQuestIndicatorState state, out Sprite icon, out Color color)
    {
        icon = null;
        color = Color.white;

        switch (state)
        {
            case NpcQuestGiver.NpcQuestIndicatorState.Available:
                icon = style.availableIcon;
                color = style.availableColor;
                return icon != null;
            case NpcQuestGiver.NpcQuestIndicatorState.InProgress:
                if (!style.showInProgressIndicator)
                    return false;
                icon = style.inProgressIcon;
                color = style.inProgressColor;
                return icon != null;
            case NpcQuestGiver.NpcQuestIndicatorState.ReadyToTurnIn:
                icon = style.readyToTurnInIcon;
                color = style.readyToTurnInColor;
                return icon != null;
            case NpcQuestGiver.NpcQuestIndicatorState.CompletedReplayable:
                icon = style.replayableIcon;
                color = style.replayableColor;
                return icon != null;
            case NpcQuestGiver.NpcQuestIndicatorState.Blocked:
                if (!style.showBlockedIndicator)
                    return false;
                icon = style.blockedIcon;
                color = style.blockedColor;
                return icon != null;
            default:
                return false;
        }
    }

    private bool ShouldHideForDialogue()
    {
        if (dialogueAgent == null || dialogueAgent.Presenter == null)
            return false;

        if (style.hideWhenQuestOfferVisible && dialogueAgent.Presenter.IsShowingPersistentContent)
            return true;

        if (style.hideWhenDialogueVisible && dialogueAgent.Presenter.IsShowing)
            return true;

        return false;
    }
}
