using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Checklists;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    public interface IPungentChecklistDefinitionProvider
    {
        string ProviderId { get; }
        IEnumerable<PungentChecklistDefinition> GetChecklistDefinitions();
        IEnumerable<PungentLinkedStateGroupDefinition> GetLinkedStateGroups();
    }

    public static class PungentChecklistDefinitionRegistry
    {
        private static readonly List<IPungentChecklistDefinitionProvider> Providers = new List<IPungentChecklistDefinitionProvider>();

        public static event Action Changed;

        public static void Register(IPungentChecklistDefinitionProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;

            Providers.RemoveAll(item => item == null || string.Equals(item.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));
            Providers.Add(provider);
            Changed?.Invoke();
        }

        public static IReadOnlyList<PungentChecklistDefinition> ProviderDefinitions
        {
            get
            {
                List<PungentChecklistDefinition> result = new List<PungentChecklistDefinition>();
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (IPungentChecklistDefinitionProvider provider in Providers.ToArray())
                {
                    if (provider == null)
                        continue;

                    IEnumerable<PungentChecklistDefinition> definitions;
                    try
                    {
                        definitions = provider.GetChecklistDefinitions();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (PungentChecklistDefinition definition in definitions ?? Enumerable.Empty<PungentChecklistDefinition>())
                    {
                        if (definition == null || string.IsNullOrWhiteSpace(definition.checklistId) || !ids.Add(definition.checklistId))
                            continue;

                        PungentChecklistDefinition copy = PungentChecklistSerialization.Clone(definition);
                        if (copy != null)
                        {
                            copy.sourceProviderId = string.IsNullOrWhiteSpace(copy.sourceProviderId) ? provider.ProviderId : copy.sourceProviderId;
                            copy.NormalizeInPlace();
                            result.Add(copy);
                        }
                    }
                }

                return result;
            }
        }

        public static IReadOnlyList<PungentChecklistDefinition> ProjectDefinitions
        {
            get
            {
                return (PungentChecklistStorage.Database.checklists ?? new List<PungentChecklistDefinition>())
                    .Where(item => item != null)
                    .Select(PungentChecklistSerialization.Clone)
                    .Where(item => item != null)
                    .ToList();
            }
        }

        public static IReadOnlyList<PungentChecklistDefinition> AllDefinitions
        {
            get
            {
                List<PungentChecklistDefinition> result = new List<PungentChecklistDefinition>();
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (PungentChecklistDefinition providerDefinition in ProviderDefinitions)
                {
                    if (providerDefinition == null || string.IsNullOrWhiteSpace(providerDefinition.checklistId) || !ids.Add(providerDefinition.checklistId))
                        continue;
                    result.Add(providerDefinition);
                }

                foreach (PungentChecklistDefinition projectDefinition in ProjectDefinitions)
                {
                    if (projectDefinition == null || string.IsNullOrWhiteSpace(projectDefinition.checklistId) || !ids.Add(projectDefinition.checklistId))
                        continue;
                    result.Add(projectDefinition);
                }

                return result;
            }
        }

        public static IReadOnlyList<PungentLinkedStateGroupDefinition> AllLinkedStateGroups
        {
            get
            {
                List<PungentLinkedStateGroupDefinition> result = new List<PungentLinkedStateGroupDefinition>();
                HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (IPungentChecklistDefinitionProvider provider in Providers.ToArray())
                {
                    if (provider == null)
                        continue;

                    IEnumerable<PungentLinkedStateGroupDefinition> groups;
                    try
                    {
                        groups = provider.GetLinkedStateGroups();
                    }
                    catch
                    {
                        continue;
                    }

                    AddGroups(result, keys, groups);
                }

                AddGroups(result, keys, PungentChecklistStorage.Database.linkedStateGroups);
                HydrateGroupsFromDefinitions(result, keys, AllDefinitions);
                return result;
            }
        }

        public static PungentChecklistDefinition Find(string checklistId)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
                return null;

            return AllDefinitions.FirstOrDefault(item => item != null && string.Equals(item.checklistId, checklistId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsProviderDefinitionId(string checklistId)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
                return false;

            return ProviderDefinitions.Any(item => item != null && string.Equals(item.checklistId, checklistId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsProjectDefinitionId(string checklistId)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
                return false;

            return ProjectDefinitions.Any(item => item != null && string.Equals(item.checklistId, checklistId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool UpsertProjectDefinition(PungentChecklistDefinition checklist, IEnumerable<PungentLinkedStateGroupDefinition> linkedStateGroups, out string error)
        {
            error = string.Empty;
            if (checklist == null)
            {
                error = "Checklist definition is missing.";
                return false;
            }

            if (IsProviderDefinitionId(checklist.checklistId))
            {
                error = "Checklist ID '" + checklist.checklistId + "' is reserved by an installed package-provided checklist.";
                return false;
            }

            checklist.schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
            checklist.Touch();
            if (!PungentChecklistStorage.UpsertChecklist(checklist, out error, false))
                return false;

            MergeProjectLinkedStateGroups(linkedStateGroups);
            bool saved = PungentChecklistStorage.Save(out error);
            Changed?.Invoke();
            return saved;
        }

        public static bool IsEditableProjectDefinition(string checklistId)
        {
            return IsProjectDefinitionId(checklistId) && !IsProviderDefinitionId(checklistId);
        }

        public static bool CloneToProject(PungentChecklistDefinition source, out PungentChecklistDefinition clone, out string error)
        {
            clone = null;
            error = string.Empty;
            if (source == null)
            {
                error = "Checklist definition is missing.";
                return false;
            }

            clone = PungentChecklistSerialization.Clone(source);
            if (clone == null)
            {
                error = "Checklist definition could not be cloned.";
                return false;
            }

            clone.checklistId = UniqueProjectChecklistId(source.checklistId + "-project-copy");
            clone.title = source.title + " Copy";
            clone.sourceProviderId = PungentChecklistConstants.ProviderId;
            clone.sourceItemId = source.checklistId;
            clone.sourceLabel = "Cloned from " + (string.IsNullOrWhiteSpace(source.sourceLabel) ? source.title : source.sourceLabel);
            clone.sourcePath = string.Empty;
            clone.createdUtc = DateTime.UtcNow.ToString("o");
            clone.updatedUtc = clone.createdUtc;
            clone.NormalizeInPlace();

            if (!UpsertProjectDefinition(clone, AllLinkedStateGroups, out error))
                return false;

            return true;
        }

        public static bool ArchiveProjectDefinition(string checklistId, bool archived, out string error)
        {
            error = string.Empty;
            PungentChecklistDefinition checklist = ProjectDefinitions.FirstOrDefault(item => item != null && string.Equals(item.checklistId, checklistId, StringComparison.OrdinalIgnoreCase));
            if (checklist == null)
            {
                error = "Project checklist could not be found.";
                return false;
            }

            checklist.archived = archived;
            return UpsertProjectDefinition(checklist, null, out error);
        }

        public static bool DeleteProjectDefinition(string checklistId, out string error)
        {
            if (!IsProjectDefinitionId(checklistId))
            {
                error = "Project checklist could not be found.";
                return false;
            }

            bool removed = PungentChecklistStorage.TryDeleteChecklist(checklistId, out error);
            if (removed)
                Changed?.Invoke();
            return removed;
        }

        public static string UniqueProjectChecklistId(string preferred)
        {
            HashSet<string> used = new HashSet<string>(AllDefinitions.Where(item => item != null).Select(item => item.checklistId), StringComparer.OrdinalIgnoreCase);
            return PungentChecklistSerialization.UniqueId(preferred, used, "checklist");
        }

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static void MergeProjectLinkedStateGroups(IEnumerable<PungentLinkedStateGroupDefinition> linkedStateGroups)
        {
            if (linkedStateGroups == null)
                return;

            foreach (PungentLinkedStateGroupDefinition group in linkedStateGroups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.linkedStateKey))
                    continue;

                group.NormalizeInPlace();
                int index = PungentChecklistStorage.Database.linkedStateGroups.FindIndex(item => item != null && string.Equals(item.linkedStateKey, group.linkedStateKey, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                    PungentChecklistStorage.Database.linkedStateGroups[index] = PungentChecklistSerialization.Clone(group);
                else
                    PungentChecklistStorage.Database.linkedStateGroups.Add(PungentChecklistSerialization.Clone(group));
            }
        }

        private static void AddGroups(List<PungentLinkedStateGroupDefinition> result, HashSet<string> keys, IEnumerable<PungentLinkedStateGroupDefinition> groups)
        {
            foreach (PungentLinkedStateGroupDefinition group in groups ?? Enumerable.Empty<PungentLinkedStateGroupDefinition>())
            {
                if (group == null || string.IsNullOrWhiteSpace(group.linkedStateKey) || !keys.Add(group.linkedStateKey))
                    continue;

                PungentLinkedStateGroupDefinition copy = PungentChecklistSerialization.Clone(group);
                if (copy != null)
                    result.Add(copy);
            }
        }

        private static void HydrateGroupsFromDefinitions(List<PungentLinkedStateGroupDefinition> result, HashSet<string> keys, IEnumerable<PungentChecklistDefinition> definitions)
        {
            foreach (PungentChecklistDefinition checklist in definitions ?? Enumerable.Empty<PungentChecklistDefinition>())
            {
                foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.linkedStateKey) || !keys.Add(item.linkedStateKey))
                        continue;

                    result.Add(new PungentLinkedStateGroupDefinition
                    {
                        linkedStateKey = item.linkedStateKey,
                        label = item.label,
                        ownerPackageId = string.IsNullOrWhiteSpace(item.canonicalOwnerPackageId) ? item.ownerPackageId : item.canonicalOwnerPackageId,
                        sourceProviderId = checklist.sourceProviderId,
                        sourceItemId = checklist.sourceItemId
                    });
                }
            }
        }
    }
#endif
}
