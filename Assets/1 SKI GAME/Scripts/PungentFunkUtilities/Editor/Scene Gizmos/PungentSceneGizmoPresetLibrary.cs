using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentSceneGizmoBuiltInPreset
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Category;
        public readonly string Description;
        private readonly Func<List<PungentSceneGizmoSource.GizmoRule>> _createRules;

        public PungentSceneGizmoBuiltInPreset(string id, string displayName, string category, string description, Func<List<PungentSceneGizmoSource.GizmoRule>> createRules)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Description = description;
            _createRules = createRules;
        }

        public List<PungentSceneGizmoSource.GizmoRule> CreateRuleCopies()
        {
            List<PungentSceneGizmoSource.GizmoRule> rules = _createRules != null ? _createRules.Invoke() : new List<PungentSceneGizmoSource.GizmoRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null)
                    continue;
                rules[i].presetId = Id;
                rules[i].presetCategory = Category;
            }
            return rules;
        }
    }

    public static class PungentSceneGizmoPresetLibrary
    {
        private static readonly List<PungentSceneGizmoBuiltInPreset> BuiltIns = new List<PungentSceneGizmoBuiltInPreset>
        {
            new PungentSceneGizmoBuiltInPreset("velocity-state-vector", "Velocity / State Vector", "Debug Motion", "Velocity arrow with optional reflected state color mapping.", CreateVelocityStateVector),
            new PungentSceneGizmoBuiltInPreset("ground-probe", "Ground Probe", "Probes", "Downward probe ray for grounding and contact debugging.", CreateGroundProbe),
            new PungentSceneGizmoBuiltInPreset("probe-fan", "Probe Fan", "Probes", "Three lightweight probe rays for fan or feeler-style checks.", CreateProbeFan),
            new PungentSceneGizmoBuiltInPreset("interaction-ray", "Interaction Ray", "Interaction", "Forward interaction ray with clear authoring label.", CreateInteractionRay),
            new PungentSceneGizmoBuiltInPreset("spawn-point-set", "Spawn Point Set", "Authoring", "Spawn marker plus forward direction cue.", CreateSpawnPointSet),
            new PungentSceneGizmoBuiltInPreset("collider-trigger-volume", "Collider / Trigger Volume", "Volumes", "Collider bounds visualization with trigger/volume-friendly color.", CreateColliderTriggerVolume),
            new PungentSceneGizmoBuiltInPreset("activity-objective-area", "Activity / Objective Area", "Volumes", "Objective area disc and radius ring.", CreateActivityObjectiveArea),
            new PungentSceneGizmoBuiltInPreset("poi-map-anchor", "POI / Map Anchor", "Map", "Point-of-interest anchor marker and label.", CreatePoiMapAnchor),
            new PungentSceneGizmoBuiltInPreset("direction-force-vector", "Direction / Force Vector", "Debug Motion", "Direction or force vector arrow with reflected vector support.", CreateDirectionForceVector),
            new PungentSceneGizmoBuiltInPreset("path-centerline", "Path Centerline", "Path / Area", "Generic centerline direction cue for path authoring adapters.", CreatePathCenterline),
            new PungentSceneGizmoBuiltInPreset("path-corridor-width-preview", "Path Corridor / Width Preview", "Path / Area", "Generic corridor width preview that can bind to a preview width field.", CreatePathCorridorWidthPreview),
            new PungentSceneGizmoBuiltInPreset("checkpoint-gate-volume", "Checkpoint / Gate Volume", "Path / Area", "Generic checkpoint or gate volume marker.", CreateCheckpointGateVolume),
            new PungentSceneGizmoBuiltInPreset("exclusion-span", "Exclusion Span", "Path / Area", "Generic exclusion span or blocked area marker.", CreateExclusionSpan),
            new PungentSceneGizmoBuiltInPreset("cable-support-route", "Cable / Support Route", "Path / Area", "Generic cable or support-route direction cue.", CreateCableSupportRoute),
            new PungentSceneGizmoBuiltInPreset("scene-beacon-marker", "Scene Beacon Marker", "Provider Setup", "Static source-rule marker that pairs with the Scene Beacon component for pingable navigation.", CreateSceneBeaconMarker),
            new PungentSceneGizmoBuiltInPreset("pingable-navigation-marker", "Pingable Navigation Marker", "Provider Setup", "Static navigation marker. Add the Scene Beacon component for runtime ping state and animated editor pulses.", CreatePingableNavigationMarker),
            new PungentSceneGizmoBuiltInPreset("trajectory-origin-marker", "Trajectory Origin Marker", "Provider Setup", "Static trajectory origin cue. Add the Trajectory Visualizer component for live trajectory samples.", CreateTrajectoryOriginMarker),
            new PungentSceneGizmoBuiltInPreset("collision-volume-marker", "Collision Volume Marker", "Provider Setup", "Static collision volume cue. Add the Collision Sensor component for runtime contact diagnostics.", CreateCollisionVolumeMarker),
            new PungentSceneGizmoBuiltInPreset("trigger-volume-marker", "Trigger Volume Marker", "Provider Setup", "Static trigger volume cue. Add the Trigger Sensor component for runtime overlap diagnostics.", CreateTriggerVolumeMarker)
        };

        public static IReadOnlyList<PungentSceneGizmoBuiltInPreset> All => BuiltIns;

        public static string[] Categories => BuiltIns
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        public static PungentSceneGizmoBuiltInPreset Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return BuiltIns.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static void AddBuiltInPresetMenu(GenericMenu menu, Action<PungentSceneGizmoBuiltInPreset> onSelect)
        {
            if (menu == null)
                return;

            for (int i = 0; i < BuiltIns.Count; i++)
            {
                PungentSceneGizmoBuiltInPreset preset = BuiltIns[i];
                menu.AddItem(new GUIContent(preset.Category + "/" + preset.DisplayName), false, () => onSelect?.Invoke(preset));
            }
        }

        public static PungentSceneGizmoPreset CreateTransientPreset(PungentSceneGizmoBuiltInPreset builtIn)
        {
            if (builtIn == null)
                return null;

            PungentSceneGizmoPreset preset = ScriptableObject.CreateInstance<PungentSceneGizmoPreset>();
            preset.name = builtIn.DisplayName;
            preset.presetId = builtIn.Id;
            preset.displayName = builtIn.DisplayName;
            preset.category = builtIn.Category;
            preset.description = builtIn.Description;
            preset.replaceExistingRules = false;
            preset.rules = builtIn.CreateRuleCopies();
            return preset;
        }

        private static PungentSceneGizmoSource.GizmoRule Rule(string name, PungentSceneGizmoSource.GizmoShape shape, Color color, string label, float size = 1f)
        {
            return new PungentSceneGizmoSource.GizmoRule
            {
                name = name,
                label = label,
                shape = shape,
                color = color,
                size = size,
                targetTransform = null,
                drawWhen = PungentSceneGizmoSource.DrawWhen.Selected
            };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateVelocityStateVector()
        {
            PungentSceneGizmoSource.GizmoRule velocity = Rule("Velocity Vector", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.2f, 0.85f, 1f, 0.9f), "Velocity", 2.5f);
            velocity.directionFieldPath = "velocity";
            velocity.sizeFieldPath = "velocity";
            velocity.sizeMode = PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude;

            PungentSceneGizmoSource.GizmoRule state = Rule("State Label", PungentSceneGizmoSource.GizmoShape.StateLabel, new Color(0.75f, 0.85f, 1f, 0.95f), "State", 1f);
            state.useStateColorMap = true;
            state.stateColorMap.entries.Add(new PungentSceneGizmoStateColorEntry
            {
                name = "Specific Active State",
                priority = 20,
                sourceKind = PungentSceneGizmoStateSourceKind.ActiveStateProvider,
                expectedValue = "WallRunning",
                color = new Color(0.75f, 0.35f, 1f, 0.95f)
            });
            state.stateColorMap.entries.Add(new PungentSceneGizmoStateColorEntry
            {
                name = "Grounded",
                priority = 10,
                sourceKind = PungentSceneGizmoStateSourceKind.Bool,
                memberPath = "isGrounded",
                expectedValue = "true",
                color = new Color(0.35f, 1f, 0.45f, 0.95f)
            });

            return new List<PungentSceneGizmoSource.GizmoRule> { velocity, state };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateGroundProbe()
        {
            PungentSceneGizmoSource.GizmoRule ray = Rule("Ground Probe", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.35f, 1f, 0.55f, 0.9f), "Ground", 2f);
            ray.direction = Vector3.down;
            ray.useTargetRotation = false;
            return new List<PungentSceneGizmoSource.GizmoRule> { ray };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateProbeFan()
        {
            Color color = new Color(0.35f, 0.95f, 1f, 0.82f);
            return new List<PungentSceneGizmoSource.GizmoRule>
            {
                ProbeFanRule("Probe Center", Vector3.forward, color),
                ProbeFanRule("Probe Left", Quaternion.Euler(0f, -25f, 0f) * Vector3.forward, color),
                ProbeFanRule("Probe Right", Quaternion.Euler(0f, 25f, 0f) * Vector3.forward, color)
            };
        }

        private static PungentSceneGizmoSource.GizmoRule ProbeFanRule(string name, Vector3 direction, Color color)
        {
            PungentSceneGizmoSource.GizmoRule rule = Rule(name, PungentSceneGizmoSource.GizmoShape.Arrow, color, "Probe", 3f);
            rule.direction = direction;
            return rule;
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateInteractionRay()
        {
            PungentSceneGizmoSource.GizmoRule ray = Rule("Interaction Ray", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(1f, 0.78f, 0.28f, 0.9f), "Interact", 4f);
            ray.direction = Vector3.forward;
            return new List<PungentSceneGizmoSource.GizmoRule> { ray };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateSpawnPointSet()
        {
            PungentSceneGizmoSource.GizmoRule marker = Rule("Spawn Point", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.4f, 1f, 0.75f, 0.95f), "Spawn", 0.6f);
            PungentSceneGizmoSource.GizmoRule forward = Rule("Spawn Forward", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.15f, 0.9f, 1f, 0.95f), "Forward", 1.8f);
            return new List<PungentSceneGizmoSource.GizmoRule> { marker, forward };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateColliderTriggerVolume()
        {
            PungentSceneGizmoSource.GizmoRule bounds = Rule("Collider / Trigger Volume", PungentSceneGizmoSource.GizmoShape.ColliderBounds, new Color(0.9f, 0.55f, 1f, 0.85f), "Volume", 1f);
            return new List<PungentSceneGizmoSource.GizmoRule> { bounds };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateActivityObjectiveArea()
        {
            PungentSceneGizmoSource.GizmoRule disc = Rule("Objective Area Disc", PungentSceneGizmoSource.GizmoShape.Disc, new Color(1f, 0.78f, 0.24f, 0.9f), "Objective", 4f);
            disc.direction = Vector3.up;
            disc.useTargetRotation = false;
            PungentSceneGizmoSource.GizmoRule radius = Rule("Objective Radius", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(1f, 0.62f, 0.26f, 0.55f), "Area", 4f);
            return new List<PungentSceneGizmoSource.GizmoRule> { disc, radius };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreatePoiMapAnchor()
        {
            PungentSceneGizmoSource.GizmoRule anchor = Rule("POI Anchor", PungentSceneGizmoSource.GizmoShape.Sphere, new Color(0.2f, 0.8f, 1f, 0.9f), "POI", 0.35f);
            PungentSceneGizmoSource.GizmoRule label = Rule("POI Label", PungentSceneGizmoSource.GizmoShape.Label, new Color(0.95f, 1f, 0.75f, 1f), "POI", 1f);
            return new List<PungentSceneGizmoSource.GizmoRule> { anchor, label };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateDirectionForceVector()
        {
            PungentSceneGizmoSource.GizmoRule force = Rule("Direction / Force Vector", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(1f, 0.42f, 0.34f, 0.92f), "Vector", 3f);
            force.directionFieldPath = "force";
            force.sizeFieldPath = "force";
            force.sizeMode = PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude;
            return new List<PungentSceneGizmoSource.GizmoRule> { force };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreatePathCenterline()
        {
            PungentSceneGizmoSource.GizmoRule center = Rule("Path Centerline", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.22f, 0.95f, 1f, 0.9f), "Centerline", 4f);
            center.direction = Vector3.forward;
            return new List<PungentSceneGizmoSource.GizmoRule> { center };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreatePathCorridorWidthPreview()
        {
            PungentSceneGizmoSource.GizmoRule corridor = Rule("Path Corridor Width", PungentSceneGizmoSource.GizmoShape.Disc, new Color(0.35f, 1f, 0.75f, 0.88f), "Corridor", 1.5f);
            corridor.direction = Vector3.up;
            corridor.useTargetRotation = false;
            corridor.sizeMode = PungentSceneGizmoSource.SizeMode.FieldFloat;
            corridor.sizeFieldPath = "previewCorridorWidth";

            PungentSceneGizmoSource.GizmoRule marker = Rule("Path Corridor Marker", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.35f, 0.95f, 1f, 0.55f), "Width", 1.5f);
            marker.sizeMode = PungentSceneGizmoSource.SizeMode.FieldFloat;
            marker.sizeFieldPath = "previewCorridorWidth";
            return new List<PungentSceneGizmoSource.GizmoRule> { corridor, marker };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateCheckpointGateVolume()
        {
            PungentSceneGizmoSource.GizmoRule gate = Rule("Checkpoint / Gate Volume", PungentSceneGizmoSource.GizmoShape.WireCube, new Color(0.35f, 0.75f, 1f, 0.9f), "Gate", 1f);
            gate.vectorSize = new Vector3(4f, 3f, 0.35f);
            return new List<PungentSceneGizmoSource.GizmoRule> { gate };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateExclusionSpan()
        {
            PungentSceneGizmoSource.GizmoRule span = Rule("Exclusion Span", PungentSceneGizmoSource.GizmoShape.WireCube, new Color(1f, 0.28f, 0.24f, 0.9f), "Exclude", 1f);
            span.vectorSize = new Vector3(4f, 1.25f, 2f);
            PungentSceneGizmoSource.GizmoRule label = Rule("Exclusion Label", PungentSceneGizmoSource.GizmoShape.Label, new Color(1f, 0.65f, 0.35f, 1f), "Exclusion", 1f);
            return new List<PungentSceneGizmoSource.GizmoRule> { span, label };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateCableSupportRoute()
        {
            PungentSceneGizmoSource.GizmoRule route = Rule("Cable / Support Route", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.8f, 0.95f, 1f, 0.9f), "Route", 4f);
            route.direction = Vector3.forward;
            PungentSceneGizmoSource.GizmoRule support = Rule("Support Marker", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.95f, 0.85f, 0.45f, 0.85f), "Support", 0.55f);
            return new List<PungentSceneGizmoSource.GizmoRule> { route, support };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateSceneBeaconMarker()
        {
            PungentSceneGizmoSource.GizmoRule marker = Rule("Scene Beacon Marker", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.32f, 0.92f, 1f, 0.95f), "Beacon", 0.75f);
            marker.drawWhen = PungentSceneGizmoSource.DrawWhen.Always;
            PungentSceneGizmoSource.GizmoRule ring = Rule("Scene Beacon Ring", PungentSceneGizmoSource.GizmoShape.Disc, new Color(0.32f, 0.92f, 1f, 0.6f), "Beacon", 1.25f);
            ring.direction = Vector3.up;
            ring.useTargetRotation = false;
            ring.drawWhen = PungentSceneGizmoSource.DrawWhen.Always;
            return new List<PungentSceneGizmoSource.GizmoRule> { marker, ring };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreatePingableNavigationMarker()
        {
            PungentSceneGizmoSource.GizmoRule marker = Rule("Pingable Navigation Marker", PungentSceneGizmoSource.GizmoShape.Sphere, new Color(0.2f, 0.75f, 1f, 0.9f), "Ping", 0.35f);
            marker.drawWhen = PungentSceneGizmoSource.DrawWhen.Always;
            PungentSceneGizmoSource.GizmoRule forward = Rule("Navigation Direction", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.95f, 0.95f, 0.45f, 0.9f), "Nav", 1.75f);
            forward.direction = Vector3.forward;
            forward.drawWhen = PungentSceneGizmoSource.DrawWhen.Selected;
            return new List<PungentSceneGizmoSource.GizmoRule> { marker, forward };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateTrajectoryOriginMarker()
        {
            PungentSceneGizmoSource.GizmoRule origin = Rule("Trajectory Origin", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.2f, 0.9f, 1f, 0.85f), "Trajectory", 0.35f);
            PungentSceneGizmoSource.GizmoRule direction = Rule("Trajectory Direction", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.22f, 1f, 0.7f, 0.9f), "Origin", 2.5f);
            direction.direction = Vector3.forward;
            return new List<PungentSceneGizmoSource.GizmoRule> { origin, direction };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateCollisionVolumeMarker()
        {
            PungentSceneGizmoSource.GizmoRule bounds = Rule("Collision Volume Marker", PungentSceneGizmoSource.GizmoShape.ColliderBounds, new Color(1f, 0.48f, 0.28f, 0.82f), "Collision", 1f);
            bounds.drawWhen = PungentSceneGizmoSource.DrawWhen.Selected;
            return new List<PungentSceneGizmoSource.GizmoRule> { bounds };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateTriggerVolumeMarker()
        {
            PungentSceneGizmoSource.GizmoRule bounds = Rule("Trigger Volume Marker", PungentSceneGizmoSource.GizmoShape.ColliderBounds, new Color(0.95f, 0.35f, 1f, 0.78f), "Trigger", 1f);
            bounds.drawWhen = PungentSceneGizmoSource.DrawWhen.Selected;
            return new List<PungentSceneGizmoSource.GizmoRule> { bounds };
        }
    }
#endif
}
