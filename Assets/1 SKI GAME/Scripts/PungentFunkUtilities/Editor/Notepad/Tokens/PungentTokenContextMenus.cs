namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentTokenContextMenus
    {
        static PungentTokenContextMenus()
        {
            EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        [MenuItem("Assets/PungentFunk Utilities/Link Token...", priority = 620)]
        private static void LinkAssetToken()
        {
            if (!PungentUtilityRegistry.CanOpen("token-validator", true))
                return;

            if (Selection.activeObject != null)
                PungentTokenLinkPopup.Open(Selection.activeObject);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Link Token...", priority = 80)]
        private static void LinkGameObjectToken(MenuCommand command)
        {
            if (!PungentUtilityRegistry.CanOpen("token-validator", true))
                return;

            GameObject go = command.context as GameObject ?? Selection.activeGameObject;
            if (go != null)
                PungentTokenLinkPopup.Open(go);
        }

        private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            if (property == null || property.serializedObject == null)
                return;

            menu.AddItem(new GUIContent("PungentFunk/Link Token..."), false, () =>
            {
                if (!PungentUtilityRegistry.CanOpen("token-validator", true))
                    return;

                PungentTokenLinkPopup.Open(property.serializedObject.targetObject, property.propertyPath, property.displayName);
            });
        }
    }
#endif
}
