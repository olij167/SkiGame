#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mighty
{
    public interface IPopOutWindow
    {
        void AddContent(VisualElement content);
    }
}
#endif