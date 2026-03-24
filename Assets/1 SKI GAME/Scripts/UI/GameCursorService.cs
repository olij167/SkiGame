using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SkiGame.UI
{
    public enum GameCursorMode
    {
        HiddenLocked = 0,
        VisibleUnlocked = 100,
    }

    public static class GameCursorService
    {
        private struct CursorRequest
        {
            public int priority;
            public GameCursorMode mode;
        }

        private static readonly Dictionary<int, CursorRequest> Requests = new();

        public static void Request(object owner, GameCursorMode mode, int priority)
        {
            if (owner == null) return;

            int key = RuntimeHelpers.GetHashCode(owner);
            Requests[key] = new CursorRequest
            {
                priority = priority,
                mode = mode
            };

            Apply();
        }

        public static void Release(object owner)
        {
            if (owner == null) return;

            int key = RuntimeHelpers.GetHashCode(owner);
            if (Requests.Remove(key))
                Apply();
        }

        public static void ClearAll()
        {
            Requests.Clear();
            Apply();
        }

        private static void Apply()
        {
            GameCursorMode resolvedMode = GameCursorMode.HiddenLocked;
            int bestPriority = int.MinValue;

            foreach (var kvp in Requests)
            {
                var req = kvp.Value;
                if (req.priority >= bestPriority)
                {
                    bestPriority = req.priority;
                    resolvedMode = req.mode;
                }
            }

            switch (resolvedMode)
            {
                case GameCursorMode.VisibleUnlocked:
                    UnityEngine.Cursor.visible = true;
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    break;

                default:
                    UnityEngine.Cursor.visible = false;
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    break;
            }
        }
    }
}