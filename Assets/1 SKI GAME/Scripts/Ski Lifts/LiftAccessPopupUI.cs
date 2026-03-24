using UnityEngine;
using UnityEngine.UIElements;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class LiftAccessPopupUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private int documentSortOrder = 2475;
        [SerializeField] private float fadeOutDuration = 0.2f;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _icon;
        private Label _title;
        private Label _body;

        private float _hideAtUnscaledTime = -1f;
        private bool _visible;

        private void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            document.sortingOrder = documentSortOrder;

            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            _panel = _root.Q<VisualElement>("LiftAccessPopupPanel");
            _icon = _root.Q<Label>("Lbl_LiftAccessIcon");
            _title = _root.Q<Label>("Lbl_LiftAccessTitle");
            _body = _root.Q<Label>("Lbl_LiftAccessBody");

            HideImmediate();
        }

        private void OnEnable()
        {
            LiftAccessPopupBus.OnPopup += HandlePopup;
        }

        private void OnDisable()
        {
            LiftAccessPopupBus.OnPopup -= HandlePopup;
        }

        private void Update()
        {
            if (!_visible || _hideAtUnscaledTime < 0f)
                return;

            float remaining = _hideAtUnscaledTime - Time.unscaledTime;
            if (remaining <= 0f)
            {
                HideImmediate();
                return;
            }

            if (_panel != null)
            {
                float alpha = remaining <= fadeOutDuration
                    ? Mathf.Clamp01(remaining / fadeOutDuration)
                    : 1f;

                _panel.style.opacity = alpha;
            }
        }

        private void HandlePopup(LiftAccessPopupBus.PopupMessage msg)
        {
            if (_panel == null)
                return;

            _panel.EnableInClassList("lift-access-popup-positive", msg.positive);
            _panel.EnableInClassList("lift-access-popup-negative", !msg.positive);

            if (_icon != null)
                _icon.text = msg.positive ? "?" : "!";

            if (_title != null)
                _title.text = string.IsNullOrWhiteSpace(msg.title) ? (msg.positive ? "Access Granted" : "Lift Locked") : msg.title;

            if (_body != null)
                _body.text = msg.body ?? string.Empty;

            if (msg.accent.a > 0f)
                _panel.style.borderLeftColor = msg.accent;

            _root.style.display = DisplayStyle.Flex;
            _root.style.visibility = Visibility.Visible;
            _panel.style.display = DisplayStyle.Flex;
            _panel.style.opacity = 1f;

            _visible = true;
            _hideAtUnscaledTime = Time.unscaledTime + Mathf.Max(0.5f, msg.seconds);
        }

        private void HideImmediate()
        {
            _visible = false;
            _hideAtUnscaledTime = -1f;

            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
                _root.style.visibility = Visibility.Hidden;
            }

            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
                _panel.style.opacity = 0f;
            }
        }
    }
}