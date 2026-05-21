using System;
using System.Collections.Generic;
using System.Reflection;
using SkiGame.Activities;
using SkiGame.Progression;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class ActivityCompletionFanfareUI : MonoBehaviour
    {
        public enum FanfareStateId
        {
            RaceVictory,
            RaceFinish,
            RaceFailure,
            RaceCountdown,
            RaceGo,
            RescueSuccess,
            RescueFailure,
            Cancelled,
            QuestAccepted,
            QuestStageAdvanced,
            QuestCompleted,
            CustomDebug,
            QuestFocused
        }

        [Serializable]
        public sealed class FanfareAnimationSettings
        {
            [Min(0.01f)] public float introDuration = 0.14f;
            [Min(0.01f)] public float outroDuration = 0.30f;
            [Min(0.8f)] public float headlinePopScale = 1.26f;
            [Min(0.8f)] public float detailPopScale = 1.10f;
            public float introLiftPixels = 18f;
            public float pulseAmplitude = 0.035f;
            public float pulseSpeed = 11f;

            public FanfareAnimationSettings Clone()
            {
                return new FanfareAnimationSettings
                {
                    introDuration = introDuration,
                    outroDuration = outroDuration,
                    headlinePopScale = headlinePopScale,
                    detailPopScale = detailPopScale,
                    introLiftPixels = introLiftPixels,
                    pulseAmplitude = pulseAmplitude,
                    pulseSpeed = pulseSpeed
                };
            }
        }

        [Serializable]
        public sealed class FanfareStateStyle
        {
            public FanfareStateId stateId = FanfareStateId.RaceFinish;
            public bool uppercaseHeadline = true;
            [Min(0.5f)] public float duration = 2.5f;

            [Header("Headline")]
            public UnityEngine.Object headlineFontAsset;
            public Color headlineColor = Color.white;
            [Min(0f)] public float headlineFontSize = 0f;

            [Header("Detail")]
            public UnityEngine.Object detailFontAsset;
            public Color detailColor = Color.white;
            [Min(0f)] public float detailFontSize = 0f;

            [Header("Animation")]
            public FanfareAnimationSettings animation = new FanfareAnimationSettings();
        }

        private readonly struct FanfareRequest
        {
            public readonly FanfareStateId StateId;
            public readonly string Headline;
            public readonly string Detail;
            public readonly FanfareStateStyle Style;

            public FanfareRequest(FanfareStateId stateId, string headline, string detail, FanfareStateStyle style)
            {
                StateId = stateId;
                Headline = headline ?? string.Empty;
                Detail = detail ?? string.Empty;
                Style = style;
            }
        }

        private struct LabelBaseStyle
        {
            public FontDefinition FontDefinition;
            public float FontSize;
            public Color Color;
        }

        [Header("Document")]
        [SerializeField] private UIDocument document;
        [SerializeField] private int documentSortOrder = 890;

        [Header("Default Duration")]
        [SerializeField] private float defaultDuration = 2.25f;

        [Header("Default Animation")]
        [SerializeField] private FanfareAnimationSettings defaultAnimation = new FanfareAnimationSettings();

        [Header("State Styles")]
        [SerializeField] private List<FanfareStateStyle> stateStyles = new List<FanfareStateStyle>();

        [Header("Input Prompts")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Debug")]
        [SerializeField] private string debugHeadline = "QUEST STAGE COMPLETE";
        [SerializeField] private string debugDetail = "Unlocked: Ride the lift  •  New goals: Board, Dismount safely";
        [SerializeField] private FanfareStateId debugState = FanfareStateId.QuestStageAdvanced;

        private VisualElement _root;
        private VisualElement _container;
        private Label _headlineLabel;
        private Label _detailLabel;

        private LabelBaseStyle _headlineBaseStyle;
        private LabelBaseStyle _detailBaseStyle;

        private MountainActivityManager _boundActivityManager;
        private QuestDirector _boundQuestDirector;

        private readonly Queue<FanfareRequest> _pendingRequests = new Queue<FanfareRequest>();

        private string _headline = string.Empty;
        private string _detail = string.Empty;
        private FanfareStateId _activeStateId = FanfareStateId.CustomDebug;
        private FanfareStateStyle _activeStyle;
        private float _showUntil = -1f;
        private float _duration = 0f;

        private void Reset()
        {
            EnsureDefaultStateStyles();
        }

        private void OnValidate()
        {
            EnsureDefaultStateStyles();
        }

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

            if (inputActions == null)
                inputActions = new InputSystem_Actions().asset;

            Bind();
            EnsureDefaultStateStyles();
            TryBindActivityManager();
            TryBindQuestDirector();
            RaceCourseLine.OnRaceCountdownTick += HandleRaceCountdownTick;
            RaceCourseLine.OnRaceCountdownGo += HandleRaceCountdownGo;
            MiniMountainHudController.OnFocusedQuestChanged += HandleFocusedQuestChanged;
            HideImmediate();
        }

        private void OnDisable()
        {
            MiniMountainHudController.OnFocusedQuestChanged -= HandleFocusedQuestChanged;
            RaceCourseLine.OnRaceCountdownTick -= HandleRaceCountdownTick;
            RaceCourseLine.OnRaceCountdownGo -= HandleRaceCountdownGo;

            UnbindActivityManager();
            UnbindQuestDirector();
            HideImmediate();
        }

        private void Update()
        {
            TryBindActivityManager();
            TryBindQuestDirector();
            RefreshVisuals();
        }

        private void Bind()
        {
            _container = _root.Q<VisualElement>("ActivityFanfareContainer");
            _headlineLabel = _root.Q<Label>("Lbl_ActivityFanfareHeadline");
            _detailLabel = _root.Q<Label>("Lbl_ActivityFanfareDetail");

            CacheBaseLabelStyles();
        }

        private void CacheBaseLabelStyles()
        {
            if (_headlineLabel != null)
            {
                _headlineBaseStyle = new LabelBaseStyle
                {
                    FontDefinition = _headlineLabel.resolvedStyle.unityFontDefinition,
                    FontSize = _headlineLabel.resolvedStyle.fontSize,
                    Color = _headlineLabel.resolvedStyle.color
                };
            }

            if (_detailLabel != null)
            {
                _detailBaseStyle = new LabelBaseStyle
                {
                    FontDefinition = _detailLabel.resolvedStyle.unityFontDefinition,
                    FontSize = _detailLabel.resolvedStyle.fontSize,
                    Color = _detailLabel.resolvedStyle.color
                };
            }
        }

        private void TryBindActivityManager()
        {
            MountainActivityManager mgr = MountainActivityManager.Instance;
            if (mgr == null || mgr == _boundActivityManager)
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

        private void TryBindQuestDirector()
        {
            QuestDirector director = QuestDirector.Instance != null
                ? QuestDirector.Instance
                : FindObjectOfType<QuestDirector>();

            if (director == null || director == _boundQuestDirector)
                return;

            UnbindQuestDirector();

            _boundQuestDirector = director;
            _boundQuestDirector.OnQuestAccepted += HandleQuestAccepted;
            _boundQuestDirector.OnQuestStageAdvanced += HandleQuestStageAdvanced;
            _boundQuestDirector.OnQuestCompleted += HandleQuestCompleted;
        }

        private void UnbindQuestDirector()
        {
            if (_boundQuestDirector == null)
                return;

            _boundQuestDirector.OnQuestAccepted -= HandleQuestAccepted;
            _boundQuestDirector.OnQuestStageAdvanced -= HandleQuestStageAdvanced;
            _boundQuestDirector.OnQuestCompleted -= HandleQuestCompleted;
            _boundQuestDirector = null;
        }

        private void HandleCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    {
                        RaceCourseLine race = source as RaceCourseLine;
                        if (race == null)
                            return;

                        FanfareStateId stateId = GetRaceSuccessState(race);
                        Enqueue(stateId, BuildRaceSuccessHeadline(race), BuildRaceSuccessDetail(race, displayName));
                        break;
                    }

                case MountainActivityKind.Rescue:
                    {
                        RescueService rescue = RescueService.Instance;
                        Enqueue(FanfareStateId.RescueSuccess, BuildRescueSuccessHeadline(rescue), BuildRescueSuccessDetail(rescue, displayName));
                        break;
                    }
            }
        }

        private void HandleFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    Enqueue(FanfareStateId.RaceFailure, BuildRaceFailureHeadline(reason), BuildFailureDetail(reason, "Race failed."));
                    break;

                case MountainActivityKind.Rescue:
                    Enqueue(FanfareStateId.RescueFailure, "BOTCHED", BuildFailureDetail(reason, "Rescue failed."));
                    break;
            }
        }

        private void HandleCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    Enqueue(FanfareStateId.Cancelled, "ABORTED", "Race cancelled.");
                    break;

                case MountainActivityKind.Rescue:
                    Enqueue(FanfareStateId.Cancelled, "CANCELLED", "Rescue dispatch cancelled.");
                    break;
            }
        }

        private void HandleRaceCountdownTick(RaceCourseLine race, int seconds)
        {
            ShowRaceCountdown(seconds, race);
        }

        private void HandleRaceCountdownGo(RaceCourseLine race)
        {
            ShowRaceGo(race);
        }

        private void HandleQuestStageAdvanced(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            if (definition == null || runtimeState == null)
                return;

            Enqueue(
                FanfareStateId.QuestStageAdvanced,
                BuildQuestStageAdvancedHeadline(definition, runtimeState),
                BuildQuestStageAdvancedDetail(definition, runtimeState));
        }

        private void HandleQuestAccepted(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            if (definition == null)
                return;

            Enqueue(
                FanfareStateId.QuestAccepted,
                "QUEST STARTED",
                BuildQuestAcceptedDetail(definition));
        }

        private void HandleQuestCompleted(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            if (definition == null)
                return;

            Enqueue(
                FanfareStateId.QuestCompleted,
                BuildQuestCompletedHeadline(definition),
                BuildQuestCompletedDetail(definition));
        }

        private void HandleFocusedQuestChanged(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            if (definition == null)
                return;

            if (runtimeState != null && runtimeState.completed)
                return;

            if (_boundQuestDirector != null && _boundQuestDirector.IsQuestCompleted(definition.SafeId))
                return;

            string headline = string.IsNullOrWhiteSpace(definition.title)
                ? "Quest Updated"
                : definition.title.Trim();

            string detail = string.IsNullOrWhiteSpace(definition.description)
                ? "Current quest changed."
                : definition.description.Trim();

            Enqueue(
                FanfareStateId.QuestFocused,
                InputPromptResolver.FormatBindingPlaceholders(headline, inputActions),
                InputPromptResolver.FormatBindingPlaceholders(detail, inputActions));
        }

        private void Enqueue(FanfareStateId stateId, string headline, string detail)
        {
            FanfareStateStyle style = ResolveStyle(stateId);
            string resolvedHeadline = string.IsNullOrWhiteSpace(headline) ? "COMPLETE" : headline.Trim();

            if (style.uppercaseHeadline)
                resolvedHeadline = resolvedHeadline.ToUpperInvariant();

            _pendingRequests.Enqueue(new FanfareRequest(stateId, resolvedHeadline, detail?.Trim() ?? string.Empty, style));

            if (!HasActiveRequest())
                ActivateNextRequest();
        }

        public void ShowRaceCountdown(int seconds, RaceCourseLine race)
        {
            if (seconds <= 0)
                return;

            ShowImmediate(
                FanfareStateId.RaceCountdown,
                seconds.ToString(),
                BuildRaceCountdownDetail(race));
        }

        public void ShowRaceGo(RaceCourseLine race)
        {
            ShowImmediate(
                FanfareStateId.RaceGo,
                "GO!",
                BuildRaceCountdownDetail(race));
        }

        private void ShowImmediate(FanfareStateId stateId, string headline, string detail)
        {
            FanfareStateStyle style = ResolveStyle(stateId);
            string resolvedHeadline = string.IsNullOrWhiteSpace(headline) ? "GO!" : headline.Trim();

            if (style.uppercaseHeadline)
                resolvedHeadline = resolvedHeadline.ToUpperInvariant();

            RemovePendingCountdownRequests();

            _headline = resolvedHeadline;
            _detail = detail?.Trim() ?? string.Empty;
            _activeStateId = stateId;
            _activeStyle = style;
            _duration = Mathf.Max(0.25f, style != null ? style.duration : defaultDuration);
            _showUntil = Time.unscaledTime + _duration;
        }

        private void RemovePendingCountdownRequests()
        {
            if (_pendingRequests.Count <= 0)
                return;

            int count = _pendingRequests.Count;
            for (int i = 0; i < count; i++)
            {
                FanfareRequest request = _pendingRequests.Dequeue();

                if (request.StateId == FanfareStateId.RaceCountdown || request.StateId == FanfareStateId.RaceGo)
                    continue;

                _pendingRequests.Enqueue(request);
            }
        }

        private void ActivateNextRequest()
        {
            if (_pendingRequests.Count <= 0)
            {
                _headline = string.Empty;
                _detail = string.Empty;
                _activeStateId = FanfareStateId.CustomDebug;
                _activeStyle = null;
                _showUntil = -1f;
                _duration = 0f;
                return;
            }

            FanfareRequest request = _pendingRequests.Dequeue();
            _headline = request.Headline;
            _detail = request.Detail;
            _activeStateId = request.StateId;
            _activeStyle = request.Style;
            _duration = Mathf.Max(0.5f, _activeStyle != null ? _activeStyle.duration : defaultDuration);
            _showUntil = Time.unscaledTime + _duration;
        }

        private bool HasActiveRequest()
        {
            return !string.IsNullOrWhiteSpace(_headline) && _activeStyle != null && Time.unscaledTime < _showUntil;
        }

        private void HideImmediate()
        {
            _headline = string.Empty;
            _detail = string.Empty;
            _activeStateId = FanfareStateId.CustomDebug;
            _activeStyle = null;
            _showUntil = -1f;
            _duration = 0f;
            _pendingRequests.Clear();

            if (_container != null)
                _container.style.display = DisplayStyle.None;
        }

        private void RefreshVisuals()
        {
            if (_container == null || _headlineLabel == null || _detailLabel == null)
                return;

            if (!HasActiveRequest())
                ActivateNextRequest();

            if (!HasActiveRequest())
            {
                _container.style.display = DisplayStyle.None;
                return;
            }

            float remaining = _showUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                ActivateNextRequest();

                if (!HasActiveRequest())
                {
                    _container.style.display = DisplayStyle.None;
                    return;
                }

                remaining = _showUntil - Time.unscaledTime;
            }

            FanfareAnimationSettings anim = ResolveAnimation(_activeStyle);
            float age = _duration - remaining;

            float intro01 = Mathf.Clamp01(age / Mathf.Max(0.01f, anim.introDuration));
            float outro01 = Mathf.Clamp01(remaining / Mathf.Max(0.01f, anim.outroDuration));
            float visibility = Mathf.Min(intro01, outro01);

            float pulse = 1f + Mathf.Sin(age * anim.pulseSpeed) * anim.pulseAmplitude;
            float headlineScale = Mathf.Lerp(anim.headlinePopScale, 1f, intro01) * pulse;
            float detailScale = Mathf.Lerp(anim.detailPopScale, 1f, intro01);
            float y = Mathf.Lerp(anim.introLiftPixels, 0f, intro01);

            ApplyStyleToLabels(_activeStyle, visibility, headlineScale, detailScale, y);
        }

        private void ApplyStyleToLabels(FanfareStateStyle style, float visibility, float headlineScale, float detailScale, float y)
        {
            _container.style.display = DisplayStyle.Flex;

            ApplyLabelBaseStyle(_headlineLabel, _headlineBaseStyle);
            ApplyLabelBaseStyle(_detailLabel, _detailBaseStyle);

            _headlineLabel.text = _headline;
            _headlineLabel.style.color = new StyleColor(new Color(style.headlineColor.r, style.headlineColor.g, style.headlineColor.b, visibility));
            _headlineLabel.style.opacity = visibility;
            _headlineLabel.style.scale = new Scale(new Vector3(headlineScale, headlineScale, 1f));
            _headlineLabel.style.translate = new Translate(0f, y);

            ApplyCustomTypography(_headlineLabel, style.headlineFontAsset, style.headlineFontSize);

            bool hasDetail = !string.IsNullOrWhiteSpace(_detail);
            _detailLabel.style.display = hasDetail ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasDetail)
            {
                _detailLabel.text = _detail;
                _detailLabel.style.color = new StyleColor(new Color(style.detailColor.r, style.detailColor.g, style.detailColor.b, visibility));
                _detailLabel.style.opacity = visibility;
                _detailLabel.style.scale = new Scale(new Vector3(detailScale, detailScale, 1f));
                _detailLabel.style.translate = new Translate(0f, y * 0.45f);

                ApplyCustomTypography(_detailLabel, style.detailFontAsset, style.detailFontSize);
            }
        }

        private static void ApplyLabelBaseStyle(Label label, LabelBaseStyle baseStyle)
        {
            if (label == null)
                return;

            label.style.unityFontDefinition = baseStyle.FontDefinition;

            if (baseStyle.FontSize > 0f)
                label.style.fontSize = baseStyle.FontSize;

            label.style.color = new StyleColor(baseStyle.Color);
        }

        private void ApplyCustomTypography(Label label, UnityEngine.Object fontAsset, float fontSize)
        {
            if (label == null)
                return;

            if (fontAsset != null && TryBuildFontDefinition(fontAsset, out FontDefinition def))
                label.style.unityFontDefinition = def;

            if (fontSize > 0f)
                label.style.fontSize = fontSize;
        }

        private bool TryBuildFontDefinition(UnityEngine.Object fontObject, out FontDefinition def)
        {
            if (fontObject is Font unityFont)
            {
                def = FontDefinition.FromFont(unityFont);
                return true;
            }

            if (fontObject is FontAsset textCoreFont)
            {
                def = FontDefinition.FromSDFFont(textCoreFont);
                return true;
            }

            Type objType = fontObject.GetType();
            if (objType != null && objType.Name == "TMP_FontAsset")
            {
                PropertyInfo sourceFontFileProp = objType.GetProperty("sourceFontFile", BindingFlags.Public | BindingFlags.Instance);
                if (sourceFontFileProp != null)
                {
                    object sourceFont = sourceFontFileProp.GetValue(fontObject, null);
                    if (sourceFont is Font fallbackFont)
                    {
                        def = FontDefinition.FromFont(fallbackFont);
                        return true;
                    }
                }
            }

            def = default;
            Debug.LogWarning($"[{nameof(ActivityCompletionFanfareUI)}] Unsupported font asset type '{fontObject.GetType().Name}' on '{fontObject.name}'.", this);
            return false;
        }

        private FanfareAnimationSettings ResolveAnimation(FanfareStateStyle style)
        {
            if (style?.animation == null)
                return defaultAnimation;

            return style.animation;
        }

        private FanfareStateStyle ResolveStyle(FanfareStateId stateId)
        {
            EnsureDefaultStateStyles();

            for (int i = 0; i < stateStyles.Count; i++)
            {
                FanfareStateStyle style = stateStyles[i];
                if (style != null && style.stateId == stateId)
                    return NormalizeStyle(style);
            }

            FanfareStateStyle fallback = new FanfareStateStyle
            {
                stateId = stateId,
                duration = defaultDuration,
                headlineColor = Color.white,
                detailColor = new Color(1f, 1f, 1f, 0.98f),
                animation = defaultAnimation.Clone()
            };

            return fallback;
        }

        private FanfareStateStyle NormalizeStyle(FanfareStateStyle style)
        {
            if (style == null)
                style = new FanfareStateStyle();

            if (style.duration <= 0f)
                style.duration = defaultDuration;

            if (style.animation == null)
                style.animation = defaultAnimation.Clone();

            return style;
        }

        private void EnsureDefaultStateStyles()
        {
            if (stateStyles == null)
                stateStyles = new List<FanfareStateStyle>();

            EnsureStyle(
                FanfareStateId.RaceVictory,
                new Color(0.55f, 1f, 0.62f, 1f),
                new Color(1f, 1f, 1f, 0.98f),
                2.5f);

            EnsureStyle(
                FanfareStateId.RaceFinish,
                new Color(0.93f, 0.97f, 1f, 1f),
                new Color(1f, 1f, 1f, 0.98f),
                2.5f);

            EnsureStyle(
                FanfareStateId.RaceFailure,
                new Color(1f, 0.40f, 0.40f, 1f),
                new Color(1f, 0.95f, 0.95f, 0.98f),
                2.1f);

            EnsureStyle(
                FanfareStateId.RaceCountdown,
                new Color(1f, 0.88f, 0.36f, 1f),
                new Color(1f, 0.985f, 0.94f, 0.99f),
                0.82f);

            EnsureStyle(
                FanfareStateId.RaceGo,
                new Color(0.42f, 1f, 0.62f, 1f),
                new Color(0.94f, 1f, 0.96f, 0.99f),
                1.1f);

            EnsureStyle(
                FanfareStateId.RescueSuccess,
                new Color(0.58f, 0.92f, 1f, 1f),
                new Color(0.94f, 0.98f, 1f, 0.98f),
                2.5f);

            EnsureStyle(
                FanfareStateId.RescueFailure,
                new Color(1f, 0.60f, 0.46f, 1f),
                new Color(1f, 0.96f, 0.94f, 0.98f),
                2.1f);

            EnsureStyle(
                FanfareStateId.Cancelled,
                new Color(1f, 0.84f, 0.52f, 1f),
                new Color(1f, 0.98f, 0.94f, 0.98f),
                1.75f);

            EnsureStyle(
                FanfareStateId.QuestFocused,
                new Color(0.78f, 0.92f, 1f, 1f),
                new Color(0.96f, 0.99f, 1f, 0.98f),
                2.15f);

            EnsureStyle(
                FanfareStateId.QuestAccepted,
                new Color(0.62f, 0.86f, 1f, 1f),
                new Color(0.96f, 0.99f, 1f, 0.98f),
                2.25f);

            EnsureStyle(
                FanfareStateId.QuestStageAdvanced,
                new Color(0.42f, 0.84f, 1f, 1f),
                new Color(0.96f, 0.99f, 1f, 0.98f),
                2.8f);

            EnsureStyle(
                FanfareStateId.QuestCompleted,
                new Color(1f, 0.88f, 0.36f, 1f),
                new Color(1f, 0.985f, 0.94f, 0.99f),
                3.15f);

            EnsureStyle(
                FanfareStateId.CustomDebug,
                new Color(1f, 1f, 1f, 1f),
                new Color(1f, 1f, 1f, 0.98f),
                defaultDuration);
        }

        private void EnsureStyle(FanfareStateId stateId, Color headlineColor, Color detailColor, float duration)
        {
            for (int i = 0; i < stateStyles.Count; i++)
            {
                if (stateStyles[i] != null && stateStyles[i].stateId == stateId)
                    return;
            }

            stateStyles.Add(new FanfareStateStyle
            {
                stateId = stateId,
                duration = duration,
                headlineColor = headlineColor,
                detailColor = detailColor,
                animation = defaultAnimation.Clone()
            });
        }

        private FanfareStateId GetRaceSuccessState(RaceCourseLine race)
        {
            if (race != null && (race.LastResolvedPlacement == 1 || race.LastChampionshipCompletedForFirstTime))
                return FanfareStateId.RaceVictory;

            return FanfareStateId.RaceFinish;
        }

        public void DebugShowConfiguredPayload()
        {
            Enqueue(debugState, debugHeadline, debugDetail);
        }

        public void DebugShowState(FanfareStateId stateId)
        {
            switch (stateId)
            {
                case FanfareStateId.RaceVictory:
                    Enqueue(stateId, "VICTORY", "1ST / 12  •  +$500  •  New PB");
                    break;

                case FanfareStateId.RaceFinish:
                    Enqueue(stateId, "FINISHED", "4TH / 12  •  +$125  •  61.42s");
                    break;

                case FanfareStateId.RaceFailure:
                    Enqueue(stateId, "FAILED", "Missed checkpoint.");
                    break;

                case FanfareStateId.RaceCountdown:
                    Enqueue(stateId, "3", "Race starting");
                    break;

                case FanfareStateId.RaceGo:
                    Enqueue(stateId, "GO!", "Race started");
                    break;

                case FanfareStateId.RescueSuccess:
                    Enqueue(stateId, "RESCUED", "Rank 3  •  +$200  •  98.4s");
                    break;

                case FanfareStateId.RescueFailure:
                    Enqueue(stateId, "BOTCHED", "Casualty lost.");
                    break;

                case FanfareStateId.Cancelled:
                    Enqueue(stateId, "CANCELLED", "Activity cancelled.");
                    break;

                case FanfareStateId.QuestFocused:
                    Enqueue(stateId, "LIFT BASICS", "Now that you have a pass, use the lift to move uphill instead of hiking or skating back up.");
                    break;

                case FanfareStateId.QuestAccepted:
                    Enqueue(stateId, "QUEST STARTED", "Lift Basics");
                    break;

                case FanfareStateId.QuestStageAdvanced:
                    Enqueue(stateId, "LEARN THE BASICS COMPLETE", "Unlocked: Ride Your First Lift  •  New goals: Board the lift, Dismount safely");
                    break;

                case FanfareStateId.QuestCompleted:
                    Enqueue(stateId, "FIRST DAY COMPLETE", "All 4 quest stages completed.");
                    break;

                case FanfareStateId.CustomDebug:
                    Enqueue(stateId, debugHeadline, debugDetail);
                    break;
            }
        }

        public void DebugClearQueueAndHide()
        {
            HideImmediate();
        }

        private static string BuildRaceSuccessHeadline(RaceCourseLine race)
        {
            if (race == null)
                return "FINISHED";

            if (race.LastChampionshipCompletedForFirstTime)
                return "CHAMPION";

            if (race.LastResolvedPlacement == 1)
                return "VICTORY";

            if (race.LastPersonalBestImproved)
                return "PERSONAL BEST";

            return "FINISHED";
        }

        private static string BuildRaceFailureHeadline(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                if (reason.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "WIPEOUT";

                if (reason.IndexOf("course", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "OFF LINE";
            }

            return "FAILED";
        }

        private static string BuildRaceSuccessDetail(RaceCourseLine race, string displayName)
        {
            if (race == null)
                return $"{displayName} complete";

            string parts = string.Empty;

            if (race.LastResolvedPlacement > 0 && race.LastResolvedEntrantCount > 0)
                AppendPart(ref parts, $"{ToOrdinal(race.LastResolvedPlacement)} / {race.LastResolvedEntrantCount}");

            if (race.LastRewardGranted > 0)
                AppendPart(ref parts, $"+${race.LastRewardGranted}");

            if (race.LastChampionshipCompletedForFirstTime && !string.IsNullOrWhiteSpace(race.LastChampionshipRewardSummary))
            {
                string rewardSummary = race.LastChampionshipRewardSummary.Replace("Permanent pass unlock: ", string.Empty).Trim();
                AppendPart(ref parts, rewardSummary);
            }
            else if (race.LastUnlockedLeagueNumber > 0)
            {
                AppendPart(ref parts, $"Unlocked {race.GetLeagueDisplayName(race.LastUnlockedLeagueNumber)}");
            }

            if (race.LastPersonalBestImproved)
                AppendPart(ref parts, "New PB");

            if (race.LastCompletionTimeSeconds > 0f)
                AppendPart(ref parts, $"{race.LastCompletionTimeSeconds:0.00}s");

            return parts;
        }

        private static string BuildRaceCountdownDetail(RaceCourseLine race)
        {
            if (race == null)
                return "Race starting";

            string parts = string.Empty;

            if (!string.IsNullOrWhiteSpace(race.RaceName))
                AppendPart(ref parts, race.RaceName);

            if (race.ActiveLeagueNumber > 0)
                AppendPart(ref parts, race.GetLeagueDisplayName(race.ActiveLeagueNumber));

            return string.IsNullOrWhiteSpace(parts) ? "Race starting" : parts;
        }

        private static string BuildRescueSuccessHeadline(RescueService rescue)
        {
            if (rescue != null && rescue.LastMissionRankIncreased)
                return "PROMOTED";

            return "RESCUED";
        }

        private static string BuildRescueSuccessDetail(RescueService rescue, string displayName)
        {
            if (rescue == null)
                return $"{displayName} complete";

            string parts = string.Empty;

            int rescueRank = Mathf.Max(1, RaceRescueProgression.GetRescueCareerRank());
            AppendPart(ref parts, $"Rank {rescueRank}");

            if (rescue.LastMissionRewardGranted > 0)
                AppendPart(ref parts, $"+${rescue.LastMissionRewardGranted}");

            if (rescue.LastMissionRankIncreased)
                AppendPart(ref parts, $"Career up to {Mathf.Max(1, rescue.LastMissionNewRank)}");

            if (rescue.LastMissionCompletionSeconds > 0f)
                AppendPart(ref parts, $"{rescue.LastMissionCompletionSeconds:0.0}s");

            return parts;
        }

        private static string BuildQuestStageAdvancedHeadline(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            int completedStageIndex = Mathf.Max(0, runtimeState.currentStageIndex - 1);
            QuestStageDefinition completedStage = definition.GetStage(completedStageIndex);

            if (completedStage != null && !string.IsNullOrWhiteSpace(completedStage.title))
                return $"{completedStage.title.Trim()} COMPLETE";

            return definition.stages != null && definition.stages.Count > 1
                ? $"STAGE {completedStageIndex + 1} COMPLETE"
                : "QUEST PROGRESS";
        }

        private static string BuildQuestAcceptedDetail(QuestDefinitionSO definition)
        {
            if (definition == null)
                return "Quest accepted.";

            if (!string.IsNullOrWhiteSpace(definition.title))
                return definition.title.Trim();

            if (!string.IsNullOrWhiteSpace(definition.description))
                return definition.description.Trim();

            return "Quest accepted.";
        }

        private static string BuildQuestStageAdvancedDetail(QuestDefinitionSO definition, QuestRuntimeState runtimeState)
        {
            QuestStageDefinition unlockedStage = definition.GetStage(runtimeState.currentStageIndex);
            if (unlockedStage == null)
                return "Quest progress updated.";

            string parts = string.Empty;

            if (!string.IsNullOrWhiteSpace(unlockedStage.title))
                AppendPart(ref parts, $"Unlocked: {unlockedStage.title.Trim()}");

            string unlockedObjectives = BuildStageObjectiveUnlockSummary(unlockedStage, 2);
            if (!string.IsNullOrWhiteSpace(unlockedObjectives))
                AppendPart(ref parts, unlockedObjectives);

            if (string.IsNullOrWhiteSpace(parts))
                parts = "New quest objectives available.";

            return parts;
        }

        private static string BuildQuestCompletedHeadline(QuestDefinitionSO definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.title))
                return "QUEST COMPLETE";

            return $"{definition.title.Trim()} COMPLETE";
        }

        private static string BuildQuestCompletedDetail(QuestDefinitionSO definition)
        {
            int stageCount = definition?.stages != null ? definition.stages.Count : 0;

            if (stageCount > 1)
                return $"All {stageCount} quest stages completed.";

            return "Quest fully completed.";
        }

        private static string BuildStageObjectiveUnlockSummary(QuestStageDefinition stage, int maxObjectives)
        {
            if (stage?.objectives == null || stage.objectives.Count == 0 || maxObjectives <= 0)
                return string.Empty;

            List<string> labels = new List<string>(maxObjectives);

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                QuestObjectiveDefinition objective = stage.objectives[i];
                if (objective == null)
                    continue;

                string label = !string.IsNullOrWhiteSpace(objective.title)
                    ? objective.title.Trim()
                    : objective.BuildPromptLabel();

                if (string.IsNullOrWhiteSpace(label))
                    continue;

                labels.Add(label);

                if (labels.Count >= maxObjectives)
                    break;
            }

            if (labels.Count == 0)
                return string.Empty;

            if (stage.objectives.Count > labels.Count)
                return $"New goals: {string.Join(", ", labels)}...";

            return $"New goals: {string.Join(", ", labels)}";
        }

        private static string BuildFailureDetail(string reason, string fallback)
        {
            return string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
        }

        private static void AppendPart(ref string buffer, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!string.IsNullOrWhiteSpace(buffer))
                buffer += "  •  ";

            buffer += value.Trim();
        }

        private static string ToOrdinal(int value)
        {
            int abs = Mathf.Abs(value);
            int lastTwo = abs % 100;

            if (lastTwo is >= 11 and <= 13)
                return abs + "TH";

            return (abs % 10) switch
            {
                1 => abs + "ST",
                2 => abs + "ND",
                3 => abs + "RD",
                _ => abs + "TH"
            };
        }
    }
}
