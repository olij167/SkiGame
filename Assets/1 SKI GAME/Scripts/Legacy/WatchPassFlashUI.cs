using UnityEngine;
using UnityEngine.UIElements;

public class WatchPassFlashUI : MonoBehaviour
{
    [SerializeField] private UIDocument doc;

    private VisualElement _root;
    private VisualElement _flash;
    private Label _icon;
    private Label _text;

    private IVisualElementScheduledItem _hideSchedule;

    private void Awake()
    {
        if (doc == null) doc = GetComponent<UIDocument>();
        _root = doc != null ? doc.rootVisualElement : null;

        if (_root == null) return;

        _flash = _root.Q<VisualElement>("WatchPassFlash");
        _icon = _root.Q<Label>("WatchPassFlashIcon");
        _text = _root.Q<Label>("WatchPassFlashText");

        HideImmediate();
    }

    private void OnEnable()
    {
        SkiPassWatchFeedbackBus.OnFeedback += OnFeedback;
    }

    private void OnDisable()
    {
        SkiPassWatchFeedbackBus.OnFeedback -= OnFeedback;
    }

    private void OnFeedback(SkiPassWatchFeedbackBus.Feedback f)
    {
        if (_flash == null) return;

        // Ensure it renders above other watch contents regardless of UXML sibling order.
        _flash.BringToFront();

        // Ensure visible
        _flash.RemoveFromClassList("is-hidden");
        _flash.AddToClassList("is-on");

        // State classes
        _flash.RemoveFromClassList("allow");
        _flash.RemoveFromClassList("deny");
        _flash.AddToClassList(f.allowed ? "allow" : "deny");

        // Content
        if (_icon != null) _icon.text = f.allowed ? "✓" : "✕";
        if (_text != null) _text.text = f.message ?? "";

        // Cancel prior scheduled hide
        _hideSchedule?.Pause();

        // Hide after delay
        float seconds = Mathf.Max(0.4f, f.seconds);
        _hideSchedule = _flash.schedule.Execute(() =>
        {
            HideImmediate();
        }).StartingIn((long)(seconds * 1000f));
    }

    private void HideImmediate()
    {
        if (_flash == null) return;

        _flash.RemoveFromClassList("is-on");
        _flash.AddToClassList("is-hidden");
    }
}
