namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.IO;
    using UnityEditor;

    public sealed class PungentEditorSkinDiagnostics
    {
        public bool bridgeEnabled;
        public bool proSkin;
        public string baseTheme;
        public string extensionFolder;
        public string commonPath;
        public string darkPath;
        public string lightPath;
        public bool commonExists;
        public bool darkExists;
        public bool lightExists;
        public int selectorCount;
        public string lastAction;

        public static PungentEditorSkinDiagnostics Capture()
        {
            string common = PungentEditorSkinBridge.CommonUssPath;
            string dark = PungentEditorSkinBridge.DarkUssPath;
            string light = PungentEditorSkinBridge.LightUssPath;
            return new PungentEditorSkinDiagnostics
            {
                bridgeEnabled = PungentEditorSkinBridge.ExperimentalBridgeEnabled,
                proSkin = EditorGUIUtility.isProSkin,
                baseTheme = EditorGUIUtility.isProSkin ? "Unity Dark" : "Unity Light",
                extensionFolder = PungentEditorSkinBridge.ExtensionFolder,
                commonPath = common,
                darkPath = dark,
                lightPath = light,
                commonExists = File.Exists(PungentEditorSkinBridge.ProjectRelativeToAbsolute(common)),
                darkExists = File.Exists(PungentEditorSkinBridge.ProjectRelativeToAbsolute(dark)),
                lightExists = File.Exists(PungentEditorSkinBridge.ProjectRelativeToAbsolute(light)),
                selectorCount = PungentEditorSkinSelectorMap.Defaults.Count,
                lastAction = PungentEditorSkinBridge.LastAction
            };
        }
    }
#endif
}
