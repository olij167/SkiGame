using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Progression;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SkiLessonOverlayUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private SkiLessonDirector lessonDirector;
        [SerializeField] private float completeFlashSeconds = 0.95f;
        [SerializeField] private float completionPanelSeconds = 3.5f;
        [SerializeField] private int modalCursorPriority = 850;
        [SerializeField] private int documentSortOrder = 2500;

        private VisualElement _root;

        private VisualElement _offerBlocker;
        private VisualElement _offerPanel;
        private Button _offerStartButton;
        private Button _offerSkipButton;

        private VisualElement _tutorialPanel;
        private Label _progressLabel;
        private Label _title;
        private Label _instruction;
        private Label _hint;
        private Label _progressText;
        private VisualElement _progressFill;
        private Label _checkLabel;

        private VisualElement _completeBlocker;
        private VisualElement _completePanel;
        private Label _completeContinueHint;

        private int _lastObservedStepIndex = -1;
        private bool _seenActiveLesson;
        private float _flashUntil;
        private float _completePanelUntil;
        private bool _lastLessonsCompleted;
        private bool _completionStateInitialized;
        private bool _sessionSawActiveLesson;
        private bool _completionTrackingInitialized;

        private bool _offerModalOwnsPause;
        private CameraController _cameraController;
        private bool _cameraWasEnabledBeforeModal;

        private int _displayedStepIndex = -1;
        private bool _holdStepTransition;
        private int _pendingStepIndex = -1;

        private string _displayedTitle = string.Empty;
        private string _displayedInstruction = string.Empty;
        private string _displayedHint = string.Empty;
        private string _displayedProgressText = string.Empty;
        private float _displayedProgress01 = 0f;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (lessonDirector == null)
                lessonDirector = FindObjectOfType<SkiLessonDirector>();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            document.sortingOrder = documentSortOrder;

            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            _root.style.flexGrow = 1f;

            _offerBlocker = _root.Q<VisualElement>("TutorialOfferBlocker");
            _offerPanel = _root.Q<VisualElement>("TutorialOfferPanel");
            _offerStartButton = _root.Q<Button>("Btn_TutorialOfferStart");
            _offerSkipButton = _root.Q<Button>("Btn_TutorialOfferSkip");

            _tutorialPanel = _root.Q<VisualElement>("TutorialPanel");
            _progressLabel = _root.Q<Label>("Lbl_TutorialProgress");
            _title = _root.Q<Label>("Lbl_TutorialTitle");
            _instruction = _root.Q<Label>("Lbl_TutorialInstruction");
            _hint = _root.Q<Label>("Lbl_TutorialHint");
            _progressText = _root.Q<Label>("Lbl_TutorialProgressText");
            _progressFill = _root.Q<VisualElement>("TutorialProgressFill");
            _checkLabel = _root.Q<Label>("Lbl_TutorialCheck");

            _completeBlocker = _root.Q<VisualElement>("TutorialCompleteBlocker");
            _completePanel = _root.Q<VisualElement>("TutorialCompletePanel");
            _completeContinueHint = _root.Q<Label>("Lbl_TutorialCompleteContinueHint");

            if (_offerBlocker != null)
                _offerBlocker.pickingMode = PickingMode.Position;

            if (_offerPanel != null)
                _offerPanel.pickingMode = PickingMode.Position;

            if (_tutorialPanel != null)
                _tutorialPanel.pickingMode = PickingMode.Ignore;

            if (_completeBlocker != null)
                _completeBlocker.pickingMode = PickingMode.Position;

            if (_completePanel != null)
                _completePanel.pickingMode = PickingMode.Ignore;

            if (_offerStartButton != null)
            {
                _offerStartButton.clicked -= HandleOfferAccepted;
                _offerStartButton.clicked += HandleOfferAccepted;
            }

            if (_offerSkipButton != null)
            {
                _offerSkipButton.clicked -= HandleOfferDeclined;
                _offerSkipButton.clicked += HandleOfferDeclined;
            }

            _cameraController = FindObjectOfType<CameraController>();
            _lastLessonsCompleted = lessonDirector != null && lessonDirector.LessonsCompleted;
            _completionTrackingInitialized = true;

            if (_lastLessonsCompleted)
                _completePanelUntil = 0f;

            _lastLessonsCompleted = lessonDirector != null && lessonDirector.LessonsCompleted;
            _completionStateInitialized = true;
            _sessionSawActiveLesson = lessonDirector != null && lessonDirector.IsLessonActive;
            _completePanelUntil = 0f;

            RefreshImmediate();
        }

        private void OnDisable()
        {
            ReleaseOfferModalPause();
        }

        private void Update()
        {
            if (lessonDirector == null)
                return;

            UpdateCompletionState();
            RefreshImmediate();
        }

        private void UpdateCompletionState()
        {
            if (lessonDirector == null)
                return;

            bool nowActive = lessonDirector.IsLessonActive;
            bool nowCompleted = lessonDirector.LessonsCompleted;

            if (!_completionStateInitialized)
            {
                _lastLessonsCompleted = nowCompleted;
                _completionStateInitialized = true;
                _sessionSawActiveLesson = nowActive;
                _completePanelUntil = 0f;
                return;
            }

            if (nowActive)
                _sessionSawActiveLesson = true;

            bool completedThisSession = nowCompleted && !_lastLessonsCompleted && _sessionSawActiveLesson;
            if (completedThisSession)
                _completePanelUntil = Time.unscaledTime + completionPanelSeconds;

            _lastLessonsCompleted = nowCompleted;
        }

        private void RefreshImmediate()
        {
            bool enabledBySettings = GameSettingsService.Current == null || GameSettingsService.Current.showTutorialOverlay;
            bool shopOpen = CustomizationShopRuntime.IsOpen;

            // Keep the offer modal suppressed in the shop, but do NOT hide the active tutorial panel.
            bool showOffer = enabledBySettings && !shopOpen && lessonDirector.CanOfferLessons;
            bool showTutorial = enabledBySettings && lessonDirector.IsLessonActive;
            bool showComplete = enabledBySettings && !showTutorial && Time.unscaledTime < _completePanelUntil;

            if (_offerBlocker != null)
                _offerBlocker.style.display = showOffer ? DisplayStyle.Flex : DisplayStyle.None;

            if (_offerPanel != null)
                _offerPanel.style.display = showOffer ? DisplayStyle.Flex : DisplayStyle.None;

            if (_tutorialPanel != null)
                _tutorialPanel.style.display = showTutorial ? DisplayStyle.Flex : DisplayStyle.None;

            if (_completeBlocker != null)
                _completeBlocker.style.display = showComplete ? DisplayStyle.Flex : DisplayStyle.None;

            if (_completePanel != null)
                _completePanel.style.display = showComplete ? DisplayStyle.Flex : DisplayStyle.None;

            if (showOffer)
                AcquireOfferModalPause();
            else
                ReleaseOfferModalPause();

            if (showTutorial)
                RefreshTutorialPanel();

            if (showComplete)
                RefreshCompletionPanel();
        }

        private void AcquireOfferModalPause()
        {
            if (_offerModalOwnsPause)
                return;

            _offerModalOwnsPause = true;

            Time.timeScale = 0f;
            GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, modalCursorPriority);

            if (_cameraController == null)
                _cameraController = FindObjectOfType<CameraController>();

            if (_cameraController != null)
            {
                _cameraWasEnabledBeforeModal = _cameraController.enabled;

                // Keep the camera controller alive so it continues tracking the player,
                // but suppress manual look input while the modal is open.
                if (!_cameraController.enabled)
                    _cameraController.enabled = true;

                _cameraController.SetExternalUiLookLock(true);
            }
        }

        private void ReleaseOfferModalPause()
        {
            if (!_offerModalOwnsPause)
                return;

            _offerModalOwnsPause = false;

            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;

            GameCursorService.Release(this);

            if (_cameraController != null)
            {
                _cameraController.SetExternalUiLookLock(false);
                _cameraController.enabled = _cameraWasEnabledBeforeModal;
            }
        }

        private void HandleOfferAccepted()
        {
            if (lessonDirector == null)
                return;

            lessonDirector.BeginLessons();

            _lastObservedStepIndex = -1;
            _displayedStepIndex = -1;
            _pendingStepIndex = -1;
            _holdStepTransition = false;
            _seenActiveLesson = false;

            ReleaseOfferModalPause();
            RefreshImmediate();
        }

        private void HandleOfferDeclined()
        {
            if (lessonDirector == null)
                return;

            lessonDirector.DeferLessons();
            ReleaseOfferModalPause();
            RefreshImmediate();
        }

        private void RefreshTutorialPanel()
        {
            int observedStepIndex = lessonDirector.CurrentStepIndex;

            if (!_seenActiveLesson)
            {
                _seenActiveLesson = true;
                _lastObservedStepIndex = observedStepIndex;
                SetDisplayedStepFromDirector(observedStepIndex);
            }
            else if (observedStepIndex != _lastObservedStepIndex)
            {
                _flashUntil = Time.unscaledTime + completeFlashSeconds;
                _holdStepTransition = true;
                _pendingStepIndex = observedStepIndex;
                _lastObservedStepIndex = observedStepIndex;
            }

            bool isFlashingComplete = _holdStepTransition && Time.unscaledTime < _flashUntil;

            if (_holdStepTransition && !isFlashingComplete)
            {
                _holdStepTransition = false;
                if (_pendingStepIndex >= 0)
                    SetDisplayedStepFromDirector(_pendingStepIndex);
                _pendingStepIndex = -1;
            }

            if (_tutorialPanel != null)
            {
                if (isFlashingComplete) _tutorialPanel.AddToClassList("tutorial-panel-complete");
                else _tutorialPanel.RemoveFromClassList("tutorial-panel-complete");
            }

            if (_checkLabel != null)
                _checkLabel.style.display = isFlashingComplete ? DisplayStyle.Flex : DisplayStyle.None;

            if (_progressLabel != null)
                _progressLabel.text = $"Lesson {_displayedStepIndex + 1}/{lessonDirector.TotalStepCount}";

            if (_title != null)
                _title.text = _displayedTitle;

            if (_instruction != null)
                _instruction.text = _displayedInstruction;

            if (_hint != null)
                _hint.text = _displayedHint;

            if (_progressText != null)
                _progressText.text = _displayedProgressText;

            if (_progressFill != null)
                _progressFill.style.width = Length.Percent(Mathf.Clamp01(_displayedProgress01) * 100f);

            if (!_holdStepTransition && _displayedStepIndex == lessonDirector.CurrentStepIndex)
            {
                _displayedProgress01 = lessonDirector.CurrentStepProgress01;
                _displayedProgressText = lessonDirector.CurrentStepProgressText;

                if (_progressText != null)
                    _progressText.text = _displayedProgressText;

                if (_progressFill != null)
                    _progressFill.style.width = Length.Percent(Mathf.Clamp01(_displayedProgress01) * 100f);
            }
        }

        private void SetDisplayedStepFromDirector(int stepIndex)
        {
            _displayedStepIndex = stepIndex;
            _displayedTitle = lessonDirector.CurrentStepTitle;
            _displayedInstruction = lessonDirector.CurrentStepBody;
            _displayedHint = lessonDirector.CurrentStepHint;
            _displayedProgressText = lessonDirector.CurrentStepProgressText;
            _displayedProgress01 = lessonDirector.CurrentStepProgress01;
        }

        private void RefreshCompletionPanel()
        {
            if (_completeContinueHint != null)
                _completeContinueHint.text = "Lessons complete. Keep skiing and explore the resort.";
        }
    }
}