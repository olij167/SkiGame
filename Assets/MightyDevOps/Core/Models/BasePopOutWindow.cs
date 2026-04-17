#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;

namespace Mighty
{
    public abstract class BasePopOutWindow : EditorWindow, IPopOutWindow
    {
        protected VisualElement content;

        public virtual void AddContent(VisualElement content)
        {
            rootVisualElement.Clear();
            this.content = content;

            // Create the Toolbar
            var toolbar = new Toolbar();

            // Create the Pop Back button
            var popBackButton = new ToolbarButton(() =>
            {
                // Implementation for popping back to regular editor
                // You might need to call some code to register this window back to your regular editor
                DevLog("Pop Back Button Pressed!");
                // this.titleContent.text = "Pop Back";
                OnPopBack();
            })
            {
                text = "Pop Back"
            };

            // Add the button to the Toolbar
            toolbar.Add(popBackButton);

            // Add the Toolbar to the root visual element
            rootVisualElement.Add(toolbar);

            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.Add(content);

            // Add the ScrollView below the Toolbar
            rootVisualElement.Add(scrollView);
        }

        protected virtual void OnPopBack()
        {
            DevLog("Pop Back Button Pressed in BasePopOutWindow!");

        }

        public virtual void OnDisable()
        {
            Mighty.MightyCoreData.windowManagerStateful.DeregisterWindow(this.titleContent.text);
        }
    }
}
#endif