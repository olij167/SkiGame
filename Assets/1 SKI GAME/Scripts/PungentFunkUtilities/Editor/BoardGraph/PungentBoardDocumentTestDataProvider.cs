using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public sealed class PungentBoardDocumentTestDataProvider : IPungentDocumentationTestDataProvider
    {
        private const string Id = PungentBoardProvider.Id;

        static PungentBoardDocumentTestDataProvider()
        {
            PungentDocumentationTestDataRegistry.Register(new PungentBoardDocumentTestDataProvider());
        }

        public string ProviderId => Id;
        public string DisplayName => "Boards";
        public string DocumentType => "Node Graphs";

        public IEnumerable<PungentDocumentationRecord> ListRecords()
        {
            foreach (PungentBoardDocument board in PungentBoardEditorStorage.Database.documents ?? new List<PungentBoardDocument>())
            {
                if (board == null)
                    continue;

                yield return new PungentDocumentationRecord
                {
                    providerId = ProviderId,
                    itemId = board.id,
                    kind = PungentAuthoringItemKind.Board,
                    typeLabel = DocumentType,
                    title = board.title,
                    summary = board.summary,
                    archived = board.archived,
                    locked = board.locked,
                    generated = IsGenerated(board),
                    generatedBy = board.generatedBy,
                    generatedTemplateId = board.generatedTemplateId,
                    generatedUtc = board.generatedUtc
                };
            }
        }

        public IEnumerable<PungentGeneratedDocumentTemplate> ListTemplates()
        {
            foreach (PungentBoardTemplateKind kind in PungentBoardTemplates.All)
            {
                yield return new PungentGeneratedDocumentTemplate
                {
                    providerId = ProviderId,
                    templateId = TemplateId(kind),
                    documentType = DocumentType,
                    title = PungentBoardTemplates.GetDisplayName(kind),
                    summary = "Generated board test data from the " + PungentBoardTemplates.GetDisplayName(kind) + " template."
                };
            }
        }

        public void Generate(string templateId, bool replaceExisting, PungentDocumentationManagementResult result)
        {
            PungentBoardTemplateKind kind;
            if (!TryTemplateKind(templateId, out kind))
            {
                result.skipped++;
                return;
            }

            PungentBoardDocument existing = FindGenerated(templateId);
            if (existing != null)
            {
                if (existing.locked)
                {
                    result.skippedLocked++;
                    result.Add("Locked board skipped: " + existing.title);
                    return;
                }

                if (!replaceExisting)
                {
                    result.skipped++;
                    return;
                }

                PungentBoardEditorStorage.Database.DeleteDocument(existing.id);
            }

            PungentBoardDocument board = PungentBoardTemplates.Create(kind);
            board.id = existing == null ? SafeGeneratedId(templateId) : existing.id;
            board.tags = MergeTags(board.tags, "generated-test-document", "documentation-test-data");
            board.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
            board.generatedTemplateId = templateId;
            board.generatedUtc = DateTime.UtcNow.ToString("o");
            board.NormalizeInPlace();
            PungentBoardEditorStorage.Database.UpsertDocument(board);
            PungentBoardEditorStorage.Save(out _);
            if (existing == null)
                result.created++;
            else
                result.updated++;
        }

        public void Remove(IEnumerable<string> itemIds, bool requireGenerated, PungentDocumentationManagementResult result)
        {
            bool dirty = false;
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentBoardDocument board = Find(itemId);
                if (board == null)
                {
                    result.skipped++;
                    continue;
                }

                if (board.locked)
                {
                    result.skippedLocked++;
                    continue;
                }

                if (requireGenerated && !IsGenerated(board))
                {
                    result.skipped++;
                    continue;
                }

                dirty |= PungentBoardEditorStorage.Database.DeleteDocument(board.id);
                result.removed++;
            }

            if (dirty)
                PungentBoardEditorStorage.Save(out _);
        }

        public void SetLocked(IEnumerable<string> itemIds, bool locked, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, board =>
            {
                board.locked = locked;
                if (locked)
                    result.locked++;
                else
                    result.unlocked++;
            }, result);
        }

        public void AdoptAsGenerated(IEnumerable<string> itemIds, PungentDocumentationManagementResult result)
        {
            Mutate(itemIds, board =>
            {
                board.generatedBy = PungentDocumentationTestDataRegistry.GeneratedBy;
                board.generatedTemplateId = string.IsNullOrWhiteSpace(board.generatedTemplateId) ? "adopted-board-" + board.id : board.generatedTemplateId;
                board.generatedUtc = DateTime.UtcNow.ToString("o");
                result.adopted++;
            }, result);
        }

        private static void Mutate(IEnumerable<string> itemIds, Action<PungentBoardDocument> mutation, PungentDocumentationManagementResult result)
        {
            bool dirty = false;
            foreach (string itemId in itemIds ?? new string[0])
            {
                PungentBoardDocument board = Find(itemId);
                if (board == null)
                {
                    result.skipped++;
                    continue;
                }

                mutation(board);
                PungentBoardEditorStorage.Database.UpsertDocument(board);
                dirty = true;
            }

            if (dirty)
                PungentBoardEditorStorage.Save(out _);
        }

        private static PungentBoardDocument Find(string itemId)
        {
            return PungentBoardEditorStorage.Database.FindDocument(itemId);
        }

        private static PungentBoardDocument FindGenerated(string templateId)
        {
            return (PungentBoardEditorStorage.Database.documents ?? new List<PungentBoardDocument>()).FirstOrDefault(item => item != null && string.Equals(item.generatedTemplateId, templateId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGenerated(PungentBoardDocument board)
        {
            return board != null &&
                   (!string.IsNullOrWhiteSpace(board.generatedTemplateId) ||
                    string.Equals(board.generatedBy, PungentDocumentationTestDataRegistry.GeneratedBy, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeGeneratedId(string templateId)
        {
            string stable = PungentDocumentationTestDataRegistry.StableGeneratedId(Id, templateId);
            PungentBoardDocument existing = Find(stable);
            return existing == null || IsGenerated(existing) ? stable : PungentAuthoringId.NewValue();
        }

        private static string TemplateId(PungentBoardTemplateKind kind)
        {
            return "board." + kind;
        }

        private static bool TryTemplateKind(string templateId, out PungentBoardTemplateKind kind)
        {
            kind = PungentBoardTemplateKind.BlankBoard;
            string value = (templateId ?? string.Empty).StartsWith("board.", StringComparison.OrdinalIgnoreCase)
                ? templateId.Substring("board.".Length)
                : templateId ?? string.Empty;
            return Enum.TryParse(value, true, out kind);
        }

        private static List<string> MergeTags(List<string> source, params string[] tags)
        {
            HashSet<string> result = new HashSet<string>(source ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (string tag in tags ?? new string[0])
                if (!string.IsNullOrWhiteSpace(tag))
                    result.Add(tag);
            return result.ToList();
        }
    }
#endif
}
