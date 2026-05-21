using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Deprecated compatibility shim. Category appearance editing now lives in
    /// PungentUtilityCategoryMembershipPopup alongside category IDs and membership.
    /// </summary>
    public sealed class PungentUtilityCategoryEditorPopup : EditorWindow
    {
        public static void Open(string categoryId)
        {
            PungentUtilityDeveloperToolsWindow.OpenCategory(categoryId);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Category editing has moved to the unified Developer Tools metadata tab.",
                MessageType.Info);

            if (GUILayout.Button("Open Developer Tools Metadata"))
                PungentUtilityDeveloperToolsWindow.OpenCategory(PungentUtilityCategories.Core);
        }
    }
#endif
}


//using PungentFunk.Utilities.Editor.Developer;
//using PungentFunk.Utilities.Editor.Theme;

//namespace PungentFunk.Utilities.Editor.Core
//{
//#if UNITY_EDITOR
//    using UnityEditor;
//    using UnityEngine;

//    /// <summary>
//    /// Developer-mode category appearance override editor for the Utilities Browser sidebar.
//    /// </summary>
//    public sealed class PungentUtilityCategoryEditorPopup : EditorWindow
//    {
//        private string _categoryId;
//        private string _factoryDisplayName;
//        private Color _factoryTint;
//        private PungentUtilityCategoryOverrides.CategoryOverride _categoryOverride;
//        private string _status = "Ready.";

//        public static void Open(string categoryId)
//        {
//            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
//            {
//                EditorUtility.DisplayDialog("Utility Category", "Category editing is only available when PungentFunk developer mode is enabled.", "OK");
//                return;
//            }

//            PungentUtilityCategoryEditorPopup window = GetWindow<PungentUtilityCategoryEditorPopup>(true, "Utility Category", true);
//            window.minSize = new Vector2(420f, 260f);
//            window.Load(categoryId);
//            window.ShowUtility();
//        }

//        private void OnEnable()
//        {
//            titleContent = new GUIContent("Utility Category");
//            PungentUtilityCategoryOverrides.Changed -= HandleCategoryOverridesChanged;
//            PungentUtilityCategoryOverrides.Changed += HandleCategoryOverridesChanged;
//        }

//        private void OnDisable()
//        {
//            PungentUtilityCategoryOverrides.Changed -= HandleCategoryOverridesChanged;
//        }

//        private void HandleCategoryOverridesChanged()
//        {
//            if (!string.IsNullOrWhiteSpace(_categoryId))
//                Load(_categoryId);
//            Repaint();
//        }

//        private void Load(string categoryId)
//        {
//            _categoryId = PungentUtilityCategories.Normalize(categoryId);
//            _factoryDisplayName = PungentUtilityCategories.GetFactoryDisplayName(_categoryId);
//            _factoryTint = PungentUtilityCategories.GetFactoryTint(_categoryId);
//            _categoryOverride = PungentUtilityCategoryOverrides.instance.CreateEditableCopy(_categoryId, _factoryDisplayName, _factoryTint);
//            _status = PungentUtilityCategoryOverrides.instance.HasOverride(_categoryId) ? "Loaded project category override." : "Editing factory-default category metadata.";
//        }

//        private void OnGUI()
//        {
//            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
//            {
//                UtilityWindowTheme.Header("Utility Category", "Developer-only category overrides.", "Developer mode is not enabled.");
//                EditorGUILayout.HelpBox("Enable PungentFunk developer mode before editing category names or colours.", MessageType.Warning);
//                return;
//            }

//            if (_categoryOverride == null)
//                Load(_categoryId);

//            UtilityWindowTheme.Header("Utility Category", "Edit project-local category name and colour overrides.", _categoryId + " · " + _status);

//            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
//            {
//                EditorGUILayout.LabelField("Category ID", _categoryId, EditorStyles.boldLabel);
//                EditorGUILayout.LabelField("Factory Name", _factoryDisplayName);
//                using (new EditorGUILayout.HorizontalScope())
//                {
//                    EditorGUILayout.LabelField("Factory Colour", GUILayout.Width(112f));
//                    EditorGUILayout.ColorField(GUIContent.none, _factoryTint, false, false, false, GUILayout.Width(58f));
//                    GUILayout.FlexibleSpace();
//                }
//            }

//            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
//            {
//                UtilityWindowTheme.SectionTitle("Overrides", UtilityWindowTheme.Blue);

//                using (new EditorGUILayout.HorizontalScope())
//                {
//                    _categoryOverride.overrideDisplayName = EditorGUILayout.ToggleLeft(new GUIContent("Display Name", "Override the category name shown in the Utilities Browser."), _categoryOverride.overrideDisplayName, GUILayout.Width(136f));
//                    using (new EditorGUI.DisabledScope(!_categoryOverride.overrideDisplayName))
//                        _categoryOverride.displayName = EditorGUILayout.TextField(_categoryOverride.displayName ?? string.Empty);
//                }

//                using (new EditorGUILayout.HorizontalScope())
//                {
//                    _categoryOverride.overrideTint = EditorGUILayout.ToggleLeft(new GUIContent("Default Colour", "Override the default category tint used by cards, pills, and sidebar entries."), _categoryOverride.overrideTint, GUILayout.Width(136f));
//                    using (new EditorGUI.DisabledScope(!_categoryOverride.overrideTint))
//                        _categoryOverride.tint = EditorGUILayout.ColorField(GUIContent.none, _categoryOverride.tint, false, false, false, GUILayout.Width(68f));
//                    GUILayout.FlexibleSpace();
//                }
//            }

//            EditorGUILayout.HelpBox("These edits are project-local developer overrides. They do not change the factory category IDs used by registry descriptors.", MessageType.Info);

//            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.05f, 6, 3)))
//            {
//                using (new EditorGUI.DisabledScope(!PungentUtilityCategoryOverrides.instance.HasOverride(_categoryId)))
//                {
//                    if (GUILayout.Button(new GUIContent("Reset to Defaults", "Remove this category's project-local name and colour overrides."), EditorStyles.miniButton, GUILayout.Width(128f), GUILayout.Height(24f)))
//                    {
//                        if (EditorUtility.DisplayDialog("Reset Category", "Remove the category override for '" + _factoryDisplayName + "' and return to package defaults?", "Reset", "Cancel"))
//                        {
//                            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Reset Utility Category Override");
//                            PungentUtilityCategoryOverrides.instance.RemoveOverride(_categoryId);
//                            Close();
//                        }
//                    }
//                }

//                GUILayout.FlexibleSpace();

//                if (GUILayout.Button(new GUIContent("Cancel", "Close without saving."), EditorStyles.miniButton, GUILayout.Width(72f), GUILayout.Height(24f)))
//                    Close();

//                if (UtilityWindowTheme.TintedButton("Save", UtilityWindowTheme.Green, GUILayout.Width(86f), GUILayout.Height(26f)))
//                {
//                    Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Save Utility Category Override");
//                    PungentUtilityCategoryOverrides.instance.SetOverride(_categoryOverride);
//                    Close();
//                }
//            }
//        }
//    }
//#endif
//}
