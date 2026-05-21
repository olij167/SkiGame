namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Core;

    public enum PungentUtilityHelpSelectionKind
    {
        Category,
        Subcategory,
        Utility,
        Topic,
        SearchResults,
        MissingTopic
    }

    [Serializable]
    public sealed class PungentUtilityHelpNavigationSelection
    {
        public PungentUtilityHelpSelectionKind kind = PungentUtilityHelpSelectionKind.SearchResults;
        public string categoryId = string.Empty;
        public string subcategoryId = string.Empty;
        public string utilityId = string.Empty;
        public string topicStableId = string.Empty;
        public string missingSectionId = string.Empty;
        public string missingTopicId = string.Empty;

        public static PungentUtilityHelpNavigationSelection SearchResults()
        {
            return new PungentUtilityHelpNavigationSelection { kind = PungentUtilityHelpSelectionKind.SearchResults };
        }

        public static PungentUtilityHelpNavigationSelection Category(string categoryId)
        {
            return new PungentUtilityHelpNavigationSelection
            {
                kind = PungentUtilityHelpSelectionKind.Category,
                categoryId = PungentUtilityHelpIds.Normalize(categoryId, PungentUtilityCategories.Other)
            };
        }

        public static PungentUtilityHelpNavigationSelection Subcategory(string categoryId, string subcategoryId)
        {
            return new PungentUtilityHelpNavigationSelection
            {
                kind = PungentUtilityHelpSelectionKind.Subcategory,
                categoryId = PungentUtilityHelpIds.Normalize(categoryId, PungentUtilityCategories.Other),
                subcategoryId = PungentUtilityHelpIds.Normalize(subcategoryId, "documentation")
            };
        }

        public static PungentUtilityHelpNavigationSelection Utility(string categoryId, string subcategoryId, string utilityId)
        {
            return new PungentUtilityHelpNavigationSelection
            {
                kind = PungentUtilityHelpSelectionKind.Utility,
                categoryId = PungentUtilityHelpIds.Normalize(categoryId, PungentUtilityCategories.Other),
                subcategoryId = PungentUtilityHelpIds.Normalize(subcategoryId, "documentation"),
                utilityId = PungentUtilityHelpIds.Normalize(utilityId)
            };
        }

        public static PungentUtilityHelpNavigationSelection Topic(PungentUtilityHelpTopic topic, PungentUtilityHelpUtilityNode utilityNode = null)
        {
            if (topic == null)
                return SearchResults();

            return new PungentUtilityHelpNavigationSelection
            {
                kind = PungentUtilityHelpSelectionKind.Topic,
                categoryId = utilityNode == null ? string.Empty : utilityNode.categoryId,
                subcategoryId = utilityNode == null ? string.Empty : utilityNode.subcategoryId,
                utilityId = topic.utilityId,
                topicStableId = topic.StableId
            };
        }

        public static PungentUtilityHelpNavigationSelection MissingTopic(string utilityId, string sectionId, string topicId)
        {
            return new PungentUtilityHelpNavigationSelection
            {
                kind = PungentUtilityHelpSelectionKind.MissingTopic,
                utilityId = PungentUtilityHelpIds.Normalize(utilityId),
                missingSectionId = PungentUtilityHelpIds.Normalize(sectionId, PungentUtilityHelpIds.DefaultSection),
                missingTopicId = PungentUtilityHelpIds.Normalize(topicId, PungentUtilityHelpIds.DefaultTopic)
            };
        }

        public string Encode()
        {
            return string.Join("|", new[]
            {
                kind.ToString(),
                categoryId ?? string.Empty,
                subcategoryId ?? string.Empty,
                utilityId ?? string.Empty,
                topicStableId ?? string.Empty,
                missingSectionId ?? string.Empty,
                missingTopicId ?? string.Empty
            });
        }

        public static PungentUtilityHelpNavigationSelection Decode(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded))
                return SearchResults();

            string[] parts = encoded.Split('|');
            PungentUtilityHelpNavigationSelection selection = new PungentUtilityHelpNavigationSelection();
            if (parts.Length > 0 && Enum.TryParse(parts[0], true, out PungentUtilityHelpSelectionKind kind))
                selection.kind = kind;
            if (parts.Length > 1)
                selection.categoryId = parts[1];
            if (parts.Length > 2)
                selection.subcategoryId = parts[2];
            if (parts.Length > 3)
                selection.utilityId = parts[3];
            if (parts.Length > 4)
                selection.topicStableId = parts[4];
            if (parts.Length > 5)
                selection.missingSectionId = parts[5];
            if (parts.Length > 6)
                selection.missingTopicId = parts[6];
            return selection;
        }

        public bool SameTarget(PungentUtilityHelpNavigationSelection other)
        {
            if (other == null || kind != other.kind)
                return false;

            return string.Equals(categoryId, other.categoryId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(subcategoryId, other.subcategoryId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(utilityId, other.utilityId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(topicStableId, other.topicStableId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(missingSectionId, other.missingSectionId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(missingTopicId, other.missingTopicId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class PungentUtilityHelpNavigationModel
    {
        public readonly List<PungentUtilityHelpCategoryNode> categories = new List<PungentUtilityHelpCategoryNode>();
        public readonly List<PungentUtilityHelpTopic> searchTopics = new List<PungentUtilityHelpTopic>();
        public readonly List<PungentUtilityDescriptor> searchUtilities = new List<PungentUtilityDescriptor>();
        public string searchText = string.Empty;

        public static PungentUtilityHelpNavigationModel Build(string searchText, bool includeDeveloperOnly, bool includeHidden, bool includeGenerated)
        {
            PungentUtilityHelpNavigationModel model = new PungentUtilityHelpNavigationModel { searchText = searchText ?? string.Empty };
            string query = model.searchText.Trim();

            List<PungentUtilityHelpTopic> visibleTopics = PungentUtilityHelpRegistry.Search(string.Empty, includeDeveloperOnly, includeHidden, includeGenerated);
            HashSet<string> matchingTopicIds = new HashSet<string>(PungentUtilityHelpRegistry.Search(query, includeDeveloperOnly, includeHidden, includeGenerated).Select(t => t.StableId), StringComparer.OrdinalIgnoreCase);

            List<PungentUtilityDescriptor> descriptors = PungentUtilityRegistry.Query(includeHidden: includeDeveloperOnly || includeHidden)
                .Where(d => ShouldIncludeDescriptor(d, includeDeveloperOnly, includeHidden))
                .OrderBy(d => PungentUtilityRegistry.GetAreaCategory(d), StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => PungentUtilityRegistry.GetModule(d), StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, PungentUtilityDescriptor> descriptorsById = descriptors
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id))
                .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (PungentUtilityDescriptor descriptor in descriptors)
            {
                if (!ShouldShowDescriptorForSearch(descriptor, query, visibleTopics, matchingTopicIds))
                    continue;

                List<PungentUtilityHelpTopic> descriptorTopics = visibleTopics
                    .Where(t => string.Equals(t.utilityId, descriptor.Id, StringComparison.OrdinalIgnoreCase))
                    .Where(t => string.IsNullOrWhiteSpace(query) || matchingTopicIds.Contains(t.StableId) || DescriptorMatches(descriptor, query))
                    .OrderBy(t => t.sectionId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.topicId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (PungentUtilityHelpUtilityNode utilityNode in model.GetOrCreateUtilities(descriptor))
                    utilityNode.topics.AddRange(descriptorTopics.Where(t => !utilityNode.topics.Any(existing => string.Equals(existing.StableId, t.StableId, StringComparison.OrdinalIgnoreCase))));
            }

            foreach (PungentUtilityHelpTopic topic in visibleTopics)
            {
                if (topic == null || descriptorsById.ContainsKey(topic.utilityId))
                    continue;
                if (!includeDeveloperOnly && !includeHidden)
                    continue;
                if (!string.IsNullOrWhiteSpace(query) && !matchingTopicIds.Contains(topic.StableId))
                    continue;

                PungentUtilityHelpUtilityNode draft = model.GetOrCreateDraftUtility(topic.utilityId);
                draft.topics.Add(topic);
            }

            model.searchTopics.AddRange(visibleTopics.Where(t => string.IsNullOrWhiteSpace(query) || matchingTopicIds.Contains(t.StableId)));
            model.searchUtilities.AddRange(descriptors.Where(d => DescriptorMatches(d, query)));
            model.PruneEmpty(query);
            return model;
        }

        public PungentUtilityHelpCategoryNode FindCategory(string categoryId)
        {
            return categories.FirstOrDefault(c => string.Equals(c.id, categoryId, StringComparison.OrdinalIgnoreCase));
        }

        public PungentUtilityHelpSubcategoryNode FindSubcategory(string categoryId, string subcategoryId)
        {
            PungentUtilityHelpCategoryNode category = FindCategory(categoryId);
            return category == null ? null : category.subcategories.FirstOrDefault(s => string.Equals(s.id, subcategoryId, StringComparison.OrdinalIgnoreCase));
        }

        public PungentUtilityHelpUtilityNode FindUtility(string utilityId)
        {
            foreach (PungentUtilityHelpCategoryNode category in categories)
            foreach (PungentUtilityHelpSubcategoryNode subcategory in category.subcategories)
            {
                PungentUtilityHelpUtilityNode utility = subcategory.utilities.FirstOrDefault(u => string.Equals(u.id, utilityId, StringComparison.OrdinalIgnoreCase));
                if (utility != null)
                    return utility;
            }

            return null;
        }

        public PungentUtilityHelpUtilityNode FindUtilityForTopic(PungentUtilityHelpTopic topic)
        {
            return topic == null ? null : FindUtility(topic.utilityId);
        }

        private List<PungentUtilityHelpUtilityNode> GetOrCreateUtilities(PungentUtilityDescriptor descriptor)
        {
            List<PungentUtilityHelpUtilityNode> utilities = new List<PungentUtilityHelpUtilityNode>();
            string[] categories = descriptor.GetAllCategoryIds();
            if (categories == null || categories.Length == 0)
                categories = new[] { PungentUtilityRegistry.GetAreaCategory(descriptor) };

            foreach (string category in categories)
                utilities.Add(GetOrCreateUtility(descriptor, PungentUtilityCategories.Normalize(category)));

            return utilities;
        }

        private PungentUtilityHelpUtilityNode GetOrCreateUtility(PungentUtilityDescriptor descriptor, string categoryId)
        {
            categoryId = PungentUtilityCategories.Normalize(categoryId);
            string subcategoryId = NormalizeSubcategoryId(GetSubcategoryLabel(descriptor));
            PungentUtilityHelpSubcategoryNode subcategory = GetOrCreateSubcategory(categoryId, PungentUtilityCategories.GetDisplayName(categoryId), subcategoryId, GetSubcategoryLabel(descriptor));
            PungentUtilityHelpUtilityNode utility = subcategory.utilities.FirstOrDefault(u => string.Equals(u.id, descriptor.Id, StringComparison.OrdinalIgnoreCase));
            if (utility == null)
            {
                utility = new PungentUtilityHelpUtilityNode
                {
                    id = descriptor.Id,
                    displayName = descriptor.DisplayName,
                    summary = descriptor.Description,
                    categoryId = categoryId,
                    subcategoryId = subcategoryId,
                    descriptor = descriptor
                };
                subcategory.utilities.Add(utility);
            }

            return utility;
        }

        private PungentUtilityHelpUtilityNode GetOrCreateDraftUtility(string utilityId)
        {
            PungentUtilityHelpSubcategoryNode subcategory = GetOrCreateSubcategory("unregistered-draft-topics", "Unregistered", "draft-topics", "Draft Topics");
            string id = PungentUtilityHelpIds.Normalize(utilityId, "draft-topic");
            PungentUtilityHelpUtilityNode utility = subcategory.utilities.FirstOrDefault(u => string.Equals(u.id, id, StringComparison.OrdinalIgnoreCase));
            if (utility == null)
            {
                utility = new PungentUtilityHelpUtilityNode
                {
                    id = id,
                    displayName = string.IsNullOrWhiteSpace(utilityId) ? "Draft Topics" : utilityId,
                    summary = "Help topics that are not attached to a registered utility.",
                    categoryId = subcategory.categoryId,
                    subcategoryId = subcategory.id
                };
                subcategory.utilities.Add(utility);
            }

            return utility;
        }

        private PungentUtilityHelpSubcategoryNode GetOrCreateSubcategory(string categoryId, string categoryLabel, string subcategoryId, string subcategoryLabel)
        {
            PungentUtilityHelpCategoryNode category = categories.FirstOrDefault(c => string.Equals(c.id, categoryId, StringComparison.OrdinalIgnoreCase));
            if (category == null)
            {
                category = new PungentUtilityHelpCategoryNode
                {
                    id = categoryId,
                    displayName = string.IsNullOrWhiteSpace(categoryLabel) ? categoryId : categoryLabel
                };
                categories.Add(category);
            }

            PungentUtilityHelpSubcategoryNode subcategory = category.subcategories.FirstOrDefault(s => string.Equals(s.id, subcategoryId, StringComparison.OrdinalIgnoreCase));
            if (subcategory == null)
            {
                subcategory = new PungentUtilityHelpSubcategoryNode
                {
                    id = subcategoryId,
                    displayName = string.IsNullOrWhiteSpace(subcategoryLabel) ? subcategoryId : subcategoryLabel,
                    categoryId = categoryId
                };
                category.subcategories.Add(subcategory);
            }

            return subcategory;
        }

        private void PruneEmpty(string query)
        {
            bool searching = !string.IsNullOrWhiteSpace(query);
            foreach (PungentUtilityHelpCategoryNode category in categories)
            {
                foreach (PungentUtilityHelpSubcategoryNode subcategory in category.subcategories)
                {
                    subcategory.utilities.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
                    if (searching)
                        subcategory.utilities.RemoveAll(u => !UtilityNodeMatches(u, query));
                }

                category.subcategories.RemoveAll(s => s.utilities.Count == 0 && searching && !Contains(s.displayName, query));
                category.subcategories.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            }

            categories.RemoveAll(c => c.subcategories.Count == 0 && searching && !Contains(c.displayName, query));
            categories.Sort((a, b) =>
            {
                int sort = PungentUtilityCategories.SortKey(a.id).CompareTo(PungentUtilityCategories.SortKey(b.id));
                return sort != 0 ? sort : string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool ShouldIncludeDescriptor(PungentUtilityDescriptor descriptor, bool includeDeveloperOnly, bool includeHidden)
        {
            if (descriptor == null)
                return false;
            if (descriptor.IsVisibleInDefaultBrowser)
                return true;
            return (includeDeveloperOnly && descriptor.IsDeveloperOnly) || (includeHidden && descriptor.IsHiddenOrInternal);
        }

        private static bool ShouldShowDescriptorForSearch(PungentUtilityDescriptor descriptor, string query, List<PungentUtilityHelpTopic> topics, HashSet<string> matchingTopicIds)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            if (DescriptorMatches(descriptor, query))
                return true;
            return topics.Any(t => string.Equals(t.utilityId, descriptor.Id, StringComparison.OrdinalIgnoreCase) && matchingTopicIds.Contains(t.StableId));
        }

        private static bool UtilityNodeMatches(PungentUtilityHelpUtilityNode utility, string query)
        {
            if (utility == null)
                return false;
            if (string.IsNullOrWhiteSpace(query))
                return true;
            return Contains(utility.displayName, query) ||
                   Contains(utility.summary, query) ||
                   utility.topics.Any(t => Contains(t.title, query) || Contains(t.summary, query) || Contains(t.topicId, query) || Contains(t.sectionId, query));
        }

        private static bool DescriptorMatches(PungentUtilityDescriptor descriptor, string query)
        {
            return string.IsNullOrWhiteSpace(query) || (descriptor != null && descriptor.Matches(query));
        }

        private static string GetSubcategoryLabel(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return "Documentation";
            string module = PungentUtilityRegistry.GetModule(descriptor);
            if (!string.IsNullOrWhiteSpace(module))
                return module.Trim();
            return string.IsNullOrWhiteSpace(descriptor.UtilityType) ? "Documentation" : descriptor.UtilityType.Trim();
        }

        private static string NormalizeSubcategoryId(string value)
        {
            return PungentUtilityHelpIds.Normalize(value, "documentation");
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class PungentUtilityHelpCategoryNode
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public readonly List<PungentUtilityHelpSubcategoryNode> subcategories = new List<PungentUtilityHelpSubcategoryNode>();
    }

    public sealed class PungentUtilityHelpSubcategoryNode
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string categoryId = string.Empty;
        public readonly List<PungentUtilityHelpUtilityNode> utilities = new List<PungentUtilityHelpUtilityNode>();
    }

    public sealed class PungentUtilityHelpUtilityNode
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string summary = string.Empty;
        public string categoryId = string.Empty;
        public string subcategoryId = string.Empty;
        public PungentUtilityDescriptor descriptor;
        public readonly List<PungentUtilityHelpTopic> topics = new List<PungentUtilityHelpTopic>();
    }
#endif
}
