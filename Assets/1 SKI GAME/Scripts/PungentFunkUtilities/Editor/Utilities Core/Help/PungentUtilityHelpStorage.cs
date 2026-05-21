namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    [FilePath("ProjectSettings/PungentFunkUtilities/UtilityHelp.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityHelpStorage : ScriptableSingleton<PungentUtilityHelpStorage>
    {
        public List<PungentUtilityHelpTopic> manualTopics = new List<PungentUtilityHelpTopic>();
        public List<PungentUtilityHelpTopic> generatedTopics = new List<PungentUtilityHelpTopic>();
        public List<PungentUtilityHelpContext> contextualHelpContexts = new List<PungentUtilityHelpContext>();
        public List<PungentUtilityHelpCoverageDecision> coverageDecisions = new List<PungentUtilityHelpCoverageDecision>();
        public string lastGeneratedUtc = string.Empty;
        public string lastGeneratedStatus = "Generated scripting index has not been built yet.";
        public string lastTooltipGeneratedUtc = string.Empty;
        public string lastTooltipGeneratedStatus = "Tooltip index has not been built yet.";
        public string lastRegistryGeneratedUtc = string.Empty;
        public string lastRegistryGeneratedStatus = "Registry overview topics have not been generated yet.";
        public string lastContextGeneratedUtc = string.Empty;
        public string lastContextGeneratedStatus = "Contextual help buttons have not been indexed yet.";

        public static string StorageLocation => "ProjectSettings/PungentFunkUtilities/UtilityHelp.asset";

        public void Persist()
        {
            EnsureLists();
            Save(true);
            PungentUtilityHelpRegistry.Rebuild();
        }

        public PungentUtilityHelpTopic GetOrCreateManualTopic(string utilityId, string sectionId, string topicId)
        {
            EnsureLists();
            string key = PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId);
            PungentUtilityHelpTopic topic = manualTopics.FirstOrDefault(t => t != null && string.Equals(t.StableId, key, StringComparison.OrdinalIgnoreCase));
            if (topic != null)
                return topic;

            topic = new PungentUtilityHelpTopic
            {
                utilityId = PungentUtilityHelpIds.Normalize(utilityId),
                sectionId = PungentUtilityHelpIds.Normalize(sectionId, PungentUtilityHelpIds.DefaultSection),
                topicId = PungentUtilityHelpIds.Normalize(topicId, PungentUtilityHelpIds.DefaultTopic),
                title = ObjectNames.NicifyVariableName(PungentUtilityHelpIds.Normalize(topicId, utilityId)),
                summary = "This topic has not been written yet.",
                sourceOwner = "Manual override",
                lastUpdatedUtc = DateTime.UtcNow.ToString("o")
            };
            manualTopics.Add(topic);
            Persist();
            return topic;
        }

        public void RemoveManualTopic(string stableId)
        {
            EnsureLists();
            if (string.IsNullOrWhiteSpace(stableId))
                return;

            manualTopics.RemoveAll(t => t != null && string.Equals(t.StableId, stableId, StringComparison.OrdinalIgnoreCase));
            Persist();
        }

        public void ReplaceGeneratedTopics(IEnumerable<PungentUtilityHelpTopic> topics, string status)
        {
            ReplaceGeneratedTopicsByOwner(null, topics, status);
        }

        public void ReplaceGeneratedTopicsByOwner(string sourceOwner, IEnumerable<PungentUtilityHelpTopic> topics, string status)
        {
            EnsureLists();
            List<PungentUtilityHelpTopic> next = topics == null
                ? new List<PungentUtilityHelpTopic>()
                : topics.Where(t => t != null).ToList();

            if (string.IsNullOrWhiteSpace(sourceOwner))
            {
                generatedTopics = next;
            }
            else
            {
                generatedTopics.RemoveAll(t => t != null && string.Equals(t.sourceOwner, sourceOwner, StringComparison.OrdinalIgnoreCase));
                generatedTopics.AddRange(next);
            }

            lastGeneratedUtc = DateTime.UtcNow.ToString("o");
            lastGeneratedStatus = string.IsNullOrWhiteSpace(status) ? "Generated scripting index refreshed." : status;
            Persist();
        }

        public void ReplaceContextualHelpContexts(IEnumerable<PungentUtilityHelpContext> contexts, string status)
        {
            EnsureLists();
            contextualHelpContexts = contexts == null
                ? new List<PungentUtilityHelpContext>()
                : contexts.Where(c => c != null).ToList();
            foreach (PungentUtilityHelpContext context in contextualHelpContexts)
                context.Normalize();
            lastContextGeneratedUtc = DateTime.UtcNow.ToString("o");
            lastContextGeneratedStatus = string.IsNullOrWhiteSpace(status) ? "Contextual help buttons indexed." : status;
            Save(true);
        }

        public PungentUtilityHelpCoverageDecision GetCoverageDecision(string utilityId, string topicStableId, string entryId, PungentUtilityHelpCoverageDecisionKind kind)
        {
            EnsureLists();
            string key = PungentUtilityHelpCoverageDecision.BuildKey(utilityId, topicStableId, entryId, kind);
            return coverageDecisions.FirstOrDefault(d => d != null && string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasCoverageDecisionState(string utilityId, string topicStableId, string entryId, PungentUtilityHelpCoverageDecisionKind kind, params PungentUtilityHelpCoverageDecisionState[] states)
        {
            PungentUtilityHelpCoverageDecision decision = GetCoverageDecision(utilityId, topicStableId, entryId, kind);
            return decision != null && states != null && states.Contains(decision.state);
        }

        public void SetCoverageDecision(string utilityId, string topicStableId, string entryId, PungentUtilityHelpCoverageDecisionKind kind, PungentUtilityHelpCoverageDecisionState state, string notes)
        {
            EnsureLists();
            string key = PungentUtilityHelpCoverageDecision.BuildKey(utilityId, topicStableId, entryId, kind);
            PungentUtilityHelpCoverageDecision decision = coverageDecisions.FirstOrDefault(d => d != null && string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
            if (decision == null)
            {
                decision = new PungentUtilityHelpCoverageDecision
                {
                    utilityId = PungentUtilityHelpIds.Normalize(utilityId),
                    topicStableId = topicStableId ?? string.Empty,
                    entryId = entryId ?? string.Empty,
                    kind = kind
                };
                coverageDecisions.Add(decision);
            }

            decision.state = state;
            decision.notes = notes ?? string.Empty;
            decision.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
            Save(true);
        }

        public void ClearCoverageDecision(string utilityId, string topicStableId, string entryId, PungentUtilityHelpCoverageDecisionKind kind)
        {
            EnsureLists();
            string key = PungentUtilityHelpCoverageDecision.BuildKey(utilityId, topicStableId, entryId, kind);
            coverageDecisions.RemoveAll(d => d != null && string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
            Save(true);
        }

        private void EnsureLists()
        {
            if (manualTopics == null)
                manualTopics = new List<PungentUtilityHelpTopic>();
            if (generatedTopics == null)
                generatedTopics = new List<PungentUtilityHelpTopic>();
            if (contextualHelpContexts == null)
                contextualHelpContexts = new List<PungentUtilityHelpContext>();
            if (coverageDecisions == null)
                coverageDecisions = new List<PungentUtilityHelpCoverageDecision>();
        }
    }
#endif
}
