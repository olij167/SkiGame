#if UNITY_EDITOR
using Mighty;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;
using static Mighty.MightyCoreData.SceneData;


namespace MightyScreenshots
{

    public class ScreenshotsData : ScriptableObject
    {
        [Serializable]
        public class Settings
        {
            public Color borderColor = new Color(0, 0, 0, 0);

            public KeyCode hotkey1 = KeyCode.LeftShift,
            hotkey2 = KeyCode.LeftControl,
            hotkey3 = KeyCode.LeftAlt;

            public ScreenshotResolution screenshotResolution = ScreenshotResolution.Medium_1080;
            public int screenshotQuality = 100;
            public ScreenshotFormat screenshotFormat = ScreenshotFormat.JPG;
            public MightySceneViewManager.Settings sceneView = new MightySceneViewManager.Settings();
        }

        public static void Save()
        {
            string path = $"{corePath}/Modules/Screenshots/Data/ScreenshotsData.asset";
            if (File.Exists(path))
            {
                DevLog($"{path} already exists...");
                return;
            }

            ScreenshotsData asset = ScriptableObject.CreateInstance<ScreenshotsData>();

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }

        public static ScreenshotsData Load()
        {
            string path = $"{corePath}/Modules/Screenshots/Data/ScreenshotsData.asset";
            // string fallbackPath = $"{corePath}/Modules/Screenshots/Data/ScreenshotsData_.asset";

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
            //return Resources.Load("ScreenshotModuleData", typeof(ScreenshotData)) as ScreenshotData;
            return AssetDatabase.LoadAssetAtPath<ScreenshotsData>(path);
        }


        [SerializeField]
        public Settings settings = new Settings();
        // [SerializeField]
        // public List<Screenshot.Root> Screenshots;
        [SerializeField]
        public List<SceneData> scenes;
        private static bool clickedFrontView = false;
        private static Texture2D thumbnail;


        [Serializable]
        public class SceneData
        {
            public string name;
            [SerializeField]
            public List<Screenshot.Root> Screenshots;
        }
        const string iconAddMappablePath = "mighty_icon_screenshot";
        Color defaultColor = new Color(1, 0, 0, 1f);
        [Serializable]
        public enum ScreenshotResolution
        {
            Low_640,
            Medium_1080,
            High_4k
        }

        [Serializable]
        public enum ScreenshotFormat
        {
            JPG,
            PNG
        }
        // float defaultScale = 1.5f;


        [Serializable]
        public class Screenshot
        {
            // [Serializable]
            // public class PlayTracking
            // {
            //     [SerializeField]
            //     public string name;

            //     // [SerializeField]
            //     // public String RunID;

            //     [SerializeField]
            //     public Color trackingColor;


            //     public List<Positional> positional;
            //     public List<Log> log;

            //     public PlayTracking(string n, Color color)
            //     {
            //         sceneData.RunIDList.Add(sceneData.RunID);
            //         // RunID = sceneData.RunID;
            //         name = $"{n} ({sceneData.RunID})";
            //         positional = new List<Positional>();
            //         trackingColor = color;
            //     }
            //     //
            //     public void AddPosition(Transform transform, Color color)
            //     {
            //         //   DevLog("AddData");
            //         if (sceneData.RunID == "")
            //         {
            //             DevLog("No run_id");
            //             return;
            //         }

            //         RunID = sceneData.RunID;
            //         trackingColor = color;

            //         positional.Add(new Positional()
            //         {
            //             name = "Screenshot taken on "+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            //             timeStamp = DateTime.Now.Ticks,
            //             position = transform.position,
            //             rotation = transform.rotation,
            //             scale = transform.localScale
            //         });



            //     }

            //     // public void AddLog(String text, Transform transform, Color tc, Color bgc)
            //     // {
            //     //     DevLog($"AddLog transform: {transform} tc: {tc} bgc: {bgc}");
            //     //     if (sceneData.RunID == "")
            //     //     {
            //     //         DevLog("No run_id");
            //     //         return;
            //     //     }

            //     //     RunID = sceneData.RunID;

            //     //     this.log.Add(new Log()
            //     //     {
            //     //         name = text,
            //     //         textColor = tc,
            //     //         backgroundColor = bgc,
            //     //         positional = new Positional()
            //     //         {
            //     //             name = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            //     //             timeStamp = DateTime.Now.Ticks,
            //     //             position = transform.position,
            //     //             rotation = transform.rotation,
            //     //             scale = transform.localScale
            //     //         }
            //     //     });


            //     // }

            //     [Serializable]
            //     public class Positional
            //     {
            //         [SerializeField]
            //         public string name;

            //         [SerializeField]
            //         public long timeStamp;
            //         [SerializeField]
            //         public Vector3 position;
            //         [SerializeField]
            //         public Quaternion rotation;
            //         [SerializeField]
            //         public Vector3 scale;

            //     }


            //     [Serializable]
            //     public class Log
            //     {
            //         [SerializeField]
            //         public string name;
            //         [SerializeField]
            //         public Positional positional;
            //         [SerializeField]
            //         public Color textColor, backgroundColor;
            //     }
            // }

            [Serializable]
            public class SceneViewLabel
            {
                public bool fade;
                public bool show;
                public Vector3 offset;
                public int fadeMax;
                public int fadeMin;
            }


