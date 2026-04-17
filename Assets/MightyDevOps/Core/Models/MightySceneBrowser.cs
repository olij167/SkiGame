#if UNITY_EDITOR    
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;

namespace Mighty
{
    public class MightySceneBrowser : ScriptableObject
    {
        static Action saveState;
        public static void Save()
        {
            string path = $"{corePath}/Core/Data/MightySceneBrowserData.asset";
            if (File.Exists(path))
            {
                DevLog($"{path} already exists...");
                return;
            }

            MightySceneBrowser asset = ScriptableObject.CreateInstance<MightySceneBrowser>();
            DevLog($"Saving MightySceneBrowserData to {path}");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }


        public static MightySceneBrowser Load()
        {
            string path = $"{corePath}/Core/Data/MightySceneBrowserData.asset";
            if (!File.Exists(path))
            {
                Save();
            }
            DevLog($"Loading MightySceneBrowserData from {path}");
            return AssetDatabase.LoadAssetAtPath<MightySceneBrowser>(path);
        }

        private void OnEnable()
        {
            EditorApplication.quitting += SaveState;
            AssemblyReloadEvents.beforeAssemblyReload += SaveState;
            saveState += SaveState;
        }

        void SaveState()
        {
            DevLog($"SaveState");

            if (this != null)
                EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public VisualElement view;
        ToolbarPopupSearchField sceneSelectSearchField;
        List<VisualElement> sceneCards = new();
        List<String> sceneNames = new();
        private VisualElement top;
        private ScrollView mid;
        private VisualElement midContent;
        private VisualElement bottom;
        private VisualElement sceneDetails;
        private Label sceneDetailsHeaderLabel;
        private VisualElement sceneDetailsContent;
        private string searchQuery = "";
        private SearchType currentSearchType;
        private bool isCaseSensitive;
        private List<SceneData.GameObjectData> results = new List<SceneData.GameObjectData>();
        // private SceneData sceneData;
        private MightyCoreData dataCore;


        [System.Serializable]
        public class State
        {
            public string selectedScene;
            public int selectedTab;
        }

        [SerializeField]
        public State state;

        // private int itemsLoaded = 0;
        // private const int itemsPerLoad = 10;


        [System.Serializable]
        public enum SceneType
        {
            Environment,
            Overlay,
            // Add more types as needed.
        }



        #region BuildView Method
        public void BuildView()
        {
            MightyCore.data.SceneDupeCheck();
            state ??= new State();

            CreateView();
            CreateTopSection();
            CreateMiddleSection();
            CreateBottomSection();
            CreateSceneDetails();

            // Build scene cards
            CreateSceneCards();

            // Add sceneDetails to view
            view.Add(sceneDetails);
        }
        #endregion

        #region View Creation
        private void CreateView()
        {
            view = new VisualElement
            {
                name = "SceneBrowser",
                style =
                {
                    // height = Length.Percent(100),
                    flexGrow = 0,
                    flexDirection = FlexDirection.Column,
                    flexShrink = 0,
                    overflow = Overflow.Hidden,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.SpaceAround,
                    minHeight = 256,
                    maxHeight = 512,
                    minWidth = 256,
                    maxWidth = 512,
                    height = 512,
                    width = 512,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                }
            };
        }
        #endregion

        #region Top Section
        private void CreateTopSection()
        {
            top = new VisualElement
            {
                name = "SceneSelectTop",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 0,
                    flexShrink = 0,
                    height = 20,
                    width = Length.Percent(100),
                }
            };

            // Scene selection search field
            sceneSelectSearchField = new ToolbarPopupSearchField
            {
                name = "sceneSelectSearchField",
                style =
        {
            flexGrow = 1,
            marginBottom = 10,
        }
            };

            // Add search type options to the menu
            sceneSelectSearchField.menu.AppendAction("Name Search", (a) =>
            {
                currentSearchType = MightyCoreData.SearchType.Name;
                FilterScenes();
            }, (a) => currentSearchType == MightyCoreData.SearchType.Name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            sceneSelectSearchField.menu.AppendAction("Deep Search", (a) =>
            {
                currentSearchType = MightyCoreData.SearchType.Deep;
                FilterScenes();
            }, (a) => currentSearchType == MightyCoreData.SearchType.Deep ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            // Add a separator
            sceneSelectSearchField.menu.AppendSeparator();

            // Add case-sensitive toggle
            sceneSelectSearchField.menu.AppendAction("Case Sensitive", (a) =>
            {
                isCaseSensitive = !isCaseSensitive;
                FilterScenes();
            }, (a) => isCaseSensitive ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            sceneSelectSearchField.RegisterValueChangedCallback((evt) =>
{
    searchQuery = evt.newValue; // Update the global searchQuery
    FilterScenes(); // Use the global searchQuery in FilterScenes
});

            top.Add(sceneSelectSearchField);

            view.Add(top);

            sceneSelectSearchField.value = searchQuery;
            FilterScenes();
        }

        private void FilterScenes()
        {
            List<string> sceneNames = new List<string>();

            foreach (var scene in MightyCore.data.scenes)
            {
                if (scene.DeleteMe) continue;

                if (scene.CollectedData == null || scene.CollectedData.Count == 0)
                {
                    // Start data collection
                    scene.StartCollection();
                    EditorApplication.update += scene.UpdateDeepDive;
                }

                var results = scene.SearchCollectedData(searchQuery, currentSearchType, isCaseSensitive);
                if (results.Count > 0)
                {
                    sceneNames.Add(scene.Name);
                }
            }

            // Update scene card visibility
            foreach (var card in sceneCards)
            {
                if (searchQuery != "" && sceneNames.Contains(card.name))
                {
                    card.style.display = DisplayStyle.Flex;
                    var sceneName = card.name.Split("___").First();
                    // Debug.Log($"BOB sceneName: {sceneName} SceneManager.GetActiveScene().name: {SceneManager.GetActiveScene().name} sceneName == SceneManager.GetActiveScene().name: {sceneName == SceneManager.GetActiveScene().name}");
                    var cardHeader = card.Q<VisualElement>(name: card.name + " Header Container");
                    if (sceneName == SceneManager.GetActiveScene().name) cardHeader.style.backgroundColor = new Color(0.5f, 0.5f, 0.1f); else cardHeader.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                }
                else
                {
                    card.style.display = searchQuery == "" ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }


        private void ConfigureSearchField()
        {
            // Implement search field configuration
            // Add menu actions, value change callbacks, etc.
        }
        #endregion

        #region Middle Section
        private void CreateMiddleSection()
        {
            mid = new ScrollView
            {
                name = "SceneSelectMid",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    flexShrink = 1,
                    height = Length.Percent(100),
                    width = Length.Percent(100),
                }
            };

            midContent = new VisualElement
            {
                name = "SceneSelectMidContent",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    flexGrow = 1,
                    flexShrink = 1,
                    height = Length.Percent(100),
                    width = Length.Percent(100),
                }
            };
            mid.Add(midContent);
            view.Add(mid);
        }
        #endregion

        #region Bottom Section
        private void CreateBottomSection()
        {
            bottom = new VisualElement
            {
                name = "SceneSelectBottom",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 0,
                    flexShrink = 0,
                    height = 20,
                    width = Length.Percent(100),
                }
            };
            view.Add(bottom);
        }
        #endregion

        #region Scene Details
        private void CreateSceneDetails()
        {
            sceneDetails = new VisualElement
            {
                name = "SceneBrowserDetails",
                style = {
                    color = Color.white,
                    height = Length.Percent(100),
                    width = Length.Percent(100),
                    flexGrow = 1,
                    flexShrink = 0,
                    position = Position.Absolute,
                    display = DisplayStyle.None,
                    flexDirection = FlexDirection.Column,
                }
            };
            VisualElement sceneDetailsHeader = new VisualElement
            {
                name = "SceneBrowserDetailsHeader",
                style = {
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                    color = Color.white,
                    maxHeight = 20,
                    width = Length.Percent(100),
                    flexGrow = 1,
                    flexShrink = 1,
                    flexDirection = FlexDirection.Row,
                }
            };
            sceneDetailsHeaderLabel = new Label
            {
                name = "SceneBrowserDetailsHeaderLabel",
                text = "Scene Details",
                style = {
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                    color = Color.white,
                    height = 20,
                    flexGrow = 1,
                    flexShrink = 1,
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                }
            };
            Button back = new Button
            {
                name = "BackButton",
                text = "Back",
                style = {
                    backgroundColor = Color.white,
                    color = Color.black,
                    flexGrow = 0,
                    flexShrink = 0,
                }
            };
            back.clicked += () =>
            {
                sceneDetails.style.display = DisplayStyle.None;
                state.selectedScene = "";
            };

            sceneDetailsContent = new VisualElement
            {
                name = "SceneBrowserDetailsContent",
                style = {
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                    color = Color.white,
                    flexGrow = 2,
                    flexShrink = 1,
                }
            };
            sceneDetails.style.display = DisplayStyle.None;
            state.selectedScene = "";
            // sceneDetailsHeader.Add(back);
            // sceneDetailsHeader.Add(sceneDetailsHeaderLabel);
            // sceneDetails.Add(sceneDetailsHeader);
            // sceneDetails.Add(sceneDetailsContent);
        }
        #endregion

        #region Scene Cards
        private void CreateSceneCards()
        {
            foreach (var scene in MightyCore.data.scenes)
            {
                if (scene.Name == "Project" || scene.DeleteMe)
                {
                    continue;
                }
                VisualElement sceneCard = CreateSceneCard(scene);
                sceneCards.Add(sceneCard);
                midContent.Add(sceneCard);

                if (state.selectedScene != null && state.selectedScene == scene.Name)
                {
                    sceneDetails.style.display = DisplayStyle.Flex;
                    sceneDetails.style.backgroundImage = scene.MiniMap.map;
                    sceneDetailsHeaderLabel.text = scene.Name;
                    ShowSceneInfoPanel(sceneDetailsContent, scene);
                    OpenDetails();
                }
            }
        }

        private VisualElement CreateSceneCard(SceneData scene)
        {
            // Root card container
            VisualElement sceneCard = new VisualElement
            {
                name = scene.Name,
                style =
        {
            flexDirection = FlexDirection.Column,
            width = Length.Percent(100),
            height = 140,
            marginBottom = 10,
            backgroundColor = new Color(0.15f, 0.15f, 0.15f),
            position = Position.Relative,
            overflow = Overflow.Hidden,
            borderTopLeftRadius = 8,
            borderTopRightRadius = 8,
            borderBottomLeftRadius = 8,
            borderBottomRightRadius = 8,
            scale = new Scale(new Vector3(0.95f, 0.95f, 1f)),
            transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") },
            transitionDuration = new List<TimeValue> { new TimeValue(200, TimeUnit.Millisecond) },
        }
            };

            // Header container
            VisualElement headerContainer = new VisualElement
            {
                name = scene.Name + " Header Container",
                style =
        {
            flexDirection = FlexDirection.Row,
            justifyContent = Justify.SpaceBetween,
            alignItems = Align.Center,
            paddingLeft = 10,
            paddingRight = 10,
            paddingTop = 5,
            paddingBottom = 5,
            backgroundColor = new Color(0.2f, 0.2f, 0.2f),
        }
            };

            var sceneName = scene.Name.Split("___").First();
            if (sceneName == SceneManager.GetActiveScene().name) headerContainer.style.backgroundColor = new Color(0.5f, 0.5f, 0.1f);

            // Scene title
            Label sceneTitle = new Label(scene.Name.Split("___").First())
            {
                name = scene.Name + " Title",
                style =
        {
            color = Color.white,
            fontSize = 16,
            unityFontStyleAndWeight = FontStyle.Bold,
        }
            };
            headerContainer.Add(sceneTitle);

            // Delete button
            Button deleteButton = new Button
            {
                name = scene.Name + " Delete Button",
                style =
        {
            width = 24,
            height = 24,
            backgroundColor = Color.black,
            unityBackgroundImageTintColor = Color.white,
            backgroundImage = icons?.trashcanIcon,
            borderTopLeftRadius = 12,
            borderTopRightRadius = 12,
            borderBottomLeftRadius = 12,
            borderBottomRightRadius = 12,
        }
            };


            deleteButton.tooltip = "Delete Scene";

            deleteButton.clicked += () =>
            {
                if (EditorUtility.DisplayDialog(
                    "Remove Scene?",
                    $"Are you sure you want to remove '{scene.Name}' from Mighty DevOps?",
                    "Yes",
                    "No"))
                {
                    scene.DeleteMe = true;
                    sceneCard.RemoveFromHierarchy();
                }
            };
            headerContainer.Add(deleteButton);

            sceneCard.Add(headerContainer);

            // Content container (Image and Metadata)
            VisualElement contentContainer = new VisualElement
            {
                name = scene.Name + " Content Container",
                style =
        {
            flexDirection = FlexDirection.Row,
            flexGrow = 1,
            paddingLeft = 10,
            paddingRight = 10,
            paddingTop = 5,
            paddingBottom = 10,
            backgroundColor = new Color(0.15f, 0.15f, 0.15f),
        }
            };

            // Image container
            VisualElement imageContainer = new VisualElement
            {
                name = scene.Name + " Image Container",
                style =
        {
            width = 96,
            height = 96,
            position = Position.Relative,
            backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
            borderTopLeftRadius = 8,
            borderBottomLeftRadius = 8,
            borderTopRightRadius = 8,
            borderBottomRightRadius = 8,
            overflow = Overflow.Hidden,
        }
            };

            // Scene image
            Image sceneImage = new Image
            {
                name = scene.Name + " Scene Image",
                image = GetSceneImage(scene),
                scaleMode = ScaleMode.ScaleAndCrop,
                style =
        {
            width = Length.Percent(100),
            height = Length.Percent(100),
        }
            };
            imageContainer.Add(sceneImage);

            contentContainer.Add(imageContainer);

            // Metadata container
            VisualElement metadataContainer = new VisualElement
            {
                name = scene.Name + " Metadata Container",
                style =
        {
            flexDirection = FlexDirection.Column,
            justifyContent = Justify.FlexStart,
            paddingLeft = 15,
            paddingRight = 10,
            paddingTop = 5,
            paddingBottom = 5,
            marginLeft = 10,
            backgroundColor = new Color(0.2f, 0.2f, 0.2f),
            flexGrow = 1,
        }
            };

            // Object count
            VisualElement objectCountContainer = CreateMetadataItem("Objects", scene.CollectedData.Count.ToString(), icons?.gameObjectIcon);
            metadataContainer.Add(objectCountContainer);

            // Polygon count
            VisualElement polyCountContainer = CreateMetadataItem("Polygons", scene.totalPolyCount.ToString(), icons?.polyIcon);
            metadataContainer.Add(polyCountContainer);

            // // Landmarks count
            // string landmarksCount = LandmarksCore.data.scenes
            //     .Where(anchor => anchor.name == scene.Name)
            //     .Select(anchor => anchor.landmarks.Count.ToString())
            //     .FirstOrDefault() ?? "0";

            // VisualElement landmarksCountContainer = CreateMetadataItem("Landmarks", landmarksCount, icons?.landmarkIcon);
            // metadataContainer.Add(landmarksCountContainer);

            // // Screenshots count 
            // string screenshotCount = ScreenshotsCore.data.scenes
            //     .Where(anchor => anchor.name == scene.Name)
            //     .Select(anchor => anchor.Screenshots.Count.ToString())
            //     .FirstOrDefault() ?? "0";
            // VisualElement screenshotsCountContainer = CreateMetadataItem("Screenshots", screenshotCount.ToString(), icons?.screenshotIcon);
            // metadataContainer.Add(screenshotsCountContainer);

            metadataContainer.style.marginTop = 10;

            contentContainer.Add(metadataContainer);

            sceneCard.Add(contentContainer);

            // Clickable area to open scene details (excluding the delete button)
            sceneCard.RegisterCallback<ClickEvent>(evt =>
            {
                // Prevent triggering when clicking the delete button
                if (evt.target == deleteButton)
                    return;

                state.selectedScene = scene.Name;
                ShowSceneDetails(scene);
            });

            // Hover effect: Scale up the card slightly
            sceneCard.RegisterCallback<MouseEnterEvent>(evt =>
            {
                sceneCard.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
            });
            sceneCard.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                sceneCard.style.scale = new StyleScale(new Scale(new Vector3(0.95f, 0.95f, 1f)));
            });

            return sceneCard;
        }

