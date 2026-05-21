namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    public static class PungentUtilityHelpRegistry
    {
        private static readonly Dictionary<string, PungentUtilityHelpTopic> Topics = new Dictionary<string, PungentUtilityHelpTopic>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static event Action Changed;

        public static IReadOnlyCollection<PungentUtilityHelpTopic> AllTopics
        {
            get
            {
                EnsureInitialized();
                return Topics.Values;
            }
        }

        public static void Rebuild()
        {
            _initialized = false;
            Topics.Clear();
            EnsureInitialized();
            Changed?.Invoke();
        }

        public static void RegisterBuiltInTopics(IEnumerable<PungentUtilityHelpTopic> topics)
        {
            EnsureInitialized();
            RegisterLayer(topics, HelpLayer.BuiltIn);
            Changed?.Invoke();
        }

        public static void RegisterGeneratedTopics(IEnumerable<PungentUtilityHelpTopic> topics)
        {
            EnsureInitialized();
            RegisterLayer(topics, HelpLayer.Generated);
            Changed?.Invoke();
        }

        public static void RegisterManualOverrideTopics(IEnumerable<PungentUtilityHelpTopic> topics)
        {
            EnsureInitialized();
            RegisterLayer(topics, HelpLayer.Manual);
            Changed?.Invoke();
        }

        public static PungentUtilityHelpTopic Find(string utilityId, string sectionId = null, string topicId = null)
        {
            EnsureInitialized();
            string key = PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId);
            if (Topics.TryGetValue(key, out PungentUtilityHelpTopic topic))
                return topic;

            if (!string.IsNullOrWhiteSpace(sectionId) && string.IsNullOrWhiteSpace(topicId))
            {
                key = PungentUtilityHelpIds.TopicKey(utilityId, sectionId, sectionId);
                if (Topics.TryGetValue(key, out topic))
                    return topic;
            }

            return topic;
        }

        public static PungentUtilityHelpTopic FindByStableId(string stableId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(stableId))
                return null;

            Topics.TryGetValue(stableId, out PungentUtilityHelpTopic topic);
            return topic;
        }

        public static List<PungentUtilityHelpTopic> TopicsForUtility(string utilityId, bool includeDeveloperOnly, bool includeHidden, bool includeGenerated)
        {
            EnsureInitialized();
            string normalized = PungentUtilityHelpIds.Normalize(utilityId);
            return Topics.Values
                .Where(t => string.Equals(t.utilityId, normalized, StringComparison.OrdinalIgnoreCase))
                .Where(t => PassesVisibility(t, includeDeveloperOnly, includeHidden, includeGenerated))
                .OrderBy(t => t.sectionId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.topicId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<PungentUtilityHelpTopic> Search(string query, bool includeDeveloperOnly, bool includeHidden, bool includeGenerated)
        {
            EnsureInitialized();
            string q = query == null ? string.Empty : query.Trim();
            IEnumerable<PungentUtilityHelpTopic> source = Topics.Values.Where(t => PassesVisibility(t, includeDeveloperOnly, includeHidden, includeGenerated));
            if (string.IsNullOrWhiteSpace(q))
                return source.OrderBy(t => t.utilityId).ThenBy(t => t.title).ToList();

            return source
                .Where(t => Matches(t, q))
                .OrderBy(t => t.utilityId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void Open(string utilityId, string sectionId = null, string topicId = null)
        {
            PungentUtilityHelpBrowserWindow.Open(utilityId, sectionId, topicId);
        }

        internal static PungentUtilityHelpTopic CreateTopicStub(string utilityId, string sectionId, string topicId)
        {
            PungentUtilityHelpTopic topic = PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(utilityId, sectionId, topicId);
            Rebuild();
            return Find(topic.utilityId, topic.sectionId, topic.topicId) ?? topic;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            Topics.Clear();
            RegisterLayer(PungentUtilityHelpStorage.instance.generatedTopics, HelpLayer.Generated);
            RegisterLayer(PungentUtilityHelpSeedData.CreateTopics(), HelpLayer.BuiltIn);
            RegisterLayer(PungentUtilityHelpStorage.instance.manualTopics, HelpLayer.Manual);
        }

        private static void RegisterLayer(IEnumerable<PungentUtilityHelpTopic> topics, HelpLayer layer)
        {
            if (topics == null)
                return;

            foreach (PungentUtilityHelpTopic topic in topics)
            {
                if (topic == null || string.IsNullOrWhiteSpace(topic.utilityId))
                    continue;

                NormalizeTopic(topic);
                string key = topic.StableId;
                if (!Topics.TryGetValue(key, out PungentUtilityHelpTopic existing))
                {
                    Topics[key] = Clone(topic);
                    continue;
                }

                Topics[key] = Merge(existing, topic, layer);
            }
        }

        private static PungentUtilityHelpTopic Merge(PungentUtilityHelpTopic lower, PungentUtilityHelpTopic higher, HelpLayer layer)
        {
            PungentUtilityHelpTopic result = Clone(lower);

            OverrideString(ref result.utilityId, higher.utilityId);
            OverrideString(ref result.sectionId, higher.sectionId);
            OverrideString(ref result.topicId, higher.topicId);
            OverrideString(ref result.title, higher.title);
            OverrideString(ref result.summary, higher.summary);
            OverrideString(ref result.quickUseMarkdown, higher.quickUseMarkdown);
            OverrideString(ref result.sourceOwner, higher.sourceOwner);
            OverrideString(ref result.lastUpdatedUtc, higher.lastUpdatedUtc);

            if (layer == HelpLayer.Manual)
            {
                result.developerOnly = higher.developerOnly;
                result.hidden = higher.hidden;
                result.generated = lower.generated && higher.generated;
            }
            else if (layer == HelpLayer.BuiltIn)
            {
                result.developerOnly = higher.developerOnly;
                result.hidden = higher.hidden;
                result.generated = false;
            }
            else
            {
                result.generated = lower.generated || higher.generated;
            }

            result.featureEntries = MergeFeatures(lower.featureEntries, higher.featureEntries, layer);
            result.scriptingEntries = MergeScripting(lower.scriptingEntries, higher.scriptingEntries, layer);
            result.troubleshootingEntries = MergeTroubleshooting(lower.troubleshootingEntries, higher.troubleshootingEntries);
            result.relatedTopicIds = MergeStrings(lower.relatedTopicIds, higher.relatedTopicIds);
            result.relatedUtilityIds = MergeStrings(lower.relatedUtilityIds, higher.relatedUtilityIds);
            result.relatedNoteIds = MergeStrings(lower.relatedNoteIds, higher.relatedNoteIds);
            result.relatedDocumentationLinkIds = MergeStrings(lower.relatedDocumentationLinkIds, higher.relatedDocumentationLinkIds);
            result.tags = MergeStrings(lower.tags, higher.tags);
            return result;
        }

        private static List<PungentUtilityHelpFeatureEntry> MergeFeatures(List<PungentUtilityHelpFeatureEntry> lower, List<PungentUtilityHelpFeatureEntry> higher, HelpLayer layer)
        {
            Dictionary<string, PungentUtilityHelpFeatureEntry> byId = new Dictionary<string, PungentUtilityHelpFeatureEntry>(StringComparer.OrdinalIgnoreCase);
            AddFeatureLayer(byId, lower, HelpLayer.Generated);
            AddFeatureLayer(byId, higher, layer);
            return byId.Values.ToList();
        }

        private static void AddFeatureLayer(Dictionary<string, PungentUtilityHelpFeatureEntry> byId, List<PungentUtilityHelpFeatureEntry> entries, HelpLayer layer)
        {
            if (entries == null)
                return;

            foreach (PungentUtilityHelpFeatureEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    continue;

                if (!byId.TryGetValue(entry.id, out PungentUtilityHelpFeatureEntry existing))
                {
                    byId[entry.id] = Clone(entry);
                    continue;
                }

                OverrideString(ref existing.label, entry.label);
                OverrideString(ref existing.description, entry.description);
                OverrideString(ref existing.location, entry.location);
                OverrideString(ref existing.safetyNotes, entry.safetyNotes);
                OverrideString(ref existing.sourcePath, entry.sourcePath);
                OverrideString(ref existing.sourceConfidence, entry.sourceConfidence);
                if (entry.sourceLine > 0)
                    existing.sourceLine = entry.sourceLine;
                existing.needsBetterWording = entry.needsBetterWording;
                existing.ignoredGenerated = entry.ignoredGenerated;
                if (layer != HelpLayer.Generated)
                {
                    existing.developerOnly = entry.developerOnly;
                    existing.hidden = entry.hidden;
                    existing.generated = layer == HelpLayer.Manual ? existing.generated && entry.generated : entry.generated;
                }
            }
        }

        private static List<PungentUtilityHelpScriptingEntry> MergeScripting(List<PungentUtilityHelpScriptingEntry> lower, List<PungentUtilityHelpScriptingEntry> higher, HelpLayer layer)
        {
            Dictionary<string, PungentUtilityHelpScriptingEntry> byId = new Dictionary<string, PungentUtilityHelpScriptingEntry>(StringComparer.OrdinalIgnoreCase);
            AddScriptingLayer(byId, lower, HelpLayer.Generated);
            AddScriptingLayer(byId, higher, layer);
            return byId.Values.OrderBy(e => e.declaringType).ThenBy(e => e.memberName).ToList();
        }

        private static void AddScriptingLayer(Dictionary<string, PungentUtilityHelpScriptingEntry> byId, List<PungentUtilityHelpScriptingEntry> entries, HelpLayer layer)
        {
            if (entries == null)
                return;

            foreach (PungentUtilityHelpScriptingEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    continue;

                if (!byId.TryGetValue(entry.id, out PungentUtilityHelpScriptingEntry existing))
                {
                    byId[entry.id] = Clone(entry);
                    continue;
                }

                OverrideString(ref existing.declaringType, entry.declaringType);
                OverrideString(ref existing.memberName, entry.memberName);
                OverrideString(ref existing.signature, entry.signature);
                OverrideString(ref existing.description, entry.description);
                OverrideString(ref existing.usageNotes, entry.usageNotes);
                OverrideString(ref existing.minimalExample, entry.minimalExample);
                OverrideString(ref existing.whereItAppears, entry.whereItAppears);
                OverrideString(ref existing.sourcePath, entry.sourcePath);
                if (layer != HelpLayer.Generated)
                {
                    existing.developerOnly = entry.developerOnly;
                    existing.hidden = entry.hidden;
                    existing.generated = layer == HelpLayer.Manual ? existing.generated && entry.generated : entry.generated;
                }
            }
        }

        private static List<PungentUtilityHelpTroubleshootingEntry> MergeTroubleshooting(List<PungentUtilityHelpTroubleshootingEntry> lower, List<PungentUtilityHelpTroubleshootingEntry> higher)
        {
            Dictionary<string, PungentUtilityHelpTroubleshootingEntry> byId = new Dictionary<string, PungentUtilityHelpTroubleshootingEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentUtilityHelpTroubleshootingEntry entry in lower ?? new List<PungentUtilityHelpTroubleshootingEntry>())
                if (entry != null && !string.IsNullOrWhiteSpace(entry.id))
                    byId[entry.id] = Clone(entry);
            foreach (PungentUtilityHelpTroubleshootingEntry entry in higher ?? new List<PungentUtilityHelpTroubleshootingEntry>())
                if (entry != null && !string.IsNullOrWhiteSpace(entry.id))
                    byId[entry.id] = Clone(entry);
            return byId.Values.ToList();
        }

        private static bool Matches(PungentUtilityHelpTopic topic, string query)
        {
            if (topic == null)
                return false;

            return Contains(topic.utilityId, query) ||
                   Contains(topic.sectionId, query) ||
                   Contains(topic.topicId, query) ||
                   Contains(topic.title, query) ||
                   Contains(topic.summary, query) ||
                   Contains(topic.quickUseMarkdown, query) ||
                   ContainsAny(topic.tags, query) ||
                   (topic.featureEntries != null && topic.featureEntries.Any(f => Contains(f.label, query) || Contains(f.description, query) || Contains(f.location, query) || Contains(f.safetyNotes, query))) ||
                   (topic.scriptingEntries != null && topic.scriptingEntries.Any(s => Contains(s.declaringType, query) || Contains(s.memberName, query) || Contains(s.signature, query) || Contains(s.description, query) || Contains(s.usageNotes, query))) ||
                   (topic.troubleshootingEntries != null && topic.troubleshootingEntries.Any(t => Contains(t.symptom, query) || Contains(t.likelyCause, query) || Contains(t.nextStep, query)));
        }

        private static bool PassesVisibility(PungentUtilityHelpTopic topic, bool includeDeveloperOnly, bool includeHidden, bool includeGenerated)
        {
            if (topic == null)
                return false;
            if (topic.hidden && !includeHidden)
                return false;
            if (topic.developerOnly && !includeDeveloperOnly)
                return false;
            if (topic.generated && !includeGenerated)
                return false;
            return true;
        }

        internal static bool EntryVisible(PungentUtilityHelpFeatureEntry entry, bool includeDeveloperOnly, bool includeHidden, bool includeGenerated)
        {
            return entry != null && (!entry.hidden || includeHidden) && (!entry.developerOnly || includeDeveloperOnly) && (!entry.generated || includeGenerated);
        }

        internal static bool EntryVisible(PungentUtilityHelpScriptingEntry entry, bool includeDeveloperOnly, bool includeHidden, bool includeGenerated)
        {
            return entry != null && (!entry.hidden || includeHidden) && (!entry.developerOnly || includeDeveloperOnly) && (!entry.generated || includeGenerated);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(List<string> values, string query)
        {
            return values != null && values.Any(v => Contains(v, query));
        }

        private static List<string> MergeStrings(List<string> lower, List<string> higher)
        {
            return (lower ?? new List<string>())
                .Concat(higher ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void OverrideString(ref string target, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target = value.Trim();
        }

        private static void NormalizeTopic(PungentUtilityHelpTopic topic)
        {
            topic.utilityId = PungentUtilityHelpIds.Normalize(topic.utilityId);
            topic.sectionId = PungentUtilityHelpIds.Normalize(topic.sectionId, PungentUtilityHelpIds.DefaultSection);
            topic.topicId = PungentUtilityHelpIds.Normalize(topic.topicId, PungentUtilityHelpIds.DefaultTopic);
        }

        private static PungentUtilityHelpTopic Clone(PungentUtilityHelpTopic source)
        {
            if (source == null)
                return null;

            return new PungentUtilityHelpTopic
            {
                utilityId = source.utilityId,
                sectionId = source.sectionId,
                topicId = source.topicId,
                title = source.title,
                summary = source.summary,
                quickUseMarkdown = source.quickUseMarkdown,
                featureEntries = (source.featureEntries ?? new List<PungentUtilityHelpFeatureEntry>()).Select(Clone).Where(e => e != null).ToList(),
                scriptingEntries = (source.scriptingEntries ?? new List<PungentUtilityHelpScriptingEntry>()).Select(Clone).Where(e => e != null).ToList(),
                troubleshootingEntries = (source.troubleshootingEntries ?? new List<PungentUtilityHelpTroubleshootingEntry>()).Select(Clone).Where(e => e != null).ToList(),
                relatedTopicIds = new List<string>(source.relatedTopicIds ?? new List<string>()),
                relatedUtilityIds = new List<string>(source.relatedUtilityIds ?? new List<string>()),
                relatedNoteIds = new List<string>(source.relatedNoteIds ?? new List<string>()),
                relatedDocumentationLinkIds = new List<string>(source.relatedDocumentationLinkIds ?? new List<string>()),
                tags = new List<string>(source.tags ?? new List<string>()),
                developerOnly = source.developerOnly,
                hidden = source.hidden,
                generated = source.generated,
                sourceOwner = source.sourceOwner,
                lastUpdatedUtc = source.lastUpdatedUtc
            };
        }

        private static PungentUtilityHelpFeatureEntry Clone(PungentUtilityHelpFeatureEntry source)
        {
            if (source == null)
                return null;
            return new PungentUtilityHelpFeatureEntry
            {
                id = source.id,
                label = source.label,
                description = source.description,
                location = source.location,
                safetyNotes = source.safetyNotes,
                sourcePath = source.sourcePath,
                sourceLine = source.sourceLine,
                sourceConfidence = source.sourceConfidence,
                needsBetterWording = source.needsBetterWording,
                ignoredGenerated = source.ignoredGenerated,
                developerOnly = source.developerOnly,
                hidden = source.hidden,
                generated = source.generated
            };
        }

        private static PungentUtilityHelpScriptingEntry Clone(PungentUtilityHelpScriptingEntry source)
        {
            if (source == null)
                return null;
            return new PungentUtilityHelpScriptingEntry
            {
                id = source.id,
                declaringType = source.declaringType,
                memberName = source.memberName,
                signature = source.signature,
                description = source.description,
                usageNotes = source.usageNotes,
                minimalExample = source.minimalExample,
                whereItAppears = source.whereItAppears,
                developerOnly = source.developerOnly,
                hidden = source.hidden,
                generated = source.generated,
                stale = source.stale,
                sourcePath = source.sourcePath
            };
        }

        private static PungentUtilityHelpTroubleshootingEntry Clone(PungentUtilityHelpTroubleshootingEntry source)
        {
            if (source == null)
                return null;
            return new PungentUtilityHelpTroubleshootingEntry
            {
                id = source.id,
                symptom = source.symptom,
                likelyCause = source.likelyCause,
                nextStep = source.nextStep
            };
        }

        private enum HelpLayer
        {
            Generated,
            BuiltIn,
            Manual
        }
    }
#endif
}
