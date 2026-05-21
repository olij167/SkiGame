using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentPathRebuildScheduler
    {
        private sealed class PendingRequest
        {
            public ModularPathSpawner path;
            public EditorWindow ownerWindow;
            public double rebuildAt;
            public string undoName;
        }

        private static readonly Dictionary<int, PendingRequest> Pending = new Dictionary<int, PendingRequest>();
        private static double _nextAllowedSceneRepaintTime;
        private static double _nextAllowedWindowRepaintTime;

        public static event Action<ModularPathSpawner> Rebuilt;

        static PungentPathRebuildScheduler()
        {
            EditorApplication.update += EditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Pending.Clear;
        }

        public static void RequestPreviewRefresh(ModularPathSpawner path, EditorWindow ownerWindow = null)
        {
            if (path == null)
                return;

            EditorUtility.SetDirty(path);
            RequestRepaints(ownerWindow);
        }

        public static void QueueRebuild(ModularPathSpawner path, EditorWindow ownerWindow = null, string undoName = "Rebuild Modular Path")
        {
            if (path == null)
                return;

            RequestPreviewRefresh(path, ownerWindow);
            if (!path.autoRebuildInEditor)
                return;

            int id = path.GetInstanceID();
            double rebuildAt = EditorApplication.timeSinceStartup + Math.Max(0d, path.editorRebuildDebounce);
            if (!Pending.TryGetValue(id, out PendingRequest request))
            {
                request = new PendingRequest { path = path };
                Pending[id] = request;
            }

            request.path = path;
            request.ownerWindow = ownerWindow ?? request.ownerWindow;
            request.rebuildAt = rebuildAt;
            request.undoName = string.IsNullOrWhiteSpace(undoName) ? "Rebuild Modular Path" : undoName;
        }

        public static void RebuildNow(ModularPathSpawner path, EditorWindow ownerWindow = null, string undoName = "Rebuild Modular Path")
        {
            if (path == null)
                return;

            Pending.Remove(path.GetInstanceID());
            ExecuteRebuild(path, ownerWindow, undoName);
        }

        public static bool HasPendingRebuild(ModularPathSpawner path)
        {
            return path != null && Pending.ContainsKey(path.GetInstanceID());
        }

        private static void EditorUpdate()
        {
            if (Pending.Count == 0)
                return;

            double now = EditorApplication.timeSinceStartup;
            s_ReadyIds.Clear();
            foreach (KeyValuePair<int, PendingRequest> pair in Pending)
            {
                PendingRequest request = pair.Value;
                if (request == null || request.path == null)
                {
                    s_ReadyIds.Add(pair.Key);
                    continue;
                }

                if (now >= request.rebuildAt)
                    s_ReadyIds.Add(pair.Key);
            }

            for (int i = 0; i < s_ReadyIds.Count; i++)
            {
                int id = s_ReadyIds[i];
                if (!Pending.TryGetValue(id, out PendingRequest request))
                    continue;

                Pending.Remove(id);
                if (request != null && request.path != null && request.path.autoRebuildInEditor)
                    ExecuteRebuild(request.path, request.ownerWindow, request.undoName);
            }

            s_ReadyIds.Clear();
        }

        private static readonly List<int> s_ReadyIds = new List<int>(16);

        private static void ExecuteRebuild(ModularPathSpawner path, EditorWindow ownerWindow, string undoName)
        {
            if (path == null)
                return;

            Undo.RecordObject(path, string.IsNullOrWhiteSpace(undoName) ? "Rebuild Modular Path" : undoName);
            path.Rebuild();
            EditorUtility.SetDirty(path);
            Rebuilt?.Invoke(path);
            RequestRepaints(ownerWindow);
        }

        private static void RequestRepaints(EditorWindow ownerWindow)
        {
            PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.08d);
            if (ownerWindow != null)
                PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(ownerWindow, ref _nextAllowedWindowRepaintTime, 0.08d);
        }
    }
#endif
}
