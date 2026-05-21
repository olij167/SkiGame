namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;

    /// <summary>
    /// Shared lab names used by the PungentFunk utility registry and launcher UI.
    /// Labs are product/navigation boundaries, not mandatory hard dependencies between tools.
    /// </summary>
    public static class PungentUtilityLabs
    {
        public const string UtilityCore = "Utility Core";
        public const string DebugLab = "Debug Lab";
        public const string AssetPlacementLab = "Asset Placement Lab";
        public const string SceneWorkflowLab = "Scene Workflow Lab";
        public const string ColourLab = "Colour Lab";
        public const string TextureLab = "Texture Lab";
        public const string AudioLab = "Audio Lab";
        public const string AssetPreviewExportLab = "Asset Preview & Export Lab";
        public const string ProjectAuditAuthoringLab = "Project Audit & Authoring Lab";
        public const string ContentGenerationLab = "Content Generation Lab";
        public const string UIFeedbackLab = "UI & Feedback Lab";
        public const string EnvironmentSimulationLab = "Environment Simulation Lab";
        public const string SaveSettingsLab = "Save & Settings Lab";
        public const string ActionAuthoringLab = "Action Authoring Lab";
        public const string AgentSimulationLab = "Agent Simulation Lab";
        public const string VisualizationLab = "Visualization Lab";
        public const string Other = "Other";

        public static readonly string[] CurrentLabs =
        {
            UtilityCore,
            DebugLab,
            AssetPlacementLab,
            SceneWorkflowLab,
            ColourLab,
            TextureLab,
            AudioLab,
            AssetPreviewExportLab,
            ProjectAuditAuthoringLab,
            ContentGenerationLab,
            UIFeedbackLab
        };

        public static readonly string[] PlannedLabs =
        {
            EnvironmentSimulationLab,
            SaveSettingsLab,
            ActionAuthoringLab,
            AgentSimulationLab,
            VisualizationLab
        };

        public static int SortKey(string lab)
        {
            switch (lab)
            {
                case UtilityCore: return 0;
                case DebugLab: return 10;
                case AssetPlacementLab: return 20;
                case SceneWorkflowLab: return 30;
                case ColourLab: return 40;
                case TextureLab: return 50;
                case AudioLab: return 60;
                case AssetPreviewExportLab: return 70;
                case ProjectAuditAuthoringLab: return 80;
                case ContentGenerationLab: return 90;
                case UIFeedbackLab: return 100;
                case EnvironmentSimulationLab: return 200;
                case SaveSettingsLab: return 210;
                case ActionAuthoringLab: return 220;
                case AgentSimulationLab: return 230;
                case VisualizationLab: return 240;
                case Other: return 1000;
                default: return 900;
            }
        }

        public static string Normalize(string lab)
        {
            if (string.IsNullOrWhiteSpace(lab))
                return Other;

            string trimmed = lab.Trim();
            for (int i = 0; i < CurrentLabs.Length; i++)
            {
                if (string.Equals(CurrentLabs[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return CurrentLabs[i];
            }

            for (int i = 0; i < PlannedLabs.Length; i++)
            {
                if (string.Equals(PlannedLabs[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return PlannedLabs[i];
            }

            if (string.Equals(Other, trimmed, StringComparison.OrdinalIgnoreCase))
                return Other;

            return trimmed;
        }

        public static string GetDescription(string lab)
        {
            switch (Normalize(lab))
            {
                case UtilityCore:
                    return "Shared shell, registry, lab routing, theme system, preferences, tray, and common editor UX helpers.";
                case DebugLab:
                    return "Runtime/editor debug control, signal routing, reflected debug actions, and diagnostic workflows.";
                case AssetPlacementLab:
                    return "Reusable scene asset placement, procedural dressing, surface workflows, sockets, groups, and placement validation.";
                case SceneWorkflowLab:
                    return "Scene authoring speed tools: navigation, gizmo sources, surface alignment, path authoring, and SceneView helpers.";
                case ColourLab:
                    return "Palette creation, generation, accessibility analysis, harmony tools, and palette application workflows.";
                case TextureLab:
                    return "Procedural texture and mask generation plus texture-array baking for reusable material workflows.";
                case AudioLab:
                    return "Surface/contact audio data, setup coverage, audio catalog audits, interaction matrices, and runtime routing helpers.";
                case AssetPreviewExportLab:
                    return "Reusable asset preview, icon rendering, font preview, prefab extraction, and export-preparation tools.";
                case ProjectAuditAuthoringLab:
                    return "Generic scanners, validators, coverage matrices, reference repair, tuning copy, rename, and documentation helpers.";
                case ContentGenerationLab:
                    return "Reusable content generation utilities such as name lists, MadLib patterns, and future token/text generators.";
                case UIFeedbackLab:
                    return "Input prompt, tooltip, floating text, notification, animation, and runtime UI feedback tools without fixed layouts.";
                case EnvironmentSimulationLab:
                    return "Planned suite for generic time, calendar, weather, wind, surface conditions, and environment volumes.";
                case SaveSettingsLab:
                    return "Planned suite for generic settings profiles, JSON storage, save slots, and input binding persistence.";
                case ActionAuthoringLab:
                    return "Planned suite for generic action sequences, conditions, targeting, effects, and resolver workflows.";
                case AgentSimulationLab:
                    return "Planned suite for generic utility AI, agent state, needs, traits, emotions, and interaction scoring.";
                case VisualizationLab:
                    return "Planned suite for reusable matrix, graph, radar, heatmap, and coverage visualizers.";
                default:
                    return "Unclassified or custom lab. Consider assigning a standard PungentFunk lab before distribution.";
            }
        }
    }
    #endif

}