using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    [Serializable]
    public sealed class PungentUtilityPackageRecord
    {
        public string packageId;
        public string displayName;
        public PungentUtilityPackageTier tier;
        public PungentUtilityPackageAvailability availability;
        public string assetStoreUrl;
        public string packageManagerId;
        public string importPackagePath;
        [TextArea(2, 5)] public string description;
        public string[] includedUtilityIds = new string[0];
        public string[] includedCapabilities = new string[0];
        public string installHint;
        public bool developerOverrideAvailability;
        public bool showPromotionalPricing;
        public string currencyCode;
        public string regularPriceText;
        public string salePriceText;
        public int discountPercent;
        public string saleEndsIsoUtc;
        public string promotionalLabel;
        public PungentUtilityPackageLifecycle lifecycle = PungentUtilityPackageLifecycle.Visible;
    }

    [FilePath("ProjectSettings/PungentFunkUtilities/PackageCatalog.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityPackageCatalogSettings : ScriptableSingleton<PungentUtilityPackageCatalogSettings>
    {
        [SerializeField] private List<PungentUtilityPackageRecord> records = new List<PungentUtilityPackageRecord>();

        public IReadOnlyList<PungentUtilityPackageRecord> Records
        {
            get
            {
                NormalizeInMemory();
                return records;
            }
        }

        public PungentUtilityPackageRecord GetOverrideCopy(string packageId)
        {
            PungentUtilityPackageRecord record = FindInternal(packageId);
            return record == null ? null : PungentUtilityPackageCatalog.CloneRecord(record);
        }

        public bool HasOverride(string packageId)
        {
            return FindInternal(packageId) != null;
        }

        public void SetRecordOverride(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return;

            PungentUtilityPackageCatalog.NormalizeRecord(record);
            if (string.IsNullOrWhiteSpace(record.packageId))
                return;

            NormalizeInMemory();
            int index = records.FindIndex(item => string.Equals(item.packageId, record.packageId, StringComparison.OrdinalIgnoreCase));
            PungentUtilityPackageRecord copy = PungentUtilityPackageCatalog.CloneRecord(record);
            if (index >= 0)
                records[index] = copy;
            else
                records.Add(copy);

            SaveStore();
            PungentUtilityPackageCatalog.NotifyChanged();
        }

        public void RemoveRecordOverride(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return;

            NormalizeInMemory();
            records.RemoveAll(item => string.Equals(item.packageId, PungentUtilityPackageCatalog.NormalizePackageId(packageId), StringComparison.OrdinalIgnoreCase));
            SaveStore();
            PungentUtilityPackageCatalog.NotifyChanged();
        }

        public void SaveStore()
        {
            NormalizeInMemory();
            records.Sort((a, b) => string.Compare(a.packageId, b.packageId, StringComparison.OrdinalIgnoreCase));
            Save(true);
        }

        private PungentUtilityPackageRecord FindInternal(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return null;

            NormalizeInMemory();
            string id = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            return records.FirstOrDefault(item => string.Equals(item.packageId, id, StringComparison.OrdinalIgnoreCase));
        }

        private void NormalizeInMemory()
        {
            if (records == null)
                records = new List<PungentUtilityPackageRecord>();

            records.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.packageId));

            for (int i = 0; i < records.Count; i++)
                PungentUtilityPackageCatalog.NormalizeRecord(records[i]);

            for (int i = records.Count - 1; i >= 0; i--)
            {
                string id = records[i].packageId;
                int first = records.FindIndex(item => string.Equals(item.packageId, id, StringComparison.OrdinalIgnoreCase));
                if (first >= 0 && first != i)
                    records.RemoveAt(i);
            }
        }
    }

    public static class PungentUtilityPackageCatalog
    {
        public const string CorePackageId = "com.pungentfunk.utilities.core";
        public const string CorePackageDisplayName = "PungentFunk Utilities Core";

        private const string FullBundlePackageId = "com.pungentfunk.utilities.full-bundle";

        private static readonly Dictionary<string, string> DefaultUtilityPackages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "utilities-browser", CorePackageId },
            { "theme-customizer", CorePackageId },
            { "help-browser", CorePackageId },
            { "utility-tray", CorePackageId },
            { "minimize-focused-window", CorePackageId },
            { "open-minimized-utilities-tray", CorePackageId },
            { "developer-tools", CorePackageId },
            { "utility-metadata-editor", CorePackageId },
            { "category-reference-membership", CorePackageId },
            { "documentation-links", CorePackageId },
            { "qa-checklist-utility", CorePackageId },

            { "design-validation-audit", "com.pungentfunk.utilities.audit" },
            { "reference-assignment-scanner", "com.pungentfunk.utilities.audit" },
            { "terrain-usage-scanner", "com.pungentfunk.utilities.audit" },
            { "scene-issue-scanner", "com.pungentfunk.utilities.audit" },
            { "coverage-matrix", "com.pungentfunk.utilities.audit" },
            { "token-validator", "com.pungentfunk.utilities.audit" },

            { "asset-placement-lab", "com.pungentfunk.utilities.scene-authoring" },
            { "create-placement-asset-set-from-selection", "com.pungentfunk.utilities.scene-authoring" },
            { "add-placement-socket", "com.pungentfunk.utilities.scene-authoring" },
            { "surface-align", "com.pungentfunk.utilities.scene-authoring" },
            { "scene-navigation", "com.pungentfunk.utilities.scene-authoring" },
            { "fast-travel-sceneview-to-selection", "com.pungentfunk.utilities.scene-authoring" },
            { "spatial-authoring-workbench", "com.pungentfunk.utilities.scene-authoring" },
            { "path-authoring-toolkit", "com.pungentfunk.utilities.scene-authoring" },
            { "spatial-authoring-toolkit", "com.pungentfunk.utilities.scene-authoring" },
            { "bulk-rename", "com.pungentfunk.utilities.scene-authoring" },

            { "scene-gizmo-browser", "com.pungentfunk.utilities.scene-authoring" },
            { "add-scene-gizmo-source", "com.pungentfunk.utilities.scene-authoring" },
            { "add-scene-beacon", "com.pungentfunk.utilities.scene-authoring" },
            { "add-collision-sensor-gizmo", "com.pungentfunk.utilities.scene-authoring" },
            { "add-trigger-sensor-gizmo", "com.pungentfunk.utilities.scene-authoring" },
            { "add-trajectory-visualizer", "com.pungentfunk.utilities.scene-authoring" },

            { "font-preview", "com.pungentfunk.utilities.asset-production" },
            { "prefab-icon-generator", "com.pungentfunk.utilities.asset-production" },
            { "prefab-asset-exporter", "com.pungentfunk.utilities.asset-production" },
            { "save-selected-prefab-with-mesh-assets", "com.pungentfunk.utilities.asset-production" },
            { "texture-array-baker", "com.pungentfunk.utilities.asset-production" },

            { "palette-designer", "com.pungentfunk.utilities.appearance" },
            { "procedural-texture-lab", "com.pungentfunk.utilities.appearance" },
            { "editor-style-explorer", "com.pungentfunk.utilities.appearance" },

            { "audio-setup-coverage", "com.pungentfunk.utilities.audio" },
            { "audio-catalog-coverage", "com.pungentfunk.utilities.audio" },

            { "debug-control", "com.pungentfunk.utilities.debug" },

            { "input-prompt-icon-library", "com.pungentfunk.utilities.ui-input" },
            { "populate-selected-input-prompt-library", "com.pungentfunk.utilities.ui-input" },
            { "populate-default-input-prompt-library", "com.pungentfunk.utilities.ui-input" },

            { "name-generator", "com.pungentfunk.utilities.content-generation" },

            { "board-editor", "com.pungentfunk.utilities.board" },

            { "tooltip-notes", "com.pungentfunk.utilities.authoring" },
            { "component-tuning-copy", "com.pungentfunk.utilities.authoring" },

            { "data-sheet-editor", "com.pungentfunk.utilities.datasheet" }
        };

        public static event Action Changed;
        private static List<PungentUtilityPackageRecord> _cachedMergedRecords;
        private static List<PungentUtilityPackageRecord> _cachedSortedRecords;
        private static Dictionary<string, PungentUtilityPackageRecord> _cachedMergedRecordsById;

        public static IReadOnlyList<PungentUtilityPackageRecord> Records
        {
            get
            {
                EnsureMergedRecordCache();
                return _cachedSortedRecords;
            }
        }

        public static void NotifyChanged()
        {
            InvalidateCache();
            Changed?.Invoke();
        }

        public static void InvalidateCache()
        {
            _cachedMergedRecords = null;
            _cachedSortedRecords = null;
            _cachedMergedRecordsById = null;
        }

        public static string NormalizePackageId(string packageId)
        {
            return string.IsNullOrWhiteSpace(packageId) ? CorePackageId : packageId.Trim().ToLowerInvariant();
        }

        public static string GetDisplayName(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return CorePackageDisplayName;

            if (!string.IsNullOrWhiteSpace(descriptor.PackageDisplayName))
                return descriptor.PackageDisplayName.Trim();

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            return record == null || string.IsNullOrWhiteSpace(record.displayName) ? descriptor.NormalizedPackageId : record.displayName;
        }

        public static string GetDisplayName(string packageId)
        {
            PungentUtilityPackageRecord record = FindRecord(packageId);
            return record == null || string.IsNullOrWhiteSpace(record.displayName) ? NormalizePackageId(packageId) : record.displayName;
        }

        public static PungentUtilityPackageRecord FindRecord(string packageId)
        {
            string id = NormalizePackageId(packageId);
            EnsureMergedRecordCache();
            return _cachedMergedRecordsById.TryGetValue(id, out PungentUtilityPackageRecord record) ? record : null;
        }

        public static PungentUtilityPackageRecord CreateEditableRecord(string packageId)
        {
            PungentUtilityPackageRecord record = FindRecord(packageId);
            if (record != null)
                return CloneRecord(record);

            return new PungentUtilityPackageRecord
            {
                packageId = NormalizePackageId(packageId),
                displayName = GetDisplayName(packageId),
                tier = PungentUtilityPackageTier.Extension,
                availability = PungentUtilityPackageAvailability.OwnedNotInstalled
            };
        }

        public static void SaveRecordOverride(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return;

            Undo.RecordObject(PungentUtilityPackageCatalogSettings.instance, "Save Utility Package Catalog");
            PungentUtilityPackageCatalogSettings.instance.SetRecordOverride(record);
        }

        public static void ClearRecordOverride(string packageId)
        {
            Undo.RecordObject(PungentUtilityPackageCatalogSettings.instance, "Clear Utility Package Catalog Override");
            PungentUtilityPackageCatalogSettings.instance.RemoveRecordOverride(packageId);
        }

        public static void ApplyDescriptorDefaults(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return;

            if (string.IsNullOrWhiteSpace(descriptor.PackageId) && DefaultUtilityPackages.TryGetValue(descriptor.Id ?? string.Empty, out string mappedPackageId))
                descriptor.PackageId = mappedPackageId;

            NormalizeDescriptorMetadata(descriptor);

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            if (record == null)
                return;

            if (string.IsNullOrWhiteSpace(descriptor.PackageDisplayName))
                descriptor.PackageDisplayName = record.displayName;
            if (descriptor.PackageTier == PungentUtilityPackageTier.Core && record.tier != PungentUtilityPackageTier.Core)
                descriptor.PackageTier = record.tier;
            if ((descriptor.ProvidedCapabilities == null || descriptor.ProvidedCapabilities.Length == 0) && record.includedCapabilities != null)
                descriptor.ProvidedCapabilities = CleanArray(record.includedCapabilities);
            if (string.IsNullOrWhiteSpace(descriptor.AssetStoreUrl))
                descriptor.AssetStoreUrl = record.assetStoreUrl ?? string.Empty;
            if (string.IsNullOrWhiteSpace(descriptor.PackageManagerId))
                descriptor.PackageManagerId = record.packageManagerId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(descriptor.ImportPackagePath))
                descriptor.ImportPackagePath = record.importPackagePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(descriptor.InstallHint))
                descriptor.InstallHint = record.installHint ?? string.Empty;
        }

        public static void NormalizeDescriptorMetadata(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return;

            descriptor.PackageId = NormalizePackageId(descriptor.PackageId);
            descriptor.PackageDisplayName = descriptor.PackageDisplayName ?? string.Empty;
            if (!Enum.IsDefined(typeof(PungentUtilityPackageTier), descriptor.PackageTier))
                descriptor.PackageTier = PungentUtilityPackageTier.Core;
            descriptor.RequiredPackageIds = CleanPackageIds(descriptor.RequiredPackageIds);
            descriptor.OptionalPackageIds = CleanPackageIds(descriptor.OptionalPackageIds);
            descriptor.ProvidedCapabilities = CleanArray(descriptor.ProvidedCapabilities);
            descriptor.ConsumedCapabilities = CleanArray(descriptor.ConsumedCapabilities);
            descriptor.ExtensionPointsProvided = CleanArray(descriptor.ExtensionPointsProvided);
            descriptor.ExtensionPointsConsumed = CleanArray(descriptor.ExtensionPointsConsumed);
            descriptor.BridgeId = descriptor.BridgeId ?? string.Empty;
            descriptor.BridgeAvailabilityState = descriptor.BridgeAvailabilityState ?? string.Empty;
            descriptor.MissingDependencyMessage = descriptor.MissingDependencyMessage ?? string.Empty;
            descriptor.FallbackBehavior = descriptor.FallbackBehavior ?? string.Empty;
            descriptor.InstallHint = descriptor.InstallHint ?? string.Empty;
            descriptor.MinimumCompatibleVersion = descriptor.MinimumCompatibleVersion ?? string.Empty;
            descriptor.DocumentationTopicId = descriptor.DocumentationTopicId ?? string.Empty;
            descriptor.AssetStoreUrl = descriptor.AssetStoreUrl ?? string.Empty;
            descriptor.PackageManagerId = descriptor.PackageManagerId ?? string.Empty;
            descriptor.ImportPackagePath = descriptor.ImportPackagePath ?? string.Empty;
            descriptor.ParentUtilityId = descriptor.ParentUtilityId ?? string.Empty;
            descriptor.BrowserPriority = descriptor.BrowserPriority == 0 && descriptor.SortOrder != 0 ? descriptor.SortOrder : descriptor.BrowserPriority;
            if (!Enum.IsDefined(typeof(PungentUtilityBrowserRole), descriptor.BrowserRole))
                descriptor.BrowserRole = descriptor.IsAction ? PungentUtilityBrowserRole.Action : PungentUtilityBrowserRole.CoreUtility;
            if (!Enum.IsDefined(typeof(PungentUtilityBrowserProminence), descriptor.BrowserProminence))
                descriptor.BrowserProminence = descriptor.IsDeveloperOnly ? PungentUtilityBrowserProminence.DeveloperOnly : PungentUtilityBrowserProminence.Standard;
            descriptor.BrowserTags = CleanArray(descriptor.BrowserTags);
            descriptor.AccessoryUtilityIds = CleanArray(descriptor.AccessoryUtilityIds);
            descriptor.ActionIds = CleanArray(descriptor.ActionIds);
        }

        public static PungentUtilityPackageAvailability ResolveAvailability(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return PungentUtilityPackageAvailability.OwnedInstalled;

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            if (record != null)
            {
                if (record.developerOverrideAvailability)
                    return record.availability;
            }

            return PungentUtilityPackageAvailability.OwnedInstalled;
        }

        public static PungentUtilityPackageAvailability ResolveAvailability(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return PungentUtilityPackageAvailability.OwnedInstalled;

            NormalizeRecord(record);
            if (record.developerOverrideAvailability)
                return record.availability;

            return record.availability;
        }

        public static bool MatchesRecord(PungentUtilityPackageRecord record, string query)
        {
            if (record == null)
                return false;

            if (string.IsNullOrWhiteSpace(query))
                return true;

            string q = query.Trim();
            return Contains(record.packageId, q) ||
                   Contains(record.displayName, q) ||
                   Contains(record.description, q) ||
                   Contains(record.installHint, q) ||
                   Contains(record.assetStoreUrl, q) ||
                   Contains(record.packageManagerId, q) ||
                   Contains(record.importPackagePath, q) ||
                   Contains(record.promotionalLabel, q) ||
                   Contains(record.regularPriceText, q) ||
                   Contains(record.salePriceText, q) ||
                   Contains(record.currencyCode, q) ||
                   Contains(record.tier.ToString(), q) ||
                   Contains(ResolveAvailability(record).ToString(), q) ||
                   ArrayContains(record.includedUtilityIds, q) ||
                   ArrayContains(record.includedCapabilities, q);
        }

        public static bool DescriptorMatchesCatalogMetadata(PungentUtilityDescriptor descriptor, string query)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(query))
                return false;

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            return record != null && MatchesRecord(record, query);
        }

        public static string BuildAvailabilityLabel(PungentUtilityPackageAvailability availability)
        {
            switch (availability)
            {
                case PungentUtilityPackageAvailability.Unowned:
                    return "Available";
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return "Owned - Not Installed";
                default:
                    return "Installed";
            }
        }

        public static string BuildShortAvailabilityLabel(PungentUtilityPackageAvailability availability)
        {
            switch (availability)
            {
                case PungentUtilityPackageAvailability.Unowned:
                    return "Available";
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return "Install";
                default:
                    return "Installed";
            }
        }

        public static Color GetAvailabilityTint(PungentUtilityPackageAvailability availability)
        {
            switch (availability)
            {
                case PungentUtilityPackageAvailability.Unowned:
                    return UtilityWindowTheme.Purple;
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Green;
            }
        }

        public static string BuildAvailabilityTooltip(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return string.Empty;

            PungentUtilityPackageAvailability availability = ResolveAvailability(descriptor);
            string required = descriptor.RequiredPackageIds == null || descriptor.RequiredPackageIds.Length == 0
                ? "Required packages: none"
                : "Required packages: " + string.Join(", ", descriptor.RequiredPackageIds);
            string hint = FirstNonEmpty(descriptor.InstallHint, descriptor.FallbackBehavior, descriptor.MissingDependencyMessage, "No install hint configured.");

            return descriptor.EffectivePackageDisplayName + "\n" +
                   "Package ID: " + descriptor.NormalizedPackageId + "\n" +
                   "Availability: " + BuildAvailabilityLabel(availability) + "\n" +
                   "Install/fallback: " + hint + "\n" +
                   required;
        }

        public static bool TryOpenAssetStore(PungentUtilityDescriptor descriptor, out string message)
        {
            PungentUtilityPackageRecord record = descriptor == null ? null : FindRecord(descriptor.NormalizedPackageId);
            string url = FirstNonEmpty(descriptor == null ? null : descriptor.AssetStoreUrl, record == null ? null : record.assetStoreUrl);
            return TryOpenUrl(url, "Asset Store", out message);
        }

        public static bool TryOpenAssetStore(PungentUtilityPackageRecord record, out string message)
        {
            return TryOpenUrl(record == null ? null : record.assetStoreUrl, "Asset Store", out message);
        }

        public static bool HasAssetStoreRoute(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return false;

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            string url = FirstNonEmpty(descriptor.AssetStoreUrl, record == null ? null : record.assetStoreUrl);
            return PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(url);
        }

        public static bool HasAssetStoreRoute(PungentUtilityPackageRecord record)
        {
            return record != null && PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(record.assetStoreUrl);
        }

        public static bool OpenInstallFlow(PungentUtilityDescriptor descriptor, out string message)
        {
            if (descriptor == null)
            {
                message = "No utility was selected.";
                return false;
            }

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            string packageManagerId = FirstNonEmpty(descriptor.PackageManagerId, record == null ? null : record.packageManagerId);
            string importPackagePath = FirstNonEmpty(descriptor.ImportPackagePath, record == null ? null : record.importPackagePath);

            return OpenInstallFlow(descriptor.EffectivePackageDisplayName, descriptor.NormalizedPackageId, packageManagerId, importPackagePath, FirstNonEmpty(descriptor.InstallHint, record == null ? null : record.installHint), out message);
        }

        public static bool OpenInstallFlow(PungentUtilityPackageRecord record, out string message)
        {
            if (record == null)
            {
                message = "No package was selected.";
                return false;
            }

            NormalizeRecord(record);
            return OpenInstallFlow(record.displayName, record.packageId, record.packageManagerId, record.importPackagePath, record.installHint, out message);
        }

        public static bool HasPackageManagerRoute(PungentUtilityPackageRecord record)
        {
            return record != null && !string.IsNullOrWhiteSpace(record.packageManagerId);
        }

        public static bool HasPackageManagerRoute(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return false;

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            return !string.IsNullOrWhiteSpace(FirstNonEmpty(descriptor.PackageManagerId, record == null ? null : record.packageManagerId));
        }

        public static bool HasValidImportPackagePath(PungentUtilityPackageRecord record)
        {
            return record != null && HasValidImportPackagePath(record.importPackagePath);
        }

        public static bool HasValidImportPackagePath(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return false;

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            string importPackagePath = FirstNonEmpty(descriptor.ImportPackagePath, record == null ? null : record.importPackagePath);
            return HasValidImportPackagePath(importPackagePath);
        }

        public static bool HasValidImportPackagePath(string importPackagePath)
        {
            string resolvedPath = ResolveImportPackagePath(importPackagePath);
            return !string.IsNullOrWhiteSpace(resolvedPath) &&
                   File.Exists(resolvedPath) &&
                   string.Equals(Path.GetExtension(resolvedPath), ".unitypackage", StringComparison.OrdinalIgnoreCase);
        }

        public static bool OpenImportPackage(PungentUtilityPackageRecord record, out string message)
        {
            if (record == null)
            {
                message = "No package was selected.";
                return false;
            }

            NormalizeRecord(record);
            return OpenImportPackage(record.importPackagePath, record.displayName, out message);
        }

        public static bool OpenImportPackage(PungentUtilityDescriptor descriptor, out string message)
        {
            if (descriptor == null)
            {
                message = "No utility was selected.";
                return false;
            }

            PungentUtilityPackageRecord record = FindRecord(descriptor.NormalizedPackageId);
            string importPackagePath = FirstNonEmpty(descriptor.ImportPackagePath, record == null ? null : record.importPackagePath);
            return OpenImportPackage(importPackagePath, descriptor.DisplayName, out message);
        }

        public static bool OpenImportPackage(string importPackagePath, string displayName, out string message)
        {
            string resolvedPath = ResolveImportPackagePath(importPackagePath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                message = "Import route not configured.";
                return false;
            }

            if (!string.Equals(Path.GetExtension(resolvedPath), ".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                message = "Import route must point to a .unitypackage file.";
                return false;
            }

            if (!File.Exists(resolvedPath))
            {
                message = "Import route is configured but the .unitypackage file was not found: " + importPackagePath;
                return false;
            }

            AssetDatabase.ImportPackage(resolvedPath, false);
            message = "Opened Unity's import dialog for " + FirstNonEmpty(displayName, "package") + ".";
            return true;
        }

        public static List<PungentUtilityDescriptor> RegisteredUtilitiesForPackage(string packageId)
        {
            string id = NormalizePackageId(packageId);
            return PungentUtilityRegistry.All
                .Where(utility => utility != null && string.Equals(utility.NormalizedPackageId, id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(utility => utility.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int PackageTierSortKey(PungentUtilityPackageTier tier)
        {
            switch (tier)
            {
                case PungentUtilityPackageTier.Core: return 0;
                case PungentUtilityPackageTier.Internal: return 5;
                case PungentUtilityPackageTier.Extension: return 10;
                case PungentUtilityPackageTier.Bridge: return 20;
                case PungentUtilityPackageTier.ProjectAdapter: return 30;
                case PungentUtilityPackageTier.Bundle: return 40;
                default: return 100;
            }
        }

        public static void NormalizeRecord(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return;

            record.packageId = NormalizePackageId(record.packageId);
            record.displayName = string.IsNullOrWhiteSpace(record.displayName) ? record.packageId : record.displayName.Trim();
            if (!Enum.IsDefined(typeof(PungentUtilityPackageTier), record.tier))
                record.tier = PungentUtilityPackageTier.Extension;
            if (!Enum.IsDefined(typeof(PungentUtilityPackageAvailability), record.availability))
                record.availability = PungentUtilityPackageAvailability.OwnedInstalled;
            record.assetStoreUrl = record.assetStoreUrl ?? string.Empty;
            record.packageManagerId = record.packageManagerId ?? string.Empty;
            record.importPackagePath = record.importPackagePath ?? string.Empty;
            record.description = record.description ?? string.Empty;
            record.includedUtilityIds = CleanArray(record.includedUtilityIds);
            record.includedCapabilities = CleanArray(record.includedCapabilities);
            record.installHint = record.installHint ?? string.Empty;
            record.currencyCode = string.IsNullOrWhiteSpace(record.currencyCode) ? string.Empty : record.currencyCode.Trim().ToUpperInvariant();
            record.regularPriceText = record.regularPriceText ?? string.Empty;
            record.salePriceText = record.salePriceText ?? string.Empty;
            record.discountPercent = Mathf.Clamp(record.discountPercent, 0, 100);
            record.saleEndsIsoUtc = record.saleEndsIsoUtc ?? string.Empty;
            record.promotionalLabel = record.promotionalLabel ?? string.Empty;
            if (!Enum.IsDefined(typeof(PungentUtilityPackageLifecycle), record.lifecycle))
                record.lifecycle = PungentUtilityPackageLifecycle.Visible;
        }

        public static PungentUtilityPackageRecord CloneRecord(PungentUtilityPackageRecord source)
        {
            if (source == null)
                return null;

            PungentUtilityPackageRecord copy = new PungentUtilityPackageRecord
            {
                packageId = source.packageId,
                displayName = source.displayName,
                tier = source.tier,
                availability = source.availability,
                assetStoreUrl = source.assetStoreUrl,
                packageManagerId = source.packageManagerId,
                importPackagePath = source.importPackagePath,
                description = source.description,
                includedUtilityIds = CleanArray(source.includedUtilityIds),
                includedCapabilities = CleanArray(source.includedCapabilities),
                installHint = source.installHint,
                developerOverrideAvailability = source.developerOverrideAvailability,
                showPromotionalPricing = source.showPromotionalPricing,
                currencyCode = source.currencyCode,
                regularPriceText = source.regularPriceText,
                salePriceText = source.salePriceText,
                discountPercent = source.discountPercent,
                saleEndsIsoUtc = source.saleEndsIsoUtc,
                promotionalLabel = source.promotionalLabel,
                lifecycle = source.lifecycle
            };
            NormalizeRecord(copy);
            return copy;
        }

        private static bool OpenInstallFlow(string displayName, string packageId, string packageManagerId, string importPackagePath, string installHint, out string message)
        {
            packageId = NormalizePackageId(packageId);

            if (!string.IsNullOrWhiteSpace(packageManagerId))
            {
                EditorGUIUtility.systemCopyBuffer = packageManagerId.Trim();
                if (TryOpenPackageManager(packageManagerId.Trim(), out string packageManagerMessage))
                {
                    message = packageManagerMessage;
                    return true;
                }

                message = packageManagerMessage;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(importPackagePath))
            {
                string resolvedPath = ResolveImportPackagePath(importPackagePath);
                if (File.Exists(resolvedPath) && string.Equals(Path.GetExtension(resolvedPath), ".unitypackage", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportPackage(resolvedPath, false);
                    message = "Opened Unity's import dialog for " + displayName + ".";
                    return true;
                }

                message = "Import route is configured but the .unitypackage file was not found: " + importPackagePath;
                return false;
            }

            message = "Install route not configured for " + displayName + " (" + packageId + "). " +
                      FirstNonEmpty(installHint, "Configure a Package Manager ID or package-specific .unitypackage path in Developer Mode.");
            return false;
        }

        private static bool TryOpenPackageManager(string packageManagerId, out string message)
        {
            Type windowType = Type.GetType("UnityEditor.PackageManager.UI.Window,UnityEditor");
            if (windowType != null)
            {
                MethodInfo openWithId = windowType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method =>
                    {
                        if (!string.Equals(method.Name, "Open", StringComparison.Ordinal))
                            return false;

                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
                    });

                if (openWithId != null)
                {
                    try
                    {
                        openWithId.Invoke(null, new object[] { packageManagerId });
                        message = "Opened Package Manager for " + packageManagerId + ".";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        message = "Could not select the package directly. Package ID copied: " + packageManagerId + ". " + ex.Message;
                    }
                }

                MethodInfo openDefault = windowType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => string.Equals(method.Name, "Open", StringComparison.Ordinal) && method.GetParameters().Length == 0);
                if (openDefault != null)
                {
                    try
                    {
                        openDefault.Invoke(null, null);
                        message = "Opened Package Manager and copied the package ID: " + packageManagerId;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        message = "Could not open Package Manager through the UI API. Package ID copied: " + packageManagerId + ". " + ex.Message;
                    }
                }
            }

            if (EditorApplication.ExecuteMenuItem("Window/Package Manager"))
            {
                message = "Opened Package Manager and copied the package ID: " + packageManagerId;
                return true;
            }

            message = "Could not open Package Manager automatically. Package ID copied: " + packageManagerId;
            return false;
        }

        private static bool TryOpenUrl(string url, string label, out string message)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                message = label + " link not configured.";
                return false;
            }

            if (!PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(url))
            {
                message = label + " link is not a valid absolute URL.";
                return false;
            }

            Application.OpenURL(url.Trim());
            message = "Opened " + label + ".";
            return true;
        }

        private static string ResolveImportPackagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string trimmed = path.Trim();
            if (Path.IsPathRooted(trimmed))
                return trimmed;

            return Path.GetFullPath(trimmed);
        }

        private static IReadOnlyList<PungentUtilityPackageRecord> GetMergedRecords()
        {
            EnsureMergedRecordCache();
            return _cachedMergedRecords;
        }

        private static void EnsureMergedRecordCache()
        {
            if (_cachedMergedRecords != null && _cachedMergedRecordsById != null && _cachedSortedRecords != null)
                return;

            using (PungentEditorPerformanceUtility.Sample("PFU.PackageCatalog.BuildMergedRecordCache"))
            {
                _cachedMergedRecords = BuildMergedRecords();
                _cachedMergedRecordsById = _cachedMergedRecords.ToDictionary(record => record.packageId, record => record, StringComparer.OrdinalIgnoreCase);
                _cachedSortedRecords = _cachedMergedRecords
                    .OrderBy(record => PackageTierSortKey(record.tier))
                    .ThenBy(record => record.displayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private static List<PungentUtilityPackageRecord> BuildMergedRecords()
        {
            Dictionary<string, PungentUtilityPackageRecord> records = CreateDefaultRecords()
                .ToDictionary(record => record.packageId, CloneRecord, StringComparer.OrdinalIgnoreCase);

            foreach (PungentUtilityPackageRecord record in PungentUtilityPackageCatalogSettings.instance.Records)
            {
                if (record == null)
                    continue;

                PungentUtilityPackageRecord copy = CloneRecord(record);
                records[copy.packageId] = copy;
            }

            return records.Values.ToList();
        }

        private static IEnumerable<PungentUtilityPackageRecord> CreateDefaultRecords()
        {
            yield return Record(CorePackageId, CorePackageDisplayName, PungentUtilityPackageTier.Core, PungentUtilityPackageAvailability.OwnedInstalled,
                "Core provides the launcher, help, themes, shared settings, checklist definitions, and tray surfaces used by the utility suite.",
                new[] { "utilities-browser", "theme-customizer", "help-browser", "utility-tray", "minimize-focused-window", "open-minimized-utilities-tray", "developer-tools", "utility-metadata-editor", "category-reference-membership", "documentation-links", "qa-checklist-utility" },
                new[] { "Utility launcher", "Help browser", "Theme controls", "Tool settings", "Shared checklists" });

            yield return Record("com.pungentfunk.utilities.authoring", "PungentFunk Utilities Documentation and Authoring", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Documentation and Authoring adds notes, rich authoring, and reusable authoring helpers. Documentation Links are surfaced as a Core/accessory utility.",
                new[] { "tooltip-notes", "component-tuning-copy" },
                new[] { "Tooltip notes", "Authoring notes", "Serialized editing" });

            yield return Record("com.pungentfunk.utilities.board", "PungentFunk Utilities Board / Graph / Visualization", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Board, Graph, and Visualization helps plan, map, and inspect relationships through board-style and graph-style views.",
                new[] { "board-editor" },
                new[] { "Board layouts", "Graph views", "Planning views", "Visual maps", "Authoring reference maps" });

            yield return Record("com.pungentfunk.utilities.datasheet", "PungentFunk Utilities Spreadsheet / Data Sheet", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Spreadsheet and Data Sheet adds tabular review, editing, and export workflows for structured project data.",
                new[] { "data-sheet-editor" },
                new[] { "Data tables", "Sheet review", "Structured export", "Batch editing", "CSV import/export", "Authoring references", "Checklist definition sheets" });

            yield return Record("com.pungentfunk.utilities.audit", "PungentFunk Utilities Audit / Validation / Scanning", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Audit, Validation, and Scanning helps inspect project health, references, scenes, coverage, and documentation tokens.",
                new[] { "design-validation-audit", "reference-assignment-scanner", "terrain-usage-scanner", "scene-issue-scanner", "coverage-matrix", "token-validator" },
                new[] { "Reference scans", "Scene health", "Coverage matrix", "Token checks" });

            yield return Record("com.pungentfunk.utilities.scene-authoring", "PungentFunk Utilities Scene Authoring / Placement", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Scene Authoring and Placement adds tools for building, aligning, organizing, navigating, validating, visualizing, and spatially authoring scene content directly inside the Unity Editor.",
                new[] { "asset-placement-lab", "create-placement-asset-set-from-selection", "add-placement-socket", "surface-align", "scene-navigation", "fast-travel-sceneview-to-selection", "scene-gizmo-browser", "add-scene-gizmo-source", "add-scene-beacon", "add-collision-sensor-gizmo", "add-trigger-sensor-gizmo", "add-trajectory-visualizer", "spatial-authoring-workbench", "path-authoring-toolkit", "spatial-authoring-toolkit", "bulk-rename" },
                new[] { "Placement sets", "Surface alignment", "Scene navigation", "Scene visualization", "Gizmo browser", "Spatial paths", "Spatial areas", "Spatial output recipes", "Open-scene validation" });

            yield return Record("com.pungentfunk.utilities.scene-gizmos", "PungentFunk Utilities Scene Gizmos (Legacy Alias)", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Legacy package alias retained for compatibility. Scene Gizmo utilities now belong to Scene Authoring metadata and keep their existing utility IDs and menu paths.",
                Array.Empty<string>(),
                new[] { "Legacy alias", "Scene visualization now owned by Scene Authoring" });

            yield return Record("com.pungentfunk.utilities.map", "PungentFunk Utilities Map", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedNotInstalled,
                "Map Utilities is reserved for generic map data, projection, layers, markers, regions, bake sources, and runtime map views.",
                Array.Empty<string>(),
                new[] { "Map data", "Map projection", "Markers", "Regions", "Runtime map" });

            yield return Record("com.pungentfunk.utilities.spatial-query", "PungentFunk Utilities Spatial Query", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedNotInstalled,
                "Spatial Query is reserved for generic query profiles, ray and overlap sensors, line of sight, ground probes, and interaction contracts.",
                Array.Empty<string>(),
                new[] { "Spatial queries", "Interaction sensors", "Raycasts", "Overlaps", "Line of sight" });

            yield return Record("com.pungentfunk.utilities.asset-production", "PungentFunk Utilities Asset Production", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Asset Production adds preview, export, icon, and texture baking helpers for reusable project assets.",
                new[] { "font-preview", "prefab-icon-generator", "prefab-asset-exporter", "save-selected-prefab-with-mesh-assets", "texture-array-baker" },
                new[] { "Prefab icons", "Prefab export", "Font previews", "Texture array baking" });

            yield return Record("com.pungentfunk.utilities.appearance", "PungentFunk Utilities Appearance / Colour / Texture", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Appearance, Colour, and Texture adds palette design, procedural texture creation, and editor style inspection tools.",
                new[] { "palette-designer", "procedural-texture-lab", "editor-style-explorer" },
                new[] { "Palette design", "Procedural textures", "Editor styles", "Visual diagnostics" });

            yield return Record("com.pungentfunk.utilities.audio", "PungentFunk Utilities Audio Authoring", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Audio Authoring adds coverage checks for reusable audio setup and catalog workflows.",
                new[] { "audio-setup-coverage", "audio-catalog-coverage" },
                new[] { "Setup coverage", "Catalog coverage", "Cue checks" });

            yield return Record("com.pungentfunk.utilities.debug", "PungentFunk Utilities Debug / Diagnostics", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Debug and Diagnostics adds editor controls for inspecting project state and troubleshooting signals.",
                new[] { "debug-control" },
                new[] { "Debug controls", "Diagnostics", "Troubleshooting tools" });

            yield return Record("com.pungentfunk.utilities.ui-input", "PungentFunk Utilities UI / Input Feedback", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "UI and Input Feedback adds helpers for maintaining input prompt icon libraries and related UI assets.",
                new[] { "input-prompt-icon-library", "populate-selected-input-prompt-library", "populate-default-input-prompt-library" },
                new[] { "Input prompt icons", "Library population", "UI asset helpers" });

            yield return Record("com.pungentfunk.utilities.content-generation", "PungentFunk Utilities Content Generation", PungentUtilityPackageTier.Extension, PungentUtilityPackageAvailability.OwnedInstalled,
                "Content Generation adds reusable helpers for naming and generating lightweight project content.",
                new[] { "name-generator" },
                new[] { "Name generation", "Content helpers" });

            yield return Record(FullBundlePackageId, "PungentFunk Utilities Full Bundle", PungentUtilityPackageTier.Bundle, PungentUtilityPackageAvailability.OwnedInstalled,
                "Full Bundle brings the utility suite together for teams that want the complete editor workflow collection.",
                DefaultUtilityPackages.Keys.ToArray(),
                new[] { "Complete suite", "Suite coordination", "Bundle distribution" });
        }

        private static PungentUtilityPackageRecord Record(string packageId, string displayName, PungentUtilityPackageTier tier, PungentUtilityPackageAvailability availability, string description, string[] utilityIds, string[] capabilities)
        {
            PungentUtilityPackageRecord record = new PungentUtilityPackageRecord
            {
                packageId = packageId,
                displayName = displayName,
                tier = tier,
                availability = availability,
                description = description,
                includedUtilityIds = utilityIds ?? new string[0],
                includedCapabilities = capabilities ?? new string[0],
                installHint = "Configure an install route in Developer Mode if this package is distributed separately."
            };
            NormalizeRecord(record);
            return record;
        }

        private static string[] CleanPackageIds(IEnumerable<string> values)
        {
            if (values == null)
                return new string[0];

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizePackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            }

            return string.Empty;
        }
    }
#endif
}
