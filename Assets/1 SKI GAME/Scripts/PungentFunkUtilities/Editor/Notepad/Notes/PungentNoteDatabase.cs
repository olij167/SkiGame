using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [FilePath("ProjectSettings/PungentFunkUtilities/NotesAndRoadmap.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentNoteDatabase : ScriptableSingleton<PungentNoteDatabase>
    {
        public List<PungentNote> notes = new List<PungentNote>();
        public List<PungentFutureUtilityRecord> futureUtilities = new List<PungentFutureUtilityRecord>();
        public List<PungentNoteImportSource> importSources = new List<PungentNoteImportSource>();
        public PungentNoteDisplaySettings displaySettings = new PungentNoteDisplaySettings();
        public bool importedLegacyTooltipNotes;
        public string lastSavedUtc = string.Empty;

        public static string StorageLocation => "ProjectSettings/PungentFunkUtilities/NotesAndRoadmap.asset";

        public PungentNote CreateNote(string title = "New Note", PungentNoteKind kind = PungentNoteKind.General)
        {
            PungentNote note = new PungentNote
            {
                id = Guid.NewGuid().ToString("N"),
                title = string.IsNullOrWhiteSpace(title) ? "New Note" : title.Trim(),
                kind = kind,
                createdUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o")
            };
            notes.Add(note);
            PungentNoteStorage.Save();
            return note;
        }

        public PungentFutureUtilityRecord CreateFutureUtility()
        {
            PungentFutureUtilityRecord record = new PungentFutureUtilityRecord
            {
                id = Guid.NewGuid().ToString("N")
            };
            futureUtilities.Add(record);
            PungentNoteStorage.Save();
            return record;
        }

        public PungentNote Duplicate(PungentNote source)
        {
            if (source == null)
                return null;

            string now = DateTime.UtcNow.ToString("o");
            PungentNote copy = new PungentNote
            {
                id = Guid.NewGuid().ToString("N"),
                title = source.title + " Copy",
                body = source.body,
                kind = source.kind,
                status = source.status,
                priority = source.priority,
                visibility = source.visibility,
                tags = new List<string>(source.tags ?? new List<string>()),
                targets = CloneTargets(source.targets),
                relatedNoteIds = new List<string>(source.relatedNoteIds ?? new List<string>()),
                linkedTokenKeys = new List<string>(source.linkedTokenKeys ?? new List<string>()),
                importSourceIds = new List<string>(source.importSourceIds ?? new List<string>()),
                linkedUtilityId = source.linkedUtilityId,
                linkedFutureUtilityId = source.linkedFutureUtilityId,
                auditIssueCode = source.auditIssueCode,
                stableKey = source.stableKey,
                createdUtc = now,
                updatedUtc = now,
                archived = source.archived,
                developerOnly = source.developerOnly,
                locked = source.locked,
                generatedBy = source.generatedBy,
                generatedTemplateId = source.generatedTemplateId,
                generatedUtc = source.generatedUtc
            };
            notes.Add(copy);
            PungentNoteStorage.Save();
            return copy;
        }

        public void Touch(PungentNote note)
        {
            if (note != null)
                note.updatedUtc = DateTime.UtcNow.ToString("o");
        }

        public void Persist()
        {
            Save(true);
        }

        private static List<PungentNoteTargetLink> CloneTargets(List<PungentNoteTargetLink> source)
        {
            List<PungentNoteTargetLink> copy = new List<PungentNoteTargetLink>();
            if (source == null)
                return copy;

            for (int i = 0; i < source.Count; i++)
            {
                PungentNoteTargetLink link = source[i];
                if (link == null)
                    continue;

                copy.Add(new PungentNoteTargetLink
                {
                    type = link.type,
                    label = link.label,
                    utilityId = link.utilityId,
                    futureUtilityId = link.futureUtilityId,
                    assetGuid = link.assetGuid,
                    sceneObjectGlobalId = link.sceneObjectGlobalId,
                    componentType = link.componentType,
                    propertyPath = link.propertyPath,
                    scriptPath = link.scriptPath,
                    tokenKey = link.tokenKey,
                    auditIssueCode = link.auditIssueCode,
                    documentationLinkId = link.documentationLinkId,
                    noteId = link.noteId,
                    externalPathOrUrl = link.externalPathOrUrl
                });
            }

            return copy;
        }
    }
#endif
}
