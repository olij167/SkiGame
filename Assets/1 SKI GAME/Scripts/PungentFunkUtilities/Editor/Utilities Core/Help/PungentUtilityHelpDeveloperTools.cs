namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Linq;
    using UnityEditor;

    internal static class PungentUtilityHelpDeveloperTools
    {
        public static PungentUtilityHelpTopic GetEditableTopic(PungentUtilityHelpTopic source)
        {
            if (source == null)
                return null;

            return PungentUtilityHelpStorage.instance.GetOrCreateManualTopic(source.utilityId, source.sectionId, source.topicId);
        }

        public static PungentUtilityHelpScriptingEntry GetEditableScriptingEntry(PungentUtilityHelpTopic source, PungentUtilityHelpScriptingEntry sourceEntry)
        {
            if (source == null || sourceEntry == null)
                return null;

            PungentUtilityHelpTopic manual = GetOrCreateManualTopicShell(source);
            if (manual.scriptingEntries == null)
                manual.scriptingEntries = new System.Collections.Generic.List<PungentUtilityHelpScriptingEntry>();
            PungentUtilityHelpScriptingEntry entry = manual.scriptingEntries.FirstOrDefault(e => e != null && string.Equals(e.id, sourceEntry.id, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                return entry;

            entry = new PungentUtilityHelpScriptingEntry
            {
                id = sourceEntry.id,
                declaringType = sourceEntry.declaringType,
                memberName = sourceEntry.memberName,
                signature = sourceEntry.signature,
                description = sourceEntry.description,
                usageNotes = sourceEntry.usageNotes,
                minimalExample = sourceEntry.minimalExample,
                whereItAppears = sourceEntry.whereItAppears,
                developerOnly = sourceEntry.developerOnly,
                hidden = sourceEntry.hidden,
                generated = sourceEntry.generated,
                stale = sourceEntry.stale,
                sourcePath = sourceEntry.sourcePath
            };
            manual.scriptingEntries.Add(entry);
            PungentUtilityHelpStorage.instance.Persist();
            return entry;
        }

        public static PungentUtilityHelpFeatureEntry GetEditableFeatureEntry(PungentUtilityHelpTopic source, PungentUtilityHelpFeatureEntry sourceEntry)
        {
            if (source == null || sourceEntry == null)
                return null;

            PungentUtilityHelpTopic manual = GetOrCreateManualTopicShell(source);
            if (manual.featureEntries == null)
                manual.featureEntries = new System.Collections.Generic.List<PungentUtilityHelpFeatureEntry>();
            PungentUtilityHelpFeatureEntry entry = manual.featureEntries.FirstOrDefault(e => e != null && string.Equals(e.id, sourceEntry.id, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                return entry;

            entry = new PungentUtilityHelpFeatureEntry
            {
                id = sourceEntry.id,
                label = sourceEntry.label,
                description = sourceEntry.description,
                location = sourceEntry.location,
                safetyNotes = sourceEntry.safetyNotes,
                sourcePath = sourceEntry.sourcePath,
                sourceLine = sourceEntry.sourceLine,
                sourceConfidence = sourceEntry.sourceConfidence,
                needsBetterWording = sourceEntry.needsBetterWording,
                ignoredGenerated = sourceEntry.ignoredGenerated,
                developerOnly = sourceEntry.developerOnly,
                hidden = sourceEntry.hidden,
                generated = sourceEntry.generated
            };
            manual.featureEntries.Add(entry);
            PungentUtilityHelpStorage.instance.Persist();
            return entry;
        }

        public static void MarkShippable(PungentUtilityHelpTopic source, PungentUtilityHelpScriptingEntry sourceEntry)
        {
            PungentUtilityHelpScriptingEntry entry = GetEditableScriptingEntry(source, sourceEntry);
            if (entry == null)
                return;

            entry.developerOnly = false;
            entry.hidden = false;
            entry.generated = false;
            Touch(source);
        }

        public static void HideFromShippable(PungentUtilityHelpTopic source, PungentUtilityHelpScriptingEntry sourceEntry)
        {
            PungentUtilityHelpScriptingEntry entry = GetEditableScriptingEntry(source, sourceEntry);
            if (entry == null)
                return;

            entry.hidden = true;
            Touch(source);
        }

        public static void MarkFeatureShippable(PungentUtilityHelpTopic source, PungentUtilityHelpFeatureEntry sourceEntry)
        {
            PungentUtilityHelpFeatureEntry entry = GetEditableFeatureEntry(source, sourceEntry);
            if (entry == null)
                return;

            entry.developerOnly = false;
            entry.hidden = false;
            entry.generated = false;
            entry.ignoredGenerated = false;
            entry.needsBetterWording = false;
            Touch(source);
        }

        public static void HideFeatureFromShippable(PungentUtilityHelpTopic source, PungentUtilityHelpFeatureEntry sourceEntry)
        {
            PungentUtilityHelpFeatureEntry entry = GetEditableFeatureEntry(source, sourceEntry);
            if (entry == null)
                return;

            entry.hidden = true;
            Touch(source);
        }

        public static void MarkFeatureNeedsBetterWording(PungentUtilityHelpTopic source, PungentUtilityHelpFeatureEntry sourceEntry)
        {
            PungentUtilityHelpFeatureEntry entry = GetEditableFeatureEntry(source, sourceEntry);
            if (entry == null)
                return;

            entry.needsBetterWording = true;
            entry.developerOnly = true;
            Touch(source);
        }

        public static void IgnoreFeatureFalsePositive(PungentUtilityHelpTopic source, PungentUtilityHelpFeatureEntry sourceEntry)
        {
            PungentUtilityHelpFeatureEntry entry = GetEditableFeatureEntry(source, sourceEntry);
            if (entry == null)
                return;

            entry.ignoredGenerated = true;
            entry.hidden = true;
            entry.developerOnly = true;
            Touch(source);
        }

        public static void ResetScriptingEntryToGenerated(PungentUtilityHelpTopic source, PungentUtilityHelpScriptingEntry sourceEntry)
        {
            if (source == null || sourceEntry == null)
                return;

            if (PungentUtilityHelpStorage.instance.manualTopics == null)
                return;

            PungentUtilityHelpTopic manual = PungentUtilityHelpStorage.instance.manualTopics.FirstOrDefault(t => t != null && string.Equals(t.StableId, source.StableId, StringComparison.OrdinalIgnoreCase));
            if (manual == null)
                return;

            if (manual.scriptingEntries == null)
                return;
            manual.scriptingEntries.RemoveAll(e => e != null && string.Equals(e.id, sourceEntry.id, StringComparison.OrdinalIgnoreCase));

            if (IsEmptyShell(manual))
                PungentUtilityHelpStorage.instance.manualTopics.Remove(manual);
            else
                manual.lastUpdatedUtc = DateTime.UtcNow.ToString("o");

            PungentUtilityHelpStorage.instance.Persist();
        }

        public static void ResetFeatureEntryToGenerated(PungentUtilityHelpTopic source, PungentUtilityHelpFeatureEntry sourceEntry)
        {
            if (source == null || sourceEntry == null)
                return;

            if (PungentUtilityHelpStorage.instance.manualTopics == null)
                return;

            PungentUtilityHelpTopic manual = PungentUtilityHelpStorage.instance.manualTopics.FirstOrDefault(t => t != null && string.Equals(t.StableId, source.StableId, StringComparison.OrdinalIgnoreCase));
            if (manual == null || manual.featureEntries == null)
                return;

            manual.featureEntries.RemoveAll(e => e != null && string.Equals(e.id, sourceEntry.id, StringComparison.OrdinalIgnoreCase));

            if (IsEmptyShell(manual))
                PungentUtilityHelpStorage.instance.manualTopics.Remove(manual);
            else
                manual.lastUpdatedUtc = DateTime.UtcNow.ToString("o");

            PungentUtilityHelpStorage.instance.Persist();
        }

        public static void CopyTopicId(PungentUtilityHelpTopic topic)
        {
            if (topic != null)
                EditorGUIUtility.systemCopyBuffer = topic.StableId;
        }

        public static void CopyHelpLink(PungentUtilityHelpTopic topic)
        {
            if (topic != null)
                EditorGUIUtility.systemCopyBuffer = "pungent-help://" + topic.StableId;
        }

        private static void Touch(PungentUtilityHelpTopic source)
        {
            PungentUtilityHelpTopic manual = GetOrCreateManualTopicShell(source);
            if (manual != null)
                manual.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
            PungentUtilityHelpStorage.instance.Persist();
        }

        private static PungentUtilityHelpTopic GetOrCreateManualTopicShell(PungentUtilityHelpTopic source)
        {
            if (source == null)
                return null;

            if (PungentUtilityHelpStorage.instance.manualTopics == null)
                PungentUtilityHelpStorage.instance.manualTopics = new System.Collections.Generic.List<PungentUtilityHelpTopic>();

            string key = source.StableId;
            PungentUtilityHelpTopic manual = PungentUtilityHelpStorage.instance.manualTopics.FirstOrDefault(t => t != null && string.Equals(t.StableId, key, StringComparison.OrdinalIgnoreCase));
            if (manual != null)
                return manual;

            manual = new PungentUtilityHelpTopic
            {
                utilityId = source.utilityId,
                sectionId = source.sectionId,
                topicId = source.topicId,
                sourceOwner = "Manual scripting override",
                lastUpdatedUtc = DateTime.UtcNow.ToString("o")
            };
            PungentUtilityHelpStorage.instance.manualTopics.Add(manual);
            PungentUtilityHelpStorage.instance.Persist();
            return manual;
        }

        private static bool IsEmptyShell(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return true;

            return string.IsNullOrWhiteSpace(topic.title) &&
                   string.IsNullOrWhiteSpace(topic.summary) &&
                   string.IsNullOrWhiteSpace(topic.quickUseMarkdown) &&
                   (topic.featureEntries == null || topic.featureEntries.Count == 0) &&
                   (topic.scriptingEntries == null || topic.scriptingEntries.Count == 0) &&
                   (topic.troubleshootingEntries == null || topic.troubleshootingEntries.Count == 0) &&
                   (topic.relatedTopicIds == null || topic.relatedTopicIds.Count == 0) &&
                   (topic.relatedUtilityIds == null || topic.relatedUtilityIds.Count == 0) &&
                   (topic.relatedNoteIds == null || topic.relatedNoteIds.Count == 0) &&
                   (topic.relatedDocumentationLinkIds == null || topic.relatedDocumentationLinkIds.Count == 0) &&
                   (topic.tags == null || topic.tags.Count == 0) &&
                   !topic.developerOnly &&
                   !topic.hidden &&
                   !topic.generated;
        }
    }
#endif
}
