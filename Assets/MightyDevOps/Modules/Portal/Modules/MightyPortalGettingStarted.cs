#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mighty;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static Mighty.MightyCoreData;

namespace MightyPortal
{
    public class MightyPortalGettingStartedWindow
    {
        public VisualElement view;

        private struct TutorialStep
        {
            public string title;
            public string description;
            public string actionText;
            public System.Action action;
            public Texture2D image;
        }

        List<Texture2D> tutorialImages = new();

        public void BuildView()
        {
            for (int i = 1; i < 6; i++)
            {
                int zeroPad = 0;
                if (i > 9) zeroPad = 1;
                var texture = Resources.Load<Texture2D>("mighty_tutorial_portal_" + zeroPad + i);
                tutorialImages.Add(texture);
            }

            if (view == null)
                view = new VisualElement
                {
                    name = "PortalGettingStarted",
                    style = {
                    height = Length.Percent(100),
                    flexGrow = 0,
                    flexDirection = FlexDirection.Column,
                    flexShrink = 0,
                    overflow = Overflow.Hidden,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.SpaceAround,
                    minHeight = 420,
                    maxHeight = 700,
                    minWidth = 380,
                    maxWidth = 480,
                    backgroundColor = new Color(0.98f, 0.98f, 0.98f, 1f),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                }
                };
            else view.Clear();

            // Header
            VisualElement header = new()
            {
                name = "Header",
                style = {
                flexDirection = FlexDirection.Row,
                flexGrow = 0,
                flexShrink = 0,
                height = 64,
                width = Length.Percent(100),
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f, 1f)),
                paddingLeft = 20,
                paddingRight = 20,
                borderTopLeftRadius = 8,
                borderTopRightRadius = 8,
            }
            };

