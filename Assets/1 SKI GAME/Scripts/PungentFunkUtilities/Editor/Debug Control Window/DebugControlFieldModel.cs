using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Debugging;

namespace PungentFunk.Utilities.Editor.Debugging
{
#if UNITY_EDITOR
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public sealed partial class DebugControlWindow
    {
        // Discovered component/static-field models used by the Debug Control Center.

        private sealed class DebugComponentInfo
        {
            public MonoBehaviour component;
            public string objectPath;
            public List<FieldInfo> boolToggles = new List<FieldInfo>();
            public List<MethodInfo> contextActions = new List<MethodInfo>();
        }

        private sealed class StaticDebugFieldInfo
        {
            public Type declaringType;
            public FieldInfo field;
        }

    }
#endif
}
