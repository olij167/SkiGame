#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Mighty;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static Mighty.MightyCoreData;

namespace MightyScreenshots
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class ScreenshotsCore : ModuleBase
    {
        // Module identification
        public override string ModuleId => "com.mighty.screenshots";
        public override string ModuleName => "Mighty Screenshots";

        // Module data - now instance fields instead of static
        private MightyCoreData _core;
        private ScreenshotsData _data;
        private ScreenshotsData.SceneData _sceneData;
        private int _sceneIndex = -1;
        private VisualElement sceneViewContainer;
        private bool isTakingScreenshot = false;

        static ScreenshotsCore()
        {
            DevLog("ScreenshotsCore - Registering with new lifecycle system");

            // Register with new lifecycle system
            var module = new ScreenshotsCore();
            MightyCore.RegisterModule(module);

            // Keep the old Scene GUI subscription for now since it's not part of normal lifecycle
            SceneView.duringSceneGui -= OnSceneStatic;
            SceneView.duringSceneGui += OnSceneStatic;
        }

        /// <summary>
        /// Initialize the module - load data, setup references
        /// </summary>
        protected override void OnInitialize()
        {
            DevLog($"Initializing {ModuleName}...");

            // Load module data
            _core = MightyCoreData.Load();
            _data = ScreenshotsData.Load();

            DevLog($"{ModuleName} initialization complete");
        }

        /// <summary>
        /// Start the module - subscribe to events, setup UI
        /// </summary>
        protected override void OnStart()
        {
            DevLog($"Starting {ModuleName}...");

            // Subscribe to Unity events safely
            SafeSubscribeSceneOpened(GetSceneData);
            SafeSubscribeSceneClosing(SceneClosing);
            SafeSubscribeEditorCallback(ref EditorApplication.update, OnUpdate);
            SafeSubscribeEditorWantsToQuit(WantsToQuit);

            // Setup UI and components
            GetSceneDataInstance();
            if (_sceneIndex >= 0 && _data?.scenes != null)
            {
                _sceneData = _data.scenes[_sceneIndex];
                BuildScreenshotUI();
            }

            DevLog($"{ModuleName} started successfully");
        }

        /// <summary>
        /// Stop the module - cleanup UI, but keep data
        /// </summary>
        protected override void OnStop()
        {
            DevLog($"Stopping {ModuleName}...");

            // Clear scene view elements
            ClearSceneViewElements();

            // Events are automatically unsubscribed by base class
            DevLog($"{ModuleName} stopped");
        }

        /// <summary>
        /// Shutdown the module completely - cleanup all resources
        /// </summary>
        protected override void OnShutdown()
        {
            DevLog($"Shutting down {ModuleName}...");

            // Save data before shutdown
            SaveModuleData();

            // Clear all references
            core = null;
            data = null;
            sceneData = null;
            sceneViewContainer = null;
            sceneIndex = -1;
            isTakingScreenshot = false;

            DevLog($"{ModuleName} shutdown complete");
        }

        #region Helper Methods for Safe Event Subscription

        /// <summary>
        /// Safely subscribe to Unity's scene opened event
        /// </summary>
        private void SafeSubscribeSceneOpened(EditorSceneManager.SceneOpenedCallback handler)
        {
            EditorSceneManager.sceneOpened -= handler;
            EditorSceneManager.sceneOpened += handler;
            eventUnsubscribers.Add(() => EditorSceneManager.sceneOpened -= handler);
        }

        /// <summary>
        /// Safely subscribe to Unity's scene closing event
        /// </summary>
        private void SafeSubscribeSceneClosing(EditorSceneManager.SceneClosingCallback handler)
        {
            EditorSceneManager.sceneClosing -= handler;
            EditorSceneManager.sceneClosing += handler;
            eventUnsubscribers.Add(() => EditorSceneManager.sceneClosing -= handler);
        }

        /// <summary>
        /// Safely subscribe to Unity's editor wants to quit event
        /// </summary>
        private void SafeSubscribeEditorWantsToQuit(Func<bool> handler)
        {
            EditorApplication.wantsToQuit -= handler;
            EditorApplication.wantsToQuit += handler;
            eventUnsubscribers.Add(() => EditorApplication.wantsToQuit -= handler);
        }

        #endregion

        #region Module-Specific Methods

        private void SaveModuleData()
        {
            if (data != null)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }

        private void ClearSceneViewElements()
        {
            // Clear any scene view screenshot elements
            if (sceneViewContainer != null)
            {
                sceneViewContainer.Clear();
                sceneViewContainer = null;
            }
        }

        private void BuildScreenshotUI()
        {
            if (sceneData?.Screenshots == null) return;

            for (int i = 0; i < sceneData.Screenshots.Count; i++)
            {
                VisualElement container = new()
                {
                    name = $"Screenshot {i}",
                    style = {
                        position = Position.Absolute,
                        flexGrow = 1,
                        flexShrink = 1,
                        backgroundColor = new Color(0, 0, 0, 1f),
                        flexDirection = FlexDirection.Row,
                        paddingBottom = 8,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 8,
                        borderBottomColor = Color.white,
                        borderBottomWidth = 1,
                        borderLeftColor = Color.white,
                        borderLeftWidth = 1,
                        borderRightColor = Color.white,
                        borderRightWidth = 1,
                        borderTopColor = Color.white,
                        borderTopWidth = 1,
                    }
                };

                container.AddToClassList("scalewithopacity");
                Label label = new()
                {
                    text = sceneData.Screenshots[i].Name,
                    style = {
                        color = Color.white,
                        fontSize = 10,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        unityFontStyleAndWeight = FontStyle.Bold,
                    }
                };
                container.Add(label);

                VisualElement thumbnail = new()
                {
                    name = $"Screenshot {i}",
                    style = {
                        flexGrow = 0,
                        flexShrink = 0,
                        width = 20,
                        height = 20,
                        backgroundImage = sceneData.Screenshots[i].Pic.img,
                        backgroundColor = new Color(0, 0, 0, 0.5f),
                    }
                };
                container.Add(thumbnail);
                int ii = i;
                thumbnail.RegisterCallback<MouseDownEvent>(evt =>
                {
                    var sd = sceneData = data.scenes[sceneIndex];
                    var ml = sd.Screenshots[ii].MapLocation;

                    if (evt.button == 0)
                    {
                        core.SceneViewGoToPosition(ml.worldPosition, ml.worldRotation);
                    }
                });

                container.tooltip = sceneData.Screenshots[i].Description;
                // MightySceneViewManager.Instance.RegisterElement("ScreenshotOverlay", container, sceneData.Screenshots[i].MapLocation.worldPosition);
            }
        }

        #endregion

        #region Static Methods (Legacy Support)

        /// <summary>
        /// Static scene GUI handler - kept for compatibility
        /// </summary>
        private static void OnSceneStatic(SceneView view)
        {
            // Find the active module instance and delegate to it
            var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
            if (module?.State == ModuleLifecycleState.Started)
            {
                module.OnScene(view);
            }
        }

        private void OnScene(SceneView view)
        {
            if (State != ModuleLifecycleState.Started)
                return;

            // Handle scene GUI
        }

        #endregion

        #region Event Handlers (converted from static methods)

        private void OnUpdate()
        {
            if (State != ModuleLifecycleState.Started || data == null)
                return;

            // Debug.Log($"ScreenshotsCore: OnUpdate  EditorApplication.isPlaying {EditorApplication.isPlaying}");
            if (!EditorApplication.isPlaying) return;

            if ((data.settings.hotkey1 == KeyCode.None || Input.GetKey(data.settings.hotkey1)) &&
                (data.settings.hotkey2 == KeyCode.None || Input.GetKey(data.settings.hotkey2)) &&
                (data.settings.hotkey3 == KeyCode.None || Input.GetKey(data.settings.hotkey3)))
            {
                if (!isTakingScreenshot)
                {
                    ShowToast("Screenshot Captured");
                    isTakingScreenshot = true;
                    if (sceneData.Screenshots == null) sceneData.Screenshots = new List<ScreenshotsData.Screenshot.Root>();
                    sceneData.Screenshots.Add(new ScreenshotsData.Screenshot.Root(Camera.main.transform.position.ToString(), Camera.main));
                    updatingMappables = false;
                }
            }
            else
            {
                isTakingScreenshot = false;
            }
        }

        private bool WantsToQuit()
        {
            DevLog("ScreenshotsCore WantsToQuit");
            SaveModuleData();
            return true;
        }

        private void SceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            DevLog($"ScreenshotsCore SceneClosing({scene.name}, {removingScene})");
            SaveModuleData();
        }

        public void GetSceneData(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            DevLog($"ScreenshotsCore GetSceneData({scene.name}, {mode})");
            GetSceneDataInstance();
        }

        public static void GetSceneData()
        {
            var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
            module?.GetSceneDataInstance();
        }

        public void GetSceneDataInstance()
        {
            DevLog("ScreenshotsCore.GetSceneData");
            _core ??= MightyCoreData.Load();
            if (_core == null) return;

            _core.CheckSceneData();
            _data ??= ScreenshotsData.Load();
            _data.scenes ??= new();

            if (!isSceneAnchored) return;

            _sceneIndex = -1;
            for (int i = 0; i < _data.scenes.Count; i++)
            {

                if (_data.scenes[i].name == dataSetName) _sceneIndex = i;
            }

            if (_sceneIndex < 0)
            {
                DevLog("SceneIndex not found");
                _data.scenes.Add(new ScreenshotsData.SceneData());
                _sceneIndex = _data.scenes.Count - 1;
                _data.scenes[_sceneIndex].name = dataSetName;
                _data.scenes[_sceneIndex].Screenshots = new List<ScreenshotsData.Screenshot.Root>();
            }
            DevLog($"SceneIndex is {_sceneIndex}");
            _sceneData = _data.scenes[_sceneIndex];
            for (int i = 0; i < _sceneData.Screenshots.Count; i++)
            {
                _sceneData.Screenshots[i].RegisterMappable();
            }
        }

        #endregion

        #region Static Accessors for Backward Compatibility

        /// <summary>
        /// Static accessor for data - delegates to current active instance
        /// </summary>
        public static ScreenshotsData data
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                return module?._data;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                if (module != null) module._data = value;
            }
        }

        /// <summary>
        /// Static accessor for core - delegates to current active instance
        /// </summary>
        public static MightyCoreData core
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                return module?._core;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                if (module != null) module._core = value;
            }
        }

        /// <summary>
        /// Static accessor for sceneData - delegates to current active instance
        /// </summary>
        public static ScreenshotsData.SceneData sceneData
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                return module?._sceneData;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                if (module != null) module._sceneData = value;
            }
        }

        /// <summary>
        /// Static accessor for sceneIndex - delegates to current active instance
        /// </summary>
        public static int sceneIndex
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                return module?._sceneIndex ?? -1;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.screenshots") as ScreenshotsCore;
                if (module != null) module._sceneIndex = value;
            }
        }



        #endregion
    }
}
#endif