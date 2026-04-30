using System;
using System.Collections.Generic;
using SkiGame.Progression;
using UnityEditor;
using UnityEngine;

namespace SkiGame.EditorTools
{
    public static class TutorialQuestGenerator
    {
        private const string RootFolder = "Assets/1 SKI GAME/Data";
        private const string QuestFolder = "Assets/1 SKI GAME/Data/Quests";
        private const string TutorialFolder = "Assets/1 SKI GAME/Data/Quests/Tutorial";
        private const string LessonFolder = "Assets/1 SKI GAME/Data/Quests/Tutorial/Lessons";
        private const string NpcFolder = "Assets/1 SKI GAME/Data/NPC";
        private const string QuestOfferFolder = "Assets/1 SKI GAME/Data/NPC/QuestOffers";
        private const string TutorialQuestOfferFolder = "Assets/1 SKI GAME/Data/NPC/QuestOffers/Tutorial";
        private const string DialogueDataFolder = "Assets/1 SKI GAME/Data/NPC/Dialogue";
        private const string TutorialCatalogPath = "Assets/1 SKI GAME/Data/Quests/QuestCatalog_Tutorial.asset";
        private const string MainCatalogPath = "Assets/1 SKI GAME/Prefabs/QuestCatalog.asset";
        private const string ExistingInstructorDialogueBankPath = "Assets/1 SKI GAME/Prefabs/Dialogue/Instructor DialogueBank.asset";
        private const string GeneratedInstructorDialogueBankPath = "Assets/1 SKI GAME/Data/NPC/Dialogue/DB_Instructor_Tutorial.asset";
        private const string TutorialDialogueStylePath = "Assets/1 SKI GAME/Prefabs/Dialogue/TutorialStyle.asset";

        private static readonly string[] LessonQuestIds =
        {
            "tutorial.lesson.beginner",
            "tutorial.lesson.intermediate",
            "tutorial.lesson.advanced",
            "tutorial.lesson.expert"
        };

        [MenuItem("Tools/Ski Game/Quests/Generate Tutorial Quest Assets")]
        public static void GenerateTutorialQuests()
        {
            GenerateInstructorLessonTutorialQuests();
        }

        [MenuItem("Tools/Ski Game/Quests/Generate Instructor Lesson Tutorial Quests")]
        public static void GenerateInstructorLessonTutorialQuests()
        {
            EnsureCoreFolders();
            EnsureFolder(TutorialFolder, "Lessons");
            EnsureFolder(RootFolder, "NPC");
            EnsureFolder(NpcFolder, "QuestOffers");
            EnsureFolder(QuestOfferFolder, "Tutorial");
            EnsureFolder(NpcFolder, "Dialogue");

            var tutorialCatalog = LoadOrCreateAsset<QuestCatalogSO>(TutorialCatalogPath);

            var generated = new List<QuestDefinitionSO>
            {
                CreateBeginnerLessonQuest(),
                CreateIntermediateLessonQuest(),
                CreateAdvancedLessonQuest(),
                CreateExpertLessonQuest()
            };

            UpsertGeneratedQuests(tutorialCatalog, generated);

            var mainCatalog = AssetDatabase.LoadAssetAtPath<QuestCatalogSO>(MainCatalogPath);
            if (mainCatalog != null)
                UpsertGeneratedQuests(mainCatalog, generated);

            GenerateInstructorQuestOffers(generated);
            UpsertInstructorDialogueBank();
            ValidateGeneratedQuests(generated);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = tutorialCatalog;
            Debug.Log(
                $"Generated {generated.Count} instructor-led tutorial lesson quests, updated {TutorialCatalogPath}," +
                $" generated tutorial quest offers, and refreshed the instructor dialogue bank." +
                (mainCatalog != null ? $" Also updated {MainCatalogPath}." : string.Empty));
        }

        [MenuItem("Tools/Ski Game/Quests/Generate Legacy Granular Tutorial Quest Assets")]
        public static void GenerateLegacyGranularTutorialQuests()
        {
            EnsureCoreFolders();

            var tutorialCatalog = LoadOrCreateAsset<QuestCatalogSO>(TutorialCatalogPath);

            var generated = new List<QuestDefinitionSO>
            {
                CreateMoveAndLeanQuest(),
                CreateTurnAndBrakeQuest(),
                CreateSkateAndPolesQuest(),
                CreateSpeedControlQuest(),
                CreateJumpAndAirQuest(),
                CreateStackRecoveryQuest(),
                CreateResortRecoveryQuest(),
                CreateOverlayBasicsQuest(),
                CreateResortServicesQuest(),
                CreateLiftBasicsQuest(),
                CreateRaceIntroQuest(),
                CreateRescueIntroQuest()
            };

            UpsertGeneratedQuests(tutorialCatalog, generated);

            var mainCatalog = AssetDatabase.LoadAssetAtPath<QuestCatalogSO>(MainCatalogPath);
            if (mainCatalog != null)
                UpsertGeneratedQuests(mainCatalog, generated);

            ValidateGeneratedQuests(generated);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = tutorialCatalog;
            Debug.Log(
                $"Generated {generated.Count} legacy granular tutorial quest assets and updated {TutorialCatalogPath}" +
                (mainCatalog != null ? $" and {MainCatalogPath}" : string.Empty));
        }

        [MenuItem("Tools/Ski Game/Quests/Remove Legacy Tutorial Quests From Catalogs")]
        public static void RemoveLegacyTutorialQuestsFromCatalogs()
        {
            bool changed = false;
            var lessonIds = new HashSet<string>(LessonQuestIds, StringComparer.OrdinalIgnoreCase);

            var tutorialCatalog = AssetDatabase.LoadAssetAtPath<QuestCatalogSO>(TutorialCatalogPath);
            changed |= RetainTutorialQuestIds(tutorialCatalog, lessonIds);

            var mainCatalog = AssetDatabase.LoadAssetAtPath<QuestCatalogSO>(MainCatalogPath);
            changed |= RetainTutorialQuestIds(mainCatalog, lessonIds);

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Selection.activeObject = tutorialCatalog;
            Debug.Log(changed
                ? "Removed legacy tutorial quests from active catalogs."
                : "Active catalogs already reference only the instructor lesson tutorial quests.");
        }

