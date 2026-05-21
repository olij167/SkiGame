using PungentFunk.Utilities.Editor.Checklists;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public sealed class PungentDataSheetQaChecklistWindow : EditorWindow
    {
        public static void Open()
        {
            PungentChecklistUtilityWindow.OpenDataSheetCurrentQa();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Data Sheet Checklist");
            EditorApplication.delayCall += RedirectToChecklistUtility;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RedirectToChecklistUtility;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("The Data Sheet checklist now lives in the shared Checklist Utility.", MessageType.Info);
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
