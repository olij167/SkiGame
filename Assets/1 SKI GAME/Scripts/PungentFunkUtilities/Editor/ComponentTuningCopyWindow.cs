using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Generic component tuning copy window. It replaces the old NPC-specific movement tuning copier with a reusable
    /// source-to-target SerializedObject copier that can be used in any Unity project.
    /// </summary>
    public sealed class ComponentTuningCopyWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtility.ComponentTuningCopy.";
        private const string PrefDryRun = PrefPrefix + "DryRun";
        private const string PrefAssetRefs = PrefPrefix + "AssetRefs";
        private const string PrefSceneRefs = PrefPrefix + "SceneRefs";
        private const string PrefArrays = PrefPrefix + "Arrays";
        private const string PrefNested = PrefPrefix + "Nested";
        private const string PrefProtectRefs = PrefPrefix + "ProtectRefs";
        private const string PrefCreateMissing = PrefPrefix + "CreateMissing";

        private GameObject _sourceObject;
        private GameObject _targetObject;
        private readonly List<ComponentSelection> _components = new List<ComponentSelection>();
        private Vector2 _scroll;
        private Vector2 _componentScroll;
        private string _search = string.Empty;
        private string _lastReport = "Select a source and target GameObject, then refresh the component list.";
        private readonly PungentScanSession _operationSession = new PungentScanSession("component-tuning-copy", "Component Tuning Copy");
        private string _resultSourceBanner = string.Empty;
        private bool _copyQueued;

        private bool _dryRun;
        private bool _copyAssetReferences;
        private bool _copySceneReferences;
        private bool _copyArrays;
        private bool _copyNestedProperties;
        private bool _protectTargetSpecificReferences;
        private bool _createMissingComponents;

        public static void Open()
        {
            ComponentTuningCopyWindow window = GetWindow<ComponentTuningCopyWindow>("Component Tuning Copy");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _dryRun = UtilityWindowPrefs.GetBool(PrefDryRun, true);
            _copyAssetReferences = UtilityWindowPrefs.GetBool(PrefAssetRefs, true);
            _copySceneReferences = UtilityWindowPrefs.GetBool(PrefSceneRefs, false);
            _copyArrays = UtilityWindowPrefs.GetBool(PrefArrays, true);
            _copyNestedProperties = UtilityWindowPrefs.GetBool(PrefNested, true);
            _protectTargetSpecificReferences = UtilityWindowPrefs.GetBool(PrefProtectRefs, true);
            _createMissingComponents = UtilityWindowPrefs.GetBool(PrefCreateMissing, false);
            if (PungentScanCache.TryHydrateSession(_operationSession, out _resultSourceBanner) && _operationSession.Result != null)
                _lastReport = _operationSession.Result.StatusMessage;

            if (_sourceObject == null && Selection.activeGameObject != null)
                _sourceObject = Selection.activeGameObject;

            RefreshComponents();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Component Tuning Copy",
                "Copy serialized tuning values from matching components on a source object to a target object. References can be protected so target-specific links are not overwritten.",
                CountSelectedComponents() + " selected");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawObjectsPanel();
            DrawOptionsPanel();
            DrawComponentsPanel();
            DrawActionsPanel();
            DrawReportPanel();

            EditorGUILayout.EndScrollView();
        }

        private void DrawObjectsPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Objects", UtilityWindowTheme.Blue, ReadyToCopy() ? "ready" : "setup");

                EditorGUI.BeginChangeCheck();
                _sourceObject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Source", "The object whose component values will be copied."), _sourceObject, typeof(GameObject), true);
                _targetObject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Target", "The object that will receive copied component values."), _targetObject, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                    RefreshComponents();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                    {
                        if (UtilityWindowTheme.TintedButton("Selection → Source", UtilityWindowTheme.Blue, GUILayout.Height(24f)))
                        {
                            _sourceObject = Selection.activeGameObject;
                            RefreshComponents();
                        }

                        if (UtilityWindowTheme.TintedButton("Selection → Target", UtilityWindowTheme.Cyan, GUILayout.Height(24f)))
                        {
                            _targetObject = Selection.activeGameObject;
                            RefreshComponents();
                        }
                    }
                }
            }
        }

        private void DrawOptionsPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("Copy Rules", UtilityWindowTheme.Cyan, _dryRun ? "dry run" : "will modify");

                EditorGUI.BeginChangeCheck();
                _dryRun = EditorGUILayout.ToggleLeft(new GUIContent("Dry Run", "Report what would be copied without modifying the target."), _dryRun);
                _copyAssetReferences = EditorGUILayout.ToggleLeft(new GUIContent("Copy Asset References", "Allow references to project assets such as materials, ScriptableObjects, meshes, textures, and prefabs."), _copyAssetReferences);
                _copySceneReferences = EditorGUILayout.ToggleLeft(new GUIContent("Copy Scene References", "Allow references to scene objects/components. Usually keep this off when copying tuning between different actors."), _copySceneReferences);
                _copyArrays = EditorGUILayout.ToggleLeft(new GUIContent("Copy Arrays/Lists", "Allow serialized arrays and lists to be copied."), _copyArrays);
                _copyNestedProperties = EditorGUILayout.ToggleLeft(new GUIContent("Copy Nested Properties", "Allow child properties inside structs/classes to be copied."), _copyNestedProperties);
                _protectTargetSpecificReferences = EditorGUILayout.ToggleLeft(new GUIContent("Protect Common Target References", "Skip object-reference properties whose names look target-specific, such as camera, input, UI, source, target, anchor, audio, profile, or identity."), _protectTargetSpecificReferences);
                _createMissingComponents = EditorGUILayout.ToggleLeft(new GUIContent("Create Missing MonoBehaviour Components", "When a selected source MonoBehaviour is missing on the target, try to add the same component type before copying."), _createMissingComponents);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            }
        }

        private void DrawComponentsPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Components", UtilityWindowTheme.Purple, _components.Count.ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    _search = EditorGUILayout.TextField(new GUIContent("Search", "Filter source components by name/type."), _search);
                    if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                        RefreshComponents();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Visible", GUILayout.Height(22f)))
                        SetVisibleSelections(true);
                    if (GUILayout.Button("Clear Visible", GUILayout.Height(22f)))
                        SetVisibleSelections(false);
                }

                _componentScroll = EditorGUILayout.BeginScrollView(_componentScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
                if (_components.Count == 0)
                {
                    EditorGUILayout.LabelField("No source components found.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                else
                {
                    for (int i = 0; i < _components.Count; i++)
                    {
                        ComponentSelection selection = _components[i];
                        if (!MatchesSearch(selection))
                            continue;

                        DrawComponentRow(selection);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawComponentRow(ComponentSelection selection)
        {
            Component target = FindMatchingTargetComponent(selection);
            string status = target != null ? "match" : (_createMissingComponents && CanCreateMissingComponent(selection.SourceComponent) ? "can add" : "missing");

            using (new EditorGUILayout.HorizontalScope())
            {
                selection.Enabled = EditorGUILayout.Toggle(selection.Enabled, GUILayout.Width(18f));
                EditorGUILayout.LabelField(selection.DisplayName, UtilityWindowTheme.CardLabelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(status, UtilityWindowTheme.CountPillStyle);
            }
        }

        private void DrawActionsPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Actions", UtilityWindowTheme.Green, CountSelectedComponents().ToString());

                using (new EditorGUI.DisabledScope(!ReadyToCopy() || CountSelectedComponents() == 0))
                {
                    string label = _dryRun ? "Run Copy Preview" : "Copy Selected Component Tuning";
                    if (UtilityWindowTheme.TintedButton(label, UtilityWindowTheme.Green, GUILayout.Height(30f)))
                        QueueCopySelectedComponents();
                }
            }
        }

        private void DrawReportPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Report", UtilityWindowTheme.Amber, _dryRun ? "preview" : "result");
                PungentScanGUI.DrawResultHeader(_operationSession.Result, _resultSourceBanner);
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
            }
        }

        private void RefreshComponents()
        {
            _components.Clear();
            if (_sourceObject == null)
                return;

            Component[] sourceComponents = _sourceObject.GetComponents<Component>();
            Dictionary<Type, int> typeCounts = new Dictionary<Type, int>();

            for (int i = 0; i < sourceComponents.Length; i++)
            {
                Component component = sourceComponents[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                int occurrenceIndex;
                typeCounts.TryGetValue(type, out occurrenceIndex);
                typeCounts[type] = occurrenceIndex + 1;

                ComponentSelection selection = new ComponentSelection
                {
                    SourceComponent = component,
                    ComponentType = type,
                    OccurrenceIndex = occurrenceIndex,
                    DisplayName = GetDisplayName(component, occurrenceIndex),
                    Enabled = type != typeof(Transform)
                };

                if (_targetObject != null && FindMatchingTargetComponent(selection) == null)
                    selection.Enabled = false;

                _components.Add(selection);
            }
        }

        private void QueueCopySelectedComponents()
        {
            if (_copyQueued)
                return;

            _copyQueued = true;
            _lastReport = "Copy operation queued.";
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;

                _copyQueued = false;
                if (ReadyToCopy() && CountSelectedComponents() > 0)
                    CopySelectedComponents();
                Repaint();
            };
        }

        private void CopySelectedComponents()
        {
            if (!ReadyToCopy())
                return;

            PungentScanResult operationResult = _operationSession.Begin(PungentScanScope.Selection, _dryRun ? "Copy Preview" : "Copy Apply");

            try
            {
                int selected = 0;
                int copiedComponents = 0;
                int missingComponents = 0;
                int copiedProperties = 0;
                int skippedProperties = 0;
                List<string> warnings = new List<string>();

                if (!_dryRun)
                    Undo.RegisterFullObjectHierarchyUndo(_targetObject, "Copy Component Tuning");

                for (int i = 0; i < _components.Count; i++)
                {
                    ComponentSelection selection = _components[i];
                    if (!selection.Enabled || selection.SourceComponent == null)
                        continue;

                    selected++;
                    Component targetComponent = FindMatchingTargetComponent(selection);
                    if (targetComponent == null && _createMissingComponents)
                        targetComponent = TryCreateMissingComponent(selection.SourceComponent);

                    if (targetComponent == null)
                    {
                        missingComponents++;
                        string warning = "Missing target component: " + selection.DisplayName;
                        warnings.Add(warning);
                        operationResult.AddIssue(PungentScanSeverity.Warning, "Missing target component", warning, _targetObject, null, "COMPONENT_TUNING_MISSING_TARGET");
                        continue;
                    }

                    CopyStats stats = CopyComponent(selection.SourceComponent, targetComponent, _dryRun);
                    copiedProperties += stats.Copied;
                    skippedProperties += stats.Skipped;
                    copiedComponents++;

                    if (stats.Copied == 0)
                    {
                        operationResult.AddIssue(
                            PungentScanSeverity.Info,
                            "No copyable properties",
                            selection.DisplayName + " had no copyable serialized properties under the current copy rules.",
                            selection.SourceComponent,
                            null,
                            "COMPONENT_TUNING_NO_COPYABLE_PROPERTIES");
                    }
                }

                if (!_dryRun)
                {
                    EditorUtility.SetDirty(_targetObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(_targetObject);
                }

                _lastReport =
                    "Selected components: " + selected +
                    "\nComponents processed: " + copiedComponents +
                    "\nMissing components: " + missingComponents +
                    "\nProperties " + (_dryRun ? "that would be copied" : "copied") + ": " + copiedProperties +
                    "\nProperties skipped: " + skippedProperties;

                if (warnings.Count > 0)
                {
                    int max = Mathf.Min(warnings.Count, 8);
                    _lastReport += "\n\nWarnings:";
                    for (int i = 0; i < max; i++)
                        _lastReport += "\n- " + warnings[i];
                    if (warnings.Count > max)
                        _lastReport += "\n- + " + (warnings.Count - max) + " more";
                }

                string status = (_dryRun ? "Previewed" : "Copied") + " " + copiedComponents + " component(s), " + copiedProperties + " property value(s).";
                if (selected == 0)
                    operationResult.AddIssue(PungentScanSeverity.Warning, "No selected components", "No enabled source components were selected for the operation.", _sourceObject, null, "COMPONENT_TUNING_NONE_SELECTED");
                else if (missingComponents == 0)
                    operationResult.AddIssue(PungentScanSeverity.Success, "Operation ready", status, _targetObject, null, "COMPONENT_TUNING_SUCCESS");

                _operationSession.Complete(selected, copiedComponents, missingComponents, _dryRun ? 0 : copiedProperties, status);
                Debug.Log("[ComponentTuningCopy] " + _lastReport, _targetObject);
            }
            catch (Exception ex)
            {
                _lastReport = "Component tuning copy failed: " + ex.Message;
                _operationSession.Fail(ex, _lastReport);
                Debug.LogException(ex, _targetObject);
            }
        }

        private CopyStats CopyComponent(Component source, Component target, bool dryRun)
        {
            CopyStats stats = new CopyStats();
            SerializedObject sourceSerialized = new SerializedObject(source);
            SerializedObject targetSerialized = new SerializedObject(target);
            SerializedProperty iterator = sourceSerialized.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                SerializedProperty sourceProperty = iterator.Copy();

                if (ShouldSkipProperty(sourceProperty))
                {
                    stats.Skipped++;
                    continue;
                }

                SerializedProperty targetProperty = targetSerialized.FindProperty(sourceProperty.propertyPath);
                if (targetProperty == null || !targetProperty.editable)
                {
                    stats.Skipped++;
                    continue;
                }

                if (!dryRun)
                    targetSerialized.CopyFromSerializedProperty(sourceProperty);

                stats.Copied++;
            }

            if (!dryRun)
            {
                targetSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }

            return stats;
        }

        private bool ShouldSkipProperty(SerializedProperty property)
        {
            if (property == null)
                return true;

            if (property.propertyPath == "m_Script")
                return true;

            if (!_copyNestedProperties && property.depth > 0)
                return true;

            if (!_copyArrays && IsArrayLikeProperty(property))
                return true;

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (_protectTargetSpecificReferences && IsTargetSpecificReferencePath(property.propertyPath))
                    return true;

                UnityEngine.Object reference = property.objectReferenceValue;
                if (reference == null)
                    return false;

                bool persistent = EditorUtility.IsPersistent(reference);
                if (persistent && !_copyAssetReferences)
                    return true;
                if (!persistent && !_copySceneReferences)
                    return true;
            }

            return false;
        }

        private static bool IsArrayLikeProperty(SerializedProperty property)
        {
            if (property == null)
                return false;

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                return true;

            return property.propertyPath.IndexOf(".Array.", StringComparison.Ordinal) >= 0;
        }

        private Component FindMatchingTargetComponent(ComponentSelection selection)
        {
            if (_targetObject == null || selection == null || selection.ComponentType == null)
                return null;

            Component[] candidates = _targetObject.GetComponents(selection.ComponentType);
            if (candidates == null || candidates.Length == 0)
                return null;

            if (selection.OccurrenceIndex >= 0 && selection.OccurrenceIndex < candidates.Length)
                return candidates[selection.OccurrenceIndex];

            return candidates[0];
        }

        private Component TryCreateMissingComponent(Component sourceComponent)
        {
            if (!CanCreateMissingComponent(sourceComponent) || _targetObject == null)
                return null;

            try
            {
                return Undo.AddComponent(_targetObject, sourceComponent.GetType());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ComponentTuningCopy] Could not add missing component " + sourceComponent.GetType().Name + ": " + ex.Message, _targetObject);
                return null;
            }
        }

        private static bool CanCreateMissingComponent(Component sourceComponent)
        {
            if (sourceComponent == null)
                return false;

            Type type = sourceComponent.GetType();
            return type != typeof(Transform) && typeof(MonoBehaviour).IsAssignableFrom(type) && !type.IsAbstract;
        }

        private static string GetDisplayName(Component component, int occurrenceIndex)
        {
            if (component == null)
                return "Missing Component";

            string name = component.GetType().Name;
            return occurrenceIndex > 0 ? name + " #" + (occurrenceIndex + 1) : name;
        }

        private bool MatchesSearch(ComponentSelection selection)
        {
            if (selection == null || selection.SourceComponent == null)
                return false;

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string haystack = selection.DisplayName + " " + selection.ComponentType.FullName;
            return haystack.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetVisibleSelections(bool value)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (MatchesSearch(_components[i]))
                    _components[i].Enabled = value;
            }
        }

        private int CountSelectedComponents()
        {
            int count = 0;
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i].Enabled)
                    count++;
            }
            return count;
        }

        private bool ReadyToCopy()
        {
            return _sourceObject != null && _targetObject != null && _sourceObject != _targetObject;
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefDryRun, _dryRun);
            UtilityWindowPrefs.SetBool(PrefAssetRefs, _copyAssetReferences);
            UtilityWindowPrefs.SetBool(PrefSceneRefs, _copySceneReferences);
            UtilityWindowPrefs.SetBool(PrefArrays, _copyArrays);
            UtilityWindowPrefs.SetBool(PrefNested, _copyNestedProperties);
            UtilityWindowPrefs.SetBool(PrefProtectRefs, _protectTargetSpecificReferences);
            UtilityWindowPrefs.SetBool(PrefCreateMissing, _createMissingComponents);
        }

        private static bool IsTargetSpecificReferencePath(string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
                return false;

            string lower = propertyPath.ToLowerInvariant();
            return lower.Contains("camera") ||
                   lower.Contains("input") ||
                   lower.Contains("ui") ||
                   lower.Contains("dialogue") ||
                   lower.Contains("identity") ||
                   lower.Contains("profile") ||
                   lower.Contains("appearance") ||
                   lower.Contains("player") ||
                   lower.Contains("target") ||
                   lower.Contains("source") ||
                   lower.Contains("anchor") ||
                   lower.Contains("audio");
        }

        private sealed class ComponentSelection
        {
            public Component SourceComponent;
            public Type ComponentType;
            public int OccurrenceIndex;
            public string DisplayName;
            public bool Enabled;
        }

        private struct CopyStats
        {
            public int Copied;
            public int Skipped;
        }
    }

#endif

}
