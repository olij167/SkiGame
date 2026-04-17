#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static Mighty.MightyCore;
using static Mighty.MightyCoreData;
using static Mighty.MightyCoreData.SceneData;
using static Mighty.MightyWindowManagerStateful;



namespace Mighty
{
    public class MightyMap : EditorWindow
    {
        #region Variables

        public static bool isInit, isAdjust, didAdjust, rebuildClusters, is2DMode, GUILoaded = false;
        private static VisualElement playthroughContainer, playthroughContainerExtra;
        public static float svOrthSize;
        static SceneView sceneView;
        static bool sceneLoading = false;

        static StyleSheet mightyStylesheet;

        public static bool isHoveringOnMappable = false;
        public static List<Button> addMappableButtons = new List<Button>();
        private static Debouncer debouncer;

        private static Button sceneCamIcon;

        private static bool firstRun = true;

        private const float SIDE_MENU_WIDTH = 196f;
        private const float TITLE_BAR_HEIGHT = 48f;
        private const float BUTTON_WIDTH = 32f;

        #endregion

        #region initialize
        static MightyMap()
        {
            // Clean up any existing handlers before recompilation
            CleanupEventHandlers();

            StartWindow -= Start;
            StartWindow += Start;
        }

        public static bool showSceneCamIcon;


        [MenuItem("Window/MIGHTY MAP")]
        private static void OpenMightyMapWindow()
        {
            window = GetWindow();
        }

        public static EditorWindow GetWindow()
        {
            // Debug.Log("MightyMap GetWindow");
            if (window == null)
            {
                // Debug.Log("MightyMap GetWindow: window is null, creating new window");
                window = GetWindow<MightyMap>(false, "Mighty Map", false);

                window.minSize = new Vector2(400, 600);
                window.Show();
                window.titleContent.text = "Mighty Map";
                Rebuild?.Invoke();
            }
            return window;
        }

        void OnDestroy()
        {
            CleanupEventHandlers();
            if (Map != null && data?.MappableTypesInfo != null)
            {
                // Clean up visual content event handlers
                foreach (var typeInfo in data.MappableTypesInfo)
                {
                    if (typeInfo?.Mappable != null && typeInfo.Mappable.HasVisualContent && Map != null)
                    {
                        Map.generateVisualContent -= typeInfo.Mappable.OnGenerateVisualContent;
                    }
                }
            }

            // Clear static references to prevent stale references
            window = null;
            root = null;
            Map = null;
            sceneCamIcon = null;
            sceneView = null;
            isInit = false;
            GUILoaded = false;
        }


        public static void Start()
        {
            // Clean up any existing event handlers first to prevent duplicates during recompilation
            CleanupEventHandlers();

            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;

            EditorApplication.quitting -= EditorQuit;
            EditorApplication.quitting += EditorQuit;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            RebuildRunIdDropDown -= InitDropDownRunIds;
            RebuildRunIdDropDown += InitDropDownRunIds;

            BuildWindowBar -= PopulateWindowBar;
            BuildWindowBar += PopulateWindowBar;

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.sceneClosing += OnSceneClosing;

            Rebuild -= RebuildView;
            Rebuild += RebuildView;

            OpenUpdatesWindow -= OpenUpdates;
            OpenUpdatesWindow += OpenUpdates;

            Dirty = true;
            isInit = false;
        }

        private static void CleanupEventHandlers()
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.quitting -= EditorQuit;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;

            if (RebuildRunIdDropDown != null)
                RebuildRunIdDropDown -= InitDropDownRunIds;
            if (BuildWindowBar != null)
                BuildWindowBar -= PopulateWindowBar;
            if (Rebuild != null)
                Rebuild -= RebuildView;
            if (OpenUpdatesWindow != null)
                OpenUpdatesWindow -= OpenUpdates;
        }

        private static void OpenUpdates()
        {
            ICommand command = new OpenUpdateWindowCommand();
            command.Execute();
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            sceneLoading = true;
            MightyCoreData.sceneLoaded = false;
            MightyCoreData.isSceneAnchored = false;
            MightyCoreData.modulesStarted = false;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            sceneLoading = true;
            MightyCoreData.sceneLoaded = false;
            MightyCoreData.isSceneAnchored = false;
            MightyCoreData.modulesStarted = false;
        }

        private static void EditorUpdate()
        {
            if (!isSceneAnchored) return;
            if (EditorApplication.isPlaying) return;
            if (window == null) return; // Early exit if window is null

            if (sceneLoading && EditorSceneManager.GetActiveScene().isLoaded)
            {
                sceneLoading = false;
                Rebuild();
            }

            if (sceneData != null)
                if (sceneData.CollectedData == null)
                {
                    sceneData.CollectedData = new List<SceneData.GameObjectData>();
                }

            // Cache SceneView reference to avoid multiple calls
            if (sceneView == null || !sceneView)
                sceneView = GetSceneView();

            if (sceneView != null)
                is2DMode = sceneView.in2DMode;
            else
                is2DMode = false;

            if (svCameraOverride == null)
            {
                data.svPos = GetSVCameraPosition();
                data.svRot = GetSVCameraRotation();
            }
            else
            {
                data.svPos = svCameraOverride.transform.position;
                data.svRot = svCameraOverride.transform.rotation;
            }

            bool positionChanged = Math.Round(data.svPos.x, 3) != Math.Round(data._svPos.x, 3) &&
                                 Math.Round(data.svPos.z, 3) != Math.Round(data._svPos.z, 3);
            bool rotationChanged = data.svRot != data._svRot;
            bool positionOrRotationChanged = positionChanged || (data.svPos != data._svPos);

            if (positionChanged && showSceneCamIcon)
            {
                if (followSceneView)
                {
                    sceneData.MiniMap.Position = data.svPos;
                    sceneData.MiniMap.Rotation = data.svRot;

                    if (isSceneAnchored)
                        UpdateMarkers();
                    root?.MarkDirtyRepaint();
                }
                else
                {
                    sceneCamIcon?.MarkDirtyRepaint();
                }
            }

            else if ((rotationChanged || positionOrRotationChanged) && showSceneCamIcon)
            {
                sceneData.MiniMap.Rotation = data.svRot;

                // Only query for sceneCamIcon if it's null and we have a valid window
                if (sceneCamIcon == null && window?.rootVisualElement != null)
                {
                    sceneCamIcon = window.rootVisualElement.Q<Button>(name: "sceneCamIcon");
                }

                if (sceneCamIcon != null)
                {
                    sceneCamIcon.style.rotate = new StyleRotate(new UnityEngine.UIElements.Rotate(new Angle(data.svRot.eulerAngles.y)));
                    sceneCamIcon.MarkDirtyRepaint();
                }
            }

            data._svPos = data.svPos;
            data._svRot = data.svRot;
        }

        private static void UpdateNotificationIcon()
        {
            if (notifications != null && dataCore != null)
            {
                // Use the persistent global flag instead of recalculating
                notifications.style.backgroundImage = dataCore.hasUnreadNews ? icons.notificationOnIcon : icons.notificationOffIcon;
            }
        }

        private static void EditorQuit()
        {

        }

        public static void Init()
        {

            if (initializing) return;
            initializing = true;

            // try
            // {
            // if (window == null) OpenMightyMapWindow();
            if (MightyCore.isInit == false) MightyCore.Init();

            Dirty = false;
            isInit = true;

            if (window?.rootVisualElement != null)
            {
                window.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(GeometryChangedCallback);
                window.rootVisualElement.RegisterCallback<GeometryChangedEvent>(GeometryChangedCallback);
            }


            CreateGUI();
            RestoreWindows();
            // }
            // catch (System.Exception ex)
            // {
            //     Debug.LogError($"MightyMap Init failed: {ex.Message}");
            // }
            // finally
            // {
            initializing = false;
            // }
        }
        #endregion


        #region Commands

        static void RestoreWindows()
        {
            var windowsCopy = windowManagerStateful.serializableWindows.ToList();

            foreach (var windowState in windowsCopy)
            {
                if (windowState.restorationCommandTypeName == null) continue;
                Type commandType = Type.GetType(windowState.restorationCommandTypeName);
                if (commandType != null)
                {
                    ICommand restoreCommand = (ICommand)Activator.CreateInstance(commandType);
                    restoreCommand?.Execute();
                }
                else
                {
                    DevLogError("Could not find command type: " + windowState.restorationCommandTypeName);
                }
            }
        }

        public class OpenSceneGraphWindowCommand : ICommand
        {
            public OpenSceneGraphWindowCommand()
            {
                // this.root = root;
            }

            public void Execute()
            {
                MightySceneBrowser sceneBrowser = MightySceneBrowser.Load();
                sceneBrowser.BuildView();

                var win = new MightyWindowStateful(sceneBrowser.view,
                    typeof(PopOutSceneBrowser),
                    "Scene Browser",
                    new Vector2(32, 32),
                    typeof(OpenSceneGraphWindowCommand));

                if (win.content != null)
                {
                    root.Add(win);
                }
                BuildWindowBar?.Invoke();
            }
        }

        public class OpenNotificationsWindowCommand : ICommand
        {
            public OpenNotificationsWindowCommand()
            {
                // this.root = root;
            }

            public void Execute()
            {
                MightyNotifications notifications = MightyNotifications.Load();
                notifications.BuildView();

                var win = new MightyWindowStateful(notifications.view,
                    typeof(PopOutSceneBrowser),
                    "Mighty News",
                    new Vector2(32, 32),
                    typeof(OpenNotificationsWindowCommand));

                if (win.content != null)
                {
                    root.Add(win);
                }
                BuildWindowBar?.Invoke();
            }
        }

        public class OpenUpdateWindowCommand : ICommand
        {
            public OpenUpdateWindowCommand()
            {
                // this.root = root;
            }

            public void Execute()
            {
                MightyUpdates updates = MightyUpdates.Load();
                updates.BuildView();

                var win = new MightyWindowStateful(updates.view,
                    typeof(PopOutSceneBrowser),
                    "Mighty Updates",
                    new Vector2(32, 32),
                    typeof(OpenNotificationsWindowCommand));

                if (win.content != null)
                {
                    root.Add(win);
                }
                BuildWindowBar?.Invoke();
            }
        }

        #endregion

        #region Util



        #endregion

        #region sceneData.miniMap

        static float targetYPos = 0;
        static float currentYPos = 0;
        static float lerpSpeed = 0.5f;
        static float threshold = 0.5f;
        static float ceilingOffset = 100f;

        static public void UpdateMiniMap()
        {
            //Ensure the window is still open otherwise return
            if (window == null) return;

            // Debug.Log("UpdateMiniMap");

            if (EditorApplication.isPlaying)
            {
                return;
            }

            if (sceneData == null)
            {
                return;
            }

            if (sceneData.MiniMap == null)
            {
                sceneData.MiniMap = new MiniMapData();
            }

            if (Map == null)
            {
                Map = new()
                {
                    name = "map",
                    style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                position = Position.Relative,
                alignItems = Align.FlexStart,
                flexGrow = 1,
                flexShrink = 1,
                }
                };

            }

            //check if root has map, if not add it
            if (root.Q<VisualElement>(name: "map") == null && Map != null)
            {
                root.Add(Map);
                Map.SendToBack();
            }

            // Debug.Log("UpdateMiniMap");
            // DevLog("UpdateMiniMap");
            // ShowToast("Updating MiniMap");
            sceneData.MiniMap.CachePos = sceneData.MiniMap.Position;
            sceneData.MiniMap.WidthCache = screenWidth;
            sceneData.MiniMap.HeightCache = screenHeight;
            sceneData.MiniMap.OrthSizeCache = sceneData.MiniMap.OrthSize;

            // Debug.Log($"cameraTopDown is null: {cameraTopDown == null}");
            GameObject cameraGO = cameraTopDown;
            sceneCamera = cameraGO.GetComponent<Camera>();

