using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public static class PungentSceneGizmoCommandService
    {
        public static bool ApplyBuiltInPresetToSelection(string presetId, bool replaceExistingRules, out PungentSceneGizmoPresetApplySummary summary)
        {
            summary = default;
            PungentSceneGizmoBuiltInPreset builtIn = PungentSceneGizmoPresetLibrary.Find(presetId);
            if (builtIn == null)
                return false;

            PungentSceneGizmoPreset preset = PungentSceneGizmoPresetLibrary.CreateTransientPreset(builtIn);
            if (preset == null)
                return false;

            try
            {
                return ApplyPresetToSelection(preset, replaceExistingRules, out summary);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        public static bool ApplyPresetToSelection(PungentSceneGizmoPreset preset, bool replaceExistingRules, out PungentSceneGizmoPresetApplySummary summary)
        {
            summary = default;
            GameObject[] targets = Selection.gameObjects;
            if (preset == null || targets == null || targets.Length == 0)
                return false;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Scene Gizmo Preset");

            summary.targetCount = targets.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject go = targets[i];
                if (go == null || EditorUtility.IsPersistent(go))
                {
                    summary.skippedTargets++;
                    continue;
                }

                PungentSceneGizmoSource source = go.GetComponent<PungentSceneGizmoSource>();
                bool addedSource = source == null;
                if (source == null)
                {
                    source = Undo.AddComponent<PungentSceneGizmoSource>(go);
                    summary.sourcesAdded++;
                }
                else
                {
                    summary.sourcesUpdated++;
                }

                ApplyPresetToSourceInternal(source, preset, replaceExistingRules, addedSource, ref summary);
            }

            Undo.CollapseUndoOperations(group);
            RefreshSceneGizmoCaches();
            return summary.rulesAdded > 0 || summary.sourcesAdded > 0 || summary.sourcesUpdated > 0;
        }

        public static bool ApplyPresetToSource(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset, bool replaceExistingRules, out PungentSceneGizmoPresetApplySummary summary)
        {
            summary = default;
            if (source == null || preset == null)
                return false;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Scene Gizmo Preset");
            summary.targetCount = 1;
            summary.sourcesUpdated = 1;
            ApplyPresetToSourceInternal(source, preset, replaceExistingRules, false, ref summary);
            Undo.CollapseUndoOperations(group);
            RefreshSceneGizmoCaches();
            return summary.rulesAdded > 0 || summary.rulesReplaced > 0 || summary.sourcesUpdated > 0;
        }

        public static bool UpdateSourceFromLinkedPreset(PungentSceneGizmoSource source, out PungentSceneGizmoPresetApplySummary summary)
        {
            summary = default;
            if (source == null || source.lockPresetPropagation)
                return false;

            return ApplyResolvedLinkedPreset(source, "Update Scene Gizmo From Preset", out summary);
        }

        public static bool RevertSourceToLinkedPreset(PungentSceneGizmoSource source, out PungentSceneGizmoPresetApplySummary summary)
        {
            return ApplyResolvedLinkedPreset(source, "Revert Scene Gizmo To Preset", out summary);
        }

        public static bool UpdateOpenSceneLinkedInstances(PungentSceneGizmoPreset preset, IEnumerable<PungentSceneGizmoSource> candidateSources, bool includeLocked, out PungentSceneGizmoPresetApplySummary summary)
        {
            summary = default;
            if (preset == null)
                return false;

            List<PungentSceneGizmoSource> sources = candidateSources != null
                ? candidateSources.Where(s => s != null).Distinct().ToList()
                : FindOpenSceneSourcesLinkedTo(preset);

            if (sources.Count == 0)
                return false;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update Linked Scene Gizmo Sources");
            summary.targetCount = sources.Count;

            for (int i = 0; i < sources.Count; i++)
            {
                PungentSceneGizmoSource source = sources[i];
                if (source == null || EditorUtility.IsPersistent(source.gameObject) || !IsSourceLinkedToPreset(source, preset))
                {
                    summary.skippedTargets++;
                    continue;
                }

                if (source.lockPresetPropagation && !includeLocked)
                {
                    summary.skippedTargets++;
                    continue;
                }

                summary.sourcesUpdated++;
                ApplyPresetToSourceInternal(source, preset, true, false, ref summary);
            }

            Undo.CollapseUndoOperations(group);
            RefreshSceneGizmoCaches();
            return summary.rulesAdded > 0 || summary.rulesReplaced > 0 || summary.sourcesUpdated > 0;
        }

        public static bool SaveSourceAsNewPreset(PungentSceneGizmoSource source, out PungentSceneGizmoPreset preset)
        {
            preset = null;
            if (source == null)
                return false;

            if (!PungentSceneGizmoPresetActions.CreatePresetAssetFromSource(source, out preset) || preset == null)
                return false;

            Undo.RecordObject(source, "Link Scene Gizmo Preset");
            LinkSourceToPreset(source, preset);
            EditorUtility.SetDirty(source);
            RefreshSceneGizmoCaches();
            return true;
        }

        public static bool OverwriteLinkedPreset(PungentSceneGizmoSource source, out PungentSceneGizmoPreset preset)
        {
            preset = null;
            if (source == null)
                return false;

            if (!PungentSceneGizmoPresetDiffUtility.TryResolveLinkedPreset(source, out PungentSceneGizmoPreset resolved, out bool destroyWhenDone))
                return false;

            try
            {
                if (!PungentSceneGizmoPresetDiffUtility.IsPersistentPreset(resolved))
                    return false;

                preset = resolved;
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Overwrite Scene Gizmo Preset");
                Undo.RecordObject(preset, "Overwrite Scene Gizmo Preset");
                Undo.RecordObject(source, "Refresh Scene Gizmo Preset Link");

                preset.drawInScene = source.drawInScene;
                preset.drawLabels = source.drawLabels;
                preset.drawOnlyWhenComponentEnabled = source.drawOnlyWhenComponentEnabled;
                if (preset.rules == null)
                    preset.rules = new List<PungentSceneGizmoSource.GizmoRule>();
                preset.rules.Clear();

                if (source.rules != null)
                {
                    for (int i = 0; i < source.rules.Count; i++)
                    {
                        PungentSceneGizmoSource.GizmoRule copy = PungentSceneGizmoSource.CloneRule(source.rules[i]);
                        if (copy == null)
                            continue;

                        StripSceneReferencesForPreset(copy);
                        copy.presetId = PungentSceneGizmoPresetDiffUtility.PresetId(preset);
                        copy.presetCategory = preset.category;
                        preset.rules.Add(copy);
                    }
                }

                EditorUtility.SetDirty(preset);
                LinkSourceToPreset(source, preset);
                EditorUtility.SetDirty(source);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                RefreshSceneGizmoCaches();
                return true;
            }
            finally
            {
                if (destroyWhenDone && resolved != null)
                    UnityEngine.Object.DestroyImmediate(resolved);
            }
        }

        public static void LinkSourceToPreset(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset)
        {
            if (source == null || preset == null)
                return;

            bool persistent = PungentSceneGizmoPresetDiffUtility.IsPersistentPreset(preset);
            source.linkedPreset = persistent ? preset : null;
            source.linkedPresetGuid = persistent ? PungentSceneGizmoPresetDiffUtility.GetPresetGuid(preset) : string.Empty;
            source.linkedPresetId = PungentSceneGizmoPresetDiffUtility.PresetId(preset);
            source.linkedPresetHash = PungentSceneGizmoPresetDiffUtility.ComputePresetHash(preset);
        }

        public static void ClearPresetLink(PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            Undo.RecordObject(source, "Clear Scene Gizmo Preset Link");
            source.linkedPreset = null;
            source.linkedPresetGuid = string.Empty;
            source.linkedPresetId = string.Empty;
            source.linkedPresetHash = string.Empty;
            EditorUtility.SetDirty(source);
            RefreshSceneGizmoCaches();
        }

        public static bool SetPresetLock(PungentSceneGizmoSource source, bool locked)
        {
            if (source == null || source.lockPresetPropagation == locked)
                return false;

            Undo.RecordObject(source, locked ? "Lock Scene Gizmo Preset Link" : "Unlock Scene Gizmo Preset Link");
            source.lockPresetPropagation = locked;
            EditorUtility.SetDirty(source);
            RefreshSceneGizmoCaches();
            return true;
        }

        public static bool DuplicateRule(PungentSceneGizmoSource source, int ruleIndex, out int newIndex)
        {
            newIndex = -1;
            if (!IsValidRuleIndex(source, ruleIndex))
                return false;

            PungentSceneGizmoSource.GizmoRule copy = PungentSceneGizmoSource.CloneRule(source.rules[ruleIndex]);
            if (copy == null)
                return false;

            Undo.RecordObject(source, "Duplicate Scene Gizmo Rule");
            if (!string.IsNullOrWhiteSpace(copy.name))
                copy.name += " Copy";
            newIndex = Mathf.Clamp(ruleIndex + 1, 0, source.rules.Count);
            source.rules.Insert(newIndex, copy);
            MarkSourceRulesChanged(source);
            return true;
        }

        public static bool MoveRule(PungentSceneGizmoSource source, int fromIndex, int toIndex)
        {
            if (!IsValidRuleIndex(source, fromIndex) || source.rules == null)
                return false;

            int clampedTo = Mathf.Clamp(toIndex, 0, source.rules.Count - 1);
            if (fromIndex == clampedTo)
                return false;

            Undo.RecordObject(source, "Move Scene Gizmo Rule");
            PungentSceneGizmoSource.GizmoRule rule = source.rules[fromIndex];
            source.rules.RemoveAt(fromIndex);
            source.rules.Insert(clampedTo, rule);
            MarkSourceRulesChanged(source);
            return true;
        }

        public static bool RemoveRule(PungentSceneGizmoSource source, int ruleIndex)
        {
            if (!IsValidRuleIndex(source, ruleIndex))
                return false;

            Undo.RecordObject(source, "Remove Scene Gizmo Rule");
            source.rules.RemoveAt(ruleIndex);
            MarkSourceRulesChanged(source);
            return true;
        }

        public static bool SetRuleEnabled(PungentSceneGizmoSource source, int ruleIndex, bool enabled)
        {
            if (!IsValidRuleIndex(source, ruleIndex) || source.rules[ruleIndex].enabled == enabled)
                return false;

            Undo.RecordObject(source, enabled ? "Enable Scene Gizmo Rule" : "Disable Scene Gizmo Rule");
            source.rules[ruleIndex].enabled = enabled;
            MarkSourceRulesChanged(source);
            return true;
        }

        public static bool AddSourceToSelection(out PungentSceneGizmoComponentActionSummary summary)
        {
            return AddComponentToSelection<PungentSceneGizmoSource>("Add Scene Gizmo Source", out summary);
        }

        public static bool AddProviderComponentToSelection<TComponent>(string undoName, out PungentSceneGizmoComponentActionSummary summary)
            where TComponent : Component
        {
            return AddComponentToSelection<TComponent>(undoName, out summary);
        }

        public static bool AddProviderComponentToGameObject<TComponent>(GameObject go, string undoName, out TComponent component, out bool added)
            where TComponent : Component
        {
            component = null;
            added = false;
            if (go == null || EditorUtility.IsPersistent(go))
                return false;

            component = go.GetComponent<TComponent>();
            if (component != null)
                return true;

            Undo.SetCurrentGroupName(undoName);
            component = Undo.AddComponent<TComponent>(go);
            if (component == null)
                return false;

            added = true;
            EditorUtility.SetDirty(component);
            RefreshSceneGizmoCaches();
            return true;
        }

        public static bool SetDrawState(IEnumerable<Component> components, bool drawInScene, out PungentSceneGizmoComponentActionSummary summary)
        {
            summary = default;
            if (components == null)
                return false;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(drawInScene ? "Show Scene Gizmos" : "Hide Scene Gizmos");

            foreach (Component component in components)
            {
                summary.targetCount++;
                if (component == null)
                {
                    summary.skippedCount++;
                    continue;
                }

                if (TrySetDrawInScene(component, drawInScene))
                {
                    summary.changedCount++;
                }
                else
                {
                    summary.skippedCount++;
                }
            }

            Undo.CollapseUndoOperations(group);
            RefreshSceneGizmoCaches();
            return summary.changedCount > 0;
        }

        private static bool ApplyResolvedLinkedPreset(PungentSceneGizmoSource source, string undoName, out PungentSceneGizmoPresetApplySummary summary)
        {
            summary = default;
            if (source == null)
                return false;

            if (!PungentSceneGizmoPresetDiffUtility.TryResolveLinkedPreset(source, out PungentSceneGizmoPreset preset, out bool destroyWhenDone))
                return false;

            try
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                summary.targetCount = 1;
                summary.sourcesUpdated = 1;
                ApplyPresetToSourceInternal(source, preset, true, false, ref summary);
                Undo.CollapseUndoOperations(group);
                RefreshSceneGizmoCaches();
                return summary.rulesAdded > 0 || summary.rulesReplaced > 0 || summary.sourcesUpdated > 0;
            }
            finally
            {
                if (destroyWhenDone && preset != null)
                    UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        private static bool IsValidRuleIndex(PungentSceneGizmoSource source, int ruleIndex)
        {
            return source != null &&
                   source.rules != null &&
                   ruleIndex >= 0 &&
                   ruleIndex < source.rules.Count &&
                   source.rules[ruleIndex] != null;
        }

        private static void MarkSourceRulesChanged(PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            source.InvalidateChildBoundsCache();
            EditorUtility.SetDirty(source);
            RefreshSceneGizmoCaches();
        }

        private static void ApplyPresetToSourceInternal(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset, bool replaceExistingRules, bool addedSource, ref PungentSceneGizmoPresetApplySummary summary)
        {
            if (source == null || preset == null)
                return;

            Undo.RecordObject(source, "Apply Scene Gizmo Preset");
            if (source.rules == null)
                source.rules = new List<PungentSceneGizmoSource.GizmoRule>();

            bool emptyBefore = source.rules.Count == 0;
            bool replace = addedSource || emptyBefore || replaceExistingRules || preset.replaceExistingRules;
            int replaced = 0;
            if (replace)
            {
                replaced = source.rules.Count;
                source.rules.Clear();
            }

            List<PungentSceneGizmoSource.GizmoRule> newRules = preset.CreateRuleCopies();
            for (int r = 0; r < newRules.Count; r++)
            {
                PungentSceneGizmoSource.GizmoRule rule = newRules[r];
                if (rule == null)
                    continue;

                PungentSceneGizmoPresetActions.ApplyTargetDefaults(source.gameObject, rule);
                source.rules.Add(rule);
                summary.rulesAdded++;
            }

            source.drawInScene = preset.drawInScene;
            source.drawLabels = preset.drawLabels;
            source.drawOnlyWhenComponentEnabled = preset.drawOnlyWhenComponentEnabled;
            if (replace)
                LinkSourceToPreset(source, preset);
            source.InvalidateChildBoundsCache();
            summary.rulesReplaced += replaced;
            EditorUtility.SetDirty(source);
        }

        private static bool AddComponentToSelection<TComponent>(string undoName, out PungentSceneGizmoComponentActionSummary summary)
            where TComponent : Component
        {
            summary = default;
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0)
                return false;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            summary.targetCount = targets.Length;

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
                if (component == null)
                {
                    summary.skippedCount++;
                    continue;
                }

                EditorUtility.SetDirty(component);
                summary.addedCount++;
                summary.changedCount++;
            }

            Undo.CollapseUndoOperations(group);
            RefreshSceneGizmoCaches();
            return summary.addedCount > 0;
        }

        private static List<PungentSceneGizmoSource> FindOpenSceneSourcesLinkedTo(PungentSceneGizmoPreset preset)
        {
            List<PungentSceneGizmoSource> sources = new List<PungentSceneGizmoSource>();
            PungentSceneGizmoSource[] allSources = Resources.FindObjectsOfTypeAll<PungentSceneGizmoSource>();
            for (int i = 0; i < allSources.Length; i++)
            {
                PungentSceneGizmoSource source = allSources[i];
                if (source == null || source.gameObject == null || EditorUtility.IsPersistent(source.gameObject))
                    continue;

                if (IsSourceLinkedToPreset(source, preset))
                    sources.Add(source);
            }

            return sources;
        }

        private static bool IsSourceLinkedToPreset(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset)
        {
            if (source == null || preset == null)
                return false;

            if (source.linkedPreset == preset)
                return true;

            string presetId = PungentSceneGizmoPresetDiffUtility.PresetId(preset);
            if (!string.IsNullOrWhiteSpace(presetId) && string.Equals(source.linkedPresetId, presetId, StringComparison.OrdinalIgnoreCase))
                return true;

            string guid = PungentSceneGizmoPresetDiffUtility.GetPresetGuid(preset);
            return !string.IsNullOrWhiteSpace(guid) && string.Equals(source.linkedPresetGuid, guid, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySetDrawInScene(Component component, bool drawInScene)
        {
            if (component is PungentSceneGizmoSource source)
            {
                if (source.drawInScene == drawInScene)
                    return false;
                Undo.RecordObject(source, drawInScene ? "Show Scene Gizmo Source" : "Hide Scene Gizmo Source");
                source.drawInScene = drawInScene;
                EditorUtility.SetDirty(source);
                return true;
            }

            if (component is PungentSceneBeacon beacon)
            {
                if (beacon.drawInScene == drawInScene)
                    return false;
                Undo.RecordObject(beacon, drawInScene ? "Show Scene Beacon" : "Hide Scene Beacon");
                beacon.drawInScene = drawInScene;
                EditorUtility.SetDirty(beacon);
                return true;
            }

            if (component is PungentCollisionSensorGizmo collision)
            {
                if (collision.drawInScene == drawInScene)
                    return false;
                Undo.RecordObject(collision, drawInScene ? "Show Collision Sensor" : "Hide Collision Sensor");
                collision.drawInScene = drawInScene;
                EditorUtility.SetDirty(collision);
                return true;
            }

            if (component is PungentTriggerSensorGizmo trigger)
            {
                if (trigger.drawInScene == drawInScene)
                    return false;
                Undo.RecordObject(trigger, drawInScene ? "Show Trigger Sensor" : "Hide Trigger Sensor");
                trigger.drawInScene = drawInScene;
                EditorUtility.SetDirty(trigger);
                return true;
            }

            if (component is PungentTrajectoryVisualizer trajectory)
            {
                if (trajectory.drawInScene == drawInScene)
                    return false;
                Undo.RecordObject(trajectory, drawInScene ? "Show Trajectory Visualizer" : "Hide Trajectory Visualizer");
                trajectory.drawInScene = drawInScene;
                EditorUtility.SetDirty(trajectory);
                return true;
            }

            if (component is ModularPathSpawner path)
            {
                if (path.drawGizmos == drawInScene)
                    return false;
                Undo.RecordObject(path, drawInScene ? "Show Spatial Path" : "Hide Spatial Path");
                path.drawGizmos = drawInScene;
                EditorUtility.SetDirty(path);
                return true;
            }

            if (component is PungentAreaAuthoringShape area)
            {
                if (area.drawGizmo == drawInScene)
                    return false;
                Undo.RecordObject(area, drawInScene ? "Show Spatial Area" : "Hide Spatial Area");
                area.drawGizmo = drawInScene;
                EditorUtility.SetDirty(area);
                return true;
            }

            return false;
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

        private static void RefreshSceneGizmoCaches()
        {
            PungentSceneGizmoProviderCache.MarkDirty();
            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
        }
    }
#endif
}