            [Serializable]
            public class Root : IMappable
            {
                private string _version = "1.0.0";
                public string Version
                {
                    get => _version;
                    set => _version = value;
                }

                private string _packageName = "Mighty DevOps";
                public string PackageName
                {
                    get => _packageName;
                    set => _packageName = value;
                }

                public bool UpdateAvailable()
                {
                    // var updateInfo = dataCore.moduleUpdates.FirstOrDefault(update => update.module == "screenshots");
                    // if (updateInfo != null)
                    // {
                    //     return updateInfo.version != Version;
                    // }
                    return false;
                }

                public void InitViews()
                {
                    if (Pic == null) LoadImage();
                    //if (view == null) 
                    View = new VisualElement();

                    //                    views.SetRoot(view);
                    ViewUI = new Views();
                    ViewUI.AddView("Screenshot_front", 96, 96);
                    ViewUI.AddView("Screenshot_back", 256, 256);
                    ViewUI.AddView("Screenshot_back_s1", 192, 192);


                    var frontView = ViewUI.GetView("Screenshot_front");
                    var backView = ViewUI.GetView("Screenshot_back");
                    //var backs1View = ViewUI.GetView("Screenshot_back_s1");

                    DevLog($"InitViews {frontView}");
                    DevLog($"InitViews {backView}");
                    //DevLog($"InitViews {backs1View}");

                    frontView.AddToClassList("ss_anchor_" + anchorTo);
                    //frontView.AddToClassList("mappable");
                    frontView.name = Pic.filename;
                    frontView.styleSheets.Add(Resources.Load("UI/mightystyles", typeof(StyleSheet)) as StyleSheet);
                    //frontView.style.maxWidth = 128;

                    frontView.RegisterCallback<MouseEnterEvent>((evt) =>
                    {
                        //DevLog("Hovering!");
                        frontView.BringToFront();
                        //isHoveringOnMappable = true;
                        frontView.Query(className: "showOnHover")
                            .ForEach((element) =>
                            {
                                element.RemoveFromClassList("fadeOut");
                                element.AddToClassList("fadeIn");

                                string c = "";
                                foreach (var x in element.GetClasses())
                                {
                                    c += "," + x.ToString();
                                }
                                //DevLog($"{element.name}: {c}");
                            });
                    }
                    );


                    frontView.RegisterCallback<MouseLeaveEvent>((evt) =>
                    {
                        //                        DevLog("Leaving!");
                        //isHoveringOnMappable = false;
                        frontView.Query(className: "showOnHover").ForEach((element) =>
                        {
                            element.RemoveFromClassList("fadeIn");
                            element.AddToClassList("fadeOut");
                            string c = "";
                            foreach (var x in element.GetClasses())
                            {
                                c += "," + x.ToString();
                            }
                            //                          DevLog($"{element.name}: {c}");
                        });
                    }
                    );

                    frontView.visible = true;

                    var button = new Button();
                    button = frontView.Q<Button>(className: "delete");
                    {
                        button.tooltip = "Delete Screenshot";
                        button.name = "X";

                        button.clicked += () =>
                        {
                            Delete();
                            var v = root.Q<VisualElement>(name: Pic.filename);
                            if (v != null) v.RemoveFromHierarchy();
                            UpdateMappables();
                        };
                        button.visible = true;
                    }


                    button = frontView.Q<Button>(className: "gotoScreenshot");
                    if (button != null)
                    {
                        button.tooltip = "Go To Screenshot";
                        button.name = "GO";

                        button.clicked += () =>
                        {
                            ScreenshotsCore.core.SceneViewGoToPosition(MapLocation.worldPosition, MapLocation.worldRotation);
                        };
                        button.visible = true;
                    }

                    button = frontView.Q<Button>(className: "thumbnail");
                    {
                        Front = !Front;
                        button.tooltip = Name; //description;
                        button.name = Pic.filename;
                        //                        DevLog($"Creating button {pic.filename}");

                        if (Pic.img == null) LoadImage();
                        button.style.backgroundImage = Pic.img;
                        DevLog($"LLL Screenshot {Name} has image {Pic.img} and button.style.backgroundImage {button.style.backgroundImage}");

                        var currentViewUI = ViewUI;
                        button.clicked += () =>
                        {
                            if (clickedFrontView == true) return;
                            clickedFrontView = true;
                            Dirty = true;

                            currentViewUI.PushView("Screenshot_back");

                            var ScreenshotName = backView.Q<TextField>(name: "ScreenshotName");
                            if (ScreenshotName != null)
                            {
                                ScreenshotName.value = Name;
                                new EditableLabel(ScreenshotName, newValue => Name = newValue);
                            }


                            // var ScreenshotDescription = backView.Q<TextField>(name: "ScreenshotDescription");
                            // if (ScreenshotDescription != null)
                            // {
                            //     ScreenshotDescription.value = Description;
                            //     new EditableLabel(ScreenshotDescription, newValue => Description = newValue);
                            // }
                            //backView.AddToClassList("openHorizontal");

                            View.style.top = View.style.top.value.value + ((View.style.height.value.value - backView.style.height.value.value) / 2);
                            View.style.left = View.style.left.value.value + ((View.style.width.value.value - backView.style.width.value.value) / 2);
                            View.MarkDirtyRepaint();
                            RebuildMappables();
                            //Rebuild();
                            clickedFrontView = false;
                        };
                        button.visible = true;
                    }
                    thumbnail = button.style.backgroundImage.value.texture;
                    DevLog($"LLL Screenshot {Name} has thumbnail {thumbnail}");
                    button.MarkDirtyRepaint();

                    backView.RegisterCallback<WheelEvent>((evt) =>
                    {
                        evt.StopImmediatePropagation();
                    });

                    ToggleButton toggle = backView.Q<ToggleButton>(name: "ssAlwaysShow");
                    toggle.IsToggled = ShowAlways;

                    // Setup callbacks
                    toggle.clicked += () =>
                    {
                        ShowAlways = toggle.IsToggled;

                    };


                    button = backView.Q<Button>(name: "ssBack");
                    {
                        DevLog($"button is null? {button == null}");
                        Front = !Front;
                        // button.name = Pic.filename;
                        //                        DevLog($"Creating button {pic.filename}");
                        if (Pic.img == null) LoadImage();
                        button.style.backgroundColor = Color.white;
                        button.style.color = Color.black;
                        //button.style.backgroundImage = Pic.img;

                        var currentViewUI = ViewUI;

                        button.clicked += () =>
                        {
                            Dirty = true;
                            DevLog($"back on {Name}");

                            currentViewUI.PopView();

                            frontView.Q<Button>(className: "thumbnail").tooltip = Name;
                            View.MarkDirtyRepaint();
                            RebuildMappables();
                        };

                        button.visible = true;
                    }


                    button = backView.Q<Button>(name: "screenshot_large");
                    {
                        if (Pic.img == null) LoadImage();
                        button.style.backgroundImage = Pic.img;
                    }

                    button.RegisterCallback<ClickEvent>(evt =>
                    {
                        Pic.path = $"{Application.dataPath.Replace("Assets", "")}{GetCache()}{Pic.filename}";
                        if (!string.IsNullOrEmpty(Pic.path))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Pic.path) { UseShellExecute = true });
                        }
                    });

