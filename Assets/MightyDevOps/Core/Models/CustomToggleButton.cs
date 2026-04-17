#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;

namespace Mighty
{
    public class CustomToggleButton : VisualElement, INotifyValueChanged<bool>
    {
        //public event Action<bool> OnToggleStateChanged;

        private Texture2D onIcon;
        private Texture2D offIcon;
        public bool isToggledOn;
        private Label buttonName;
        private VisualElement optionsButton, updateButton;
        public MappableTypeInfo mappableTypeInfo;
        private VisualElement toggleElement;
        private string previewURL = "";

        private Action action;

        public CustomToggleButton(Texture2D onIcon, MappableTypeInfo mappableTypeInfoRef, string elementToToggle = "", Action actionToInvoke = null, string previewName = "", string URL = "")
        {
            OpenModuleSubMenu += OpenSubMenu;
            CloseModuleSubMenu += CloseSubMenu;

            toggleElement = GetSceneView().rootVisualElement.Q<VisualElement>(name: elementToToggle);

            if (actionToInvoke != null)
                action = actionToInvoke;


            mappableTypeInfo = mappableTypeInfoRef;

            previewURL = URL;

            if (onIcon == null)
            {
                onIcon = new Texture2D(1024, 1024);
                Color[] pixels = onIcon.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.white;
                }
                onIcon.SetPixels(pixels);
                onIcon.Apply();
            }

            this.onIcon = onIcon;
            offIcon = CreateGrayscaleIcon(onIcon);

            style.width = 32;
            style.height = 32;
            style.top = 0;
            style.left = 0;
            if (mappableTypeInfo != null)
            {
                isToggledOn = mappableTypeInfo.IsActive;
            }
            else
            {
                isToggledOn = true;
            }
            style.backgroundImage = isToggledOn ? onIcon : offIcon;

            style.transitionProperty = new List<StylePropertyName>
            {
                new StylePropertyName("left"),
                new StylePropertyName("top"),
                new StylePropertyName("width"),
                new StylePropertyName("height"),
            };

            style.transitionDuration = new List<TimeValue>()
            {
                new TimeValue(transitionSpeed, TimeUnit.Millisecond)
            };

            buttonName = new Label(previewName)
            {
                style =
            {
                width = 80,
                position = Position.Absolute,
                backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                color = Color.white,
                visibility = Visibility.Hidden,
                unityTextAlign = TextAnchor.MiddleCenter,
            }
            };
            if (mappableTypeInfo != null) buttonName.text = mappableTypeInfo.Name;

            this.Add(buttonName);

            updateButton = new VisualElement()
            {
                style =
                    {
                        width = 16,
                        height = 16,
                        left = 8,
                        top = 8,
                        position = Position.Absolute,
                        backgroundColor = new Color(0, 0.5f, 0), // Dark green
                        // backgroundImage = new StyleBackground(icons.upgradeIcon),
                    }
            };

            Label up = new()
            {
                text = "⬆",
                style =
                {
                    color = Color.white,
                    fontSize = 14,
                    width = 16,
                    height = 16,
                    unityTextAlign = TextAnchor.MiddleCenter,

                }
            };

            updateButton.Add(up);
            if (mappableTypeInfo != null)
                if (mappableTypeInfoRef.Mappable.UpdateAvailable())
                    this.Add(updateButton);

            updateButton.RegisterCallback<MouseDownEvent>((evt) =>
            {
                // EditorApplication.ExecuteMenuItem("Window/Package Manager");
                // void OnNewsFetchComplete()
                // {
                //     Debug.Log("News fetch operation completed.");
                //     //ICommand command = new OpenUpdateWindowCommand();
                //     //command.Execute();
                // }
                OpenUpdatesWindow?.Invoke();

                // EditorGUIUtility.systemCopyBuffer = mappableTypeInfo.Mappable.PackageName;
                // EditorUtility.DisplayDialog($"Upgrade {mappableTypeInfo.Mappable.PackageName}", "Asset name '{mappableTypeInfo.Mappable.PackageName}' copied to clipboard, paste it in the search box!", "OK");
                // EditorApplication.ExecuteMenuItem("Window/Package Manager");
                // ShowToast("Package name copied! Paste it in the search box.");

                evt.StopPropagation();
            });

            optionsButton = new VisualElement()
            {
                style =
            {
                width = 16,
                height = 16,
                position = Position.Absolute,
                backgroundImage = new StyleBackground(icons.blueGearIcon),
                visibility = Visibility.Hidden
            }
            };

