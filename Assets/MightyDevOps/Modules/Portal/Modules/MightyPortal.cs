#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UIElements;
using System.Linq;
using UnityEditor.UIElements;
using UnityEditorInternal;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

using static Mighty.MightyCoreData;
using Mighty;
using static Mighty.MightyWindowManagerStateful;

namespace MightyPortal
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(-1000)]
    public class Portal : ModuleBase
    {
        // Module identification
        public override string ModuleId => "com.mighty.portal";
        public override string ModuleName => "Mighty Portal";

        // Module data - now instance fields instead of static
        private Texture2D image;
        private Camera portalCamera, undoCamera;
        private Material material;
        private GameObject _zoomObject, _undoObject;
        private Vector2 posPortal;
        private Vector3 posFinal;
        private Quaternion rotFinal;
        private float rtAlpha = 0.75f, rtAlphaOld = 0.75f;
        private bool _isEnabled = false;
        private float boxXSizeTarget = 0, boxYSizeTarget = 0, boxXSize = 0, boxYSize = 0, boxSizeHover = 150f, boxSizeDrag = 300f;
        private MightyPortalSettings _settings;
        private float precision = 1.0f, undoTimerCurrent = 0f;
        private string mouseOver;
        private GameObject svOriginal;
        private bool followSceneViewOriginal;
        private ComputeShader gammaComputeShader;
        private double lastTime = 0;

        // UIToolkit helper elements
        private VisualElement helperContainer;
        private Label helperText;
        private VisualElement helperList;
        private List<VisualElement> controlLines;
        private List<Label> controlLabels;
        private List<VisualElement> controlIndicators;
        private bool helperOnRightSide = false;
        private SceneView currentSceneView;
        private bool showInitialHelper = true; // Show the activation helper initially

        // Note: State management flags declared elsewhere in the file

        static Portal()
        {
            DevLog("Portal - Registering with new lifecycle system");

            // Register with new lifecycle system
            var module = new Portal();
            MightyCore.RegisterModule(module);

            // Ensure Portal is ready immediately – initialise and start it right after registration.
            // This is unique among Mighty modules because Portal must work even before Mighty Core
            // has finished its anchor/map boot-sequence.
            try
            {
                if (module.State == ModuleLifecycleState.Uninitialized)
                {
                    module.Initialize();
                }

                if (module.State == ModuleLifecycleState.Initialized)
                {
                    module.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Mighty Portal failed to auto-start: {ex.Message}\n{ex.StackTrace}");
            }

#if MIGHTY_PORTAL
#else
            Debug.Log($"Thank you for installing Mighty Portal!  If you enjoy, don't forget to leave a review!");
            MightyPortalDefineSymbol();
#endif

            // Keep the old Scene GUI subscription for now since it's not part of normal lifecycle
#if UNITY_2018
            SceneView.onSceneGUIDelegate -= OnSceneStatic;
            SceneView.onSceneGUIDelegate += OnSceneStatic;
#endif

#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnSceneStatic;
            SceneView.duringSceneGui += OnSceneStatic;
#endif

            // Subscribe to play mode state change
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Subscribe to scene change
            EditorSceneManager.sceneOpened += OnSceneOpened;

            // Cleanup before domain reload or editor quit
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnBeforeAssemblyReload()
        {
            var module = MightyCore.GetModule("com.mighty.portal") as Portal;
            if (module != null)
            {
                module.CleanupResources();
            }
        }

        private static void OnEditorQuitting()
        {
            var module = MightyCore.GetModule("com.mighty.portal") as Portal;
            if (module != null)
            {
                module.CleanupResources();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                if (module != null)
                {
                    module.CleanupResources();
                }
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            var module = MightyCore.GetModule("com.mighty.portal") as Portal;
            if (module != null)
            {
                module.CleanupResources();
            }
        }

        /// <summary>
        /// Initialize the module - load data, setup references
        /// </summary>
        protected override void OnInitialize()
        {
            DevLog($"Initializing {ModuleName}...");

            // Load settings and resources
            RefreshSettings();
            lastTime = EditorApplication.timeSinceStartup;

            DevLog($"{ModuleName} initialization complete");
        }

        /// <summary>
        /// Start the module - subscribe to events, setup UI
        /// </summary>
        protected override void OnStart()
        {
            DevLog($"Starting {ModuleName}...");

            // Subscribe to Unity events safely
            SafeSubscribeEditorCallback(ref EditorApplication.update, EditorUpdate);

            // Setup UIToolkit helper
            SetupUIToolkitHelper();

            // Show initial activation helper
            ShowInitialHelper();

            // Show getting started if first run
            ShowGettingStartedIfFirstRun();

            DevLog($"{ModuleName} started successfully");
        }

        /// <summary>
        /// Stop the module - cleanup UI, but keep data
        /// </summary>
        protected override void OnStop()
        {
            DevLog($"Stopping {ModuleName}...");

            // Disable portal functionality
            if (isEnabled)
            {
                Disable();
            }

            // Cleanup UIToolkit helper
            CleanupUIToolkitHelper();

            // Ensure render textures are cleaned up
            CleanupResources();

            // Events are automatically unsubscribed by base class
            DevLog($"{ModuleName} stopped");
        }

        /// <summary>
        /// Shutdown the module completely - cleanup all resources
        /// </summary>
        protected override void OnShutdown()
        {
            DevLog($"Shutting down {ModuleName}...");

            // Cleanup resources
            CleanupResources();

            // Unsubscribe from events
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;

            DevLog($"{ModuleName} shutdown complete");
        }

        #region Helper Methods

        private void ShowGettingStartedIfFirstRun()
        {
            // Check if this is the first run
            string firstRunKey = "MightyPortal_FirstRun";
            DevLog($"Mighty Portal: First run key: {firstRunKey} {EditorPrefs.GetBool(firstRunKey, false)}");
            EditorPrefs.SetBool(firstRunKey, true);
            if (!EditorPrefs.GetBool(firstRunKey, false))
            {
                ICommand command = new OpenGettingStartedWindowCommand();
                command.Execute();
                EditorPrefs.SetBool(firstRunKey, true);
            }
        }

        private void CleanupResources()
        {
            // Clean up portal objects
            if (zoomObject != null)
            {
                zoomObject.SetActive(false);
            }
            if (undoObject != null)
            {
                undoObject.SetActive(false);
            }

            // Clean up render textures
            if (portalCamera != null && portalCamera.targetTexture != null)
            {
                var temp = portalCamera.targetTexture;
                portalCamera.targetTexture = null;
                if (temp != null)
                {
                    temp.Release();
                    UnityEngine.Object.DestroyImmediate(temp);
                }
            }
            if (undoCamera != null && undoCamera.targetTexture != null)
            {
                var temp = undoCamera.targetTexture;
                undoCamera.targetTexture = null;
                if (temp != null)
                {
                    temp.Release();
                    UnityEngine.Object.DestroyImmediate(temp);
                }
            }

            // Clear references
            image = null;
            portalCamera = null;
            undoCamera = null;
            material = null;
            zoomObject = null;
            undoObject = null;
            svOriginal = null;
            gammaComputeShader = null;
            settings = null;
        }

        /// <summary>
        /// Static scene GUI handler - kept for compatibility
        /// </summary>
        private static void OnSceneStatic(SceneView view)
        {
            // Find the active module instance and delegate to it
            var module = MightyCore.GetModule("com.mighty.portal") as Portal;
            if (module?.State == ModuleLifecycleState.Started)
            {
                module.OnSceneInstance(view);
            }
        }


        // Removed duplicate OnScene method - using the main static one below

        #endregion

        #region Static Accessors for Backward Compatibility

        /// <summary>
        /// Static accessor for settings - delegates to current active instance
        /// </summary>
        public static MightyPortalSettings settings
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                return module?._settings;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                if (module != null) module._settings = value;
            }
        }

        /// <summary>
        /// Static accessor for undoObject - delegates to current active instance
        /// </summary>
        public static GameObject undoObject
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                return module?._undoObject;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                if (module != null) module._undoObject = value;
            }
        }

        /// <summary>
        /// Static accessor for zoomObject - delegates to current active instance
        /// </summary>
        public static GameObject zoomObject
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                return module?._zoomObject;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                if (module != null) module._zoomObject = value;
            }
        }

        /// <summary>
        /// Static accessor for isEnabled - delegates to current active instance
        /// </summary>
        public static bool isEnabled
        {
            get
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                return module?._isEnabled ?? false;
            }
            set
            {
                var module = MightyCore.GetModule("com.mighty.portal") as Portal;
                if (module != null) module._isEnabled = value;
            }
        }



        #endregion

        static void MightyPortalDefineSymbol()
        {
#if UNITY_2023_2_OR_NEWER // covers 2023.2, 2023.3, 6.x and beyond
            const string symbol = "MIGHTY_PORTAL";

            // New API that uses NamedBuildTarget
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string scriptingDefines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

            if (!scriptingDefines.Contains(symbol))
            {
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.IsNullOrEmpty(scriptingDefines) ? symbol : scriptingDefines + ";" + symbol);
            }
