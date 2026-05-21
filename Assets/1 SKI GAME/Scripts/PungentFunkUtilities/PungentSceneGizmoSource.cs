using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSceneGizmoPresetPropagationMode
    {
        Manual,
        AutoOnWorkspaceSave
    }

    /// <summary>
    /// Inspector-configurable generic scene gizmo source. Add this to any GameObject and define one or more gizmo rules
    /// without modifying the target component's code. Editor-only label/handle support is supplied by companion editor scripts.
    /// </summary>
    [ExecuteAlways]
    public sealed class PungentSceneGizmoSource : MonoBehaviour
    {
        public enum DrawWhen
        {
            Always,
            Selected,
            NotPlaying,
            Playing
        }

        public enum GizmoShape
        {
            Sphere,
            WireSphere,
            Cube,
            WireCube,
            Line,
            Ray,
            Arrow,
            Disc,
            Bounds,
            ColliderBounds,
            Label,
            DistanceBetween,
            ChildBounds,
            StateLabel
        }

        public enum PositionMode
        {
            Transform,
            LocalOffset,
            WorldPosition,
            FieldVector3,
            SecondaryTransform,
            MidpointToSecondary
        }

        public enum SizeMode
        {
            Constant,
            FieldFloat,
            FieldVector3Magnitude,
            DistanceToSecondary,
            BoundsMagnitude
        }

        public enum ConditionMode
        {
            Always,
            ObjectActive,
            ComponentEnabled,
            BoolFieldTrue,
            BoolFieldFalse,
            Equals,
            NotEquals,
            GreaterThan,
            LessThan,
            ObjectReferenceExists,
            ObjectReferenceMissing
        }

        public enum TemplateKind
        {
            TriggerRadius,
            ForwardRay,
            ColliderBounds,
            DistanceBetweenObjects,
            ChildBounds,
            StateLabel,
            FieldRadius,
            FieldPosition
        }

        public enum BindingValueKind
        {
            Any,
            Bool,
            Number,
            Vector2,
            Vector3,
            String,
            Enum,
            Object,
            Color
        }

        public enum RuleStatusSeverity
        {
            Valid,
            Info,
            Warning,
            Error
        }

        [Serializable]
        public sealed class GizmoRule
        {
            public string name = "Gizmo";
            public bool enabled = true;
            public DrawWhen drawWhen = DrawWhen.Selected;
            public GizmoShape shape = GizmoShape.WireSphere;
            public Color color = new Color(0.2f, 0.8f, 1f, 0.85f);
            public Component targetComponent;
            public Transform targetTransform;
            public Transform secondaryTransform;
            public Component secondaryComponent;
            public PositionMode positionMode = PositionMode.Transform;
            public Vector3 localOffset = Vector3.zero;
            public Vector3 worldPosition = Vector3.zero;
            public string positionFieldPath = string.Empty;
            public SizeMode sizeMode = SizeMode.Constant;
            public float size = 1f;
            public Vector3 vectorSize = Vector3.one;
            public string sizeFieldPath = string.Empty;
            public Vector3 direction = Vector3.forward;
            public string directionFieldPath = string.Empty;
            public string label = string.Empty;
            public string labelFieldPath = string.Empty;
            public bool useTargetRotation = true;
            public ConditionMode condition = ConditionMode.Always;
            public Component conditionComponent;
            public string conditionFieldPath = string.Empty;
            public string conditionExpectedValue = "true";
            [Tooltip("Maximum SceneView camera distance for editor labels/handles. Zero means unlimited.")]
            public float maxDrawDistance = 0f;
            [Tooltip("When enabled, this rule's editor label is shown only while the source object is selected.")]
            public bool drawLabelsWhenSelectedOnly = false;
            public bool useStateColorMap;
            public PungentSceneGizmoStateColorMap stateColorMap = new PungentSceneGizmoStateColorMap();
            public string presetId = string.Empty;
            public string presetCategory = string.Empty;
        }

        public sealed class RuleStatus
        {
            public int ruleIndex;
            public string ruleName;
            public RuleStatusSeverity severity = RuleStatusSeverity.Valid;
            public readonly List<string> messages = new List<string>();
            public bool hasInvalidBinding;
            public bool hasExpensiveRuleWarning;
            public string presetId;
            public string presetCategory;
            public int estimatedDrawOperations;
            public int labelCountEstimate;
            public int trajectorySampleEstimate;
            public string providerCategory;

            public bool IsValid => severity != RuleStatusSeverity.Error && !hasInvalidBinding;
        }

        private struct BindingCacheKey : IEquatable<BindingCacheKey>
        {
            private readonly Type _componentType;
            private readonly string _path;
            private readonly BindingValueKind _expectedKind;

            public BindingCacheKey(Type componentType, string path, BindingValueKind expectedKind)
            {
                _componentType = componentType;
                _path = path ?? string.Empty;
                _expectedKind = expectedKind;
            }

            public bool Equals(BindingCacheKey other)
            {
                return _componentType == other._componentType &&
                       string.Equals(_path, other._path, StringComparison.Ordinal) &&
                       _expectedKind == other._expectedKind;
            }

            public override bool Equals(object obj)
            {
                return obj is BindingCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _componentType != null ? _componentType.GetHashCode() : 0;
                    hash = (hash * 397) ^ (_path != null ? _path.GetHashCode() : 0);
                    hash = (hash * 397) ^ (int)_expectedKind;
                    return hash;
                }
            }
        }

        private sealed class ReflectedMemberBinding
        {
            public bool IsValid;
            public string Diagnostic;
            public Type ValueType;
            public BindingValueKind ExpectedKind;
            public MemberInfo[] Members;

            public bool TryGetValue(object root, out object value, out string diagnostic)
            {
                value = null;
                diagnostic = Diagnostic;

                if (!IsValid || Members == null || root == null)
                    return false;

                object current = root;
                for (int i = 0; i < Members.Length; i++)
                {
                    if (current == null)
                    {
                        diagnostic = $"'{Members[i].Name}' could not be read because an earlier value was null.";
                        return false;
                    }

                    try
                    {
                        if (Members[i] is FieldInfo field)
                            current = field.GetValue(current);
                        else if (Members[i] is PropertyInfo property)
                            current = property.GetValue(current, null);
                        else
                        {
                            diagnostic = $"'{Members[i].Name}' is not a supported field or property binding.";
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        diagnostic = $"'{Members[i].Name}' could not be read: {ex.Message}";
                        return false;
                    }
                }

                value = current;
                diagnostic = string.Empty;
                return true;
            }
        }

        private const BindingFlags ReflectedMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float DefaultChildBoundsRefreshInterval = 0.35f;
        private static readonly Dictionary<BindingCacheKey, ReflectedMemberBinding> BindingCache = new Dictionary<BindingCacheKey, ReflectedMemberBinding>();
        private static readonly HashSet<string> CloneRuleCopiedSerializedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(GizmoRule.name),
            nameof(GizmoRule.enabled),
            nameof(GizmoRule.drawWhen),
            nameof(GizmoRule.shape),
            nameof(GizmoRule.color),
            nameof(GizmoRule.targetComponent),
            nameof(GizmoRule.targetTransform),
            nameof(GizmoRule.secondaryTransform),
            nameof(GizmoRule.secondaryComponent),
            nameof(GizmoRule.positionMode),
            nameof(GizmoRule.localOffset),
            nameof(GizmoRule.worldPosition),
            nameof(GizmoRule.positionFieldPath),
            nameof(GizmoRule.sizeMode),
            nameof(GizmoRule.size),
            nameof(GizmoRule.vectorSize),
            nameof(GizmoRule.sizeFieldPath),
            nameof(GizmoRule.direction),
            nameof(GizmoRule.directionFieldPath),
            nameof(GizmoRule.label),
            nameof(GizmoRule.labelFieldPath),
            nameof(GizmoRule.useTargetRotation),
            nameof(GizmoRule.condition),
            nameof(GizmoRule.conditionComponent),
            nameof(GizmoRule.conditionFieldPath),
            nameof(GizmoRule.conditionExpectedValue),
            nameof(GizmoRule.maxDrawDistance),
            nameof(GizmoRule.drawLabelsWhenSelectedOnly),
            nameof(GizmoRule.useStateColorMap),
            nameof(GizmoRule.stateColorMap),
            nameof(GizmoRule.presetId),
            nameof(GizmoRule.presetCategory)
        };

        public bool drawInScene = true;
        public bool drawLabels = true;
        public bool drawOnlyWhenComponentEnabled = true;
        [Tooltip("Caches child renderer bounds for child-bounds gizmo rules so routine scene repaints do not rescan the hierarchy.")]
        public bool cacheChildRendererBounds = true;
        [Min(0.05f), Tooltip("Minimum seconds between child renderer bounds rebuilds for child-bounds gizmo rules.")]
        public float childBoundsRefreshInterval = DefaultChildBoundsRefreshInterval;
        [Tooltip("Preset asset this source is linked to for update/revert workflows. Built-in presets may only populate the ID fields.")]
        public PungentSceneGizmoPreset linkedPreset;
        [Tooltip("Asset GUID of the linked preset, stored so editor tools can recover the link after asset moves.")]
        public string linkedPresetGuid = string.Empty;
        [Tooltip("Stable preset ID used to detect built-in preset links and related scene instances.")]
        public string linkedPresetId = string.Empty;
        [Tooltip("Hash of the preset state when it was last applied or saved to this source.")]
        public string linkedPresetHash = string.Empty;
        [Tooltip("Prevents browser/workspace preset propagation from changing this source until unlocked.")]
        public bool lockPresetPropagation;
        [Tooltip("Controls whether this instance is updated only by explicit commands or after a workspace preset save.")]
        public PungentSceneGizmoPresetPropagationMode presetPropagationMode = PungentSceneGizmoPresetPropagationMode.Manual;
        public List<GizmoRule> rules = new List<GizmoRule>();

        [NonSerialized] private Bounds _cachedChildBounds;
        [NonSerialized] private bool _hasCachedChildBounds;
        [NonSerialized] private float _nextChildBoundsRefreshTime;
        [NonSerialized] private int _cachedChildRendererCount;
        [NonSerialized] private int _cachedChildCount = -1;

        public int CachedChildRendererCount => _cachedChildRendererCount;

        private void Reset()
        {
            AddTemplate(TemplateKind.TriggerRadius);
        }

        private void OnValidate()
        {
            childBoundsRefreshInterval = Mathf.Max(0.05f, childBoundsRefreshInterval);
            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    if (rules[i] != null)
                        rules[i].maxDrawDistance = Mathf.Max(0f, rules[i].maxDrawDistance);
                }
            }
            InvalidateChildBoundsCache();
        }

        public void AddTemplate(TemplateKind template)
        {
            GizmoRule rule = new GizmoRule { targetTransform = transform };
            switch (template)
            {
                case TemplateKind.TriggerRadius:
                    rule.name = "Trigger Radius";
                    rule.label = "Radius";
                    rule.shape = GizmoShape.WireSphere;
                    rule.size = 1f;
                    rule.presetId = "template-trigger-radius";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.ForwardRay:
                    rule.name = "Forward Ray";
                    rule.label = "Forward";
                    rule.shape = GizmoShape.Arrow;
                    rule.size = 3f;
                    rule.presetId = "template-forward-ray";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.ColliderBounds:
                    rule.name = "Collider Bounds";
                    rule.label = "Bounds";
                    rule.shape = GizmoShape.ColliderBounds;
                    rule.targetComponent = GetComponent<Collider>();
                    rule.presetId = "template-collider-bounds";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.DistanceBetweenObjects:
                    rule.name = "Distance";
                    rule.label = "Distance";
                    rule.shape = GizmoShape.DistanceBetween;
                    rule.positionMode = PositionMode.MidpointToSecondary;
                    rule.sizeMode = SizeMode.DistanceToSecondary;
                    rule.presetId = "template-distance";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.ChildBounds:
                    rule.name = "Child Bounds";
                    rule.label = "Children";
                    rule.shape = GizmoShape.ChildBounds;
                    rule.sizeMode = SizeMode.BoundsMagnitude;
                    rule.presetId = "template-child-bounds";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.StateLabel:
                    rule.name = "State Label";
                    rule.shape = GizmoShape.StateLabel;
                    rule.label = "State";
                    rule.useStateColorMap = true;
                    rule.presetId = "template-state-label";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.FieldRadius:
                    rule.name = "Field Radius";
                    rule.label = "Radius Field";
                    rule.shape = GizmoShape.WireSphere;
                    rule.sizeMode = SizeMode.FieldFloat;
                    rule.presetId = "template-field-radius";
                    rule.presetCategory = "Template";
                    break;
                case TemplateKind.FieldPosition:
                    rule.name = "Field Position";
                    rule.label = "Position Field";
                    rule.shape = GizmoShape.Sphere;
                    rule.positionMode = PositionMode.FieldVector3;
                    rule.presetId = "template-field-position";
                    rule.presetCategory = "Template";
                    break;
            }
            rules.Add(rule);
        }

        public void InvalidateChildBoundsCache()
        {
            _hasCachedChildBounds = false;
            _nextChildBoundsRefreshTime = 0f;
            _cachedChildRendererCount = 0;
            _cachedChildCount = -1;
        }

        private void OnDrawGizmos()
        {
            DrawRuntimeGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRuntimeGizmos(true);
        }

        public void DrawRuntimeGizmos(bool selectedPass)
        {
            if (!drawInScene || rules == null)
                return;

            for (int i = 0; i < rules.Count; i++)
            {
                GizmoRule rule = rules[i];
                if (!ShouldDraw(rule, selectedPass))
                    continue;

                Color previous = Gizmos.color;
                Gizmos.color = ResolveRuleColor(rule);
                DrawRule(rule);
                Gizmos.color = previous;
            }
        }

        public bool ShouldDraw(GizmoRule rule, bool selectedPass)
        {
            if (rule == null || !rule.enabled)
                return false;

            if (drawOnlyWhenComponentEnabled && !isActiveAndEnabled)
                return false;

            if (rule.drawWhen == DrawWhen.Selected && !selectedPass)
                return false;

            if (rule.drawWhen == DrawWhen.NotPlaying && Application.isPlaying)
                return false;

            if (rule.drawWhen == DrawWhen.Playing && !Application.isPlaying)
                return false;

            return ConditionPasses(rule);
        }

        public bool ConditionPasses(GizmoRule rule)
        {
            if (rule == null || rule.condition == ConditionMode.Always)
                return true;

            Component component = rule.conditionComponent != null ? rule.conditionComponent : rule.targetComponent;
            GameObject owner = component != null ? component.gameObject : gameObject;
            switch (rule.condition)
            {
                case ConditionMode.ObjectActive:
                    return owner != null && owner.activeInHierarchy;
                case ConditionMode.ComponentEnabled:
                    return component is Behaviour behaviour ? behaviour.enabled : component != null;
            }

            if (component == null || string.IsNullOrWhiteSpace(rule.conditionFieldPath))
                return false;

            BindingValueKind expectedKind = ExpectedKindForCondition(rule.condition);
            if (!TryResolveObjectValue(component, rule.conditionFieldPath, expectedKind, out object value, out _))
                return false;

            switch (rule.condition)
            {
                case ConditionMode.BoolFieldTrue:
                    return value is bool boolValue && boolValue;
                case ConditionMode.BoolFieldFalse:
                    return value is bool b2 && !b2;
                case ConditionMode.Equals:
                    return string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), rule.conditionExpectedValue, StringComparison.OrdinalIgnoreCase);
                case ConditionMode.NotEquals:
                    return !string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), rule.conditionExpectedValue, StringComparison.OrdinalIgnoreCase);
                case ConditionMode.GreaterThan:
                    return TryFloat(value, out float a) && TryFloat(rule.conditionExpectedValue, out float thresholdGreater) && a > thresholdGreater;
                case ConditionMode.LessThan:
                    return TryFloat(value, out float currentLess) && TryFloat(rule.conditionExpectedValue, out float thresholdLess) && currentLess < thresholdLess;
                case ConditionMode.ObjectReferenceExists:
                    return value != null && (!(value is UnityEngine.Object obj) || obj != null);
                case ConditionMode.ObjectReferenceMissing:
                    return value == null || (value is UnityEngine.Object obj2 && obj2 == null);
                default:
                    return true;
            }
        }

        public Vector3 ResolvePosition(GizmoRule rule)
        {
            if (rule == null)
                return transform.position;

            Transform anchor = rule.targetTransform != null ? rule.targetTransform : transform;
            switch (rule.positionMode)
            {
                case PositionMode.WorldPosition:
                    return rule.worldPosition;
                case PositionMode.LocalOffset:
                    return anchor.TransformPoint(rule.localOffset);
                case PositionMode.FieldVector3:
                    if (TryResolveFieldValue(rule.targetComponent, rule.positionFieldPath, BindingValueKind.Vector3, out Vector3 value))
                        return value;
                    return anchor.position;
                case PositionMode.SecondaryTransform:
                    return rule.secondaryTransform != null ? rule.secondaryTransform.position : anchor.position;
                case PositionMode.MidpointToSecondary:
                    return rule.secondaryTransform != null ? Vector3.Lerp(anchor.position, rule.secondaryTransform.position, 0.5f) : anchor.position;
                default:
                    return anchor.position + (rule.useTargetRotation ? anchor.rotation * rule.localOffset : rule.localOffset);
            }
        }

        public Vector3 ResolveDirection(GizmoRule rule)
        {
            if (rule == null)
                return transform.forward;

            Transform anchor = rule.targetTransform != null ? rule.targetTransform : transform;
            if (rule.secondaryTransform != null && (rule.shape == GizmoShape.DistanceBetween || rule.positionMode == PositionMode.MidpointToSecondary))
            {
                Vector3 delta = rule.secondaryTransform.position - anchor.position;
                if (delta.sqrMagnitude > 0.0001f)
                    return delta.normalized;
            }

            if (TryResolveFieldValue(rule.targetComponent, rule.directionFieldPath, BindingValueKind.Vector3, out Vector3 fieldDirection) && fieldDirection.sqrMagnitude > 0.0001f)
                return fieldDirection.normalized;

            Vector3 dir = rule.direction.sqrMagnitude > 0.0001f ? rule.direction.normalized : Vector3.forward;
            return rule.useTargetRotation ? anchor.rotation * dir : dir;
        }

        public float ResolveSize(GizmoRule rule)
        {
            if (rule == null)
                return 1f;

            switch (rule.sizeMode)
            {
                case SizeMode.FieldFloat:
                    if (TryResolveFieldValue(rule.targetComponent, rule.sizeFieldPath, BindingValueKind.Number, out float f))
                        return Mathf.Max(0.001f, f);
                    break;
                case SizeMode.FieldVector3Magnitude:
                    if (TryResolveFieldValue(rule.targetComponent, rule.sizeFieldPath, BindingValueKind.Vector3, out Vector3 v))
                        return Mathf.Max(0.001f, v.magnitude);
                    break;
                case SizeMode.DistanceToSecondary:
                    if (rule.secondaryTransform != null)
                    {
                        Transform anchor = rule.targetTransform != null ? rule.targetTransform : transform;
                        return Mathf.Max(0.001f, Vector3.Distance(anchor.position, rule.secondaryTransform.position));
                    }
                    break;
                case SizeMode.BoundsMagnitude:
                    if (TryGetChildBounds(out Bounds b))
                        return Mathf.Max(0.001f, b.extents.magnitude);
                    break;
            }

            return Mathf.Max(0.001f, rule.size);
        }

        public string ResolveLabel(GizmoRule rule)
        {
            if (rule == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(rule.labelFieldPath) && TryResolveObjectValue(rule.targetComponent, rule.labelFieldPath, BindingValueKind.Any, out object labelValue, out _))
                return Convert.ToString(labelValue, CultureInfo.InvariantCulture);

            if (rule.shape == GizmoShape.StateLabel && TryResolveStateColor(rule, out _, out string stateName) && !string.IsNullOrWhiteSpace(stateName))
                return (string.IsNullOrWhiteSpace(rule.label) ? "State" : rule.label) + ": " + stateName;

            if (rule.shape == GizmoShape.DistanceBetween && rule.secondaryTransform != null)
                return (string.IsNullOrWhiteSpace(rule.label) ? "Distance" : rule.label) + ": " + ResolveSize(rule).ToString("0.00", CultureInfo.InvariantCulture);

            return string.IsNullOrWhiteSpace(rule.label) ? rule.name : rule.label;
        }

        public Color ResolveRuleColor(GizmoRule rule)
        {
            return TryResolveStateColor(rule, out Color color, out _) ? color : (rule != null ? rule.color : Color.white);
        }

        public bool TryResolveStateColor(GizmoRule rule, out Color color, out string stateName)
        {
            color = rule != null ? rule.color : Color.white;
            stateName = string.Empty;
            if (rule == null || !rule.useStateColorMap || rule.stateColorMap == null)
                return false;

            return rule.stateColorMap.TryResolve(rule.targetComponent, gameObject, out color, out stateName);
        }

        private void DrawRule(GizmoRule rule)
        {
            Vector3 position = ResolvePosition(rule);
            Vector3 direction = ResolveDirection(rule);
            float size = ResolveSize(rule);

            switch (rule.shape)
            {
                case GizmoShape.Sphere:
                    Gizmos.DrawSphere(position, size);
                    break;
                case GizmoShape.WireSphere:
                    Gizmos.DrawWireSphere(position, size);
                    break;
                case GizmoShape.Cube:
                    Gizmos.DrawCube(position, rule.vectorSize == Vector3.zero ? Vector3.one * size : rule.vectorSize);
                    break;
                case GizmoShape.WireCube:
                case GizmoShape.Bounds:
                    Gizmos.DrawWireCube(position, rule.vectorSize == Vector3.zero ? Vector3.one * size : rule.vectorSize);
                    break;
                case GizmoShape.Line:
                case GizmoShape.Ray:
                case GizmoShape.Arrow:
                    Gizmos.DrawLine(position, position + direction * size);
                    break;
                case GizmoShape.DistanceBetween:
                    if (rule.secondaryTransform != null)
                    {
                        Transform anchor = rule.targetTransform != null ? rule.targetTransform : transform;
                        Gizmos.DrawLine(anchor.position, rule.secondaryTransform.position);
                    }
                    break;
                case GizmoShape.ColliderBounds:
                    Collider c = rule.targetComponent as Collider;
                    if (c == null)
                        c = GetComponent<Collider>();
                    if (c != null)
                        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
                    break;
                case GizmoShape.ChildBounds:
                    if (TryGetChildBounds(out Bounds b))
                        Gizmos.DrawWireCube(b.center, b.size);
                    break;
                case GizmoShape.Disc:
                case GizmoShape.Label:
                case GizmoShape.StateLabel:
                    break;
            }
        }

        private bool TryGetChildBounds(out Bounds bounds)
        {
            if (cacheChildRendererBounds && _hasCachedChildBounds && Time.realtimeSinceStartup < _nextChildBoundsRefreshTime && _cachedChildCount == transform.childCount)
            {
                bounds = _cachedChildBounds;
                return _cachedChildRendererCount > 0;
            }

            return RebuildChildBoundsCache(out bounds);
        }

        private bool RebuildChildBoundsCache(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            _cachedChildRendererCount = renderers == null ? 0 : renderers.Length;
            _cachedChildCount = transform.childCount;

            if (renderers == null || renderers.Length == 0)
            {
                bounds = new Bounds(transform.position, Vector3.zero);
                _cachedChildBounds = bounds;
                _hasCachedChildBounds = true;
                _nextChildBoundsRefreshTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, childBoundsRefreshInterval);
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            _cachedChildBounds = bounds;
            _hasCachedChildBounds = true;
            _nextChildBoundsRefreshTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, childBoundsRefreshInterval);
            return true;
        }

        public RuleStatus GetRuleStatus(int index, bool includeChildBoundsEstimate = false)
        {
            RuleStatus status = new RuleStatus { ruleIndex = index };
            if (rules == null || index < 0 || index >= rules.Count)
            {
                status.severity = RuleStatusSeverity.Error;
                status.messages.Add("Rule index is outside the source rule list.");
                status.hasInvalidBinding = true;
                return status;
            }

            AppendRuleStatus(rules[index], status, includeChildBoundsEstimate);
            return status;
        }

        public List<RuleStatus> GetRuleStatuses(bool includeChildBoundsEstimate = false)
        {
            List<RuleStatus> statuses = new List<RuleStatus>();
            if (rules == null)
                return statuses;

            for (int i = 0; i < rules.Count; i++)
                statuses.Add(GetRuleStatus(i, includeChildBoundsEstimate));

            return statuses;
        }

        public int EstimateEnabledRuleCount()
        {
            int count = 0;
            if (rules == null)
                return count;

            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].enabled)
                    count++;
            }

            return count;
        }

        public int EstimateDrawOperations()
        {
            if (!drawInScene || rules == null)
                return 0;

            int count = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null || !rules[i].enabled)
                    continue;

                count += EstimateRuleDrawOperations(rules[i]);
            }

            return count;
        }

        public int EstimateLabelCount()
        {
            if (!drawInScene || !drawLabels || rules == null)
                return 0;

            int count = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null || !rules[i].enabled)
                    continue;

                count += EstimateRuleLabelCount(rules[i]);
            }

            return count;
        }

        private static int EstimateRuleDrawOperations(GizmoRule rule)
        {
            if (rule == null || !rule.enabled)
                return 0;

            switch (rule.shape)
            {
                case GizmoShape.Line:
                case GizmoShape.Ray:
                case GizmoShape.Arrow:
                    return 2 + EstimateRuleLabelCount(rule);
                case GizmoShape.DistanceBetween:
                    return 2 + EstimateRuleLabelCount(rule);
                case GizmoShape.ChildBounds:
                    return 4 + EstimateRuleLabelCount(rule);
                case GizmoShape.ColliderBounds:
                case GizmoShape.Bounds:
                case GizmoShape.Cube:
                case GizmoShape.WireCube:
                    return 2 + EstimateRuleLabelCount(rule);
                case GizmoShape.Label:
                case GizmoShape.StateLabel:
                    return 1;
                default:
                    return 1 + EstimateRuleLabelCount(rule);
            }
        }

        private static int EstimateRuleLabelCount(GizmoRule rule)
        {
            if (rule == null || !rule.enabled)
                return 0;

            if (rule.shape == GizmoShape.Label || rule.shape == GizmoShape.StateLabel || rule.shape == GizmoShape.DistanceBetween)
                return 1;

            return string.IsNullOrWhiteSpace(rule.label) && string.IsNullOrWhiteSpace(rule.labelFieldPath) ? 0 : 1;
        }

        private void AppendRuleStatus(GizmoRule rule, RuleStatus status, bool includeChildBoundsEstimate)
        {
            status.ruleName = rule != null && !string.IsNullOrWhiteSpace(rule.name) ? rule.name : "Gizmo Rule";
            status.presetId = rule != null ? rule.presetId : string.Empty;
            status.presetCategory = rule != null ? rule.presetCategory : string.Empty;
            status.providerCategory = "Source Rule";

            if (rule == null)
            {
                AddStatus(status, RuleStatusSeverity.Error, "Rule is null.", invalidBinding: true);
                return;
            }

            status.estimatedDrawOperations = EstimateRuleDrawOperations(rule);
            status.labelCountEstimate = EstimateRuleLabelCount(rule);
            status.trajectorySampleEstimate = 0;

            if (!rule.enabled)
                AddStatus(status, RuleStatusSeverity.Info, "Rule is disabled.");

            if (rule.positionMode == PositionMode.FieldVector3)
                ValidateBinding(status, rule.targetComponent, rule.positionFieldPath, BindingValueKind.Vector3, "Position field");

            if (rule.sizeMode == SizeMode.FieldFloat)
                ValidateBinding(status, rule.targetComponent, rule.sizeFieldPath, BindingValueKind.Number, "Size field");

            if (rule.sizeMode == SizeMode.FieldVector3Magnitude)
                ValidateBinding(status, rule.targetComponent, rule.sizeFieldPath, BindingValueKind.Vector3, "Size vector field");

            if (!string.IsNullOrWhiteSpace(rule.directionFieldPath))
                ValidateBinding(status, rule.targetComponent, rule.directionFieldPath, BindingValueKind.Vector3, "Direction field");

            if (!string.IsNullOrWhiteSpace(rule.labelFieldPath))
                ValidateBinding(status, rule.targetComponent, rule.labelFieldPath, BindingValueKind.Any, "Label field");

            if (RequiresSecondaryTransform(rule) && rule.secondaryTransform == null)
                AddStatus(status, RuleStatusSeverity.Warning, "Secondary transform is required for this rule but is unresolved.");

            if (rule.shape == GizmoShape.ColliderBounds)
            {
                Collider collider = rule.targetComponent as Collider;
                if (collider == null && GetComponent<Collider>() == null)
                    AddStatus(status, RuleStatusSeverity.Warning, "Collider bounds rule has no collider on the target component or source GameObject.");
            }

            if (rule.condition != ConditionMode.Always)
            {
                Component conditionComponent = rule.conditionComponent != null ? rule.conditionComponent : rule.targetComponent;
                if (RequiresConditionBinding(rule.condition))
                    ValidateBinding(status, conditionComponent, rule.conditionFieldPath, ExpectedKindForCondition(rule.condition), "Condition field");
                else if (conditionComponent == null && (rule.condition == ConditionMode.ComponentEnabled || rule.condition == ConditionMode.ObjectActive))
                    AddStatus(status, RuleStatusSeverity.Warning, "Condition component is unresolved.");
            }

            bool childBoundsRule = rule.shape == GizmoShape.ChildBounds || rule.sizeMode == SizeMode.BoundsMagnitude;
            if (childBoundsRule)
            {
                status.hasExpensiveRuleWarning = true;
                string detail = cacheChildRendererBounds
                    ? $"Child renderer bounds are cached every {Mathf.Max(0.05f, childBoundsRefreshInterval):0.00}s."
                    : "Child renderer bounds caching is disabled.";

                if (includeChildBoundsEstimate)
                    detail += $" Last renderer count: {_cachedChildRendererCount}.";

                AddStatus(status, cacheChildRendererBounds ? RuleStatusSeverity.Info : RuleStatusSeverity.Warning, detail, expensive: !cacheChildRendererBounds);
            }

            if (rule.useStateColorMap && rule.stateColorMap != null)
            {
                rule.stateColorMap.AppendDiagnostics(rule.targetComponent, gameObject, message =>
                {
                    AddStatus(status, message.IsError ? RuleStatusSeverity.Error : RuleStatusSeverity.Warning, message.Text, invalidBinding: message.IsError);
                });
            }

            if (status.messages.Count == 0)
                status.messages.Add("Ready.");
        }

        private void ValidateBinding(RuleStatus status, Component component, string path, BindingValueKind expectedKind, string label)
        {
            if (component == null)
            {
                AddStatus(status, RuleStatusSeverity.Warning, $"{label} needs a component target.");
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                AddStatus(status, RuleStatusSeverity.Warning, $"{label} path is empty.");
                return;
            }

            ReflectedMemberBinding binding = GetBinding(component.GetType(), path, expectedKind);
            if (!binding.IsValid)
                AddStatus(status, RuleStatusSeverity.Error, $"{label}: {binding.Diagnostic}", invalidBinding: true);
        }

        private static void AddStatus(RuleStatus status, RuleStatusSeverity severity, string message, bool invalidBinding = false, bool expensive = false)
        {
            if (status == null || string.IsNullOrWhiteSpace(message))
                return;

            status.messages.Add(message);
            if ((int)severity > (int)status.severity)
                status.severity = severity;
            if (invalidBinding)
                status.hasInvalidBinding = true;
            if (expensive)
                status.hasExpensiveRuleWarning = true;
        }

        private static bool RequiresSecondaryTransform(GizmoRule rule)
        {
            if (rule == null)
                return false;

            return rule.shape == GizmoShape.DistanceBetween ||
                   rule.positionMode == PositionMode.SecondaryTransform ||
                   rule.positionMode == PositionMode.MidpointToSecondary ||
                   rule.sizeMode == SizeMode.DistanceToSecondary;
        }

        private static bool RequiresConditionBinding(ConditionMode mode)
        {
            return mode == ConditionMode.BoolFieldTrue ||
                   mode == ConditionMode.BoolFieldFalse ||
                   mode == ConditionMode.Equals ||
                   mode == ConditionMode.NotEquals ||
                   mode == ConditionMode.GreaterThan ||
                   mode == ConditionMode.LessThan ||
                   mode == ConditionMode.ObjectReferenceExists ||
                   mode == ConditionMode.ObjectReferenceMissing;
        }

        private static BindingValueKind ExpectedKindForCondition(ConditionMode mode)
        {
            switch (mode)
            {
                case ConditionMode.BoolFieldTrue:
                case ConditionMode.BoolFieldFalse:
                    return BindingValueKind.Bool;
                case ConditionMode.GreaterThan:
                case ConditionMode.LessThan:
                    return BindingValueKind.Number;
                case ConditionMode.ObjectReferenceExists:
                case ConditionMode.ObjectReferenceMissing:
                    return BindingValueKind.Object;
                default:
                    return BindingValueKind.Any;
            }
        }

        public static GizmoRule CloneRule(GizmoRule source)
        {
            if (source == null)
                return null;

            return new GizmoRule
            {
                name = source.name,
                enabled = source.enabled,
                drawWhen = source.drawWhen,
                shape = source.shape,
                color = source.color,
                targetComponent = source.targetComponent,
                targetTransform = source.targetTransform,
                secondaryTransform = source.secondaryTransform,
                secondaryComponent = source.secondaryComponent,
                positionMode = source.positionMode,
                localOffset = source.localOffset,
                worldPosition = source.worldPosition,
                positionFieldPath = source.positionFieldPath,
                sizeMode = source.sizeMode,
                size = source.size,
                vectorSize = source.vectorSize,
                sizeFieldPath = source.sizeFieldPath,
                direction = source.direction,
                directionFieldPath = source.directionFieldPath,
                label = source.label,
                labelFieldPath = source.labelFieldPath,
                useTargetRotation = source.useTargetRotation,
                condition = source.condition,
                conditionComponent = source.conditionComponent,
                conditionFieldPath = source.conditionFieldPath,
                conditionExpectedValue = source.conditionExpectedValue,
                maxDrawDistance = source.maxDrawDistance,
                drawLabelsWhenSelectedOnly = source.drawLabelsWhenSelectedOnly,
                useStateColorMap = source.useStateColorMap,
                stateColorMap = PungentSceneGizmoStateColorMap.Clone(source.stateColorMap),
                presetId = source.presetId,
                presetCategory = source.presetCategory
            };
        }

        public static List<string> GetMissingCloneRuleSerializedFieldsForAudit()
        {
            List<string> missing = new List<string>();
            FieldInfo[] fields = typeof(GizmoRule).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
                    continue;

                bool serializedByUnity = field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
                if (!serializedByUnity)
                    continue;

                if (!CloneRuleCopiedSerializedFields.Contains(field.Name))
                    missing.Add(field.Name);
            }

            return missing;
        }

        public static bool TryResolveFieldValue<T>(Component component, string path, out T value)
        {
            return TryResolveFieldValue(component, path, ExpectedKindForType(typeof(T)), out value);
        }

        public static bool TryResolveFieldValue<T>(Component component, string path, BindingValueKind expectedKind, out T value)
        {
            value = default;
            if (!TryResolveObjectValue(component, path, expectedKind, out object current, out _))
                return false;

            if (current is T typed)
            {
                value = typed;
                return true;
            }

            try
            {
                value = (T)Convert.ChangeType(current, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryResolveObjectValue(Component component, string path, out object value)
        {
            return TryResolveObjectValue(component, path, BindingValueKind.Any, out value, out _);
        }

        public static bool TryResolveObjectValue(Component component, string path, BindingValueKind expectedKind, out object value, out string diagnostic)
        {
            value = null;
            diagnostic = string.Empty;
            if (component == null)
            {
                diagnostic = "Component target is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                diagnostic = "Member path is empty.";
                return false;
            }

            ReflectedMemberBinding binding = GetBinding(component.GetType(), path, expectedKind);
            if (!binding.IsValid)
            {
                diagnostic = binding.Diagnostic;
                return false;
            }

            return binding.TryGetValue(component, out value, out diagnostic);
        }

        public static bool TryResolveFirstObjectValue(Component component, string[] candidatePaths, BindingValueKind expectedKind, out object value, out string resolvedPath)
        {
            value = null;
            resolvedPath = string.Empty;
            if (component == null || candidatePaths == null)
                return false;

            for (int i = 0; i < candidatePaths.Length; i++)
            {
                string candidate = candidatePaths[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (TryResolveObjectValue(component, candidate, expectedKind, out value, out _))
                {
                    resolvedPath = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool IsSupportedBindingType(Type type, BindingValueKind expectedKind)
        {
            if (type == null)
                return expectedKind == BindingValueKind.Any;

            switch (expectedKind)
            {
                case BindingValueKind.Bool:
                    return type == typeof(bool);
                case BindingValueKind.Number:
                    return IsNumericType(type);
                case BindingValueKind.Vector2:
                    return type == typeof(Vector2);
                case BindingValueKind.Vector3:
                    return type == typeof(Vector3);
                case BindingValueKind.String:
                    return type == typeof(string);
                case BindingValueKind.Enum:
                    return type.IsEnum;
                case BindingValueKind.Object:
                    return !type.IsValueType || typeof(UnityEngine.Object).IsAssignableFrom(type);
                case BindingValueKind.Color:
                    return type == typeof(Color);
                default:
                    return type == typeof(bool) ||
                           type == typeof(string) ||
                           type.IsEnum ||
                           IsNumericType(type) ||
                           type == typeof(Vector2) ||
                           type == typeof(Vector3) ||
                           type == typeof(Color) ||
                           typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                           !type.IsValueType;
            }
        }

        public static BindingValueKind ExpectedKindForType(Type type)
        {
            if (type == typeof(bool))
                return BindingValueKind.Bool;
            if (type == typeof(Vector2))
                return BindingValueKind.Vector2;
            if (type == typeof(Vector3))
                return BindingValueKind.Vector3;
            if (type == typeof(string))
                return BindingValueKind.String;
            if (type == typeof(Color))
                return BindingValueKind.Color;
            if (type != null && type.IsEnum)
                return BindingValueKind.Enum;
            if (IsNumericType(type))
                return BindingValueKind.Number;
            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                return BindingValueKind.Object;
            return BindingValueKind.Any;
        }

        private static ReflectedMemberBinding GetBinding(Type componentType, string path, BindingValueKind expectedKind)
        {
            BindingCacheKey key = new BindingCacheKey(componentType, path, expectedKind);
            if (BindingCache.TryGetValue(key, out ReflectedMemberBinding binding))
                return binding;

            binding = BuildBinding(componentType, path, expectedKind);
            BindingCache[key] = binding;
            return binding;
        }

        private static ReflectedMemberBinding BuildBinding(Type componentType, string path, BindingValueKind expectedKind)
        {
            ReflectedMemberBinding binding = new ReflectedMemberBinding
            {
                IsValid = false,
                ExpectedKind = expectedKind,
                Diagnostic = "Binding has not been resolved."
            };

            if (componentType == null)
            {
                binding.Diagnostic = "Component type is missing.";
                return binding;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                binding.Diagnostic = "Member path is empty.";
                return binding;
            }

            string[] parts = path.Split('.');
            List<MemberInfo> members = new List<MemberInfo>(parts.Length);
            Type currentType = componentType;

            for (int i = 0; i < parts.Length; i++)
            {
                string memberName = parts[i];
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    binding.Diagnostic = $"Member path '{path}' contains an empty segment.";
                    return binding;
                }

                FieldInfo field = currentType.GetField(memberName, ReflectedMemberFlags);
                if (field != null)
                {
                    members.Add(field);
                    currentType = field.FieldType;
                    continue;
                }

                PropertyInfo property = currentType.GetProperty(memberName, ReflectedMemberFlags);
                if (property != null)
                {
                    if (property.GetIndexParameters().Length != 0)
                    {
                        binding.Diagnostic = $"Property '{memberName}' on {currentType.Name} is indexed and cannot be used for gizmo binding.";
                        return binding;
                    }

                    if (!property.CanRead)
                    {
                        binding.Diagnostic = $"Property '{memberName}' on {currentType.Name} is not readable.";
                        return binding;
                    }

                    members.Add(property);
                    currentType = property.PropertyType;
                    continue;
                }

                binding.Diagnostic = $"Member '{memberName}' was not found on {currentType.Name}.";
                return binding;
            }

            if (!IsSupportedBindingType(currentType, expectedKind))
            {
                binding.Diagnostic = $"Member path '{path}' resolves to {currentType.Name}, which is not compatible with expected {expectedKind}.";
                return binding;
            }

            binding.IsValid = true;
            binding.ValueType = currentType;
            binding.Members = members.ToArray();
            binding.Diagnostic = string.Empty;
            return binding;
        }

        private static bool IsNumericType(Type type)
        {
            if (type == null || type.IsEnum)
                return false;

            return type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal);
        }

        private static bool TryFloat(object value, out float result)
        {
            if (value is float f)
            {
                result = f;
                return true;
            }

            if (value is double d)
            {
                result = (float)d;
                return true;
            }

            if (value is int i)
            {
                result = i;
                return true;
            }

            return float.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
