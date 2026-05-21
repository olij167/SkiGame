using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.RichDocuments
{
    [Serializable]
    public sealed class PungentRichDocumentDatabase
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int migrationVersion = PungentRichDocument.CurrentMigrationVersion;
        public List<PungentRichDocument> documents = new List<PungentRichDocument>();
        public string lastSavedUtc = string.Empty;

        public IReadOnlyList<PungentRichDocument> Documents
        {
            get
            {
                EnsureDefaults();
                return documents;
            }
        }

        public void EnsureDefaults()
        {
            if (documents == null)
                documents = new List<PungentRichDocument>();

            schemaVersion = Math.Max(1, schemaVersion);
            migrationVersion = Math.Max(0, migrationVersion);
            NormalizeDocuments();
        }

        public PungentRichDocument CreateDocument(string title = null, string templateId = null)
        {
            EnsureDefaults();
            PungentRichDocument document = PungentRichDocument.Create(title, templateId);
            documents.Add(document);
            return document;
        }

        public void AddOrUpdate(PungentRichDocument document)
        {
            if (document == null)
                return;

            EnsureDefaults();
            document.NormalizeInPlace();

            int existing = documents.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, document.id));
            if (existing >= 0)
                documents[existing] = document;
            else
                documents.Add(document);
        }

        public bool Delete(string documentId)
        {
            EnsureDefaults();
            string normalized = PungentAuthoringId.Normalize(documentId);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            return documents.RemoveAll(item => item != null && PungentAuthoringId.EqualsId(item.id, normalized)) > 0;
        }

        public PungentRichDocument Find(string documentId)
        {
            EnsureDefaults();
            string normalized = PungentAuthoringId.Normalize(documentId);
            return string.IsNullOrWhiteSpace(normalized)
                ? null
                : documents.FirstOrDefault(item => item != null && PungentAuthoringId.EqualsId(item.id, normalized));
        }

        public List<PungentRichDocument> FindBySourceLegacyNoteId(string legacyNoteId)
        {
            EnsureDefaults();
            string normalized = PungentAuthoringId.Normalize(legacyNoteId);
            if (string.IsNullOrWhiteSpace(normalized))
                return new List<PungentRichDocument>();

            return documents
                .Where(item => item != null && PungentAuthoringId.EqualsId(item.sourceLegacyNoteId, normalized))
                .OrderByDescending(item => item.updatedUtc, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Touch(PungentRichDocument document)
        {
            if (document == null)
                return;

            document.Touch();
            AddOrUpdate(document);
        }

        private void NormalizeDocuments()
        {
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = documents.Count - 1; i >= 0; i--)
            {
                PungentRichDocument document = documents[i];
                if (document == null)
                {
                    documents.RemoveAt(i);
                    continue;
                }

                document.NormalizeInPlace();
                if (seenIds.Contains(document.id))
                {
                    document.id = PungentAuthoringId.NewValue();
                    document.NormalizeInPlace();
                }

                seenIds.Add(document.id);
            }
        }
    }
}
