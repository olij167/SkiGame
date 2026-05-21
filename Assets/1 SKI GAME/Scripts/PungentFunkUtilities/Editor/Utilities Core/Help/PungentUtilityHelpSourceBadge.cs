namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEngine;

    public static class PungentUtilityHelpSourceBadge
    {
        public static void DrawTopicBadges(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
            {
                UtilityWindowTheme.CountPill("Missing", UtilityWindowTheme.Red, 70f);
                return;
            }

            if (HasManualTopicOverride(topic))
                UtilityWindowTheme.CountPill("Manual Override", UtilityWindowTheme.Purple, 116f);
            else if (topic.generated)
                UtilityWindowTheme.CountPill("Generated", UtilityWindowTheme.Amber, 84f);
            else
                UtilityWindowTheme.CountPill("Curated", UtilityWindowTheme.Green, 72f);

            if (topic.developerOnly)
                UtilityWindowTheme.CountPill("Developer Only", UtilityWindowTheme.Purple, 112f);
            if (topic.hidden)
                UtilityWindowTheme.CountPill("Hidden", UtilityWindowTheme.Red, 64f);
            if (string.IsNullOrWhiteSpace(topic.summary) || string.Equals(topic.summary, "This topic has not been written yet.", StringComparison.OrdinalIgnoreCase))
                UtilityWindowTheme.CountPill("Draft", UtilityWindowTheme.Amber, 58f);
        }

        public static void DrawFeatureBadges(PungentUtilityHelpTopic topic, PungentUtilityHelpFeatureEntry entry)
        {
            if (entry == null)
            {
                UtilityWindowTheme.CountPill("Missing", UtilityWindowTheme.Red, 70f);
                return;
            }

            if (HasManualFeatureOverride(topic, entry.id))
                UtilityWindowTheme.CountPill("Manual Override", UtilityWindowTheme.Purple, 116f);
            else
                UtilityWindowTheme.CountPill(entry.generated ? "Generated" : "Curated", entry.generated ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, entry.generated ? 84f : 72f);
            if (entry.developerOnly)
                UtilityWindowTheme.CountPill("Developer Only", UtilityWindowTheme.Purple, 112f);
            if (entry.hidden)
                UtilityWindowTheme.CountPill("Hidden", UtilityWindowTheme.Red, 64f);
            if (entry.ignoredGenerated)
                UtilityWindowTheme.CountPill("Ignored", UtilityWindowTheme.Neutral, 68f);
            if (entry.needsBetterWording)
                UtilityWindowTheme.CountPill("Needs Wording", UtilityWindowTheme.Amber, 112f);
            if (!string.IsNullOrWhiteSpace(entry.sourceConfidence))
                UtilityWindowTheme.CountPill("Confidence " + entry.sourceConfidence, ConfidenceTint(entry.sourceConfidence), 116f);
        }

        public static void DrawFeatureBadges(PungentUtilityHelpFeatureEntry entry)
        {
            DrawFeatureBadges(null, entry);
        }

        public static void DrawScriptingBadges(PungentUtilityHelpTopic topic, PungentUtilityHelpScriptingEntry entry)
        {
            if (entry == null)
            {
                UtilityWindowTheme.CountPill("Missing", UtilityWindowTheme.Red, 70f);
                return;
            }

            if (HasManualScriptingOverride(topic, entry.id))
                UtilityWindowTheme.CountPill("Manual Override", UtilityWindowTheme.Purple, 116f);
            else if (entry.generated)
                UtilityWindowTheme.CountPill("Generated", UtilityWindowTheme.Amber, 84f);
            else
                UtilityWindowTheme.CountPill("Curated", UtilityWindowTheme.Green, 72f);

            if (entry.developerOnly)
                UtilityWindowTheme.CountPill("Developer Only", UtilityWindowTheme.Purple, 112f);
            if (entry.hidden)
                UtilityWindowTheme.CountPill("Hidden", UtilityWindowTheme.Red, 64f);
            if (entry.stale)
                UtilityWindowTheme.CountPill("Stale", UtilityWindowTheme.Red, 54f);
            if (string.IsNullOrWhiteSpace(entry.description) || entry.description.IndexOf("awaiting developer review", StringComparison.OrdinalIgnoreCase) >= 0)
                UtilityWindowTheme.CountPill("Draft", UtilityWindowTheme.Amber, 58f);
        }

        private static bool HasManualTopicOverride(PungentUtilityHelpTopic topic)
        {
            return topic != null &&
                   PungentUtilityHelpStorage.instance.manualTopics != null &&
                   PungentUtilityHelpStorage.instance.manualTopics.Any(t => t != null && string.Equals(t.StableId, topic.StableId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasManualScriptingOverride(PungentUtilityHelpTopic topic, string entryId)
        {
            if (topic == null || string.IsNullOrWhiteSpace(entryId) || PungentUtilityHelpStorage.instance.manualTopics == null)
                return false;

            PungentUtilityHelpTopic manual = PungentUtilityHelpStorage.instance.manualTopics.FirstOrDefault(t => t != null && string.Equals(t.StableId, topic.StableId, StringComparison.OrdinalIgnoreCase));
            return manual != null &&
                   manual.scriptingEntries != null &&
                   manual.scriptingEntries.Any(e => e != null && string.Equals(e.id, entryId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasManualFeatureOverride(PungentUtilityHelpTopic topic, string entryId)
        {
            if (topic == null || string.IsNullOrWhiteSpace(entryId) || PungentUtilityHelpStorage.instance.manualTopics == null)
                return false;

            PungentUtilityHelpTopic manual = PungentUtilityHelpStorage.instance.manualTopics.FirstOrDefault(t => t != null && string.Equals(t.StableId, topic.StableId, StringComparison.OrdinalIgnoreCase));
            return manual != null &&
                   manual.featureEntries != null &&
                   manual.featureEntries.Any(e => e != null && string.Equals(e.id, entryId, StringComparison.OrdinalIgnoreCase));
        }

        private static Color ConfidenceTint(string confidence)
        {
            if (string.Equals(confidence, "high", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Green;
            if (string.Equals(confidence, "medium", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Amber;
            if (string.Equals(confidence, "low", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Red;
            return UtilityWindowTheme.Neutral;
        }
    }
#endif
}
