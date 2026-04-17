#if UNITY_EDITOR    
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static Mighty.MightyHeatmap;

namespace Mighty
{
    [DefaultExecutionOrder(-1000)]
    public class MightyCoreData : ScriptableObject
    {
        public static string Version = "1.7.0";
        private static bool DevLogs = false;

        public static void DevLog(object message, long ticks = 10)
        {
            //if (ticks != 0 || (DateTime.Now.Ticks > ticks && DevTicks > ticks))
            if (DevLogs)
            {
                if (message is IConvertible convertible)
                {
                    Debug.Log(convertible.ToString());

                }
                else
                {
                    Debug.Log(message);
                }
            }
        }


        public static void DevLogWarning(object message, long ticks = 0)
        {
            // if (ticks == 0 || DateTime.Now.Ticks > ticks)

            if (DevLogs) Debug.LogWarning(message);
        }

        public static void DevLogError(object message, long ticks = 0)
        {
            // if (ticks == 0 || DateTime.Now.Ticks > ticks)

            if (DevLogs) Debug.LogError(message);
        }

        [SerializeField]
        static public void Save()
        {
            string path = $"{corePath}/Core/Data/MightyCoreEditor.asset";
            if (File.Exists(path))
            {
                DevLog($"{path} already exists...");
                return;
            }

            DevLog($"{path} does not exist, creating...");

            MightyCoreData asset = ScriptableObject.CreateInstance<MightyCoreData>();

            asset.scenes = new List<SceneData>
            {
                new SceneData { Name = "Project" }
            };

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }


        static public MightyCoreData Load()
        {
            string path = $"{corePath}/Core/Data/MightyCoreEditor.asset";
            // string fallbackPath = $"{corePath}/Core/Data/MightyCoreData_.asset";

            if (!File.Exists(path))
            {
                // if (File.Exists(fallbackPath))
                // {
                //     DevLog($"Renaming {fallbackPath} to {path}");
                //     File.Copy(fallbackPath, path);
                //     AssetDatabase.Refresh();
                // }
                // else
                // {
                Save();
                // }
            }
            DevLog($"Loading Core Data at {path}");

            windowManagerStateful = AssetDatabase.LoadAssetAtPath<MightyWindowManagerStateful>($"{corePath}/Core/Data/MightyWindowManagerStateful.asset");

            if (dataCore == null)
            {
                dataCore = AssetDatabase.LoadAssetAtPath<MightyCoreData>(path);
            }

            return dataCore;
        }


        private static bool _dirty = true;
        public static bool Dirty
        {
            get
            {
                // DevLog($"Get Dirty as {_dirty}");
                return _dirty;
            }
            set
            {
                _dirty = value;
                // Debug.Log($"Dirty set to {value}");
            }
        }

        public static bool IsDirty(bool verbose = false)
        {
            if (Dirty)
            {
                if (verbose) DevLog($"dirty: {Dirty}");
                // Dirty = false;
                return true;
            }

            // DevLog($"sceneData.MiniMap.Position.x = {sceneData.MiniMap.Position.x} != sceneData.MiniMap.CachePos.x = {sceneData.MiniMap.CachePos.x} = {sceneData.MiniMap.Position.x != sceneData.MiniMap.CachePos.x} sceneData.MiniMap.Position.z = {sceneData.MiniMap.Position.z} != sceneData.MiniMap.CachePos.z = {sceneData.MiniMap.CachePos.z} = {sceneData.MiniMap.Position.z != sceneData.MiniMap.CachePos.z}");

            if (sceneData == null)
            {
                DevLog($"sceneData is null");
                return true;
            }
            if (sceneData.MiniMap == null)
            {
                DevLog($"sceneData.MiniMap is null");
                return true;
            }

            if (sceneData.MiniMap.Position.x != sceneData.MiniMap.CachePos.x && sceneData.MiniMap.Position.z != sceneData.MiniMap.CachePos.z)
            {
                if (verbose) DevLog($"sceneData.miniMap.pos == sceneData.miniMap.cachePos: {sceneData.MiniMap.Position} == {sceneData.MiniMap.CachePos} = {sceneData.MiniMap.Position == sceneData.MiniMap.CachePos}");
                return true;
            }
            if (screenWidth != sceneData.MiniMap.WidthCache)
            {
                if (verbose) DevLog($"{screenWidth} != {sceneData.MiniMap.WidthCache}");
                return true;
            }
            if (screenHeight != sceneData.MiniMap.HeightCache)
            {
                if (verbose) DevLog($"{screenHeight} != {sceneData.MiniMap.HeightCache}");
                return true;
            }
            if (sceneData.MiniMap.OrthSize != sceneData.MiniMap.OrthSizeCache)
            {
                if (verbose) DevLog($"{sceneData.MiniMap.OrthSize} != {sceneData.MiniMap.OrthSizeCache}");
                return true;
            }
            if (Dirty != false)
            {
                if (verbose) DevLog($"dirty != false");
                return true;
            }
            if (Map == null)
            {
                if (verbose) DevLog($"map == null");
                return true;
            }

            if (IsSceneViewDirty())
            {
                if (verbose) DevLog($"sceneView is dirty");
                return true;
            }
            return false;
        }


        static public Action Rebuild, RebuildMappables, DeletePlaythroughData, UpdateMappables, UpdateMiniMap, UpdateMarkers, ClearMarkers, InitPlaythroughs, RebuildLanes, UpdateSceneView,
        MapInitialized, RebuildRunIdDropDown, StartModules, EndTrackingCleanup, RefreshSceneView, StartWindow, BuildTopUI, BuildWindowBar, TaskListPopulate, OpenModuleSubMenu, CloseModuleSubMenu, OpenUpdatesWindow, RunPlaybackMinMaxUpdated, RunPlaybackChanged;
        static public Action<bool> RecordPlaythroughChanged;
        [SerializeField]
        static public List<IMappable> mappables = new List<IMappable>();
        public static bool rebuildingView = false, updatingMappables = false, buildMappables = false, initializing = false, isPlaying = false;
        public float mapRefreshSeconds = 5f;
        public float newsRefreshSeconds = 300f; // Check for news every 5 minutes
        public double lastNewsCheckTime = 0; // Track last news check time
        public static TimestampedSlider timestampedSlider;

        [field: SerializeField]
        public List<MappableTypeInfo> MappableTypesInfo;
        static public CustomToggleButton selectedModule;
        static public bool moduleSubMenuActive = false;
        /// <summary>
        /// 
        /// </summary>/
        static public float transitionSpeed = 500;


        // static public MightySceneAnchor sceneAnchor;
        static public string dataSetName;

        static public Camera sceneCamera, mainCamera;
        static public Icons icons;
        public static EditorWindow window;
        public static MightyWindowManagerStateful windowManagerStateful;
        static public SceneData sceneData;
        static public bool isSceneAnchored = false, sceneLoaded = false, modulesStarted = false;
        [SerializeField]
        public int sceneIndex = -1, projectIndex = -1;
        [SerializeField]
        public static bool followSceneView;

        public static HashSet<Heatmap> heatmaps = new HashSet<Heatmap>();

        [SerializeField]
        public static bool showLanes;

        static public float screenWidth, screenHeight, screenPrev,
        targetRatio, hh, ww, xOffset, x1, x2, z1, z2;
        static public VisualElement quickActions;
        static public VisualElement root, ux, mapIconLayer, mapMarkerLayer, top, mid, bot, toastBox, addSceneAnchor, sideMenu, sceneCamIcon, windowBar;

        static private VisualElement map;
        static public VisualElement Map
        {
            get => map;
            set
            {
                //show logs showing the before and after state of the map
                DevLog("a. Map set to null? " + map == null);
                DevLog("b. Map set to null? " + value == null);
                map = value;
            }
        }
        static public bool isPlaythroughExtraOpen = false;
        public enum SearchType
        {
            Name,
            Deep
        }

        public static SearchType currentSearchType = SearchType.Name;
        public static bool isCaseSensitive = false;
        public static string searchQuery = "";

        [Serializable]
        public class NewsItem
        {
            [SerializeField]
            public int id;
            [SerializeField]
            public string title;
            [SerializeField]
            public string content;
            [SerializeField]
            public string date_posted;
            [SerializeField]
            public string url;
            [SerializeField]
            public bool expired;

            [SerializeField]
            public bool isRead;
            [SerializeField]
            public bool archived;
        }

        [SerializeField]
        public List<NewsItem> newsItems;// = new List<NewsItem>();

        [Serializable]
        public class NewsItemsResponse
        {
            [SerializeField]
            public NewsItem[] items;
        }

        [Serializable]
        public class ModuleUpdate
        {
            [SerializeField]
            public int id;
            [SerializeField]
            public string date_posted;
            [SerializeField]
            public string module;
            [SerializeField]
            public string version;

            [SerializeField]
            public string notes;
            [SerializeField]
            public string status;
            [SerializeField]
            public string imgurl;
        }

        [SerializeField]
        public List<ModuleUpdate> moduleUpdates;

        [SerializeField]
        public bool hasUnreadNews = false;

        [Serializable]
        public class ModuleUpdateResponse
        {
            [SerializeField]
            public ModuleUpdate[] items;
        }

        [SerializeField]
        public static bool newNews = false;
        public static Button notifications, ratings, features, bugs;


        public class ProjectDossier
        {
            public static string name;
            public static string genre;
            public static string description;
            public static string plot;
            public static string platforms;

            public class Scenes
            {
                public string name;
                public string description;

                override public string ToString() => $"{name} - {description}";
            }
            public static List<Scenes> scenes;

            static ProjectDossier()
            {
                name = "Space Blasters 3000";
                genre = "3D 3rd Person Shooter";
                description = "Lighthearted 3rd person shooter with a retro feel, platformer elements, puzzle solving, and a humorous story.";
                plot = "Becky is a space cadet who must save the universe from the evil space aliens.  Her spaceship crashed so she must salvage and explore so that she can repair her ship and get back to Earth.  However, the evil space aliens have taken over the planet and are trying to stop her.  She must fight her way through the aliens and their minions to get to the mothership and destroy it.  Once the mothership is destroyed, she can repair her ship and get back to Earth.";
                platforms = "PC, Android, iOS";
                scenes = new List<Scenes>
                {
                    new Scenes { name = "Space Station", description = "Becky's ship has crashed on an abandoned space station.  She must explore the station to find the parts she needs to repair her ship." },
                    new Scenes { name = "Slime Caves", description = "Becky has found the parts she needs to repair her ship, but the evil space aliens have taken over the planet and are trying to stop her.  She must fight her way through the aliens and their minions to get to the mothership and destroy it." },
                    new Scenes { name = "Mothership", description = "Becky has destroyed the mothership and can now repair her ship and get back to Earth." },
                    new Scenes { name = "Earth", description = "Becky has returned to Earth and is hailed as a hero." }
                };
            }


        }
        //
        // public static ProjectDossier projectDossier;

        public long run_id = 0;
        public string run_selected = "";
        public List<string> run_ids = new List<string>();
        public long run_playbackCursor = 0, run_playbackCount = 0, run_playbackMax = 0, run_playbackMin = 0;
        public Vector2 run_playbackRange = new Vector2(0, 0);

        public void SetDataSetName()
        {
            GameObject go = GameObject.Find("MightySceneAnchor");
            if (go != null)
            {
                var sa = go.GetComponent<MightySceneAnchor>();
                if (sa != null)
                {
                    dataSetName = sa.DataSetName;
                    isSceneAnchored = true;
                }
                else
                {
                    isSceneAnchored = false;
                }
            }
            else
            {
                isSceneAnchored = false;
            }
        }

        public void CheckSceneData()
        {
            DevLog("CheckSceneData");

            if (dataSetName == null || dataSetName == "")
            {
                SetDataSetName();
            }

            bool sceneFound = false;
            if (isSceneAnchored)
                foreach (var scene in scenes)
                {
                    DevLog($"Scene: {scene.Name}");

                    if (scene.Name == dataSetName)
                    {
                        sceneData = scene;
                        sceneFound = true;
                        DevLog($"Scene Data: {sceneData.Name}");
                    }
                }
            if (!sceneFound && isSceneAnchored)
            {
                DevLog($"Scene not found...");
                scenes.Add(new SceneData { Name = dataSetName });
            }
        }

        [SerializeField]
        public List<SceneData> scenes;