        private static QuestDefinitionSO CreateBeginnerLessonQuest()
        {
            return CreateQuest(
                $"{LessonFolder}/Quest_Tutorial_Lesson_Beginner.asset",
                "tutorial.lesson.beginner",
                "Beginner Ski Lesson",
                "Start with the fundamentals: moving on skis, steering, braking, visiting the resort, claiming a pass, and taking your first lift-assisted run.",
                Stage(
                    "tutorial.lesson.beginner.stage1",
                    "Reach the Instructor",
                    "Get moving on skis and settle into the core weight-shift basics before the rest of the lesson begins.",
                    UseInput("tutorial.lesson.beginner.move", "Use movement input", QuestInputActionType.MoveInput, 3, "Start moving around the instructor and get your skis responding."),
                    UseInput("tutorial.lesson.beginner.lean_forward", "Lean forward", QuestInputActionType.LeanForward, 1, "Lean forward once to build speed."),
                    UseInput("tutorial.lesson.beginner.lean_backward", "Lean backward", QuestInputActionType.LeanBackward, 1, "Lean backward once to settle the skis."),
                    Travel("tutorial.lesson.beginner.distance", "Travel 12m", QuestDistanceStatType.GroundDistance, 12f, "Glide a short distance while you get comfortable.")),
                Stage(
                    "tutorial.lesson.beginner.stage2",
                    "Basic Ski Control",
                    "Use skating to create momentum, then practice turning, wedging, and a quick stop.",
                    Movement("tutorial.lesson.beginner.skate", "Skate forward", QuestMovementSkillType.SkateSwitch, 2, "Alternate ski pressure to skate forward."),
                    Travel("tutorial.lesson.beginner.turn", "Turn for 2.5 seconds", QuestDistanceStatType.TurnTime, 2.5f, "Steer across the slope and hold the turn."),
                    Movement("tutorial.lesson.beginner.wedge", "Wedge to brake", QuestMovementSkillType.Wedge, 1, "Point the skis inward to slow down."),
                    Optional(Movement("tutorial.lesson.beginner.quick_stop", "Quick stop", QuestMovementSkillType.QuickStop, 1, "Try a sharper braking move once you have a little speed."))),
                Stage(
                    "tutorial.lesson.beginner.stage3",
                    "Resort Basics",
                    "Visit the resort, then claim the default pass so the lift network is available.",
                    Interaction("tutorial.lesson.beginner.enter_resort", "Enter the resort", QuestInteractionTargetType.EnterResort, 1, "Head into the resort area."),
                    Optional(State("tutorial.lesson.beginner.in_resort", "Be inside the resort", QuestStateTargetType.InResort, true, "Settle inside the resort once you arrive.")),
                    Interaction("tutorial.lesson.beginner.claim_pass", "Claim the default pass", QuestInteractionTargetType.ClaimDefaultPass, 1, "Claim the starter pass from the kiosk."),
                    Optional(Ui("tutorial.lesson.beginner.kiosk_opened", "Open the ski pass kiosk", QuestUiScreenTargetType.KioskOpened, description: "Open the kiosk before claiming your pass."))),
                Stage(
                    "tutorial.lesson.beginner.stage4",
                    "First Lift and First Run",
                    "Ride a lift, step off at the top, and complete a short downhill run.",
                    Interaction("tutorial.lesson.beginner.mount_lift", "Mount a lift", QuestInteractionTargetType.MountLift, 1, "Board a lift at the base station."),
                    Interaction("tutorial.lesson.beginner.dismount_lift", "Dismount the lift", QuestInteractionTargetType.DismountLift, 1, "Step off cleanly at the top station."),
                    Travel("tutorial.lesson.beginner.post_lift_run", "Travel 90m after the lift", QuestDistanceStatType.GroundDistance, 90f, "Take a short first run back down the mountain."),
                    Optional(State("tutorial.lesson.beginner.grounded", "Stay grounded on snow", QuestStateTargetType.Grounded, true, "Keep it stable on the snow after the dismount."))));
        }

        private static QuestDefinitionSO CreateIntermediateLessonQuest()
        {
            return CreateQuest(
                $"{LessonFolder}/Quest_Tutorial_Lesson_Intermediate.asset",
                "tutorial.lesson.intermediate",
                "Intermediate Ski Lesson",
                "Start with a readiness check, then work on poles, tuck, air control, and the core mountain HUD tabs.",
                Stage(
                    "tutorial.lesson.intermediate.stage1",
                    "Readiness Check",
                    "Show that you can already manage the basics before moving into intermediate terrain habits.",
                    UseInput("tutorial.lesson.intermediate.lean_forward", "Lean forward", QuestInputActionType.LeanForward, 1, "Show a clean forward lean input."),
                    UseInput("tutorial.lesson.intermediate.lean_backward", "Lean backward", QuestInputActionType.LeanBackward, 1, "Show a clean backward lean input."),
                    Movement("tutorial.lesson.intermediate.wedge", "Wedge to brake", QuestMovementSkillType.Wedge, 1, "Brake in control before we move on."),
                    Travel("tutorial.lesson.intermediate.distance", "Travel 24m", QuestDistanceStatType.GroundDistance, 24f, "Stay balanced over a longer glide.")),
                Stage(
                    "tutorial.lesson.intermediate.stage2",
                    "Poles and Tuck",
                    "Use poles to build or resist speed, then hold a tuck on the move.",
                    UseInput("tutorial.lesson.intermediate.pole_push", "Push with poles", QuestInputActionType.PolePush, 2, "Use a pair of pole pushes on flatter terrain."),
                    UseInput("tutorial.lesson.intermediate.pole_drag", "Drag the poles", QuestInputActionType.PoleDrag, 1, "Use pole drag to add resistance."),
                    UseInput("tutorial.lesson.intermediate.tuck", "Use a tuck", QuestInputActionType.Tuck, 1, "Settle into a tuck while moving."),
                    Travel("tutorial.lesson.intermediate.tuck_time", "Hold tuck for 3 seconds", QuestDistanceStatType.TuckTime, 3f, "Carry speed in a stable tuck.")),
                Stage(
                    "tutorial.lesson.intermediate.stage3",
                    "Air Control",
                    "Jump, adjust your body in the air, and land under control.",
                    Movement("tutorial.lesson.intermediate.jump_adjust", "Adjust in the air", QuestMovementSkillType.JumpAdjust, 1, "Make a deliberate in-air adjustment."),
                    UseInput("tutorial.lesson.intermediate.air_pose", "Use an air pose", QuestInputActionType.AirPose, 1, "Trigger an air pose while airborne."),
                    Travel("tutorial.lesson.intermediate.air_time", "Stay airborne for 0.5 seconds", QuestDistanceStatType.AirTime, 0.5f, "Spend a little more time in the air."),
                    Optional(State("tutorial.lesson.intermediate.grounded", "Land back on snow", QuestStateTargetType.Grounded, true, "Touch back down cleanly."))),
                Stage(
                    "tutorial.lesson.intermediate.stage4",
                    "Mountain HUD Basics",
                    "Use the overlay to check quests, tasks, map, and stats while you move around the mountain.",
                    Ui("tutorial.lesson.intermediate.overlay_opened", "Open the overlay", QuestUiScreenTargetType.OverlayOpened, description: "Open the mountain HUD."),
                    Ui("tutorial.lesson.intermediate.quests_viewed", "View Quests", QuestUiScreenTargetType.QuestsViewed, description: "Visit the Quests tab."),
                    Ui("tutorial.lesson.intermediate.stats_viewed", "View Stats", QuestUiScreenTargetType.StatsViewed, description: "Visit the Stats tab."),
                    Ui("tutorial.lesson.intermediate.tasks_viewed", "View Tasks", QuestUiScreenTargetType.TasksViewed, description: "Visit the Tasks tab."),
                    Ui("tutorial.lesson.intermediate.map_viewed", "View Map", QuestUiScreenTargetType.MapViewed, description: "Visit the map tab.")));
        }