            if (sceneCamera.orthographic)
            {
                if (GetSceneView().in2DMode)
                {
                    sceneData.MiniMap.Rotation = Quaternion.Euler(0f, 0f, 0f);
                    sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x, sceneData.MiniMap.Position.y, -10f);
                }
                else
                {
                    sceneData.MiniMap.Rotation = Quaternion.Euler(90f, 0f, 0f);
                    RaycastHit hit;
                    float upwardRayLength = 700;
                    Vector3 rayOrigin = GetSVCameraPosition();
                    Vector3 rayDirection = Vector3.up;

                    if (Physics.Raycast(rayOrigin, rayDirection, out hit, upwardRayLength))
                    {
                        float newYPos = hit.point.y - 0.1f;
                        float deltaY = Mathf.Abs(newYPos - targetYPos);
                        if (deltaY > threshold)
                        {
                            targetYPos = newYPos;
                        }
                    }
                    else
                    {
                        if (Physics.Raycast(rayOrigin + new Vector3(0, 1, 0), -rayDirection, out hit, upwardRayLength))
                        {
                            targetYPos = hit.point.y + ceilingOffset;
                        }
                    }

                    sceneCamera.orthographicSize = sceneData.MiniMap.OrthSize;

                    currentYPos = Mathf.Lerp(currentYPos, targetYPos, lerpSpeed);
                    sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x, currentYPos, sceneData.MiniMap.Position.z);
                }
            }
            else

            {
                sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x, sceneData.MiniMap.OrthSize, sceneData.MiniMap.Position.z);
            }

            if (sceneData.MiniMap.OrthSize < 1)
            {
                sceneData.MiniMap.OrthSize = 1;
            }
            sceneCamera.orthographicSize = sceneData.MiniMap.OrthSize;
            sceneCamera.transform.position = sceneData.MiniMap.Position;
            sceneCamera.transform.rotation = sceneData.MiniMap.Rotation;

            sceneData.MiniMap.PixelHeight = sceneCamera.pixelHeight;
            sceneData.MiniMap.PixelWidth = sceneCamera.pixelWidth;

            sceneData.MiniMap.Topleft = sceneCamera.ScreenToWorldPoint(new Vector3(0, sceneCamera.pixelHeight, sceneCamera.nearClipPlane));
            sceneData.MiniMap.Topright = sceneCamera.ScreenToWorldPoint(new Vector3(sceneCamera.pixelWidth, sceneCamera.pixelHeight, sceneCamera.nearClipPlane));
            sceneData.MiniMap.Botleft = sceneCamera.ScreenToWorldPoint(new Vector3(0, 0, sceneCamera.nearClipPlane));
            sceneData.MiniMap.Botright = sceneCamera.ScreenToWorldPoint(new Vector3(sceneCamera.pixelWidth, 0, sceneCamera.nearClipPlane));

            if (screenWidth != 0 || screenHeight != 0)
            {
                RenderTexture currentRT = new RenderTexture((int)screenWidth, (int)screenHeight, 24);
                sceneCamera.targetTexture = currentRT;
                sceneCamera.Render();

                RenderTexture.active = currentRT;




                // Assume you have a material set up with the HeatmapShader
                // Material heatmapMaterial = new Material(Shader.Find("Custom/HeatmapShader"));
                if (heatmaps.Count > 0)
                    foreach (var heatmap in heatmaps)
                    {
                        RenderTexture heatmapRT = heatmap.RenderHeatmap((int)screenWidth, (int)screenHeight, sceneCamera);

                        // SaveRenderTextureToPNG(heatmapRT, $"{MightyCoreData.GetCache()}heatmap.png");

                        if (heatmapRT != null)
                        {
                            // Blend the heatmapRT with currentRT
                            BlendRenderTextures(currentRT, heatmapRT);
                        }
                    }

                sceneData.MiniMap.map = new Texture2D((int)screenWidth, (int)screenHeight, TextureFormat.RGB24, false);
                sceneData.MiniMap.map.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);

                sceneData.MiniMap.map.Apply();

                RenderTexture.active = null;
                sceneCamera.targetTexture = null;
            }

            if (sceneData.MiniMap.map == null) sceneData.MiniMap.map = MakeTex((int)screenWidth, (int)screenHeight, Color.blue);

            Map.style.backgroundImage = sceneData.MiniMap.map;

            if (isSceneAnchored)
            {
                if (firstRun)
                {
                    sceneData.MiniMap.SaveImage();
                    firstRun = false;
                }
                else if (sceneData.MiniMap.MapPath == "")
                {
                    sceneData.MiniMap.SaveImage();
                }
            }
            // MightyCoreData.sceneData.MiniMap.SaveImage();
        }

        static void BlendRenderTextures(RenderTexture baseRT, RenderTexture overlayRT)
        {
            Material blendMaterial = new Material(Shader.Find("Custom/HeatmapBlend"));

            blendMaterial.SetTexture("_MainTex", baseRT);
            blendMaterial.SetTexture("_OverlayTex", overlayRT);

            RenderTexture tempRT = RenderTexture.GetTemporary(baseRT.width, baseRT.height, 0, baseRT.format);

            // Save base and overlay textures to PNG for debugging
            // SaveRenderTextureToPNG(baseRT, $"{MightyCoreData.GetCache()}base.png");
            // SaveRenderTextureToPNG(overlayRT, $"{MightyCoreData.GetCache()}overlay.png");

            // Blend overlayRT onto tempRT using the custom shader
            Graphics.Blit(baseRT, tempRT, blendMaterial);

            // Save the blended texture to PNG for debugging
            // SaveRenderTextureToPNG(tempRT, $"{MightyCoreData.GetCache()}blended.png");

            // Copy the result back to baseRT
            Graphics.Blit(tempRT, baseRT);

            RenderTexture.ReleaseTemporary(tempRT);
        }

        public static void SaveRenderTextureToPNG(RenderTexture rt, string filePath)
        {
            RenderTexture currentActiveRT = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);

            RenderTexture.active = currentActiveRT;

            DestroyImmediate(tex); // Clean up the Texture2D to free memory
        }

        // static public void UpdateMiniMap()
        // {
        //     sceneData.MiniMap.CachePos = sceneData.MiniMap.Position;
        //     sceneData.MiniMap.WidthCache = screenWidth;
        //     sceneData.MiniMap.HeightCache = screenHeight;
        //     sceneData.MiniMap.OrthSizeCache = sceneData.MiniMap.OrthSize;

        //     GameObject cameraGO = cameraTopDown;
        //     sceneCamera = cameraGO.GetComponent<Camera>();

        //     if (sceneCamera.orthographic)
        //     {
        //         if (GetSceneView().in2DMode)
        //         {
        //             sceneData.MiniMap.Rotation = Quaternion.Euler(0f, 0f, 0f);

        //             sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x, sceneData.MiniMap.Position.y, -10f);
        //         }
        //         else
        //         {
        //             sceneData.MiniMap.Rotation = Quaternion.Euler(90f, 0f, 0f);
        //             RaycastHit hit;
        //             float upwardRayLength = 700;
        //             Vector3 rayOrigin = GetSVCameraPosition();
        //             Vector3 rayDirection = Vector3.up;

        //             //Debug.DrawRay(rayOrigin, rayDirection * upwardRayLength, Color.red);

        //             if (Physics.Raycast(rayOrigin, rayDirection, out hit, upwardRayLength))
        //             {
        //                 float newYPos = hit.point.y - 0.1f;
        //                 float deltaY = Mathf.Abs(newYPos - targetYPos);
        //                 if (deltaY > threshold)
        //                 {
        //                     targetYPos = newYPos;
        //                 }
        //             }
        //             else
        //             {
        //                 if (Physics.Raycast(rayOrigin + new Vector3(0, 1, 0), -rayDirection, out hit, upwardRayLength))
        //                 {
        //                     targetYPos = hit.point.y + ceilingOffset;
        //                 }
        //             }

        //             sceneCamera.orthographicSize = sceneData.MiniMap.OrthSize;

        //             currentYPos = Mathf.Lerp(currentYPos, targetYPos, lerpSpeed);
        //             sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x,
        //                                                     currentYPos,
        //                                                     sceneData.MiniMap.Position.z);
        //         }
        //     }
        //     else
        //     {
        //         sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x,
        //                                                 sceneData.MiniMap.OrthSize,
        //                                                 sceneData.MiniMap.Position.z);
        //     }

        //     if (sceneData.MiniMap.OrthSize < 1)
        //     {
        //         sceneData.MiniMap.OrthSize = 1;
        //         // DevLogWarning("Mighty Dev Ops: Mighty Map: Orthographic size is too small. Setting to 1.");
        //     }
        //     sceneCamera.orthographicSize = sceneData.MiniMap.OrthSize;
        //     sceneCamera.transform.position = sceneData.MiniMap.Position;
        //     sceneCamera.transform.rotation = sceneData.MiniMap.Rotation;

        //     sceneData.MiniMap.PixelHeight = sceneCamera.pixelHeight;
        //     sceneData.MiniMap.PixelWidth = sceneCamera.pixelWidth;

        //     sceneData.MiniMap.Topleft = sceneCamera.ScreenToWorldPoint(new Vector3(0, sceneCamera.pixelHeight, sceneCamera.nearClipPlane));
        //     sceneData.MiniMap.Topright = sceneCamera.ScreenToWorldPoint(new Vector3(sceneCamera.pixelWidth, sceneCamera.pixelHeight, sceneCamera.nearClipPlane));
        //     sceneData.MiniMap.Botleft = sceneCamera.ScreenToWorldPoint(new Vector3(0, 0, sceneCamera.nearClipPlane));
        //     sceneData.MiniMap.Botright = sceneCamera.ScreenToWorldPoint(new Vector3(sceneCamera.pixelWidth, 0, sceneCamera.nearClipPlane));

        //     // DevLog($"sceneData.MiniMap.Topleft: {sceneData.MiniMap.Topleft} sceneData.MiniMap.Topright: {sceneData.MiniMap.Topright} sceneData.MiniMap.Botleft: {sceneData.MiniMap.Botleft} sceneData.MiniMap.Botright: {sceneData.MiniMap.Botright}");
        //     if (screenWidth != 0 || screenHeight != 0)
        //     {
        //         RenderTexture currentRT = new RenderTexture((int)screenWidth, (int)screenHeight, 24);
        //         sceneCamera.targetTexture = currentRT;
        //         sceneCamera.Render();

        //         RenderTexture.active = currentRT;

        //         sceneData.MiniMap.map = new Texture2D((int)screenWidth, (int)screenHeight, TextureFormat.RGB24, false);
        //         sceneData.MiniMap.map.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);

        //         sceneData.MiniMap.map.Apply();

        //         RenderTexture.active = currentRT;

        //         sceneData.MiniMap.map.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
        //         sceneData.MiniMap.map.Apply();

        //         sceneCamera.targetTexture = null;
        //         RenderTexture.active = null;

        //     }
        //     if (sceneData.MiniMap.map == null) sceneData.MiniMap.map = MakeTex((int)screenWidth, (int)screenHeight, Color.blue);

        //     map.style.backgroundImage = sceneData.MiniMap.map;

        //     if (isSceneAnchored)
        //     {
        //         if (firstRun)
        //         {
        //             sceneData.MiniMap.SaveImage();
        //             firstRun = false;
        //         }
        //         else if (sceneData.MiniMap.MapPath == "")
        //         {
        //             sceneData.MiniMap.SaveImage();
        //         }
        //     }
        //     MightyCoreData.sceneData.MiniMap.SaveImage();
        // }

        #endregion

        #region GUI

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            DevLog($"OnPlayModeStateChanged: {state}");
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    if (cameraTopDown == null)
                        cameraTopDown = Resources.Load("MightyMapCam") as GameObject;
                    cameraTopDown?.SetActive(false);
                    Rebuild();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    // Disable MiniMap during play mode
                    if (Map != null)
                    {
                        Map.style.display = DisplayStyle.None;
                    }
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    if (cameraTopDown == null)
                        cameraTopDown = Resources.Load("MightyMapCam") as GameObject;
                    cameraTopDown?.SetActive(true);
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    // Re-enable and rebuild MiniMap when returning to edit mode
                    if (Map != null)
                    {
                        Map.style.display = DisplayStyle.Flex;
                        Rebuild();
                        UpdateMiniMap();
                    }
                    break;
            }
        }

        void OnGUI()
        {
            //Ensure the window is still open otherwise return
            if (window == null) return;

            if (EditorApplication.isPlaying)
            {
                return;
            }
            // if(EditorApplication.is)
            if (!enabled) return;

            if (!isInit)
            {
                Init();
            }
            if (sceneData == null) return;

            if (sceneData.IsCollecting)
                sceneData.UpdateDeepDive();

            screenWidth = window.position.width;
            screenHeight = window.position.height;
            var sv = GetSceneView();
            if (sv == null) return;
            Camera camera = Application.isPlaying ? Camera.main : sv.camera;
            if (svCameraOverride != null) camera = svCameraOverride;
            data.svPos = camera.transform.position;
            data.svRot = camera.transform.rotation;
            svOrthSize = GetSVCOrthographicSize();

            if (EditorApplication.isPlaying)
            {
                if (Math.Abs(EditorApplication.timeSinceStartup % data.mapRefreshSeconds - data.mapRefreshSeconds) < 0.01f) UpdateView();
            }

            // Periodic news checking (every 5 minutes by default)
            if (EditorApplication.timeSinceStartup - data.lastNewsCheckTime >= data.newsRefreshSeconds)
            {
                data.lastNewsCheckTime = EditorApplication.timeSinceStartup;
                GetLatestNews(() =>
                {
                    // Update notification icon if new items were found
                    UpdateNotificationIcon();
                });
            }
            // DevLog($"EditorApplication.timeSinceStartup: {EditorApplication.timeSinceStartup} data.mapRefreshSeconds: {data.mapRefreshSeconds} EditorApplication.timeSinceStartup % data.mapRefreshSeconds: {EditorApplication.timeSinceStartup % data.mapRefreshSeconds}");

            var targetRatio = (float)screenWidth / (float)screenHeight;

            var hh = sceneData.MiniMap.Topleft.z - sceneData.MiniMap.Botleft.z;
            var ww = hh * targetRatio;
            float xOffset = ((sceneData.MiniMap.Topright.x - sceneData.MiniMap.Topleft.x) / 2) - (ww / 2);
            float x1 = sceneData.MiniMap.Topleft.x + xOffset;
            float x2 = sceneData.MiniMap.Topright.x - xOffset;
            float z1 = sceneData.MiniMap.Botright.z;
            float z2 = sceneData.MiniMap.Topleft.z;

            sceneCamIcon ??= window.rootVisualElement.Q<Button>(name: "sceneCamIcon");

            if (sceneCamIcon != null)
                if (data.svPos.x >= x1 && data.svPos.x <= x2 &&
                    data.svPos.z >= z1 && data.svPos.z <= z2)
                {
                    showSceneCamIcon = true;
                    var xx = (1 - ((x2 - data.svPos.x) / ww)) * screenWidth;
                    var zz = (1 - ((data.svPos.z - z1) / hh)) * screenHeight;

                    sceneCamIcon.style.display = DisplayStyle.Flex;
                    sceneCamIcon.SendToBack();

                    // if (!isHoveringOnMappable) sceneCamIcon.BringToFront();
                    float prevTop = sceneCamIcon.style.top.value.value;
                    float prevLeft = sceneCamIcon.style.left.value.value;
                    // var prevRotate = sceneCamIcon.style.rotate.value.angle;

                    sceneCamIcon.style.top = zz - 8;
                    sceneCamIcon.style.left = xx - 8;
                    sceneCamIcon.style.rotate = new StyleRotate(new UnityEngine.UIElements.Rotate(new Angle(camera.transform.rotation.eulerAngles.y)));

                    float tolerance = 0.1f; // Adjust the tolerance value as needed
                    bool topEqual = Math.Abs(prevTop - sceneCamIcon.style.top.value.value) < tolerance;
                    bool leftEqual = Math.Abs(prevLeft - sceneCamIcon.style.left.value.value) < tolerance;

                    if (!topEqual || !leftEqual)
                    {
                        // DevLog($"prevTop: {prevTop} / sceneCamIcon.style.top.value.value: {sceneCamIcon.style.top.value.value} prevTop==sceneCamIcon.style.top.value.value: {prevTop == sceneCamIcon.style.top.value.value}");
                        // DevLog($"prevLeft: {prevLeft} / sceneCamIcon.style.left.value.value: {sceneCamIcon.style.left.value.value} prevLeft==sceneCamIcon.style.left.value.value: {prevLeft == sceneCamIcon.style.left.value.value}");

                        Dirty = true;
                        sceneCamIcon.MarkDirtyRepaint();
                    }
                }
                else
                {
                    sceneCamIcon.style.display = DisplayStyle.None;

                }

            if (IsDirty(true)) UpdateView();
        }

        static bool enabled = true;

        static void InitDropDownRunIds()
        {
            DevLog("InitDropDownRunIds");
            if (EditorApplication.isPlaying) return;

            DropdownField dropDown = root.Q<DropdownField>("dd_run_ids");
            var runIds = sceneData.PlayTrackingList.Select(x => x.name).ToList();
            dropDown.choices = runIds;
            if (runIds.Count == 0)
            {
                sceneData.SelectedRun = "No Playthroughs Recorded";
            }
            dropDown.value = sceneData.SelectedRun;
            PlayTracking playthrough = sceneData.PlayTrackingList.FirstOrDefault(x => x.name == sceneData.SelectedRun);

            RunPlaybackMinMaxUpdated?.Invoke();

            // sceneData.RunPlaybackSelectedMin = sceneData.RunPlaybackSelectedMax = 0;

            if (playthrough != null && sceneData != null)
            {
                playthrough.SelectPlaythrough();
                // sceneData.RunPlaybackMin = playthrough.startTicks;
                // sceneData.RunPlaybackMax = playthrough.endTicks;
            }

            dropDown.RegisterValueChangedCallback((evt) =>
            {
                sceneData.SelectedRun = evt.newValue;
                dropDown.value = sceneData.SelectedRun;
                DevLog(sceneData.SelectedRun);

                PlayTracking playthrough = sceneData.PlayTrackingList.FirstOrDefault(x => x.name == sceneData.SelectedRun) as PlayTracking;

                if (playthrough == null) return;

                Dirty = true;

                sceneData.RunPlaybackMin = playthrough.startTicks;
                sceneData.RunPlaybackMax = playthrough.endTicks;

                sceneData.RunPlaybackSelectedMin = playthrough.startTicks;
                sceneData.RunPlaybackSelectedMax = playthrough.endTicks;

                ClearMarkers?.Invoke();

                RunPlaybackChanged?.Invoke();
                RunPlaybackMinMaxUpdated?.Invoke();

                BuildMappables();
                UpdateMiniMap();

                UpdateMarkers?.Invoke();
                RefreshSceneView?.Invoke();
                BuildLanes();
            });
        }

        static void CreateGUI()
        {
            //check if window named "MightyMap" is open
            if (!EditorWindow.HasOpenInstances<MightyMap>()) return;

            // if (map != null) return;

            // Debug.Log("CreateGUI");
            if (mappables == null)
            {
                MightyCore.Init();
            }

            //if (root != null) root.Clear();
            root = GetWindow().rootVisualElement;
            root.Clear();
            root.style.display = DisplayStyle.Flex;

            if (mightyStylesheet == null)
                mightyStylesheet = Resources.Load<StyleSheet>("UI/mightystyles");

            if (!root.styleSheets.Contains(mightyStylesheet))
            {
                root.styleSheets.Add(mightyStylesheet);
            }

            root.AddToClassList("root");

            #region sceneAnchorCheck
            // if (!isSceneAnchored)
            // {
            //     DevLog($"isSceneAnchored: {isSceneAnchored} creating button on minimap");
            addSceneAnchor = new()
            {
                name = "addSceneAnchor",
                style = {
                backgroundColor = Color.black,
                width = Length.Percent (100),
                height = Length.Percent (100),
                fontSize = 12,
                display = DisplayStyle.None,
                justifyContent = Justify.Center, // Center horizontally
                alignItems = Align.Center, // Center vertically
                }
            };

            if (icons.mightybot == null) icons = new();
            VisualElement mightyBot = new()
            {
                name = "mightyBot",
                style = {
                backgroundImage = icons.mightybot,
                width = 64,
                height = 64,
                flexGrow = 0,
                flexShrink = 0,
                }
            };
            addSceneAnchor.Add(mightyBot);
            Label addSceneAnchorLabel = new()
            {
                name = "addSceneAnchorLabel",
                text = "Enable Mighty DevOps on this Scene?",
                style = {
                color = Color.white,
                // width= Length.Percent(100),
                // height= Length.Percent(100),
                flexGrow = 0,
                flexShrink = 0,
                unityTextAlign = TextAnchor.MiddleCenter,
                }
            };

            addSceneAnchor.Add(addSceneAnchorLabel);
            addSceneAnchor.RegisterCallback<MouseDownEvent>((evt) =>
            {
                DevLog("addSceneAnchor Path: " + SceneManager.GetActiveScene().path);
                if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) // If the scene is not saved yet, save it first
                {
                    EditorUtility.DisplayDialog("Save Scene", "This scene isn't saved yet, please save first then try again!", "Ok");
                    return;
                }

                if (EditorUtility.DisplayDialog("Create Scene Anchor", "Mighty Dev Ops needs to anchor to this scene.  This creates a small Editor Only reference object within your scene.  Would you like to anchor now?", "Yes", "No"))
                {

                    var go = GameObject.Find("MightySceneAnchor");
                    var sceneIndex = 0;
                    if (go != null)
                    {
                        var _sa = go.GetComponent<MightySceneAnchor>();
                        if (_sa == null)
                        {
                            go.AddComponent<MightySceneAnchor>();
                            _sa = go.GetComponent<MightySceneAnchor>();
                            _sa.DataSetName = $"{SceneManager.GetActiveScene().name}___{SceneManager.GetActiveScene().path.Replace("/", "_").Replace(".unity", "")}";
                        }

                        bool hasEntry = data.scenes.Any(scene => scene.Name == _sa.DataSetName);
                        if (!hasEntry)
                            data.scenes.Add(new SceneData() { Name = _sa.DataSetName });

                        isSceneAnchored = true;
                        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                        return;

                    }
                    go ??= new GameObject("MightySceneAnchor")
                    {
                        tag = "EditorOnly",
                    };

                    //go.hideFlags = HideFlags.HideInHierarchy;

                    EditorUtility.SetDirty(go);
                    go.AddComponent<MightySceneAnchor>();
                    var sa = go.GetComponent<MightySceneAnchor>();
                    sa.DataSetName = $"{SceneManager.GetActiveScene().name}___{SceneManager.GetActiveScene().path.Replace("/", "_").Replace(".unity", "")}";
                    data.scenes.Add(new SceneData());
                    sceneIndex = MightyCore.data.scenes.Count - 1;
                    data.scenes[sceneIndex].Name = sa.DataSetName;
                    //UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                    isSceneAnchored = true;
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                }
            });
            // root.Add(addSceneAnchor);




            if (isSceneAnchored)
            {
                addSceneAnchor.style.display = DisplayStyle.None;
            }
            else
            {
                root.style.display = DisplayStyle.Flex;
                addSceneAnchor.style.display = DisplayStyle.Flex;
                root.Add(addSceneAnchor);
                MightyMap.GUILoaded = true;
                DevLog("Mighty Dev Ops: Mighty Map: map is " + (Map == null ? "null" : "not null"));
                return;
            }
            #endregion

            DevLog("Mighty Dev Ops: Mighty Map: map is " + (Map == null ? "null" : "not null"));
            if (Map == null)
            {
                DevLog("Mighty Dev Ops: Mighty Map: map is null, creating new map");
                Map = new()
                {
                    name = "map",
                    style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                position = Position.Relative,
                alignItems = Align.FlexStart,
                flexGrow = 1,
                flexShrink = 1,
                }
                };

            }

            //if root doesn't contain visualelement map add it
            if (!root.Contains(Map))
            {
                DevLog("Mighty Dev Ops: Mighty Map: map is not in root, adding it");
                root.Add(Map);
                Map.SendToBack();
            }


            VisualElement container = new()
            {
                name = "container",
                pickingMode = PickingMode.Ignore,
                style = {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.FlexStart,
                position = Position.Absolute,
                alignItems = Align.FlexStart,
                flexGrow = 1,
                flexShrink = 1,
                height = Length.Percent (100),
                width = Length.Percent (100),
                }
            };

            top = new()
            {
                name = "top",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                height = 32,
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = new Color (0.1f, 0.1f, 0.1f, 0.1f),
                width = Length.Percent (100)
                }
            };

            container.Add(top);

            mid = new()
            {
                name = "middle",
                pickingMode = PickingMode.Ignore,
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                flexGrow = 1,
                flexShrink = 1,
                width = Length.Percent (100),
                height = window.position.height - 128,
                }
            };

            Button addNewMappable = new()
            {
                name = "addNewMappable",
                text = "+",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                position = Position.Absolute,
                height = 64,
                width = 64,
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = Color.white,
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 64,
                bottom = 16,
                right = 16,
                }
            };
            addNewMappable.style.borderTopLeftRadius = addNewMappable.style.borderTopRightRadius = addNewMappable.style.borderBottomLeftRadius = addNewMappable.style.borderBottomRightRadius = 360;
            //mid.Add(addNewMappable);
            addNewMappable.style.color = Color.black;

            container.Add(mid);

            //mid.Add(sceneData.GetProgressBar());

            bot = new()
            {
                name = "bottom",
                style = {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                minHeight = 48,
                width = Length.Percent (100),
                // flexGrow = 1,
                // flexShrink = 0,
                backgroundColor = new Color (0.1f, 0.1f, 0.1f, 0.9f),
                marginBottom = 4,
                marginTop = 4,
                paddingBottom = 4,
                paddingTop = 4,
                paddingLeft = 4,
                paddingRight = 4,
                }
            };

            container.Add(bot);

            windowBar = new()
            {
                name = "WindowBar",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                height = 32,
                width = Length.Percent (100),
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = new Color (0.1f, 0.1f, 0.1f, 0.9f)
                }
            };
            //container.Add(windowBar);

            root.Add(container);

            var mappablesCopy = new List<IMappable>(mappables);
            foreach (var mappable in mappablesCopy)
            {
                mappable.RegisterMappable();
            }

            var button = new Button
            {
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.Center,
                height = 30,
                // marginTop = 10,
                // marginBottom = 10,
                // marginLeft = 20,
                // marginRight = 20,
                paddingLeft = 10,
                paddingRight = 10,
                paddingTop = 5,
                paddingBottom = 5,
                borderLeftWidth = 2,
                borderRightWidth = 2,
                borderTopWidth = 2,
                borderBottomWidth = 2,
                borderLeftColor = Color.gray,
                borderRightColor = Color.gray,
                borderTopColor = Color.gray,
                borderBottomColor = Color.gray,
                backgroundColor = Color.black
                }
            };

            var icon = new Image
            {
                image = icons.mightyeye,
                style = {
                width = 24,
                height = 24,
                marginRight = 5
                }
            };
            button.Add(icon);

            var label = new Label
            {
                text = "Scene Browser",
                style = {
                fontSize = 14,
                unityFontStyleAndWeight = FontStyle.Bold,
                }
            };

            button.Add(label);
            button.clicked += () =>
            {
                ICommand command = new OpenSceneGraphWindowCommand();
                command.Execute();
            };

            top.Add(button);

            notifications = new()
            {
                name = "notifications",
                text = "",
                style = {
                width = 32,
                height = 32,
                backgroundImage = icons.notificationOffIcon,
                paddingLeft = 5,
                paddingRight = 5,
                paddingTop = 5,
                paddingBottom = 5,
                borderLeftWidth = 2,
                borderRightWidth = 2,
                borderTopWidth = 2,
                borderBottomWidth = 2,
                borderLeftColor = Color.gray,
                borderRightColor = Color.gray,
                borderTopColor = Color.gray,
                borderBottomColor = Color.gray,
                backgroundColor = Color.black
                }
            };
            top.Add(notifications);

            notifications.clicked += () =>
            {
                if (dataCore?.newsItems != null)
                {
                    dataCore.newsItems.Clear();
                }

                ICommand command = new OpenNotificationsWindowCommand();
                command.Execute();

                var notificationsWindow = MightyNotifications.Load();
                notificationsWindow.ShowLoadingState();

                void OnNewsFetchComplete()
                {
                    var existingNotifications = MightyNotifications.Load();
                    existingNotifications.BuildView();
                    UpdateNotificationIcon();
                }

                GetLatestNews(OnNewsFetchComplete);
            };

            button = new Button()
            {
                name = "followSceneView",
                tooltip = "Follow Scene View",
                style = {
                width = 48,
                height = 48,
                top = 48,
                right = 16,
                backgroundColor = new Color (0, 0, 0, 0),
                borderTopWidth = 0,
                borderRightWidth = 0,
                borderBottomWidth = 0,
                borderLeftWidth = 0,
                position = Position.Absolute,
                }
            };

            if (icons.map_follow_sceneview_on == null) icons = new();
            if (followSceneView)
            {
                button.style.backgroundImage = icons.map_follow_sceneview_on;
            }
            else
            {
                button.style.backgroundImage = icons.map_follow_sceneview_off;
            }
            // button.style.backgroundImage = icons.map_follow_sceneview_on;

            button.clicked += () =>
            {
                if (followSceneView)
                {
                    button.style.backgroundImage = icons.map_follow_sceneview_off;
                    followSceneView = false;
                }
                else
                {
                    button.style.backgroundImage = icons.map_follow_sceneview_on;
                    followSceneView = true;
                }
                ShowToast($"{(followSceneView ? "Following Sceneview" : "Explorer Mode")}");
            };
            top.Add(button);

            toastBox = new()
            {
                name = "toastBox",
                pickingMode = PickingMode.Ignore,
                style = {
                flexDirection = FlexDirection.ColumnReverse,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                position = Position.Absolute,

                bottom = 16,
                right = 16,
                flexGrow = 0,
                flexShrink = 0,
                width = 256,
                height = 256,
                // backgroundColor = new Color(1f, 0.1f, 0.1f, 0.9f),
                }
            };
            mid.Add(toastBox);

            float toggleIconWidth = 96 + 15;

            #region sideMenu
            sideMenu = new()
            {
                name = "sideMenu",
                style = {
                display = DisplayStyle.Flex,
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                overflow = Overflow.Hidden,
                height = Length.Percent (100),
                width = toggleIconWidth - 14, // Match the button width exactly
                left = -(toggleIconWidth - 14), // Adjust positioning to match new width
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = new Color (0f, 0.102f, 0.247f, 1f), // #001a3fff
                transitionProperty = new List<StylePropertyName> {
                new StylePropertyName ("left"),
                new StylePropertyName ("width")
                },
                transitionDuration = new List<TimeValue> () {
                new TimeValue (transitionSpeed, TimeUnit.Millisecond)
                }
                }
            };

            VisualElement sideMenuContentContainer = new()
            {
                name = "sideMenuContentContainer",
                style = {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                overflow = Overflow.Hidden,
                height = 1,
                width = 196,
                backgroundColor = new Color (0.5f, 0.5f, 1.0f, 0.9f),
                flexGrow = 1,
                flexShrink = 0,
                }
            };

            VisualElement sideMenuTitleBar = new()
            {
                name = "sideMenuTitleBar",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                height = 0,
                width = 196,
                top = -48,
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = new Color (0.1f, 0.1f, 0.1f, 0.9f),
                }
            };

            VisualElement sideMenuTitleIcon = new()
            {
                name = "sideMenuTitleIcon",
                style = {
                width = 48,
                height = 48,
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = new Color (1f, 0.1f, 0.1f, 0.9f),
                }
            };
            sideMenuTitleBar.Add(sideMenuTitleIcon);

            sideMenuTitleIcon.RegisterCallback<MouseDownEvent>((evt) =>
            {
                CloseModuleSubMenu?.Invoke();
            });

            Label sideMenuTitle = new()
            {
                text = "Modules",
                style = {
                fontSize = 24,
                color = Color.white,
                height = Length.Percent (100),
                flexGrow = 1,
                unityTextAlign = TextAnchor.MiddleCenter,
                transitionProperty = new List<StylePropertyName> {
                new StylePropertyName ("top"),
                new StylePropertyName ("height"),
                },
                transitionDuration = new List<TimeValue> () {
                new TimeValue (transitionSpeed, TimeUnit.Millisecond)
                }
                }
            };
            sideMenuTitleBar.Add(sideMenuTitle);

            sideMenuContentContainer.Add(sideMenuTitleBar);

            ScrollView sideMenuContent = new()
            {
                name = "sideMenuContent",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                style = {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                overflow = Overflow.Hidden,
                width = 0,
                flexGrow = 1,
                flexShrink = 1,
                backgroundColor = new StyleColor (new Color (0.1f, 0.1f, 0.1f, 0.9f)),
                transitionProperty = new List<StylePropertyName> {
                new StylePropertyName ("width")
                },
                transitionDuration = new List<TimeValue> () {
                new TimeValue (transitionSpeed, TimeUnit.Millisecond)
                }
                }
            };

            sideMenuContentContainer.Add(sideMenuContent);

            sideMenu.Add(sideMenuContentContainer);

            mid.Add(sideMenu);

            VisualElement sideMenuButton = new()
            {
                name = "sideMenuButton",
                style = {
                height = Length.Percent (100),
                width = 32,
                left = -(toggleIconWidth - 14), // Match the new sideMenu position
                backgroundColor = new Color (0.1f, 0.1f, 0.1f, 0.9f),
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                }
            };

            label = new()
            {
                text = "≡",
                style = {
                fontSize = 32,
                color = Color.white,
                }
            };
            sideMenuButton.Add(label);

            sideMenuButton.RegisterCallback<MouseDownEvent>((evt) =>
            {
                if (sideMenu.resolvedStyle.left == 0)
                {
                    sideMenu.style.left = -(toggleIconWidth - 14);
                    sideMenuButton.style.left = -(toggleIconWidth - 14);
                    CloseModuleSubMenu?.Invoke();
                }
                else
                {
                    sideMenu.style.left = 0;
                    sideMenuButton.style.left = 0;
                }
            });

            sideMenuButton.style.transitionProperty = new List<StylePropertyName> {
                new StylePropertyName ("left")
            };

            sideMenuButton.style.transitionDuration = new List<TimeValue>() {
                new TimeValue (transitionSpeed, TimeUnit.Millisecond)
            };

            mid.Add(sideMenuButton);

            ScrollView sideMenuScrollView = new()
            {
                name = "sideMenuScrollView",
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                style = {
                height = Length.Percent (100),
                width = toggleIconWidth - 14, // Match the button width exactly
                }
            };

            DevLog($"data.mapShapes is Null?? {data?.MappableTypesInfo == null}");
            int count = 0;
            if (data?.MappableTypesInfo != null)
            {
                foreach (var typeInfo in data.MappableTypesInfo)
                {
                    if (typeInfo.Mappable == null)
                    {
                        DevLogError($"TypeInfo Mappable is null for {typeInfo.Name}");
                        continue;
                    }


                    DevLog($"typeInfo.IsActive: {typeInfo.IsActive}");
                    DevLog($"toggleInfo.Name: {typeInfo.Name}");

                    DevLog($"typeInfo.name = {typeInfo.Name}");
                    DevLog($"typeInfo.Mappable is null? {typeInfo.Mappable == null}");

                    DevLog($"checking if mappable name = {typeInfo.Name} has OnGenerateVisualContent");
                    if (typeInfo.Mappable.HasVisualContent & Map != null)
                    {
                        DevLog($"mappable name = {typeInfo.Name} has OnGenerateVisualContent");
                        Map.generateVisualContent -= typeInfo.Mappable.OnGenerateVisualContent;
                        Map.generateVisualContent += typeInfo.Mappable.OnGenerateVisualContent;
                    }

                    CustomToggleButton toggleModule = typeInfo.Mappable.AddModuleToggle(typeInfo);
                    toggleModule.style.width = toggleIconWidth - 14;
                    toggleModule.style.height = toggleIconWidth - 14;
                    // toggleModule.style.position = Position.Absolute;
                    // toggleModule.style.top = toggleModule.topCache = count * (toggleIconWidth - 15);
                    toggleModule.isToggledOn = typeInfo.IsActive;

                    string localName = typeInfo.Name;
                    var localTypeInfo = typeInfo; // Capture the current iteration
                    toggleModule.RegisterValueChangedCallback((ChangeEvent<bool> evt) =>
                    {
                        var infoToUpdate = data.MappableTypesInfo.Find(info => info.Name == localName);
                        if (infoToUpdate != null)
                        {
                            string s = evt.newValue ? "Enabled" : "Disabled";
                            ShowToast($"{localName} {s}"); // Use localName consistently
                            infoToUpdate.IsActive = evt.newValue;
                            Dirty = true;
                            Rebuild();
                            DevLog($"Mappable Updated {infoToUpdate.TypeName} to {infoToUpdate.IsActive}");
                        }
                        else
                        {
                            DevLog($"mappable infoToUpdate is Null?? {infoToUpdate == null}");
                        }
                        DevLog($"Mappable Updated {localName} to {evt.newValue}");

                        if (isSceneAnchored)
                        {
                            BuildMappables();
                            UpdateMarkers();
                        }
                        UpdateMiniMap();
                    });

                    sideMenuScrollView.Add(toggleModule);
                    count++;
                    // data.mapShapes[i].active = GUILayout.Toggle(data.mapShapes[i].active,
                    //                                                 data.mapShapes[i].ToString());
                }
            }

            VisualElement addonImage = new()
            {
                name = "addonImage",
                style = {
                backgroundImage = icons.previewTracking,
                width = toggleIconWidth - 14,
                height = toggleIconWidth - 14,
                // position = Position.Absolute,
                // top = count * (toggleIconWidth - 15),
                flexGrow = 0,
                flexShrink = 0,
                backgroundColor = new Color (0.1f, 0.1f, 0.1f, 0.9f),
                }
            };

            count = 0;
