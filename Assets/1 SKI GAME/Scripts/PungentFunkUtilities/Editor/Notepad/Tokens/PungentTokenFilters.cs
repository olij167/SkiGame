using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public enum PungentTokenQuickFilter
    {
        All,
        Required,
        Deprecated,
        Archived,
        WithBindings,
        WithoutBindings,
        BrokenBindings,
        UnknownFromLastScan
    }

    public static class PungentTokenFilters
    {
        public static List<PungentTokenDefinition> Query(string search, bool showArchived, bool showDeprecated, PungentTokenQuickFilter filter)
        {
            IEnumerable<PungentTokenDefinition> query = PungentTokenStorage.Database.tokens.Where(t => t != null);
            if (!showArchived && filter != PungentTokenQuickFilter.Archived)
                query = query.Where(t => !t.archived);
            if (!showDeprecated && filter != PungentTokenQuickFilter.Deprecated)
                query = query.Where(t => !t.deprecated);

            switch (filter)
            {
                case PungentTokenQuickFilter.Required: query = query.Where(t => t.required); break;
                case PungentTokenQuickFilter.Deprecated: query = query.Where(t => t.deprecated); break;
                case PungentTokenQuickFilter.Archived: query = query.Where(t => t.archived); break;
                case PungentTokenQuickFilter.WithBindings: query = query.Where(t => PungentTokenStorage.Database.GetBindingsForToken(t.key).Count > 0); break;
                case PungentTokenQuickFilter.WithoutBindings: query = query.Where(t => PungentTokenStorage.Database.GetBindingsForToken(t.key).Count == 0); break;
                case PungentTokenQuickFilter.BrokenBindings:
                    HashSet<string> broken = new HashSet<string>(
                        PungentTokenStorage.Database.bindings
                            .Where(b => b != null && !b.archived && !PungentTokenStorage.Database.ContainsToken(b.tokenKey))
                            .Select(b => b.tokenKey),
                        StringComparer.OrdinalIgnoreCase);
                    query = query.Where(t => broken.Contains(t.key));
                    break;
                case PungentTokenQuickFilter.UnknownFromLastScan:
                    HashSet<string> unknown = new HashSet<string>(PungentTokenValidatorService.LastUsages.Where(u => u.status == PungentTokenUsageStatus.Unknown).Select(u => u.tokenKey), StringComparer.OrdinalIgnoreCase);
                    query = query.Where(t => unknown.Contains(t.key));
                    break;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim();
                query = query.Where(t => Contains(t.key, q) || Contains(t.displayName, q) || Contains(t.description, q) || Contains(t.category, q) || Contains(t.previewValue, q) || (t.tags != null && t.tags.Any(tag => Contains(tag, q))) || (t.examples != null && t.examples.Any(e => Contains(e, q))));
            }

            return query.OrderBy(t => t.category).ThenBy(t => t.key).ToList();
        }

        private static bool Contains(string value, string query) => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
#endif
}