                    TextField textField = backView.Q<TextField>(name: "ssName");
                    {
                        textField.value = Name;
                        new EditableLabel(textField, newValue => Name = newValue);
                    }

                    // button = backs1View.Q<Button>(name: "ssBack");
                    // {
                    //     Front = !Front;
                    //     button.tooltip = Pic.filename; //description;
                    //     button.name = Pic.filename;
                    //     //                        DevLog($"Creating button {pic.filename}");

                    //     if (Pic.img == null) LoadImage();
                    //     //button.style.backgroundImage = Pic.img;

                    //     button.clicked += () =>
                    //     {
                    //         Dirty = true;
                    //         DevLog("back");

                    //         ViewUI.PopView();

                    //         View.MarkDirtyRepaint();
                    //         Rebuild();
                    //     };
                    //     button.visible = true;
                    // }


                    backView.AddToClassList(Pic.filename);

                    backView.name = Pic.filename;
                    backView.styleSheets.Add(Resources.Load("UI/mightystyles", typeof(StyleSheet)) as StyleSheet);
                    backView.visible = true;

                    View.Add(frontView);
                    View.Add(backView);

                    ViewUI.PushView("Screenshot_front");
                    View.MarkDirtyRepaint();
                }

                public void RegisterMappable()
                {
                    if (mappables.Contains(this)) return;
                    if (sceneData == null) ScreenshotsCore.GetSceneData();
                    if (AnchorTo == sceneData.Name)
                    {
                        PlayTracking playthrough = sceneData.PlayTrackingList.FirstOrDefault(x => x.name == sceneData.SelectedRun) as PlayTracking;
                        if (playthrough == null) return;

                        // if (ShowAlways || this.CreatedAt >= playthrough.startTicks && this.CreatedAt <= playthrough.endTicks)
                        // {
                        //     DevLog($"Screenshot {this.Name} is within the playthrough {sceneData.SelectedRun} at {playthrough.startTicks} to {playthrough.endTicks}");
                        //     DevLog($"Adding Mappable: {this.Name}");
                        //     // mappables.Add(this);
                        //     InitViews();
                        // }
                        // else
                        // {
                        //     DevLog($"Screenshot {this.Name} is NOT within the playthrough {sceneData.SelectedRun} at {playthrough.startTicks} to {playthrough.endTicks}");
                        // }

                        mappables.Add(this);
                        InitViews();
                    }
                    else
                    {
                        // Debug.Log($"Not Adding Mappable: {this.Name} as it is anchored to {AnchorTo} and not {sceneData.Name}");
                    }
                }

                public void OnGenerateVisualContent(MeshGenerationContext mgc)
                {
                    DevLog($"Screenshot OnGenerateVisualContent - {ViewUI.GetView().ToString()}");
                }

