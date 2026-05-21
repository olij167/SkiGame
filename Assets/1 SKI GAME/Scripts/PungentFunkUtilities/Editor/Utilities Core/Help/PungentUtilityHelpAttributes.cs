namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor)]
    public sealed class PungentHelpIncludeAttribute : Attribute
    {
        public PungentHelpIncludeAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor)]
    public sealed class PungentHelpHideAttribute : Attribute
    {
        public PungentHelpHideAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = true)]
    public sealed class PungentHelpTopicAttribute : Attribute
    {
        public readonly string UtilityId;
        public readonly string SectionId;
        public readonly string TopicId;

        public PungentHelpTopicAttribute(string utilityId, string sectionId = null, string topicId = null)
        {
            UtilityId = utilityId ?? string.Empty;
            SectionId = sectionId ?? PungentUtilityHelpIds.DefaultSection;
            TopicId = topicId ?? PungentUtilityHelpIds.DefaultTopic;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class PungentScriptingReferenceAttribute : Attribute
    {
        public readonly string Description;
        public readonly string UsageNotes;
        public readonly string MinimalExample;
        public readonly string WhereItAppears;

        public PungentScriptingReferenceAttribute(string description = null, string usageNotes = null, string minimalExample = null, string whereItAppears = null)
        {
            Description = description ?? string.Empty;
            UsageNotes = usageNotes ?? string.Empty;
            MinimalExample = minimalExample ?? string.Empty;
            WhereItAppears = whereItAppears ?? string.Empty;
        }
    }
#endif
}