        private static QuestDefinitionSO CreateAdvancedLessonQuest()
        {
            return CreateQuest(
                $"{LessonFolder}/Quest_Tutorial_Lesson_Advanced.asset",
                "tutorial.lesson.advanced",
                "Advanced Ski Lesson",
                "Pass a tighter readiness check, then focus on trick landings, style, and mountain navigation awareness.",
                Stage(
                    "tutorial.lesson.advanced.stage1",
                    "Readiness Check",
                    "Show sharp braking and controlled airtime before the advanced lesson opens up.",
                    Movement("tutorial.lesson.advanced.quick_stop", "Quick stop", QuestMovementSkillType.QuickStop, 1, "Brake aggressively and in control."),
                    Movement("tutorial.lesson.advanced.jump_adjust", "Jump adjust", QuestMovementSkillType.JumpAdjust, 1, "Stay deliberate in the air."),
                    Travel("tutorial.lesson.advanced.air_time", "Stay airborne for 0.5 seconds", QuestDistanceStatType.AirTime, 0.5f, "Hold the landing window a little longer.")),
                Stage(
                    "tutorial.lesson.advanced.stage2",
                    "Tricks and Style",
                    "Start landing tricks intentionally, then layer in style and more specific landings.",
                    Trick("tutorial.lesson.advanced.any_landing", "Land any trick", QuestTrickTargetType.AnyLanding, 1, "Land one successful trick."),
                    Trick("tutorial.lesson.advanced.styled_landing", "Land a styled trick", QuestTrickTargetType.StyledLanding, 1, "Use style during the trick and land it."),
                    Optional(Trick("tutorial.lesson.advanced.named_landing", "Land a named trick", QuestTrickTargetType.NamedLanding, 1, "Land a trick that resolves with a named landing.")),
                    Optional(Trick("tutorial.lesson.advanced.grind_landing", "Land a grind trick", QuestTrickTargetType.GrindLanding, 1, "Use a grind feature if one is available nearby."))),
                Stage(
                    "tutorial.lesson.advanced.stage3",
                    "Map and Navigation Features",
                    "Use the map deliberately before committing to more complex routes.",
                    Ui("tutorial.lesson.advanced.map_viewed", "View the map", QuestUiScreenTargetType.MapViewed, description: "Open the map and use it to plan your next route.")));
        }

        private static QuestDefinitionSO CreateExpertLessonQuest()
        {
            // TODO Phase 6: extend these instructor-led lessons with authored NPC action systems
            // such as InstructorTutorialGuide, NpcActionPerformer, NpcQuestActionSequence,
            // NpcQuestActionStepSO, MoveToMarker, WaitForPlayerNearby, SayLine,
            // DemonstrateMovement, PerformTrickPose, and RaiseQuestSignal.
            return CreateQuest(
                $"{LessonFolder}/Quest_Tutorial_Lesson_Expert.asset",
                "tutorial.lesson.expert",
                "Expert Ski Lesson",
                "Work through races, rescue activities, and the mountain HUD tools that support higher-level runs.",
                Stage(
                    "tutorial.lesson.expert.stage1",
                    "Activity Readiness",
                    "Open the race kiosk and start a race to show you can work with mountain activity systems.",
                    Ui("tutorial.lesson.expert.race_kiosk", "Open the race kiosk", QuestUiScreenTargetType.RaceKioskOpened, description: "Visit the race signup kiosk."),
                    Activity("tutorial.lesson.expert.race_started", "Start a race", QuestActivityTargetType.RaceStarted, 1, "Begin a race from the kiosk flow.")),
                Stage(
                    "tutorial.lesson.expert.stage2",
                    "Race Completion",
                    "Finish the race cleanly before moving into rescue work.",
                    Activity("tutorial.lesson.expert.race_completed", "Complete a race", QuestActivityTargetType.RaceCompleted, 1, "Cross the finish line on an active race.")),
                Stage(
                    "tutorial.lesson.expert.stage3",
                    "Rescue Basics",
                    "Take a rescue dispatch and complete it successfully.",
                    Activity("tutorial.lesson.expert.rescue_started", "Start a rescue", QuestActivityTargetType.RescueStarted, 1, "Accept a rescue activity."),
                    Activity("tutorial.lesson.expert.rescue_completed", "Complete a rescue", QuestActivityTargetType.RescueCompleted, 1, "Finish the rescue activity.")),
                Stage(
                    "tutorial.lesson.expert.stage4",
                    "HUD Utilities",
                    "Use the overlay tools that help you track work across the mountain.",
                    Ui("tutorial.lesson.expert.overlay_opened", "Open the overlay", QuestUiScreenTargetType.OverlayOpened, description: "Open the mountain HUD."),
                    Compound(
                        "tutorial.lesson.expert.progress_tabs",
                        "Check your progress tabs",
                        "Review either the Tasks or Quests tab while the overlay is open.",
                        QuestConditionKind.AnyOf,
                        Ui("tutorial.lesson.expert.tasks_viewed", "View Tasks", QuestUiScreenTargetType.TasksViewed, description: "Open the Tasks tab."),
                        Ui("tutorial.lesson.expert.quests_viewed", "View Quests", QuestUiScreenTargetType.QuestsViewed, description: "Open the Quests tab."))));
        }