                public void CheckIntegrity()
                {
                    DevLog($"Screenshot CheckIntegrity of {Name} {Pic.filename} {CreatedAt} {Pic.format} {Pic.img} {Pic.path}");
                    VisualElement e = Map.Query<VisualElement>(name: Pic.filename);
                    if (e == null) return;
                    Button tn = e.Query<Button>(name: Pic.filename);

                    if (tn == null)
                    {
                        DevLog($"Screenshot {Name} is missing from the map");
                        //   mappables.Remove(this);
                    }
                    else
                    {
                        if (tn.style.backgroundImage.value.texture != Pic.img)
                        {
                            DevLog($"Screenshot {Name} has a different image");
                            tn.style.backgroundImage = Pic.img;
                            // InitViews();
                        }
                        // if (ss.style.backgroundImage.value.texture != Pic.img)
                        // {
                        //     DevLog($"Screenshot {Name} has a different image");
                        //     ss.style.backgroundImage = Pic.img;
                        // }
                    }

                    if (thumbnail == null)
                    {
                        InitViews();
                    }
                }
                public void LoadImage()
                {
                    DevLog("LLL LoadImage()");
                    if (Pic == null)
                    {
                        DevLogError($"LLL Pic is Null for Screenshot {Name}");
                        Pic = new Picture();
                    }
                    Pic.img = new Texture2D(1, 1);
                    //
                    if (Pic.filename == null)
                    {
                        Pic.filename = $"ss_{CreatedAt.ToString()}.{Pic.format}";

                    }
                    // Pic.filename = $"ss_{CreatedAt.ToString()}.jpg";
                    DevLog($"LLL Screenshot Pic Filename {Pic.filename}");
                    string fileName;
                    int lastIndex = Pic.filename.LastIndexOf('.');
                    if (lastIndex > -1)
                    {
                        fileName = Pic.filename.Substring(0, lastIndex);
                    }
                    else
                    {
                        fileName = Pic.filename;
                    }

                    Pic.img = Resources.Load($"Cache/{fileName}", typeof(Texture2D)) as Texture2D;
                    DevLog($"LLL Loaded Screenshot Image {fileName} and it is {Pic.img} with resolution {Pic.img.width}x{Pic.img.height}");
                }

                public void PopulatePlayTrackingLane(int laneIndex)
                {
                    DevLog("Screenshots PopulatePlayTrackingLane");
                    if (ScreenshotsCore.sceneData == null)
                    {
                        DevLog("Screenshots PopulatePlayTrackingLane: ScreenshotsCore.sceneData is null");
                        return;
                    }

                    VisualElement icon = new()
                    {
                        name = "icon",
                        style =
                        {
                            width = 16,
                            height = 16,
                            backgroundImage = icons.screenshotIcon,
                        }
                    };

                    var ptl = sceneData.PlayTrackingList;
                    if (ptl == null)
                    {
                        DevLog("Screenshots PopulatePlayTrackingLane: sceneData.PlayTrackingList is null");
                        return;
                    }

                    PlayTracking playthrough = ptl.FirstOrDefault(x => x.name == sceneData.SelectedRun) as PlayTracking;
                    if (playthrough == null) return;

                    var screenshots = ScreenshotsCore.sceneData.Screenshots;
                    if (screenshots == null)
                    {
                        DevLog("Screenshots PopulatePlayTrackingLane: ScreenshotsCore.sceneData.Screenshots is null");
                        return;
                    }

                    var filteredScreenshots = screenshots
                        .Where(screenshot => playthrough.startTicks <= screenshot.createdAt && playthrough.endTicks > screenshot.createdAt);

                    foreach (var screenshot in filteredScreenshots)
                    {
                        DateTime screenshotTime = new DateTime(screenshot.createdAt);
                        DateTime startPlaythroughTime = new DateTime(playthrough.startTicks);
                        DateTime endPlaythroughTime = new DateTime(playthrough.endTicks);
                        DevLog($"Screenshot {screenshot.Name} at {screenshotTime} in {sceneData.SelectedRun} at {startPlaythroughTime} to {endPlaythroughTime} in lane {laneIndex} created at {screenshotTime.AddSeconds(10)}");

                        // Create a new icon for each screenshot
                        VisualElement screenshotIcon = new()
                        {
                            name = "icon_" + screenshot.Name, // Unique name for debugging
                            style =
                            {
                                width = 16,
                                height = 16,
                                backgroundImage = icons.screenshotIcon,
                            }
                        };

                        Action<VisualElement> myAction = (VisualElement element) =>
                        {
                            // Your custom code here
                            // For example, let's change the background color of the element
                            ScreenshotsCore.core.SceneViewGoToPosition(screenshot.MapLocation.worldPosition, screenshot.MapLocation.worldRotation);

                            root.Q<Button>(name: "followSceneView").style.backgroundImage = icons.map_follow_sceneview_on;
                            ShowToast($"Zooming to {screenshot.Name}");
                            DevLog("Awesome Screenshot");
                        };

                        // Adjust the timestamp by adding 10 seconds as per original logic

                        timestampedSlider.AddTimestamp(screenshot.createdAt, laneIndex, screenshotIcon, screenshot.Name, myAction);
                        var currentSliderRange = timestampedSlider.timeSlider.value; // Assuming timeSlider is accessible here
                        timestampedSlider.UpdateHighlighter(currentSliderRange);
                        timestampedSlider.UpdateLaneHighlights(currentSliderRange);
                    }
                }

