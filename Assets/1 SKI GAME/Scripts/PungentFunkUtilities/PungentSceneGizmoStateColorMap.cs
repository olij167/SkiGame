using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSceneGizmoStateSourceKind
    {
        Bool,
        Enum,
        String,
        NumericThreshold,
        AnimatorParameter,
        ActiveStateProvider
    }

    public enum PungentSceneGizmoNumericComparison
    {
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        Equal,
        NotEqual,
        BetweenInclusive
    }

    [Serializable]
    public sealed class PungentSceneGizmoStateColorEntry
    {
        [Tooltip("Human-readable state label used in diagnostics and state labels.")]
        public string name = "State";
        [Tooltip("Disabled entries are ignored without removing their configuration.")]
        public bool enabled = true;
        [Tooltip("Higher priority entries override lower priority matches.")]
        public int priority;
        [Tooltip("Color used when this state entry matches.")]
        public Color color = new Color(0.25f, 0.85f, 1f, 0.9f);
        [Tooltip("How this entry reads the active state.")]
        public PungentSceneGizmoStateSourceKind sourceKind = PungentSceneGizmoStateSourceKind.Bool;
        [Tooltip("Optional component override. If empty, the gizmo rule target component is used.")]
        public Component component;
        [Tooltip("Reflected field or readable non-indexed property path.")]
        public string memberPath = string.Empty;
        [Tooltip("Expected value for bool, enum, string, and active-state-provider matches.")]
        public string expectedValue = "true";
        [Tooltip("Numeric comparison used for numeric and Animator numeric parameters.")]
        public PungentSceneGizmoNumericComparison numericComparison = PungentSceneGizmoNumericComparison.GreaterThan;
        [Tooltip("Primary numeric threshold.")]
        public float numericThreshold;
        [Tooltip("Upper threshold used by Between Inclusive.")]
        public float numericThresholdMax = 1f;
        [Tooltip("Optional Animator override. If empty, the owner GameObject Animator is used.")]
        public Animator animator;
        [Tooltip("Animator bool, float, or int parameter name.")]
        public string animatorParameter = string.Empty;
        [Tooltip("Optional reflected state path for project adapters. Leave empty to try common generic state names.")]
        public string activeStateProviderPath = string.Empty;
    }

    [Serializable]
    public sealed class PungentSceneGizmoStateColorMap
    {
        private static readonly string[] ActiveStateProviderCandidatePaths =
        {
            "CurrentState",
            "currentState",
            "ActiveState",
            "activeState",
            "State",
            "state",
            "MovementState",
            "movementState",
            "CurrentStateName",
            "currentStateName"
        };

        [Tooltip("When disabled, the rule uses its base color.")]
        public bool enabled = true;
        [Tooltip("Color used when fallback matching is enabled and no entry matches.")]
        public Color fallbackColor = new Color(0.2f, 0.8f, 1f, 0.85f);
        [Tooltip("When enabled, the fallback color is applied even if no state entry matches.")]
        public bool useFallbackWhenNoEntryMatches;
        [Tooltip("Priority-ordered state color entries.")]
        public List<PungentSceneGizmoStateColorEntry> entries = new List<PungentSceneGizmoStateColorEntry>();

        public bool TryResolve(Component defaultComponent, GameObject owner, out Color color, out string stateName)
        {
            color = fallbackColor;
            stateName = string.Empty;
            if (!enabled || entries == null || entries.Count == 0)
                return false;

            PungentSceneGizmoStateColorEntry best = null;
            for (int i = 0; i < entries.Count; i++)
            {
                PungentSceneGizmoStateColorEntry entry = entries[i];
                if (entry == null || !entry.enabled)
                    continue;

                if (!EntryMatches(entry, defaultComponent, owner, out string matchedName))
                    continue;

                if (best == null || entry.priority > best.priority)
                {
                    best = entry;
                    stateName = string.IsNullOrWhiteSpace(matchedName) ? entry.name : matchedName;
                }
            }

            if (best != null)
            {
                color = best.color;
                if (string.IsNullOrWhiteSpace(stateName))
                    stateName = best.name;
                return true;
            }

            return useFallbackWhenNoEntryMatches;
        }

        public void AppendDiagnostics(Component defaultComponent, GameObject owner, Action<DiagnosticMessage> append)
        {
            if (append == null || !enabled || entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                PungentSceneGizmoStateColorEntry entry = entries[i];
                if (entry == null || !entry.enabled)
                    continue;

                string prefix = string.IsNullOrWhiteSpace(entry.name) ? $"State entry {i + 1}" : $"State entry '{entry.name}'";
                Component component = entry.component != null ? entry.component : defaultComponent;

                switch (entry.sourceKind)
                {
                    case PungentSceneGizmoStateSourceKind.AnimatorParameter:
                        Animator animator = entry.animator != null ? entry.animator : (owner != null ? owner.GetComponent<Animator>() : null);
                        if (animator == null)
                            append(new DiagnosticMessage(false, $"{prefix} needs an Animator."));
                        else if (string.IsNullOrWhiteSpace(entry.animatorParameter))
                            append(new DiagnosticMessage(true, $"{prefix} animator parameter is empty."));
                        break;
                    case PungentSceneGizmoStateSourceKind.ActiveStateProvider:
                        if (component == null)
                        {
                            append(new DiagnosticMessage(false, $"{prefix} needs a component target for active-state-provider reflection."));
                        }
                        else if (!string.IsNullOrWhiteSpace(entry.activeStateProviderPath))
                        {
                            if (!PungentSceneGizmoSource.TryResolveObjectValue(component, entry.activeStateProviderPath, PungentSceneGizmoSource.BindingValueKind.Any, out _, out string diagnostic))
                                append(new DiagnosticMessage(true, $"{prefix}: {diagnostic}"));
                        }
                        else if (!PungentSceneGizmoSource.TryResolveFirstObjectValue(component, ActiveStateProviderCandidatePaths, PungentSceneGizmoSource.BindingValueKind.Any, out _, out _))
                        {
                            append(new DiagnosticMessage(false, $"{prefix} has no common active-state property. Set Active State Provider Path to bridge project-specific state components."));
                        }
                        break;
                    default:
                        if (component == null)
                        {
                            append(new DiagnosticMessage(false, $"{prefix} needs a component target."));
                        }
                        else if (string.IsNullOrWhiteSpace(entry.memberPath))
                        {
                            append(new DiagnosticMessage(true, $"{prefix} member path is empty."));
                        }
                        else
                        {
                            PungentSceneGizmoSource.BindingValueKind expectedKind = ExpectedKind(entry.sourceKind);
                            if (!PungentSceneGizmoSource.TryResolveObjectValue(component, entry.memberPath, expectedKind, out _, out string diagnostic))
                                append(new DiagnosticMessage(true, $"{prefix}: {diagnostic}"));
                        }
                        break;
                }
            }
        }

        public static PungentSceneGizmoStateColorMap Clone(PungentSceneGizmoStateColorMap source)
        {
            PungentSceneGizmoStateColorMap clone = new PungentSceneGizmoStateColorMap();
            if (source == null)
                return clone;

            clone.enabled = source.enabled;
            clone.fallbackColor = source.fallbackColor;
            clone.useFallbackWhenNoEntryMatches = source.useFallbackWhenNoEntryMatches;
            clone.entries.Clear();

            if (source.entries != null)
            {
                for (int i = 0; i < source.entries.Count; i++)
                {
                    PungentSceneGizmoStateColorEntry entry = source.entries[i];
                    if (entry == null)
                        continue;

                    clone.entries.Add(new PungentSceneGizmoStateColorEntry
                    {
                        name = entry.name,
                        enabled = entry.enabled,
                        priority = entry.priority,
                        color = entry.color,
                        sourceKind = entry.sourceKind,
                        component = entry.component,
                        memberPath = entry.memberPath,
                        expectedValue = entry.expectedValue,
                        numericComparison = entry.numericComparison,
                        numericThreshold = entry.numericThreshold,
                        numericThresholdMax = entry.numericThresholdMax,
                        animator = entry.animator,
                        animatorParameter = entry.animatorParameter,
                        activeStateProviderPath = entry.activeStateProviderPath
                    });
                }
            }

            return clone;
        }

        private static bool EntryMatches(PungentSceneGizmoStateColorEntry entry, Component defaultComponent, GameObject owner, out string stateName)
        {
            stateName = entry != null ? entry.name : string.Empty;
            if (entry == null)
                return false;

            Component component = entry.component != null ? entry.component : defaultComponent;
            switch (entry.sourceKind)
            {
                case PungentSceneGizmoStateSourceKind.AnimatorParameter:
                    return AnimatorParameterMatches(entry, owner, out stateName);
                case PungentSceneGizmoStateSourceKind.ActiveStateProvider:
                    return ActiveStateProviderMatches(entry, component, out stateName);
                case PungentSceneGizmoStateSourceKind.Bool:
                    return TryResolve(entry, component, PungentSceneGizmoSource.BindingValueKind.Bool, out object boolValue) &&
                           boolValue is bool b &&
                           b == ParseBool(entry.expectedValue, true);
                case PungentSceneGizmoStateSourceKind.Enum:
                case PungentSceneGizmoStateSourceKind.String:
                    if (!TryResolve(entry, component, entry.sourceKind == PungentSceneGizmoStateSourceKind.Enum ? PungentSceneGizmoSource.BindingValueKind.Enum : PungentSceneGizmoSource.BindingValueKind.String, out object value))
                        return false;
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    stateName = text;
                    return string.Equals(text, entry.expectedValue, StringComparison.OrdinalIgnoreCase);
                case PungentSceneGizmoStateSourceKind.NumericThreshold:
                    return TryResolve(entry, component, PungentSceneGizmoSource.BindingValueKind.Number, out object numberValue) &&
                           TryFloat(numberValue, out float numeric) &&
                           NumericPasses(numeric, entry.numericComparison, entry.numericThreshold, entry.numericThresholdMax);
                default:
                    return false;
            }
        }

        private static bool TryResolve(PungentSceneGizmoStateColorEntry entry, Component component, PungentSceneGizmoSource.BindingValueKind expectedKind, out object value)
        {
            value = null;
            return component != null &&
                   !string.IsNullOrWhiteSpace(entry.memberPath) &&
                   PungentSceneGizmoSource.TryResolveObjectValue(component, entry.memberPath, expectedKind, out value, out _);
        }

        private static bool AnimatorParameterMatches(PungentSceneGizmoStateColorEntry entry, GameObject owner, out string stateName)
        {
            stateName = entry.name;
            Animator animator = entry.animator != null ? entry.animator : (owner != null ? owner.GetComponent<Animator>() : null);
            if (animator == null || string.IsNullOrWhiteSpace(entry.animatorParameter))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (!string.Equals(parameter.name, entry.animatorParameter, StringComparison.Ordinal))
                    continue;

                stateName = parameter.name;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        return animator.GetBool(parameter.name) == ParseBool(entry.expectedValue, true);
                    case AnimatorControllerParameterType.Float:
                        return NumericPasses(animator.GetFloat(parameter.name), entry.numericComparison, entry.numericThreshold, entry.numericThresholdMax);
                    case AnimatorControllerParameterType.Int:
                        return NumericPasses(animator.GetInteger(parameter.name), entry.numericComparison, entry.numericThreshold, entry.numericThresholdMax);
                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool ActiveStateProviderMatches(PungentSceneGizmoStateColorEntry entry, Component component, out string stateName)
        {
            stateName = entry.name;
            if (component == null)
                return false;

            object value;
            string resolvedPath;
            bool resolved = !string.IsNullOrWhiteSpace(entry.activeStateProviderPath)
                ? PungentSceneGizmoSource.TryResolveObjectValue(component, entry.activeStateProviderPath, PungentSceneGizmoSource.BindingValueKind.Any, out value, out _)
                : PungentSceneGizmoSource.TryResolveFirstObjectValue(component, ActiveStateProviderCandidatePaths, PungentSceneGizmoSource.BindingValueKind.Any, out value, out resolvedPath);

            if (!resolved)
                return false;

            stateName = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.Equals(stateName, entry.expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static PungentSceneGizmoSource.BindingValueKind ExpectedKind(PungentSceneGizmoStateSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case PungentSceneGizmoStateSourceKind.Bool:
                    return PungentSceneGizmoSource.BindingValueKind.Bool;
                case PungentSceneGizmoStateSourceKind.Enum:
                    return PungentSceneGizmoSource.BindingValueKind.Enum;
                case PungentSceneGizmoStateSourceKind.String:
                    return PungentSceneGizmoSource.BindingValueKind.String;
                case PungentSceneGizmoStateSourceKind.NumericThreshold:
                    return PungentSceneGizmoSource.BindingValueKind.Number;
                default:
                    return PungentSceneGizmoSource.BindingValueKind.Any;
            }
        }

        private static bool NumericPasses(float value, PungentSceneGizmoNumericComparison comparison, float threshold, float thresholdMax)
        {
            switch (comparison)
            {
                case PungentSceneGizmoNumericComparison.GreaterThan:
                    return value > threshold;
                case PungentSceneGizmoNumericComparison.GreaterOrEqual:
                    return value >= threshold;
                case PungentSceneGizmoNumericComparison.LessThan:
                    return value < threshold;
                case PungentSceneGizmoNumericComparison.LessOrEqual:
                    return value <= threshold;
                case PungentSceneGizmoNumericComparison.Equal:
                    return Mathf.Approximately(value, threshold);
                case PungentSceneGizmoNumericComparison.NotEqual:
                    return !Mathf.Approximately(value, threshold);
                case PungentSceneGizmoNumericComparison.BetweenInclusive:
                    return value >= Mathf.Min(threshold, thresholdMax) && value <= Mathf.Max(threshold, thresholdMax);
                default:
                    return false;
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            return bool.TryParse(value, out bool parsed) ? parsed : fallback;
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

        public struct DiagnosticMessage
        {
            public bool IsError;
            public string Text;

            public DiagnosticMessage(bool isError, string text)
            {
                IsError = isError;
                Text = text;
            }
        }
    }
}
