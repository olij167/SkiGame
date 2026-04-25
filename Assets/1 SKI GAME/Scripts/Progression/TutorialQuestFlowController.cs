using UnityEngine;

namespace SkiGame.Progression
{
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class TutorialQuestFlowController : MonoBehaviour
    {
        private const string MoveAndLeanQuestId = "tutorial.move_and_lean";
        private const string TurnAndBrakeQuestId = "tutorial.turn_and_brake";
        private const string SkateAndPolesQuestId = "tutorial.skate_and_poles";
        private const string SpeedControlQuestId = "tutorial.speed_control";
        private const string JumpAndAirQuestId = "tutorial.jump_and_air";
        private const string StackRecoveryQuestId = "tutorial.stack_recovery";
        private const string ResortRecoveryQuestId = "tutorial.resort_recovery";
        private const string OverlayQuestId = "tutorial.overlay_basics";
        private const string ServicesQuestId = "tutorial.resort_services";
        private const string LiftQuestId = "tutorial.lift_basics";
        private const string RaceQuestId = "tutorial.race_intro";
        private const string RescueQuestId = "tutorial.rescue_intro";

        [SerializeField] private QuestDirector questDirector;
        [SerializeField] private QuestContextProvider questContext;
        [SerializeField] private QuestSignalBus signalBus;
        [SerializeField] private bool requireLegacyLessonActive = false;
        [SerializeField, Range(0.1f, 2f)] private float evaluateIntervalSeconds = 0.25f;

        private float _nextEvaluateTime;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            EvaluateTutorialQuestAvailability();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextEvaluateTime)
                return;

            _nextEvaluateTime = Time.unscaledTime + evaluateIntervalSeconds;

            ResolveReferencesIfMissing();
            EvaluateTutorialQuestAvailability();
        }

        private void ResolveReferencesIfMissing()
        {
            if (questDirector == null || questContext == null || signalBus == null)
                ResolveReferences();
        }

        private void EvaluateTutorialQuestAvailability()
        {
            if (questDirector == null || questContext == null)
                return;

            if (requireLegacyLessonActive && !questContext.ReadBool("tutorial.lesson.active"))
                return;

            float speed = questContext.ReadFloat("player.speed");
            float airTime = questContext.ReadFloat("player.air_time");
            float groundDistance = questContext.ReadFloat("player.ground_distance");

            bool inResort = questContext.ReadBool("player.in_resort");
            bool skisEquipped = questContext.ReadBool("player.skis_equipped");
            bool hasDefaultPass = questContext.ReadBool("player.has_default_pass");
            bool onLift = questContext.ReadBool("player.on_lift");
            bool grounded = questContext.ReadBool("player.grounded");
            bool airborne = questContext.ReadBool("player.airborne");
            bool stacked = questContext.ReadBool("player.stacked");
            bool everStacked = questContext.ReadBool("player.ever_stacked");
            bool overlayOpened = HasEvent("ui.overlay.opened");
            bool raceStarted = HasEvent("activity.race.started");
            bool rescueStarted = HasEvent("activity.rescue.started");
            bool stackEventSeen = HasEvent("player.stacked");
            bool usedTuck = HasEvent("input.tuck.used");
            bool usedPoles = HasEvent("input.poles.push") || HasEvent("input.poles.drag");
            bool usedJumpAdjust = HasEvent("movement.jump_adjusted");
            bool viewedQuests = HasEvent("ui.overlay.quests_viewed");
            bool viewedTasks = HasEvent("ui.overlay.tasks_viewed");
            bool visitedRaceKiosk = HasEvent("ui.race_kiosk.opened");

            TryAccept(MoveAndLeanQuestId, true);
            TryAccept(TurnAndBrakeQuestId,
                questDirector.IsQuestCompleted(MoveAndLeanQuestId) ||
                speed >= 3.5f ||
                groundDistance >= 16f);

            TryAccept(SkateAndPolesQuestId,
                questDirector.IsQuestCompleted(TurnAndBrakeQuestId) ||
                (grounded && speed <= 2.2f && groundDistance >= 14f) ||
                usedPoles);

            TryAccept(SpeedControlQuestId,
                questDirector.IsQuestCompleted(TurnAndBrakeQuestId) ||
                questDirector.IsQuestCompleted(SkateAndPolesQuestId) ||
                speed >= 5.5f ||
                usedTuck);

            TryAccept(JumpAndAirQuestId,
                questDirector.IsQuestCompleted(SpeedControlQuestId) ||
                airTime >= 0.1f ||
                airborne ||
                usedJumpAdjust);

            TryAccept(StackRecoveryQuestId,
                stacked || everStacked || stackEventSeen);

            TryAccept(ResortRecoveryQuestId,
                questDirector.IsQuestCompleted(StackRecoveryQuestId));

            TryAccept(OverlayQuestId,
                questDirector.IsQuestCompleted(TurnAndBrakeQuestId) ||
                questDirector.IsQuestCompleted(ResortRecoveryQuestId) ||
                inResort ||
                overlayOpened);

            TryAccept(ServicesQuestId,
                inResort ||
                (overlayOpened && viewedQuests) ||
                viewedTasks ||
                questDirector.IsQuestCompleted(OverlayQuestId));

            TryAccept(LiftQuestId,
                hasDefaultPass ||
                onLift);

            TryAccept(RaceQuestId,
                hasDefaultPass &&
                (questDirector.IsQuestCompleted(LiftQuestId) ||
                 visitedRaceKiosk ||
                 raceStarted));

            TryAccept(RescueQuestId,
                (questDirector.IsQuestCompleted(RaceQuestId) && inResort) ||
                rescueStarted);
        }

        private void TryAccept(string questId, bool shouldAccept)
        {
            if (!shouldAccept || string.IsNullOrWhiteSpace(questId) || questDirector == null)
                return;

            if (questDirector.IsQuestAccepted(questId) || questDirector.IsQuestCompleted(questId))
                return;

            questDirector.TryAcceptQuest(questId);
        }

        private bool HasEvent(string key)
        {
            return signalBus != null && signalBus.CountEvents(key, 0, null) > 0;
        }

        private void ResolveReferences()
        {
            if (questDirector == null)
                questDirector = QuestDirector.Instance != null
                    ? QuestDirector.Instance
                    : FindObjectOfType<QuestDirector>();

            if (questContext == null)
                questContext = FindObjectOfType<QuestContextProvider>();

            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();
        }
    }
}