                public Button AddMappable(bool setClickedCallback = true)
                {
                    return null;
                    //     if (!sceneData.RecordPlaythrough)
                    //     {
                    //         return null;
                    //     }
                    //     DevLog($"mappable adding creation button");
                    //     if (iconAddMappable == null)
                    //         iconAddMappable = Resources.Load<Texture2D>(iconAddMappablePath);
                    //     DevLog($"mappable adding creation button {iconAddMappablePath} {iconAddMappable != null}");
                    //     Button b = new()
                    //     {
                    //         name = "addScreenshot",
                    //         tooltip = "Add Screenshot",
                    //         style = {
                    //             backgroundColor = new Color(0, 0, 0, 0),
                    //             borderBottomColor  = new Color(0, 0, 0, 0),
                    //             borderLeftColor  = new Color(0, 0, 0, 0),
                    //             borderRightColor  = new Color(0, 0, 0, 0),
                    //             borderTopColor  = new Color(0, 0, 0, 0),
                    // }
                    //     };

                    //     if (iconAddMappable != null)
                    //         b.style.backgroundImage = iconAddMappable;
                    //     else
                    //         b.text = "SS";

                    //     if (setClickedCallback)
                    //         b.clicked += () =>
                    //         {
                    //             DevLog("Screenshot YAY");
                    //             DevLog("New Screenshot");
                    //             if (ScreenshotsCore.sceneData.Screenshots == null) ScreenshotsCore.sceneData.Screenshots = new List<ScreenshotsData.Screenshot.Root>();
                    //             // MightyCoreData.mainCamera = Camera.main;
                    //             ScreenshotsCore.sceneData.Screenshots.Add(new ScreenshotsData.Screenshot.Root(Camera.main.transform.position.ToString(), Camera.main));
                    //             MightyCoreData.updatingMappables = false;
                    //             // MightyCoreData.rebuildingView = false;
                    //             Rebuild();

                    //         };

                    //
                    // return b;
                }

                public CustomToggleButton AddModuleToggle(MappableTypeInfo mappableTypeInfo)
                {
                    DevLog($"AddModuleToggle named {mappableTypeInfo.Name}");
                    return new(Icon, mappableTypeInfo, "ScreenshotOverlay");
                }

                public VisualElement SceneSummary(MightyCoreData.SceneData scene)
                {
                    VisualElement summary = new VisualElement
                    {
                        name = "Screenshots",
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            flexGrow = 0,
                            flexShrink = 0,
                            height = 80,
                        }
                    };

                    int count = 0;
                    foreach (var anchor in ScreenshotsCore.data.scenes)
                    {
                        if (anchor.name == scene.Name)
                        {
                            DevLog($"SceneSummary Found {anchor.name} in {scene.Name}");
                            foreach (var Screenshot in anchor.Screenshots)
                            {
                                count++;
                                DevLog($"SceneSummary Found {Screenshot.Name} in {scene.Name}");
                                Screenshot.LoadImage();

                                VisualElement pic = new()
                                {
                                    name = Screenshot.Name,
                                    style = {
                                        minWidth = 64,
                                        minHeight = 64,
                                        height = 64,
                                        width = 64,
                                        marginLeft = 4,
                                        marginRight = 4,
                                        flexGrow=0,
                                        flexShrink=0,
                                        backgroundImage = Screenshot.Pic.img
                                    }
                                };

                                // Create a larger version of the image
                                VisualElement largePic = new()
                                {
                                    name = Screenshot.Name,
                                    style = {
                                        width = 128,
                                        height = 128,
                                        backgroundImage = Screenshot.Pic.img,
                                        position = Position.Absolute,
                                        display = DisplayStyle.None
                                    }
                                };

                                Label ScreenshotName = new()
                                {
                                    text = Screenshot.Name,
                                    style = {
                                        fontSize = 14,
                                        backgroundColor = new Color(0, 0, 0, 0.5f),
                                        color = Color.white,
                                        unityTextAlign = TextAnchor.MiddleCenter,
                                    }
                                };

                                largePic.Add(ScreenshotName);
                                pic.RegisterCallback<MouseEnterEvent>((evt) =>
                                {
                                    largePic.style.display = DisplayStyle.Flex;
                                    root.Add(largePic);
                                });

                                pic.RegisterCallback<MouseMoveEvent>((evt) =>
                                {
                                    // Position the larger pic relative to the mouse cursor
                                    largePic.style.left = evt.mousePosition.x + 10;
                                    largePic.style.top = evt.mousePosition.y + 10;
                                });

                                pic.RegisterCallback<MouseLeaveEvent>((evt) =>
                                {
                                    largePic.style.display = DisplayStyle.None;
                                });

                                pic.RegisterCallback<MouseDownEvent>((evt) =>
                                {
                                    if (evt.button == 0)
                                    {
                                        if (EditorSceneManager.GetActiveScene().path != scene.ScenePath)
                                        {
                                            DevLog($"Opening Scene {scene.Name} from {EditorSceneManager.GetActiveScene().name}"); ;
                                            //need a dialog window to confirm if we open the scene or not
                                            if (EditorUtility.DisplayDialog(
                                                "Open Scene",
                                                $"Are you sure you want to open {scene.Name}?",
                                                "Yes",
                                                "No"
                                            ))
                                                EditorSceneManager.OpenScene(scene.ScenePath, OpenSceneMode.Single);
                                        }
                                        DevLog($"Screenshot {Screenshot.Name} clicked");
                                        var sv = GetSceneView();
                                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                        cube.transform.position = new Vector3(Screenshot.MapLocation.worldPosition.x, Screenshot.MapLocation.worldPosition.y, Screenshot.MapLocation.worldPosition.z);
                                        cube.transform.rotation = new Quaternion(Screenshot.MapLocation.worldRotation.x, Screenshot.MapLocation.worldRotation.y, Screenshot.MapLocation.worldRotation.z, Screenshot.MapLocation.worldRotation.w);

                                        sv.AlignViewToObject(cube.transform);
                                        sv.Repaint();
                                        GameObject.DestroyImmediate(cube);
                                    }
                                });

                                summary.Add(pic);
                            }
                        }
                    }