#else
            // Legacy API for 2021.x and early 2022.x
            const string symbol = "MIGHTY_PORTAL";

            string scriptingDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            if (!scriptingDefines.Contains(symbol))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.IsNullOrEmpty(scriptingDefines) ? symbol : scriptingDefines + ";" + symbol);
            }
#endif
        }

        // StartModule functionality moved to OnStart lifecycle method


        public class OpenGettingStartedWindowCommand : ICommand
        {
            public OpenGettingStartedWindowCommand()
            {
                // this.root = root;
            }

            public void Execute()
            {
                // Check if the window system is ready
                if (MightyCoreData.window == null)
                {
                    // Defer the window creation until the next editor update when the window system should be ready
                    EditorApplication.delayCall += Execute;
                    return;
                }

                var gettingStarted = new MightyPortalGettingStartedWindow();
                gettingStarted.BuildView();

                var win = new MightyWindowStateful(gettingStarted.view,
                    typeof(OpenGettingStartedWindowCommand),
                    "Mighty Portal - Getting Started",
                    new Vector2(32, 32),
                    typeof(OpenGettingStartedWindowCommand));

                if (win.content != null)
                {
                    root.Add(win);
                }
                BuildWindowBar?.Invoke();
            }
        }


        /// <summary>
        /// Instance-based editor update method
        /// </summary>
        private void EditorUpdate()
        {
            if (State != ModuleLifecycleState.Started || settings?.isActive != true)
            {
                return;
            }

            if (isEnabled && EditorWindow.mouseOverWindow != null)
            {
                mouseOver = EditorWindow.mouseOverWindow.ToString();
            }
            else
            {
                mouseOver = "Nothing...";
            }

            if (isEnabled && !mouseOver.Contains("SceneView"))
            {
                if (!isOOB)
                {
                    isOOB = true;
                    rtAlphaOld = rtAlpha;
                    rtAlpha = 0f;
                    if (settings.showOOB) Debug.LogWarning("Mighty Portal: Mouse events must remain on top of the SceneView window to work, please hover back or release keys to cancel navigation.");
                }
            }
            else
            {
                if (isOOB)
                {
                    isOOB = false;
                    rtAlpha = rtAlphaOld;
                }
            }

            if (isUndo)
            {
                EditorWindow.GetWindow<SceneView>().Repaint();
            }
        }

        /// <summary>
        /// Enable portal functionality
        /// </summary>
        public void Enable()
        {
            isEnabled = firstEnabled = true;
            zoomObject.SetActive(true);

            boxSizeHover = settings.hoverBoxSize;
            boxSizeDrag = settings.adjustBoxSize;
            boxXSize = 1;
            boxXSizeTarget = boxSizeHover;
            boxYSize = 1;
            boxYSizeTarget = boxSizeHover;

            // Hide initial helper and show portal helper
            showInitialHelper = false;
            ShowHelper();
        }

        /// <summary>
        /// Disable portal functionality
        /// </summary>
        public void Disable()
        {
            isEnabled = false;
            isAdjust = false;
            isDragging = false;
            isMidDown = false;
            isDone = false;
            svCameraOverride = null;
            //followSceneView = followSceneViewOriginal;
            //zoomObject.SetActive(false);

            // Reset to initial helper state
            showInitialHelper = true;
            if (helperContainer != null)
            {
                helperContainer.style.display = DisplayStyle.Flex;
                UpdateHelperContent();
            }
        }

        /// <summary>
        /// Reset the helper GUI state to initial state
        /// </summary>
        private void ResetHelperState()
        {
            isAdjust = false;
            isDragging = false;
            isMidDown = false;
            isDone = false;
            precision = 1.0f;
        }

        public static GameObject FindObject(GameObject parent, string name)
        {
            Transform[] trs = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in trs)
            {
                if (t.name == name)
                {
                    //Debug.Log("Found " + name);
                    return t.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Refresh portal settings and load resources
        /// </summary>
        public void RefreshSettings()
        {
            settings = Resources.Load("MightyPortalSettingsSO") as MightyPortalSettings;
            zoomObject = Resources.Load("MightyPortalCamera") as GameObject;
            svOriginal = Resources.Load("MightyPortalSVOriginal") as GameObject;
            undoObject = Resources.Load("MightyPortalUndoCamera") as GameObject;

            if (zoomObject != null) zoomObject.hideFlags = HideFlags.None;
            if (undoObject != null) undoObject.hideFlags = HideFlags.None;

            if (zoomObject != null)
            {
                portalCamera = zoomObject.GetComponentInChildren<Camera>();
                if (portalCamera != null && SceneView.lastActiveSceneView?.camera != null)
                {
                    CopyCameraSettings(SceneView.lastActiveSceneView.camera, portalCamera);
                    portalCamera.forceIntoRenderTexture = true;
                    portalCamera.targetTexture = new RenderTexture(1024, 1024, 24, GetBestRenderTextureFormat(), RenderTextureReadWrite.Default);
                    portalCamera.targetTexture.Create();
                    portalCamera.orthographic = false;
                }
            }

            image = new Texture2D(1024, 1024, TextureFormat.ARGB32, false);


            if (undoObject != null)
            {
                undoCamera = undoObject.GetComponentInChildren<Camera>();
            }

            //imageMask = Resources.Load<Texture2D>("Textures/MightyPortalPreviewMask");
            material = Resources.Load<Material>("Textures/MightyPortalMaterial");

            if (settings != null)
            {
                settings.isDeleted = false;
            }

            // Load compute shader once
            if (gammaComputeShader == null)
                gammaComputeShader = Resources.Load<ComputeShader>("MightyPortalComputerShader");
        }

        static void CopyCameraSettings(Camera source, Camera target)
        {
            if (source == null || target == null) return;

            target.fieldOfView = source.fieldOfView;
            target.orthographic = source.orthographic;
            target.orthographicSize = source.orthographicSize;
            target.nearClipPlane = source.nearClipPlane;
            target.farClipPlane = source.farClipPlane;
            target.backgroundColor = source.backgroundColor;
            target.clearFlags = source.clearFlags;
            target.depth = source.depth;
            target.renderingPath = source.renderingPath;
            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;
            target.allowDynamicResolution = source.allowDynamicResolution;

            // If using URP/HDRP, also copy additional data if available
            // (requires using UnityEngine.Rendering.Universal or HighDefinition)

            var sceneSkybox = RenderSettings.skybox;
            if (sceneSkybox != null)
            {
                var skyboxComponent = target.GetComponent<Skybox>();
                if (skyboxComponent == null)
                    skyboxComponent = target.gameObject.AddComponent<Skybox>();
                skyboxComponent.material = sceneSkybox;
            }
        }



        // Converted static variables to instance variables 
        private bool isDragging = false, isMidDown = false, isDone = false, isAdjust = false, isOOB = false, firstEnabled = true, isUndo = false;
        private int shOffset = 39;
        private float screenWidth, screenHeight;

        /// <summary>
        /// Instance-based scene GUI handler (converted from static)
        /// </summary>
        private void OnSceneInstance(SceneView sceneview)
        {
            if (State != ModuleLifecycleState.Started)
            {
                Debug.Log($"Portal is not started");
                return;
            }

            // Ensure we have the helper set up for the current SceneView
            if (currentSceneView != sceneview)
            {
                DevLog($"SceneView changed from {currentSceneView?.titleContent.text} to {sceneview.titleContent.text}");
                CleanupUIToolkitHelper();
                currentSceneView = sceneview;
                CreateHelperUI();
            }

            Event e = Event.current;
            float YOffset = 0;

            if (settings == null) RefreshSettings();
            if (settings?.isDeleted == true) RefreshSettings();
            if (undoObject == null) RefreshSettings();
            if (zoomObject == null) RefreshSettings();

            if (settings?.isActive != true) return;

            screenWidth = sceneview.position.width;
            screenHeight = sceneview.position.height;

            CheckInput(e);

            // Update UIToolkit helper content and position
            if (isEnabled)
            {
                UpdateHelperContent();
                UpdateHelperPosition(); // Check for portal intersection and reposition if needed

                // Update scroll wheel active state
                if (e.type == EventType.ScrollWheel && isAdjust)
                {
                    UpdateHelperContentWithScrolling(true); // Show scroll wheel as active
                }
            }
            else if (showInitialHelper)
            {
                // Show initial activation helper when portal is not enabled
                if (helperContainer != null && helperContainer.style.display == DisplayStyle.None)
                {
                    helperContainer.style.display = DisplayStyle.Flex;
                    UpdateHelperContent();
                }
            }

            if (isUndo)
            {
                undoObject.SetActive(true);
                double currentTime = EditorApplication.timeSinceStartup;
                undoTimerCurrent += (float)(currentTime - lastTime);
                lastTime = currentTime;
                Handles.BeginGUI();
                {
                    var originalColor = GUI.color;
                    float alpha = 1 - (float)(undoTimerCurrent / settings.undoTimerMax);
                    GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, alpha);

                    // Make sure the undo camera renders before we try to use its texture
                    var undoCamera = undoObject.GetComponent<Camera>();
                    if (undoCamera != null && undoCamera.targetTexture != null)
                    {
                        undoCamera.Render();
                        if (GUI.Button(new Rect(screenWidth - 116, 128, 100, 100), new GUIContent(undoCamera.targetTexture, "UNDOBUTTON"), GUIStyle.none))
                        {
                            SceneView.currentDrawingSceneView.AlignViewToObject(undoObject.transform);
                            undoTimerCurrent = 0f;
                            isUndo = false;
                        }
                    }

                    if (GUI.tooltip == "UNDOBUTTON")
                    {
                        undoTimerCurrent = 0f;
                    }
                    GUI.color = originalColor;
                    GUIStyle s = new GUIStyle(GUI.skin.textArea);
                    s.alignment = TextAnchor.MiddleCenter;
                    GUI.TextArea(new Rect(screenWidth - 116, 128, 100, 16), "Undo Teleport", s);
                    EditorGUI.ProgressBar(new Rect(screenWidth - 116, 144, 100, 4), alpha, "");

                    undoTimerCurrent += Time.deltaTime;
                    if (undoTimerCurrent >= settings.undoTimerMax)
                    {
                        undoTimerCurrent = 0f;
                        isUndo = false;
                        undoObject.SetActive(false);
                    }
                }
                Handles.EndGUI();
            }
            else
            {
                try
                {
                    //if (undoObject.activeInHierarchy == true) undoObject.SetActive(false);
                }
                catch { }
            }

            if (boxXSize + boxYSize < 1 || isDone) Disable();

            if (!isEnabled)
            {
                if (boxXSize > 10 && settings.animateBoxes)
                {
                    Handles.BeginGUI();
                    {
                        var originalColor = GUI.color;
                        GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, rtAlpha);
                        boxXSize = Mathf.Lerp(boxXSize, 0, .5f);
                        boxYSize = Mathf.Lerp(boxYSize, 0, .5f);
                        float xPos, yPos;
                        xPos = e.mousePosition.x - (boxXSize / 2);
                        yPos = e.mousePosition.y - (boxYSize / 2);
                        GUI.DrawTexture(new Rect(xPos, yPos, boxXSize, boxYSize), image);
                        GUI.color = originalColor;
                    }
                    Handles.EndGUI();
                    //SceneView.RepaintAll();
                }
                return;
            }

            #region CheckBounds
            if (e.mousePosition.y < 0)
            {
                if (!isOOB)
                {
                    isOOB = true;
                    rtAlphaOld = rtAlpha;
                    rtAlpha = 0f;
                    if (settings.showOOB) Debug.LogWarning("Mighty Portal: Mouse events must remain on top of the SceneView window to work, please hover back or release keys to cancel navigation.");
                }
            }
            #endregion

            #region OverlayZoomWindow
            if (!isOOB && (firstEnabled || e.type == EventType.MouseMove))
            {
                rtAlpha = settings.hoverAlpha;
                var mousePos = e.mousePosition;
                posPortal = mousePos;

                float ppp = EditorGUIUtility.pixelsPerPoint;
                mousePos.y = sceneview.camera.pixelHeight - mousePos.y * ppp;
                mousePos.x *= ppp;

                Ray ray = sceneview.camera.ScreenPointToRay(mousePos);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 10000, settings.layerMask))
                {
                    zoomObject.transform.position = hit.point + new Vector3(0, 2, 0);
                    var sv = SceneView.currentDrawingSceneView.camera.transform.rotation;
                    zoomObject.transform.rotation = Quaternion.Euler(0, sv.eulerAngles.y, 0);
                }
                if (e.type != EventType.Layout && e.type != EventType.Repaint) e.Use();
                if (firstEnabled) firstEnabled = false;

                svOriginal.transform.position = GetSVCameraPosition();
                svOriginal.transform.rotation = GetSVCameraRotation();
                followSceneViewOriginal = followSceneView;

                followSceneView = true;

                svCameraOverride = zoomObject.GetComponentInChildren<Camera>();

            }
            #endregion

            #region AdjustZoomView

            //Increase window size, lower transparency
            if (!isOOB && e.type == EventType.MouseDown && e.button == 1)
            {
                isAdjust = true;

                Vector3 mousePos = e.mousePosition;
                float ppp = EditorGUIUtility.pixelsPerPoint;
                mousePos.y = sceneview.camera.pixelHeight - mousePos.y * ppp;
                mousePos.x *= ppp;
                Ray ray = sceneview.camera.ScreenPointToRay(mousePos);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 10000, settings.layerMask))//ScenePilotEditorWindow.layerMask))
                {
                    posFinal = hit.point + new Vector3(0, YOffset + 2, 0);
                    var rot = SceneView.currentDrawingSceneView.camera.transform.rotation;
                    rotFinal = Quaternion.Euler(rot.eulerAngles.x, rot.eulerAngles.y, 0f);
                }

                e.Use();

                rtAlpha = settings.adjustAlpha;
                boxXSizeTarget = boxYSizeTarget = boxSizeDrag;

                // Update helper to show active state
                UpdateHelperContent();
            }

            //Adjust rotation and height
            if (!isOOB && isAdjust && e.type == EventType.MouseDrag)
            {
                isDragging = true;
                int modifier = 1;

                if (!isMidDown)
                {
                    if (settings.invertRotation) { modifier = -1; } else { modifier = 1; }
                    rotFinal = Quaternion.Euler(rotFinal.eulerAngles.x, rotFinal.eulerAngles.y, 0) * Quaternion.Euler(e.delta.y * precision * modifier, e.delta.x * precision * modifier, 0f);
                    zoomObject.transform.localRotation = rotFinal;

                }
                else
                {
                    if (settings.invertMovement) { modifier = 1; } else { modifier = -1; }

                    posFinal += zoomObject.transform.right * (-e.delta.x * modifier) * precision;
                    posFinal += new Vector3(0, (e.delta.y * modifier) * precision, 0);
                    zoomObject.transform.localPosition = posFinal;
                }

                e.Use();

                // Update helper to show dragging state
                UpdateHelperContent();
            }


            if (!isOOB && isDragging && e.isScrollWheel)
            {
                int modifier;
                if (settings.invertScrolWheel) { modifier = 1; } else { modifier = -1; }
                float scrollDelta = e.delta.x;
                Vector3 movement = zoomObject.transform.forward * scrollDelta * modifier * precision;
                posFinal += movement;
                zoomObject.transform.localPosition = posFinal;
                e.Use();

                // Update helper to show scrolling state
                UpdateHelperContentWithScrolling(true);
            }

            if (!isOOB && isDragging && e.type == EventType.MouseDown && e.button == 2)
            {
                isMidDown = true;
                e.Use();

                // Update helper to show panning state
                UpdateHelperContent();
            }

            if (!isOOB && isDragging && e.type == EventType.MouseUp && e.button == 2)
            {
                isMidDown = false;

                // Update helper to remove panning state
                UpdateHelperContent();
            }


            #endregion

            #region ExecuteZoom
            if (!isOOB && e.type == EventType.MouseUp && e.button == 1)
            {
                isUndo = true;
                ExecuteFlight();


                svCameraOverride = null;
                //followSceneView = followSceneViewOriginal;

                rtAlpha = settings.hoverAlpha;
                boxXSizeTarget = boxYSizeTarget = 0;
                isDragging = false;

                // Update helper back to initial state
                UpdateHelperContent();
            }
            #endregion
            #region DrawGUI
            if (Event.current.type.Equals(EventType.Repaint))
            {
                Handles.BeginGUI();
                {
                    #region PreviewWindow
                    var originalColor = GUI.color;
                    GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, rtAlpha);

                    if (settings.animateBoxes && isEnabled)
                    {
                        boxXSize = Mathf.Lerp(boxXSize, boxXSizeTarget, .5f);
                        boxYSize = Mathf.Lerp(boxYSize, boxYSizeTarget, .5f);
                    }
                    else
                    {
                        boxXSize = boxXSizeTarget;
                        boxYSize = boxYSizeTarget;
                    }

                    float xPos, yPos;
                    // xPos = e.mousePosition.x - (boxXSize / 2);
                    // yPos = e.mousePosition.y - (boxYSize / 2);
                    xPos = posPortal.x - (boxXSize / 2);
                    yPos = posPortal.y - (boxYSize / 2);

                    if (xPos < 0) xPos = 0;
                    if (yPos < 0) yPos = 0;

                    if (xPos + boxXSize > screenWidth) xPos = screenWidth - boxXSize;
                    if (yPos + boxYSize + shOffset > screenHeight) yPos = screenHeight - boxYSize - shOffset;

                    //portalCamera.targetTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                    portalCamera.Render();
                    if (portalCamera.targetTexture == null)
                    {
                        RefreshSettings();
                        portalCamera.Render();
                    }

                    if (portalCamera.targetTexture == null)
                    {
                        Debug.LogError("Mighty Portal: RenderTexture is null, please restart Unity or try recompiling the any code to reset the script.  This is a rare error.");
                        return;
                    }
                    RenderTexture.active = portalCamera.targetTexture;
                    image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                    image.Apply();
                    RenderTexture.active = null;
                    if (settings.UseShader)
                    {
                        material.SetFloat("_Alpha", rtAlpha);
                        material.SetFloat("_Brightness", settings.shaderBrightness);
                        EditorGUI.DrawPreviewTexture(new Rect(xPos, yPos, boxXSize, boxYSize), image, material);
                    }
                    else
                    {
                        GUI.DrawTexture(new Rect(xPos, yPos, boxXSize, boxYSize), image);
                    }

                    if (settings.toggleMode) GUI.TextArea(new Rect(xPos, yPos, boxXSize, 20), "Press Esc to Cancel");
                    GUI.color = originalColor;
                    #endregion
                }
                Handles.EndGUI();
            }
            #endregion

            // UIToolkit helper is now handled automatically in Enable/Disable methods
        }



        static public void ExecuteFlight()
        {
            var svc = SceneView.currentDrawingSceneView.camera;

            undoObject.transform.position = svc.transform.position;
            undoObject.transform.rotation = svc.transform.rotation;
            undoObject.GetComponent<Camera>().fieldOfView = svc.fieldOfView;
            undoObject.GetComponent<Camera>().orthographic = svc.orthographic;
            undoObject.GetComponent<Camera>().orthographicSize = svc.orthographicSize;

            // Reset helper state after execution
            var module = MightyCore.GetModule("com.mighty.portal") as Portal;
            if (module != null)
            {
                module.ResetHelperState();
                module.undoTimerCurrent = 0;
                SceneView.currentDrawingSceneView.AlignViewToObject(module._zoomObject.transform);
            }

            // Set up the undo camera's render texture
            var undoCamera = undoObject.GetComponent<Camera>();

            // Store the current active render texture
            RenderTexture previousRT = RenderTexture.active;

            // Store the current target texture
            RenderTexture currentTargetTexture = undoCamera.targetTexture;

            // Create new render texture if needed
            if (currentTargetTexture == null)
            {
                currentTargetTexture = new RenderTexture(1024, 1024, 24, GetBestRenderTextureFormat());
                currentTargetTexture.Create();
                undoCamera.targetTexture = currentTargetTexture;
            }

            undoCamera.Render();

            // Apply gamma correction to the render texture
            RenderTexture.active = currentTargetTexture;
            Texture2D tempTex = new Texture2D(currentTargetTexture.width, currentTargetTexture.height, TextureFormat.RGBA32, false);
            tempTex.ReadPixels(new Rect(0, 0, currentTargetTexture.width, currentTargetTexture.height), 0, 0);
            tempTex.Apply();

            // Apply gamma correction
            Color[] pixels = tempTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(
                    Mathf.Pow(pixels[i].r, 1.0f / 2.2f),
                    Mathf.Pow(pixels[i].g, 1.0f / 2.2f),
                    Mathf.Pow(pixels[i].b, 1.0f / 2.2f),
                    pixels[i].a
                );
            }
            tempTex.SetPixels(pixels);
            tempTex.Apply();

            // Create a new render texture with the corrected image
            if (currentTargetTexture != null)
            {
                // Unassign the texture from the camera before releasing
                undoCamera.targetTexture = null;

                // Release the old texture
                if (RenderTexture.active == currentTargetTexture)
                {
                    RenderTexture.active = null;
                }
                currentTargetTexture.Release();

                // Create and assign new texture
                currentTargetTexture = new RenderTexture(1024, 1024, 24, GetBestRenderTextureFormat());
                currentTargetTexture.Create();
                undoCamera.targetTexture = currentTargetTexture;

                Graphics.Blit(tempTex, currentTargetTexture);
            }

            UnityEngine.Object.DestroyImmediate(tempTex);

            // Restore the previous active render texture
            RenderTexture.active = previousRT;
        }

        static private void CheckInput(Event current)
        {
            var module = MightyCore.GetModule("com.mighty.portal") as Portal;
            if (module == null) return;

            try
            {
                if (EditorWindow.mouseOverWindow != null) { module.mouseOver = EditorWindow.mouseOverWindow.ToString(); } else { module.mouseOver = "Nothing..."; }
            }
            catch
            {
                // Debug.Log(current);
            }
            if (!module.mouseOver.Contains("SceneView")) return;

            if (current.keyCode == KeyCode.Escape)
            {
                module.boxXSizeTarget = module.boxYSizeTarget = 0;
                module.Disable();
                module.ResetHelperState(); // Reset helper state on cancel
                return;
            }

            if (current.modifiers == (module._settings.keyStartA | module._settings.keyStartB))
            {
                module.precision = 1f;
                if (module._isEnabled) return;
                module.Enable();
            }
            else if (current.modifiers == (module._settings.keyStartA | module._settings.keyStartB | module._settings.keyPrecision) || (module._settings.toggleMode && current.modifiers == module._settings.keyPrecision))
            {
                module.precision = 0.1f;
            }
            else if (!module._settings.toggleMode)
            {
                module.boxXSizeTarget = module.boxYSizeTarget = 0;
            }
        }
        static float nfmod(float a, float b)
        {
            return a - b * Mathf.Floor(a / b);
        }

        public static class MenuUtility
        {
            public static bool DoesMenuItemExist(string menuPath)
            {
                var menuCommands = typeof(Menu).GetField("s_MenuItems", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as object[];
                if (menuCommands != null)
                {
                    foreach (var menuItem in menuCommands)
                    {
                        var menuItemPath = menuItem.GetType().GetField("path", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menuItem) as string;
                        if (menuItemPath == menuPath)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }


        [Serializable]
        public class Root : IMappable
        {
            private string _version = "2.1.0";
            public string Version
            {
                get => _version;
                set => _version = value;
            }
            private string _packageName = "Mighty Portal";
            public string PackageName
            {
                get => _packageName;
                set => _packageName = value;
            }

            [SerializeField] private string name;
            [SerializeField] private string description;
            [SerializeField] private string anchorTo;
            [SerializeField] private Location mapLocation;
            [SerializeField] private long createdAt;
            [SerializeField] private long lastModified;
            [SerializeField] private long lastQueried;
            [SerializeField] private string status;
            [SerializeField] private int id;
            [SerializeField] private int parentId;
            [SerializeField] private bool active;
            [SerializeField] private bool front;
            [SerializeField] private bool dirty;
            [SerializeField] private bool hasVisualContent;
            [SerializeField] private bool hasPlayTracking = false;
            [SerializeField] private bool showAlways = false;
            [SerializeField] private Views viewUI;
            [SerializeField] private VisualElement prevView;

            [SerializeField] private VisualElement view;
            [SerializeField] private Vector3 offset;
            [SerializeField] private Attributes mapAttributes;
            [SerializeField] private Picture pic;

            public string Name { get => name; set => name = value; }
            public string Description { get => description; set => description = value; }
            public string AnchorTo { get => anchorTo; set => anchorTo = value; }
            public Location MapLocation { get => mapLocation; set => mapLocation = value; }
            public long CreatedAt { get => createdAt; set => createdAt = value; }
            public long LastModified { get => lastModified; set => lastModified = value; }
            public long LastQueried { get => lastQueried; set => lastQueried = value; }
            public string Status { get => status; set => status = value; }
            public int ID { get => id; set => id = value; }
            public int ParentId { get => parentId; set => parentId = value; }
            public bool Active { get => active; set => active = value; }
            public bool Front { get => front; set => front = value; }
            public bool Dirty { get => dirty; set => dirty = value; }
            public VisualElement PrevView { get => prevView; set => prevView = value; }
            public bool HasVisualContent { get => hasVisualContent; set => hasVisualContent = value; }
            public bool HasPlayTracking { get => hasPlayTracking; set => hasPlayTracking = value; }
            public bool ShowAlways { get => showAlways; set => showAlways = value; }
            public Views ViewUI { get => viewUI; set => viewUI = value; }
            public VisualElement View { get => view; set => view = value; }
            public Vector3 Offset { get => offset; set => offset = value; }

            public Attributes MapAttributes { get => mapAttributes; set => mapAttributes = value; }
            public Picture Pic { get => pic; set => pic = value; }

            private Texture2D _icon;

            public Texture2D Icon
            {
                get
                {
                    if (_icon == null)
                    {
                        _icon = Resources.Load<Texture2D>("mighty_icon_toggle_Portal");
                    }
                    return _icon;
                }
                set
                {
                    _icon = value;
                }
            }

            public Texture2D iconAddMappable { get; set; }

            public override string ToString()
            {
                return "Portal";
            }

            public bool UpdateAvailable()
            {
                // var PortalUpdate = dataCore.moduleUpdates.FirstOrDefault(update => update.module == "Portal");
                // if (PortalUpdate != null)
                // {
                //     ShowToast($"New Mighty Portal Version: {PortalUpdate.version}");
                //     return PortalUpdate.version != Version;
                // }
                return false;
            }

            public void RegisterMappable()
            {
                // Debug.Log("Registering Portal");
            }

            public void OnGenerateVisualContent(MeshGenerationContext mgc)
            {
                // Debug.Log("Generating Portal Visual Content");
            }

            public void Delete()
            {
                // Debug.Log("Deleting Portal");
            }

            public void LoadImage()
            {
                // Debug.Log("Loading Portal Image");
            }

            public void CheckIntegrity()
            {
                // Debug.Log("Checking Portal Integrity");
            }

            public Button AddMappable(bool setClickedCallback = true)
            {
                // Debug.Log("Adding Portal Mappable");
                return null;
            }

            public CustomToggleButton AddModuleToggle(MappableTypeInfo mappableTypeInfo)
            {
                DevLog($"AddModuleToggle named {mappableTypeInfo.Name}");
                return new(Icon, mappableTypeInfo, "PortalOverlay", () =>
                {
                    // Debug.Log($"Toggled {mappableTypeInfo.Name} settings.isActive: {settings.isActive} isEnabled: {isEnabled}");
                    if (settings.isActive)
                    {
                        settings.isActive = false;
                        isEnabled = false;
                    }
                    else
                    {
                        settings.isActive = true;

                    }
                });
            }

            public VisualElement SceneSummary(SceneData scene)
            {
                return new VisualElement();
            }

            public VisualElement SettingsView()
            {
                VisualElement settingsView = new VisualElement()
                {
                    name = "ScenePilotSettingsView",
                    style = {
                 flexDirection = FlexDirection.Column,
                 width = Length.Percent(100),
                 height = Length.Percent(100),
                 backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.9f)),
                 flexGrow = 1,
                 }
                };

                // Add centralized feedback section
                var feedbackSection = CreateFeedbackSection(
                    "Mighty Tracking",
                    "https://prf.hn/click/camref:1011lf9gY/pubref:mdo/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Flevel-design%2Fmighty-leap-teleport-around-your-scene-instantly-177244",
                    "https://github.com/ShrinkRayEntertainment/Mighty-Portal/issues/new?template=%F0%9F%90%9B-bug-report.md&labels=bug&title=[{VERSION}%20|%20{UNITY_VERSION}]%20Your%20Title",
                    "https://github.com/ShrinkRayEntertainment/Mighty-Portal/issues/new?template=%E2%9C%A8-feature-request.md&labels=enhancement&title=[{VERSION}%20|%20{UNITY_VERSION}]%20Your%20Title",
                    Version
                );
                settingsView.Add(feedbackSection);
                settingsView.Add(Spacer());

                Button gettingStartedButton = new Button(() =>
{
    ICommand command = new OpenGettingStartedWindowCommand();
    command.Execute();
})
                {
                    text = "Getting Started",
                    style = {
                            height = 32,
                            backgroundColor = new Color(0.3f, 0.6f, 0.9f, 1f),
                            color = Color.white,
                            borderTopLeftRadius = 6,
                            borderTopRightRadius = 6,
                            borderBottomLeftRadius = 6,
                            borderBottomRightRadius = 6,
                            borderTopWidth = 0,
                            borderBottomWidth = 0,
                            borderLeftWidth = 0,
                            borderRightWidth = 0,
                            fontSize = 12,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginTop = 10,
                            marginBottom = 10,
                        }
                };
                settingsView.Add(gettingStartedButton);

                settingsView.Add(Header("Portal Settings"));
                settingsView.Add(Spacer());

                // settingsView.Add(Header("General"));
                settingsView.Add(HelpText("Choose which layers can be targetted by the Raycast"));

                var layers = InternalEditorUtility.layers.ToList(); // Convert to List<string>
                Debug.Log($"Settings is null: {settings == null}");
                Debug.Log($"Settings.layerMask: {settings.layerMask}");
                var layersSelection = new MaskField(layers, settings.layerMask);
                settingsView.Add(layersSelection);

                settingsView.Add(HelpText("Toggle Mode On means you hit the Enable keys and release, press Escape or complete travel to disable Scene Pilot.  Off means you hold down the Enable Keys and releasing them will disable.  If Scene Pilot keeps disabling (happens with resource intense situations), try Toggle Mode on!"));
                var toggleMode = new Toggle("Toggle Mode") { value = settings.toggleMode };
                settingsView.Add(toggleMode);

                settingsView.Add(HelpText("When adjusting the final placement, should the mouse be inverted?"));
                var invertMovement = new Toggle("Invert Movement") { value = settings.invertMovement };
                var invertRotation = new Toggle("Invert Rotation") { value = settings.invertRotation };
                var invertScrollWheel = new Toggle("Invert Scrollwheel") { value = settings.invertScrolWheel };
                settingsView.Add(invertMovement);
                settingsView.Add(invertRotation);
                settingsView.Add(invertScrollWheel);

                settingsView.Add(Spacer());
                settingsView.Add(Spacer());
                settingsView.Add(Header("Hotkeys"));
                settingsView.Add(HelpText("Which key(s) trigger Scene Pilot to start?"));
                settingsView.Add(StyledLabel("Start Key 1"));
                var keyStartA = new EnumField(settings.keyStartA);
                settingsView.Add(keyStartA);
                settingsView.Add(StyledLabel("Start Key 2"));
                var keyStartB = new EnumField(settings.keyStartB);
                settingsView.Add(keyStartB);

                settingsView.Add(HelpText("Which key allows for precision movement?"));
                settingsView.Add(StyledLabel("Precision Key"));
                var keyPrecision = new EnumField(settings.keyPrecision);
                settingsView.Add(keyPrecision);

                settingsView.Add(Spacer());
                settingsView.Add(Spacer());
                settingsView.Add(Header("Graphics"));
                settingsView.Add(HelpText("How big should the preview images be?"));
                settingsView.Add(StyledLabel("When Hovering"));
                var hoverBoxSize = new Slider(0, 600) { value = settings.hoverBoxSize };
                settingsView.Add(hoverBoxSize);
                settingsView.Add(StyledLabel("When Adjusting View"));
                var adjustBoxSize = new Slider(0, 600) { value = settings.adjustBoxSize };
                settingsView.Add(adjustBoxSize);

                settingsView.Add(HelpText("How transparent should the preview images be?"));
                settingsView.Add(StyledLabel("When Hovering"));
                var hoverAlpha = new Slider(0f, 1f) { value = settings.hoverAlpha };
                settingsView.Add(hoverAlpha);
                settingsView.Add(StyledLabel("When Adjusting View"));
                var adjustAlpha = new Slider(0f, 1f) { value = settings.adjustAlpha };
                settingsView.Add(adjustAlpha);

                settingsView.Add(HelpText("Increase Performance?"));
                var animateBoxes = new Toggle("Animate Preview") { value = settings.animateBoxes };
                var useShader = new Toggle("Use Sphere Shader") { value = settings.UseShader };
                settingsView.Add(animateBoxes);
                settingsView.Add(useShader);

                var shaderBrightness = new Slider("Shader Brightness", -1f, 1f) { value = settings.shaderBrightness };
                settingsView.Add(shaderBrightness);


                settingsView.Add(Spacer());
                settingsView.Add(Spacer());
                settingsView.Add(Header("Miscellaneous"));

                settingsView.Add(HelpText("Set how long the Undo Timer button will appear for.  Set to 0 to disable."));
                settingsView.Add(StyledLabel("Undo Timer Seconds"));
                var undoTimerMax = new Slider(0, 60) { value = settings.undoTimerMax };
                settingsView.Add(undoTimerMax);

                settingsView.Add(HelpText("Warn me when I hover outside of the Scene Window?"));
                var showOOB = new Toggle("Show warning") { value = settings.showOOB };
                settingsView.Add(showOOB);

                // Handle changes
                layersSelection.RegisterValueChangedCallback(evt => settings.layerMask = evt.newValue);
                toggleMode.RegisterValueChangedCallback(evt => settings.toggleMode = evt.newValue);
                invertMovement.RegisterValueChangedCallback(evt => settings.invertMovement = evt.newValue);
                invertRotation.RegisterValueChangedCallback(evt => settings.invertRotation = evt.newValue);
                invertScrollWheel.RegisterValueChangedCallback(evt => settings.invertScrolWheel = evt.newValue);
                keyStartA.RegisterValueChangedCallback(evt => settings.keyStartA = (EventModifiers)evt.newValue);
                keyStartB.RegisterValueChangedCallback(evt => settings.keyStartB = (EventModifiers)evt.newValue);
                keyPrecision.RegisterValueChangedCallback(evt => settings.keyPrecision = (EventModifiers)evt.newValue);
                hoverBoxSize.RegisterValueChangedCallback(evt => settings.hoverBoxSize = (int)evt.newValue);
                adjustBoxSize.RegisterValueChangedCallback(evt => settings.adjustBoxSize = (int)evt.newValue);
                hoverAlpha.RegisterValueChangedCallback(evt => settings.hoverAlpha = evt.newValue);
                adjustAlpha.RegisterValueChangedCallback(evt => settings.adjustAlpha = evt.newValue);
                animateBoxes.RegisterValueChangedCallback(evt => settings.animateBoxes = evt.newValue);
                useShader.RegisterValueChangedCallback(evt => settings.UseShader = evt.newValue);
                shaderBrightness.RegisterValueChangedCallback(evt => settings.shaderBrightness = evt.newValue);
                undoTimerMax.RegisterValueChangedCallback(evt => settings.undoTimerMax = (int)evt.newValue);
                showOOB.RegisterValueChangedCallback(evt => settings.showOOB = evt.newValue);

                // Other assets section
                // settingsView.Add(new Label("Other Assets from ShrinkRay Entertainment!")
                // {
                //     style = { unityTextAlign = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold }
                // });

                // var taLogoButton = new Button() { style = { backgroundImage = new StyleBackground(taLogo), width = 256, height = 217 } };
                // taLogoButton.clicked += () => Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/task-atlas-185959?aid=1011lf9gY&pubref=ep");
                // settingsView.Add(taLogoButton);
                // settingsView.Add(new Label(taText));
                // var taButton = new Button() { text = "GET IT HERE" };
                // taButton.clicked += () => Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/task-atlas-185959?aid=1011lf9gY&pubref=ep");
                // settingsView.Add(taButton);

                // var pfLogoButton = new Button() { style = { backgroundImage = new StyleBackground(pfLogo), width = 128, height = 128 } };
                // pfLogoButton.clicked += () => Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/perfect-f-177783?aid=1011lf9gY&pubref=ep");
                // settingsView.Add(pfLogoButton);
                // settingsView.Add(new Label(pfText));
                // var pfButton = new Button() { text = "GET IT HERE" };
                // pfButton.clicked += () => Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/perfect-f-177783?aid=1011lf9gY&pubref=ep");
                // settingsView.Add(pfButton);


                return settingsView;
            }

            public MightySceneViewManager.Settings GetSceneViewSettings()
            {
                return new MightySceneViewManager.Settings();
            }

            public void PopulatePlayTrackingLane(int laneIndex)
            {
                // Debug.Log("Populating Portal Play Tracking Lane");
            }
        }


        // Helper to detect the best RenderTextureFormat for the current pipeline
        static RenderTextureFormat GetBestRenderTextureFormat()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                string pipelineName = pipeline.GetType().Name;
                if (pipelineName.Contains("HDRenderPipelineAsset") || pipelineName.Contains("HDRP"))
                {
                    return RenderTextureFormat.DefaultHDR;
                }
                else if (pipelineName.Contains("UniversalRenderPipelineAsset") || pipelineName.Contains("URP"))
                {
                    return RenderTextureFormat.ARGBHalf;
                }
            }
            // Built-in pipeline fallback
            return RenderTextureFormat.ARGB32;
        }

        private void SetupUIToolkitHelper()
        {
            // Find the active SceneView
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                // Try to get the focused SceneView
                sceneView = SceneView.currentDrawingSceneView;
            }

            if (sceneView != null)
            {
                currentSceneView = sceneView;
                CreateHelperUI();
                DevLog($"Portal helper setup for SceneView: {currentSceneView.titleContent.text}");
            }
            else
            {
                DevLog("Portal helper setup failed - no SceneView found");
                // Retry on next frame
                EditorApplication.delayCall += SetupUIToolkitHelper;
            }
        }

        private void CleanupUIToolkitHelper()
        {
            if (helperContainer != null && currentSceneView != null)
            {
                currentSceneView.rootVisualElement.Remove(helperContainer);
                helperContainer = null;
                helperText = null;
                helperList = null;
                DevLog("Portal helper cleaned up");
            }
        }

        private void CreateHelperUI()
        {
            if (currentSceneView == null)
            {
                DevLog("Cannot create helper UI - currentSceneView is null");
                return;
            }

            // Clean up any existing helper first
            if (helperContainer != null)
            {
                currentSceneView.rootVisualElement.Remove(helperContainer);
            }

            // Create main container
            helperContainer = new VisualElement();
            helperContainer.name = "portal-helper";
            helperContainer.style.position = Position.Absolute;
            helperContainer.style.bottom = 14;
            helperContainer.style.left = 14;
            helperContainer.style.width = 130; // Slightly wider to fit text
            helperContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.88f);
            helperContainer.style.borderTopLeftRadius = 4;
            helperContainer.style.borderTopRightRadius = 4;
            helperContainer.style.borderBottomLeftRadius = 4;
            helperContainer.style.borderBottomRightRadius = 4;
            helperContainer.style.paddingTop = 5; // 50% of 10
            helperContainer.style.paddingBottom = 5; // 50% of 10
            helperContainer.style.paddingLeft = 7; // 50% of 14
            helperContainer.style.paddingRight = 7; // 50% of 14
            helperContainer.style.display = DisplayStyle.None; // Hidden by default

            // Create single text label for initial state
            helperText = new Label("• Right-click to open");
            helperText.style.color = new Color(0.9f, 0.9f, 0.9f);
            helperText.style.fontSize = 10; // Reduced from 12
            helperText.style.unityTextAlign = TextAnchor.MiddleLeft;
            helperText.style.height = 18; // Reduced from 24
            helperText.style.display = DisplayStyle.Flex;

            // Create container for multiple lines (active state)
            helperList = new VisualElement();
            helperList.style.display = DisplayStyle.None;

            // Create control lines once and store references
            CreatePersistentControlLines();

            // Add elements to container
            helperContainer.Add(helperText);
            helperContainer.Add(helperList);

            // Add to SceneView
            currentSceneView.rootVisualElement.Add(helperContainer);

            DevLog($"Portal helper UI created and added to SceneView. Container children: {helperContainer.childCount}");
        }

        private void CreatePersistentControlLines()
        {
            controlLines = new List<VisualElement>();
            controlLabels = new List<Label>();
            controlIndicators = new List<VisualElement>();

            string[] controlTexts = {
                "• Drag: Rotate",
                "• Scroll: Move",
                "• Mid: Pan",
                $"• {settings?.keyPrecision ?? EventModifiers.Alt}: Precision"
            };

            foreach (string text in controlTexts)
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.height = 18; // Reduced from 24
                container.style.alignItems = Align.Center;

                // Active indicator (initially hidden)
                var indicator = new VisualElement();
                indicator.style.width = 3; // Reduced from 4
                indicator.style.height = 18; // Reduced from 24
                indicator.style.backgroundColor = new Color(1f, 0.8f, 0.2f, 0.8f);
                indicator.style.marginLeft = -4; // Reduced from -6
                indicator.style.marginRight = 1; // Reduced from 2
                indicator.style.display = DisplayStyle.None; // Hidden by default
                container.Add(indicator);

                // Text label
                var label = new Label(text);
                label.style.color = new Color(0.9f, 0.9f, 0.9f);
                label.style.fontSize = 10; // Reduced from 12
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexGrow = 1;
                container.Add(label);

                helperList.Add(container);
                controlLines.Add(container);
                controlLabels.Add(label);
                controlIndicators.Add(indicator);
            }

            DevLog($"Created {controlLines.Count} persistent control lines");
        }

        private void UpdateHelperContent()
        {
            if (helperContainer == null)
            {
                DevLog("UpdateHelperContent: helperContainer is null");
                return;
            }

            DevLog($"UpdateHelperContent: showInitialHelper={showInitialHelper}, isAdjust={isAdjust}, isDragging={isDragging}, isMidDown={isMidDown}, precision={precision}");

            if (showInitialHelper)
            {
                // Show activation helper
                helperText.style.display = DisplayStyle.Flex;
                helperList.style.display = DisplayStyle.None;
                string keyText = GetActivationKeyText();
                helperText.text = $"• Hold {keyText}";
                helperText.style.color = new Color(0.7f, 0.9f, 1f); // Light blue to indicate it's different
                DevLog("UpdateHelperContent: Showing activation helper");
            }
            else if (!isAdjust)
            {
                // Show portal hover state
                helperText.style.display = DisplayStyle.Flex;
                helperList.style.display = DisplayStyle.None;
                helperText.text = "• Right-click to open";
                helperText.style.color = new Color(0.9f, 0.9f, 0.9f);
                DevLog("UpdateHelperContent: Showing portal hover state");
            }
            else
            {
                // Show active state
                helperText.style.display = DisplayStyle.None;
                helperList.style.display = DisplayStyle.Flex;

                // Update existing control lines instead of recreating them
                UpdateControlLineStates();

                DevLog("UpdateHelperContent: Showing active state with updated control lines");
            }
        }

        private string GetActivationKeyText()
        {
            if (settings == null) return "Ctrl+Shift";

            string keyA = settings.keyStartA.ToString();
            string keyB = settings.keyStartB.ToString();

            // Clean up the key names for display
            keyA = keyA.Replace("Modifiers", "").Replace("Event", "");
            keyB = keyB.Replace("Modifiers", "").Replace("Event", "");

            return $"{keyA}+{keyB}";
        }

        private void ShowInitialHelper()
        {
            if (helperContainer != null)
            {
                showInitialHelper = true;
                helperContainer.style.display = DisplayStyle.Flex;
                UpdateHelperContent();
                DevLog("Initial activation helper shown");
            }
        }

        private void UpdateControlLineStates()
        {
            if (controlLines == null || controlLabels == null || controlIndicators == null) return;

            // Determine active states
            bool[] activeStates = {
                isDragging && !isMidDown,  // Drag Mouse: Rotate
                false,                     // Scrollwheel: Move (updated separately)
                isDragging && isMidDown,   // Middle Mouse: Pan
                precision < 1f             // Precision mode
            };

            // Update precision key text if needed
            if (controlLabels.Count > 3)
            {
                controlLabels[3].text = $"• {settings?.keyPrecision ?? EventModifiers.Alt}: Precision";
            }

            for (int i = 0; i < Math.Min(activeStates.Length, controlLines.Count); i++)
            {
                bool isActive = activeStates[i];

                // Update indicator visibility
                controlIndicators[i].style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;

                // Update text color
                controlLabels[i].style.color = isActive ? new Color(1f, 0.8f, 0.2f) : new Color(0.9f, 0.9f, 0.9f);
            }

            DevLog($"UpdateControlLineStates: Updated states - drag={activeStates[0]}, scroll={activeStates[1]}, pan={activeStates[2]}, precision={activeStates[3]}");
        }

        private void UpdateHelperContentWithScrolling(bool isScrolling)
        {
            if (helperContainer == null || !isAdjust) return;

            // Update just the scroll wheel state without recreating everything
            if (controlIndicators != null && controlIndicators.Count > 1 && controlLabels != null && controlLabels.Count > 1)
            {
                controlIndicators[1].style.display = isScrolling ? DisplayStyle.Flex : DisplayStyle.None;
                controlLabels[1].style.color = isScrolling ? new Color(1f, 0.8f, 0.2f) : new Color(0.9f, 0.9f, 0.9f);
            }

            // Also update other states
            UpdateControlLineStates();

            DevLog($"UpdateHelperContentWithScrolling: scroll={isScrolling}");
        }

        private void OnHelperMouseEnter(MouseEnterEvent evt)
        {
            // Remove the old mouse-based flipping - we'll use portal intersection instead
            // This method can be removed or left empty
        }

        private void UpdateHelperPosition()
        {
            if (helperContainer == null || currentSceneView == null) return;

            // Calculate portal rect
            Rect portalRect = GetPortalRect();

            // Calculate helper rect for both sides
            float helperWidth = 130f; // Adjusted to fit shorter text
            float helperHeight = isAdjust ? (4 * 18f) + 10f : 18f + 10f; // 4 lines + padding or 1 line + padding (all 50% smaller)
            float padding = 14f;

            Rect leftHelperRect = new Rect(padding, screenHeight - helperHeight - padding, helperWidth, helperHeight);
            Rect rightHelperRect = new Rect(screenWidth - helperWidth - padding, screenHeight - helperHeight - padding, helperWidth, helperHeight);

            // Check for intersections
            bool leftIntersects = portalRect.Overlaps(leftHelperRect);
            bool rightIntersects = portalRect.Overlaps(rightHelperRect);

            // Choose the side that doesn't intersect, prefer left if both are clear
            bool shouldBeOnRight;
            if (leftIntersects && !rightIntersects)
            {
                shouldBeOnRight = true;
            }
            else if (!leftIntersects && rightIntersects)
            {
                shouldBeOnRight = false;
            }
            else
            {
                // Both sides are clear or both intersect - keep current side or default to left
                shouldBeOnRight = helperOnRightSide;
            }

            // Only update position if it needs to change
            if (shouldBeOnRight != helperOnRightSide)
            {
                helperOnRightSide = shouldBeOnRight;
                ApplyHelperPosition();
                DevLog($"Helper moved to {(helperOnRightSide ? "right" : "left")} side to avoid portal overlap");
            }
        }

        private void ApplyHelperPosition()
        {
            if (helperContainer == null) return;

            if (helperOnRightSide)
            {
                helperContainer.style.left = StyleKeyword.Auto;
                helperContainer.style.right = 14;
            }
            else
            {
                helperContainer.style.right = StyleKeyword.Auto;
                helperContainer.style.left = 14;
            }
        }

        private Rect GetPortalRect()
        {
            if (!isEnabled)
            {
                return new Rect(0, 0, 0, 0); // No portal when disabled
            }

            // Use the current portal position and size
            float portalWidth = boxXSize > 0 ? boxXSize : boxSizeHover;
            float portalHeight = boxYSize > 0 ? boxYSize : boxSizeHover;

            // Calculate portal position (same logic as in the original portal rendering)
            float xPos = posPortal.x - (portalWidth / 2);
            float yPos = posPortal.y - (portalHeight / 2);

            // Ensure portal stays within screen bounds (same clamping as original)
            if (xPos < 0) xPos = 0;
            if (yPos < 0) yPos = 0;
            if (xPos + portalWidth > screenWidth) xPos = screenWidth - portalWidth;
            if (yPos + portalHeight + 39 > screenHeight) yPos = screenHeight - portalHeight - 39; // 39 is shOffset

            return new Rect(xPos, yPos, portalWidth, portalHeight);
        }

        private void ShowHelper()
        {
            if (helperContainer != null)
            {
                helperContainer.style.display = DisplayStyle.Flex;
                UpdateHelperContent();
                DevLog("Portal helper shown");
            }
            else
            {
                DevLog("Portal helper container is null - attempting to recreate");
                SetupUIToolkitHelper();
            }
        }

        private void HideHelper()
        {
            if (helperContainer != null)
            {
                helperContainer.style.display = DisplayStyle.None;
                DevLog("Portal helper hidden");
            }
        }
    }
}   // End of Class
#endif