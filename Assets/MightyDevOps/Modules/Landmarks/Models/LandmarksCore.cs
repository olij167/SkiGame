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

namespace MightyLandmarks
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class LandmarksCore : ModuleBase
    {
        // Module identification
        public override string ModuleId => "com.mighty.landmarks";
        public override string ModuleName => "Mighty Landmarks";

        // Module data - now instance fields instead of static
        private MightyCoreData _core;
        private LandmarksData _data;
        private LandmarksData.Scene _sceneData;
        private int _sceneIndex = -1;
        private VisualElement sceneViewContainer;

        static LandmarksCore()
        {
            DevLog("LandmarksCore - Registering with new lifecycle system");

            // Register with new lifecycle system
            var module = new LandmarksCore();
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
            _data = LandmarksData.Load();

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
            SafeSubscribeEditorWantsToQuit(WantsToQuit);

            // Setup UI and components
            GetSceneDataInstance();
            if (_sceneIndex >= 0 && _data?.scenes != null)
            {
                _sceneData = _data.scenes[_sceneIndex];
                BuildLandmarkUI();
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
            _core = null;
            _data = null;
            _sceneData = null;
            sceneViewContainer = null;
            _sceneIndex = -1;

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
            if (_data != null)
            {
                EditorUtility.SetDirty(_data);
                AssetDatabase.SaveAssets();
            }
        }

        private void ClearSceneViewElements()
        {
            // Clear any scene view landmark elements
            if (sceneViewContainer != null)
            {
                sceneViewContainer.Clear();
                sceneViewContainer = null;
            }
        }

        private void BuildLandmarkUI()
        {
            if (_sceneData?.landmarks == null) return;

            for (int i = 0; i < _sceneData.landmarks.Count; i++)
            {
                VisualElement container = new()
                {
                    name = $"sv_{_sceneData.landmarks[i].CreatedAt}",
                    style = {
                        position = Position.Absolute,
                        width = 64,
                        height = 64,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        justifyContent = Justify.Center,
                        backgroundImage = _sceneData.landmarks[i].Pic.img,
                        borderTopLeftRadius = 10,
                        borderTopRightRadius = 10,
                        borderBottomLeftRadius = 10,
                        borderBottomRightRadius = 10,
                    }
                };

                // Creating a semi-transparent overlay for text to improve readability
                VisualElement textOverlay = new()
                {
                    style = {
                        flexGrow = 1,
                        flexShrink = 1,
                        backgroundColor = new Color(0, 0, 0, 0.5f),
                        justifyContent = Justify.Center,
                        alignItems = Align.Center,
                        borderTopLeftRadius = 10,
                        borderTopRightRadius = 10,
                        borderBottomLeftRadius = 10,
                        borderBottomRightRadius = 10,
                    }
                };
                container.Add(textOverlay);

                // Overlaying the landmark name on the image
                Label label = new()
                {
                    name = "TextOverlay",
                    style = {
                        color = Color.white,
                        fontSize = 16,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        unityFontStyleAndWeight = FontStyle.Bold,
                    }
                };
                textOverlay.Add(label);

                container.AddToClassList("scalewithopacity");
                container.Add(label);

                int ii = i;
                container.RegisterCallback<MouseDownEvent>(evt =>
                {
                    var sd = _sceneData = _data.scenes[_sceneIndex];
                    var ml = sd.landmarks[ii].MapLocation;

                    if (evt.button == 0)
                    {
                        _core.SceneViewGoToPosition(ml.worldPosition, ml.worldRotation);
                    }
                });

                container.tooltip = _sceneData.landmarks[i].Name;

                MightySceneViewManager.Instance.RegisterElement("LandmarkOverlay", container, _data.scenes[_sceneIndex].landmarks[ii], _sceneData.landmarks[i].MapLocation.worldPosition);
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
            var module = MightyCore.GetModule("com.mighty.landmarks") as LandmarksCore;
            if (module?.State == ModuleLifecycleState.Started)
            {
                module.OnScene(view);
            }
        }

        private void OnScene(SceneView view)
        {
            if (State != ModuleLifecycleState.Started)
                return;

            // Handle scene GUI - most functionality moved to BuildLandmarkUI()
        }

        #endregion

        // StartModule functionality moved to OnStart lifecycle method

        #region Event Handlers (converted from static methods)

        private bool WantsToQuit()
        {
            DevLog("LandmarksCore WantsToQuit");
            SaveModuleData();
            return true;
        }

        private void SceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            DevLog($"LandmarksCore SceneClosing({scene.name}, {removingScene})");
            SaveModuleData();
        }

        public void GetSceneData(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            DevLog($"LandmarksCore GetSceneData({scene.name}, {mode})");
            GetSceneDataInstance();
        }

        public static void GetSceneData()
        {
            var module = MightyCore.GetModule("com.mighty.landmarks") as LandmarksCore;
            module?.GetSceneDataInstance();
        }

        public void GetSceneDataInstance()
        {
            DevLog("LandmarksCore.GetSceneData");
            _core ??= MightyCoreData.Load();
            if (_core == null) return;

            _core.CheckSceneData();
            _data ??= LandmarksData.Load();
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
                _data.scenes.Add(new LandmarksData.Scene());
                _sceneIndex = _data.scenes.Count - 1;
                _data.scenes[_sceneIndex].name = dataSetName;
                _data.scenes[_sceneIndex].landmarks = new List<LandmarksData.Landmark.Root>();
            }
            DevLog($"SceneIndex is {_sceneIndex}");
            _sceneData = _data.scenes[_sceneIndex];
            for (int i = 0; i < _sceneData.landmarks.Count; i++)
            {
                _sceneData.landmarks[i].RegisterMappable();
            }
        }

        #endregion

        #region Static Accessors for Backward Compatibility

        /// <summary>
        /// Static accessor for data - delegates to current active instance
        /// </summary>
        public static LandmarksData data
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.landmarks") as LandmarksCore;
                return module?._data;
            }
        }

        /// <summary>
        /// Static accessor for core - delegates to current active instance
        /// </summary>
        public static MightyCoreData core
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.landmarks") as LandmarksCore;
                return module?._core;
            }
        }

        /// <summary>
        /// Static accessor for sceneData - delegates to current active instance
        /// </summary>
        public static LandmarksData.Scene sceneData
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.landmarks") as LandmarksCore;
                return module?._sceneData;
            }
        }

        /// <summary>
        /// Static accessor for sceneIndex - delegates to current active instance
        /// </summary>
        public static int sceneIndex
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.landmarks") as LandmarksCore;
                return module?._sceneIndex ?? -1;
            }
        }



        #endregion

        // Old UI building code removed - functionality moved to BuildLandmarkUI method
    }
}

#endif