using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SkiGame.UI
{
    public enum InputPromptTokenType
    {
        Text,
        Separator,
        Icon
    }

    public readonly struct InputPromptToken
    {
        public readonly InputPromptTokenType Type;
        public readonly string Value;
        public readonly Sprite Sprite;

        public InputPromptToken(InputPromptTokenType type, string value, Sprite sprite = null)
        {
            Type = type;
            Value = value ?? string.Empty;
            Sprite = sprite;
        }

        public static InputPromptToken Text(string value) => new(InputPromptTokenType.Text, value);
        public static InputPromptToken Separator(string value) => new(InputPromptTokenType.Separator, value);
        public static InputPromptToken Icon(string key, Sprite sprite, string fallbackText) => new(InputPromptTokenType.Icon, key ?? fallbackText, sprite);
        public string GetSignaturePart() => $"{Type}:{Value}";
    }

    public static class InputPromptTokens
    {
        public static InputPromptToken[] Concat(params IReadOnlyList<InputPromptToken>[] groups)
        {
            int total = 0;
            for (int i = 0; i < groups.Length; i++)
                total += groups[i]?.Count ?? 0;

            if (total == 0)
                return new[] { InputPromptToken.Text("-") };

            var result = new InputPromptToken[total];
            int index = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                if (group == null)
                    continue;

                for (int j = 0; j < group.Count; j++)
                    result[index++] = group[j];
            }

            return result;
        }

        public static InputPromptToken[] Text(string value)
        {
            return new[] { InputPromptToken.Text(string.IsNullOrWhiteSpace(value) ? "-" : value.Trim()) };
        }

        public static InputPromptToken[] Phrase(string prefix, IReadOnlyList<InputPromptToken> prompt)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return prompt as InputPromptToken[] ?? Concat(prompt);

            return Concat(Text(prefix), prompt);
        }
    }

    public static class InputPromptResolver
    {
        private const string DefaultLibraryResourcePath = "InputPromptIconLibrary_Default";
        private static InputPromptIconLibrary _cachedDefaultLibrary;

        public static InputPromptIconLibrary LoadDefaultLibrary()
        {
            if (_cachedDefaultLibrary == null)
                _cachedDefaultLibrary = Resources.Load<InputPromptIconLibrary>(DefaultLibraryResourcePath);

            return _cachedDefaultLibrary;
        }

        public static InputPromptToken[] ResolveActionTokens(InputActionAsset inputActions, string actionName, InputPromptIconLibrary library = null, params string[] compositePartNames)
        {
            var action = FindActionByFriendlyName(inputActions, actionName);
            if (action == null)
                return InputPromptTokens.Text("-");

            int bindingIndex = compositePartNames != null && compositePartNames.Length > 0
                ? FindCompositePartBindingIndex(action, compositePartNames)
                : FindPrimaryBindingIndex(action);

            return ResolveBindingTokens(action, bindingIndex, library);
        }

        public static string GetBindingDisplay(InputActionAsset inputActions, string actionName, params string[] compositePartNames)
        {
            var action = FindActionByFriendlyName(inputActions, actionName);
            if (action == null)
                return "-";

            int bindingIndex = compositePartNames != null && compositePartNames.Length > 0
                ? FindCompositePartBindingIndex(action, compositePartNames)
                : FindPrimaryBindingIndex(action);

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return "-";

            return CondenseBindingDisplay(action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames));
        }

        public static string GetBindingDisplay(InputAction action, params string[] compositePartNames)
        {
            if (action == null)
                return "-";

            int bindingIndex = compositePartNames != null && compositePartNames.Length > 0
                ? FindCompositePartBindingIndex(action, compositePartNames)
                : FindPrimaryBindingIndex(action);

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return "-";

            return CondenseBindingDisplay(action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames));
        }

        public static string CombineBindings(InputActionAsset inputActions, params string[] actionNames)
        {
            if (inputActions == null || actionNames == null || actionNames.Length == 0)
                return "-";

            var builder = new System.Text.StringBuilder(actionNames.Length * 8);
            for (int i = 0; i < actionNames.Length; i++)
            {
                if (i > 0)
                    builder.Append(" / ");

                builder.Append(GetBindingDisplay(inputActions, actionNames[i]));
            }

            return builder.ToString();
        }

        public static InputPromptToken[] ResolveBindingTokens(InputAction action, int bindingIndex, InputPromptIconLibrary library = null)
        {
            library ??= LoadDefaultLibrary();

            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return InputPromptTokens.Text("-");

            string display = CondenseBindingDisplay(action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames));
            var binding = action.bindings[bindingIndex];
            string effectivePath = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;

            if (TryBuildIconToken(effectivePath, display, library, out var token))
                return new[] { token };

            return InputPromptTokens.Text(display);
        }

        public static InputAction FindActionByFriendlyName(InputActionAsset inputActions, string actionName)
        {
            if (inputActions == null || string.IsNullOrWhiteSpace(actionName))
                return null;

            string normalizedTarget = Normalize(actionName);

            foreach (var map in inputActions.actionMaps)
            {
                for (int i = 0; i < map.actions.Count; i++)
                {
                    var action = map.actions[i];
                    if (Normalize(action.name) == normalizedTarget)
                        return action;
                }
            }

            return null;
        }

        public static string CondenseBindingDisplay(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";

            return value
                .Replace("Left Mouse Button", "LMB")
                .Replace("Right Mouse Button", "RMB")
                .Replace("Middle Mouse Button", "MMB")
                .Replace("Mouse Scroll Wheel", "Scroll")
                .Replace("Scroll Wheel", "Scroll")
                .Replace("Left Shift", "Shift")
                .Replace("Right Shift", "Shift")
                .Replace("Control", "Ctrl")
                .Replace("Left Ctrl", "Ctrl")
                .Replace("Right Ctrl", "Ctrl")
                .Replace("Left Alt", "Alt")
                .Replace("Right Alt", "Alt")
                .Replace("Up Arrow", "Up")
                .Replace("Down Arrow", "Down")
                .Replace("Left Arrow", "Left")
                .Replace("Right Arrow", "Right")
                .Replace("Button South", "A")
                .Replace("Button North", "Y")
                .Replace("Button West", "X")
                .Replace("Button East", "B")
                .Trim();
        }

        public static int FindPrimaryBindingIndex(InputAction action)
        {
            if (action == null)
                return 0;

            int fallback = -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.path))
                    continue;

                if (fallback < 0)
                    fallback = i;

                if (IsKeyboardMouseBinding(binding))
                    return i;
            }

            return fallback >= 0 ? fallback : 0;
        }

        public static int FindCompositePartBindingIndex(InputAction action, params string[] partNames)
        {
            if (action == null)
                return 0;

            int fallback = -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.name))
                    continue;

                bool matchesPart = false;
                for (int p = 0; p < partNames.Length; p++)
                {
                    if (string.Equals(binding.name, partNames[p], StringComparison.OrdinalIgnoreCase))
                    {
                        matchesPart = true;
                        break;
                    }
                }

                if (!matchesPart)
                    continue;

                if (fallback < 0)
                    fallback = i;

                if (IsKeyboardMouseBinding(binding))
                    return i;
            }

            return fallback >= 0 ? fallback : FindPrimaryBindingIndex(action);
        }

        public static string BuildSignature(IReadOnlyList<InputPromptToken> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return "text:-";

            var signature = new System.Text.StringBuilder(tokens.Count * 12);
            for (int i = 0; i < tokens.Count; i++)
                signature.Append(tokens[i].GetSignaturePart()).Append(';');

            return signature.ToString();
        }

        private static bool IsKeyboardMouseBinding(InputBinding binding)
        {
            string path = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
            return !string.IsNullOrWhiteSpace(path) &&
                   (path.IndexOf("<Keyboard>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("<Mouse>", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool TryBuildIconToken(string effectivePath, string display, InputPromptIconLibrary library, out InputPromptToken token)
        {
            if (TryGetIconKey(effectivePath, out var iconKey))
            {
                string fallback = string.IsNullOrWhiteSpace(display) ? iconKey : display;
                if (library != null && library.TryGetIcon(iconKey, out var sprite, out var libraryFallback))
                {
                    if (!string.IsNullOrWhiteSpace(libraryFallback))
                        fallback = libraryFallback;

                    token = InputPromptToken.Icon(iconKey, sprite, fallback);
                    return true;
                }
            }

            token = default;
            return false;
        }

        private static bool TryGetIconKey(string effectivePath, out string key)
        {
            key = null;
            if (string.IsNullOrWhiteSpace(effectivePath))
                return false;

            if (effectivePath.IndexOf("<Keyboard>/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string control = effectivePath[(effectivePath.IndexOf('/') + 1)..];
                switch (control)
                {
                    case "leftShift":
                    case "rightShift":
                    case "shift":
                        key = "keyboard:shift";
                        return true;
                    case "leftCtrl":
                    case "rightCtrl":
                    case "ctrl":
                        key = "keyboard:ctrl";
                        return true;
                    case "leftAlt":
                    case "rightAlt":
                    case "alt":
                        key = "keyboard:alt";
                        return true;
                    case "escape":
                        key = "keyboard:esc";
                        return true;
                    case "space":
                        key = "keyboard:space";
                        return true;
                    case "enter":
                    case "numpadEnter":
                        key = "keyboard:enter";
                        return true;
                    case "tab":
                        key = "keyboard:tab";
                        return true;
                    case "pause":
                        key = "keyboard:pause";
                        return true;
                    case "upArrow":
                        key = "keyboard:arrow-up";
                        return true;
                    case "downArrow":
                        key = "keyboard:arrow-down";
                        return true;
                    case "leftArrow":
                        key = "keyboard:arrow-left";
                        return true;
                    case "rightArrow":
                        key = "keyboard:arrow-right";
                        return true;
                }

                if (control.Length == 1 && char.IsLetterOrDigit(control[0]))
                {
                    key = $"keyboard:{control.ToLowerInvariant()}";
                    return true;
                }
            }

            if (effectivePath.IndexOf("<Mouse>/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string control = effectivePath[(effectivePath.IndexOf('/') + 1)..];
                switch (control)
                {
                    case "leftButton":
                        key = "mouse:left";
                        return true;
                    case "rightButton":
                        key = "mouse:right";
                        return true;
                    case "middleButton":
                        key = "mouse:middle";
                        return true;
                    case "delta/x":
                    case "x":
                        key = "mouse:move-hor";
                        return true;
                    case "delta/y":
                    case "y":
                        key = "mouse:move-vert";
                        return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }
    }

    public static class InputPromptVisualBuilder
    {
        public static VisualElement CreatePrompt(IReadOnlyList<InputPromptToken> tokens, params string[] classNames)
        {
            var root = new VisualElement();
            root.AddToClassList("input-prompt");
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexWrap = Wrap.NoWrap;
            root.style.alignItems = Align.Center;

            if (classNames != null)
            {
                for (int i = 0; i < classNames.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(classNames[i]))
                        root.AddToClassList(classNames[i]);
                }
            }

            Populate(root, tokens);
            return root;
        }

        public static void Populate(VisualElement root, IReadOnlyList<InputPromptToken> tokens)
        {
            if (root == null)
                return;

            root.Clear();
            if (tokens == null || tokens.Count == 0)
                tokens = InputPromptTokens.Text("-");

            for (int i = 0; i < tokens.Count; i++)
            {
                switch (tokens[i].Type)
                {
                    case InputPromptTokenType.Icon:
                        if (tokens[i].Sprite != null)
                        {
                            var image = new Image
                            {
                                sprite = tokens[i].Sprite,
                                scaleMode = ScaleMode.ScaleToFit,
                                tooltip = tokens[i].Value
                            };
                            image.AddToClassList("input-prompt-icon");
                            root.Add(image);
                            break;
                        }

                        goto default;

                    case InputPromptTokenType.Separator:
                        var separator = new Label(tokens[i].Value);
                        separator.AddToClassList("input-prompt-separator");
                        root.Add(separator);
                        break;

                    default:
                        var label = new Label(tokens[i].Value);
                        label.AddToClassList("input-prompt-text");
                        root.Add(label);
                        break;
                }
            }
        }
    }
}
