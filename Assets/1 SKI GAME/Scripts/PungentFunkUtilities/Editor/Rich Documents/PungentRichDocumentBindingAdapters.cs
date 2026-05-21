using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public enum PungentRichDocumentBindingEndpointKind
    {
        Unknown = 0,
        SerializedStringProperty = 10,
        UnityText = 20,
        TMProText = 30,
        Custom = 1000
    }

    public sealed class PungentRichDocumentBindingEndpoint
    {
        public string id = string.Empty;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string label = string.Empty;
        public string description = string.Empty;
        public string tooltip = string.Empty;
        public string disabledReason = string.Empty;
        public PungentRichDocumentBindingEndpointKind endpointKind = PungentRichDocumentBindingEndpointKind.Unknown;
        public PungentRichDocumentSemanticKind kind = PungentRichDocumentSemanticKind.None;
        public PungentAuthoringTarget target;
        public bool canRead = true;
        public bool canApply = true;

        public bool HasTarget => target != null && target.HasTarget;
        public bool CanBind => HasTarget && string.IsNullOrWhiteSpace(disabledReason);

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(label) ? id ?? string.Empty : label;
        }
    }

    public sealed class PungentRichDocumentBindingPreview
    {
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string currentValue = string.Empty;
        public string disabledReason = string.Empty;
        public string warning = string.Empty;
        public bool canRead;
        public bool canApply;
    }

    public sealed class PungentRichDocumentBindingApplyResult
    {
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string targetLabel = string.Empty;
        public string message = string.Empty;
        public bool applied;
    }

    public interface IPungentRichDocumentBindingAdapter
    {
        string Id { get; }
        string DisplayName { get; }
        IEnumerable<PungentRichDocumentBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject, PungentRichDocumentSemanticKind semanticKind);
    }

    public interface IPungentRichDocumentBindingPreviewAdapter : IPungentRichDocumentBindingAdapter
    {
        bool TryPreview(PungentAuthoringTarget target, PungentRichDocumentSemanticBinding binding, out PungentRichDocumentBindingPreview preview);
    }

    public interface IPungentRichDocumentBindingApplyAdapter : IPungentRichDocumentBindingPreviewAdapter
    {
        bool CanApply(PungentAuthoringTarget target, PungentRichDocumentSemanticBinding binding, out string disabledReason);
        PungentRichDocumentBindingApplyResult Apply(PungentAuthoringTarget target, PungentRichDocumentSemanticBinding binding, string value);
    }

    public static class PungentRichDocumentBindingAdapterRegistry
    {
        private static readonly List<IPungentRichDocumentBindingAdapter> Adapters = new List<IPungentRichDocumentBindingAdapter>();

        public static void Register(IPungentRichDocumentBindingAdapter adapter)
        {
            if (adapter == null || string.IsNullOrWhiteSpace(adapter.Id))
                return;

            string id = adapter.Id.Trim();
            if (Adapters.Any(existing => existing != null && string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)))
                return;

            Adapters.Add(adapter);
        }

        public static IReadOnlyList<IPungentRichDocumentBindingAdapter> RegisteredAdapters =>
            Adapters.Where(adapter => adapter != null).ToList();

        public static IReadOnlyList<IPungentRichDocumentBindingPreviewAdapter> PreviewAdapters =>
            Adapters.OfType<IPungentRichDocumentBindingPreviewAdapter>().ToList();

        public static IReadOnlyList<IPungentRichDocumentBindingApplyAdapter> ApplyAdapters =>
            Adapters.OfType<IPungentRichDocumentBindingApplyAdapter>().ToList();

        public static List<PungentRichDocumentBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject, PungentRichDocumentSemanticKind semanticKind)
        {
            if (targetObject == null)
                return new List<PungentRichDocumentBindingEndpoint>();

            return Adapters
                .Where(adapter => adapter != null)
                .SelectMany(adapter => SafeGetEndpoints(adapter, targetObject, semanticKind))
                .Where(endpoint => endpoint != null && endpoint.HasTarget)
                .GroupBy(endpoint => endpoint.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(endpoint => endpoint.adapterDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(endpoint => endpoint.label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<PungentRichDocumentBindingEndpoint> SafeGetEndpoints(
            IPungentRichDocumentBindingAdapter adapter,
            UnityEngine.Object targetObject,
            PungentRichDocumentSemanticKind semanticKind)
        {
            try
            {
                return (adapter.GetEndpoints(targetObject, semanticKind) ?? Enumerable.Empty<PungentRichDocumentBindingEndpoint>()).ToList();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Rich Document binding adapter '" + adapter.Id + "' failed to provide endpoints: " + exception.Message);
                return Enumerable.Empty<PungentRichDocumentBindingEndpoint>();
            }
        }
    }

    [InitializeOnLoad]
    internal static class PungentRichDocumentBuiltInBindingAdapterBootstrap
    {
        static PungentRichDocumentBuiltInBindingAdapterBootstrap()
        {
            PungentRichDocumentBindingAdapterRegistry.Register(new PungentRichDocumentSerializedStringBindingAdapter());
        }
    }

    public sealed class PungentRichDocumentSerializedStringBindingAdapter : IPungentRichDocumentBindingApplyAdapter
    {
        public const string AdapterId = "rich-document-serialized-string-property";

        public string Id => AdapterId;
        public string DisplayName => "Serialized String Field";

        public IEnumerable<PungentRichDocumentBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject, PungentRichDocumentSemanticKind semanticKind)
        {
            foreach (PungentAuthoringBindingEndpoint endpoint in PungentAuthoringBindingAdapterRegistry.GetEndpoints(targetObject))
            {
                if (endpoint == null || endpoint.valueKind != PungentAuthoringBindingValueKind.Text || endpoint.target == null)
                    continue;

                yield return new PungentRichDocumentBindingEndpoint
                {
                    id = AdapterId + ":" + endpoint.target.contextId + ":" + endpoint.target.propertyPath,
                    adapterId = Id,
                    adapterDisplayName = DisplayName,
                    label = endpoint.label,
                    description = "Explicitly apply this semantic text to a serialized string field on the selected object or component.",
                    tooltip = endpoint.tooltip,
                    disabledReason = endpoint.disabledReason,
                    endpointKind = ClassifyEndpoint(endpoint.target.label, endpoint.target.propertyPath),
                    kind = semanticKind,
                    target = endpoint.target,
                    canRead = endpoint.canRead,
                    canApply = endpoint.canApply
                };
            }
        }

        public bool TryPreview(PungentAuthoringTarget target, PungentRichDocumentSemanticBinding binding, out PungentRichDocumentBindingPreview preview)
        {
            preview = new PungentRichDocumentBindingPreview
            {
                adapterId = Id,
                adapterDisplayName = DisplayName
            };

            if (!OwnsTarget(target))
            {
                preview.disabledReason = "Target is not a serialized string-property binding.";
                return false;
            }

            PungentAuthoringBindingPreview sharedPreview = PungentAuthoringBindingApplicationService.Preview(target);
            if (!sharedPreview.canRead)
            {
                preview.disabledReason = sharedPreview.disabledReason;
                return false;
            }

            if (sharedPreview.valueKind != PungentAuthoringBindingValueKind.Text)
            {
                preview.disabledReason = "Rich Document semantic text can only bind to serialized string fields.";
                return false;
            }

            preview.canRead = true;
            preview.canApply = sharedPreview.canApply;
            preview.currentValue = sharedPreview.currentValue ?? string.Empty;
            preview.warning = sharedPreview.warning;
            preview.disabledReason = sharedPreview.disabledReason;
            return true;
        }

        public bool CanApply(PungentAuthoringTarget target, PungentRichDocumentSemanticBinding binding, out string disabledReason)
        {
            if (!OwnsTarget(target))
            {
                disabledReason = "Target is not a serialized string-property binding.";
                return false;
            }

            PungentAuthoringBindingPreview preview = PungentAuthoringBindingApplicationService.Preview(target);
            if (preview.valueKind != PungentAuthoringBindingValueKind.Text)
            {
                disabledReason = "Rich Document semantic text can only bind to serialized string fields.";
                return false;
            }

            return PungentAuthoringBindingApplicationService.CanApply(target, out disabledReason);
        }

        public PungentRichDocumentBindingApplyResult Apply(PungentAuthoringTarget target, PungentRichDocumentSemanticBinding binding, string value)
        {
            PungentRichDocumentBindingApplyResult result = new PungentRichDocumentBindingApplyResult
            {
                adapterId = Id,
                adapterDisplayName = DisplayName,
                targetLabel = target == null ? string.Empty : target.label ?? string.Empty
            };

            if (!OwnsTarget(target))
            {
                result.message = "Target is not a serialized string-property binding.";
                return result;
            }

            PungentAuthoringBindingPreview preview = PungentAuthoringBindingApplicationService.Preview(target);
            if (preview.valueKind != PungentAuthoringBindingValueKind.Text)
            {
                result.message = "Rich Document semantic text can only bind to serialized string fields.";
                return result;
            }

            PungentAuthoringBindingApplyResult sharedResult = PungentAuthoringBindingApplicationService.Apply(target, value, "Apply Rich Document Text");
            if (sharedResult != null)
            {
                result.adapterId = string.IsNullOrWhiteSpace(sharedResult.adapterId) ? Id : sharedResult.adapterId;
                result.adapterDisplayName = string.IsNullOrWhiteSpace(sharedResult.adapterDisplayName) ? DisplayName : sharedResult.adapterDisplayName;
                result.targetLabel = sharedResult.targetLabel;
                result.message = sharedResult.message;
                result.applied = sharedResult.applied;
            }
            return result;
        }

        private static PungentRichDocumentBindingEndpointKind ClassifyEndpoint(string label, string propertyPath)
        {
            string typeName = label ?? string.Empty;
            string path = propertyPath ?? string.Empty;
            if (typeName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(path, "m_Text", StringComparison.Ordinal))
                return PungentRichDocumentBindingEndpointKind.UnityText;
            if (typeName.StartsWith("TMPro.", StringComparison.Ordinal) || string.Equals(path, "m_text", StringComparison.Ordinal))
                return PungentRichDocumentBindingEndpointKind.TMProText;
            return PungentRichDocumentBindingEndpointKind.SerializedStringProperty;
        }

        private static bool OwnsTarget(PungentAuthoringTarget target)
        {
            return target != null && target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath;
        }
    }

    public static class PungentRichDocumentBindingApplicationService
    {
        public static PungentRichDocumentBindingPreview Preview(PungentRichDocumentSemanticBinding binding)
        {
            PungentRichDocumentBindingPreview fallback = new PungentRichDocumentBindingPreview();
            if (binding == null || binding.target == null || !binding.target.HasTarget)
            {
                fallback.disabledReason = "No project target is bound.";
                return fallback;
            }

            foreach (IPungentRichDocumentBindingPreviewAdapter adapter in PungentRichDocumentBindingAdapterRegistry.PreviewAdapters)
            {
                if (adapter == null)
                    continue;

                try
                {
                    if (adapter.TryPreview(binding.target, binding, out PungentRichDocumentBindingPreview preview) && preview != null)
                    {
                        if (string.IsNullOrWhiteSpace(preview.adapterId))
                            preview.adapterId = adapter.Id;
                        if (string.IsNullOrWhiteSpace(preview.adapterDisplayName))
                            preview.adapterDisplayName = adapter.DisplayName;
                        return preview;
                    }
                }
                catch (Exception exception)
                {
                    fallback.disabledReason = "Binding preview adapter '" + adapter.Id + "' failed: " + exception.Message;
                    return fallback;
                }
            }

            PungentAuthoringBindingPreview sharedPreview = PungentAuthoringBindingApplicationService.Preview(binding.target);
            if (sharedPreview != null &&
                sharedPreview.valueKind != PungentAuthoringBindingValueKind.Unknown &&
                sharedPreview.valueKind != PungentAuthoringBindingValueKind.Text)
            {
                fallback.adapterId = sharedPreview.adapterId;
                fallback.adapterDisplayName = sharedPreview.adapterDisplayName;
                fallback.currentValue = sharedPreview.currentValue;
                fallback.warning = sharedPreview.warning;
                fallback.disabledReason = "Rich Document semantic text can only bind to serialized string fields.";
                fallback.canRead = sharedPreview.canRead;
                fallback.canApply = false;
                return fallback;
            }

            if (sharedPreview != null && (sharedPreview.canRead || !string.IsNullOrWhiteSpace(sharedPreview.disabledReason)))
            {
                fallback.adapterId = sharedPreview.adapterId;
                fallback.adapterDisplayName = sharedPreview.adapterDisplayName;
                fallback.currentValue = sharedPreview.currentValue;
                fallback.warning = sharedPreview.warning;
                fallback.disabledReason = sharedPreview.disabledReason;
                fallback.canRead = sharedPreview.canRead;
                fallback.canApply = sharedPreview.canApply;
                return fallback;
            }

            fallback.disabledReason = "No installed binding adapter can preview this target.";
            return fallback;
        }

        public static bool CanApply(PungentRichDocumentSemanticBinding binding, out string disabledReason)
        {
            disabledReason = string.Empty;
            if (binding == null || binding.target == null || !binding.target.HasTarget)
            {
                disabledReason = "Bind this integrated text to a target in the Properties tray before applying.";
                return false;
            }

            foreach (IPungentRichDocumentBindingApplyAdapter adapter in PungentRichDocumentBindingAdapterRegistry.ApplyAdapters)
            {
                if (adapter == null)
                    continue;

                try
                {
                    if (adapter.CanApply(binding.target, binding, out disabledReason))
                        return true;
                }
                catch (Exception exception)
                {
                    disabledReason = "Binding apply adapter '" + adapter.Id + "' failed: " + exception.Message;
                    return false;
                }
            }

            PungentAuthoringBindingPreview sharedPreview = PungentAuthoringBindingApplicationService.Preview(binding.target);
            if (sharedPreview != null &&
                sharedPreview.valueKind != PungentAuthoringBindingValueKind.Unknown &&
                sharedPreview.valueKind != PungentAuthoringBindingValueKind.Text)
            {
                disabledReason = "Rich Document semantic text can only bind to serialized string fields.";
                return false;
            }

            if (PungentAuthoringBindingApplicationService.CanApply(binding.target, out disabledReason))
                return true;

            if (string.IsNullOrWhiteSpace(disabledReason))
                disabledReason = "No installed binding adapter can apply this target.";
            return false;
        }

        public static PungentRichDocumentBindingApplyResult Apply(PungentRichDocumentSemanticBinding binding, string value)
        {
            PungentRichDocumentBindingApplyResult fallback = new PungentRichDocumentBindingApplyResult
            {
                targetLabel = binding == null || binding.target == null ? string.Empty : binding.target.label ?? string.Empty
            };

            if (binding == null || binding.target == null || !binding.target.HasTarget)
            {
                fallback.message = "No project target is bound.";
                return fallback;
            }

            foreach (IPungentRichDocumentBindingApplyAdapter adapter in PungentRichDocumentBindingAdapterRegistry.ApplyAdapters)
            {
                if (adapter == null)
                    continue;

                try
                {
                    string disabledReason;
                    if (!adapter.CanApply(binding.target, binding, out disabledReason))
                        continue;

                    PungentRichDocumentBindingApplyResult result = adapter.Apply(binding.target, binding, value);
                    if (result != null)
                        return result;
                }
                catch (Exception exception)
                {
                    fallback.adapterId = adapter.Id;
                    fallback.adapterDisplayName = adapter.DisplayName;
                    fallback.message = "Binding apply adapter failed: " + exception.Message;
                    return fallback;
                }
            }

            PungentAuthoringBindingPreview sharedPreview = PungentAuthoringBindingApplicationService.Preview(binding.target);
            if (sharedPreview != null &&
                sharedPreview.valueKind != PungentAuthoringBindingValueKind.Unknown &&
                sharedPreview.valueKind != PungentAuthoringBindingValueKind.Text)
            {
                fallback.adapterId = sharedPreview.adapterId;
                fallback.adapterDisplayName = sharedPreview.adapterDisplayName;
                fallback.message = "Rich Document semantic text can only bind to serialized string fields.";
                return fallback;
            }

            PungentAuthoringBindingApplyResult sharedResult = PungentAuthoringBindingApplicationService.Apply(binding.target, value, "Apply Rich Document Text");
            if (sharedResult != null)
            {
                fallback.adapterId = sharedResult.adapterId;
                fallback.adapterDisplayName = sharedResult.adapterDisplayName;
                fallback.targetLabel = sharedResult.targetLabel;
                fallback.message = sharedResult.message;
                fallback.applied = sharedResult.applied;
                return fallback;
            }

            fallback.message = "No installed binding adapter can apply this target.";
            return fallback;
        }

        public static bool TryResolveUnityTarget(PungentAuthoringTarget target, out UnityEngine.Object targetObject, out string error)
        {
            return PungentAuthoringBindingApplicationService.TryResolveUnityTarget(target, out targetObject, out error);
        }

        public static bool TryResolveStringProperty(
            PungentAuthoringTarget target,
            out UnityEngine.Object targetObject,
            out SerializedObject serializedObject,
            out SerializedProperty property,
            out string error)
        {
            targetObject = null;
            serializedObject = null;
            property = null;
            error = string.Empty;

            if (!PungentAuthoringBindingApplicationService.TryResolveSerializedProperty(target, out targetObject, out serializedObject, out property, out error))
                return false;

            if (property.propertyType != SerializedPropertyType.String)
            {
                error = "Bound property is no longer a string field: " + target.propertyPath;
                return false;
            }

            return true;
        }
    }
#endif
}
