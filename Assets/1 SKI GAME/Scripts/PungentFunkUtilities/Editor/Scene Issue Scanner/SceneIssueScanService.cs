namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using PungentFunk.Utilities.Editor.Scanning;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    internal static class SceneIssueScanService
    {
        public const string ProviderId = "scene-issue-scanner";
        public const string DisplayName = "Scene Issue Scanner";

        public static PungentScanResult ScanOpenScenes(SceneIssueScanSettings settings, out SceneIssueScanSummary summary)
        {
            SceneIssueScanSettings activeSettings = settings != null ? settings.Clone() : new SceneIssueScanSettings();
            activeSettings.Scope = SceneIssueScanScope.OpenScenesOnly;
            summary = new SceneIssueScanSummary();

            Stopwatch stopwatch = Stopwatch.StartNew();
            PungentScanSession session = new PungentScanSession(ProviderId, DisplayName);
            PungentScanResult result = session.Begin(PungentScanScope.OpenScenes, "Open Scenes Only");
            result.StatusMessage = "Scanning currently open scenes for non-destructive scene issues.";

            try
            {
                HashSet<string> enabledBuildScenes = GetEnabledBuildScenes();
                summary.BuildScenesIncluded = enabledBuildScenes.Count;

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    summary.ScenesDiscovered++;
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        summary.ScenesSkipped++;
                        continue;
                    }

                    string scenePath = NormalizePath(scene.path);
                    if (!activeSettings.IncludePackageScenes && IsPackageScene(scenePath))
                    {
                        summary.ScenesSkipped++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(scenePath) && !enabledBuildScenes.Contains(scenePath))
                        summary.BuildScenesExcluded++;

                    summary.ScenesScanned++;
                    ScanLoadedScene(scene, scenePath, activeSettings, result, summary);
                }

                stopwatch.Stop();
                summary.Duration = stopwatch.Elapsed;
                summary.IssuesFound = result.Issues.Count;
                summary.StatusMessage = BuildStatus(summary);
                return session.Complete(summary.ScenesScanned, summary.IssuesFound, summary.ScenesSkipped, 0, summary.StatusMessage);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                summary.Duration = stopwatch.Elapsed;
                summary.StatusMessage = "Scene Issue Scanner failed: " + ex.Message;
                return session.Fail(ex, summary.StatusMessage);
            }
        }

        internal static void ScanLoadedScene(Scene scene, string scenePath, SceneIssueScanSettings settings, PungentScanResult result, SceneIssueScanSummary summary)
        {
            List<GameObject> objects = CollectSceneObjects(scene, settings.IncludeInactiveObjects);
            summary.ObjectsScanned += objects.Count;

            List<AudioListener> activeAudioListeners = new List<AudioListener>();
            List<Component> activeEventSystems = new List<Component>();
            List<Camera> activeMainCameras = new List<Camera>();
            int activeCameraCount = 0;
            int activeLightCount = 0;

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject gameObject = objects[i];
                if (gameObject == null)
                    continue;

                string hierarchyPath = BuildHierarchyPath(gameObject.transform);
                ScanObject(scene, scenePath, hierarchyPath, gameObject, settings, result, summary);
                CollectSceneServiceCandidates(gameObject, activeAudioListeners, activeEventSystems, activeMainCameras, ref activeCameraCount, ref activeLightCount);
            }

            AddDuplicateServiceIssues(scene, scenePath, activeAudioListeners, activeEventSystems, activeMainCameras, result, summary);
            if (settings.IncludeAdvisoryFindings)
                AddAdvisoryIssues(scene, scenePath, activeCameraCount, activeLightCount, settings, result, summary);
        }

        private static void ScanObject(Scene scene, string scenePath, string hierarchyPath, GameObject gameObject, SceneIssueScanSettings settings, PungentScanResult result, SceneIssueScanSummary summary)
        {
            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missingScriptCount > 0)
            {
                AddIssue(result, summary, new SceneIssueRecord
                {
                    Severity = SceneIssueSeverity.Error,
                    Category = SceneIssueCategory.MissingScript,
                    ScenePath = scenePath,
                    SceneName = scene.name,
                    HierarchyPath = hierarchyPath,
                    Context = gameObject,
                    Title = "Missing script on scene object",
                    Message = scene.name + " contains " + missingScriptCount + " missing script component(s) on " + hierarchyPath + ".",
                    Code = "SCENE_MISSING_SCRIPT"
                });
            }

            Renderer[] renderers = gameObject.GetComponents<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
                ScanRenderer(scene, scenePath, hierarchyPath, renderers[i], result, summary);

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == null)
                AddIssue(result, summary, MissingAssetRecord(SceneIssueCategory.MissingMeshOrSprite, SceneIssueSeverity.Warning, scene, scenePath, hierarchyPath, meshFilter, "MeshFilter is missing a mesh", "MeshFilter on " + hierarchyPath + " has no shared mesh.", "SCENE_MISSING_MESH"));

            SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh == null)
                AddIssue(result, summary, MissingAssetRecord(SceneIssueCategory.MissingMeshOrSprite, SceneIssueSeverity.Warning, scene, scenePath, hierarchyPath, skinnedMeshRenderer, "SkinnedMeshRenderer is missing a mesh", "SkinnedMeshRenderer on " + hierarchyPath + " has no shared mesh.", "SCENE_MISSING_SKINNED_MESH"));

            if (settings.IncludeAdvisoryFindings)
            {
                SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite == null)
                    AddIssue(result, summary, MissingAssetRecord(SceneIssueCategory.MissingMeshOrSprite, SceneIssueSeverity.Warning, scene, scenePath, hierarchyPath, spriteRenderer, "SpriteRenderer is missing a sprite", "SpriteRenderer on " + hierarchyPath + " has no sprite assigned. This may be intentional for pooled or configured-at-runtime objects.", "SCENE_MISSING_SPRITE"));
            }
        }

        private static void ScanRenderer(Scene scene, string scenePath, string hierarchyPath, Renderer renderer, PungentScanResult result, SceneIssueScanSummary summary)
        {
            if (renderer == null)
                return;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    AddIssue(result, summary, MissingAssetRecord(SceneIssueCategory.BrokenMaterial, SceneIssueSeverity.Warning, scene, scenePath, hierarchyPath, renderer, "Renderer has an empty material slot", renderer.GetType().Name + " on " + hierarchyPath + " has an empty material slot at index " + i + ".", "SCENE_MISSING_MATERIAL_SLOT"));
                    continue;
                }

                if (material.shader == null || string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal))
                    AddIssue(result, summary, MissingAssetRecord(SceneIssueCategory.BrokenMaterial, SceneIssueSeverity.Error, scene, scenePath, hierarchyPath, material, "Material has a missing shader", material.name + " is assigned to " + hierarchyPath + " but has a missing/error shader.", "SCENE_MISSING_SHADER"));
            }
        }

        private static SceneIssueRecord MissingAssetRecord(SceneIssueCategory category, SceneIssueSeverity severity, Scene scene, string scenePath, string hierarchyPath, UnityEngine.Object context, string title, string message, string code)
        {
            return new SceneIssueRecord
            {
                Severity = severity,
                Category = category,
                ScenePath = scenePath,
                SceneName = scene.name,
                HierarchyPath = hierarchyPath,
                Context = context,
                Title = title,
                Message = scene.name + ": " + message,
                Code = code
            };
        }

        private static void CollectSceneServiceCandidates(GameObject gameObject, List<AudioListener> audioListeners, List<Component> eventSystems, List<Camera> mainCameras, ref int cameraCount, ref int lightCount)
        {
            AudioListener[] listeners = gameObject.GetComponents<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                if (IsActiveBehaviour(listeners[i]))
                    audioListeners.Add(listeners[i]);
            }

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;
                Type type = component.GetType();
                if (type != null && string.Equals(type.FullName, "UnityEngine.EventSystems.EventSystem", StringComparison.Ordinal) && component is Behaviour behaviour && IsActiveBehaviour(behaviour))
                    eventSystems.Add(component);
            }

            Camera[] cameras = gameObject.GetComponents<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (!IsActiveBehaviour(camera))
                    continue;
                cameraCount++;
                if (camera.gameObject.CompareTag("MainCamera"))
                    mainCameras.Add(camera);
            }

            Light[] lights = gameObject.GetComponents<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (IsActiveBehaviour(lights[i]))
                    lightCount++;
            }
        }

        private static void AddDuplicateServiceIssues(Scene scene, string scenePath, List<AudioListener> audioListeners, List<Component> eventSystems, List<Camera> mainCameras, PungentScanResult result, SceneIssueScanSummary summary)
        {
            if (audioListeners.Count > 1)
                AddCollectionIssue(scene, scenePath, audioListeners[0], "Multiple active AudioListeners", "Scene has " + audioListeners.Count + " active AudioListeners. Unity expects one listener for normal runtime audio.", "SCENE_DUPLICATE_AUDIO_LISTENER", result, summary);
            if (eventSystems.Count > 1)
                AddCollectionIssue(scene, scenePath, eventSystems[0], "Multiple active EventSystems", "Scene has " + eventSystems.Count + " active EventSystems. Multiple systems can duplicate UI input processing.", "SCENE_DUPLICATE_EVENT_SYSTEM", result, summary);
            if (mainCameras.Count > 1)
                AddCollectionIssue(scene, scenePath, mainCameras[0], "Multiple active MainCamera objects", "Scene has " + mainCameras.Count + " active cameras tagged MainCamera.", "SCENE_DUPLICATE_MAIN_CAMERA", result, summary);
        }

        private static void AddCollectionIssue(Scene scene, string scenePath, Component context, string title, string message, string code, PungentScanResult result, SceneIssueScanSummary summary)
        {
            AddIssue(result, summary, new SceneIssueRecord
            {
                Severity = SceneIssueSeverity.Warning,
                Category = SceneIssueCategory.DuplicateSceneService,
                ScenePath = scenePath,
                SceneName = scene.name,
                HierarchyPath = context != null ? BuildHierarchyPath(context.transform) : string.Empty,
                Context = context,
                Title = title,
                Message = scene.name + ": " + message,
                Code = code
            });
        }

        private static void AddAdvisoryIssues(Scene scene, string scenePath, int activeCameraCount, int activeLightCount, SceneIssueScanSettings settings, PungentScanResult result, SceneIssueScanSummary summary)
        {
            if (activeCameraCount > settings.ExcessiveCameraThreshold)
                AddSceneAdvisory(scene, scenePath, "Many active cameras", "Scene has " + activeCameraCount + " active cameras. Review whether all cameras need to render at runtime.", "SCENE_MANY_CAMERAS", result, summary);
            if (activeLightCount > settings.ExcessiveRealtimeLightThreshold)
                AddSceneAdvisory(scene, scenePath, "Many active lights", "Scene has " + activeLightCount + " active lights. This is an advisory threshold, not proof of a performance bug.", "SCENE_MANY_LIGHTS", result, summary);
        }

        private static void AddSceneAdvisory(Scene scene, string scenePath, string title, string message, string code, PungentScanResult result, SceneIssueScanSummary summary)
        {
            AddIssue(result, summary, new SceneIssueRecord
            {
                Severity = SceneIssueSeverity.Info,
                Category = SceneIssueCategory.AdvisoryPerformance,
                ScenePath = scenePath,
                SceneName = scene.name,
                HierarchyPath = string.Empty,
                Context = null,
                Title = title,
                Message = scene.name + ": " + message,
                Code = code
            });
        }

        private static void AddIssue(PungentScanResult result, SceneIssueScanSummary summary, SceneIssueRecord record)
        {
            if (result == null || record == null)
                return;
            result.AddIssue(record.ToScanIssue());
            summary.IssuesFound++;
        }

        private static List<GameObject> CollectSceneObjects(Scene scene, bool includeInactive)
        {
            List<GameObject> objects = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;
                Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] != null)
                        objects.Add(transforms[t].gameObject);
                }
            }
            return objects;
        }

        private static bool IsActiveBehaviour(Behaviour behaviour)
        {
            return behaviour != null && behaviour.enabled && behaviour.gameObject != null && behaviour.gameObject.activeInHierarchy;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static HashSet<string> GetEnabledBuildScenes()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                if (scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                    paths.Add(NormalizePath(scene.path));
            }
            return paths;
        }

        private static bool IsPackageScene(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static string BuildStatus(SceneIssueScanSummary summary)
        {
            if (summary.ScenesScanned == 0)
                return "No loaded scenes were scanned. Open a scene, then run Scan Open Scenes.";
            if (summary.IssuesFound == 0)
                return "Open scene scan complete. No scene issues were found in " + summary.ScenesScanned + " loaded scene(s).";
            return "Open scene scan complete. Found " + summary.IssuesFound + " issue(s) across " + summary.ScenesScanned + " loaded scene(s).";
        }
    }

    internal sealed class SceneIssueSceneScanAnalyzer : IPungentSceneScanAnalyzer
    {
        private readonly SceneIssueScanSettings _settings = new SceneIssueScanSettings();
        private readonly SceneIssueScanSummary _summary = new SceneIssueScanSummary();
        private readonly PungentScanSession _session = new PungentScanSession(SceneIssueScanService.ProviderId, SceneIssueScanService.DisplayName);
        private PungentScanResult _result;

        public string ProviderId => SceneIssueScanService.ProviderId;
        public string DisplayName => SceneIssueScanService.DisplayName;

        public void Begin(PungentSceneScanBatchContext context)
        {
            _settings.Scope = context != null && context.Mode == PungentAuditScanMode.BackgroundIdle
                ? SceneIssueScanScope.OpenScenesOnly
                : SceneIssueScanScope.AllProjectScenes;
            _settings.IncludeInactiveObjects = true;
            _settings.IncludeAdvisoryFindings = false;
            _settings.IncludePackageScenes = false;
            _result = _session.Begin(context != null && context.Mode == PungentAuditScanMode.BackgroundIdle ? PungentScanScope.OpenScenes : PungentScanScope.ProjectScenes, context != null ? context.ScopeLabel : "Shared Scene Pass");
            _result.StatusMessage = "Scene Issue Scanner is running through the shared scene scan pass.";
            _summary.ScenesDiscovered = context != null && context.SceneTargets != null ? context.SceneTargets.Count : 0;
        }

        public bool ShouldAnalyzeScene(PungentSceneScanTarget target)
        {
            return target != null && !string.IsNullOrWhiteSpace(target.Path);
        }

        public void RecordSkippedScene(PungentSceneScanTarget target, string reason)
        {
            _summary.ScenesSkipped++;
        }

        public void AnalyzeScene(PungentSceneScanContext context)
        {
            if (context == null || !context.Scene.IsValid() || !context.Scene.isLoaded)
            {
                _summary.ScenesSkipped++;
                return;
            }

            _summary.ScenesScanned++;
            string scenePath = context.Target != null ? context.Target.Path : context.Scene.path;
            if (context.Target != null && context.Target.IsEnabledBuildScene)
                _summary.BuildScenesIncluded++;
            else if (!string.IsNullOrWhiteSpace(scenePath))
                _summary.BuildScenesExcluded++;
            SceneIssueScanService.ScanLoadedScene(context.Scene, scenePath, _settings, _result, _summary);
        }

        public PungentScanResult Complete(PungentSceneScanBatchContext context)
        {
            _summary.IssuesFound = _result != null ? _result.Issues.Count : 0;
            _summary.StatusMessage = _summary.IssuesFound == 0
                ? "Shared scene scan complete. No scene issues were found in " + _summary.ScenesScanned + " scene(s)."
                : "Shared scene scan complete. Found " + _summary.IssuesFound + " scene issue(s) across " + _summary.ScenesScanned + " scene(s).";
            return _session.Complete(_summary.ScenesScanned, _summary.IssuesFound, _summary.ScenesSkipped, 0, _summary.StatusMessage);
        }

        public PungentScanResult Cancel(string status)
        {
            return _session.Cancel(status, false);
        }

        public PungentScanResult Fail(Exception exception, string status)
        {
            return _session.Fail(exception, status);
        }
    }
#endif
}
