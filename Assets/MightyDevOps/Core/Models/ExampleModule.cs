#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Mighty.MightyCoreData;

namespace Mighty.Examples
{
    /// <summary>
    /// Example module demonstrating how to implement the new module lifecycle system
    /// </summary>
    public class ExampleModule : ModuleBase
    {
        // Module identification
        public override string ModuleId => "com.mighty.example";
        public override string ModuleName => "Example Module";

        // Module data
        private ExampleModuleData moduleData;
        private bool isDataLoaded = false;

        /// <summary>
        /// Static method that modules can call to register themselves with the core system
        /// </summary>
        [InitializeOnLoadMethod]
        static void RegisterModule()
        {
            // Register this module with the lifecycle manager
            var module = new ExampleModule();
            MightyCore.RegisterModule(module);
        }

        /// <summary>
        /// Initialize the module - load data, setup references, but don't subscribe to events yet
        /// </summary>
        protected override void OnInitialize()
        {
            DevLog($"Initializing {ModuleName}...");

            // Load module data
            LoadModuleData();

            // Setup any non-event references
            SetupReferences();

            DevLog($"{ModuleName} initialization complete");
        }

        /// <summary>
        /// Start the module - subscribe to events, start processing
        /// </summary>
        protected override void OnStart()
        {
            DevLog($"Starting {ModuleName}...");

            // Subscribe to Unity editor events using the safe subscription methods
            SafeSubscribeEditorCallback(ref EditorApplication.update, OnEditorUpdate);
            SafeSubscribe(ref StartModules, OnStartModules);
            SafeSubscribe(ref Rebuild, OnRebuild);

            // Subscribe to scene events (Unity events need special handling)
            SafeSubscribeSceneOpened(OnSceneOpened);
            SafeSubscribeSceneClosing(OnSceneClosing);

            // Start module-specific processing
            StartProcessing();

            DevLog($"{ModuleName} started successfully");
        }

        /// <summary>
        /// Stop the module - unsubscribe from events, pause processing, but keep data
        /// </summary>
        protected override void OnStop()
        {
            DevLog($"Stopping {ModuleName}...");

            // Stop any ongoing processing
            StopProcessing();

            // Event unsubscription is handled automatically by the base class

            DevLog($"{ModuleName} stopped");
        }

        /// <summary>
        /// Shutdown the module completely - cleanup all resources, clear data
        /// </summary>
        protected override void OnShutdown()
        {
            DevLog($"Shutting down {ModuleName}...");

            // Cleanup resources
            CleanupResources();

            // Clear module data
            moduleData = null;
            isDataLoaded = false;

            DevLog($"{ModuleName} shutdown complete");
        }

        #region Module-Specific Methods

        private void LoadModuleData()
        {
            try
            {
                // Example: Load module-specific data
                moduleData = new ExampleModuleData();
                isDataLoaded = true;

                DevLog($"Module data loaded for {ModuleName}");
            }
            catch (Exception ex)
            {
                DevLogError($"Failed to load data for {ModuleName}: {ex.Message}");
                throw;
            }
        }

        private void SetupReferences()
        {
            // Setup any non-event references here
            // This is called during initialization
        }

        private void StartProcessing()
        {
            // Start any module-specific processing here
            // This is called when the module starts
        }

        private void StopProcessing()
        {
            // Stop any ongoing processing here
            // This is called when the module stops
        }

        private void CleanupResources()
        {
            // Cleanup any resources here
            // This is called during shutdown
        }

        #endregion

        #region Event Handlers

        private void OnEditorUpdate()
        {
            if (!isDataLoaded || State != ModuleLifecycleState.Started)
                return;

            // Handle editor update
        }

        private void OnStartModules()
        {
            // Handle start modules event
        }

        private void OnRebuild()
        {
            // Handle rebuild event
        }

        private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            // Handle scene opened
        }

        private void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            // Handle scene closing
        }

        #endregion

        #region Helper Methods for Safe Event Subscription

        /// <summary>
        /// Safely subscribe to Unity's scene opened event
        /// </summary>
        private void SafeSubscribeSceneOpened(EditorSceneManager.SceneOpenedCallback handler)
        {
            EditorSceneManager.sceneOpened -= handler; // Remove first to prevent duplicates
            EditorSceneManager.sceneOpened += handler;

            // Store unsubscriber
            eventUnsubscribers.Add(() => EditorSceneManager.sceneOpened -= handler);
        }

        /// <summary>
        /// Safely subscribe to Unity's scene closing event
        /// </summary>
        private void SafeSubscribeSceneClosing(EditorSceneManager.SceneClosingCallback handler)
        {
            EditorSceneManager.sceneClosing -= handler; // Remove first to prevent duplicates
            EditorSceneManager.sceneClosing += handler;

            // Store unsubscriber
            eventUnsubscribers.Add(() => EditorSceneManager.sceneClosing -= handler);
        }

        #endregion
    }

    /// <summary>
    /// Example module data class
    /// </summary>
    [Serializable]
    public class ExampleModuleData
    {
        public string exampleString = "Hello World";
        public int exampleInt = 42;
        public bool exampleBool = true;

        public ExampleModuleData()
        {
            // Initialize with default values
        }
    }
}
#endif