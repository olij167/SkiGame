using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public static class PungentAuthoringProviderRegistry
    {
        private static readonly Dictionary<string, IPungentAuthoringProvider> ProvidersById = new Dictionary<string, IPungentAuthoringProvider>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<IPungentAuthoringProvider> ProvidersInOrder = new List<IPungentAuthoringProvider>();

        public static event Action Changed;

        public static void Register(IPungentAuthoringProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;

            string id = provider.ProviderId.Trim();
            if (ProvidersById.ContainsKey(id))
            {
                ProvidersById[id] = provider;
                for (int i = 0; i < ProvidersInOrder.Count; i++)
                {
                    if (ProvidersInOrder[i] != null && string.Equals(ProvidersInOrder[i].ProviderId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        ProvidersInOrder[i] = provider;
                        Changed?.Invoke();
                        return;
                    }
                }
            }
            else
            {
                ProvidersById.Add(id, provider);
            }

            ProvidersInOrder.RemoveAll(item => item == null || string.Equals(item.ProviderId, id, StringComparison.OrdinalIgnoreCase));
            ProvidersInOrder.Add(provider);
            Changed?.Invoke();
        }

        public static void Unregister(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return;

            string id = providerId.Trim();
            bool removed = ProvidersById.Remove(id);
            removed |= ProvidersInOrder.RemoveAll(item => item == null || string.Equals(item.ProviderId, id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                Changed?.Invoke();
        }

        public static void Clear()
        {
            ProvidersById.Clear();
            ProvidersInOrder.Clear();
            Changed?.Invoke();
        }

        public static IReadOnlyList<IPungentAuthoringProvider> GetProviders()
        {
            return ProvidersInOrder.Where(provider => provider != null).ToArray();
        }

        public static IPungentAuthoringProvider FindProvider(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return null;

            ProvidersById.TryGetValue(providerId.Trim(), out IPungentAuthoringProvider provider);
            return provider;
        }

        public static IReadOnlyList<IPungentAuthoringProvider> GetProvidersForKind(PungentAuthoringItemKind kind)
        {
            return ProvidersInOrder
                .Where(provider => provider != null && SupportsKind(provider, kind))
                .ToArray();
        }

        public static bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            foreach (IPungentAuthoringProvider provider in GetCandidateProviders(reference))
            {
                if (provider.TryGetMetadata(reference, out metadata) && metadata != null)
                    return true;
            }

            return false;
        }

        public static bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            IReadOnlyList<IPungentAuthoringProvider> providers = GetCandidateProviders(reference).ToArray();
            if (providers.Count == 0)
            {
                preview = PungentAuthoringPreview.Missing(reference != null ? reference.KindLabel : "Missing Authoring Provider", MissingProviderMessage(reference));
                return false;
            }

            for (int i = 0; i < providers.Count; i++)
            {
                if (providers[i] is IPungentAuthoringPreviewProvider previewProvider &&
                    previewProvider.TryGetPreview(reference, out preview) &&
                    preview != null)
                    return true;
            }

            if (TryGetMetadata(reference, out PungentAuthoringMetadata metadata))
            {
                int targetCount = 0;
                foreach (IPungentAuthoringProvider provider in providers)
                    targetCount += provider.GetTargets(reference)?.Count() ?? 0;
                preview = PungentAuthoringPreview.FromMetadata(metadata, targetCount);
                return true;
            }

            preview = PungentAuthoringPreview.Missing(reference != null ? reference.KindLabel : "Missing Authoring Item", "The requested authoring item could not be found.");
            return false;
        }

        public static bool TryOpen(PungentAuthoringReference reference)
        {
            foreach (IPungentAuthoringProvider provider in GetCandidateProviders(reference))
            {
                if (provider is IPungentAuthoringEditorLauncher launcher &&
                    launcher.CanOpen(reference, out _) &&
                    launcher.Open(reference))
                    return true;
            }

            return false;
        }

        public static bool TryEdit(PungentAuthoringReference reference)
        {
            foreach (IPungentAuthoringProvider provider in GetCandidateProviders(reference))
            {
                if (provider is IPungentAuthoringEditorLauncher launcher &&
                    launcher.CanEdit(reference, out _) &&
                    launcher.Edit(reference))
                    return true;
            }

            return false;
        }

        public static bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = string.Empty;
            error = string.Empty;

            foreach (IPungentAuthoringProvider provider in GetCandidateProviders(reference))
            {
                if (provider is IPungentAuthoringCopyProvider copyProvider &&
                    copyProvider.TryCopy(reference, out copiedValue, out error))
                    return true;
            }

            error = MissingProviderMessage(reference);
            return false;
        }

        public static bool TryResolveTarget(PungentAuthoringTarget target, out UnityEngine.Object unityObject, out string error)
        {
            unityObject = null;
            error = string.Empty;

            if (TryResolveUnityTargetDirect(target, out unityObject, out error))
                return true;

            IEnumerable<IPungentAuthoringProvider> providers = string.IsNullOrWhiteSpace(target?.providerId)
                ? GetProviders()
                : new[] { FindProvider(target.providerId) }.Where(provider => provider != null);

            foreach (IPungentAuthoringProvider provider in providers)
            {
                if (provider is IPungentAuthoringTargetResolver resolver &&
                    resolver.TryResolveTarget(target, out unityObject, out error))
                    return true;
            }

            error = "No authoring provider could resolve this target.";
            return false;
        }

        private static bool TryResolveUnityTargetDirect(PungentAuthoringTarget target, out UnityEngine.Object unityObject, out string error)
        {
            unityObject = null;
            error = string.Empty;
            if (target == null)
            {
                error = "Target is missing.";
                return false;
            }

            if (target.targetKind == PungentAuthoringTargetKind.AssetGuid)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(target.rawValue);
                unityObject = string.IsNullOrWhiteSpace(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                error = unityObject == null ? "Asset target could not be resolved." : string.Empty;
                return unityObject != null;
            }

            if (target.targetKind == PungentAuthoringTargetKind.SceneObjectGlobalId ||
                target.targetKind == PungentAuthoringTargetKind.ComponentInstanceId ||
                target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath)
            {
                string globalIdValue = target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath ? target.contextId : target.rawValue;
                if (!string.IsNullOrWhiteSpace(globalIdValue) &&
                    UnityEditor.GlobalObjectId.TryParse(globalIdValue, out UnityEditor.GlobalObjectId globalId))
                {
                    unityObject = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                    error = unityObject == null ? "Scene object target could not be resolved." : string.Empty;
                    return unityObject != null;
                }

                if (target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath && !string.IsNullOrWhiteSpace(target.contextId))
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(target.contextId);
                    unityObject = string.IsNullOrWhiteSpace(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    error = unityObject == null ? "Serialized property context could not be resolved." : string.Empty;
                    return unityObject != null;
                }
            }

            return false;
        }

        public static IEnumerable<PungentAuthoringAction> GetConversionActions(PungentAuthoringReference reference)
        {
            foreach (IPungentAuthoringProvider provider in GetProviders())
            {
                if (provider is IPungentAuthoringConversionProvider conversionProvider)
                {
                    foreach (PungentAuthoringAction action in conversionProvider.GetConversionActions(reference) ?? Enumerable.Empty<PungentAuthoringAction>())
                        yield return action;
                }
            }

            bool hasRichDocumentProvider = GetProvidersForKind(PungentAuthoringItemKind.RichDocument).Count > 0;
            bool hasBoardProvider = GetProvidersForKind(PungentAuthoringItemKind.Board).Count > 0;
            bool hasDataSheetProvider = GetProvidersForKind(PungentAuthoringItemKind.DataSheet).Count > 0;
            foreach (PungentAuthoringAction placeholder in PungentAuthoringConversionActions.GetMissingExtensionPlaceholders(reference))
            {
                if (placeholder == null)
                    continue;
                if (string.Equals(placeholder.extensionId, PungentAuthoringPackageCapabilities.RichDocuments, StringComparison.OrdinalIgnoreCase) && hasRichDocumentProvider)
                    continue;
                if (string.Equals(placeholder.extensionId, PungentAuthoringPackageCapabilities.BoardWhiteboard, StringComparison.OrdinalIgnoreCase) && hasBoardProvider)
                    continue;
                if (string.Equals(placeholder.extensionId, PungentAuthoringPackageCapabilities.DataSheet, StringComparison.OrdinalIgnoreCase) && hasDataSheetProvider)
                    continue;

                yield return placeholder;
            }
        }

        public static bool TryRunConversionAction(PungentAuthoringAction action, out PungentAuthoringReference result, out string error)
        {
            result = null;
            error = string.Empty;
            if (action == null)
            {
                error = "Conversion action is missing.";
                return false;
            }

            if (!action.enabled)
            {
                error = string.IsNullOrWhiteSpace(action.disabledReason) ? "Conversion action is disabled." : action.disabledReason;
                return false;
            }

            IEnumerable<IPungentAuthoringProvider> providers = !string.IsNullOrWhiteSpace(action.providerId)
                ? new[] { FindProvider(action.providerId) }.Where(provider => provider != null)
                : GetProviders();

            foreach (IPungentAuthoringProvider provider in providers)
            {
                if (provider is IPungentAuthoringConversionProvider conversionProvider &&
                    conversionProvider.TryRunConversionAction(action, out result, out error))
                    return true;
            }

            error = string.IsNullOrWhiteSpace(error) ? "No authoring provider could run this conversion action." : error;
            return false;
        }

        public static string MissingProviderMessage(PungentAuthoringReference reference)
        {
            if (reference == null)
                return "Authoring provider is not installed.";

            if (reference.itemKind != PungentAuthoringItemKind.Unknown)
                return PungentAuthoringItemKinds.GetMissingProviderMessage(reference.itemKind, reference.customKind);

            return !string.IsNullOrWhiteSpace(reference.providerId)
                ? "Authoring provider '" + reference.providerId.Trim() + "' is not installed."
                : "Authoring provider is not installed.";
        }

        private static IEnumerable<IPungentAuthoringProvider> GetCandidateProviders(PungentAuthoringReference reference)
        {
            if (reference == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(reference.providerId))
            {
                IPungentAuthoringProvider provider = FindProvider(reference.providerId);
                if (provider != null)
                    yield return provider;
                yield break;
            }

            foreach (IPungentAuthoringProvider provider in GetProvidersForKind(reference.itemKind))
                yield return provider;
        }

        private static bool SupportsKind(IPungentAuthoringProvider provider, PungentAuthoringItemKind kind)
        {
            if (provider == null || provider.SupportedKinds == null)
                return false;

            for (int i = 0; i < provider.SupportedKinds.Count; i++)
            {
                if (provider.SupportedKinds[i] == kind)
                    return true;
            }

            return false;
        }
    }
#endif
}
