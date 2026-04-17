//using System.Collections;
//using UnityEngine;
//using UnityEngine.UIElements;
//using SkiGame.Progression;

//namespace SkiGame.UI
//{
//    [RequireComponent(typeof(UIDocument))]
//    public sealed class SkiLessonCompletionUI : MonoBehaviour
//    {
//        [SerializeField] private UIDocument document;
//        [SerializeField] private SkiLessonDirector lessonDirector;
//        [SerializeField] private float visibleSeconds = 3.0f;

//        private VisualElement _root;
//        private VisualElement _panel;

//        private Coroutine _hideRoutine;
//        private bool _shownThisSession;

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
//            _panel = _root.Q<VisualElement>("TutorialCompletePanel");

//            if (lessonDirector != null)
//            {
//                lessonDirector.OnLessonsCompleted -= HandleLessonsCompleted;
//                lessonDirector.OnLessonsStarted -= HideImmediate;
//                lessonDirector.OnLessonsSkipped -= HideImmediate;

//                lessonDirector.OnLessonsCompleted += HandleLessonsCompleted;
//                lessonDirector.OnLessonsStarted += HideImmediate;
//                lessonDirector.OnLessonsSkipped += HideImmediate;
//            }

//            HideImmediate();
//        }

//        private void OnDisable()
//        {
//            if (lessonDirector != null)
//            {
//                lessonDirector.OnLessonsCompleted -= HandleLessonsCompleted;
//                lessonDirector.OnLessonsStarted -= HideImmediate;
//                lessonDirector.OnLessonsSkipped -= HideImmediate;
//            }
//        }

//        private void HandleLessonsCompleted()
//        {
//            if (_shownThisSession)
//                return;

//            _shownThisSession = true;
//            ShowForDuration();
//        }

//        private void ShowForDuration()
//        {
//            if (_panel != null)
//                _panel.style.display = DisplayStyle.Flex;

//            if (_hideRoutine != null)
//                StopCoroutine(_hideRoutine);

//            _hideRoutine = StartCoroutine(HideAfterDelay());
//        }

//        private IEnumerator HideAfterDelay()
//        {
//            yield return new WaitForSecondsRealtime(visibleSeconds);
//            HideImmediate();
//            _hideRoutine = null;
//        }

//        private void HideImmediate()
//        {
//            if (_hideRoutine != null)
//            {
//                StopCoroutine(_hideRoutine);
//                _hideRoutine = null;
//            }

//            if (_panel != null)
//                _panel.style.display = DisplayStyle.None;
//        }
//    }
//}