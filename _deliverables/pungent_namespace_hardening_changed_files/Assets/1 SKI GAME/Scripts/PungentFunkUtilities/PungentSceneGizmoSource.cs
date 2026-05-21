using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
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
        }

        public bool drawInScene = true;
        public bool drawLabels = true;
        public bool drawOnlyWhenComponentEnabled = true;
        public List<GizmoRule> rules = new List<GizmoRule>();

        private void Reset()
        {
            AddTemplate(TemplateKind.TriggerRadius);
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
                    break;
                case TemplateKind.ForwardRay:
                    rule.name = "Forward Ray";
                    rule.label = "Forward";
                    rule.shape = GizmoShape.Arrow;
                    rule.size = 3f;
                    break;
                case TemplateKind.ColliderBounds:
                    rule.name = "Collider Bounds";
                    rule.label = "Bounds";
                    rule.shape = GizmoShape.ColliderBounds;
                    rule.targetComponent = GetComponent<Collider>();
                    break;
                case TemplateKind.DistanceBetweenObjects:
                    rule.name = "Distance";
                    rule.label = "Distance";
                    rule.shape = GizmoShape.DistanceBetween;
                    rule.positionMode = PositionMode.MidpointToSecondary;
                    rule.sizeMode = SizeMode.DistanceToSecondary;
                    break;
                case TemplateKind.ChildBounds:
                    rule.name = "Child Bounds";
                    rule.label = "Children";
                    rule.shape = GizmoShape.ChildBounds;
                    rule.sizeMode = SizeMode.BoundsMagnitude;
                    break;
                case TemplateKind.StateLabel:
                    rule.name = "State Label";
                    rule.shape = GizmoShape.StateLabel;
                    rule.label = "State";
                    break;
                case TemplateKind.FieldRadius:
                    rule.name = "Field Radius";
                    rule.label = "Radius Field";
                    rule.shape = GizmoShape.WireSphere;
                    rule.sizeMode = SizeMode.FieldFloat;
                    break;
                case TemplateKind.FieldPosition:
                    rule.name = "Field Position";
                    rule.label = "Position Field";
                    rule.shape = GizmoShape.Sphere;
                    rule.positionMode = PositionMode.FieldVector3;
                    break;
            }
            rules.Add(rule);
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
                Gizmos.color = rule.color;
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

            if (!TryResolveObjectValue(component, rule.conditionFieldPath, out object value))
                return false;

            switch (rule.condition)
            {
                case ConditionMode.BoolFieldTrue:
                    return value is bool boolValue && boolValue;
                case ConditionMode.BoolFieldFalse:
                    return value is bool b2 && !b2;
                case ConditionMode.Equals:
                    return string.Equals(Convert.ToString(value), rule.conditionExpectedValue, StringComparison.OrdinalIgnoreCase);
                case ConditionMode.NotEquals:
                    return !string.Equals(Convert.ToString(value), rule.conditionExpectedValue, StringComparison.OrdinalIgnoreCase);
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
                    if (TryResolveFieldValue(rule.targetComponent, rule.positionFieldPath, out Vector3 value))
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

            if (TryResolveFieldValue(rule.targetComponent, rule.directionFieldPath, out Vector3 fieldDirection) && fieldDirection.sqrMagnitude > 0.0001f)
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
                    if (TryResolveFieldValue(rule.targetComponent, rule.sizeFieldPath, out float f))
                        return Mathf.Max(0.001f, f);
                    break;
                case SizeMode.FieldVector3Magnitude:
                    if (TryResolveFieldValue(rule.targetComponent, rule.sizeFieldPath, out Vector3 v))
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

            if (!string.IsNullOrWhiteSpace(rule.labelFieldPath) && TryResolveObjectValue(rule.targetComponent, rule.labelFieldPath, out object labelValue))
                return Convert.ToString(labelValue);

            if (rule.shape == GizmoShape.DistanceBetween && rule.secondaryTransform != null)
                return (string.IsNullOrWhiteSpace(rule.label) ? "Distance" : rule.label) + ": " + ResolveSize(rule).ToString("0.00");

            return string.IsNullOrWhiteSpace(rule.label) ? rule.name : rule.label;
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
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                bounds = new Bounds(transform.position, Vector3.zero);
                return false;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        public static bool TryResolveFieldValue<T>(Component component, string path, out T value)
        {
            value = default;
            if (!TryResolveObjectValue(component, path, out object current))
                return false;

            if (current is T typed)
            {
                value = typed;
                return true;
            }

            try
            {
                value = (T)Convert.ChangeType(current, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryResolveObjectValue(Component component, string path, out object value)
        {
            value = null;
            if (component == null || string.IsNullOrWhiteSpace(path))
                return false;

            object current = component;
            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null)
                    return false;

                Type type = current.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo field = type.GetField(parts[i], flags);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                PropertyInfo property = type.GetProperty(parts[i], flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    current = property.GetValue(current, null);
                    continue;
                }

                return false;
            }

            value = current;
            return true;
        }

        private static bool TryFloat(object value, out float result)
        {
            return float.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
        }
    }

}