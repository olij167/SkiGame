using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PungentSceneGizmoSource))]
    public sealed class PungentSceneGizmoSourceEditor : Editor
    {
        private SerializedProperty _drawInScene;
        private SerializedProperty _drawLabels;
        private SerializedProperty _drawOnlyWhenComponentEnabled;
        private SerializedProperty _cacheChildRendererBounds;
        private SerializedProperty _childBoundsRefreshInterval;
        private SerializedProperty _linkedPreset;
        private SerializedProperty _linkedPresetGuid;
        private SerializedProperty _linkedPresetId;
        private SerializedProperty _linkedPresetHash;
        private SerializedProperty _lockPresetPropagation;
        private SerializedProperty _presetPropagationMode;
        private SerializedProperty _rules;
        private PungentSceneGizmoSource.TemplateKind _template = PungentSceneGizmoSource.TemplateKind.TriggerRadius;
        private bool _replaceRulesWhenApplyingPreset;
        private string _lastPresetSummary = string.Empty;
        private PungentSceneGizmoPreset _assetPreset;
        private bool _showAdvancedRuleFields;
        private bool _showPresetLinkOverrides;

        private void OnEnable()
        {
            _drawInScene = serializedObject.FindProperty("drawInScene");
            _drawLabels = serializedObject.FindProperty("drawLabels");
            _drawOnlyWhenComponentEnabled = serializedObject.FindProperty("drawOnlyWhenComponentEnabled");
            _cacheChildRendererBounds = serializedObject.FindProperty("cacheChildRendererBounds");
            _childBoundsRefreshInterval = serializedObject.FindProperty("childBoundsRefreshInterval");
            _linkedPreset = serializedObject.FindProperty("linkedPreset");
            _linkedPresetGuid = serializedObject.FindProperty("linkedPresetGuid");
            _linkedPresetId = serializedObject.FindProperty("linkedPresetId");
            _linkedPresetHash = serializedObject.FindProperty("linkedPresetHash");
            _lockPresetPropagation = serializedObject.FindProperty("lockPresetPropagation");
            _presetPropagationMode = serializedObject.FindProperty("presetPropagationMode");
            _rules = serializedObject.FindProperty("rules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PungentSceneGizmoSource source = (PungentSceneGizmoSource)target;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Scene Gizmo Source", UtilityWindowTheme.Blue, _rules != null ? _rules.arraySize + " rules" : null);
                EditorGUILayout.PropertyField(_drawInScene);
                EditorGUILayout.PropertyField(_drawLabels);
                EditorGUILayout.PropertyField(_drawOnlyWhenComponentEnabled);
                EditorGUILayout.PropertyField(_cacheChildRendererBounds);
                using (new EditorGUI.DisabledScope(!_cacheChildRendererBounds.boolValue))
                    EditorGUILayout.PropertyField(_childBoundsRefreshInterval);
                EditorGUILayout.HelpBox("Start with a template, then bind position/size/label/conditions to transforms, constants, or reflected fields. This avoids writing custom OnDrawGizmos code for common debugging tasks.", MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Templates & Presets", UtilityWindowTheme.Teal);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _template = (PungentSceneGizmoSource.TemplateKind)EditorGUILayout.EnumPopup(_template);
                    if (UtilityWindowTheme.TintedButton("Add", UtilityWindowTheme.Green, GUILayout.Width(64f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        Undo.RecordObject(source, "Add Scene Gizmo Template");
                        source.AddTemplate(_template);
                        EditorUtility.SetDirty(source);
                        serializedObject.Update();
                        Repaint();
                    }
                    if (GUILayout.Button("Preset...", GUILayout.Width(76f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        ShowPresetMenu(source);
                    }
                    if (GUILayout.Button("Create Preset", GUILayout.Width(104f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        if (PungentSceneGizmoCommandService.SaveSourceAsNewPreset(source, out PungentSceneGizmoPreset preset))
                            _lastPresetSummary = $"Created preset asset '{preset.displayName}' with {preset.rules.Count} rule(s).";
                        serializedObject.Update();
                        Repaint();
                    }
                    if (GUILayout.Button(new GUIContent("Design", "Open this source in the Scene Gizmo Design workspace."), GUILayout.Width(72f)))
                        PungentGizmoBrowserWindow.OpenDesign(source);
                }

                _replaceRulesWhenApplyingPreset = EditorGUILayout.ToggleLeft("Replace existing rules when applying presets", _replaceRulesWhenApplyingPreset);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _assetPreset = (PungentSceneGizmoPreset)EditorGUILayout.ObjectField("Preset Asset", _assetPreset, typeof(PungentSceneGizmoPreset), false);
                    using (new EditorGUI.DisabledScope(_assetPreset == null))
                    {
                        if (GUILayout.Button("Apply Asset", GUILayout.Width(88f)))
                        {
                            serializedObject.ApplyModifiedProperties();
                            ApplyPresetToSource(source, _assetPreset, _assetPreset != null ? _assetPreset.displayName : "Preset");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(_lastPresetSummary))
                    EditorGUILayout.HelpBox(_lastPresetSummary, MessageType.Info);

                DrawCloneRuleAudit();
            }

            DrawPresetLinkOverrides(source);
            DrawComponentSetupActions(source);
            DrawSourcePerformanceBox(source);
            DrawRules();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresetLinkOverrides(PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            PungentSceneGizmoPresetDiffStatus status = PungentSceneGizmoPresetDiffUtility.GetStatus(source);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.10f, 0.04f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showPresetLinkOverrides = EditorGUILayout.Foldout(_showPresetLinkOverrides, "Preset Link & Overrides", true);
                    GUILayout.FlexibleSpace();
                    DrawPresetStateChip(status);
                }

                if (!_showPresetLinkOverrides)
                    return;

                EditorGUILayout.HelpBox(status.message, status.state == PungentSceneGizmoPresetInstanceState.Base || status.state == PungentSceneGizmoPresetInstanceState.Unlinked ? MessageType.Info : MessageType.Warning);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_linkedPreset);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    if (source.linkedPreset != null)
                    {
                        Undo.RecordObject(source, "Link Scene Gizmo Preset");
                        PungentSceneGizmoCommandService.LinkSourceToPreset(source, source.linkedPreset);
                        EditorUtility.SetDirty(source);
                        _lastPresetSummary = "Linked source to preset asset.";
                    }
                    else
                    {
                        PungentSceneGizmoCommandService.ClearPresetLink(source);
                        _lastPresetSummary = "Cleared preset link.";
                    }
                    serializedObject.Update();
                    Repaint();
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_linkedPresetId);
                    EditorGUILayout.PropertyField(_linkedPresetGuid);
                    EditorGUILayout.PropertyField(_linkedPresetHash);
                }

                EditorGUILayout.PropertyField(_lockPresetPropagation, new GUIContent("Lock From Preset Updates"));
                EditorGUILayout.PropertyField(_presetPropagationMode, new GUIContent("Propagation Mode"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!status.canUpdateFromPreset))
                    {
                        if (GUILayout.Button(new GUIContent("Update From Preset", status.canUpdateFromPreset ? "Replace this source with the latest linked preset." : "Already up to date, unlocked source required, or no linked preset.")))
                        {
                            serializedObject.ApplyModifiedProperties();
                            if (PungentSceneGizmoCommandService.UpdateSourceFromLinkedPreset(source, out PungentSceneGizmoPresetApplySummary summary))
                                _lastPresetSummary = "Updated from preset. " + summary;
                            serializedObject.Update();
                            Repaint();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!status.canRevertToPreset))
                    {
                        if (GUILayout.Button(new GUIContent("Revert To Preset", "Replace local rules with the linked preset.")))
                        {
                            if (EditorUtility.DisplayDialog("Revert Scene Gizmo Source", "Replace this source's rules with the linked preset?", "Revert", "Cancel"))
                            {
                                serializedObject.ApplyModifiedProperties();
                                if (PungentSceneGizmoCommandService.RevertSourceToLinkedPreset(source, out PungentSceneGizmoPresetApplySummary summary))
                                    _lastPresetSummary = "Reverted to preset. " + summary;
                                serializedObject.Update();
                                Repaint();
                            }
                        }
                    }
                }

                bool showSaveAsNew = status.canSaveAsNewPreset &&
                                     (status.state == PungentSceneGizmoPresetInstanceState.Unlinked ||
                                      status.state == PungentSceneGizmoPresetInstanceState.Customized ||
                                      status.state == PungentSceneGizmoPresetInstanceState.Mixed ||
                                      status.state == PungentSceneGizmoPresetInstanceState.MissingPreset);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!status.canOverwritePreset))
                    {
                        if (GUILayout.Button(new GUIContent("Overwrite Preset", "Write this source's rules back into the linked preset asset. Scene references are stripped.")))
                        {
                            if (EditorUtility.DisplayDialog("Overwrite Scene Gizmo Preset", "Overwrite the linked preset asset with this source's current rules? Scene references will be stripped from the asset.", "Overwrite", "Cancel"))
                            {
                                serializedObject.ApplyModifiedProperties();
                                if (PungentSceneGizmoCommandService.OverwriteLinkedPreset(source, out PungentSceneGizmoPreset preset))
                                    _lastPresetSummary = "Overwrote preset asset '" + preset.displayName + "'.";
                                serializedObject.Update();
                                Repaint();
                            }
                        }
                    }

                    using (new EditorGUI.DisabledScope(!showSaveAsNew))
                    {
                        if (GUILayout.Button(new GUIContent("Save As New Preset", showSaveAsNew ? "Create a new preset asset and link this source to it." : "Save-as-new appears when the source is custom, mixed, missing, or unlinked.")))
                        {
                            serializedObject.ApplyModifiedProperties();
                            if (PungentSceneGizmoCommandService.SaveSourceAsNewPreset(source, out PungentSceneGizmoPreset preset))
                                _lastPresetSummary = "Created and linked preset asset '" + preset.displayName + "'.";
                            serializedObject.Update();
                            Repaint();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!status.hasLinkedPreset))
                    {
                        if (GUILayout.Button(new GUIContent("Clear Link", "Keep current rules but remove preset linkage."), GUILayout.Width(76f)))
                        {
                            serializedObject.ApplyModifiedProperties();
                            PungentSceneGizmoCommandService.ClearPresetLink(source);
                            _lastPresetSummary = "Cleared preset link.";
                            serializedObject.Update();
                            Repaint();
                        }
                    }
                }

                if (status.state == PungentSceneGizmoPresetInstanceState.Base)
                    EditorGUILayout.LabelField("Raw rule editing remains available below; edited linked sources will show as Custom.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawRules()
        {
            if (_rules == null)
                return;

            for (int i = 0; i < _rules.arraySize; i++)
            {
                SerializedProperty rule = _rules.GetArrayElementAtIndex(i);
                SerializedProperty name = rule.FindPropertyRelative("name");
                SerializedProperty enabled = rule.FindPropertyRelative("enabled");
                PungentSceneGizmoSource source = (PungentSceneGizmoSource)target;
                PungentSceneGizmoSource.RuleStatus status = source.GetRuleStatus(i);

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(enabled.boolValue ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Amber, 0.12f, 0.05f, 7, 4)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
                        rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, string.IsNullOrWhiteSpace(name.stringValue) ? "Gizmo Rule" : name.stringValue, true);
                        GUILayout.FlexibleSpace();
                        PungentSceneGizmoRuleEditorUtility.DrawStatusChip(status);
                        if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(72f)))
                        {
                            _rules.InsertArrayElementAtIndex(i);
                            GUIUtility.ExitGUI();
                        }
                        if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24f)))
                        {
                            _rules.DeleteArrayElementAtIndex(i);
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (!rule.isExpanded)
                        continue;

                    PungentSceneGizmoRuleEditorUtility.DrawRuleMessages(status);
                    DrawContextualRuleFields(rule);
                }
            }
        }

        private void DrawContextualRuleFields(SerializedProperty rule)
        {
            SerializedProperty name = rule.FindPropertyRelative("name");
            SerializedProperty shapeProperty = rule.FindPropertyRelative("shape");
            SerializedProperty positionModeProperty = rule.FindPropertyRelative("positionMode");
            SerializedProperty sizeModeProperty = rule.FindPropertyRelative("sizeMode");
            SerializedProperty conditionProperty = rule.FindPropertyRelative("condition");
            SerializedProperty useStateColorMap = rule.FindPropertyRelative("useStateColorMap");

            PungentSceneGizmoSource.GizmoShape shape = (PungentSceneGizmoSource.GizmoShape)shapeProperty.enumValueIndex;
            PungentSceneGizmoSource.PositionMode positionMode = (PungentSceneGizmoSource.PositionMode)positionModeProperty.enumValueIndex;
            PungentSceneGizmoSource.SizeMode sizeMode = (PungentSceneGizmoSource.SizeMode)sizeModeProperty.enumValueIndex;
            PungentSceneGizmoSource.ConditionMode condition = (PungentSceneGizmoSource.ConditionMode)conditionProperty.enumValueIndex;

            EditorGUILayout.PropertyField(name);
            EditorGUILayout.PropertyField(shapeProperty);
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("drawWhen"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("color"));

            EditorGUILayout.Space(4f);
            UtilityWindowTheme.SectionTitle("Targets", UtilityWindowTheme.Cyan);
            if (RuleUsesTargetTransform(shape, positionMode))
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("targetTransform"));
            if (RuleNeedsSecondaryTransform(shape, positionMode, sizeMode))
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("secondaryTransform"));
            if (RuleNeedsTargetComponent(rule, shape, positionMode, sizeMode, condition, useStateColorMap.boolValue))
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("targetComponent"));

            EditorGUILayout.Space(4f);
            UtilityWindowTheme.SectionTitle("Position", UtilityWindowTheme.Cyan);
            EditorGUILayout.PropertyField(positionModeProperty);
            if (positionMode == PungentSceneGizmoSource.PositionMode.LocalOffset)
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("localOffset"));
            if (positionMode == PungentSceneGizmoSource.PositionMode.WorldPosition)
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("worldPosition"));
            if (positionMode == PungentSceneGizmoSource.PositionMode.FieldVector3)
                DrawPathField(rule, "positionFieldPath", "Position Field", typeof(Vector3));

            EditorGUILayout.Space(4f);
            UtilityWindowTheme.SectionTitle("Size", UtilityWindowTheme.Teal);
            EditorGUILayout.PropertyField(sizeModeProperty);
            if (sizeMode == PungentSceneGizmoSource.SizeMode.Constant)
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("size"));
            if (sizeMode == PungentSceneGizmoSource.SizeMode.FieldFloat || sizeMode == PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude)
                DrawPathField(rule, "sizeFieldPath", "Size Field", null);
            if (PungentSceneGizmoRuleEditorUtility.ShapeUsesVectorSize(shape))
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("vectorSize"));

            if (PungentSceneGizmoRuleEditorUtility.ShapeUsesDirection(shape) || !string.IsNullOrWhiteSpace(rule.FindPropertyRelative("directionFieldPath").stringValue))
            {
                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle("Direction", UtilityWindowTheme.Green);
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("direction"));
                DrawPathField(rule, "directionFieldPath", "Direction Field", typeof(Vector3));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("useTargetRotation"));
            }

            if (PungentSceneGizmoRuleEditorUtility.ShapeCanShowLabel(shape))
            {
                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle("Label", UtilityWindowTheme.Purple);
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("label"));
                DrawPathField(rule, "labelFieldPath", "Label Field", null);
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("drawLabelsWhenSelectedOnly"));
            }

            EditorGUILayout.Space(4f);
            UtilityWindowTheme.SectionTitle("Conditions", UtilityWindowTheme.Amber);
            EditorGUILayout.PropertyField(conditionProperty);
            if (condition != PungentSceneGizmoSource.ConditionMode.Always)
            {
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("conditionComponent"));
                DrawPathField(rule, "conditionFieldPath", "Condition Field", null, "conditionComponent");
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("conditionExpectedValue"));
            }

            if (shape == PungentSceneGizmoSource.GizmoShape.StateLabel || useStateColorMap.boolValue)
            {
                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle("State Colour Map", UtilityWindowTheme.Purple);
                EditorGUILayout.PropertyField(useStateColorMap);
                if (useStateColorMap.boolValue)
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("stateColorMap"), true);
            }

            _showAdvancedRuleFields = EditorGUILayout.Foldout(_showAdvancedRuleFields, "Advanced Rule Fields", true);
            if (_showAdvancedRuleFields)
            {
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("targetComponent"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("secondaryComponent"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("localOffset"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("worldPosition"));
                DrawPathField(rule, "positionFieldPath", "Position Field", typeof(Vector3));
                DrawPathField(rule, "sizeFieldPath", "Size Field", null);
                DrawPathField(rule, "directionFieldPath", "Direction Field", typeof(Vector3));
                DrawPathField(rule, "labelFieldPath", "Label Field", null);
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxDrawDistance"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("drawLabelsWhenSelectedOnly"));
                EditorGUILayout.PropertyField(useStateColorMap);
                if (useStateColorMap.boolValue)
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("stateColorMap"), true);
                DrawPresetMetadata(rule);
            }
        }

        private void DrawComponentSetupActions(PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green, 0.10f, 0.04f, 6, 3)))
            {
                UtilityWindowTheme.SectionTitle("Provider Components", UtilityWindowTheme.Green, "Optional no-code visuals");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Beacon Component"))
                        AddComponentIfMissing<PungentSceneBeacon>(source.gameObject, "Add Scene Beacon");
                    if (GUILayout.Button("Add Collision Sensor Component"))
                        AddComponentIfMissing<PungentCollisionSensorGizmo>(source.gameObject, "Add Collision Sensor Gizmo");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Trigger Sensor Component"))
                        AddComponentIfMissing<PungentTriggerSensorGizmo>(source.gameObject, "Add Trigger Sensor Gizmo");
                    if (GUILayout.Button("Add Trajectory Visualizer Component"))
                        AddComponentIfMissing<PungentTrajectoryVisualizer>(source.gameObject, "Add Trajectory Visualizer");
                }
                if (GUILayout.Button("Open Design Workspace"))
                    PungentGizmoBrowserWindow.OpenDesign(source);
            }
        }

        private void DrawSourcePerformanceBox(PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            int enabledRules = source.EstimateEnabledRuleCount();
            int drawOps = source.EstimateDrawOperations();
            int labels = source.EstimateLabelCount();
            bool childBoundsNoCache = false;
            if (source.rules != null)
            {
                for (int i = 0; i < source.rules.Count; i++)
                {
                    PungentSceneGizmoSource.GizmoRule rule = source.rules[i];
                    if (rule != null && rule.enabled && rule.shape == PungentSceneGizmoSource.GizmoShape.ChildBounds && !source.cacheChildRendererBounds)
                    {
                        childBoundsNoCache = true;
                        break;
                    }
                }
            }

            if (enabledRules >= 25 || labels >= 20 || drawOps >= 80 || childBoundsNoCache)
            {
                string message = $"This source estimates {enabledRules} enabled rules, {labels} labels, and {drawOps} draw operations.";
                if (childBoundsNoCache)
                    message += " Child-bounds caching is disabled for at least one rule.";
                EditorGUILayout.HelpBox(message + " Consider Selected-only drawing, label limits, or child-bounds caching if Scene View becomes sluggish.", childBoundsNoCache || drawOps >= 80 ? MessageType.Warning : MessageType.Info);
            }
        }

        private static void AddComponentIfMissing<TComponent>(GameObject go, string undoName) where TComponent : Component
        {
            if (go == null)
                return;

            if (PungentSceneGizmoCommandService.AddProviderComponentToGameObject(go, undoName, out TComponent component, out bool _))
                EditorGUIUtility.PingObject(component);
        }

        private static bool RuleUsesTargetTransform(PungentSceneGizmoSource.GizmoShape shape, PungentSceneGizmoSource.PositionMode positionMode)
        {
            return positionMode == PungentSceneGizmoSource.PositionMode.Transform ||
                   positionMode == PungentSceneGizmoSource.PositionMode.LocalOffset ||
                   positionMode == PungentSceneGizmoSource.PositionMode.MidpointToSecondary ||
                   shape == PungentSceneGizmoSource.GizmoShape.ChildBounds;
        }

        private static bool RuleNeedsSecondaryTransform(
            PungentSceneGizmoSource.GizmoShape shape,
            PungentSceneGizmoSource.PositionMode positionMode,
            PungentSceneGizmoSource.SizeMode sizeMode)
        {
            return shape == PungentSceneGizmoSource.GizmoShape.DistanceBetween ||
                   positionMode == PungentSceneGizmoSource.PositionMode.SecondaryTransform ||
                   positionMode == PungentSceneGizmoSource.PositionMode.MidpointToSecondary ||
                   sizeMode == PungentSceneGizmoSource.SizeMode.DistanceToSecondary;
        }

        private static bool RuleNeedsTargetComponent(
            SerializedProperty rule,
            PungentSceneGizmoSource.GizmoShape shape,
            PungentSceneGizmoSource.PositionMode positionMode,
            PungentSceneGizmoSource.SizeMode sizeMode,
            PungentSceneGizmoSource.ConditionMode condition,
            bool stateMapEnabled)
        {
            return shape == PungentSceneGizmoSource.GizmoShape.ColliderBounds ||
                   positionMode == PungentSceneGizmoSource.PositionMode.FieldVector3 ||
                   sizeMode == PungentSceneGizmoSource.SizeMode.FieldFloat ||
                   sizeMode == PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude ||
                   condition != PungentSceneGizmoSource.ConditionMode.Always ||
                   stateMapEnabled ||
                   !string.IsNullOrWhiteSpace(rule.FindPropertyRelative("directionFieldPath").stringValue) ||
                   !string.IsNullOrWhiteSpace(rule.FindPropertyRelative("labelFieldPath").stringValue);
        }

        private void ShowPresetMenu(PungentSceneGizmoSource source)
        {
            GenericMenu menu = new GenericMenu();
            PungentSceneGizmoPresetLibrary.AddBuiltInPresetMenu(menu, preset => ApplyBuiltInPresetToSource(source, preset));
            menu.ShowAsContext();
        }

        private void ApplyBuiltInPresetToSource(PungentSceneGizmoSource source, PungentSceneGizmoBuiltInPreset builtIn)
        {
            if (source == null || builtIn == null)
                return;

            PungentSceneGizmoPreset preset = PungentSceneGizmoPresetLibrary.CreateTransientPreset(builtIn);
            if (preset == null)
                return;

            try
            {
                ApplyPresetToSource(source, preset, builtIn.DisplayName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        private void ApplyPresetToSource(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset, string displayName)
        {
            if (source == null || preset == null)
                return;

            if (PungentSceneGizmoCommandService.ApplyPresetToSource(source, preset, _replaceRulesWhenApplyingPreset, out PungentSceneGizmoPresetApplySummary summary))
                _lastPresetSummary = $"Applied '{displayName}'. {summary}";
            else
                _lastPresetSummary = $"Preset '{displayName}' made no changes.";
            serializedObject.Update();
            Repaint();
        }

        private static void DrawPresetStateChip(PungentSceneGizmoPresetDiffStatus status)
        {
            string label = string.IsNullOrWhiteSpace(status.stateLabel) ? status.state.ToString() : status.stateLabel;
            UtilityWindowTheme.CountPill(label, PresetStateTint(status.state), 82f);
        }

        private static Color PresetStateTint(PungentSceneGizmoPresetInstanceState state)
        {
            switch (state)
            {
                case PungentSceneGizmoPresetInstanceState.Base:
                    return UtilityWindowTheme.Green;
                case PungentSceneGizmoPresetInstanceState.Customized:
                case PungentSceneGizmoPresetInstanceState.Mixed:
                    return UtilityWindowTheme.Amber;
                case PungentSceneGizmoPresetInstanceState.Outdated:
                case PungentSceneGizmoPresetInstanceState.MissingPreset:
                    return UtilityWindowTheme.Red;
                case PungentSceneGizmoPresetInstanceState.Locked:
                    return UtilityWindowTheme.Purple;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static void DrawPresetMetadata(SerializedProperty rule)
        {
            SerializedProperty presetId = rule.FindPropertyRelative("presetId");
            SerializedProperty presetCategory = rule.FindPropertyRelative("presetCategory");
            if (presetId == null || presetCategory == null)
                return;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 2)))
            {
                EditorGUILayout.LabelField("Preset Metadata", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.PropertyField(presetId);
                EditorGUILayout.PropertyField(presetCategory);
            }
        }

        private void DrawCloneRuleAudit()
        {
            List<string> missing = PungentSceneGizmoSource.GetMissingCloneRuleSerializedFieldsForAudit();
            if (missing == null || missing.Count == 0)
                return;

            EditorGUILayout.HelpBox("CloneRule audit: serialized fields not copied: " + string.Join(", ", missing), MessageType.Error);
        }

        private void DrawPathField(SerializedProperty rule, string propertyName, string label, Type preferredType, string componentPropertyName = "targetComponent")
        {
            SerializedProperty path = rule.FindPropertyRelative(propertyName);
            SerializedProperty componentProperty = rule.FindPropertyRelative(componentPropertyName);
            Component component = componentProperty.objectReferenceValue as Component;
            using (new EditorGUILayout.HorizontalScope())
            {
                path.stringValue = EditorGUILayout.TextField(new GUIContent(label, "Reflected field or readable non-indexed property path."), path.stringValue);
                if (GUILayout.Button("Pick", GUILayout.Width(48f)))
                {
                    GenericMenu menu = new GenericMenu();
                    if (component == null)
                        menu.AddDisabledItem(new GUIContent("Assign a component first"));
                    else
                        PopulateMemberMenu(menu, component.GetType(), preferredType, path.serializedObject, path.propertyPath, path.stringValue);
                    menu.ShowAsContext();
                }
            }

            if (component != null && !string.IsNullOrWhiteSpace(path.stringValue))
            {
                PungentSceneGizmoSource.BindingValueKind expectedKind = preferredType == null
                    ? PungentSceneGizmoSource.BindingValueKind.Any
                    : PungentSceneGizmoSource.ExpectedKindForType(preferredType);

                if (!PungentSceneGizmoSource.TryResolveObjectValue(component, path.stringValue, expectedKind, out _, out string diagnostic))
                    EditorGUILayout.HelpBox(label + ": " + diagnostic, MessageType.Warning);
            }
        }

        private void PopulateMemberMenu(GenericMenu menu, Type type, Type preferredType, SerializedObject owner, string propertyPath, string currentValue)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in type.GetFields(flags).Where(f => IsSupported(f.FieldType, preferredType)).OrderBy(f => f.Name))
            {
                string name = field.Name;
                menu.AddItem(new GUIContent(name + " : " + field.FieldType.Name), string.Equals(currentValue, name), () => SetPickedMemberPath(owner, propertyPath, name));
            }
            foreach (PropertyInfo property in type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0 && IsSupported(p.PropertyType, preferredType)).OrderBy(p => p.Name))
            {
                string name = property.Name;
                menu.AddItem(new GUIContent(name + " : " + property.PropertyType.Name), string.Equals(currentValue, name), () => SetPickedMemberPath(owner, propertyPath, name));
            }
        }

        private void SetPickedMemberPath(SerializedObject owner, string propertyPath, string value)
        {
            if (owner == null || string.IsNullOrWhiteSpace(propertyPath))
                return;

            owner.Update();
            SerializedProperty property = owner.FindProperty(propertyPath);
            if (property == null)
                return;

            property.stringValue = value;
            owner.ApplyModifiedProperties();
            serializedObject.Update();
            Repaint();
        }

        private static bool IsSupported(Type type, Type preferredType)
        {
            if (preferredType != null)
                return type == preferredType;
            return type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(string) || type.IsEnum || type == typeof(Vector2) || type == typeof(Vector3) || typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
        private static void DrawEditorGizmos(PungentSceneGizmoSource source, GizmoType gizmoType)
        {
            if (source == null || !source.drawInScene || source.rules == null)
                return;

            bool selected = (gizmoType & GizmoType.Selected) != 0 || (gizmoType & GizmoType.Active) != 0;
            foreach (PungentSceneGizmoSource.GizmoRule rule in source.rules)
            {
                if (!source.ShouldDraw(rule, selected))
                    continue;

                Vector3 position = source.ResolvePosition(rule);
                SceneView sceneView = SceneView.currentDrawingSceneView;
                if (rule.maxDrawDistance > 0f && sceneView != null && sceneView.camera != null &&
                    Vector3.Distance(sceneView.camera.transform.position, position) > rule.maxDrawDistance)
                    continue;

                Vector3 direction = source.ResolveDirection(rule);
                float size = source.ResolveSize(rule);

                Color previous = Handles.color;
                Handles.color = source.ResolveRuleColor(rule);

                if (rule.shape == PungentSceneGizmoSource.GizmoShape.Disc)
                    Handles.DrawWireDisc(position, direction.sqrMagnitude > 0.001f ? direction : Vector3.up, size);
                else if (rule.shape == PungentSceneGizmoSource.GizmoShape.Arrow)
                    Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward), size, EventType.Repaint);
                else if (rule.shape == PungentSceneGizmoSource.GizmoShape.DistanceBetween && rule.secondaryTransform != null)
                    Handles.Label(position, source.ResolveLabel(rule));

                if (source.drawLabels && (!rule.drawLabelsWhenSelectedOnly || selected))
                {
                    string label = source.ResolveLabel(rule);
                    if (!string.IsNullOrWhiteSpace(label) && PungentSceneGizmoPerformancePolicy.TryConsumeLabel(selected))
                        Handles.Label(position + Vector3.up * (HandleUtility.GetHandleSize(position) * 0.08f + 0.1f), label);
                }

                Handles.color = previous;
            }
        }
    }
    #endif

}
