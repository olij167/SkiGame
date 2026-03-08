using UnityEngine;
using UnityEngine.SceneManagement;
using TimeWeather;
using SkiGame.Runs;

namespace SkiGame.Progression
{
    public sealed class GameStartBootstrap : MonoBehaviour
    {
        private static GameStartBootstrap _instance;
        public static GameStartBootstrap Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ✅ NEW: only apply when the user explicitly requested Load/New
        public static bool HasPendingRequest { get; private set; }
        public static bool PendingNewGame { get; private set; }

        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "Game";
        [SerializeField] private string resortSpawnTag = "ResortSpawn";

        [Header("New Game Start Date")]
        [SerializeField] private int startYear = 1;
        [SerializeField] private int startMonthIndex = 0;
        [SerializeField] private int startDayOfMonth = 1;
        [SerializeField] private float startTimeOfDay = 9.0f;

        [Header("Defaults")]
        [SerializeField] private CustomizationCatalogSO customizationCatalog;

        [Header("New Game - Default Appearance")]
        [SerializeField] private Color defaultSkinColor = Color.yellowNice;
        [SerializeField] private Color defaultEyeColor = Color.black;

        public static void RequestNewGame(int slotId)
        {
            GameSaveSystem.ActiveSlotId = slotId;
            PendingNewGame = true;
            HasPendingRequest = true;
        }

        public static void RequestLoadGame(int slotId)
        {
            GameSaveSystem.ActiveSlotId = slotId;
            PendingNewGame = false;
            HasPendingRequest = true;
        }

        /// <summary>
        /// Call this when the Game scene is already loaded (e.g. loaded additively for menu background).
        /// </summary>
        public static void ApplyPendingNowIfReady()
        {
            if (!HasPendingRequest) return;
            if (Instance == null) return;

            var gameplayScene = SceneManager.GetSceneByName(Instance.gameplaySceneName);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded) return;

            Instance.ApplyPending(gameplayScene);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != gameplaySceneName) return;

            // ✅ Gate so loading Game additively for menu background does NOT apply save state.
            if (!HasPendingRequest) return;