#if MIGHTY_TRACKING
#else
            count++;
#endif
#if MIGHTY_LEAP
#else
            count++;
#endif
#if MIGHTY_FPS_HEATMAP
#else
            count++;
#endif
            // count = 1;
            if (count > 0)
            {
                sideMenuScrollView.Add(Spacer());
                sideMenuScrollView.Add(Header("More Modules:", 12));
            }

#if MIGHTY_FPS_HEATMAP
#else
            CustomToggleButton previewFPSHeatmap = new CustomToggleButton(icons.previewHeatmaps, null, "", null, "FPS Heatmap", "https://assetstore.unity.com/packages/slug/319014?aid=1011lf9gY&pubref=mdo");
            previewFPSHeatmap.style.width = toggleIconWidth - 14;
            previewFPSHeatmap.style.height = toggleIconWidth - 14;
            previewFPSHeatmap.isToggledOn = true;

            sideMenuScrollView.Add(previewFPSHeatmap);
#endif

#if MIGHTY_TRACKING
#else
            CustomToggleButton previewTracking = new CustomToggleButton(icons.previewTracking, null, "", null, "Tracking", "https://assetstore.unity.com/packages/slug/318759?aid=1011lf9gY&pubref=mdo");
            previewTracking.style.width = toggleIconWidth - 14;
            previewTracking.style.height = toggleIconWidth - 14;
            previewTracking.isToggledOn = true;

            sideMenuScrollView.Add(previewTracking);
