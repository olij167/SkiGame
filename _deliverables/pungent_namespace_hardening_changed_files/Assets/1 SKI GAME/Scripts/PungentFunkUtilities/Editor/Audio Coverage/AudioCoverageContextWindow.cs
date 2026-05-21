using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Generic, profile-driven audio setup coverage scanner.
    /// Uses configurable component, field, prefab, and asset rules instead of hard-coded gameplay checks.
    /// </summary>
    public sealed class AudioCoverageContextWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtility.AudioSetupCoverage.";
        private const string PrefProfileGuid = PrefPrefix + "ProfileGuid";
        private const string PrefSelectionOnly = PrefPrefix + "SelectionOnly";
        private const string PrefIncludeSceneRefs = PrefPrefix + "IncludeSceneRefs";
        private const string PrefIncludeTerrainProfiles = PrefPrefix + "IncludeTerrainProfiles";
        private const string PrefIncludeMatrices = PrefPrefix + "IncludeMatrices";
        private const string PrefIncludeComponentExpectations = PrefPrefix + "IncludeComponentExpectations";
        private const string PrefIncludeRequiredFields = PrefPrefix + "IncludeRequiredFields";
        private const string PrefConfigHeight = PrefPrefix + "ConfigHeight";

        private enum ResultSeverity
        {
            Info,
            Warning,
            Error
        }

        private sealed class ScanResult
        {
            public ResultSeverity Severity;
            public string Category;
            public string Message;
            public Object Context;
            public string Path;
        }

        private Vector2 _mainScroll;
        private Vector2 _configScroll;
        private Vector2 _resultsScroll;
        private AudioCoverageProfileSO _profile;
        private readonly List<ScanResult> _results = new List<ScanResult>();
        private string _status = "Ready.";
        private bool _selectionOnly;
        private bool _includeSceneReferences = true;
        private bool _includeTerrainProfiles = true;
        private bool _includeInteractionMatrices = true;
        private bool _includeComponentExpectations = true;
        private bool _includeRequiredFields = true;
        private float _configHeight = 184f;

        [MenuItem("Tools/Utilities/Audio/Audio Setup Coverage", priority = 1201)]
        public static void OpenGeneric()
        {
            Open();
        }

        public static void Open()
        {
            AudioCoverageContextWindow window = GetWindow<AudioCoverageContextWindow>("Audio Setup Coverage");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Audio Setup Coverage");
            LoadPrefs();
            LoadRememberedProfile();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Audio Setup Coverage",
                "Validate scene references, prefab component expectations, required config fields, terrain profiles, and interaction matrices from a reusable coverage profile.",
                _status);

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawConfigPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _configHeight, 120f, 360f, SavePrefs);
            DrawResultsPanel();
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void LoadPrefs()
        {
            _selectionOnly = UtilityWindowPrefs.GetBool(PrefSelectionOnly, _selectionOnly);
            _includeSceneReferences = UtilityWindowPrefs.GetBool(PrefIncludeSceneRefs, _includeSceneReferences);
            _includeTerrainProfiles = UtilityWindowPrefs.GetBool(PrefIncludeTerrainProfiles, _includeTerrainProfiles);
            _includeInteractionMatrices = UtilityWindowPrefs.GetBool(PrefIncludeMatrices, _includeInteractionMatrices);
            _includeComponentExpectations = UtilityWindowPrefs.GetBool(PrefIncludeComponentExpectations, _includeComponentExpectations);
            _includeRequiredFields = UtilityWindowPrefs.GetBool(PrefIncludeRequiredFields, _includeRequiredFields);
            _configHeight = UtilityWindowPrefs.GetFloat(PrefConfigHeight, _configHeight);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefSelectionOnly, _selectionOnly);
            UtilityWindowPrefs.SetBool(PrefIncludeSceneRefs, _includeSceneReferences);
            UtilityWindowPrefs.SetBool(PrefIncludeTerrainProfiles, _includeTerrainProfiles);
            UtilityWindowPrefs.SetBool(PrefIncludeMatrices, _includeInteractionMatrices);
            UtilityWindowPrefs.SetBool(PrefIncludeComponentExpectations, _includeComponentExpectations);
            UtilityWindowPrefs.SetBool(PrefIncludeRequiredFields, _includeRequiredFields);
            UtilityWindowPrefs.SetFloat(PrefConfigHeight, _configHeight);
            StoreAssetGuid(PrefProfileGuid, _profile);
        }

        private void LoadRememberedProfile()
        {
            _profile = LoadAssetFromGuid<AudioCoverageProfileSO>(UtilityWindowPrefs.GetString(PrefProfileGuid, string.Empty));

            if (_profile == null)
            {
                string activeGuid = SessionState.GetString("GenericUtility.AudioCoverage.ActiveProfileGuid", string.Empty);
                _profile = LoadAssetFromGuid<AudioCoverageProfileSO>(activeGuid);
            }

            if (_profile == null)
                _profile = FindFirstAsset<AudioCoverageProfileSO>();

            if (Selection.activeObject is AudioCoverageProfileSO selectedProfile)
                _profile = selectedProfile;
        }

        private void DrawConfigPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Configuration", UtilityWindowTheme.Blue, _profile != null ? _profile.profileName : "No Profile");
                _configScroll = EditorGUILayout.BeginScrollView(_configScroll, GUILayout.Height(_configHeight));

                using (new EditorGUILayout.HorizontalScope())
                {
                    _profile = (AudioCoverageProfileSO)EditorGUILayout.ObjectField(
                        new GUIContent("Coverage Profile", "Profile containing setup scan configuration and binding expectations."),
                        _profile,
                        typeof(AudioCoverageProfileSO),
                        false);

                    if (GUILayout.Button("Use Selection", GUILayout.Width(104f)) && Selection.activeObject is AudioCoverageProfileSO selectedProfile)
                        _profile = selectedProfile;

                    using (new EditorGUI.DisabledScope(_profile == null))
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(72f)))
                        {
                            Selection.activeObject = _profile;
                            EditorGUIUtility.PingObject(_profile);
                        }
                    }
                }

                _selectionOnly = EditorGUILayout.ToggleLeft(new GUIContent("Scan Selection Only", "Only validate selected GameObjects and their children for scene-object checks."), _selectionOnly);
                _includeSceneReferences = EditorGUILayout.ToggleLeft(new GUIContent("Scene Collider Reference Coverage", "Check colliders for the configured scene reference component and reference field."), _includeSceneReferences);
                _includeTerrainProfiles = EditorGUILayout.ToggleLeft(new GUIContent("Terrain Profile Coverage", "Check configured terrain audio profile assets if their type exists."), _includeTerrainProfiles);
                _includeInteractionMatrices = EditorGUILayout.ToggleLeft(new GUIContent("Interaction Matrix Coverage", "Check configured audio interaction matrix assets if their type exists."), _includeInteractionMatrices);
                _includeComponentExpectations = EditorGUILayout.ToggleLeft(new GUIContent("Prefab Component Expectations", "Check profile component-pair rules without compile-time type references."), _includeComponentExpectations);
                _includeRequiredFields = EditorGUILayout.ToggleLeft(new GUIContent("Required Config Field Coverage", "Check profile-defined ScriptableObject field requirements."), _includeRequiredFields);

                if (_profile == null)
                    EditorGUILayout.HelpBox("Assign an AudioCoverageProfileSO before scanning.", MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    using (new EditorGUI.DisabledScope(_profile == null))
                    {
                        if (GUILayout.Button("Run Scan", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                            RunScan();
                        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                        {
                            _results.Clear();
                            _status = "Cleared.";
                        }
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open Catalog Coverage", EditorStyles.toolbarButton, GUILayout.Width(140f)))
                        AudioCoverageWindow.Open();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawResultsPanel()
        {
            int warnings = 0;
            int errors = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].Severity == ResultSeverity.Warning)
                    warnings++;
                else if (_results[i].Severity == ResultSeverity.Error)
                    errors++;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(errors > 0 ? UtilityWindowTheme.Red : warnings > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scan Results", errors > 0 ? UtilityWindowTheme.Red : warnings > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, _results.Count.ToString());

                if (_results.Count == 0)
                    EditorGUILayout.HelpBox("Run a scan to validate the current profile against open scenes, prefabs, and assets.", MessageType.Info);

                _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, GUILayout.MinHeight(260f));
                for (int i = 0; i < _results.Count; i++)
                    DrawResult(_results[i]);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawResult(ScanResult result)
        {
            if (result == null)
                return;

            Color tint = result.Severity == ResultSeverity.Error
                ? UtilityWindowTheme.Red
                : result.Severity == ResultSeverity.Warning
                    ? UtilityWindowTheme.Amber
                    : UtilityWindowTheme.Green;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.14f, 0.06f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(result.Category, UtilityWindowTheme.SectionHeaderStyle, GUILayout.Width(190f));
                    UtilityWindowTheme.CountPill(result.Severity.ToString(), tint, 78f);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(result.Context == null))
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                        {
                            EditorGUIUtility.PingObject(result.Context);
                            Selection.activeObject = result.Context;
                        }
                    }
                }

                EditorGUILayout.LabelField(result.Message, UtilityWindowTheme.CardLabelStyle);
                if (!string.IsNullOrWhiteSpace(result.Path))
                    EditorGUILayout.LabelField(result.Path, UtilityWindowTheme.PathLabelStyle);
            }
        }

        private void RunScan()
        {
            _results.Clear();

            if (_profile == null)
            {
                AddResult(ResultSeverity.Warning, "Profile", "No AudioCoverageProfileSO is assigned.", null, null);
                _status = "No profile assigned.";
                return;
            }

            if (_includeSceneReferences)
                ScanSceneReferenceCoverage();
            if (_includeTerrainProfiles)
                ScanTerrainProfiles();
            if (_includeInteractionMatrices)
                ScanInteractionMatrices();
            if (_includeComponentExpectations)
                ScanComponentExpectations();
            if (_includeRequiredFields)
                ScanRequiredFields();

            if (_results.Count == 0)
                AddResult(ResultSeverity.Info, "Complete", "No obvious contextual audio setup gaps were found in this pass.", _profile, AssetDatabase.GetAssetPath(_profile));

            _status = $"Scan complete: {_results.Count} result(s).";
        }

        private void ScanSceneReferenceCoverage()
        {
            Type componentType = FindType(_profile.sceneReferenceComponentTypeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                AddResult(ResultSeverity.Warning, "Scene References", $"Scene reference component type '{_profile.sceneReferenceComponentTypeName}' was not found or is not a Component.", _profile, AssetDatabase.GetAssetPath(_profile));
                return;
            }

            Collider[] colliders = _selectionOnly ? GetSelectedColliders() : FindSceneColliders();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                if (_profile.sceneReferenceSkipTriggers && collider.isTrigger)
                    continue;

                if (_profile.sceneReferenceSkipTerrains && IsTerrainCollider(collider))
                    continue;

                Component component = FindComponentInParents(collider.transform, componentType);
                if (component == null)
                {
                    AddResult(ResultSeverity.Warning, "Scene References", $"Collider is missing {_profile.sceneReferenceComponentTypeName} on itself or a parent: {GetHierarchyPath(collider.transform)}", collider, null);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(_profile.sceneReferenceFieldName) && !IsSerializedObjectReferenceAssigned(component, _profile.sceneReferenceFieldName, out string fieldMessage))
                    AddResult(ResultSeverity.Warning, "Scene References", fieldMessage, component, null);
            }
        }

        private void ScanTerrainProfiles()
        {
            Type profileType = FindType(_profile.terrainProfileTypeName);
            if (profileType == null || !typeof(Object).IsAssignableFrom(profileType))
            {
                AddResult(ResultSeverity.Info, "Terrain Profiles", $"Terrain profile type '{_profile.terrainProfileTypeName}' was not found. Skipping terrain profile scan.", _profile, AssetDatabase.GetAssetPath(_profile));
                return;
            }

            foreach (Object asset in FindAssetsByType(profileType))
            {
                if (asset == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(asset);
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty bindings = so.FindProperty(_profile.terrainLayerBindingsPropertyName);
                if (bindings == null || !bindings.isArray)
                {
                    AddResult(ResultSeverity.Warning, "Terrain Profiles", $"Terrain profile has no array/list property named '{_profile.terrainLayerBindingsPropertyName}'.", asset, path);
                    continue;
                }

                HashSet<Object> seenLayers = new HashSet<Object>();
                if (bindings.arraySize == 0)
                    AddResult(ResultSeverity.Warning, "Terrain Profiles", "Terrain profile has no layer bindings.", asset, path);

                for (int i = 0; i < bindings.arraySize; i++)
                {
                    SerializedProperty element = bindings.GetArrayElementAtIndex(i);
                    SerializedProperty layer = element.FindPropertyRelative(_profile.terrainLayerPropertyName);
                    SerializedProperty material = element.FindPropertyRelative(_profile.terrainMaterialPropertyName);

                    Object layerObject = layer != null && layer.propertyType == SerializedPropertyType.ObjectReference ? layer.objectReferenceValue : null;
                    Object materialObject = material != null && material.propertyType == SerializedPropertyType.ObjectReference ? material.objectReferenceValue : null;

                    if (layerObject == null || materialObject == null)
                        AddResult(ResultSeverity.Warning, "Terrain Profiles", $"Terrain profile has incomplete layer binding at index {i}.", asset, path);
                    else if (!seenLayers.Add(layerObject))
                        AddResult(ResultSeverity.Warning, "Terrain Profiles", $"Terrain profile has duplicate terrain layer binding: {layerObject.name}.", asset, path);
                }
            }
        }

        private void ScanInteractionMatrices()
        {
            Type matrixType = FindType(_profile.interactionMatrixTypeName);
            if (matrixType == null || !typeof(Object).IsAssignableFrom(matrixType))
            {
                AddResult(ResultSeverity.Info, "Interaction Matrix", $"Interaction matrix type '{_profile.interactionMatrixTypeName}' was not found. Skipping matrix scan.", _profile, AssetDatabase.GetAssetPath(_profile));
                return;
            }

            foreach (Object asset in FindAssetsByType(matrixType))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty exactProfiles = so.FindProperty(_profile.exactProfilesPropertyName);
                SerializedProperty categoryFallbacks = so.FindProperty(_profile.categoryFallbacksPropertyName);
                int exactCount = exactProfiles != null && exactProfiles.isArray ? exactProfiles.arraySize : 0;
                int fallbackCount = categoryFallbacks != null && categoryFallbacks.isArray ? categoryFallbacks.arraySize : 0;

                if (exactProfiles == null && categoryFallbacks == null)
                    AddResult(ResultSeverity.Warning, "Interaction Matrix", "Matrix scan properties were not found. Check exact/category fallback property names in the profile.", asset, path);
                else if (exactCount == 0 && fallbackCount == 0)
                    AddResult(ResultSeverity.Warning, "Interaction Matrix", "Interaction matrix has no exact profiles or category fallbacks.", asset, path);
            }
        }

        private void ScanComponentExpectations()
        {
            if (_profile.componentExpectations == null || _profile.componentExpectations.Count == 0)
            {
                AddResult(ResultSeverity.Info, "Component Expectations", "No component expectation rules are configured in the profile.", _profile, AssetDatabase.GetAssetPath(_profile));
                return;
            }

            for (int r = 0; r < _profile.componentExpectations.Count; r++)
            {
                AudioComponentExpectationRule rule = _profile.componentExpectations[r];
                if (rule == null || !rule.required)
                    continue;

                Type sourceType = FindType(rule.sourceComponentTypeName);
                Type requiredType = FindType(rule.requiredComponentTypeName);

                if (sourceType == null || !typeof(Component).IsAssignableFrom(sourceType))
                {
                    AddResult(ResultSeverity.Warning, "Component Expectations", $"Source component type '{rule.sourceComponentTypeName}' was not found or is not a Component.", _profile, AssetDatabase.GetAssetPath(_profile));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rule.requiredComponentTypeName) && (requiredType == null || !typeof(Component).IsAssignableFrom(requiredType)))
                {
                    AddResult(ResultSeverity.Warning, "Component Expectations", $"Required component type '{rule.requiredComponentTypeName}' was not found or is not a Component.", _profile, AssetDatabase.GetAssetPath(_profile));
                    continue;
                }

                if (rule.validationScope == AudioBindingValidationScope.OpenScenes || rule.validationScope == AudioBindingValidationScope.SceneAndPrefabs || rule.validationScope == AudioBindingValidationScope.SelectedObjects)
                    ScanSceneComponentExpectation(rule, sourceType, requiredType);

                if (rule.validationScope == AudioBindingValidationScope.ProjectPrefabs || rule.validationScope == AudioBindingValidationScope.SceneAndPrefabs)
                    ScanPrefabComponentExpectation(rule, sourceType, requiredType);
            }
        }

        private void ScanSceneComponentExpectation(AudioComponentExpectationRule rule, Type sourceType, Type requiredType)
        {
            Component[] sources = _selectionOnly ? GetSelectedComponents(sourceType) : FindSceneComponents(sourceType);
            for (int i = 0; i < sources.Length; i++)
            {
                Component source = sources[i];
                if (source == null || requiredType == null)
                    continue;

                if (!HasComponentInChildrenOrParents(source.transform, requiredType))
                    AddResult(ResultSeverity.Warning, "Component Expectations", $"{sourceType.Name} object is missing expected {requiredType.Name}: {GetHierarchyPath(source.transform)}", source, null);
            }
        }

        private void ScanPrefabComponentExpectation(AudioComponentExpectationRule rule, Type sourceType, Type requiredType)
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
                    AddResult(ResultSeverity.Warning, "Component Expectations", $"Prefab contains {sourceType.Name} but is missing expected {requiredType.Name}.", prefab, path);
            }
        }

        private void ScanRequiredFields()
        {
            if (_profile.requiredFieldRules == null || _profile.requiredFieldRules.Count == 0)
            {
                AddResult(ResultSeverity.Info, "Required Fields", "No required field rules are configured in the profile.", _profile, AssetDatabase.GetAssetPath(_profile));
                return;
            }

            for (int r = 0; r < _profile.requiredFieldRules.Count; r++)
            {
                AudioRequiredFieldRule rule = _profile.requiredFieldRules[r];
                if (rule == null || !rule.required)
                    continue;

                Type assetType = FindType(rule.assetTypeName);
                if (assetType == null || !typeof(Object).IsAssignableFrom(assetType))
                {
                    AddResult(ResultSeverity.Warning, "Required Fields", $"Required-field asset type '{rule.assetTypeName}' was not found.", _profile, AssetDatabase.GetAssetPath(_profile));
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
                            AddResult(ResultSeverity.Warning, "Required Fields", $"Required field '{fieldName}' was not found on {assetType.Name}.", asset, path);
                            continue;
                        }

                        if (IsSerializedPropertyMissing(property))
                            AddResult(ResultSeverity.Warning, "Required Fields", $"Required field '{fieldName}' is unassigned or empty on {asset.name}.", asset, path);
                    }
                }
            }
        }

        private void AddResult(ResultSeverity severity, string category, string message, Object context, string path)
        {
            _results.Add(new ScanResult
            {
                Severity = severity,
                Category = category,
                Message = message,
                Context = context,
                Path = path
            });
        }

        private static Collider[] FindSceneColliders()
        {
    #if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
            return Object.FindObjectsOfType<Collider>(true);
    #endif
        }

        private static Component[] FindSceneComponents(Type type)
        {
            if (type == null)
                return Array.Empty<Component>();

    #if UNITY_2023_1_OR_NEWER
            Object[] objects = Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        private static void StoreAssetGuid(string key, Object asset)
        {
            string guid = string.Empty;
            if (asset != null)
                guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            UtilityWindowPrefs.SetString(key, guid);
        }

        private static T LoadAssetFromGuid<T>(string guid) where T : Object
        {
            if (string.IsNullOrWhiteSpace(guid))
                return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T FindFirstAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            typeName = typeName.Trim();
            Type direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type exact = assemblies[i].GetType(typeName);
                if (exact != null)
                    return exact;
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type candidate = types[t];
                    if (candidate == null)
                        continue;

                    if (candidate.Name == typeName || candidate.FullName == typeName)
                        return candidate;
                }
            }

            return null;
        }
    }
    #endif

}