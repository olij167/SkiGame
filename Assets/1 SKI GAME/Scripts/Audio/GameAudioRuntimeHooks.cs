using System.Collections.Generic;
using SkiGame.Activities;
using SkiGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SkiGame.Audio
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class GameAudioRuntimeHooks : MonoBehaviour
    {
        private const string SilentClassName = "audio-silent";
        private const string BoundClassName = "audio-bound";
        private const string ConfirmClassName = "audio-confirm";
        private const string DenyClassName = "audio-deny";
        private const string BackClassName = "audio-back";

        public static GameAudioRuntimeHooks Instance { get; private set; }

        private readonly HashSet<int> _boundSkiControllers = new HashSet<int>();
        private readonly HashSet<int> _boundTrickTrackers = new HashSet<int>();
        private MountainActivityManager _boundActivityManager;
        private float _nextSceneScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
                return;

#if UNITY_2023_1_OR_NEWER
            GameAudioRuntimeHooks existing = FindAnyObjectByType<GameAudioRuntimeHooks>();
#else
            GameAudioRuntimeHooks existing = FindObjectOfType<GameAudioRuntimeHooks>();
#endif
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            GameObject root = new GameObject("[GameAudioRuntimeHooks]");
            Instance = root.AddComponent<GameAudioRuntimeHooks>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            LiftAccessPopupBus.OnPopup += OnLiftPopup;
            SkiPassWatchFeedbackBus.OnFeedback += OnWatchFeedback;
            CustomizationShopRuntime.OnOpenChanged += OnCustomizationOpenChanged;
            ScanSceneBindings(forceUiRebind: true);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LiftAccessPopupBus.OnPopup -= OnLiftPopup;
            SkiPassWatchFeedbackBus.OnFeedback -= OnWatchFeedback;
            CustomizationShopRuntime.OnOpenChanged -= OnCustomizationOpenChanged;

            if (_boundActivityManager != null)
            {
                _boundActivityManager.OnActivityStarted -= OnActivityStarted;
                _boundActivityManager.OnActivityCompleted -= OnActivityCompleted;
                _boundActivityManager.OnActivityFailed -= OnActivityFailed;
                _boundActivityManager.OnActivityCancelled -= OnActivityCancelled;
                _boundActivityManager = null;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextSceneScanTime)
                return;

            _nextSceneScanTime = Time.unscaledTime + 1f;
            ScanSceneBindings(forceUiRebind: false);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScanSceneBindings(forceUiRebind: true);
        }

        private void ScanSceneBindings(bool forceUiRebind)
        {
            BindActivityManager();
            BindPlayerEvents();
            BindUiDocuments();
        }

        private void BindActivityManager()
        {
            MountainActivityManager manager = MountainActivityManager.Instance;
            if (manager == _boundActivityManager || manager == null)
                return;

            if (_boundActivityManager != null)
            {
                _boundActivityManager.OnActivityStarted -= OnActivityStarted;
                _boundActivityManager.OnActivityCompleted -= OnActivityCompleted;
                _boundActivityManager.OnActivityFailed -= OnActivityFailed;
                _boundActivityManager.OnActivityCancelled -= OnActivityCancelled;
            }

            _boundActivityManager = manager;
            _boundActivityManager.OnActivityStarted += OnActivityStarted;
            _boundActivityManager.OnActivityCompleted += OnActivityCompleted;
            _boundActivityManager.OnActivityFailed += OnActivityFailed;
            _boundActivityManager.OnActivityCancelled += OnActivityCancelled;
        }

        private void BindPlayerEvents()
        {
#if UNITY_2023_1_OR_NEWER
            SkiController[] controllers = FindObjectsByType<SkiController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            SkierTrickTracker[] trackers = FindObjectsByType<SkierTrickTracker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            SkiController[] controllers = FindObjectsOfType<SkiController>();
            SkierTrickTracker[] trackers = FindObjectsOfType<SkierTrickTracker>();
#endif

            for (int i = 0; i < controllers.Length; i++)
            {
                SkiController controller = controllers[i];
                if (controller == null || !_boundSkiControllers.Add(controller.GetInstanceID()))
                    continue;

                controller.OnStacked += OnPlayerStacked;
                controller.OnRecoveredFromStack += OnPlayerRecovered;
            }

            for (int i = 0; i < trackers.Length; i++)
            {
                SkierTrickTracker tracker = trackers[i];
                if (tracker == null || !_boundTrickTrackers.Add(tracker.GetInstanceID()))
                    continue;

                tracker.OnTrickResolved += OnTrickResolved;
            }
        }

        private void BindUiDocuments()
        {
#if UNITY_2023_1_OR_NEWER
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            UIDocument[] documents = FindObjectsOfType<UIDocument>();
#endif

            for (int i = 0; i < documents.Length; i++)
            {
                UIDocument document = documents[i];
                if (document == null)
                    continue;

                VisualElement root = document.rootVisualElement;
                if (root == null)
                    continue;

                List<Button> buttons = root.Query<Button>().ToList();
                for (int j = 0; j < buttons.Count; j++)
                    BindButton(buttons[j]);

                List<Toggle> toggles = root.Query<Toggle>().ToList();
                for (int j = 0; j < toggles.Count; j++)
                    BindToggle(toggles[j]);

                List<Slider> sliders = root.Query<Slider>().ToList();
                for (int j = 0; j < sliders.Count; j++)
                    BindSlider(sliders[j]);

                List<SliderInt> intSliders = root.Query<SliderInt>().ToList();
                for (int j = 0; j < intSliders.Count; j++)
                    BindSliderInt(intSliders[j]);

                List<DropdownField> dropdowns = root.Query<DropdownField>().ToList();
                for (int j = 0; j < dropdowns.Count; j++)
                    BindDropdown(dropdowns[j]);
            }
        }

        private static void BindButton(Button button)
        {
            if (button == null || IsSilent(button) || button.ClassListContains(BoundClassName))
                return;

            GameAudioCueId cueId = ResolveButtonCue(button);
            button.clicked += () => GameAudio.PlayUi(cueId);
            button.AddToClassList(BoundClassName);
        }

        private static void BindToggle(Toggle toggle)
        {
            if (toggle == null || IsSilent(toggle) || toggle.ClassListContains(BoundClassName))
                return;

            toggle.RegisterValueChangedCallback(_ => GameAudio.PlayUi(GameAudioCueId.UiAdjust, 0.9f));
            toggle.AddToClassList(BoundClassName);
        }

        private static void BindSlider(Slider slider)
        {
            if (slider == null || IsSilent(slider) || slider.ClassListContains(BoundClassName))
                return;

            slider.RegisterValueChangedCallback(_ => GameAudio.PlayUi(GameAudioCueId.UiAdjust, 0.75f));
            slider.AddToClassList(BoundClassName);
        }

        private static void BindSliderInt(SliderInt slider)
        {
            if (slider == null || IsSilent(slider) || slider.ClassListContains(BoundClassName))
                return;

            slider.RegisterValueChangedCallback(_ => GameAudio.PlayUi(GameAudioCueId.UiAdjust, 0.75f));
            slider.AddToClassList(BoundClassName);
        }

        private static void BindDropdown(DropdownField dropdown)
        {
            if (dropdown == null || IsSilent(dropdown) || dropdown.ClassListContains(BoundClassName))
                return;

            dropdown.RegisterValueChangedCallback(_ => GameAudio.PlayUi(GameAudioCueId.UiNavigate, 0.85f));
            dropdown.AddToClassList(BoundClassName);
        }

        private static bool IsSilent(VisualElement element)
        {
            return element != null && element.ClassListContains(SilentClassName);
        }

        private static GameAudioCueId ResolveButtonCue(Button button)
        {
            if (button.ClassListContains(BackClassName))
                return GameAudioCueId.UiBack;

            if (button.ClassListContains(DenyClassName))
                return GameAudioCueId.UiDeny;

            if (button.ClassListContains(ConfirmClassName))
                return GameAudioCueId.UiConfirm;

            return GameAudioCueId.UiClick;
        }

        private static void OnPlayerStacked(SkiController.StackEventInfo info)
        {
            GameAudio.PlayWorld(GameAudioCueId.PlayerStack, info.position, Mathf.Lerp(0.8f, 1.2f, info.severity01));
        }

        private static void OnPlayerRecovered(SkiController.StackRecoveryEventInfo info)
        {
            GameAudio.PlayWorld(GameAudioCueId.PlayerRecover, info.position);
        }

        private static void OnTrickResolved(SkierTrickTracker.TrickResult result)
        {
            GameAudio.PlayUi(result.success ? GameAudioCueId.TrickLand : GameAudioCueId.TrickFail, Mathf.Lerp(0.9f, 1.15f, result.severity01));
        }

        private static void OnLiftPopup(LiftAccessPopupBus.PopupMessage message)
        {
            GameAudio.PlayUi(message.positive ? GameAudioCueId.LiftAccessGranted : GameAudioCueId.LiftAccessDenied);
        }

        private static void OnWatchFeedback(SkiPassWatchFeedbackBus.Feedback feedback)
        {
            GameAudio.PlayUi(feedback.allowed ? GameAudioCueId.LiftAccessGranted : GameAudioCueId.LiftAccessDenied, 0.7f);
        }

        private static void OnCustomizationOpenChanged(bool open)
        {
            GameAudio.PlayUi(open ? GameAudioCueId.ShopOpen : GameAudioCueId.ShopClose);
        }

        private static void OnActivityStarted(MountainActivityKind kind, MonoBehaviour source, string displayName, int variantNumber)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    GameAudio.PlayUi(GameAudioCueId.RaceStart);
                    break;
                case MountainActivityKind.Rescue:
                    GameAudio.PlayUi(GameAudioCueId.RescueStart);
                    break;
                default:
                    GameAudio.PlayUi(GameAudioCueId.InteractionAccept);
                    break;
            }
        }

        private static void OnActivityCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    GameAudio.PlayUi(GameAudioCueId.RaceFinish);
                    break;
                case MountainActivityKind.Rescue:
                    GameAudio.PlayUi(GameAudioCueId.RescueComplete);
                    break;
                default:
                    GameAudio.PlayUi(GameAudioCueId.UiConfirm);
                    break;
            }
        }

        private static void OnActivityFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    GameAudio.PlayUi(GameAudioCueId.RaceFail);
                    break;
                case MountainActivityKind.Rescue:
                    GameAudio.PlayUi(GameAudioCueId.RescueFail);
                    break;
                default:
                    GameAudio.PlayUi(GameAudioCueId.UiDeny);
                    break;
            }
        }

        private static void OnActivityCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            GameAudio.PlayUi(GameAudioCueId.InteractionCancel);
        }
    }
}
