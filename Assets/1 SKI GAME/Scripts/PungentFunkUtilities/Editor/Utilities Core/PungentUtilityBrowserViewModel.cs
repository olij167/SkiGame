namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    [Flags]
    internal enum BrowserDirtyFlags
    {
        None = 0,
        Registry = 1 << 0,
        Query = 1 << 1,
        Filters = 1 << 2,
        Sort = 1 << 3,
        PackageState = 1 << 4,
        DeveloperMode = 1 << 5,
        Layout = 1 << 6,
        All = ~0
    }

    internal enum PungentUtilityBrowserSortMode
    {
        Recommended,
        BrowserPriority,
        PinnedFirst,
        RecentlyUsed,
        Alphabetical,
        Category,
        Package,
        Status
    }

    internal sealed class PungentUtilityBrowserViewModel
    {
        internal sealed class Item
        {
            public PungentUtilityDescriptor Descriptor;
            public string Id;
            public string SearchText;
            public string Category;
            public string Type;
            public string Status;
            public string PackageId;
            public string PackageLabel;
            public string AvailabilityLabel;
            public string ShortAvailabilityLabel;
            public Color AvailabilityTint;
            public PungentUtilityPackageAvailability Availability;
            public PungentUtilityBrowserRole Role;
            public PungentUtilityBrowserProminence Prominence;
            public string ParentUtilityId;
            public int BrowserPriority;
            public readonly List<PungentUtilityDescriptor> Actions = new List<PungentUtilityDescriptor>();
            public readonly List<PungentUtilityDescriptor> Accessories = new List<PungentUtilityDescriptor>();
            public readonly List<PungentUtilityDescriptor> Related = new List<PungentUtilityDescriptor>();
        }

        internal sealed class CategoryGroup
        {
            public string Category;
            public int Count;
            public readonly List<TypeGroup> Types = new List<TypeGroup>();
        }

        internal sealed class TypeGroup
        {
            public string Name;
            public readonly List<PungentUtilityDescriptor> Utilities = new List<PungentUtilityDescriptor>();
        }

        private readonly List<Item> _allItems = new List<Item>();
        private readonly List<Item> _visibleItems = new List<Item>();
        private readonly List<PungentUtilityDescriptor> _visibleDescriptors = new List<PungentUtilityDescriptor>();
        private readonly List<CategoryGroup> _categoryGroups = new List<CategoryGroup>();
        private readonly Dictionary<string, Item> _itemsById = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<PungentUtilityDescriptor>> _actionsByParent = new Dictionary<string, List<PungentUtilityDescriptor>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<PungentUtilityDescriptor>> _accessoriesByParent = new Dictionary<string, List<PungentUtilityDescriptor>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _roleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _packageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private BrowserDirtyFlags _dirty = BrowserDirtyFlags.All;

        public IReadOnlyList<Item> VisibleItems => _visibleItems;
        public IReadOnlyList<PungentUtilityDescriptor> VisibleDescriptors => _visibleDescriptors;
        public IReadOnlyList<CategoryGroup> CategoryGroups => _categoryGroups;
        public IReadOnlyDictionary<string, int> CategoryCounts => _categoryCounts;
        public IReadOnlyDictionary<string, int> RoleCounts => _roleCounts;
        public IReadOnlyDictionary<string, int> PackageCounts => _packageCounts;

        public void MarkDirty(BrowserDirtyFlags flags)
        {
            _dirty |= flags == BrowserDirtyFlags.None ? BrowserDirtyFlags.Query : flags;
        }

        public void RefreshIfNeeded(
            IEnumerable<PungentUtilityDescriptor> source,
            string search,
            string category,
            string type,
            string status,
            string focusedPackageId,
            Func<PungentUtilityDescriptor, bool> visibilityFilter,
            Func<PungentUtilityDescriptor, bool> developerFilter,
            Func<PungentUtilityDescriptor, bool> packageFilter,
            Func<PungentUtilityDescriptor, bool> itemKindFilter,
            bool includeAccessories,
            bool showNestedActions,
            bool includeDeveloperHidden,
            PungentUtilityBrowserSortMode sortMode,
            ISet<string> favorites,
            IList<string> recents)
        {
            if (_dirty == BrowserDirtyFlags.None)
                return;

            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.CacheRefresh"))
            {
                RebuildAll(
                    source,
                    search,
                    category,
                    type,
                    status,
                    focusedPackageId,
                    visibilityFilter,
                    developerFilter,
                    packageFilter,
                    itemKindFilter,
                    includeAccessories,
                    showNestedActions,
                    includeDeveloperHidden,
                    sortMode,
                    favorites,
                    recents);
                _dirty = BrowserDirtyFlags.None;
            }
        }

        public Item GetItem(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return null;

            return _itemsById.TryGetValue(utilityId, out Item item) ? item : null;
        }

        public IReadOnlyList<PungentUtilityDescriptor> GetActions(string utilityId)
        {
            return !string.IsNullOrWhiteSpace(utilityId) && _actionsByParent.TryGetValue(utilityId, out List<PungentUtilityDescriptor> list)
                ? list
                : (IReadOnlyList<PungentUtilityDescriptor>)Array.Empty<PungentUtilityDescriptor>();
        }

        public IReadOnlyList<PungentUtilityDescriptor> GetAccessories(string utilityId)
        {
            return !string.IsNullOrWhiteSpace(utilityId) && _accessoriesByParent.TryGetValue(utilityId, out List<PungentUtilityDescriptor> list)
                ? list
                : (IReadOnlyList<PungentUtilityDescriptor>)Array.Empty<PungentUtilityDescriptor>();
        }

        private void RebuildAll(
            IEnumerable<PungentUtilityDescriptor> source,
            string search,
            string category,
            string type,
            string status,
            string focusedPackageId,
            Func<PungentUtilityDescriptor, bool> visibilityFilter,
            Func<PungentUtilityDescriptor, bool> developerFilter,
            Func<PungentUtilityDescriptor, bool> packageFilter,
            Func<PungentUtilityDescriptor, bool> itemKindFilter,
            bool includeAccessories,
            bool showNestedActions,
            bool includeDeveloperHidden,
            PungentUtilityBrowserSortMode sortMode,
            ISet<string> favorites,
            IList<string> recents)
        {
            _allItems.Clear();
            _visibleItems.Clear();
            _visibleDescriptors.Clear();
            _categoryGroups.Clear();
            _itemsById.Clear();
            _actionsByParent.Clear();
            _accessoriesByParent.Clear();
            _categoryCounts.Clear();
            _roleCounts.Clear();
            _packageCounts.Clear();

            string q = (search ?? string.Empty).Trim();
            string categoryFilter = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : category;
            string typeFilter = string.Equals(type, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : type;
            string statusFilter = string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : status;
            string packageFilterId = PungentUtilityPackageCatalog.NormalizePackageId(focusedPackageId);
            bool hasPackageFocus = !string.IsNullOrWhiteSpace(focusedPackageId);
            bool hasSearch = !string.IsNullOrWhiteSpace(q);

            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.PackageStateResolution"))
            {
                foreach (PungentUtilityDescriptor descriptor in source ?? Enumerable.Empty<PungentUtilityDescriptor>())
                {
                    if (descriptor == null)
                        continue;

                    Item item = BuildItem(descriptor);
                    _allItems.Add(item);
                    _itemsById[item.Id] = item;
                    Increment(_categoryCounts, item.Category);
                    Increment(_roleCounts, item.Role.ToString());
                    Increment(_packageCounts, item.PackageId);
                }
            }

            BuildRelationshipMaps();

            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.QueryRebuild"))
            {
                foreach (Item item in _allItems)
                {
                    PungentUtilityDescriptor descriptor = item.Descriptor;
                    if (!PassesCoreSurface(item, hasSearch, includeAccessories, showNestedActions, includeDeveloperHidden))
                        continue;
                    if (!string.IsNullOrEmpty(categoryFilter) && !descriptor.HasCategory(categoryFilter))
                        continue;
                    if (!string.IsNullOrEmpty(typeFilter) && !string.Equals(PungentUtilityRegistry.GetUtilityType(descriptor), typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(statusFilter) && !string.Equals(PungentUtilityRegistry.GetStatus(descriptor), statusFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (hasPackageFocus && !string.Equals(item.PackageId, packageFilterId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!hasSearch && item.Prominence == PungentUtilityBrowserProminence.HiddenUnlessSearched)
                        continue;
                    if (hasSearch && item.SearchText.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (visibilityFilter != null && !visibilityFilter(descriptor))
                        continue;
                    if (developerFilter != null && !developerFilter(descriptor))
                        continue;
                    if (packageFilter != null && !packageFilter(descriptor))
                        continue;
                    if (itemKindFilter != null && !itemKindFilter(descriptor))
                        continue;

                    _visibleItems.Add(item);
                }
            }

            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.SortGroupRebuild"))
            {
                _visibleItems.Sort((a, b) => CompareItems(a, b, sortMode, favorites, recents));
                _visibleDescriptors.AddRange(_visibleItems.Select(item => item.Descriptor));
                BuildGroups();
            }
        }

        private static Item BuildItem(PungentUtilityDescriptor descriptor)
        {
            PungentUtilityPackageAvailability availability = PungentUtilityPackageCatalog.ResolveAvailability(descriptor);
            string category = PungentUtilityRegistry.GetAreaCategory(descriptor);
            string type = PungentUtilityRegistry.GetUtilityType(descriptor);
            string status = PungentUtilityRegistry.GetStatus(descriptor);
            string packageId = descriptor.NormalizedPackageId;
            return new Item
            {
                Descriptor = descriptor,
                Id = descriptor.Id,
                Category = category,
                Type = type,
                Status = status,
                PackageId = packageId,
                PackageLabel = descriptor.EffectivePackageDisplayName,
                Availability = availability,
                AvailabilityLabel = PungentUtilityPackageCatalog.BuildAvailabilityLabel(availability),
                ShortAvailabilityLabel = PungentUtilityPackageCatalog.BuildShortAvailabilityLabel(availability),
                AvailabilityTint = PungentUtilityPackageCatalog.GetAvailabilityTint(availability),
                Role = descriptor.BrowserRole,
                Prominence = descriptor.BrowserProminence,
                ParentUtilityId = descriptor.ParentUtilityId ?? string.Empty,
                BrowserPriority = descriptor.BrowserPriority,
                SearchText = BuildSearchText(descriptor, category, type, status, packageId)
            };
        }

        private void BuildRelationshipMaps()
        {
            foreach (Item item in _allItems)
            {
                PungentUtilityDescriptor descriptor = item.Descriptor;
                if (descriptor == null)
                    continue;

                if (item.Role == PungentUtilityBrowserRole.Action)
                    AddRelationship(_actionsByParent, FirstNonEmpty(item.ParentUtilityId, FirstRelatedCoreId(descriptor)), descriptor);
                else if (item.Role == PungentUtilityBrowserRole.AccessoryUtility)
                    AddRelationship(_accessoriesByParent, FirstNonEmpty(item.ParentUtilityId, FirstRelatedCoreId(descriptor)), descriptor);
            }

            foreach (Item item in _allItems)
            {
                PungentUtilityDescriptor descriptor = item.Descriptor;
                if (descriptor == null)
                    continue;

                AddExplicitChildren(item.Actions, descriptor.ActionIds);
                AddExplicitChildren(item.Accessories, descriptor.AccessoryUtilityIds);
                AddRelated(item.Related, descriptor.RelatedUtilityIds, item.Id);

                if (_actionsByParent.TryGetValue(item.Id, out List<PungentUtilityDescriptor> actions))
                    item.Actions.AddRange(actions.Where(action => item.Actions.All(existing => !SameId(existing, action))));
                if (_accessoriesByParent.TryGetValue(item.Id, out List<PungentUtilityDescriptor> accessories))
                    item.Accessories.AddRange(accessories.Where(accessory => item.Accessories.All(existing => !SameId(existing, accessory))));

                item.Actions.Sort(CompareChildDescriptor);
                item.Accessories.Sort(CompareChildDescriptor);
                item.Related.Sort(CompareChildDescriptor);
            }
        }

        private void AddExplicitChildren(List<PungentUtilityDescriptor> target, string[] ids)
        {
            if (target == null || ids == null)
                return;

            for (int i = 0; i < ids.Length; i++)
            {
                if (_itemsById.TryGetValue(ids[i], out Item item) && item.Descriptor != null && target.All(existing => !SameId(existing, item.Descriptor)))
                    target.Add(item.Descriptor);
            }
        }

        private void AddRelated(List<PungentUtilityDescriptor> target, string[] ids, string currentId)
        {
            if (target == null || ids == null)
                return;

            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], currentId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_itemsById.TryGetValue(ids[i], out Item item) && item.Descriptor != null && item.Role != PungentUtilityBrowserRole.Action && target.All(existing => !SameId(existing, item.Descriptor)))
                    target.Add(item.Descriptor);
            }
        }

        private string FirstRelatedCoreId(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || descriptor.RelatedUtilityIds == null)
                return string.Empty;

            for (int i = 0; i < descriptor.RelatedUtilityIds.Length; i++)
            {
                if (_itemsById.TryGetValue(descriptor.RelatedUtilityIds[i], out Item item) && item.Role == PungentUtilityBrowserRole.CoreUtility)
                    return item.Id;
            }

            return string.Empty;
        }

        private static void AddRelationship(Dictionary<string, List<PungentUtilityDescriptor>> map, string parentId, PungentUtilityDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(parentId) || descriptor == null)
                return;

            if (!map.TryGetValue(parentId, out List<PungentUtilityDescriptor> list))
            {
                list = new List<PungentUtilityDescriptor>();
                map[parentId] = list;
            }

            if (list.All(existing => !SameId(existing, descriptor)))
                list.Add(descriptor);
        }

        private static bool PassesCoreSurface(Item item, bool hasSearch, bool includeAccessories, bool showNestedActions, bool includeDeveloperHidden)
        {
            if (item == null)
                return false;

            switch (item.Role)
            {
                case PungentUtilityBrowserRole.Action:
                    return showNestedActions || hasSearch;
                case PungentUtilityBrowserRole.AccessoryUtility:
                    return includeAccessories || hasSearch;
                case PungentUtilityBrowserRole.Internal:
                case PungentUtilityBrowserRole.LegacyAlias:
                    return includeDeveloperHidden || hasSearch;
                default:
                    return true;
            }
        }

        private static string BuildSearchText(PungentUtilityDescriptor descriptor, string category, string type, string status, string packageId)
        {
            return string.Join(" ", new[]
            {
                descriptor.Id,
                descriptor.DisplayName,
                descriptor.Description,
                descriptor.Module,
                category,
                type,
                status,
                packageId,
                descriptor.EffectivePackageDisplayName,
                descriptor.ParentUtilityId,
                descriptor.BrowserRole.ToString(),
                descriptor.BrowserProminence.ToString(),
                Join(descriptor.Tags),
                Join(descriptor.BrowserTags),
                Join(descriptor.CategoryFacets),
                Join(descriptor.RelatedUtilityIds),
                Join(descriptor.ActionIds),
                Join(descriptor.AccessoryUtilityIds)
            });
        }

        private void BuildGroups()
        {
            foreach (IGrouping<string, Item> categoryGroup in _visibleItems
                         .GroupBy(item => item.Category)
                         .OrderBy(g => PungentUtilityCategories.SortKey(g.Key))
                         .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                CategoryGroup categoryView = new CategoryGroup
                {
                    Category = categoryGroup.Key,
                    Count = categoryGroup.Count()
                };

                foreach (IGrouping<string, Item> typeGroup in categoryGroup
                             .GroupBy(item => item.Type)
                             .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    TypeGroup typeView = new TypeGroup
                    {
                        Name = typeGroup.Key
                    };
                    typeView.Utilities.AddRange(typeGroup.Select(item => item.Descriptor));
                    categoryView.Types.Add(typeView);
                }

                _categoryGroups.Add(categoryView);
            }
        }

        private static int CompareItems(Item a, Item b, PungentUtilityBrowserSortMode sortMode, ISet<string> favorites, IList<string> recents)
        {
            switch (sortMode)
            {
                case PungentUtilityBrowserSortMode.PinnedFirst:
                    return ComparePinnedThenRecommended(a, b, favorites);
                case PungentUtilityBrowserSortMode.RecentlyUsed:
                    return CompareRecentThenRecommended(a, b, recents);
                case PungentUtilityBrowserSortMode.Alphabetical:
                    return string.Compare(a.Descriptor.DisplayName, b.Descriptor.DisplayName, StringComparison.OrdinalIgnoreCase);
                case PungentUtilityBrowserSortMode.Category:
                    return CompareCategoryThenRecommended(a, b);
                case PungentUtilityBrowserSortMode.Package:
                    return ComparePackageThenRecommended(a, b);
                case PungentUtilityBrowserSortMode.Status:
                    return CompareStatusThenRecommended(a, b);
                case PungentUtilityBrowserSortMode.BrowserPriority:
                case PungentUtilityBrowserSortMode.Recommended:
                default:
                    return CompareRecommended(a, b, favorites, recents);
            }
        }

        private static int CompareRecommended(Item a, Item b, ISet<string> favorites, IList<string> recents)
        {
            int priority = a.BrowserPriority.CompareTo(b.BrowserPriority);
            if (priority != 0)
                return priority;

            int pinned = IsFavorite(b, favorites).CompareTo(IsFavorite(a, favorites));
            if (pinned != 0)
                return pinned;

            int recent = GetRecentIndex(a, recents).CompareTo(GetRecentIndex(b, recents));
            if (recent != 0)
                return recent;

            return CompareCategoryThenRecommended(a, b);
        }

        private static int CompareCategoryThenRecommended(Item a, Item b)
        {
            int category = PungentUtilityCategories.SortKey(a.Category).CompareTo(PungentUtilityCategories.SortKey(b.Category));
            if (category != 0)
                return category;
            int categoryName = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            if (categoryName != 0)
                return categoryName;
            int type = string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
            if (type != 0)
                return type;
            int order = a.BrowserPriority.CompareTo(b.BrowserPriority);
            if (order != 0)
                return order;
            return string.Compare(a.Descriptor.DisplayName, b.Descriptor.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static int ComparePinnedThenRecommended(Item a, Item b, ISet<string> favorites)
        {
            int pinned = IsFavorite(b, favorites).CompareTo(IsFavorite(a, favorites));
            return pinned != 0 ? pinned : CompareRecommended(a, b, favorites, null);
        }

        private static int CompareRecentThenRecommended(Item a, Item b, IList<string> recents)
        {
            int recent = GetRecentIndex(a, recents).CompareTo(GetRecentIndex(b, recents));
            return recent != 0 ? recent : CompareRecommended(a, b, null, recents);
        }

        private static int ComparePackageThenRecommended(Item a, Item b)
        {
            int package = string.Compare(a.PackageLabel, b.PackageLabel, StringComparison.OrdinalIgnoreCase);
            return package != 0 ? package : CompareCategoryThenRecommended(a, b);
        }

        private static int CompareStatusThenRecommended(Item a, Item b)
        {
            int status = PungentUtilityPackageStatus.SortKey(a.Status).CompareTo(PungentUtilityPackageStatus.SortKey(b.Status));
            return status != 0 ? status : CompareCategoryThenRecommended(a, b);
        }

        private static int CompareChildDescriptor(PungentUtilityDescriptor a, PungentUtilityDescriptor b)
        {
            int order = a.BrowserPriority.CompareTo(b.BrowserPriority);
            if (order != 0)
                return order;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFavorite(Item item, ISet<string> favorites)
        {
            return item != null && favorites != null && favorites.Contains(item.Id);
        }

        private static int GetRecentIndex(Item item, IList<string> recents)
        {
            if (item == null || recents == null)
                return int.MaxValue;

            for (int i = 0; i < recents.Count; i++)
            {
                if (string.Equals(recents[i], item.Id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return int.MaxValue;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                key = "Other";

            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static string Join(IEnumerable<string> values)
        {
            return values == null ? string.Empty : string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
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

        private static bool SameId(PungentUtilityDescriptor a, PungentUtilityDescriptor b)
        {
            return a != null && b != null && string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
#endif
}