            ApplyPending(scene);
        }

        private void ApplyPending(Scene gameplayScene)
        {
            var statsMgr = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance
                : FindObjectOfType<PlayerStatsManager>();

            if (statsMgr == null)
            {
                Debug.LogWarning("[GameStartBootstrap] PlayerStatsManager not found in gameplay scene.");
                return;
            }

            if (PendingNewGame)
                DoNewGame(statsMgr);
            else
                DoLoadGame(statsMgr);

            // ✅ Profile is now loaded/applied. Enable pause menu gating.
            NotifyPauseMenuProfileLoaded();

            PendingNewGame = false;
            HasPendingRequest = false;
        }

        private void DoNewGame(PlayerStatsManager statsMgr)
        {
            GameSaveSystem.DeleteSlot(GameSaveSystem.ActiveSlotId);

            statsMgr.Load();   // LoadOrCreate
            statsMgr.ResetAll();

            PlayerPrefs.DeleteKey("skigame.runs.calendarBaseDay");

            RunProgressTracker.ClearAllPersistentDataForSlot(GameSaveSystem.ActiveSlotId);

            var tc = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (tc != null)
            {
                var ts = new TimeState
                {
                    year = startYear,
                    monthIndex = startMonthIndex,
                    dayOfMonth = startDayOfMonth,
                    dayCount = 0,
                    timeOfDay = startTimeOfDay
                };

                TimeStateStorage.Apply(tc, ts);

                // ✅ Ensure tc.dayCount is correct for the applied month/day (and sets currentDay)
                tc.SyncDayCountFromDate();

                statsMgr.Profile.playthrough ??= new PlaythroughState();
                statsMgr.Profile.playthrough.playthroughId += 1;
                statsMgr.Profile.playthrough.startedUtc = DateTimeUtc.Now();

                // ✅ Baseline should be the *applied* start dayCount (after sync)
                statsMgr.Profile.playthrough.calendarStartDayOfYear = tc.dayCount;
                statsMgr.Profile.playthrough.calendarStartYear = tc.currentYear;

                TimeStateStorage.Save(TimeStateStorage.Capture(tc), GameSaveSystem.GetTimePath(GameSaveSystem.ActiveSlotId));

                // ✅ Force UI baseline refresh
                PostApplyTimeAndUIRefresh(tc);
            }

            ApplyDefaultCustomization(statsMgr.Profile);

            statsMgr.Save();
            GameSaveSystem.MarkSlotPlayed(GameSaveSystem.ActiveSlotId);

            TryMovePlayerToResortSpawn();
        }

        private void DoLoadGame(PlayerStatsManager statsMgr)
        {
            statsMgr.Load();

            var tc = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (tc != null)
            {
                var ts = TimeStateStorage.LoadOrNull(GameSaveSystem.GetTimePath(GameSaveSystem.ActiveSlotId));
                if (ts != null)
                {
                    TimeStateStorage.Apply(tc, ts);
                    tc.SyncDayCountFromDate();
                }

                // ✅ Force UI baseline refresh
                PostApplyTimeAndUIRefresh(tc);
            }

            GameSaveSystem.MarkSlotPlayed(GameSaveSystem.ActiveSlotId);

            TryMovePlayerToResortSpawn();

        }

        private void ApplyDefaultCustomization(PlayerStatsProfile profile)
        {
            if (profile == null) return;

            profile.customization ??= new PlayerStatsProfile.CustomizationState();
            profile.customization.customizationInitialized = false;

            profile.customization.skinColor = defaultSkinColor;
            profile.customization.eyeColor = defaultEyeColor;

            // Treat as defaults (not user overrides)
            profile.customization.hasSetSkinColor = false;
            profile.customization.hasSetEyeColor = false;

            if (customizationCatalog == null) return;

            UnlockDefault(CustomizationOptionType.EyeIcon);
            UnlockDefault(CustomizationOptionType.Skis);
            UnlockDefault(CustomizationOptionType.Poles);
            UnlockDefault(CustomizationOptionType.Hat);
            UnlockDefault(CustomizationOptionType.Jacket);
            UnlockDefault(CustomizationOptionType.SkinPattern);

            void UnlockDefault(CustomizationOptionType type)
            {
                var enumerable = customizationCatalog.GetByType(type);
                if (enumerable == null) return;

                foreach (var o in enumerable)
                {
                    if (o == null) continue;
                    if (o.customizerIndex != 0) continue;
                    profile.customization.Unlock(o.id);
                    break;
                }
            }
        }

        private void TryMovePlayerToResortSpawn()
        {
            var spawn = GameObject.FindWithTag(resortSpawnTag);
            if (spawn == null) return;

            var player = GameObject.FindObjectOfType<SkiController>();
            if (player == null) return;

            player.transform.position = spawn.transform.position;
            player.transform.rotation = spawn.transform.rotation;
        }

        private void PostApplyTimeAndUIRefresh(TimeController tc)
        {
            if (tc != null)
            {
                // Ensure dayCount/day-of-week match the applied date (and refresh weather)
                tc.SyncDayCountFromDate();
            }

            // Runs page caches week baseline; force it to recompute from playthrough baseline.
            var runsPages = FindObjectsOfType<RunsPageUI>(true);
            for (int i = 0; i < runsPages.Length; i++)
                runsPages[i]?.ForceRefreshCalendarBaseline();
        }

        private void NotifyPauseMenuProfileLoaded()
        {
            // Include inactive because your pause menu UIDocument might start disabled.
            var pause = FindObjectOfType<PauseMenuController>(includeInactive: true);
            if (pause != null)
                pause.NotifyProfileLoaded();
        }
    }
}
