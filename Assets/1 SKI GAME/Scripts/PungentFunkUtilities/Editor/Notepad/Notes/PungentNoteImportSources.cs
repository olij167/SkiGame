using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using UnityEditor;

    public static class PungentNoteImportSources
    {
        public static PungentNoteImportSource GetOrCreateCsvSource(string path)
        {
            PungentNoteDatabase db = PungentNoteStorage.Database;
            PungentNoteImportSource source = db.importSources.FirstOrDefault(s => s != null && string.Equals(s.sourcePath, path, StringComparison.OrdinalIgnoreCase));
            if (source != null)
                return source;

            source = new PungentNoteImportSource
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = string.IsNullOrWhiteSpace(path) ? "CSV Import" : Path.GetFileName(path),
                sourcePath = path ?? string.Empty,
                sourceType = "CSV",
                importedUtc = DateTime.UtcNow.ToString("o")
            };
            db.importSources.Add(source);
            return source;
        }

        public static void RecordApply(PungentNoteImportSource source, PungentBacklogSeedApplyResult result)
        {
            if (source == null || result == null)
                return;

            source.lastRefreshedUtc = DateTime.UtcNow.ToString("o");
            source.importedCount += result.notesCreated;
            source.skippedDuplicateCount += result.skipped;
            source.noteIds = Merge(source.noteIds, result.noteIds);
            source.futureUtilityIds = Merge(source.futureUtilityIds, result.futureUtilityIds);
            source.missing = !string.IsNullOrWhiteSpace(source.sourcePath) && !File.Exists(source.sourcePath);
            PungentNoteStorage.Save();
        }

        public static List<PungentNote> NotesFor(PungentNoteImportSource source)
        {
            if (source == null)
                return new List<PungentNote>();

            return PungentNoteStorage.Database.notes
                .Where(n => n != null && ((n.importSourceIds != null && n.importSourceIds.Contains(source.id)) || (source.noteIds != null && source.noteIds.Contains(n.id))))
                .ToList();
        }

        public static List<PungentBacklogSeedPreviewItem> ReimportPreview(PungentNoteImportSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.sourcePath) || !File.Exists(source.sourcePath))
                return new List<PungentBacklogSeedPreviewItem>();

            return PungentNoteBacklogSeeder.BuildPreview(PungentNoteInventoryImporter.ParseCsvFile(source.sourcePath));
        }

        public static void Reveal(PungentNoteImportSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.sourcePath))
                return;
            EditorUtility.RevealInFinder(source.sourcePath);
        }

        public static void RemoveReference(PungentNoteImportSource source)
        {
            if (source == null)
                return;
            PungentNoteStorage.Database.importSources.Remove(source);
            PungentNoteStorage.Save();
        }

        public static void Archive(PungentNoteImportSource source, bool archived)
        {
            if (source == null)
                return;
            source.archived = archived;
            PungentNoteStorage.Save();
        }

        private static List<string> Merge(List<string> a, List<string> b)
        {
            return (a ?? new List<string>())
                .Concat(b ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
#endif
}
