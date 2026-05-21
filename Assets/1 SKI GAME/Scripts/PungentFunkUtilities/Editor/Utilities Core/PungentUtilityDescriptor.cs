namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    public enum PungentUtilityItemKind
    {
        Utility,
        Action,
        Internal,
        LegacyAlias
    }

    public enum PungentUtilityVisibility
    {
        Visible,
        Hidden,
        DeveloperOnly,
        Archived
    }

    public enum PungentUtilityBrowserRole
    {
        CoreUtility,
        AccessoryUtility,
        Action,
        Bridge,
        Internal,
        LegacyAlias
    }

    public enum PungentUtilityBrowserProminence
    {
        Featured,
        Standard,
        Secondary,
        HiddenUnlessSearched,
        DeveloperOnly
    }

    public enum PungentUtilityPackageTier
    {
        Core,
        Extension,
        Bridge,
        Bundle,
        ProjectAdapter,
        Internal
    }

    public enum PungentUtilityPackageAvailability
    {
        Unowned,
        OwnedNotInstalled,
        OwnedInstalled
    }

    public enum PungentUtilityPackageLifecycle
    {
        Visible,
        Hidden,
        Archived
    }

    /// <summary>
    /// Describes a reusable PungentFunk utility window or action.
    /// Descriptors are consumed by the control panel, context menus, shortcuts, and tray systems.
    /// </summary>
    [Serializable]
    public sealed class PungentUtilityDescriptor
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public string Lab;
        public string Module;
        public string Description;
        public string MenuPath;
        public string[] Tags;
        public string[] CategoryFacets;
        public string WindowTypeName;
        public bool SupportsSceneOverlay;
        public bool SupportsContextMenu;
        public bool SupportsSelection;
        public bool IsLabHub;
        public int SortOrder;
        public string PackageStatus;
        public string PackageId;
        public string PackageDisplayName;
        public PungentUtilityPackageTier PackageTier = PungentUtilityPackageTier.Core;
        public string[] RequiredPackageIds;
        public string[] OptionalPackageIds;
        public string[] ProvidedCapabilities;
        public string[] ConsumedCapabilities;
        public string[] ExtensionPointsProvided;
        public string[] ExtensionPointsConsumed;
        public string BridgeId;
        public string BridgeAvailabilityState;
        public string MissingDependencyMessage;
        public string FallbackBehavior;
        public string InstallHint;
        public string MinimumCompatibleVersion;
        public string DocumentationTopicId;
        public string AssetStoreUrl;
        public string PackageManagerId;
        public string ImportPackagePath;
        public bool IsBundleOnly;
        public string ParentUtilityId;
        public int BrowserPriority;
        public PungentUtilityBrowserRole BrowserRole = PungentUtilityBrowserRole.CoreUtility;
        public PungentUtilityBrowserProminence BrowserProminence = PungentUtilityBrowserProminence.Standard;
        public string[] BrowserTags;
        public string[] AccessoryUtilityIds;
        public string[] ActionIds;
        public string[] RelatedUtilityIds;
        public Texture2D Icon;
        public bool OverrideBrowserCardTint;
        public Color BrowserCardTint = Color.clear;
        public bool ShowInUtilitiesBrowser = true;
        public PungentUtilityItemKind ItemKind = PungentUtilityItemKind.Utility;
        public PungentUtilityVisibility Visibility = PungentUtilityVisibility.Visible;

        private Action _openAction;
        private Func<bool> _canRunAction;
        private Func<string> _disabledReasonProvider;
        [NonSerialized] private Type _resolvedWindowType;
        [NonSerialized] private bool _resolveAttempted;

        private static readonly Dictionary<string, Type> WindowTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingWindowTypeKeys = new HashSet<string>(StringComparer.Ordinal);
        private static bool WindowTypeCacheBuilt;

        public PungentUtilityDescriptor(
            string id,
            string displayName,
            string category,
            string description,
            string menuPath,
            string windowTypeName,
            string[] tags = null,
            bool supportsSceneOverlay = false,
            bool supportsContextMenu = false,
            bool supportsSelection = false,
            Action openAction = null,
            string lab = null,
            string module = null,
            int sortOrder = 0,
            bool isLabHub = false,
            string packageStatus = null,
            string[] relatedUtilityIds = null,
            string[] categoryFacets = null,
            PungentUtilityItemKind itemKind = PungentUtilityItemKind.Utility,
            PungentUtilityVisibility visibility = PungentUtilityVisibility.Visible,
            Func<bool> canRunAction = null,
            Func<string> disabledReasonProvider = null,
            string packageId = null,
            string packageDisplayName = null,
            PungentUtilityPackageTier packageTier = PungentUtilityPackageTier.Core,
            string[] requiredPackageIds = null,
            string[] optionalPackageIds = null,
            string[] providedCapabilities = null,
            string[] consumedCapabilities = null,
            string[] extensionPointsProvided = null,
            string[] extensionPointsConsumed = null,
            string bridgeId = null,
            string bridgeAvailabilityState = null,
            string missingDependencyMessage = null,
            string fallbackBehavior = null,
            string installHint = null,
            string minimumCompatibleVersion = null,
            string documentationTopicId = null,
            string assetStoreUrl = null,
            string packageManagerId = null,
            string importPackagePath = null,
            bool isBundleOnly = false,
            string parentUtilityId = null,
            int browserPriority = int.MinValue,
            PungentUtilityBrowserRole? browserRole = null,
            PungentUtilityBrowserProminence? browserProminence = null,
            string[] browserTags = null,
            string[] accessoryUtilityIds = null,
            string[] actionIds = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim();
            Lab = PungentUtilityCategories.Normalize(lab);
            Module = string.IsNullOrWhiteSpace(module) ? Category : module.Trim();
            Description = description ?? string.Empty;
            MenuPath = menuPath ?? string.Empty;
            WindowTypeName = windowTypeName ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
            CategoryFacets = PungentUtilityCategories.NormalizeMany(categoryFacets);
            SupportsSceneOverlay = supportsSceneOverlay;
            SupportsContextMenu = supportsContextMenu;
            SupportsSelection = supportsSelection;
            IsLabHub = isLabHub;
            SortOrder = sortOrder;
            PackageStatus = PungentUtilityPackageStatus.Normalize(packageStatus);
            PackageId = packageId ?? string.Empty;
            PackageDisplayName = packageDisplayName ?? string.Empty;
            PackageTier = packageTier;
            RequiredPackageIds = CleanArray(requiredPackageIds);
            OptionalPackageIds = CleanArray(optionalPackageIds);
            ProvidedCapabilities = CleanArray(providedCapabilities);
            ConsumedCapabilities = CleanArray(consumedCapabilities);
            ExtensionPointsProvided = CleanArray(extensionPointsProvided);
            ExtensionPointsConsumed = CleanArray(extensionPointsConsumed);
            BridgeId = bridgeId ?? string.Empty;
            BridgeAvailabilityState = bridgeAvailabilityState ?? string.Empty;
            MissingDependencyMessage = missingDependencyMessage ?? string.Empty;
            FallbackBehavior = fallbackBehavior ?? string.Empty;
            InstallHint = installHint ?? string.Empty;
            MinimumCompatibleVersion = minimumCompatibleVersion ?? string.Empty;
            DocumentationTopicId = documentationTopicId ?? string.Empty;
            AssetStoreUrl = assetStoreUrl ?? string.Empty;
            PackageManagerId = packageManagerId ?? string.Empty;
            ImportPackagePath = importPackagePath ?? string.Empty;
            IsBundleOnly = isBundleOnly;
            ParentUtilityId = CleanId(parentUtilityId);
            BrowserPriority = browserPriority == int.MinValue ? sortOrder : browserPriority;
            BrowserRole = browserRole ?? MapDefaultBrowserRole(itemKind, visibility, packageTier, ParentUtilityId);
            BrowserProminence = browserProminence ?? MapDefaultBrowserProminence(visibility);
            BrowserTags = CleanArray(browserTags);
            AccessoryUtilityIds = CleanArray(accessoryUtilityIds);
            ActionIds = CleanArray(actionIds);
            RelatedUtilityIds = relatedUtilityIds ?? Array.Empty<string>();
            ItemKind = itemKind;
            Visibility = visibility;
            _openAction = openAction;
            _canRunAction = canRunAction;
            _disabledReasonProvider = disabledReasonProvider;
        }

        public bool CanOpen => _openAction != null || ResolveWindowType() != null;

        public bool CanRun
        {
            get
            {
                if (!CanOpen)
                    return false;

                if (!PungentUtilityAccessPolicy.CanAccess(this))
                    return false;

                if (_canRunAction == null)
                    return true;

                try
                {
                    return _canRunAction.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"PungentFunk utility action validation failed for '{Id}': {ex.Message}");
                    return false;
                }
            }
        }

        public string DisabledReason
        {
            get
            {
                if (!CanOpen)
                    return string.IsNullOrWhiteSpace(WindowTypeName) ? "No callable action is registered." : $"Missing window type: {WindowTypeName}";

                if (!PungentUtilityAccessPolicy.CanAccess(this, out string accessReason))
                    return accessReason;

                if (CanRun)
                    return string.Empty;

                if (_disabledReasonProvider != null)
                {
                    try
                    {
                        string reason = _disabledReasonProvider.Invoke();
                        if (!string.IsNullOrWhiteSpace(reason))
                            return reason.Trim();
                    }
                    catch (Exception ex)
                    {
                        return "Validation failed: " + ex.Message;
                    }
                }

                return IsAction ? "This action is not available in the current context." : "This utility is not available in the current context.";
            }
        }

        /// <summary>Broad product-facing category used by the Utilities Browser. Backed by the legacy Lab field for compatibility.</summary>
        public string AreaCategory
        {
            get => PungentUtilityCategories.Normalize(Lab);
            set => Lab = PungentUtilityCategories.Normalize(value);
        }

        /// <summary>Granular utility type used for filtering inside a broad category. Backed by the legacy Category field for compatibility.</summary>
        public string UtilityType
        {
            get => string.IsNullOrWhiteSpace(Category) ? "Other" : Category.Trim();
            set => Category = string.IsNullOrWhiteSpace(value) ? "Other" : value.Trim();
        }

        /// <summary>Product-facing primary/workspace marker. Backed by the legacy IsLabHub field for compatibility.</summary>
        public bool IsPrimaryUtility
        {
            get => IsLabHub;
            set => IsLabHub = value;
        }

        public string Status => PungentUtilityPackageStatus.Normalize(PackageStatus);

        public string NormalizedPackageId => PungentUtilityPackageCatalog.NormalizePackageId(PackageId);

        public string EffectivePackageDisplayName => PungentUtilityPackageCatalog.GetDisplayName(this);

        public PungentUtilityPackageAvailability Availability => PungentUtilityPackageCatalog.ResolveAvailability(this);

        public bool IsOwned => Availability != PungentUtilityPackageAvailability.Unowned;

        public bool IsInstalled => Availability == PungentUtilityPackageAvailability.OwnedInstalled;

        public bool IsPackageGated
        {
            get
            {
                return IsBundleOnly ||
                       RequiredPackageIdsContainAny() ||
                       Availability != PungentUtilityPackageAvailability.OwnedInstalled ||
                       !string.Equals(NormalizedPackageId, PungentUtilityPackageCatalog.CorePackageId, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool HasRelatedUtilities => RelatedUtilityIds != null && RelatedUtilityIds.Length > 0;

        public bool HasCategoryFacets => CategoryFacets != null && CategoryFacets.Length > 0;

        public bool IsAction => ItemKind == PungentUtilityItemKind.Action;

        public bool IsUtility => ItemKind == PungentUtilityItemKind.Utility || ItemKind == PungentUtilityItemKind.Internal || ItemKind == PungentUtilityItemKind.LegacyAlias;

        public bool IsArchived => Visibility == PungentUtilityVisibility.Archived || ItemKind == PungentUtilityItemKind.LegacyAlias;

        public bool IsDeveloperOnly => Visibility == PungentUtilityVisibility.DeveloperOnly || ItemKind == PungentUtilityItemKind.Internal;

        public bool IsHiddenOrInternal => !ShowInUtilitiesBrowser || Visibility == PungentUtilityVisibility.Hidden || IsDeveloperOnly;

        public bool IsVisibleInDefaultBrowser => ShowInUtilitiesBrowser && Visibility == PungentUtilityVisibility.Visible && ItemKind != PungentUtilityItemKind.Internal && ItemKind != PungentUtilityItemKind.LegacyAlias;

        public IReadOnlyList<string> AllCategories => GetAllCategoryIds();

        public string[] GetAllCategoryIds()
        {
            return new[] { AreaCategory }
                .Concat(CategoryFacets ?? Array.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(PungentUtilityCategories.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityCategories.SortKey)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool HasCategory(string categoryIdOrName)
        {
            if (string.IsNullOrWhiteSpace(categoryIdOrName))
                return false;

            string normalized = PungentUtilityCategories.Normalize(categoryIdOrName);
            return GetAllCategoryIds().Any(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public void SetOpenAction(Action action)
        {
            _openAction = action;
        }

        public void SetActionValidation(Func<bool> canRunAction, Func<string> disabledReasonProvider = null)
        {
            _canRunAction = canRunAction;
            _disabledReasonProvider = disabledReasonProvider;
        }

        public void Open()
        {
            OpenDirect();
        }

        /// <summary>
        /// Backwards-compatible open method used by the Utility Tray and older callers.
        /// </summary>
        public void OpenDirect()
        {
            if (!CanRun)
            {
                EditorUtility.DisplayDialog("PungentFunk Utilities", DisabledReason, "OK");
                return;
            }

            if (_openAction != null)
            {
                _openAction.Invoke();
                return;
            }

            Type type = ResolveWindowType();
            if (type == null)
            {
                EditorUtility.DisplayDialog("PungentFunk Utilities", $"Could not find utility window type '{WindowTypeName}'.", "OK");
                return;
            }

            EditorWindow window = EditorWindow.GetWindow(type, false, DisplayName);
            window.titleContent = new GUIContent(DisplayName, Icon);
            window.Show();
        }

        public Type ResolveWindowType()
        {
            if (_resolveAttempted)
                return _resolvedWindowType;

            _resolveAttempted = true;

            if (string.IsNullOrEmpty(WindowTypeName))
                return null;

            BuildWindowTypeCacheIfNeeded();

            if (WindowTypeCache.TryGetValue(WindowTypeName, out _resolvedWindowType))
                return _resolvedWindowType;

            Type direct = Type.GetType(WindowTypeName);
            if (IsEditorWindowType(direct))
            {
                CacheWindowType(direct);
                _resolvedWindowType = direct;
                return _resolvedWindowType;
            }

            if (!MissingWindowTypeKeys.Contains(WindowTypeName))
            {
                MissingWindowTypeKeys.Add(WindowTypeName);
                PungentEditorPerformanceUtility.RecordTypeResolutionFallbackScan(WindowTypeName);
            }

            return null;
        }

        public static void InvalidateTypeCache()
        {
            WindowTypeCache.Clear();
            MissingWindowTypeKeys.Clear();
            WindowTypeCacheBuilt = false;
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string q = query.Trim();
            return Contains(DisplayName, q) ||
                   Contains(UtilityType, q) ||
                   Contains(AreaCategory, q) ||
                   PungentUtilityCategories.Matches(AreaCategory, q) ||
                   Contains(Module, q) ||
                   Contains(Status, q) ||
                   Contains(NormalizedPackageId, q) ||
                   Contains(EffectivePackageDisplayName, q) ||
                   Contains(PackageTier.ToString(), q) ||
                   Contains(Availability.ToString(), q) ||
                   Contains(BridgeId, q) ||
                   Contains(BridgeAvailabilityState, q) ||
                   Contains(MissingDependencyMessage, q) ||
                   Contains(FallbackBehavior, q) ||
                   Contains(InstallHint, q) ||
                   Contains(MinimumCompatibleVersion, q) ||
                   Contains(DocumentationTopicId, q) ||
                   Contains(AssetStoreUrl, q) ||
                   Contains(PackageManagerId, q) ||
                   Contains(ImportPackagePath, q) ||
                   Contains(ParentUtilityId, q) ||
                   Contains(BrowserPriority.ToString(), q) ||
                   Contains(BrowserRole.ToString(), q) ||
                   Contains(BrowserProminence.ToString(), q) ||
                   Contains(ItemKind.ToString(), q) ||
                   Contains(Visibility.ToString(), q) ||
                   Contains(Description, q) ||
                   Contains(MenuPath, q) ||
                   Contains(WindowTypeName, q) ||
                   CategoryFacetsContain(q) ||
                   TagsContain(q) ||
                   BrowserTagsContain(q) ||
                   PackageArraysContain(q) ||
                   PungentUtilityPackageCatalog.DescriptorMatchesCatalogMetadata(this, q) ||
                   BrowserIdsContain(q) ||
                   RelatedIdsContain(q);
        }

        public bool MatchesSearch(string query) => Matches(query);

        private bool CategoryFacetsContain(string query)
        {
            foreach (string category in GetAllCategoryIds())
            {
                if (Contains(category, query) || PungentUtilityCategories.Matches(category, query))
                    return true;
            }

            return false;
        }

        private bool TagsContain(string query)
        {
            if (Tags == null)
                return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (Contains(Tags[i], query))
                    return true;
            }

            return false;
        }

        private bool BrowserTagsContain(string query)
        {
            if (BrowserTags == null)
                return false;

            for (int i = 0; i < BrowserTags.Length; i++)
            {
                if (Contains(BrowserTags[i], query))
                    return true;
            }

            return false;
        }

        private bool BrowserIdsContain(string query)
        {
            return ArrayContains(AccessoryUtilityIds, query) ||
                   ArrayContains(ActionIds, query);
        }

        private bool RelatedIdsContain(string query)
        {
            if (RelatedUtilityIds == null)
                return false;

            for (int i = 0; i < RelatedUtilityIds.Length; i++)
            {
                if (Contains(RelatedUtilityIds[i], query))
                    return true;
            }

            return false;
        }

        private bool PackageArraysContain(string query)
        {
            return ArrayContains(RequiredPackageIds, query) ||
                   ArrayContains(OptionalPackageIds, query) ||
                   ArrayContains(ProvidedCapabilities, query) ||
                   ArrayContains(ConsumedCapabilities, query) ||
                   ArrayContains(ExtensionPointsProvided, query) ||
                   ArrayContains(ExtensionPointsConsumed, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool RequiredPackageIdsContainAny()
        {
            return RequiredPackageIds != null && RequiredPackageIds.Any(id => !string.IsNullOrWhiteSpace(id));
        }

        private static bool ArrayContains(string[] values, string query)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (Contains(values[i], query))
                    return true;
            }

            return false;
        }

        private static string[] CleanArray(IEnumerable<string> values)
        {
            if (values == null)
                return Array.Empty<string>();

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string CleanId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static PungentUtilityBrowserRole MapDefaultBrowserRole(PungentUtilityItemKind itemKind, PungentUtilityVisibility visibility, PungentUtilityPackageTier packageTier, string parentUtilityId)
        {
            if (itemKind == PungentUtilityItemKind.Action)
                return PungentUtilityBrowserRole.Action;
            if (itemKind == PungentUtilityItemKind.Internal || visibility == PungentUtilityVisibility.DeveloperOnly)
                return PungentUtilityBrowserRole.Internal;
            if (itemKind == PungentUtilityItemKind.LegacyAlias || visibility == PungentUtilityVisibility.Archived)
                return PungentUtilityBrowserRole.LegacyAlias;
            if (packageTier == PungentUtilityPackageTier.Bridge)
                return PungentUtilityBrowserRole.Bridge;
            if (!string.IsNullOrWhiteSpace(parentUtilityId))
                return PungentUtilityBrowserRole.AccessoryUtility;
            return PungentUtilityBrowserRole.CoreUtility;
        }

        private static PungentUtilityBrowserProminence MapDefaultBrowserProminence(PungentUtilityVisibility visibility)
        {
            if (visibility == PungentUtilityVisibility.DeveloperOnly)
                return PungentUtilityBrowserProminence.DeveloperOnly;
            if (visibility == PungentUtilityVisibility.Hidden || visibility == PungentUtilityVisibility.Archived)
                return PungentUtilityBrowserProminence.HiddenUnlessSearched;
            return PungentUtilityBrowserProminence.Standard;
        }

        private static void BuildWindowTypeCacheIfNeeded()
        {
            if (WindowTypeCacheBuilt)
                return;

            WindowTypeCacheBuilt = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (!IsEditorWindowType(type))
                        continue;

                    CacheWindowType(type);
                }
            }
        }

        private static void CacheWindowType(Type type)
        {
            if (!IsEditorWindowType(type))
                return;

            if (!string.IsNullOrEmpty(type.AssemblyQualifiedName))
                WindowTypeCache[type.AssemblyQualifiedName] = type;
            if (!string.IsNullOrEmpty(type.FullName))
                WindowTypeCache[type.FullName] = type;
            if (!string.IsNullOrEmpty(type.Name))
                WindowTypeCache[type.Name] = type;
        }

        private static bool IsEditorWindowType(Type type)
        {
            return type != null && typeof(EditorWindow).IsAssignableFrom(type);
        }
    }
#endif
}
