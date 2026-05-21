namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;

    /// <summary>
    /// Compatibility shim for older editor scripts that used the old "Lab" terminology.
    /// New product-facing code should use PungentUtilityCategories instead.
    /// </summary>
    [Obsolete("Use PungentUtilityCategories. The old Lab terminology is retained only for source compatibility.")]
    public static class PungentUtilityLabs
    {
        public const string UtilityCore = PungentUtilityCategories.Core;
        public const string DebugLab = PungentUtilityCategories.Debug;
        public const string AssetPlacementLab = PungentUtilityCategories.Creation;
        public const string SceneWorkflowLab = PungentUtilityCategories.SceneTools;
        public const string ColourLab = PungentUtilityCategories.Creation;
        public const string TextureLab = PungentUtilityCategories.Creation;
        public const string AudioLab = PungentUtilityCategories.Audio;
        public const string AssetPreviewExportLab = PungentUtilityCategories.Assets;
        public const string ProjectAuditAuthoringLab = PungentUtilityCategories.Audit;
        public const string ContentGenerationLab = PungentUtilityCategories.Creation;
        public const string UIFeedbackLab = PungentUtilityCategories.UI;
        public const string EnvironmentSimulationLab = PungentUtilityCategories.Creation;
        public const string SaveSettingsLab = PungentUtilityCategories.Core;
        public const string ActionAuthoringLab = PungentUtilityCategories.Creation;
        public const string AgentSimulationLab = PungentUtilityCategories.Creation;
        public const string VisualizationLab = PungentUtilityCategories.UI;
        public const string Other = PungentUtilityCategories.Other;

        public static readonly string[] CurrentLabs = PungentUtilityCategories.CurrentCategories;
        public static readonly string[] PlannedLabs = Array.Empty<string>();

        public static int SortKey(string lab) => PungentUtilityCategories.SortKey(lab);
        public static string Normalize(string lab) => PungentUtilityCategories.Normalize(lab);
        public static string GetDescription(string lab) => PungentUtilityCategories.GetDescription(lab);
    }
#endif
}
