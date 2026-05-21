using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public sealed class PungentDataSheetUnityObjectReferenceProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider
    {
        public const string Id = "data-sheet-unity-object";
        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.ExternalReference };

        public string ProviderId => Id;
        public string DisplayName => "Unity Object Reference";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Edit |
            PungentAuthoringProviderCapabilities.Copy;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.DataSheet;
        public string ExtensionId => "com.pungentfunk.utilities.datasheet";

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            yield break;
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            if (!TryResolve(reference, out UnityEngine.Object target))
                return false;

            metadata = PungentAuthoringMetadata.Create(reference.itemId, target.name, PungentAuthoringItemKind.ExternalReference, Id, PackageCapabilityId);
            metadata.customKind = target.GetType().Name;
            metadata.summary = string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(target))
                ? "Scene object or component reference."
                : AssetDatabase.GetAssetPath(target);
            metadata.tags = PungentAuthoringMetadata.NormalizeTags(new[] { "unity-object", target.GetType().Name });
            metadata.NormalizeInPlace();
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            yield break;
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            if (!TryResolve(reference, out UnityEngine.Object target))
                yield break;

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrWhiteSpace(guid))
                    yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.AssetGuid, guid, target.name, Id);
            }
            else
            {
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.SceneObjectGlobalId, reference.itemId, target.name, Id);
            }
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            if (!TryGetMetadata(reference, out PungentAuthoringMetadata metadata))
                return false;

            preview = PungentAuthoringPreview.FromMetadata(metadata, 1);
            preview.primaryActionLabels.Add("Ping");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = TryResolve(reference, out _) ? string.Empty : "Unity object reference could not be resolved.";
            return string.IsNullOrWhiteSpace(reason);
        }

        public bool Open(PungentAuthoringReference reference)
        {
            if (!TryResolve(reference, out UnityEngine.Object target))
                return false;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            return true;
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            return CanOpen(reference, out reason);
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return Open(reference);
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Unity object links are created from the active Unity selection.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Unity object links are created from the active Unity selection.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = reference != null ? reference.itemId ?? string.Empty : string.Empty;
            error = string.IsNullOrWhiteSpace(copiedValue) ? "No Unity object reference ID to copy." : string.Empty;
            return string.IsNullOrWhiteSpace(error);
        }

        private static bool TryResolve(PungentAuthoringReference reference, out UnityEngine.Object target)
        {
            target = null;
            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
                return false;

            if (!string.IsNullOrWhiteSpace(reference.providerId) && !string.Equals(reference.providerId, Id, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (GlobalObjectId.TryParse(reference.itemId, out GlobalObjectId globalId))
                target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);

            return target != null;
        }
    }
#endif
}
