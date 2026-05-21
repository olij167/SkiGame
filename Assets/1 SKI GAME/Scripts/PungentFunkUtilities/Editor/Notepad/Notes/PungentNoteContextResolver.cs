using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public static class PungentNoteContextResolver
    {
        public static PungentNoteTargetLink CreateTargetLink(UnityEngine.Object target, string propertyPath = null)
        {
            if (target == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(target);
            string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            string globalId = TryGetGlobalId(target);

            return new PungentNoteTargetLink
            {
                type = GuessTargetType(target, propertyPath, assetPath),
                label = BuildLabel(target, propertyPath),
                assetGuid = guid,
                sceneObjectGlobalId = globalId,
                componentType = target.GetType().FullName,
                propertyPath = propertyPath ?? string.Empty,
                scriptPath = target is MonoScript ? assetPath : string.Empty
            };
        }

        public static PungentNoteTargetLink CreatePropertyTargetLink(SerializedProperty property)
        {
            if (property == null || property.serializedObject == null)
                return null;
            return CreateTargetLink(property.serializedObject.targetObject, property.propertyPath);
        }

        public static List<PungentNote> FindNotesFor(UnityEngine.Object target, bool includeTypeNotes = true, bool includeArchived = false)
        {
            if (target == null)
                return new List<PungentNote>();

            PungentNoteTargetLink context = CreateTargetLink(target, string.Empty);
            return FindNotes(context, includeTypeNotes, includeArchived);
        }

        public static List<PungentNote> FindNotesForProperty(SerializedProperty property, bool includeTypeNotes = true, bool includeArchived = false)
        {
            PungentNoteTargetLink context = CreatePropertyTargetLink(property);
            return FindNotes(context, includeTypeNotes, includeArchived);
        }

        public static List<PungentNote> FindNotes(PungentNoteTargetLink context, bool includeTypeNotes = true, bool includeArchived = false)
        {
            if (context == null)
                return new List<PungentNote>();

            PungentNoteDatabase db = PungentNoteStorage.Database;
            return db.notes
                .Where(n => n != null && (includeArchived || !n.archived))
                .Where(n => n.targets != null && n.targets.Any(t => Matches(t, context, includeTypeNotes)))
                .ToList();
        }

        public static List<PungentNote> FindNotesForUtility(string utilityId, bool includeArchived = false)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return new List<PungentNote>();

            return PungentNoteStorage.Database.notes
                .Where(n => n != null && (includeArchived || !n.archived))
                .Where(n => string.Equals(n.linkedUtilityId, utilityId, StringComparison.OrdinalIgnoreCase) ||
                            (n.targets != null && n.targets.Any(t => string.Equals(t.utilityId, utilityId, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        public static UnityEngine.Object ResolveTarget(PungentNoteTargetLink link)
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

            if (!string.IsNullOrEmpty(link.scriptPath))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.scriptPath);

            return null;
        }

        public static GameObject ResolveGameObject(PungentNote note)
        {
            if (note == null || note.targets == null)
                return null;

            for (int i = 0; i < note.targets.Count; i++)
            {
                UnityEngine.Object target = ResolveTarget(note.targets[i]);
                if (target is GameObject go)
                    return go;
                if (target is Component component)
                    return component.gameObject;
            }

            return null;
        }

        public static bool Matches(PungentNoteTargetLink noteTarget, PungentNoteTargetLink context, bool includeTypeNotes)
        {
            if (noteTarget == null || context == null)
                return false;

            if (!string.IsNullOrEmpty(noteTarget.assetGuid) && string.Equals(noteTarget.assetGuid, context.assetGuid, StringComparison.OrdinalIgnoreCase))
            {
                if (noteTarget.type == PungentNoteTargetType.SerializedProperty)
                    return string.Equals(noteTarget.propertyPath, context.propertyPath, StringComparison.OrdinalIgnoreCase);
                return true;
            }

            if (!string.IsNullOrEmpty(noteTarget.sceneObjectGlobalId) && string.Equals(noteTarget.sceneObjectGlobalId, context.sceneObjectGlobalId, StringComparison.OrdinalIgnoreCase))
            {
                if (noteTarget.type == PungentNoteTargetType.SerializedProperty)
                    return string.Equals(noteTarget.propertyPath, context.propertyPath, StringComparison.OrdinalIgnoreCase);
                return true;
            }

            if (includeTypeNotes && noteTarget.type == PungentNoteTargetType.ComponentType && !string.IsNullOrEmpty(noteTarget.componentType))
                return string.Equals(noteTarget.componentType, context.componentType, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(noteTarget.scriptPath) && string.Equals(noteTarget.scriptPath, context.scriptPath, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrEmpty(noteTarget.label) &&
                   !HasStableIdentity(noteTarget) &&
                   !HasStableIdentity(context) &&
                   string.Equals(noteTarget.label, context.label, StringComparison.OrdinalIgnoreCase);
        }

        public static string DisplayLabel(PungentNoteTargetLink link)
        {
            if (link == null)
                return "Unknown";
            if (!string.IsNullOrWhiteSpace(link.label))
                return link.label;
            if (!string.IsNullOrWhiteSpace(link.propertyPath))
                return link.propertyPath;
            if (!string.IsNullOrWhiteSpace(link.componentType))
                return ObjectNames.NicifyVariableName(link.componentType.Split('.').Last());
            if (!string.IsNullOrWhiteSpace(link.scriptPath))
                return link.scriptPath;
            return link.type.ToString();
        }

        private static PungentNoteTargetType GuessTargetType(UnityEngine.Object target, string propertyPath, string assetPath)
        {
            if (!string.IsNullOrEmpty(propertyPath))
                return PungentNoteTargetType.SerializedProperty;
            if (target is Component)
                return PungentNoteTargetType.ComponentInstance;
            if (target is GameObject)
                return PungentNoteTargetType.SceneObject;
            if (target is MonoScript)
                return PungentNoteTargetType.ScriptPath;
            return string.IsNullOrEmpty(assetPath) ? PungentNoteTargetType.None : PungentNoteTargetType.Asset;
        }

        private static string BuildLabel(UnityEngine.Object target, string propertyPath)
        {
            if (!string.IsNullOrEmpty(propertyPath))
                return ObjectNames.NicifyVariableName(propertyPath.Split('.').Last());
            return target != null ? target.name : "Unknown Target";
        }

        private static bool HasStableIdentity(PungentNoteTargetLink link)
        {
            return link != null && (!string.IsNullOrEmpty(link.assetGuid) || !string.IsNullOrEmpty(link.sceneObjectGlobalId) || !string.IsNullOrEmpty(link.scriptPath));
        }

        public static string TryGetGlobalId(UnityEngine.Object obj)
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
