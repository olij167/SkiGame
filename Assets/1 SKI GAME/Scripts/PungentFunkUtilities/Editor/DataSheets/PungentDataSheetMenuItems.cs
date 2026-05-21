using UnityEditor;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetMenuItems
    {
        [MenuItem("Assets/PungentFunk Utilities/Open Data Sheet Editor", priority = 45)]
        public static void OpenDataSheetEditorFromAssets()
        {
            OpenDataSheetEditor();
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Checklist Utility", priority = 46)]
        public static void OpenDataSheetQaChecklistFromAssets()
        {
            OpenDataSheetQaChecklist();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Data Sheet Editor", false, 27)]
        public static void OpenDataSheetEditorFromGameObject()
        {
            OpenDataSheetEditor();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Checklist Utility", false, 28)]
        public static void OpenDataSheetQaChecklistFromGameObject()
        {
            OpenDataSheetQaChecklist();
        }

        private static void OpenDataSheetEditor()
        {
            PungentDataSheetProviderRegistration.RegisterProvider();
            PungentUtilityRegistry.Open("data-sheet-editor");
        }

        private static void OpenDataSheetQaChecklist()
        {
            PungentChecklistUtilityWindow.OpenDataSheetCurrentQa();
        }
    }
#endif
}
