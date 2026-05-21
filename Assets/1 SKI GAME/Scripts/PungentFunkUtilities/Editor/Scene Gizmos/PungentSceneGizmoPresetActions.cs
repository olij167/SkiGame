using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public struct PungentSceneGizmoPresetApplySummary
    {
        public int targetCount;
        public int sourcesAdded;
        public int sourcesUpdated;
        public int rulesAdded;
        public int rulesReplaced;
        public int skippedTargets;

        public override string ToString()
        {
            return $"Targets {targetCount}, sources added {sourcesAdded}, sources updated {sourcesUpdated}, rules added {rulesAdded}, rules replaced {rulesReplaced}, skipped {skippedTargets}.";
        }
    }

    public struct PungentSceneGizmoComponentActionSummary
    {
        public int targetCount;
        public int changedCount;
        public int addedCount;
        public int skippedCount;
        public int pingedCount;
        public int disabledCount;

        public override string ToString()
        {
            return $"Targets {targetCount}, changed {changedCount}, added {addedCount}, disabled {disabledCount}, pinged {pingedCount}, skipped {skippedCount}.";
        }
    }

    public static class PungentSceneGizmoPresetActions
    {
        public static bool ApplyBuiltInPresetToSelection(string presetId, bool replaceExistingRules, out PungentSceneGizmoPresetApplySummary summary)
        {
            return PungentSceneGizmoCommandService.ApplyBuiltInPresetToSelection(presetId, replaceExistingRules, out summary);
        }

        public static bool ApplyPresetToSelection(PungentSceneGizmoPreset preset, bool replaceExistingRules, out PungentSceneGizmoPresetApplySummary summary)
        {
            return PungentSceneGizmoCommandService.ApplyPresetToSelection(preset, replaceExistingRules, out summary);
        }

        public static void ApplyTargetDefaults(GameObject go, PungentSceneGizmoSource.GizmoRule rule)
        {
            if (go == null || rule == null)
                return;

            if (rule.targetTransform == null)
                rule.targetTransform = go.transform;

            if (rule.shape == PungentSceneGizmoSource.GizmoShape.ColliderBounds && rule.targetComponent == null)
                rule.targetComponent = go.GetComponent<Collider>();

            if (rule.useStateColorMap && rule.stateColorMap != null && rule.stateColorMap.entries != null)
            {
                Component fallbackComponent = FirstNonGizmoComponent(go);
                for (int i = 0; i < rule.stateColorMap.entries.Count; i++)
                {
                    PungentSceneGizmoStateColorEntry entry = rule.stateColorMap.entries[i];
                    if (entry == null)
                        continue;

                    if (entry.component == null)
                        entry.component = fallbackComponent;

                    if (entry.animator == null)
                        entry.animator = go.GetComponent<Animator>();
                }
            }

            if (rule.targetComponent == null && NeedsLikelyComponent(rule))
                rule.targetComponent = FirstNonGizmoComponent(go);
        }

        public static bool AddBeaconToSelection(out PungentSceneGizmoComponentActionSummary summary)
        {
            return AddComponentToSelection<PungentSceneBeacon>("Add Scene Beacon", out summary);
        }

        public static bool AddCollisionSensorToSelection(out PungentSceneGizmoComponentActionSummary summary)
        {
            return AddComponentToSelection<PungentCollisionSensorGizmo>("Add Collision Sensor Gizmo", out summary);
        }

        public static bool AddTriggerSensorToSelection(out PungentSceneGizmoComponentActionSummary summary)
        {
            return AddComponentToSelection<PungentTriggerSensorGizmo>("Add Trigger Sensor Gizmo", out summary);
        }

        public static bool AddTrajectoryVisualizerToSelection(out PungentSceneGizmoComponentActionSummary summary)
        {
            return AddComponentToSelection<PungentTrajectoryVisualizer>("Add Trajectory Visualizer", out summary);
        }

        public static bool PingSelectedBeacons(out PungentSceneGizmoComponentActionSummary summary)
        {
            summary = default;
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0)
                return false;

            summary.targetCount = targets.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject go = targets[i];
                if (go == null || EditorUtility.IsPersistent(go))
                {
                    summary.skippedCount++;
                    continue;
                }

                PungentSceneBeacon[] beacons = go.GetComponents<PungentSceneBeacon>();
                if (beacons == null || beacons.Length == 0)
                {
                    summary.skippedCount++;
                    continue;
                }

                for (int b = 0; b < beacons.Length; b++)
                {
                    PungentSceneBeacon beacon = beacons[b];
                    if (beacon == null || !beacon.pingable)
                        continue;

                    Undo.RecordObject(beacon, "Ping Scene Beacon");
                    beacon.Ping();
                    EditorUtility.SetDirty(beacon);
                    summary.pingedCount++;
                    summary.changedCount++;
                }
            }

            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.pingedCount > 0;
        }

        public static bool PingSelectedBeacons(IEnumerable<PungentSceneBeacon> beacons, out PungentSceneGizmoComponentActionSummary summary)
        {
            summary = default;
            if (beacons == null)
                return false;

            foreach (PungentSceneBeacon beacon in beacons)
            {
                if (beacon == null || !beacon.pingable)
                {
                    summary.skippedCount++;
                    continue;
                }

                summary.targetCount++;
                Undo.RecordObject(beacon, "Ping Scene Beacon");
                beacon.Ping();
                EditorUtility.SetDirty(beacon);
                summary.pingedCount++;
                summary.changedCount++;
            }

            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.pingedCount > 0;
        }

        public static bool DisableTrajectories(out PungentSceneGizmoComponentActionSummary summary)
        {
            return DisableTrajectories(FindSelectionComponents<PungentTrajectoryVisualizer>(), out summary);
        }

        public static bool DisableTrajectories(IEnumerable<PungentTrajectoryVisualizer> trajectories, out PungentSceneGizmoComponentActionSummary summary)
        {
            summary = default;
            if (trajectories == null)
                return false;

            foreach (PungentTrajectoryVisualizer trajectory in trajectories)
            {
                if (trajectory == null)
                {
                    summary.skippedCount++;
                    continue;
                }

                summary.targetCount++;
                if (!trajectory.drawInScene)
                {
                    summary.skippedCount++;
                    continue;
                }

                Undo.RecordObject(trajectory, "Disable Scene Trajectory Visualizer");
                trajectory.drawInScene = false;
                EditorUtility.SetDirty(trajectory);
                summary.disabledCount++;
                summary.changedCount++;
            }

            PungentSceneGizmoProviderCache.MarkDirty();
            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.changedCount > 0;
        }

        public static bool DisableLabels(out PungentSceneGizmoComponentActionSummary summary)
        {
            List<Component> components = new List<Component>();
            components.AddRange(FindSelectionComponents<PungentSceneGizmoSource>());
            components.AddRange(FindSelectionComponents<PungentSceneBeacon>());
            components.AddRange(FindSelectionComponents<PungentCollisionSensorGizmo>());
            components.AddRange(FindSelectionComponents<PungentTriggerSensorGizmo>());
            components.AddRange(FindSelectionComponents<PungentTrajectoryVisualizer>());
            return DisableLabels(components, out summary);
        }

        public static bool DisableLabels(IEnumerable<Component> components, out PungentSceneGizmoComponentActionSummary summary)
        {
            summary = default;
            if (components == null)
                return false;

            foreach (Component component in components)
                DisableLabelsOnComponent(component, ref summary);

            PungentSceneGizmoProviderCache.MarkDirty();
            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.changedCount > 0;
        }

        public static bool DisableAlwaysVisibleBeacons(out PungentSceneGizmoComponentActionSummary summary)
        {
            return DisableAlwaysVisibleBeacons(FindSelectionComponents<PungentSceneBeacon>(), out summary);
        }

        public static bool DisableAlwaysVisibleBeacons(IEnumerable<PungentSceneBeacon> beacons, out PungentSceneGizmoComponentActionSummary summary)
        {
            summary = default;
            if (beacons == null)
                return false;

            foreach (PungentSceneBeacon beacon in beacons)
            {
                if (beacon == null)
                {
                    summary.skippedCount++;
                    continue;
                }

                summary.targetCount++;
                if (!beacon.alwaysVisible)
                {
                    summary.skippedCount++;
                    continue;
                }

                Undo.RecordObject(beacon, "Disable Always Visible Beacon");
                beacon.alwaysVisible = false;
                beacon.drawWhenSelectedOnly = true;
                EditorUtility.SetDirty(beacon);
                summary.disabledCount++;
                summary.changedCount++;
            }

            PungentSceneGizmoProviderCache.MarkDirty();
            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.changedCount > 0;
        }

        public static bool CreatePresetAssetFromSource(PungentSceneGizmoSource source, out PungentSceneGizmoPreset preset)
        {
            preset = null;
            if (source == null)
                return false;

            string defaultName = ObjectNames.NicifyVariableName(source.name) + " Gizmo Preset";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Scene Gizmo Preset",
                defaultName,
                "asset",
                "Choose where to save the Scene Gizmo preset asset.");

            if (string.IsNullOrWhiteSpace(path))
                return false;

            preset = ScriptableObject.CreateInstance<PungentSceneGizmoPreset>();
            preset.name = defaultName;
            preset.presetId = SanitizePresetId(defaultName);
            preset.displayName = defaultName;
            preset.category = "Custom";
            preset.description = "Created from " + source.name + ".";
            preset.replaceExistingRules = false;
            preset.drawInScene = source.drawInScene;
            preset.drawLabels = source.drawLabels;
            preset.drawOnlyWhenComponentEnabled = source.drawOnlyWhenComponentEnabled;

            if (source.rules != null)
            {
                for (int i = 0; i < source.rules.Count; i++)
                {
                    PungentSceneGizmoSource.GizmoRule copy = PungentSceneGizmoSource.CloneRule(source.rules[i]);
                    if (copy == null)
                        continue;

                    StripSceneReferencesForPreset(copy);
                    preset.rules.Add(copy);
                }
            }

            AssetDatabase.CreateAsset(preset, path);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = preset;
            return true;
        }

        private static void StripSceneReferencesForPreset(PungentSceneGizmoSource.GizmoRule rule)
        {
            if (rule == null)
                return;

            rule.targetComponent = null;
            rule.targetTransform = null;
            rule.secondaryComponent = null;
            rule.secondaryTransform = null;
            rule.conditionComponent = null;

            if (rule.stateColorMap == null || rule.stateColorMap.entries == null)
                return;

            for (int i = 0; i < rule.stateColorMap.entries.Count; i++)
            {
                PungentSceneGizmoStateColorEntry entry = rule.stateColorMap.entries[i];
                if (entry == null)
                    continue;

                entry.component = null;
                entry.animator = null;
            }
        }

        private static bool NeedsLikelyComponent(PungentSceneGizmoSource.GizmoRule rule)
        {
            if (rule == null)
                return false;

            return rule.positionMode == PungentSceneGizmoSource.PositionMode.FieldVector3 ||
                   rule.sizeMode == PungentSceneGizmoSource.SizeMode.FieldFloat ||
                   rule.sizeMode == PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude ||
                   !string.IsNullOrWhiteSpace(rule.directionFieldPath) ||
                   !string.IsNullOrWhiteSpace(rule.labelFieldPath) ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.BoolFieldTrue ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.BoolFieldFalse ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.Equals ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.NotEquals ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.GreaterThan ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.LessThan ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.ObjectReferenceExists ||
                   rule.condition == PungentSceneGizmoSource.ConditionMode.ObjectReferenceMissing;
        }

        private static Component FirstNonGizmoComponent(GameObject go)
        {
            if (go == null)
                return null;

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && !(component is Transform) && !(component is PungentSceneGizmoSource))
                    return component;
            }

            return null;
        }

        private static bool AddComponentToSelection<TComponent>(string undoName, out PungentSceneGizmoComponentActionSummary summary)
            where TComponent : Component
        {
            summary = default;
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0)
                return false;

            summary.targetCount = targets.Length;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);

            for (int i = 0; i < targets.Length; i++)
            {
                GameObject go = targets[i];
                if (go == null || EditorUtility.IsPersistent(go))
                {
                    summary.skippedCount++;
                    continue;
                }

                if (go.GetComponent<TComponent>() != null)
                {
                    summary.skippedCount++;
                    continue;
                }

                TComponent component = Undo.AddComponent<TComponent>(go);
                if (component != null)
                {
                    EditorUtility.SetDirty(component);
                    summary.addedCount++;
                    summary.changedCount++;
                }
            }

            Undo.CollapseUndoOperations(group);
            PungentSceneGizmoProviderCache.MarkDirty();
            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.addedCount > 0;
        }

        private static List<TComponent> FindSelectionComponents<TComponent>() where TComponent : Component
        {
            List<TComponent> components = new List<TComponent>();
            GameObject[] targets = Selection.gameObjects;
            if (targets == null)
                return components;

            for (int i = 0; i < targets.Length; i++)
            {
                GameObject go = targets[i];
                if (go == null || EditorUtility.IsPersistent(go))
                    continue;

                components.AddRange(go.GetComponents<TComponent>());
            }

            return components;
        }

        private static void DisableLabelsOnComponent(Component component, ref PungentSceneGizmoComponentActionSummary summary)
        {
            if (component == null)
            {
                summary.skippedCount++;
                return;
            }

            summary.targetCount++;

            if (component is PungentSceneGizmoSource source)
            {
                if (!source.drawLabels)
                {
                    summary.skippedCount++;
                    return;
                }

                Undo.RecordObject(source, "Disable Scene Gizmo Labels");
                source.drawLabels = false;
                EditorUtility.SetDirty(source);
                summary.disabledCount++;
                summary.changedCount++;
                return;
            }

            if (component is PungentSceneBeacon beacon)
            {
                if (!beacon.drawLabel && !beacon.drawDistanceToSceneCamera)
                {
                    summary.skippedCount++;
                    return;
                }

                Undo.RecordObject(beacon, "Disable Scene Beacon Labels");
                beacon.drawLabel = false;
                beacon.drawDistanceToSceneCamera = false;
                EditorUtility.SetDirty(beacon);
                summary.disabledCount++;
                summary.changedCount++;
                return;
            }

            if (component is PungentCollisionSensorGizmo collision)
            {
                if (!collision.drawContactLabels)
                {
                    summary.skippedCount++;
                    return;
                }

                Undo.RecordObject(collision, "Disable Collision Sensor Labels");
                collision.drawContactLabels = false;
                EditorUtility.SetDirty(collision);
                summary.disabledCount++;
                summary.changedCount++;
                return;
            }

            if (component is PungentTriggerSensorGizmo trigger)
            {
                if (!trigger.drawOverlapLabels)
                {
                    summary.skippedCount++;
                    return;
                }

                Undo.RecordObject(trigger, "Disable Trigger Sensor Labels");
                trigger.drawOverlapLabels = false;
                EditorUtility.SetDirty(trigger);
                summary.disabledCount++;
                summary.changedCount++;
                return;
            }

            if (component is PungentTrajectoryVisualizer trajectory)
            {
                if (!trajectory.drawLabels)
                {
                    summary.skippedCount++;
                    return;
                }

                Undo.RecordObject(trajectory, "Disable Trajectory Labels");
                trajectory.drawLabels = false;
                EditorUtility.SetDirty(trajectory);
                summary.disabledCount++;
                summary.changedCount++;
            }
        }

        private static string SanitizePresetId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "scene-gizmo-preset";

            string lower = input.Trim().ToLowerInvariant();
            char[] chars = lower.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '-';
            }

            string result = new string(chars);
            while (result.Contains("--"))
                result = result.Replace("--", "-");

            return result.Trim('-');
        }
    }
#endif
}