        private static void GenerateInstructorQuestOffers(IReadOnlyList<QuestDefinitionSO> quests)
        {
            if (quests == null)
                return;

            var byId = new Dictionary<string, QuestDefinitionSO>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest != null)
                    byId[quest.SafeId] = quest;
            }

            CreateOrUpdateQuestOffer(
                $"{TutorialQuestOfferFolder}/NpcQuestOffer_Instructor_BeginnerLesson.asset",
                FindQuest(byId, "tutorial.lesson.beginner"),
                "Beginner Ski Lesson",
                "Ready for your first lesson? We'll start with skating, leaning, turning, and getting your first pass.",
                "Great. Start by moving around me, then I'll send you toward the resort.",
                "Keep working through the basics. Move, turn, brake, then check in at the resort.",
                "Nice. You know enough to start exploring safely.",
                "That's your first lesson done. Come back when you want to learn faster movement.");

            CreateOrUpdateQuestOffer(
                $"{TutorialQuestOfferFolder}/NpcQuestOffer_Instructor_IntermediateLesson.asset",
                FindQuest(byId, "tutorial.lesson.intermediate"),
                "Intermediate Ski Lesson",
                "Want the intermediate lesson? I'll first check that you can move, lean, and brake cleanly.",
                "Good. Show me the basics first, then we'll work on poles, tuck, and air control.",
                "Start with the readiness check, then move on to poles, tuck, and air.",
                "You're starting to move with intent now.",
                "Intermediate lesson complete. Advanced lessons will push tricks and navigation.");

            CreateOrUpdateQuestOffer(
                $"{TutorialQuestOfferFolder}/NpcQuestOffer_Instructor_AdvancedLesson.asset",
                FindQuest(byId, "tutorial.lesson.advanced"),
                "Advanced Ski Lesson",
                "Advanced lesson? I'll check your stops and air control first, then we'll move into tricks and map navigation.",
                "Show me control first. After that, we'll focus on style and planning.",
                "Control first, style second. Keep going.",
                "Good work. You're ready for more demanding routes and activities.",
                "Advanced lesson complete. Expert lessons focus on activities and rescue work.");

