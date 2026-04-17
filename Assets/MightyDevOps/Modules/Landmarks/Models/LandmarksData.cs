#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mighty;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;


namespace MightyLandmarks
{

    public class LandmarksData : ScriptableObject
    {




        public static void Save()
        {
            string path = $"{corePath}/Modules/Landmarks/Data/LandmarksData.asset";
            if (File.Exists(path))
            {
                DevLog($"{path} already exists...");
                return;
            }

            LandmarksData asset = ScriptableObject.CreateInstance<LandmarksData>();

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }

        public static LandmarksData Load()
        {
            string path = $"{corePath}/Modules/Landmarks/Data/LandmarksData.asset";
            // string fallbackPath = $"{corePath}/Modules/Landmarks/Data/LandmarksData_.asset";

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
            //return Resources.Load("LandmarkModuleData", typeof(LandmarkData)) as LandmarkData;
            return AssetDatabase.LoadAssetAtPath<LandmarksData>(path);
        }

        // [SerializeField]
        // public List<Landmark.Root> landmarks;
        [SerializeField]
        public Settings settings = new Settings();

        [SerializeField]
        public List<Scene> scenes;
        private static bool clickedFrontView = false;

        [Serializable]
        public class Scene
        {
            public string name;
            [SerializeField]
            public List<Landmark.Root> landmarks;
        }

        [Serializable]
        public class Settings
        {
            [SerializeField]
            public Color borderColor = new Color(0, 0, 0, 0);
            [SerializeField]
            public MightySceneViewManager.Settings sceneView = new MightySceneViewManager.Settings();
        }

        const string iconAddMappablePath = "mighty_icon_add_landmark";
        Color defaultColor = new Color(1, 0, 0, 1f);
        // float defaultScale = 1.5f;


        [Serializable]
        public class Landmark
        {


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
                private string _version = "1.7.0";
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
                    // var landmarkUpdate = dataCore.moduleUpdates.FirstOrDefault(update => update.module == "landmarks");
                    // if (landmarkUpdate != null)
                    // {
                    //     ShowToast($"New Landmarks Version: {landmarkUpdate.version}");
                    //     return landmarkUpdate.version != Version;
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
                    ViewUI.AddView("landmark_front", 96, 96);
                    ViewUI.AddView("landmark_back", 128, 128);
                    ViewUI.AddView("landmark_back_s1", 192, 192);



                    var frontView = ViewUI.GetView("landmark_front");
                    var backView = ViewUI.GetView("landmark_back");
                    var backs1View = ViewUI.GetView("landmark_back_s1");

                    DevLog($"InitViews {frontView}");
                    DevLog($"InitViews {backView}");
                    DevLog($"InitViews {backs1View}");


                    frontView.AddToClassList("lm_anchor_" + anchorTo);
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
                        button.tooltip = "Delete Landmark";
                        button.name = "X";

                        button.clicked += () =>
                        {
                            Delete();
                            var v = root.Q<VisualElement>(name: Pic.filename);
                            if (v != null) v.RemoveFromHierarchy();
                            MightySceneViewManager.Rebuild();
                            UpdateMappables();
                        };
                        button.visible = true;
                    }



