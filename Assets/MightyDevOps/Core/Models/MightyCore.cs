#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;
using UnityEditor.Build;
using Unity.VisualScripting;


namespace Mighty
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-10000)]
    public static class MightyCore
    {
        public static MightyCoreData data;

        static public bool isInit = false, hasFirstFramePassed = false;

        static public GameObject cameraTopDown;

        // static public Camera cameraScene;
        static public Material matFade;


        // delegate void RefreshAll();
        static event Action RefreshAll;
        // public static Action Rebuild;
        public static DateTime lastNewsUpdate = DateTime.MinValue;
        // static public List<IMappable> mapShapes;

        static MightyCore()
        {
            // Clean up any existing event handlers first to prevent duplicates during recompilation
            CleanupCoreEventHandlers();

            EditorSceneManager.sceneOpened -= SceneOpened;
            EditorSceneManager.sceneOpened += SceneOpened;

            EditorSceneManager.sceneSaved -= SceneSaved;
            EditorSceneManager.sceneSaved += SceneSaved;

            EditorSceneManager.newSceneCreated -= NewSceneCreated;
            EditorSceneManager.newSceneCreated += NewSceneCreated;

            EditorApplication.delayCall -= InitAfterFirstFrame;
            EditorApplication.delayCall += InitAfterFirstFrame;

            EditorSceneManager.sceneClosing -= SceneClosing;
            EditorSceneManager.sceneClosing += SceneClosing;

            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            InitPlaythroughs -= InitRunIds;
            InitPlaythroughs += InitRunIds;

            EditorApplication.quitting -= EditorQuitting;
            EditorApplication.quitting += EditorQuitting;

#if MIGHTY_DEVOPS
#else
            DevLog($"Thank you for installing Mighty Dev Ops!  If you enjoy, don't forget to leave a review!  Version {MightyCoreData.Version}");
            MightyDevOpsDefineSymbol();
#endif
        }

        static void MightyDevOpsDefineSymbol()
        {
#if UNITY_2023_2_OR_NEWER // includes 2023.3 and Unity 6+
            const string symbol = "MIGHTY_DEVOPS";
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string scriptingDefines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

            if (!scriptingDefines.Contains(symbol))
            {
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.IsNullOrEmpty(scriptingDefines) ? symbol : scriptingDefines + ";" + symbol);
            }
#else
            // Fallback for 2021-2022.1
            const string symbol = "MIGHTY_DEVOPS";
            string scriptingDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            if (!scriptingDefines.Contains(symbol))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.IsNullOrEmpty(scriptingDefines) ? symbol : scriptingDefines + ";" + symbol);
            }
