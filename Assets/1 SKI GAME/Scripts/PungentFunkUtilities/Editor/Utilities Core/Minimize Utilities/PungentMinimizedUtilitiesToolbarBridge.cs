namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Cleanup owner for the retired Unity main-toolbar minimizer bridge.
    /// The minimizer now uses stable Core access points: utility headers, Tools menus, the Utilities Browser, and the minimized tab panel/overlay.
    /// </summary>
    [InitializeOnLoad]
    internal static class PungentMinimizedUtilitiesToolbarBridge
    {
        private const string ContainerName = "PungentFunkMinimizedUtilitiesToolbarBridge";
        private const double RetryIntervalSeconds = 1.0d;
        private const int MaxCleanupAttempts = 12;

        private static int _cleanupAttempts;
        private static double _nextCleanupAttempt;

        static PungentMinimizedUtilitiesToolbarBridge()
        {
            EditorApplication.delayCall += CleanupRetiredToolbarBridge;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_cleanupAttempts >= MaxCleanupAttempts)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextCleanupAttempt)
                return;

            _nextCleanupAttempt = EditorApplication.timeSinceStartup + RetryIntervalSeconds;
            CleanupRetiredToolbarBridge();
        }

        private static void CleanupRetiredToolbarBridge()
        {
            _cleanupAttempts++;

            try
            {
                VisualElement root = FindToolbarRoot();
                if (root != null)
                    RemoveExistingBridge(root);

                CloseFallbackWindows();
            }
            catch
            {
                // Main-toolbar internals vary by Unity version; cleanup should never break editor startup.
            }
        }

        private static VisualElement FindToolbarRoot()
        {
            System.Type toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
                return null;

            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            for (int i = 0; i < toolbars.Length; i++)
            {
                object toolbar = toolbars[i];
                if (toolbar == null)
                    continue;

                VisualElement root = GetRootVisualElement(toolbar);
                if (root != null)
                    return root;
            }

            return null;
        }

        private static VisualElement GetRootVisualElement(object toolbar)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo rootProperty = toolbar.GetType().GetProperty("rootVisualElement", Flags);
            if (rootProperty != null && rootProperty.GetValue(toolbar, null) is VisualElement propertyRoot)
                return propertyRoot;

            FieldInfo rootField = toolbar.GetType().GetField("m_Root", Flags);
            return rootField != null ? rootField.GetValue(toolbar) as VisualElement : null;
        }

        private static VisualElement FindByExactName(VisualElement element, string name)
        {
            if (element == null || string.IsNullOrEmpty(name))
                return null;

            if (string.Equals(element.name, name, System.StringComparison.Ordinal))
                return element;

            for (int i = 0; i < element.childCount; i++)
            {
                VisualElement found = FindByExactName(element[i], name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void RemoveExistingBridge(VisualElement root)
        {
            while (true)
            {
                VisualElement existing = FindByExactName(root, ContainerName);
                if (existing == null)
                    return;

                existing.RemoveFromHierarchy();
            }
        }

        private static void CloseFallbackWindows()
        {
            PungentMinimizedUtilitiesQuickAccessWindow[] windows = Resources.FindObjectsOfTypeAll<PungentMinimizedUtilitiesQuickAccessWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                PungentMinimizedUtilitiesQuickAccessWindow window = windows[i];
                if (window != null)
                    window.Close();
            }
        }
    }

    internal sealed class PungentMinimizedUtilitiesQuickAccessWindow : EditorWindow
    {
        private void OnEnable()
        {
            titleContent = new GUIContent("Minimized Utilities");
            EditorApplication.delayCall += Close;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "This legacy quick access overlay has been retired. Open Minimized Utilities from the Utilities Browser, utility headers, or Tools > PungentFunk Utilities > Core > Minimized Utilities.",
                MessageType.Info);

            if (GUILayout.Button("Close", EditorStyles.miniButton))
                Close();
        }
    }
#endif
}
