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
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private float refreshInterval = 0.1f;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _labelAction;
        private Label _labelDescription;

        private float _nextRefreshTime;
        private readonly List<IWorldInteractionPromptSource> _sources = new();

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

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
            _labelAction = _root.Q<Label>("Lbl_WorldPromptAction");
            _labelDescription = _root.Q<Label>("Lbl_WorldPromptDescription");
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

            string binding = GetBindingDisplay(interactActionName);
            string actionText = string.IsNullOrWhiteSpace(binding)
                ? best.PromptActionText
                : $"{(best.PromptUsesHold ? "Hold" : "Press")} {binding}";

            if (_labelAction != null)
                _labelAction.text = actionText;

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

        private string GetBindingDisplay(string actionName)
        {
            if (inputActions == null || string.IsNullOrWhiteSpace(actionName))
                return string.Empty;

            foreach (var map in inputActions.actionMaps)
            {
                var action = map.FindAction(actionName, throwIfNotFound: false);
                if (action == null)
                    continue;

                int bindingIndex = FindPrimaryBindingIndex(action);
                return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            }

            return string.Empty;
        }

        private static int FindPrimaryBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (!string.IsNullOrEmpty(b.path)) return i;
            }

            return 0;
        }
    }
}