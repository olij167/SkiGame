using UnityEngine;
using SkiGame.Progression;

namespace SkiGame.UI
{
    [DisallowMultipleComponent]
    public sealed class TutorialQuestBridge : MonoBehaviour
    {
        [SerializeField] private SkiLessonDirector lessonDirector;
        [SerializeField] private QuestSignalBus signalBus;

        private bool _wasLessonActive;
        private bool _wasCompleted;
        private int _lastStepIndex = -1;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            if (lessonDirector == null || signalBus == null)
                return;

            bool active = lessonDirector.IsLessonActive;
            bool completed = lessonDirector.LessonsCompleted;

            signalBus.SetState("tutorial.lesson.active", active);
            signalBus.SetState("tutorial.lesson.completed", completed);
            signalBus.SetStat("tutorial.step.index", lessonDirector.CurrentStepIndex);
            signalBus.SetStat("tutorial.step.total", lessonDirector.TotalStepCount);
            signalBus.SetText("tutorial.step.title", lessonDirector.CurrentStepTitle ?? string.Empty);

            if (active && !_wasLessonActive)
                signalBus.RaiseEvent("tutorial.lesson.started");

            if (active && lessonDirector.CurrentStepIndex != _lastStepIndex)
            {
                signalBus.RaiseEvent(
                    "tutorial.step.changed",
                    QuestSignalData.Create()
                        .WithTag("stepIndex", lessonDirector.CurrentStepIndex.ToString())
                        .WithTag("title", lessonDirector.CurrentStepTitle ?? string.Empty));
                _lastStepIndex = lessonDirector.CurrentStepIndex;
            }

            if (completed && !_wasCompleted)
                signalBus.RaiseEvent("tutorial.lesson.completed");

            _wasLessonActive = active;
            _wasCompleted = completed;
        }

        private void ResolveReferences()
        {
            if (lessonDirector == null)
                lessonDirector = FindObjectOfType<SkiLessonDirector>();

            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();
        }
    }
}
