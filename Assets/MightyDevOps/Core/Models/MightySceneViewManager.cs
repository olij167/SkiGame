#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SearchService;
using Mighty;
using static Mighty.MightyCoreData;
using System;

public class MightySceneViewManager
{
    private static MightySceneViewManager _instance;
    public static MightySceneViewManager Instance => _instance ?? (_instance = new MightySceneViewManager());
    static public VisualElement root;
    static private Dictionary<string, VisualElement> categories = new Dictionary<string, VisualElement>();
    static private SceneView sceneView;

    static private Quaternion cacheRotation;
    static private Vector3 cachePosition;

    [Serializable]
    public class Settings
    {
        [SerializeField]
        public Color borderColor = new Color(0, 0, 0, 0);
        [SerializeField]
        public float distanceStart = 5, distanceEnd = 100;
        [SerializeField]
        public bool show = true;
    }

    private static Dictionary<string, Settings> settings = new Dictionary<string, Settings>();

    private MightySceneViewManager()
    {
        Init();
    }

    public static void Init()
    {
        DevLog("Initializing MightySceneViewManager");
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;

        // Add more frequent updates for smoother tracking
        SceneView.beforeSceneGui -= OnBeforeSceneGUI;
        SceneView.beforeSceneGui += OnBeforeSceneGUI;

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;

        UpdateSceneView -= UpdateWorldSpaceElements;
        UpdateSceneView += UpdateWorldSpaceElements;
        sceneView = SceneView.lastActiveSceneView;
        cacheRotation = sceneView.camera.transform.rotation;
        cachePosition = sceneView.camera.transform.position;
        root = new VisualElement();
        sceneView.rootVisualElement.Add(root);
        var mightyStylesheet = Resources.Load<StyleSheet>("UI/mightystyles");

        if (!root.styleSheets.Contains(mightyStylesheet))
        {
            root.styleSheets.Add(mightyStylesheet);
        }
    }

    private static void OnBeforeSceneGUI(SceneView sceneView)
    {
        // Update before scene GUI for more responsive tracking
        CheckAndUpdateCameraTransform(sceneView);
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        // Keep the existing during scene GUI update
        CheckAndUpdateCameraTransform(sceneView);
    }

    private static void OnEditorUpdate()
    {
        // Update every editor frame for maximum responsiveness
        if (SceneView.lastActiveSceneView != null)
        {
            CheckAndUpdateCameraTransform(SceneView.lastActiveSceneView);
        }
    }

    private static void CheckAndUpdateCameraTransform(SceneView sceneView)
    {
        if (cacheRotation != sceneView.camera.transform.rotation || cachePosition != sceneView.camera.transform.position)
        {
            cacheRotation = sceneView.camera.transform.rotation;
            cachePosition = sceneView.camera.transform.position;
            UpdateWorldSpaceElements();
        }
    }

    public void RegisterElement(string category, VisualElement element, IMappable mappable, Vector3? worldPosition = null)
    {
        element.AddToClassList("scalewithopacity");
        var x = mappable.GetSceneViewSettings();
        if (!settings.ContainsKey(category))
        {
            settings.Add(category, x);
        }

        if (!categories.TryGetValue(category, out var categoryRoot))
        {
            categoryRoot = new VisualElement
            {
                name = category
            };
            categories[category] = categoryRoot;
            root.Add(categoryRoot);
        }

        if (worldPosition.HasValue)
        {
            element.userData = worldPosition.Value; // Store the world position in userData
            Vector2 screenPosition = WorldToScreenPoint(SceneView.lastActiveSceneView.camera, worldPosition.Value);
            element.style.left = screenPosition.x;
            element.style.top = screenPosition.y;
        }

        element.RegisterCallback<MouseEnterEvent>(evt =>
        {
            element.BringToFront();
        });

        categoryRoot.Add(element);
    }


    public void ClearCategory(string category)
    {
        if (categories.TryGetValue(category, out var categoryRoot))
        {
            // Remove all child elements from the category root
            categoryRoot.RemoveFromHierarchy();
            categories.Remove(category);
        }
    }

    public void ToggleCategory(string category, bool? forceState = null)
    {
        if (settings.TryGetValue(category, out var categorySettings))
        {
            // If forceState is provided, use that value, otherwise toggle the current state
            categorySettings.show = forceState ?? !categorySettings.show;

            // Force immediate update of the scene view
            if (SceneView.lastActiveSceneView != null)
            {
                UpdateWorldSpaceElements();
                SceneView.lastActiveSceneView.Repaint();
            }
        }
    }

