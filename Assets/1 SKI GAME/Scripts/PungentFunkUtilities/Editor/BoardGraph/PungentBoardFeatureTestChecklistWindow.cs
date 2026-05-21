using PungentFunk.Utilities.Editor.Checklists;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public sealed class PungentBoardFeatureTestChecklistWindow : EditorWindow
    {
        public static void Open()
        {
            PungentChecklistUtilityWindow.OpenBoardGraphFeatureTest();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Board Checklist");
            EditorApplication.delayCall += RedirectToChecklistUtility;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RedirectToChecklistUtility;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("The BoardGraph feature checklist now lives in the shared Checklist Utility.", MessageType.Info);
            if (GUILayout.Button("Open Checklist Utility"))
                Open();
        }

        private void RedirectToChecklistUtility()
        {
            if (this == null)
                return;

            Close();
            Open();
        }
    }
#endif
}
