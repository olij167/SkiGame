using SkiGame.Activities;
using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestActivitySignalSource : MonoBehaviour
    {
        [SerializeField] private QuestSignalBus signalBus;
        [SerializeField] private MountainActivityManager activityManager;

        private void OnEnable()
        {
            ResolveReferences();
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (activityManager == null)
            {
                ResolveReferences();
                Bind();
            }
        }

        private void HandleActivityStarted(MountainActivityKind kind, MonoBehaviour source, string displayName, int variantNumber)
        {
            signalBus?.RaiseEvent(
                $"activity.{kind.ToString().ToLowerInvariant()}.started",
                QuestSignalData.Create()
                    .WithTag("activityKind", kind.ToString().ToLowerInvariant())
                    .WithTag("displayName", displayName ?? string.Empty)
                    .WithTag("variant", variantNumber.ToString()));
        }

        private void HandleActivityCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            signalBus?.RaiseEvent(
                $"activity.{kind.ToString().ToLowerInvariant()}.completed",
                QuestSignalData.Create()
                    .WithTag("activityKind", kind.ToString().ToLowerInvariant())
                    .WithTag("displayName", displayName ?? string.Empty));
        }

        private void HandleActivityFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
        {
            signalBus?.RaiseEvent(
                $"activity.{kind.ToString().ToLowerInvariant()}.failed",
                QuestSignalData.Create()
                    .WithTag("activityKind", kind.ToString().ToLowerInvariant())
                    .WithTag("displayName", displayName ?? string.Empty)
                    .WithTag("reason", reason ?? string.Empty));
        }

        private void HandleActivityCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            signalBus?.RaiseEvent(
                $"activity.{kind.ToString().ToLowerInvariant()}.cancelled",
                QuestSignalData.Create()
                    .WithTag("activityKind", kind.ToString().ToLowerInvariant())
                    .WithTag("displayName", displayName ?? string.Empty));
        }

        private void ResolveReferences()
        {
            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();

            if (activityManager == null)
                activityManager = MountainActivityManager.Instance != null
                    ? MountainActivityManager.Instance
                    : FindObjectOfType<MountainActivityManager>();
        }

        private void Bind()
        {
            if (activityManager == null)
                return;

            Unbind();
            activityManager.OnActivityStarted += HandleActivityStarted;
            activityManager.OnActivityCompleted += HandleActivityCompleted;
            activityManager.OnActivityFailed += HandleActivityFailed;
            activityManager.OnActivityCancelled += HandleActivityCancelled;
        }

        private void Unbind()
        {
            if (activityManager == null)
                return;

            activityManager.OnActivityStarted -= HandleActivityStarted;
            activityManager.OnActivityCompleted -= HandleActivityCompleted;
            activityManager.OnActivityFailed -= HandleActivityFailed;
            activityManager.OnActivityCancelled -= HandleActivityCancelled;
        }
    }
}
