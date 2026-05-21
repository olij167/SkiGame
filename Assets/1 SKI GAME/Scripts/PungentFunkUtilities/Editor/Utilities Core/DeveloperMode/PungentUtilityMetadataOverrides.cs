namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Project-local metadata overrides for PungentUtilityRegistry descriptors.
    /// Registry entries remain the package/factory defaults; this store holds reversible developer-mode card customisations.
    /// </summary>
    [FilePath("ProjectSettings/PungentFunkUtilities/UtilityMetadataOverrides.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityMetadataOverrides : ScriptableSingleton<PungentUtilityMetadataOverrides>
    {
        [Serializable]
        public sealed class UtilityMetadataOverride
        {
            public string utilityId;

            public bool overrideDisplayName;
            public string displayName;

            public bool overrideUtilityType;
            public string utilityType;

            public bool overrideAreaCategory;
            public string areaCategory;

            public bool overrideModule;
            public string module;

            public bool overrideDescription;
            [TextArea(2, 6)] public string description;

            public bool overrideMenuPath;
            public string menuPath;

            public bool overrideTags;
            public string[] tags = new string[0];

            public bool overrideSortOrder;
            public int sortOrder;

            public bool overrideSupportsSceneOverlay;
            public bool supportsSceneOverlay;

            public bool overrideSupportsContextMenu;
            public bool supportsContextMenu;

            public bool overrideSupportsSelection;
            public bool supportsSelection;

            public bool overrideIsLabHub;
            public bool isLabHub;

            public bool overridePackageStatus;
            public string packageStatus;

            public bool overridePackageId;
            public string packageId;

            public bool overridePackageDisplayName;
            public string packageDisplayName;

            public bool overridePackageTier;
            public PungentUtilityPackageTier packageTier = PungentUtilityPackageTier.Core;

            public bool overrideAssetStoreUrl;
            public string assetStoreUrl;

            public bool overridePackageManagerId;
            public string packageManagerId;

            public bool overrideImportPackagePath;
            public string importPackagePath;

            public bool overrideInstallHint;
            public string installHint;

            public bool overrideMissingDependencyMessage;
            public string missingDependencyMessage;

            public bool overrideDocumentationTopicId;
            public string documentationTopicId;

            public bool overrideProvidedCapabilities;
            public string[] providedCapabilities = new string[0];

            public bool overrideRequiredPackageIds;
            public string[] requiredPackageIds = new string[0];

            public bool overrideOptionalPackageIds;
            public string[] optionalPackageIds = new string[0];

            public bool overrideRelatedUtilityIds;
            public string[] relatedUtilityIds = new string[0];

            public bool overrideParentUtilityId;
            public string parentUtilityId;

            public bool overrideBrowserPriority;
            public int browserPriority;

            public bool overrideBrowserRole;
            public PungentUtilityBrowserRole browserRole = PungentUtilityBrowserRole.CoreUtility;

            public bool overrideBrowserProminence;
            public PungentUtilityBrowserProminence browserProminence = PungentUtilityBrowserProminence.Standard;

            public bool overrideBrowserTags;
            public string[] browserTags = new string[0];

            public bool overrideBrowserCardTint;
            public Color browserCardTint = Color.clear;

            public bool overrideAccessoryUtilityIds;
            public string[] accessoryUtilityIds = new string[0];

            public bool overrideActionIds;
            public string[] actionIds = new string[0];

            public bool overrideCategoryFacets;
            public string[] categoryFacets = new string[0];

            public bool overrideShowInUtilitiesBrowser;
            public bool showInUtilitiesBrowser = true;

            public bool overrideItemKind;
            public PungentUtilityItemKind itemKind = PungentUtilityItemKind.Utility;

            public bool overrideVisibility;
            public PungentUtilityVisibility visibility = PungentUtilityVisibility.Visible;
        }

        [SerializeField] private List<UtilityMetadataOverride> _overrides = new List<UtilityMetadataOverride>();

        public IReadOnlyList<UtilityMetadataOverride> Overrides
        {
            get
            {
                NormalizeInMemory();
                return _overrides;
            }
        }

        public bool HasOverride(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return false;

            NormalizeInMemory();
            return _overrides.Any(item => string.Equals(item.utilityId, utilityId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public UtilityMetadataOverride GetOverrideCopy(string utilityId)
        {
            UtilityMetadataOverride item = FindInternal(utilityId);
            return item == null ? null : Clone(item);
        }

        public UtilityMetadataOverride CreateEditableCopy(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return null;

            UtilityMetadataOverride existing = FindInternal(descriptor.Id);
            UtilityMetadataOverride copy = existing != null ? Clone(existing) : new UtilityMetadataOverride { utilityId = descriptor.Id };

            copy.utilityId = descriptor.Id;
            FillUnsetValuesFromDescriptor(copy, descriptor);
            return copy;
        }

        public void ApplyTo(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                return;

            UtilityMetadataOverride metadata = FindInternal(descriptor.Id);
            if (metadata == null)
                return;

            if (metadata.overrideDisplayName && !string.IsNullOrWhiteSpace(metadata.displayName))
                descriptor.DisplayName = metadata.displayName.Trim();

            if (metadata.overrideUtilityType)
                descriptor.UtilityType = string.IsNullOrWhiteSpace(metadata.utilityType) ? "Other" : metadata.utilityType.Trim();

            if (metadata.overrideAreaCategory)
                descriptor.AreaCategory = PungentUtilityCategories.Normalize(metadata.areaCategory);

            if (metadata.overrideModule)
                descriptor.Module = string.IsNullOrWhiteSpace(metadata.module) ? descriptor.UtilityType : metadata.module.Trim();

            if (metadata.overrideDescription)
                descriptor.Description = metadata.description ?? string.Empty;

            if (metadata.overrideMenuPath)
                descriptor.MenuPath = metadata.menuPath ?? string.Empty;

            if (metadata.overrideTags)
                descriptor.Tags = CleanArray(metadata.tags);

            if (metadata.overrideSortOrder)
                descriptor.SortOrder = metadata.sortOrder;

            if (metadata.overrideSupportsSceneOverlay)
                descriptor.SupportsSceneOverlay = metadata.supportsSceneOverlay;

            if (metadata.overrideSupportsContextMenu)
                descriptor.SupportsContextMenu = metadata.supportsContextMenu;

            if (metadata.overrideSupportsSelection)
                descriptor.SupportsSelection = metadata.supportsSelection;

            if (metadata.overrideIsLabHub)
                descriptor.IsLabHub = metadata.isLabHub;

            if (metadata.overridePackageStatus)
                descriptor.PackageStatus = PungentUtilityPackageStatus.Normalize(metadata.packageStatus);

            if (metadata.overridePackageId)
                descriptor.PackageId = PungentUtilityPackageCatalog.NormalizePackageId(metadata.packageId);

            if (metadata.overridePackageDisplayName)
                descriptor.PackageDisplayName = metadata.packageDisplayName ?? string.Empty;

            if (metadata.overridePackageTier)
                descriptor.PackageTier = metadata.packageTier;

            if (metadata.overrideAssetStoreUrl)
                descriptor.AssetStoreUrl = metadata.assetStoreUrl ?? string.Empty;

            if (metadata.overridePackageManagerId)
                descriptor.PackageManagerId = metadata.packageManagerId ?? string.Empty;

            if (metadata.overrideImportPackagePath)
                descriptor.ImportPackagePath = metadata.importPackagePath ?? string.Empty;

            if (metadata.overrideInstallHint)
                descriptor.InstallHint = metadata.installHint ?? string.Empty;

            if (metadata.overrideMissingDependencyMessage)
                descriptor.MissingDependencyMessage = metadata.missingDependencyMessage ?? string.Empty;

            if (metadata.overrideDocumentationTopicId)
                descriptor.DocumentationTopicId = metadata.documentationTopicId ?? string.Empty;

            if (metadata.overrideProvidedCapabilities)
                descriptor.ProvidedCapabilities = CleanArray(metadata.providedCapabilities);

            if (metadata.overrideRequiredPackageIds)
                descriptor.RequiredPackageIds = CleanPackageIds(metadata.requiredPackageIds);

            if (metadata.overrideOptionalPackageIds)
                descriptor.OptionalPackageIds = CleanPackageIds(metadata.optionalPackageIds);

            if (metadata.overrideRelatedUtilityIds)
                descriptor.RelatedUtilityIds = CleanArray(metadata.relatedUtilityIds);

            if (metadata.overrideParentUtilityId)
                descriptor.ParentUtilityId = string.IsNullOrWhiteSpace(metadata.parentUtilityId) ? string.Empty : metadata.parentUtilityId.Trim();

            if (metadata.overrideBrowserPriority)
                descriptor.BrowserPriority = metadata.browserPriority;

            if (metadata.overrideBrowserRole)
                descriptor.BrowserRole = metadata.browserRole;

            if (metadata.overrideBrowserProminence)
                descriptor.BrowserProminence = metadata.browserProminence;

            if (metadata.overrideBrowserTags)
                descriptor.BrowserTags = CleanArray(metadata.browserTags);

            if (metadata.overrideBrowserCardTint)
            {
                descriptor.OverrideBrowserCardTint = true;
                descriptor.BrowserCardTint = metadata.browserCardTint;
            }

            if (metadata.overrideAccessoryUtilityIds)
                descriptor.AccessoryUtilityIds = CleanArray(metadata.accessoryUtilityIds);

            if (metadata.overrideActionIds)
                descriptor.ActionIds = CleanArray(metadata.actionIds);

            if (metadata.overrideCategoryFacets)
                descriptor.CategoryFacets = PungentUtilityCategories.NormalizeMany(CleanArray(metadata.categoryFacets));

            if (metadata.overrideShowInUtilitiesBrowser)
                descriptor.ShowInUtilitiesBrowser = metadata.showInUtilitiesBrowser;

            if (metadata.overrideItemKind)
                descriptor.ItemKind = metadata.itemKind;

            if (metadata.overrideVisibility)
                descriptor.Visibility = metadata.visibility;
        }

        public void SetOverride(UtilityMetadataOverride metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.utilityId))
                return;

            NormalizeOverride(metadata);

            if (!HasAnyOverrideEnabled(metadata))
            {
                RemoveOverride(metadata.utilityId);
                return;
            }

            int index = _overrides.FindIndex(item => string.Equals(item.utilityId, metadata.utilityId, StringComparison.OrdinalIgnoreCase));
            UtilityMetadataOverride copy = Clone(metadata);
            if (index >= 0)
                _overrides[index] = copy;
            else
                _overrides.Add(copy);

            SaveStore();
        }

        public void RemoveOverride(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            NormalizeInMemory();
            _overrides.RemoveAll(item => string.Equals(item.utilityId, utilityId.Trim(), StringComparison.OrdinalIgnoreCase));
            SaveStore();
        }

        public void SaveStore()
        {
            NormalizeInMemory();
            _overrides.Sort((a, b) => string.Compare(a.utilityId, b.utilityId, StringComparison.OrdinalIgnoreCase));
            Save(true);
        }

        private UtilityMetadataOverride FindInternal(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return null;

            NormalizeInMemory();
            string id = utilityId.Trim();
            return _overrides.FirstOrDefault(item => string.Equals(item.utilityId, id, StringComparison.OrdinalIgnoreCase));
        }

        private void NormalizeInMemory()
        {
            if (_overrides == null)
                _overrides = new List<UtilityMetadataOverride>();

            _overrides.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.utilityId));

            for (int i = 0; i < _overrides.Count; i++)
                NormalizeOverride(_overrides[i]);

            for (int i = _overrides.Count - 1; i >= 0; i--)
            {
                string id = _overrides[i].utilityId;
                int first = _overrides.FindIndex(item => string.Equals(item.utilityId, id, StringComparison.OrdinalIgnoreCase));
                if (first >= 0 && first != i)
                    _overrides.RemoveAt(i);
            }
        }

        private static void NormalizeOverride(UtilityMetadataOverride metadata)
        {
            if (metadata == null)
                return;

            metadata.utilityId = (metadata.utilityId ?? string.Empty).Trim();
            metadata.displayName = metadata.displayName ?? string.Empty;
            metadata.utilityType = string.IsNullOrWhiteSpace(metadata.utilityType) ? "Other" : metadata.utilityType.Trim();
            metadata.areaCategory = PungentUtilityCategories.Normalize(metadata.areaCategory);
            metadata.module = metadata.module ?? string.Empty;
            metadata.description = metadata.description ?? string.Empty;
            metadata.menuPath = metadata.menuPath ?? string.Empty;
            metadata.tags = CleanArray(metadata.tags);
            metadata.packageStatus = PungentUtilityPackageStatus.Normalize(metadata.packageStatus);
            metadata.packageId = PungentUtilityPackageCatalog.NormalizePackageId(metadata.packageId);
            metadata.packageDisplayName = metadata.packageDisplayName ?? string.Empty;
            if (!Enum.IsDefined(typeof(PungentUtilityPackageTier), metadata.packageTier))
                metadata.packageTier = PungentUtilityPackageTier.Core;
            metadata.assetStoreUrl = metadata.assetStoreUrl ?? string.Empty;
            metadata.packageManagerId = metadata.packageManagerId ?? string.Empty;
            metadata.importPackagePath = metadata.importPackagePath ?? string.Empty;
            metadata.installHint = metadata.installHint ?? string.Empty;
            metadata.missingDependencyMessage = metadata.missingDependencyMessage ?? string.Empty;
            metadata.documentationTopicId = metadata.documentationTopicId ?? string.Empty;
            metadata.providedCapabilities = CleanArray(metadata.providedCapabilities);
            metadata.requiredPackageIds = CleanPackageIds(metadata.requiredPackageIds);
            metadata.optionalPackageIds = CleanPackageIds(metadata.optionalPackageIds);
            metadata.relatedUtilityIds = CleanArray(metadata.relatedUtilityIds);
            metadata.parentUtilityId = metadata.parentUtilityId ?? string.Empty;
            if (!Enum.IsDefined(typeof(PungentUtilityBrowserRole), metadata.browserRole))
                metadata.browserRole = PungentUtilityBrowserRole.CoreUtility;
            if (!Enum.IsDefined(typeof(PungentUtilityBrowserProminence), metadata.browserProminence))
                metadata.browserProminence = PungentUtilityBrowserProminence.Standard;
            metadata.browserTags = CleanArray(metadata.browserTags);
            if (!metadata.overrideBrowserCardTint && metadata.browserCardTint == default)
                metadata.browserCardTint = Color.clear;
            metadata.accessoryUtilityIds = CleanArray(metadata.accessoryUtilityIds);
            metadata.actionIds = CleanArray(metadata.actionIds);
            metadata.categoryFacets = PungentUtilityCategories.NormalizeMany(CleanArray(metadata.categoryFacets));

            if (!Enum.IsDefined(typeof(PungentUtilityItemKind), metadata.itemKind))
                metadata.itemKind = PungentUtilityItemKind.Utility;
            if (!Enum.IsDefined(typeof(PungentUtilityVisibility), metadata.visibility))
                metadata.visibility = PungentUtilityVisibility.Visible;
        }

        private static void FillUnsetValuesFromDescriptor(UtilityMetadataOverride metadata, PungentUtilityDescriptor descriptor)
        {
            if (metadata == null || descriptor == null)
                return;

            if (string.IsNullOrWhiteSpace(metadata.displayName))
                metadata.displayName = descriptor.DisplayName;
            if (string.IsNullOrWhiteSpace(metadata.utilityType))
                metadata.utilityType = descriptor.UtilityType;
            if (string.IsNullOrWhiteSpace(metadata.areaCategory) || string.Equals(metadata.areaCategory, PungentUtilityCategories.Other, StringComparison.OrdinalIgnoreCase))
                metadata.areaCategory = descriptor.AreaCategory;
            if (string.IsNullOrWhiteSpace(metadata.module))
                metadata.module = descriptor.Module;
            if (string.IsNullOrWhiteSpace(metadata.description))
                metadata.description = descriptor.Description;
            if (string.IsNullOrWhiteSpace(metadata.menuPath))
                metadata.menuPath = descriptor.MenuPath;
            if (metadata.tags == null || metadata.tags.Length == 0)
                metadata.tags = CleanArray(descriptor.Tags);
            if (metadata.sortOrder == 0)
                metadata.sortOrder = descriptor.SortOrder;
            if (string.IsNullOrWhiteSpace(metadata.packageStatus))
                metadata.packageStatus = descriptor.PackageStatus;
            if (string.IsNullOrWhiteSpace(metadata.packageId))
                metadata.packageId = descriptor.NormalizedPackageId;
            if (string.IsNullOrWhiteSpace(metadata.packageDisplayName))
                metadata.packageDisplayName = descriptor.EffectivePackageDisplayName;
            metadata.packageTier = descriptor.PackageTier;
            if (string.IsNullOrWhiteSpace(metadata.assetStoreUrl))
                metadata.assetStoreUrl = descriptor.AssetStoreUrl;
            if (string.IsNullOrWhiteSpace(metadata.packageManagerId))
                metadata.packageManagerId = descriptor.PackageManagerId;
            if (string.IsNullOrWhiteSpace(metadata.importPackagePath))
                metadata.importPackagePath = descriptor.ImportPackagePath;
            if (string.IsNullOrWhiteSpace(metadata.installHint))
                metadata.installHint = descriptor.InstallHint;
            if (string.IsNullOrWhiteSpace(metadata.missingDependencyMessage))
                metadata.missingDependencyMessage = descriptor.MissingDependencyMessage;
            if (string.IsNullOrWhiteSpace(metadata.documentationTopicId))
                metadata.documentationTopicId = descriptor.DocumentationTopicId;
            if (metadata.providedCapabilities == null || metadata.providedCapabilities.Length == 0)
                metadata.providedCapabilities = CleanArray(descriptor.ProvidedCapabilities);
            if (metadata.requiredPackageIds == null || metadata.requiredPackageIds.Length == 0)
                metadata.requiredPackageIds = CleanPackageIds(descriptor.RequiredPackageIds);
            if (metadata.optionalPackageIds == null || metadata.optionalPackageIds.Length == 0)
                metadata.optionalPackageIds = CleanPackageIds(descriptor.OptionalPackageIds);
            if (metadata.relatedUtilityIds == null || metadata.relatedUtilityIds.Length == 0)
                metadata.relatedUtilityIds = CleanArray(descriptor.RelatedUtilityIds);
            if (!metadata.overrideParentUtilityId && string.IsNullOrWhiteSpace(metadata.parentUtilityId))
                metadata.parentUtilityId = descriptor.ParentUtilityId;
            if (!metadata.overrideBrowserPriority && metadata.browserPriority == 0)
                metadata.browserPriority = descriptor.BrowserPriority;
            if (!metadata.overrideBrowserRole)
                metadata.browserRole = descriptor.BrowserRole;
            if (!metadata.overrideBrowserProminence)
                metadata.browserProminence = descriptor.BrowserProminence;
            if (!metadata.overrideBrowserTags && (metadata.browserTags == null || metadata.browserTags.Length == 0))
                metadata.browserTags = CleanArray(descriptor.BrowserTags);
            if (!metadata.overrideBrowserCardTint)
                metadata.browserCardTint = descriptor.OverrideBrowserCardTint ? descriptor.BrowserCardTint : PungentUtilityCategories.GetTint(descriptor.AreaCategory);
            if (!metadata.overrideAccessoryUtilityIds && (metadata.accessoryUtilityIds == null || metadata.accessoryUtilityIds.Length == 0))
                metadata.accessoryUtilityIds = CleanArray(descriptor.AccessoryUtilityIds);
            if (!metadata.overrideActionIds && (metadata.actionIds == null || metadata.actionIds.Length == 0))
                metadata.actionIds = CleanArray(descriptor.ActionIds);
            if (metadata.categoryFacets == null || metadata.categoryFacets.Length == 0)
                metadata.categoryFacets = CleanArray(descriptor.CategoryFacets);

            metadata.supportsSceneOverlay = descriptor.SupportsSceneOverlay;
            metadata.supportsContextMenu = descriptor.SupportsContextMenu;
            metadata.supportsSelection = descriptor.SupportsSelection;
            metadata.isLabHub = descriptor.IsLabHub;
            metadata.showInUtilitiesBrowser = descriptor.ShowInUtilitiesBrowser;
            metadata.itemKind = descriptor.ItemKind;
            metadata.visibility = descriptor.Visibility;
            NormalizeOverride(metadata);
        }

        private static UtilityMetadataOverride Clone(UtilityMetadataOverride source)
        {
            if (source == null)
                return null;

            return new UtilityMetadataOverride
            {
                utilityId = source.utilityId,
                overrideDisplayName = source.overrideDisplayName,
                displayName = source.displayName,
                overrideUtilityType = source.overrideUtilityType,
                utilityType = source.utilityType,
                overrideAreaCategory = source.overrideAreaCategory,
                areaCategory = source.areaCategory,
                overrideModule = source.overrideModule,
                module = source.module,
                overrideDescription = source.overrideDescription,
                description = source.description,
                overrideMenuPath = source.overrideMenuPath,
                menuPath = source.menuPath,
                overrideTags = source.overrideTags,
                tags = CleanArray(source.tags),
                overrideSortOrder = source.overrideSortOrder,
                sortOrder = source.sortOrder,
                overrideSupportsSceneOverlay = source.overrideSupportsSceneOverlay,
                supportsSceneOverlay = source.supportsSceneOverlay,
                overrideSupportsContextMenu = source.overrideSupportsContextMenu,
                supportsContextMenu = source.supportsContextMenu,
                overrideSupportsSelection = source.overrideSupportsSelection,
                supportsSelection = source.supportsSelection,
                overrideIsLabHub = source.overrideIsLabHub,
                isLabHub = source.isLabHub,
                overridePackageStatus = source.overridePackageStatus,
                packageStatus = source.packageStatus,
                overridePackageId = source.overridePackageId,
                packageId = source.packageId,
                overridePackageDisplayName = source.overridePackageDisplayName,
                packageDisplayName = source.packageDisplayName,
                overridePackageTier = source.overridePackageTier,
                packageTier = source.packageTier,
                overrideAssetStoreUrl = source.overrideAssetStoreUrl,
                assetStoreUrl = source.assetStoreUrl,
                overridePackageManagerId = source.overridePackageManagerId,
                packageManagerId = source.packageManagerId,
                overrideImportPackagePath = source.overrideImportPackagePath,
                importPackagePath = source.importPackagePath,
                overrideInstallHint = source.overrideInstallHint,
                installHint = source.installHint,
                overrideMissingDependencyMessage = source.overrideMissingDependencyMessage,
                missingDependencyMessage = source.missingDependencyMessage,
                overrideDocumentationTopicId = source.overrideDocumentationTopicId,
                documentationTopicId = source.documentationTopicId,
                overrideProvidedCapabilities = source.overrideProvidedCapabilities,
                providedCapabilities = CleanArray(source.providedCapabilities),
                overrideRequiredPackageIds = source.overrideRequiredPackageIds,
                requiredPackageIds = CleanPackageIds(source.requiredPackageIds),
                overrideOptionalPackageIds = source.overrideOptionalPackageIds,
                optionalPackageIds = CleanPackageIds(source.optionalPackageIds),
                overrideRelatedUtilityIds = source.overrideRelatedUtilityIds,
                relatedUtilityIds = CleanArray(source.relatedUtilityIds),
                overrideParentUtilityId = source.overrideParentUtilityId,
                parentUtilityId = source.parentUtilityId,
                overrideBrowserPriority = source.overrideBrowserPriority,
                browserPriority = source.browserPriority,
                overrideBrowserRole = source.overrideBrowserRole,
                browserRole = source.browserRole,
                overrideBrowserProminence = source.overrideBrowserProminence,
                browserProminence = source.browserProminence,
                overrideBrowserTags = source.overrideBrowserTags,
                browserTags = CleanArray(source.browserTags),
                overrideBrowserCardTint = source.overrideBrowserCardTint,
                browserCardTint = source.browserCardTint,
                overrideAccessoryUtilityIds = source.overrideAccessoryUtilityIds,
                accessoryUtilityIds = CleanArray(source.accessoryUtilityIds),
                overrideActionIds = source.overrideActionIds,
                actionIds = CleanArray(source.actionIds),
                overrideCategoryFacets = source.overrideCategoryFacets,
                categoryFacets = CleanArray(source.categoryFacets),
                overrideShowInUtilitiesBrowser = source.overrideShowInUtilitiesBrowser,
                showInUtilitiesBrowser = source.showInUtilitiesBrowser,
                overrideItemKind = source.overrideItemKind,
                itemKind = source.itemKind,
                overrideVisibility = source.overrideVisibility,
                visibility = source.visibility
            };
        }

        private static bool HasAnyOverrideEnabled(UtilityMetadataOverride metadata)
        {
            return metadata != null &&
                   (metadata.overrideDisplayName ||
                    metadata.overrideUtilityType ||
                    metadata.overrideAreaCategory ||
                    metadata.overrideModule ||
                    metadata.overrideDescription ||
                    metadata.overrideMenuPath ||
                    metadata.overrideTags ||
                    metadata.overrideSortOrder ||
                    metadata.overrideSupportsSceneOverlay ||
                    metadata.overrideSupportsContextMenu ||
                    metadata.overrideSupportsSelection ||
                    metadata.overrideIsLabHub ||
                    metadata.overridePackageStatus ||
                    metadata.overridePackageId ||
                    metadata.overridePackageDisplayName ||
                    metadata.overridePackageTier ||
                    metadata.overrideAssetStoreUrl ||
                    metadata.overridePackageManagerId ||
                    metadata.overrideImportPackagePath ||
                    metadata.overrideInstallHint ||
                    metadata.overrideMissingDependencyMessage ||
                    metadata.overrideDocumentationTopicId ||
                    metadata.overrideProvidedCapabilities ||
                    metadata.overrideRequiredPackageIds ||
                    metadata.overrideOptionalPackageIds ||
                    metadata.overrideRelatedUtilityIds ||
                    metadata.overrideParentUtilityId ||
                    metadata.overrideBrowserPriority ||
                    metadata.overrideBrowserRole ||
                    metadata.overrideBrowserProminence ||
                    metadata.overrideBrowserTags ||
                    metadata.overrideBrowserCardTint ||
                    metadata.overrideAccessoryUtilityIds ||
                    metadata.overrideActionIds ||
                    metadata.overrideCategoryFacets ||
                    metadata.overrideShowInUtilitiesBrowser ||
                    metadata.overrideItemKind ||
                    metadata.overrideVisibility);
        }

        private static string[] CleanArray(IEnumerable<string> values)
        {
            if (values == null)
                return new string[0];

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] CleanPackageIds(IEnumerable<string> values)
        {
            if (values == null)
                return new string[0];

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(PungentUtilityPackageCatalog.NormalizePackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
#endif
}
