using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.BoardGraph
{
    [Serializable]
    public sealed class PungentBoardDocumentDatabase
    {
        public int schemaVersion = PungentBoardDocument.CurrentMigrationVersion;
        public int migrationVersion = PungentBoardDocument.CurrentMigrationVersion;
        public List<PungentBoardDocument> documents = new List<PungentBoardDocument>();
        public string lastSavedUtc = string.Empty;

        public PungentBoardDocument CreateDocument(string title = "Untitled Board")
        {
            EnsureLoaded();
            PungentBoardDocument document = PungentBoardDocument.Create(title);
            documents.Add(document);
            Touch(document);
            return document;
        }

        public bool DeleteDocument(string boardId)
        {
            EnsureLoaded();
            string clean = PungentAuthoringId.Normalize(boardId);
            for (int i = documents.Count - 1; i >= 0; i--)
            {
                PungentBoardDocument document = documents[i];
                if (document != null && PungentAuthoringId.EqualsId(document.id, clean))
                {
                    documents.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public PungentBoardDocument FindDocument(string boardId)
        {
            EnsureLoaded();
            for (int i = 0; i < documents.Count; i++)
            {
                PungentBoardDocument document = documents[i];
                if (document != null && PungentAuthoringId.EqualsId(document.id, boardId))
                    return document;
            }

            return null;
        }

        public PungentBoardDocument UpsertDocument(PungentBoardDocument document)
        {
            EnsureLoaded();
            if (document == null)
                return null;

            document.NormalizeInPlace();
            for (int i = 0; i < documents.Count; i++)
            {
                if (documents[i] != null && PungentAuthoringId.EqualsId(documents[i].id, document.id))
                {
                    documents[i] = document;
                    Touch(document);
                    return document;
                }
            }

            documents.Add(document);
            Touch(document);
            return document;
        }

        public void Touch(PungentBoardDocument document)
        {
            if (document == null)
                return;

            string now = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrWhiteSpace(document.createdUtc))
                document.createdUtc = now;
            document.updatedUtc = now;
        }

        public void EnsureLoaded()
        {
            documents = documents ?? new List<PungentBoardDocument>();
        }

        public void NormalizeInPlace()
        {
            schemaVersion = Math.Max(PungentBoardDocument.CurrentMigrationVersion, schemaVersion);
            migrationVersion = Math.Max(PungentBoardDocument.CurrentMigrationVersion, migrationVersion);
            EnsureLoaded();
            for (int i = documents.Count - 1; i >= 0; i--)
            {
                if (documents[i] == null)
                {
                    documents.RemoveAt(i);
                    continue;
                }

                documents[i].NormalizeInPlace();
            }

            if (string.IsNullOrWhiteSpace(lastSavedUtc))
                lastSavedUtc = string.Empty;
        }
    }
}
