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
            if (_indicator == null || lessonDirector == null || targetCamera == null || _root == null)
                return;

            var target = lessonDirector.CurrentObjectiveTarget;
            string label = lessonDirector.CurrentObjectiveLabel;

            bool show = lessonDirector.IsLessonActive && target != null && !string.IsNullOrEmpty(label);

            _indicator.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;

            Vector3 world = target.position + Vector3.up * 2f;
            Vector3 viewport = targetCamera.WorldToViewportPoint(world);

            bool behindCamera = viewport.z < 0f;
            if (behindCamera)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
                viewport.z = 0f;
            }

            float rootWidth = Mathf.Max(1f, _root.resolvedStyle.width);
            float rootHeight = Mathf.Max(1f, _root.resolvedStyle.height);

            float padX = edgePadding / rootWidth;
            float padY = edgePadding / rootHeight;

            viewport.x = Mathf.Clamp(viewport.x, padX, 1f - padX);
            viewport.y = Mathf.Clamp(viewport.y, padY, 1f - padY);

            float x = viewport.x * rootWidth;
            float y = viewport.y * rootHeight;

            if (_label != null)
            {
                float dist = Vector3.Distance(target.position, targetCamera.transform.position);
                _label.text = $"{label}  {dist:0} m";
            }

            _indicator.style.left = x - 90f;
            _indicator.style.top = y - 20f;
        }
    }
}