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
        private const string TutorialCatalogPath = "Assets/1 SKI GAME/Data/Quests/QuestCatalog_Tutorial.asset";
        private const string MainCatalogPath = "Assets/1 SKI GAME/Prefabs/QuestCatalog.asset";

        [MenuItem("Tools/Ski Game/Quests/Generate Tutorial Quest Assets")]
        public static void GenerateTutorialQuests()
        {
            EnsureFolder("Assets", "1 SKI GAME");
            EnsureFolder("Assets/1 SKI GAME", "Data");
            EnsureFolder(RootFolder, "Quests");
            EnsureFolder(QuestFolder, "Tutorial");

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
                CreateRescueIntroQuest(),
            };

            UpsertGeneratedQuests(tutorialCatalog, generated);

            var mainCatalog = AssetDatabase.LoadAssetAtPath<QuestCatalogSO>(MainCatalogPath);
            if (mainCatalog != null)
                UpsertGeneratedQuests(mainCatalog, generated);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = tutorialCatalog;
            Debug.Log(
                $"Generated {generated.Count} tutorial quest assets and updated {TutorialCatalogPath}" +
                (mainCatalog != null ? $" and {MainCatalogPath}" : string.Empty));
        }

        private static QuestDefinitionSO CreateMoveAndLeanQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_01_MoveAndLean",
                "tutorial.move_and_lean",
                "Move and Lean",
                "Start by feeling how W and S shape your momentum on the snow.",
                Stage(
                    "tutorial.move_and_lean.stage1",
                    "Learn Your Weight Shift",
                    "Use W to build speed, then use S to feel how your stance calms the skis back down.",
                    Compound(
                        "tutorial.move_and_lean.travel",
                        "Use W to start moving",
                        "Lead with forward lean, then keep gliding until the skis start carrying you naturally.",
                        QuestConditionKind.AllOf,
                        UseInput("tutorial.move_and_lean.forward", "Lean forward once", QuestInputActionType.LeanForward, 1, "Press W to lean into the slope and start the slide."),
                        Travel("tutorial.move_and_lean.distance", "Glide 12m", QuestDistanceStatType.GroundDistance, 12f, "Keep moving until the skis carry you a short distance.")),
                    UseInput("tutorial.move_and_lean.backward", "Lean back to settle your speed", QuestInputActionType.LeanBackward, 1, "Press S to shift your weight back and feel the skis calm down.")));
        }

        private static QuestDefinitionSO CreateTurnAndBrakeQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_02_TurnAndBrake",
                "tutorial.turn_and_brake",
                "Turn and Brake",
                "A and D steer your skis. Those same inputs also become your main braking tools.",
                Stage(
                    "tutorial.turn_and_brake.stage1",
                    "One Pair of Inputs, Three Uses",
                    "Use single-ski pressure to carve, both skis together to wedge, and S plus a hard turn to stop quickly.",
                    Travel("tutorial.turn_and_brake.turn", "Carve with A or D", QuestDistanceStatType.TurnTime, 1f, "Press A or D while moving to arc across the slope and feel the edge bite."),
                    Movement("tutorial.turn_and_brake.wedge", "Wedge to brake", QuestMovementSkillType.Wedge, 1, "Press A + D together to point the skis inward and brake harder."),
                    Movement("tutorial.turn_and_brake.quickstop", "Quick stop", QuestMovementSkillType.QuickStop, 1, "Build a little speed, then press S with a sharp A or D turn to scrub speed fast.")));
        }

        private static QuestDefinitionSO CreateSkateAndPolesQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_03_SkateAndPoles",
                "tutorial.skate_and_poles",
                "Skate and Poles",
                "When gravity is not helping, use your ski inputs rhythmically or push with the poles.",
                Stage(
                    "tutorial.skate_and_poles.stage1",
                    "Create Speed on Flats",
                    "Skating and pole pushes help you accelerate when the slope is too gentle to do it for you.",
                    Movement("tutorial.skate_and_poles.skate", "Skate forward", QuestMovementSkillType.SkateSwitch, 4, "Tap A and D in an alternating rhythm to push yourself forward."),
                    UseInput("tutorial.skate_and_poles.push", "Push with poles", QuestInputActionType.PolePush, 1, "Press Left Ctrl on flat ground to add another burst of speed."),
                    UseInput("tutorial.skate_and_poles.drag", "Drag your poles to resist speed", QuestInputActionType.PoleDrag, 1, "Use Left Ctrl while moving to drag the poles and gently slow yourself.")));
        }

        private static QuestDefinitionSO CreateSpeedControlQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_04_SpeedControl",
                "tutorial.speed_control",
                "Speed Control",
                "Speed control is a toolkit: W, skating, and tuck build speed; S, wedge, and quick stop bleed it away.",
                Stage(
                    "tutorial.speed_control.stage1",
                    "Use the Fastest Option for the Moment",
                    "On steeper terrain, tuck is the cleanest way to hold speed without extra ski movement.",
                    UseInput("tutorial.speed_control.tuck", "Tuck to carry speed", QuestInputActionType.Tuck, 1, "Press Left Shift while moving to reduce drag and hold your speed longer.")));
        }

        private static QuestDefinitionSO CreateJumpAndAirQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_05_JumpAndAir",
                "tutorial.jump_and_air",
                "Jump and Air Control",
                "The same body controls still matter in the air. They help you stay balanced before landing.",
                Stage(
                    "tutorial.jump_and_air.stage1",
                    "Stay Deliberate in the Air",
                    "Jump first, then keep using body and ski inputs so the landing feels controlled instead of lucky.",
                    Movement("tutorial.jump_and_air.jump_adjust", "Jump and adjust in the air", QuestMovementSkillType.JumpAdjust, 1, "Press Space, then use W or S with A or D in the air to steady yourself before landing."),
                    UseInput("tutorial.jump_and_air.pose", "Use an air pose", QuestInputActionType.AirPose, 1, "Press Left Ctrl while airborne to trigger an air pose.")));
        }

        private static QuestDefinitionSO CreateStackRecoveryQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_06_StackRecovery",
                "tutorial.stack_recovery",
                "Recover from a Stack",
                "If you topple into a stacked state, switch to walking, reset yourself, and put the skis back on.",
                Stage(
                    "tutorial.stack_recovery.stage1",
                    "Reset Yourself",
                    "A stack means you are no longer in a clean skiing stance. Remove the skis, get upright, then put them back on.",
                    Interaction("tutorial.stack_recovery.stacked", "Stack once", QuestInteractionTargetType.Stacked, 1, "Crash or land awkwardly enough to topple into a stacked state."),
                    Interaction("tutorial.stack_recovery.unequip", "Remove your skis", QuestInteractionTargetType.UnequipSkis, 1, "Hold Q to enter walking mode and reset your orientation."),
                    Interaction("tutorial.stack_recovery.equip", "Put your skis back on", QuestInteractionTargetType.EquipSkis, 1, "Once you are upright again, hold Q to re-equip your skis.")));
        }

        private static QuestDefinitionSO CreateResortRecoveryQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_07_ResortRecovery",
                "tutorial.resort_recovery",
                "Visit the Resort",
                "The resort is where you regroup, recover, and access the mountain's support services.",
                Stage(
                    "tutorial.resort_recovery.stage1",
                    "Enter and Leave the Resort",
                    "Follow the beacon to the resort entrance, step inside, then head back out once you are ready to ski again.",
                    Interaction("tutorial.resort_recovery.enter", "Enter the resort", QuestInteractionTargetType.EnterResort, 1, "Follow the beacon to the resort entrance and press E to step inside."),
                    Interaction("tutorial.resort_recovery.exit", "Leave the resort", QuestInteractionTargetType.ExitResort, 1, "When you are ready, head back out of the resort to return to the mountain.")));
        }

        private static QuestDefinitionSO CreateOverlayBasicsQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_08_OverlayBasics",
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
                "Quest_Tutorial_09_ResortServices",
                "tutorial.resort_services",
                "Resort Services",
                "The resort is your logistics hub: quests, tasks, rewards, gear, and passes all connect through it.",
                Stage(
                    "tutorial.resort_services.stage1",
                    "Use the Resort Hub",
                    "Learn the parts of the resort that keep your progression moving.",
                    Ui("tutorial.resort_services.tasks", "View the Tasks tab", QuestUiScreenTargetType.TasksViewed, description: "Open Goals > Tasks to inspect current task ladders and available rewards."),
                    Ui("tutorial.resort_services.claim", "Claim a task reward", QuestUiScreenTargetType.TaskRewardClaimed, description: "If a task reward is ready, claim it from the Tasks tab."),
                    Ui("tutorial.resort_services.shop", "Open the customisation shop", QuestUiScreenTargetType.ShopOpened, description: "Follow the beacon to the customisation shop and press E to enter."),
                    Compound(
                        "tutorial.resort_services.pass",
                        "Get your ski pass",
                        "A ski pass is your key to the lift network.",
                        QuestConditionKind.AllOf,
                        Ui("tutorial.resort_services.kiosk", "Open the ski pass kiosk", QuestUiScreenTargetType.KioskOpened, description: "Follow the beacon to the ski pass kiosk and press E to open it."),
                        Interaction("tutorial.resort_services.claim_default", "Claim the default pass", QuestInteractionTargetType.ClaimDefaultPass, 1, "Claim the free default pass so lifts become available."))));
        }

        private static QuestDefinitionSO CreateLiftBasicsQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_10_LiftBasics",
                "tutorial.lift_basics",
                "Lift Basics",
                "Now that you have a pass, use the lift to move uphill instead of hiking or skating back up.",
                Stage(
                    "tutorial.lift_basics.stage1",
                    "Ride the Lift",
                    "Follow the lift route, board at the base, then step off smoothly at the top station.",
                    Interaction("tutorial.lift_basics.mount", "Board a lift", QuestInteractionTargetType.MountLift, 1, "Follow the beacon to the base station and press E to board."),
                    Interaction("tutorial.lift_basics.dismount", "Dismount at the top", QuestInteractionTargetType.DismountLift, 1, "When you reach the top station, press E again to step off cleanly.")));
        }

        private static QuestDefinitionSO CreateRaceIntroQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_11_RaceIntro",
                "tutorial.race_intro",
                "Race Introduction",
                "Races start through the kiosk, then continue through the world at the gate and on-course markers.",
                Stage(
                    "tutorial.race_intro.stage1",
                    "Start Your First Race",
                    "Open the signup kiosk, then follow the route to the gate and finish the course cleanly.",
                    Ui("tutorial.race_intro.kiosk", "Open the race kiosk", QuestUiScreenTargetType.RaceKioskOpened, description: "Follow the race beacon to the signup kiosk and press E to open it."),
                    Activity("tutorial.race_intro.start", "Start a race", QuestActivityTargetType.RaceStarted, 1, "Sign up, then follow the guidance to the race gate and begin."),
                    Activity("tutorial.race_intro.complete", "Finish the race", QuestActivityTargetType.RaceCompleted, 1, "Stay on course and cross the finish line to complete your first race.")));
        }

        private static QuestDefinitionSO CreateRescueIntroQuest()
        {
            return CreateQuest(
                "Quest_Tutorial_12_RescueIntro",
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

        private static QuestDefinitionSO CreateQuest(string assetName, string id, string title, string description, params QuestStageDefinition[] stages)
        {
            var quest = LoadOrCreateAsset<QuestDefinitionSO>($"{TutorialFolder}/{assetName}.asset");
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

        private static QuestObjectiveDefinition Threshold(string id, string title, QuestThresholdStatType stat, QuestComparisonOp comparison, float target, string description = null)
        {
            return new QuestObjectiveDefinition
            {
                id = id,
                title = title,
                description = description,
                template = QuestObjectiveTemplate.ReachThreshold,
                thresholdStat = stat,
                comparison = comparison,
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

        private static void UpsertGeneratedQuests(QuestCatalogSO catalog, List<QuestDefinitionSO> generated)
        {
            if (catalog == null)
                return;

            catalog.quests ??= new List<QuestDefinitionSO>();
            var generatedIds = new HashSet<string>();

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

                if (!existing.SafeId.StartsWith("tutorial.", System.StringComparison.OrdinalIgnoreCase))
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

                    if (catalog.quests[q] != null && catalog.quests[q].SafeId == quest.SafeId)
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

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
