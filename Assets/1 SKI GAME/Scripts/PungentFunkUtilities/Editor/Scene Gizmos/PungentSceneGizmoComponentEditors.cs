using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal static class PungentSceneGizmoComponentEditorUI
    {
        private static readonly List<PungentTrajectorySample> Samples = new List<PungentTrajectorySample>(PungentSceneGizmoPerformancePolicy.DefaultMaxTrajectorySamplesPerComponent);

        public static void DrawSection(string title, Color tint)
        {
            EditorGUILayout.Space(4f);
            UtilityWindowTheme.SectionTitle(title, tint);
        }

        public static void DrawProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }

        public static void DrawBrowserButton()
        {
            if (GUILayout.Button("Open Scene Gizmo Browser"))
                PungentGizmoBrowserWindow.Open();
        }

        public static void DrawBeaconRuntimeSummary(PungentSceneBeacon beacon)
        {
            if (beacon == null)
                return;

            if (beacon.TryGetBeaconSnapshot(out PungentSceneBeaconSnapshot snapshot))
                EditorGUILayout.HelpBox($"Beacon ready at {snapshot.worldPosition.ToString("F2")} with priority {snapshot.priority}.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Beacon has no drawable snapshot.", MessageType.Warning);
        }

        public static void DrawCollisionRuntimeSummary(PungentCollisionSensorGizmo sensor)
        {
            if (sensor == null)
                return;

            if (sensor.TryGetCollisionSensorSnapshot(out PungentCollisionSensorSnapshot snapshot))
            {
                MessageType type = snapshot.hasCollider ? MessageType.Info : MessageType.Warning;
                string message = snapshot.hasCollider
                    ? $"Contacts {snapshot.contactCount}/{snapshot.maxContacts}. Active collisions {snapshot.currentCollisionCount}. Last event: {snapshot.lastEventType}."
                    : snapshot.warning;
                EditorGUILayout.HelpBox(message, type);
            }
        }

        public static void DrawTriggerRuntimeSummary(PungentTriggerSensorGizmo sensor)
        {
            if (sensor == null)
                return;

            if (sensor.TryGetTriggerSensorSnapshot(out PungentTriggerSensorSnapshot snapshot))
            {
                MessageType type = snapshot.hasCollider && snapshot.colliderIsTrigger ? MessageType.Info : MessageType.Warning;
                string message = snapshot.hasCollider && snapshot.colliderIsTrigger
                    ? $"Overlaps {snapshot.overlapCount}/{snapshot.maxOverlaps}. Recent exits {snapshot.recentExitCount}."
                    : snapshot.warning;
                EditorGUILayout.HelpBox(message, type);
            }
        }

        public static void DrawTrajectoryRuntimeSummary(PungentTrajectoryVisualizer visualizer)
        {
            if (visualizer == null)
                return;

            Samples.Clear();
            if (visualizer.TryGetTrajectorySnapshot(Samples, out PungentTrajectorySnapshot snapshot))
            {
                string message = snapshot.hitFound
                    ? $"Samples {snapshot.sampleCount}. Hit at {snapshot.hitPoint.ToString("F2")}."
                    : $"Samples {snapshot.sampleCount}. No hit.";
                if (!string.IsNullOrWhiteSpace(snapshot.warning))
                    message += " " + snapshot.warning;
                EditorGUILayout.HelpBox(message, string.IsNullOrWhiteSpace(snapshot.warning) ? MessageType.Info : MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Trajectory has no drawable snapshot. Check source mode, velocity, duration, and sample settings.", MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(PungentSceneBeacon))]
    internal sealed class PungentSceneBeaconEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PungentSceneGizmoComponentEditorUI.DrawSection("Visibility", UtilityWindowTheme.Blue);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawInScene");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawWhenSelectedOnly");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "alwaysVisible");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxDrawDistance");

            PungentSceneGizmoComponentEditorUI.DrawSection("Drawing", UtilityWindowTheme.Teal);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "label");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "color");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawLabel");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawRing");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawVerticalLine");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawDistanceToSceneCamera");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "radius");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "verticalLineHeight");

            PungentSceneGizmoComponentEditorUI.DrawSection("Source", UtilityWindowTheme.Cyan);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "labelAnchor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "localOffset");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "priority");

            PungentSceneGizmoComponentEditorUI.DrawSection("Diagnostics", UtilityWindowTheme.Amber);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "pingable");
            if (GUILayout.Button("Ping Beacon"))
            {
                foreach (Object targetObject in targets)
                {
                    if (targetObject is PungentSceneBeacon beacon)
                    {
                        Undo.RecordObject(beacon, "Ping Scene Beacon");
                        beacon.Ping();
                        EditorUtility.SetDirty(beacon);
                    }
                }

                PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            }
            PungentSceneGizmoComponentEditorUI.DrawBrowserButton();
            PungentSceneGizmoComponentEditorUI.DrawBeaconRuntimeSummary((PungentSceneBeacon)target);

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(PungentCollisionSensorGizmo))]
    internal sealed class PungentCollisionSensorGizmoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PungentSceneGizmoComponentEditorUI.DrawSection("Visibility", UtilityWindowTheme.Blue);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawInScene");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawWhenSelectedOnly");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxDrawDistance");

            PungentSceneGizmoComponentEditorUI.DrawSection("Source", UtilityWindowTheme.Cyan);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "observedCollider");
            PungentCollisionSensorGizmo sensor = (PungentCollisionSensorGizmo)target;
            if (sensor != null && sensor.GetComponent<Collider>() == null && serializedObject.FindProperty("observedCollider").objectReferenceValue == null)
                EditorGUILayout.HelpBox("Assign an observed collider or add a Collider to this GameObject.", MessageType.Warning);

            PungentSceneGizmoComponentEditorUI.DrawSection("Drawing", UtilityWindowTheme.Teal);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawColliderBounds");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawContactPoints");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawContactNormals");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawContactLabels");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "contactPointSize");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "normalLength");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "idleColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "activeColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "contactColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "normalColor");

            PungentSceneGizmoComponentEditorUI.DrawSection("Runtime Tracking", UtilityWindowTheme.Purple);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "keepRecentContacts");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "recentContactLifetime");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxContacts");
            if (serializedObject.FindProperty("maxContacts").intValue > PungentSceneGizmoPerformancePolicy.MaxCollisionContactsPerSensor)
                EditorGUILayout.HelpBox("High contact caps can increase Scene View label/handle cost.", MessageType.Warning);

            PungentSceneGizmoComponentEditorUI.DrawSection("Diagnostics", UtilityWindowTheme.Amber);
            if (GUILayout.Button("Clear Collision History"))
            {
                foreach (Object targetObject in targets)
                {
                    if (targetObject is PungentCollisionSensorGizmo gizmo)
                    {
                        Undo.RecordObject(gizmo, "Clear Collision History");
                        gizmo.ClearHistory();
                        EditorUtility.SetDirty(gizmo);
                    }
                }
            }
            PungentSceneGizmoComponentEditorUI.DrawBrowserButton();
            if (EditorApplication.isPlaying)
                PungentSceneGizmoComponentEditorUI.DrawCollisionRuntimeSummary(sensor);

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(PungentTriggerSensorGizmo))]
    internal sealed class PungentTriggerSensorGizmoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PungentSceneGizmoComponentEditorUI.DrawSection("Visibility", UtilityWindowTheme.Blue);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawInScene");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawWhenSelectedOnly");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxDrawDistance");

            PungentSceneGizmoComponentEditorUI.DrawSection("Source", UtilityWindowTheme.Cyan);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "observedTrigger");
            PungentTriggerSensorGizmo sensor = (PungentTriggerSensorGizmo)target;
            Collider collider = serializedObject.FindProperty("observedTrigger").objectReferenceValue as Collider;
            if (collider == null && sensor != null)
                collider = sensor.GetComponent<Collider>();
            if (collider == null)
                EditorGUILayout.HelpBox("Assign an observed trigger or add a Collider to this GameObject.", MessageType.Warning);
            else if (!collider.isTrigger)
                EditorGUILayout.HelpBox("The observed collider is not marked as Trigger.", MessageType.Warning);

            PungentSceneGizmoComponentEditorUI.DrawSection("Drawing", UtilityWindowTheme.Teal);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawTriggerBounds");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawOverlapLinks");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawOverlapLabels");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawRecentExits");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "idleColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "activeColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "overlapColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "recentExitColor");

            PungentSceneGizmoComponentEditorUI.DrawSection("Runtime Tracking", UtilityWindowTheme.Purple);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "recentExitLifetime");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxOverlaps");
            if (serializedObject.FindProperty("maxOverlaps").intValue > PungentSceneGizmoPerformancePolicy.MaxTriggerOverlapsPerSensor)
                EditorGUILayout.HelpBox("High overlap caps can increase Scene View link/label cost.", MessageType.Warning);

            PungentSceneGizmoComponentEditorUI.DrawSection("Diagnostics", UtilityWindowTheme.Amber);
            if (GUILayout.Button("Clear Trigger History"))
            {
                foreach (Object targetObject in targets)
                {
                    if (targetObject is PungentTriggerSensorGizmo gizmo)
                    {
                        Undo.RecordObject(gizmo, "Clear Trigger History");
                        gizmo.ClearHistory();
                        EditorUtility.SetDirty(gizmo);
                    }
                }
            }
            PungentSceneGizmoComponentEditorUI.DrawBrowserButton();
            if (EditorApplication.isPlaying)
                PungentSceneGizmoComponentEditorUI.DrawTriggerRuntimeSummary(sensor);

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(PungentTrajectoryVisualizer))]
    internal sealed class PungentTrajectoryVisualizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PungentSceneGizmoComponentEditorUI.DrawSection("Visibility", UtilityWindowTheme.Blue);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawInScene");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawWhenSelectedOnly");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxDrawDistance");

            PungentSceneGizmoComponentEditorUI.DrawSection("Source", UtilityWindowTheme.Cyan);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "sourceMode");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "origin");
            TrajectorySourceMode sourceMode = (TrajectorySourceMode)serializedObject.FindProperty("sourceMode").enumValueIndex;
            if (sourceMode == TrajectorySourceMode.RigidbodyVelocity)
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "sourceRigidbody");
            if (sourceMode == TrajectorySourceMode.ReflectedVector3Field)
            {
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "reflectedComponent");
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "velocityFieldPath");
            }
            if (sourceMode == TrajectorySourceMode.ExplicitVelocity)
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "explicitVelocity");
            if (sourceMode == TrajectorySourceMode.TransformForward)
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "forwardSpeed");

            PungentSceneGizmoComponentEditorUI.DrawSection("Drawing", UtilityWindowTheme.Teal);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawSamplePoints");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawHitMarker");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "drawLabels");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "trajectoryColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "hitColor");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "sampleColor");

            PungentSceneGizmoComponentEditorUI.DrawSection("Performance", UtilityWindowTheme.Purple);
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "gravityMode");
            TrajectoryGravityMode gravityMode = (TrajectoryGravityMode)serializedObject.FindProperty("gravityMode").enumValueIndex;
            if (gravityMode == TrajectoryGravityMode.CustomGravity)
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "customGravity");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "collisionMode");
            TrajectoryCollisionMode collisionMode = (TrajectoryCollisionMode)serializedObject.FindProperty("collisionMode").enumValueIndex;
            if (collisionMode != TrajectoryCollisionMode.None)
            {
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "collisionMask");
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "stopAtFirstHit");
            }
            if (collisionMode == TrajectoryCollisionMode.SphereCast)
                PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "sphereCastRadius");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "duration");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "timeStep");
            PungentSceneGizmoComponentEditorUI.DrawProperty(serializedObject, "maxSamples");
            if (serializedObject.FindProperty("maxSamples").intValue > PungentSceneGizmoPerformancePolicy.DefaultMaxTrajectorySamplesPerComponent)
                EditorGUILayout.HelpBox("Samples above the default per-component budget are clamped in editor drawing.", MessageType.Warning);
            if (serializedObject.FindProperty("timeStep").floatValue <= 0f)
                EditorGUILayout.HelpBox("Time step must be greater than zero.", MessageType.Error);

            PungentSceneGizmoComponentEditorUI.DrawSection("Diagnostics", UtilityWindowTheme.Amber);
            if (GUILayout.Button("Refresh Trajectory Preview"))
            {
                foreach (Object targetObject in targets)
                {
                    if (targetObject is PungentTrajectoryVisualizer visualizer)
                    {
                        Undo.RecordObject(visualizer, "Refresh Trajectory Preview");
                        visualizer.RefreshPreview();
                        EditorUtility.SetDirty(visualizer);
                    }
                }

                PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            }
            PungentSceneGizmoComponentEditorUI.DrawBrowserButton();
            PungentSceneGizmoComponentEditorUI.DrawTrajectoryRuntimeSummary((PungentTrajectoryVisualizer)target);

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
