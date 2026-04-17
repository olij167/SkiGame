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
    public class MightyNotifications : ScriptableObject
    {
        static Action saveState;
        public static void Save()
        {
            string path = $"{corePath}/Core/Data/MightyNotificationsData.asset";
            if (File.Exists(path))
            {
                DevLog($"{path} already exists...");
                return;
            }

            MightyNotifications asset = ScriptableObject.CreateInstance<MightyNotifications>();
            DevLog($"Saving MightyNotificationsData to {path}");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }


        public static MightyNotifications Load()
        {
            string path = $"{corePath}/Core/Data/MightyNotificationsData.asset";
            if (!File.Exists(path))
            {
                Save();
            }
            DevLog($"Loading MightyNotificationsData from {path}");
            return AssetDatabase.LoadAssetAtPath<MightyNotifications>(path);
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

        private bool mainView = true;

        public void ShowLoadingState()
        {
            state ??= new State();
            BuildViewStructure();

            // Clear the scroll view and show loading message
            var scrollView = view.Q<ScrollView>("NotificationsMid");
            scrollView.Clear();

            // Create loading container
            VisualElement loadingContainer = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    height = Length.Percent(100),
                    width = Length.Percent(100),
                    paddingTop = 60,
                    paddingBottom = 60,
                }
            };

            // Loading icon (you can replace with a spinner if you have one)
            VisualElement loadingIcon = new VisualElement
            {
                style = {
                    width = 32,
                    height = 32,
                    backgroundImage = icons.notificationOnIcon,
                    marginBottom = 16,
                }
            };

            // Loading text
            Label loadingText = new Label("Getting Latest News...")
            {
                style = {
                    fontSize = 16,
                    color = new Color(0.5f, 0.5f, 0.5f, 1f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Normal,
                }
            };

            loadingContainer.Add(loadingIcon);
            loadingContainer.Add(loadingText);
            scrollView.Add(loadingContainer);
        }

        public void BuildView()
        {
            // MightyCore.data.SceneDupeCheck();
            // Debug.Log($"Building Notifications View dataCore.newsItems.Count: {dataCore.newsItems.Count}");
            state ??= new State();
            BuildViewStructure();
            PopulateContent();
        }

        private void BuildViewStructure()
        {
            if (view == null)
                view = new VisualElement
                {
                    name = "Notifications",
                    style = {
                    height = Length.Percent(100),
                    flexGrow = 0,
                    flexDirection = FlexDirection.Column,
                    flexShrink = 0,
                    overflow = Overflow.Hidden,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.SpaceAround,
                    minHeight = 320,
                    maxHeight = 600,
                    minWidth = 380,
                    maxWidth = 650,
                    backgroundColor = new Color(0.98f, 0.98f, 0.98f, 1f), // Clean light background
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                }
                };
            else view.Clear();

            // Modern header with gradient-like effect
            VisualElement top = new()
            {
                name = "NotificationsTop",
                style = {
                flexDirection = FlexDirection.Row,
                flexGrow = 0,
                flexShrink = 0,
                height = 56,
                width = Length.Percent(100),
                justifyContent = Justify.SpaceBetween,
                alignItems = Align.Center,
                backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f, 1f)), // Deep dark header
                paddingLeft = 20,
                paddingRight = 20,
                borderTopLeftRadius = 8,
                borderTopRightRadius = 8,
            }
            };

            // Header container for icon and title
            VisualElement titleContainer = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                }
            };

            // Header icon
            VisualElement headerIcon = new VisualElement
            {
                style = {
                    width = 20,
                    height = 20,
                    backgroundImage = mainView ? icons.newsIcon : icons.archiveIcon,
                    marginRight = 8,
                }
            };

            Label topTitle = new Label("NotificationsTitle")
            {
                text = mainView ? "Latest News" : "Archives",
                style = {
                fontSize = 18,
                color = new StyleColor(Color.white),
                marginLeft = 0,
                marginRight = 0,
                marginTop = 0,
                marginBottom = 0,
                unityFontStyleAndWeight = FontStyle.Bold,
                letterSpacing = 0.5f,
            }
            };

            titleContainer.Add(headerIcon);
            titleContainer.Add(topTitle);
            top.Add(titleContainer);

            var viewInstance = view;
            Button button = new Button(() =>
            {
                mainView = !mainView;
                BuildView();
            })
            {
                text = mainView ? "View Archives" : "Back to News",
                style = {
                width = 120,
                height = 32,
                backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f), // Modern blue
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

            // Add hover effect for button
            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.backgroundColor = new Color(0.15f, 0.35f, 0.75f, 1f);
            });
            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
            });

            top.Add(button);

            ScrollView mid = new()
            {
                name = "NotificationsMid",
                style = {
                flexDirection = FlexDirection.Column,
                flexGrow = 1,
                flexShrink = 1,
                height = Length.Percent(100),
                width = Length.Percent(100),
                paddingLeft = 16,
                paddingRight = 16,
                paddingTop = 16,
                paddingBottom = 16,
            }
            };

            view.Add(top);
            view.Add(mid);
        }

        private void PopulateContent()
        {
            var mid = view.Q<ScrollView>("NotificationsMid");
            mid.Clear();

            dataCore.newsItems.Sort((item1, item2) => DateTime.Parse(item2.date_posted).CompareTo(DateTime.Parse(item1.date_posted)));
            notifications.style.backgroundImage = icons.notificationOffIcon;

            foreach (var item in dataCore.newsItems)
            {
                item.isRead = true;
                if (mainView)
                {
                    if (item.archived) continue;
                }
                else
                {
                    if (!item.archived) continue;
                }

                // Modern card design
                VisualElement newsCard = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingBottom = 20,
                        paddingLeft = 20,
                        paddingRight = 20,
                        paddingTop = 20,
                        marginBottom = 12,
                        marginLeft = 0,
                        marginRight = 0,
                        marginTop = 0,
                        backgroundColor = new StyleColor(Color.white),
                        borderTopLeftRadius = 12,
                        borderTopRightRadius = 12,
                        borderBottomLeftRadius = 12,
                        borderBottomRightRadius = 12,
                        borderRightWidth = new StyleFloat(1),
                        borderBottomWidth = new StyleFloat(1),
                        borderLeftWidth = new StyleFloat(1),
                        borderTopWidth = new StyleFloat(1),
                        borderBottomColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f)),
                        borderLeftColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f)),
                        borderRightColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f)),
                        borderTopColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f)),
                    }
                };

                // Add subtle hover effect
                newsCard.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    newsCard.style.backgroundColor = new Color(0.99f, 0.99f, 0.99f, 1f);
                    newsCard.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.4f, 0.8f, 0.3f));
                    newsCard.style.borderLeftColor = new StyleColor(new Color(0.2f, 0.4f, 0.8f, 0.3f));
                    newsCard.style.borderRightColor = new StyleColor(new Color(0.2f, 0.4f, 0.8f, 0.3f));
                    newsCard.style.borderTopColor = new StyleColor(new Color(0.2f, 0.4f, 0.8f, 0.3f));
                });
                newsCard.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    newsCard.style.backgroundColor = Color.white;
                    newsCard.style.borderBottomColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                    newsCard.style.borderLeftColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                    newsCard.style.borderRightColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                    newsCard.style.borderTopColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                });

                // Header container with date and archive button
                VisualElement headerContainer = new VisualElement
                {
                    style = {
                        flexDirection = FlexDirection.Row,
                        justifyContent = Justify.SpaceBetween,
                        alignItems = Align.Center,
                        marginBottom = 12,
                        width = Length.Percent(100),
                    }
                };

                // Date with modern styling
                DateTime postedDate = DateTime.Parse(item.date_posted);
                Label datePosted = new Label(postedDate.ToString("MMM dd, yyyy"))
                {
                    style =
                    {
                        fontSize = 11,
                        color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f)),
                        backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f),
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 4,
                        paddingBottom = 4,
                        borderTopLeftRadius = 12,
                        borderTopRightRadius = 12,
                        borderBottomLeftRadius = 12,
                        borderBottomRightRadius = 12,
                        unityFontStyleAndWeight = FontStyle.Normal,
                    }
                };

                // Modern archive button
                Button archive = new(() =>
                {
                    item.archived = !item.archived;
                    newsCard.RemoveFromHierarchy();
                    ShowToast(item.archived ? $"Archived {item.title}" : $"Unarchived {item.title}");
                })
                {
                    name = "archive",
                    text = "",
                    style ={
                        width = 28,
                        height = 28,
                        backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f),
                        backgroundImage = item.archived ? icons.archiveIcon : icons.trashcanIcon,
                        borderTopLeftRadius = 14,
                        borderTopRightRadius = 14,
                        borderBottomLeftRadius = 14,
                        borderBottomRightRadius = 14,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                    }
                };

                // Archive button hover effect
                archive.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    archive.style.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                    archive.style.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                });
                archive.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    archive.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                    archive.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                });

                headerContainer.Add(datePosted);
                headerContainer.Add(archive);

                // Modern title
                Label title = new Label(item.title)
                {
                    style =
                    {
                        fontSize = 16,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f)),
                        marginBottom = 12,
                        marginLeft = 0,
                        marginRight = 0,
                        marginTop = 0,
                        whiteSpace = WhiteSpace.Normal,
                    }
                };

                // Modern content
                Label content = new Label(item.content)
                {
                    style =
                    {
                        fontSize = 13,
                        color = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f)),
                        whiteSpace = WhiteSpace.Normal,
                        marginBottom = 16,
                    }
                };

                // Modern URL button
                if (item.url != null && item.url != "")
                {
                    Button gotoURL = new Button(() =>
                    {
                        ShowToast($"Opening {item.title} in browser...");
                        Application.OpenURL(item.url);
                    })
                    {
                        name = "gotoURL",
                        text = "Read More →",
                        style ={
                            height = 36,
                            backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f),
                            color = Color.white,
                            borderTopLeftRadius = 18,
                            borderTopRightRadius = 18,
                            borderBottomLeftRadius = 18,
                            borderBottomRightRadius = 18,
                            borderTopWidth = 0,
                            borderBottomWidth = 0,
                            borderLeftWidth = 0,
                            borderRightWidth = 0,
                            fontSize = 12,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            paddingLeft = 16,
                            paddingRight = 16,
                        }
                    };

                    // URL button hover effect
                    gotoURL.RegisterCallback<MouseEnterEvent>(evt =>
                    {
                        gotoURL.style.backgroundColor = new Color(0.15f, 0.35f, 0.75f, 1f);
                    });
                    gotoURL.RegisterCallback<MouseLeaveEvent>(evt =>
                    {
                        gotoURL.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
                    });

                    newsCard.Add(headerContainer);
                    newsCard.Add(title);
                    newsCard.Add(content);
                    newsCard.Add(gotoURL);
                }
                else
                {
                    newsCard.Add(headerContainer);
                    newsCard.Add(title);
                    newsCard.Add(content);
                }

                mid.Add(newsCard);
            }

            // Mark all news as read since user opened the notifications window
            dataCore.hasUnreadNews = false;
        }

        // [MenuItem("Assets/Create/Mighty/Mighty News")]
        // public static void CreateMightyNewsAsset()
        // {
        //     MightyNotifications news = ScriptableObject.CreateInstance<MightyNotifications>();
        //     AssetDatabase.CreateAsset(news, "Assets/MightyDevOps/Core/Models/MightyNotificationsData.asset");
        //     AssetDatabase.SaveAssets();
        //     AssetDatabase.Refresh();
        //     EditorUtility.FocusProjectWindow();
        //     Selection.activeObject = news;
        // }
    }
}
#endif