    public bool IsCategoryVisible(string category)
    {
        return settings.TryGetValue(category, out var categorySettings) && categorySettings.show;
    }

    public static void UpdateWorldSpaceElements()
    {
        Camera sceneCamera = SceneView.lastActiveSceneView.camera;
        // Debug.Log($"Updating World Space Elements: {categories.Count} categories");


        foreach (var category in categories)
        {
            var s = settings[category.Key];
            DevLog("Category: " + category.Key + " " + s.show);
            if (!s.show)
            {
                foreach (var element in category.Value.Children())
                {
                    element.style.display = DisplayStyle.None;
                }
                continue; // Skip further processing for this category
            }

            foreach (var element in category.Value.Children())
            {
                if (!(element.userData is Vector3 worldPos)) continue;

                Vector3 viewPos = sceneCamera.WorldToViewportPoint(worldPos);
                // Calculate fade start distance (5% before the actual distance start)
                float fadeStartDistance = s.distanceStart * 0.95f;

                if (viewPos.z <= 0 || viewPos.z < fadeStartDistance || viewPos.z > s.distanceEnd)
                {
                    element.style.display = DisplayStyle.None;
                    continue; // Skip further processing for this element
                }

                // At this point, the element is confirmed to be displayed, proceed with positioning and opacity adjustment
                Vector2 screenPos = WorldToScreenPoint(sceneCamera, worldPos);


                element.style.display = DisplayStyle.Flex;
                element.style.left = screenPos.x - (element.resolvedStyle.width / 2);
                element.style.top = screenPos.y - (element.resolvedStyle.height / 2);
                element.style.opacity = viewPos.z >= s.distanceStart && viewPos.z <= s.distanceEnd ?
                                        1f - ((viewPos.z - fadeStartDistance) / (s.distanceEnd - fadeStartDistance)) : 1f;

                // Update distance label if applicable
                var distanceLabel = element.Q<Label>(name: "TextOverlay");
                if (distanceLabel != null)
                {
                    distanceLabel.text = $"{Vector3.Distance(sceneCamera.transform.position, worldPos):F2}m";
                }

                // Re-register mouse event callbacks
                bool ShouldElementBeInteractive(VisualElement element, Vector3 viewPos)
                {
                    // Replace 's' with your settings object that should be accessible in this context
                    float fadeStartDistance = s.distanceStart * 0.80f;
                    return viewPos.z >= fadeStartDistance && viewPos.z <= s.distanceEnd;
                }

                // Registering mouse event callbacks without unregistering them
                element.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    if (ShouldElementBeInteractive(element, sceneCamera.WorldToViewportPoint(worldPos)))
                    {
                        element.BringToFront();
                        element.style.opacity = 1f; // Ensure full visibility when mouse is over
                    }
                });
                element.RegisterCallback<MouseLeaveEvent>(evt =>
                {

                    Vector3 viewPos = sceneCamera.WorldToViewportPoint(worldPos);
                    float fadeStartDistance = s.distanceStart * 0.95f;
                    element.style.opacity = viewPos.z >= fadeStartDistance && viewPos.z <= s.distanceEnd ?
                                            1f - ((viewPos.z - fadeStartDistance) / (s.distanceEnd - fadeStartDistance)) : 1f;
                });
            }
        }
    }


    private static Vector2 WorldToScreenPoint(Camera sceneCamera, Vector3 worldPosition)
    {
        Vector3 screenPoint = sceneCamera.WorldToScreenPoint(worldPosition);
        return new Vector2(screenPoint.x, Screen.height - screenPoint.y);
    }

    public class CustomVisualElement : VisualElement
    {
        public bool IsHovered { get; set; } = false;
    }

    static public void Rebuild()
    {
        DevLog("Rebuilding MightySceneViewManager");

        // Step 1: Unsubscribe from all events to prevent multiple subscriptions
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.beforeSceneGui -= OnBeforeSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
        UpdateSceneView -= UpdateWorldSpaceElements;

        // Step 2: Clear all visual elements and categories
        foreach (var category in categories.Values)
        {
            category.RemoveFromHierarchy();
        }
        categories.Clear();

        // Step 3: Remove root from scene view and clear it
        if (root != null && root.parent != null)
        {
            root.RemoveFromHierarchy();
            root.Clear();
        }

        // Step 4: Clear settings
        settings.Clear();

        // Step 5: Reset instance state
        _instance = null;
        root = null;
        sceneView = null;

        // Step 6: Reinitialize everything from scratch
        _instance = new MightySceneViewManager();
        // UpdateWorldSpaceElements();
    }
}
#endif