                    button = frontView.Q<Button>(className: "gotoLandmark");
                    if (button != null)
                    {
                        button.tooltip = "Go To Landmark";
                        button.name = "GO";

                        button.clicked += () =>
                        {
                            LandmarksCore.core.SceneViewGoToPosition(MapLocation.worldPosition, MapLocation.worldRotation);
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


                        button.clicked += () =>
                        {
                            if (clickedFrontView == true) return;
                            clickedFrontView = true;
                            Dirty = true;

                            ViewUI.PushView("landmark_back");

                            var landmarkName = backView.Q<TextField>(name: "LandmarkName");
                            if (landmarkName != null)
                            {
                                if (MightySceneViewManager.root != null)
                                {
                                    var x = MightySceneViewManager.root.Q<VisualElement>(name: "sv_" + CreatedAt.ToString());
                                    if (x != null)
                                    {
                                        x.tooltip = Name;
                                        x.MarkDirtyRepaint();
                                    }

                                }


                                landmarkName.value = Name;
                                void UpdateLabel()
                                {
                                    Name = landmarkName.value;
                                    if (MightySceneViewManager.root != null)
                                    {
                                        var x = MightySceneViewManager.root.Q<VisualElement>(name: "sv_" + CreatedAt.ToString());
                                        if (x != null)
                                        {
                                            x.tooltip = Name;
                                            x.MarkDirtyRepaint();
                                        }

                                    }
                                }
                                new EditableLabel(landmarkName, newValue => UpdateLabel());
                                // new EditableLabel(landmarkName, newValue => Name = newValue);
                            }

                            var landmarkDescription = backView.Q<TextField>(name: "LandmarkDescription");
                            if (landmarkDescription != null)
                            {
                                landmarkDescription.value = Description;
                                // new EditableLabel(landmarkDescription, newValue => Description = newValue);
                            }
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


                    backView.RegisterCallback<WheelEvent>((evt) =>
                    {
                        evt.StopImmediatePropagation();
                    });

                    button = backView.Q<Button>(className: "back");
                    {
                        DevLog($"button is null? {button == null}");
                        Front = !Front;
                        button.name = Pic.filename;
                        //                        DevLog($"Creating button {pic.filename}");

                        if (Pic.img == null) LoadImage();
                        button.style.backgroundColor = Color.white;
                        button.style.color = Color.black;
                        //button.style.backgroundImage = Pic.img;
                        var currentViewUI = ViewUI;
                        button.clicked += () =>
                        {
                            Dirty = true;
                            DevLog("back");

                            currentViewUI.PopView();
                            frontView.Q<Button>(className: "thumbnail").tooltip = Name;
                            View.MarkDirtyRepaint();
                            RebuildMappables();
                        };
                        button.visible = true;
                    }




                    button = backs1View.Q<Button>(className: "back");
                    {
                        Front = !Front;
                        button.tooltip = Pic.filename; //description;
                        button.name = Pic.filename;
                        //                        DevLog($"Creating button {pic.filename}");

                        if (Pic.img == null) LoadImage();
                        button.style.backgroundImage = Pic.img;

                        button.clicked += () =>
                        {
                            Dirty = true;
                            DevLog("back");

                            ViewUI.PopView();

                            View.MarkDirtyRepaint();
                            Rebuild();
                        };
                        button.visible = true;
                    }


                    backView.AddToClassList(Pic.filename);

                    backView.name = Pic.filename;
                    backView.styleSheets.Add(Resources.Load("UI/mightystyles", typeof(StyleSheet)) as StyleSheet);
                    backView.visible = true;

                    View.Add(frontView);
                    View.Add(backView);

                    ViewUI.PushView("landmark_front");
                    View.MarkDirtyRepaint();
                }

                public void RegisterMappable()
                {
                    if (mappables.Contains(this)) return;
                    if (sceneData == null) LandmarksCore.GetSceneData();
                    if (AnchorTo == sceneData.Name)
                    {
                        DevLog($"Adding Mappable: {this.Name}");
                        mappables.Add(this);
                        InitViews();
                    }
                    else
                    {
                        DevLog($"Not Adding Mappable: {this.Name} as it is anchored to {AnchorTo} and not {sceneData.Name}");
                    }
                }

                public void OnGenerateVisualContent(MeshGenerationContext mgc)
                {
                    DevLog($"Landmark OnGenerateVisualContent - {ViewUI.GetView().ToString()}");
                }

                public void CheckIntegrity()
                {

                }

                public void LoadImage()
                {
                    DevLog("LoadImage()");
                    if (Pic == null)
                    {
                        DevLogError($"Pic is Null for Landmark {Name}");
                        Pic = new Picture();
                    }
                    Pic.img = new Texture2D(1, 1);
                    //
                    Pic.filename = $"lm_{CreatedAt.ToString()}.jpg";
                    DevLog($"Landmark Pic Filename {Pic.filename}");
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
                    DevLog($"Loaded Landmark Image {fileName} and it is {Pic.img}");



                }


                public Button AddMappable(bool setClickedCallback = true)
                {
                    DevLog($"mappable adding creation button");
                    if (iconAddMappable == null)
                        iconAddMappable = Resources.Load<Texture2D>(iconAddMappablePath);
                    DevLog($"mappable adding creation button {iconAddMappablePath} {iconAddMappable != null}");
                    Button b = new()
                    {
                        name = "addLandmark",
                        tooltip = "Add Landmark",
                        style = {
                            backgroundColor = new Color(0, 0, 0, 0),
                            borderBottomColor  = new Color(0, 0, 0, 0),
                            borderLeftColor  = new Color(0, 0, 0, 0),
                            borderRightColor  = new Color(0, 0, 0, 0),
                            borderTopColor  = new Color(0, 0, 0, 0),
                            // position = Position.Absolute,
                            // right = 48,
                            // bottom = 48,
                }
                    };


                    if (iconAddMappable != null)
                        b.style.backgroundImage = iconAddMappable;
                    else
                        b.text = "LMRK";

                    if (setClickedCallback)
                        b.clicked += () =>
                        {
                            DevLog("Landmark YAY");
                            // Debug.Log("New Landmark");
                            if (LandmarksCore.sceneData.landmarks == null) LandmarksCore.sceneData.landmarks = new List<LandmarksData.Landmark.Root>();
                            MightyCoreData.sceneCamera = GetSceneView().camera;
                            LandmarksCore.sceneData.landmarks.Add(new LandmarksData.Landmark.Root(GetSVCameraPosition().ToString(), MightyCoreData.sceneCamera));
                            MightyCoreData.updatingMappables = false;
                            // MightyCoreData.rebuildingView = false;
                            ShowToast($"Landmark added at ({GetSVCameraPosition().ToString()}");
                            Rebuild();

                            MightySceneViewManager.Rebuild();
                        };
                    return b;
                }

                public CustomToggleButton AddModuleToggle(MappableTypeInfo mappableTypeInfo)
                {
                    DevLog($"AddModuleToggle named {mappableTypeInfo.Name}");
                    return new(Icon, mappableTypeInfo, "LandmarkOverlay");
                }

                public VisualElement SceneSummary(MightyCoreData.SceneData scene)
                {
                    VisualElement summary = new VisualElement
                    {
                        name = "Landmarks",
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            flexGrow = 0,
                            flexShrink = 0,
                            height = 80,
                        }
                    };

                    int count = 0;
                    foreach (var anchor in LandmarksCore.data.scenes)
                    {
                        if (anchor.name == scene.Name)
                        {
                            DevLog($"SceneSummary Found {anchor.name} in {scene.Name}");
                            foreach (var landmark in anchor.landmarks)
                            {
                                count++;
                                DevLog($"SceneSummary Found {landmark.Name} in {scene.Name}");
                                landmark.LoadImage();

                                VisualElement pic = new()
                                {
                                    name = landmark.Name,
                                    style = {
                                        minWidth = 64,
                                        minHeight = 64,
                                        height = 64,
                                        width = 64,
                                        marginLeft = 4,
                                        marginRight = 4,
                                        flexGrow=0,
                                        flexShrink=0,
                                        backgroundImage = landmark.Pic.img
                                    }
                                };

                                // Create a larger version of the image
                                VisualElement largePic = new()
                                {
                                    name = landmark.Name,
                                    style = {
                                        width = 128,
                                        height = 128,
                                        backgroundImage = landmark.Pic.img,
                                        position = Position.Absolute,
                                        display = DisplayStyle.None
                                    }
                                };

                                Label landmarkName = new()
                                {
                                    text = landmark.Name,
                                    style = {
                                        fontSize = 14,
                                        backgroundColor = new Color(0, 0, 0, 0.5f),
                                        color = Color.white,
                                        unityTextAlign = TextAnchor.MiddleCenter,
                                    }
                                };

                                largePic.Add(landmarkName);
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
                                        DevLog($"Landmark {landmark.Name} clicked");
                                        var sv = GetSceneView();
                                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                        cube.transform.position = new Vector3(landmark.MapLocation.worldPosition.x, landmark.MapLocation.worldPosition.y, landmark.MapLocation.worldPosition.z);
                                        cube.transform.rotation = new Quaternion(landmark.MapLocation.worldRotation.x, landmark.MapLocation.worldRotation.y, landmark.MapLocation.worldRotation.z, landmark.MapLocation.worldRotation.w);

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
                    scrollView.name = $"[{count}] Landmarks";
                    scrollView.Add(summary);
                    return scrollView;
                }

                public void PopulatePlayTrackingLane(int laneIndex)
                {
                    DevLog($"Lane {laneIndex}: Landmark PopulatePlayTrackingLane, no playmode tracking for landmarks");
                }

                public VisualElement SettingsView()
                {
                    VisualElement settingsView = new VisualElement()
                    {
                        name = "LandmarksSettingsView",
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
                        "Landmarks",
                        "https://prf.hn/click/camref:1011lf9gY/pubref:editor/ar:internal/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Fslug%2F318759",
                        "https://github.com/ShrinkRayEntertainment/Mighty-Maps/issues/new?template=%F0%9F%90%9B-bug-report.md&labels=bug&title=[{VERSION}%20|%20{UNITY_VERSION}]%20Your%20Title",
                        "https://github.com/ShrinkRayEntertainment/Mighty-Maps/issues/new?template=%E2%9C%A8-feature-request.md&labels=enhancement&title=[{VERSION}%20|%20{UNITY_VERSION}]%20Your%20Title",
                        Version
                    );
                    settingsView.Add(feedbackSection);

                    settingsView.Add(CreateSectionHeader("Landmarks Settings", "Configure landmark visualization", new Color(1f, 0.6f, 0.2f, 1f)));

                    string fullPath = MightyPath + "/Resources/UI/mightystyles.uss";

                    settingsView.Add(Header("Mighty Map Settings"));
                    settingsView.Add(Spacer());

                    Label label = new()
                    {
                        text = "Default Color",
                        style = {
                            unityTextAlign = TextAnchor.MiddleCenter,
                            fontSize = 14,
                            color = Color.white
                        }
                    };
                    settingsView.Add(label);


                    Color initialColor = GetColorFromFile(fullPath);
                    var data = LandmarksCore.data;

                    data.settings.borderColor = initialColor; // Access the settings field using the instance
                    ColorField colorField = new()
                    {
                        name = "TaskablesColorField",
                        value = initialColor
                    };
                    settingsView.Add(colorField);

                    settingsView.Add(Spacer());
                    settingsView.Add(Spacer());
                    settingsView.Add(Header("Sceneview Settings"));
                    settingsView.Add(Spacer());


                    Toggle svShow = new Toggle("Show Landmarks");
                    svShow.value = data.settings.sceneView.show;
                    svShow.RegisterValueChangedCallback(evt => { data.settings.sceneView.show = evt.newValue; });
                    settingsView.Add(svShow);
                    svShow.RegisterValueChangedCallback(evt =>
                    {
                        data.settings.sceneView.show = evt.newValue;
                        UpdateSceneView.Invoke();
                    });

                    settingsView.Add(StyledLabel("Distance"));
                    MinMaxSlider distanceSlider = new MinMaxSlider(data.settings.sceneView.distanceStart, data.settings.sceneView.distanceEnd, 0, 1000);
                    distanceSlider.RegisterValueChangedCallback(evt =>
                    {
                        data.settings.sceneView.distanceStart = evt.newValue.x;
                        data.settings.sceneView.distanceEnd = evt.newValue.y;
                        // Debug.Log($"Updating World Space Elements {data.settings.sceneView.distanceStart} {data.settings.sceneView.distanceEnd}");
                        UpdateSceneView.Invoke();
                    });
                    settingsView.Add(distanceSlider);



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

                            // Define the regex pattern to locate the .landmarkable background-color
                            string pattern = @"(\.landmarkable\s*{[^}]*background-color:\s*)rgb\(\d{1,3}, \d{1,3}, \d{1,3}\)(;[^}]*})";

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


                        // var landmarkableElements = root.Query<VisualElement>(className: "landmarkable").ToList();

                        // // Change the background color of each element to green
                        // foreach (var element in landmarkableElements)
                        // {
                        //     DevLog($"Changing color of {element.name} to {evt.newValue}");
                        //     element.style.backgroundColor = evt.newValue;
                        // }
                    });


                    return settingsView;
                }

                public MightySceneViewManager.Settings GetSceneViewSettings()
                {
                    if (LandmarksCore.data.settings.sceneView == null)
                        return new MightySceneViewManager.Settings
                        {
                            show = true,
                            distanceStart = 0,
                            distanceEnd = 1000,
                            borderColor = Color.red,
                        };
                    else
                        return LandmarksCore.data.settings.sceneView;
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
                    string pattern = @"\.landmarkable\s*{\s*[^}]*background-color:\s*(rgb\(\d{1,3}, \d{1,3}, \d{1,3}\));?";

                    var match = Regex.Match(content, pattern);
                    if (match.Success)
                    {
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
                            _icon = Resources.Load<Texture2D>("mighty_icon_toggle_landmark3");
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
                    return "Landmarks";
                }


                public void Delete()
                {
                    MightyCoreData.mappables.Remove(this);
                    LandmarksCore.sceneData.landmarks.Remove(this);
                }

                public Root()
                {

                }
                public Root(string n, Camera camera)
                {
                    DevLog($"Creating Landmark {n} at {camera.transform.position} with rotation {camera.transform.rotation} Camera Name: {camera.name}");
                    ID = 0;
                    LandmarksCore.sceneData.landmarks ??= new List<Root>();
                    if (LandmarksCore.sceneData.landmarks.Count > 0)
                        ID = LandmarksCore.sceneData.landmarks.Max(lm => lm.ID) + 1;
                    ParentId = 0;

                    AnchorTo = sceneData.Name;
                    Name = n;
                    Description = "New Landmark";
                    Status = "active";

                    CreatedAt = LastModified = LastQueried = DateTime.Now.Ticks;

                    HasVisualContent = false;




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


                    RenderTexture currentRT = new RenderTexture(1024, 1024, 24);
                    camera.targetTexture = currentRT;
                    camera.Render();

                    RenderTexture.active = currentRT;

                    //
                    Texture2D texture2D = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                    texture2D.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                    texture2D.Apply();

                    camera.targetTexture = null;
                    RenderTexture.active = null;

                    var texture2DBytes = texture2D.EncodeToJPG(10);

                    Pic = new Picture
                    {
                        path = $"",
                        filename = $"lm_{CreatedAt.ToString()}.jpg",
                        format = "jpeg"
                    };
                    Pic.width = Pic.height = 1024;
                    Pic.img = texture2D;//new Texture2D(texture2D);
                    Pic.imgLoaded = true;


                    DevLog("Writing to " + Pic.path);

                    File.WriteAllBytes($"{MightyCoreData.GetCache()}{Pic.filename}",
                                       texture2DBytes);

                    RegisterMappable();
                    //DestroyImmediate(texture2D);
                }
            }

            // [Serializable]
            // public class Rotation
            // {
            //     public float x;
            //     public float y;
            //     public float z;
            //     public float w;
            // }

            // [Serializable]
            // public class Transform
            // {
            //     public Position position;
            //     public Rotation rotation;
            // }


        }
    }
}
#endif