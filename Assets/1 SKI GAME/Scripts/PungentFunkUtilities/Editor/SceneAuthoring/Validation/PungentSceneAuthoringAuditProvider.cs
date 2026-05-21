using System;
using System.Collections.Generic;
using System.Diagnostics;
using PungentFunk.Utilities.Editor.Scanning;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PungentFunk.Utilities.Editor.SceneAuthoring
{
#if UNITY_EDITOR
    internal sealed class PungentSceneAuthoringAuditProvider : IPungentAuditScanProvider
    {
        private const int HighProviderCountWarning = 96;
        private const int HighDrawCostWarning = 1800;

        public string ProviderId => "scene-authoring-foundation";
        public string DisplayName => "Scene Authoring Foundation";
        public string Description => "Scans currently open scenes for generic spatial authoring setup, path/area validity, generated-output state, runtime consumer bindings, and spatial draw density.";
        public string OpenButtonLabel => "Open Spatial Authoring";
        public string RunButtonLabel => "Scan Scene Authoring";
        public bool CanRunImmediate => true;
        public bool CanRunBackground => true;
        public bool CanRunFromCoordinator => true;
        public bool CanPause => false;
        public bool CanCancel => true;
        public bool UsesSceneOpening => false;
        public bool UsesAssetDatabase => false;
        public bool UsesModalProgress => false;
        public bool IsCooperative => true;
        public bool IsMonolithic => false;
        public bool IsScanOnly => true;

        public bool TryGetNotConfiguredReason(out string reason)
        {
            reason = string.Empty;
            return false;
        }

        public PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative(ProviderId, DisplayName, RunScan);
            job.scanMode = mode;
            job.canPause = false;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.BackgroundSafe |
                               PungentAuditScanJobCapabilities.ScanOnly;
            return job;
        }

        public void OpenWindow()
        {
            PungentFunk.Utilities.Editor.Core.PungentUtilityRegistry.Open("spatial-authoring-workbench");
        }

        private PungentAuditScanStepResult RunScan(PungentAuditScanContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PungentScanResult result = new PungentScanResult(ProviderId, DisplayName, PungentScanScope.OpenScenes, "Current open scenes");
            context.Report(0.05f, 0, 1, "Scanning scene authoring providers...", false, "Scene Authoring");

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            Dictionary<string, Component> stableIds = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
            List<PungentSpatialValidationIssue> validationIssues = new List<PungentSpatialValidationIssue>();
            int scanned = 0;
            int spatialProviderCount = 0;
            int estimatedDrawCost = 0;
            int estimatedPointCount = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (!IsOpenSceneComponent(behaviour))
                    continue;

                bool isPath = behaviour is IPungentPathPointProvider || behaviour is IPungentPathQueryProvider;
                bool isArea = behaviour is IPungentAreaShapeProvider || behaviour is IPungentAreaVolumeProvider;
                bool isGenerated = behaviour is IPungentGeneratedOutputProvider;
                bool isVisualization = behaviour is IPungentSpatialVisualizationProvider;
                bool isConsumer = behaviour is IPungentSpatialConsumer;
                bool isMetadata = behaviour is IPungentSpatialObjectMetadataProvider;

                if (!isPath && !isArea && !isGenerated && !isVisualization && !isConsumer && !isMetadata)
                    continue;

                scanned++;
                ScanStableId(result, stableIds, behaviour);
                ScanMetadata(result, behaviour);

                if (isPath)
                    ScanPath(result, behaviour, validationIssues);
                if (isArea)
                    ScanArea(result, behaviour, validationIssues);
                if (isGenerated)
                    ScanGeneratedOutputs(result, (IPungentGeneratedOutputProvider)behaviour, behaviour);
                if (isConsumer)
                    ScanConsumer(result, (IPungentSpatialConsumer)behaviour, behaviour);
                if (isVisualization)
                    CollectVisualizationDensity(result, (IPungentSpatialVisualizationProvider)behaviour, behaviour, ref spatialProviderCount, ref estimatedDrawCost, ref estimatedPointCount);

                ScanBrokenReferences(result, behaviour);
            }

            if (spatialProviderCount >= HighProviderCountWarning || estimatedDrawCost >= HighDrawCostWarning)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "High scene-authoring draw density",
                    $"Open scenes contain {spatialProviderCount} spatial visualization provider(s), about {estimatedDrawCost} estimated draw operations, and {estimatedPointCount} point/label candidates. Prefer selected-only drawing, lower preview density, or split authoring work into focused scenes if Scene View slows down.",
                    null,
                    null,
                    "SCENE_AUTHORING_DRAW_DENSITY");
            }

            result.TotalScanned = scanned;
            result.TotalMatched = result.Issues.Count;
            stopwatch.Stop();
            result.Complete(stopwatch.Elapsed.TotalSeconds, $"Scene Authoring Foundation scan complete: {result.Issues.Count} finding(s) across {scanned} provider component(s).");
            context.Report(1f, scanned, scanned, result.StatusMessage, false, "Complete");
            return PungentAuditScanStepResult.Complete(result.StatusMessage, result);
        }

        private static bool IsOpenSceneComponent(Component component)
        {
            if (component == null || component.gameObject == null)
                return false;
            if (EditorUtility.IsPersistent(component) || EditorUtility.IsPersistent(component.gameObject))
                return false;

            Scene scene = component.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static void ScanStableId(PungentScanResult result, Dictionary<string, Component> stableIds, Component component)
        {
            string id = GetStableId(component);
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (stableIds.TryGetValue(id, out Component first) && first != null && first != component)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "Duplicate spatial stable ID",
                    $"'{component.name}' shares stable ID '{id}' with '{first.name}'. Stable IDs should be unique before map, output, query, or data-sheet bridges depend on them.",
                    component,
                    null,
                    "SCENE_AUTHORING_DUPLICATE_STABLE_ID");
                return;
            }

            stableIds[id] = component;
        }

        private static string GetStableId(Component component)
        {
            if (component is IPungentSpatialObjectMetadataProvider metadataProvider &&
                metadataProvider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) &&
                metadata != null)
                return metadata.StableId;

            if (component is IPungentSpatialLabelProvider labels)
                return labels.SpatialId;

            return string.Empty;
        }

        private static void ScanMetadata(PungentScanResult result, Component component)
        {
            if (!(component is IPungentSpatialObjectMetadataProvider metadataProvider))
                return;

            if (!metadataProvider.TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata) || metadata == null)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "Missing spatial metadata",
                    $"'{component.name}' exposes spatial metadata but returned no metadata object.",
                    component,
                    null,
                    "SCENE_AUTHORING_MISSING_METADATA");
                return;
            }

            if (metadata.semanticProfile == PungentSpatialProfile.Generic)
            {
                result.AddIssue(
                    PungentScanSeverity.Info,
                    "Spatial profile is generic",
                    $"'{component.name}' uses the Generic spatial profile. Assign a more specific profile when runtime consumers need to distinguish paths, regions, zones, or placement surfaces.",
                    component,
                    null,
                    "SCENE_AUTHORING_GENERIC_PROFILE");
            }
        }

        private static void ScanPath(PungentScanResult result, Component component, List<PungentSpatialValidationIssue> validationIssues)
        {
            if (component is IPungentPathPointProvider pointProvider)
            {
                if (pointProvider.PointCount < 2)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Path has too few points",
                        $"'{component.name}' needs at least two points before it can provide useful sampling, output lanes, or runtime queries.",
                        component,
                        null,
                        "SCENE_AUTHORING_PATH_TOO_FEW_POINTS");
                }
            }

            validationIssues.Clear();
            if (component is IPungentSpatialValidationProvider validationProvider)
                validationProvider.GetSpatialValidationIssues(validationIssues);
            else if (component is IPungentPathPointProvider provider)
                PungentSpatialValidationUtility.ValidatePath(provider, validationIssues);

            AddValidationIssues(result, component, validationIssues);

            if (component is IPungentPathQueryProvider queryProvider &&
                component is IPungentPathPointProvider pathProvider &&
                pathProvider.PointCount >= 2 &&
                !queryProvider.TrySamplePath(0.5f, out _))
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "Path sample data is invalid",
                    $"'{component.name}' has enough control points but could not sample the path midpoint. Check duplicate points, sampling settings, or adapter implementation.",
                    component,
                    null,
                    "SCENE_AUTHORING_PATH_SAMPLE_INVALID");
            }
        }

        private static void ScanArea(PungentScanResult result, Component component, List<PungentSpatialValidationIssue> validationIssues)
        {
            if (component is IPungentAreaShapeProvider shapeProvider && !shapeProvider.TryGetAreaShape(out _))
            {
                result.AddIssue(
                    PungentScanSeverity.Error,
                    "Area shape is invalid",
                    $"'{component.name}' could not provide a usable area shape.",
                    component,
                    null,
                    "SCENE_AUTHORING_AREA_INVALID_SHAPE");
            }

            validationIssues.Clear();
            if (component is IPungentSpatialValidationProvider validationProvider)
                validationProvider.GetSpatialValidationIssues(validationIssues);
            else if (component is IPungentAreaShapeProvider provider)
                PungentSpatialValidationUtility.ValidateArea(provider, validationIssues);

            AddValidationIssues(result, component, validationIssues);
        }

        private static void AddValidationIssues(PungentScanResult result, Component component, List<PungentSpatialValidationIssue> validationIssues)
        {
            for (int i = 0; i < validationIssues.Count; i++)
            {
                PungentSpatialValidationIssue issue = validationIssues[i];
                result.AddIssue(
                    ToScanSeverity(issue.Severity),
                    issue.Code,
                    issue.Message,
                    component,
                    null,
                    NormalizeIssueCode(issue.Code));
            }
        }

        private static PungentScanSeverity ToScanSeverity(PungentSpatialValidationSeverity severity)
        {
            switch (severity)
            {
                case PungentSpatialValidationSeverity.Error:
                    return PungentScanSeverity.Error;
                case PungentSpatialValidationSeverity.Warning:
                    return PungentScanSeverity.Warning;
                default:
                    return PungentScanSeverity.Info;
            }
        }

        private static string NormalizeIssueCode(string code)
        {
            return string.IsNullOrWhiteSpace(code)
                ? "SCENE_AUTHORING_VALIDATION"
                : "SCENE_AUTHORING_" + code.Trim().Replace('-', '_').ToUpperInvariant();
        }

        private static void ScanGeneratedOutputs(PungentScanResult result, IPungentGeneratedOutputProvider provider, Component component)
        {
            int count = Mathf.Max(0, provider.GeneratedOutputCount);
            for (int i = 0; i < count; i++)
            {
                if (!provider.TryGetGeneratedOutput(i, out PungentGeneratedOutputDescriptor output))
                    continue;

                if (output.MayBeStale)
                {
                    result.AddIssue(
                        PungentScanSeverity.Info,
                        "Generated output may be stale",
                        $"'{component.name}' reports '{output.DisplayName}' may be stale. Validate or apply the output recipe before relying on generated scene objects.",
                        component,
                        null,
                        "SCENE_AUTHORING_OUTPUT_STALE");
                }

                if (output.EstimatedObjectCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold ||
                    output.ExistingObjectCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Generated output count is high",
                        $"'{component.name}' output '{output.DisplayName}' estimates {output.EstimatedObjectCount} object(s) and currently has {output.ExistingObjectCount}. Consider spacing, gaps, selected-only previews, or splitting recipes.",
                        component,
                        null,
                        "SCENE_AUTHORING_OUTPUT_COUNT_HIGH");
                }
            }
        }

        private static void ScanConsumer(PungentScanResult result, IPungentSpatialConsumer consumer, Component component)
        {
            if (consumer.HasValidSpatialSource)
                return;

            result.AddIssue(
                PungentScanSeverity.Warning,
                "Spatial consumer is missing data",
                $"'{component.name}' is a runtime spatial consumer but has no valid spatial source assigned.",
                component,
                null,
                "SCENE_AUTHORING_CONSUMER_MISSING_SOURCE");
        }

        private static void CollectVisualizationDensity(
            PungentScanResult result,
            IPungentSpatialVisualizationProvider provider,
            Component component,
            ref int providerCount,
            ref int estimatedDrawCost,
            ref int estimatedPointCount)
        {
            if (!provider.TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot))
                return;

            providerCount++;
            estimatedDrawCost += Mathf.Max(0, snapshot.EstimatedDrawCost);
            estimatedPointCount += Mathf.Max(0, snapshot.PointCount);
            if (snapshot.HasWarnings)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "Spatial visualization warning",
                    $"'{component.name}' reports elevated spatial visualization cost or validation risk. Reduce preview density, labels, or generated child counts if Scene View becomes slow.",
                    component,
                    null,
                    "SCENE_AUTHORING_SPATIAL_VISUAL_WARNING");
            }
        }

        private static void ScanBrokenReferences(PungentScanResult result, MonoBehaviour behaviour)
        {
            if (behaviour is PungentPathInstance path)
            {
                if ((path.useAssetPoints || path.useAssetMetadata) && path.asset == null)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Path instance asset reference is missing",
                        $"'{path.name}' is configured to use path asset data, but no PungentPathAsset is assigned.",
                        path,
                        null,
                        "SCENE_AUTHORING_PATH_ASSET_MISSING");
                }
            }

            if (behaviour is PungentAreaInstance area)
            {
                if ((area.useAssetShape || area.useAssetMetadata) && area.asset == null)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Area instance asset reference is missing",
                        $"'{area.name}' is configured to use area asset data, but no PungentAreaAsset is assigned.",
                        area,
                        null,
                        "SCENE_AUTHORING_AREA_ASSET_MISSING");
                }
            }

            if (behaviour is PungentModularSpatialOutput output && output.source == null)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "Spatial Output Recipe has no source",
                    $"'{output.name}' cannot generate until a path or area source is assigned.",
                    output,
                    null,
                    "SCENE_AUTHORING_OUTPUT_SOURCE_MISSING");
            }
        }
    }
#endif
}
