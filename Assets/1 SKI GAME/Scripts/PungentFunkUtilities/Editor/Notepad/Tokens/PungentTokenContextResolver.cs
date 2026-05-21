using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public static class PungentTokenContextResolver
    {
        public static PungentTokenBinding CreateBindingTemplate(string tokenKey, UnityEngine.Object target, string propertyPath = null, string label = null)
        {
            PungentNoteTargetLink noteTarget = PungentNoteContextResolver.CreateTargetLink(target, propertyPath);
            if (noteTarget == null)
                return null;

            return new PungentTokenBinding
            {
                id = Guid.NewGuid().ToString("N"),
                tokenKey = PungentTokenParser.NormalizeKey(tokenKey),
                label = string.IsNullOrWhiteSpace(label) ? PungentNoteContextResolver.DisplayLabel(noteTarget) : label,
                targetType = Map(noteTarget.type),
                assetGuid = noteTarget.assetGuid,
                sceneObjectGlobalId = noteTarget.sceneObjectGlobalId,
                componentType = noteTarget.componentType,
                componentInstanceId = noteTarget.sceneObjectGlobalId,
                propertyPath = noteTarget.propertyPath,
                scriptPath = noteTarget.scriptPath,
                createdUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o")
            };
        }

        public static List<PungentTokenBinding> GetBindingsForTarget(UnityEngine.Object target, string propertyPath = null)
        {
            PungentTokenBinding context = CreateBindingTemplate(string.Empty, target, propertyPath);
            if (context == null)
                return new List<PungentTokenBinding>();

            return PungentTokenStorage.Database.bindings
                .Where(b => b != null && !b.archived && MatchesTarget(b, context))
                .ToList();
        }

        public static bool BindingExists(PungentTokenBinding candidate)
        {
            return candidate != null && PungentTokenStorage.Database.bindings.Any(b => b != null && !b.archived && MatchesTarget(b, candidate) && string.Equals(PungentTokenParser.NormalizeKey(b.tokenKey), PungentTokenParser.NormalizeKey(candidate.tokenKey), StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesTarget(PungentTokenBinding binding, PungentTokenBinding context)
        {
            if (binding.targetType != context.targetType)
                return false;
            if (!string.IsNullOrEmpty(context.assetGuid) && !string.Equals(binding.assetGuid, context.assetGuid, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(context.sceneObjectGlobalId) && !string.Equals(binding.sceneObjectGlobalId, context.sceneObjectGlobalId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(context.componentType) && !string.Equals(binding.componentType, context.componentType, StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(binding.propertyPath ?? string.Empty, context.propertyPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static PungentTokenTargetType Map(PungentNoteTargetType type)
        {
            switch (type)
            {
                case PungentNoteTargetType.Asset: return PungentTokenTargetType.Asset;
                case PungentNoteTargetType.SceneObject: return PungentTokenTargetType.SceneObject;
                case PungentNoteTargetType.ComponentType: return PungentTokenTargetType.ComponentType;
                case PungentNoteTargetType.ComponentInstance: return PungentTokenTargetType.ComponentInstance;
                case PungentNoteTargetType.SerializedProperty: return PungentTokenTargetType.SerializedProperty;
                case PungentNoteTargetType.ScriptPath: return PungentTokenTargetType.ScriptPath;
                case PungentNoteTargetType.Note: return PungentTokenTargetType.Note;
                case PungentNoteTargetType.AuditIssue: return PungentTokenTargetType.AuditIssue;
                case PungentNoteTargetType.RegisteredUtility: return PungentTokenTargetType.RegisteredUtility;
                case PungentNoteTargetType.FutureUtility: return PungentTokenTargetType.FutureUtility;
                default: return PungentTokenTargetType.None;
            }
        }
    }
#endif
}
