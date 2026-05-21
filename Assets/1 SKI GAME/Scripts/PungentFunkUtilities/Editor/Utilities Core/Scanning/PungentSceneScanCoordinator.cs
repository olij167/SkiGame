namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Stopwatch = System.Diagnostics.Stopwatch;

    public interface IPungentSceneScanProvider
    {
        bool TryCreateSceneScanBinding(PungentAuditScanMode mode, out PungentSceneScanProviderBinding binding);
    }

    public sealed class PungentSceneScanProviderBinding
    {
        public string ProviderId;
        public string DisplayName;
        public Func<PungentAuditScanMode, IPungentSceneScanAnalyzer> CreateAnalyzer;
    }

    public interface IPungentSceneScanAnalyzer
    {
        string ProviderId { get; }
        string DisplayName { get; }
        void Begin(PungentSceneScanBatchContext context);
        bool ShouldAnalyzeScene(PungentSceneScanTarget target);
        void RecordSkippedScene(PungentSceneScanTarget target, string reason);
        void AnalyzeScene(PungentSceneScanContext context);
        PungentScanResult Complete(PungentSceneScanBatchContext context);
        PungentScanResult Cancel(string status);
        PungentScanResult Fail(Exception exception, string status);
    }

    public sealed class PungentSceneScanTarget
    {
        public string Path;
        public string Name;
        public Scene LoadedScene;
        public bool IsLoadedScene;
        public bool IsEnabledBuildScene;
    }

    public sealed class PungentSceneScanBatchContext
    {
        public PungentAuditScanMode Mode;
        public IReadOnlyList<PungentSceneScanTarget> SceneTargets;
        public string ScopeLabel;
        public bool OpensScenes;
    }

    public sealed class PungentSceneScanContext
    {
        public PungentAuditScanMode Mode;
        public PungentSceneScanTarget Target;
        public Scene Scene;
        public int SceneIndex;
        public int SceneCount;
        public double SceneOpenDurationSeconds;
    }

    public sealed class PungentSceneScanIssueSink
    {
        private readonly PungentScanResult _result;

        public PungentSceneScanIssueSink(PungentScanResult result)
        {
            _result = result;
        }

        public PungentScanResult Result => _result;

        public void AddIssue(PungentScanSeverity severity, string title, string message, UnityEngine.Object context = null, string path = null, string code = null)
        {
            _result?.AddIssue(severity, title, message, context, path, code);
        }
    }

    public static class PungentSceneScanCoordinator
    {
        public const string ProviderId = "shared-scene-scan-pass";
        public const string DisplayName = "Shared Scene Scan Pass";

        public static PungentAuditScanJob CreateJob(IReadOnlyList<PungentSceneScanProviderBinding> bindings, PungentAuditScanMode mode)
        {
            PungentSceneScanCoordinatorService service = new PungentSceneScanCoordinatorService(bindings, mode);
            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative(ProviderId, DisplayName, service.Step);
            job.canPause = true;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.UsesAssetDatabase |
                               PungentAuditScanJobCapabilities.ScanOnly;
            if (mode != PungentAuditScanMode.BackgroundIdle)
                job.capabilities |= PungentAuditScanJobCapabilities.RequiresSceneOpening;
            if (mode == PungentAuditScanMode.BackgroundIdle)
                job.capabilities |= PungentAuditScanJobCapabilities.BackgroundSafe;
            job.Report(0f, 0, 1, "Queued shared scene scan pass.", false, "Queued");
            return job;
        }
    }

    internal sealed class PungentSceneScanCoordinatorService
    {
        private const string TerrainPrefPrefix = "GenericUtilities.TerrainUsageScanner.";

        private enum Stage
        {
            Begin,
            PrepareScenes,
            BeginAnalyzers,
            ScanScenes,
            CompleteAnalyzers,
            Restore,
            Done
        }

        private readonly List<PungentSceneScanProviderBinding> _bindings = new List<PungentSceneScanProviderBinding>();
        private readonly List<IPungentSceneScanAnalyzer> _analyzers = new List<IPungentSceneScanAnalyzer>();
        private readonly List<IPungentSceneScanAnalyzer> _activeAnalyzers = new List<IPungentSceneScanAnalyzer>();
        private readonly List<PungentSceneScanTarget> _scenes = new List<PungentSceneScanTarget>();
        private readonly PungentAuditScanMode _mode;
        private readonly PungentScanSession _session = new PungentScanSession(PungentSceneScanCoordinator.ProviderId, PungentSceneScanCoordinator.DisplayName);
        private readonly Stopwatch _openStopwatch = new Stopwatch();
        private PungentScanResult _result;
        private SceneSetup[] _originalSetup;
        private Stage _stage = Stage.Begin;
        private Scene _activeScene;
        private PungentSceneScanTarget _activeTarget;
        private bool _restoreSceneSetup;
        private bool _sceneSetupDisplaced;
        private bool _sceneOpeningAnnounced;
        private bool _sceneReadyForAnalyzers;
        private bool _resumeMessagePending;
        private int _sceneIndex;
        private int _openedSceneCount;
        private int _skippedSceneCount;
        private double _activeSceneOpenDurationSeconds;
        private bool _scanScenesProjectWide = true;
        private string _sceneFolderPath = "Assets";
        private bool _skipPackageScenes = true;
        private bool _includeSceneSubfolders = true;
        private string _scopeLabel = "Project scenes";

        public PungentSceneScanCoordinatorService(IReadOnlyList<PungentSceneScanProviderBinding> bindings, PungentAuditScanMode mode)
        {
            _mode = mode;
            if (bindings != null)
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i] != null && bindings[i].CreateAnalyzer != null)
                        _bindings.Add(bindings[i]);
                }
            }
            LoadSceneScopeFromTerrainSettings();
        }

        public PungentAuditScanStepResult Step(PungentAuditScanContext context)
        {
            if (context.IsCancellationRequested())
                return Cancel("Shared scene scan interrupted before the next scene checkpoint.");
            if (context.IsPauseRequested())
                return PauseAtCheckpoint(context);

            try
            {
                switch (_stage)
                {
                    case Stage.Begin:
                        _result = _session.Begin(GetCoordinatorScope(), GetCoordinatorScopeLabel());
                        CreateAnalyzers();
                        _stage = Stage.PrepareScenes;
                        context.Report(0.03f, 0, 1, "Preparing shared scene scan pass.", false, "Prepare");
                        return PungentAuditScanStepResult.Continue("Preparing shared scene scan pass.");

                    case Stage.PrepareScenes:
                        if (_mode == PungentAuditScanMode.BackgroundIdle)
                            GatherLoadedScenes();
                        else
                        {
                            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                return Cancel("Shared scene scan interrupted before scene changes were saved.");
                            _originalSetup = EditorSceneManager.GetSceneManagerSetup();
                            _restoreSceneSetup = true;
                            GatherSceneAssets();
                        }

                        _stage = Stage.BeginAnalyzers;
                        context.Report(0.10f, 0, Math.Max(1, _scenes.Count), "Gathered " + _scenes.Count + " scene(s) for shared scene scan.", false, "Gather scenes");
                        return PungentAuditScanStepResult.Continue("Gathered " + _scenes.Count + " scene(s) for shared scene scan.");

                    case Stage.BeginAnalyzers:
                        BeginAnalyzers();
                        _stage = Stage.ScanScenes;
                        context.Report(0.15f, 0, Math.Max(1, _scenes.Count), "Scene analyzers prepared.", false, "Prepare analyzers");
                        return PungentAuditScanStepResult.Continue("Scene analyzers prepared.");

                    case Stage.ScanScenes:
                        if (_resumeMessagePending)
                        {
                            _resumeMessagePending = false;
                            string resumeStatus = "Resuming shared scene scan pass...";
                            context.Report(GetSceneProgress(), _sceneIndex, Math.Max(1, _scenes.Count), resumeStatus, false, "Resume");
                            return PungentAuditScanStepResult.Continue(resumeStatus);
                        }

                        if (_sceneIndex < _scenes.Count)
                            return StepScene(context);

                        _stage = Stage.CompleteAnalyzers;
                        return PungentAuditScanStepResult.Continue("Publishing scene analyzer results.");

                    case Stage.CompleteAnalyzers:
                        CompleteAnalyzers();
                        _stage = Stage.Restore;
                        context.Report(0.94f, _sceneIndex, Math.Max(1, _scenes.Count), "Restoring original scene setup...", false, "Restore scenes");
                        return PungentAuditScanStepResult.Continue("Restoring original scene setup...");

                    case Stage.Restore:
                        RestoreSceneSetup();
                        string status = "Shared scene audit completed: " + _sceneIndex + " scene(s) processed, " + _openedSceneCount + " opened, " + _skippedSceneCount + " skipped.";
                        PungentScanResult completed = _session.Complete(_sceneIndex, _analyzers.Count, _skippedSceneCount, 0, status);
                        _stage = Stage.Done;
                        context.Report(1f, _sceneIndex, Math.Max(1, _scenes.Count), status, false, "Complete");
                        return PungentAuditScanStepResult.Complete(status, completed);

                    default:
                        return PungentAuditScanStepResult.Complete("Shared scene scan already completed.", _result);
                }
            }
            catch (Exception ex)
            {
                RestoreSceneSetup();
                FailAnalyzers(ex, "Shared scene scan failed: " + ex.Message);
                UnityEngine.Debug.LogException(ex);
                PungentScanResult failed = _session.Fail(ex, "Shared scene scan failed: " + ex.Message);
                _stage = Stage.Done;
                return PungentAuditScanStepResult.Failed("Shared scene scan failed: " + ex.Message, failed);
            }
        }

        private PungentAuditScanStepResult StepScene(PungentAuditScanContext context)
        {
            PungentSceneScanTarget target = _scenes[_sceneIndex];
            BuildActiveAnalyzers(target);
            int sceneNumber = _sceneIndex + 1;

            if (_activeAnalyzers.Count == 0)
            {
                _skippedSceneCount++;
                _sceneIndex++;
                string skippedStatus = "Skipped scene " + _sceneIndex + " / " + _scenes.Count + ": " + target.Name;
                context.Report(GetSceneProgress(), _sceneIndex, _scenes.Count, skippedStatus, false, "Skip scene");
                return PungentAuditScanStepResult.Continue(skippedStatus);
            }

            if (!_sceneOpeningAnnounced && _mode != PungentAuditScanMode.BackgroundIdle)
            {
                _sceneOpeningAnnounced = true;
                string openingStatus = "Opening scene " + sceneNumber + " / " + _scenes.Count + ": " + target.Name;
                context.Report(GetSceneProgress(), _sceneIndex, _scenes.Count, openingStatus, false, "Open scene");
                return PungentAuditScanStepResult.Continue(openingStatus);
            }

            if (!_sceneReadyForAnalyzers)
            {
                _activeTarget = target;
                _activeScene = OpenSceneTarget(target);
                _sceneReadyForAnalyzers = true;
                string runningStatus = "Running scene analyzers for " + sceneNumber + " / " + _scenes.Count + ": " + target.Name;
                context.Report(GetSceneProgress(), _sceneIndex, _scenes.Count, runningStatus, false, "Analyze scene");
                return PungentAuditScanStepResult.Continue(runningStatus);
            }

            PungentSceneScanContext sceneContext = new PungentSceneScanContext
            {
                Mode = _mode,
                Target = _activeTarget,
                Scene = _activeScene,
                SceneIndex = _sceneIndex,
                SceneCount = _scenes.Count,
                SceneOpenDurationSeconds = _activeSceneOpenDurationSeconds
            };

            for (int i = 0; i < _activeAnalyzers.Count; i++)
                _activeAnalyzers[i].AnalyzeScene(sceneContext);

            _sceneIndex++;
            string status = "Scanned scene " + _sceneIndex + " / " + _scenes.Count + ": " + target.Name;
            ResetActiveScene();
            context.Report(GetSceneProgress(), _sceneIndex, _scenes.Count, status, false, "Analyze scenes");
            return PungentAuditScanStepResult.Continue(status);
        }

        private void CreateAnalyzers()
        {
            _analyzers.Clear();
            for (int i = 0; i < _bindings.Count; i++)
            {
                IPungentSceneScanAnalyzer analyzer = _bindings[i].CreateAnalyzer(_mode);
                if (analyzer != null)
                    _analyzers.Add(analyzer);
            }
        }

        private void BeginAnalyzers()
        {
            PungentSceneScanBatchContext batchContext = new PungentSceneScanBatchContext
            {
                Mode = _mode,
                SceneTargets = _scenes,
                ScopeLabel = GetCoordinatorScopeLabel(),
                OpensScenes = _mode != PungentAuditScanMode.BackgroundIdle
            };
            for (int i = 0; i < _analyzers.Count; i++)
                _analyzers[i].Begin(batchContext);
        }

        private void BuildActiveAnalyzers(PungentSceneScanTarget target)
        {
            _activeAnalyzers.Clear();
            for (int i = 0; i < _analyzers.Count; i++)
            {
                IPungentSceneScanAnalyzer analyzer = _analyzers[i];
                if (analyzer.ShouldAnalyzeScene(target))
                    _activeAnalyzers.Add(analyzer);
                else
                    analyzer.RecordSkippedScene(target, "Analyzer skipped this scene before opening.");
            }
        }

        private void CompleteAnalyzers()
        {
            PungentSceneScanBatchContext batchContext = new PungentSceneScanBatchContext
            {
                Mode = _mode,
                SceneTargets = _scenes,
                ScopeLabel = GetCoordinatorScopeLabel(),
                OpensScenes = _mode != PungentAuditScanMode.BackgroundIdle
            };
            for (int i = 0; i < _analyzers.Count; i++)
                _analyzers[i].Complete(batchContext);
        }

        private void FailAnalyzers(Exception ex, string status)
        {
            for (int i = 0; i < _analyzers.Count; i++)
                _analyzers[i].Fail(ex, status);
        }

        private PungentAuditScanStepResult Cancel(string status)
        {
            RestoreSceneSetup();
            for (int i = 0; i < _analyzers.Count; i++)
                _analyzers[i].Cancel(status);
            _session.Cancel(status, false);
            _stage = Stage.Done;
            return PungentAuditScanStepResult.Cancelled(status);
        }

        private PungentAuditScanStepResult PauseAtCheckpoint(PungentAuditScanContext context)
        {
            RestoreSceneSetup(releaseSnapshot: false);
            ResetActiveScene();
            _resumeMessagePending = true;
            string status = BuildPauseStatus();
            context.Report(GetSceneProgress(), _sceneIndex, Math.Max(1, _scenes.Count), status, false, "Paused");
            return PungentAuditScanStepResult.Continue(status);
        }

        private string BuildPauseStatus()
        {
            if (_scenes.Count == 0)
                return "Shared scene scan paused. Original scene setup restored. Progress preserved.";
            int nextScene = Mathf.Min(_sceneIndex + 1, _scenes.Count);
            return "Paused after scene " + _sceneIndex + " / " + _scenes.Count + ". Original scene setup restored. Resume will continue at scene " + nextScene + " / " + _scenes.Count + ".";
        }

        private Scene OpenSceneTarget(PungentSceneScanTarget target)
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return target.LoadedScene;

            _openStopwatch.Reset();
            _openStopwatch.Start();
            _sceneSetupDisplaced = true;
            Scene scene = EditorSceneManager.OpenScene(target.Path, OpenSceneMode.Single);
            _openStopwatch.Stop();
            _activeSceneOpenDurationSeconds = _openStopwatch.Elapsed.TotalSeconds;
            _openedSceneCount++;
            return scene;
        }

        private void RestoreSceneSetup(bool releaseSnapshot = true)
        {
            if (_restoreSceneSetup && _sceneSetupDisplaced && _originalSetup != null)
                EditorSceneManager.RestoreSceneManagerSetup(_originalSetup);

            _sceneSetupDisplaced = false;
            if (releaseSnapshot)
            {
                _restoreSceneSetup = false;
                _originalSetup = null;
            }
        }

        private void ResetActiveScene()
        {
            _activeTarget = null;
            _activeScene = default(Scene);
            _activeSceneOpenDurationSeconds = 0d;
            _activeAnalyzers.Clear();
            _sceneOpeningAnnounced = false;
            _sceneReadyForAnalyzers = false;
        }

        private void GatherLoadedScenes()
        {
            _scenes.Clear();
            HashSet<string> enabledBuildScenes = GetEnabledBuildScenePaths();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                if (_skipPackageScenes && IsPackagePath(scene.path))
                    continue;
                string scenePath = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
                _scenes.Add(new PungentSceneScanTarget
                {
                    Path = scenePath,
                    Name = scene.name,
                    LoadedScene = scene,
                    IsLoadedScene = true,
                    IsEnabledBuildScene = enabledBuildScenes.Contains(NormalizeSlashes(scenePath))
                });
            }
            _scopeLabel = "Open scenes";
        }

        private void GatherSceneAssets()
        {
            _scenes.Clear();
            HashSet<string> enabledBuildScenes = GetEnabledBuildScenePaths();
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            IEnumerable<string> scenePaths = sceneGuids.Select(AssetDatabase.GUIDToAssetPath).Where(path => !string.IsNullOrEmpty(path));
            if (_skipPackageScenes)
                scenePaths = scenePaths.Where(path => !IsPackagePath(path));
            if (!_scanScenesProjectWide)
                scenePaths = scenePaths.Where(path => IsPathWithinFolderScope(path, _sceneFolderPath, _includeSceneSubfolders));
            foreach (string path in scenePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                _scenes.Add(new PungentSceneScanTarget
                {
                    Path = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path),
                    IsLoadedScene = false,
                    IsEnabledBuildScene = enabledBuildScenes.Contains(NormalizeSlashes(path))
                });
            }
            _scopeLabel = _scanScenesProjectWide ? (_skipPackageScenes ? "Project scenes" : "Project + package scenes") : GetScopeDescription(_sceneFolderPath, _includeSceneSubfolders);
        }

        private void LoadSceneScopeFromTerrainSettings()
        {
            _scanScenesProjectWide = UtilityWindowPrefs.GetBool(TerrainPrefPrefix + "ScanScenesProjectWide", _scanScenesProjectWide);
            _sceneFolderPath = NormalizeFolderPath(UtilityWindowPrefs.GetString(TerrainPrefPrefix + "SceneFolderPath", _sceneFolderPath), "Assets");
            _skipPackageScenes = UtilityWindowPrefs.GetBool(TerrainPrefPrefix + "SkipPackageScenes", _skipPackageScenes);
            _includeSceneSubfolders = UtilityWindowPrefs.GetBool(TerrainPrefPrefix + "IncludeSceneSubfolders", _includeSceneSubfolders);
        }

        private PungentScanScope GetCoordinatorScope()
        {
            if (_mode == PungentAuditScanMode.BackgroundIdle)
                return PungentScanScope.OpenScenes;
            return _scanScenesProjectWide ? PungentScanScope.ProjectScenes : PungentScanScope.Custom;
        }

        private string GetCoordinatorScopeLabel()
        {
            return _mode == PungentAuditScanMode.BackgroundIdle ? "Open scenes" : _scopeLabel;
        }

        private float GetSceneProgress()
        {
            if (_scenes.Count == 0)
                return 0.15f;
            return 0.15f + (0.76f * _sceneIndex / Mathf.Max(1, _scenes.Count));
        }

        private static bool IsPackagePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithinFolderScope(string path, string folderPath, bool includeSubfolders)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string normalizedPath = NormalizeSlashes(path);
            string normalizedFolder = NormalizeFolderPath(folderPath, "Assets");
            if (includeSubfolders)
                return normalizedPath.StartsWith(normalizedFolder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalizedPath, normalizedFolder, StringComparison.OrdinalIgnoreCase);
            return string.Equals(System.IO.Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/'), normalizedFolder.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFolderPath(string path, string fallbackPath)
        {
            string normalized = NormalizeSlashes(path);
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = fallbackPath;
            return normalized.TrimEnd('/');
        }

        private static string NormalizeSlashes(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static string GetScopeDescription(string folderPath, bool includeSubfolders)
        {
            return NormalizeFolderPath(folderPath, "Assets") + (includeSubfolders ? " + subfolders" : " only");
        }

        private static HashSet<string> GetEnabledBuildScenePaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                if (scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                    paths.Add(NormalizeSlashes(scene.path));
            }
            return paths;
        }
    }
#endif
}
