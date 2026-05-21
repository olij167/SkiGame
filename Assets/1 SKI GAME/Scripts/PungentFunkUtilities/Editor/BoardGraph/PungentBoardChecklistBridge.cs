using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.ProjectAudit;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public static class PungentBoardChecklistBridge
    {
        public static bool AttachChecklistReference(PungentBoardDocument document, string checklistId)
        {
            if (document == null || string.IsNullOrWhiteSpace(checklistId))
                return false;

            PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.Find(checklistId);
            if (checklist == null)
                return false;

            document.references = document.references ?? new List<PungentAuthoringReference>();
            AddReference(document.references, checklist.ToReference());
            document.NormalizeInPlace();
            return true;
        }

        public static bool AttachChecklistReference(PungentBoardNode node, string checklistId, bool asPrimaryLink)
        {
            if (node == null || string.IsNullOrWhiteSpace(checklistId))
                return false;

            PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.Find(checklistId);
            if (checklist == null)
                return false;

            PungentAuthoringReference reference = checklist.ToReference();
            if (asPrimaryLink)
                node.linkedAuthoringRef = reference;
            else
            {
                node.references = node.references ?? new List<PungentAuthoringReference>();
                AddReference(node.references, reference);
            }

            node.NormalizeInPlace();
            return true;
        }

        public static bool TryOpenChecklistOverlay(PungentAuthoringReference reference, Rect anchorRect)
        {
            if (reference == null || !reference.HasItemId)
                return false;

            if (reference.itemKind != PungentAuthoringItemKind.Checklist &&
                !string.Equals(reference.providerId, PungentChecklistConstants.ProviderId, StringComparison.OrdinalIgnoreCase))
                return false;

            PungentStickyNoteOverlayController.OpenAuthoringPreview(reference, null, anchorRect, PungentStickyNoteOverlayOwner.AuthoringBrowser, "Board Checklist");
            return true;
        }

        public static bool TryGetChecklistProgress(PungentAuthoringReference reference, out string progress)
        {
            progress = string.Empty;
            if (reference == null || !reference.HasItemId)
                return false;

            PungentChecklistDefinition checklist = PungentChecklistDefinitionRegistry.Find(reference.itemId);
            if (checklist == null)
                return false;

            PungentChecklistProgressSummary summary = PungentChecklistUtilityStateService.Count(checklist, PungentChecklistDefinitionRegistry.Find, true);
            progress = summary.total <= 0
                ? "No checklist items"
                : summary.complete + "/" + summary.total + " complete";
            if (summary.problem > 0)
                progress += " | " + summary.problem + " needs attention";
            return true;
        }

        public static bool TryCreateBoardReviewChecklist(PungentBoardDocument document, IEnumerable<PungentBoardNode> nodes, out PungentChecklistDefinition checklist, out string error)
        {
            checklist = null;
            error = string.Empty;
            if (document == null)
            {
                error = "Board document is missing.";
                return false;
            }

            List<PungentBoardNode> sourceNodes = (nodes ?? document.nodes ?? new List<PungentBoardNode>())
                .Where(node => node != null)
                .ToList();
            if (sourceNodes.Count == 0)
            {
                error = "Board has no nodes to turn into review checklist items.";
                return false;
            }

            string baseId = "board-" + PungentAuthoringId.Normalize(document.id) + "-review";
            checklist = new PungentChecklistDefinition
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = PungentChecklistDefinitionRegistry.UniqueProjectChecklistId(baseId),
                title = (string.IsNullOrWhiteSpace(document.title) ? "Board" : document.title) + " Review Checklist",
                description = "Review checklist generated from Board / Graph nodes.",
                listKind = PungentChecklistListKinds.Review,
                stateProfileId = PungentChecklistProfileIds.Review,
                targetUtilityId = PungentBoardProvider.UtilityId,
                sourceProviderId = PungentBoardProvider.Id,
                sourceItemId = document.id,
                sourceLabel = document.title,
                tags = PungentAuthoringMetadata.NormalizeTags(new[] { "board", "review", "checklist" }),
                sections = new List<PungentChecklistSectionDefinition>
                {
                    new PungentChecklistSectionDefinition
                    {
                        id = "nodes",
                        title = "Nodes",
                        guidancePrompt = "Review node labels, summaries, links, and graph flow.",
                        items = new List<PungentChecklistItemDefinition>()
                    }
                }
            };

            HashSet<string> usedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardNode node in sourceNodes)
            {
                string label = string.IsNullOrWhiteSpace(node.title) ? node.id : node.title;
                checklist.sections[0].items.Add(new PungentChecklistItemDefinition
                {
                    id = PungentChecklistSerialization.UniqueId(label, usedItemIds, "node"),
                    label = label,
                    detail = string.IsNullOrWhiteSpace(node.summary) ? node.body : node.summary,
                    linkedStateKey = "board-node:" + node.id,
                    owner = "Reviewer",
                    priority = "Normal"
                });
            }

            checklist.NormalizeInPlace();
            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, new List<PungentLinkedStateGroupDefinition>(), out error))
                return false;

            AttachChecklistReference(document, checklist.checklistId);
            return true;
        }

        private static void AddReference(List<PungentAuthoringReference> references, PungentAuthoringReference reference)
        {
            if (references == null || reference == null || !reference.HasItemId)
                return;

            if (references.Any(existing => existing != null &&
                                           existing.itemKind == reference.itemKind &&
                                           PungentAuthoringId.EqualsId(existing.itemId, reference.itemId)))
                return;

            references.Add(reference);
        }
    }
#endif
}
