using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentNoteBridge
    {
        public static bool TryCreateLinkedCopy(PungentNote note, out PungentRichDocument document, out string error, bool saveImmediately = true)
        {
            document = null;
            error = string.Empty;
            if (note == null)
            {
                error = "Legacy note could not be found.";
                return false;
            }

            PungentAuthoringReference legacyReference = PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, note.id, PungentAuthoringLegacyNoteProvider.Id, note.title);
            PungentAuthoringLegacyNoteProvider legacyProvider = new PungentAuthoringLegacyNoteProvider();

            document = PungentRichDocumentStorage.CreateDocument(string.IsNullOrWhiteSpace(note.title) ? "Imported Legacy Note" : note.title, "legacy-note-copy");
            document.summary = PungentRichDocument.BuildPreview(note.body);
            document.kind = "Legacy Note Copy";
            document.status = note.status.ToString();
            document.priority = note.priority.ToString();
            document.visibility = note.visibility.ToString();
            document.tags = PungentAuthoringMetadata.NormalizeTags(note.tags);
            document.developerOnly = note.developerOnly;
            document.archived = false;
            document.sourceLegacyNoteId = note.id;
            document.bodyText = note.body ?? string.Empty;
            document.blocks = CreateBlocksFromBody(note.body);
            document.references = legacyProvider.GetReferences(legacyReference).ToList();
            document.references.Insert(0, legacyReference);
            document.targets = legacyProvider.GetTargets(legacyReference).ToList();
            document.NormalizeInPlace();
            document.Touch();
            PungentRichDocumentStorage.Database.AddOrUpdate(document);

            // Linked copy only: this bridge never mutates, clears, or deletes the source PungentNote.body.
            if (!saveImmediately)
                return true;

            return PungentRichDocumentStorage.Save(out error);
        }

        private static List<PungentRichDocumentBlock> CreateBlocksFromBody(string body)
        {
            return PungentRichDocumentBlockSync.ParseBodyToBlocks(body);
        }

        private static bool TryStripMarker(string trimmed, string marker, out string text)
        {
            text = string.Empty;
            string working = StripListPrefix(trimmed);
            string prefix = "[" + marker + "]";
            if (!working.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            text = working.Substring(prefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(text);
        }

        private static bool TryParseCommand(string trimmed, out string command)
        {
            command = string.Empty;
            if (!trimmed.StartsWith("<command:", StringComparison.OrdinalIgnoreCase) || !trimmed.EndsWith(">", StringComparison.Ordinal))
                return false;

            command = trimmed.Substring("<command:".Length, trimmed.Length - "<command:".Length - 1).Trim();
            return !string.IsNullOrWhiteSpace(command);
        }

        private static bool TryParseSpeakerLine(string trimmed, out string speaker, out string text)
        {
            speaker = string.Empty;
            text = string.Empty;
            int colon = trimmed.IndexOf(':');
            if (colon <= 0 || colon > 40)
                return false;

            string candidate = trimmed.Substring(0, colon).Trim();
            if (candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase) || candidate.IndexOf('<') >= 0 || candidate.IndexOf('{') >= 0)
                return false;

            string remainder = trimmed.Substring(colon + 1).Trim();
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(remainder))
                return false;

            speaker = candidate;
            text = remainder;
            return true;
        }

        private static string StripListPrefix(string trimmed)
        {
            string working = (trimmed ?? string.Empty).Trim();
            if (working.StartsWith("- ", StringComparison.Ordinal) || working.StartsWith("* ", StringComparison.Ordinal))
                return working.Substring(2).Trim();

            int dot = working.IndexOf('.');
            if (dot > 0 && int.TryParse(working.Substring(0, dot), out _))
                return working.Substring(dot + 1).Trim();

            return working;
        }
    }
#endif
}