#endif

#if MIGHTY_PORTAL
#else
            CustomToggleButton previewLeap = new CustomToggleButton(icons.previewLeap, null, "", null, "Portal", "https://assetstore.unity.com/packages/tools/level-design/leap-177244?aid=1011lf9gY&pubref=mdo");
            previewLeap.style.width = toggleIconWidth - 14;
            previewLeap.style.height = toggleIconWidth - 14;
            previewLeap.isToggledOn = true;

            sideMenuScrollView.Add(previewLeap);
#endif

            sideMenu.Add(sideMenuScrollView);

            OpenModuleSubMenu += () =>
            {
                sideMenu.style.width = 196;
                sideMenuTitleBar.style.top = 0;
                sideMenuTitleBar.style.height = 48;
                sideMenuContent.style.width = 196;
                sideMenuScrollView.style.height = 0;
                sideMenuScrollView.style.width = 0;
                sideMenuTitle.text = selectedModule.mappableTypeInfo.Name;
                sideMenuTitleIcon.style.backgroundImage = selectedModule.mappableTypeInfo.Mappable.Icon;

                // Reset the content container width to full expanded width
                sideMenuContentContainer.style.width = 196;

                sideMenuContent.Clear();
                sideMenuContent.Add(selectedModule.mappableTypeInfo.Mappable.SettingsView());
            };

            CloseModuleSubMenu += () =>
            {
                sideMenu.style.width = toggleIconWidth - 14; // Match button width
                sideMenuTitleBar.style.top = -48;
                sideMenuTitleBar.style.height = 0;
                sideMenuContent.style.width = 0;
                sideMenuScrollView.style.height = Length.Percent(100);
                sideMenuScrollView.style.width = toggleIconWidth - 14; // Match button width

                // Clear the content and reset container width to prevent horizontal scrolling
                sideMenuContent.Clear();
                sideMenuContentContainer.style.width = toggleIconWidth - 14; // Match button width

                sideMenuScrollView.MarkDirtyRepaint();
                sideMenuContent.MarkDirtyRepaint();
                sideMenu.MarkDirtyRepaint();
            };
            #endregion

            #region playthroughs
            if (isSceneAnchored)
                BuildLanes();

            // Example of adding timestamps
            // Replace with your actual data and icons

            // timestampedSlider.AddTimestamp(startPlaythrough + TimeSpan.TicksPerSecond * 10, 0, icon, "Awesome Screenshot", myAction);
            // timestampedSlider.AddTimestamp(startPlaythrough + TimeSpan.TicksPerSecond * 40, 1, new Label("Z"), "Awesome Screenshot");
            // RebuildLanes.Invoke();

            bot.Add(playthroughContainer);
            bot.Add(playthroughContainerExtra);
            #endregion

            #region searchMap
            ToolbarPopupSearchField searchField = new ToolbarPopupSearchField();

            searchField.menu.AppendAction("Name Search", (a) =>
            {
                currentSearchType = SearchType.Name;
                DevLog("Changed to Name Search.");
            }, (a) => currentSearchType == SearchType.Name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            searchField.menu.AppendAction("Deep Search", (a) =>
            {
                currentSearchType = SearchType.Deep;
                DevLog("Changed to Deep Search.");
            }, (a) => currentSearchType == SearchType.Deep ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            searchField.menu.AppendSeparator();

            searchField.menu.AppendAction("Case Sensitive", (a) =>
            {
                isCaseSensitive = !isCaseSensitive;
                DevLog($"Case sensitivity is now {(isCaseSensitive ? "enabled" : "disabled")}.");
            }, (a) => isCaseSensitive ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            searchField.RegisterValueChangedCallback((evt) =>
            {
                searchQuery = evt.newValue;
                DevLog($"Search string changed to {searchQuery}.");

                sceneData.PerformSearch(searchQuery);
            });

            PopulateWindowBar();

            quickActions = new()
            {
                name = "quickActions",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexEnd,
                alignItems = Align.FlexEnd,

                height = Length.Percent (100),
                width = Length.Percent (100),
                flexGrow = 1,
                flexShrink = 1,
                // backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                backgroundImage = icons.quickActionsFade,
                }
            };

            top.Add(quickActions);


            int bSize = 24;
            if (data?.MappableTypesInfo != null)
            {
                var mappableTypesCopy = data.MappableTypesInfo.ToList(); // Create a copy to avoid concurrent modification
                for (int i = 0; i < mappableTypesCopy.Count; i++)
                {
                    if (i >= mappableTypesCopy.Count || mappableTypesCopy[i]?.Mappable == null) continue;
                    Button addMappable = mappableTypesCopy[i].Mappable.AddMappable();
                    DevLog($"mappable {i} button is null = {addMappable == null}");
                    if (addMappable == null) continue;
                    addMappable.style.width = bSize;
                    addMappable.style.height = bSize;
                    addMappable.style.position = Position.Relative;
                    addMappable.style.transformOrigin = new StyleTransformOrigin();
                    // addMappable.style.transformOrigin = StyleKeyword.Auto;
                    // x.style.bottom = 24;
                    // x.style.right = 8;
                    addMappable.style.transitionProperty = new List<StylePropertyName> {
                    new StylePropertyName ("scale"),
                };
                    addMappable.style.transitionDuration = new List<TimeValue>() {
                    new TimeValue (transitionSpeed, TimeUnit.Millisecond)
                };
                    addMappable.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseInBounce };
                    addMappable.visible = true;
                    addMappableButtons.Add(addMappable);
                    quickActions.Add(addMappable);

                    addMappable.RegisterCallback<MouseEnterEvent>(e =>
                    {
                        addMappable.style.scale = new Scale(new Vector2(1.2f, 1.2f));
                        // addMappable.style.height = bSize + 4;
                        // x.style.bottom = 32;
                        // x.style.right = 0;
                    });

                    addMappable.RegisterCallback<MouseLeaveEvent>(e =>
                    {
                        addMappable.style.scale = new Scale(new Vector2(1f, 1f));
                        // addMappable.style.width = bSize;
                        // addMappable.style.height = bSize;
                        // x.style.bottom = 24;
                        // x.style.right = 8;
                    });

                    // var slider = new MinMaxSlider();
                    // slider.lowLimit = 0;
                    // slider.highLimit = 100;
                    // slider.minValue = 0;
                    // slider.maxValue = 100;
                    // slider.style.width = 200;

                    // slider.RegisterValueChangedCallback((evt) =>
                    // {
                    //     //run_playbackCursor = evt.newValue;
                    //     sceneData.RunPlaybackRange = evt.newValue;
                    //     DevLog("Slider value changed to " + evt.newValue);
                    //     UpdateMiniMap();
                    //     if (isSceneAnchored)
                    //     {
                    //         UpdateMappables();
                    //         UpdateMarkers();
                    //     }
                    // });
                    //bot.Add(slider);
                }
            }
            #endregion

            Map.RegisterCallback<MouseLeaveEvent>(e =>
            {
                isAdjust = false;
            }, TrickleDown.NoTrickleDown);

            Map.RegisterCallback<MouseEnterEvent>(e =>
            {
                isAdjust = false;
            }, TrickleDown.NoTrickleDown);

            Map.RegisterCallback<WheelEvent>(e =>
            {
                // if (zoomMarker != null)
                // {
                //     root.Remove(zoomMarker);
                // }
                // followSceneView = false;

                Vector3 mouseWorldPosBefore = GetWorldPositionFromMouse(e.mousePosition, sceneData.MiniMap.Position, sceneData.MiniMap.OrthSize, screenWidth, screenHeight);

                if (e.delta.y > 0)
                {
                    sceneData.MiniMap.OrthSize *= 1.05f;
                }
                else
                {
                    sceneData.MiniMap.OrthSize /= 1.05f;
                }

                Vector3 mouseWorldPosAfter = GetWorldPositionFromMouse(e.mousePosition, sceneData.MiniMap.Position, sceneData.MiniMap.OrthSize, screenWidth, screenHeight);



                sceneData.MiniMap.Position += (mouseWorldPosBefore - mouseWorldPosAfter);
                sceneData.MiniMap.CachePos = sceneData.MiniMap.Position;

                DevLog($"New Camera Position: {sceneData.MiniMap.Position}");
                DevLog($"Mouse Position: {e.mousePosition}");
                DevLog($"Old Camera Position: {sceneData.MiniMap.Position}");
                DevLog($"New Camera Position: {sceneData.MiniMap.Position}");
                DevLog($"Orthographic Size: {sceneData.MiniMap.OrthSize}");
                DevLog($"World Position Before: {mouseWorldPosBefore}");
                DevLog($"World Position After: {mouseWorldPosAfter}");
                if (isSceneAnchored)
                {
                    UpdateMappables();
                    UpdateMarkers();
                }
                UpdateMiniMap();


                // zoomMarker = new VisualElement();
                // zoomMarker.style.width = 10;
                // zoomMarker.style.height = 10;
                // zoomMarker.style.backgroundColor = new StyleColor(Color.red);
                // zoomMarker.style.position = Position.Absolute;
                // zoomMarker.style.left = e.mousePosition.x - 5;
                // zoomMarker.style.top = e.mousePosition.y - 5;

                // root.Add(zoomMarker);
            }, TrickleDown.NoTrickleDown);

            Map.RegisterCallback<MouseDownEvent>(e =>
            {
                isAdjust = true;
            }, TrickleDown.NoTrickleDown);

            Map.RegisterCallback<MouseUpEvent>(e =>
            {
                isAdjust = false;
            }, TrickleDown.TrickleDown);

            Map.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (isAdjust)
                {
                    if (GetSceneView().in2DMode)
                    {
                        sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x + -e.mouseDelta.x * (sceneData.MiniMap.OrthSize / 100),
                            sceneData.MiniMap.Position.y + e.mouseDelta.y * (sceneData.MiniMap.OrthSize / 100),
                            sceneData.MiniMap.Position.z);
                    }
                    else
                    {
                        sceneData.MiniMap.Position = new Vector3(sceneData.MiniMap.Position.x + -e.mouseDelta.x * (sceneData.MiniMap.OrthSize / 100),
                            sceneData.MiniMap.Position.y,
                            sceneData.MiniMap.Position.z + e.mouseDelta.y * (sceneData.MiniMap.OrthSize / 100));
                    }

                    followSceneView = false;
                    var button = top.Q<Button>(name: "followSceneView");
                    if (button != null)
                    {
                        button.style.backgroundImage = icons.map_follow_sceneview_off;
                    }

                    Dirty = true;
                    // UpdateMiniMap();
                    // if (isSceneAnchored)
                    // {
                    //     UpdateMappables();
                    //     UpdateMarkers();
                    // }

                }
            }, TrickleDown.NoTrickleDown);
            //mapIconLayer.style.backgroundImage = sceneData.miniMap.map;

            //map.generateVisualContent += OnGenerateVisualContent;

            if (Map.Q<Button>(name: "sceneCamIcon") == null)
            {
                sceneCamIcon = new Button();
                sceneCamIcon.style.position = Position.Absolute;
                sceneCamIcon.style.width = sceneCamIcon.style.height = 16;
                sceneCamIcon.name = "sceneCamIcon";
                if (icons.mmCamera == null) icons = new Icons();
                sceneCamIcon.style.backgroundImage = icons.mmCamera;
                sceneCamIcon.style.backgroundColor =
                    sceneCamIcon.style.borderTopColor =
                    sceneCamIcon.style.borderBottomColor =
                    sceneCamIcon.style.borderLeftColor =
                    sceneCamIcon.style.borderRightColor =
                    new Color(0, 0, 1, 1);

                sceneCamIcon.clicked += () => followSceneView = true;

                Map.Add(sceneCamIcon);
            }

            root.RegisterCallback<MouseMoveEvent>(e =>
            {
                e.StopPropagation();
            });

            Map.RegisterCallback<MouseDownEvent>(evt =>
            {
                // if (evt.button == 1)  // Right mouse button
                // {
                //     DevLog($"Right mouse button clicked at {evt.localMousePosition} / {evt.mousePosition}");

                //     VisualElement radialMenu = CreateRadialMenu(evt.localMousePosition, addMappableButtons);
                //     map.Add(radialMenu);
                //     evt.StopPropagation();
                // }
            });
            BuildMappables();
            if (BuildTopUI != null) BuildTopUI();

            DevLog($"InitDropDownRunIDS isSceneAnchored = {isSceneAnchored}");
            if (isSceneAnchored)
                InitPlaythroughs?.Invoke();

            // Update notification icon on initial load
            UpdateNotificationIcon();

            GUILoaded = true;
            MapInitialized?.Invoke();
        }


        private static void BuildLanes()
        {
            DevLog($"BuildLanes");
            long startPlaythrough;

            playthroughContainer = new()
            {
                name = "playthroughContainer",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                flexGrow = 1,
                flexShrink = 0,
                // transitionProperty = new List<StylePropertyName>
                //     {
                //         new StylePropertyName("height"),
                //         new StylePropertyName("width")
                //     },
                transitionDuration = new List<TimeValue> () {
                new TimeValue (10000, TimeUnit.Millisecond)
                }
                }
            };
            //            StyleEnum<DisplayStyle> displayCache = DisplayStyle.None;

            // var displayCache = playthroughContainerExtra?.style.display;
            // DevLog($"displayCache = {displayCache}");

            if (playthroughContainerExtra == null)
                playthroughContainerExtra = new()
                {
                    name = "playthroughContainerExtra",
                    style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.Center,
                    display = DisplayStyle.None,
                    backgroundColor = Color.black,
                    flexGrow = 1,
                    flexShrink = 0,
                    marginBottom = 4,
                    marginTop = 4,
                    minHeight = 48,

                    }

                };

            EditorToolbarToggle toggle = new()
            {
                name = "toggle_playthrough",
                text = "R",
                tooltip = "Record Playthrough Data if available",
                style = {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.FlexStart,
                alignItems = Align.FlexStart,
                height = 30,
                width = 32,
                // marginTop = 10,
                // marginBottom = 10,
                // marginLeft = 20,
                // marginRight = 20,
                paddingLeft = 10,
                paddingRight = 10,
                paddingTop = 5,
                paddingBottom = 5,
                borderLeftWidth = 2,
                borderRightWidth = 2,
                borderTopWidth = 2,
                borderBottomWidth = 2,
                borderLeftColor = Color.gray,
                borderRightColor = Color.gray,
                borderTopColor = Color.gray,
                borderBottomColor = Color.gray,
                backgroundColor = Color.black,
                //backgroundImage = icons.recorderIcon,
                }
            };
            if (sceneData.RecordPlaythrough)
            {
                toggle.style.backgroundColor = Color.red;
                toggle.style.color = Color.white;
                toggle.value = true;
            }
            else
            {
                toggle.style.backgroundColor = Color.black;
                toggle.style.color = Color.white;
                toggle.value = false;
            }

            toggle.RegisterValueChangedCallback((evt) =>
            {
                if (!sceneData.RecordPlaythrough)
                {
                    sceneData.RecordPlaythrough = true;
                    toggle.style.backgroundColor = Color.red;
                    toggle.style.color = Color.white;
                }
                else
                {
                    sceneData.RecordPlaythrough = false;
                    toggle.style.backgroundColor = Color.black;
                    toggle.style.color = Color.white;
                }

                RecordPlaythroughChanged?.Invoke(sceneData.RecordPlaythrough);
                ShowToast($"Recording Playthroughs {(sceneData.RecordPlaythrough ? "enabled" : "disabled")}");
            });
            playthroughContainer.Add(toggle);

            var dropDown = new DropdownField
            {
                name = "dd_run_ids",
                style = {
                flexShrink = 1,
                flexGrow = 1,
                width = 200,
                height = 30,
                display = DisplayStyle.Flex,
                }
            };

            Button rename = new()
            {
                name = "rename",
                text = "",
                style = {
                width = 30,
                height = 30,
                backgroundColor = Color.black,
                backgroundImage = icons.editPenIcon,
                color = Color.white,
                }
            };

            var renameTextBox = new TextField
            {
                name = "renameTextBox",
                style = {
                flexShrink = 1,
                flexGrow = 1,
                width = 200,
                height = 30,
                display = DisplayStyle.None,
                }
            };

            Button cancel = new()
            {
                name = "cancel",
                text = "✖",
                style = {
                width = 30,
                height = 30,
                backgroundColor = Color.red,
                color = Color.white,
                display = DisplayStyle.None,
                }
            };
            cancel.clicked += () =>
            {
                renameTextBox.style.display = DisplayStyle.None;
                cancel.style.display = DisplayStyle.None;
                dropDown.style.display = DisplayStyle.Flex;
                rename.style.backgroundImage = icons.editPenIcon;
                rename.style.backgroundColor = Color.black;
                rename.text = "";
            };

            playthroughContainer.Add(dropDown);
            playthroughContainer.Add(renameTextBox);
            playthroughContainer.Add(cancel);

            if (EditorApplication.isPlaying)
            {
                dropDown.SetEnabled(false);
                rename.SetEnabled(false);
                toggle.SetEnabled(false);
            }
            else
            {
                dropDown.SetEnabled(true);
                rename.SetEnabled(true);
                toggle.SetEnabled(true);
            }

            rename.clicked += () =>
            {
                if (renameTextBox.style.display == DisplayStyle.None)
                {
                    renameTextBox.style.display = DisplayStyle.Flex;
                    cancel.style.display = DisplayStyle.Flex;

                    dropDown.style.display = DisplayStyle.None;
                    renameTextBox.value = sceneData.SelectedRun;
                    renameTextBox.Focus();
                    rename.style.backgroundImage = null;
                    rename.style.backgroundColor = new Color(0.0f, 0.6f, 0); ;
                    rename.text = "✔";

                }
                else
                {
                    renameTextBox.style.display = DisplayStyle.None;
                    cancel.style.display = DisplayStyle.None;
                    dropDown.style.display = DisplayStyle.Flex;
                    rename.style.backgroundImage = icons.editPenIcon;
                    rename.style.backgroundColor = Color.black;
                    rename.text = "";

                    PlayTracking playthrough = sceneData.PlayTrackingList.FirstOrDefault(x => x.name == sceneData.SelectedRun) as PlayTracking;
                    playthrough.name = renameTextBox.value;
                    sceneData.SelectedRun = renameTextBox.value;
                    InitDropDownRunIds();
                }
            };
            playthroughContainer.Add(rename);

            Button delete = new()
            {
                name = "delete",
                text = "",
                style = {
                width = 30,
                height = 30,
                backgroundColor = Color.black,
                backgroundImage = icons.trashcanIcon,
                color = Color.white,
                }
            };
            playthroughContainer.Add(delete);

            delete.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Delete Playthrough", "Are you sure you want to delete this playthrough? THIS CANNOT BE UNDONE", "Yes", "No"))
                {
                    DeletePlaythroughData?.Invoke();
                    sceneData.PlayTrackingList.RemoveAll(x => x.name == sceneData.SelectedRun);
                    sceneData.SelectedRun = sceneData.PlayTrackingList.Count > 0 ? sceneData.PlayTrackingList[0].name : "Default";
                    InitDropDownRunIds();
                }
            };

            Button openExtra = new()
            {
                name = "openExtra",
                text = "▼",
                style = {
                width = 30,
                height = 30,
                backgroundColor = Color.black,
                color = Color.white,
                }
            };
            // playthroughContainerExtra.style.display = DisplayStyle.None;

            if (showLanes)
                playthroughContainerExtra.style.display = DisplayStyle.Flex;
            else playthroughContainerExtra.style.display = DisplayStyle.None;

            openExtra.clicked += () =>
            {
                var playthroughContainerExtra_ = root.Q<VisualElement>("playthroughContainerExtra");

                if (playthroughContainerExtra_.style.display == DisplayStyle.None)
                {
                    playthroughContainerExtra_.style.display = DisplayStyle.Flex;
                    openExtra.text = "▲";
                    showLanes = true;
                }
                else
                {
                    playthroughContainerExtra_.style.display = DisplayStyle.None;
                    openExtra.text = "▼";
                    showLanes = false;
                }
                BuildLanes();
            };

            if (!EditorApplication.isPlaying)
                playthroughContainer.Add(openExtra);

            Slider slider = new()
            {
                name = "slider",
                style = {
                flexGrow = 1,
                flexShrink = 1,
                }
            };
            slider.RegisterValueChangedCallback((evt) =>
            {
                //run_playbackCursor = evt.newValue;
                sceneData.RunPlaybackCursor = (long)evt.newValue;
                DevLog("Slider value changed to " + evt.newValue);


                // UpdateMiniMap();
                // if (isSceneAnchored)
                // {
                //     UpdateMappables();
                //     UpdateMarkers();
                // }
            });
            // playthroughContainerExtra.Add(slider);


            int numberOfLanes = 1;
            startPlaythrough = DateTime.Now.Ticks;
            long EnteredPlayMode = DateTime.Now.Ticks + TimeSpan.TicksPerSecond * 60;
            PlayTracking playthrough = sceneData.PlayTrackingList.FirstOrDefault(x => x.name == sceneData.SelectedRun) as PlayTracking;

            sceneData.RunPlaybackMin = playthrough?.startTicks ?? 0;
            sceneData.RunPlaybackMax = playthrough?.endTicks ?? 0;
            DevLog($"typeInfo sceneData.SelectedRun = {sceneData.SelectedRun}");
            DevLog($"typeInfo playthough is null? {playthrough == null}");
            DevLog($"typeInfo playthough startTicks: {playthrough?.startTicks}");
            DevLog($"typeInfo playthough endTicks: {playthrough?.endTicks}");
            if (timestampedSlider != null)
            {
                timestampedSlider.UpdateTimeline(playthrough?.startTicks ?? 0, playthrough?.endTicks ?? 0, numberOfLanes);
            }
            else
            {
                timestampedSlider = new TimestampedSlider(playthrough?.startTicks ?? 0, playthrough?.endTicks ?? 0, numberOfLanes);
                playthroughContainerExtra.Add(timestampedSlider);
            }

            timestampedSlider.timeSlider.RegisterValueChangedCallback((evt) =>
            {
                DevLog("AAA Slider value changed to " + evt.newValue + " / " + timestampedSlider.startSelectedTicks + " / " + timestampedSlider.endSelectedTicks + " / " + sceneData.RunPlaybackSelectedMin + " / " + sceneData.RunPlaybackSelectedMax);
                sceneData.RunPlaybackSelectedMin = timestampedSlider.startSelectedTicks;
                sceneData.RunPlaybackSelectedMax = timestampedSlider.endSelectedTicks;

                DevLog("BBB Slider value changed to " + evt.newValue + " / " + timestampedSlider.startSelectedTicks + " / " + timestampedSlider.endSelectedTicks + " / " + sceneData.RunPlaybackSelectedMin + " / " + sceneData.RunPlaybackSelectedMax);

                Dirty = true;
                RunPlaybackMinMaxUpdated?.Invoke();
                MightyCoreData.UpdateMiniMap?.Invoke();
                // UpdateMiniMap();
                // if (isSceneAnchored)
                // {
                //     UpdateMappables();
                //     UpdateMarkers();
                // }
            });

            DevLog($"playthrough is null? {playthrough == null}");

            if (data?.MappableTypesInfo != null)
            {
                DevLog($"typeinfo data.MappableTypesInfo.Count = {data.MappableTypesInfo.Count}");

                int ii = 0;
                var mappableTypesCopy = data.MappableTypesInfo.ToList(); // Create a copy to avoid concurrent modification
                for (int i = 0; i < mappableTypesCopy.Count; i++)
                {
                    if (i >= mappableTypesCopy.Count) break;
                    var typeInfo = mappableTypesCopy[i];
                    DevLog($"typeInfo.name = {typeInfo?.Name}");
                    if (typeInfo?.Mappable != null && typeInfo.Mappable.HasPlayTracking)
                    {
                        typeInfo.Mappable.PopulatePlayTrackingLane(ii);
                        ii++;
                    }
                }
            }
        }

        public static VisualElement CreateRadialMenu(Vector2 clickPosition, List<Button> menuItems)
        {
            var container = new VisualElement
            {
                style = {
                position = Position.Absolute,
                left = clickPosition.x - 16,
                top = clickPosition.y - 16
                }
            };

            var centralCircle = new VisualElement
            {
                style = {
                width = 32,
                height = 32,
                backgroundColor = new Color (0.2f, 0.2f, 0.2f, 1.0f),
                position = Position.Absolute,
                }
            };

            centralCircle.style.borderTopLeftRadius = centralCircle.style.borderTopRightRadius = centralCircle.style.borderBottomLeftRadius = centralCircle.style.borderBottomRightRadius = 16;
            container.Insert(0, centralCircle);

            int numberOfItems = menuItems.Count;
            float angleStep = 360f / numberOfItems;

            for (int i = 0; i < numberOfItems; i++)
            {

                var item = menuItems[i];
                item.style.position = Position.Absolute;
                item.style.left = 0;
                item.style.top = 0;
                item.style.width = 0;
                item.style.height = 0;
                item.style.borderTopLeftRadius = item.style.borderTopRightRadius = item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 32;

                container.Add(item);
            }
            for (int i = 0; i < numberOfItems; i++)
            {
                float angleRad = Mathf.Deg2Rad * (i * angleStep);
                float xPos = Mathf.Cos(angleRad) * 50 - 4;
                float yPos = Mathf.Sin(angleRad) * 50 - 4;

                var item = menuItems[i];
                item.name = menuItems[i].text;
                item.AddToClassList("menu-item");
                item.style.left = xPos;
                item.style.top = yPos;
                item.style.width = 32;
                item.style.height = 32;

                container.Add(item);
            }

            var styleSheet = Resources.Load("RadialMenu") as StyleSheet;
            container.styleSheets.Add(styleSheet);

            return container;
        }

        //static VisualElement zoomMarker;

        static Vector3 GetWorldPositionFromMouse(Vector2 mousePosition, Vector3 camPos, float orthSize, float screenWidth, float screenHeight)
        {
            bool is2DMode = GetSceneView().in2DMode;
            float x1 = camPos.x - orthSize;
            float x2 = camPos.x + orthSize;
            float z1 = is2DMode ? (camPos.y - orthSize) : (camPos.z - orthSize);
            float z2 = is2DMode ? (camPos.y + orthSize) : (camPos.z + orthSize);
            float ww = x2 - x1;
            float hh = z2 - z1;

            float xx = x1 + ((mousePosition.x) / screenWidth * ww);
            float zz = z1 + ((screenHeight - mousePosition.y) / screenHeight * hh);

            DevLog($"x1: {x1} / x2: {x2} / z1: {z1} / z2: {z2} / ww: {ww} / hh: {hh} / xx: {xx} / zz: {zz} / camPos: {camPos} / mousePosition: {mousePosition} / orthSize: {orthSize} / screenWidth: {screenWidth} / screenHeight: {screenHeight} / is2DMode: {is2DMode}");
            if (is2DMode)
            {
                return new Vector3(xx, zz, camPos.z);
            }
            else
            {
                return new Vector3(xx, camPos.y, zz);
            }
        }


        private static void PopulateWindowBar()
        {
            // windowBar.Clear();
            // windowBar.Add(windowManagerStateful.PopulateWindowBar());
        }

        public static void BuildMappables()
        {
            DevLog($"Building Mappables == true? {buildMappables == true}");
            if (buildMappables == true) return;
            buildMappables = true;

            if (mappables == null)
            {
                MightyCore.Init();
            }

            if (Map != null && mapIconLayer != null && root.Q<VisualElement>(name: "MapIconLayer") != null && root.Q<VisualElement>(name: "map") != null)
            {
                DevLog($"mapIconLayer.Clear(); ");
                mapIconLayer.Clear();
            }

            if (mapIconLayer == null)
                mapIconLayer = new()
                {
                    name = "MapIconLayer"
                };

            Map ??= new();

            var mappablesToRemove = new List<IMappable>();

            foreach (var mappable in mappables)
            {
                if (mappable == null || mappable.AnchorTo == null || sceneData == null || sceneData.Name == null) continue;

                if (mappable.HasPlayTracking)
                {
                    if (!mappable.ShowAlways || mappable.CreatedAt < sceneData.RunPlaybackMin && mappable.CreatedAt > sceneData.RunPlaybackMax)
                    {
                        DevLog($"mappable {mappable.Name} / {mappable.CreatedAt} is outside of the playback range. Removing it from the map.");
                        mappable.ViewUI.GetView().style.display = DisplayStyle.None;
                        continue;
                    }
                    else
                    {
                        DevLog($"mappable {mappable.Name} / {mappable.CreatedAt} is inside of the playback range.");
                        var x = mappable.ViewUI.GetView();
                        if (x != null)
                            x.style.display = DisplayStyle.Flex;
                    }
                }

                if (mappable.AnchorTo != sceneData.Name)
                {
                    mappablesToRemove.Add(mappable);
                }
                if (mappable.Name == "remove_mappable")
                {
                    mappablesToRemove.Add(mappable);
                }
            }

            foreach (var mappableToRemove in mappablesToRemove)
            {
                mappables.Remove(mappableToRemove);
            }

            var ae = mapIconLayer.Query<VisualElement>("[class^='lm_anchor_']");
            if (ae == null) ae = new();
            var anchorElements = ae.ToList();

            foreach (var element in anchorElements)
            {
                var className = element.GetClasses().FirstOrDefault();
                if (className != null && !className.Equals("lm_anchor_" + sceneData.Name))
                {
                    element.RemoveFromHierarchy();
                }
            }

            int i = 0;
            foreach (var mappable in mappables)
            {
                i++;
                var view = mappable.ViewUI.GetView();
                if (view == null) continue;
                if (view == mappable.PrevView) continue;

                var _mappable = view.Q<VisualElement>(className: "mappable");
                if (_mappable == null)
                {
                    DevLog("_mappable is null");
                    continue;
                }

                DevLog($"xxz mappable.Pic.img == null? {mappable.Pic.img == null} mappable.Name = {mappable.Name} mappable.Pic.img = {mappable.Pic.img} mappable.Pic.img.Path = {mappable.Pic.path}");

                if (mappable.Pic.img == null) mappable.LoadImage();
                mappable.ViewUI.SetRoot(mapIconLayer);
                view.style.position = Position.Absolute;

                // DevLog($"checking if mappable name = {mappable.Name} has OnGenerateVisualContent");
                // if (mappable.HasVisualContent & map != null)
                // {
                //     DevLog($"mappable name = {mappable.Name} has OnGenerateVisualContent");
                //     map.generateVisualContent -= mappable.OnGenerateVisualContent;
                //     map.generateVisualContent += mappable.OnGenerateVisualContent;
                // }
                DevLog($"mappable {i} left: {mappable.MapLocation.left} / top: {mappable.MapLocation.top} ");
                mapIconLayer.Add(view);
            }

            mapIconLayer.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            mapIconLayer.style.position = Position.Absolute;

            Map.Add(mapIconLayer);
            mapIconLayer.BringToFront();
            if (mapMarkerLayer == null)
            {
                mapMarkerLayer = new VisualElement();
                mapMarkerLayer.name = "MapMarkerLayer";
                mapMarkerLayer.style.position = Position.Absolute;
                Map.Add(mapMarkerLayer);
            }

            buildMappables = false;
            UpdateMappables();
        }

        class Cluster
        {
            public VisualElement view;
            public float zoomAtCreation = 0f;

            public Cluster(VisualElement view, float zoomAtCreation)
            {
                this.view = view;
                this.zoomAtCreation = zoomAtCreation;
            }
        }

        public static void UpdateMappables()
        {
            // DevLog($"isSceneAnchored: {isSceneAnchored} GetSceneView() is null? {GetSceneView() == null} updatingMappables is true? {updatingMappables == true}");
            if (!isSceneAnchored) return;
            if (GetSceneView() == null) return;
            if (updatingMappables == true) return;

            // DevLog($"UpdateMappables");
            updatingMappables = true;

            bool is2DMode = GetSceneView().in2DMode;
            if (window != null)
            {
                screenWidth = window.position.width;
                screenHeight = window.position.height;
            }

            var targetRatio = screenWidth / screenHeight;
            hh = is2DMode ? sceneData.MiniMap.Topleft.y - sceneData.MiniMap.Botleft.y : sceneData.MiniMap.Topleft.z - sceneData.MiniMap.Botleft.z;
            ww = hh * targetRatio;

            if (hh == 0 || ww == 0)
            {
                DevLogError($"Invalid values for hh or ww! hh: {hh} / ww: {ww} / targetRatio: {targetRatio} / screenWidth: {screenWidth} / screenHeight: {screenHeight} / is2DMode: {is2DMode} / sceneData.MiniMap is null? {sceneData.MiniMap == null}");
                DevLogError($"sceneData.MiniMap.Topleft: {sceneData.MiniMap.Topleft} / sceneData.MiniMap.Botleft: {sceneData.MiniMap.Botleft} / sceneData.MiniMap.Topleft.y: {sceneData.MiniMap.Topleft.y} / sceneData.MiniMap.Botleft.y: {sceneData.MiniMap.Botleft.y} / sceneData.MiniMap.Topleft.z: {sceneData.MiniMap.Topleft.z} / sceneData.MiniMap.Botleft.z: {sceneData.MiniMap.Botleft.z}");
                return;
            }

            float xOffset = ((sceneData.MiniMap.Topright.x - sceneData.MiniMap.Topleft.x) / 2) - (ww / 2);
            float x1 = sceneData.MiniMap.Topleft.x + xOffset;
            float x2 = sceneData.MiniMap.Topright.x - xOffset;
            float z1 = is2DMode ? sceneData.MiniMap.Botright.y : sceneData.MiniMap.Botright.z;
            float z2 = is2DMode ? sceneData.MiniMap.Topleft.y : sceneData.MiniMap.Topleft.z;

            HashSet<string> filterShow = new HashSet<string>();

            if (data?.MappableTypesInfo != null)
            {
                foreach (MappableTypeInfo typeInfo in data.MappableTypesInfo)
                {
                    if (typeInfo.IsActive)
                    {
                        // DevLog($"Adding {typeInfo.Name} to filterShow");
                        filterShow.Add(typeInfo.Name);
                    }
                }
            }

            foreach (var mappable in mappables)
            {
                var view = mappable.ViewUI.GetView();
                if (view == null)
                {
                    continue;
                }
                mappable.CheckIntegrity();

                // bool withinTimeTracking = false;
                if (mappable.HasPlayTracking)
                {
                    if (mappable.CreatedAt >= sceneData.RunPlaybackSelectedMin & mappable.CreatedAt <= sceneData.RunPlaybackSelectedMax)
                    {
                        // withinTimeTracking = true;
                        view.style.display = DisplayStyle.Flex;
                        // long ticksPerSecond = TimeSpan.TicksPerSecond;
                        // double minTicksPerSecond = (double)sceneData.RunPlaybackSelectedMin / ticksPerSecond;
                        // double maxTicksPerSecond = (double)sceneData.RunPlaybackSelectedMax / ticksPerSecond;

                        // DevLog($"CreatedAt: {mappable.CreatedAt} (Ticks per second: {ticksPerSecond})");
                        // DevLog($"RunPlaybackSelectedMin: {sceneData.RunPlaybackSelectedMin} (Ticks per second: {minTicksPerSecond})");
                        // DevLog($"RunPlaybackSelectedMax: {sceneData.RunPlaybackSelectedMax} (Ticks per second: {maxTicksPerSecond})");
                    }
                    else
                    {
                        // if (!mappable.ShowAlways)
                        view.style.display = DisplayStyle.None;
                        // withinTimeTracking = false;
                        // else
                        //     view.style.display = DisplayStyle.Flex;
                        if (!mappable.ShowAlways) continue;
                    }
                }
                else
                {
                    // withinTimeTracking = true;
                }

                if (!filterShow.Contains(mappable.ToString()))
                {
                    view.style.display = DisplayStyle.None;
                    continue;
                }

                var mp = mappable.MapLocation?.worldPosition + mappable.Offset ?? new Vector3();

                float mpx = mp.x;
                float mpz = is2DMode ? mp.y : mp.z;

                float layoutHeight = view.style.height.value.value;
                float layoutWidth = view.style.width.value.value;

                mappable.Offset = new Vector3(0, 0, 0);

                if (mpx >= x1 - layoutHeight && mpx <= x2 + layoutHeight &&
                    mpz >= z1 - layoutWidth && mpz <= z2 + layoutWidth)
                {
                    float xx = (1 - ((x2 - mpx) / ww)) * screenWidth;
                    float zz = (1 - ((mpz - z1) / hh)) * screenHeight;
                    if (layoutHeight == 0) layoutHeight = mappable.ViewUI.maxHeight;
                    if (layoutWidth == 0) layoutWidth = mappable.ViewUI.maxWidth;

                    view.style.top = zz - (layoutHeight / 2) + mappable.Offset.y;
                    view.style.left = xx - (layoutWidth / 2) + mappable.Offset.x;
                    view.style.display = DisplayStyle.Flex;

                    if (mappable.Name == "Bob")
                    {
                        int abc = 0;
                        abc++;
                    }
                }
                else
                {
                    view.style.display = DisplayStyle.None;
                }

            }

            updatingMappables = false;
        }

        /// <summary>
        ///old clustering version
        /// </summary>
        /// <param name="targetMappable"></param>
        /// <param name="zoomFactor"></param>
        /// <returns></returns>
        //private static List<Cluster> clusterList = new();
        //public static void UpdateMappables()
        //{
        //    DevLog($"isSceneAnchored: {isSceneAnchored} GetSceneView() is null? {GetSceneView() == null} updatingMappables is true? {updatingMappables == true}");
        //    if (!isSceneAnchored) return;
        //    if (GetSceneView() == null) return;
        //    if (updatingMappables == true) return;

        //    DevLog($"UpdateMappables");
        //    updatingMappables = true;

        //    bool is2DMode = GetSceneView().in2DMode;
        //    if (window != null)
        //    {
        //        screenWidth = window.position.width;
        //        screenHeight = window.position.height;
        //    }

        //    var targetRatio = screenWidth / screenHeight;
        //    hh = is2DMode ? sceneData.MiniMap.Topleft.y - sceneData.MiniMap.Botleft.y : sceneData.MiniMap.Topleft.z - sceneData.MiniMap.Botleft.z;
        //    ww = hh * targetRatio;

        //    if (hh == 0 || ww == 0)
        //    {
        //        DevLogError($"Invalid values for hh or ww! hh: {hh} / ww: {ww} / targetRatio: {targetRatio} / screenWidth: {screenWidth} / screenHeight: {screenHeight} / is2DMode: {is2DMode} / sceneData.MiniMap is null? {sceneData.MiniMap == null}");
        //        DevLogError($"sceneData.MiniMap.Topleft: {sceneData.MiniMap.Topleft} / sceneData.MiniMap.Botleft: {sceneData.MiniMap.Botleft} / sceneData.MiniMap.Topleft.y: {sceneData.MiniMap.Topleft.y} / sceneData.MiniMap.Botleft.y: {sceneData.MiniMap.Botleft.y} / sceneData.MiniMap.Topleft.z: {sceneData.MiniMap.Topleft.z} / sceneData.MiniMap.Botleft.z: {sceneData.MiniMap.Botleft.z}");
        //        return;
        //    }

        //    float xOffset = ((sceneData.MiniMap.Topright.x - sceneData.MiniMap.Topleft.x) / 2) - (ww / 2);
        //    float x1 = sceneData.MiniMap.Topleft.x + xOffset;
        //    float x2 = sceneData.MiniMap.Topright.x - xOffset;
        //    float z1 = is2DMode ? sceneData.MiniMap.Botright.y : sceneData.MiniMap.Botright.z;
        //    float z2 = is2DMode ? sceneData.MiniMap.Topleft.y : sceneData.MiniMap.Topleft.z;

        //    DevLog($"x1: {x1} / x2: {x2} / z1: {z1} / z2: {z2} / ww: {ww} / hh: {hh} / xOffset: {xOffset} / targetRatio: {targetRatio} / screenWidth: {screenWidth} / screenHeight: {screenHeight} / is2DMode: {is2DMode}");

        //    HashSet<string> filterShow = new HashSet<string>();

        //    // DevLog($"mappable data.MapppableTypesInfo: {data.MappableTypesInfo.Count}");
        //    foreach (MappableTypeInfo typeInfo in data.MappableTypesInfo)
        //    {
        //        // DevLog($"mappable typeInfo: {typeInfo.Name} / {typeInfo.IsActive}");
        //        if (typeInfo.IsActive)
        //        {
        //            filterShow.Add(typeInfo.Name);
        //            // DevLog($"mappable typeInfo isactive");
        //        }
        //    }

        //    // DevLog($"mappable filterShow: {filterShow.Count}");
        //    // foreach (var item in filterShow)
        //    // {
        //    //     DevLog($"mappable filterShow: {item}");
        //    // }

        //    //pass one
        //    HashSet<IMappable> passOne = new();
        //    HashSet<Vector2> occupiedCoordinates = new();

        //    foreach (var mappable in mappables)
        //    {
        //        var view = mappable.ViewUI.GetView();
        //        if (view == null) continue;

        //        if (mappable.HasPlayTracking)
        //        {
        //            DevLog($"mappable {mappable.Name} / {new DateTime(mappable.CreatedAt)}  Checking the playback range. Adding it to the map. mappable.CreatedAt: {new DateTime(mappable.CreatedAt)} / sceneData.RunPlaybackMin: {new DateTime(sceneData.RunPlaybackMin)} / sceneData.RunPlaybackMax: {new DateTime(sceneData.RunPlaybackMax)} | mappable.CreatedAt >= sceneData.RunPlaybackMin: {mappable.CreatedAt >= sceneData.RunPlaybackMin} / mappable.CreatedAt <= sceneData.RunPlaybackMax: {mappable.CreatedAt <= sceneData.RunPlaybackMax}");
        //            if (mappable.CreatedAt >= sceneData.RunPlaybackSelectedMin & mappable.CreatedAt <= sceneData.RunPlaybackSelectedMax)
        //            {
        //                DevLog($"mappable {mappable.Name} is in playback range");
        //                view.style.display = DisplayStyle.Flex;
        //            }
        //            else
        //            {
        //                DevLog($"mappable {mappable.Name} is not in playback range");
        //                view.style.display = DisplayStyle.None;
        //                continue;
        //            }
        //        }

        //        if (!filterShow.Contains(mappable.ToString()))
        //        {
        //            view.style.display = DisplayStyle.None;
        //            DevLog($"mappable {mappable} has display set to none.  FilterShow does not contain {mappable}.");
        //            continue;
        //        }

        //        var mp = mappable.MapLocation?.worldPosition + mappable.Offset ?? new Vector3();

        //        float mpx = mp.x;
        //        float mpz = is2DMode ? mp.y : mp.z;

        //        var _mappable = view.Q<VisualElement>(className: "mappable");
        //        if (_mappable == null) continue;

        //        float layoutHeight = view.style.height.value.value;
        //        float layoutWidth = view.style.width.value.value;

        //        mappable.Offset = new Vector3(0, 0, 0);

        //        if (mpx >= x1 - layoutHeight && mpx <= x2 + layoutHeight
        //                && mpz >= z1 - layoutWidth && mpz <= z2 + layoutWidth)
        //        {
        //            float xx = (1 - ((x2 - mpx) / ww)) * screenWidth;
        //            float zz = (1 - ((mpz - z1) / hh)) * screenHeight;
        //            if (layoutHeight == 0) layoutHeight = mappable.ViewUI.maxHeight;
        //            if (layoutWidth == 0) layoutWidth = mappable.ViewUI.maxWidth;

        //            Vector2 jitteredCoordinate = new Vector2(xx + mappable.Offset.x, zz + mappable.Offset.y);
        //            int jitterAttempts = 10; float jitterRange = 0.5f;
        //            float jitteredX = UnityEngine.Random.Range(-jitterRange, jitterRange);
        //            float jitteredY = UnityEngine.Random.Range(-jitterRange, jitterRange);

        //            if (occupiedCoordinates.Contains(jitteredCoordinate))
        //            {
        //                DevLog($"occupiedCoordinates.Contains(jitteredCoordinate) = {occupiedCoordinates.Contains(jitteredCoordinate)}");
        //                while (occupiedCoordinates.Contains(jitteredCoordinate))
        //                {
        //                    float jitterAmount = jitterRange * jitterAttempts;
        //                    jitteredCoordinate += new Vector2(UnityEngine.Random.Range(-jitterAmount, jitterAmount),
        //                                                      UnityEngine.Random.Range(-jitterAmount, jitterAmount));
        //                    DevLog($"jitteredCoordinate = {jitteredCoordinate} / jitteredX = {jitteredX} / jitteredY = {jitteredY} / jitteredCoordinate = {jitteredCoordinate} / jitterAttempts = {jitterAttempts} / jitterRange = {jitterRange} / occupiedCoordinates = {occupiedCoordinates.Count}");
        //                    jitterAttempts++;
        //                }
        //                occupiedCoordinates.Add(jitteredCoordinate);
        //                if (mappable.Offset.x == 0 && mappable.Offset.y == 0)
        //                {
        //                    mappable.Offset = new Vector3(jitteredCoordinate.x, jitteredCoordinate.y, 0);
        //                    DevLog($"mappable.Offset = {mappable.Offset} / jitteredCoordinate = {jitteredCoordinate} / jitteredX = {jitteredX} / jitteredY = {jitteredY} / jitteredCoordinate = {jitteredCoordinate} / jitterAttempts = {jitterAttempts} / jitterRange = {jitterRange} / occupiedCoordinates = {occupiedCoordinates.Count}");
        //                }
        //            }

        //            // view.style.top = mappable.MapLocation.top + mappable.Offset.y - layoutHeight / 2;
        //            // view.style.left = mappable.MapLocation.left + mappable.Offset.x - layoutWidth / 2;

        //            view.style.top = mappable.MapLocation.top = zz + mappable.Offset.y - (layoutHeight / 2);
        //            view.style.left = mappable.MapLocation.left = xx + mappable.Offset.x - (layoutWidth / 2);

        //            DevLog($"mappable.name = {mappable.Name} / mappable.MapLocation.top = {mappable.MapLocation.top} / mappable.Offset.y = {mappable.Offset.y} / layoutHeight = {layoutHeight} / mappable.MapLocation.left = {mappable.MapLocation.left} / mappable.Offset.x = {mappable.Offset.x} / layoutWidth = {layoutWidth} / jitteredCoordinate = {jitteredCoordinate} / jitteredX = {jitteredX} / jitteredY = {jitteredY} / jitteredCoordinate = {jitteredCoordinate} / jitterAttempts = {jitterAttempts} / jitterRange = {jitterRange} / occupiedCoordinates = {occupiedCoordinates.Count}");

        //            mappable.MapLocation.rect = new Rect(
        //                mappable.MapLocation.left,
        //                mappable.MapLocation.top,
        //                layoutWidth,
        //                layoutHeight);
        //            DevLog($"drawmappable mappabletype: {mappable} / {mappable.MapLocation.rect} / {mappable.MapLocation.top} / {mappable.MapLocation.left} / {mappable.MapLocation.rect.width} / {mappable.MapLocation.rect.height} / {mappable.MapLocation.rect.x} / {mappable.MapLocation.rect.y} ");
        //            view.style.display = DisplayStyle.Flex;
        //            passOne.Add(mappable);
        //        }
        //        else
        //        {
        //            view.style.display = DisplayStyle.None;
        //        }
        //    }

        //    //pass two
        //    Dictionary<IMappable, List<IMappable>> overlapMap = new Dictionary<IMappable, List<IMappable>>();
        //    List<HashSet<IMappable>> clusters = new List<HashSet<IMappable>>();

        //    int abc = 0;
        //    DevLog($"abc = {abc++}");

        //    foreach (var mappable in passOne)
        //    {
        //        var currentView = mappable.ViewUI.GetView();
        //        if (currentView == null) continue;

        //        // DevLog($"{mappable} / {mappable.location.rect}");
        //        foreach (var otherMappable in passOne)
        //        {
        //            if (mappable == otherMappable) continue;

        //            if (SignificantOverlap(mappable.MapLocation.rect, otherMappable.MapLocation.rect, 0.5f))
        //            {
        //                // DevLog($"{mappable.pic.filename} overlaps {otherMappable.pic.filename}");
        //                currentView.style.borderTopColor = currentView.style.borderBottomColor = currentView.style.borderLeftColor = currentView.style.borderRightColor = new Color(1, 0, 0, 1);
        //                currentView.style.borderTopWidth = currentView.style.borderBottomWidth = currentView.style.borderLeftWidth = currentView.style.borderRightWidth = 2;

        //                if (!overlapMap.ContainsKey(mappable))
        //                {
        //                    overlapMap[mappable] = new List<IMappable>();
        //                }
        //                overlapMap[mappable].Add(otherMappable);
        //                currentView.style.display = DisplayStyle.None;
        //            }
        //            else
        //            {
        //                currentView.style.borderTopColor = currentView.style.borderBottomColor = currentView.style.borderLeftColor = currentView.style.borderRightColor = new Color(0, 0, 0, 0);
        //                currentView.style.borderTopWidth = currentView.style.borderBottomWidth = currentView.style.borderLeftWidth = currentView.style.borderRightWidth = 0;
        //            }
        //        }
        //    }

        //    //pass three
        //    foreach (var cluster in clusterList) cluster.view.RemoveFromHierarchy();

        //    // Create or update clusters
        //    foreach (var keyValuePair in overlapMap)
        //    {
        //        var mappable = keyValuePair.Key;
        //        var overlappingMappables = keyValuePair.Value;

        //        HashSet<IMappable> foundCluster = null;
        //        List<HashSet<IMappable>> mergeClusters = new List<HashSet<IMappable>>();

        //        // Find clusters that should merge
        //        foreach (var cluster in clusters)
        //        {
        //            if (cluster.Contains(mappable) || overlappingMappables.Any(x => cluster.Contains(x)))
        //            {
        //                mergeClusters.Add(cluster);
        //            }
        //        }

        //        // Merge clusters or create a new one
        //        if (mergeClusters.Count > 0)
        //        {
        //            foundCluster = new HashSet<IMappable>(mergeClusters.SelectMany(x => x));
        //            // Remove the old clusters that we're merging
        //            foreach (var oldCluster in mergeClusters)
        //            {
        //                clusters.Remove(oldCluster);
        //            }
        //        }
        //        else
        //        {
        //            foundCluster = new HashSet<IMappable>();
        //        }

        //        // Add the mappables to the cluster
        //        foundCluster.Add(mappable);
        //        foreach (var overlappingMappable in overlappingMappables)
        //        {
        //            foundCluster.Add(overlappingMappable);
        //        }

        //        // Add the updated or new cluster back to the list
        //        clusters.Add(foundCluster);
        //    }

        //    // DevLog("--------------------");
        //    foreach (var cluster in clusters)
        //    {
        //        // DevLog($"{cluster.Count} / {cluster}");
        //        if (cluster.Count < 2) continue;  // Skip single-item "clusters"

        //        float minX = float.MaxValue, minY = float.MaxValue;
        //        float maxX = float.MinValue, maxY = float.MinValue;

        //        // Calculate the bounding box for the cluster
        //        foreach (var mappable in cluster)
        //        {
        //            float left = mappable.MapLocation.left;
        //            float top = mappable.MapLocation.top;
        //            float right = left + mappable.MapLocation.rect.width;
        //            float bottom = top + mappable.MapLocation.rect.height;

        //            if (left < minX) minX = left;
        //            if (top < minY) minY = top;
        //            if (right > maxX) maxX = right;
        //            if (bottom > maxY) maxY = bottom;
        //        }

        //        // Create a VisualElement to represent the cluster
        //        VisualElement clusterElement = new VisualElement();
        //        clusterElement.style.width = maxX - minX;  // Set the width to the cluster's width
        //        clusterElement.style.height = maxY - minY; // Set the height to the cluster's height
        //        clusterElement.style.backgroundColor = new Color(1, 1, 1, 0.1f); // Set to desired color

        //        clusterElement.style.borderTopLeftRadius = clusterElement.style.borderTopRightRadius = clusterElement.style.borderBottomLeftRadius = clusterElement.style.borderBottomRightRadius = 90;

        //        // Position the element
        //        clusterElement.style.left = minX;
        //        clusterElement.style.top = minY;
        //        clusterElement.style.position = Position.Absolute;

        //        string id = DateTime.Now.Ticks.ToString();
        //        clusterElement.name = id;
        //        clusterElement.AddToClassList("mighty-cluster");

        //        // clusterElement.RegisterCallback<MouseOverEvent>(e =>
        //        // {
        //        //     DevLog($"MouseOverEvent: {id}");
        //        //     clusterElement.style.backgroundColor = new Color(1, 1, 1, 0.5f);
        //        //     clusterElement.BringToFront();
        //        // }, TrickleDown.NoTrickleDown);

        //        // Add it to the UI hierarchy
        //        clusterList.Add(new Cluster(clusterElement, sceneData.MiniMap.OrthSize));
        //        mapIconLayer.Add(clusterElement);

        //        foreach (var mappable in cluster)
        //        {
        //            VisualElement mark = new();
        //            float w = mappable.MapLocation.rect.width / 3;
        //            float h = mappable.MapLocation.rect.height / 3;
        //            mark.style.left = mappable.MapLocation.left + (mappable.MapLocation.rect.width / 2) - (w / 2);
        //            mark.style.top = mappable.MapLocation.top + (mappable.MapLocation.rect.height / 2) - (h / 2);
        //            mark.style.width = w;
        //            mark.style.height = h;
        //            mark.style.backgroundColor = new Color(1, 0, 0, 0.5f);
        //            mark.style.backgroundImage = mappable.Pic.img;
        //            mark.style.borderTopLeftRadius = mark.style.borderTopRightRadius = mark.style.borderBottomLeftRadius = mark.style.borderBottomRightRadius = 45;
        //            mark.style.borderBottomColor = mark.style.borderTopColor = mark.style.borderLeftColor = mark.style.borderRightColor = StringToColor(mappable.ToString());
        //            mark.style.borderLeftWidth = mark.style.borderRightWidth = mark.style.borderTopWidth = mark.style.borderBottomWidth = 5;

        //            mark.style.position = Position.Absolute;

        //            mark.RegisterCallback<MouseDownEvent>(e =>
        //            {
        //                DevLog("Mark Clicked");
        //                zoomCount = 0;
        //                StartZoomUntilFreedFromCluster(mappable);
        //            }, TrickleDown.NoTrickleDown);

        //            mapIconLayer.Add(mark);
        //            clusterList.Add(new Cluster(mark, sceneData.MiniMap.OrthSize));
        //        }
        //    }

        //    updatingMappables = false;
        //}

        static private IEnumerator ZoomUntilFree(IMappable targetMappable, float zoomFactor)
        {
            int i = 0;

            sceneData.MiniMap.Position = targetMappable.MapLocation.worldPosition;
            int ii = 0;
            bool t = true;
            while (t)
            {
                if (zoomCount++ > 30) t = false;
                i++;

                UpdateMappables();
                // UpdateView();

                // Check if the target is still in a cluster
                if (!IsMappableInAnyCluster(targetMappable))
                {
                    if (ii == 0) ii = i + 10;
                    if (i == ii)
                        break;
                }

                float newOrthoSize = sceneData.MiniMap.OrthSize * zoomFactor;
                sceneData.MiniMap.OrthSize = newOrthoSize;
                sceneData.MiniMap.Position = targetMappable.MapLocation.worldPosition;

                yield return null;
            }
            mapIconLayer.style.display = DisplayStyle.Flex;
        }

        static private bool IsMappableInAnyCluster(IMappable targetMappable)
        {
            var x = mapIconLayer.Q<VisualElement>(name: targetMappable.Pic.filename);
            if (x == null)
            {
                DevLog($"IsMappableInAnyCluster: {targetMappable.Pic.filename} is null");
                return false;
            }

            if (x.style.display == DisplayStyle.None)
            {
                return true;
            }

            return false;
        }

        static int zoomCount = 0;
        static private void StartZoomUntilFreedFromCluster(IMappable targetMappable)
        {
            float zoomFactor = 0.95f;
            EditorCoroutineUtility.StartCoroutineOwnerless(ZoomUntilFree(targetMappable, zoomFactor));
        }

        private static void GeometryChangedCallback(GeometryChangedEvent evt)
        {
            window.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(GeometryChangedCallback);
            DevLog("GeometryChangedCallback");
        }


        public static void RebuildView()
        {
            if (!EditorWindow.HasOpenInstances<MightyMap>()) return;


            if (rebuildingView == true) return;

            if (EditorApplication.isPlaying) return;


            // Debug.Log($"isPlaying: {EditorApplication.isPlaying}");

            DevLog("Rebuilding view");
            rebuildingView = true;

            CreateGUI();
            if (!isSceneAnchored) return;

            if (!sceneLoaded) DevLogWarning("Scene not loaded");
            if (sceneData == null) MightyCore.GetSceneData();
            // if (sceneData == null) return;
            //CreateGUI();
            UpdateMiniMap();

            if (isSceneAnchored)
            {
                BuildMappables();
                UpdateMappables();
                UpdateMarkers();
                // 
            }
            rebuildingView = false;
        }

        public static void UpdateView()
        {
            //DevLog("Rebuilding view");
            //CreateGUI();
            //BuildMappables();
            UpdateMiniMap();
            if (isSceneAnchored)
            {
                UpdateMappables();
                UpdateMarkers();
            }

            Dirty = false;
        }

        private static void DrawClusters()
        {

        }

        private static void CalculateClusters()
        {

        }

        private static void CalculateMappables()
        {

        }
        #endregion
    }

}
#endif