            if (mappableTypeInfo != null)
                this.Add(optionsButton);


            RegisterCallback<MouseEnterEvent>(evt => ShowElements());
            RegisterCallback<MouseLeaveEvent>(evt => HideElements());

            if (mappableTypeInfo == null)
            {
                RegisterCallback<MouseDownEvent>(evt => Application.OpenURL(previewURL));
            }
            else
            {
                RegisterCallback<MouseDownEvent>(evt => OpenSettings());
            }




        }

        public float topCache = 0f;
        private void OpenSubMenu()
        {
            // if (selectedModule == this)
            // {
            //     // Assuming the parent container's top left corner is at (0, 0)
            //     // float targetTop = -this.worldBound.y + this.parent.worldBound.y;
            //     style.top = 0;//new StyleLength(targetTop);
            //     style.width = 48;
            //     style.height = 48;
            // }
            // else
            // {
            // Assuming the parent container's top left corner is at (0, 0)
            style.left = -96;
            style.top = topCache;
            // }
        }


        private void CloseSubMenu()
        {
            style.top = topCache;
            style.left = 0;
            // if (selectedModule == this)
            // {
            //     style.top = topCache;
            //     style.width = 96;
            //     style.height = 96;
            // }
            // else
            // {
            //     // Assuming the parent container's top left corner is at (0, 0)
            //     style.left = 0;
            // }
        }

        private void ShowElements()
        {
            buttonName.style.visibility = Visibility.Visible;
            optionsButton.style.visibility = Visibility.Visible;

            // Position the tooltip
            float tooltipX = (resolvedStyle.width - buttonName.resolvedStyle.width) / 2;
            float tooltipY = resolvedStyle.height - buttonName.resolvedStyle.height - 8;
            buttonName.style.left = tooltipX;
            buttonName.style.top = tooltipY;

            // Position the options button
            optionsButton.style.right = 8;
            optionsButton.style.top = 8;


        }

        private void HideElements()
        {
            buttonName.style.visibility = Visibility.Hidden;
            optionsButton.style.visibility = Visibility.Hidden;
        }

        private void OpenSettings()
        {
            selectedModule = this;
            if (moduleSubMenuActive)
            {
                CloseModuleSubMenu?.Invoke();
                moduleSubMenuActive = false;
            }
            else
            {
                OpenModuleSubMenu?.Invoke();
                moduleSubMenuActive = true;
            }
            OpenModuleSubMenu?.Invoke();
        }

        public bool value
        {
            get => isToggledOn;
            set
            {
                if (isToggledOn != value)
                {
                    isToggledOn = value;
                    var changeEvent = ChangeEvent<bool>.GetPooled(!isToggledOn, isToggledOn);
                    changeEvent.target = this;
                    SendEvent(changeEvent);
                    ValueChanged?.Invoke(changeEvent);
                    action?.Invoke();
                    // Debug.Log($"value: {value} isToggledOn: {isToggledOn} changeEvent.newValue: {changeEvent.newValue}");
                }
            }
        }

        public void Toggle()
        {
            Debug.Log($"Toggle: {previewURL}");
            if (mappableTypeInfo == null)
            {
                Debug.Log($"OpenURL: {previewURL}");
                Application.OpenURL(previewURL);
                return;
            }
            value = !value;
            style.backgroundImage = value ? onIcon : offIcon;  // Switch icon
            if (toggleElement != null)
                toggleElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            // DevLog($"toggleElement.name: {toggleElement.name}");
        }

        public void SetValueWithoutNotify(bool newValue)
        {
            isToggledOn = newValue;
            style.backgroundImage = new StyleBackground(isToggledOn ? onIcon : offIcon);  // Update icon
        }


        public EventCallback<ChangeEvent<bool>> ValueChanged { get; set; }

        private Texture2D CreateGrayscaleIcon(Texture2D original)
        {
            Texture2D grayTexture = new Texture2D(original.width, original.height);
            Color[] pixels = original.GetPixels();
            Color[] grayPixels = new Color[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                float grayValue = pixels[i].grayscale / 2;
                grayPixels[i] = new Color(grayValue, grayValue, grayValue, pixels[i].a);
            }

            grayTexture.SetPixels(grayPixels);
            grayTexture.Apply();

            return grayTexture;
        }
    }
}
#endif