                    ScrollView scrollView = new ScrollView(ScrollViewMode.Horizontal);
                    scrollView.style.height = Length.Percent(100);
                    scrollView.name = $"[{count}] Screenshots";
                    scrollView.Add(summary);
                    return scrollView;
                }


                public VisualElement SettingsView()
                {
                    VisualElement settingsView = new VisualElement()
                    {
                        name = "ScreenshotSettingsView",
                        style = {
                                 flexDirection = FlexDirection.Column,
                                 width = Length.Percent(100),
                                 height = Length.Percent(100),
                                 backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.9f)),
                                 flexGrow = 1,
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

                    // Add centralized feedback section
                    var feedbackSection = CreateFeedbackSection(
                        "Screenshots",
                        "https://prf.hn/click/camref:1011lf9gY/pubref:editor/ar:internal/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Futilities%2Fmighty-devops-minimap-bookmarks-screenshots-and-more-267512",
                        "https://github.com/ShrinkRayEntertainment/Mighty-Maps/issues/new?template=%F0%9F%90%9B-bug-report.md&labels=bug&title=[{VERSION}%20|%20{UNITY_VERSION}]%20Your%20Title",
                        "https://github.com/ShrinkRayEntertainment/Mighty-Maps/issues/new?template=%E2%9C%A8-feature-request.md&labels=enhancement&title=[{VERSION}%20|%20{UNITY_VERSION}]%20Your%20Title",
                        Version
                    );
                    settingsView.Add(feedbackSection);

                    settingsView.Add(CreateSectionHeader("Screenshots Settings", "Configure screenshot functionality", new Color(0.8f, 0.6f, 1f, 1f)));

                    string fullPath = MightyPath + "/Resources/UI/mightystyles.uss";
                    settingsView.Add(Header("Mighty Map Settings"));
                    settingsView.Add(Spacer());

                    settingsView.Add(Title("Default Color"));

                    Color initialColor = GetColorFromFile(fullPath);
                    ColorField colorField = new()
                    {
                        name = "TaskablesColorField",
                        value = initialColor
                    };
                    settingsView.Add(colorField);

                    settingsView.Add(Spacer());
                    settingsView.Add(Title("Screenshot Resolution"));


                    EnumField resolutionField = new()
                    {
                        name = "ScreenshotResolutionField",
                        value = ScreenshotsCore.data.settings.screenshotResolution,
                        style = {
                                    unityTextAlign = TextAnchor.MiddleCenter,
                                }
                    };

                    resolutionField.Init(ScreenshotsCore.data.settings.screenshotResolution);
                    resolutionField.RegisterValueChangedCallback((evt) =>
                    {
                        ScreenshotsCore.data.settings.screenshotResolution = (ScreenshotResolution)evt.newValue;
                    });

                    settingsView.Add(resolutionField);

                    settingsView.Add(Spacer());
                    settingsView.Add(Title("Screenshot Format"));


                    EnumField formatField = new()
                    {
                        name = "ScreenshotFormatField",
                        value = ScreenshotsCore.data.settings.screenshotFormat,
                        style = {
                                    unityTextAlign = TextAnchor.MiddleCenter,
                                }
                    };

                    formatField.Init(ScreenshotsCore.data.settings.screenshotFormat);
                    formatField.RegisterValueChangedCallback((evt) =>
                    {
                        ScreenshotsCore.data.settings.screenshotFormat = (ScreenshotFormat)evt.newValue;
                    });


                    settingsView.Add(formatField);

                    settingsView.Add(Spacer());
                    settingsView.Add(Title("Screenshot Quality"));

                    Slider slider = new()
                    {
                        name = "ScreenshotQualitySlider",
                        value = ScreenshotsCore.data.settings.screenshotQuality,
                        lowValue = 1,
                        highValue = 100,
                        style = {
                                    unityTextAlign = TextAnchor.MiddleCenter,
                                }
                    };

                    slider.value = ScreenshotsCore.data.settings.screenshotQuality;

                    slider.RegisterValueChangedCallback((evt) =>
                    {
                        ScreenshotsCore.data.settings.screenshotQuality = (int)evt.newValue;
                    });
                    settingsView.Add(slider);

                    settingsView.Add(Spacer());
                    settingsView.Add(Spacer());
                    settingsView.Add(Header("Playmode Settings"));
                    settingsView.Add(Spacer());
                    settingsView.Add(Title("Screenshot Hotkey(s)"));

                    VisualElement container = new VisualElement()
                    {
                        style = {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 10
                        }
                    };
                    var label = new Label("Hotkey 1")
                    {
                        style = {

                            flexGrow = 0,
                            flexShrink = 0,
                        }
                    };
                    container.Add(label);

                    var hotkey1Field = new DropdownField()
                    {
                        value = ScreenshotsCore.data.settings.hotkey1.ToString(),
                        style =
                        {
                            width= 144,
                        }
                    };
                    container.Add(hotkey1Field);

                    // Populate dropdown with KeyCode options
                    foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                    {
                        hotkey1Field.choices.Add(key.ToString());
                    }

                    // Set initial selection
                    hotkey1Field.index = hotkey1Field.choices.IndexOf(ScreenshotsCore.data.settings.hotkey1.ToString());

                    // Listen for changes
                    hotkey1Field.RegisterValueChangedCallback(evt =>
                    {
                        ScreenshotsCore.data.settings.hotkey1 = (KeyCode)Enum.Parse(typeof(KeyCode), evt.newValue);
                    });

                    settingsView.Add(container);

                    container = new VisualElement()
                    {
                        style = {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 10
                        }
                    };

                    label = new Label("Hotkey 2")
                    {
                        style = {

                            flexGrow = 0,
                            flexShrink = 0,
                        }
                    };

                    container.Add(label);

                    var hotkey2Field = new DropdownField()
                    {
                        value = ScreenshotsCore.data.settings.hotkey2.ToString(),
                        style =
                        {
                            width= 144,
                        }
                    };

                    container.Add(hotkey2Field);

                    // Populate dropdown with KeyCode options

                    foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                    {
                        hotkey2Field.choices.Add(key.ToString());
                    }

                    // Set initial selection
                    hotkey2Field.index = hotkey2Field.choices.IndexOf(ScreenshotsCore.data.settings.hotkey2.ToString());

                    // Listen for changes
                    hotkey2Field.RegisterValueChangedCallback(evt =>
                    {
                        ScreenshotsCore.data.settings.hotkey2 = (KeyCode)Enum.Parse(typeof(KeyCode), evt.newValue);
                    });

                    settingsView.Add(container);

                    container = new VisualElement()
                    {
                        style = {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 10
                        }
                    };

                    label = new Label("Hotkey 3")
                    {
                        style = {

                            flexGrow = 0,
                            flexShrink = 0,
                        }
                    };

                    container.Add(label);

                    var hotkey3Field = new DropdownField()
                    {
                        value = ScreenshotsCore.data.settings.hotkey3.ToString(),
                        style =
                        {
                            width= 144,
                        }
                    };

                    container.Add(hotkey3Field);

                    // Populate dropdown with KeyCode options

                    foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                    {
                        hotkey3Field.choices.Add(key.ToString());
                    }

                    // Set initial selection
                    hotkey3Field.index = hotkey3Field.choices.IndexOf(ScreenshotsCore.data.settings.hotkey3.ToString());

                    // Listen for changes
                    hotkey3Field.RegisterValueChangedCallback(evt =>
                    {
                        ScreenshotsCore.data.settings.hotkey3 = (KeyCode)Enum.Parse(typeof(KeyCode), evt.newValue);
                    });

                    settingsView.Add(container);

                    colorField.RegisterCallback<ChangeEvent<Color>>((evt) =>
                    {
                        DevLog($"Color changed to {evt.newValue}");



                        // Check if the file exists
                        if (!File.Exists(fullPath))
                        {
                            DevLogError("File not found: " + fullPath);
                            return;
                        }

                        try
                        {
                            // Convert Color to rgb format
                            string colorString = $"rgb({(int)(evt.newValue.r * 255)}, {(int)(evt.newValue.g * 255)}, {(int)(evt.newValue.b * 255)})";

                            // Read the contents of the file
                            string content = File.ReadAllText(fullPath);

                            // Define the regex pattern to locate the .Screenshotable background-color
                            string pattern = @"(\.screenshotable\s*{[^}]*background-color:\s*)rgb\(\d{1,3}, \d{1,3}, \d{1,3}\)(;[^}]*})";

                            // Replace the old color with the new color
                            string modifiedContent = Regex.Replace(content, pattern, $"$1{colorString}$2");

                            // Write the modified content back to the file
                            File.WriteAllText(fullPath, modifiedContent);

                            // Refresh the AssetDatabase to update the changes in the editor
                            AssetDatabase.Refresh();

                            DevLog("USS file modified successfully.");
                        }
                        catch (System.Exception ex)
                        {
                            DevLog("Error modifying USS file: " + ex.Message);
                        }


                        // var ScreenshotableElements = root.Query<VisualElement>(className: "Screenshotable").ToList();

                        // // Change the background color of each element to green
                        // foreach (var element in ScreenshotableElements)
                        // {
                        //     DevLog($"Changing color of {element.name} to {evt.newValue}");
                        //     element.style.backgroundColor = evt.newValue;
                        // }
                    });


                    return settingsView;
                }

                public MightySceneViewManager.Settings GetSceneViewSettings()
                {
                    return ScreenshotsCore.data.settings.sceneView;
                }


                // Function to parse RGB string to Color
                private Color ParseRgbToColor(string rgb)
                {
                    var match = Regex.Match(rgb, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
                    if (match.Success)
                    {
                        return new Color(
                            int.Parse(match.Groups[1].Value) / 255f,
                            int.Parse(match.Groups[2].Value) / 255f,
                            int.Parse(match.Groups[3].Value) / 255f
                        );
                    }
                    return Color.white; // Default to white if parsing fails
                }

                private Color GetColorFromFile(string fullPath)
                {
                    if (!File.Exists(fullPath))
                    {
                        DevLogError("File not found: " + fullPath);
                        return Color.white; // Default to white if file not found
                    }

                    string content = File.ReadAllText(fullPath);
                    string pattern = @"\.screenshotable\s*{\s*[^}]*background-color:\s*(rgb\(\d{1,3}, \d{1,3}, \d{1,3}\));?";

                    var match = Regex.Match(content, pattern);
                    if (match.Success)
                    {
                        // Debug.Log($"LLL {match.Groups[1].Value}");
                        return ParseRgbToColor(match.Groups[1].Value);
                    }

                    return Color.white; // Default to white if color not found
                }

                [SerializeField] private string name;
                [SerializeField] private string description;
                [SerializeField] private string anchorTo;
                [SerializeField] private Location mapLocation;
                [SerializeField] private long createdAt;
                [SerializeField] private long lastModified;
                [SerializeField] private long lastQueried;
                [SerializeField] private string status;
                [SerializeField] private SceneViewLabel label;
                [SerializeField] private int id;
                [SerializeField] private int parentId;
                [SerializeField] private bool active;
                [SerializeField] private bool front;
                [SerializeField] private bool dirty;
                [SerializeField] private bool hasVisualContent;
                [SerializeField] private bool hasPlayTracking = true;
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
                public SceneViewLabel Label { get => label; set => label = value; }
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
                            _icon = Resources.Load<Texture2D>("mighty_icon_toggle_Screenshot2");
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
                    return "Screenshots";
                }


                public void Delete()
                {
                    MightyCoreData.mappables.Remove(this);
                    ScreenshotsCore.sceneData.Screenshots.Remove(this);
                }

                public Root()
                {

                }
                public Root(string n, Camera camera)
                {
                    DevLog($"Creating Screenshot {n} at {camera.transform.position} with rotation {camera.transform.rotation} Camera Name: {camera.name}");
                    ID = 0;
                    ScreenshotsCore.sceneData.Screenshots ??= new List<Root>();
                    if (ScreenshotsCore.sceneData.Screenshots.Count > 0)
                        ID = ScreenshotsCore.sceneData.Screenshots.Max(lm => lm.ID) + 1;
                    ParentId = 0;

                    AnchorTo = sceneData.Name;
                    Name = n;
                    Description = "New Screenshot";
                    Status = "active";

                    CreatedAt = LastModified = LastQueried = DateTime.Now.Ticks;

                    HasVisualContent = false;
                    HasPlayTracking = true;


                    MapAttributes = new Attributes
                    {
                        textMainColor = new Color(1, 1, 1, 1f),
                        backgroundColor = new Color(0, 0, 0, 1f),
                        textAccentColor = new Color(0, 1, 1, 1f),
                        backgroundAccentColor = new Color(0, 1, 1, 1.0f)
                    };

                    MapLocation = new Location
                    {
                        worldPosition = camera.transform.position,//new Vector3(pos.x, pos.y, pos.z);
                        worldRotation = camera.transform.rotation//new Quaternion(rot.x, rot.y, rot.z, rot.w);
                    };
                    //transform.rotation = new Rotation();


                    Label = new SceneViewLabel
                    {
                        show = true,
                        fade = true,
                        fadeMin = 100,
                        fadeMax = 1000,
                        offset = new Vector3()
                    };
                    Label.offset.x = Label.offset.y = Label.offset.z = 0;

                    int resolution = 1024;
                    switch (ScreenshotsCore.data.settings.screenshotResolution)
                    {
                        case ScreenshotResolution.Low_640:
                            resolution = 640;
                            break;
                        case ScreenshotResolution.Medium_1080:
                            resolution = 1024;
                            break;
                        case ScreenshotResolution.High_4k:
                            resolution = 4096;
                            break;
                    }

                    RenderTexture currentRT = new RenderTexture(resolution, resolution, 24);
                    camera.targetTexture = currentRT;
                    camera.Render();

                    RenderTexture.active = currentRT;

                    //


                    Texture2D texture2D = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
                    texture2D.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                    texture2D.Apply();

                    camera.targetTexture = null;
                    RenderTexture.active = null;
                    string ext = "jpg";
                    byte[] texture2DBytes;
                    switch (ScreenshotsCore.data.settings.screenshotFormat)
                    {
                        case ScreenshotFormat.JPG:
                            texture2DBytes = texture2D.EncodeToJPG(ScreenshotsCore.data.settings.screenshotQuality);
                            ext = "jpg";
                            break;
                        case ScreenshotFormat.PNG:
                            texture2DBytes = texture2D.EncodeToPNG();
                            ext = "png";
                            break;
                        default:
                            texture2DBytes = texture2D.EncodeToJPG(ScreenshotsCore.data.settings.screenshotQuality);
                            ext = "jpg";
                            break;
                    }

                    Pic = new Picture
                    {
                        path = $"",
                        filename = $"ss_{CreatedAt.ToString()}.{ext}",
                        format = ext
                    };
                    Pic.width = Pic.height = resolution;
                    Pic.img = texture2D;//new Texture2D(texture2D);
                    Pic.imgLoaded = true;

                    Debug.Log($"Screenshot {Name} created at {CreatedAt} with {resolution}x{resolution} {ext} format {Pic.filename} quality {ScreenshotsCore.data.settings.screenshotQuality}");


                    DevLog("Writing to " + Pic.path);

                    File.WriteAllBytes($"{MightyCoreData.GetCache()}{Pic.filename}",
                                       texture2DBytes);
                    sceneData.PlayTrackingDirty = true;

                    RegisterMappable();
                    //DestroyImmediate(texture2D);
                }
            }
        }
    }
}
#endif