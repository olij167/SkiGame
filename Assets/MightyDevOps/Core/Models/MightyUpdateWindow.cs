#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;


namespace Mighty
{
    public class MightyUpdates : ScriptableObject
    {
        static Action saveState;
        public static void Save()
        {
            string path = $"{corePath}/Core/Data/MightyUpdatesData.asset";
            if (File.Exists(path))
            {
                DevLog($"{path} already exists...");
                return;
            }

            MightyUpdates asset = ScriptableObject.CreateInstance<MightyUpdates>();
            DevLog($"Saving MightyUpdatesData to {path}");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }


        public static MightyUpdates Load()
        {
            string path = $"{corePath}/Core/Data/MightyUpdatesData.asset";
            if (!File.Exists(path))
            {
                Save();
            }
            DevLog($"Loading MightyUpdatesData from {path}");
            return AssetDatabase.LoadAssetAtPath<MightyUpdates>(path);
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

        [System.Serializable]
        public class State
        {

        }

        [SerializeField]
        public State state;
        public VisualElement view;


        public void BuildView()
        {
            // MightyCore.data.SceneDupeCheck();
            // Debug.Log("Building Updates View");
            state ??= new State();
            if (view == null)
                view = new VisualElement
                {
                    name = "Updates",
                    style = {
                    height = Length.Percent(100),
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
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f)
                }
                };
            else view.Clear();

            VisualElement top = new()
            {
                name = "UpdatesTop",
                style = {
                flexDirection = FlexDirection.Row,
                flexGrow = 0,
                flexShrink = 0,
                height = 24,
                width = Length.Percent(100),
                justifyContent = Justify.SpaceBetween,
                backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.9f)),
            }
            };

            Label topTitle = new Label("UpdatesTitle")
            {
                text = "Updates Available",
                style = {
                fontSize = 16,
                color = new StyleColor(Color.white),
                marginLeft = 10,
                marginRight = 10,
                marginTop = 0,
                marginBottom = 0,
                unityFontStyleAndWeight = FontStyle.Bold,
            }
            };
            top.Add(topTitle);


            ScrollView mid = new()
            {
                name = "UpdatesMid",
                style = {
                flexDirection = FlexDirection.Row,
                flexGrow = 1,
                flexShrink = 1,
                height = Length.Percent(100),
                width = Length.Percent(100),
            }
            };


            foreach (var item in dataCore.moduleUpdates)
            {
                // Create a container for each news item
                VisualElement newsCard = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingBottom = 10,
                        paddingLeft = 10,
                        paddingRight = 10,
                        paddingTop = 10,
                        marginBottom = 5,
                        marginLeft = 5,
                        marginRight = 5,
                        marginTop = 5,
                        backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f)), // Dark grey
                        borderTopLeftRadius = 5,
                        borderTopRightRadius = 5,
                        borderBottomLeftRadius = 5,
                        borderBottomRightRadius = 5,
                        borderRightWidth = new StyleFloat(2),
                        borderBottomWidth = new StyleFloat(2),
                        borderLeftWidth = new StyleFloat(2),
                        borderTopWidth = new StyleFloat(2),
                        borderBottomColor = new StyleColor(Color.grey),
                        borderLeftColor = new StyleColor(Color.grey),
                        borderRightColor = new StyleColor(Color.grey),
                        borderTopColor = new StyleColor(Color.grey),
                    }
                };

                // Title Label
                Label title = new Label(item.module)
                {
                    style =
                    {
                        fontSize = 18,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new StyleColor(Color.white),
                        marginBottom = 5, // Space between title and content
                        marginLeft = 0,
                        marginRight = 0,
                        marginTop = 0,
                    }
                };

                // Date Label
                DateTime postedDate = DateTime.Parse(item.date_posted); // Assuming date_posted is in a standard DateTime format
                Label datePosted = new Label(item.version)
                {
                    style =
                    {
                        fontSize = 8,
                        color = new StyleColor(Color.gray),
                        marginBottom = 5, // Space between date and content
                    }
                };

                // Content Label
                Label content = new Label(item.notes)
                {
                    style =
                    {
                        fontSize = 14,
                        color = new StyleColor(Color.white),
                        whiteSpace = WhiteSpace.Normal,
                    }
                };



                var newsCardInstance = newsCard;


                VisualElement container = new VisualElement
                {
                    style = {
                          flexDirection = FlexDirection.Row,
                          flexGrow = 1,
                          flexShrink = 1,
                          justifyContent = Justify.SpaceBetween,
                          height = Length.Percent(100),
                          width = Length.Percent(100),
                          }
                }
                ;

                container.Add(datePosted);

                // Add elements to the card
                newsCard.Add(container);
                newsCard.Add(title);
                newsCard.Add(content);

                Button getUpdate = new Button(() =>
                {
                    EditorGUIUtility.systemCopyBuffer = item.module;
                    EditorUtility.DisplayDialog($"Upgrade {item.module}", $"Asset name '{item.module}' copied to clipboard, paste it in the search box!", "OK");
                    EditorApplication.ExecuteMenuItem("Window/Package Manager");
                    ShowToast($"'{item.module}' copied! Paste it in the search box.");
                })
                {
                    name = "gotoURL",
                    text = "Get Update Now",
                    style ={
                    width = 100,
                    height = 20,
                    backgroundColor = Color.blue,
                    color = Color.white,
                }
                };

                newsCard.Add(getUpdate);



                // Debug.Log($"newsCard child count: {newsCard.childCount}");

                // Add the card to the mid container (assuming 'mid' is your container for these elements)
                mid.Add(newsCard);
            }


            VisualElement midContent = new()
            {
                name = "UpdatesMidContent",
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

            // VisualElement mid = new()
            // {
            //     name = "UpdatesMid",
            //     style = {
            //         flexDirection = FlexDirection.Row,
            //         flexGrow = 1,
            //         flexShrink = 1,
            //         height = Length.Percent(100),
            //         width = Length.Percent(100),
            //     }
            // };

            VisualElement bottom = new()
            {
                name = "UpdatesBottom",
                style = {
                flexDirection = FlexDirection.Row,
                flexGrow = 0,
                flexShrink = 0,
                height = 20,
                width = Length.Percent(100),
            }
            };

            view.Add(top);
            view.Add(mid);
            view.Add(bottom);


            // Debug.Log("Updates View Built");
            // Debug.Log($"top child count: {top.childCount} / mid child count: {mid.childCount} / bottom child count: {bottom.childCount}");
            // Debug.Log($"view parent: {view.parent} / view child count: {view.childCount}");

        }

        [MenuItem("Assets/Create/Mighty/Mighty News")]
        public static void CreateMightyNewsAsset()
        {
            MightyUpdates news = ScriptableObject.CreateInstance<MightyUpdates>();
            AssetDatabase.CreateAsset(news, "Assets/MightyDevOps/Core/Models/MightyUpdatesData.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = news;
        }
    }
}
#endif