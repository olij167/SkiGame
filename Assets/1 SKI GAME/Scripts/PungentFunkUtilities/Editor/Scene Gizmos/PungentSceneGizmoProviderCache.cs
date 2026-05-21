using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [InitializeOnLoad]
    public static class PungentSceneGizmoProviderCache
    {
        public sealed class ProviderRecord
        {
            public MonoBehaviour behaviour;
            public Component component;
            public GameObject gameObject;
            public int instanceId;
            public bool active;
            public bool visible;
            public bool selectedOnly;
            public bool alwaysVisible;
            public bool isBeacon;
            public bool isCollisionSensor;
            public bool isTriggerSensor;
            public bool isTrajectory;
            public bool isSpatial;
            public bool invalid;
            public string warning;
            public string providerCategory;
            public string searchText;
            public int estimatedDrawOperations;
            public int estimatedLabels;
            public int estimatedTrajectorySamples;
            public int priority;
        }

        public struct Summary
        {
            public int providerComponentCount;
            public int activeProviderCount;
            public int beaconCount;
            public int collisionSensorCount;
            public int triggerSensorCount;
            public int trajectoryCount;
            public int spatialCount;
            public int estimatedDrawOperations;
            public int estimatedLabels;
            public int estimatedTrajectorySamples;
            public int providerExceptionCount;
            public int invalidProviderCount;
            public string status;
        }

        private static readonly List<ProviderRecord> Records = new List<ProviderRecord>();
        private static bool _dirty = true;
        private static Summary _summary;

        static PungentSceneGizmoProviderCache()
        {
            EditorApplication.hierarchyChanged += MarkDirty;
            EditorApplication.playModeStateChanged += _ => MarkDirty();
            AssemblyReloadEvents.afterAssemblyReload += MarkDirty;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        public static void MarkDirty()
        {
            _dirty = true;
        }

        public static void RebuildIfDirty()
        {
            if (_dirty)
                ForceRebuild();
        }

        public static void ForceRebuild()
        {
            Records.Clear();
            Summary summary = new Summary();
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.gameObject == null)
                    continue;
                if (EditorUtility.IsPersistent(behaviour) || EditorUtility.IsPersistent(behaviour.gameObject))
                    continue;

                bool isBeacon = behaviour is IPungentSceneBeaconProvider;
                bool isCollision = behaviour is IPungentCollisionSensorProvider;
                bool isTrigger = behaviour is IPungentTriggerSensorProvider;
                bool isTrajectory = behaviour is IPungentTrajectoryProvider;
                bool isSpatial = behaviour is IPungentSpatialVisualizationProvider;
                bool isPerformance = behaviour is IPungentSceneGizmoPerformanceProvider;
                if (!isBeacon && !isCollision && !isTrigger && !isTrajectory && !isSpatial && !isPerformance)
                    continue;

                ProviderRecord record = BuildRecord(behaviour, isBeacon, isCollision, isTrigger, isTrajectory, isSpatial, isPerformance, ref summary);
                Records.Add(record);
            }

            Records.Sort(CompareRecords);
            summary.providerComponentCount = Records.Count;
            summary.status = $"Cached {Records.Count} provider component{(Records.Count == 1 ? string.Empty : "s")}.";
            _summary = summary;
            _dirty = false;
        }

        public static IReadOnlyList<ProviderRecord> GetProviders()
        {
            RebuildIfDirty();
            return Records;
        }

        public static Summary GetSummary()
        {
            RebuildIfDirty();
            return _summary;
        }

        private static ProviderRecord BuildRecord(
            MonoBehaviour behaviour,
            bool isBeacon,
            bool isCollision,
            bool isTrigger,
            bool isTrajectory,
            bool isSpatial,
            bool isPerformance,
            ref Summary summary)
        {
            ProviderRecord record = new ProviderRecord
            {
                behaviour = behaviour,
                component = behaviour,
                gameObject = behaviour.gameObject,
                instanceId = behaviour.GetInstanceID(),
                active = behaviour.isActiveAndEnabled && behaviour.gameObject.activeInHierarchy,
                visible = true,
                selectedOnly = true,
                isBeacon = isBeacon,
                isCollisionSensor = isCollision,
                isTriggerSensor = isTrigger,
                isTrajectory = isTrajectory,
                isSpatial = isSpatial,
                providerCategory = GetCategory(isBeacon, isCollision, isTrigger, isTrajectory, isSpatial),
                searchText = behaviour.name + " " + behaviour.GetType().Name
            };

            if (isBeacon)
                summary.beaconCount++;
            if (isCollision)
                summary.collisionSensorCount++;
            if (isTrigger)
                summary.triggerSensorCount++;
            if (isTrajectory)
                summary.trajectoryCount++;
            if (isSpatial)
            {
                summary.spatialCount++;
                ApplySpatialSnapshot(record, behaviour, ref summary);
            }

            if (isPerformance)
            {
                try
                {
                    if (((IPungentSceneGizmoPerformanceProvider)behaviour).TryGetPerformanceEstimate(out PungentSceneGizmoPerformanceEstimate estimate))
                    {
                        record.visible = estimate.drawInScene;
                        record.selectedOnly = estimate.selectedOnly;
                        record.alwaysVisible = estimate.alwaysVisible;
                        record.estimatedDrawOperations = Mathf.Max(0, estimate.estimatedDrawOperations);
                        record.estimatedLabels = Mathf.Max(0, estimate.estimatedLabels);
                        record.estimatedTrajectorySamples = Mathf.Max(0, estimate.estimatedTrajectorySamples);
                        record.priority = estimate.priority;
                        record.warning = estimate.warning;
                        if (!string.IsNullOrWhiteSpace(estimate.providerCategory))
                            record.providerCategory = estimate.providerCategory;
                    }
                }
                catch (Exception ex)
                {
                    record.invalid = true;
                    record.warning = "Performance estimate failed: " + ex.Message;
                    summary.providerExceptionCount++;
                }
            }

            if (!string.IsNullOrWhiteSpace(record.warning))
                record.invalid = true;
            if (record.invalid)
                summary.invalidProviderCount++;

            record.searchText = string.Concat(record.searchText, " ", record.providerCategory, " ", record.warning);
            if (record.active && record.visible)
            {
                summary.activeProviderCount++;
                summary.estimatedDrawOperations += record.estimatedDrawOperations;
                summary.estimatedLabels += record.estimatedLabels;
                summary.estimatedTrajectorySamples += record.estimatedTrajectorySamples;
            }

            return record;
        }

        private static void ApplySpatialSnapshot(ProviderRecord record, MonoBehaviour behaviour, ref Summary summary)
        {
            try
            {
                if (!(behaviour is IPungentSpatialVisualizationProvider spatialProvider) ||
                    !spatialProvider.TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot))
                    return;

                record.estimatedDrawOperations = Mathf.Max(record.estimatedDrawOperations, snapshot.EstimatedDrawCost);
                record.estimatedLabels = Mathf.Max(record.estimatedLabels, snapshot.PointCount > 0 ? 1 : 0);
                record.priority = Mathf.Max(record.priority, snapshot.Kind == PungentSpatialVisualizationKind.Area ? 10 : 8);
                record.providerCategory = snapshot.Kind == PungentSpatialVisualizationKind.Area ? "Spatial Area" : "Spatial Path";
                if (snapshot.HasWarnings && string.IsNullOrWhiteSpace(record.warning))
                    record.warning = snapshot.Kind + " has elevated draw or validation cost.";

                if (behaviour is ModularPathSpawner path)
                {
                    record.visible = path.drawGizmos;
                    record.selectedOnly = !path.drawUnselectedGizmo;
                    record.alwaysVisible = path.drawGizmos && path.drawUnselectedGizmo;
                }
                else if (behaviour is PungentAreaAuthoringShape area)
                {
                    record.visible = area.drawGizmo;
                    record.selectedOnly = !area.drawUnselectedGizmo;
                    record.alwaysVisible = area.drawGizmo && area.drawUnselectedGizmo;
                }
            }
            catch (Exception ex)
            {
                record.invalid = true;
                record.warning = "Spatial snapshot failed: " + ex.Message;
                summary.providerExceptionCount++;
            }
        }

        private static string GetCategory(bool beacon, bool collision, bool trigger, bool trajectory, bool spatial)
        {
            if (beacon)
                return "Beacon";
            if (collision)
                return "Collision Sensor";
            if (trigger)
                return "Trigger Sensor";
            if (trajectory)
                return "Trajectory";
            if (spatial)
                return "Spatial";
            return "Provider";
        }

        private static int CompareRecords(ProviderRecord a, ProviderRecord b)
        {
            int priority = b.priority.CompareTo(a.priority);
            if (priority != 0)
                return priority;

            return string.Compare(a.gameObject != null ? a.gameObject.name : string.Empty, b.gameObject != null ? b.gameObject.name : string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            MarkDirty();
        }

        private static void OnSceneClosed(Scene scene)
        {
            MarkDirty();
        }
    }
#endif
}
