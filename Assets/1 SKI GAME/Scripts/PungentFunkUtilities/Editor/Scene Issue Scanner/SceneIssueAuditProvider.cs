using PungentFunk.Utilities.Editor.Scanning;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    internal sealed class SceneIssueAuditProvider : IPungentAuditScanProvider, IPungentSceneScanProvider
    {
        public string ProviderId => SceneIssueScanService.ProviderId;
        public string DisplayName => SceneIssueScanService.DisplayName;
        public string Description => "Scans scenes for missing scripts, broken renderer assets, duplicate scene services, and optional advisory thresholds. Full-project audits run through the shared scene scan pass.";
        public string OpenButtonLabel => "Open Scene Scanner";
        public string RunButtonLabel => "Run Scene Scan";
        public bool CanRunImmediate => true;
        public bool CanRunBackground => false;
        public bool CanRunFromCoordinator => true;
        public bool CanPause => true;
        public bool CanCancel => true;
        public bool UsesSceneOpening => true;
        public bool UsesAssetDatabase => true;
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
            return new PungentAuditScanJob(ProviderId, DisplayName, job =>
            {
                string message = "Scene Issue Scanner runs through the shared scene scan pass during Design Validation Audit.";
                job.Report(1f, 0, 0, message, false, "Shared scene pass");
                return PungentAuditScanStepResult.Skipped(message);
            })
            {
                scanMode = mode,
                canPause = false,
                canCancel = false,
                capabilities = PungentAuditScanJobCapabilities.CacheOnlyOrManualOnly | PungentAuditScanJobCapabilities.ScanOnly
            };
        }

        public bool TryCreateSceneScanBinding(PungentAuditScanMode mode, out PungentSceneScanProviderBinding binding)
        {
            binding = null;
            if (mode == PungentAuditScanMode.BackgroundIdle)
                return false;

            binding = new PungentSceneScanProviderBinding
            {
                ProviderId = ProviderId,
                DisplayName = DisplayName,
                CreateAnalyzer = scanMode => new SceneIssueSceneScanAnalyzer()
            };
            return true;
        }

        public void OpenWindow()
        {
            SceneIssueScannerWindow.Open();
        }
    }
#endif
}
