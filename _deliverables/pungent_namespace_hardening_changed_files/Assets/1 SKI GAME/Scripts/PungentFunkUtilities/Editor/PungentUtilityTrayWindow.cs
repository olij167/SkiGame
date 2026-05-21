using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Registered utility tray. Parked utilities are tracked by ID and reopened through PungentUtilityRegistry.
    /// This avoids trying to force native minimisation into Unity's unsupported bottom status bar API.
    /// </summary>
    [InitializeOnLoad]
    public sealed class PungentUtilityTrayWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.UtilityTray.";
        private const string PrefItems = PrefPrefix + "Items";
        private const string PrefPinned = PrefPrefix + "Pinned";
        private const string PrefCompact = PrefPrefix + "Compact";
        private const string PrefOverlay = PrefPrefix + "Overlay";

        private static readonly List<string> SharedItems = new List<string>();
        private static readonly HashSet<string> SharedPinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        private static bool _showOverlay = true;

        private Vector2 _scroll;
        private bool _compact;
        private string _status = "Ready.";

        static PungentUtilityTrayWindow()
        {
            LoadShared();
            SceneView.duringSceneGui -= DrawSceneOverlay;
            SceneView.duringSceneGui += DrawSceneOverlay;
        }

        public static int MinimizedCount
        {
            get
            {
                LoadShared();
                return SharedItems.Count;
            }
        }

        [MenuItem("Tools/Utilities/Workflow/Utility Tray", priority = 300)]
        public static void Open()
        {
            PungentUtilityTrayWindow window = GetWindow<PungentUtilityTrayWindow>("Utility Tray");
            window.minSize = new Vector2(420f, 150f);
            window.Show();
        }

        [MenuItem("Tools/Utilities/Workflow/Park Focused Utility", priority = 301)]
        public static void MinimizeFocusedUtility()
        {
            EditorWindow focused = focusedWindow;
            if (focused == null)
            {
                EditorUtility.DisplayDialog("PungentFunk Utility Tray", "No focused utility window was found.", "OK");
                return;
            }

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.All.FirstOrDefault(u =>
            {
                Type type = u.ResolveWindowType();
                return type != null && type == focused.GetType();
            });

            if (descriptor == null || descriptor.Id == "utility-tray")
            {
                EditorUtility.DisplayDialog("PungentFunk Utility Tray", "The focused window is not a parkable PungentFunk utility.", "OK");
                return;
            }

            ParkUtility(descriptor.Id, true);
            focused.Close();
        }

        public static void ParkUtility(string utilityId, bool focusTray = false)
        {
            if (string.IsNullOrWhiteSpace(utilityId) || string.Equals(utilityId, "utility-tray", StringComparison.OrdinalIgnoreCase))
                return;

            LoadShared();
            SharedItems.RemoveAll(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase));
            SharedItems.Insert(0, utilityId);
            SaveShared();

            if (focusTray)
                Open();

            RepaintActiveSceneView();
        }

        public static bool TryOpenMinimized(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return false;

            LoadShared();
            if (!SharedItems.Any(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase)))
                return false;

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            if (descriptor == null)
                return false;

            descriptor.OpenDirect();
            if (!SharedPinned.Contains(utilityId))
            {
                SharedItems.RemoveAll(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase));
                SaveShared();
            }
            return true;
        }

        public static bool IsParked(string utilityId)
        {
            LoadShared();
            return SharedItems.Any(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase));
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Utility Tray");
            LoadShared();
            _compact = EditorPrefs.GetBool(PrefCompact, false);
        }

        private void OnDisable()
        {
            SaveShared();
            EditorPrefs.SetBool(PrefCompact, _compact);
        }

        private void OnGUI()
        {
            LoadShared();
            UtilityWindowTheme.Header(
                "Utility Tray",
                "Park registered utilities here. Opening a parked utility from any PungentFunk access point restores the parked entry.",
                _status);

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _compact = GUILayout.Toggle(_compact, "Compact", EditorStyles.toolbarButton, GUILayout.Width(82f));
                _showOverlay = GUILayout.Toggle(_showOverlay, "Scene Overlay", EditorStyles.toolbarButton, GUILayout.Width(112f));
                if (UtilityWindowTheme.TintedButton("Park Focused", UtilityWindowTheme.Blue, GUILayout.Width(104f)))
                    MinimizeFocusedUtility();
                if (UtilityWindowTheme.TintedButton("Control Panel", UtilityWindowTheme.Teal, GUILayout.Width(104f)))
                    PungentUtilityControlPanelWindow.Open();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear Unpinned", EditorStyles.miniButton, GUILayout.Width(104f)))
                    ClearUnpinned();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawItems();
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                SaveShared();
                EditorPrefs.SetBool(PrefCompact, _compact);
                EditorPrefs.SetBool(PrefOverlay, _showOverlay);
                RepaintActiveSceneView();
            }
        }

        private void DrawItems()
        {
            if (SharedItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No parked utilities yet. Use Park on a utility card in the Control Panel or Tools > Utilities > Workflow > Park Focused Utility.", MessageType.Info);
                return;
            }

            for (int i = SharedItems.Count - 1; i >= 0; i--)
            {
                string id = SharedItems[i];
                PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(id);
                if (utility == null)
                {
                    SharedItems.RemoveAt(i);
                    continue;
                }

                DrawItem(utility);
            }
        }

        private void DrawItem(PungentUtilityDescriptor utility)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.14f, 0.08f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool pinned = SharedPinned.Contains(utility.Id);
                    if (GUILayout.Button(pinned ? "★" : "☆", GUILayout.Width(28f)))
                        TogglePinned(utility.Id);

                    GUILayout.Label(new GUIContent(utility.DisplayName, BuildUtilityTooltip(utility)), EditorStyles.boldLabel, GUILayout.MinWidth(150f));
                    if (!_compact)
                    {
                        GUILayout.Label(PungentUtilityRegistry.GetLab(utility), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(132f));
                        UtilityWindowTheme.CountPill(PungentUtilityRegistry.GetStatus(utility), PungentUtilityPackageStatus.GetTint(PungentUtilityRegistry.GetStatus(utility)), Mathf.Clamp(PungentUtilityRegistry.GetStatus(utility).Length * 7f + 18f, 70f, 136f));
                    }

                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(!utility.CanOpen))
                    {
                        if (UtilityWindowTheme.TintedButton("Restore", UtilityWindowTheme.Blue, GUILayout.Width(76f)))
                        {
                            TryOpenMinimized(utility.Id);
                            _status = "Restored " + utility.DisplayName + ".";
                        }
                    }

                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(72f)))
                    {
                        SharedItems.RemoveAll(id => string.Equals(id, utility.Id, StringComparison.OrdinalIgnoreCase));
                        SharedPinned.Remove(utility.Id);
                    }
                }

                if (!_compact)
                {
                    EditorGUILayout.LabelField(utility.Description, UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField(PungentUtilityRegistry.GetModule(utility) + " · " + utility.MenuPath, UtilityWindowTheme.PathLabelStyle);
                }
            }
        }

        private void TogglePinned(string id)
        {
            if (!SharedPinned.Add(id))
                SharedPinned.Remove(id);
            SaveShared();
        }

        private void ClearUnpinned()
        {
            SharedItems.RemoveAll(id => !SharedPinned.Contains(id));
            SaveShared();
            _status = "Cleared unpinned tray entries.";
        }

        private static void DrawSceneOverlay(SceneView sceneView)
        {
            LoadShared();
            if (!_showOverlay || sceneView == null || SharedItems.Count == 0)
                return;

            Handles.BeginGUI();
            Rect rect = new Rect(8f, sceneView.position.height - 58f, 210f, 44f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("PFU " + SharedItems.Count, EditorStyles.miniButton, GUILayout.Width(62f)))
                    Open();

                for (int i = 0; i < Mathf.Min(2, SharedItems.Count); i++)
                {
                    PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(SharedItems[i]);
                    if (utility != null && GUILayout.Button(utility.DisplayName, EditorStyles.miniButton, GUILayout.Width(66f)))
                        TryOpenMinimized(utility.Id);
                }
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void RepaintActiveSceneView()
        {
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Repaint();
        }

        private static string BuildUtilityTooltip(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return string.Empty;

            return utility.DisplayName + "\n" +
                   PungentUtilityRegistry.GetLab(utility) + " / " + PungentUtilityRegistry.GetModule(utility) + "\n" +
                   PungentUtilityRegistry.GetStatus(utility) + ": " + PungentUtilityPackageStatus.GetDescription(PungentUtilityRegistry.GetStatus(utility)) + "\n\n" +
                   utility.Description;
        }

        private static void LoadShared()
        {
            if (_loaded)
                return;
            _loaded = true;
            SharedItems.Clear();
            SharedItems.AddRange(Decode(EditorPrefs.GetString(PrefItems, string.Empty)));
            SharedPinned.Clear();
            foreach (string id in Decode(EditorPrefs.GetString(PrefPinned, string.Empty)))
                SharedPinned.Add(id);
            _showOverlay = EditorPrefs.GetBool(PrefOverlay, true);
        }

        private static void SaveShared()
        {
            _loaded = true;
            EditorPrefs.SetString(PrefItems, Encode(SharedItems));
            EditorPrefs.SetString(PrefPinned, Encode(SharedPinned));
        }

        private static string Encode(IEnumerable<string> values)
        {
            return string.Join("|", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Replace("|", string.Empty)));
        }

        private static List<string> Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return new List<string>();

            return encoded.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
    #endif

}