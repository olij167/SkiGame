using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentDocumentationRecord
    {
        public string providerId = string.Empty;
        public string itemId = string.Empty;
        public PungentAuthoringItemKind kind = PungentAuthoringItemKind.Unknown;
        public string typeLabel = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
        public bool archived;
        public bool locked;
        public bool generated;
        public string generatedBy = string.Empty;
        public string generatedTemplateId = string.Empty;
        public string generatedUtc = string.Empty;

        public string Key => PungentDocumentationTestDataRegistry.RecordKey(providerId, itemId);
    }

    public sealed class PungentGeneratedDocumentTemplate
    {
        public string providerId = string.Empty;
        public string templateId = string.Empty;
        public string documentType = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
    }

    public sealed class PungentDocumentationManagementResult
    {
        public int created;
        public int updated;
        public int removed;
        public int locked;
        public int unlocked;
        public int adopted;
        public int skipped;
        public int skippedLocked;
        public readonly List<string> messages = new List<string>();

        public int Affected => created + updated + removed + locked + unlocked + adopted;

        public void Add(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                messages.Add(message.Trim());
        }

        public string ToStatus(string fallback = "Documentation test-data action complete.")
        {
            List<string> parts = new List<string>();
            if (created > 0) parts.Add("created " + created);
            if (updated > 0) parts.Add("updated " + updated);
            if (removed > 0) parts.Add("removed " + removed);
            if (locked > 0) parts.Add("locked " + locked);
            if (unlocked > 0) parts.Add("unlocked " + unlocked);
            if (adopted > 0) parts.Add("adopted " + adopted);
            if (skippedLocked > 0) parts.Add("skipped locked " + skippedLocked);
            if (skipped > 0) parts.Add("skipped " + skipped);
            return parts.Count == 0 ? fallback : "Documentation test data: " + string.Join(", ", parts.ToArray()) + ".";
        }
    }

    public interface IPungentDocumentationTestDataProvider
    {
        string ProviderId { get; }
        string DisplayName { get; }
        string DocumentType { get; }
        IEnumerable<PungentDocumentationRecord> ListRecords();
        IEnumerable<PungentGeneratedDocumentTemplate> ListTemplates();
        void Generate(string templateId, bool replaceExisting, PungentDocumentationManagementResult result);
        void Remove(IEnumerable<string> itemIds, bool requireGenerated, PungentDocumentationManagementResult result);
        void SetLocked(IEnumerable<string> itemIds, bool locked, PungentDocumentationManagementResult result);
        void AdoptAsGenerated(IEnumerable<string> itemIds, PungentDocumentationManagementResult result);
    }

    public static class PungentDocumentationTestDataRegistry
    {
        public const string GeneratedBy = "developer-documentation-test-data";

        private static readonly List<IPungentDocumentationTestDataProvider> Providers = new List<IPungentDocumentationTestDataProvider>();

        public static IReadOnlyList<IPungentDocumentationTestDataProvider> AllProviders => Providers;

        public static void Register(IPungentDocumentationTestDataProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;

            Providers.RemoveAll(item => item == null || string.Equals(item.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));
            Providers.Add(provider);
        }

        public static IReadOnlyList<PungentDocumentationRecord> AllRecords()
        {
            return Providers
                .SelectMany(provider => SafeRecords(provider))
                .OrderBy(record => record.typeLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<PungentGeneratedDocumentTemplate> AllTemplates()
        {
            return Providers
                .SelectMany(provider => SafeTemplates(provider))
                .OrderBy(template => template.documentType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(template => template.title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static PungentDocumentationManagementResult GenerateMissing(string providerId = null)
        {
            PungentDocumentationManagementResult result = new PungentDocumentationManagementResult();
            foreach (IPungentDocumentationTestDataProvider provider in MatchingProviders(providerId))
                foreach (PungentGeneratedDocumentTemplate template in SafeTemplates(provider))
                    provider.Generate(template.templateId, false, result);
            return result;
        }

        public static PungentDocumentationManagementResult RegenerateUnlocked(string providerId = null)
        {
            PungentDocumentationManagementResult result = new PungentDocumentationManagementResult();
            foreach (IPungentDocumentationTestDataProvider provider in MatchingProviders(providerId))
                foreach (PungentGeneratedDocumentTemplate template in SafeTemplates(provider))
                    provider.Generate(template.templateId, true, result);
            return result;
        }

        public static PungentDocumentationManagementResult GenerateTemplate(string providerId, string templateId, bool replaceExisting)
        {
            PungentDocumentationManagementResult result = new PungentDocumentationManagementResult();
            IPungentDocumentationTestDataProvider provider = FindProvider(providerId);
            if (provider == null)
            {
                result.skipped++;
                result.Add("Provider '" + providerId + "' is not available.");
                return result;
            }

            provider.Generate(templateId, replaceExisting, result);
            return result;
        }

        public static PungentDocumentationManagementResult RemoveRecords(IEnumerable<string> recordKeys, bool requireGenerated)
        {
            PungentDocumentationManagementResult result = new PungentDocumentationManagementResult();
            foreach (IGrouping<string, string> group in GroupItemIdsByProvider(recordKeys))
            {
                IPungentDocumentationTestDataProvider provider = FindProvider(group.Key);
                if (provider == null)
                {
                    result.skipped += group.Count();
                    continue;
                }

                provider.Remove(group.ToList(), requireGenerated, result);
            }

            return result;
        }

        public static PungentDocumentationManagementResult RemoveAllGeneratedUnlocked()
        {
            return RemoveRecords(AllRecords().Where(record => record.generated).Select(record => record.Key), true);
        }

        public static PungentDocumentationManagementResult SetLocked(IEnumerable<string> recordKeys, bool locked)
        {
            PungentDocumentationManagementResult result = new PungentDocumentationManagementResult();
            foreach (IGrouping<string, string> group in GroupItemIdsByProvider(recordKeys))
            {
                IPungentDocumentationTestDataProvider provider = FindProvider(group.Key);
                if (provider == null)
                {
                    result.skipped += group.Count();
                    continue;
                }

                provider.SetLocked(group.ToList(), locked, result);
            }

            return result;
        }

        public static PungentDocumentationManagementResult AdoptAsGenerated(IEnumerable<string> recordKeys)
        {
            PungentDocumentationManagementResult result = new PungentDocumentationManagementResult();
            foreach (IGrouping<string, string> group in GroupItemIdsByProvider(recordKeys))
            {
                IPungentDocumentationTestDataProvider provider = FindProvider(group.Key);
                if (provider == null)
                {
                    result.skipped += group.Count();
                    continue;
                }

                provider.AdoptAsGenerated(group.ToList(), result);
            }

            return result;
        }

        public static string RecordKey(string providerId, string itemId)
        {
            return (providerId ?? string.Empty).Trim() + "|" + (itemId ?? string.Empty).Trim();
        }

        public static bool TrySplitRecordKey(string key, out string providerId, out string itemId)
        {
            providerId = string.Empty;
            itemId = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string[] parts = key.Split(new[] { '|' }, 2);
            if (parts.Length != 2)
                return false;

            providerId = parts[0].Trim();
            itemId = parts[1].Trim();
            return !string.IsNullOrWhiteSpace(providerId) && !string.IsNullOrWhiteSpace(itemId);
        }

        public static string StableGeneratedId(string providerId, string templateId)
        {
            string source = (providerId ?? string.Empty) + "-" + (templateId ?? string.Empty);
            char[] chars = source.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            string collapsed = new string(chars);
            while (collapsed.IndexOf("--", StringComparison.Ordinal) >= 0)
                collapsed = collapsed.Replace("--", "-");
            return "generated-" + collapsed.Trim('-');
        }

        private static IPungentDocumentationTestDataProvider FindProvider(string providerId)
        {
            return Providers.FirstOrDefault(provider => provider != null && string.Equals(provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<IPungentDocumentationTestDataProvider> MatchingProviders(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return Providers.ToArray();
            IPungentDocumentationTestDataProvider provider = FindProvider(providerId);
            return provider == null ? new IPungentDocumentationTestDataProvider[0] : new[] { provider };
        }

        private static IEnumerable<IGrouping<string, string>> GroupItemIdsByProvider(IEnumerable<string> recordKeys)
        {
            return (recordKeys ?? new string[0])
                .Select(key =>
                {
                    string providerId;
                    string itemId;
                    return TrySplitRecordKey(key, out providerId, out itemId) ? new { providerId, itemId } : null;
                })
                .Where(item => item != null)
                .GroupBy(item => item.providerId, item => item.itemId, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<PungentDocumentationRecord> SafeRecords(IPungentDocumentationTestDataProvider provider)
        {
            try
            {
                return provider.ListRecords() ?? new PungentDocumentationRecord[0];
            }
            catch
            {
                return new PungentDocumentationRecord[0];
            }
        }

        private static IEnumerable<PungentGeneratedDocumentTemplate> SafeTemplates(IPungentDocumentationTestDataProvider provider)
        {
            try
            {
                return provider.ListTemplates() ?? new PungentGeneratedDocumentTemplate[0];
            }
            catch
            {
                return new PungentGeneratedDocumentTemplate[0];
            }
        }
    }
#endif
}
