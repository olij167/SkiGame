//using UnityEngine;
//using UnityEngine.UIElements;
//using SkiGame.Progression;

//namespace SkiGame.UI
//{
//    [RequireComponent(typeof(UIDocument))]
//    public sealed class SkiLessonOfferUI : MonoBehaviour
//    {
//        [SerializeField] private UIDocument document;
//        [SerializeField] private SkiLessonDirector lessonDirector;

//        private VisualElement _root;
//        private VisualElement _panel;
//        private Label _title;
//        private Label _body;
//        private Button _startButton;
//        private Button _skipButton;

//        private void OnEnable()
//        {
//            if (document == null)
//                document = GetComponent<UIDocument>();

//            if (lessonDirector == null)
//                lessonDirector = FindObjectOfType<SkiLessonDirector>();

//            if (document == null || document.rootVisualElement == null)
//            {
//                enabled = false;
//                return;
//            }

//            _root = document.rootVisualElement;
//            _panel = _root.Q<VisualElement>("TutorialOfferPanel");
//            _title = _root.Q<Label>("Lbl_TutorialOfferTitle");
//            _body = _root.Q<Label>("Lbl_TutorialOfferBody");
//            _startButton = _root.Q<Button>("Btn_TutorialOfferStart");
//            _skipButton = _root.Q<Button>("Btn_TutorialOfferSkip");

//            if (_startButton != null)
//            {
//                _startButton.clicked -= OnStartClicked;
//                _startButton.clicked += OnStartClicked;
//            }

//            if (_skipButton != null)
//            {
//                _skipButton.clicked -= OnSkipClicked;
//                _skipButton.clicked += OnSkipClicked;
//            }

//            if (lessonDirector != null)
//            {
//                lessonDirector.OnLessonsOffered -= Refresh;
//                lessonDirector.OnLessonsStarted -= Hide;
//                lessonDirector.OnLessonsCompleted -= Hide;
//                lessonDirector.OnLessonsSkipped -= Hide;

//                lessonDirector.OnLessonsOffered += Refresh;
//                lessonDirector.OnLessonsStarted += Hide;
//                lessonDirector.OnLessonsCompleted += Hide;
//                lessonDirector.OnLessonsSkipped += Hide;
//            }

//            Refresh();
//        }

//        private void OnDisable()
//        {
//            if (lessonDirector != null)
//            {
//                lessonDirector.OnLessonsOffered -= Refresh;
//                lessonDirector.OnLessonsStarted -= Hide;
//                lessonDirector.OnLessonsCompleted -= Hide;
//                lessonDirector.OnLessonsSkipped -= Hide;
//            }
//        }

//        private void Refresh()
//        {
//            bool shouldShow = lessonDirector != null && lessonDirector.CanOfferLessons;
//            if (_panel != null)
//                _panel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;

//            if (!shouldShow)
//                return;

//            if (_title != null)
//                _title.text = "Sign Up for Ski Lessons?";

//            if (_body != null)
//            {
//                _body.text =
//                    "Learn the basics of speed control, carving, skating, stopping, jumps, poles, soreness, and resort exploration.\n\n" +
//                    "You can skip for now and come back later.";
//            }
//        }

//        private void Hide()
//        {
//            if (_panel != null)
//                _panel.style.display = DisplayStyle.None;
//        }

//        private void OnStartClicked()
//        {
//            if (lessonDirector != null)
//                lessonDirector.BeginLessons();
//        }

//        private void OnSkipClicked()
//        {
//            if (lessonDirector != null)
//                lessonDirector.DeferLessons();

//            Hide();
//        }
//    }
//}