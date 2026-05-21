using PungentFunk.Utilities.Audio;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;

namespace PungentFunk.Utilities.Editor.Audio
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal static class AudioCoverageContextScanner
    {
        public static List<AudioCoverageContextResult> Run(AudioCoverageProfileSO profile, AudioCoverageContextScanOptions options)
        {
            List<AudioCoverageContextResult> results = new List<AudioCoverageContextResult>();
            if (profile == null)
            {
                AddResult(results, AudioCoverageResultSeverity.Warning, "Profile/setup issue", "No AudioCoverageProfileSO is assigned.", null, null);
                return results;
            }

            if (options.IncludeSceneReferences)
                ScanSceneReferenceCoverage(profile, options, results);
            if (options.IncludeTerrainProfiles)
                ScanTerrainProfiles(profile, results);
            if (options.IncludeInteractionMatrices)
                ScanInteractionMatrices(profile, results);
            if (options.IncludeComponentExpectations)
                ScanComponentExpectations(profile, options, results);
            if (options.IncludeRequiredFields)
                ScanRequiredFields(profile, results);

            if (results.Count == 0)
                AddResult(results, AudioCoverageResultSeverity.Info, "Complete", "No obvious contextual audio setup gaps were found in this pass.", profile, AssetDatabase.GetAssetPath(profile));

            return results;
        }

        public static PungentAuditScanJob CreateAuditJob(AudioCoverageProfileSO profile, AudioCoverageContextScanOptions options, PungentAuditScanMode mode)
        {
            CooperativeContextAudit audit = new CooperativeContextAudit(profile, options);
            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative("audio-setup-coverage", "Audio Setup Coverage", audit.Step);
            job.canPause = true;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.BackgroundSafe |
                               PungentAuditScanJobCapabilities.UsesAssetDatabase |
                               PungentAuditScanJobCapabilities.ScanOnly;
            job.Report(0f, 0, 5, "Queued audio setup coverage audit.", false, "Queued");
            return job;
        }

        public static void AddResultsToScan(PungentScanResult scan, IReadOnlyList<AudioCoverageContextResult> results)
        {
            if (scan == null || results == null)
                return;

            for (int i = 0; i < results.Count; i++)
            {
                AudioCoverageContextResult result = results[i];
                if (result == null)
                    continue;

                scan.AddIssue(ToScanSeverity(result.Severity), result.Category, result.Message, result.Context, result.Path, "AUDIO_SETUP_" + SanitizeCode(result.Category));
            }
        }

        public static PungentScanSeverity ToScanSeverity(AudioCoverageResultSeverity severity)
        {
            switch (severity)
            {
                case AudioCoverageResultSeverity.Error:
                    return PungentScanSeverity.Error;
                case AudioCoverageResultSeverity.Warning:
                    return PungentScanSeverity.Warning;
                default:
                    return PungentScanSeverity.Info;
            }
        }

        private static void ScanSceneReferenceCoverage(AudioCoverageProfileSO profile, AudioCoverageContextScanOptions options, List<AudioCoverageContextResult> results)
        {
            Type componentType = FindType(profile.sceneReferenceComponentTypeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                AddResult(results, AudioCoverageResultSeverity.Warning, "Profile/setup issue", $"Scene reference component type '{profile.sceneReferenceComponentTypeName}' was not found or is not a Component.", profile, AssetDatabase.GetAssetPath(profile));
                return;
            }

            Collider[] colliders = options.SelectionOnly ? GetSelectedColliders() : FindSceneColliders();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                if (profile.sceneReferenceSkipTriggers && collider.isTrigger)
                    continue;

                if (profile.sceneReferenceSkipTerrains && IsTerrainCollider(collider))
                    continue;

                Component component = FindComponentInParents(collider.transform, componentType);
                if (component == null)
                {
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Missing setup reference", $"Collider is missing {profile.sceneReferenceComponentTypeName} on itself or a parent: {GetHierarchyPath(collider.transform)}", collider, null);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(profile.sceneReferenceFieldName) && !IsSerializedObjectReferenceAssigned(component, profile.sceneReferenceFieldName, out string fieldMessage))
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Missing setup reference", fieldMessage, component, null);
            }
        }

        private static void ScanTerrainProfiles(AudioCoverageProfileSO profile, List<AudioCoverageContextResult> results)
        {
            Type profileType = FindType(profile.terrainProfileTypeName);
            if (profileType == null || !typeof(Object).IsAssignableFrom(profileType))
            {
                AddResult(results, AudioCoverageResultSeverity.Info, "Terrain/material issue", $"Terrain profile type '{profile.terrainProfileTypeName}' was not found. Skipping terrain profile scan.", profile, AssetDatabase.GetAssetPath(profile));
                return;
            }

            foreach (Object asset in FindAssetsByType(profileType))
            {
                if (asset == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(asset);
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty bindings = so.FindProperty(profile.terrainLayerBindingsPropertyName);
                if (bindings == null || !bindings.isArray)
                {
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Terrain/material issue", $"Terrain profile has no array/list property named '{profile.terrainLayerBindingsPropertyName}'.", asset, path);
                    continue;
                }

                HashSet<Object> seenLayers = new HashSet<Object>();
                if (bindings.arraySize == 0)
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Terrain/material issue", "Terrain profile has no layer bindings.", asset, path);

                for (int i = 0; i < bindings.arraySize; i++)
                {
                    SerializedProperty element = bindings.GetArrayElementAtIndex(i);
                    SerializedProperty layer = element.FindPropertyRelative(profile.terrainLayerPropertyName);
                    SerializedProperty material = element.FindPropertyRelative(profile.terrainMaterialPropertyName);

                    Object layerObject = layer != null && layer.propertyType == SerializedPropertyType.ObjectReference ? layer.objectReferenceValue : null;
                    Object materialObject = material != null && material.propertyType == SerializedPropertyType.ObjectReference ? material.objectReferenceValue : null;

                    if (layerObject == null || materialObject == null)
                        AddResult(results, AudioCoverageResultSeverity.Warning, "Terrain/material issue", $"Terrain profile has incomplete layer binding at index {i}.", asset, path);
                    else if (!seenLayers.Add(layerObject))
                        AddResult(results, AudioCoverageResultSeverity.Warning, "Terrain/material issue", $"Terrain profile has duplicate terrain layer binding: {layerObject.name}.", asset, path);
                }
            }
        }

        private static void ScanInteractionMatrices(AudioCoverageProfileSO profile, List<AudioCoverageContextResult> results)
        {
            Type matrixType = FindType(profile.interactionMatrixTypeName);
            if (matrixType == null || !typeof(Object).IsAssignableFrom(matrixType))
            {
                AddResult(results, AudioCoverageResultSeverity.Info, "Terrain/material issue", $"Interaction matrix type '{profile.interactionMatrixTypeName}' was not found. Skipping matrix scan.", profile, AssetDatabase.GetAssetPath(profile));
                return;
            }

            foreach (Object asset in FindAssetsByType(matrixType))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty exactProfiles = so.FindProperty(profile.exactProfilesPropertyName);
                SerializedProperty categoryFallbacks = so.FindProperty(profile.categoryFallbacksPropertyName);
                int exactCount = exactProfiles != null && exactProfiles.isArray ? exactProfiles.arraySize : 0;
                int fallbackCount = categoryFallbacks != null && categoryFallbacks.isArray ? categoryFallbacks.arraySize : 0;

                if (exactProfiles == null && categoryFallbacks == null)
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Terrain/material issue", "Matrix scan properties were not found. Check exact/category fallback property names in the profile.", asset, path);
                else if (exactCount == 0 && fallbackCount == 0)
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Terrain/material issue", "Interaction matrix has no exact profiles or category fallbacks.", asset, path);
            }
        }

        private static void ScanComponentExpectations(AudioCoverageProfileSO profile, AudioCoverageContextScanOptions options, List<AudioCoverageContextResult> results)
        {
            if (profile.componentExpectations == null || profile.componentExpectations.Count == 0)
            {
                AddResult(results, AudioCoverageResultSeverity.Info, "Suggested", "No component expectation rules are configured in the profile.", profile, AssetDatabase.GetAssetPath(profile));
                return;
            }

            for (int r = 0; r < profile.componentExpectations.Count; r++)
            {
                AudioComponentExpectationRule rule = profile.componentExpectations[r];
                if (rule == null || !rule.required)
                    continue;

                Type sourceType = FindType(rule.sourceComponentTypeName);
                Type requiredType = FindType(rule.requiredComponentTypeName);

                if (sourceType == null || !typeof(Component).IsAssignableFrom(sourceType))
                {
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Profile/setup issue", $"Source component type '{rule.sourceComponentTypeName}' was not found or is not a Component.", profile, AssetDatabase.GetAssetPath(profile));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rule.requiredComponentTypeName) && (requiredType == null || !typeof(Component).IsAssignableFrom(requiredType)))
                {
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Profile/setup issue", $"Required component type '{rule.requiredComponentTypeName}' was not found or is not a Component.", profile, AssetDatabase.GetAssetPath(profile));
                    continue;
                }

                if (rule.validationScope == AudioBindingValidationScope.OpenScenes || rule.validationScope == AudioBindingValidationScope.SceneAndPrefabs || rule.validationScope == AudioBindingValidationScope.SelectedObjects)
                    ScanSceneComponentExpectation(rule, sourceType, requiredType, options, results);

                if (rule.validationScope == AudioBindingValidationScope.ProjectPrefabs || rule.validationScope == AudioBindingValidationScope.SceneAndPrefabs)
                    ScanPrefabComponentExpectation(rule, sourceType, requiredType, results);
            }
        }

        private static void ScanSceneComponentExpectation(AudioComponentExpectationRule rule, Type sourceType, Type requiredType, AudioCoverageContextScanOptions options, List<AudioCoverageContextResult> results)
        {
            Component[] sources = options.SelectionOnly ? GetSelectedComponents(sourceType) : FindSceneComponents(sourceType);
            for (int i = 0; i < sources.Length; i++)
            {
                Component source = sources[i];
                if (source == null || requiredType == null)
                    continue;

                if (!HasComponentInChildrenOrParents(source.transform, requiredType))
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Missing setup reference", $"{sourceType.Name} object is missing expected {requiredType.Name}: {GetHierarchyPath(source.transform)}", source, null);
            }
        }

        private static void ScanPrefabComponentExpectation(AudioComponentExpectationRule rule, Type sourceType, Type requiredType, List<AudioCoverageContextResult> results)
        {
            string filter = string.IsNullOrWhiteSpace(rule.prefabSearchFilter) ? "t:Prefab" : rule.prefabSearchFilter;
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                Component[] sources = prefab.GetComponentsInChildren(sourceType, true) as Component[];
                if (sources == null || sources.Length == 0)
                    continue;

                if (requiredType == null)
                    continue;

                Component[] required = prefab.GetComponentsInChildren(requiredType, true) as Component[];
                if (required == null || required.Length == 0)
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Missing setup reference", $"Prefab contains {sourceType.Name} but is missing expected {requiredType.Name}.", prefab, path);
            }
        }

        private static void ScanRequiredFields(AudioCoverageProfileSO profile, List<AudioCoverageContextResult> results)
        {
            if (profile.requiredFieldRules == null || profile.requiredFieldRules.Count == 0)
            {
                AddResult(results, AudioCoverageResultSeverity.Info, "Suggested", "No required field rules are configured in the profile.", profile, AssetDatabase.GetAssetPath(profile));
                return;
            }

            for (int r = 0; r < profile.requiredFieldRules.Count; r++)
            {
                AudioRequiredFieldRule rule = profile.requiredFieldRules[r];
                if (rule == null || !rule.required)
                    continue;

                Type assetType = FindType(rule.assetTypeName);
                if (assetType == null || !typeof(Object).IsAssignableFrom(assetType))
                {
                    AddResult(results, AudioCoverageResultSeverity.Warning, "Profile/setup issue", $"Required-field asset type '{rule.assetTypeName}' was not found.", profile, AssetDatabase.GetAssetPath(profile));
                    continue;
                }

                foreach (Object asset in FindAssetsByType(assetType))
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    SerializedObject so = new SerializedObject(asset);

                    for (int i = 0; i < rule.requiredObjectFieldNames.Count; i++)
                    {
                        string fieldName = rule.requiredObjectFieldNames[i];
                        if (string.IsNullOrWhiteSpace(fieldName))
                            continue;

                        SerializedProperty property = so.FindProperty(fieldName);
                        if (property == null)
                        {
                            AddResult(results, AudioCoverageResultSeverity.Warning, "Invalid", $"Required field '{fieldName}' was not found on {assetType.Name}.", asset, path);
                            continue;
                        }

                        if (IsSerializedPropertyMissing(property))
                            AddResult(results, AudioCoverageResultSeverity.Warning, "Missing setup reference", $"Required field '{fieldName}' is unassigned or empty on {asset.name}.", asset, path);
                    }
                }
            }
        }

        private static void AddResult(List<AudioCoverageContextResult> results, AudioCoverageResultSeverity severity, string category, string message, Object context, string path)
        {
            results.Add(new AudioCoverageContextResult
            {
                Severity = severity,
                Category = category,
                Message = message,
                Context = context,
                Path = path
            });
        }

        private static string SanitizeCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "ISSUE";
            return value.Trim().ToUpperInvariant().Replace(' ', '_').Replace('/', '_');
        }

        private static Collider[] FindSceneColliders()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);
