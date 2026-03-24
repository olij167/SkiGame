using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Progression;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class TutorialObjectiveIndicatorUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private SkiLessonDirector lessonDirector;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float edgePadding = 48f;

        private VisualElement _root;
        private VisualElement _indicator;
        private Label _label;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (lessonDirector == null)
                lessonDirector = FindObjectOfType<SkiLessonDirector>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            _root = document.rootVisualElement;
            _indicator = _root.Q<VisualElement>("TutorialObjectiveIndicator");
            _label = _root.Q<Label>("Lbl_TutorialObjective");
        }

        private void Update()
        {
            if (_indicator == null || lessonDirector == null || targetCamera == null)
                return;

            var target = lessonDirector.CurrentObjectiveTarget;
            bool show = lessonDirector.IsLessonActive && target != null && !string.IsNullOrEmpty(lessonDirector.CurrentObjectiveLabel);

            _indicator.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;

            if (_label != null)
            {
                float dist = Vector3.Distance(target.position, targetCamera.transform.position);
                _label.text = $"{lessonDirector.CurrentObjectiveLabel}  {dist:0} m";
            }

            Vector3 screen = targetCamera.WorldToScreenPoint(target.position + Vector3.up * 2f);
            float x = Mathf.Clamp(screen.x, edgePadding, Screen.width - edgePadding);
            float y = Mathf.Clamp(Screen.height - screen.y, edgePadding, Screen.height - edgePadding);

            _indicator.style.left = x - 80f;
            _indicator.style.top = y - 18f;
        }
    }
}