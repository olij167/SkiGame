using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentSceneGizmoBlockDefinition
    {
        private readonly Func<List<PungentSceneGizmoSource.GizmoRule>> _createRules;

        public PungentSceneGizmoBlockDefinition(
            string id,
            string displayName,
            string category,
            string description,
            Func<List<PungentSceneGizmoSource.GizmoRule>> createRules,
            Type componentType = null,
            string iconText = null,
            int sortOrder = 0,
            Color? tint = null,
            string effectSummary = null,
            int estimatedRuleCount = -1)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Description = description;
            ComponentType = componentType;
            IconText = string.IsNullOrWhiteSpace(iconText) ? "BLK" : iconText;
            SortOrder = sortOrder;
            Tint = tint ?? Color.white;
            EffectSummary = string.IsNullOrWhiteSpace(effectSummary) ? description : effectSummary;
            EstimatedRuleCount = estimatedRuleCount;
            _createRules = createRules;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public Type ComponentType { get; }
        public string IconText { get; }
        public int SortOrder { get; }
        public Color Tint { get; }
        public string EffectSummary { get; }
        public int EstimatedRuleCount { get; }
        public int EstimatedProviderCount => CreatesProvider ? 1 : 0;
        public bool CreatesProvider => ComponentType != null;

        public List<PungentSceneGizmoSource.GizmoRule> CreateRules()
        {
            return _createRules != null ? _createRules.Invoke() : new List<PungentSceneGizmoSource.GizmoRule>();
        }
    }

    public struct PungentSceneGizmoBlockApplySummary
    {
        public int rulesAdded;
        public int componentsAdded;
        public int componentsSkipped;
        public int skipped;

        public override string ToString()
        {
            return $"Rules added {rulesAdded}, components added {componentsAdded}, components skipped {componentsSkipped}, skipped {skipped}.";
        }
    }

    public static class PungentSceneGizmoBlockLibrary
    {
        private static readonly string[] CategoryOrder =
        {
            "Anchors",
            "Vectors",
            "Probes",
            "Volumes",
            "State",
            "Relationships",
            "Providers"
        };

        private static readonly List<PungentSceneGizmoBlockDefinition> Blocks = new List<PungentSceneGizmoBlockDefinition>
        {
            new PungentSceneGizmoBlockDefinition("origin", "Origin", "Anchors", "Marker at the source origin.", () => One(Rule("Origin", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.25f, 0.85f, 1f, 0.9f), "Origin", 0.5f)), iconText: "OR", sortOrder: 10, tint: new Color(0.25f, 0.85f, 1f, 1f), effectSummary: "1 origin marker", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("target", "Target", "Anchors", "Secondary target marker for relationship rules.", CreateTargetRules, iconText: "TG", sortOrder: 20, tint: new Color(1f, 0.78f, 0.25f, 1f), effectSummary: "1 secondary marker", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("direction-vector", "Direction Vector", "Vectors", "Forward or reflected direction arrow.", CreateDirectionVectorRules, iconText: "DV", sortOrder: 110, tint: new Color(0.2f, 0.9f, 1f, 1f), effectSummary: "1 arrow rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("velocity-vector", "Velocity Vector", "Vectors", "Reflected velocity arrow using velocity path defaults.", CreateVelocityVectorRules, iconText: "VV", sortOrder: 120, tint: new Color(0.25f, 1f, 0.72f, 1f), effectSummary: "1 bound vector rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("probe-ray", "Probe Ray", "Probes", "Single probe ray for spatial checks.", CreateProbeRayRules, iconText: "PR", sortOrder: 210, tint: new Color(0.35f, 1f, 0.55f, 1f), effectSummary: "1 probe arrow", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("probe-fan", "Probe Fan", "Probes", "Three lightweight probe rays.", CreateProbeFanRules, iconText: "PF", sortOrder: 220, tint: new Color(0.35f, 0.95f, 1f, 1f), effectSummary: "3 probe arrows", estimatedRuleCount: 3),
            new PungentSceneGizmoBlockDefinition("collider-volume", "Collider Volume", "Volumes", "Collider bounds marker.", CreateColliderVolumeRules, iconText: "CV", sortOrder: 310, tint: new Color(0.35f, 0.95f, 1f, 1f), effectSummary: "1 collider bounds rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("trigger-volume", "Trigger Volume", "Volumes", "Trigger-style collider bounds marker.", CreateTriggerVolumeRules, iconText: "TV", sortOrder: 320, tint: new Color(0.9f, 0.55f, 1f, 1f), effectSummary: "1 trigger bounds rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("state-label", "State Label", "State", "Label driven by reflected or adapter-exposed state.", CreateStateLabelRules, iconText: "SL", sortOrder: 410, tint: new Color(0.78f, 0.88f, 1f, 1f), effectSummary: "1 state label rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("state-color-map", "State Color Map", "State", "Color-mapped marker for generic state diagnostics.", CreateStateColorMapRules, iconText: "SC", sortOrder: 420, tint: new Color(0.35f, 1f, 0.65f, 1f), effectSummary: "1 state-coloured rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("distance", "Distance", "Relationships", "Line/label between source and secondary transform.", CreateDistanceRules, iconText: "DS", sortOrder: 510, tint: new Color(1f, 0.86f, 0.25f, 1f), effectSummary: "1 relationship rule", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("trajectory", "Trajectory", "Providers", "Live trajectory preview component.", CreateTrajectoryMarkerRules, typeof(PungentTrajectoryVisualizer), iconText: "TR", sortOrder: 610, tint: new Color(0.25f, 0.95f, 1f, 1f), effectSummary: "2 rules + trajectory provider", estimatedRuleCount: 2),
            new PungentSceneGizmoBlockDefinition("beacon", "Beacon", "Providers", "Pingable navigation beacon component.", CreateBeaconMarkerRules, typeof(PungentSceneBeacon), iconText: "BC", sortOrder: 620, tint: new Color(0.95f, 1f, 0.75f, 1f), effectSummary: "2 rules + beacon provider", estimatedRuleCount: 2),
            new PungentSceneGizmoBlockDefinition("collision-sensor", "Collision Sensor", "Providers", "Runtime collision contact diagnostics component.", CreateColliderVolumeRules, typeof(PungentCollisionSensorGizmo), iconText: "CO", sortOrder: 630, tint: new Color(1f, 0.72f, 0.35f, 1f), effectSummary: "1 rule + collision provider", estimatedRuleCount: 1),
            new PungentSceneGizmoBlockDefinition("trigger-sensor", "Trigger Sensor", "Providers", "Runtime trigger overlap diagnostics component.", CreateTriggerVolumeRules, typeof(PungentTriggerSensorGizmo), iconText: "TS", sortOrder: 640, tint: new Color(0.9f, 0.55f, 1f, 1f), effectSummary: "1 rule + trigger provider", estimatedRuleCount: 1)
        };

        public static IReadOnlyList<PungentSceneGizmoBlockDefinition> All => Blocks
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        public static string[] Categories
        {
            get
            {
                HashSet<string> existing = new HashSet<string>(
                    Blocks.Select(b => b.Category).Where(c => !string.IsNullOrWhiteSpace(c)),
                    StringComparer.OrdinalIgnoreCase);

                List<string> ordered = CategoryOrder.Where(existing.Contains).ToList();
                ordered.AddRange(existing
                    .Where(c => !CategoryOrder.Any(known => string.Equals(known, c, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
                return ordered.ToArray();
            }
        }

        public static IEnumerable<PungentSceneGizmoBlockDefinition> InCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
                return All;

            return Blocks
                .Where(b => string.Equals(b.Category, category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ApplyBlock(PungentSceneGizmoSource source, PungentSceneGizmoBlockDefinition block, out PungentSceneGizmoBlockApplySummary summary)
        {
            summary = default;
            if (source == null || source.gameObject == null || block == null || EditorUtility.IsPersistent(source.gameObject))
            {
                summary.skipped++;
                return false;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Scene Gizmo Block");
            Undo.RecordObject(source, "Add Scene Gizmo Block");

            if (source.rules == null)
                source.rules = new List<PungentSceneGizmoSource.GizmoRule>();

            List<PungentSceneGizmoSource.GizmoRule> rules = block.CreateRules();
            for (int i = 0; i < rules.Count; i++)
            {
                PungentSceneGizmoSource.GizmoRule rule = rules[i];
                if (rule == null)
                    continue;

                rule.presetId = "block-" + block.Id;
                rule.presetCategory = "Block";
                PungentSceneGizmoPresetActions.ApplyTargetDefaults(source.gameObject, rule);
                source.rules.Add(rule);
                summary.rulesAdded++;
            }

            if (block.ComponentType != null)
                ApplyProviderComponent(source.gameObject, block.ComponentType, ref summary);

            source.InvalidateChildBoundsCache();
            EditorUtility.SetDirty(source);
            Undo.CollapseUndoOperations(group);
            PungentSceneGizmoProviderCache.MarkDirty();
            PungentSceneGizmoProviderDrawer.RequestActiveSceneRepaint();
            return summary.rulesAdded > 0 || summary.componentsAdded > 0;
        }

        private static void ApplyProviderComponent(GameObject go, Type componentType, ref PungentSceneGizmoBlockApplySummary summary)
        {
            if (go == null || componentType == null)
            {
                summary.skipped++;
                return;
            }

            if (componentType == typeof(PungentSceneBeacon))
            {
                AddProvider<PungentSceneBeacon>(go, "Add Scene Beacon", ref summary);
                return;
            }

            if (componentType == typeof(PungentCollisionSensorGizmo))
            {
                AddProvider<PungentCollisionSensorGizmo>(go, "Add Collision Sensor Gizmo", ref summary);
                return;
            }

            if (componentType == typeof(PungentTriggerSensorGizmo))
            {
                AddProvider<PungentTriggerSensorGizmo>(go, "Add Trigger Sensor Gizmo", ref summary);
                return;
            }

            if (componentType == typeof(PungentTrajectoryVisualizer))
            {
                AddProvider<PungentTrajectoryVisualizer>(go, "Add Trajectory Visualizer", ref summary);
                return;
            }

            summary.skipped++;
        }

        private static void AddProvider<TComponent>(GameObject go, string undoName, ref PungentSceneGizmoBlockApplySummary summary)
            where TComponent : Component
        {
            if (PungentSceneGizmoCommandService.AddProviderComponentToGameObject(go, undoName, out TComponent _, out bool added))
            {
                if (added)
                    summary.componentsAdded++;
                else
                    summary.componentsSkipped++;
            }
            else
            {
                summary.skipped++;
            }
        }

        private static List<PungentSceneGizmoSource.GizmoRule> One(PungentSceneGizmoSource.GizmoRule rule)
        {
            return new List<PungentSceneGizmoSource.GizmoRule> { rule };
        }

        private static PungentSceneGizmoSource.GizmoRule Rule(string name, PungentSceneGizmoSource.GizmoShape shape, Color color, string label, float size)
        {
            return new PungentSceneGizmoSource.GizmoRule
            {
                name = name,
                shape = shape,
                color = color,
                label = label,
                size = size,
                drawWhen = PungentSceneGizmoSource.DrawWhen.Selected
            };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateTargetRules()
        {
            PungentSceneGizmoSource.GizmoRule target = Rule("Target", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(1f, 0.78f, 0.25f, 0.9f), "Target", 0.55f);
            target.positionMode = PungentSceneGizmoSource.PositionMode.SecondaryTransform;
            return One(target);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateDirectionVectorRules()
        {
            PungentSceneGizmoSource.GizmoRule arrow = Rule("Direction Vector", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.2f, 0.9f, 1f, 0.95f), "Direction", 2.25f);
            arrow.direction = Vector3.forward;
            return One(arrow);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateVelocityVectorRules()
        {
            PungentSceneGizmoSource.GizmoRule velocity = Rule("Velocity Vector", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.25f, 1f, 0.72f, 0.92f), "Velocity", 2.5f);
            velocity.directionFieldPath = "velocity";
            velocity.sizeMode = PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude;
            velocity.sizeFieldPath = "velocity";
            return One(velocity);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateDistanceRules()
        {
            PungentSceneGizmoSource.GizmoRule distance = Rule("Distance", PungentSceneGizmoSource.GizmoShape.DistanceBetween, new Color(1f, 0.86f, 0.25f, 0.95f), "Distance", 1f);
            distance.positionMode = PungentSceneGizmoSource.PositionMode.MidpointToSecondary;
            distance.sizeMode = PungentSceneGizmoSource.SizeMode.DistanceToSecondary;
            return One(distance);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateStateLabelRules()
        {
            PungentSceneGizmoSource.GizmoRule label = Rule("State Label", PungentSceneGizmoSource.GizmoShape.StateLabel, new Color(0.78f, 0.88f, 1f, 1f), "State", 1f);
            label.useStateColorMap = true;
            label.stateColorMap.entries.Add(new PungentSceneGizmoStateColorEntry
            {
                name = "Active",
                priority = 10,
                sourceKind = PungentSceneGizmoStateSourceKind.ActiveStateProvider,
                expectedValue = "Active",
                color = new Color(0.35f, 1f, 0.65f, 0.95f)
            });
            return One(label);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateStateColorMapRules()
        {
            PungentSceneGizmoSource.GizmoRule marker = Rule("State Color Map", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.25f, 0.85f, 1f, 0.9f), "State", 0.85f);
            marker.useStateColorMap = true;
            marker.stateColorMap.useFallbackWhenNoEntryMatches = true;
            marker.stateColorMap.entries.Add(new PungentSceneGizmoStateColorEntry
            {
                name = "Enabled",
                priority = 1,
                sourceKind = PungentSceneGizmoStateSourceKind.Bool,
                memberPath = "enabled",
                expectedValue = "true",
                color = new Color(0.35f, 1f, 0.65f, 0.95f)
            });
            return One(marker);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateProbeRayRules()
        {
            PungentSceneGizmoSource.GizmoRule ray = Rule("Probe Ray", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.35f, 1f, 0.55f, 0.9f), "Probe", 2f);
            ray.direction = Vector3.down;
            ray.useTargetRotation = false;
            return One(ray);
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateProbeFanRules()
        {
            Color color = new Color(0.35f, 0.95f, 1f, 0.82f);
            return new List<PungentSceneGizmoSource.GizmoRule>
            {
                ProbeRule("Probe Center", Vector3.forward, color),
                ProbeRule("Probe Left", Quaternion.Euler(0f, -25f, 0f) * Vector3.forward, color),
                ProbeRule("Probe Right", Quaternion.Euler(0f, 25f, 0f) * Vector3.forward, color)
            };
        }

        private static PungentSceneGizmoSource.GizmoRule ProbeRule(string name, Vector3 direction, Color color)
        {
            PungentSceneGizmoSource.GizmoRule rule = Rule(name, PungentSceneGizmoSource.GizmoShape.Arrow, color, "Probe", 2.8f);
            rule.direction = direction;
            return rule;
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateColliderVolumeRules()
        {
            return One(Rule("Collider Volume", PungentSceneGizmoSource.GizmoShape.ColliderBounds, new Color(0.35f, 0.95f, 1f, 0.85f), "Collider", 1f));
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateTriggerVolumeRules()
        {
            return One(Rule("Trigger Volume", PungentSceneGizmoSource.GizmoShape.ColliderBounds, new Color(0.9f, 0.55f, 1f, 0.85f), "Trigger", 1f));
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateTrajectoryMarkerRules()
        {
            PungentSceneGizmoSource.GizmoRule origin = Rule("Trajectory Origin", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.2f, 0.9f, 1f, 0.95f), "Trajectory", 0.35f);
            PungentSceneGizmoSource.GizmoRule direction = Rule("Trajectory Direction", PungentSceneGizmoSource.GizmoShape.Arrow, new Color(0.25f, 0.95f, 1f, 0.8f), "Launch", 1.5f);
            return new List<PungentSceneGizmoSource.GizmoRule> { origin, direction };
        }

        private static List<PungentSceneGizmoSource.GizmoRule> CreateBeaconMarkerRules()
        {
            PungentSceneGizmoSource.GizmoRule marker = Rule("Beacon Marker", PungentSceneGizmoSource.GizmoShape.WireSphere, new Color(0.25f, 0.85f, 1f, 0.95f), "Beacon", 1f);
            PungentSceneGizmoSource.GizmoRule label = Rule("Beacon Label", PungentSceneGizmoSource.GizmoShape.Label, new Color(0.95f, 1f, 0.75f, 1f), "Beacon", 1f);
            return new List<PungentSceneGizmoSource.GizmoRule> { marker, label };
        }
    }
#endif
}
