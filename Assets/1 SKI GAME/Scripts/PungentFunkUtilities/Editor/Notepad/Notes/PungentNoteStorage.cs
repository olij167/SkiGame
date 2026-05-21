using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using UnityEditor;
    using UnityEngine;

    public static class PungentNoteStorage
    {
        private const string LegacyTooltipDatabasePath = "Assets/PungentFunkUtilitiesData/TooltipNotes/PungentTooltipNotes.asset";

        public static PungentNoteDatabase Database
        {
            get
            {
                EnsureLoaded();
                return PungentNoteDatabase.instance;
            }
        }

        public static void EnsureLoaded()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "PungentFunkUtilities"));
            PungentNoteDatabase db = PungentNoteDatabase.instance;
            if (db.notes == null)
                db.notes = new List<PungentNote>();
            if (db.futureUtilities == null)
                db.futureUtilities = new List<PungentFutureUtilityRecord>();
            if (db.importSources == null)
                db.importSources = new List<PungentNoteImportSource>();
            if (db.displaySettings == null)
                db.displaySettings = new PungentNoteDisplaySettings();
            ImportLegacyTooltipNotesIfNeeded(db);
        }

        public static void Save()
        {
            PungentNoteDatabase db = PungentNoteDatabase.instance;
            db.lastSavedUtc = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "PungentFunkUtilities"));
            db.Persist();
            PungentStickyNoteOverlayController.InvalidateNoteIndex();
            PungentNoteSceneOverlay.QueueInvalidateCache();
        }

        public static void Delete(PungentNote note)
        {
            if (note == null)
                return;

            Database.notes.Remove(note);
            Save();
        }

        public static void Archive(PungentNote note, bool archived)
        {
            if (note == null)
                return;

            note.archived = archived;
            note.updatedUtc = DateTime.UtcNow.ToString("o");
            Save();
        }

        public static PungentNote CreateNoteForTarget(UnityEngine.Object target, string propertyPath, string propertyName)
        {
            PungentNote note = Database.CreateNote(string.IsNullOrWhiteSpace(propertyName) ? "New Note" : ObjectNames.NicifyVariableName(propertyName), PungentNoteKind.TooltipAnnotation);
            note.visibility = PungentNoteVisibility.PrivateProject;
            note.priority = PungentNotePriority.NiceToHave;
            note.status = PungentNoteStatus.ToDo;
            PungentNoteTargetLink link = PungentNoteContextResolver.CreateTargetLink(target, propertyPath);
            if (link != null)
                note.targets.Add(link);
            Save();
            return note;
        }

        public static void AddTargetLink(PungentNote note, UnityEngine.Object target, string propertyPath)
        {
            if (note == null)
                return;

            PungentNoteTargetLink link = PungentNoteContextResolver.CreateTargetLink(target, propertyPath);
            if (link != null)
                note.targets.Add(link);
        }

        public static PungentNoteTargetLink CreateDocumentationLinkTarget(PungentUtilityDocumentationLinks.DocumentationLink documentationLink)
        {
            if (documentationLink == null || string.IsNullOrWhiteSpace(documentationLink.id))
                return null;

            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(documentationLink);
            return new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.DocumentationLink,
                label = PungentUtilityDocumentationLinks.GetDisplayName(documentationLink),
                documentationLinkId = documentationLink.id,
                externalPathOrUrl = status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl ? status.targetValue : string.Empty
            };
        }

        public static bool AddDocumentationLinkTarget(PungentNote note, PungentUtilityDocumentationLinks.DocumentationLink documentationLink)
        {
            if (note == null || documentationLink == null || string.IsNullOrWhiteSpace(documentationLink.id))
                return false;

            if (note.targets == null)
                note.targets = new List<PungentNoteTargetLink>();

            if (note.targets.Any(target => target != null &&
                                           target.type == PungentNoteTargetType.DocumentationLink &&
                                           string.Equals(target.documentationLinkId, documentationLink.id, StringComparison.OrdinalIgnoreCase)))
                return false;

            PungentNoteTargetLink link = CreateDocumentationLinkTarget(documentationLink);
            if (link == null)
                return false;

            note.targets.Add(link);
            if (string.IsNullOrWhiteSpace(note.linkedUtilityId) && documentationLink.utilityIds != null)
                note.linkedUtilityId = documentationLink.utilityIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
            Database.Touch(note);
            Save();
            return true;
        }

        public static List<PungentNote> GetNotesReferencingDocumentationLink(string documentationLinkId, bool includeArchived = false)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(documentationLinkId))
                return new List<PungentNote>();

            return Database.notes
                .Where(note => note != null && (includeArchived || !note.archived))
                .Where(note => note.targets != null && note.targets.Any(target =>
                    target != null &&
                    target.type == PungentNoteTargetType.DocumentationLink &&
                    string.Equals(target.documentationLinkId, documentationLinkId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public static List<PungentNote> FindNotesFor(UnityEngine.Object target, bool includeTypeNotes, bool includeArchived = false)
        {
            return PungentNoteContextResolver.FindNotesFor(target, includeTypeNotes, includeArchived);
        }

        public static UnityEngine.Object ResolveTarget(PungentNote note)
        {
            if (note == null || note.targets == null)
                return null;

            for (int i = 0; i < note.targets.Count; i++)
            {
                UnityEngine.Object resolved = ResolveTarget(note.targets[i]);
                if (resolved != null)
                    return resolved;
            }

            return null;
        }

        private static UnityEngine.Object ResolveTarget(PungentNoteTargetLink link)
        {
            if (link == null)
                return null;

            if (!string.IsNullOrEmpty(link.sceneObjectGlobalId) && GlobalObjectId.TryParse(link.sceneObjectGlobalId, out GlobalObjectId globalId))
            {
                UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (obj != null)
                    return obj;
            }

            if (!string.IsNullOrEmpty(link.assetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(link.assetGuid);
                if (!string.IsNullOrEmpty(path))
                    return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            }

            return null;
        }

        private static void ImportLegacyTooltipNotesIfNeeded(PungentNoteDatabase db)
        {
            if (db.importedLegacyTooltipNotes)
                return;

            PungentTooltipNoteDatabase legacy = AssetDatabase.LoadAssetAtPath<PungentTooltipNoteDatabase>(LegacyTooltipDatabasePath);
            if (legacy == null || legacy.notes == null || legacy.notes.Count == 0)
            {
                db.importedLegacyTooltipNotes = true;
                Save();
                return;
            }

            HashSet<string> existingIds = new HashSet<string>(db.notes.Where(n => n != null).Select(n => n.id), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < legacy.notes.Count; i++)
            {
                PungentTooltipNote old = legacy.notes[i];
                if (old == null || existingIds.Contains(old.id))
                    continue;

                PungentNote note = new PungentNote
                {
                    id = string.IsNullOrWhiteSpace(old.id) ? Guid.NewGuid().ToString("N") : old.id,
                    title = string.IsNullOrWhiteSpace(old.title) ? "Imported Tooltip Note" : old.title,
                    body = old.body ?? string.Empty,
                    kind = PungentNoteKind.TooltipAnnotation,
                    status = old.priority == PungentTooltipNotePriority.Todo ? PungentNoteStatus.ToDo : PungentNoteStatus.FurtherConsideration,
                    priority = MapPriority(old.priority),
                    visibility = PungentNoteVisibility.PrivateProject,
                    tags = SplitTags(old.tags),
                    createdUtc = new DateTime(Math.Max(DateTime.MinValue.Ticks, old.createdTicks), DateTimeKind.Utc).ToString("o"),
                    updatedUtc = new DateTime(Math.Max(DateTime.MinValue.Ticks, old.updatedTicks), DateTimeKind.Utc).ToString("o"),
                    archived = !old.enabled
                };

                string guid = string.IsNullOrEmpty(old.assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(old.assetPath);
                note.targets.Add(new PungentNoteTargetLink
                {
                    type = MapScope(old.scope),
                    label = old.targetName,
                    assetGuid = guid,
                    sceneObjectGlobalId = old.targetGlobalId,
                    componentType = old.targetTypeName,
                    propertyPath = old.propertyPath
                });
                if (!string.IsNullOrWhiteSpace(old.category))
                    note.tags.Add(old.category.Trim());

                db.notes.Add(note);
                existingIds.Add(note.id);
            }

            db.importedLegacyTooltipNotes = true;
            Save();
        }

        private static bool MatchesTarget(PungentNoteTargetLink link, UnityEngine.Object target, string selectedId, string selectedType, string selectedGuid, bool includeTypeNotes)
        {
            if (link == null || target == null)
                return false;

            if (!string.IsNullOrEmpty(link.sceneObjectGlobalId) && string.Equals(link.sceneObjectGlobalId, selectedId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(link.assetGuid) && string.Equals(link.assetGuid, selectedGuid, StringComparison.OrdinalIgnoreCase))
                return true;

            return includeTypeNotes &&
                   link.type == PungentNoteTargetType.ComponentType &&
                   string.Equals(link.componentType, selectedType, StringComparison.OrdinalIgnoreCase);
        }

        private static PungentNoteTargetType GuessTargetType(UnityEngine.Object target, string assetPath)
        {
            if (target is Component)
                return PungentNoteTargetType.ComponentInstance;
            if (target is GameObject)
                return PungentNoteTargetType.SceneObject;
            return string.IsNullOrEmpty(assetPath) ? PungentNoteTargetType.None : PungentNoteTargetType.Asset;
        }

        private static PungentNoteTargetType MapScope(PungentTooltipNoteScope scope)
        {
            switch (scope)
            {
                case PungentTooltipNoteScope.ComponentType: return PungentNoteTargetType.ComponentType;
                case PungentTooltipNoteScope.ComponentInstance: return PungentNoteTargetType.ComponentInstance;
                case PungentTooltipNoteScope.SerializedProperty: return PungentNoteTargetType.SerializedProperty;
                case PungentTooltipNoteScope.SceneObject: return PungentNoteTargetType.SceneObject;
                default: return PungentNoteTargetType.Asset;
            }
        }

        private static PungentNotePriority MapPriority(PungentTooltipNotePriority priority)
        {
            switch (priority)
            {
                case PungentTooltipNotePriority.Critical: return PungentNotePriority.Crucial;
                case PungentTooltipNotePriority.Warning: return PungentNotePriority.Important;
                case PungentTooltipNotePriority.Todo: return PungentNotePriority.Important;
                default: return PungentNotePriority.NiceToHave;
            }
        }

        private static List<string> SplitTags(string tags)
        {
            return (tags ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().TrimStart('#'))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string TryGetGlobalId(UnityEngine.Object obj)
        {
            if (obj == null)
                return string.Empty;
            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
#endif
}