            VisualElement titleContainer = new()
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                }
            };

            // Header icon (using tracking icon if available)
            VisualElement headerIcon = new()
            {
                style = {
                    width = 24,
                    height = 24,
                    marginRight = 12,
                    backgroundImage = icons.previewTracking,
                    backgroundColor = new Color(0.7f, 0.3f, 1f, 1f), // Fallback color
                }
            };

            Label title = new()
            {
                text = "Mighty Portal - Getting Started",
                style = {
                fontSize = 20,
                color = new StyleColor(Color.white),
                unityFontStyleAndWeight = FontStyle.Bold,
                letterSpacing = 0.5f,
            }
            };

            titleContainer.Add(headerIcon);
            titleContainer.Add(title);
            header.Add(titleContainer);

            // Content scroll area
            ScrollView content = new()
            {
                name = "Content",
                style = {
                flexDirection = FlexDirection.Column,
                flexGrow = 1,
                flexShrink = 1,
                height = Length.Percent(100),
                width = Length.Percent(100),
                paddingLeft = 24,
                paddingRight = 24,
                paddingTop = 24,
                paddingBottom = 24,
            }
            };

            // Callout section
            VisualElement calloutCard = new()
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

            Label calloutText = new("You can reopen this window anytime by clicking 'Getting Started' at the top of the settings pane.")
            {
                style = {
                    fontSize = 12,
                    color = new Color(0.3f, 0.3f, 0.6f, 1f),
                    whiteSpace = WhiteSpace.Normal,
                    flexGrow = 1,
                }
            };

            calloutCard.Add(calloutText);

            // Welcome section
            VisualElement welcomeCard = CreateCard();
            Label welcomeTitle = new("Welcome to Mighty Portal!")
            {
                style = {
                    fontSize = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.15f, 0.15f, 0.15f, 1f),
                    marginBottom = 12,
                }
            };

            Label welcomeText = new("Mighty Portal lets you build your level quickly by just clicking off in the distance and teleporting there.  This means you can build your level in a fraction of the time it would take using built in sceneview navigation.  It's also fun!")
            {
                style = {
                    fontSize = 13,
                    color = new Color(0.4f, 0.4f, 0.4f, 1f),
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 16,
                }
            };

            welcomeCard.Add(welcomeTitle);
            welcomeCard.Add(welcomeText);

            // Tutorial steps
            List<TutorialStep> steps = new()
            {
                new TutorialStep
                {
                    title = "1. Hover and activate",
                    description = "Hover over a distant area, then press the activate hotkey which is first set up as CTRL+SHIFT.  Hold those keys down and move the mouse around, the portal should show a distant preview.  Releasing CTRL+SHIFT at this point cancels the portal.",
                    image = tutorialImages[0]
                },
                new TutorialStep
                {
                    title = "2. Enlarge the portal, adjust the view",
                    description = "While still holding CTRL+SHIFT now hold down RIGHTCLICK as well.  Sudddenly the portal grows much larger, but that's not all.  While still holding CTRL+SHIFT+RIGHTCLICK, drag around - the view moves with you!",

                    image = tutorialImages[1]
                },
                new TutorialStep
                {
                    title = "3. More controls",
                    description = "You can also SCROLL the MOUSEWHEEL up and down to move forward and backward.  If you press the MOUSEWHEEL IN and HOLD it, you can pan/strafe around the view as well!",
                },
                new TutorialStep
                {
                    title = "4. Step inside the portal",
                    description = "Now that you've adjusted the view, you can step inside the portal by RELEASING RIGHTCLICK (keeping holding CTRL+SHIFT for a moment).  Instantly, your across your entire scene!",
                    actionText = "",
                    action = () => EditorApplication.isPlaying = true,
                },
                new TutorialStep
                {
                    title = "5. Mighty Maps",
                    description = "Open Window/Mighty Map to get a topdown view of your destination before you arrive!  Mighty Map also comes with many built in features such as a Scene Browser, Landmarks, Screenshots and more modules (this is a Module!)",
                }
            };

            // Tips section
            VisualElement tipsCard = CreateCard();
            Label tipsTitle = new("Pro Tips")
            {
                style = {
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.15f, 0.15f, 0.15f, 1f),
                    marginBottom = 12,
                }
            };

            VisualElement tipsList = new()
            {
                style = { flexDirection = FlexDirection.Column }
            };

            string[] tips = {
                "- Explore the settings for things like the ability to Invert Mouse.",
                "- You can also filter out Layers, for example you might not want your tree layer to get in the way of your portal jumps.",
                "- Practice makes perfect, use it a lot!",
            };

            foreach (string tip in tips)
            {
                Label tipLabel = new(tip)
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

            tipsCard.Add(tipsTitle);
            tipsCard.Add(tipsList);

            // Close button
            Button closeButton = new(() =>
            {
                if (view.parent != null)
                {
                    view.parent.RemoveFromHierarchy();
                }
            })
            {
                text = "Get Started!",
                style = {
                    height = 40,
                    backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f),
                    color = Color.white,
                    borderTopLeftRadius = 20,
                    borderTopRightRadius = 20,
                    borderBottomLeftRadius = 20,
                    borderBottomRightRadius = 20,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 16,
                }
            };

            closeButton.RegisterCallback<MouseEnterEvent>(evt =>
            {
                closeButton.style.backgroundColor = new Color(0.15f, 0.35f, 0.75f, 1f);
            });
            closeButton.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                closeButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
            });

            content.Add(calloutCard);
            content.Add(welcomeCard);
            foreach (var step in steps)
            {
                content.Add(CreateStepCard(step));
            }
            content.Add(tipsCard);
            // content.Add(closeButton);

            view.Add(header);
            view.Add(content);
        }

        private VisualElement CreateCard()
        {
            return new VisualElement
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
        }

        private VisualElement CreateStepCard(TutorialStep step)
        {
            VisualElement card = CreateCard();

            // Generate a unique color for this step based on its title
            Color stepColor = MightyCoreData.StringToColor(step.title, 0.8f);

            VisualElement headerContainer = new()
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

            Label stepTitle = new(step.title)
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
            if (step.image != null)
            {
                VisualElement imageContainer = new()
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

                float aspectRatio = (float)step.image.height / step.image.width;
                float imageWidth = Mathf.Min(step.image.width, maxWidth);
                float imageHeight = imageWidth * aspectRatio;

                Image image = new()
                {
                    image = step.image,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = {
                        width = imageWidth,
                        height = imageHeight,
                    }
                };

                imageContainer.Add(image);
                card.Add(imageContainer);
            }

            Label description = new(step.description)
            {
                style = {
                    fontSize = 13,
                    color = new Color(0.4f, 0.4f, 0.4f, 1f),
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 16,
                    marginTop = 8, // Small margin to separate from image
                }
            };

            card.Add(description);

            // Only add action button if actionText is not empty
            if (step.action != null && !string.IsNullOrEmpty(step.actionText))
            {
                Button actionButton = new(() => step.action?.Invoke())
                {
                    text = step.actionText,
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
    }
}
#endif