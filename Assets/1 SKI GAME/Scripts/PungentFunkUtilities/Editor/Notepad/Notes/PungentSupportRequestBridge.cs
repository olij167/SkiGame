using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    public static class PungentSupportRequestBridge
    {
        private const string RequestTag = "support-request";

        public static PungentNote OpenFromBugReportContext(PungentBugReportContext context, PungentBugReportCategory category = PungentBugReportCategory.Bug)
        {
            PungentNote note = CreateSupportRequest(context, category);
            PungentNotesRoadmapWindow.OpenAndSelect(note.id);
            return note;
        }

        public static PungentNote CreateSupportRequest(PungentBugReportContext context, PungentBugReportCategory category = PungentBugReportCategory.Bug, string title = null, string body = null)
        {
            context = context ?? new PungentBugReportContext();
            context.Normalize();

            PungentNote note = PungentNoteStorage.Database.CreateNote(string.IsNullOrWhiteSpace(title) ? DefaultTitle(context, category) : title.Trim(), PungentNoteKind.SupportRequest);
            note.body = string.IsNullOrWhiteSpace(body) ? DefaultBody(context, category) : body;
            note.status = PungentNoteStatus.ToDo;
            note.priority = category == PungentBugReportCategory.MissingFeature ? PungentNotePriority.NiceToHave : PungentNotePriority.Important;
            note.visibility = PungentNoteVisibility.PrivateProject;
            note.linkedUtilityId = context.utilityId;
            note.stableKey = "support-request:" + note.id;
            EnsureTag(note, RequestTag);
            EnsureTag(note, category == PungentBugReportCategory.MissingFeature ? "feature-request" : "bug-report");

            if (!string.IsNullOrWhiteSpace(context.utilityId))
            {
                note.targets.Add(new PungentNoteTargetLink
                {
                    type = PungentNoteTargetType.RegisteredUtility,
                    utilityId = context.utilityId,
                    label = context.utilityId
                });
            }

            PungentSupportRequestRecord record = GetOrCreateRecord(note);
            if (record != null)
            {
                record.category = category;
                record.severity = PungentBugReportSeverity.Normal;
                record.context = context;
                PungentBugReportStorage.instance.TouchSupportRequest(record);
            }

            PungentNoteStorage.Save();
            return note;
        }

        public static void OpenRequests()
        {
            PungentNotesRoadmapWindow.OpenRequests();
        }

        public static bool IsSupportRequest(PungentNote note)
        {
            return note != null && note.kind == PungentNoteKind.SupportRequest;
        }

        public static PungentSupportRequestRecord GetOrCreateRecord(PungentNote note)
        {
            if (note == null)
                return null;
            PungentSupportRequestRecord record = PungentBugReportStorage.instance.GetOrCreateSupportRequest(note.id);
            if (record != null && record.context == null)
                record.context = new PungentBugReportContext();
            ApplySharedContactDefaults(record);
            return record;
        }

        public static bool IsFeatureRequest(PungentSupportRequestRecord record)
        {
            return record != null && record.category == PungentBugReportCategory.MissingFeature;
        }

        public static void SetRequestKind(PungentSupportRequestRecord record, bool featureRequest)
        {
            if (record == null)
                return;
            record.category = featureRequest ? PungentBugReportCategory.MissingFeature : PungentBugReportCategory.Bug;
            PungentBugReportStorage.instance.TouchSupportRequest(record);
        }

        public static PungentBugReportSeverity[] PriorityOrder { get; } =
        {
            PungentBugReportSeverity.Low,
            PungentBugReportSeverity.Normal,
            PungentBugReportSeverity.High,
            PungentBugReportSeverity.Blocking,
            PungentBugReportSeverity.DataLoss
        };

        public static string[] PriorityLabels { get; } =
        {
            "1 Low",
            "2 Normal",
            "3 High",
            "4 Blocking",
            "5 Data Loss"
        };

        public static int PriorityIndex(PungentBugReportSeverity severity)
        {
            for (int i = 0; i < PriorityOrder.Length; i++)
            {
                if (PriorityOrder[i] == severity)
                    return i;
            }

            return 1;
        }

        public static PungentBugReportSeverity PriorityFromIndex(int index)
        {
            return PriorityOrder[Math.Max(0, Math.Min(PriorityOrder.Length - 1, index))];
        }

        public static void UpdateSharedContact(PungentSupportRequestRecord record, string email, string discord)
        {
            PungentBugReportStorage storage = PungentBugReportStorage.instance;
            string cleanEmail = Clean(email);
            string cleanDiscord = Clean(discord);
            storage.EnsureDefaults();
            storage.defaultContactEmail = cleanEmail;
            storage.defaultContactDiscord = cleanDiscord;

            foreach (PungentSupportRequestRecord existing in storage.supportRequests ?? Enumerable.Empty<PungentSupportRequestRecord>())
            {
                if (existing == null)
                    continue;
                existing.contactEmail = cleanEmail;
                existing.contactDiscord = cleanDiscord;
                existing.updatedUtc = DateTime.UtcNow.ToString("o");
            }

            if (record != null)
            {
                record.contactEmail = cleanEmail;
                record.contactDiscord = cleanDiscord;
            }

            storage.Persist();
        }

        public static PungentBugReportPayload BuildPayload(PungentNote note, PungentSupportRequestRecord record)
        {
            record = record ?? GetOrCreateRecord(note);
            PungentBugReportContext context = record == null ? new PungentBugReportContext() : record.context ?? new PungentBugReportContext();
            PungentBugReportPayload payload = PungentBugReportService.BuildPayload(
                context,
                note == null ? string.Empty : note.title,
                note == null ? string.Empty : note.body,
                record == null ? PungentBugReportCategory.Bug : record.category,
                record == null ? PungentBugReportSeverity.Normal : record.severity,
                record == null ? string.Empty : record.contactEmail,
                record == null ? string.Empty : record.contactDiscord,
                record != null && record.includeDiagnostics,
                record != null && record.includeConsoleSummary);

            if (record != null)
            {
                payload.parentLocalId = record.parentNoteId;
                payload.parentRemoteReportId = record.parentRemoteReportId;
            }

            return payload;
        }

        public static bool Send(PungentNote note, Action<bool, string> onComplete)
        {
            if (note == null)
            {
                onComplete?.Invoke(false, "No support request note selected.");
                return false;
            }

            PungentSupportRequestRecord record = GetOrCreateRecord(note);
            string validation = Validate(note, record);
            if (!string.IsNullOrEmpty(validation))
            {
                onComplete?.Invoke(false, validation);
                return false;
            }

            PungentBugReportPayload payload = BuildPayload(note, record);
            if (!PungentBugReportService.TrySubmit(payload, (success, message, reportId) =>
            {
                ApplySubmitResult(note, record, payload, success, message, reportId);
                onComplete?.Invoke(success, message);
            }, out string status))
            {
                record.state = PungentSupportRequestRelayState.Queued;
                record.lastError = status;
                record.lastStatus = "Queued locally.";
                record.queuedPayload = payload;
                PungentBugReportService.QueueLocally(payload);
                PungentBugReportStorage.instance.TouchSupportRequest(record);
                onComplete?.Invoke(false, status);
                return false;
            }

            record.lastStatus = status;
            PungentBugReportStorage.instance.TouchSupportRequest(record);
            return true;
        }

        public static void QueueLocally(PungentNote note)
        {
            PungentSupportRequestRecord record = GetOrCreateRecord(note);
            if (record == null)
                return;
            PungentBugReportPayload payload = BuildPayload(note, record);
            record.state = PungentSupportRequestRelayState.Queued;
            record.lastStatus = "Queued locally.";
            record.lastError = string.Empty;
            record.queuedPayload = payload;
            PungentBugReportService.QueueLocally(payload);
            PungentBugReportStorage.instance.TouchSupportRequest(record);
        }

        public static void CopyJson(PungentNote note)
        {
            PungentBugReportService.CopyPayloadToClipboard(BuildPayload(note, GetOrCreateRecord(note)));
        }

        public static void OpenSendReview(PungentNote note, UnityEngine.Rect anchorRect, PungentStickyNoteOverlayOwner owner, string sourceLabel)
        {
            if (note == null)
                return;
            PungentStickyNoteOverlayController.OpenSupportRequestReview(note.id, anchorRect, owner, string.IsNullOrWhiteSpace(sourceLabel) ? "Support Request" : sourceLabel);
        }

        public static bool IsSent(PungentNote note)
        {
            PungentSupportRequestRecord record = GetOrCreateRecord(note);
            return record != null && record.state == PungentSupportRequestRelayState.Sent;
        }

        public static string BrowserSendLabel(PungentNote note)
        {
            PungentSupportRequestRecord record = GetOrCreateRecord(note);
            if (record != null && (record.state == PungentSupportRequestRelayState.Queued || record.state == PungentSupportRequestRelayState.Failed))
                return "Retry Send";
            return "Send";
        }

        public static void CopyRequestMetadata(PungentNote source, PungentNote copy)
        {
            if (!IsSupportRequest(source) || !IsSupportRequest(copy))
                return;

            PungentSupportRequestRecord sourceRecord = GetOrCreateRecord(source);
            PungentSupportRequestRecord copyRecord = GetOrCreateRecord(copy);
            if (sourceRecord == null || copyRecord == null)
                return;

            copyRecord.state = PungentSupportRequestRelayState.Draft;
            copyRecord.category = sourceRecord.category;
            copyRecord.severity = sourceRecord.severity;
            copyRecord.contactEmail = sourceRecord.contactEmail;
            copyRecord.contactDiscord = sourceRecord.contactDiscord;
            copyRecord.includeDiagnostics = sourceRecord.includeDiagnostics;
            copyRecord.includeConsoleSummary = sourceRecord.includeConsoleSummary;
            copyRecord.remoteReportId = string.Empty;
            copyRecord.sentUtc = string.Empty;
            copyRecord.parentNoteId = sourceRecord.parentNoteId;
            copyRecord.parentRemoteReportId = sourceRecord.parentRemoteReportId;
            copyRecord.lastError = string.Empty;
            copyRecord.lastStatus = "Duplicated draft.";
            copyRecord.queuedPayload = new PungentBugReportPayload();
            copyRecord.context = CloneContext(sourceRecord.context);
            copy.locked = false;
            if (copy.status == PungentNoteStatus.Complete)
                copy.status = PungentNoteStatus.ToDo;
            PungentBugReportStorage.instance.TouchSupportRequest(copyRecord);
            PungentNoteStorage.Database.Touch(copy);
            PungentNoteStorage.Save();
        }

        public static PungentNote CreateFollowUp(PungentNote original)
        {
            if (original == null)
                return null;

            PungentSupportRequestRecord originalRecord = GetOrCreateRecord(original);
            PungentBugReportContext context = originalRecord == null ? new PungentBugReportContext() : originalRecord.context ?? new PungentBugReportContext();
            PungentNote followUp = CreateSupportRequest(
                context,
                originalRecord == null ? PungentBugReportCategory.Bug : originalRecord.category,
                "Re: " + (string.IsNullOrWhiteSpace(original.title) ? "Support Request" : original.title),
                "Follow-up for support request " + original.id + (string.IsNullOrWhiteSpace(originalRecord?.remoteReportId) ? "." : " / remote " + originalRecord.remoteReportId + ".") + "\n\n## Update\n\n");

            EnsureRelated(original, followUp.id);
            EnsureRelated(followUp, original.id);

            PungentSupportRequestRecord followRecord = GetOrCreateRecord(followUp);
            if (followRecord != null)
            {
                followRecord.parentNoteId = original.id;
                followRecord.parentRemoteReportId = originalRecord == null ? string.Empty : originalRecord.remoteReportId;
                followRecord.contactEmail = originalRecord == null ? string.Empty : originalRecord.contactEmail;
                followRecord.contactDiscord = originalRecord == null ? string.Empty : originalRecord.contactDiscord;
                followRecord.severity = originalRecord == null ? PungentBugReportSeverity.Normal : originalRecord.severity;
                PungentBugReportStorage.instance.TouchSupportRequest(followRecord);
            }

            PungentNoteStorage.Save();
            return followUp;
        }

        public static void ArchiveLocally(PungentNote note)
        {
            if (note == null)
                return;
            PungentNoteStorage.Archive(note, true);
        }

        public static PungentNote CreateArchiveFollowUp(PungentNote original)
        {
            if (original == null)
                return null;

            PungentSupportRequestRecord originalRecord = GetOrCreateRecord(original);
            PungentBugReportContext context = originalRecord == null ? new PungentBugReportContext() : originalRecord.context ?? new PungentBugReportContext();
            PungentNote followUp = CreateSupportRequest(
                context,
                originalRecord == null ? PungentBugReportCategory.Other : originalRecord.category,
                "Re: " + (string.IsNullOrWhiteSpace(original.title) ? "Support Request" : original.title),
                "This is an automatic follow-up to request archival of the original support request.\n\nOriginal local ID: " + original.id +
                (string.IsNullOrWhiteSpace(originalRecord?.remoteReportId) ? string.Empty : "\nOriginal remote ID: " + originalRecord.remoteReportId) +
                "\n\nPlease archive/close the original support ticket on the backend/support side.");

            EnsureRelated(original, followUp.id);
            EnsureRelated(followUp, original.id);

            PungentSupportRequestRecord followRecord = GetOrCreateRecord(followUp);
            if (followRecord != null)
            {
                followRecord.parentNoteId = original.id;
                followRecord.parentRemoteReportId = originalRecord == null ? string.Empty : originalRecord.remoteReportId;
                followRecord.contactEmail = originalRecord == null ? string.Empty : originalRecord.contactEmail;
                followRecord.contactDiscord = originalRecord == null ? string.Empty : originalRecord.contactDiscord;
                followRecord.severity = PungentBugReportSeverity.Normal;
                PungentBugReportStorage.instance.TouchSupportRequest(followRecord);
            }

            PungentNoteStorage.Save();
            return followUp;
        }

        public static void ArchiveSupportTicket(PungentNote original, Action<bool, string> onComplete)
        {
            PungentNote followUp = CreateArchiveFollowUp(original);
            if (followUp == null)
            {
                onComplete?.Invoke(false, "Could not create archive follow-up.");
                return;
            }

            Send(followUp, (success, message) =>
            {
                ArchiveLocally(original);
                onComplete?.Invoke(success, success ? "Sent archive follow-up and archived locally." : "Archive follow-up was queued or failed; original request was archived locally. " + message);
            });
        }

        public static List<PungentNote> GetSupportRequestNotes(bool includeArchived = true)
        {
            PungentNoteStorage.EnsureLoaded();
            return PungentNoteStorage.Database.notes
                .Where(note => note != null && note.kind == PungentNoteKind.SupportRequest && (includeArchived || !note.archived))
                .ToList();
        }

        private static void ApplySubmitResult(PungentNote note, PungentSupportRequestRecord record, PungentBugReportPayload payload, bool success, string message, string reportId)
        {
            if (record == null)
                return;

            if (success)
            {
                record.state = PungentSupportRequestRelayState.Sent;
                record.remoteReportId = reportId ?? string.Empty;
                record.sentUtc = DateTime.UtcNow.ToString("o");
                record.lastError = string.Empty;
                record.lastStatus = string.IsNullOrWhiteSpace(message) ? "Sent." : message;
                record.queuedPayload = payload;
                if (note != null)
                {
                    note.status = PungentNoteStatus.Complete;
                    note.locked = true;
                    PungentNoteStorage.Database.Touch(note);
                    PungentNoteStorage.Save();
                }
            }
            else
            {
                record.state = PungentSupportRequestRelayState.Failed;
                record.lastError = message ?? string.Empty;
                record.lastStatus = "Relay failed; queued locally.";
                record.queuedPayload = payload;
            }

            PungentBugReportStorage.instance.TouchSupportRequest(record);
        }

        private static string Validate(PungentNote note, PungentSupportRequestRecord record)
        {
            if (note == null)
                return "No support request note selected.";
            if (string.IsNullOrWhiteSpace(note.title) || note.title.Trim().Length < 3)
                return "Add a short request title before sending.";
            if (string.IsNullOrWhiteSpace(note.body) || note.body.Trim().Length < 10)
                return "Add request details before sending.";
            if (record != null && record.state == PungentSupportRequestRelayState.Sent)
                return "This request has already been sent. Create a follow-up instead.";
            return string.Empty;
        }

        private static void EnsureTag(PungentNote note, string tag)
        {
            if (note == null || string.IsNullOrWhiteSpace(tag))
                return;
            if (note.tags == null)
                note.tags = new List<string>();
            if (!note.tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
                note.tags.Add(tag);
        }

        private static void EnsureRelated(PungentNote note, string relatedId)
        {
            if (note == null || string.IsNullOrWhiteSpace(relatedId))
                return;
            if (note.relatedNoteIds == null)
                note.relatedNoteIds = new List<string>();
            if (!note.relatedNoteIds.Any(id => string.Equals(id, relatedId, StringComparison.OrdinalIgnoreCase)))
                note.relatedNoteIds.Add(relatedId);
        }

        private static void ApplySharedContactDefaults(PungentSupportRequestRecord record)
        {
            if (record == null)
                return;

            PungentBugReportStorage storage = PungentBugReportStorage.instance;
            storage.EnsureDefaults();
            bool changed = false;
            if (string.IsNullOrWhiteSpace(record.contactEmail) && !string.IsNullOrWhiteSpace(storage.defaultContactEmail))
            {
                record.contactEmail = storage.defaultContactEmail;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(record.contactDiscord) && !string.IsNullOrWhiteSpace(storage.defaultContactDiscord))
            {
                record.contactDiscord = storage.defaultContactDiscord;
                changed = true;
            }

            if (changed)
                storage.TouchSupportRequest(record);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static PungentBugReportContext CloneContext(PungentBugReportContext source)
        {
            if (source == null)
                return new PungentBugReportContext();

            return new PungentBugReportContext
            {
                utilityId = source.utilityId,
                sectionId = source.sectionId,
                topicId = source.topicId,
                contextLabel = source.contextLabel,
                contextPath = source.contextPath,
                sourceWindow = source.sourceWindow,
                helpTab = source.helpTab,
                selectedContextId = source.selectedContextId,
                selectedContextLabel = source.selectedContextLabel,
                sourcePath = source.sourcePath,
                sourceLine = source.sourceLine,
                generatedEntryId = source.generatedEntryId
            };
        }

        private static string DefaultTitle(PungentBugReportContext context, PungentBugReportCategory category)
        {
            string label = context == null ? string.Empty : context.contextLabel;
            string topic = context == null ? string.Empty : context.topicId;
            string prefix = category == PungentBugReportCategory.MissingFeature ? "Feature request: " : "Support request: ";
            if (!string.IsNullOrWhiteSpace(label))
                return prefix + label;
            if (!string.IsNullOrWhiteSpace(topic))
                return prefix + ObjectNames.NicifyVariableName(topic);
            return prefix + "PungentFunk Utilities";
        }

        private static string DefaultBody(PungentBugReportContext context, PungentBugReportCategory category)
        {
            string kind = category == PungentBugReportCategory.MissingFeature ? "Feature request" : "Bug report";
            string utility = context == null || string.IsNullOrWhiteSpace(context.utilityId) ? "(none)" : context.utilityId;
            string topic = context == null ? string.Empty : context.StableTopicId;
            return "## " + kind + "\n\nDescribe what happened, what you expected, and any steps that help reproduce or understand it.\n\n" +
                   "## Context\n\nUtility: " + utility + "\nTopic: " + topic + "\n";
        }
    }
#endif
}
