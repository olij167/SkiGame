using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SkiGame.Progression;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class WorldInteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private InputPromptIconLibrary iconLibrary;
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private float refreshInterval = 0.1f;

        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _actionHost;
        private Label _labelDescription;

        private float _nextRefreshTime;
        private readonly List<IWorldInteractionPromptSource> _sources = new();

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (inputActions == null)
                inputActions = new InputSystem_Actions().asset;

            if (iconLibrary == null)
                iconLibrary = InputPromptResolver.LoadDefaultLibrary();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            _root = document.rootVisualElement;
            Bind();
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        private void Bind()
        {
            _panel = _root.Q<VisualElement>("WorldPromptPanel");
            _labelDescription = _root.Q<Label>("Lbl_WorldPromptDescription");

            var existingLabel = _root.Q<Label>("Lbl_WorldPromptAction");
            if (existingLabel != null)
            {
                var parent = existingLabel.parent;
                if (parent != null)
                {
                    _actionHost = InputPromptVisualBuilder.CreatePrompt(InputPromptTokens.Text(existingLabel.text), "world-prompt-action");
                    _actionHost.name = "WorldPromptActionHost";
                    parent.Insert(parent.IndexOf(existingLabel), _actionHost);
                    parent.Remove(existingLabel);
                }
            }
        }

        private void Refresh()
        {
            bool enabledBySettings = GameSettingsService.Current == null || GameSettingsService.Current.showWorldInteractionPrompts;
            if (!enabledBySettings)
            {
                SetVisible(false);
                return;
            }

            CollectSources();

            IWorldInteractionPromptSource best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null || !src.IsPromptAvailable)
                    continue;

                if (src.PromptPriority > bestPriority)
                {
                    best = src;
                    bestPriority = src.PromptPriority;
                }
            }

            if (best == null)
            {
                SetVisible(false);
                return;
            }

            if (_actionHost != null)
            {
                var actionTokens = InputPromptResolver.ResolveActionTokens(inputActions, interactActionName, iconLibrary);
                var promptTokens = string.IsNullOrWhiteSpace(best.PromptActionText)
                    ? InputPromptTokens.Phrase(best.PromptUsesHold ? "Hold" : "Press", actionTokens)
                    : InputPromptTokens.Phrase(best.PromptUsesHold ? "Hold" : "Press", actionTokens);

                InputPromptVisualBuilder.Populate(_actionHost, promptTokens);
            }

            if (_labelDescription != null)
                _labelDescription.text = best.PromptDescriptionText;

            SetVisible(true);
        }

        private void CollectSources()
        {
            _sources.Clear();

#if UNITY_2023_1_OR_NEWER
            var monoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var monoBehaviours = FindObjectsOfType<MonoBehaviour>();
#endif

            for (int i = 0; i < monoBehaviours.Length; i++)
            {
                if (monoBehaviours[i] is IWorldInteractionPromptSource src)
                    _sources.Add(src);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_panel != null)
                _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

    }
}
