using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    [Flags]
    public enum PungentAuthoringProviderCapabilities
    {
        None = 0,
        EnumerateItems = 1 << 0,
        Metadata = 1 << 1,
        References = 1 << 2,
        Targets = 1 << 3,
        Preview = 1 << 4,
        Open = 1 << 5,
        Edit = 1 << 6,
        Copy = 1 << 7,
        CreateFromContext = 1 << 8,
        ResolveTarget = 1 << 9,
        Validate = 1 << 10,
        ConversionHooks = 1 << 11
    }

    public interface IPungentAuthoringProvider
    {
        string ProviderId { get; }
        string DisplayName { get; }
        IReadOnlyList<PungentAuthoringItemKind> SupportedKinds { get; }
        PungentAuthoringProviderCapabilities Capabilities { get; }
        string PackageCapabilityId { get; }
        string ExtensionId { get; }

        IEnumerable<PungentAuthoringMetadata> EnumerateItems();
        bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata);
        IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference);
        IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference);
    }

    public interface IPungentAuthoringPreviewProvider
    {
        bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview);
    }

    public interface IPungentAuthoringEditorLauncher
    {
        bool CanOpen(PungentAuthoringReference reference, out string reason);
        bool Open(PungentAuthoringReference reference);
        bool CanEdit(PungentAuthoringReference reference, out string reason);
        bool Edit(PungentAuthoringReference reference);
        bool CanCreateFromContext(PungentAuthoringTarget context, out string reason);
        bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error);
    }

    public interface IPungentAuthoringCopyProvider
    {
        bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error);
    }

    public interface IPungentAuthoringTargetResolver
    {
        bool TryResolveTarget(PungentAuthoringTarget target, out UnityEngine.Object unityObject, out string error);
    }

    public interface IPungentAuthoringValidator
    {
        PungentAuthoringValidationResult ValidateReference(PungentAuthoringReference reference);
        PungentAuthoringValidationResult ValidateTarget(PungentAuthoringTarget target);
    }

    public interface IPungentAuthoringConversionProvider
    {
        IEnumerable<PungentAuthoringAction> GetConversionActions(PungentAuthoringReference reference);
        bool TryRunConversionAction(PungentAuthoringAction action, out PungentAuthoringReference result, out string error);
    }
#endif
}
