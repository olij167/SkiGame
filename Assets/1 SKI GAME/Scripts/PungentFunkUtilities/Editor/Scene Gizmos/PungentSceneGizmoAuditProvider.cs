using PungentFunk.Utilities.Editor.Scanning;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using UnityEditor;
    using UnityEngine;

    internal sealed class PungentSceneGizmoAuditProvider : IPungentAuditScanProvider
    {
        public string ProviderId => "scene-gizmo-performance";
        public string DisplayName => "Scene Gizmo Performance";
        public string Description => "Scans current open scenes for excessive PungentFunk gizmo density, invalid rules, expensive child-bounds rules, trajectory overload, label overload, and provider-cache issues.";
        public string OpenButtonLabel => "Open Scene Gizmos";
        public string RunButtonLabel => "Scan Scene Gizmos";
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
            job.capabilities |= PungentAuditScanJobCapabilities.BackgroundSafe | PungentAuditScanJobCapabilities.ScanOnly;
            return job;
        }

        public void OpenWindow()
        {
            PungentGizmoBrowserWindow.Open();
        }

        private PungentAuditScanStepResult RunScan(PungentAuditScanContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PungentScanResult result = new PungentScanResult(ProviderId, DisplayName, PungentScanScope.OpenScenes, "Current open scenes");
            context.Report(0.05f, 0, 1, "Scanning scene gizmos...", false, "Scene Gizmos");

            PungentSceneGizmoProviderCache.ForceRebuild();
            IReadOnlyList<PungentSceneGizmoProviderCache.ProviderRecord> providers = PungentSceneGizmoProviderCache.GetProviders();
            PungentSceneGizmoProviderCache.Summary providerSummary = PungentSceneGizmoProviderCache.GetSummary();
            PungentSceneGizmoSource[] sources = Resources.FindObjectsOfTypeAll<PungentSceneGizmoSource>();

            int activeSourceCount = 0;
            int enabledRuleCount = 0;
            int labelCount = providerSummary.estimatedLabels;
            int drawOps = providerSummary.estimatedDrawOperations;
            int alwaysVisibleBeaconCount = 0;

            for (int i = 0; i < sources.Length; i++)
            {
                PungentSceneGizmoSource source = sources[i];
                if (source == null || source.gameObject == null || EditorUtility.IsPersistent(source.gameObject))
                    continue;

                if (source.drawInScene && source.isActiveAndEnabled && source.gameObject.activeInHierarchy)
                    activeSourceCount++;

                enabledRuleCount += source.EstimateEnabledRuleCount();
                labelCount += source.EstimateLabelCount();
                drawOps += source.EstimateDrawOperations();
                ScanSource(result, source);
            }

            for (int i = 0; i < providers.Count; i++)
            {
                PungentSceneGizmoProviderCache.ProviderRecord provider = providers[i];
                if (provider == null || provider.component == null)
                    continue;

                if (provider.isBeacon && provider.alwaysVisible && provider.visible && provider.active)
                    alwaysVisibleBeaconCount++;

                ScanProviderRecord(result, provider);
                CollectProviderSnapshotDiagnostics(result, provider);
            }

            int activeCount = activeSourceCount + providerSummary.activeProviderCount;
            AddDensityIssues(result, activeCount, enabledRuleCount, providerSummary.providerComponentCount, providerSummary.estimatedTrajectorySamples, labelCount, drawOps, alwaysVisibleBeaconCount);

            result.TotalScanned = sources.Length + providerSummary.providerComponentCount;
            result.TotalMatched = result.Issues.Count;
            stopwatch.Stop();
            result.Complete(stopwatch.Elapsed.TotalSeconds, $"Scene Gizmo Performance scan complete: {result.Issues.Count} finding(s).");
            context.Report(1f, result.TotalScanned, result.TotalScanned, result.StatusMessage, false, "Complete");
            return PungentAuditScanStepResult.Complete(result.StatusMessage, result);
        }

        private static void ScanSource(PungentScanResult result, PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            List<PungentSceneGizmoSource.RuleStatus> statuses = source.GetRuleStatuses(includeChildBoundsEstimate: true);
            for (int i = 0; i < statuses.Count; i++)
            {
                PungentSceneGizmoSource.RuleStatus status = statuses[i];
                if (status == null)
                    continue;

                if (!status.IsValid)
                {
                    result.AddIssue(
                        PungentScanSeverity.Error,
                        "Invalid scene gizmo binding",
                        $"Rule '{status.ruleName}' on '{source.name}' has an invalid reflected binding. Fix the member path or assign the required component before relying on this gizmo.",
                        source,
                        null,
                        "SCENE_GIZMO_INVALID_BINDING");
                }

                if (status.hasExpensiveRuleWarning)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Expensive scene gizmo rule",
                        $"Rule '{status.ruleName}' on '{source.name}' may require expensive child bounds or binding work. Enable caching, reduce scope, or switch non-essential rules to Selected-only drawing.",
                        source,
                        null,
                        "SCENE_GIZMO_EXPENSIVE_RULE");
                }
            }

            if (source.rules == null)
                return;

            for (int i = 0; i < source.rules.Count; i++)
            {
                PungentSceneGizmoSource.GizmoRule rule = source.rules[i];
                if (rule == null || !rule.enabled)
                    continue;

                if (rule.shape == PungentSceneGizmoSource.GizmoShape.ChildBounds && !source.cacheChildRendererBounds)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Child-bounds caching disabled",
                        $"'{source.name}' has a child-bounds rule with renderer bounds caching disabled. This can make Scene View repaints traverse child renderers often; enable caching or reduce the rule.",
                        source,
                        null,
                        "SCENE_GIZMO_CHILD_BOUNDS_CACHE");
                }

                if (source.drawInScene && rule.drawWhen == PungentSceneGizmoSource.DrawWhen.Always && PungentSceneGizmoPerformancePolicy.IsHighCost(statusEstimatedOps(rule), 0))
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "High-cost always-visible rule",
                        $"'{source.name}' has a high-cost rule that is always visible. Consider changing it to Selected-only drawing to reduce routine Scene View cost.",
                        source,
                        null,
                        "SCENE_GIZMO_HIGH_COST_ALWAYS_VISIBLE");
                }
            }
        }

        private static int statusEstimatedOps(PungentSceneGizmoSource.GizmoRule rule)
        {
            if (rule == null || !rule.enabled)
                return 0;

            if (rule.shape == PungentSceneGizmoSource.GizmoShape.ChildBounds)
                return 64;
            if (rule.shape == PungentSceneGizmoSource.GizmoShape.DistanceBetween)
                return 3;
            return 1;
        }

        private static void ScanProviderRecord(PungentScanResult result, PungentSceneGizmoProviderCache.ProviderRecord provider)
        {
            if (provider == null || provider.component == null)
                return;

            if (!string.IsNullOrWhiteSpace(provider.warning))
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    provider.providerCategory + " setup warning",
                    $"{provider.component.name}: {provider.warning} Fix the provider setup before relying on this Scene View diagnostic.",
                    provider.component,
                    null,
                    "SCENE_GIZMO_PROVIDER_WARNING");
            }

            if (PungentSceneGizmoPerformancePolicy.IsHighCost(provider.estimatedDrawOperations, provider.estimatedTrajectorySamples) && provider.visible && !provider.selectedOnly)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "High-cost provider is not selected-only",
                    $"{provider.component.name} estimates {provider.estimatedDrawOperations} draw operations. Switch it to Selected-only drawing or lower sample/label limits if the Scene View becomes sluggish.",
                    provider.component,
                    null,
                    "SCENE_GIZMO_HIGH_COST_PROVIDER");
            }

            if (provider.component is PungentCollisionSensorGizmo collision)
            {
                Collider collider = collision.observedCollider != null ? collision.observedCollider : collision.GetComponent<Collider>();
                if (collider == null)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Collision sensor has no collider",
                        $"{collision.name} cannot draw collider/contact diagnostics until an observed collider is assigned or a Collider is added to the same GameObject.",
                        collision,
                        null,
                        "SCENE_GIZMO_COLLISION_NO_COLLIDER");
                }
            }

            if (provider.component is PungentTriggerSensorGizmo trigger)
            {
                Collider collider = trigger.observedTrigger != null ? trigger.observedTrigger : trigger.GetComponent<Collider>();
                if (collider == null)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Trigger sensor has no collider",
                        $"{trigger.name} cannot draw trigger overlap diagnostics until an observed trigger is assigned or a Collider is added to the same GameObject.",
                        trigger,
                        null,
                        "SCENE_GIZMO_TRIGGER_NO_COLLIDER");
                }
                else if (!collider.isTrigger)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Trigger sensor collider is not a trigger",
                        $"{trigger.name} observes '{collider.name}', but that collider is not marked as Trigger. Enable Is Trigger or assign a trigger collider.",
                        trigger,
                        null,
                        "SCENE_GIZMO_TRIGGER_NOT_TRIGGER");
                }
            }

            if (provider.component is PungentTrajectoryVisualizer trajectory)
                ScanTrajectory(result, trajectory);
        }

        private static void ScanTrajectory(PungentScanResult result, PungentTrajectoryVisualizer trajectory)
        {
            if (trajectory == null)
                return;

            if (trajectory.timeStep <= 0f)
            {
                result.AddIssue(
                    PungentScanSeverity.Error,
                    "Trajectory time step is invalid",
                    $"{trajectory.name} has a non-positive time step. Increase Time Step so trajectory sampling cannot become unbounded.",
                    trajectory,
                    null,
                    "SCENE_GIZMO_TRAJECTORY_TIMESTEP");
            }

            if (trajectory.maxSamples > PungentSceneGizmoPerformancePolicy.DefaultMaxTrajectorySamplesPerComponent || trajectory.duration > PungentTrajectoryUtility.MaxDuration * 0.5f)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "Trajectory sample budget is high",
                    $"{trajectory.name} can request {trajectory.maxSamples} samples over {trajectory.duration:0.##}s. Lower Max Samples, increase Time Step, or disable labels/sample points for routine authoring.",
                    trajectory,
                    null,
                    "SCENE_GIZMO_TRAJECTORY_OVERLOAD");
            }
        }

        private static void CollectProviderSnapshotDiagnostics(PungentScanResult result, PungentSceneGizmoProviderCache.ProviderRecord provider)
        {
            try
            {
                Component component = provider.component;
                if (component is IPungentSceneBeaconProvider beacon)
                    beacon.TryGetBeaconSnapshot(out _);
                if (component is IPungentCollisionSensorProvider collision)
                    collision.TryGetCollisionSensorSnapshot(out _);
                if (component is IPungentTriggerSensorProvider trigger)
                    trigger.TryGetTriggerSensorSnapshot(out _);
                if (component is IPungentTrajectoryProvider trajectory)
                    trajectory.TryGetTrajectorySnapshot(new List<PungentTrajectorySample>(PungentSceneGizmoPerformancePolicy.DefaultMaxTrajectorySamplesPerComponent), out _);
            }
            catch (Exception ex)
            {
                result.AddIssue(
                    PungentScanSeverity.Error,
                    "Provider snapshot threw an exception",
                    $"{provider.component.name} threw while collecting a scene gizmo snapshot: {ex.Message}. Provider snapshot methods should return false rather than throwing when optional data is missing.",
                    provider.component,
                    null,
                    "SCENE_GIZMO_PROVIDER_EXCEPTION");
            }
        }

        private static void AddDensityIssues(
            PungentScanResult result,
            int activeCount,
            int enabledRuleCount,
            int providerCount,
            int trajectorySamples,
            int labelCount,
            int drawOps,
            int alwaysVisibleBeaconCount)
        {
            if (PungentSceneGizmoPerformancePolicy.ShouldShowWarning(activeCount, enabledRuleCount, providerCount, trajectorySamples, labelCount, drawOps))
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "High active scene gizmo count",
                    $"This scene has {activeCount} active PungentFunk gizmo sources/providers and approximately {drawOps} estimated draw operations. This can slow Scene View repainting. Consider hiding low-priority gizmos, disabling labels, limiting trajectory samples, or switching non-essential gizmos to Selected-only drawing.",
                    null,
                    null,
                    "SCENE_GIZMO_DENSITY");
            }

            if (labelCount >= PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "High active scene gizmo label count",
                    $"This scene estimates {labelCount} active gizmo labels. Labels are skipped before core shapes when the Scene View label budget is exceeded; disable non-essential labels or use Selected-only labels.",
                    null,
                    null,
                    "SCENE_GIZMO_LABEL_DENSITY");
            }

            if (trajectorySamples >= PungentSceneGizmoPerformancePolicy.WarningTrajectorySamples)
            {
                result.AddIssue(
                    PungentScanSeverity.Warning,
                    "High trajectory sample count",
                    $"Trajectory visualizers estimate {trajectorySamples} samples in this scene. Lower maxSamples, shorten duration, or disable trajectories that are not needed for current authoring.",
                    null,
                    null,
                    "SCENE_GIZMO_TRAJECTORY_DENSITY");
            }

            if (alwaysVisibleBeaconCount > 16)
            {
                result.AddIssue(
                    PungentScanSeverity.Info,
                    "Many always-visible beacons",
                    $"{alwaysVisibleBeaconCount} always-visible beacons are active. Consider switching navigation markers to Selected-only if they clutter Scene View work.",
                    null,
                    null,
                    "SCENE_GIZMO_ALWAYS_VISIBLE_BEACONS");
            }
        }
    }
#endif
}
