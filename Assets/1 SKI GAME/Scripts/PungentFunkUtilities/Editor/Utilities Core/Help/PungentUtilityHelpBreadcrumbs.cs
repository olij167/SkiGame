namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using PungentFunk.Utilities.Editor.Core;

    public sealed class PungentUtilityHelpBreadcrumbSegment
    {
        public string label = string.Empty;
        public string tooltip = string.Empty;
        public PungentUtilityHelpNavigationSelection selection;
    }

    public static class PungentUtilityHelpBreadcrumbs
    {
        public static List<PungentUtilityHelpBreadcrumbSegment> Build(PungentUtilityHelpNavigationModel model, PungentUtilityHelpNavigationSelection selection)
        {
            List<PungentUtilityHelpBreadcrumbSegment> segments = new List<PungentUtilityHelpBreadcrumbSegment>
            {
                new PungentUtilityHelpBreadcrumbSegment
                {
                    label = "Help Browser",
                    tooltip = "Show help search results and top-level documentation directories.",
                    selection = PungentUtilityHelpNavigationSelection.SearchResults()
                }
            };

            if (selection == null || selection.kind == PungentUtilityHelpSelectionKind.SearchResults)
                return segments;

            PungentUtilityHelpCategoryNode category = model == null ? null : model.FindCategory(selection.categoryId);
            if (!string.IsNullOrWhiteSpace(selection.categoryId))
            {
                segments.Add(new PungentUtilityHelpBreadcrumbSegment
                {
                    label = category == null ? PungentUtilityCategories.GetDisplayName(selection.categoryId) : category.displayName,
                    tooltip = "Open this help category.",
                    selection = PungentUtilityHelpNavigationSelection.Category(selection.categoryId)
                });
            }

            PungentUtilityHelpSubcategoryNode subcategory = model == null ? null : model.FindSubcategory(selection.categoryId, selection.subcategoryId);
            if (!string.IsNullOrWhiteSpace(selection.subcategoryId))
            {
                segments.Add(new PungentUtilityHelpBreadcrumbSegment
                {
                    label = subcategory == null ? selection.subcategoryId : subcategory.displayName,
                    tooltip = "Open this help subcategory.",
                    selection = PungentUtilityHelpNavigationSelection.Subcategory(selection.categoryId, selection.subcategoryId)
                });
            }

            PungentUtilityHelpUtilityNode utility = model == null ? null : model.FindUtility(selection.utilityId);
            if (!string.IsNullOrWhiteSpace(selection.utilityId))
            {
                segments.Add(new PungentUtilityHelpBreadcrumbSegment
                {
                    label = utility == null ? selection.utilityId : utility.displayName,
                    tooltip = "Open this utility help directory.",
                    selection = PungentUtilityHelpNavigationSelection.Utility(selection.categoryId, selection.subcategoryId, selection.utilityId)
                });
            }

            if (selection.kind == PungentUtilityHelpSelectionKind.Topic && !string.IsNullOrWhiteSpace(selection.topicStableId))
            {
                PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(selection.topicStableId);
                segments.Add(new PungentUtilityHelpBreadcrumbSegment
                {
                    label = topic == null ? "Missing Topic" : string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title,
                    tooltip = "Open this help topic.",
                    selection = selection
                });
            }
            else if (selection.kind == PungentUtilityHelpSelectionKind.MissingTopic)
            {
                segments.Add(new PungentUtilityHelpBreadcrumbSegment
                {
                    label = "Missing Topic",
                    tooltip = "This help destination has not been written yet.",
                    selection = selection
                });
            }

            return segments;
        }

        public static string CompactPath(IEnumerable<PungentUtilityHelpBreadcrumbSegment> segments)
        {
            List<string> labels = new List<string>();
            if (segments != null)
            {
                foreach (PungentUtilityHelpBreadcrumbSegment segment in segments)
                {
                    if (segment != null && !string.IsNullOrWhiteSpace(segment.label) && segment.label != "Help Browser")
                        labels.Add(segment.label);
                }
            }

            return labels.Count == 0 ? "Help Browser" : string.Join(" / ", labels.ToArray());
        }
    }
#endif
}
