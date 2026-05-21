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
        // Serializable scheduled-action save models used by the Debug Control Center.

        [Serializable]
        private sealed class ScheduledCall
        {
            public UnityEngine.Object target;
            public string targetGlobalId;
            public string targetScenePath;
            public string targetTypeName;
            public ScheduledActionKind actionKind = ScheduledActionKind.ContextMethod;
            public string methodName;
            public string displayName;
            public string routerChannel = "Scheduler";
            public string routerMessage = "Scheduled debug log";
            public int routerLevel = (int)DebugRouter.Level.Info;
            public string routerSignalName = "ScheduledEvent";
            public string routerStateName = "ScheduledState";
            public bool routerStateValue = true;
            public bool enabled = true;
            public bool foldout = true;
            public float intervalSeconds = 5f;
            public double nextRunTime;
            public ConditionMode conditionMode = ConditionMode.PlayModeOnly;
            public UnityEngine.Object conditionTarget;
            public string conditionTargetGlobalId;
            public string conditionScenePath;
            public string conditionTargetTypeName;
            public string conditionMemberName;
            public int fireCount;
            public string lastResult = "Never";
        }

        [Serializable]
        private sealed class ScheduledCallSaveData
        {
            public List<ScheduledCallSave> calls = new List<ScheduledCallSave>();
        }

        [Serializable]
        private sealed class ScheduledCallSave
        {
            public string targetGlobalId;
            public string targetScenePath;
            public string targetTypeName;
            public int actionKind;
            public string methodName;
            public string displayName;
            public string routerChannel;
            public string routerMessage;
            public int routerLevel;
            public string routerSignalName;
            public string routerStateName;
            public bool routerStateValue;
            public bool enabled;
            public bool foldout;
            public float intervalSeconds;
            public int conditionMode;
            public string conditionTargetGlobalId;
            public string conditionScenePath;
            public string conditionTargetTypeName;
            public string conditionMemberName;
            public int fireCount;
            public string lastResult;
        }

    }
#endif
}
