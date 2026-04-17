#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mighty
{
    public class PopOutWindow : EditorWindow
    {
        VisualElement originalContent;
        public void AddContent(VisualElement content)
        {
            originalContent = content;
            rootVisualElement.Add(content);
        }

        PopOutWindow()
        {
            Debug.Log("PopOutWindow");

        }

        private void OnEnable()
        {
            Debug.Log("PopOutWindow OnEnable");
            // rootVisualElement.Add(new Label("Hello World"));
            rootVisualElement.Add(new Label(this.name));
            rootVisualElement.Add(originalContent);
        }

    }
}
#endif