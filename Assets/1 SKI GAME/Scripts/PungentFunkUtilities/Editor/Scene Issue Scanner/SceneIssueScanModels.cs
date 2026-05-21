namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using PungentFunk.Utilities.Editor.Scanning;
    using UnityEngine;

    internal enum SceneIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    internal enum SceneIssueCategory
    {
        MissingScript,
        MissingReference,
        BrokenMaterial,
        MissingMeshOrSprite,
        DuplicateSceneService,
        AdvisoryPerformance,
        SharedScenePassRequired
    }

    internal enum SceneIssueScanScope
    {
        OpenScenesOnly,
        BuildScenesOnly,
        AllProjectScenes,
        SelectedSceneAssets
    }

    internal sealed class SceneIssueScanSettings
    {
        public SceneIssueScanScope Scope = SceneIssueScanScope.OpenScenesOnly;
        public bool IncludeInactiveObjects = true;
        public bool IncludeAdvisoryFindings;
        public bool IncludePackageScenes;
        public int ExcessiveCameraThreshold = 8;
        public int ExcessiveRealtimeLightThreshold = 16;

        public SceneIssueScanSettings Clone()
        {
            return new SceneIssueScanSettings
            {
                Scope = Scope,
                IncludeInactiveObjects = IncludeInactiveObjects,
                IncludeAdvisoryFindings = IncludeAdvisoryFindings,
                IncludePackageScenes = IncludePackageScenes,
                ExcessiveCameraThreshold = ExcessiveCameraThreshold,
                ExcessiveRealtimeLightThreshold = ExcessiveRealtimeLightThreshold
            };
        }
    }

    internal sealed class SceneIssueRecord
    {
        public SceneIssueSeverity Severity;
        public SceneIssueCategory Category;
        public string ScenePath;
        public string SceneName;
        public string HierarchyPath;
        public string Title;
        public string Message;
        public string Code;
        public UnityEngine.Object Context;

        public PungentScanIssue ToScanIssue()
        {
            return new PungentScanIssue(
                ToScanSeverity(Severity),
                Title,
                Message,
                Context,
                BuildPath(),
                Code);
        }

        private string BuildPath()
        {
            if (string.IsNullOrWhiteSpace(ScenePath))
                return HierarchyPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(HierarchyPath))
                return ScenePath;
            return ScenePath + " :: " + HierarchyPath;
        }

        private static PungentScanSeverity ToScanSeverity(SceneIssueSeverity severity)
        {
            switch (severity)
            {
                case SceneIssueSeverity.Error:
                    return PungentScanSeverity.Error;
                case SceneIssueSeverity.Warning:
                    return PungentScanSeverity.Warning;
                default:
                    return PungentScanSeverity.Info;
            }
        }
    }

    internal sealed class SceneIssueScanSummary
    {
        public int ScenesDiscovered;
        public int BuildScenesIncluded;
        public int BuildScenesExcluded;
        public int ScenesScanned;
        public int ScenesSkipped;
        public int ObjectsScanned;
        public int IssuesFound;
        public TimeSpan Duration;
        public string StatusMessage;
    }

    internal static class SceneIssueSharedSceneScanPlan
    {
        public const string Note =
            "Full-project Scene Issue scanning now runs through the shared scene scan coordinator. Design Validation Audit resolves the Terrain Usage scene scope once, opens each scoped scene once, runs enabled scene analyzers while that scene is loaded, publishes provider-specific PungentScanResult entries, and restores the original scene setup on pause, completion, restart cleanup, or failure.";
    }
#endif
}