#else
            return Object.FindObjectsOfType<Collider>(true);
#endif
        }

        private static Component[] FindSceneComponents(Type type)
        {
            if (type == null)
                return Array.Empty<Component>();

#if UNITY_2023_1_OR_NEWER
            Object[] objects = Object.FindObjectsByType(type, FindObjectsInactive.Include);
#else
            Object[] objects = Object.FindObjectsOfType(type, true);
#endif
            List<Component> components = new List<Component>();
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] is Component component)
                    components.Add(component);
            return components.ToArray();
        }

        private static Collider[] GetSelectedColliders()
        {
            List<Collider> colliders = new List<Collider>();
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null)
                    continue;
                colliders.AddRange(selected[i].GetComponentsInChildren<Collider>(true));
            }
            return colliders.ToArray();
        }

        private static Component[] GetSelectedComponents(Type type)
        {
            if (type == null)
                return Array.Empty<Component>();

            List<Component> components = new List<Component>();
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null)
                    continue;
                Component[] found = selected[i].GetComponentsInChildren(type, true) as Component[];
                if (found != null)
                    components.AddRange(found);
            }
            return components.ToArray();
        }

        private static IEnumerable<Object> FindAssetsByType(Type type)
        {
            if (type == null)
                yield break;

            string[] guids = AssetDatabase.FindAssets("t:" + type.Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset != null)
                    yield return asset;
            }
        }

        private static bool IsTerrainCollider(Collider collider)
        {
            if (collider == null)
                return false;

            if (collider is TerrainCollider)
                return true;

            return collider.GetComponent<Terrain>() != null || collider.GetComponentInParent<Terrain>() != null;
        }

        private static Component FindComponentInParents(Transform transform, Type componentType)
        {
            if (transform == null || componentType == null)
                return null;

            Transform current = transform;
            while (current != null)
            {
                Component component = current.GetComponent(componentType) as Component;
                if (component != null)
                    return component;
                current = current.parent;
            }

            return null;
        }

        private static bool HasComponentInChildrenOrParents(Transform transform, Type componentType)
        {
            if (transform == null || componentType == null)
                return false;

            if (FindComponentInParents(transform, componentType) != null)
                return true;

            Component[] children = transform.GetComponentsInChildren(componentType, true) as Component[];
            return children != null && children.Length > 0;
        }

        private static bool IsSerializedObjectReferenceAssigned(Component component, string fieldName, out string message)
        {
            message = string.Empty;
            if (component == null)
                return false;

            SerializedObject so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                message = $"{component.GetType().Name} has no serialized field named '{fieldName}'.";
                return false;
            }

            if (IsSerializedPropertyMissing(property))
            {
                message = $"{component.GetType().Name}.{fieldName} is unassigned on {GetHierarchyPath(component.transform)}.";
                return false;
            }

            return true;
        }

        private static bool IsSerializedPropertyMissing(SerializedProperty property)
        {
            if (property == null)
                return true;

            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null;
                case SerializedPropertyType.String:
                    return string.IsNullOrWhiteSpace(property.stringValue);
                case SerializedPropertyType.Generic:
                    return property.isArray && property.arraySize == 0;
                default:
                    return false;
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static Type FindType(string typeName)
        {
            return string.IsNullOrWhiteSpace(typeName) ? null : PungentEditorPerformanceUtility.ResolveTypeCached(typeName);
        }

        private sealed class CooperativeContextAudit
        {
            private readonly AudioCoverageProfileSO _profile;
            private readonly AudioCoverageContextScanOptions _options;
            private readonly List<AudioCoverageContextResult> _results = new List<AudioCoverageContextResult>();
            private PungentScanSession _session;
            private PungentScanResult _scan;
            private int _moduleIndex = -1;
            private bool _done;

            public CooperativeContextAudit(AudioCoverageProfileSO profile, AudioCoverageContextScanOptions options)
            {
                _profile = profile;
                _options = options ?? new AudioCoverageContextScanOptions();
            }

            public PungentAuditScanStepResult Step(PungentAuditScanContext context)
            {
                if (context.IsCancellationRequested())
                    return Cancel("Audio Setup Coverage cancelled before the next module.");
                if (context.IsPauseRequested())
                    return PungentAuditScanStepResult.Continue("Audio Setup Coverage paused.");
                if (_done)
                    return PungentAuditScanStepResult.Complete("Audio Setup Coverage already completed.", _scan);

                if (_session == null)
                {
                    _session = new PungentScanSession("audio-setup-coverage", "Audio Setup Coverage");
                    _scan = _session.Begin(_options.SelectionOnly ? PungentScanScope.Selection : PungentScanScope.ProjectAssets, _options.SelectionOnly ? "Selection" : "Open Scenes + Project Assets");
                    _moduleIndex = 0;
                    context.Report(0.05f, 0, 5, "Resolving audio setup coverage profile.", false, "Resolve profile");
                    return PungentAuditScanStepResult.Continue("Resolving audio setup coverage profile.");
                }

                if (_profile == null)
                {
                    _results.Add(new AudioCoverageContextResult
                    {
                        Severity = AudioCoverageResultSeverity.Warning,
                        Category = "Profile/setup issue",
                        Message = "No AudioCoverageProfileSO is assigned.",
                        Context = null,
                        Path = null
                    });
                    AddResultsToScan(_scan, _results);
                    PungentScanResult notConfigured = _session.Complete(_results.Count, _results.Count, 0, 0, "No AudioCoverageProfileSO is configured.");
                    _done = true;
                    return PungentAuditScanStepResult.NotConfigured(notConfigured.StatusMessage, notConfigured);
                }

                while (_moduleIndex < 5)
                {
                    switch (_moduleIndex)
                    {
                        case 0:
                            if (_options.IncludeSceneReferences)
                                ScanSceneReferenceCoverage(_profile, _options, _results);
                            context.Report(0.22f, 1, 5, "Scene reference coverage checked.", false, "Scene references");
                            _moduleIndex++;
                            return PungentAuditScanStepResult.Continue("Scene reference coverage checked.");
                        case 1:
                            if (_options.IncludeTerrainProfiles)
                                ScanTerrainProfiles(_profile, _results);
                            context.Report(0.40f, 2, 5, "Terrain profile coverage checked.", false, "Terrain profiles");
                            _moduleIndex++;
                            return PungentAuditScanStepResult.Continue("Terrain profile coverage checked.");
                        case 2:
                            if (_options.IncludeInteractionMatrices)
                                ScanInteractionMatrices(_profile, _results);
                            context.Report(0.58f, 3, 5, "Interaction matrix coverage checked.", false, "Interaction matrices");
                            _moduleIndex++;
                            return PungentAuditScanStepResult.Continue("Interaction matrix coverage checked.");
                        case 3:
                            if (_options.IncludeComponentExpectations)
                                ScanComponentExpectations(_profile, _options, _results);
                            context.Report(0.76f, 4, 5, "Component expectations checked.", false, "Component expectations");
                            _moduleIndex++;
                            return PungentAuditScanStepResult.Continue("Component expectations checked.");
                        default:
                            if (_options.IncludeRequiredFields)
                                ScanRequiredFields(_profile, _results);
                            context.Report(0.92f, 5, 5, "Required field coverage checked.", false, "Required fields");
                            _moduleIndex++;
                            return PungentAuditScanStepResult.Continue("Required field coverage checked.");
                    }
                }

                if (_results.Count == 0)
                    AddResult(_results, AudioCoverageResultSeverity.Info, "Complete", "No obvious contextual audio setup gaps were found in this pass.", _profile, AssetDatabase.GetAssetPath(_profile));

                AddResultsToScan(_scan, _results);
                string status = "Scan complete: " + _results.Count + " result(s).";
                PungentScanResult completed = _session.Complete(_results.Count, _results.Count, 0, 0, status);
                context.Report(1f, _results.Count, Math.Max(1, _results.Count), status, false, "Complete");
                _done = true;
                return PungentAuditScanStepResult.Complete(status, completed);
            }

            private PungentAuditScanStepResult Cancel(string status)
            {
                if (_session != null)
                    _session.Cancel(status, false);
                _done = true;
                return PungentAuditScanStepResult.Cancelled(status);
            }
        }
    }
#endif
}
