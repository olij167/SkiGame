#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif
public partial class ToggleButton : Button
{
    private bool _isToggled;
    public bool IsToggled
    {
        get => _isToggled;
        set
        {
            if (_isToggled != value)
            {
                _isToggled = value;
                UpdateVisualState();
            }
        }
    }

    public ToggleButton()
    {
        text = "Toggle"; // Default text
        clicked += ToggleState;
        UpdateVisualState();
    }

    private void ToggleState()
    {
        IsToggled = !IsToggled;
    }

    private void UpdateVisualState()
    {
        if (_isToggled)
        {
            style.backgroundColor = new StyleColor(Color.white);
            style.color = Color.black;
        }
        else
        {
            style.backgroundColor = new StyleColor(Color.black);
            style.color = Color.white;
        }
    }

#if UNITY_2023_3_OR_NEWER && !UNITY_6000_0_OR_NEWER
    public new class UxmlFactory : UxmlFactory<ToggleButton, UxmlTraits> { }

    public new class UxmlTraits : Button.UxmlTraits { }
#endif
}
#endif