using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NpcConversationPanel : MonoBehaviour
{
    public sealed class Request
    {
        public string speakerName;
        public string bodyText;
        public string questTitle;
        public string acceptLabel;
        public string declineLabel;
        public string trackLabel;
        public string closeLabel;
        public Action onAccept;
        public Action onDecline;
        public Action onTrack;
        public Action onClose;
    }

    private static NpcConversationPanel _instance;

    [SerializeField] private bool dontDestroyOnLoad = true;

    private Canvas _canvas;
    private GameObject _backdrop;
    private RectTransform _panelRect;
    private TextMeshProUGUI _speakerLabel;
    private TextMeshProUGUI _bodyLabel;
    private TextMeshProUGUI _questLabel;
    private Button _acceptButton;
    private Button _declineButton;
    private Button _trackButton;
    private Button _closeButton;
    private TextMeshProUGUI _acceptButtonText;
    private TextMeshProUGUI _declineButtonText;
    private TextMeshProUGUI _trackButtonText;
    private TextMeshProUGUI _closeButtonText;

    private Request _activeRequest;

    public static NpcConversationPanel Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<NpcConversationPanel>() ?? CreateRuntimeInstance();

            return _instance;
        }
    }

    public bool IsOpen => _canvas != null && _canvas.enabled;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        EnsureVisuals();
        Hide();
    }

    public void Show(Request request)
    {
        if (request == null)
            return;

        EnsureVisuals();
        EnsureEventSystem();

        _activeRequest = request;
        _speakerLabel.text = string.IsNullOrWhiteSpace(request.speakerName) ? "NPC" : request.speakerName.Trim();
        _bodyLabel.text = request.bodyText ?? string.Empty;
        bool hasQuestTitle = !string.IsNullOrWhiteSpace(request.questTitle);
        _questLabel.gameObject.SetActive(hasQuestTitle);
        _questLabel.text = hasQuestTitle ? request.questTitle.Trim() : string.Empty;

        ConfigureButton(_acceptButton, _acceptButtonText, request.acceptLabel, HandleAccept);
        ConfigureButton(_declineButton, _declineButtonText, request.declineLabel, HandleDecline);
        ConfigureButton(_trackButton, _trackButtonText, request.trackLabel, HandleTrack);
        ConfigureButton(_closeButton, _closeButtonText, request.closeLabel, HandleClose);

        _canvas.enabled = true;
    }

    public void Hide()
    {
        if (_canvas != null)
            _canvas.enabled = false;

        _activeRequest = null;
    }

    private void HandleAccept()
    {
        var request = _activeRequest;
        request?.onAccept?.Invoke();
        Hide();
    }

    private void HandleDecline()
    {
        var request = _activeRequest;
        request?.onDecline?.Invoke();
        Hide();
    }

    private void HandleTrack()
    {
        _activeRequest?.onTrack?.Invoke();
    }

    private void HandleClose()
    {
        var request = _activeRequest;
        request?.onClose?.Invoke();
        Hide();
    }

    private void EnsureVisuals()
    {
        if (_canvas != null)
            return;

        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null)
            _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (gameObject.GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        _backdrop = CreateUiObject("Backdrop", transform, out var backdropRect);
        var backdropImage = _backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0.35f);
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        var panel = CreateUiObject("Panel", _backdrop.transform, out _panelRect);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);
        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _panelRect.sizeDelta = new Vector2(700f, 380f);

        _speakerLabel = CreateText("Speaker", panel.transform, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        StretchRect(_speakerLabel.rectTransform, 28f, 20f, -28f, -300f);

        _questLabel = CreateText("QuestTitle", panel.transform, 22f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        _questLabel.color = new Color(0.97f, 0.86f, 0.51f, 1f);
        StretchRect(_questLabel.rectTransform, 28f, 74f, -28f, -248f);

        _bodyLabel = CreateText("Body", panel.transform, 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _bodyLabel.enableWordWrapping = true;
        StretchRect(_bodyLabel.rectTransform, 28f, 122f, -28f, -92f);

        _acceptButton = CreateButton("AcceptButton", panel.transform, new Vector2(110f, 46f), new Vector2(-268f, 28f), out _acceptButtonText);
        _declineButton = CreateButton("DeclineButton", panel.transform, new Vector2(110f, 46f), new Vector2(-146f, 28f), out _declineButtonText);
        _trackButton = CreateButton("TrackButton", panel.transform, new Vector2(110f, 46f), new Vector2(-24f, 28f), out _trackButtonText);
        _closeButton = CreateButton("CloseButton", panel.transform, new Vector2(110f, 46f), new Vector2(98f, 28f), out _closeButtonText);
    }

    private static void ConfigureButton(Button button, TextMeshProUGUI label, string text, UnityEngine.Events.UnityAction onClick)
    {
        if (button == null || label == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        bool visible = !string.IsNullOrWhiteSpace(text);
        button.gameObject.SetActive(visible);
        if (visible)
            label.text = text.Trim();
    }

    private static GameObject CreateUiObject(string name, Transform parent, out RectTransform rectTransform)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        rectTransform = go.GetComponent<RectTransform>();
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, out TextMeshProUGUI label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.36f, 0.58f, 1f);

        var button = go.GetComponent<Button>();
        label = CreateText("Label", go.transform, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchRect(label.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private static void StretchRect(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, -top);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private static NpcConversationPanel CreateRuntimeInstance()
    {
        var go = new GameObject(nameof(NpcConversationPanel));
        return go.AddComponent<NpcConversationPanel>();
    }
}
