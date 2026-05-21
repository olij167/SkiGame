using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
    #if UNITY_EDITOR
    using UnityEditor;

    public static class PungentAssetPlacementAccessMenus
    {
        [MenuItem("Assets/PungentFunk Utilities/Create Placement Asset Set From Selected Prefabs", priority = 40)]
        public static void CreateAssetSetFromSelectedPrefabs()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Placement Asset Set", "Placement Asset Set", "asset", "Choose where to save the placement asset set.");
            if (string.IsNullOrEmpty(path))
                return;

            PungentPlacementAssetSetBuilder.CreateAssetSetFromSelection(path);
        }

        [MenuItem("Assets/PungentFunk Utilities/Create Placement Asset Set From Selected Prefabs", true)]
        private static bool ValidateCreateAssetSetFromSelectedPrefabs()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("GameObject/PungentFunk Utilities/Add Placement Socket", false, 41)]
        public static void AddPlacementSocketToSelected()
        {
            PungentPlacementSocketValidator.AddSocketToSelected();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Add Placement Socket", true)]
        private static bool ValidateAddPlacementSocketToSelected()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }
    }
    #endif

}