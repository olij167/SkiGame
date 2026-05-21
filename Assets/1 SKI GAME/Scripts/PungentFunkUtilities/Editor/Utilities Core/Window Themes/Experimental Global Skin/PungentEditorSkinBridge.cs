namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Experimental editor-wide skin bridge that emits Unity editor USS extension files.
    /// It deliberately does not patch Unity installation resources or wrap IMGUI containers.
    /// </summary>
    [InitializeOnLoad]
    public static class PungentEditorSkinBridge
    {
        private const string PrefPrefix = "GenericUtility.WindowTheme.ExperimentalEditorSkin.";
        private const string PrefEnabled = PrefPrefix + "Enabled";
        private const string PrefApplyOnLoad = PrefPrefix + "ApplyOnLoad";
        private const string PrefLastAction = PrefPrefix + "LastAction";

        public const string ExtensionFolder = "Assets/PungentFunkUtilities/Editor/StyleSheets/Extensions";
        public const string CommonUssPath = ExtensionFolder + "/common.uss";
        public const string DarkUssPath = ExtensionFolder + "/dark.uss";
        public const string LightUssPath = ExtensionFolder + "/light.uss";

        static PungentEditorSkinBridge()
        {
            if (ApplyOnLoad && ExperimentalBridgeEnabled)
            {
                EditorApplication.delayCall += GenerateAndImportCurrentTheme;
            }
        }

        public static bool ExperimentalBridgeEnabled
        {
            get => UtilityWindowPrefs.GetBool(PrefEnabled, false);
            set => UtilityWindowPrefs.SetBool(PrefEnabled, value);
        }

        public static bool ApplyOnLoad
        {
            get => UtilityWindowPrefs.GetBool(PrefApplyOnLoad, false);
            set => UtilityWindowPrefs.SetBool(PrefApplyOnLoad, value);
        }

        public static string LastAction
        {
            get => UtilityWindowPrefs.GetString(PrefLastAction, "No editor skin action has been run yet.");
            private set => UtilityWindowPrefs.SetString(PrefLastAction, value);
        }

        public static void EnableAndGenerate(bool applyOnLoad)
        {
            ExperimentalBridgeEnabled = true;
            ApplyOnLoad = applyOnLoad;
            GenerateAndImportCurrentTheme();
        }

        public static void GenerateAndImportCurrentTheme()
        {
            if (!ExperimentalBridgeEnabled)
                return;

            try
            {
                EnsureExtensionFolder();
                var profile = PungentEditorSkinProfile.Default;
                string common = PungentEditorSkinUssGenerator.BuildUss(profile, PungentEditorSkinSelectorMap.Defaults, false);
                string dark = profile.compatibility == PungentEditorSkinCompatibility.LightOnly
                    ? PungentEditorSkinUssGenerator.BuildUss(profile, PungentEditorSkinSelectorMap.Defaults, true)
                    : common;
                string light = profile.compatibility == PungentEditorSkinCompatibility.DarkOnly
                    ? PungentEditorSkinUssGenerator.BuildUss(profile, PungentEditorSkinSelectorMap.Defaults, true)
                    : common;

                WriteProjectFile(CommonUssPath, common);
                WriteProjectFile(DarkUssPath, dark);
                WriteProjectFile(LightUssPath, light);
                ImportGeneratedFiles();
                LastAction = $"Generated editor USS bridge files at {DateTime.Now:HH:mm:ss}. Unity may require a panel redraw, domain refresh, or editor restart for every selector to update.";
                if (PungentEditorGuiStyleBridge.SessionOverridesEnabled)
                    PungentEditorGuiStyleBridge.ApplySessionThemeToKnownStyles();
                RepaintAllViewsSoon();
            }
            catch (Exception ex)
            {
                LastAction = "Failed to generate editor USS bridge files: " + ex.Message;
                Debug.LogException(ex);
            }
        }

        public static void DisableAndRestore()
        {
            ExperimentalBridgeEnabled = false;
            try
            {
                EnsureExtensionFolder();
                PungentEditorGuiStyleBridge.DisableAndRestore();
                string disabled = PungentEditorSkinUssGenerator.BuildUss(PungentEditorSkinProfile.Default, PungentEditorSkinSelectorMap.Defaults, true);
                WriteProjectFile(CommonUssPath, disabled);
                WriteProjectFile(DarkUssPath, disabled);
                WriteProjectFile(LightUssPath, disabled);
                ImportGeneratedFiles();
                LastAction = $"Disabled editor USS bridge and restored empty extension files at {DateTime.Now:HH:mm:ss}.";
                RepaintAllViewsSoon();
            }
            catch (Exception ex)
            {
                LastAction = "Failed to restore editor USS bridge files: " + ex.Message;
                Debug.LogException(ex);
            }
        }

        public static string ProjectRelativeToAbsolute(string projectRelativePath)
        {
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
            return Path.Combine(projectRoot, projectRelativePath).Replace('\\', '/');
        }

        public static void PingGeneratedFolder()
        {
            DefaultAsset asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ExtensionFolder);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        private static void EnsureExtensionFolder()
        {
            string absolute = ProjectRelativeToAbsolute(ExtensionFolder);
            if (!Directory.Exists(absolute))
                Directory.CreateDirectory(absolute);
        }

        private static void WriteProjectFile(string projectRelativePath, string contents)
        {
            string absolute = ProjectRelativeToAbsolute(projectRelativePath);
            string directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(absolute, contents ?? string.Empty);
        }

        private static void ImportGeneratedFiles()
        {
            AssetDatabase.ImportAsset(CommonUssPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(DarkUssPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(LightUssPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void RepaintAllViewsSoon()
        {
            EditorApplication.delayCall += () => UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
#endif
}
