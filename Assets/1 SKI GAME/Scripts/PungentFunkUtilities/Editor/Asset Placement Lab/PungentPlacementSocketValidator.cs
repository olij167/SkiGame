using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public static class PungentPlacementSocketValidator
    {
        public sealed class ValidationMessage
        {
            public Object context;
            public string severity;
            public string message;
        }

        public static List<ValidationMessage> Validate(GameObject root)
        {
            List<ValidationMessage> messages = new List<ValidationMessage>();
            if (root == null)
            {
                messages.Add(new ValidationMessage { severity = "Error", message = "Missing root object." });
                return messages;
            }

            PungentPlacementSocket[] sockets = root.GetComponentsInChildren<PungentPlacementSocket>(true);
            if (sockets.Length == 0)
            {
                messages.Add(new ValidationMessage { context = root, severity = "Warning", message = "No PungentPlacementSocket components found." });
                return messages;
            }

            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < sockets.Length; i++)
            {
                PungentPlacementSocket socket = sockets[i];
                if (socket == null)
                    continue;

                if (string.IsNullOrWhiteSpace(socket.socketId))
                    messages.Add(new ValidationMessage { context = socket, severity = "Warning", message = "Socket has no ID." });
                else if (!ids.Add(socket.socketId))
                    messages.Add(new ValidationMessage { context = socket, severity = "Warning", message = $"Duplicate socket ID '{socket.socketId}'." });

                if (string.IsNullOrWhiteSpace(socket.category))
                    messages.Add(new ValidationMessage { context = socket, severity = "Warning", message = "Socket has no category." });

                if (socket.size.x <= 0f || socket.size.y <= 0f)
                    messages.Add(new ValidationMessage { context = socket, severity = "Warning", message = "Socket size should be greater than zero." });

                if (socket.transform.forward.sqrMagnitude < 0.001f)
                    messages.Add(new ValidationMessage { context = socket, severity = "Error", message = "Socket forward direction is invalid." });
            }

            if (messages.Count == 0)
                messages.Add(new ValidationMessage { context = root, severity = "OK", message = $"{sockets.Length} socket(s) validated." });

            return messages;
        }

        public static void AddSocketToSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Asset Placement Lab", "Select one or more GameObjects first.", "OK");
                return;
            }

            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null || selected[i].GetComponent<PungentPlacementSocket>() != null)
                    continue;

                PungentPlacementSocket socket = Undo.AddComponent<PungentPlacementSocket>(selected[i]);
                socket.socketId = selected[i].name;
                EditorUtility.SetDirty(selected[i]);
            }
        }
    }
    #endif

}