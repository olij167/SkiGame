using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class ModalHelper
{
    public static void ShowConfirmationModal(VisualElement root, string modalTitle, string modalMessage, Action onConfirm, Action onCancel)
    {
        VisualElement overlay = new VisualElement
        {
            name = "ConfirmationModalOverlay",
            style =
            {
                position = Position.Absolute,
                top = 0,
                left = 0,
                right = 0,
                bottom = 0,
                backgroundColor = new Color(0f, 0f, 0f, 0.6f),
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.Center,
                alignItems = Align.Center
            }
        };

        VisualElement container = new VisualElement
        {
            name = "ConfirmationModalContainer",
            style =
            {
                backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f),
                borderTopLeftRadius = 10,
                borderTopRightRadius = 10,
                borderBottomLeftRadius = 10,
                borderBottomRightRadius = 10,
                paddingLeft = 10,
                paddingRight = 10,
                paddingTop = 15,
                paddingBottom = 15,
                width = 400,
                maxWidth = Length.Percent(90),
                maxHeight = Length.Percent(80),
                flexDirection = FlexDirection.Column
            }
        };

        Label titleLabel = new Label(modalTitle)
        {
            style =
            {
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 16,
                color = Color.white,
                marginBottom = 10
            }
        };
        container.Add(titleLabel);

        Label messageLabel = new Label(modalMessage)
        {
            style =
            {
                color = Color.white,
                marginBottom = 10,
                whiteSpace = WhiteSpace.Normal, // Allow text wrapping.
                unityTextAlign = TextAnchor.MiddleLeft,
                maxWidth = 380
            }
        };
        container.Add(messageLabel);

        VisualElement buttons = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexEnd
            }
        };

        Button yesButton = new Button(() =>
        {
            onConfirm?.Invoke();
            overlay.RemoveFromHierarchy();
        })
        {
            text = "Yes, Confirm",
            style =
            {
                marginRight = 5,
                backgroundColor = new Color(0.8f, 0.0f, 0.0f, 1f),
                color = Color.white
            }
        };
        buttons.Add(yesButton);

        Button cancelButton = new Button(() =>
        {
            onCancel?.Invoke();
            overlay.RemoveFromHierarchy();
        })
        {
            text = "Cancel",
            style =
            {
                backgroundColor = new Color(0.6f, 0.2f, 0.2f),
                color = Color.white
            }
        };
        buttons.Add(cancelButton);

        container.Add(buttons);
        overlay.Add(container);
        root.Add(overlay);
    }
}

