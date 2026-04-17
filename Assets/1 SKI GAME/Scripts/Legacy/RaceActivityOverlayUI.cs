using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SkiGame.Activities;
using SkiGame.Progression;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RaceActivityOverlayUI : MonoBehaviour
    {
        [Header("Document")]
        [SerializeField] private UIDocument document;
        [SerializeField] private int documentSortOrder = 920;
        [SerializeField] private int modalCursorPriority = 900;
        [SerializeField] private float refreshInterval = 0.05f;

        [Header("Race Input Actions")]
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference previousLeagueAction;
        [SerializeField] private InputActionReference nextLeagueAction;
        [SerializeField] private InputActionReference cancelAction;
        [SerializeField] private InputPromptIconLibrary iconLibrary;


        [Header("Input Ownership")]
        [SerializeField] private bool captureSelectionInputsInOverlay = false;
        [SerializeField] private bool captureFailedModalInputsInOverlay = true;
        [SerializeField][Min(0.05f)] private float startHoldSeconds = 0.15f;

        private VisualElement _root;
        private VisualElement _centerCallout;
        private Label _centerCalloutLabel;
        private VisualElement _toastPanel;
        private Label _toastLabel;

        private VisualElement _selectPanel;
        private Label _selectTitle;
        private Label _selectBadge;
        private Label _selectSubtitle;
        private Label _leagueName;
        private Label _leagueStatus;
        private Label _leagueDescription;
        private Label _bestPlacement;
        private Label _bestTime;
        private Label _reward;
        private Label _bonusSummary;
        private Label _startHint;
        private Button _prevLeagueButton;
        private Button _nextLeagueButton;

        private VisualElement _activePanel;
        private Label _activeTitle;
        private Label _activeLeague;
        private Label _stateChip;
        private Label _activeTime;
        private Label _checkpoint;
        private Label _npcProgress;
        private VisualElement _checkpointProgressFill;
        private Label _activeHint;

        private VisualElement _failedBlocker;
        private VisualElement _failedModalPanel;
        private Label _failTitle;
        private Label _failBody;
        private Label _failHint;
        private Button _restartButton;
        private Button _quitButton;

        private MountainActivityManager _boundActivityManager;
        private RaceCourseLine _cachedSelectableRace;
        private RaceCourseLine _failedRace;
        private string _failedRaceReason = string.Empty;
        private bool _showFailedModal;

        private RaceCourseLine _lastCompletedRace;
        private string _completionSummary = string.Empty;
        private float _completionSummaryUntil = -1f;
        private string _toastMessage = string.Empty;
        private float _toastUntil = -1f;
        private string _calloutMessage = string.Empty;
        private Color _calloutColor = Color.white;
        private float _calloutUntil = -1f;
        private float _calloutDuration = 1.65f;
        private float _nextRefreshTime;

        private bool _ownsFailureModalPause;
        private CameraController _cameraController;
        private bool _cameraWasEnabledBeforeModal;

        private bool _interactWasPressed;
        private float _interactHeldTime;
        private bool _selectionStartTriggeredThisPress;

        private string _cachedInteractHint = "Interact";
        private string _cachedPreviousHint = "Previous";
        private string _cachedNextHint = "Next";
        private string _cachedCancelHint = "Cancel";


        private VisualElement _startHintHost;
        private VisualElement _failHintHost;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            document.sortingOrder = documentSortOrder;
            _root = document.rootVisualElement;

            EnableConfiguredActions();
            CacheBindingHints();

            if (iconLibrary == null)
                iconLibrary = InputPromptResolver.LoadDefaultLibrary();

            Bind();
            HookButtons();
            TryBindActivityManager();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            UnhookButtons();
            UnbindActivityManager();
            ReleaseFailureModalPause();
            DisableConfiguredActions();
            ResetSelectionInputState();
        }

        private void Update()
        {
            TryBindActivityManager();

            HandleOverlayInputs();
            UpdateCenterCallout();

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshInterval;
            CacheBindingHints();
            RefreshImmediate();
        }

        private void Bind()
        {
            _centerCallout = _root.Q<VisualElement>("RaceCenterCallout");
            _centerCalloutLabel = _root.Q<Label>("Lbl_RaceCenterCallout");
            _toastPanel = _root.Q<VisualElement>("RaceToastPanel");
            _toastLabel = _root.Q<Label>("Lbl_RaceToast");
            _selectPanel = _root.Q<VisualElement>("RaceSelectPanel");
            _selectTitle = _root.Q<Label>("Lbl_RaceSelectTitle");
            _selectBadge = _root.Q<Label>("Lbl_RaceSelectBadge");
            _selectSubtitle = _root.Q<Label>("Lbl_RaceSelectSubtitle");
            _leagueName = _root.Q<Label>("Lbl_RaceLeagueName");
            _leagueStatus = _root.Q<Label>("Lbl_RaceLeagueStatus");
            _leagueDescription = _root.Q<Label>("Lbl_RaceLeagueDescription");
            _bestPlacement = _root.Q<Label>("Lbl_RaceBestPlacement");
            _bestTime = _root.Q<Label>("Lbl_RaceBestTime");
            _reward = _root.Q<Label>("Lbl_RaceReward");
            _bonusSummary = _root.Q<Label>("Lbl_RaceBonusSummary");
            _startHint = _root.Q<Label>("Lbl_RaceStartHint");
            if (_startHint != null && _startHint.parent != null)
            {
                _startHintHost = InputPromptVisualBuilder.CreatePrompt(InputPromptTokens.Text(_startHint.text), "race-hint-prompt");
                _startHintHost.name = "RaceStartHintHost";
                var parent = _startHint.parent;
                parent.Insert(parent.IndexOf(_startHint), _startHintHost);
                parent.Remove(_startHint);
                _startHint = null;
            }
            _prevLeagueButton = _root.Q<Button>("Btn_RacePrevLeague");
            _nextLeagueButton = _root.Q<Button>("Btn_RaceNextLeague");

            _activePanel = _root.Q<VisualElement>("RaceActivePanel");
            _activeTitle = _root.Q<Label>("Lbl_RaceActiveTitle");
            _activeLeague = _root.Q<Label>("Lbl_RaceActiveLeague");
            _stateChip = _root.Q<Label>("Lbl_RaceStateChip");
            _activeTime = _root.Q<Label>("Lbl_RaceActiveTime");
            _checkpoint = _root.Q<Label>("Lbl_RaceCheckpoint");
            _npcProgress = _root.Q<Label>("Lbl_RaceNpcProgress");
            _checkpointProgressFill = _root.Q<VisualElement>("RaceCheckpointProgressFill");
            _activeHint = _root.Q<Label>("Lbl_RaceActiveHint");

            _failedBlocker = _root.Q<VisualElement>("RaceFailedBlocker");
            _failedModalPanel = _root.Q<VisualElement>("RaceFailedModal");
            _failTitle = _root.Q<Label>("Lbl_RaceFailTitle");
            _failBody = _root.Q<Label>("Lbl_RaceFailBody");
            _failHint = _root.Q<Label>("Lbl_RaceFailHint");
            if (_failHint != null && _failHint.parent != null)
            {
                _failHintHost = InputPromptVisualBuilder.CreatePrompt(InputPromptTokens.Text(_failHint.text), "race-hint-prompt");
                _failHintHost.name = "RaceFailHintHost";
                var parent = _failHint.parent;
                parent.Insert(parent.IndexOf(_failHint), _failHintHost);
                parent.Remove(_failHint);
                _failHint = null;
            }
            _restartButton = _root.Q<Button>("Btn_RaceRestart");
            _quitButton = _root.Q<Button>("Btn_RaceQuit");

            if (_failedBlocker != null)
                _failedBlocker.pickingMode = PickingMode.Position;

            if (_failedModalPanel != null)
                _failedModalPanel.pickingMode = PickingMode.Position;
        }

        private void HookButtons()
        {
            if (_prevLeagueButton != null) _prevLeagueButton.clicked += HandlePrevLeague;
            if (_nextLeagueButton != null) _nextLeagueButton.clicked += HandleNextLeague;
            if (_restartButton != null) _restartButton.clicked += HandleRestartRace;
            if (_quitButton != null) _quitButton.clicked += HandleQuitRace;
        }

        private void UnhookButtons()
        {
            if (_prevLeagueButton != null) _prevLeagueButton.clicked -= HandlePrevLeague;
            if (_nextLeagueButton != null) _nextLeagueButton.clicked -= HandleNextLeague;
            if (_restartButton != null) _restartButton.clicked -= HandleRestartRace;
            if (_quitButton != null) _quitButton.clicked -= HandleQuitRace;
        }

        private void EnableConfiguredActions()
        {
            EnableAction(interactAction);
            EnableAction(previousLeagueAction);
            EnableAction(nextLeagueAction);
            EnableAction(cancelAction);
        }

        private void DisableConfiguredActions()
        {
            DisableAction(interactAction);
            DisableAction(previousLeagueAction);
            DisableAction(nextLeagueAction);
            DisableAction(cancelAction);
        }

        private static void EnableAction(InputActionReference actionRef)
        {
            if (actionRef != null && actionRef.action != null && !actionRef.action.enabled)
                actionRef.action.Enable();
        }

        private static void DisableAction(InputActionReference actionRef)
        {
            if (actionRef != null && actionRef.action != null && actionRef.action.enabled)
                actionRef.action.Disable();
        }

        private void TryBindActivityManager()
        {
            var mgr = MountainActivityManager.Instance;
            if (mgr == null || _boundActivityManager == mgr)
                return;

            UnbindActivityManager();

            _boundActivityManager = mgr;
            _boundActivityManager.OnActivityCompleted += HandleCompleted;
            _boundActivityManager.OnActivityFailed += HandleFailed;
            _boundActivityManager.OnActivityCancelled += HandleCancelled;
        }

        private void UnbindActivityManager()
        {
            if (_boundActivityManager == null)
                return;

            _boundActivityManager.OnActivityCompleted -= HandleCompleted;
            _boundActivityManager.OnActivityFailed -= HandleFailed;
            _boundActivityManager.OnActivityCancelled -= HandleCancelled;
            _boundActivityManager = null;
        }

        private void HandleCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            if (kind != MountainActivityKind.Race || source is not RaceCourseLine race)
                return;

            _showFailedModal = false;
            _failedRace = null;
            _failedRaceReason = string.Empty;
            _lastCompletedRace = race;
            _completionSummary = BuildCompletionSummary(race, displayName);
            _completionSummaryUntil = Time.unscaledTime + 6f;

            // ActivityCompletionFanfareUI now owns the punchy result callout.
            // Keep this overlay focused on the persistent summary panel.
            _toastMessage = string.Empty;
            _toastUntil = -1f;

            RefreshImmediate();
        }

        private void HandleFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
        {
            if (kind != MountainActivityKind.Race || source is not RaceCourseLine race)
                return;

            _failedRace = race;
            _failedRaceReason = string.IsNullOrWhiteSpace(reason) ? race.LastFailureReason : reason;
            _showFailedModal = true;
            ResetSelectionInputState();
            RefreshImmediate();
        }

        private void HandleCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            if (kind != MountainActivityKind.Race)
                return;

            if (source == _failedRace)
            {
                _showFailedModal = false;
                _failedRace = null;
                _failedRaceReason = string.Empty;
                RefreshImmediate();
            }
        }

        private void HandleOverlayInputs()
        {
            if (_showFailedModal)
            {
                HandleFailedModalInputs();
                return;
            }

            ResetSelectionInputState();
        }

        private void HandleSelectionInputs()
        {
            var race = _cachedSelectableRace;
            if (race == null)
            {
                ResetSelectionInputState();
                return;
            }

            if (WasActionPressedThisFrame(previousLeagueAction))
            {
                race.SelectPreviousLeague();
                RefreshImmediate();
            }

            if (WasActionPressedThisFrame(nextLeagueAction))
            {
                race.SelectNextLeague();
                RefreshImmediate();
            }

            var interact = interactAction != null ? interactAction.action : null;
            if (interact == null)
            {
                ResetSelectionInputState();
                return;
            }

            bool interactPressed = interact.IsPressed();

            if (!interactPressed)
            {
                _interactWasPressed = false;
                _interactHeldTime = 0f;
                _selectionStartTriggeredThisPress = false;
                return;
            }

            if (!_interactWasPressed)
            {
                _interactWasPressed = true;
                _interactHeldTime = 0f;
                _selectionStartTriggeredThisPress = false;
                return;
            }

            if (_selectionStartTriggeredThisPress)
                return;

            _interactHeldTime += Time.unscaledDeltaTime;
            if (_interactHeldTime >= startHoldSeconds)
            {
                _selectionStartTriggeredThisPress = true;

                if (race.CanStartLeague(race.SelectedLeagueNumber))
                    race.TryStartSelectedLeague();

                RefreshImmediate();
            }
        }

        private void HandleFailedModalInputs()
        {
            if (_failedRace == null)
                return;

            if (WasActionPressedThisFrame(interactAction))
            {
                HandleRestartRace();
                return;
            }

            if (WasActionPressedThisFrame(cancelAction))
            {
                HandleQuitRace();
            }
        }

        private static bool WasActionPressedThisFrame(InputActionReference actionRef)
        {
            return actionRef != null &&
                   actionRef.action != null &&
                   actionRef.action.enabled &&
                   actionRef.action.WasPressedThisFrame();
        }

        private void ResetSelectionInputState()
        {
            _interactWasPressed = false;
            _interactHeldTime = 0f;
            _selectionStartTriggeredThisPress = false;
        }

        private void CacheBindingHints()
        {
            _cachedInteractHint = GetBindingDisplay(interactAction, "Interact");
            _cachedPreviousHint = GetBindingDisplay(previousLeagueAction, "Previous");
            _cachedNextHint = GetBindingDisplay(nextLeagueAction, _cachedInteractHint);
            _cachedCancelHint = GetBindingDisplay(cancelAction, "Cancel");
        }

        private InputPromptToken[] ResolvePromptTokens(InputActionReference actionRef, string fallbackText)
        {
            if (actionRef == null || actionRef.action == null)
                return InputPromptTokens.Text(fallbackText);

            var action = actionRef.action;
            if (action.bindings.Count == 0)
                return InputPromptTokens.Text(fallbackText);

            int bindingIndex = InputPromptResolver.FindPrimaryBindingIndex(action);
            return InputPromptResolver.ResolveBindingTokens(action, bindingIndex, iconLibrary);
        }

        private InputPromptToken[] BuildRaceStartHintTokens(bool canCycleWithSeparateButtons)
        {
            if (canCycleWithSeparateButtons)
            {
                return InputPromptTokens.Concat(
                    InputPromptTokens.Text("Tap"),
                    ResolvePromptTokens(nextLeagueAction, "Next"),
                    InputPromptTokens.Text("next  •  Tap"),
                    ResolvePromptTokens(previousLeagueAction, "Previous"),
                    InputPromptTokens.Text("back  •  Hold"),
                    ResolvePromptTokens(interactAction, "Interact"),
                    InputPromptTokens.Text("begin"));
            }

            return InputPromptTokens.Concat(
                InputPromptTokens.Text("Controls:"),
                ResolvePromptTokens(previousLeagueAction, "Previous"),
                InputPromptTokens.Text("previous  •"),
                ResolvePromptTokens(nextLeagueAction, "Next"),
                InputPromptTokens.Text("next  •  Hold"),
                ResolvePromptTokens(interactAction, "Interact"),
                InputPromptTokens.Text("begin"));
        }

        private InputPromptToken[] BuildRaceFailHintTokens()
        {
            return InputPromptTokens.Concat(
                InputPromptTokens.Text("Press"),
                ResolvePromptTokens(interactAction, "Interact"),
                InputPromptTokens.Text("restart  •  Press"),
                ResolvePromptTokens(cancelAction, "Cancel"),
                InputPromptTokens.Text("quit and continue skiing"));
        }

        private static string GetBindingDisplay(InputActionReference actionRef, string fallback)
        {
            if (actionRef == null || actionRef.action == null)
                return fallback;

            var action = actionRef.action;
            if (action.bindings.Count == 0)
                return fallback;

            string display = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontOmitDevice);
            if (string.IsNullOrWhiteSpace(display))
                display = action.GetBindingDisplayString();

            return string.IsNullOrWhiteSpace(display) ? fallback : display;
        }

        private void RefreshImmediate()
        {
            var mgr = MountainActivityManager.Instance;
            RaceCourseLine activeRace = null;

            if (mgr != null && mgr.HasActiveActivity && mgr.ActiveKind == MountainActivityKind.Race)
                activeRace = mgr.ActiveSource as RaceCourseLine;

            _cachedSelectableRace = null;

            const bool showSelect = false;
            const bool showCompletionSummary = false;
            const bool showActive = false;
            const bool showToast = false;

            if (_toastPanel != null)
                _toastPanel.style.display = showToast ? DisplayStyle.Flex : DisplayStyle.None;

            if (_toastLabel != null)
                _toastLabel.text = _toastMessage;

            if (_selectPanel != null)
                _selectPanel.style.display = DisplayStyle.None;

            if (_activePanel != null)
                _activePanel.style.display = DisplayStyle.None;

            if (_failedBlocker != null)
                _failedBlocker.style.display = _showFailedModal ? DisplayStyle.Flex : DisplayStyle.None;

            if (_failedModalPanel != null)
                _failedModalPanel.style.display = _showFailedModal ? DisplayStyle.Flex : DisplayStyle.None;

            ApplyPanelVisualState(activeRace, showCompletionSummary);

            if (_showFailedModal)
                AcquireFailureModalPause();
            else
                ReleaseFailureModalPause();

            if (_showFailedModal)
                RefreshFailedModal();
        }

        private void RefreshSelectPanel(RaceCourseLine race)
        {
            int selectedLeague = race.SelectedLeagueNumber;
            bool unlocked = race.IsRegionalChampionship
                ? race.IsChampionshipUnlocked()
                : race.IsLeagueUnlocked(selectedLeague);
            bool completed = race.IsRegionalChampionship
                ? RaceRescueProgression.IsRaceChampionshipCompleted(race.RaceId)
                : race.HasCompletedLeague(selectedLeague);
            int bestPlacement = race.GetBestPlacement(selectedLeague);
            float bestTime = race.GetBestTimeSeconds(selectedLeague);
            var league = race.GetLeague(selectedLeague);

            if (_selectTitle != null)
                _selectTitle.text = race.IsRegionalChampionship ? $"{race.RaceName} Championship" : race.RaceName;

            if (_selectBadge != null)
                _selectBadge.text = unlocked ? (race.IsRegionalChampionship ? "Championship" : "Race") : "Locked";

            if (_selectSubtitle != null)
            {
                _selectSubtitle.text = race.IsRegionalChampionship
                    ? "Regional finale event with a bigger field and course-side spectators."
                    : "Select a league before you line up at the start gate.";
            }

            if (_leagueName != null)
                _leagueName.text = race.GetLeagueDisplayName(selectedLeague);

            if (_leagueDescription != null)
            {
                if (race.IsRegionalChampionship)
                {
                    string rewardSummary = race.GetChampionshipRewardSummary();
                    _leagueDescription.text = string.IsNullOrWhiteSpace(rewardSummary)
                        ? "Win the regional championship to cap off this race circuit."
                        : $"Win the regional championship to earn {rewardSummary.Replace("Permanent pass unlock: ", string.Empty)}.";
                }
                else
                {
                    _leagueDescription.text = league != null && !string.IsNullOrWhiteSpace(league.description)
                        ? league.description
                        : "Race the course against the current league field.";
                }
            }

            if (_leagueStatus != null)
            {
                if (unlocked)
                {
                    _leagueStatus.text = completed ? "Completed" : (race.IsRegionalChampionship ? "Championship unlocked" : "Unlocked");
                }
                else if (race.IsRegionalChampionship)
                {
                    _leagueStatus.text = race.BuildChampionshipLockReason();
                }
                else
                {
                    int required = race.GetPreviousLeagueNumber(selectedLeague);
                    _leagueStatus.text = required > 0
                        ? $"Locked - win {race.GetLeagueDisplayName(required)}"
                        : "Locked";
                }
            }

            if (_bestPlacement != null)
                _bestPlacement.text = bestPlacement > 0 ? $"{bestPlacement} place" : "--";

            if (_bestTime != null)
                _bestTime.text = bestTime >= 0f ? $"{bestTime:0.00}s" : "--";

            if (_reward != null)
                _reward.text = race.IsRegionalChampionship
                    ? race.GetChampionshipRewardSummary()
                    : "$" + race.CompletionReward;

            if (_bonusSummary != null)
            {
                string bonusText;
                if (race.IsRegionalChampionship)
                {
                    int completedRegionalRaces = race.GetChampionshipCompletedStandardRaceCount();
                    int totalRegionalRaces = Mathf.Max(1, race.GetChampionshipTotalStandardRaceCount());
                    bonusText = $"Regional requirement {completedRegionalRaces}/{totalRegionalRaces} cleared";
                }
                else
                {
                    bonusText = $"1st +${race.FirstPlaceBonus}  |  2nd +${race.SecondPlaceBonus}  |  3rd +${race.ThirdPlaceBonus}";
                    if (!completed && race.FirstTimeLeagueCompletionBonus > 0)
                        bonusText += $"  |  First clear +${race.FirstTimeLeagueCompletionBonus}";
                }

                _bonusSummary.text = bonusText;
            }

            if (_startHint != null)

                if (_prevLeagueButton != null)
                _prevLeagueButton.SetEnabled(true);

            if (_nextLeagueButton != null)
                _nextLeagueButton.SetEnabled(true);
        }
        private void RefreshActivePanel(RaceCourseLine race)
        {
            if (_activeTitle != null) _activeTitle.text = race.RaceName;
            if (_activeLeague != null) _activeLeague.text = race.GetLeagueDisplayName(race.ActiveLeagueNumber);

            if (_stateChip != null)
            {
                if (race.IsSkisOffWarningActive)
                    _stateChip.text = $"Skis off {Mathf.CeilToInt(race.SkisOffGraceSecondsRemaining)}";
                else if (race.IsOffCourseWarningActive)
                    _stateChip.text = $"Return {Mathf.CeilToInt(race.OffCourseGraceSecondsRemaining)}";
                else if (race.IsCountdownActive)
                    _stateChip.text = $"Starts in {Mathf.CeilToInt(race.CountdownRemainingSeconds)}";
                else
                    _stateChip.text = "Racing";
            }

            if (_activeTime != null)
            {
                if (race.IsCountdownActive)
                    _activeTime.text = Mathf.CeilToInt(race.CountdownRemainingSeconds).ToString();
                else
                    _activeTime.text = race.AttemptTimeSeconds.ToString("0.00") + "s";
            }

            if (_checkpoint != null)
                _checkpoint.text = $"{Mathf.Min(race.CurrentCheckpointIndex + 1, Mathf.Max(1, race.CheckpointCount))} / {Mathf.Max(1, race.CheckpointCount)}";

            if (_npcProgress != null)
            {
                if (RaceActivityService.Instance != null)
                    _npcProgress.text = $"{RaceActivityService.Instance.FinishedNpcCount} / {RaceActivityService.Instance.ActiveNpcCount}";
                else
                    _npcProgress.text = "--";
            }

            if (_checkpointProgressFill != null)
            {
                float progress01 = race.CheckpointCount > 0
                    ? Mathf.Clamp01((float)race.CurrentCheckpointIndex / race.CheckpointCount)
                    : 0f;
                _checkpointProgressFill.style.width = Length.Percent(progress01 * 100f);
            }

            if (_activeHint != null)
                _activeHint.text = BuildActiveHint(race);
        }

        private void RefreshCompletionPanel(RaceCourseLine race, string summary)
        {
            if (race == null)
                return;

            if (_activeTitle != null)
                _activeTitle.text = race.LastResolvedPlacement == 1 ? $"{race.RaceName} Victory" : race.RaceName;

            if (_activeLeague != null)
                _activeLeague.text = race.IsRegionalChampionship
                    ? "Championship result"
                    : $"{race.GetLeagueDisplayName(Mathf.Max(1, race.LastAttemptLeagueNumber))} result";

            if (_stateChip != null)
                _stateChip.text = race.LastResolvedPlacement == 1 ? "Victory!" : "Finished";

            if (_activeTime != null)
                _activeTime.text = race.LastCompletionTimeSeconds > 0f ? $"{race.LastCompletionTimeSeconds:0.00}s" : "--";

            if (_checkpoint != null)
                _checkpoint.text = race.LastResolvedPlacement > 0 && race.LastResolvedEntrantCount > 0
                    ? $"{race.LastResolvedPlacement} / {race.LastResolvedEntrantCount}"
                    : "Complete";

            if (_npcProgress != null)
                _npcProgress.text = race.LastRewardGranted > 0 ? $"+${race.LastRewardGranted}" : (race.LastResolvedPlacement == 1 ? "Champion" : "--");

            if (_checkpointProgressFill != null)
                _checkpointProgressFill.style.width = Length.Percent(100f);

            if (_activeHint != null)
                _activeHint.text = summary;
        }

        private void RefreshFailedModal()
        {
            if (_failTitle != null)
                _failTitle.text = _failedRace != null ? _failedRace.RaceName : "Race Failed";

            if (_failBody != null)
                _failBody.text = string.IsNullOrWhiteSpace(_failedRaceReason) ? "Race failed." : _failedRaceReason;

            if (_failHintHost != null)
                InputPromptVisualBuilder.Populate(_failHintHost, BuildRaceFailHintTokens());

            _restartButton?.Focus();
        }

        private void ApplyPanelVisualState(RaceCourseLine activeRace, bool showingCompletionSummary)
        {
            if (_activePanel != null)
            {
                Color bg = new Color(10f / 255f, 16f / 255f, 28f / 255f, 0.95f);
                Color border = new Color(1f, 1f, 1f, 0.08f);

                if (showingCompletionSummary && _lastCompletedRace != null)
                {
                    bool victory = _lastCompletedRace.LastResolvedPlacement == 1 || _lastCompletedRace.LastChampionshipCompletedForFirstTime;
                    bg = victory
                        ? new Color(0.07f, 0.22f, 0.11f, 0.96f)
                        : new Color(0.10f, 0.18f, 0.12f, 0.95f);
                    border = victory
                        ? new Color(0.42f, 0.93f, 0.55f, 0.40f)
                        : new Color(0.29f, 0.74f, 0.42f, 0.28f);
                }

                _activePanel.style.backgroundColor = new StyleColor(bg);
                _activePanel.style.borderLeftColor = new StyleColor(border);
                _activePanel.style.borderRightColor = new StyleColor(border);
                _activePanel.style.borderTopColor = new StyleColor(border);
                _activePanel.style.borderBottomColor = new StyleColor(border);
            }

            if (_failedBlocker != null)
                _failedBlocker.style.backgroundColor = new StyleColor(new Color(0.19f, 0.03f, 0.04f, _showFailedModal ? 0.72f : 0f));

            if (_failedModalPanel != null)
            {
                _failedModalPanel.style.backgroundColor = new StyleColor(new Color(0.16f, 0.04f, 0.05f, 0.97f));
                StyleColor border = new StyleColor(new Color(1f, 0.36f, 0.36f, 0.34f));
                _failedModalPanel.style.borderLeftColor = border;
                _failedModalPanel.style.borderRightColor = border;
                _failedModalPanel.style.borderTopColor = border;
                _failedModalPanel.style.borderBottomColor = border;
                _failedModalPanel.BringToFront();
            }
        }

        private void ShowCenterCallout(string message, Color color, float duration)
        {
            _calloutMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim().ToUpperInvariant();
            _calloutColor = color;
            _calloutDuration = Mathf.Max(0.5f, duration);
            _calloutUntil = Time.unscaledTime + _calloutDuration;
        }

        private void UpdateCenterCallout()
        {
            if (_centerCallout == null || _centerCalloutLabel == null)
                return;

            if (string.IsNullOrWhiteSpace(_calloutMessage) || Time.unscaledTime >= _calloutUntil)
            {
                _centerCallout.style.display = DisplayStyle.None;
                return;
            }

            float age = _calloutDuration - (_calloutUntil - Time.unscaledTime);
            float intro = Mathf.Clamp01(age / 0.16f);
            float outro = Mathf.Clamp01((_calloutUntil - Time.unscaledTime) / 0.28f);
            float visibility = Mathf.Min(intro, outro);
            float pulse = 1f + Mathf.Sin(age * 12f) * 0.045f;
            float scale = Mathf.Lerp(1.32f, 1f, intro) * pulse;
            float y = Mathf.Lerp(18f, 0f, intro);

            _centerCallout.style.display = DisplayStyle.Flex;
            _centerCalloutLabel.text = _calloutMessage;
            _centerCalloutLabel.style.color = new StyleColor(new Color(_calloutColor.r, _calloutColor.g, _calloutColor.b, visibility));
            _centerCalloutLabel.style.opacity = visibility;
            _centerCalloutLabel.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _centerCalloutLabel.style.translate = new Translate(0f, y);
        }

        private void HandlePrevLeague()
        {
            // Deprecated: league selection is now surfaced through the mini objective HUD.
        }

        private void HandleNextLeague()
        {
            // Deprecated: league selection is now surfaced through the mini objective HUD.
        }

        private void HandleRestartRace()
        {
            RaceCourseLine failedRace = _failedRace;

            _showFailedModal = false;
            _failedRace = null;
            _failedRaceReason = string.Empty;
            ReleaseFailureModalPause();

            if (failedRace == null || failedRace.TryRestartLastAttempt())
            {
                RefreshImmediate();
                return;
            }

            _completionSummary = "Could not restart race. Move back into the start gate and try again.";
            _completionSummaryUntil = Time.unscaledTime + 3f;
            RefreshImmediate();
        }

        private void HandleQuitRace()
        {
            _showFailedModal = false;
            _failedRace = null;
            _failedRaceReason = string.Empty;
            ReleaseFailureModalPause();
            RefreshImmediate();
        }

        private void AcquireFailureModalPause()
        {
            if (_ownsFailureModalPause)
                return;

            _ownsFailureModalPause = true;
            Time.timeScale = 0f;
            GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, modalCursorPriority);

            if (_cameraController == null)
                _cameraController = FindObjectOfType<CameraController>();

            if (_cameraController != null)
            {
                _cameraWasEnabledBeforeModal = _cameraController.enabled;
                if (!_cameraController.enabled)
                    _cameraController.enabled = true;

                _cameraController.SetExternalUiLookLock(true);
            }
        }

        private void ReleaseFailureModalPause()
        {
            if (!_ownsFailureModalPause)
                return;

            _ownsFailureModalPause = false;

            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;

            GameCursorService.Release(this);

            if (_cameraController != null)
            {
                _cameraController.SetExternalUiLookLock(false);
                _cameraController.enabled = _cameraWasEnabledBeforeModal;
            }
        }

        private static string BuildCompletionSummary(RaceCourseLine race, string displayName)
        {
            if (race == null)
                return $"{displayName} finished";

            string title = race.IsRegionalChampionship && race.LastChampionshipCompletedForFirstTime
                ? "Regional championship complete."
                : race.LastResolvedPlacement > 0 && race.LastResolvedEntrantCount > 0
                    ? $"Finished {race.LastResolvedPlacement}/{race.LastResolvedEntrantCount}."
                    : $"{displayName} finished.";

            if (race.LastRewardGranted > 0)
                title += $" Reward +${race.LastRewardGranted}.";

            if (race.LastChampionshipCompletedForFirstTime && !string.IsNullOrWhiteSpace(race.LastChampionshipRewardSummary))
                title += $" {race.LastChampionshipRewardSummary}.";
            else if (race.LastUnlockedLeagueNumber > 0)
                title += $" Unlocked {race.GetLeagueDisplayName(race.LastUnlockedLeagueNumber)}.";
            else if (race.LastPersonalBestImproved)
                title += " New personal best.";

            return title;
        }

        private static string BuildUnlockToast(RaceCourseLine race)
        {
            if (race == null)
                return string.Empty;

            if (race.LastChampionshipCompletedForFirstTime)
            {
                if (!string.IsNullOrWhiteSpace(race.LastChampionshipRewardSummary))
                    return race.LastChampionshipRewardSummary;

                return "Championship complete";
            }

            if (race.LastUnlockedLeagueNumber > 0)
                return $"Unlocked {race.GetLeagueDisplayName(race.LastUnlockedLeagueNumber)}";

            return string.Empty;
        }

        private static string BuildCompletionHeadline(RaceCourseLine race)
        {
            if (race == null)
                return "Finished";

            if (race.LastChampionshipCompletedForFirstTime)
                return "Champion";

            if (race.LastResolvedPlacement == 1)
                return "Victory";

            if (race.LastPersonalBestImproved)
                return "Personal Best";

            return "Finished";
        }

        private static Color GetCompletionCalloutColor(RaceCourseLine race)
        {
            if (race != null && (race.LastResolvedPlacement == 1 || race.LastChampionshipCompletedForFirstTime))
                return new Color(0.50f, 1f, 0.58f, 1f);

            return new Color(0.72f, 0.96f, 0.78f, 1f);
        }

        private static string BuildFailureHeadline(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason) && reason.IndexOf("stack", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Wipeout";

            if (!string.IsNullOrWhiteSpace(reason) && reason.IndexOf("course", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Off Line";

            if (!string.IsNullOrWhiteSpace(reason) && reason.IndexOf("checkpoint", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Missed It";

            return "Failed";
        }

        private static string BuildActiveHint(RaceCourseLine race)
        {
            if (race == null)
                return string.Empty;

            if (race.IsSkisOffWarningActive)
                return $"You can step out of the skis, but you must re-equip within {Mathf.CeilToInt(race.SkisOffGraceSecondsRemaining)} seconds or the run fails.";

            if (race.IsOffCourseWarningActive)
                return $"You are outside the course boundary. Get back inside within {Mathf.CeilToInt(race.OffCourseGraceSecondsRemaining)} seconds.";

            if (race.IsCountdownActive)
            {
                return race.IsRegionalChampionship
                    ? "Championship event live. Time of day is paused, the crowd is in place, and checkpoints are marked."
                    : "Time of day is paused for the race. Settle into the start lane and be ready for the countdown.";
            }

            return race.IsRegionalChampionship
                ? "Championship rules apply. Hit every checkpoint cleanly and protect your line when the pressure builds."
                : "Hit each checkpoint in order. Going too far off course or missing the next gate will end the attempt.";
        }

        private RaceCourseLine FindSelectableRace()
        {
#if UNITY_2023_1_OR_NEWER
            var races = FindObjectsByType<RaceCourseLine>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var races = FindObjectsOfType<RaceCourseLine>();
#endif
            RaceCourseLine best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < races.Length; i++)
            {
                var race = races[i];
                if (race == null || !race.CanShowLeagueSelection)
                    continue;

                if (race.PromptPriority > bestPriority)
                {
                    best = race;
                    bestPriority = race.PromptPriority;
                }
            }

            return best;
        }
    }
}
