#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Mighty;
using static Mighty.MightyCoreData;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Mighty
{
    //   [CreateAssetMenu(menuName = "MyEditor/WindowManagerStateful")]
    public class MightyWindowManagerStateful : ScriptableObject
    {
        public interface ICommand
        {
            void Execute();
        }

        public class RegisterWindowCommand : ICommand
        {
            private string id;
            private MightyWindowStateful window;
            private MightyWindowManagerStateful manager;
            private string restorationCommandTypeName;

            public RegisterWindowCommand(string id, MightyWindowStateful window, MightyWindowManagerStateful manager, string restorationCommandTypeName)
            {
                this.id = id;
                this.window = window;
                this.manager = windowManagerStateful;
                this.restorationCommandTypeName = restorationCommandTypeName;

                DevLog($"RegisterWindowCommand() {id} {window} {window.ID} {manager} {restorationCommandTypeName}");
            }

            public void Execute()
            {
                manager.serializableWindows.Add(new SerializableWindowState
                {
                    id = id,
                    window = window,
                    restorationCommandTypeName = restorationCommandTypeName
                });
            }
        }

        public class DeregisterWindowCommand : ICommand
        {
            private string id;
            private MightyWindowManagerStateful manager;

            public DeregisterWindowCommand(string id, MightyWindowManagerStateful manager)
            {
                this.id = id;
                this.manager = manager;
            }

            public void Execute()
            {
                manager.serializableWindows.RemoveAll(w => w.id == id);
            }
        }

        [System.Serializable]
        public struct SerializableWindowState
        {
            public string id;
            public MightyWindowStateful window;
            public string restorationCommandTypeName;
            public float left, top, width, height;
            public Vector2 size;
            public bool firstRun;
            // Add any other stateful or style information here
        }


        public SerializableWindowState? GetSavedState(string id)
        {
            var windowState = serializableWindows.FirstOrDefault(w => w.id == id);
            DevLog($"GetSavedState() {id} {windowState.id} ");
            if (windowState.id != null) // assuming id can't be null when a valid state is present
            {
                return windowState;
            }
            return null;
        }

        public void ReplaceSavedState(string id, SerializableWindowState newState)
        {
            // DevLog($"ReplaceSavedState SaveWindowState() {id}");
            // Find the index of the saved state
            int index = serializableWindows.FindIndex(w => w.id == id);
            if (index != -1)
            {
                // Replace the existing state with the new one
                serializableWindows[index] = newState;
                // DevLog($"ReplaceSavedState SaveWindowState() {id} {index} {newState} {newState.top} {newState.left} {newState.width} {newState.height}");
            }
            else
            {
                // DevLogWarning($"No saved state found for id: {id}");
            }
        }



        [SerializeField]
        public List<SerializableWindowState> serializableWindows = new List<SerializableWindowState>();


        private Queue<ICommand> commandQueue = new Queue<ICommand>();

        public bool IsWindowOpenInVisualTree(string id)
        {
            VisualElement root = MightyCoreData.window.rootVisualElement;
            var customWindows = root.Query<MightyWindowStateful>().ToList();
            return customWindows.Any(w => w.name == id);
        }


        public bool RegisterWindow(string id, MightyWindowStateful window, Type restorationCommandType)
        {
            if (IsWindowOpenInVisualTree(id))
            {
                DevLogWarning("A window with this ID already exists: " + id);
                return false;
            }


            if (!serializableWindows.Exists(w => w.id == id))
            {
                QueueCommand(new RegisterWindowCommand(id, window, this, restorationCommandType.FullName));
            }


            ExecuteCommands();

            return true;
        }

        public void DeregisterWindow(string id)
        {
            if (!serializableWindows.Exists(w => w.id == id))
            {
                DevLogWarning("No window registered with this ID: " + id);
                return;
            }

            QueueCommand(new DeregisterWindowCommand(id, this));

            ExecuteCommands();
        }

        private void QueueCommand(ICommand command)
        {
            commandQueue.Enqueue(command);
        }

        private void ExecuteCommands()
        {
            while (commandQueue.Count > 0)
            {
                ICommand command = commandQueue.Dequeue();
                command.Execute();
            }
        }

        public void ClearCommands()
        {
            commandQueue.Clear();
        }

        #region WindowBar cretion
        public VisualElement PopulateWindowBar()
        {
            VisualElement view = new();
            DevLog($"PopulateWindowBar() {serializableWindows.Count}");

            foreach (var windowState in serializableWindows)
            {
                //            DevLog($"PopulateWindowBar() {windowState.id} {windowState.window} {windowState.window.style.display}");
                Button button = new Button();
                button.clicked += () =>
                {
                    if (windowState.window != null)
                    {
                        if (windowState.window.style.display == DisplayStyle.Flex)
                        {
                            windowState.window.style.display = DisplayStyle.None;
                        }
                        else
                        {
                            windowState.window.style.display = DisplayStyle.Flex;
                            windowState.window.BringToFront();
                        }
                    }
                    // Update button text and tooltip based on current state
                    UpdateButtonState(button, windowState);
                };

                // Set initial button text and tooltip
                UpdateButtonState(button, windowState);
                view.Add(button);
            }
            return view;
        }

        private static void UpdateButtonState(Button button, SerializableWindowState windowState)
        {
            if (windowState.window != null)
            {
                button.text = windowState.id + (windowState.window.style.display == DisplayStyle.Flex ? " [-]" : " [+]");
                button.tooltip = windowState.window.style.display == DisplayStyle.Flex ? windowState.id + " is currently visible. Click to minimize." : windowState.id + " is currently minimized. Click to show.";
            }
            else
            {
                button.text = windowState.id + " [?]";
                button.tooltip = "Window reference not found.";
            }
        }

        #endregion

        #region Window VisualElement management
        #endregion
    }

}
#endif