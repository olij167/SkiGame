namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Developer;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;

    [Serializable]
    public sealed class PungentUtilityHelpTopic
    {
        public string utilityId = string.Empty;
        public string sectionId = string.Empty;
        public string topicId = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
        public string quickUseMarkdown = string.Empty;
        public List<PungentUtilityHelpFeatureEntry> featureEntries = new List<PungentUtilityHelpFeatureEntry>();
        public List<PungentUtilityHelpScriptingEntry> scriptingEntries = new List<PungentUtilityHelpScriptingEntry>();
        public List<PungentUtilityHelpTroubleshootingEntry> troubleshootingEntries = new List<PungentUtilityHelpTroubleshootingEntry>();
        public List<string> relatedTopicIds = new List<string>();
        public List<string> relatedUtilityIds = new List<string>();
        public List<string> relatedNoteIds = new List<string>();
        public List<string> relatedDocumentationLinkIds = new List<string>();
        public List<string> tags = new List<string>();
        public bool developerOnly;
        public bool hidden;
        public bool generated;
        public string sourceOwner = string.Empty;
        public string lastUpdatedUtc = string.Empty;

        public string StableId => PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId);
    }

    [Serializable]
    public sealed class PungentUtilityHelpFeatureEntry
    {
        public string id = string.Empty;
        public string label = string.Empty;
        public string description = string.Empty;
        public string location = string.Empty;
        public string safetyNotes = string.Empty;
        public string sourcePath = string.Empty;
        public int sourceLine;
        public string sourceConfidence = string.Empty;
        public bool needsBetterWording;
        public bool ignoredGenerated;
        public bool developerOnly;
        public bool hidden;
        public bool generated;
    }

    [Serializable]
    public sealed class PungentUtilityHelpScriptingEntry
    {
        public string id = string.Empty;
        public string declaringType = string.Empty;
        public string memberName = string.Empty;
        public string signature = string.Empty;
        public string description = string.Empty;
        public string usageNotes = string.Empty;
        public string minimalExample = string.Empty;
        public string whereItAppears = string.Empty;
        public bool developerOnly;
        public bool hidden;
        public bool generated;
        public bool stale;
        public string sourcePath = string.Empty;
    }

    [Serializable]
    public sealed class PungentUtilityHelpTroubleshootingEntry
    {
        public string id = string.Empty;
        public string symptom = string.Empty;
        public string likelyCause = string.Empty;
        public string nextStep = string.Empty;
    }

    internal static class PungentUtilityHelpIds
    {
        public const string DefaultSection = "overview";
        public const string DefaultTopic = "overview";

        public static string TopicKey(string utilityId, string sectionId, string topicId)
        {
            return Normalize(utilityId) + "/" + Normalize(sectionId, DefaultSection) + "/" + Normalize(topicId, DefaultTopic);
        }

        public static string Normalize(string value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback ?? string.Empty;

            return value.Trim().Replace('\\', '/').ToLowerInvariant();
        }
    }

    [Serializable]
    public sealed class PungentUtilityHelpGenerationReport
    {
        public string generatedUtc = string.Empty;
        public int utilitiesWithoutOverview;
        public int missingContextualTopics;
        public int topicsWithoutQuickUse;
        public int topicsWithoutFeatureEntries;
        public int topicsWithoutScriptingEntries;
        public int generatedDraftTopics;
        public int staleScriptingEntries;
        public int tooltipEntries;
        public int missingTooltipCandidates;
        public int registeredUtilities;
        public int utilitiesWithOverviewHelp;
        public int utilitiesWithHeaderHelp;
        public int utilitiesWithSectionHelp;
        public int utilitiesWithTooltipTopic;
        public int utilitiesWithScriptingEntries;
        public int generatedEntriesAwaitingReview;
        public int hiddenEntries;
        public int developerOnlyEntries;
        public int weakTooltipEntries;
        public int documentationLinkCount;
        public int documentationLinksWithCurrentTargets;
        public int documentationLinksMissingCurrentTargets;
        public int documentationLinksWithBacklog;
        public int globalDocumentationLinks;
        public int utilityAssignedDocumentationLinks;
        public int utilitiesWithDocumentationLinks;
        public int utilitiesWithoutDocumentationLinks;
        public int topicsWithExplicitDocumentationLinks;
        public int staleRelatedDocumentationLinkIds;
        public int documentationLinksHelpTopicsPresent;
        public int documentationLinksHelpTopicsMissing;
        public readonly List<PungentUtilityHelpGenerationIssue> issues = new List<PungentUtilityHelpGenerationIssue>();
        public readonly List<PungentUtilityHelpCoverageRow> coverageRows = new List<PungentUtilityHelpCoverageRow>();
    }

    [Serializable]
    public sealed class PungentUtilityHelpGenerationIssue
    {
        public string kind = string.Empty;
        public string utilityId = string.Empty;
        public string topicStableId = string.Empty;
        public string message = string.Empty;
        public string sourcePath = string.Empty;
        public int sourceLine;
    }

    [Serializable]
    public sealed class PungentUtilityHelpCoverageRow
    {
        public string utilityId = string.Empty;
        public string displayName = string.Empty;
        public bool hasOverviewHelp;
        public bool hasHeaderHelp;
        public bool hasSectionHelp;
        public bool hasTooltipTopic;
        public bool hasScriptingEntries;
        public int topicCount;
        public int contextualHelpButtonCount;
        public int tooltipEntryCount;
        public int scriptingEntryCount;
        public int generatedEntriesAwaitingReview;
        public int hiddenEntries;
        public int developerOnlyEntries;
        public int weakTooltipEntries;
        public int documentationLinkCount;
        public int missingDocumentationTargetCount;
        public bool hasDocumentationLinks;
        public PungentUtilityHelpCoverageUtilityKind utilityKind;
        public PungentUtilityHelpCoverageExpectation coverageExpectation;
        public PungentUtilityHelpCoverageStatus coverageStatus;
        public bool missingHeaderIsActionable;
        public bool missingSectionIsActionable;
        public bool missingTooltipIsActionable;
        public bool missingScriptingIsActionable;
        public string tooltipCoverageState = string.Empty;
        public string scriptingCoverageState = string.Empty;
        public int reviewPriority;
        public string coverageNotes = string.Empty;
    }

    public enum PungentUtilityHelpCoverageUtilityKind
    {
        Unknown,
        WindowUtility,
        PopupUtility,
        CommandUtility,
        ContextMenuAction,
        CreateAssetAction,
        SceneTool,
        InspectorTool,
        ProviderOnly,
        DeveloperOnly,
        Internal
    }

    public enum PungentUtilityHelpCoverageExpectation
    {
        Unknown,
        FullWindowHelp,
        PopupHelp,
        OverviewOnly,
        ContextActionHelp,
        NotApplicable,
        DeveloperOnly
    }

    public enum PungentUtilityHelpCoverageStatus
    {
        Complete,
        Acceptable,
        NeedsReview,
        NeedsHeaderHelp,
        NeedsSectionHelp,
        NeedsTooltipReview,
        NeedsScriptingReview,
        MissingDocs,
        Ignored,
        DeveloperOnly
    }

    public enum PungentUtilityHelpGeneratedLifecycleState
    {
        GeneratedDraft,
        NeedsReview,
        NeedsBetterWording,
        Reviewed,
        PromotedToCurated,
        Hidden,
        IgnoredFalsePositive,
        Stale,
        NotApplicable
    }

    public enum PungentUtilityHelpCoverageDecisionKind
    {
        Coverage,
        HeaderHelp,
        SectionHelp,
        TooltipCoverage,
        ScriptingCoverage,
        DocumentationLinks,
        GeneratedTopic,
        FeatureEntry,
        ScriptingEntry
    }

    public enum PungentUtilityHelpCoverageDecisionState
    {
        None,
        Reviewed,
        Ignored,
        NotApplicable,
        Hidden,
        NeedsWording,
        PromotedToCurated
    }

    public enum PungentUtilityHelpReviewQueueKind
    {
        GeneratedDraftTopics,
        GeneratedTooltipEntries,
        WeakTooltips,
        GeneratedScriptingEntries,
        MissingHeaderHelp,
        MissingSectionHelp,
        MissingScripting,
        DocumentationLinkIssues,
        IgnoredHiddenEntries,
        CoverageComplete
    }

    [Serializable]
    public sealed class PungentUtilityHelpCoverageDecision
    {
        public string utilityId = string.Empty;
        public string topicStableId = string.Empty;
        public string entryId = string.Empty;
        public PungentUtilityHelpCoverageDecisionKind kind;
        public PungentUtilityHelpCoverageDecisionState state;
        public string notes = string.Empty;
        public string lastUpdatedUtc = string.Empty;

        public string Key => BuildKey(utilityId, topicStableId, entryId, kind);

        public static string BuildKey(string utilityId, string topicStableId, string entryId, PungentUtilityHelpCoverageDecisionKind kind)
        {
            return PungentUtilityHelpIds.Normalize(utilityId) + "|" +
                   (topicStableId ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                   (entryId ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                   kind;
        }
    }

    public sealed class PungentUtilityHelpReviewRow
    {
        public PungentUtilityHelpReviewQueueKind queueKind;
        public string utilityId = string.Empty;
        public string displayName = string.Empty;
        public string topicStableId = string.Empty;
        public string entryId = string.Empty;
        public string entryLabel = string.Empty;
        public string message = string.Empty;
        public string sourceOwner = string.Empty;
        public string sourcePath = string.Empty;
        public int sourceLine;
        public PungentUtilityHelpGeneratedLifecycleState lifecycleState;
        public int priority;
        public PungentUtilityHelpCoverageUtilityKind utilityKind;
        public PungentUtilityHelpCoverageStatus coverageStatus;
        public PungentUtilityHelpFeatureEntry featureEntry;
        public PungentUtilityHelpScriptingEntry scriptingEntry;
        public PungentUtilityHelpTopic topic;
        public PungentUtilityHelpCoverageRow coverageRow;

        public string Key => queueKind + "|" + utilityId + "|" + topicStableId + "|" + entryId + "|" + message;
    }

    public enum PungentBugReportCategory
    {
        Bug,
        CompileError,
        UIUXIssue,
        DocumentationIssue,
        HelpContentIssue,
        PerformanceIssue,
        MissingFeature,
        ConfusingWorkflow,
        CompatibilityIssue,
        DataLossOrDestructiveBehaviour,
        Other
    }

    public enum PungentBugReportSeverity
    {
        Low,
        Normal,
        High,
        Blocking,
        DataLoss
    }

    [Serializable]
    public sealed class PungentBugReportContext
    {
        public string utilityId = string.Empty;
        public string sectionId = string.Empty;
        public string topicId = string.Empty;
        public string contextLabel = string.Empty;
        public string contextPath = string.Empty;
        public string sourceWindow = string.Empty;
        public string helpTab = string.Empty;
        public string selectedContextId = string.Empty;
        public string selectedContextLabel = string.Empty;
        public string sourcePath = string.Empty;
        public int sourceLine;
        public string generatedEntryId = string.Empty;

        public string StableTopicId => PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId);

        public void Normalize()
        {
            utilityId = PungentUtilityHelpIds.Normalize(utilityId);
            sectionId = PungentUtilityHelpIds.Normalize(sectionId, PungentUtilityHelpIds.DefaultSection);
            topicId = PungentUtilityHelpIds.Normalize(topicId, PungentUtilityHelpIds.DefaultTopic);
            contextLabel = Clean(contextLabel);
            contextPath = Clean(contextPath);
            sourceWindow = Clean(sourceWindow);
            helpTab = Clean(helpTab);
            selectedContextId = Clean(selectedContextId);
            selectedContextLabel = Clean(selectedContextLabel);
            sourcePath = Clean(sourcePath);
            generatedEntryId = Clean(generatedEntryId);
        }

        public static PungentBugReportContext FromHelpContext(PungentUtilityHelpContext context, string sourceWindow = "Contextual Help")
        {
            if (context == null)
                return new PungentBugReportContext { sourceWindow = sourceWindow ?? string.Empty };

            PungentBugReportContext reportContext = new PungentBugReportContext
            {
                utilityId = context.utilityId,
                sectionId = context.sectionId,
                topicId = context.topicId,
                contextLabel = context.label,
                contextPath = string.IsNullOrWhiteSpace(context.location) ? context.StableTopicId : context.location,
                sourceWindow = sourceWindow ?? string.Empty,
                selectedContextId = context.selectedContextId,
                selectedContextLabel = context.selectedContextLabel,
                sourcePath = context.sourcePath,
                sourceLine = context.sourceLine
            };
            reportContext.Normalize();
            return reportContext;
        }

        private static string Clean(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBugReportPayload
    {
        public string schemaVersion = "1.1";
        public string title = string.Empty;
        public string details = string.Empty;
        public PungentBugReportCategory category = PungentBugReportCategory.Bug;
        public PungentBugReportSeverity severity = PungentBugReportSeverity.Normal;
        public string categoryLabel = string.Empty;
        public string severityLabel = string.Empty;
        public string utilityId = string.Empty;
        public string sectionId = string.Empty;
        public string topicId = string.Empty;
        public string contextLabel = string.Empty;
        public string contextPath = string.Empty;
        public string sourceWindow = string.Empty;
        public string helpTab = string.Empty;
        public string packageVersion = string.Empty;
        public string unityVersion = string.Empty;
        public string editorPlatform = string.Empty;
        public string timestampUtc = string.Empty;
        public string anonymousInstallId = string.Empty;
        public string installId = string.Empty;
        public bool includeDiagnostics;
        public bool includeConsoleSummary;
        public string contactEmail = string.Empty;
        public string email = string.Empty;
        public string contactDiscord = string.Empty;
        public string diagnosticsSummary = string.Empty;
        public string recentExceptionSummary = string.Empty;
        public string backendEndpoint = string.Empty;
        public string backendStatus = string.Empty;
        public string parentLocalId = string.Empty;
        public string parentRemoteReportId = string.Empty;
    }

    [Serializable]
    public sealed class PungentBugReportDraftRecord
    {
        public string id = string.Empty;
        public string createdUtc = string.Empty;
        public string status = string.Empty;
        public PungentBugReportPayload payload = new PungentBugReportPayload();
    }

    public enum PungentSupportRequestRelayState
    {
        Draft,
        Queued,
        Failed,
        Sent
    }

    [Serializable]
    public sealed class PungentSupportRequestRecord
    {
        public string noteId = string.Empty;
        public PungentSupportRequestRelayState state = PungentSupportRequestRelayState.Draft;
        public PungentBugReportCategory category = PungentBugReportCategory.Bug;
        public PungentBugReportSeverity severity = PungentBugReportSeverity.Normal;
        public string contactEmail = string.Empty;
        public string contactDiscord = string.Empty;
        public bool includeDiagnostics;
        public bool includeConsoleSummary;
        public string remoteReportId = string.Empty;
        public string sentUtc = string.Empty;
        public string parentNoteId = string.Empty;
        public string parentRemoteReportId = string.Empty;
        public string lastError = string.Empty;
        public string lastStatus = string.Empty;
        public string updatedUtc = string.Empty;
        public PungentBugReportContext context = new PungentBugReportContext();
        public PungentBugReportPayload queuedPayload = new PungentBugReportPayload();
    }

    [FilePath("ProjectSettings/PungentFunkUtilities/BugReports.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentBugReportStorage : ScriptableSingleton<PungentBugReportStorage>
    {
        public List<PungentBugReportDraftRecord> savedDrafts = new List<PungentBugReportDraftRecord>();
        public List<PungentBugReportDraftRecord> queuedReports = new List<PungentBugReportDraftRecord>();
        public List<PungentSupportRequestRecord> supportRequests = new List<PungentSupportRequestRecord>();
        public string lastEndpointUrl = string.Empty;
        public string anonymousInstallId = string.Empty;
        public string defaultContactEmail = string.Empty;
        public string defaultContactDiscord = string.Empty;
        public string lastReportTimestampUtc = string.Empty;
        public string lastSubmitStatus = string.Empty;
        public string lastSuccessfulReportId = string.Empty;
        public string lastSubmitError = string.Empty;
        public bool backendEnabled;
        public bool scaffoldAcknowledged;
        public bool relayDefaultsInitialized;

        public static string StorageLocation => "ProjectSettings/PungentFunkUtilities/BugReports.asset";

        public string EnsureAnonymousInstallId()
        {
            EnsureLists();
            if (string.IsNullOrWhiteSpace(anonymousInstallId))
            {
                anonymousInstallId = Guid.NewGuid().ToString("N");
                Persist();
            }
            return anonymousInstallId;
        }

        public string EffectiveEndpointUrl
        {
            get
            {
                EnsureDefaults();
                return string.IsNullOrWhiteSpace(lastEndpointUrl) ? PungentBugReportSettings.DefaultRelayEndpoint : lastEndpointUrl.Trim();
            }
        }

        public void EnsureDefaults()
        {
            EnsureLists();
            if (string.IsNullOrWhiteSpace(lastEndpointUrl))
                lastEndpointUrl = PungentBugReportSettings.DefaultRelayEndpoint;
            if (!relayDefaultsInitialized)
            {
                backendEnabled = true;
                relayDefaultsInitialized = true;
            }
        }

        public PungentBugReportDraftRecord SaveDraft(PungentBugReportPayload payload, string status)
        {
            EnsureDefaults();
            EnsureLists();
            PungentBugReportDraftRecord record = CreateRecord(payload, status);
            savedDrafts.Add(record);
            lastReportTimestampUtc = record.createdUtc;
            Persist();
            return record;
        }

        public PungentBugReportDraftRecord QueueReport(PungentBugReportPayload payload, string status)
        {
            EnsureDefaults();
            EnsureLists();
            PungentBugReportDraftRecord record = CreateRecord(payload, status);
            queuedReports.Add(record);
            lastReportTimestampUtc = record.createdUtc;
            Persist();
            return record;
        }

        public PungentSupportRequestRecord GetSupportRequest(string noteId)
        {
            EnsureDefaults();
            EnsureLists();
            if (string.IsNullOrWhiteSpace(noteId))
                return null;
            return supportRequests.FirstOrDefault(record => record != null && string.Equals(record.noteId, noteId, StringComparison.OrdinalIgnoreCase));
        }

        public PungentSupportRequestRecord GetOrCreateSupportRequest(string noteId)
        {
            EnsureDefaults();
            EnsureLists();
            if (string.IsNullOrWhiteSpace(noteId))
                return null;

            PungentSupportRequestRecord record = GetSupportRequest(noteId);
            if (record != null)
                return record;

            record = new PungentSupportRequestRecord
            {
                noteId = noteId,
                updatedUtc = DateTime.UtcNow.ToString("o"),
                context = new PungentBugReportContext()
            };
            supportRequests.Add(record);
            Persist();
            return record;
        }

        public void TouchSupportRequest(PungentSupportRequestRecord record)
        {
            if (record == null)
                return;
            EnsureDefaults();
            EnsureLists();
            if (!supportRequests.Contains(record))
                supportRequests.Add(record);
            record.updatedUtc = DateTime.UtcNow.ToString("o");
            Persist();
        }

        public void Persist()
        {
            EnsureDefaults();
            EnsureLists();
            Save(true);
        }

        private static PungentBugReportDraftRecord CreateRecord(PungentBugReportPayload payload, string status)
        {
            return new PungentBugReportDraftRecord
            {
                id = Guid.NewGuid().ToString("N"),
                createdUtc = DateTime.UtcNow.ToString("o"),
                status = string.IsNullOrWhiteSpace(status) ? "Saved locally." : status,
                payload = payload ?? new PungentBugReportPayload()
            };
        }

        private void EnsureLists()
        {
            if (savedDrafts == null)
                savedDrafts = new List<PungentBugReportDraftRecord>();
            if (queuedReports == null)
                queuedReports = new List<PungentBugReportDraftRecord>();
            if (supportRequests == null)
                supportRequests = new List<PungentSupportRequestRecord>();
        }
    }

    public static class PungentBugReportSettings
    {
        public const string DefaultRelayEndpoint = "https://www.pungentfunk.net/_functions/bugReport";
        public const string SuggestedBackendEndpoint = DefaultRelayEndpoint;
        public const string BackendStatus = "Wix relay / Discord webhook";
        public const string ScaffoldStatus = BackendStatus;
        public const string ScaffoldNotice = "Bug reports are sent to the configured PungentFunk Wix relay. The Unity package never stores the Discord webhook URL; Discord delivery is handled server-side.";
    }

    [Serializable]
    public sealed class PungentBugReportRelayResponse
    {
        public bool ok;
        public string id = string.Empty;
        public string message = string.Empty;
    }

    public static class PungentBugReportService
    {
        public static PungentBugReportPayload BuildPayload(
            PungentBugReportContext context,
            string title,
            string details,
            PungentBugReportCategory category,
            PungentBugReportSeverity severity,
            string contactEmail,
            string contactDiscord,
            bool includeDiagnostics,
            bool includeConsoleSummary)
        {
            context = context ?? new PungentBugReportContext();
            context.Normalize();

            return new PungentBugReportPayload
            {
                schemaVersion = "1.1",
                title = title ?? string.Empty,
                details = details ?? string.Empty,
                category = category,
                severity = severity,
                categoryLabel = category == PungentBugReportCategory.MissingFeature ? "Feature Request" : "Bug Report",
                severityLabel = severity.ToString(),
                utilityId = context.utilityId,
                sectionId = context.sectionId,
                topicId = context.topicId,
                contextLabel = context.contextLabel,
                contextPath = BuildContextPath(context, DeveloperContextVisible),
                sourceWindow = context.sourceWindow,
                helpTab = context.helpTab,
                packageVersion = GetPackageVersionLabel(),
                unityVersion = Application.unityVersion,
                editorPlatform = SystemInfo.operatingSystem,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                anonymousInstallId = PungentBugReportStorage.instance.EnsureAnonymousInstallId(),
                installId = PungentBugReportStorage.instance.EnsureAnonymousInstallId(),
                includeDiagnostics = includeDiagnostics,
                includeConsoleSummary = includeConsoleSummary,
                contactEmail = contactEmail ?? string.Empty,
                email = contactEmail ?? string.Empty,
                contactDiscord = contactDiscord ?? string.Empty,
                diagnosticsSummary = includeDiagnostics ? BuildDiagnosticsSummary(context, DeveloperContextVisible) : "Diagnostics not included.",
                recentExceptionSummary = includeConsoleSummary ? BuildConsoleSummary() : "Console summary not included.",
                backendEndpoint = PungentBugReportStorage.instance.EffectiveEndpointUrl,
                backendStatus = PungentBugReportSettings.BackendStatus
            };
        }

        public static string ToJson(PungentBugReportPayload payload)
        {
            return JsonUtility.ToJson(payload ?? new PungentBugReportPayload(), true);
        }

        public static void CopyPayloadToClipboard(PungentBugReportPayload payload)
        {
            EditorGUIUtility.systemCopyBuffer = ToJson(payload);
        }

        public static PungentBugReportDraftRecord SaveDraft(PungentBugReportPayload payload)
        {
            payload = payload ?? new PungentBugReportPayload();
            payload.backendStatus = PungentBugReportSettings.BackendStatus + " - saved draft locally.";
            return PungentBugReportStorage.instance.SaveDraft(payload, "Saved local bug report draft.");
        }

        public static PungentBugReportDraftRecord QueueLocally(PungentBugReportPayload payload)
        {
            payload = payload ?? new PungentBugReportPayload();
            payload.backendStatus = PungentBugReportSettings.BackendStatus + " - queued locally; send failed or was deferred.";
            return PungentBugReportStorage.instance.QueueReport(payload, "Queued locally; relay delivery was not completed.");
        }

        public static bool CanAttemptSubmit(out string reason)
        {
            PungentBugReportStorage storage = PungentBugReportStorage.instance;
            storage.EnsureDefaults();
            if (!storage.backendEnabled)
            {
                reason = "Bug report relay submission is disabled in local settings.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(storage.EffectiveEndpointUrl))
            {
                reason = "No Wix relay endpoint is configured.";
                return false;
            }

            reason = "Send this report to the configured Wix relay.";
            return true;
        }

        public static bool TrySubmit(PungentBugReportPayload payload, Action<bool, string, string> onComplete, out string status)
        {
            string reason;
            if (!CanAttemptSubmit(out reason))
            {
                status = reason;
                return false;
            }

            payload = payload ?? new PungentBugReportPayload();
            payload.backendEndpoint = PungentBugReportStorage.instance.EffectiveEndpointUrl;
            payload.backendStatus = "Submitting to Wix relay...";
            PungentBugReportSubmitJob.Start(payload, onComplete);
            status = "Submitting bug report to Wix relay...";
            return true;
        }

        public static string BackendTodoText()
        {
            return "PFU_BACKEND_TODO_BUG_REPORT\n" +
                   "Future endpoint: " + PungentBugReportSettings.SuggestedBackendEndpoint + "\n" +
                   "Wix relay must validate schema, rate-limit, store reports, send Discord notification, and own all private secrets.\n" +
                   "Unity editor scripts must never contain SMTP credentials, Discord webhook URLs, GitHub tokens, or private API keys.";
        }

        private static string BuildContextPath(PungentBugReportContext context, bool includeDeveloperDetails)
        {
            string path = string.IsNullOrWhiteSpace(context.contextPath) ? context.StableTopicId : context.contextPath;
            if (!string.IsNullOrWhiteSpace(context.selectedContextLabel))
                path += " > " + context.selectedContextLabel;
            if (includeDeveloperDetails && !string.IsNullOrWhiteSpace(context.generatedEntryId))
                path += " > generated entry " + context.generatedEntryId;
            return path;
        }

        private static string BuildDiagnosticsSummary(PungentBugReportContext context, bool includeDeveloperDetails)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Unity: " + Application.unityVersion);
            builder.AppendLine("Platform: " + SystemInfo.operatingSystem);
            builder.AppendLine("Utility: " + context.utilityId);
            builder.AppendLine("Topic: " + context.StableTopicId);
            builder.AppendLine("Source window: " + context.sourceWindow);
            if (includeDeveloperDetails && !string.IsNullOrWhiteSpace(context.sourcePath))
                builder.AppendLine("Source: " + context.sourcePath + (context.sourceLine > 0 ? ":" + context.sourceLine : string.Empty));
            return builder.ToString().Trim();
        }

        private static string BuildConsoleSummary()
        {
            return "Console summary collection is scaffolded. Future implementation should include an opt-in, sanitized recent exception summary only.";
        }

        private static string GetPackageVersionLabel()
        {
            return "PungentFunk Utilities local editor package";
        }

        private static bool DeveloperContextVisible => PungentDeveloperMode.Available && PungentDeveloperMode.Enabled;
    }

    internal static class PungentBugReportSubmitJob
    {
        private sealed class ActiveSubmit
        {
            public UnityWebRequest request;
            public PungentBugReportPayload payload;
            public Action<bool, string, string> onComplete;
            public double startedAt;
        }

        private static readonly List<ActiveSubmit> Active = new List<ActiveSubmit>();

        public static void Start(PungentBugReportPayload payload, Action<bool, string, string> onComplete)
        {
            payload = payload ?? new PungentBugReportPayload();
            string endpoint = PungentBugReportStorage.instance.EffectiveEndpointUrl;
            byte[] body = Encoding.UTF8.GetBytes(PungentBugReportService.ToJson(payload));
            UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SendWebRequest();

            Active.Add(new ActiveSubmit
            {
                request = request,
                payload = payload,
                onComplete = onComplete,
                startedAt = EditorApplication.timeSinceStartup
            });

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                ActiveSubmit active = Active[i];
                if (active == null || active.request == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (!active.request.isDone)
                {
                    if (EditorApplication.timeSinceStartup - active.startedAt < 30d)
                        continue;
                    active.request.Abort();
                }

                Complete(active);
                Active.RemoveAt(i);
            }

            if (Active.Count == 0)
                EditorApplication.update -= Tick;
        }

        private static void Complete(ActiveSubmit active)
        {
            UnityWebRequest request = active.request;
            PungentBugReportStorage storage = PungentBugReportStorage.instance;
            string body = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            bool networkOk = request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
            bool ok = false;
            string id = string.Empty;
            string message = string.Empty;

            if (networkOk)
            {
                try
                {
                    PungentBugReportRelayResponse response = JsonUtility.FromJson<PungentBugReportRelayResponse>(body);
                    ok = response != null && response.ok;
                    id = response == null ? string.Empty : response.id;
                    message = response == null ? string.Empty : response.message;
                }
                catch (Exception ex)
                {
                    message = "Relay returned an unreadable response: " + ex.Message;
                }
            }
            else
            {
                message = request.result + " (" + request.responseCode + "): " + request.error;
                if (!string.IsNullOrWhiteSpace(body))
                    message += " " + body;
            }

            if (ok)
            {
                string status = string.IsNullOrWhiteSpace(id) ? "Bug report sent." : "Bug report sent. ID: " + id;
                storage.lastSuccessfulReportId = id ?? string.Empty;
                storage.lastSubmitStatus = status;
                storage.lastSubmitError = string.Empty;
                storage.lastReportTimestampUtc = DateTime.UtcNow.ToString("o");
                storage.Persist();
                active.onComplete?.Invoke(true, status, id);
            }
            else
            {
                string status = string.IsNullOrWhiteSpace(message) ? "Bug report relay did not accept the report." : message;
                active.payload.backendStatus = "Relay failed: " + status;
                QueueFailedReport(active.payload, status);
                active.onComplete?.Invoke(false, "Relay failed; queued locally. " + status, string.Empty);
            }

            request.Dispose();
        }

        private static void QueueFailedReport(PungentBugReportPayload payload, string status)
        {
            PungentBugReportStorage storage = PungentBugReportStorage.instance;
            storage.lastSubmitStatus = "Queued locally after relay failure.";
            storage.lastSubmitError = status ?? string.Empty;
            PungentBugReportService.QueueLocally(payload);
            storage.Persist();
        }
    }

    public enum PungentUtilityHelpDocumentationLinkSource
    {
        ExplicitTopicRelation,
        UtilityAssigned,
        Global
    }

    public sealed class PungentUtilityHelpDocumentationLinkRow
    {
        public string id = string.Empty;
        public PungentUtilityDocumentationLinks.DocumentationLink link;
        public PungentUtilityHelpDocumentationLinkSource source;
        public string sourceLabel = string.Empty;
        public bool staleReference;
        public PungentUtilityDocumentationLinks.DocumentationTargetStatus status;
    }

    public static class PungentUtilityHelpDocumentationLinksProvider
    {
        public static readonly string[] RequiredDocumentationLinksTopicIds =
        {
            "overview",
            "linked-docs-list",
            "selected-document-details",
            "current-target",
            "utility-assignments",
            "current-version",
            "replace-current-target",
            "backlog",
            "notes-integration",
            "help-browser-related-docs"
        };

        public static List<PungentUtilityHelpDocumentationLinkRow> GetRelatedLinks(PungentUtilityHelpTopic topic, bool includeUtilityAssigned = true, bool includeGlobal = true)
        {
            List<PungentUtilityHelpDocumentationLinkRow> rows = new List<PungentUtilityHelpDocumentationLinkRow>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (topic != null && topic.relatedDocumentationLinkIds != null)
            {
                foreach (string id in topic.relatedDocumentationLinkIds)
                    AddExplicitRow(rows, seen, id);
            }

            if (includeUtilityAssigned && topic != null && !string.IsNullOrWhiteSpace(topic.utilityId))
                AddRows(rows, seen, PungentUtilityDocumentationLinks.instance.GetLinksForUtility(topic.utilityId), PungentUtilityHelpDocumentationLinkSource.UtilityAssigned);

            if (includeGlobal)
                AddRows(rows, seen, PungentUtilityDocumentationLinks.instance.GetGlobalLinks(), PungentUtilityHelpDocumentationLinkSource.Global);

            return rows;
        }

        public static List<PungentUtilityHelpDocumentationLinkRow> GetLinksForUtility(string utilityId, bool includeGlobal = true)
        {
            List<PungentUtilityHelpDocumentationLinkRow> rows = new List<PungentUtilityHelpDocumentationLinkRow>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(utilityId))
                AddRows(rows, seen, PungentUtilityDocumentationLinks.instance.GetLinksForUtility(utilityId), PungentUtilityHelpDocumentationLinkSource.UtilityAssigned);

            if (includeGlobal)
                AddRows(rows, seen, PungentUtilityDocumentationLinks.instance.GetGlobalLinks(), PungentUtilityHelpDocumentationLinkSource.Global);

            return rows;
        }

        public static string SourceLabel(PungentUtilityHelpDocumentationLinkSource source)
        {
            switch (source)
            {
                case PungentUtilityHelpDocumentationLinkSource.ExplicitTopicRelation:
                    return "Related Topic Link";
                case PungentUtilityHelpDocumentationLinkSource.UtilityAssigned:
                    return "Assigned to Utility";
                case PungentUtilityHelpDocumentationLinkSource.Global:
                    return "Global";
                default:
                    return "Documentation Link";
            }
        }

        private static void AddExplicitRow(List<PungentUtilityHelpDocumentationLinkRow> rows, HashSet<string> seen, string id)
        {
            string normalizedId = id == null ? string.Empty : id.Trim();
            if (string.IsNullOrWhiteSpace(normalizedId))
                return;

            if (!seen.Add(normalizedId))
                return;

            PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(normalizedId);
            rows.Add(CreateRow(normalizedId, link, PungentUtilityHelpDocumentationLinkSource.ExplicitTopicRelation, link == null));
        }

        private static void AddRows(List<PungentUtilityHelpDocumentationLinkRow> rows, HashSet<string> seen, IEnumerable<PungentUtilityDocumentationLinks.DocumentationLink> links, PungentUtilityHelpDocumentationLinkSource source)
        {
            if (links == null)
                return;

            foreach (PungentUtilityDocumentationLinks.DocumentationLink link in links)
            {
                if (link == null || string.IsNullOrWhiteSpace(link.id))
                    continue;
                if (!seen.Add(link.id))
                    continue;
                rows.Add(CreateRow(link.id, link, source, false));
            }
        }

        private static PungentUtilityHelpDocumentationLinkRow CreateRow(string id, PungentUtilityDocumentationLinks.DocumentationLink link, PungentUtilityHelpDocumentationLinkSource source, bool stale)
        {
            return new PungentUtilityHelpDocumentationLinkRow
            {
                id = id ?? string.Empty,
                link = link,
                source = source,
                sourceLabel = SourceLabel(source),
                staleReference = stale,
                status = link == null ? null : PungentUtilityDocumentationLinks.GetTargetStatus(link)
            };
        }
    }

    public static class PungentUtilityHelpCoverageClassifier
    {
        public static void ClassifyRow(PungentUtilityHelpCoverageRow row, PungentUtilityDescriptor descriptor, List<PungentUtilityHelpTopic> topics, List<PungentUtilityHelpContext> contexts)
        {
            if (row == null)
                return;

            row.utilityKind = ClassifyUtility(descriptor);
            row.coverageExpectation = ExpectationFor(row.utilityKind);

            bool headerExpected = ExpectsHeader(row.utilityKind);
            bool sectionExpected = ExpectsSection(row.utilityKind, topics, contexts);
            bool tooltipExpected = ExpectsTooltipCoverage(row.utilityKind);
            bool scriptingExpected = ExpectsScripting(row, descriptor, topics);

            bool headerAccepted = HasDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.HeaderHelp, PungentUtilityHelpCoverageDecisionState.NotApplicable, PungentUtilityHelpCoverageDecisionState.Ignored, PungentUtilityHelpCoverageDecisionState.Reviewed);
            bool sectionAccepted = HasDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.SectionHelp, PungentUtilityHelpCoverageDecisionState.NotApplicable, PungentUtilityHelpCoverageDecisionState.Ignored, PungentUtilityHelpCoverageDecisionState.Reviewed);
            bool tooltipAccepted = HasDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.TooltipCoverage, PungentUtilityHelpCoverageDecisionState.NotApplicable, PungentUtilityHelpCoverageDecisionState.Ignored, PungentUtilityHelpCoverageDecisionState.Reviewed);
            bool scriptingAccepted = HasDecision(row.utilityId, string.Empty, string.Empty, PungentUtilityHelpCoverageDecisionKind.ScriptingCoverage, PungentUtilityHelpCoverageDecisionState.NotApplicable, PungentUtilityHelpCoverageDecisionState.Ignored, PungentUtilityHelpCoverageDecisionState.Reviewed);

            row.missingHeaderIsActionable = headerExpected && !row.hasHeaderHelp && !headerAccepted;
            row.missingSectionIsActionable = sectionExpected && !row.hasSectionHelp && !sectionAccepted;
            row.missingTooltipIsActionable = tooltipExpected && !row.hasTooltipTopic && !tooltipAccepted;
            row.missingScriptingIsActionable = scriptingExpected && !row.hasScriptingEntries && !scriptingAccepted;
            row.tooltipCoverageState = tooltipExpected ? (row.hasTooltipTopic ? "Generated/curated tooltip coverage present" : tooltipAccepted ? "Accepted not applicable" : "Needs tooltip review") : "Not applicable";
            row.scriptingCoverageState = scriptingExpected ? (row.hasScriptingEntries ? "Scripting coverage present" : scriptingAccepted ? "Accepted not applicable" : "Needs scripting review") : "Not applicable";

            row.reviewPriority = Score(row);
            row.coverageNotes = BuildNotes(row, headerExpected, sectionExpected, tooltipExpected, scriptingExpected);
            row.coverageStatus = StatusFor(row);
        }

        public static PungentUtilityHelpCoverageUtilityKind ClassifyUtility(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return PungentUtilityHelpCoverageUtilityKind.Unknown;
            if (descriptor.ItemKind == PungentUtilityItemKind.Internal)
                return PungentUtilityHelpCoverageUtilityKind.Internal;
            if (descriptor.IsDeveloperOnly)
                return PungentUtilityHelpCoverageUtilityKind.DeveloperOnly;
            if (descriptor.SupportsSceneOverlay)
                return PungentUtilityHelpCoverageUtilityKind.SceneTool;
            if (descriptor.ItemKind == PungentUtilityItemKind.Action)
            {
                if (ContainsAny(descriptor.MenuPath, "create", "asset") || ContainsAny(descriptor.Id, "create", "populate", "generate"))
                    return PungentUtilityHelpCoverageUtilityKind.CreateAssetAction;
                return descriptor.SupportsContextMenu || descriptor.SupportsSelection
                    ? PungentUtilityHelpCoverageUtilityKind.ContextMenuAction
                    : PungentUtilityHelpCoverageUtilityKind.CommandUtility;
            }

            string windowType = descriptor.WindowTypeName ?? string.Empty;
            if (ContainsAny(windowType, "popup"))
                return PungentUtilityHelpCoverageUtilityKind.PopupUtility;
            if (ContainsAny(windowType, "window", "editorwindow"))
                return PungentUtilityHelpCoverageUtilityKind.WindowUtility;
            if (descriptor.SupportsSelection)
                return PungentUtilityHelpCoverageUtilityKind.InspectorTool;
            if (string.IsNullOrWhiteSpace(windowType) && ContainsAny(descriptor.Id + " " + descriptor.Module + " " + string.Join(" ", descriptor.Tags ?? Array.Empty<string>()), "provider", "index", "registry", "bridge"))
                return PungentUtilityHelpCoverageUtilityKind.ProviderOnly;
            return PungentUtilityHelpCoverageUtilityKind.WindowUtility;
        }

        public static PungentUtilityHelpGeneratedLifecycleState FeatureLifecycle(PungentUtilityHelpTopic topic, PungentUtilityHelpFeatureEntry entry)
        {
            if (entry == null)
                return PungentUtilityHelpGeneratedLifecycleState.NeedsReview;
            PungentUtilityHelpCoverageDecision decision = PungentUtilityHelpStorage.instance.GetCoverageDecision(topic == null ? string.Empty : topic.utilityId, topic == null ? string.Empty : topic.StableId, entry.id, PungentUtilityHelpCoverageDecisionKind.FeatureEntry);
            if (decision != null)
            {
                if (decision.state == PungentUtilityHelpCoverageDecisionState.Reviewed)
                    return PungentUtilityHelpGeneratedLifecycleState.Reviewed;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.Ignored)
                    return PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.NotApplicable)
                    return PungentUtilityHelpGeneratedLifecycleState.NotApplicable;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.PromotedToCurated)
                    return PungentUtilityHelpGeneratedLifecycleState.PromotedToCurated;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.Hidden)
                    return PungentUtilityHelpGeneratedLifecycleState.Hidden;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.NeedsWording)
                    return PungentUtilityHelpGeneratedLifecycleState.NeedsBetterWording;
            }

            if (entry.ignoredGenerated)
                return PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive;
            if (entry.hidden)
                return PungentUtilityHelpGeneratedLifecycleState.Hidden;
            if (entry.needsBetterWording || IsWeakTooltipEntry(entry))
                return PungentUtilityHelpGeneratedLifecycleState.NeedsBetterWording;
            return entry.generated ? PungentUtilityHelpGeneratedLifecycleState.GeneratedDraft : PungentUtilityHelpGeneratedLifecycleState.Reviewed;
        }

        public static PungentUtilityHelpGeneratedLifecycleState ScriptingLifecycle(PungentUtilityHelpTopic topic, PungentUtilityHelpScriptingEntry entry)
        {
            if (entry == null)
                return PungentUtilityHelpGeneratedLifecycleState.NeedsReview;
            PungentUtilityHelpCoverageDecision decision = PungentUtilityHelpStorage.instance.GetCoverageDecision(topic == null ? string.Empty : topic.utilityId, topic == null ? string.Empty : topic.StableId, entry.id, PungentUtilityHelpCoverageDecisionKind.ScriptingEntry);
            if (decision != null)
            {
                if (decision.state == PungentUtilityHelpCoverageDecisionState.Reviewed)
                    return PungentUtilityHelpGeneratedLifecycleState.Reviewed;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.Ignored)
                    return PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.NotApplicable)
                    return PungentUtilityHelpGeneratedLifecycleState.NotApplicable;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.PromotedToCurated)
                    return PungentUtilityHelpGeneratedLifecycleState.PromotedToCurated;
                if (decision.state == PungentUtilityHelpCoverageDecisionState.Hidden)
                    return PungentUtilityHelpGeneratedLifecycleState.Hidden;
            }

            if (entry.stale)
                return PungentUtilityHelpGeneratedLifecycleState.Stale;
            if (entry.hidden)
                return PungentUtilityHelpGeneratedLifecycleState.Hidden;
            if (string.IsNullOrWhiteSpace(entry.description) || entry.description.IndexOf("awaiting developer review", StringComparison.OrdinalIgnoreCase) >= 0)
                return PungentUtilityHelpGeneratedLifecycleState.NeedsReview;
            return entry.generated ? PungentUtilityHelpGeneratedLifecycleState.GeneratedDraft : PungentUtilityHelpGeneratedLifecycleState.Reviewed;
        }

        public static bool IsFeatureReviewResolved(PungentUtilityHelpTopic topic, PungentUtilityHelpFeatureEntry entry)
        {
            PungentUtilityHelpGeneratedLifecycleState lifecycle = FeatureLifecycle(topic, entry);
            return lifecycle == PungentUtilityHelpGeneratedLifecycleState.Reviewed ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.PromotedToCurated ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.NotApplicable ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.Hidden;
        }

        public static bool IsScriptingReviewResolved(PungentUtilityHelpTopic topic, PungentUtilityHelpScriptingEntry entry)
        {
            PungentUtilityHelpGeneratedLifecycleState lifecycle = ScriptingLifecycle(topic, entry);
            return lifecycle == PungentUtilityHelpGeneratedLifecycleState.Reviewed ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.PromotedToCurated ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.NotApplicable ||
                   lifecycle == PungentUtilityHelpGeneratedLifecycleState.Hidden;
        }

        public static bool IsWeakTooltipEntry(PungentUtilityHelpFeatureEntry entry)
        {
            if (entry == null || entry.ignoredGenerated)
                return false;
            return entry.needsBetterWording ||
                   string.Equals(entry.sourceConfidence, "low", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.sourceConfidence, "medium", StringComparison.OrdinalIgnoreCase) ||
                   string.IsNullOrWhiteSpace(entry.description) ||
                   entry.description.Trim().Length < 24;
        }

        private static PungentUtilityHelpCoverageExpectation ExpectationFor(PungentUtilityHelpCoverageUtilityKind kind)
        {
            switch (kind)
            {
                case PungentUtilityHelpCoverageUtilityKind.WindowUtility:
                case PungentUtilityHelpCoverageUtilityKind.SceneTool:
                case PungentUtilityHelpCoverageUtilityKind.InspectorTool:
                    return PungentUtilityHelpCoverageExpectation.FullWindowHelp;
                case PungentUtilityHelpCoverageUtilityKind.PopupUtility:
                    return PungentUtilityHelpCoverageExpectation.PopupHelp;
                case PungentUtilityHelpCoverageUtilityKind.CommandUtility:
                case PungentUtilityHelpCoverageUtilityKind.CreateAssetAction:
                    return PungentUtilityHelpCoverageExpectation.OverviewOnly;
                case PungentUtilityHelpCoverageUtilityKind.ContextMenuAction:
                    return PungentUtilityHelpCoverageExpectation.ContextActionHelp;
                case PungentUtilityHelpCoverageUtilityKind.DeveloperOnly:
                    return PungentUtilityHelpCoverageExpectation.DeveloperOnly;
                case PungentUtilityHelpCoverageUtilityKind.ProviderOnly:
                case PungentUtilityHelpCoverageUtilityKind.Internal:
                    return PungentUtilityHelpCoverageExpectation.NotApplicable;
                default:
                    return PungentUtilityHelpCoverageExpectation.Unknown;
            }
        }

        private static bool ExpectsHeader(PungentUtilityHelpCoverageUtilityKind kind)
        {
            return kind == PungentUtilityHelpCoverageUtilityKind.WindowUtility ||
                   kind == PungentUtilityHelpCoverageUtilityKind.PopupUtility ||
                   kind == PungentUtilityHelpCoverageUtilityKind.SceneTool ||
                   kind == PungentUtilityHelpCoverageUtilityKind.InspectorTool;
        }

        private static bool ExpectsSection(PungentUtilityHelpCoverageUtilityKind kind, List<PungentUtilityHelpTopic> topics, List<PungentUtilityHelpContext> contexts)
        {
            if (!ExpectsHeader(kind))
                return false;
            return (topics != null && topics.Count > 1) || (contexts != null && contexts.Count > 1);
        }

        private static bool ExpectsTooltipCoverage(PungentUtilityHelpCoverageUtilityKind kind)
        {
            return kind == PungentUtilityHelpCoverageUtilityKind.WindowUtility ||
                   kind == PungentUtilityHelpCoverageUtilityKind.PopupUtility ||
                   kind == PungentUtilityHelpCoverageUtilityKind.SceneTool ||
                   kind == PungentUtilityHelpCoverageUtilityKind.InspectorTool;
        }

        private static bool ExpectsScripting(PungentUtilityHelpCoverageRow row, PungentUtilityDescriptor descriptor, List<PungentUtilityHelpTopic> topics)
        {
            if (row != null && row.hasScriptingEntries)
                return true;
            string haystack = ((row == null ? string.Empty : row.utilityId + " " + row.displayName) + " " +
                               (descriptor == null ? string.Empty : descriptor.Description + " " + descriptor.Module + " " + string.Join(" ", descriptor.Tags ?? Array.Empty<string>())) + " " +
                               string.Join(" ", (topics ?? new List<PungentUtilityHelpTopic>()).Select(t => t.title + " " + t.topicId + " " + string.Join(" ", t.tags ?? new List<string>())))).ToLowerInvariant();
            return ContainsAny(haystack, "scripting", "api", "reference", "runtime", "router", "token", "parser", "authoring", "provider", "input", "audio", "placement");
        }

        private static PungentUtilityHelpCoverageStatus StatusFor(PungentUtilityHelpCoverageRow row)
        {
            if (row.utilityKind == PungentUtilityHelpCoverageUtilityKind.DeveloperOnly)
                return PungentUtilityHelpCoverageStatus.DeveloperOnly;
            if (row.coverageExpectation == PungentUtilityHelpCoverageExpectation.NotApplicable)
                return PungentUtilityHelpCoverageStatus.Acceptable;
            if (row.missingDocumentationTargetCount > 0)
                return PungentUtilityHelpCoverageStatus.MissingDocs;
            if (row.missingHeaderIsActionable)
                return PungentUtilityHelpCoverageStatus.NeedsHeaderHelp;
            if (row.missingSectionIsActionable)
                return PungentUtilityHelpCoverageStatus.NeedsSectionHelp;
            if (row.weakTooltipEntries > 0 || row.missingTooltipIsActionable)
                return PungentUtilityHelpCoverageStatus.NeedsTooltipReview;
            if (row.missingScriptingIsActionable)
                return PungentUtilityHelpCoverageStatus.NeedsScriptingReview;
            if (row.generatedEntriesAwaitingReview > 0)
                return PungentUtilityHelpCoverageStatus.NeedsReview;
            return row.coverageExpectation == PungentUtilityHelpCoverageExpectation.OverviewOnly ||
                   row.coverageExpectation == PungentUtilityHelpCoverageExpectation.ContextActionHelp
                ? PungentUtilityHelpCoverageStatus.Acceptable
                : PungentUtilityHelpCoverageStatus.Complete;
        }

        private static int Score(PungentUtilityHelpCoverageRow row)
        {
            int score = 0;
            if (row.missingHeaderIsActionable)
                score += 80;
            if (row.missingSectionIsActionable)
                score += 45;
            if (row.missingTooltipIsActionable)
                score += 35;
            if (row.missingScriptingIsActionable)
                score += 30;
            score += Math.Min(80, row.generatedEntriesAwaitingReview);
            score += Math.Min(60, row.weakTooltipEntries * 2);
            score += row.missingDocumentationTargetCount * 10;
            return score;
        }

        private static string BuildNotes(PungentUtilityHelpCoverageRow row, bool headerExpected, bool sectionExpected, bool tooltipExpected, bool scriptingExpected)
        {
            List<string> notes = new List<string>();
            notes.Add(row.utilityKind + " / " + row.coverageExpectation);
            if (!headerExpected)
                notes.Add("Header help not required");
            if (!sectionExpected)
                notes.Add("Section help optional");
            if (!tooltipExpected)
                notes.Add("Controls & Tooltips not required");
            if (!scriptingExpected)
                notes.Add("Scripting index not required");
            return string.Join("; ", notes.ToArray());
        }

        private static bool HasDecision(string utilityId, string topicStableId, string entryId, PungentUtilityHelpCoverageDecisionKind kind, params PungentUtilityHelpCoverageDecisionState[] states)
        {
            return PungentUtilityHelpStorage.instance.HasCoverageDecisionState(utilityId, topicStableId, entryId, kind, states);
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null)
                return false;
            return tokens.Any(token => !string.IsNullOrWhiteSpace(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    public static class PungentUtilityHelpReviewQueue
    {
        public static List<PungentUtilityHelpReviewRow> BuildRows(PungentUtilityHelpGenerationReport report)
        {
            if (report == null)
                report = PungentUtilityHelpTopicGenerator.BuildReport();

            List<PungentUtilityHelpReviewRow> rows = new List<PungentUtilityHelpReviewRow>();
            foreach (PungentUtilityHelpTopic topic in PungentUtilityHelpRegistry.AllTopics.Where(t => t != null))
            {
                AddTopicRows(rows, topic);
                AddFeatureRows(rows, topic);
                AddScriptingRows(rows, topic);
            }

            foreach (PungentUtilityHelpCoverageRow row in report.coverageRows.Where(r => r != null))
                AddCoverageRows(rows, row);

            foreach (PungentUtilityHelpGenerationIssue issue in report.issues.Where(i => i != null))
                AddIssueRows(rows, issue);

            foreach (PungentUtilityHelpCoverageDecision decision in PungentUtilityHelpStorage.instance.coverageDecisions ?? new List<PungentUtilityHelpCoverageDecision>())
                AddDecisionRows(rows, decision);

            return rows
                .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(r => r.priority)
                .ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.entryLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddTopicRows(List<PungentUtilityHelpReviewRow> rows, PungentUtilityHelpTopic topic)
        {
            if (topic.generated || topic.developerOnly)
            {
                PungentUtilityHelpCoverageDecision decision = PungentUtilityHelpStorage.instance.GetCoverageDecision(topic.utilityId, topic.StableId, string.Empty, PungentUtilityHelpCoverageDecisionKind.GeneratedTopic);
                if (decision == null || decision.state == PungentUtilityHelpCoverageDecisionState.None || decision.state == PungentUtilityHelpCoverageDecisionState.NeedsWording)
                {
                    rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics, topic.utilityId, topic, string.Empty, string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title, "Generated/draft topic needs review.", topic.sourceOwner, string.Empty, 0, topic.generated ? PungentUtilityHelpGeneratedLifecycleState.GeneratedDraft : PungentUtilityHelpGeneratedLifecycleState.NeedsReview, 70));
                }
            }

            if (topic.hidden || (topic.featureEntries != null && topic.featureEntries.Any(e => e != null && (e.hidden || e.ignoredGenerated))) || (topic.scriptingEntries != null && topic.scriptingEntries.Any(e => e != null && e.hidden)))
                rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.IgnoredHiddenEntries, topic.utilityId, topic, string.Empty, string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title, "Topic or entries include hidden/ignored generated content.", topic.sourceOwner, string.Empty, 0, PungentUtilityHelpGeneratedLifecycleState.Hidden, 15));
        }

        private static void AddFeatureRows(List<PungentUtilityHelpReviewRow> rows, PungentUtilityHelpTopic topic)
        {
            if (topic.featureEntries == null)
                return;

            foreach (PungentUtilityHelpFeatureEntry entry in topic.featureEntries.Where(e => e != null))
            {
                PungentUtilityHelpGeneratedLifecycleState lifecycle = PungentUtilityHelpCoverageClassifier.FeatureLifecycle(topic, entry);
                bool tooltip = string.Equals(entry.SourceOwnerOrEmpty(), PungentUtilityHelpTooltipIndexer.TooltipOwner, StringComparison.OrdinalIgnoreCase);
                if ((entry.generated || tooltip) && !PungentUtilityHelpCoverageClassifier.IsFeatureReviewResolved(topic, entry))
                {
                    rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.GeneratedTooltipEntries, topic.utilityId, topic, entry.id, Label(entry), "Generated tooltip/control entry.", entry.SourceOwnerOrEmpty(), entry.sourcePath, entry.sourceLine, lifecycle, lifecycle == PungentUtilityHelpGeneratedLifecycleState.GeneratedDraft ? 45 : 35, entry));
                }

                if (tooltip && PungentUtilityHelpCoverageClassifier.IsWeakTooltipEntry(entry) && !PungentUtilityHelpCoverageClassifier.IsFeatureReviewResolved(topic, entry))
                {
                    rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.WeakTooltips, topic.utilityId, topic, entry.id, Label(entry), "Tooltip entry has low confidence, short text, missing description, or needs better wording.", entry.SourceOwnerOrEmpty(), entry.sourcePath, entry.sourceLine, lifecycle, 75, entry));
                }

                if (lifecycle == PungentUtilityHelpGeneratedLifecycleState.Hidden || lifecycle == PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive || lifecycle == PungentUtilityHelpGeneratedLifecycleState.NotApplicable)
                    rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.IgnoredHiddenEntries, topic.utilityId, topic, entry.id, Label(entry), "Generated feature entry is hidden, ignored, or not applicable.", entry.SourceOwnerOrEmpty(), entry.sourcePath, entry.sourceLine, lifecycle, 12, entry));
            }
        }

        private static void AddScriptingRows(List<PungentUtilityHelpReviewRow> rows, PungentUtilityHelpTopic topic)
        {
            if (topic.scriptingEntries == null)
                return;

            foreach (PungentUtilityHelpScriptingEntry entry in topic.scriptingEntries.Where(e => e != null))
            {
                PungentUtilityHelpGeneratedLifecycleState lifecycle = PungentUtilityHelpCoverageClassifier.ScriptingLifecycle(topic, entry);
                if (entry.generated && !PungentUtilityHelpCoverageClassifier.IsScriptingReviewResolved(topic, entry))
                    rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.GeneratedScriptingEntries, topic.utilityId, topic, entry.id, string.IsNullOrWhiteSpace(entry.signature) ? entry.memberName : entry.signature, "Generated scripting reference entry.", "Generated scripting index", entry.sourcePath, 0, lifecycle, lifecycle == PungentUtilityHelpGeneratedLifecycleState.Stale ? 90 : 45, null, entry));
                if (lifecycle == PungentUtilityHelpGeneratedLifecycleState.Hidden || lifecycle == PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive || lifecycle == PungentUtilityHelpGeneratedLifecycleState.NotApplicable)
                    rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.IgnoredHiddenEntries, topic.utilityId, topic, entry.id, string.IsNullOrWhiteSpace(entry.signature) ? entry.memberName : entry.signature, "Generated scripting entry is hidden, ignored, or not applicable.", "Generated scripting index", entry.sourcePath, 0, lifecycle, 12, null, entry));
            }
        }

        private static void AddCoverageRows(List<PungentUtilityHelpReviewRow> rows, PungentUtilityHelpCoverageRow coverage)
        {
            if (coverage.missingHeaderIsActionable)
                rows.Add(CreateCoverageRow(PungentUtilityHelpReviewQueueKind.MissingHeaderHelp, coverage, "Window/popup utility needs a header [?] or accepted not-applicable decision.", 95));
            if (coverage.missingSectionIsActionable)
                rows.Add(CreateCoverageRow(PungentUtilityHelpReviewQueueKind.MissingSectionHelp, coverage, "Utility likely needs section-level contextual help or accepted not-applicable decision.", 65));
            if (coverage.missingScriptingIsActionable)
                rows.Add(CreateCoverageRow(PungentUtilityHelpReviewQueueKind.MissingScripting, coverage, coverage.scriptingCoverageState, 55));
            if (coverage.coverageStatus == PungentUtilityHelpCoverageStatus.Complete || coverage.coverageStatus == PungentUtilityHelpCoverageStatus.Acceptable)
                rows.Add(CreateCoverageRow(PungentUtilityHelpReviewQueueKind.CoverageComplete, coverage, coverage.coverageNotes, 1));
        }

        private static void AddIssueRows(List<PungentUtilityHelpReviewRow> rows, PungentUtilityHelpGenerationIssue issue)
        {
            if (issue.kind.IndexOf("Documentation", StringComparison.OrdinalIgnoreCase) < 0 &&
                issue.kind.IndexOf("Stale Related", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(issue.topicStableId);
            rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.DocumentationLinkIssues, issue.utilityId, topic, string.Empty, string.IsNullOrWhiteSpace(issue.topicStableId) ? issue.kind : issue.topicStableId, issue.message, "Help Coverage", issue.sourcePath, issue.sourceLine, PungentUtilityHelpGeneratedLifecycleState.NeedsReview, 70));
        }

        private static void AddDecisionRows(List<PungentUtilityHelpReviewRow> rows, PungentUtilityHelpCoverageDecision decision)
        {
            if (decision == null || decision.state == PungentUtilityHelpCoverageDecisionState.None)
                return;
            if (decision.state != PungentUtilityHelpCoverageDecisionState.Ignored &&
                decision.state != PungentUtilityHelpCoverageDecisionState.Hidden &&
                decision.state != PungentUtilityHelpCoverageDecisionState.NotApplicable)
                return;

            PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.FindByStableId(decision.topicStableId);
            rows.Add(CreateRow(PungentUtilityHelpReviewQueueKind.IgnoredHiddenEntries, decision.utilityId, topic, decision.entryId, string.IsNullOrWhiteSpace(decision.entryId) ? decision.kind.ToString() : decision.entryId, decision.state + ": " + decision.notes, "Coverage decision", string.Empty, 0, decision.state == PungentUtilityHelpCoverageDecisionState.NotApplicable ? PungentUtilityHelpGeneratedLifecycleState.NotApplicable : PungentUtilityHelpGeneratedLifecycleState.IgnoredFalsePositive, 10));
        }

        private static PungentUtilityHelpReviewRow CreateCoverageRow(PungentUtilityHelpReviewQueueKind kind, PungentUtilityHelpCoverageRow coverage, string message, int priority)
        {
            return new PungentUtilityHelpReviewRow
            {
                queueKind = kind,
                utilityId = coverage.utilityId,
                displayName = coverage.displayName,
                topicStableId = PungentUtilityHelpIds.TopicKey(coverage.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic),
                entryLabel = coverage.displayName,
                message = message,
                sourceOwner = "Help Coverage",
                lifecycleState = PungentUtilityHelpGeneratedLifecycleState.NeedsReview,
                priority = priority + coverage.reviewPriority,
                utilityKind = coverage.utilityKind,
                coverageStatus = coverage.coverageStatus,
                coverageRow = coverage
            };
        }

        private static PungentUtilityHelpReviewRow CreateRow(PungentUtilityHelpReviewQueueKind queue, string utilityId, PungentUtilityHelpTopic topic, string entryId, string label, string message, string sourceOwner, string sourcePath, int sourceLine, PungentUtilityHelpGeneratedLifecycleState lifecycle, int priority, PungentUtilityHelpFeatureEntry feature = null, PungentUtilityHelpScriptingEntry scripting = null)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            return new PungentUtilityHelpReviewRow
            {
                queueKind = queue,
                utilityId = string.IsNullOrWhiteSpace(utilityId) && topic != null ? topic.utilityId : utilityId,
                displayName = descriptor == null ? (string.IsNullOrWhiteSpace(utilityId) ? "Unregistered" : utilityId) : descriptor.DisplayName,
                topicStableId = topic == null ? string.Empty : topic.StableId,
                entryId = entryId ?? string.Empty,
                entryLabel = label ?? string.Empty,
                message = message ?? string.Empty,
                sourceOwner = sourceOwner ?? string.Empty,
                sourcePath = sourcePath ?? string.Empty,
                sourceLine = sourceLine,
                lifecycleState = lifecycle,
                priority = priority,
                utilityKind = PungentUtilityHelpCoverageClassifier.ClassifyUtility(descriptor),
                coverageStatus = PungentUtilityHelpCoverageStatus.NeedsReview,
                featureEntry = feature,
                scriptingEntry = scripting,
                topic = topic
            };
        }

        private static string Label(PungentUtilityHelpFeatureEntry entry)
        {
            return string.IsNullOrWhiteSpace(entry.label) ? entry.id : entry.label;
        }
    }

    public static class PungentUtilityHelpTopicGenerator
    {
        public const string RegistryOwner = "Generated registry metadata";
        public const string ContextOwner = "Generated contextual help stub";

        private static readonly Regex HelpButtonCallRegex = new Regex(
            @"PungentUtilityHelpButton\.(?:Draw|DrawIcon|Open)\s*\(\s*""([^""]+)""(?:\s*,\s*""([^""]*)"")?(?:\s*,\s*""([^""]*)"")?(?:\s*,\s*""([^""]*)"")?(?:\s*,\s*""([^""]*)"")?",
            RegexOptions.Compiled);

        public static int GenerateRegistryOverviewTopics(out string status)
        {
            List<PungentUtilityHelpTopic> topics = new List<PungentUtilityHelpTopic>();
            foreach (PungentUtilityDescriptor descriptor in PungentUtilityRegistry.All)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;

                topics.Add(CreateRegistryOverviewTopic(descriptor));
            }

            PungentUtilityHelpStorage.instance.ReplaceGeneratedTopicsByOwner(RegistryOwner, topics, "Generated " + topics.Count + " registry overview draft topics.");
            PungentUtilityHelpStorage.instance.lastRegistryGeneratedUtc = DateTime.UtcNow.ToString("o");
            PungentUtilityHelpStorage.instance.lastRegistryGeneratedStatus = "Generated " + topics.Count + " registry overview draft topics.";
            PungentUtilityHelpStorage.instance.Persist();
            status = PungentUtilityHelpStorage.instance.lastRegistryGeneratedStatus;
            return topics.Count;
        }

        public static int GenerateMissingContextualTopicStubs(out string status)
        {
            List<PungentUtilityHelpContext> contexts = RefreshContextualHelpButtonIndex(out string contextStatus);
            List<PungentUtilityHelpTopic> stubs = new List<PungentUtilityHelpTopic>();

            foreach (PungentUtilityHelpContext context in contexts)
            {
                if (context == null)
                    continue;
                context.Normalize();
                if (PungentUtilityHelpRegistry.Find(context.utilityId, context.sectionId, context.topicId) != null)
                    continue;

                stubs.Add(CreateContextStub(context));
            }

            PungentUtilityHelpStorage.instance.ReplaceGeneratedTopicsByOwner(ContextOwner, stubs, "Generated " + stubs.Count + " contextual help draft stubs. " + contextStatus);
            PungentUtilityHelpStorage.instance.lastContextGeneratedUtc = DateTime.UtcNow.ToString("o");
            PungentUtilityHelpStorage.instance.lastContextGeneratedStatus = "Indexed " + contexts.Count + " help button contexts and generated " + stubs.Count + " missing draft stubs.";
            PungentUtilityHelpStorage.instance.Persist();
            status = PungentUtilityHelpStorage.instance.lastContextGeneratedStatus;
            return stubs.Count;
        }

        public static List<PungentUtilityHelpContext> RefreshContextualHelpButtonIndex(out string status)
        {
            List<PungentUtilityHelpContext> contexts = new List<PungentUtilityHelpContext>();
            contexts.AddRange(PungentUtilityHelpButton.GetRuntimeContexts());

            foreach (string path in GetEditorScriptPaths())
                contexts.AddRange(FindHelpButtonContexts(path));

            List<PungentUtilityHelpContext> distinct = contexts
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.utilityId))
                .Select(c =>
                {
                    c.Normalize();
                    return c;
                })
                .GroupBy(c => c.StableTopicId + "|" + c.sourcePath + "|" + c.sourceLine, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.utilityId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.sectionId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.topicId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            status = "Indexed " + distinct.Count + " contextual help button references.";
            PungentUtilityHelpStorage.instance.ReplaceContextualHelpContexts(distinct, status);
            return distinct;
        }

        public static PungentUtilityHelpGenerationReport BuildReport()
        {
            PungentUtilityHelpGenerationReport report = new PungentUtilityHelpGenerationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("o")
            };

            List<PungentUtilityHelpTopic> allTopics = PungentUtilityHelpRegistry.AllTopics.ToList();
            HashSet<string> topicIds = new HashSet<string>(allTopics.Select(t => t.StableId), StringComparer.OrdinalIgnoreCase);

            foreach (PungentUtilityDescriptor descriptor in PungentUtilityRegistry.All)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;

                string overviewId = PungentUtilityHelpIds.TopicKey(descriptor.Id, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
                if (!topicIds.Contains(overviewId))
                {
                    report.utilitiesWithoutOverview++;
                    AddIssue(report, "Missing Overview", descriptor.Id, overviewId, "Utility has no overview help topic.", string.Empty, 0);
                }
            }

            foreach (PungentUtilityHelpContext context in PungentUtilityHelpStorage.instance.contextualHelpContexts ?? new List<PungentUtilityHelpContext>())
            {
                if (context == null)
                    continue;
                context.Normalize();
                if (!topicIds.Contains(context.StableTopicId))
                {
                    report.missingContextualTopics++;
                    AddIssue(report, "Missing Context", context.utilityId, context.StableTopicId, "Help button points to a missing topic.", context.sourcePath, context.sourceLine);
                }
            }

            foreach (PungentUtilityHelpTopic topic in allTopics)
            {
                if (topic == null)
                    continue;
                if (string.IsNullOrWhiteSpace(topic.quickUseMarkdown))
                {
                    report.topicsWithoutQuickUse++;
                    AddIssue(report, "Missing Quick Use", topic.utilityId, topic.StableId, "Topic has no Quick Use content.", string.Empty, 0);
                }
                if (topic.featureEntries == null || topic.featureEntries.Count == 0)
                    report.topicsWithoutFeatureEntries++;
                if (topic.scriptingEntries == null || topic.scriptingEntries.Count == 0)
                    report.topicsWithoutScriptingEntries++;
                if (topic.generated || topic.developerOnly)
                    report.generatedDraftTopics++;
                if (topic.featureEntries != null)
                    report.tooltipEntries += topic.featureEntries.Count(e => e != null && string.Equals(e.SourceOwnerOrEmpty(), PungentUtilityHelpTooltipIndexer.TooltipOwner, StringComparison.OrdinalIgnoreCase));
                if (topic.scriptingEntries != null)
                    report.staleScriptingEntries += topic.scriptingEntries.Count(e => e != null && e.stale);
            }

            BuildCoverageRows(report, allTopics, topicIds);
            BuildDocumentationLinkCoverage(report, allTopics, topicIds);
            report.missingTooltipCandidates = report.issues.Count(i => string.Equals(i.kind, "Missing Tooltip", StringComparison.OrdinalIgnoreCase));
            return report;
        }

        public static string ExportReportText(PungentUtilityHelpGenerationReport report)
        {
            if (report == null)
                report = BuildReport();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Pungent Utility Help Generation Report");
            builder.AppendLine("Generated UTC: " + report.generatedUtc);
            builder.AppendLine("Utilities without overview: " + report.utilitiesWithoutOverview);
            builder.AppendLine("Missing contextual topics: " + report.missingContextualTopics);
            builder.AppendLine("Topics without Quick Use: " + report.topicsWithoutQuickUse);
            builder.AppendLine("Topics without Feature Index entries: " + report.topicsWithoutFeatureEntries);
            builder.AppendLine("Topics without Scripting Index entries: " + report.topicsWithoutScriptingEntries);
            builder.AppendLine("Generated/draft topics: " + report.generatedDraftTopics);
            builder.AppendLine("Stale scripting entries: " + report.staleScriptingEntries);
            builder.AppendLine("Registered utilities: " + report.registeredUtilities);
            builder.AppendLine("Utilities with overview help: " + report.utilitiesWithOverviewHelp);
            builder.AppendLine("Utilities with header help: " + report.utilitiesWithHeaderHelp);
            builder.AppendLine("Utilities with section help: " + report.utilitiesWithSectionHelp);
            builder.AppendLine("Utilities with Controls & Tooltips: " + report.utilitiesWithTooltipTopic);
            builder.AppendLine("Documentation links: " + report.documentationLinkCount);
            builder.AppendLine("Documentation links with current targets: " + report.documentationLinksWithCurrentTargets);
            builder.AppendLine("Documentation links missing current targets: " + report.documentationLinksMissingCurrentTargets);
            builder.AppendLine("Documentation links with backlog versions: " + report.documentationLinksWithBacklog);
            builder.AppendLine("Global documentation links: " + report.globalDocumentationLinks);
            builder.AppendLine("Utility-assigned documentation links: " + report.utilityAssignedDocumentationLinks);
            builder.AppendLine("Utilities with documentation links: " + report.utilitiesWithDocumentationLinks);
            builder.AppendLine("Utilities without documentation links: " + report.utilitiesWithoutDocumentationLinks);
            builder.AppendLine("Topics with explicit related docs: " + report.topicsWithExplicitDocumentationLinks);
            builder.AppendLine("Stale related documentation link IDs: " + report.staleRelatedDocumentationLinkIds);
            builder.AppendLine("Documentation Links help topics present: " + report.documentationLinksHelpTopicsPresent);
            builder.AppendLine("Documentation Links help topics missing: " + report.documentationLinksHelpTopicsMissing);
            builder.AppendLine("Generated entries awaiting review: " + report.generatedEntriesAwaitingReview);
            builder.AppendLine("Hidden entries: " + report.hiddenEntries);
            builder.AppendLine("Developer-only entries: " + report.developerOnlyEntries);
            builder.AppendLine("Bug report relay: " + PungentBugReportSettings.BackendStatus);
            builder.AppendLine();
            builder.AppendLine("Coverage Rows");
            foreach (PungentUtilityHelpCoverageRow row in report.coverageRows.Take(120))
                builder.AppendLine(row.utilityId + " | overview=" + row.hasOverviewHelp + " | header=" + row.hasHeaderHelp + " | sections=" + row.hasSectionHelp + " | docs=" + row.documentationLinkCount + " | missingTargets=" + row.missingDocumentationTargetCount + " | tooltips=" + row.tooltipEntryCount + " | scripting=" + row.scriptingEntryCount + " | review=" + row.generatedEntriesAwaitingReview);
            builder.AppendLine();
            foreach (PungentUtilityHelpGenerationIssue issue in report.issues.Take(200))
                builder.AppendLine(issue.kind + " | " + issue.topicStableId + " | " + issue.message + (string.IsNullOrWhiteSpace(issue.sourcePath) ? string.Empty : " | " + issue.sourcePath + ":" + issue.sourceLine));
            return builder.ToString();
        }

        private static void BuildCoverageRows(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpTopic> allTopics, HashSet<string> topicIds)
        {
            List<PungentUtilityDescriptor> descriptors = PungentUtilityRegistry.All
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id))
                .OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Dictionary<string, List<PungentUtilityHelpTopic>> topicsByUtility = allTopics
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.utilityId))
                .GroupBy(t => t.utilityId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            List<PungentUtilityHelpContext> contexts = (PungentUtilityHelpStorage.instance.contextualHelpContexts ?? new List<PungentUtilityHelpContext>())
                .Concat(PungentUtilityHelpButton.GetRuntimeContexts())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.utilityId))
                .Select(c =>
                {
                    c.Normalize();
                    return c;
                })
                .GroupBy(c => c.StableTopicId + "|" + c.sourcePath + "|" + c.sourceLine + "|" + c.label, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            report.registeredUtilities = descriptors.Count;

            foreach (PungentUtilityDescriptor descriptor in descriptors)
            {
                string utilityId = descriptor.Id;
                topicsByUtility.TryGetValue(utilityId, out List<PungentUtilityHelpTopic> topics);
                topics = topics ?? new List<PungentUtilityHelpTopic>();
                List<PungentUtilityHelpContext> utilityContexts = contexts
                    .Where(c => string.Equals(c.utilityId, utilityId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                bool hasOverview = topicIds.Contains(PungentUtilityHelpIds.TopicKey(utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic));
                bool hasHeader = utilityContexts.Any(IsHeaderContext);
                bool hasSection = utilityContexts.Any(c => !IsHeaderContext(c) && !IsOverviewContext(c));
                int tooltipEntries = topics.Sum(t => CountTooltipEntries(t));
                int scriptingEntries = topics.Sum(t => t.scriptingEntries == null ? 0 : t.scriptingEntries.Count(e => e != null));
                int reviewCount = topics.Sum(CountGeneratedReviewEntries);
                int hiddenCount = topics.Sum(CountHiddenEntries);
                int developerOnlyCount = topics.Sum(CountDeveloperOnlyEntries);
                int weakTooltipCount = topics.Sum(CountWeakTooltipEntries);

                PungentUtilityHelpCoverageRow row = new PungentUtilityHelpCoverageRow
                {
                    utilityId = utilityId,
                    displayName = descriptor.DisplayName,
                    hasOverviewHelp = hasOverview,
                    hasHeaderHelp = hasHeader,
                    hasSectionHelp = hasSection,
                    hasTooltipTopic = tooltipEntries > 0 || topics.Any(t => string.Equals(t.sourceOwner, PungentUtilityHelpTooltipIndexer.TooltipOwner, StringComparison.OrdinalIgnoreCase)),
                    hasScriptingEntries = scriptingEntries > 0,
                    topicCount = topics.Count,
                    contextualHelpButtonCount = utilityContexts.Count,
                    tooltipEntryCount = tooltipEntries,
                    scriptingEntryCount = scriptingEntries,
                    generatedEntriesAwaitingReview = reviewCount,
                    hiddenEntries = hiddenCount,
                    developerOnlyEntries = developerOnlyCount,
                    weakTooltipEntries = weakTooltipCount
                };
                PungentUtilityHelpCoverageClassifier.ClassifyRow(row, descriptor, topics, utilityContexts);

                report.coverageRows.Add(row);
                if (hasOverview)
                    report.utilitiesWithOverviewHelp++;
                else
                    AddIssue(report, "Missing Overview", utilityId, PungentUtilityHelpIds.TopicKey(utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic), "Registered utility has no overview topic.", string.Empty, 0);
                if (hasHeader)
                    report.utilitiesWithHeaderHelp++;
                else if (row.missingHeaderIsActionable)
                    AddIssue(report, "Missing Header Help", utilityId, utilityId + "/overview/overview", "Window/popup utility has no cached header contextual help button.", string.Empty, 0);
                if (hasSection)
                    report.utilitiesWithSectionHelp++;
                else if (row.missingSectionIsActionable)
                    AddIssue(report, "Missing Section Help", utilityId, utilityId + "/overview/overview", "Window/popup utility likely needs at least one section-level help affordance.", string.Empty, 0);
                if (row.hasTooltipTopic)
                    report.utilitiesWithTooltipTopic++;
                else if (row.missingTooltipIsActionable)
                    AddIssue(report, "Missing Tooltip Coverage", utilityId, utilityId + "/controls-tooltips/controls-tooltips", row.tooltipCoverageState, string.Empty, 0);
                if (weakTooltipCount > 0)
                    AddIssue(report, "Weak Tooltip", utilityId, utilityId + "/controls-tooltips/controls-tooltips", weakTooltipCount + " generated tooltip entries need review, better wording, or false-positive ignore.", string.Empty, 0);
                if (row.hasScriptingEntries)
                    report.utilitiesWithScriptingEntries++;
                else if (row.missingScriptingIsActionable)
                    AddIssue(report, "Missing Scripting Coverage", utilityId, utilityId + "/scripting-index/scripting-index", row.scriptingCoverageState, string.Empty, 0);

                report.generatedEntriesAwaitingReview += reviewCount;
                report.hiddenEntries += hiddenCount;
                report.developerOnlyEntries += developerOnlyCount;
                report.weakTooltipEntries += weakTooltipCount;
            }
        }

        private static void BuildDocumentationLinkCoverage(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpTopic> allTopics, HashSet<string> topicIds)
        {
            List<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetAll()
                .Where(link => link != null)
                .ToList();
            HashSet<string> linkIds = new HashSet<string>(links.Where(link => !string.IsNullOrWhiteSpace(link.id)).Select(link => link.id), StringComparer.OrdinalIgnoreCase);

            report.documentationLinkCount = links.Count;
            report.globalDocumentationLinks = links.Count(IsGlobalDocumentationLink);
            report.utilityAssignedDocumentationLinks = links.Count(link => !IsGlobalDocumentationLink(link));
            report.documentationLinksWithBacklog = links.Count(link => link.backlog != null && link.backlog.Count > 0);

            foreach (PungentUtilityDocumentationLinks.DocumentationLink link in links)
            {
                PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                if (HasUsableDocumentationTarget(status))
                    report.documentationLinksWithCurrentTargets++;
                if (IsMissingDocumentationTarget(status))
                {
                    report.documentationLinksMissingCurrentTargets++;
                    AddIssue(report, "Documentation Link Missing Target", string.Empty, link.id, "Documentation link has no usable current target.", string.Empty, 0);
                }
            }

            foreach (PungentUtilityHelpCoverageRow row in report.coverageRows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.utilityId))
                    continue;

                List<PungentUtilityDocumentationLinks.DocumentationLink> utilityLinks = links
                    .Where(link => link.utilityIds != null && link.utilityIds.Any(id => string.Equals(id, row.utilityId, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                row.documentationLinkCount = utilityLinks.Count;
                row.missingDocumentationTargetCount = utilityLinks.Count(link => IsMissingDocumentationTarget(PungentUtilityDocumentationLinks.GetTargetStatus(link)));
                row.hasDocumentationLinks = row.documentationLinkCount > 0;
                if (row.hasDocumentationLinks)
                    report.utilitiesWithDocumentationLinks++;
                else
                    report.utilitiesWithoutDocumentationLinks++;
            }

            foreach (PungentUtilityHelpTopic topic in allTopics.Where(topic => topic != null && topic.relatedDocumentationLinkIds != null && topic.relatedDocumentationLinkIds.Count > 0))
            {
                report.topicsWithExplicitDocumentationLinks++;
                foreach (string linkId in topic.relatedDocumentationLinkIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                {
                    if (linkIds.Contains(linkId))
                        continue;

                    report.staleRelatedDocumentationLinkIds++;
                    AddIssue(report, "Stale Related Documentation Link", topic.utilityId, topic.StableId, "Topic references missing documentation link ID: " + linkId, string.Empty, 0);
                }
            }

            foreach (string topicId in PungentUtilityHelpDocumentationLinksProvider.RequiredDocumentationLinksTopicIds)
            {
                string stableId = PungentUtilityHelpIds.TopicKey("documentation-links", topicId, topicId);
                if (topicIds.Contains(stableId))
                    report.documentationLinksHelpTopicsPresent++;
                else
                {
                    report.documentationLinksHelpTopicsMissing++;
                    AddIssue(report, "Missing Documentation Links Topic", "documentation-links", stableId, "Documentation Links help topic is missing.", string.Empty, 0);
                }
            }
        }

        private static bool IsHeaderContext(PungentUtilityHelpContext context)
        {
            string label = context == null ? string.Empty : context.label ?? string.Empty;
            return label.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (IsOverviewContext(context) && label.IndexOf("contextual help button", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static bool IsOverviewContext(PungentUtilityHelpContext context)
        {
            return context != null &&
                   string.Equals(PungentUtilityHelpIds.Normalize(context.sectionId, PungentUtilityHelpIds.DefaultSection), PungentUtilityHelpIds.DefaultSection, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(PungentUtilityHelpIds.Normalize(context.topicId, PungentUtilityHelpIds.DefaultTopic), PungentUtilityHelpIds.DefaultTopic, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountTooltipEntries(PungentUtilityHelpTopic topic)
        {
            return topic == null || topic.featureEntries == null
                ? 0
                : topic.featureEntries.Count(e => e != null && string.Equals(e.SourceOwnerOrEmpty(), PungentUtilityHelpTooltipIndexer.TooltipOwner, StringComparison.OrdinalIgnoreCase));
        }

        private static int CountGeneratedReviewEntries(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return 0;
            int count = topic.generated && topic.developerOnly ? 1 : 0;
            if (topic.featureEntries != null)
                count += topic.featureEntries.Count(e => e != null && e.generated && e.developerOnly && !PungentUtilityHelpCoverageClassifier.IsFeatureReviewResolved(topic, e));
            if (topic.scriptingEntries != null)
                count += topic.scriptingEntries.Count(e => e != null && e.generated && (e.developerOnly || ((e.description ?? string.Empty).IndexOf("awaiting developer review", StringComparison.OrdinalIgnoreCase) >= 0)) && !PungentUtilityHelpCoverageClassifier.IsScriptingReviewResolved(topic, e));
            return count;
        }

        private static int CountHiddenEntries(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return 0;
            int count = topic.hidden ? 1 : 0;
            if (topic.featureEntries != null)
                count += topic.featureEntries.Count(e => e != null && e.hidden);
            if (topic.scriptingEntries != null)
                count += topic.scriptingEntries.Count(e => e != null && e.hidden);
            return count;
        }

        private static int CountDeveloperOnlyEntries(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return 0;
            int count = topic.developerOnly ? 1 : 0;
            if (topic.featureEntries != null)
                count += topic.featureEntries.Count(e => e != null && e.developerOnly);
            if (topic.scriptingEntries != null)
                count += topic.scriptingEntries.Count(e => e != null && e.developerOnly);
            return count;
        }

        private static int CountWeakTooltipEntries(PungentUtilityHelpTopic topic)
        {
            return topic == null || topic.featureEntries == null
                ? 0
                : topic.featureEntries.Count(e => e != null && string.Equals(e.SourceOwnerOrEmpty(), PungentUtilityHelpTooltipIndexer.TooltipOwner, StringComparison.OrdinalIgnoreCase) && PungentUtilityHelpCoverageClassifier.IsWeakTooltipEntry(e) && !PungentUtilityHelpCoverageClassifier.IsFeatureReviewResolved(topic, e));
        }

        private static bool IsWeakTooltipEntry(PungentUtilityHelpFeatureEntry entry)
        {
            return PungentUtilityHelpCoverageClassifier.IsWeakTooltipEntry(entry);
        }

        private static bool IsGlobalDocumentationLink(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            return link == null || link.utilityIds == null || link.utilityIds.Length == 0;
        }

        private static bool HasUsableDocumentationTarget(PungentUtilityDocumentationLinks.DocumentationTargetStatus status)
        {
            return status != null &&
                   status.hasTarget &&
                   status.kind != PungentUtilityDocumentationLinks.DocumentationTargetKind.Empty &&
                   status.kind != PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget &&
                   status.kind != PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing;
        }

        private static bool IsMissingDocumentationTarget(PungentUtilityDocumentationLinks.DocumentationTargetStatus status)
        {
            return status == null ||
                   status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Empty ||
                   status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget ||
                   status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing;
        }

        private static PungentUtilityHelpTopic CreateRegistryOverviewTopic(PungentUtilityDescriptor descriptor)
        {
            string category = PungentUtilityRegistry.GetAreaCategory(descriptor);
            string module = PungentUtilityRegistry.GetModule(descriptor);
            string status = PungentUtilityRegistry.GetStatus(descriptor);
            List<string> tags = descriptor.Tags == null ? new List<string>() : descriptor.Tags.ToList();
            tags.Add("generated");
            tags.Add("registry");

            return new PungentUtilityHelpTopic
            {
                utilityId = descriptor.Id,
                sectionId = PungentUtilityHelpIds.DefaultSection,
                topicId = PungentUtilityHelpIds.DefaultTopic,
                title = descriptor.DisplayName,
                summary = string.IsNullOrWhiteSpace(descriptor.Description) ? "Generated overview draft from registered utility metadata." : descriptor.Description,
                quickUseMarkdown = "Open this utility from `" + descriptor.MenuPath + "`. This overview was generated from registry metadata and should be curated before shipping as final help.",
                featureEntries = new List<PungentUtilityHelpFeatureEntry>
                {
                    new PungentUtilityHelpFeatureEntry
                    {
                        id = "registry-metadata",
                        label = "Registry metadata",
                        description = "Category: " + category + ". Module: " + module + ". Package status: " + status + ".",
                        location = descriptor.MenuPath,
                        safetyNotes = "Generated from registry metadata; review wording before marking shippable.",
                        developerOnly = true,
                        generated = true
                    }
                },
                relatedUtilityIds = descriptor.RelatedUtilityIds == null ? new List<string>() : descriptor.RelatedUtilityIds.ToList(),
                tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                developerOnly = true,
                generated = true,
                sourceOwner = RegistryOwner,
                lastUpdatedUtc = DateTime.UtcNow.ToString("o")
            };
        }

        private static PungentUtilityHelpTopic CreateContextStub(PungentUtilityHelpContext context)
        {
            string topicLabel = string.IsNullOrWhiteSpace(context.topicId) ? context.sectionId : context.topicId;
            return new PungentUtilityHelpTopic
            {
                utilityId = context.utilityId,
                sectionId = context.sectionId,
                topicId = context.topicId,
                title = ObjectNames.NicifyVariableName(topicLabel),
                summary = "Generated draft for a contextual help button. Curate this topic before showing it as shippable help.",
                quickUseMarkdown = "This topic was generated because a contextual help button points here. Add concise workflow guidance, safety notes, and related topics.",
                featureEntries = new List<PungentUtilityHelpFeatureEntry>
                {
                    new PungentUtilityHelpFeatureEntry
                    {
                        id = "context-source",
                        label = string.IsNullOrWhiteSpace(context.label) ? "Contextual help source" : context.label,
                        description = "Help button source: " + (string.IsNullOrWhiteSpace(context.location) ? "Contextual help button" : context.location),
                        location = string.IsNullOrWhiteSpace(context.sourcePath) ? context.location : context.sourcePath + ":" + context.sourceLine,
                        safetyNotes = "Generated draft; review and curate before marking shippable.",
                        developerOnly = true,
                        generated = true
                    }
                },
                tags = new List<string> { "generated", "contextual-help", "draft" },
                developerOnly = true,
                generated = true,
                sourceOwner = ContextOwner,
                lastUpdatedUtc = DateTime.UtcNow.ToString("o")
            };
        }

        private static IEnumerable<PungentUtilityHelpContext> FindHelpButtonContexts(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
                yield break;

            string[] lines = File.ReadAllLines(assetPath);
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = HelpButtonCallRegex.Match(lines[i]);
                if (!match.Success)
                    continue;

                yield return new PungentUtilityHelpContext
                {
                    utilityId = match.Groups[1].Value,
                    sectionId = match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value : PungentUtilityHelpIds.DefaultSection,
                    topicId = match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? match.Groups[3].Value : PungentUtilityHelpIds.DefaultTopic,
                    label = match.Groups[5].Success && !string.IsNullOrWhiteSpace(match.Groups[5].Value) ? match.Groups[5].Value : "Contextual help button",
                    location = (match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? match.Groups[4].Value : "Contextual help button") + " source line " + (i + 1),
                    sourcePath = assetPath.Replace('\\', '/'),
                    sourceLine = i + 1,
                    generated = true
                };
            }
        }

        private static IEnumerable<string> GetEditorScriptPaths()
        {
            string[] roots = { "Assets/1 SKI GAME/Scripts/PungentFunkUtilities/Editor" };
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", roots);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrWhiteSpace(assetPath) && assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    yield return assetPath;
            }
        }

        private static void AddIssue(PungentUtilityHelpGenerationReport report, string kind, string utilityId, string topicStableId, string message, string sourcePath, int sourceLine)
        {
            report.issues.Add(new PungentUtilityHelpGenerationIssue
            {
                kind = kind,
                utilityId = utilityId,
                topicStableId = topicStableId,
                message = message,
                sourcePath = sourcePath,
                sourceLine = sourceLine
            });
        }
    }

    public static class PungentUtilityHelpTooltipIndexer
    {
        public const string TooltipOwner = "Generated tooltip index";
        private const string TooltipSection = "controls-tooltips";

        private static readonly Regex GuiContentRegex = new Regex(
            @"new\s+GUIContent\s*\(\s*(?:""([^""]*)""|[^,]+)\s*,\s*""([^""]+)""",
            RegexOptions.Compiled);
        private static readonly Regex LikelyUntooltippedControlRegex = new Regex(
            @"(?:GUILayout|EditorGUILayout)\.(?:Button|ToggleLeft|TextField|ObjectField|Popup|Foldout)\s*\(\s*""([^""]{3,})""",
            RegexOptions.Compiled);

        public static int RefreshTooltipIndex(out string status)
        {
            Dictionary<string, PungentUtilityHelpTopic> topics = new Dictionary<string, PungentUtilityHelpTopic>(StringComparer.OrdinalIgnoreCase);
            int tooltipCount = 0;

            try
            {
                EditorUtility.DisplayProgressBar("Pungent Help", "Scanning editor tooltips...", 0.10f);
                List<string> paths = GetEditorScriptPaths().ToList();
                int total = Math.Max(1, paths.Count);
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar("Pungent Help", "Reading " + Path.GetFileName(path), 0.10f + (0.85f * i / total));
                    string utilityId = GuessUtilityId(path);
                    if (string.IsNullOrWhiteSpace(utilityId))
                        continue;

                    foreach (PungentUtilityHelpFeatureEntry entry in ExtractTooltipEntries(path))
                    {
                        string key = PungentUtilityHelpIds.TopicKey(utilityId, TooltipSection, TooltipSection);
                        if (!topics.TryGetValue(key, out PungentUtilityHelpTopic topic))
                        {
                            topic = CreateTooltipTopic(utilityId);
                            topics[key] = topic;
                        }

                        topic.featureEntries.Add(entry);
                        tooltipCount++;
                    }

                    foreach (PungentUtilityHelpFeatureEntry entry in ExtractMissingTooltipCandidates(path))
                    {
                        string key = PungentUtilityHelpIds.TopicKey(utilityId, TooltipSection, TooltipSection);
                        if (!topics.TryGetValue(key, out PungentUtilityHelpTopic topic))
                        {
                            topic = CreateTooltipTopic(utilityId);
                            topics[key] = topic;
                        }

                        topic.featureEntries.Add(entry);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (PungentUtilityHelpTopic topic in topics.Values)
                topic.featureEntries = topic.featureEntries
                    .GroupBy(e => e.id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(e => e.label, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            List<PungentUtilityHelpTopic> generated = topics.Values.OrderBy(t => t.utilityId, StringComparer.OrdinalIgnoreCase).ToList();
            status = "Generated " + tooltipCount + " tooltip entries across " + generated.Count + " controls/tooltips topics.";
            PungentUtilityHelpStorage.instance.ReplaceGeneratedTopicsByOwner(TooltipOwner, generated, status);
            PungentUtilityHelpStorage.instance.lastTooltipGeneratedUtc = DateTime.UtcNow.ToString("o");
            PungentUtilityHelpStorage.instance.lastTooltipGeneratedStatus = status;
            PungentUtilityHelpStorage.instance.Persist();
            return tooltipCount;
        }

        private static PungentUtilityHelpTopic CreateTooltipTopic(string utilityId)
        {
            string display = utilityId;
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            if (descriptor != null)
                display = descriptor.DisplayName;

            return new PungentUtilityHelpTopic
            {
                utilityId = utilityId,
                sectionId = TooltipSection,
                topicId = TooltipSection,
                title = display + " Controls & Tooltips",
                summary = "Generated draft index of labelled controls and tooltips found in this utility source.",
                quickUseMarkdown = "Use this generated index as review material. Curate useful entries into normal Feature Index content before marking them shippable.",
                tags = new List<string> { "generated", "tooltip", "controls" },
                developerOnly = true,
                generated = true,
                sourceOwner = TooltipOwner,
                lastUpdatedUtc = DateTime.UtcNow.ToString("o")
            };
        }

        private static IEnumerable<PungentUtilityHelpFeatureEntry> ExtractTooltipEntries(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
                yield break;

            string[] lines = File.ReadAllLines(assetPath);
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = GuiContentRegex.Match(lines[i]);
                if (!match.Success)
                    continue;

                string tooltip = match.Groups[2].Value.Trim();
                if (string.IsNullOrWhiteSpace(tooltip))
                    continue;

                string label = match.Groups[1].Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value)
                    ? match.Groups[1].Value.Trim()
                    : "Control tooltip";

                yield return new PungentUtilityHelpFeatureEntry
                {
                    id = PungentUtilityHelpIds.Normalize(Path.GetFileNameWithoutExtension(assetPath) + "-" + (i + 1) + "-" + label, "tooltip-" + (i + 1)),
                    label = label,
                    description = tooltip,
                    location = "Tooltip index: " + assetPath.Replace('\\', '/') + ":" + (i + 1),
                    safetyNotes = "Generated from an existing GUIContent tooltip; review wording before promoting.",
                    sourcePath = assetPath.Replace('\\', '/'),
                    sourceLine = i + 1,
                    sourceConfidence = "high",
                    developerOnly = true,
                    generated = true
                };
            }
        }

        private static IEnumerable<PungentUtilityHelpFeatureEntry> ExtractMissingTooltipCandidates(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
                yield break;

            string[] lines = File.ReadAllLines(assetPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("GUIContent", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                Match match = LikelyUntooltippedControlRegex.Match(line);
                if (!match.Success)
                    continue;

                string label = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(label) || label.Length < 3)
                    continue;

                yield return new PungentUtilityHelpFeatureEntry
                {
                    id = PungentUtilityHelpIds.Normalize(Path.GetFileNameWithoutExtension(assetPath) + "-" + (i + 1) + "-missing-tooltip-" + label, "missing-tooltip-" + (i + 1)),
                    label = label,
                    description = "No tooltip was detected for this labelled control. Review whether it needs help text or should be ignored as a false positive.",
                    location = "Tooltip index: " + assetPath.Replace('\\', '/') + ":" + (i + 1),
                    safetyNotes = "Generated low-confidence missing-tooltip candidate. It is hidden from shippable help until reviewed.",
                    sourcePath = assetPath.Replace('\\', '/'),
                    sourceLine = i + 1,
                    sourceConfidence = "low",
                    needsBetterWording = true,
                    developerOnly = true,
                    hidden = true,
                    generated = true
                };
            }
        }

        private static IEnumerable<string> GetEditorScriptPaths()
        {
            string[] roots = { "Assets/1 SKI GAME/Scripts/PungentFunkUtilities/Editor" };
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", roots);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrWhiteSpace(assetPath) && assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    yield return assetPath;
            }
        }

        private static string GuessUtilityId(string assetPath)
        {
            string path = (assetPath ?? string.Empty).Replace('\\', '/');
            if (path.IndexOf("/Utilities Core/Help/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "help-browser";
            if (path.IndexOf("PungentUtilityControlPanelWindow.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                return "utilities-browser";
            if (path.IndexOf("DocumentationLinkEditorPopup.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                return "documentation-links";
            if (path.IndexOf("PungentUtilityDesignAuditWindow.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                return "design-validation-audit";
            if (path.IndexOf("/Debug Control Window/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "debug-control";
            if (path.IndexOf("/Notepad/Notes/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "tooltip-notes";
            if (path.IndexOf("/Notepad/Tokens/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "token-validator";
            if (path.IndexOf("/Palette Designer/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "palette-designer";
            if (path.IndexOf("/Asset Placement Lab/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "asset-placement-lab";
            if (path.IndexOf("/Audio Coverage/", StringComparison.OrdinalIgnoreCase) >= 0)
                return path.IndexOf("Context", StringComparison.OrdinalIgnoreCase) >= 0 ? "audio-setup-coverage" : "audio-catalog-coverage";
            if (path.IndexOf("/Name Generator/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "name-generator";
            return string.Empty;
        }
    }

    internal static class PungentUtilityHelpFeatureEntryGenerationExtensions
    {
        public static string SourceOwnerOrEmpty(this PungentUtilityHelpFeatureEntry entry)
        {
            return entry == null || string.IsNullOrWhiteSpace(entry.location) || entry.location.IndexOf("Tooltip index", StringComparison.OrdinalIgnoreCase) < 0
                ? string.Empty
                : PungentUtilityHelpTooltipIndexer.TooltipOwner;
        }
    }
#endif
}
