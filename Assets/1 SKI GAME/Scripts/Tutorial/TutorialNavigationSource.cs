using UnityEngine;
using SkiGame.Navigation;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkiLessonDirector))]
    public sealed class TutorialNavigationSource : MonoBehaviour
    {
        [SerializeField] private SkiLessonDirector lessonDirector;
        [SerializeField] private int priority = 100;
        [SerializeField] private Vector3 targetWorldOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float arriveDistance = 10f;
        [SerializeField] private Color accentColor = new Color(0.36f, 0.74f, 1f, 1f);

        private void OnEnable()
        {
            if (lessonDirector == null)
                lessonDirector = GetComponent<SkiLessonDirector>();
        }

        private void OnDisable()
        {
            NavigationTargetController.ClearTarget(this);
        }

        private void LateUpdate()
        {
            if (lessonDirector == null)
            {
                NavigationTargetController.ClearTarget(this);
                return;
            }

            if (!lessonDirector.IsLessonActive)
            {
                NavigationTargetController.ClearTarget(this);
                return;
            }

            Transform target = lessonDirector.CurrentObjectiveTarget;
            string label = lessonDirector.CurrentObjectiveLabel;

            if (target == null || string.IsNullOrWhiteSpace(label))
            {
                NavigationTargetController.ClearTarget(this);
                return;
            }

            NavigationTargetController.SetTarget(this, new NavigationTargetRequest
            {
                owner = this,
                id = $"tutorial-step-{lessonDirector.CurrentStepIndex}",
                displayName = label,
                kind = NavigationTargetKind.TutorialObjective,
                targetTransform = target,
                worldPosition = Vector3.zero,
                worldOffset = targetWorldOffset,
                priority = priority,
                showHud = true,
                showWorldBeacon = true,
                clearWhenReached = true,
                arriveDistance = arriveDistance,
                accentColor = accentColor
            });
        }
    }
}