        private void ShowSceneDetails(SceneData scene)
        {
            // Clear previous content
            sceneDetails.Clear();

            // Start data collection if not already collecting
            if (!scene.IsCollecting && (scene.CollectedData == null || scene.CollectedData.Count == 0))
            {
                scene.StartCollection();
                EditorApplication.update += scene.UpdateDeepDive;
            }


            // Overlay background
            sceneDetails.style.display = DisplayStyle.Flex;
            sceneDetails.style.flexDirection = FlexDirection.Column;
            sceneDetails.style.position = Position.Absolute;
            sceneDetails.style.top = 0;
            sceneDetails.style.left = 0;
            sceneDetails.style.right = 0;
            sceneDetails.style.bottom = 0;
            sceneDetails.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);

            // Header section
            VisualElement header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart, // Changed to align items properly
                    alignItems = Align.Center,
                    paddingLeft = 20,
                    paddingRight = 20,
                    paddingTop = 10,
                    paddingBottom = 10,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                }
            };

            // Level icon
            Image sceneImage = new Image
            {
                image = GetSceneImage(scene),
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = 64,
                    height = 64,
                    marginRight = 10,
                }
            };
            header.Add(sceneImage);

            // Scene title
            Label sceneTitle = new Label(scene.Name.Split("___").First())
            {
                style =
                {
                    color = Color.white,
                    fontSize = 20,
                    unityFontStyleAndWeight = FontStyle.Bold,
                }
            };
            header.Add(sceneTitle);

            // Spacer
            VisualElement spacer = new VisualElement
            {
                style = { flexGrow = 1 }
            };
            header.Add(spacer);

            // Close button
            Button closeButton = new Button
            {
                text = "X",
                style =
                {
                    width = 30,
                    height = 30,
                    backgroundColor = new Color(0.8f, 0f, 0f, 0.8f),
                    color = Color.white,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    borderTopLeftRadius = 15,
                    borderTopRightRadius = 15,
                    borderBottomLeftRadius = 15,
                    borderBottomRightRadius = 15,
                }
            };
            closeButton.clicked += () =>
            {
                sceneDetails.style.display = DisplayStyle.None;
                state.selectedScene = "";
            };
            header.Add(closeButton);

            sceneDetails.Add(header);


            // Content section
            VisualElement contentSection = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f),
                    paddingLeft = 20,
                    paddingRight = 20,
                    paddingTop = 10,
                    paddingBottom = 10,
                }
            };

            // Tabs Navigation
            VisualElement tabBar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = 30,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                }
            };

            // List to keep track of tabs and their associated content
            List<(Button tabButton, string tabName)> tabs = new List<(Button, string)>();

            // Predefined tabs
            tabs.Add((CreateTabButton("Scene Data"), "Scene Data"));

            // Dynamically add tabs from MappableTypesInfo
            foreach (var typeInfo in MightyCore.data.MappableTypesInfo)
            {
                tabs.Add((CreateTabButton(typeInfo.Name), typeInfo.Name));
            }

            // Add tabs to the tabBar
            foreach (var (tabButton, _) in tabs)
            {
                tabBar.Add(tabButton);
            }

            contentSection.Add(tabBar);

            // Tab Content Area
            VisualElement tabContentArea = new VisualElement
            {
                name = "TabContentArea",
                style =
        {
            flexGrow = 1,
            backgroundColor = new Color(0.1f, 0.1f, 0.1f),
            paddingTop = 10,
        }
            };
            contentSection.Add(tabContentArea);

            sceneDetails.Add(contentSection);

            // Footer section
            VisualElement footer = new VisualElement
            {
                style =
        {
            height = 50,
            flexShrink = 0,
            flexGrow = 0,
            flexDirection = FlexDirection.Row,
            justifyContent = Justify.Center,
            alignItems = Align.Center,
            backgroundColor = new Color(0.2f, 0.2f, 0.2f),
        }
            };

            Button loadSceneButton = new Button
            {
                text = "Load Scene",
                style =
                {
                    width = 120,
                    height = 30,
                    marginRight = 10,
                    marginTop = -10,
                    backgroundColor = Color.black,
                    color = Color.white,
                }
            };
            loadSceneButton.clicked += () =>
            {
                EditorSceneManager.OpenScene(scene.ScenePath, OpenSceneMode.Single);
            };

            Button addToSceneButton = new Button
            {
                text = "Add to Scene",
                style =
        {
            width = 120,
            height = 30,
                                marginTop = -10,
            backgroundColor = Color.black,
            color = Color.white,
        }
            };
            addToSceneButton.clicked += () =>
            {
                EditorSceneManager.OpenScene(scene.ScenePath, OpenSceneMode.Additive);
            };

            footer.Add(loadSceneButton);
            footer.Add(addToSceneButton);

            sceneDetails.Add(footer);

            // Show the first tab by default
            ShowTabContent(scene, "Scene Data");

            // Helper method to create a tab button
            Button CreateTabButton(string tabName)
            {
                Button tabButton = new Button
                {
                    text = tabName,
                    style =
            {
                flexGrow = 1,
                unityTextAlign = TextAnchor.MiddleCenter,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                color = Color.white,
                borderLeftWidth = 0,
                borderRightWidth = 0,
                borderTopWidth = 0,
                borderBottomWidth = 0,
            }
                };

                tabButton.clicked += () =>
                {
                    // Reset all tabs' background color
                    foreach (var (btn, _) in tabs)
                    {
                        btn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                    }

                    // Highlight the selected tab
                    tabButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);

                    // Show the corresponding content
                    ShowTabContent(scene, tabName);
                };

                return tabButton;
            }
        }

        private void ShowTabContent(SceneData scene, string tabName)
        {
            VisualElement tabContentArea = sceneDetails.Q<VisualElement>("TabContentArea");
            tabContentArea.Clear();

            if (tabName == "Scene Data")
            {
                CreateSceneDataContent(scene, tabContentArea);
            }
            else
            {
                // Check if the tabName matches any MappableTypeInfo
                var typeInfo = MightyCore.data.MappableTypesInfo.FirstOrDefault(t => t.Name == tabName);
                if (typeInfo != null)
                {
                    // Fetch content from the Mappable's SceneSummary method
                    VisualElement content = typeInfo.Mappable.SceneSummary(scene);
                    if (content != null)
                    {
                        tabContentArea.Add(content);
                    }
                    else
                    {
                        Label placeholder = new Label($"{tabName} Content is not available.");
                        tabContentArea.Add(placeholder);
                    }
                }
                else
                {
                    // Handle other predefined tabs if any
                    Label placeholder = new Label($"{tabName} Content is not available.");
                    tabContentArea.Add(placeholder);
                }
            }
        }

        private Texture2D GetSceneImage(SceneData scene)
        {
            string mapPath = scene.MiniMap.MapPath;
            if (mapPath != null)
            {
                return Resources.Load<Texture2D>(mapPath);
            }

            // // Ensure LandmarksCore data is loaded
            // if (LandmarksCore.data == null)
            //     LandmarksCore.GetSceneData();

            // foreach (var anchor in LandmarksCore.data.scenes)
            // {
            //     if (anchor.landmarks == null) continue;
            //     if (anchor.name == scene.Name)
            //     {
            //         if (anchor.landmarks.Count > 0)
            //         {
            //             anchor.landmarks[0].LoadImage();
            //             return anchor.landmarks[0].Pic.img; // Ensure Pic.img is a Texture2D
            //         }
            //     }
            // }

            // Fallback to a default texture if no image is available
            return icons?.mightybot; // Assign a default Texture2D in your Icons class
        }


        private VisualElement CreateMetadataItem(string label, string value, Texture2D icon)
        {
            VisualElement container = new VisualElement
            {
                style =
        {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
            marginBottom = 5, // Add spacing between metadata items
        }
            };

            if (icon != null)
            {
                Image iconImage = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    style =
            {
                width = 16,
                height = 16,
                marginRight = 5,
            }
                };
                container.Add(iconImage);
            }

            Label valueLabel = new Label($"{value} {label}")
            {
                style =
        {
            color = Color.white,
            fontSize = 12,
        }
            };
            container.Add(valueLabel);

            return container;
        }


        #endregion

        #region ShowSceneInfoPanel Method and Helper Methods
        private void ShowSceneInfoPanel(VisualElement sceneInfo, SceneData scene)
        {
            DevLog("Left-clicked on scene node");
            view.style.borderBottomColor = view.style.borderLeftColor = view.style.borderRightColor = view.style.borderTopColor = Color.white;
            sceneInfo.Clear();
            sceneInfo.style.display = DisplayStyle.Flex;

            VisualElement loadButtons = CreateLoadButtons(scene);
            sceneInfo.Add(loadButtons);

            VisualElement tabContainer = CreateTabContainer(scene);
            sceneInfo.Add(tabContainer);

            sceneInfo.style.height = Length.Percent(100);
        }

        private Texture2D GetLevelIcon(SceneData scene)
        {
            return icons.mightybot;
            // Assuming scene.LevelType is a string indicating the scene type
            // switch (scene.LevelType)
            // {
            //     case "Environment":
            //         return icons.environmentIcon;
            //     case "Overlay":
            //         return icons.overlayIcon;
            //     // Add cases for other scene types
            //     default:
            //         return icons.defaultSceneIcon;
            // }
        }


        private VisualElement CreateLoadButtons(SceneData scene)
        {
            VisualElement loadButtons = new VisualElement
            {
                name = "LoadButtons",
                style = {
                    flexDirection = FlexDirection.Row,
                }
            };

            Button loadScene = new Button
            {
                name = "LoadScene",
                text = "Load Scene",
                style ={
                    width = 100,
                    backgroundColor = Color.black,
                    color = Color.white,
                }
            };
            loadScene.clicked += () =>
            {
                DevLog("Loading scene");
                EditorSceneManager.OpenScene(scene.ScenePath, OpenSceneMode.Single);
            };
            loadButtons.Add(loadScene);

            Button loadSceneAdd = new Button
            {
                name = "LoadSceneAdd",
                text = "Add To Scene",
                style = {
                    width = 100,
                    backgroundColor = Color.black,
                    color = Color.white,
                }
            };
            loadSceneAdd.clicked += () =>
            {
                DevLog("Loading scene additively");
                EditorSceneManager.OpenScene(scene.ScenePath, OpenSceneMode.Additive);
            };
            loadButtons.Add(loadSceneAdd);

            return loadButtons;
        }

        private VisualElement CreateTabContainer(SceneData scene)
        {
            VisualElement tabContainer = new VisualElement
            {
                name = "TabContainer",
                style = {
                    flexGrow = 1,
                    flexShrink = 0,
                }
            };

            ScrollView tabScrollView = new ScrollView
            {
                name = "TabScrollView",
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                style = {
                    flexGrow = 0,
                    flexDirection = FlexDirection.Row,
                    overflow = Overflow.Hidden,
                    maxHeight = 64,
                }
            };
            tabContainer.Add(tabScrollView);

            VisualElement tabBar = new VisualElement
            {
                name = "TabBar",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 0,
                    flexShrink = 1,
                }
            };
            tabScrollView.Add(tabBar);

            VisualElement tabContentArea = new VisualElement
            {
                name = "TabContentArea",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    flexShrink = 0,
                }
            };
            tabContainer.Add(tabContentArea);

            // Create tabs and their content
            CreateTabs(scene, tabBar, tabContentArea);

            return tabContainer;
        }

        private void CreateTabs(SceneData scene, VisualElement tabBar, VisualElement tabContentArea)
        {
            // Example of creating a tab for scene data
            Button sceneDataTab = new Button
            {
                name = "SceneDataTab",
                text = "Scene Data",
                style = {
                    backgroundColor = Color.white,
                    color = Color.black,
                }
            };

            VisualElement sceneDataContent = new VisualElement
            {
                name = "SceneDataContent",
                style = {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1,
                    flexShrink = 0,
                }
            };

            sceneDataTab.clicked += () =>
            {
                DevLog("Clicked on scene data tab");
                state.selectedTab = 1;
                foreach (VisualElement child in tabContentArea.Children())
                {
                    child.style.display = DisplayStyle.None;
                }
                sceneDataContent.style.display = DisplayStyle.Flex;
                tabContentArea.Add(sceneDataContent);
            };
            tabBar.Add(sceneDataTab);

            // Create content for the scene data tab
            CreateSceneDataContent(scene, sceneDataContent);

            // Handle other tabs, including dynamically adding tabs based on MappableTypes
            int i = 2;
            foreach (var typeInfo in MightyCore.data.MappableTypesInfo)
            {
                Button typeTab = new Button
                {
                    name = typeInfo.Name + "Tab",
                    text = typeInfo.Name,
                    style = {
                        backgroundColor = Color.white,
                        color = Color.black,
                    }
                };

                typeTab.clicked += () =>
                {
                    DevLog($"Clicked on tab {typeInfo.Name}");
                    state.selectedTab = i;
                    ShowContent(scene, tabContentArea, typeInfo);
                };
                if (i == state.selectedTab)
                {
                    ShowContent(scene, tabContentArea, typeInfo);
                }
                tabBar.Add(typeTab);
                i++;
            }
        }



        private void CreateSceneDataContent(SceneData scene, VisualElement tabContentArea)
        {
            List<SceneData.GameObjectData> results = new List<SceneData.GameObjectData>();
            int itemsLoaded = 0;
            const int itemsPerLoad = 50;

            VisualElement searchContainer = new()
            {
                style = {
        flexDirection = FlexDirection.Row,
        alignItems = Align.Center,
        justifyContent = Justify.FlexStart,
        flexGrow =1 ,
        flexShrink =0,
        maxHeight = 30,
    }
            };
            // Search Field
            ToolbarPopupSearchField searchField = new ToolbarPopupSearchField
            {
                style =
        {
            // width = Length.Percent(100),
            flexGrow = 2,
            flexShrink = 2,
            marginBottom = 10,
        }
            };
            searchField.value = searchQuery;
            sceneSelectSearchField.value = searchQuery;

            // Add search field to tab content area
            searchContainer.Add(searchField);

            Button refreshData = new Button()
            {
                name = "RefreshData",
                text = "Refresh",
                style = {
                    flexGrow = 1,
                    flexShrink = 1,
                    marginBottom = 10,
                    height = 16,
                }
            };
            refreshData.clicked += () =>
            {
                sceneData.IsCollecting = true;
                sceneData.CurrentIndex = 0;
                sceneData.CollectedData = new List<SceneData.GameObjectData>();
                ShowToast("Indexing Scene Data...");
                sceneData.StartCollection();
                // Debug.Log($"IsCollecting: {sceneData.IsCollecting}");
                EditorApplication.update += scene.UpdateDeepDive;
            };
            searchContainer.Add(refreshData);

            tabContentArea.Add(searchContainer);
            // Sticky Header Row for column names
            VisualElement headerRow = new VisualElement
            {
                style = {
            flexDirection = FlexDirection.Row,
            height = 20,
            alignItems = Align.Center,
            justifyContent = Justify.FlexStart,
            backgroundColor = new Color(0.2f, 0.2f, 0.2f),
            paddingLeft = 5,
            paddingRight = 5,
            marginBottom = 5,
            borderBottomWidth = 1,
            borderBottomColor = Color.black
        }
            };

            // Add header labels
            headerRow.Add(CreateHeaderLabel("P", 26));
            headerRow.Add(CreateHeaderLabel("A", 26));
            headerRow.Add(CreateHeaderLabel("S", 26));
            headerRow.Add(CreateHeaderLabel("Layer", 60, TextAnchor.MiddleCenter));
            headerRow.Add(CreateHeaderLabel("GameObjects", 200, TextAnchor.MiddleLeft));

            // Add the header row before the scroll area
            tabContentArea.Add(headerRow);

            // ScrollView for displaying the game objects
            ScrollView sceneDataContentObjects = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
        {
            flexGrow = 1,
            flexDirection = FlexDirection.Column,
            width = Length.Percent(100),
        },
            };

            tabContentArea.Add(sceneDataContentObjects);

            // Function to perform search and refresh results
            void PerformSearch()
            {
                results = scene.SearchCollectedData(searchQuery, currentSearchType, isCaseSensitive);
                itemsLoaded = 0;
                sceneDataContentObjects.Clear();
                LoadMoreData();
            }

            searchField.RegisterValueChangedCallback(evt =>
            {
                searchQuery = evt.newValue;
                sceneSelectSearchField.value = searchQuery;
                PerformSearch();
            });
            // Function to load more data
            void LoadMoreData()
            {
                int itemsToLoad = Math.Min(itemsPerLoad, results.Count - itemsLoaded);
                for (int i = 0; i < itemsToLoad; i++)
                {
                    var gameObjectData = results[itemsLoaded];
                    var gameObjectElement = CreateGameObjectElement(gameObjectData);
                    sceneDataContentObjects.Add(gameObjectElement);
                    itemsLoaded++;
                }
            }

            // Initial search to populate the list
            PerformSearch();

            // Load more data as the user scrolls to the bottom
            sceneDataContentObjects.verticalScroller.valueChanged += (newValue) =>
            {
                if (newValue >= sceneDataContentObjects.verticalScroller.highValue)
                {
                    LoadMoreData();
                }
            };
        }

        private Label CreateHeaderLabel(string text, float width, TextAnchor textAlign = TextAnchor.MiddleCenter)
        {
            return new Label(text)
            {
                style = {
            width = width,
            unityTextAlign = textAlign,
            fontSize = 12,
            unityFontStyleAndWeight = FontStyle.Bold,
            color = Color.white,
            justifyContent = Justify.Center,
            borderRightWidth = 1,
            // borderRightColor = Color.black
        }
            };
        }

        private VisualElement CreateGameObjectElement(SceneData.GameObjectData gameObjectData)
        {
            // Root container for each row
            VisualElement row = new VisualElement
            {
                style =
        {
            flexDirection = FlexDirection.Row,
            alignItems = Align.FlexStart,
            paddingLeft = 5,
            paddingRight = 5,
            paddingTop = 2,
            paddingBottom = 2,
            marginBottom = 5,
            backgroundColor = new Color(0.15f, 0.15f, 0.15f),
            borderBottomWidth = 1,
            borderBottomColor = new Color(0.2f, 0.2f, 0.2f),
            justifyContent = Justify.FlexStart
        }
            };

            // Prefab icon
            VisualElement prefabIcon = CreateFlagIcon(gameObjectData.IsPrefab, "P", icons.prefabOn, icons.prefabOff);
            row.Add(prefabIcon);

            // Active icon
            VisualElement activeIcon = CreateFlagIcon(gameObjectData.IsActive, "A", icons.activeOn, icons.activeOff);
            row.Add(activeIcon);

            // Static icon
            VisualElement staticIcon = CreateFlagIcon(gameObjectData.IsStatic, "S", icons.staticOn, icons.staticOff);
            row.Add(staticIcon);

            // Layer label
            Label layerLabel = new Label(gameObjectData.Layer)
            {
                style =
        {
            width = 60,
            unityTextAlign = TextAnchor.MiddleCenter,
            color = Color.white,
            justifyContent = Justify.Center
        }
            };
            row.Add(layerLabel);

            // Foldout for GameObject details
            Foldout gameObjectFoldout = new Foldout
            {
                text = gameObjectData.Name,
                value = false,
                style =
        {
            flexGrow = 1,
            unityTextAlign = TextAnchor.MiddleLeft,
            color = Color.white
        }
            };
            row.Add(gameObjectFoldout);

            // Components container
            VisualElement componentsContainer = new VisualElement
            {
                style =
        {
            flexDirection = FlexDirection.Column,
            paddingLeft = 20,
        }
            };

            // List components
            foreach (var component in gameObjectData.Components)
            {
                Label componentLabel = new Label(component.TypeName)
                {
                    style =
            {
                color = Color.white,
                marginBottom = 2,
            }
                };
                componentsContainer.Add(componentLabel);
            }

            // Add components to the foldout
            gameObjectFoldout.Add(componentsContainer);

            return row;
        }




        private void ShowContent(SceneData scene, VisualElement tabContentArea, MappableTypeInfo typeInfo)
        {
            foreach (VisualElement child in tabContentArea.Children())
            {
                child.style.display = DisplayStyle.None;
            }
            VisualElement content = typeInfo.Mappable.SceneSummary(scene);

            if (content != null)
            {
                content.style.display = DisplayStyle.Flex;
                tabContentArea.Add(content);
            }
        }

        private void LoadMoreData(ScrollView sceneDataContentObjects)
        {
            // Implementation for loading more data into the scroll view
        }

        private void OnScrollChanged(float newValue, ScrollView sceneDataContentObjects)
        {
            // Implementation for handling scroll changes
        }

        // private VisualElement CreateGameObjectElement(SceneData.GameObjectData gameObjectData)
        // {
        //     // Create a Foldout with a custom header
        //     Foldout foldout = new Foldout
        //     {
        //         value = false,
        //         style =
        // {
        //     flexDirection = FlexDirection.Column,
        //     paddingTop = 5,
        //     paddingBottom = 5,
        //     borderBottomWidth = 1,
        //     borderBottomColor = new Color(0.2f, 0.2f, 0.2f),
        // }
        //     };

        //     // Customize the foldout header
        //     VisualElement foldoutHeader = new VisualElement
        //     {
        //         style =
        // {
        //     flexDirection = FlexDirection.Row,
        //     alignItems = Align.Center,
        // }
        //     };

        //     // Access the toggle part of the foldout and remove the default label
        //     Toggle foldoutToggle = foldout.Q<Toggle>();
        //     foldoutToggle.text = ""; // Remove default text

        //     // Arrow icon (rotates automatically in Foldout)
        //     Image arrowIcon = new Image
        //     {
        //         image = EditorGUIUtility.IconContent("d_ArrowRight").image,
        //         style =
        // {
        //     width = 16,
        //     height = 16,
        //     marginRight = 5,
        // }
        //     };
        //     foldoutToggle.Add(arrowIcon);

        //     // GameObject name
        //     Label nameLabel = new Label(gameObjectData.Name)
        //     {
        //         style =
        // {
        //     color = Color.white,
        //     flexGrow = 1,
        // }
        //     };
        //     foldoutHeader.Add(nameLabel);

        //     // Component count
        //     int componentCount = gameObjectData.Components.Count;
        //     Label componentCountLabel = new Label($"({componentCount})")
        //     {
        //         style =
        // {
        //     color = Color.gray,
        //     marginRight = 10,
        // }
        //     };
        //     foldoutHeader.Add(componentCountLabel);

        //     // Active flag
        //     VisualElement activeIcon = CreateFlagIcon(gameObjectData.IsActive, "Active");
        //     foldoutHeader.Add(activeIcon);

        //     // Static flag
        //     VisualElement staticIcon = CreateFlagIcon(gameObjectData.IsStatic, "Static");
        //     foldoutHeader.Add(staticIcon);

        //     // Layer
        //     Label layerLabel = new Label(gameObjectData.Layer)
        //     {
        //         style =
        // {
        //     color = Color.white,
        //     width = 100,
        //     unityTextAlign = TextAnchor.MiddleRight,
        // }
        //     };
        //     foldoutHeader.Add(layerLabel);

        //     // Add the custom header to the foldout toggle
        //     foldoutToggle.Add(foldoutHeader);

        //     // Components container
        //     VisualElement componentsContainer = new VisualElement
        //     {
        //         style =
        // {
        //     flexDirection = FlexDirection.Column,
        //     paddingLeft = 20,
        // }
        //     };

        //     // List components
        //     foreach (var component in gameObjectData.Components)
        //     {
        //         Label componentLabel = new Label(component.TypeName)
        //         {
        //             style =
        //     {
        //         color = Color.white,
        //         marginBottom = 2,
        //     }
        //         };
        //         componentsContainer.Add(componentLabel);
        //     }

        //     // Add components to the foldout
        //     foldout.Add(componentsContainer);

        //     return foldout;
        // }


        private VisualElement CreateFlagIcon(bool flagValue, string name, Texture2D onIcon, Texture2D offIcon)
        {
            VisualElement iconContainer = new VisualElement
            {
                tooltip = name,
                style =
        {
            width = 16,
            height = 16,
            backgroundImage = flagValue ? onIcon : offIcon,
            backgroundColor = Color.clear,
            marginLeft = 5,
            marginRight = 5,
        }
            };

            return iconContainer;
        }

        #endregion

        private void OpenDetails()
        {
            DevLog($"Opening details for {state.selectedScene}");
            // Implementation for opening scene details
        }
    }
}
#endif