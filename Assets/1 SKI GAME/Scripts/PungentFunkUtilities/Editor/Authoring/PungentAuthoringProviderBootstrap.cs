using UnityEditor;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class PungentAuthoringProviderBootstrap
    {
        static PungentAuthoringProviderBootstrap()
        {
            RegisterBuiltInProviders();
        }

        public static void RegisterBuiltInProviders()
        {
            PungentAuthoringProviderRegistry.Register(new PungentAuthoringLegacyNoteProvider());
            PungentAuthoringProviderRegistry.Register(new PungentAuthoringDocumentationLinkProvider());
            PungentAuthoringProviderRegistry.Register(new PungentAuthoringUtilityProvider());
            PungentAuthoringProviderRegistry.Register(new PungentAuthoringTokenProvider());
            PungentAuthoringProviderRegistry.Register(new PungentAuthoringHelpTopicProvider());
            PungentAuthoringProviderRegistry.Register(new PungentAuthoringAuditIssueProvider());
        }
    }
#endif
}
