using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    public sealed class PungentChecklistDefinitionEditSession
    {
        private string _sourceId = string.Empty;
        private string _baselineJson = string.Empty;
        private bool _dirty;

        public PungentChecklistDefinition Draft { get; private set; }

        public bool HasDraft => Draft != null;
        public bool IsDirty => Draft != null && _dirty;
        public string SourceId => _sourceId;

        public void Load(PungentChecklistDefinition source)
        {
            Draft = PungentChecklistSerialization.Clone(source);
            Draft?.NormalizeInPlace();
            _sourceId = Draft == null ? string.Empty : Draft.checklistId;
            _baselineJson = Draft == null ? string.Empty : JsonUtility.ToJson(Draft, false);
            _dirty = false;
        }

        public void Clear()
        {
            Draft = null;
            _sourceId = string.Empty;
            _baselineJson = string.Empty;
            _dirty = false;
        }

        public void MarkClean()
        {
            if (Draft != null)
                _baselineJson = JsonUtility.ToJson(Draft, false);
            _dirty = false;
        }

        public PungentChecklistSectionDefinition AddSection(int insertIndex = -1, bool includePlaceholderItem = true)
        {
            if (Draft == null)
                return null;

            if (Draft.sections == null)
                Draft.sections = new List<PungentChecklistSectionDefinition>();

            HashSet<string> used = new HashSet<string>((Draft.sections ?? new List<PungentChecklistSectionDefinition>()).Where(item => item != null).Select(item => item.id), StringComparer.OrdinalIgnoreCase);
            PungentChecklistSectionDefinition section = new PungentChecklistSectionDefinition
            {
                id = PungentChecklistSerialization.UniqueId("section", used, "section"),
                title = "New Section",
                sortOrder = Draft.sections.Count
            };
            section.NormalizeInPlace();
            int targetIndex = insertIndex < 0 ? Draft.sections.Count : Mathf.Clamp(insertIndex, 0, Draft.sections.Count);
            Draft.sections.Insert(targetIndex, section);
            ReindexSections();
            if (includePlaceholderItem)
                AddItem(section);
            else
                Touch();
            return section;
        }

        public PungentChecklistItemDefinition AddItem(PungentChecklistSectionDefinition section, int insertIndex = -1)
        {
            if (Draft == null || section == null)
                return null;

            if (section.items == null)
                section.items = new List<PungentChecklistItemDefinition>();

            HashSet<string> used = new HashSet<string>(PungentChecklistSerialization.EnumerateItems(Draft).Where(item => item != null).Select(item => item.id), StringComparer.OrdinalIgnoreCase);
            PungentChecklistItemDefinition item = new PungentChecklistItemDefinition
            {
                id = PungentChecklistSerialization.UniqueId("item", used, "item"),
                label = "New checklist item",
                sortOrder = section.items.Count
            };
            item.NormalizeInPlace();
            int targetIndex = insertIndex < 0 ? section.items.Count : Mathf.Clamp(insertIndex, 0, section.items.Count);
            section.items.Insert(targetIndex, item);
            ReindexItems(section);
            Touch();
            return item;
        }

        public void RemoveSection(PungentChecklistSectionDefinition section)
        {
            if (Draft == null || section == null || Draft.sections == null)
                return;

            Draft.sections.Remove(section);
            if (Draft.sections.Count == 0)
                AddSection(0);
            else
            {
                ReindexSections();
                Touch();
            }
        }

        public void RemoveItem(PungentChecklistSectionDefinition section, PungentChecklistItemDefinition item)
        {
            if (section == null || item == null || section.items == null)
                return;

            section.items.Remove(item);
            if (section.items.Count == 0)
                AddItem(section, 0);
            else
            {
                ReindexItems(section);
                Touch();
            }
        }

        public void Reindex()
        {
            ReindexSections();
            Touch();
        }

        public void DuplicateSection(PungentChecklistSectionDefinition section)
        {
            if (Draft == null || section == null)
                return;

            PungentChecklistSectionDefinition copy = JsonUtility.FromJson<PungentChecklistSectionDefinition>(JsonUtility.ToJson(section, false));
            HashSet<string> sectionIds = new HashSet<string>(Draft.sections.Where(item => item != null).Select(item => item.id), StringComparer.OrdinalIgnoreCase);
            HashSet<string> itemIds = new HashSet<string>(PungentChecklistSerialization.EnumerateItems(Draft).Where(item => item != null).Select(item => item.id), StringComparer.OrdinalIgnoreCase);
            copy.id = PungentChecklistSerialization.UniqueId(copy.id + "-copy", sectionIds, "section");
            copy.title = copy.title + " Copy";
            foreach (PungentChecklistItemDefinition item in copy.items ?? new List<PungentChecklistItemDefinition>())
                item.id = PungentChecklistSerialization.UniqueId(item.id + "-copy", itemIds, "item");
            copy.NormalizeInPlace();
            Draft.sections.Insert(Mathf.Clamp(Draft.sections.IndexOf(section) + 1, 0, Draft.sections.Count), copy);
            ReindexSections();
            Touch();
        }

        public void DuplicateItem(PungentChecklistSectionDefinition section, PungentChecklistItemDefinition item)
        {
            if (Draft == null || section == null || item == null)
                return;

            PungentChecklistItemDefinition copy = JsonUtility.FromJson<PungentChecklistItemDefinition>(JsonUtility.ToJson(item, false));
            HashSet<string> itemIds = new HashSet<string>(PungentChecklistSerialization.EnumerateItems(Draft).Where(existing => existing != null).Select(existing => existing.id), StringComparer.OrdinalIgnoreCase);
            copy.id = PungentChecklistSerialization.UniqueId(copy.id + "-copy", itemIds, "item");
            copy.label = copy.label + " Copy";
            copy.NormalizeInPlace();
            section.items.Insert(Mathf.Clamp(section.items.IndexOf(item) + 1, 0, section.items.Count), copy);
            ReindexItems(section);
            Touch();
        }

        public void MoveSection(PungentChecklistSectionDefinition section, int direction)
        {
            if (Draft == null || section == null || Draft.sections == null)
                return;

            int index = Draft.sections.IndexOf(section);
            int next = Mathf.Clamp(index + direction, 0, Draft.sections.Count - 1);
            if (index < 0 || index == next)
                return;
            Draft.sections.RemoveAt(index);
            Draft.sections.Insert(next, section);
            ReindexSections();
            Touch();
        }

        public void MoveItem(PungentChecklistSectionDefinition section, PungentChecklistItemDefinition item, int direction)
        {
            if (section == null || item == null || section.items == null)
                return;

            int index = section.items.IndexOf(item);
            int next = Mathf.Clamp(index + direction, 0, section.items.Count - 1);
            if (index < 0 || index == next)
                return;
            section.items.RemoveAt(index);
            section.items.Insert(next, item);
            ReindexItems(section);
            Touch();
        }

        private void ReindexSections()
        {
            if (Draft == null || Draft.sections == null)
                return;

            for (int i = 0; i < Draft.sections.Count; i++)
            {
                PungentChecklistSectionDefinition section = Draft.sections[i];
                if (section == null)
                    continue;

                section.sortOrder = i;
                ReindexItems(section);
            }
        }

        private static void ReindexItems(PungentChecklistSectionDefinition section)
        {
            if (section == null || section.items == null)
                return;

            for (int i = 0; i < section.items.Count; i++)
                if (section.items[i] != null)
                    section.items[i].sortOrder = i;
        }

        public void Touch()
        {
            if (Draft == null)
                return;

            _dirty = true;
        }

        public void NormalizeDraft()
        {
            if (Draft == null)
                return;

            Draft.NormalizeInPlace();
        }

        public static PungentChecklistDefinition CreateBlank(string title, string listKind, string profileId)
        {
            PungentChecklistDefinition checklist = PungentChecklistSerialization.CreateDefinitionTemplate();
            checklist.checklistId = PungentChecklistSerialization.Slug(title, "new-checklist");
            checklist.title = string.IsNullOrWhiteSpace(title) ? "New Checklist" : title.Trim();
            checklist.description = string.Empty;
            checklist.defaultGuidance = "Use comments and unresolved states as follow-up work.";
            checklist.listKind = PungentChecklistListKinds.Normalize(listKind);
            checklist.stateProfileId = PungentChecklistProfiles.NormalizeProfileId(profileId);
            checklist.sourceProviderId = PungentChecklistConstants.ProviderId;
            checklist.sourceLabel = "Checklist Utility";
            checklist.sections = new List<PungentChecklistSectionDefinition>
            {
                new PungentChecklistSectionDefinition
                {
                    id = "section",
                    title = "New Section",
                    sortOrder = 0,
                    items = new List<PungentChecklistItemDefinition>
                    {
                        new PungentChecklistItemDefinition
                        {
                            id = "item",
                            label = "New checklist item",
                            sortOrder = 0
                        }
                    }
                }
            };
            checklist.NormalizeInPlace();
            return checklist;
        }
    }
#endif
}
