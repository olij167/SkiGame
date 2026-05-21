using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentSceneGizmoProviderDrawer
    {
        private static readonly List<PungentTrajectorySample> TrajectorySamples = new List<PungentTrajectorySample>(PungentSceneGizmoPerformancePolicy.DefaultMaxTrajectorySamplesPerComponent);
        private static readonly List<Vector3> SpatialPoints = new List<Vector3>(256);
        private static double _nextAllowedSceneRepaintTime;

        static PungentSceneGizmoProviderDrawer()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            SceneView.duringSceneGui += DuringSceneGui;
        }

        public static void RequestActiveSceneRepaint()
        {
            PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.05d);
        }

        private static void DuringSceneGui(SceneView sceneView)
        {
            if (sceneView == null)
                return;

            PungentSceneGizmoProviderCache.RebuildIfDirty();
            IReadOnlyList<PungentSceneGizmoProviderCache.ProviderRecord> records = PungentSceneGizmoProviderCache.GetProviders();
            int drawBudget = PungentSceneGizmoPerformancePolicy.DefaultProviderDrawBudget;
            int labelsDrawn = 0;
            int pingsDrawn = 0;

            DrawPass(records, sceneView, selectedPass: true, ref drawBudget, ref labelsDrawn, ref pingsDrawn);
            DrawPass(records, sceneView, selectedPass: false, ref drawBudget, ref labelsDrawn, ref pingsDrawn);

            PungentSceneGizmoProviderCache.Summary summary = PungentSceneGizmoProviderCache.GetSummary();
            PungentSceneGizmoPerformancePolicy.MaybeLogPerformanceWarning(summary.activeProviderCount, summary.estimatedDrawOperations, PungentGizmoBrowserWindow.HasOpenInstances<PungentGizmoBrowserWindow>());
        }

        private static void DrawPass(
            IReadOnlyList<PungentSceneGizmoProviderCache.ProviderRecord> records,
            SceneView sceneView,
            bool selectedPass,
            ref int drawBudget,
            ref int labelsDrawn,
            ref int pingsDrawn)
        {
            if (records == null)
                return;

            for (int i = 0; i < records.Count && drawBudget > 0; i++)
            {
                PungentSceneGizmoProviderCache.ProviderRecord record = records[i];
                if (record == null || record.component == null)
                    continue;

                bool selected = IsSelected(record.component);
                if (selected != selectedPass)
                    continue;

                DrawRecord(record, sceneView, selected, ref drawBudget, ref labelsDrawn, ref pingsDrawn);
            }
        }

        private static void DrawRecord(
            PungentSceneGizmoProviderCache.ProviderRecord record,
            SceneView sceneView,
            bool selected,
            ref int drawBudget,
            ref int labelsDrawn,
            ref int pingsDrawn)
        {
            try
            {
                if (record.isBeacon && record.component is IPungentSceneBeaconProvider beaconProvider)
                {
                    DrawBeacon(beaconProvider, sceneView, selected, ref drawBudget, ref labelsDrawn, ref pingsDrawn);
                    return;
                }

                if (record.isCollisionSensor && record.component is IPungentCollisionSensorProvider collisionProvider)
                {
                    DrawCollisionSensor(collisionProvider, sceneView, selected, ref drawBudget, ref labelsDrawn);
                    return;
                }

                if (record.isTriggerSensor && record.component is IPungentTriggerSensorProvider triggerProvider)
                {
                    DrawTriggerSensor(triggerProvider, sceneView, selected, ref drawBudget, ref labelsDrawn);
                    return;
                }

                if (record.isTrajectory && record.component is IPungentTrajectoryProvider trajectoryProvider)
                {
                    DrawTrajectory(trajectoryProvider, sceneView, selected, ref drawBudget, ref labelsDrawn);
                    return;
                }

                if (record.isSpatial && record.component is IPungentSpatialVisualizationProvider spatialProvider)
                    DrawSpatial(spatialProvider, record, sceneView, selected, ref drawBudget, ref labelsDrawn);
            }
            catch (Exception ex)
            {
                if (PungentEditorPerformanceUtility.DiagnosticsEnabled)
                    Debug.LogWarning("[PungentFunk Utilities] Scene gizmo provider draw failed: " + ex.Message);
            }
        }

        private static void DrawBeacon(
            IPungentSceneBeaconProvider provider,
            SceneView sceneView,
            bool selected,
            ref int drawBudget,
            ref int labelsDrawn,
            ref int pingsDrawn)
        {
            if (!provider.TryGetBeaconSnapshot(out PungentSceneBeaconSnapshot snapshot) ||
                !PassCommon(snapshot.owner, snapshot.drawInScene, snapshot.drawWhenSelectedOnly, selected) ||
                !PassDistance(sceneView, snapshot.worldPosition, snapshot.maxDrawDistance))
                return;

            Color previous = Handles.color;
            Handles.color = snapshot.color;
            float handleSize = HandleUtility.GetHandleSize(snapshot.worldPosition);
            float markerSize = Mathf.Max(0.05f, handleSize * 0.045f);
            Handles.SphereHandleCap(0, snapshot.worldPosition, Quaternion.identity, markerSize, EventType.Repaint);
            drawBudget--;

            if (snapshot.drawRing && drawBudget > 0)
            {
                Handles.DrawWireDisc(snapshot.worldPosition, Vector3.up, Mathf.Max(0.01f, snapshot.radius));
                drawBudget--;
            }

            if (snapshot.drawVerticalLine && snapshot.verticalLineHeight > 0f && drawBudget > 0)
            {
                Handles.DrawLine(snapshot.worldPosition, snapshot.worldPosition + Vector3.up * snapshot.verticalLineHeight);
                drawBudget--;
            }

            if (snapshot.pingable && pingsDrawn < PungentSceneGizmoPerformancePolicy.MaxPingAnimations)
                DrawPing(snapshot, ref drawBudget, ref pingsDrawn);

            if (snapshot.drawLabel && !string.IsNullOrWhiteSpace(snapshot.label) && labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
                DrawLabel(snapshot.worldPosition, snapshot.label, selected, ref labelsDrawn);

            if (snapshot.drawDistanceToSceneCamera && sceneView.camera != null && labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
            {
                float distance = Vector3.Distance(sceneView.camera.transform.position, snapshot.worldPosition);
                DrawLabel(snapshot.worldPosition + Vector3.up * handleSize * 0.08f, distance.ToString("0.0") + "m", selected, ref labelsDrawn);
            }

            Handles.color = previous;
        }

        private static void DrawCollisionSensor(
            IPungentCollisionSensorProvider provider,
            SceneView sceneView,
            bool selected,
            ref int drawBudget,
            ref int labelsDrawn)
        {
            if (!provider.TryGetCollisionSensorSnapshot(out PungentCollisionSensorSnapshot snapshot) ||
                !PassCommon(snapshot.owner, snapshot.drawInScene, snapshot.drawWhenSelectedOnly, selected) ||
                !PassDistance(sceneView, snapshot.colliderBounds.center, snapshot.maxDrawDistance))
                return;

            Color previous = Handles.color;
            Handles.color = snapshot.currentCollisionCount > 0 ? snapshot.activeColor : snapshot.idleColor;
            if (snapshot.drawColliderBounds && snapshot.hasCollider && drawBudget > 0)
            {
                Handles.DrawWireCube(snapshot.colliderBounds.center, snapshot.colliderBounds.size);
                drawBudget--;
            }

            int count = Mathf.Min(snapshot.contactCount, snapshot.contacts != null ? snapshot.contacts.Length : 0);
            for (int i = 0; i < count && drawBudget > 0; i++)
            {
                PungentCollisionContactSnapshot contact = snapshot.contacts[i];
                if (snapshot.drawContactPoints)
                {
                    Handles.color = snapshot.contactColor;
                    Handles.SphereHandleCap(0, contact.point, Quaternion.identity, Mathf.Max(0.01f, snapshot.contactPointSize), EventType.Repaint);
                    drawBudget--;
                }

                if (snapshot.drawContactNormals && drawBudget > 0)
                {
                    Handles.color = snapshot.normalColor;
                    Handles.DrawLine(contact.point, contact.point + contact.normal.normalized * Mathf.Max(0.01f, snapshot.normalLength));
                    drawBudget--;
                }

                if (snapshot.drawContactLabels && labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
                    DrawLabel(contact.point, string.IsNullOrWhiteSpace(contact.otherObjectName) ? "Contact" : contact.otherObjectName, selected, ref labelsDrawn);
            }

            Handles.color = previous;
        }

        private static void DrawTriggerSensor(
            IPungentTriggerSensorProvider provider,
            SceneView sceneView,
            bool selected,
            ref int drawBudget,
            ref int labelsDrawn)
        {
            if (!provider.TryGetTriggerSensorSnapshot(out PungentTriggerSensorSnapshot snapshot) ||
                !PassCommon(snapshot.owner, snapshot.drawInScene, snapshot.drawWhenSelectedOnly, selected) ||
                !PassDistance(sceneView, snapshot.triggerBounds.center, snapshot.maxDrawDistance))
                return;

            Color previous = Handles.color;
            Handles.color = snapshot.overlapCount > 0 ? snapshot.activeColor : snapshot.idleColor;
            if (snapshot.drawTriggerBounds && snapshot.hasCollider && drawBudget > 0)
            {
                Handles.DrawWireCube(snapshot.triggerBounds.center, snapshot.triggerBounds.size);
                drawBudget--;
            }

            int overlapCount = Mathf.Min(snapshot.overlapCount, snapshot.overlaps != null ? snapshot.overlaps.Length : 0);
            for (int i = 0; i < overlapCount && drawBudget > 0; i++)
            {
                PungentTriggerOverlapSnapshot overlap = snapshot.overlaps[i];
                Handles.color = snapshot.overlapColor;
                if (snapshot.drawOverlapLinks)
                {
                    Handles.DrawLine(snapshot.triggerBounds.center, overlap.position);
                    drawBudget--;
                }

                if (snapshot.drawOverlapLabels && labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
                    DrawLabel(overlap.position, string.IsNullOrWhiteSpace(overlap.objectName) ? "Overlap" : overlap.objectName, selected, ref labelsDrawn);
            }

            if (snapshot.drawRecentExits)
            {
                Handles.color = snapshot.recentExitColor;
                int exitCount = Mathf.Min(snapshot.recentExitCount, snapshot.recentExits != null ? snapshot.recentExits.Length : 0);
                for (int i = 0; i < exitCount && drawBudget > 0; i++)
                {
                    Handles.DrawWireCube(snapshot.recentExits[i].bounds.center, snapshot.recentExits[i].bounds.size);
                    drawBudget--;
                }
            }

            Handles.color = previous;
        }

        private static void DrawTrajectory(
            IPungentTrajectoryProvider provider,
            SceneView sceneView,
            bool selected,
            ref int drawBudget,
            ref int labelsDrawn)
        {
            TrajectorySamples.Clear();
            if (!provider.TryGetTrajectorySnapshot(TrajectorySamples, out PungentTrajectorySnapshot snapshot) ||
                !PassCommon(snapshot.owner, snapshot.drawInScene, snapshot.drawWhenSelectedOnly, selected) ||
                !PassDistance(sceneView, snapshot.origin, snapshot.maxDrawDistance))
                return;

            Color previous = Handles.color;
            Handles.color = snapshot.trajectoryColor;
            int count = Mathf.Min(TrajectorySamples.Count, Mathf.Min(snapshot.sampleCount, PungentSceneGizmoPerformancePolicy.DefaultMaxTrajectorySamplesPerComponent));
            for (int i = 1; i < count && drawBudget > 0; i++)
            {
                Handles.DrawLine(TrajectorySamples[i - 1].position, TrajectorySamples[i].position);
                drawBudget--;
            }

            if (snapshot.drawSamplePoints)
            {
                Handles.color = snapshot.sampleColor;
                for (int i = 0; i < count && drawBudget > 0; i += Mathf.Max(1, count / 32))
                {
                    Handles.SphereHandleCap(0, TrajectorySamples[i].position, Quaternion.identity, HandleUtility.GetHandleSize(TrajectorySamples[i].position) * 0.035f, EventType.Repaint);
                    drawBudget--;
                }
            }

            if (snapshot.hitFound && snapshot.drawHitMarker && drawBudget > 0)
            {
                Handles.color = snapshot.hitColor;
                Handles.SphereHandleCap(0, snapshot.hitPoint, Quaternion.identity, HandleUtility.GetHandleSize(snapshot.hitPoint) * 0.08f, EventType.Repaint);
                Handles.DrawLine(snapshot.hitPoint, snapshot.hitPoint + snapshot.hitNormal.normalized * HandleUtility.GetHandleSize(snapshot.hitPoint) * 0.35f);
                drawBudget -= 2;
            }

            if (snapshot.drawLabels && labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
            {
                string label = snapshot.hitFound ? "Trajectory hit / " + count + " samples" : "Trajectory / " + count + " samples";
                DrawLabel(snapshot.hitFound ? snapshot.hitPoint : snapshot.origin, label, selected, ref labelsDrawn);
            }

            Handles.color = previous;
        }

        private static void DrawSpatial(
            IPungentSpatialVisualizationProvider provider,
            PungentSceneGizmoProviderCache.ProviderRecord record,
            SceneView sceneView,
            bool selected,
            ref int drawBudget,
            ref int labelsDrawn)
        {
            if (provider == null ||
                !provider.TryGetSpatialVisualization(out PungentSpatialVisualizationSnapshot snapshot) ||
                !PassCommon(snapshot.Owner, record.visible, record.selectedOnly, selected) ||
                !PassDistance(sceneView, snapshot.Bounds.center, 0f))
                return;

            if (snapshot.Kind == PungentSpatialVisualizationKind.Path)
                DrawSpatialPath(snapshot, selected, ref drawBudget, ref labelsDrawn);
            else if (snapshot.Kind == PungentSpatialVisualizationKind.Area)
                DrawSpatialArea(snapshot, selected, ref drawBudget, ref labelsDrawn);
        }

        private static void DrawSpatialPath(PungentSpatialVisualizationSnapshot snapshot, bool selected, ref int drawBudget, ref int labelsDrawn)
        {
            if (drawBudget <= 0 || snapshot.Owner == null)
                return;

            SpatialPoints.Clear();
            if (snapshot.Owner is ModularPathSpawner modularPath && modularPath.BuildSampledWorldPath(out List<Vector3> sampled, out bool sampledClosed))
            {
                int step = Mathf.Max(1, sampled.Count / 96);
                for (int i = 0; i < sampled.Count; i += step)
                    SpatialPoints.Add(sampled[i]);
                if (sampledClosed && sampled.Count > 0)
                    SpatialPoints.Add(sampled[0]);
            }
            else if (snapshot.Owner is IPungentPathPointProvider pointProvider)
            {
                int count = Mathf.Min(pointProvider.PointCount, 192);
                for (int i = 0; i < count; i++)
                    SpatialPoints.Add(pointProvider.GetWorldPoint(i));
            }

            if (SpatialPoints.Count < 2)
                return;

            Color previous = Handles.color;
            Color color = snapshot.Color;
            color.a = selected ? 0.95f : 0.55f;
            Handles.color = color;
            for (int i = 1; i < SpatialPoints.Count && drawBudget > 0; i++)
            {
                Handles.DrawLine(SpatialPoints[i - 1], SpatialPoints[i]);
                drawBudget--;
            }

            if (selected)
            {
                for (int i = 0; i < SpatialPoints.Count && drawBudget > 0; i += Mathf.Max(1, SpatialPoints.Count / 24))
                {
                    float size = HandleUtility.GetHandleSize(SpatialPoints[i]) * 0.035f;
                    Handles.SphereHandleCap(0, SpatialPoints[i], Quaternion.identity, size, EventType.Repaint);
                    drawBudget--;
                }
            }

            if (labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
                DrawLabel(snapshot.Bounds.center, snapshot.DisplayName, selected, ref labelsDrawn);

            Handles.color = previous;
        }

        private static void DrawSpatialArea(PungentSpatialVisualizationSnapshot snapshot, bool selected, ref int drawBudget, ref int labelsDrawn)
        {
            if (drawBudget <= 0 || snapshot.Owner == null)
                return;

            SpatialPoints.Clear();
            if (snapshot.Owner is PungentAreaAuthoringShape area)
            {
                area.GetWorldPolygon(SpatialPoints);
            }
            else if (snapshot.Owner is IPungentAreaShapeProvider provider && provider.TryGetAreaShape(out PungentAreaShape shape) && shape.WorldPolygon != null)
            {
                SpatialPoints.AddRange(shape.WorldPolygon);
            }

            Color previous = Handles.color;
            Color color = snapshot.Color;
            color.a = selected ? 0.95f : 0.45f;
            Handles.color = color;
            if (SpatialPoints.Count >= 2)
            {
                for (int i = 0; i < SpatialPoints.Count && drawBudget > 0; i++)
                {
                    Handles.DrawLine(SpatialPoints[i], SpatialPoints[(i + 1) % SpatialPoints.Count]);
                    drawBudget--;
                }
            }

            if (selected && drawBudget > 0)
            {
                Handles.DrawWireCube(snapshot.Bounds.center, snapshot.Bounds.size);
                drawBudget--;
            }

            if (labelsDrawn < PungentSceneGizmoPerformancePolicy.WarningActiveLabels)
                DrawLabel(snapshot.Bounds.center, snapshot.DisplayName, selected, ref labelsDrawn);

            Handles.color = previous;
        }

        private static void DrawPing(PungentSceneBeaconSnapshot snapshot, ref int drawBudget, ref int pingsDrawn)
        {
            if (drawBudget <= 0 || snapshot.lastPingTime <= 0d)
                return;

            double age = EditorApplication.timeSinceStartup - snapshot.lastPingTime;
            float duration = Mathf.Max(0.05f, snapshot.pingDuration);
            if (age < 0d || age > duration)
                return;

            float t = Mathf.Clamp01((float)(age / duration));
            Color previous = Handles.color;
            Color color = snapshot.color;
            color.a *= 1f - t;
            Handles.color = color;
            Handles.DrawWireDisc(snapshot.worldPosition, Vector3.up, Mathf.Max(0.01f, snapshot.radius) * (1.25f + t * 2.25f));
            Handles.color = previous;
            pingsDrawn++;
            drawBudget--;
            RequestActiveSceneRepaint();
        }

        private static void DrawLabel(Vector3 position, string label, bool selected, ref int labelsDrawn)
        {
            if (!PungentSceneGizmoPerformancePolicy.TryConsumeLabel(selected))
                return;

            Handles.Label(position + Vector3.up * HandleUtility.GetHandleSize(position) * 0.08f, label);
            labelsDrawn++;
        }

        private static bool PassCommon(Component owner, bool drawInScene, bool selectedOnly, bool selected)
        {
            if (!drawInScene)
                return false;
            if (selectedOnly && !selected)
                return false;
            if (owner == null || owner.gameObject == null || !owner.gameObject.activeInHierarchy)
                return false;
            if (owner is Behaviour behaviour && !behaviour.enabled)
                return false;
            return true;
        }

        private static bool PassDistance(SceneView sceneView, Vector3 position, float maxDistance)
        {
            if (maxDistance <= 0f || sceneView == null || sceneView.camera == null)
                return true;

            return Vector3.Distance(sceneView.camera.transform.position, position) <= maxDistance;
        }

        private static bool IsSelected(Component component)
        {
            return component != null && component.gameObject != null && Selection.Contains(component.gameObject);
        }
    }
#endif
}