#endif
        }

        public static void InitAfterFirstFrame()
        {
            if (hasFirstFramePassed)
            {
                DevLog($"InitAfterFirstFrame has already passed");
                return;
            }
            if (GetSceneView() == null)
            {
                // SceneView can be null on the first domain-reload in newer editor versions.
                // Re-queue this method until a SceneView exists so that module startup is not skipped.
                DevLog($"SceneView is null – retrying InitAfterFirstFrame on next delayCall");
                EditorApplication.delayCall += InitAfterFirstFrame;
                return;
            }

            EditorApplication.delayCall -= InitAfterFirstFrame;
            hasFirstFramePassed = true;
            DevLog($"InitAfterFirstFrame ----------------------------------------");
            isInit = false;
            isSceneAnchored = false;

            if (!SceneManager.GetActiveScene().isLoaded)
            {
                DevLog($"Scene not loaded");
                return;
            }



            sceneLoaded = true;
            DevLog($"Core");
            data = Load();

            GameObject mightySceneAnchor = GameObject.Find("MightySceneAnchor");
            if (mightySceneAnchor != null)
            {
                cameraTopDown = Resources.Load("MightyMapCam") as GameObject;
                if (cameraTopDown == null)
                {
                    DevLogError("Could not load MightyMapCam prefab from Resources");
                    return;
                }
            }

            GameObject[] existingCameras = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject existingCamera in existingCameras)
            {
                if (existingCamera.name == "MightyOrthoCam" && !AssetDatabase.Contains(existingCamera))
                {
                    // Debug.Log("Destroyed existing camera: " + existingCamera.name);
                    GameObject.DestroyImmediate(existingCamera, true);
                }
            }

            dataSetName = null;
            GetSceneData();

            RefreshAll -= Refresh;
            RefreshAll += Refresh;
            Rebuild = MightyMap.RebuildView;
            UpdateMappables -= MightyMap.UpdateMappables;
            UpdateMappables += MightyMap.UpdateMappables;
            UpdateMiniMap -= MightyMap.UpdateMiniMap;
            UpdateMiniMap += MightyMap.UpdateMiniMap;
            RebuildMappables -= MightyMap.BuildMappables;
            RebuildMappables += MightyMap.BuildMappables;
            UpdateMarkers -= _UpdateMarkers;
            UpdateMarkers += _UpdateMarkers;


            BuildTopUI -= TopUI;
            BuildTopUI += TopUI;

            Init();
            StartWindow?.Invoke();

            if (!modulesStarted)
            {
                // Initialise map if available
                if (window != null)
                {
                    MightyMap.Init();
                    MapInitialized?.Invoke();
                }

                MightyCoreData.ModuleManager.InitializeAllModules();
                MightyCoreData.ModuleManager.StartAllModules();

                // Legacy support callback
                StartModules?.Invoke();

                ShowToast("Mighty Core: Starting Modules");
                modulesStarted = true;
                Rebuild?.Invoke();
                CheckUpdates(() => { /*DevLog($"Getting Module Updates...");*/ });
            }
        }

        public static void InitRunIds()
        {
            DevLog($"InitRunIds data.sceneData.PlayTrackingList.Count: {sceneData.PlayTrackingList.Count}");
            if (sceneData.PlayTrackingList == null) sceneData.PlayTrackingList = new List<SceneData.PlayTracking>();

            for (int i = 0; i < sceneData.PlayTrackingList.Count; i++)
            {
                DevLog($"InitRunIds sceneData.RunIDList[i]: {sceneData.PlayTrackingList[i]}");
            }
            RebuildRunIdDropDown?.Invoke();
        }

        private static void SceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            DevLog($"SceneClosing({scene.name}, {removingScene})");

            // Properly shutdown modules before scene cleanup
            MightyCoreData.ModuleManager.StopAllModules();

            sceneLoaded = false;
            dataSetName = null;
            isSceneAnchored = false;

            MightyMap.isInit = false;
            MightyMap.GUILoaded = false;

            sceneData = null;
            isInit = false;
            hasFirstFramePassed = false;
            modulesStarted = false;

            // Clear cached references to prevent stale references
            cachedSceneView = null;
            cachedSceneCamera = null;
            frameCounter = 0;

            if (window?.rootVisualElement != null)
            {
                window.rootVisualElement.Clear();
                window.rootVisualElement.style.display = DisplayStyle.None;
            }

            SaveData();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            AssetDatabase.SaveAssets();
            DevLog($"PlayModeStateChange {state.ToString()}");
            if (sceneData == null)
            {
                hasFirstFramePassed = false;
                InitAfterFirstFrame();
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                isPlaying = true;
                // DevLog($"PlayModeStateChange");
                // DevLog($"PlayTrackingDirty is {sceneData.PlayTrackingDirty}");
                if (sceneData == null)
                {
                    hasFirstFramePassed = false;
                    InitAfterFirstFrame();
                }
                if (sceneData != null)
                {
                    DevLog($"OnPlayModeStateChanged sceneData.RecordPlaythrough {sceneData.RecordPlaythrough}");
                    if (sceneData.RecordPlaythrough)
                    {
                        sceneData.RunID = DateTime.Now.Ticks.ToString();
                        // sceneData.RunID = DateTime.Now.ToString("@@ yyyy-MM-dd ## HH:mm:ss");
                        // sceneData.RunID = sceneData.RunID.Replace("@@", "Date:");
                        // sceneData.RunID = sceneData.RunID.Replace("##", "Time:");
                        //sceneData.RunID = sceneData.RunIDList.Count.ToString() + "||" + sceneData.RunID;
                        DevLog($"OnPlayModeStateChanged: EnteredPlayMode switch");
                        sceneData.PlayTrackingList.Add(new SceneData.PlayTracking(Color.red));
                    }
                    else
                    {
                        DevLog($"OnPlayModeStateChanged: EnteredPlayMode not recording");
                    }
                }
                mainCamera = Camera.main;

            }

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                isPlaying = false;
                DevLog($"PlayModeStateChange ExitingPlayMode");
                if (sceneData == null)
                {
                    hasFirstFramePassed = false;
                    InitAfterFirstFrame();
                }

                DevLog($"PlayModeStateChange sceneData != null {sceneData != null}");

                if (sceneData != null)
                {
                    sceneData.PlayTrackingDirty = true;
                    if (sceneData.PlayTrackingList != null && sceneData.PlayTrackingList.Count > 0)
                    {
                        sceneData.PlayTrackingList[sceneData.PlayTrackingList.Count - 1].EndTracking();
                    }

                    DevLog($"PlayModeStateChange ExitingPlayMode: sceneData.playTrackingDirty {sceneData.PlayTrackingDirty}  sceneData.PlayTrackingList.Count {sceneData.PlayTrackingList?.Count ?? 0}");
                    if (!sceneData.PlayTrackingDirty && sceneData.PlayTrackingList != null && sceneData.PlayTrackingList.Count > 0)
                    {
                        DevLog($"PlayModeStateChange ExitingPlayMode: Removing PlayTrackingList -- sceneData.playTrackingDirty {sceneData.PlayTrackingDirty}  sceneData.PlayTrackingList.Count {sceneData.PlayTrackingList.Count}");
                        sceneData.RunID = "";
                        sceneData.PlayTrackingList.RemoveAt(sceneData.PlayTrackingList.Count - 1);
                    }
                    else if (sceneData.PlayTrackingList != null && sceneData.PlayTrackingList.Count > 0)
                    {
                        sceneData.PlayTrackingList[sceneData.PlayTrackingList.Count - 1].SelectPlaythrough();
                        sceneData.RunID = "";
                    }

                }
                if (sceneData?.PlayTrackingList != null)
                {
                    foreach (var runIDList in sceneData.PlayTrackingList)
                    {
                        DevLog($"PlayModeStateChange runIDList: {runIDList.ToString()}  sceneData.RunID: {sceneData.RunID.ToString()}  sceneData.PlayTrackingList.Count: {sceneData.PlayTrackingList.Count.ToString()}  sceneData.PlayTrackingList[sceneData.PlayTrackingList.Count - 1]: {sceneData.PlayTrackingList[sceneData.PlayTrackingList.Count - 1].ToString()}");
                    }
                }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                DevLog($"PlayModeStateChange EnteredEditMode");
                isPlaying = false;

                // First stop all modules
                MightyCoreData.ModuleManager.StopAllModules();

                // Then perform a complete restart
                MightyCoreData.ModuleManager.GracefulSystemRestart();

                if (sceneData == null)
                {
                    hasFirstFramePassed = false;
                    InitAfterFirstFrame();
                }
                foreach (var mappable in mappables)
                {
                    mappable.RegisterMappable();
                }
                RunPlaybackMinMaxUpdated?.Invoke();
                MightyMap.isInit = false;
                MightyMap.Init();
                RefreshAll?.Invoke();
                Rebuild();
            }
            //DevLog($"ModeChanged: {state.ToString()}  run_id: {sceneData.run_id}");

            sceneCamera = null;
            isInit = false;
            Init();
        }


        private static SceneView cachedSceneView;
        private static Camera cachedSceneCamera;
        private static int frameCounter = 0;

        private static void EditorUpdate()
        {
            // Cache SceneView reference to avoid multiple calls per frame
            if (cachedSceneView == null || !cachedSceneView)
                cachedSceneView = GetSceneView();

            var sv = cachedSceneView;
            if (sv == null)
            {
                if (!hasFirstFramePassed)
                {
                    InitAfterFirstFrame();
                }
                else if (window?.rootVisualElement != null)
                {
                    window.rootVisualElement.Clear();
                    Map = null;
                    hasFirstFramePassed = false;
                }
                return;
            }

            if (!hasFirstFramePassed || (hasFirstFramePassed && data == null))
            {
                InitAfterFirstFrame();
                return;
            }

            if (Application.isPlaying)
                return;

            // Cache camera reference
            if (cachedSceneCamera == null || !cachedSceneCamera)
                cachedSceneCamera = Application.isPlaying ? Camera.main : sv.camera;

            if (cachedSceneCamera != null)
            {
                data.svPos = cachedSceneCamera.transform.position;
                data.svRot = cachedSceneCamera.transform.rotation;

                if (data.svPos != data._svPos || data.svRot != data._svRot)
                {
                    RefreshAll?.Invoke();
                }

                data._svPos = data.svPos;
                data._svRot = data.svRot;
            }

            // Handle scene deletion (this is infrequent so can run every frame)
            if (sceneData?.DeleteMe == true)
            {
                HandleSceneDeletion();
            }

            // Update UI display state (only when needed)
            UpdateUIDisplayState();

            // Only check for news updates every 60th frame (roughly once per second at 60fps)
            frameCounter++;
            if (frameCounter >= 60)
            {
                frameCounter = 0;
                CheckForNewsUpdates();
            }
        }

        private static void HandleSceneDeletion()
        {
            var go = GameObject.Find("MightySceneAnchor");
            if (go != null)
            {
                GameObject.DestroyImmediate(go);
                if (data?.MappableTypesInfo != null)
                {
                    foreach (var typeInfo in data.MappableTypesInfo)
                    {
                        if (typeInfo.Mappable == null)
                        {
                            DevLogError($"TypeInfo Mappable is null for {typeInfo.Name}");
                            continue;
                        }
                        DevLog($"Deleting {typeInfo.Name}");
                        typeInfo.Mappable.Delete();
                    }
                }
                isSceneAnchored = false;
                dataSetName = null;
                MightyMap.isInit = false;
                hasFirstFramePassed = false;
                InitAfterFirstFrame();
                MightyMap.RebuildView();
            }
            sceneData = null;
            if (data?.scenes != null && data.sceneIndex >= 0 && data.sceneIndex < data.scenes.Count)
            {
                data.scenes.RemoveAt(data.sceneIndex);
            }
        }

        private static void UpdateUIDisplayState()
        {
            // if (addSceneAnchor != null && mid != null && bot != null)
            // {
            //     if (isSceneAnchored != MightyMap.GUILoaded)
            //     {
            //         addSceneAnchor.style.display = DisplayStyle.Flex;
            //         mid.style.display = DisplayStyle.None;
            //         bot.style.display = DisplayStyle.None;
            //     }
            //     else
            //     {
            //         addSceneAnchor.style.display = DisplayStyle.None;
            //         mid.style.display = DisplayStyle.Flex;
            //         bot.style.display = DisplayStyle.Flex;
            //     }
            // }
        }

        private static void CheckForNewsUpdates()
        {
            if (DateTime.Now - lastNewsUpdate > TimeSpan.FromMinutes(60))
            {
                void RegularCheck()
                {
                    if (dataCore?.newsItems != null && notifications != null)
                    {
                        foreach (var item in dataCore.newsItems)
                        {
                            if (!item.isRead)
                            {
                                newNews = true;
                                notifications.style.backgroundImage = icons?.notificationOnIcon;
                                break; // No need to check all items once we find one unread
                            }
                        }
                    }
                }

                GetLatestNews(RegularCheck);
                lastNewsUpdate = DateTime.Now;
            }
        }

        // DevLog($"EditorUpdate: scenePath {sceneData.scenePath}  sceneName {sceneData.Name}  sceneAnchor {sceneAnchor.DataSetName}  isSceneAnchored {isSceneAnchored}  isInit {isInit}  modulesStarted {modulesStarted}  sceneLoaded {sceneLoaded}  hasFirstFramePassed {hasFirstFramePassed}");

        private static void GetSceneData(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            GetSceneData();
        }

        public static void GetSceneData()
        {
            DevLog($"SceneManager.GetActiveScene().isLoaded = {SceneManager.GetActiveScene().isLoaded}");
            data.SetDataSetName();
            DevLog($"sceneData is null? {sceneData == null}");

            if (!isSceneAnchored) return;

            data.CheckSceneData();
            data.sceneIndex = -1;
            data.projectIndex = -1;
            DevLog($"data.scenes.Count {data.scenes.Count}");

            data.SceneDupeCheck();

            for (int i = 0; i < data.scenes.Count; i++)
            {
                if (data.scenes[i].Name == dataSetName) data.sceneIndex = i;
                if (data.scenes[i].Name == "Project") data.projectIndex = i;
            }

            if (data.projectIndex < 0)
            {
                DevLog("ProjectIndex not found");
                data.scenes.Add(new SceneData());
                data.projectIndex = data.scenes.Count - 1;
                data.scenes[data.projectIndex].Name = "Project";
                data.scenes[data.projectIndex].MiniMap = new MiniMapData();
            }

            if (data.scenes[data.sceneIndex].ScenePath == null || data.scenes[data.sceneIndex].ScenePath == "")
            {
                data.scenes[data.sceneIndex].ScenePath = SceneManager.GetActiveScene().path;
                DevLog($"scenePath is {data.scenes[data.sceneIndex].ScenePath}");
            }

            if (data.sceneIndex < 0)
            {
                DevLog("SceneIndex not found");
                if (data.projectIndex >= 0)
                {
                    DevLog("ProjectIndex found");
                    data.sceneIndex = data.projectIndex;
                }

                if (dataSetName != null && dataSetName != "")
                {
                    data.scenes.Add(new SceneData());
                    data.sceneIndex = data.scenes.Count - 1;
                }
                data.scenes[data.sceneIndex].Name = dataSetName;
                data.scenes[data.sceneIndex].MiniMap = new MiniMapData();

            }
            DevLog($"SceneIndex is {data.sceneIndex}");
            isSceneAnchored = true;
            if (data.sceneIndex == data.projectIndex)
            {
                DevLog("ProjectIndex is SceneIndex");
                isSceneAnchored = false;
            }
            sceneData = data.scenes[data.sceneIndex];
            sceneData.CurrentIndex = 0;
            ////////
        }


        static void TopUI()
        {
        }
        public static void Init()
        {
            if (isInit == true) return;

            DevLog("Init Core");
            GetPath();
            if (mappables == null) mappables = new List<IMappable>();

            icons = new MightyCoreData.Icons();

            matFade = Resources.Load("MightyFade", typeof(Material)) as Material;

            if (cameraTopDown != null)
            {
                cameraTopDown.SetActive(true);
                cameraTopDown.SetActive(false);
            }
            isInit = true;

            if (data == null) data = Load();

            data.MappableTypesInfo ??= new();
            var mapShapeTypes = TypeCache.GetTypesDerivedFrom(typeof(IMappable));
            DevLog($"Found {mapShapeTypes.Count} Adding new mappable type");
            DevLog($"Found {mappables.Count} mappables");
            foreach (var shapeType in mapShapeTypes)
            {
                // Create an instance to get the human-readable name
                IMappable mappable = (IMappable)Activator.CreateInstance(shapeType);
                // Check if this type info already exists in MappableTypesInfo
                MappableTypeInfo existingInfo = null;
                foreach (var info in data.MappableTypesInfo)
                {
                    DevLog($"Found existing mappable type {info.TypeName} ");
                    if (info.TypeName == shapeType.FullName)
                    {
                        DevLog($"Found existing mappable type {shapeType.FullName} ");
                        existingInfo = info;
                        break;
                    }
                }

                if (existingInfo == null)
                {
                    // If not, add a new entry
                    data.MappableTypesInfo.Add(new MappableTypeInfo(shapeType.FullName, mappable.ToString(), true, mappable));
                }

                else
                {//
                    // If so, update the instance
                    existingInfo.Mappable = mappable;
                }
            }
        }


        public static GameObject GetOrthoCam()
        {
            GameObject r = cameraTopDown;
            if (cameraTopDown == null) r = GameObject.Find("MightyOrthoCam");
            if (r == null) DevLog("Could not find MightyOrthoCam");
            return r;
        }

        #region Module Lifecycle Management

        /// <summary>
        /// Register a module with the lifecycle management system
        /// </summary>
        public static void RegisterModule(MightyCoreData.IModuleLifecycle module)
        {
            MightyCoreData.ModuleManager.RegisterModule(module);
        }

        /// <summary>
        /// Unregister a module from the lifecycle management system
        /// </summary>
        public static void UnregisterModule(string moduleId)
        {
            MightyCoreData.ModuleManager.UnregisterModule(moduleId);
        }

        /// <summary>
        /// Gracefully restart all modules
        /// </summary>
        public static void RestartAllModules()
        {
            MightyCoreData.ModuleManager.RestartAllModules();
        }

        /// <summary>
        /// Perform a graceful system restart
        /// </summary>
        public static void GracefulSystemRestart()
        {
            MightyCoreData.ModuleManager.GracefulSystemRestart();
        }

        /// <summary>
        /// Get health status of all modules
        /// </summary>
        public static Dictionary<string, bool> GetModuleHealthStatus()
        {
            return MightyCoreData.ModuleManager.GetModuleHealthStatus();
        }

        /// <summary>
        /// Get errors from all modules
        /// </summary>
        public static Dictionary<string, string> GetModuleErrors()
        {
            return MightyCoreData.ModuleManager.GetModuleErrors();
        }

        /// <summary>
        /// Get a specific module by ID
        /// </summary>
        public static MightyCoreData.IModuleLifecycle GetModule(string moduleId)
        {
            return MightyCoreData.ModuleManager.GetModule(moduleId);
        }

        /// <summary>
        /// Get all registered modules
        /// </summary>
        public static IReadOnlyList<MightyCoreData.IModuleLifecycle> GetAllModules()
        {
            return MightyCoreData.ModuleManager.GetAllModules();
        }

        #endregion

        private static void CleanupCoreEventHandlers()
        {
            EditorSceneManager.sceneOpened -= SceneOpened;
            EditorSceneManager.sceneSaved -= SceneSaved;
            EditorSceneManager.newSceneCreated -= NewSceneCreated;
            EditorApplication.delayCall -= InitAfterFirstFrame;
            EditorSceneManager.sceneClosing -= SceneClosing;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.quitting -= EditorQuitting;

            if (InitPlaythroughs != null)
                InitPlaythroughs -= InitRunIds;
        }

        //
        static void Refresh()
        {
            if (window != null)
                window.Repaint();
        }


        private static void EditorQuitting()
        {
            DevLog("Editor is quitting, shutting down all modules...");

            // Completely shutdown all modules
            MightyCoreData.ModuleManager.ShutdownAllModules();

            // Clean up event handlers
            CleanupCoreEventHandlers();

            // Clear cached references
            cachedSceneView = null;
            cachedSceneCamera = null;

            SaveData();
        }

        private static void SaveData()
        {
            // DevLog("SaveData");
            if (data == null) return;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        private static void SceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            DevLog($"SceneOpened({scene.name}, {mode})");

            hasFirstFramePassed = false;
            initializing = false;

            // Clear cached references for fresh scene state
            cachedSceneView = null;
            cachedSceneCamera = null;
            frameCounter = 0;
            MightyMap.isInit = false;

            InitAfterFirstFrame();
            MightySceneViewManager.Rebuild();
            MightyMap.Init();

            RestartAllModules();

            // StartModules?.Invoke();


            // GetSceneData();
            // Init();
            // window.rootVisualElement.Q<VisualElement>("MapIconLayer").Clear();
            // if (isSceneAnchored)
            //     StartModules();
            // Rebuild?.Invoke();

            // sceneLoaded = true;
            // DevLog($"Core");
            // data = Load();
            // cameraTopDown = Resources.Load("MightyOrthoCam") as GameObject;

            // sceneAnchor = null;
            // GetSceneData();

            // RefreshAll -= Refresh;
            // RefreshAll += Refresh;
            // Rebuild = MightyMap.RebuildView;


            // BuildTopUI -= TopUI;
            // BuildTopUI += TopUI;

            // //DevLog($"UpdateMarkers: {UpdateMarkers.GetInvocationList().Length}");
            // Init();
            // StartWindow();


            // EditorApplication.update += EditorUpdate;

            // EditorApplication.quitting += EditorQuitting;
            // EditorApplication.playModeStateChanged += ModeChanged;

            // if (isSceneAnchored)
            // {
            //     DevLog("Scene anchored, starting Modules");
            //     StartModules();
            //     Rebuild?.Invoke();
            // }
            // else
            // {
            //     DevLog("Scene not anchored, not starting Modules");
            // }
        }

        private static void _UpdateMarkers()
        {
            // DevLog($"UpdateMarkers:");

            // if (UpdateMarkers == null)
            // {
            //     DevLog("UpdateMarkers Delegate is null");
            //     return;
            // }
            // int i = 0;
            // foreach (Delegate singleCast in UpdateMarkers.GetInvocationList())
            // {
            //     DevLog($"   {i++}: UpdateMarkers Method: {singleCast.Method.DeclaringType}/{singleCast.Method}, Target: {singleCast.Target}");
            // }
        }

        private static void NewSceneCreated(UnityEngine.SceneManagement.Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {

        }

        private static void SceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            SaveData();
        }



    }

}
#endif