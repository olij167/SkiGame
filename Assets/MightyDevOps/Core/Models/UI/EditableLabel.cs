using System;
using UnityEngine;
using UnityEngine.UIElements;

public class EditableLabel
{
    private TextField textField;
    private Action<string> onValueChanged;
    private bool isEditable;

    private EventCallback<FocusInEvent> focusInCallback;
    private EventCallback<FocusOutEvent> focusOutCallback;
    private EventCallback<KeyDownEvent> keyDownCallback;

    public EditableLabel(TextField textField, Action<string> onValueChanged, bool isEditable = true)
    {
        this.textField = textField;
        this.textField.isDelayed = false;
        this.onValueChanged = onValueChanged;

        textField.RegisterValueChangedCallback((evt) =>
        {
            onValueChanged?.Invoke(evt.newValue);
        });
        this.isEditable = isEditable;

        StyleTextField();  // Apply the style

        if (isEditable)
            RegisterCallbacks();  // Register callbacks only if editable
        else textField.isReadOnly = true;
    }

    private void StyleTextField()
    {
        // Apply the style
        var textFieldContainer = textField.Q<VisualElement>(name: "unity-text-input");
        textFieldContainer.style.borderTopWidth = textFieldContainer.style.borderRightWidth = textFieldContainer.style.borderBottomWidth = textFieldContainer.style.borderLeftWidth = 0;
        textFieldContainer.style.borderTopLeftRadius = textFieldContainer.style.borderTopRightRadius = textFieldContainer.style.borderBottomRightRadius = textFieldContainer.style.borderBottomLeftRadius = 0;
        textFieldContainer.style.backgroundColor = new Color(0, 0, 0, 0);
        textFieldContainer.style.unityTextAlign = TextAnchor.MiddleLeft;

    }

    private void RegisterCallbacks()
    {
        focusInCallback = (FocusInEvent e) =>
        {
            textField.AddToClassList("editing");
        };
        textField.RegisterCallback(focusInCallback);

        focusOutCallback = (FocusOutEvent e) =>
        {
            textField.RemoveFromClassList("editing");
            ApplyChanges();
        };
        textField.RegisterCallback(focusOutCallback);

        keyDownCallback = (KeyDownEvent e) =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                textField.value = this.textField.value;
                textField.Blur();
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                ApplyChanges();
                textField.Blur();
                e.StopPropagation();
            }
        };
        textField.RegisterCallback(keyDownCallback);
    }

    private void ApplyChanges()
    {
        onValueChanged?.Invoke(textField.value);
    }

    public void SetEditable(bool isEditable)
    {
        this.isEditable = isEditable;
        if (isEditable)
            RegisterCallbacks();
        else
        {
            textField.UnregisterCallback(focusInCallback);
            textField.UnregisterCallback(focusOutCallback);
            textField.UnregisterCallback(keyDownCallback);
        }
    }
}