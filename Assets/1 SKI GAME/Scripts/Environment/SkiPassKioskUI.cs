using System;
using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Progression;
using SkiGame.UI;

[RequireComponent(typeof(UIDocument))]
[DisallowMultipleComponent]
public class SkiPassKioskUI : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private StyleSheet styleSheet;

    [Header("References")]
    [SerializeField] private SkiPassManager skiPassManager;
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private MiniMountainHudController miniHudController;

    private VisualElement _root;
    private VisualElement _panel;
    private Button _btnClose;
    private Label _lblCurrentPass;
    private Label _lblExpiry;
    private Label _lblQuote;
    private Label _lblResult;
    private VisualElement _passList;
    private VisualElement _durationList;
    private VisualElement _liftList;
    private Button _btnPurchase;

    private int _selectedLevel = -1;
    private int _selectedDurationIndex = 0;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool IsBlocked => false;

    private void Reset()
    {
        if (document == null) document = GetComponent<UIDocument>();
    }

    private void Awake()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (skiPassManager == null)
            skiPassManager = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();

        if (statsManager == null)
            statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();

        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        _root = document != null ? document.rootVisualElement : null;
        if (_root == null)
            return;

        if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
            _root.styleSheets.Add(styleSheet);

        _panel = _root.Q<VisualElement>("SkiPassKioskPanel");
        _btnClose = _root.Q<Button>("Btn_CloseSkiPassKiosk");
        _lblCurrentPass = _root.Q<Label>("Lbl_CurrentPass");
        _lblExpiry = _root.Q<Label>("Lbl_PassExpiry");
        _lblQuote = _root.Q<Label>("Lbl_PassQuote");
        _lblResult = _root.Q<Label>("Lbl_PassResult");
        _passList = _root.Q<VisualElement>("PassLevelList");
        _durationList = _root.Q<VisualElement>("DurationList");
        _liftList = _root.Q<VisualElement>("UnlockedLiftList");
        _btnPurchase = _root.Q<Button>("Btn_PurchasePass");

        if (_btnClose != null)
            _btnClose.clicked += Close;

        if (_btnPurchase != null)
            _btnPurchase.clicked += PurchaseSelected;

        HideImmediate();
    }

    private void OnEnable()
    {
        if (skiPassManager != null)
            skiPassManager.OnPassChanged += HandlePassChanged;
    }

    private void OnDisable()
    {
        if (skiPassManager != null)
            skiPassManager.OnPassChanged -= HandlePassChanged;

        ApplyCursorAndCameraState(false);
    }

    private void HandlePassChanged()
    {
        if (_isOpen)
            RefreshAll();
    }

    public void Open()
    {
        if (_root == null)
            return;

        _isOpen = true;
        _selectedLevel = skiPassManager != null ? skiPassManager.CurrentLevel : 0;
        _selectedDurationIndex = 0;

        _root.style.display = DisplayStyle.Flex;
        _root.style.visibility = Visibility.Visible;
        _root.style.opacity = 1f;
        _root.pickingMode = PickingMode.Position;

        ApplyCursorAndCameraState(true);

        if (miniHudController != null)
            miniHudController.SetVisible(false);

        RefreshAll();
    }

    public void Close()
    {
        _isOpen = false;
        HideImmediate();

        ApplyCursorAndCameraState(false);

        if (miniHudController != null)
            miniHudController.SetVisible(true);
    }

    private void HideImmediate()
    {
        if (_root == null)
            return;

        _root.style.display = DisplayStyle.None;
        _root.style.visibility = Visibility.Hidden;
        _root.style.opacity = 0f;
        _root.pickingMode = PickingMode.Ignore;
    }

    private void ApplyCursorAndCameraState(bool open)
    {
        if (open)
            GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 850);
        else
            GameCursorService.Release(this);

        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        if (cameraController != null)
            cameraController.SetExternalUiLookLock(open);
    }

    private void RefreshAll()
    {
        RefreshCurrentPass();
        RebuildPassButtons();
        RebuildDurationButtons();
        RefreshQuoteAndLiftList();
    }

    private void RefreshCurrentPass()
    {
        if (skiPassManager == null)
        {
            if (_lblCurrentPass != null) _lblCurrentPass.text = "No ski pass manager found.";
            if (_lblExpiry != null) _lblExpiry.text = string.Empty;
            return;
        }

        if (_lblCurrentPass != null)
            _lblCurrentPass.text = skiPassManager.GetCurrentPassDisplayName();

        if (_lblExpiry != null)
            _lblExpiry.text = skiPassManager.GetRemainingTimeString();
    }

    private void RebuildPassButtons()
    {
        if (_passList == null || skiPassManager == null || skiPassManager.Config == null)
            return;

        _passList.Clear();

        var cfg = skiPassManager.Config;
        for (int i = 0; i < cfg.levels.Length; i++)
        {
            var level = cfg.levels[i];
            if (level == null) continue;

            int levelIndex = level.levelIndex;
            var btn = new Button(() =>
            {
                _selectedLevel = levelIndex;
                RefreshAll();
            });

            btn.text = $"{level.displayName} (L{levelIndex})";
            btn.AddToClassList("ski-pass-option-button");

            if (levelIndex == _selectedLevel)
                btn.AddToClassList("is-selected");

            if (levelIndex == skiPassManager.CurrentLevel)
                btn.AddToClassList("is-current");

            _passList.Add(btn);
        }
    }

    private void RebuildDurationButtons()
    {
        if (_durationList == null || skiPassManager == null || skiPassManager.Config == null)
            return;

        _durationList.Clear();

        var cfg = skiPassManager.Config;
        for (int i = 0; i < cfg.durations.Length; i++)
        {
            var dur = cfg.durations[i];
            if (dur == null) continue;

            int idx = i;
            var btn = new Button(() =>
            {
                _selectedDurationIndex = idx;
                RefreshQuoteAndLiftList();
                RebuildDurationButtons();
            });

            btn.text = dur.label;
            btn.AddToClassList("ski-pass-option-button");

            if (idx == _selectedDurationIndex)
                btn.AddToClassList("is-selected");

            _durationList.Add(btn);
        }
    }

    private void RefreshQuoteAndLiftList()
    {
        if (skiPassManager == null || skiPassManager.Config == null)
            return;

        RebuildLiftList(_selectedLevel);

        if (_lblResult != null)
            _lblResult.text = string.Empty;

        if (_btnPurchase != null)
            _btnPurchase.SetEnabled(false);

        int freeLevel = skiPassManager.Config.defaultLevelIndex;
        if (_selectedLevel == freeLevel)
        {
            bool alreadyClaimed = skiPassManager.HasClaimedDefaultPass;

            if (_lblQuote != null)
                _lblQuote.text = alreadyClaimed
                    ? "Default pass already unlocked. It never expires."
                    : "Claim the default pass for free. It never expires.";

            if (_btnPurchase != null)
            {
                _btnPurchase.text = alreadyClaimed ? "Unlocked" : "Claim Free Pass";
                _btnPurchase.SetEnabled(!alreadyClaimed);
            }

            return;
        }

        if (skiPassManager.TryQuotePurchase(_selectedLevel, _selectedDurationIndex, out var quote, out string reason))
        {
            string mode = quote.isUpgrade ? "Upgrade" : (quote.isExtend ? "Extend" : "Purchase");
            string credit = quote.credit > 0 ? $" • Credit {quote.credit}" : "";
            if (_lblQuote != null)
                _lblQuote.text = $"{mode}: {quote.finalCost}{credit}";

            if (_btnPurchase != null)
            {
                _btnPurchase.text = mode;
                _btnPurchase.SetEnabled(true);
            }
        }
        else
        {
            if (_lblQuote != null)
                _lblQuote.text = reason;

            if (_btnPurchase != null)
                _btnPurchase.text = "Unavailable";
        }
    }

    private void PurchaseSelected()
    {
        if (skiPassManager == null)
            return;

        int freeLevel = skiPassManager.Config != null ? skiPassManager.Config.defaultLevelIndex : 0;
        if (_selectedLevel == freeLevel)
        {
            bool claimed = skiPassManager.TryClaimDefaultPass(out string claimReason);

            if (_lblResult != null)
                _lblResult.text = claimed ? "Default pass unlocked!" : claimReason;

            if (claimed)
            {
                if (statsManager != null)
                    statsManager.Save();

                _selectedLevel = skiPassManager.CurrentLevel;
                RefreshAll();
            }

            return;
        }

        bool ok = skiPassManager.TryPurchase(
            _selectedLevel,
            _selectedDurationIndex,
            TrySpendCurrency,
            out var quote,
            out string failReason);

        if (_lblResult != null)
            _lblResult.text = ok ? "Purchased!" : failReason;

        if (ok)
        {
            if (statsManager != null)
                statsManager.Save();

            _selectedLevel = skiPassManager.CurrentLevel;
            RefreshAll();
        }
    }

    private bool TrySpendCurrency(int amount)
    {
        var mgr = statsManager != null ? statsManager : PlayerStatsManager.Instance;
        if (mgr == null || mgr.Profile == null) return false;
        if (amount < 0) return false;
        if (mgr.Profile.currency < amount) return false;

        mgr.Profile.currency -= amount;
        mgr.Save();
        return true;
    }

    private void RebuildLiftList(int level)
    {
        if (_liftList == null)
            return;

        _liftList.Clear();

        var lifts = FindObjectsOfType<LiftLine>(includeInactive: false);
        if (lifts == null || lifts.Length == 0)
        {
            _liftList.Add(new Label("No lifts found."));
            return;
        }

        Array.Sort(lifts, (a, b) => a.RequiredPassLevel.CompareTo(b.RequiredPassLevel));

        for (int i = 0; i < lifts.Length; i++)
        {
            var lift = lifts[i];
            if (lift == null) continue;

            bool unlocked = level >= Mathf.Max(0, lift.RequiredPassLevel);

            var row = new Label(
                unlocked
                    ? $"{lift.name}"
                    : $"{lift.name} (Requires L{lift.RequiredPassLevel})");

            row.AddToClassList("ski-pass-lift-row");
            if (!unlocked)
                row.AddToClassList("is-locked");

            _liftList.Add(row);
        }
    }
}