        public int GetSceneIndex(String sceneName)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].Name == sceneName)
                {
                    return i;
                }
            }
            return -1;
        }

        public void SceneDupeCheck()
        {
            List<string> sceneNames = new List<string>();
            for (int i = 0; i < scenes.Count; i++)
            {
                if (sceneNames.Contains(scenes[i].Name))
                {
                    DevLog($"Scene {i} is a duplicate");
                    scenes.RemoveAt(i);
                    i--;
                    continue;
                }
                sceneNames.Add(scenes[i].Name);
            }

        }

        //
        [Serializable]
        public class SceneData
        {
            [SerializeField] private string name;
            [SerializeField] private MiniMapData miniMap;
            [SerializeField] private string scenePath;
            [SerializeField] private string runID;
            [SerializeField] private string selectedRun;
            //            [SerializeField] private List<string> runIDList;

            [SerializeField] private List<PlayTracking> playTrackingList;
            [SerializeField] private long runPlaybackCursor;
            [SerializeField] private long runPlaybackCount;
            [SerializeField] private long runPlaybackMax;
            [SerializeField] private long runPlaybackMin;
            [SerializeField] private long runPlaybackSelectedMax;
            [SerializeField] private long runPlaybackSelectedMin;
            [SerializeField] private Vector2 runPlaybackRange;
            [SerializeField] private bool deleteMe = false;
            [SerializeField] public bool recordPlaythrough = false;


            // Properties with backing fields
            public string Name
            {
                get => name;
                set
                {
                    if (name != value)
                    {
                        DevLog($"Scene name changed from {name} to {value}");
                        name = value;
                    }
                }
            }
            public MiniMapData MiniMap { get => miniMap; set => miniMap = value; }
            public string ScenePath { get => scenePath; set => scenePath = value; }
            public string RunID { get => runID; set => runID = value; }
            public string SelectedRun
            {
                get => selectedRun;
                set
                {
                    if (selectedRun != value)
                    {
                        selectedRun = value;
                        sceneData.runPlaybackMax = sceneData.RunPlaybackSelectedMax = runPlaybackMax;
                        sceneData.runPlaybackMin = sceneData.RunPlaybackSelectedMin = runPlaybackMin;
                        DevLog($"SelectedRun changed to {selectedRun} / runPlaybackMax: {runPlaybackMax} and runPlaybackMin: {runPlaybackMin} and runPlaybackSelectedMax: {RunPlaybackSelectedMax} and runPlaybackSelectedMin: {RunPlaybackSelectedMin}");
                    }
                }
            }
            public bool RecordPlaythrough { get => recordPlaythrough; set => recordPlaythrough = value; }
            public long RunPlaybackCursor { get => runPlaybackCursor; set => runPlaybackCursor = value; }
            public long RunPlaybackCount { get => runPlaybackCount; set => runPlaybackCount = value; }
            public long RunPlaybackMax { get => runPlaybackMax; set => runPlaybackMax = value; }
            public long RunPlaybackMin { get => runPlaybackMin; set => runPlaybackMin = value; }
            public long RunPlaybackSelectedMax { get => runPlaybackSelectedMax; set => runPlaybackSelectedMax = value; }
            public long RunPlaybackSelectedMin { get => runPlaybackSelectedMin; set { runPlaybackSelectedMin = value; DevLog($"RunPlaybackSelectedMin = {value}"); } }
            public Vector2 RunPlaybackRange { get => runPlaybackRange; set => runPlaybackRange = value; }
            public bool DeleteMe { get => deleteMe; set => deleteMe = value; }

            private VisualElement indexContainer;

            [SerializeField]
            public List<PlayTracking> PlayTrackingList { get => playTrackingList; set => playTrackingList = value; }
            private bool playTrackingDirty = false;

            public bool PlayTrackingDirty
            {
                get => playTrackingDirty;
                set
                {
                    playTrackingDirty = value;
                    string stackTrace = System.Environment.StackTrace;
                    DevLog($"PlayTrackingDirty set to {value} at:\n{stackTrace}");
                }
            }
            //private ProgressBar progressBar;
            //public List<Trackable.Root> trackables;
            public SceneData()
            {
                DevLog("SceneData constructor");
                Name = "Default";
                MiniMap = new MiniMapData();
                //RunIDList = new List<string>();
                PlayTrackingList = new List<PlayTracking>
                {
                    new PlayTracking()
                };

            }

            [Serializable]
            public class PlayTracking
            {
                [SerializeField]
                public string name;


                [SerializeField]
                public string run_id;

                [SerializeField]
                public long startTicks, endTicks, totalTicks;

                [SerializeField]
                public Color color;

                public PlayTracking(Color c)
                {
                    DevLog($"PlayTracking constructor {sceneData.RunID}");
                    //sceneData.RunIDList.Add(sceneData.RunID.ToString());
                    run_id = sceneData.RunID;
                    name = $"({sceneData.RunID})";
                    startTicks = DateTime.Now.Ticks;
                    color = c;
                    //DevLog($"PlayTracking constructor RunIDList count: {sceneData.RunIDList.Count}");
                }

                public PlayTracking()
                {
                    name = "Default";
                }

                public void EndTracking()
                {
                    endTicks = DateTime.Now.Ticks;
                    totalTicks = endTicks - startTicks;
                    int seconds = (int)(totalTicks / TimeSpan.TicksPerSecond);
                    DateTime startTime = new DateTime(startTicks);
                    var n = startTime.ToString("@@ yyyy-MM-dd ## HH:mm:ss");
                    n = n.Replace("@@", "Date:");
                    n = n.Replace("##", "Time:");

                    if (totalTicks < TimeSpan.TicksPerMinute)
                    {
                        // Convert to seconds
                        name = $"{n} | ({seconds}s)";
                    }
                    else
                    {
                        // Convert to minutes
                        int minutes = (int)(totalTicks / TimeSpan.TicksPerMinute);
                        int remainingSeconds = (int)((totalTicks % TimeSpan.TicksPerMinute) / TimeSpan.TicksPerSecond);
                        name = $"{name} | ({minutes}:{remainingSeconds:D2}m)";
                    }

                    EndTrackingCleanup?.Invoke();
                }

                public void SelectPlaythrough()
                {
                    sceneData.runPlaybackMin = sceneData.RunPlaybackSelectedMin = startTicks;
                    sceneData.runPlaybackMax = sceneData.RunPlaybackSelectedMax = endTicks;
                    sceneData.SelectedRun = name;
                }
            }
            public VisualElement GetProgressBar()
            {
                indexContainer = new()
                {
                    name = "indexContainer",
                    pickingMode = PickingMode.Ignore,
                    style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                position = Position.Absolute,
                flexGrow = 1,
                flexShrink = 0,
                width = Length.Percent(100),
                height = Length.Percent(100),
            }
                };

                //     progressBar = new()
                //     {
                //         name = "progressBar",
                //         title = "",

                //         pickingMode = PickingMode.Ignore,
                //         style =
                // {

                //     width = 200,
                //     height = 64,

                // }
                //     };
                //indexContainer.Add(progressBar);
                indexContainer.style.display = DisplayStyle.None;

                return indexContainer;
            }

            [Serializable]
            public class PropertyData
            {
                [SerializeField]
                public string Key;
                [SerializeField]
                public string Type; // Renamed from Value to Type
                [SerializeField]
                public string Value; // New field for actual value
            }


            [Serializable]
            public class ComponentData
            {
                [SerializeField]
                public string TypeName;
                //public GameObject ParentGameObject; // New field
                [SerializeField]
                public List<PropertyData> Properties;
            }

            [Serializable]
            public class GameObjectData
            {
                [SerializeField]
                public string Name;
                [SerializeField]
                public string Tag;
                [SerializeField]
                public string Layer;
                [SerializeField]
                public bool IsPrefab;
                [SerializeField]
                public bool IsStatic;
                [SerializeField]
                public bool IsActive;
                [SerializeField]
                public List<ComponentData> Components = new List<ComponentData>();
            }


            [HideInInspector]
            [SerializeField]
            public List<GameObjectData> CollectedData;

            private int _currentIndex = 0;


            public int CurrentIndex
            {
                get { return _currentIndex; }
                set
                {
                    DevLog($"CurrentIndex changed from {_currentIndex} to {value}");
                    if (_currentIndex != value)
                    {
                        _currentIndex = value;

                    }
                }
            }

            private GameObject[] allObjects;
            private bool _isCollecting;

            public bool IsCollecting
            {
                get { return _isCollecting; }
                set
                {
                    DevLog($"IsCollecting changed from {_isCollecting} to {value}");
                    if (_isCollecting != value)
                    {
                        _isCollecting = value;
                    }
                }
            }

            [SerializeField]
            public int totalPolyCount;  // New variable for total polygon count
            [SerializeField]
            public int meshFilterCount; // New variable for counting MeshFilter components


            public void UpdateDeepDive()
            {
                if (IsCollecting)
                {
                    ProcessBatch();
                }
            }

            public void StartCollection()
            {
                DevLog("Starting data collection");
                allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                DevLog($"Found {allObjects.Length} objects");
                CurrentIndex = 0;
                CollectedData = new List<GameObjectData>();
                IsCollecting = true;
                //indexContainer.style.display = DisplayStyle.Flex;
            }

            private void ProcessBatch()
            {
                int processedObjects = 0;
                DevLog($"Processing batch {CurrentIndex} of {allObjects.Length} objects");

                // DevLog($"Processing batch {currentIndex} of {allObjects.Length} objects");
                // progressBar.lowValue = 0;
                // progressBar.highValue = allObjects.Length;
                // progressBar.value = (float)currentIndex;

                while (CurrentIndex < allObjects.Length && processedObjects < 5000)
                {
                    GameObject currentGO = allObjects[CurrentIndex];
                    //DevLog($"Processing {currentGO.name} {currentIndex} of {allObjects.Length} objects");

                    // Skip hidden or inactive GameObjects
                    if (currentGO == null || ShouldSkipGameObject(currentGO))
                    {
                        CurrentIndex++;
                        continue;
                    }

                    GameObjectData goData = new GameObjectData
                    {
                        Name = currentGO.name,
                        Tag = currentGO.tag,
                        Layer = LayerMask.LayerToName(currentGO.layer),
                        IsPrefab = PrefabUtility.IsPartOfAnyPrefab(currentGO),
                        IsStatic = currentGO.isStatic,
                        IsActive = currentGO.activeSelf,
                        Components = new List<ComponentData>()
                    };
                    DevLog($"Processing goData {goData.Name} {CurrentIndex} of {allObjects.Length} objects");

                    Component[] components = allObjects[CurrentIndex].GetComponents<Component>();

                    // ShowToast($"{currentIndex}/{allObjects.Length}: {goData.Name}");

                    foreach (Component component in components)
                    {
                        if (component == null) continue;
                        if (component is MeshFilter meshFilter)
                        {
                            if (meshFilter.sharedMesh != null)
                            {
                                int polyCount = meshFilter.sharedMesh.triangles.Length / 3;
                                totalPolyCount += polyCount; // Update the total polygon count
                                meshFilterCount++;           // Increment the MeshFilter component count
                            }
                        }

                        ComponentData componentData = new()
                        {
                            TypeName = component.GetType().Name,
                            Properties = new List<PropertyData>(),
                        };

                        SerializedObject so = new SerializedObject(component);
                        SerializedProperty sp = so.GetIterator();

                        // Step to the first field
                        sp.Next(true);

                        while (sp.NextVisible(false))
                        {
                            // You could use the serialized property's type here to determine how to output it
                            string propertyKey = sp.name;
                            string propertyType = sp.propertyType.ToString();
                            string propertyValue = AsStringValue(sp); // We'll define this method to convert SerializedProperty to string

                            componentData.Properties.Add(new PropertyData
                            {
                                Key = propertyKey,
                                Type = propertyType,
                                Value = propertyValue // Setting actual value
                            });
                        }

                        goData.Components.Add(componentData);
                    }


                    CollectedData.Add(goData);
                    CurrentIndex++;
                    processedObjects++;
                }

                if (CurrentIndex >= allObjects.Length)
                {
                    IsCollecting = false;
                    DevLog("Data collection completed.");
                    DevLog($"Total Poly Count: {totalPolyCount}, Average Poly Count: {(float)totalPolyCount / meshFilterCount}");
                    //indexContainer.style.display = DisplayStyle.None;
                    //progressBar.value = 0;
                    // AuditData();
                }
            }

            private bool ShouldSkipGameObject(GameObject go)
            {
                // Example condition for skipping a GameObject
                if (!go.activeInHierarchy)
                {
                    return true;
                }

                // Add more conditions as needed, for example:
                // if (go.layer == LayerMask.NameToLayer("HiddenLayer"))
                // {
                //     return true;
                // }

                // if (go.tag == "HiddenTag")
                // {
                //     return true;
                // }

                return false;
            }

            public void AuditData()
            {
                if (CollectedData.Count == 0)
                {
                    DevLog("No data to audit. Please collect scene data first.");
                    return;
                }

                foreach (GameObjectData goData in CollectedData)
                {
                    DevLog($"GameObject: {goData.Name}");
                    foreach (ComponentData componentData in goData.Components)
                    {
                        DevLog($"--Component: {componentData.TypeName}");
                        foreach (PropertyData propertyData in componentData.Properties)
                        {
                            DevLog($"----Property: {propertyData.Key}, Value: {propertyData.Value}");
                        }
                    }
                }

                if (meshFilterCount > 0) // Avoid division by zero
                {
                    float avgPolyCount = (float)totalPolyCount / meshFilterCount;
                    DevLog($"Total Poly Count: {totalPolyCount}, Average Poly Count: {avgPolyCount}");
                }

                PerformSearch("Camera");
            }


            public List<GameObjectData> SearchCollectedData(string query, SearchType searchType, bool isCaseSensitive)
            {
                List<GameObjectData> searchResults = new List<GameObjectData>();
                if (CollectedData == null) return searchResults;

                foreach (GameObjectData goData in CollectedData)
                {
                    bool matchFound = false;

                    // Adjust the query and name according to the case sensitivity
                    string adjustedQuery = isCaseSensitive ? query : query.ToLower();
                    string adjustedName = isCaseSensitive ? goData.Name : goData.Name.ToLower();

                    if (searchType == SearchType.Name)
                    {
                        matchFound = adjustedName.Contains(adjustedQuery);
                    }
                    else if (searchType == SearchType.Deep)
                    {
                        matchFound = adjustedName.Contains(adjustedQuery);

                        if (!matchFound)
                        {
                            foreach (ComponentData componentData in goData.Components)
                            {
                                foreach (PropertyData propertyData in componentData.Properties)
                                {
                                    // Adjust the key and value according to the case sensitivity
                                    string adjustedKey = isCaseSensitive ? propertyData.Key : propertyData.Key.ToLower();
                                    string adjustedValue = isCaseSensitive ? propertyData.Value : propertyData.Value.ToLower();

                                    if (adjustedKey.Contains(adjustedQuery) || adjustedValue.Contains(adjustedQuery))
                                    {
                                        DevLog($"Deep search match: {goData.Name} - {componentData.TypeName} - {propertyData.Key} - {propertyData.Value}");
                                        matchFound = true;
                                        break;
                                    }
                                }
                                if (matchFound) break;
                            }
                        }
                    }

                    if (matchFound)
                    {
                        searchResults.Add(goData); // Add the GameObject, not the component
                    }
                }

                return searchResults;
            }






            public void PerformSearch(string query)
            {
                DevLogWarning($"Performing search for \"{query}\"");
                List<GameObjectData> results = SearchCollectedData(query, currentSearchType, isCaseSensitive);

                if (currentSearchType == SearchType.Deep)
                    foreach (GameObjectData goData in results)
                    {
                        DevLog($"{query}/{goData.Name}: GameObject: {goData.Name}");
                        foreach (ComponentData componentData in goData.Components)
                        {
                            DevLog($"{query}/{goData.Name}: --Component: {componentData.TypeName}");
                            foreach (PropertyData propertyData in componentData.Properties)
                            {
                                DevLog($"{query}/{goData.Name}: ----Property: {propertyData.Key}, Value: {propertyData.Value}");
                            }
                        }
                    }

                // Display the results or handle them as you wish
                foreach (GameObjectData goData in results)
                {
                    DevLog($"Found GameObject: {goData.Name}");
                }
            }


            // public GameObjectData PerformSearch(string query)
            // {
            //     DevLogWarning($"Performing search for \"{query}\"");
            //     List<GameObjectData> results = SearchCollectedData(query, currentSearchType, isCaseSensitive);

            //     if (currentSearchType == SearchType.Deep)
            //         foreach (GameObjectData goData in results)
            //         {
            //             DevLog($"{query}/{goData.Name}: GameObject: {goData.Name}");
            //             foreach (ComponentData componentData in goData.Components)
            //             {
            //                 DevLog($"{query}/{goData.Name}: --Component: {componentData.TypeName}");
            //                 foreach (PropertyData propertyData in componentData.Properties)
            //                 {
            //                     DevLog($"{query}/{goData.Name}: ----Property: {propertyData.Key}, Value: {propertyData.Value}");
            //                 }
            //             }
            //         }

            //     GameObjectData data = new GameObjectData();
            //     // Display the results or handle them as you wish
            //     foreach (GameObjectData goData in results)
            //     {
            //         DevLog($"Found GameObject: {goData.Name}");
            //         data.
            //     }
            // }




        }


        public enum UIType
        {
            TypeA,
            TypeB,
            // add more types as needed
        }

        [System.Serializable]
        public class CustomWindowContainer
        {
            public UIType uiType;
            public string data;  // data required to rebuild the UI
        }

        // public class WindowManagerStateful
        // {
        //     public interface ICommand
        //     {
        //         void Execute();
        //     }

        //     public class RegisterWindowCommand : ICommand
        //     {
        //         private string id;
        //         private CustomWindowStateful window;
        //         private static WindowManagerStateful manager;

        //         public RegisterWindowCommand(string id, CustomWindowStateful window, WindowManagerStateful manager)
        //         {
        //             this.id = id;
        //             this.window = window;
        //             this.manager = manager;
        //         }

        //         public void Execute()
        //         {
        //             manager.serializableWindows.Add(new WindowManagerStateful.SerializableWindowState { id = id, window = window });
        //         }
        //     }

        //     public class DeregisterWindowCommand : ICommand
        //     {
        //         private string id;
        //         private WindowManagerStateful manager;

        //         public DeregisterWindowCommand(string id, WindowManagerStateful manager)
        //         {
        //             this.id = id;
        //             this.manager = manager;
        //         }

        //         public void Execute()
        //         {
        //             manager.serializableWindows.RemoveAll(w => w.id == id);
        //         }
        //     }


        //     [System.Serializable]
        //     public struct SerializableWindowState
        //     {
        //         public string id;
        //         public CustomWindowStateful window;
        //     }

        //     // Serializable list to store window states
        //     [SerializeField]
        //     public List<SerializableWindowState> serializableWindows = new List<SerializableWindowState>();

        //     // Command Queue
        //     private Queue<ICommand> commandQueue = new Queue<ICommand>();

        //     // Register a new window
        //     public bool RegisterWindow(string id, CustomWindowStateful window)
        //     {
        //         // Validation logic (simplified for example)
        //         if (serializableWindows.Exists(w => w.id == id))
        //         {
        //             DevLogWarning("A window with this ID already exists: " + id);
        //             return false;
        //         }

        //         // Queue the registration command
        //         QueueCommand(new RegisterWindowCommand(id, window, this));

        //         // Execute commands to actually perform the registration
        //         ExecuteCommands();

        //         return true;
        //     }

        //     // Deregister a window
        //     public void DeregisterWindow(string id)
        //     {
        //         // Validation logic (simplified for example)
        //         if (!serializableWindows.Exists(w => w.id == id))
        //         {
        //             DevLogWarning("No window registered with this ID: " + id);
        //             return;
        //         }

        //         // Queue the deregistration command
        //         QueueCommand(new DeregisterWindowCommand(id, this));

        //         // Execute commands to actually perform the deregistration
        //         ExecuteCommands();
        //     }

        //     // Queue a command for execution
        //     private void QueueCommand(ICommand command)
        //     {
        //         commandQueue.Enqueue(command);
        //     }

        //     // Execute all queued commands
        //     private void ExecuteCommands()
        //     {
        //         while (commandQueue.Count > 0)
        //         {
        //             ICommand command = commandQueue.Dequeue();
        //             command.Execute();
        //         }
        //     }

        //     // Clear the command queue (optional, for debugging/testing)
        //     public void ClearCommands()
        //     {
        //         commandQueue.Clear();
        //     }
        // }


        // public static class WindowManager
        // {
        //     public static Dictionary<string, CustomWindowStateful> WindowDictionary = new Dictionary<string, CustomWindowStateful>();
        //     public static bool IsRegistered(string id)
        //     {
        //         return WindowDictionary.ContainsKey(id);
        //     }
        //     public static bool RegisterWindow(string id, CustomWindowStateful window)
        //     {
        //         if (WindowDictionary.ContainsKey(id))
        //         {
        //             DevLogWarning("A window with this ID already exists: " + id);
        //             return false;
        //         }
        //         // Check for any open EditorWindow instances with a title matching the provided id
        //         foreach (var openWindows in Resources.FindObjectsOfTypeAll<EditorWindow>())
        //         {
        //             if (openWindows.titleContent.text == id)
        //             {
        //                 DevLogWarning("An editor window with this ID already exists: " + id);
        //                 return false;
        //             }
        //         }

        //         DevLog("Registering window with ID: " + id);
        //         WindowDictionary[id] = window;
        //         BuildWindowBar();
        //         return true;
        //     }

        //     public static void DeregisterWindow(string id)
        //     {
        //         if (!WindowDictionary.ContainsKey(id))
        //         {
        //             DevLogWarning("No window registered with this ID: " + id);
        //             return;
        //         }
        //         WindowDictionary.Remove(id);
        //         BuildWindowBar();
        //     }
        // }

        public class HeatmapData
        {
            public Vector3 Position { get; set; }
            public float Intensity { get; set; }

            public HeatmapData(Vector3 position, float intensity)
            {
                Position = position;
                Intensity = intensity;
            }
        }

        [Serializable]
        public class MiniMapData
        {
            public Texture2D map;
            [SerializeField] private string mapPath;
            [SerializeField] private Vector3 cachePos;
            [SerializeField] private float widthCache;
            [SerializeField] private float heightCache;
            [SerializeField] private float orthSizeCache;
            [SerializeField] private Vector3 position;
            [SerializeField] private Quaternion rotation;
            [SerializeField] private float orthSize = 200f;
            [SerializeField] private float pixelWidth;
            [SerializeField] private float pixelHeight;
            [SerializeField] private Vector3 topleft;
            [SerializeField] private Vector3 topright;
            [SerializeField] private Vector3 botleft;
            [SerializeField] private Vector3 botright;

            // Properties with backing fields
            public string MapPath { get => mapPath; set => mapPath = value; }
            public Vector3 CachePos { get => cachePos; set => cachePos = value; }
            public float WidthCache { get => widthCache; set => widthCache = value; }
            public float HeightCache { get => heightCache; set => heightCache = value; }
            public float OrthSizeCache { get => orthSizeCache; set => orthSizeCache = value; }
            public Vector3 Position { get => position; set => position = value; }
            public Quaternion Rotation { get => rotation; set => rotation = value; }
            public float OrthSize { get => orthSize; set => orthSize = value; }
            public float PixelWidth { get => pixelWidth; set => pixelWidth = value; }
            public float PixelHeight { get => pixelHeight; set => pixelHeight = value; }
            public Vector3 Topleft { get => topleft; set => topleft = value; }
            public Vector3 Topright { get => topright; set => topright = value; }
            public Vector3 Botleft { get => botleft; set => botleft = value; }
            public Vector3 Botright { get => botright; set => botright = value; }

            public void SaveImage()
            {
                // DevLog("SaveImage()", 638374131298693101);

                if (map == null)
                {
                    map = new Texture2D(1, 1);
                }

                //
                byte[] bytes = map.EncodeToPNG();
                MapPath = $"map_{dataSetName}";
                File.WriteAllBytes($"{MightyCoreData.GetCache()}map_{dataSetName}.png", bytes);
                // DevLog($"Saved Map Image {MapPath} and it is {map != null}", 638374131298693101);
            }

            public Texture2D GetMapTexture()
            {
                // DevLog("LoadImage()", 638374131298693101);

                Texture2D mapTexture = Resources.Load($"Cache/{MapPath}", typeof(Texture2D)) as Texture2D;
                // DevLog($"Loaded Landmark Image {MapPath} and it is {mapTexture != null}", 638374131298693101);

                if (mapTexture == null)
                {
                    mapTexture = new Texture2D(1, 1);
                    //setpixels to red
                    mapTexture.SetPixel(0, 0, Color.red);
                }
                return mapTexture;
            }


        }

        //public  int GetMapXCoord(float x, MiniMap miniMap)
        //{
        //    targetRatio = (float)screenWidth / (float)screenHeight;
        //    hh = miniMap.topleft.z - miniMap.botleft.z;
        //    ww = hh * targetRatio;
        //    xOffset = ((miniMap.topright.x - miniMap.topleft.x) / 2) - (ww / 2);
        //    x1 = miniMap.topleft.x + xOffset;
        //    x2 = miniMap.topright.x - xOffset;
        //    z1 = miniMap.botright.z;
        //    z2 = miniMap.topleft.z;
        //    return (int)((1 - ((x2 - x) / ww)) * screenWidth);
        //}

        //public  int GetMapZCoord(float z)
        //{
        //    return (int)((1 - ((z - z1) / hh)) * screenHeight);
        //}

        public static Vector2 GetMapCoords(float x, float z)
        {
            targetRatio = (float)screenWidth / (float)screenHeight;
            try
            {
                hh = sceneData.MiniMap.Topleft.z - sceneData.MiniMap.Botleft.z;
            }
            catch
            {
                return new Vector2(0, 0);
            }
            ww = hh * targetRatio;
            xOffset = ((sceneData.MiniMap.Topright.x - sceneData.MiniMap.Topleft.x) / 2) - (ww / 2);
            x1 = sceneData.MiniMap.Topleft.x + xOffset;
            x2 = sceneData.MiniMap.Topright.x - xOffset;
            z1 = sceneData.MiniMap.Botright.z;
            z2 = sceneData.MiniMap.Topleft.z;

            return
                new Vector2((int)((1 - ((x2 - x) / ww)) * screenWidth),
                            (int)((1 - ((z - z1) / hh)) * screenHeight));
        }


        [Serializable]
        public class ColorTexture
        {
            [SerializeField]
            public Texture2D texture;
            [SerializeField]
            public Color color;
        }


        [Serializable]
        public class MappableTypeInfo
        {
            [SerializeField]
            public string TypeName; // Store the type name as a string
            [SerializeField]
            public string Name;     // Store the human-readable name
            [SerializeField]
            public bool IsActive = true;   // Store the active state
            [SerializeField]
            public IMappable Mappable; // Store the actual type
            [SerializeField]
            public bool released = true;

            public MappableTypeInfo(string typeName, string name, bool isActive, IMappable mappable)
            {
                DevLog($"Mappable TypeInfo {typeName} {name} {isActive} {mappable.ToString()}");
                TypeName = typeName;
                Name = name;
                IsActive = isActive;
                Mappable = mappable;
            }
        }


        #region IMappable
        public interface IMappable
        {

            int ID { get; set; }
            int ParentId { get; set; }
            string AnchorTo { get; set; }
            string PackageName { get; set; }
            string Name { get; set; }
            string Description { get; set; }
            bool Active { get; set; }
            bool Front { get; set; }
            bool Dirty { get; set; }
            bool HasVisualContent { get; set; }
            bool HasPlayTracking { get; set; }
            bool ShowAlways { get; set; }

            long CreatedAt { get; set; }
            string Version { get; set; }


            Views ViewUI { get; set; }
            VisualElement PrevView { get; set; }


            Vector3 Offset { get; set; }
            Attributes MapAttributes { get; set; }
            Location MapLocation { get; set; }
            Picture Pic { get; set; }

            Texture2D Icon { get; set; }

            void RegisterMappable();

            void OnGenerateVisualContent(MeshGenerationContext mgc);

            void Delete();

            void LoadImage();

            void CheckIntegrity();

            bool UpdateAvailable();

            Button AddMappable(bool setClickedCallback = true);
            CustomToggleButton AddModuleToggle(MappableTypeInfo mappableTypeInfo);

            VisualElement SceneSummary(SceneData scene);
            VisualElement SettingsView();

            MightySceneViewManager.Settings GetSceneViewSettings();

            void PopulatePlayTrackingLane(int laneIndex);

        }

        #endregion

        [Serializable]
        public class Views
        {
            private Stack<VisualElement> viewStack;
            public VisualElement root;
            private Dictionary<string, VisualElement> views;
            //initial values for width and height because UI objects don't immediately populate
            public float maxWidth, maxHeight;
            private string uxmlName;

            public Views()
            {

                Clear();
            }

            public void Init()
            {
                viewStack = new Stack<VisualElement>();
                root = new VisualElement();
                views = new Dictionary<string, VisualElement>();
            }

            public void SetRoot(VisualElement root)
            {
                this.root = root;
                //DevLog($"Setting Root of this Mappable to {root}");
            }

            public VisualElement GetRoot()
            {
                DevLog($"GetRoot {root}");
                return root;
            }

            public string GetUxmlName()
            {
                return uxmlName;
            }

            public bool AddView(string uxml, int mw, int mh)
            {
                DevLog($"AddView {uxml}   {mw}/{mh}");
                uxmlName = uxml;
                var vta = Resources.Load<VisualTreeAsset>(uxml);
                DevLog($"vta {vta}");
                if (vta == null) return false;

                if (views == null) views = new Dictionary<string, VisualElement>();

                DevLog(uxml);

                if (!views.ContainsKey(uxml))
                {
                    var ve = vta.CloneTree().Query<VisualElement>().First();
                    ve.style.width = maxWidth = mw;
                    ve.style.height = maxHeight = mh;
                    views.Add(uxml, ve);
                    DevLog(ve);
                }
                DevLog(uxml);
                // DebugDictionary();
                // load all predefined views from UXML files and store them in predefinedViews dictionary
                return true;
            }

            IEnumerator WaitForLayout(string uxml)
            {
                var visualElement = new VisualElement();
                // load visual elements from UXML file
                var uxmlFile = Resources.Load<VisualTreeAsset>(uxml);
                uxmlFile.CloneTree(visualElement);
                yield return null;
                var width = visualElement.layout.width;
            }


            public void PushView(string viewName)
            {
                // clear the root element before adding the new view
                // root.Clear();
                // push the new view to the stack
                if (viewStack == null) viewStack = new Stack<VisualElement>();
                views[viewName].style.display = DisplayStyle.Flex;
                if (viewStack.Count > 0) DevLog($"ZZZ before: viewStack.Peek().name {viewStack.Peek().name}");
                viewStack.Push(views[viewName]);

                // add the new view to the root element
                // root.Add(views[viewName]);
                // root.MarkDirtyRepaint();
            }

            public void PopView()
            {
                DevLog("PopView");
                if (viewStack == null || viewStack.Count == 0) return;

                if (viewStack.Count > 0)
                {
                    //Debug.Log("ViewStack Contents:");
                    //foreach (var view in viewStack)
                    //{
                    //    Debug.Log(view.name);
                    //}
                    //Debug.Log("About to Pop: " + viewStack.Peek().name);
                    viewStack.Pop();
                }
                // remove the current view from the root element
                // root.Remove(viewStack.Pop());
                // // add the previous view to the root element
                // root.Add(viewStack.Peek());
                // root.MarkDirtyRepaint();
            }

            public VisualElement GetView()
            {

                if (viewStack == null) viewStack = new Stack<VisualElement>();
                if (viewStack.Count == 0)
                {
                    // Debug.LogError($"GetView stack is empty");
                    return null;
                }
                // DevLog("GetView");

                DevLog($"---GetView() viewStack.Peek().Children().First().name {viewStack.Peek().Children().First().name}");
                var r = viewStack.Peek();
                if (r == null)
                {
                    DevLog($"---GetView() r is null");
                    r = new VisualElement();
                }

                return r;
            }

            public VisualElement GetView(string viewName)
            {
                if (views == null) views = new Dictionary<string, VisualElement>();
                if (views.Count == 0)
                {
                    DevLog($"views is empty");
                    return null;
                }
                //DevLog("GetView");
                if (views.ContainsKey(viewName))
                    return views[viewName];
                return null;
            }

            //
            public void RefreshView()
            {
                //DevLog($"RefreshView viewstack: {viewStack.Count} items");

                root.Clear();
                if (viewStack.Count == 0)
                {
                    foreach (var item in views)
                    {
                        DevLog(item.Key + " : " + item.Value);
                    }
                    return;
                }

                root.Add(viewStack.Peek());
                root.MarkDirtyRepaint();
            }

            public void ResetViewsToFirst()
            {
                var firstView = viewStack.First();
                viewStack.Clear();
                viewStack.Push(firstView);
                root.Clear();
                root.Add(firstView);
                root.MarkDirtyRepaint();
            }

            public void Clear()
            {
                if (viewStack != null)
                    viewStack.Clear();
                if (root != null)
                {
                    root.Clear();
                    root.MarkDirtyRepaint();
                }
            }

            public void DebugDictionary()
            {
                string[] items = new string[views.Count];
                int i = 0;
                foreach (var item in views)
                {
                    items[i] = item.Key + " : " + item.Value;
                    i++;
                }
                DevLog($"Debug Dictionary {i}: {string.Join(", ", items)}");
            }
            // private VisualTreeAsset visualTreeAsset;
            // private VisualElement view;
            // [SerializeField]
            // private string uxml;
            // private int maxWidth, maxHeight;

            // public Views(string u, int mw, int mh)
            // {
            //     uxml = u;
            //     visualTreeAsset = Resources.Load<VisualTreeAsset>(uxml);
            //     view = visualTreeAsset.CloneTree().Query<VisualElement>().First();
            //     view.style.maxWidth = maxWidth = mw;
            //     view.style.maxHeight = maxHeight = mh;
            // }

            // public VisualElement GetView()
            // {
            //     return view;
            // }

            // public void Rebuild()
            // {
            //     visualTreeAsset = Resources.Load<VisualTreeAsset>(uxml);
            //     view = visualTreeAsset.CloneTree().Query<VisualElement>().First();
            //     view.style.maxWidth = maxWidth;
            //     view.style.maxHeight = maxHeight;
            // }
        }

        [Serializable]
        public class Picture
        {
            [SerializeField]
            public string path;
            [SerializeField]
            public string filename;
            [SerializeField]
            public int width;
            [SerializeField]
            public int height;
            [SerializeField]
            public string format;
            [SerializeField]
            public bool rotateWithMappable = false;
            [NonSerialized]
            public Texture2D img, background;
            [NonSerialized]
            public bool imgLoaded = false;

            public Picture()
            {
                path = "";
                filename = "none.jpg";
                width = 1;
                height = 1;
                format = "jpeg";
            }
        }

        public class Icons
        {
            public Texture2D mmCamera, trackableIcon, blueGearIcon, screenshotIcon, trashcanIcon, recorderIcon, editPenIcon, notificationOnIcon, notificationOffIcon, upgradeIcon, bugIcon, featuresIcon, ratingsIcon,
            window_close, window_maximize, window_minimize, window_popout, window_resize,
            map_follow_sceneview_on, map_follow_sceneview_off, mightybot, mightyeye, quickActionsFade, previewTracking, previewLeap, previewHeatmaps,
            newsIcon, archiveIcon,
            prefabOn, prefabOff, activeOn, activeOff, staticOn, staticOff, polyIcon, gameObjectIcon, landmarkIcon;

            public Icons()
            {
                mmCamera = Resources.Load("ui/mighty_icon_mmcamera") as Texture2D;
                trackableIcon = Resources.Load("trackable_icon") as Texture2D;
                blueGearIcon = Resources.Load("ui/mighty_icon_toggle_gear") as Texture2D;
                screenshotIcon = Resources.Load("ui/mighty_icon_screenshot") as Texture2D;
                editPenIcon = Resources.Load("ui/mighty_icon_editpen") as Texture2D;

                notificationOnIcon = Resources.Load("ui/mighty_icon_notification_on") as Texture2D;
                notificationOffIcon = Resources.Load("ui/mighty_icon_notification_off") as Texture2D;
                upgradeIcon = Resources.Load("ui/mighty_icon_upgrade") as Texture2D;
                bugIcon = Resources.Load("ui/mighty_icon_bug") as Texture2D;
                featuresIcon = Resources.Load("ui/mighty_icon_feature") as Texture2D;
                ratingsIcon = Resources.Load("ui/mighty_icon_ratings") as Texture2D;
                trashcanIcon = Resources.Load("ui/mighty_icon_trashcan") as Texture2D;
                recorderIcon = Resources.Load("ui/mighty_icon_recorder") as Texture2D;
                window_close = Resources.Load("ui/btn_window_close") as Texture2D;
                window_maximize = Resources.Load("ui/btn_window_maximize") as Texture2D;
                window_minimize = Resources.Load("ui/btn_window_minimize") as Texture2D;
                window_popout = Resources.Load("ui/btn_window_popout") as Texture2D;
                window_resize = Resources.Load("ui/btn_window_resize") as Texture2D;
                map_follow_sceneview_on = Resources.Load("ui/btn_follow_sceneview_on") as Texture2D;
                map_follow_sceneview_off = Resources.Load("ui/btn_follow_sceneview_off") as Texture2D;
                mightybot = Resources.Load("ui/mightybot") as Texture2D;
                mightyeye = Resources.Load("ui/mightyeye") as Texture2D;
                quickActionsFade = Resources.Load("ui/QuickActionsFade") as Texture2D;
                previewTracking = Resources.Load("ui/mighty_preview_tracking") as Texture2D;
                previewLeap = Resources.Load("ui/mighty_preview_portal") as Texture2D;
                previewHeatmaps = Resources.Load("ui/mighty_preview_heatmaps") as Texture2D;
                newsIcon = Resources.Load("ui/mighty_icon_news") as Texture2D;
                archiveIcon = Resources.Load("ui/mighty_icon_archive") as Texture2D;
                prefabOn = Resources.Load("ui/mighty_icon_prefab_on") as Texture2D;
                prefabOff = Resources.Load("ui/mighty_icon_prefab_off") as Texture2D;
                activeOn = Resources.Load("ui/mighty_icon_active_on") as Texture2D;
                activeOff = Resources.Load("ui/mighty_icon_active_off") as Texture2D;
                staticOn = Resources.Load("ui/mighty_icon_static_on") as Texture2D;
                staticOff = Resources.Load("ui/mighty_icon_static_off") as Texture2D;
            }
        }
        //
        public const string corePath = "Assets/MightyDevOps";

        public Vector3 svPos, _svPos;
        public Quaternion svRot, _svRot;

        //public int sceneIndex = 0, sceneIndexPrev = 0;



        // [Serializable]
        // public class Offset
        // {
        //     public float x;
        //     public float y;
        //     public float z;

        //     public override string ToString()
        //     {
        //         return $"x: {x}, y: {y}, z: {z}";
        //     }
        // }

        [Serializable]
        public class Location
        {
            public Vector3 worldPosition;
            public float top, left, offsetTop, offsetLeft;
            public Rect rect;
            public Quaternion worldRotation;
        }

        [Serializable]
        public class Attributes
        {
            public Color textMainColor;
            public Color backgroundColor;
            public Color textAccentColor;
            public Color backgroundAccentColor;
        }


        [SerializeField]
        public int landmarkMaxId;
        public static Camera svCameraOverride = null;
        public int selectedID;
        static public Vector3 svCameraPositionCache;
        static public Quaternion svCameraRotationCache;

        static public bool IsSceneViewDirty()
        {
            Vector3 currentPosition = GetSVCameraPosition();
            Quaternion currentRotation = GetSVCameraRotation();

            if (currentPosition.x != svCameraPositionCache.x || currentPosition.z != svCameraPositionCache.z || currentPosition.y != svCameraPositionCache.y || currentRotation != svCameraRotationCache)
            {
                svCameraPositionCache = currentPosition;
                svCameraRotationCache = currentRotation;
                return true;
            }

            return false;
        }

        static public Vector3 GetSVCameraPosition()
        {
            var cameras = SceneView.GetAllSceneCameras();
            Vector3 r = Vector3.zero;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (SceneView.currentDrawingSceneView != null)
                {
                    if (SceneView.currentDrawingSceneView.camera.transform.position == cameras[i].transform.position) r = cameras[i].transform.position;
                    break;
                }

                if (SceneView.lastActiveSceneView != null)
                    if (SceneView.lastActiveSceneView.camera.transform.position == cameras[i].transform.position) r = cameras[i].transform.position;

            }

            return r;
        }

        static public Quaternion GetSVCameraRotation()
        {
            Quaternion r = Quaternion.identity;

            var cameras = SceneView.GetAllSceneCameras();

            for (int i = 0; i < cameras.Length; i++)
            {
                if (SceneView.currentDrawingSceneView != null)
                {
                    if (SceneView.currentDrawingSceneView.camera.transform.position == cameras[i].transform.position) r = cameras[i].transform.rotation;
                    break;
                }

                if (SceneView.lastActiveSceneView != null)
                    if (SceneView.lastActiveSceneView.camera.transform.position == cameras[i].transform.position) r = cameras[i].transform.rotation;
            }
            return r;
        }

        static public float GetSVCOrthographicSize()
        {
            var cameras = SceneView.GetAllSceneCameras();
            float r = 0;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (SceneView.currentDrawingSceneView != null)
                {
                    if (SceneView.currentDrawingSceneView.camera.transform.position == cameras[i].transform.position) r = SceneView.currentDrawingSceneView.camera.orthographicSize;
                    break;
                }

                if (SceneView.lastActiveSceneView != null)
                    if (SceneView.lastActiveSceneView.camera.transform.position == cameras[i].transform.position) r = SceneView.lastActiveSceneView.camera.orthographicSize;
            }
            return r;
        }

        static public SceneView GetSceneView()
        {
            if (SceneView.lastActiveSceneView != null) return SceneView.lastActiveSceneView;
            if (SceneView.currentDrawingSceneView != null) return SceneView.currentDrawingSceneView;
            DevLogError("No SceneView found");
            return null;
        }

        static string m_ScriptFilePath;
        static string m_ScriptFolder;
        static public string MightyPath, MightyCache;
        static public MightyCoreData dataCore;

        static public string GetAssetPath()
        {
            //m_ScriptFilePath = AssetDatabase.GetAssetPath(dataCore);
            ////DevLog($"m_ScriptFilePath: {m_ScriptFilePath}");
            //FileInfo fi = new FileInfo(m_ScriptFilePath);
            //m_ScriptFolder = fi.Directory.ToString();

            //string x = m_ScriptFolder;
            //DevLog(x.IndexOf("TaskAtlasOnline\\"));
            ////if (x != null && x.Length > 0)
            //DevLog(x.Substring(0, x.IndexOf("TaskAtlasOnline")) + "TaskAtlasOnline\\");
            //x = x.Substring(0, x.IndexOf("TaskAtlasOnline")) + "TaskAtlasOnline\\";
            //x = x.Replace("\\", "/");

            //DevLog(x);
            var x = "Assets/MightyDevOps/";
            return x; //.Replace("//", "/").Replace("Assets/", "");//
        }

        static public void SetPath()
        {
            MightyPath = GetAssetPath();
            MightyCache = MightyPath + "Resources/Cache/";
            DevLog("Path set to " + MightyPath + "|" + Application.dataPath);
        }

        static public string GetPath()
        {
            if (MightyPath == null || MightyPath == "") SetPath();
            DevLog("Getting path to " + MightyPath + "|" + Application.dataPath);
            return MightyPath;
        }
        static public string GetCache()
        {
            if (MightyCache == null || MightyCache == "") SetPath();
            return MightyCache;
        }

        public static VisualElement GetUXML(string uxml)
        {
            DevLog($"GetUXML {uxml}");
            var vta = Resources.Load<VisualTreeAsset>(uxml);
            var ve = vta.CloneTree().Query<VisualElement>().First();
            return ve;
        }

        static public Color StringToColor(string inputString, float brightness = 1.0f)
        {
            // Create a hash of the input string
            int hash = inputString.GetHashCode();

            // Use bitwise operations to get the first, second, and third bytes of the hash
            // Each byte is an integer between 0 and 255, which we then normalize to a float between 0 and 1
            float r = ((hash >> 24) & 0xFF) / 255f;
            float g = ((hash >> 16) & 0xFF) / 255f;
            float b = ((hash >> 8) & 0xFF) / 255f;

            // We're ignoring the least significant byte, as it won't have much visual impact

            // To ensure we don't exceed 0.6 in intensity for any of the RGB components, 
            // we find the max RGB value and divide all the RGB components by it to normalize them between 0 and 1
            // then multiply them by 0.6
            float maxRGB = Mathf.Max(Mathf.Max(r, g), b);
            r = r / maxRGB * 0.6f;
            g = g / maxRGB * 0.6f;
            b = b / maxRGB * 0.6f;

            // Adjust the brightness of the color
            float maxBrightness = Mathf.Max(r, Mathf.Max(g, b));
            if (maxBrightness > brightness)
            {
                float brightnessScale = brightness / maxBrightness;
                r *= brightnessScale;
                g *= brightnessScale;
                b *= brightnessScale;
            }

            return new Color(r, g, b, 1);
        }


        static public List<ColorTexture> colorTextures;

        static public Texture2D MakeTex(int width, int height, Color col)
        {
            if (width == 0 | height == 0 | width < 0 | height < 0)
                width = height = 1;

            if (colorTextures == null) colorTextures = new List<ColorTexture>();
            for (int i = 0; i < colorTextures.Count; i++)
            {
                if (col == colorTextures[i].color && colorTextures[i].texture != null)
                {
                    return colorTextures[i].texture;
                }
            }
            //DevLog("w: " + width + " h: " + height);
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            colorTextures.Add(new ColorTexture() { texture = result, color = col });
            colorTextures[colorTextures.Count - 1].texture.name = "Core Texture " + col.ToString();

            return result;
        }

        public void SceneViewGoToPosition(Vector3 mp, Quaternion mr)
        {
            // Debug.Log($"SceneViewGoToPosition {mp} {mr}");
            var sv = GetSceneView();
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(mp.x, mp.y, mp.z);
            cube.transform.rotation = new Quaternion(mr.x, mr.y, mr.z, mr.w);

            sv.AlignViewToObject(cube.transform);
            sv.Repaint();
            GameObject.DestroyImmediate(cube);

            sceneData.MiniMap.Position = mp;
            sceneData.MiniMap.Rotation = mr;


            // bool x = followSceneView;
            // followSceneView = true;
            // EditorApplication.delayCall += () =>
            // {
            //     followSceneView = x;
            // };
        }

        // Helper method to desaturate colors for subtler backgrounds
        public static Color Desaturate(Color color, float amount)
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            s -= amount;
            return Color.HSVToRGB(h, Mathf.Clamp01(s), v);
        }

        static public bool Overlaps(Rect rect1, Rect rect2)
        {
            return rect1.x < rect2.xMax && rect1.xMax > rect2.x && rect1.y < rect2.yMax && rect1.yMax > rect2.y;
        }

        public static bool SignificantOverlap(Rect rect1, Rect rect2, float threshold)
        {
            // Find intersection rectangle
            Rect intersection = Rect.MinMaxRect(
                Mathf.Max(rect1.xMin, rect2.xMin),
                Mathf.Max(rect1.yMin, rect2.yMin),
                Mathf.Min(rect1.xMax, rect2.xMax),
                Mathf.Min(rect1.yMax, rect2.yMax)
            );

            // If there is no intersection, return false
            if (intersection.width <= 0 || intersection.height <= 0)
            {
                return false;
            }

            // Calculate the areas
            float area1 = rect1.width * rect1.height;
            float area2 = rect2.width * rect2.height;
            float intersectionArea = intersection.width * intersection.height;

            // Check if the intersection is significant based on the threshold
            // DevLog($"SignificantOverlap {intersectionArea / area1} {intersectionArea / area2} {threshold}");
            return (intersectionArea / area1 >= threshold) || (intersectionArea / area2 >= threshold);
        }

        public static string AsStringValue(SerializedProperty sp)
        {
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return sp.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return sp.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return sp.floatValue.ToString();
                case SerializedPropertyType.String:
                    return sp.stringValue;
                case SerializedPropertyType.Color:
                    return sp.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return sp.objectReferenceValue ? sp.objectReferenceValue.name : "null";
                case SerializedPropertyType.Vector2:
                    return sp.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return sp.vector3Value.ToString();
                case SerializedPropertyType.Enum:
                    if (sp.enumValueIndex >= 0 && sp.enumValueIndex < sp.enumNames.Length)
                    {
                        return sp.enumNames[sp.enumValueIndex];
                    }
                    else
                    {
                        return "Invalid Enum Index";
                    }
                default:
                    return sp.ToString();
            }
        }


        public class Debouncer
        {
            private float debounceTime;
            private double lastInvokeTime;
            private System.Action debouncedAction;

            public Debouncer(float debounceTimeInSeconds)
            {
                this.debounceTime = debounceTimeInSeconds;
            }

            public void Invoke(System.Action action)
            {
                debouncedAction = action;
                double currentTime = EditorApplication.timeSinceStartup;
                float timeDifference = (float)(currentTime - lastInvokeTime);

                if (timeDifference < debounceTime)
                {
                    EditorApplication.delayCall -= ExecuteAction;
                    EditorApplication.delayCall += ExecuteAction; // Re-registering the callback
                }
                else
                {
                    ExecuteAction();
                }

                lastInvokeTime = currentTime;
            }

            private void ExecuteAction()
            {
                debouncedAction?.Invoke();
                EditorApplication.delayCall -= ExecuteAction; // Unregister the callback after execution
            }
        }

        public static VisualElement Header(string labelText, int fs = 18)
        {
            VisualElement header = new VisualElement();
            header.style.backgroundColor = new StyleColor(Color.gray);
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;

            Label label = new Label(labelText);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = fs;
            label.style.color = Color.white;

            header.Add(label);

            return header;
        }

        public static VisualElement HelpText(string helpText)
        {
            VisualElement helpTextElement = new VisualElement();
            helpTextElement.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.7f)); // Slightly transparent background
            helpTextElement.style.paddingTop = 10;
            helpTextElement.style.paddingBottom = 10;
            helpTextElement.style.paddingLeft = 10;
            helpTextElement.style.paddingRight = 10;
            helpTextElement.style.marginTop = 5;
            helpTextElement.style.marginBottom = 5;
            helpTextElement.style.flexDirection = FlexDirection.Row;
            helpTextElement.style.flexGrow = 1;
            helpTextElement.style.width = Length.Percent(100);

            Label label = new Label(helpText);
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            label.style.fontSize = 12;
            label.style.color = Color.white;
            label.style.whiteSpace = WhiteSpace.Normal; // Allows text to wrap

            helpTextElement.Add(label);

            return helpTextElement;
        }

        public static Label StyledLabel(string labelText)
        {
            Label label = new Label(labelText)
            {
                style = {
            unityTextAlign = TextAnchor.MiddleCenter,
            fontSize = 14,
            color = Color.white,
            whiteSpace = WhiteSpace.Normal // Ensures the text will wrap if needed
        }
            };
            return label;
        }

        public static VisualElement Title(string labelText, string tooltip = "")
        {
            Label label = new()
            {
                text = labelText,
                tooltip = tooltip,
                style = {
                                    unityTextAlign = TextAnchor.MiddleCenter,
                                    fontSize = 14,
                                    color = Color.white
                                }
            };

            return label;
        }




        public static VisualElement Spacer(int space = 10)
        {
            VisualElement spaceElement = new VisualElement();
            spaceElement.style.height = space;
            return spaceElement;
        }

        public static VisualElement FloatSliderWithField(string labelText, string tooltip, string name, float value, float lowValue, float highValue, Action<float> onValueChanged, float stepSize = 0.1f)
        {
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 10;

            Label label = new Label(labelText)
            {
                tooltip = tooltip,
                style = {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginBottom = 2,
                            fontSize = 12,
                        }
            };
            container.Add(label);

            VisualElement sliderContainer = new VisualElement()
            {
                style = {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            width = 180,
                        }
            };

            // Declare variables at the top
            Slider slider = new Slider(lowValue, highValue)
            {
                name = name,
                value = value,
                tooltip = tooltip,
                style = {
                            flexGrow = 1,
                            height = 16,
                            width = 80,
                        }
            };

            FloatField valueField = new FloatField()
            {
                name = name,
                value = value,
                style = {
                            width = 48,
                            marginLeft = 5,
                            fontSize = 12,
                        },
                tooltip = tooltip
            };

            // Decrement button
            Button decrementButton = new Button(() =>
            {
                float currentValue = slider.value;
                float newValue = Mathf.Clamp(currentValue - stepSize, lowValue, highValue);
                if (newValue != currentValue)
                {
                    slider.SetValueWithoutNotify(newValue);
                    valueField.SetValueWithoutNotify(newValue);
                    onValueChanged?.Invoke(newValue);
                }
            })
            {
                text = "−",
                style = {
                            width = 20,
                            height = 20,
                            marginRight = 2,
                            paddingLeft = 0,
                            paddingRight = 0,
                            paddingTop = 0,
                            paddingBottom = 0,
                            fontSize = 12,
                            backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)),
                            borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                        }
            };
            sliderContainer.Add(decrementButton);

            // Increment button
            Button incrementButton = new Button(() =>
            {
                float currentValue = slider.value;
                float newValue = Mathf.Clamp(currentValue + stepSize, lowValue, highValue);
                if (newValue != currentValue)
                {
                    slider.SetValueWithoutNotify(newValue);
                    valueField.SetValueWithoutNotify(newValue);
                    onValueChanged?.Invoke(newValue);
                }
            })
            {
                text = "+",
                style = {
                            width = 20,
                            height = 20,
                            marginLeft = 2,
                            paddingTop = 0,
                            paddingRight = 0,
                            paddingBottom = 0,
                            paddingLeft = 0,
                            fontSize = 12,
                            backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)),
                            borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                        }
            };

            // Synchronize slider and field
            slider.RegisterValueChangedCallback(evt =>
            {
                float newValue = Mathf.Round(evt.newValue * 10f) / 10f;
                slider.SetValueWithoutNotify(newValue);
                valueField.SetValueWithoutNotify(newValue);
                valueField.MarkDirtyRepaint();
                slider.MarkDirtyRepaint();
                onValueChanged?.Invoke(newValue);
            });
            valueField.RegisterValueChangedCallback(evt =>
            {
                float clampedValue = Mathf.Clamp(evt.newValue, lowValue, highValue);
                slider.SetValueWithoutNotify(clampedValue);
                valueField.SetValueWithoutNotify(clampedValue);
                valueField.MarkDirtyRepaint();
                slider.MarkDirtyRepaint();
                onValueChanged?.Invoke(clampedValue);
            });

            sliderContainer.Add(slider);
            sliderContainer.Add(incrementButton);
            sliderContainer.Add(valueField);

            container.Add(sliderContainer);

            return container;
        }

        // Helper method to create an int slider with an editable field next to it
        public static VisualElement IntSliderWithField(string labelText, string tooltip, string name, int value, int lowValue, int highValue, Action<int> onValueChanged)
        {
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 10;

            Label label = new Label(labelText)
            {
                tooltip = tooltip,
                style = {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginBottom = 2,
                            fontSize = 12,
                        }
            };
            container.Add(label);

            VisualElement sliderContainer = new VisualElement()
            {
                style = {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            width = 180,
                        }
            };

            // Declare variables at the top
            SliderInt slider = new SliderInt(lowValue, highValue)
            {
                name = name,
                value = value,
                tooltip = tooltip,
                style = {
                            flexGrow = 1,
                            height = 16,
                            width = 80,
                        }
            };

            IntegerField valueField = new IntegerField()
            {
                value = value,
                style = {
                            width = 48,
                            marginLeft = 5,
                            fontSize = 12,
                        },
                tooltip = tooltip
            };

            // Decrement button
            Button decrementButton = new Button(() =>
            {
                int currentValue = slider.value;
                int newValue = Mathf.Clamp(currentValue - 1, lowValue, highValue);
                if (newValue != currentValue)
                {
                    slider.SetValueWithoutNotify(newValue);
                    valueField.SetValueWithoutNotify(newValue);
                    onValueChanged?.Invoke(newValue);
                }
            })
            {
                text = "−",
                style = {
                            width = 20,
                            height = 20,
                            marginRight = 2,
                            paddingLeft = 0,
                            paddingRight = 0,
                            paddingTop = 0,
                            paddingBottom = 0,
                            fontSize = 12,
                            backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)),
                            borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                        }
            };
            sliderContainer.Add(decrementButton);

            // Increment button
            Button incrementButton = new Button(() =>
            {
                int currentValue = slider.value;
                int newValue = Mathf.Clamp(currentValue + 1, lowValue, highValue);
                if (newValue != currentValue)
                {
                    slider.SetValueWithoutNotify(newValue);
                    valueField.SetValueWithoutNotify(newValue);
                    onValueChanged?.Invoke(newValue);
                }
            })
            {
                text = "+",
                style = {
                            width = 20,
                            height = 20,
                            marginLeft = 2,
                            paddingTop = 0,
                            paddingRight = 0,
                            paddingBottom = 0,
                            paddingLeft = 0,
                            fontSize = 12,
                            backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f)),
                            borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                            borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 1f)),
                        }
            };

            // Synchronize slider and field
            slider.RegisterValueChangedCallback(evt =>
            {
                valueField.SetValueWithoutNotify(evt.newValue);
                slider.SetValueWithoutNotify(evt.newValue);
                valueField.MarkDirtyRepaint();
                slider.MarkDirtyRepaint();
                onValueChanged?.Invoke(evt.newValue);
            });
            valueField.RegisterValueChangedCallback(evt =>
            {
                int clampedValue = Mathf.Clamp(evt.newValue, lowValue, highValue);
                valueField.SetValueWithoutNotify(clampedValue);
                slider.SetValueWithoutNotify(clampedValue);
                valueField.MarkDirtyRepaint();
                slider.MarkDirtyRepaint();
                onValueChanged?.Invoke(clampedValue);
            });

            sliderContainer.Add(slider);
            sliderContainer.Add(incrementButton);
            sliderContainer.Add(valueField);

            container.Add(sliderContainer);

            return container;
        }

        public static void ShowToast(string message, int duration = 5000)
        {
            if (toastBox == null) return;
            // Debug.Log($"ShowToast {message} {duration}");
            // var root = EditorWindow.GetWindow<SceneView>().rootVisualElement;
            var toast = new Label(message)
            {
                style =
                {
                    backgroundColor = new Color(0, 0, 0, 0.8f),
                    color = Color.white,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    width = Length.Percent(100),
                    transitionProperty = new List<StylePropertyName>
                    {
                        new StylePropertyName("opacity"),
                    },
                    transitionDuration = new List<TimeValue>()
                    {
                        new TimeValue(duration, TimeUnit.Millisecond)
                    }
                }
            };
            toastBox.Add(toast);

            //     toast.RegisterCallback<GeometryChangedEvent>(e =>
            // {
            if (toastBox != null && toastBox.Contains(toast))
            {
                toast.experimental.animation
                .Start(new StyleValues { opacity = 0 }, duration)
                .Ease(Easing.OutBounce)
                .OnCompleted(() => toastBox.Remove(toast));
            }
            // }, TrickleDown.NoTrickleDown);


        }

        /// <summary>
        /// Creates a standardized feedback section with rating, bug report, feature request, and contact buttons
        /// </summary>
        /// <param name="moduleName">Name of the module for email subjects</param>
        /// <param name="ratingUrl">URL for the rating/review page</param>
        /// <param name="bugUrl">URL for bug reports</param>
        /// <param name="featureUrl">URL for feature requests</param>
        /// <param name="version">Module version for bug/feature URLs</param>
        /// <returns>A complete feedback section VisualElement</returns>
        public static VisualElement CreateFeedbackSection(string moduleName, string ratingUrl, string bugUrl, string featureUrl, string version)
        {
            var container = new VisualElement()
            {
                style = {
                    flexDirection = FlexDirection.Column,
                    marginBottom = 15,
                }
            };

            // Helper function to create section headers
            VisualElement CreateSectionHeader(string title, string tooltip, Color accentColor, bool showline = true)
            {
                var c = new Color(0.3f, 0.3f, 0.3f, 1f);

                var header = new VisualElement()
                {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginTop = 15,
                        marginBottom = 10,
                        borderTopWidth = 1,
                        paddingTop = 10,
                    }
                };

                if (showline)
                {
                    header.style.borderTopColor = new StyleColor(c);
                }

                var titleLabel = new Label(title)
                {
                    tooltip = tooltip,
                    style = {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 13,
                        color = new StyleColor(accentColor),
                        marginLeft = 5,
                    }
                };
                header.Add(titleLabel);
                return header;
            }

            // Helper function to create feedback buttons
            VisualElement CreateFeedbackButton(string text, string tooltip, Color accentColor, System.Action onClick)
            {
                var buttonContainer = new VisualElement()
                {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 8,
                    }
                };

                var button = new Button(onClick)
                {
                    text = text,
                    tooltip = tooltip,
                    style = {
                        flexGrow = 1,
                        height = 24,
                        paddingLeft = 16,
                        paddingRight = 16,
                        paddingTop = 8,
                        paddingBottom = 8,
                        backgroundColor = new StyleColor(accentColor),
                        borderTopLeftRadius = 8,
                        borderTopRightRadius = 8,
                        borderBottomLeftRadius = 8,
                        borderBottomRightRadius = 8,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        fontSize = 12,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new StyleColor(Color.white),
                    }
                };

                buttonContainer.Add(button);
                return buttonContainer;
            }

            // Feedback Section Header
            container.Add(CreateSectionHeader("We Value Your Feedback", $"Help us improve {moduleName}", new Color(1f, 0.8f, 0.2f, 1f), false));

            var feedbackContainer = new VisualElement()
            {
                style = {
                    flexDirection = FlexDirection.Column,
                }
            };

            // Love it? Rate button
            var rateButton = CreateFeedbackButton(
                "Love it? Please Rate",
                "Your support means EVERYTHING, thank you!",
                new Color(0.15f, 0.7f, 0.15f, 1f),
                () =>
                {
                    Application.OpenURL(ratingUrl);
                    DevLog("Opening rating page");
                }
            );
            feedbackContainer.Add(rateButton);

            // Bug report button
            var bugButton = CreateFeedbackButton(
                "Found a bug? Report it.",
                "Report bugs and issues to help us improve",
                new Color(0.8f, 0.25f, 0.25f, 1f),
                () =>
                {
                    string url = bugUrl.Replace("{VERSION}", version).Replace("{UNITY_VERSION}", Application.unityVersion);
                    Application.OpenURL(url);
                    DevLog("Opening bug report page");
                }
            );
            feedbackContainer.Add(bugButton);

            // Feature request button
            var featureButton = CreateFeedbackButton(
                "Big idea? Let us know!",
                "Suggest new features and improvements",
                new Color(0.2f, 0.5f, 0.8f, 1f),
                () =>
                {
                    string url = featureUrl.Replace("{VERSION}", version).Replace("{UNITY_VERSION}", Application.unityVersion);
                    Application.OpenURL(url);
                    DevLog("Opening feature request page");
                }
            );
            feedbackContainer.Add(featureButton);

            container.Add(feedbackContainer);

            // Contact buttons row
            var contactContainer = new VisualElement()
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceAround,
                    marginBottom = 2,
                    marginTop = 2,
                }
            };

            // Email button
            var emailButton = CreateFeedbackButton(
                "Email",
                "Send us an email directly",
                new Color(0.6f, 0.4f, 0.8f, 1f),
                () =>
                {
                    string emailSubject = Uri.EscapeDataString($"{moduleName} Question");
                    Application.OpenURL($"mailto:shrinkrayentertainment@gmail.com?subject={emailSubject}&body=Hello!");
                    DevLog("Opening email");
                }
            );
            contactContainer.Add(emailButton);

            // Discord button
            var discordButton = CreateFeedbackButton(
                "Discord",
                "Join our Discord community",
                new Color(0.35f, 0.4f, 0.85f, 1f),
                () =>
                {
                    Application.OpenURL("https://discord.gg/kCWvkTax37");
                    DevLog("Opening Discord");
                }
            );
            contactContainer.Add(discordButton);

            // Forum button
            var forumButton = CreateFeedbackButton(
                "Forum",
                "Visit the Unity Forum",
                new Color(0.8f, 0.6f, 0.2f, 1f),
                () =>
                {
                    Application.OpenURL("https://forum.unity.com/");
                    DevLog("Opening Unity Forum");
                }
            );
            contactContainer.Add(forumButton);

            container.Add(contactContainer);

            return container;
        }

        public class TimestampedSlider : VisualElement
        {
            public MinMaxSlider timeSlider;
            private VisualElement markerContainer, laneContainer;
            private List<VisualElement> lanes;
            private Dictionary<VisualElement, int> laneMarkers = new Dictionary<VisualElement, int>();
            private VisualElement highlighter;
            private VisualElement axis;
            public long startTicks, endTicks, startSelectedTicks, endSelectedTicks;
            private int numberOfLanes;
            private int maxTime;
            private float estimatedRightLabelWidth;

            // Constructor
            public TimestampedSlider(long startTicks, long endTicks, int numberOfLanes)
            {
                this.name = "timestamped_slider";
                SetupElements(); // Setup the static elements
                UpdateTimeline(startTicks, endTicks, numberOfLanes); // Initialize with the given parameters
            }


            // Setup static elements that don't need to be recreated on update
            private void SetupElements()
            {
                DevLog($"SetupElements");
                // Main container
                this.style.flexDirection = FlexDirection.Column;
                this.style.alignItems = Align.Stretch;
                this.style.flexGrow = 1;
                this.style.flexShrink = 1;

                // Min/Max Slider
                timeSlider = new MinMaxSlider(0, 0, 0, 0) // Temporarily initialize with dummy values
                {
                    value = new Vector2(0, 0)
                };
                timeSlider.RegisterValueChangedCallback(evt =>
                {
                    UpdateHighlighter(evt.newValue);
                    UpdateLaneHighlights(evt.newValue);
                    // UpdateIconPositions();
                });
                this.Add(timeSlider);

                // Lane container
                laneContainer = new VisualElement();
                this.Add(laneContainer);

                // Highlighter element
                highlighter = new VisualElement
                {
                    name = "highlighter",
                    style =
                    {
                        position = Position.Absolute,
                        backgroundColor = new Color(1f, 1f, 0f, 0.5f), // Yellow color, for example
                        height = new Length(100, LengthUnit.Percent)
                    }
                };
                laneContainer.Add(highlighter);

                // Marker container
                markerContainer = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        justifyContent = Justify.SpaceBetween
                    }
                };
                this.Add(markerContainer);

                // Axis for grid lines and labels
                axis = new VisualElement
                {
                    name = "playthrough axis",
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        justifyContent = Justify.SpaceBetween,
                        height = 20,
                    }
                };
                this.Add(axis);

                laneContainer.RegisterCallback<GeometryChangedEvent>((evt) =>
                {
                    schedule.Execute(UpdateIconPositions).StartingIn(1);
                    // UpdateIconPositions(); // This will update positions whenever the lane container's size changes
                });

            }


            // Method to update or initialize timeline
            public void UpdateTimeline(long startTicks, long endTicks, int numberOfLanes)
            {

                this.startTicks = startTicks;
                this.endTicks = endTicks;
                this.numberOfLanes = numberOfLanes;
                this.maxTime = (int)((endTicks - startTicks) / TimeSpan.TicksPerSecond);
                if (maxTime < 0) maxTime = 0;
                // DevLog($"UpdateTimeline {startTicks} {endTicks} {numberOfLanes} {maxTime}");
                // Debug.Log($"maxvalue {timeSlider.maxValue} {maxTime} / minvalue {timeSlider.minValue} / highlimit {timeSlider.highLimit} / lowlimit {timeSlider.lowLimit} / value {timeSlider.value} / {timeSlider.value.x} {timeSlider.value.y}");
                // Update slider range and reset value
                timeSlider.maxValue = maxTime;
                timeSlider.minValue = 0;
                timeSlider.highLimit = maxTime;
                timeSlider.lowLimit = 0;
                timeSlider.value = new Vector2(0, maxTime);

                // Clear and reinitialize lanes
                lanes?.ForEach(lane => laneContainer.Remove(lane));
                lanes = new List<VisualElement>();
                for (int i = 0; i < numberOfLanes; i++)
                {
                    var lane = new VisualElement
                    {
                        name = $"lane_{i}",
                        style =
                        {
                            height = 20,
                            backgroundColor = new Color(0.9f, 0.9f, 0.9f, 0.5f),
                            unityBackgroundImageTintColor = new Color(1, 1, 1, 0.1f),
                            borderBottomWidth = 1,
                            borderBottomColor = Color.black
                        }
                    };
                    lanes.Add(lane);
                    laneContainer.Add(lane);
                }

                // Clear existing grid lines, labels, and markers
                markerContainer.Clear();
                axis.Clear();
                laneMarkers.Clear();

                var label = new Label();
                schedule.Execute(() =>
                {
                    string formattedTime = FormatTime(maxTime);
                    label = new Label(formattedTime)
                    {
                        style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 10, // Smaller font size for better fit
                        position = Position.Absolute
                    }
                    };
                    // Debug.Log($"label.resolvedStyle.width: {label.resolvedStyle.width} label.style.width: {label.style.width}");
                }).Until(() => label.resolvedStyle.width > 0);
                estimatedRightLabelWidth = label.resolvedStyle.width;

                schedule.Execute(() =>
                {
                    // Redo the timeline initialization
                    if (this.resolvedStyle.width > 0)
                    {
                        InitializeTimeline(this.resolvedStyle.width);
                        UpdateHighlighter(timeSlider.value);
                    }
                }).Until(() => this.resolvedStyle.width > 0);
            }

            private void InitializeTimeline(float width)
            {
                int interval = CalculateProportionalInterval(width, maxTime);

                // Clear existing grid lines and labels before adding new ones
                markerContainer.Clear();
                axis.Clear();

                for (int i = 0; i <= maxTime; i += interval)
                {
                    float relativePosition = (float)i / maxTime;
                    AddGridLineAndLabel(i, relativePosition);
                }
            }


            private int CalculateProportionalInterval(float width, int maxTime)
            {
                float estimatedLabelWidth = 40; // Estimate the width of each label
                float minSpaceBetweenLabels = 10; // Minimum space between labels

                int interval = 1; // Start with the smallest interval (1 second)
                while (true)
                {
                    int labelCount = (maxTime / interval) + 1;
                    float requiredWidth = labelCount * (estimatedLabelWidth + minSpaceBetweenLabels);

                    if (requiredWidth <= width || interval * 2 > maxTime)
                    {
                        break; // Suitable interval found
                    }

                    interval *= 2; // Increase the interval
                }

                return interval;
            }



            private void AddGridLineAndLabel(int time, float relativePosition)
            {
                // Create and add a grid line
                var gridLine = new VisualElement
                {
                    style =
                    {
                        width = 1,
                        height = new Length(100, LengthUnit.Percent),
                        backgroundColor = Color.gray,
                        position = Position.Absolute,
                        left = new Length(relativePosition * 100, LengthUnit.Percent)
                    }
                };
                markerContainer.Add(gridLine);

                // Create and add a label
                string formattedTime = FormatTime(time);
                var label = new Label(formattedTime)
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 10,
                        position = Position.Absolute
                    }
                };

                if (relativePosition == 0) // Start time
                {
                    label.style.left = 0;
                }
                else if (relativePosition == 1) // We're keeping the end check just in case you decide to add the rightmost label later
                {
                    label.style.right = 0;
                }
                else
                {
                    label.style.left = new Length((relativePosition * 100) - 2, LengthUnit.Percent); // Simple adjustment for label's centering
                }

                axis.Add(label);
            }


            private string FormatTime(int timeInSeconds)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(timeInSeconds);
                return timeSpan.ToString(@"m\:ss");
            }

            public void UpdateHighlighter(Vector2 range)
            {
                float startPercent = range.x / maxTime * 100;
                float endPercent = range.y / maxTime * 100;

                // Update highlighter visual position and width
                highlighter.style.left = new Length(startPercent, LengthUnit.Percent);
                highlighter.style.width = new Length(endPercent - startPercent, LengthUnit.Percent);

                // Calculate selected ticks based on slider values
                long rangeTicks = endTicks - startTicks;
                startSelectedTicks = startTicks + (long)(range.x / maxTime * rangeTicks);
                endSelectedTicks = startTicks + (long)(range.y / maxTime * rangeTicks);

                float s1 = (float)((startSelectedTicks - startTicks) / TimeSpan.TicksPerSecond);
                float s2 = (float)((endSelectedTicks - startTicks) / TimeSpan.TicksPerSecond);
                // Debug.Log($"41359 Selected range: {s1} - {s2} seconds / {startSelectedTicks} - {endSelectedTicks} ticks");

                sceneData.RunPlaybackSelectedMin = startSelectedTicks;
                sceneData.RunPlaybackSelectedMax = endSelectedTicks;

                ClearMarkers?.Invoke();

                UpdateMiniMap?.Invoke();
                RebuildMappables?.Invoke();

                UpdateMarkers?.Invoke();
                RefreshSceneView?.Invoke();

                // Do not call UpdateIconPositions here; it will be called elsewhere if needed.
            }


            public void UpdateLaneHighlights(Vector2 range)
            {
                // Calculate the percentage range for the visible area
                float startPercent = range.x / maxTime;
                float endPercent = range.y / maxTime;

                schedule.Execute(() =>
                {
                    for (int i = 0; i < lanes.Count; i++)
                    {
                        var lane = lanes[i];
                        for (int j = 0; j < lane.childCount; j++)
                        {
                            var child = lane[j];
                            if (child.userData is float childPositionPercentage)
                            {
                                // Update the opacity based on whether the icon falls within the range
                                child.style.opacity = (childPositionPercentage >= startPercent && childPositionPercentage <= endPercent) ? 1 : 0.5f;
                            }
                        }
                    }
                    // Make sure the icons are correctly positioned
                    UpdateIconPositions();
                }).Until(() => laneContainer.resolvedStyle.width > 0);
            }


            public void AddTimestamp(long eventTicks, int laneIndex, VisualElement icon, string tooltip, Action<VisualElement> onClick = null)
            {
                if (laneIndex >= 0 && laneIndex < numberOfLanes)
                {
                    var lane = lanes[laneIndex];
                    // Calculate the position as a percentage of the lane width
                    float positionPercentage = GetPositionPercentage(eventTicks);
                    icon.style.position = Position.Absolute;
                    // Position will be set in UpdateIconPositions to handle initial placement and resizing
                    icon.userData = positionPercentage; // Store position percentage in userData
                    int eventTimeInSeconds = (int)((eventTicks - startTicks) / TimeSpan.TicksPerSecond);
                    //icon.tooltip = $"{eventTicks} / {eventTimeInSeconds}";//tooltip;
                    if (onClick != null)
                    {
                        icon.RegisterCallback<MouseDownEvent>(evt => onClick(icon));
                    }
                    if (!laneMarkers.ContainsKey(icon))
                    {
                        laneMarkers.Add(icon, laneIndex);
                        lane.Add(icon);
                    }
                    UpdateIconPositions(); // Ensure positions are updated
                }
            }
            private float GetPositionPercentage(long eventTicks)
            {
                float eventTimeInSeconds = (float)(eventTicks - startTicks) / TimeSpan.TicksPerSecond;
                float totalTimeInSeconds = (float)(endTicks - startTicks) / TimeSpan.TicksPerSecond;

                return eventTimeInSeconds / totalTimeInSeconds;
            }

            private void UpdateIconPositions()
            {
                // Schedule the update to ensure layout is computed
                schedule.Execute(() =>
                {
                    float laneWidth = laneContainer.resolvedStyle.width; // Get the current lane width

                    foreach (var lane in lanes)
                    {
                        foreach (var icon in lane.Children())
                        {
                            if (icon.userData is float positionPercentage)
                            {
                                float newPosition = positionPercentage * laneWidth; // Use the current lane width
                                icon.style.left = newPosition - (icon.resolvedStyle.width / 2); // Center the icon
                            }
                        }
                    }
                }).Until(() => laneContainer.resolvedStyle.width > 0); // Make sure the width is resolved
            }



        }

        #region Notifications
        private static string supabaseUrl = "https://nojjgqwmsfpalannmnun.supabase.co";
        private static string apiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im5vampncXdtc2ZwYWxhbm5tbnVuIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MTUxMjAzNzUsImV4cCI6MjAzMDY5NjM3NX0.-SQH01FO-BHdUIJ79LnmfUpGcRbt6rt4wBkJPVF9hyo";

        public delegate void NewsFetchComplete();

        public static void HandleNewsResponse(string json)
        {
            string jsonToParse = "{\"items\":" + json + "}";
            NewsItemsResponse response = JsonUtility.FromJson<NewsItemsResponse>(jsonToParse);

            var previousItems = dataCore.newsItems.ToList();
            var previousIds = previousItems.Select(item => item.id).ToHashSet();

            dataCore.newsItems.Clear();
            bool foundNewItems = false;

            foreach (var newItem in response.items)
            {
                if (newItem.expired) continue;

                newItem.isRead = false;
                dataCore.newsItems.Add(newItem);

                if (!previousIds.Contains(newItem.id))
                {
                    foundNewItems = true;
                }
            }

            if (foundNewItems)
            {
                dataCore.hasUnreadNews = true;
            }
        }

        public static void GetLatestNews(NewsFetchComplete callback)
        {
            EditorCoroutineUtility.StartCoroutine(GetLatestNews_(callback), window);
        }

        static IEnumerator GetLatestNews_(NewsFetchComplete callback)
        {
            if (dataCore == null) { yield break; }
            if (dataCore.newsItems == null)
            {
                dataCore.newsItems = new List<NewsItem>();
            }

            DateTime lastChecked = new DateTime(1990, 1, 1);
            string formattedDate = lastChecked.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
            string uri = $"{supabaseUrl}/rest/v1/rpc/news_update?date_threshold={Uri.EscapeDataString(formattedDate)}";

            int retries = 0;
            int maxRetries = 3;
            while (retries < maxRetries)
            {
                var www = new UnityWebRequest(uri, "GET");
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("apikey", apiKey);
                www.SetRequestHeader("Authorization", "Bearer " + apiKey);
                www.timeout = 10;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    HandleNewsResponse(www.downloadHandler.text);
                    callback?.Invoke();
                    break;
                }
                else
                {
                    retries++;
                    yield return new WaitForSeconds(1);
                }
                if (retries >= maxRetries)
                {
                    callback?.Invoke();
                }
            }
        }

        public static void HandleUpdatesResponse(string json)
        {
            string jsonToParse = "{\"items\":" + json + "}";
            // Debug.Log(jsonToParse);
            ModuleUpdateResponse response = JsonUtility.FromJson<ModuleUpdateResponse>(jsonToParse);
            dataCore.moduleUpdates.Clear();
            dataCore.moduleUpdates.AddRange(response.items);
        }

        public static void CheckUpdates(NewsFetchComplete callback)
        {
            // Debug.Log("Fetching updates...");
            EditorCoroutineUtility.StartCoroutine(CheckUpdates_(callback), window);
        }

        static IEnumerator CheckUpdates_(NewsFetchComplete callback)
        {
            // string uri = $"{supabaseUrl}/rest/v1/getnews?select=*";
            //uri += "&order=date_posted.desc&limit=1"; // Orders by date descending and limits to 1 item
            // DateTime lastChecked = new DateTime(2024, 5, 3);
            DateTime lastChecked;
            if (dataCore == null) yield break;
            if (dataCore.newsItems == null) yield break;
            if (dataCore.newsItems.Count == 0) yield break;

            if (dataCore.newsItems.Any(item => !string.IsNullOrEmpty(item.date_posted)))
            {
                lastChecked = dataCore.newsItems
                    .Where(item => !string.IsNullOrEmpty(item.date_posted))
                    .Max(item => DateTime.Parse(item.date_posted));
            }
            else
            {
                lastChecked = new DateTime(1990, 1, 1); // Default to Jan 1, 1990, if no valid dates are found
            }

            string formattedDate = lastChecked.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
            string uri = $"{supabaseUrl}/rest/v1/rpc/modules_update?date_threshold={Uri.EscapeDataString(formattedDate)}";
            // Debug.Log($"uri: {uri}");


            // var www = new UnityWebRequest(uri, "GET");
            // www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            // www.SetRequestHeader("Content-Type", "application/json");
            // www.SetRequestHeader("apikey", apiKey);
            // www.SetRequestHeader("Authorization", "Bearer " + apiKey);

            // yield return www.SendWebRequest();
            // Debug.Log(www.downloadHandler.text);
            // Debug.Log($"Status code: {www.responseCode}");
            // Debug.Log($"Error: {www.error}");



            int retries = 0;
            int maxRetries = 15;
            while (retries < maxRetries)
            {
                var www = new UnityWebRequest(uri, "GET");
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("apikey", apiKey);
                www.SetRequestHeader("Authorization", "Bearer " + apiKey);

                yield return www.SendWebRequest();
                // Debug.Log($"www.status: {www.result}");
                //!(www.result == UnityWebRequest.Result.ConnectionError) && !(www.result == UnityWebRequest.Result.ProtocolError) && 
                if (www.result == UnityWebRequest.Result.Success) // Success
                {
                    // Debug.Log(www.downloadHandler.text);
                    HandleUpdatesResponse(www.downloadHandler.text);

                    break; // Exit the loop on success
                }
                else
                {
                    // Debug.LogError($"Attempt {retries + 1}: Error fetching news: {www.error}");
                    retries++;
                    yield return new WaitForSeconds(Mathf.Pow(2, retries)); // Exponential backoff
                }
                if (retries >= maxRetries)
                {
                    // Debug.LogError("Max retries exceeded.");
                    callback?.Invoke(); // Optionally invoke callback on failure too
                }
            }
        }

        #endregion

        public static List<Texture2D> tutorialImages = new();

        public static void LoadTutorialImages(string prefix, int count)
        {
            tutorialImages.Clear();
            for (int i = 1; i <= count; i++)
            {
                int zeroPad = 0;
                if (i > 9) zeroPad = 1;
                var texture = Resources.Load<Texture2D>($"{prefix}_{zeroPad}{i}");
                Debug.Log($"Loading texture {i}: {prefix}_{zeroPad}{i} - Success: {texture != null}, Size: {(texture != null ? $"{texture.width}x{texture.height}" : "N/A")}");
                tutorialImages.Add(texture);
            }
        }

        public static VisualElement CreateTutorialCard(string title, string description, string actionText = "", System.Action action = null, Texture2D image = null)
        {
            VisualElement card = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Column,
                    paddingBottom = 20,
                    paddingLeft = 20,
                    paddingRight = 20,
                    paddingTop = 20,
                    marginBottom = 16,
                    backgroundColor = Color.white,
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12,
                    borderBottomRightRadius = 12,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderTopWidth = 1,
                    borderBottomColor = new Color(0.9f, 0.9f, 0.9f, 1f),
                    borderLeftColor = new Color(0.9f, 0.9f, 0.9f, 1f),
                    borderRightColor = new Color(0.9f, 0.9f, 0.9f, 1f),
                    borderTopColor = new Color(0.9f, 0.9f, 0.9f, 1f),
                }
            };

            // Generate a unique color for this step based on its title
            Color stepColor = StringToColor(title, 0.8f);

            VisualElement headerContainer = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.Center,
                    backgroundColor = stepColor,
                    paddingLeft = 20,
                    paddingRight = 20,
                    paddingTop = 12,
                    paddingBottom = 12,
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    marginLeft = -20,
                    marginRight = -20,
                    marginTop = -20,
                    marginBottom = 8,
                }
            };

            Label stepTitle = new Label(title)
            {
                style = {
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                }
            };

            headerContainer.Add(stepTitle);
            card.Add(headerContainer);

            // Add image if provided
            if (image != null)
            {
                Debug.Log($"Creating image for step '{title}': Size: {image.width}x{image.height}, Valid: {image != null}");

                VisualElement imageContainer = new VisualElement
                {
                    style = {
                        width = Length.Percent(100),
                        alignItems = Align.Center,
                        justifyContent = Justify.Center,
                    }
                };

                // Get the card's content width (accounting for padding)
                float cardWidth = card.style.width.value.value;
                float padding = 40; // Total horizontal padding (20px left + 20px right)
                float maxWidth = cardWidth - padding;

                float aspectRatio = (float)image.height / image.width;
                float imageWidth = Mathf.Min(image.width, maxWidth);
                float imageHeight = imageWidth * aspectRatio;

                Debug.Log($"Image dimensions - CardWidth: {cardWidth}, MaxWidth: {maxWidth}, Calculated Width: {imageWidth}, Height: {imageHeight}, Aspect: {aspectRatio}");

                Image imageElement = new Image
                {
                    image = image,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = {
                        width = imageWidth,
                        height = imageHeight,
                    }
                };

                imageContainer.Add(imageElement);
                card.Add(imageContainer);
            }

            Label descriptionLabel = new Label(description)
            {
                style = {
                    fontSize = 13,
                    color = new Color(0.4f, 0.4f, 0.4f, 1f),
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 16,
                    marginTop = 8,
                }
            };

            card.Add(descriptionLabel);

            // Only add action button if actionText is not empty
            if (action != null && !string.IsNullOrEmpty(actionText))
            {
                Button actionButton = new Button(() => action?.Invoke())
                {
                    text = actionText,
                    style = {
                        height = 32,
                        backgroundColor = new Color(0.3f, 0.6f, 0.9f, 1f),
                        color = Color.white,
                        borderTopLeftRadius = 16,
                        borderTopRightRadius = 16,
                        borderBottomLeftRadius = 16,
                        borderBottomRightRadius = 16,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        fontSize = 12,
                        unityFontStyleAndWeight = FontStyle.Bold,
                    }
                };

                actionButton.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    actionButton.style.backgroundColor = new Color(0.25f, 0.55f, 0.85f, 1f);
                });
                actionButton.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    actionButton.style.backgroundColor = new Color(0.3f, 0.6f, 0.9f, 1f);
                });

                card.Add(actionButton);
            }

            return card;
        }

        public static VisualElement CreateWelcomeCard(string title, string description)
        {
            VisualElement card = CreateTutorialCard(title, description);
            return card;
        }

        public static VisualElement CreateTipsCard(string title, string[] tips)
        {
            VisualElement card = CreateTutorialCard(title, "");

            VisualElement tipsList = new VisualElement
            {
                style = { flexDirection = FlexDirection.Column }
            };

            foreach (string tip in tips)
            {
                Label tipLabel = new Label(tip)
                {
                    style = {
                        fontSize = 12,
                        color = new Color(0.4f, 0.4f, 0.4f, 1f),
                        whiteSpace = WhiteSpace.Normal,
                        marginBottom = 6,
                    }
                };
                tipsList.Add(tipLabel);
            }

            card.Add(tipsList);
            return card;
        }

        public static VisualElement CreateCalloutCard(string message)
        {
            VisualElement calloutCard = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    paddingBottom = 12,
                    paddingLeft = 16,
                    paddingRight = 16,
                    paddingTop = 12,
                    marginBottom = 16,
                    backgroundColor = new Color(0.95f, 0.95f, 1f, 1f),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderTopWidth = 1,
                    borderBottomColor = new Color(0.7f, 0.7f, 0.9f, 1f),
                    borderLeftColor = new Color(0.7f, 0.7f, 0.9f, 1f),
                    borderRightColor = new Color(0.7f, 0.7f, 0.9f, 1f),
                    borderTopColor = new Color(0.7f, 0.7f, 0.9f, 1f),
                    alignItems = Align.Center,
                }
            };

            Label calloutText = new Label(message)
            {
                style = {
                    fontSize = 12,
                    color = new Color(0.3f, 0.3f, 0.6f, 1f),
                    whiteSpace = WhiteSpace.Normal,
                    flexGrow = 1,
                }
            };

            calloutCard.Add(calloutText);
            return calloutCard;
        }

        public class ToggleButton : Button
        {
            private bool _isToggled;
            public bool IsToggled
            {
                get { return _isToggled; }
                set
                {
                    if (_isToggled != value)
                    {
                        _isToggled = value;
                        UpdateVisualState();
                    }
                }
            }

            public ToggleButton()
            {
                clicked += () =>
                {
                    IsToggled = !IsToggled;
                };
            }

            private void UpdateVisualState()
            {
                if (_isToggled)
                {
                    style.backgroundColor = new Color(0.3f, 0.6f, 0.9f, 1f);
                    style.color = Color.white;
                }
                else
                {
                    style.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                    style.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                }
            }
        }

        #region Module Lifecycle Management

        /// <summary>
        /// Defines the lifecycle methods that modules should implement for proper initialization, shutdown, and restart capabilities
        /// </summary>
        public interface IModuleLifecycle
        {
            /// <summary>
            /// Module's unique identifier
            /// </summary>
            string ModuleId { get; }

            /// <summary>
            /// Module's display name
            /// </summary>
            string ModuleName { get; }

            /// <summary>
            /// Current lifecycle state of the module
            /// </summary>
            ModuleLifecycleState State { get; }

            /// <summary>
            /// Initialize the module (load data, setup references, but don't subscribe to events yet)
            /// </summary>
            void Initialize();

            /// <summary>
            /// Start the module (subscribe to events, start processing)
            /// </summary>
            void Start();

            /// <summary>
            /// Stop the module (unsubscribe from events, pause processing, but keep data)
            /// </summary>
            void Stop();

            /// <summary>
            /// Shutdown the module completely (cleanup all resources, clear data)
            /// </summary>
            void Shutdown();

            /// <summary>
            /// Restart the module (Stop -> Initialize -> Start)
            /// </summary>
            void Restart();

            /// <summary>
            /// Check if the module is in a healthy state
            /// </summary>
            bool IsHealthy();

            /// <summary>
            /// Get any error messages from the module
            /// </summary>
            string GetLastError();
        }

        /// <summary>
        /// Represents the current state of a module's lifecycle
        /// </summary>
        public enum ModuleLifecycleState
        {
            Uninitialized,
            Initializing,
            Initialized,
            Starting,
            Started,
            Stopping,
            Stopped,
            ShuttingDown,
            Shutdown,
            Error
        }

        /// <summary>
        /// Centralized manager for all module lifecycles
        /// </summary>
        public static class ModuleManager
        {
            private static readonly List<IModuleLifecycle> registeredModules = new List<IModuleLifecycle>();
            private static readonly Dictionary<string, IModuleLifecycle> moduleRegistry = new Dictionary<string, IModuleLifecycle>();
            private static bool isInitialized = false;
            private static bool isStarted = false;

            /// <summary>
            /// Event fired when all modules have been initialized
            /// </summary>
            public static event Action AllModulesInitialized;

            /// <summary>
            /// Event fired when all modules have been started
            /// </summary>
            public static event Action AllModulesStarted;

            /// <summary>
            /// Event fired when all modules have been stopped
            /// </summary>
            public static event Action AllModulesStopped;

            /// <summary>
            /// Event fired when all modules have been shutdown
            /// </summary>
            public static event Action AllModulesShutdown;

            /// <summary>
            /// Register a module with the lifecycle manager
            /// </summary>
            public static void RegisterModule(IModuleLifecycle module)
            {
                if (module == null)
                {
                    DevLogError("Cannot register null module");
                    return;
                }

                if (moduleRegistry.ContainsKey(module.ModuleId))
                {
                    DevLogWarning($"Module {module.ModuleId} is already registered. Replacing with new instance.");
                    UnregisterModule(module.ModuleId);
                }

                registeredModules.Add(module);
                moduleRegistry[module.ModuleId] = module;

                DevLog($"Module registered: {module.ModuleName} ({module.ModuleId})");

                // If system is already initialized/started, initialize/start this module immediately
                if (isInitialized && module.State == ModuleLifecycleState.Uninitialized)
                {
                    InitializeModule(module);
                }

                if (isStarted && module.State == ModuleLifecycleState.Initialized)
                {
                    StartModule(module);
                }
            }

            /// <summary>
            /// Unregister a module from the lifecycle manager
            /// </summary>
            public static void UnregisterModule(string moduleId)
            {
                if (moduleRegistry.TryGetValue(moduleId, out IModuleLifecycle module))
                {
                    // Ensure module is properly shutdown before unregistering
                    if (module.State != ModuleLifecycleState.Shutdown && module.State != ModuleLifecycleState.Uninitialized)
                    {
                        ShutdownModule(module);
                    }

                    registeredModules.Remove(module);
                    moduleRegistry.Remove(moduleId);

                    DevLog($"Module unregistered: {module.ModuleName} ({moduleId})");
                }
            }

            /// <summary>
            /// Get a registered module by ID
            /// </summary>
            public static IModuleLifecycle GetModule(string moduleId)
            {
                moduleRegistry.TryGetValue(moduleId, out IModuleLifecycle module);
                return module;
            }

            /// <summary>
            /// Get all registered modules
            /// </summary>
            public static IReadOnlyList<IModuleLifecycle> GetAllModules()
            {
                return registeredModules.AsReadOnly();
            }

            /// <summary>
            /// Initialize all registered modules
            /// </summary>
            public static void InitializeAllModules()
            {
                DevLog("Initializing all modules...");

                var modulesToInitialize = registeredModules.Where(m => m.State == ModuleLifecycleState.Uninitialized).ToList();

                foreach (var module in modulesToInitialize)
                {
                    InitializeModule(module);
                }

                isInitialized = true;
                AllModulesInitialized?.Invoke();

                DevLog($"All modules initialized. Total: {registeredModules.Count}");
            }

            /// <summary>
            /// Start all initialized modules
            /// </summary>
            public static void StartAllModules()
            {
                DevLog("Starting all modules...");

                // Get all modules that are either initialized or stopped
                var modulesToStart = registeredModules.Where(m =>
                    m.State == ModuleLifecycleState.Initialized ||
                    m.State == ModuleLifecycleState.Stopped).ToList();

                DevLog($"modulesToStart: {modulesToStart.Count}");


                foreach (var module in modulesToStart)
                {
                    // If module is stopped, initialize it first
                    if (module.State == ModuleLifecycleState.Stopped)
                    {
                        InitializeModule(module);
                    }
                    StartModule(module);
                }

                isStarted = true;
                AllModulesStarted?.Invoke();
                RunPlaybackMinMaxUpdated?.Invoke();
                MapInitialized?.Invoke();
                DevLog($"All modules started. Total: {registeredModules.Count}");
            }

            /// <summary>
            /// Stop all started modules
            /// </summary>
            public static void StopAllModules()
            {
                DevLog("Stopping all modules...");

                var modulesToStop = registeredModules.Where(m => m.State == ModuleLifecycleState.Started).ToList();

                // Stop in reverse order to handle dependencies
                for (int i = modulesToStop.Count - 1; i >= 0; i--)
                {
                    StopModule(modulesToStop[i]);
                }

                isStarted = false;
                AllModulesStopped?.Invoke();

                DevLog($"All modules stopped. Total: {registeredModules.Count}");
            }

            /// <summary>
            /// Shutdown all modules completely
            /// </summary>
            public static void ShutdownAllModules()
            {
                DevLog("Shutting down all modules...");

                var modulesToShutdown = registeredModules.Where(m => m.State != ModuleLifecycleState.Shutdown && m.State != ModuleLifecycleState.Uninitialized).ToList();

                // Shutdown in reverse order to handle dependencies
                for (int i = modulesToShutdown.Count - 1; i >= 0; i--)
                {
                    ShutdownModule(modulesToShutdown[i]);
                }

                isInitialized = false;
                isStarted = false;
                AllModulesShutdown?.Invoke();

                DevLog($"All modules shutdown. Total: {registeredModules.Count}");
            }

            /// <summary>
            /// Restart all modules (Stop -> Initialize -> Start)
            /// </summary>
            public static void RestartAllModules()
            {
                DevLog("Restarting all modules...");

                StopAllModules();
                InitializeAllModules();
                StartAllModules();

                DevLog("All modules restarted.");
            }

            /// <summary>
            /// Gracefully restart the entire system
            /// </summary>
            public static void GracefulSystemRestart()
            {
                DevLog("Performing graceful system restart...");

                // Stop all modules
                StopAllModules();

                // Clear any cached data or references that might cause issues
                ClearSystemCache();

                // Wait a frame to ensure all cleanup is complete
                EditorApplication.delayCall += () =>
                {
                    // Reinitialize and start all modules
                    InitializeAllModules();
                    StartAllModules();

                    DevLog("Graceful system restart completed.");
                };
            }

            /// <summary>
            /// Clear system-wide cached data
            /// </summary>
            private static void ClearSystemCache()
            {
                // Clear mappables list
                if (mappables != null)
                {
                    mappables.Clear();
                }

                // Clear any UI references that might be stale
                if (window?.rootVisualElement != null)
                {
                    window.rootVisualElement.Clear();
                }

                // Reset core flags
                // Note: Core flags will be reset by MightyCore.ClearSystemCache()
                DevLog("Core flags reset will be handled by MightyCore");

                DevLog("System cache cleared.");
            }

            /// <summary>
            /// Get health status of all modules
            /// </summary>
            public static Dictionary<string, bool> GetModuleHealthStatus()
            {
                var healthStatus = new Dictionary<string, bool>();

                foreach (var module in registeredModules)
                {
                    healthStatus[module.ModuleId] = module.IsHealthy();
                }

                return healthStatus;
            }

            /// <summary>
            /// Get error messages from all modules
            /// </summary>
            public static Dictionary<string, string> GetModuleErrors()
            {
                var errors = new Dictionary<string, string>();

                foreach (var module in registeredModules)
                {
                    string error = module.GetLastError();
                    if (!string.IsNullOrEmpty(error))
                    {
                        errors[module.ModuleId] = error;
                    }
                }

                return errors;
            }

            /// <summary>
            /// Initialize a specific module safely
            /// </summary>
            private static void InitializeModule(IModuleLifecycle module)
            {
                try
                {
                    DevLog($"Initializing module: {module.ModuleName}");
                    module.Initialize();
                    DevLog($"Module initialized: {module.ModuleName}");
                }
                catch (Exception ex)
                {
                    DevLogError($"Failed to initialize module {module.ModuleName}: {ex.Message}");
                }
            }

            /// <summary>
            /// Start a specific module safely
            /// </summary>
            private static void StartModule(IModuleLifecycle module)
            {
                try
                {
                    DevLog($"Starting module: {module.ModuleName}");
                    module.Start();
                    DevLog($"Module started: {module.ModuleName}");
                }
                catch (Exception ex)
                {
                    DevLogError($"Failed to start module {module.ModuleName}: {ex.Message}");
                }
            }

            /// <summary>
            /// Stop a specific module safely
            /// </summary>
            private static void StopModule(IModuleLifecycle module)
            {
                try
                {
                    DevLog($"Stopping module: {module.ModuleName}");
                    module.Stop();
                    DevLog($"Module stopped: {module.ModuleName}");
                }
                catch (Exception ex)
                {
                    DevLogError($"Failed to stop module {module.ModuleName}: {ex.Message}");
                }
            }

            /// <summary>
            /// Shutdown a specific module safely
            /// </summary>
            private static void ShutdownModule(IModuleLifecycle module)
            {
                try
                {
                    DevLog($"Shutting down module: {module.ModuleName}");
                    module.Shutdown();
                    DevLog($"Module shutdown: {module.ModuleName}");
                }
                catch (Exception ex)
                {
                    DevLogError($"Failed to shutdown module {module.ModuleName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Base class for modules that provides common lifecycle functionality
        /// </summary>
        public abstract class ModuleBase : IModuleLifecycle
        {
            protected ModuleLifecycleState state = ModuleLifecycleState.Uninitialized;
            protected string lastError = string.Empty;
            protected readonly List<Action> eventUnsubscribers = new List<Action>();

            public abstract string ModuleId { get; }
            public abstract string ModuleName { get; }
            public ModuleLifecycleState State => state;

            public virtual void Initialize()
            {
                if (state != ModuleLifecycleState.Uninitialized && state != ModuleLifecycleState.Shutdown && state != ModuleLifecycleState.Stopped)
                {
                    DevLogWarning($"Module {ModuleName} is already initialized (State: {state})");
                    return;
                }

                try
                {
                    state = ModuleLifecycleState.Initializing;
                    lastError = string.Empty;

                    OnInitialize();

                    state = ModuleLifecycleState.Initialized;
                    DevLog($"Module {ModuleName} initialized successfully");
                }
                catch (Exception ex)
                {
                    state = ModuleLifecycleState.Error;
                    lastError = ex.Message;
                    DevLogError($"Failed to initialize module {ModuleName}: {ex.Message}");
                    throw;
                }
            }

            public virtual void Start()
            {
                if (state != ModuleLifecycleState.Initialized)
                {
                    DevLogWarning($"Module {ModuleName} must be initialized before starting (State: {state})");
                    return;
                }

                try
                {
                    state = ModuleLifecycleState.Starting;

                    OnStart();

                    state = ModuleLifecycleState.Started;
                    DevLog($"Module {ModuleName} started successfully");
                }
                catch (Exception ex)
                {
                    state = ModuleLifecycleState.Error;
                    lastError = ex.Message;
                    DevLogError($"Failed to start module {ModuleName}: {ex.Message}");
                    throw;
                }
            }

            public virtual void Stop()
            {
                if (state != ModuleLifecycleState.Started)
                {
                    DevLogWarning($"Module {ModuleName} is not started (State: {state})");
                    return;
                }

                try
                {
                    state = ModuleLifecycleState.Stopping;

                    OnStop();
                    UnsubscribeFromAllEvents();

                    state = ModuleLifecycleState.Stopped;
                    DevLog($"Module {ModuleName} stopped successfully");
                }
                catch (Exception ex)
                {
                    state = ModuleLifecycleState.Error;
                    lastError = ex.Message;
                    DevLogError($"Failed to stop module {ModuleName}: {ex.Message}");
                    throw;
                }
            }

            public virtual void Shutdown()
            {
                try
                {
                    state = ModuleLifecycleState.ShuttingDown;

                    // Stop if currently started
                    if (state == ModuleLifecycleState.Started)
                    {
                        OnStop();
                    }

                    OnShutdown();
                    UnsubscribeFromAllEvents();

                    state = ModuleLifecycleState.Shutdown;
                    DevLog($"Module {ModuleName} shutdown successfully");
                }
                catch (Exception ex)
                {
                    state = ModuleLifecycleState.Error;
                    lastError = ex.Message;
                    DevLogError($"Failed to shutdown module {ModuleName}: {ex.Message}");
                    throw;
                }
            }

            public virtual void Restart()
            {
                DevLog($"Restarting module {ModuleName}...");

                if (state == ModuleLifecycleState.Started)
                {
                    Stop();
                }

                Initialize();
                Start();

                DevLog($"Module {ModuleName} restarted successfully");
            }

            public virtual bool IsHealthy()
            {
                return state == ModuleLifecycleState.Started && string.IsNullOrEmpty(lastError);
            }

            public virtual string GetLastError()
            {
                return lastError;
            }

            /// <summary>
            /// Helper method to safely subscribe to events and track unsubscribers
            /// </summary>
            protected void SafeSubscribe<T>(ref Action<T> eventAction, Action<T> handler)
            {
                eventAction -= handler; // Remove first to prevent duplicates
                eventAction += handler;

                // Store unsubscriber - capture the event action in a local variable
                var capturedAction = eventAction;
                eventUnsubscribers.Add(() => capturedAction -= handler);
            }

            /// <summary>
            /// Helper method to safely subscribe to events and track unsubscribers
            /// </summary>
            protected void SafeSubscribe(ref Action eventAction, Action handler)
            {
                eventAction -= handler; // Remove first to prevent duplicates
                eventAction += handler;

                // Store unsubscriber - capture the event action in a local variable
                var capturedAction = eventAction;
                eventUnsubscribers.Add(() => capturedAction -= handler);
            }

            /// <summary>
            /// Helper method to safely subscribe to Unity EditorApplication.CallbackFunction events
            /// </summary>
            protected void SafeSubscribeEditorCallback(ref EditorApplication.CallbackFunction eventAction, EditorApplication.CallbackFunction handler)
            {
                eventAction -= handler; // Remove first to prevent duplicates
                eventAction += handler;

                // Store unsubscriber - capture the event action in a local variable
                var capturedAction = eventAction;
                eventUnsubscribers.Add(() => capturedAction -= handler);
            }

            /// <summary>
            /// Unsubscribe from all tracked events
            /// </summary>
            protected void UnsubscribeFromAllEvents()
            {
                foreach (var unsubscriber in eventUnsubscribers)
                {
                    try
                    {
                        unsubscriber?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        DevLogError($"Error unsubscribing from event in module {ModuleName}: {ex.Message}");
                    }
                }
                eventUnsubscribers.Clear();
            }

            /// <summary>
            /// Override this method to implement module-specific initialization logic
            /// </summary>
            protected abstract void OnInitialize();

            /// <summary>
            /// Override this method to implement module-specific start logic
            /// </summary>
            protected abstract void OnStart();

            /// <summary>
            /// Override this method to implement module-specific stop logic
            /// </summary>
            protected abstract void OnStop();

            /// <summary>
            /// Override this method to implement module-specific shutdown logic
            /// </summary>
            protected abstract void OnShutdown();
        }

        #endregion

        #region IMappable
        // Legacy IMappable interface content will go here if needed

        #endregion
    }
}
#endif