using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal enum PungentSpatialWorkbenchTab
    {
        Scene,
        Plan,
        Bake,
        Objects,
        Validate
    }

    internal enum PungentSpatialWorkbenchEditMode
    {
        Move,
        AddRemove
    }

    internal enum PungentSpatialWorkbenchTargetKind
    {
        Path,
        Area,
        Region,
        Marker
    }

    internal static class PungentSpatialToolbarGUI
    {
        private static readonly GUIContent[] TabContents =
        {
            new GUIContent("Scene", "Scene View authoring, selection, and component handoffs."),
            new GUIContent("Plan", "Top-down 2D plan canvas for document paths, areas, and regions."),
            new GUIContent("Bake", "Projection background baking and alignment."),
            new GUIContent("Objects", "Cached spatial object browser."),
            new GUIContent("Validate", "Spatial validation and performance findings.")
        };

        public static void Draw(
            PungentSpatialAuthoringAsset activeAsset,
            ref PungentSpatialWorkbenchTab tab,
            ref PungentSpatialWorkbenchEditMode editMode,
            ref PungentSpatialWorkbenchTargetKind targetKind,
            ref bool snap,
            ref bool selectedOnly,
            ref bool showHidden,
            int warningCount,
            int recordCount,
            System.Action refresh,
            System.Action createAsset)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(activeAsset != null ? activeAsset.name : "No Spatial Asset", EditorStyles.toolbarButton, GUILayout.Width(170f));

                PungentSpatialWorkbenchTab nextTab = (PungentSpatialWorkbenchTab)GUILayout.Toolbar((int)tab, TabContents, EditorStyles.toolbarButton, GUILayout.Width(300f));
                if (nextTab != tab)
                    tab = nextTab;

                GUILayout.Space(8f);
                GUILayout.Label("Edit", GUILayout.Width(28f));
                editMode = (PungentSpatialWorkbenchEditMode)EditorGUILayout.EnumPopup(editMode, EditorStyles.toolbarPopup, GUILayout.Width(96f));

                GUILayout.Label("Target", GUILayout.Width(42f));
                targetKind = (PungentSpatialWorkbenchTargetKind)EditorGUILayout.EnumPopup(targetKind, EditorStyles.toolbarPopup, GUILayout.Width(96f));

                snap = GUILayout.Toggle(snap, new GUIContent("Snap", "Snap new points to the projection or surface where supported."), EditorStyles.toolbarButton, GUILayout.Width(48f));
                selectedOnly = GUILayout.Toggle(selectedOnly, new GUIContent("Selected", "Draw only selected records where supported."), EditorStyles.toolbarButton, GUILayout.Width(68f));
                showHidden = GUILayout.Toggle(showHidden, new GUIContent("Hidden", "Show hidden records in the browser."), EditorStyles.toolbarButton, GUILayout.Width(62f));

                GUILayout.FlexibleSpace();

                DrawChip(recordCount + " records", MessageType.None);
                DrawChip(warningCount == 0 ? "OK" : warningCount + " issues", warningCount == 0 ? MessageType.Info : MessageType.Warning);

                if (GUILayout.Button(new GUIContent("Refresh", "Refresh the cached spatial provider list."), EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    refresh?.Invoke();

                if (activeAsset == null && GUILayout.Button(new GUIContent("Create", "Create a Spatial Authoring Asset."), EditorStyles.toolbarButton, GUILayout.Width(58f)))
                    createAsset?.Invoke();
            }
        }

        public static void DrawChip(string text, MessageType type)
        {
            Color previous = GUI.color;
            switch (type)
            {
                case MessageType.Warning:
                    GUI.color = new Color(1f, 0.82f, 0.32f, 1f);
                    break;
                case MessageType.Error:
                    GUI.color = new Color(1f, 0.45f, 0.42f, 1f);
                    break;
                case MessageType.Info:
                    GUI.color = new Color(0.55f, 0.82f, 1f, 1f);
                    break;
            }

            GUILayout.Label(text, EditorStyles.toolbarButton, GUILayout.MinWidth(48f));
            GUI.color = previous;
        }
    }
#endif
}