            CreateOrUpdateQuestOffer(
                $"{TutorialQuestOfferFolder}/NpcQuestOffer_Instructor_ExpertLesson.asset",
                FindQuest(byId, "tutorial.lesson.expert"),
                "Expert Ski Lesson",
                "Expert lesson? This one is about activities, races, rescue work, and using the mountain systems properly.",
                "Let's see how you handle the broader mountain.",
                "Work through the activity objectives and check the relevant panels as needed.",
                "You're ready to handle the mountain on your own.",
                "Expert lesson complete.");
        }

        private static void UpsertInstructorDialogueBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<NpcDialogueBankSO>(ExistingInstructorDialogueBankPath);
            if (bank == null)
                bank = LoadOrCreateAsset<NpcDialogueBankSO>(GeneratedInstructorDialogueBankPath);

            var style = AssetDatabase.LoadAssetAtPath<NpcDialogueBubbleStyleSO>(TutorialDialogueStylePath);

            UpsertDialogueLine(bank, new NpcDialogueLine
            {
                id = "instructor_pre_lesson_skate",
                topic = "instructor.pre_lesson",
                trigger = NpcDialogueTrigger.PlayerNearby,
                priority = 18,
                text = "Alternate left and right to skate forward.",
                duration = 3.25f,
                cooldown = 0f,
                showSpeakerName = false,
                preferImportantStyle = false,
                importance = NpcDialogueImportance.Tutorial,
                styleOverride = style
            });

            UpsertDialogueLine(bank, new NpcDialogueLine
            {
                id = "instructor_pre_lesson_lean",
                topic = "instructor.pre_lesson",
                trigger = NpcDialogueTrigger.PlayerNearby,
                priority = 18,
                text = "Lean forward to build speed, then lean back to settle yourself.",
                duration = 3.75f,
                cooldown = 0f,
                showSpeakerName = false,
                preferImportantStyle = false,
                importance = NpcDialogueImportance.Tutorial,
                styleOverride = style
            });

            UpsertDialogueLine(bank, new NpcDialogueLine
            {
                id = "instructor_pre_lesson_talk",
                topic = "instructor.pre_lesson",
                trigger = NpcDialogueTrigger.PlayerNearby,
                priority = 20,
                text = "Come talk to me when you're ready for a ski lesson.",
                duration = 3.5f,
                cooldown = 0f,
                showSpeakerName = false,
                preferImportantStyle = false,
                importance = NpcDialogueImportance.Interaction,
                styleOverride = style
            });

            UpsertDialogueLine(bank, new NpcDialogueLine
            {
                id = "instructor_pre_lesson_choose",
                topic = "instructor.pre_lesson",
                trigger = NpcDialogueTrigger.Ambient,
                priority = 17,
                text = "Start with the beginner lesson if you want the full tour, or pick a harder lesson if you already know the basics.",
                duration = 4.25f,
                cooldown = 0f,
                showSpeakerName = false,
                preferImportantStyle = false,
                importance = NpcDialogueImportance.Tutorial,
                styleOverride = style
            });
        }

        private static QuestDefinitionSO FindQuest(Dictionary<string, QuestDefinitionSO> questsById, string questId)
        {
            if (questsById == null || string.IsNullOrWhiteSpace(questId))
                return null;

            questsById.TryGetValue(questId, out var quest);
            return quest;
        }

        private static void CreateOrUpdateQuestOffer(
            string assetPath,
            QuestDefinitionSO questDefinition,
            string offerTitle,
            string offerDialogue,
            string acceptedDialogue,
            string inProgressDialogue,
            string completedDialogue,
            string turnInDialogue)
        {
            if (questDefinition == null)
                return;

            var offer = LoadOrCreateAsset<NpcQuestOfferSO>(assetPath);
            var serializedObject = new SerializedObject(offer);

            serializedObject.FindProperty("questDefinition").objectReferenceValue = questDefinition;
            serializedObject.FindProperty("offerTitle").stringValue = offerTitle ?? string.Empty;
            serializedObject.FindProperty("offerDialogue").stringValue = offerDialogue ?? string.Empty;
            serializedObject.FindProperty("acceptedDialogue").stringValue = acceptedDialogue ?? string.Empty;
            serializedObject.FindProperty("inProgressDialogue").stringValue = inProgressDialogue ?? string.Empty;
            serializedObject.FindProperty("completedDialogue").stringValue = completedDialogue ?? string.Empty;
            serializedObject.FindProperty("turnInDialogue").stringValue = turnInDialogue ?? string.Empty;
            serializedObject.FindProperty("autoTrackOnAccept").boolValue = true;
            serializedObject.FindProperty("showNavigationTarget").boolValue = true;
            serializedObject.FindProperty("requiredCompletedQuests").ClearArray();
            serializedObject.FindProperty("blockedByCompletedQuests").ClearArray();

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(offer);
        }

        private static void UpsertDialogueLine(NpcDialogueBankSO bank, NpcDialogueLine line)
        {
            if (bank == null || line == null || string.IsNullOrWhiteSpace(line.id))
                return;

            var serializedObject = new SerializedObject(bank);
            var lines = serializedObject.FindProperty("lines");
            int index = FindDialogueLineIndex(lines, line.id);
            if (index < 0)
            {
                index = lines.arraySize;
                lines.InsertArrayElementAtIndex(index);
            }

            var element = lines.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = line.id ?? string.Empty;
            element.FindPropertyRelative("topic").stringValue = line.topic ?? string.Empty;
            element.FindPropertyRelative("trigger").enumValueIndex = (int)line.trigger;
            element.FindPropertyRelative("priority").intValue = line.priority;
            element.FindPropertyRelative("text").stringValue = line.text ?? string.Empty;
            element.FindPropertyRelative("duration").floatValue = Mathf.Max(0.25f, line.duration);
            element.FindPropertyRelative("cooldown").floatValue = Mathf.Max(0f, line.cooldown);
            element.FindPropertyRelative("showSpeakerName").boolValue = line.showSpeakerName;
            element.FindPropertyRelative("preferImportantStyle").boolValue = line.preferImportantStyle;
            element.FindPropertyRelative("importance").enumValueIndex = (int)line.importance;
            element.FindPropertyRelative("styleOverride").objectReferenceValue = line.styleOverride;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);
        }

        private static int FindDialogueLineIndex(SerializedProperty lines, string lineId)
        {
            if (lines == null || string.IsNullOrWhiteSpace(lineId))
                return -1;

            for (int i = 0; i < lines.arraySize; i++)
            {
                var element = lines.GetArrayElementAtIndex(i);
                string existingId = element.FindPropertyRelative("id").stringValue;
                if (string.Equals(existingId, lineId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static QuestDefinitionSO CreateQuest(string assetPath, string id, string title, string description, params QuestStageDefinition[] stages)
        {
            var quest = LoadOrCreateAsset<QuestDefinitionSO>(assetPath);
            quest.id = id;
            quest.title = title;
            quest.description = description;
            quest.autoAccept = false;
            quest.tutorialQuest = true;
            quest.stages = stages != null ? new List<QuestStageDefinition>(stages) : new List<QuestStageDefinition>();
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static QuestStageDefinition Stage(string id, string title, string description, params QuestObjectiveDefinition[] objectives)
        {
            return new QuestStageDefinition
            {
                id = id,
                title = title,
                description = description,
                objectives = objectives != null ? new List<QuestObjectiveDefinition>(objectives) : new List<QuestObjectiveDefinition>()
            };
        }

        private static QuestObjectiveDefinition UseInput(string id, string title, QuestInputActionType action, int count, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.UseInput,
                inputAction = action,
                requiredCount = Mathf.Max(1, count)
            };
        }

        private static QuestObjectiveDefinition Movement(string id, string title, QuestMovementSkillType skill, int count, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.PerformMovementSkill,
                movementSkill = skill,
                requiredCount = Mathf.Max(1, count)
            };
        }

        private static QuestObjectiveDefinition Travel(string id, string title, QuestDistanceStatType stat, float target, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.TravelDistance,
                distanceStat = stat,
                comparison = QuestComparisonOp.GreaterOrEqual,
                targetValue = target
            };
        }

        private static QuestObjectiveDefinition State(string id, string title, QuestStateTargetType state, bool requiredValue, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.ReachState,
                stateTarget = state,
                advancedCondition = new QuestObjectiveCondition
                {
                    kind = QuestConditionKind.StateEquals,
                    key = QuestSignalCatalog.GetStateKey(state),
                    stateValueKind = QuestValueKind.Bool,
                    boolValue = requiredValue
                }
            };
        }

        private static QuestObjectiveDefinition Activity(string id, string title, QuestActivityTargetType activity, int count, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.Activity,
                activityTarget = activity,
                requiredCount = Mathf.Max(1, count)
            };
        }

        private static QuestObjectiveDefinition Trick(string id, string title, QuestTrickTargetType trick, int count, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.Trick,
                trickTarget = trick,
                requiredCount = Mathf.Max(1, count)
            };
        }

        private static QuestObjectiveDefinition Interaction(string id, string title, QuestInteractionTargetType interaction, int count, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.Interaction,
                interactionTarget = interaction,
                requiredCount = Mathf.Max(1, count)
            };
        }

        private static QuestObjectiveDefinition Ui(string id, string title, QuestUiScreenTargetType uiTarget, int count = 1, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.OpenUiScreen,
                uiTarget = uiTarget,
                requiredCount = Mathf.Max(1, count)
            };
        }

        private static QuestObjectiveDefinition Compound(string id, string title, string description, QuestConditionKind mode, params QuestObjectiveDefinition[] children)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.Compound,
                compoundMode = mode,
                compoundObjectives = children != null ? new List<QuestObjectiveDefinition>(children) : new List<QuestObjectiveDefinition>()
            };
        }

        private static QuestObjectiveDefinition Optional(QuestObjectiveDefinition objective)
        {
            if (objective != null)
                objective.optional = true;

            return objective;
        }

        private static void UpsertGeneratedQuests(QuestCatalogSO catalog, List<QuestDefinitionSO> generated)
        {
            if (catalog == null)
                return;

            catalog.quests ??= new List<QuestDefinitionSO>();
            var generatedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < generated.Count; i++)
            {
                var generatedQuest = generated[i];
                if (generatedQuest != null)
                    generatedIds.Add(generatedQuest.SafeId);
            }

            for (int i = catalog.quests.Count - 1; i >= 0; i--)
            {
                var existing = catalog.quests[i];
                if (existing == null)
                    continue;

                if (!existing.tutorialQuest)
                    continue;

                if (!existing.SafeId.StartsWith("tutorial.", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!generatedIds.Contains(existing.SafeId))
                    catalog.quests.RemoveAt(i);
            }

            for (int i = 0; i < generated.Count; i++)
            {
                var quest = generated[i];
                if (quest == null)
                    continue;

                bool found = false;
                for (int q = 0; q < catalog.quests.Count; q++)
                {
                    if (catalog.quests[q] == quest)
                    {
                        found = true;
                        break;
                    }

                    if (catalog.quests[q] != null && string.Equals(catalog.quests[q].SafeId, quest.SafeId, StringComparison.OrdinalIgnoreCase))
                    {
                        catalog.quests[q] = quest;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    catalog.quests.Add(quest);
            }

            EditorUtility.SetDirty(catalog);
        }

        private static bool RetainTutorialQuestIds(QuestCatalogSO catalog, HashSet<string> allowedIds)
        {
            if (catalog == null || catalog.quests == null)
                return false;

            bool changed = false;
            for (int i = catalog.quests.Count - 1; i >= 0; i--)
            {
                var quest = catalog.quests[i];
                if (quest == null || !quest.tutorialQuest)
                    continue;

                if (!quest.SafeId.StartsWith("tutorial.", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (allowedIds != null && allowedIds.Contains(quest.SafeId))
                    continue;

                catalog.quests.RemoveAt(i);
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(catalog);

            return changed;
        }

        private static void ValidateGeneratedQuests(List<QuestDefinitionSO> generated)
        {
            if (generated == null)
                return;

            for (int i = 0; i < generated.Count; i++)
            {
                QuestDefinitionSO quest = generated[i];
                if (quest == null)
                    continue;

                string questId = quest.SafeId;

                if (string.IsNullOrWhiteSpace(quest.title))
                    Warn(questId, string.Empty, "Empty quest title");

                WarnHardcodedBindings(questId, string.Empty, quest.title);
                WarnHardcodedBindings(questId, string.Empty, quest.description);

                if (quest.stages == null)
                    continue;

                for (int s = 0; s < quest.stages.Count; s++)
                {
                    QuestStageDefinition stage = quest.stages[s];
                    if (stage == null)
                        continue;

                    string stageId = string.IsNullOrWhiteSpace(stage.id) ? $"stage[{s}]" : stage.id;
                    WarnHardcodedBindings(questId, stageId, stage.title);
                    WarnHardcodedBindings(questId, stageId, stage.description);

                    if (stage.objectives == null)
                        continue;

                    for (int o = 0; o < stage.objectives.Count; o++)
                        ValidateObjectiveRecursive(questId, stage.objectives[o], true);
                }
            }
        }

        private static void ValidateObjectiveRecursive(string questId, QuestObjectiveDefinition objective, bool visible)
        {
            if (objective == null)
                return;

            string objectiveId = objective.id ?? string.Empty;
            bool objectiveVisible = visible && !objective.hiddenUntilAvailable;

            if (objectiveVisible && string.IsNullOrWhiteSpace(objective.title))
                Warn(questId, objectiveId, "Empty visible objective title");

            if (objectiveVisible && ExpectsDescription(objective) && string.IsNullOrWhiteSpace(objective.description))
                Warn(questId, objectiveId, "Empty visible objective description");

            WarnHardcodedBindings(questId, objectiveId, objective.title);
            WarnHardcodedBindings(questId, objectiveId, objective.description);

            QuestObjectiveCondition condition = objective.BuildCondition();
            ValidateConditionRecursive(questId, objectiveId, condition);

            string prompt = objective.BuildPromptLabel();
            if (ContainsRepeatedPromptFragment(prompt) ||
                (!string.IsNullOrWhiteSpace(prompt) &&
                 prompt.IndexOf("PRESS E, THEN PRESS E", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                WarnRepeatedPrompt(questId, objectiveId, prompt);
            }

            if (objective.compoundObjectives == null)
                return;

            for (int i = 0; i < objective.compoundObjectives.Count; i++)
                ValidateObjectiveRecursive(questId, objective.compoundObjectives[i], objectiveVisible);
        }

        private static void WarnHardcodedBindings(string questId, string ownerId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (ContainsBindingPlaceholder(text))
                return;

            if (!ContainsLikelyHardcodedBinding(text))
                return;

            Debug.LogWarning($"[TutorialQuestGenerator] Hardcoded input binding in {questId} / {ownerId}: {text}");
        }

        private static bool ContainsBindingPlaceholder(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   text.IndexOf("{input:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsLikelyHardcodedBinding(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string padded = $" {text} ";

            string[] phrases =
            {
                "Press W",
                "Press S",
                "Press A",
                "Press D",
                "Press Q",
                "Press E",
                "Hold W",
                "Hold S",
                "Hold A",
                "Hold D",
                "Hold Q",
                "Hold E",
                "Left Shift",
                "Left Ctrl",
                "Right Shift",
                "Right Ctrl",
                "Space",
                "PRESS W",
                "PRESS S",
                "PRESS A",
                "PRESS D",
                "PRESS Q",
                "PRESS E",
                "HOLD Q"
            };

            for (int i = 0; i < phrases.Length; i++)
            {
                if (padded.IndexOf(phrases[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            string[] standaloneKeys = { " W ", " S ", " A ", " D ", " Q ", " E " };
            for (int i = 0; i < standaloneKeys.Length; i++)
            {
                if (padded.IndexOf(standaloneKeys[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static void ValidateConditionRecursive(string questId, string objectiveId, QuestObjectiveCondition condition)
        {
            if (condition == null)
                return;

            if (ConditionExpectsKey(condition.kind) && string.IsNullOrWhiteSpace(condition.key))
                Warn(questId, objectiveId, "Empty/missing generated signal key");

            if (condition.children == null)
                return;

            for (int i = 0; i < condition.children.Count; i++)
                ValidateConditionRecursive(questId, objectiveId, condition.children[i]);
        }

        private static bool ConditionExpectsKey(QuestConditionKind kind)
        {
            return kind == QuestConditionKind.EventCount ||
                   kind == QuestConditionKind.StateEquals ||
                   kind == QuestConditionKind.StatThreshold ||
                   kind == QuestConditionKind.StatAccumulate ||
                   kind == QuestConditionKind.TrickRuleEventCount;
        }

        private static bool ExpectsDescription(QuestObjectiveDefinition objective)
        {
            return objective != null && !objective.hiddenUntilAvailable;
        }

        private static bool ContainsRepeatedPromptFragment(string prompt)
        {
            var fragments = SplitPromptFragments(prompt);
            for (int i = 1; i < fragments.Count; i++)
            {
                if (string.Equals(fragments[i], fragments[i - 1], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> SplitPromptFragments(string prompt)
        {
            var fragments = new List<string>();
            if (string.IsNullOrWhiteSpace(prompt))
                return fragments;

            string normalized = prompt.Replace(", THEN ", ",");
            string[] parts = normalized.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i]?.Trim() ?? string.Empty;
                if (part.StartsWith("THEN ", StringComparison.OrdinalIgnoreCase))
                    part = part.Substring(5).Trim();

                if (!string.IsNullOrWhiteSpace(part))
                    fragments.Add(part);
            }

            return fragments;
        }

        private static void Warn(string questId, string objectiveId, string message)
        {
            Debug.LogWarning($"[TutorialQuestGenerator] {message} in {questId} / {objectiveId}");
        }

        private static void WarnRepeatedPrompt(string questId, string objectiveId, string prompt)
        {
            Debug.LogWarning($"[TutorialQuestGenerator] Repeated prompt fragment in {questId} / {objectiveId}: {prompt}");
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureCoreFolders()
        {
            EnsureFolder("Assets", "1 SKI GAME");
            EnsureFolder("Assets/1 SKI GAME", "Data");
            EnsureFolder(RootFolder, "Quests");
            EnsureFolder(QuestFolder, "Tutorial");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static QuestDefinitionSO CreateMoveAndLeanQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_01_MoveAndLean.asset",
                "tutorial.move_and_lean",
                "Move and Lean",
                "Start by feeling how forward and backward lean shape your momentum on the snow.",
                Stage(
                    "tutorial.move_and_lean.stage1",
                    "Learn Your Weight Shift",
                    "Lean forward to build speed, then lean back to feel how your stance calms the skis down.",
                    Compound(
                        "tutorial.move_and_lean.travel",
                        "Start moving",
                        "Lead with forward lean, then keep gliding until the skis start carrying you naturally.",
                        QuestConditionKind.AllOf,
                        UseInput("tutorial.move_and_lean.forward", "Lean forward once", QuestInputActionType.LeanForward, 1, "Lean into the slope and start the slide."),
                        Travel("tutorial.move_and_lean.distance", "Glide 12m", QuestDistanceStatType.GroundDistance, 12f, "Keep moving until the skis carry you a short distance.")),
                    UseInput("tutorial.move_and_lean.backward", "Lean back to settle your speed", QuestInputActionType.LeanBackward, 1, "Shift your weight back and feel the skis calm down.")));
        }

        private static QuestDefinitionSO CreateTurnAndBrakeQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_02_TurnAndBrake.asset",
                "tutorial.turn_and_brake",
                "Turn and Brake",
                "Single-ski pressure steers your skis. Both ski inputs together become one of your main braking tools.",
                Stage(
                    "tutorial.turn_and_brake.stage1",
                    "One Pair of Inputs, Three Uses",
                    "Use single-ski pressure to carve, both skis together to wedge, and backward lean plus a hard turn to stop quickly.",
                    Travel("tutorial.turn_and_brake.turn", "Carve across the slope", QuestDistanceStatType.TurnTime, 1f, "Apply single-ski pressure while moving to arc across the slope and feel the edge bite."),
                    Movement("tutorial.turn_and_brake.wedge", "Wedge to brake", QuestMovementSkillType.Wedge, 1, "Use both ski inputs together to point the skis inward and brake harder."),
                    Movement("tutorial.turn_and_brake.quickstop", "Quick stop", QuestMovementSkillType.QuickStop, 1, "Build a little speed, then combine backward lean with a sharp turn to scrub speed fast.")));
        }

        private static QuestDefinitionSO CreateSkateAndPolesQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_03_SkateAndPoles.asset",
                "tutorial.skate_and_poles",
                "Skate and Poles",
                "When gravity is not helping, use rhythmic ski pressure or push with the poles.",
                Stage(
                    "tutorial.skate_and_poles.stage1",
                    "Create Speed on Flats",
                    "Skating and pole pushes help you accelerate when the slope is too gentle to do it for you.",
                    Movement("tutorial.skate_and_poles.skate", "Skate forward", QuestMovementSkillType.SkateSwitch, 4, "Alternate ski pressure rhythmically to push yourself forward."),
                    UseInput("tutorial.skate_and_poles.push", "Push with poles", QuestInputActionType.PolePush, 1, "Use your poles on flat ground to add another burst of speed."),
                    UseInput("tutorial.skate_and_poles.drag", "Drag your poles to resist speed", QuestInputActionType.PoleDrag, 1, "Use your poles while moving to add gentle resistance.")));
        }

        private static QuestDefinitionSO CreateSpeedControlQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_04_SpeedControl.asset",
                "tutorial.speed_control",
                "Speed Control",
                "Speed control is a toolkit: forward lean, skating, and tuck build speed; backward lean, wedge, and quick stop bleed it away.",
                Stage(
                    "tutorial.speed_control.stage1",
                    "Use the Fastest Option for the Moment",
                    "On steeper terrain, tuck is the cleanest way to hold speed without extra ski movement.",
                    UseInput("tutorial.speed_control.tuck", "Tuck to carry speed", QuestInputActionType.Tuck, 1, "Tuck while moving to reduce drag and hold your speed longer.")));
        }

        private static QuestDefinitionSO CreateJumpAndAirQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_05_JumpAndAir.asset",
                "tutorial.jump_and_air",
                "Jump and Air Control",
                "The same body controls still matter in the air. They help you stay balanced before landing.",
                Stage(
                    "tutorial.jump_and_air.stage1",
                    "Stay Deliberate in the Air",
                    "Jump first, then keep using body and ski inputs so the landing feels controlled instead of lucky.",
                    Movement("tutorial.jump_and_air.jump_adjust", "Jump and adjust in the air", QuestMovementSkillType.JumpAdjust, 1, "Jump, then use body and ski inputs in the air to steady yourself before landing."),
                    UseInput("tutorial.jump_and_air.pose", "Use an air pose", QuestInputActionType.AirPose, 1, "Use your air-pose input while airborne to trigger an air pose.")));
        }

        private static QuestDefinitionSO CreateStackRecoveryQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_06_StackRecovery.asset",
                "tutorial.stack_recovery",
                "Recover from a Stack",
                "If you topple into a stacked state, switch to walking, reset yourself, and put the skis back on.",
                Stage(
                    "tutorial.stack_recovery.stage1",
                    "Reset Yourself",
                    "A stack means you are no longer in a clean skiing stance. Remove the skis, get upright, then put them back on.",
                    Interaction("tutorial.stack_recovery.stacked", "Stack once", QuestInteractionTargetType.Stacked, 1, "Crash or land awkwardly enough to topple into a stacked state."),
                    Interaction("tutorial.stack_recovery.unequip", "Remove your skis", QuestInteractionTargetType.UnequipSkis, 1, "Enter walking mode and reset your orientation."),
                    Interaction("tutorial.stack_recovery.equip", "Put your skis back on", QuestInteractionTargetType.EquipSkis, 1, "Once you are upright again, re-equip your skis.")));
        }

        private static QuestDefinitionSO CreateResortRecoveryQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_07_ResortRecovery.asset",
                "tutorial.resort_recovery",
                "Visit the Resort",
                "The resort is where you regroup, recover, and access the mountain's support services.",
                Stage(
                    "tutorial.resort_recovery.stage1",
                    "Enter and Leave the Resort",
                    "Follow the beacon to the resort entrance, step inside, then head back out once you are ready to ski again.",
                    Interaction("tutorial.resort_recovery.enter", "Enter the resort", QuestInteractionTargetType.EnterResort, 1, "Follow the beacon to the resort entrance and step inside."),
                    Interaction("tutorial.resort_recovery.exit", "Leave the resort", QuestInteractionTargetType.ExitResort, 1, "When you are ready, head back out of the resort to return to the mountain.")));
        }

        private static QuestDefinitionSO CreateOverlayBasicsQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_08_OverlayBasics.asset",
                "tutorial.overlay_basics",
                "Mountain HUD Basics",
                "The overlay is where you plan your next move. Start with Quests, then branch into Stats and Map.",
                Stage(
                    "tutorial.overlay_basics.stage1",
                    "Use the Overlay to Plan",
                    "Open the overlay, check what you should do next, then use the map and stats to support that plan.",
                    Ui("tutorial.overlay_basics.open", "Open the Mountain Overlay", QuestUiScreenTargetType.OverlayOpened, description: "Use your overlay input to open the full mountain HUD."),
                    Ui("tutorial.overlay_basics.quests", "View the Quests tab", QuestUiScreenTargetType.QuestsViewed, description: "The Quests tab is the default planning view. Use it to see what is tracked right now."),
                    Ui("tutorial.overlay_basics.map", "View the Map tab", QuestUiScreenTargetType.MapViewed, description: "Open the map to see where lifts, services, and guided objectives are located."),
                    Ui("tutorial.overlay_basics.stats", "View the Stats tab", QuestUiScreenTargetType.StatsViewed, description: "Open Stats to review how your run is developing.")));
        }

        private static QuestDefinitionSO CreateResortServicesQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_09_ResortServices.asset",
                "tutorial.resort_services",
                "Resort Services",
                "The resort is your logistics hub: quests, tasks, rewards, gear, and passes all connect through it.",
                Stage(
                    "tutorial.resort_services.stage1",
                    "Use the Resort Hub",
                    "Learn the parts of the resort that keep your progression moving.",
                    Ui("tutorial.resort_services.tasks", "View the Tasks tab", QuestUiScreenTargetType.TasksViewed, description: "Open Goals > Tasks to inspect current task ladders and available rewards."),
                    Ui("tutorial.resort_services.claim", "Claim a task reward", QuestUiScreenTargetType.TaskRewardClaimed, description: "If a task reward is ready, claim it from the Tasks tab."),
                    Ui("tutorial.resort_services.shop", "Open the customisation shop", QuestUiScreenTargetType.ShopOpened, description: "Follow the beacon to the customisation shop and enter."),
                    Compound(
                        "tutorial.resort_services.pass",
                        "Get your ski pass",
                        "A ski pass is your key to the lift network.",
                        QuestConditionKind.AllOf,
                        Ui("tutorial.resort_services.kiosk", "Open the ski pass kiosk", QuestUiScreenTargetType.KioskOpened, description: "Follow the beacon to the ski pass kiosk and open it."),
                        Interaction("tutorial.resort_services.claim_default", "Claim the default pass", QuestInteractionTargetType.ClaimDefaultPass, 1, "Claim the free default pass so lifts become available."))));
        }

        private static QuestDefinitionSO CreateLiftBasicsQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_10_LiftBasics.asset",
                "tutorial.lift_basics",
                "Lift Basics",
                "Now that you have a pass, use the lift to move uphill instead of hiking or skating back up.",
                Stage(
                    "tutorial.lift_basics.stage1",
                    "Ride the Lift",
                    "Follow the lift route, board at the base, then step off smoothly at the top station.",
                    Interaction("tutorial.lift_basics.mount", "Board a lift", QuestInteractionTargetType.MountLift, 1, "Follow the beacon to the base station and board the lift."),
                    Interaction("tutorial.lift_basics.dismount", "Dismount at the top", QuestInteractionTargetType.DismountLift, 1, "Step off cleanly when you reach the top station.")));
        }

        private static QuestDefinitionSO CreateRaceIntroQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_11_RaceIntro.asset",
                "tutorial.race_intro",
                "Race Introduction",
                "Races start through the kiosk, then continue through the world at the gate and on-course markers.",
                Stage(
                    "tutorial.race_intro.stage1",
                    "Start Your First Race",
                    "Open the signup kiosk, then follow the route to the gate and finish the course cleanly.",
                    Ui("tutorial.race_intro.kiosk", "Open the race kiosk", QuestUiScreenTargetType.RaceKioskOpened, description: "Follow the race beacon to the signup kiosk and open it."),
                    Activity("tutorial.race_intro.start", "Start a race", QuestActivityTargetType.RaceStarted, 1, "Sign up, then follow the guidance to the race gate and begin."),
                    Activity("tutorial.race_intro.complete", "Finish the race", QuestActivityTargetType.RaceCompleted, 1, "Stay on course and cross the finish line to complete your first race.")));
        }

        private static QuestDefinitionSO CreateRescueIntroQuest()
        {
            return CreateQuest(
                $"{TutorialFolder}/Quest_Tutorial_12_RescueIntro.asset",
                "tutorial.rescue_intro",
                "Rescue Introduction",
                "Rescues start from the medic tent, then hand you over to mission guidance in the world.",
                Stage(
                    "tutorial.rescue_intro.stage1",
                    "Take a Rescue Dispatch",
                    "Visit the medic tent, accept a dispatch, then follow the rescue guidance to completion.",
                    Activity("tutorial.rescue_intro.start", "Visit the medic tent and start a rescue mission", QuestActivityTargetType.RescueStarted, 1, "Follow the medic tent beacon, interact with the tent, and accept a rescue mission."),
                    Activity("tutorial.rescue_intro.complete", "Complete the rescue mission", QuestActivityTargetType.RescueCompleted, 1, "Use the mission guidance to find the casualty, finish the task, and return successfully.")));
        }
    }
}
