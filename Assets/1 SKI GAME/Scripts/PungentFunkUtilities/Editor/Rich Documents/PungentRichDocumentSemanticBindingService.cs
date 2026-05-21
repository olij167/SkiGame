using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentSemanticBindingService
    {
        public static void RefreshBindingsFromBody(PungentRichDocument document)
        {
            if (document == null)
                return;

            if (document.semanticBindings == null)
                document.semanticBindings = new List<PungentRichDocumentSemanticBinding>();

            Dictionary<string, PungentRichDocumentSemanticBinding> existing = document.semanticBindings
                .Where(binding => binding != null)
                .ToDictionary(binding => binding.id, StringComparer.OrdinalIgnoreCase);

            foreach (PungentRichDocumentSemanticSpan span in PungentRichDocumentSemanticParser.ExtractSemanticSpans(document.bodyText))
            {
                if (span == null || string.IsNullOrWhiteSpace(span.id))
                    continue;

                if (!existing.TryGetValue(span.id, out PungentRichDocumentSemanticBinding binding))
                {
                    binding = new PungentRichDocumentSemanticBinding
                    {
                        id = span.id
                    };
                    document.semanticBindings.Add(binding);
                    existing[span.id] = binding;
                }

                binding.kind = span.kind;
                binding.label = BuildLabel(span);
                binding.sourceText = span.innerText;
                binding.sourceFingerprint = span.Fingerprint;
                MergeFields(binding, span.fields);
                binding.NormalizeInPlace();
            }

            RefreshBindingExpressionSlots(document);
            document.NormalizeInPlace();
        }

        public static List<PungentRichDocumentBindingExpression> RefreshBindingExpressionSlots(PungentRichDocument document)
        {
            List<PungentRichDocumentBindingExpression> expressions = PungentRichDocumentBindingExpressionParser.ExtractExpressions(
                document == null ? string.Empty : document.bodyText,
                true);
            if (document == null)
                return expressions;

            document.bindingSlots = document.bindingSlots ?? new List<PungentAuthoringBindingSlot>();
            document.bindingSlots.Clear();
            foreach (PungentRichDocumentBindingExpression expression in expressions)
            {
                if (expression == null || expression.bindingSlot == null)
                    continue;

                expression.bindingSlot.interfaceId = "rich-documents";
                expression.bindingSlot.itemId = document.id;
                expression.bindingSlot.NormalizeInPlace();
                document.bindingSlots.Add(expression.bindingSlot);
            }

            return expressions;
        }

        public static PungentRichDocumentSemanticBinding FindBinding(PungentRichDocument document, string id)
        {
            if (document == null || string.IsNullOrWhiteSpace(id) || document.semanticBindings == null)
                return null;

            return document.semanticBindings.FirstOrDefault(binding =>
                binding != null && string.Equals(binding.id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static PungentRichDocumentSemanticBinding FindExpressionBinding(PungentRichDocument document, PungentRichDocumentBindingExpression expression)
        {
            if (document == null || expression == null || document.semanticBindings == null)
                return null;

            return document.semanticBindings.FirstOrDefault(binding =>
                binding != null &&
                (string.Equals(binding.id, expression.id, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(binding.bindingExpression, expression.rawText, StringComparison.OrdinalIgnoreCase)));
        }

        public static PungentRichDocumentSemanticBinding GetOrCreateBinding(PungentRichDocument document, PungentRichDocumentSemanticSpan span)
        {
            if (document == null || span == null)
                return null;

            if (document.semanticBindings == null)
                document.semanticBindings = new List<PungentRichDocumentSemanticBinding>();

            PungentRichDocumentSemanticBinding binding = FindBinding(document, span.id);
            if (binding == null)
            {
                binding = new PungentRichDocumentSemanticBinding { id = span.id };
                document.semanticBindings.Add(binding);
            }

            binding.kind = span.kind;
            binding.label = BuildLabel(span);
            binding.sourceText = span.innerText;
            binding.sourceFingerprint = span.Fingerprint;
            MergeFields(binding, span.fields);
            binding.NormalizeInPlace();
            return binding;
        }

        public static PungentAuthoringTarget CreateSerializedPropertyTarget(UnityEngine.Object unityObject, string propertyPath, string label = null)
        {
            if (unityObject == null || string.IsNullOrWhiteSpace(propertyPath))
                return null;

            string contextId = GetStableObjectContext(unityObject);
            PungentAuthoringTarget target = PungentAuthoringTarget.Create(
                PungentAuthoringTargetKind.SerializedPropertyPath,
                propertyPath.Trim(),
                string.IsNullOrWhiteSpace(label) ? unityObject.name + "." + propertyPath.Trim() : label,
                PungentRichDocumentProvider.Id);
            target.contextId = contextId;
            target.propertyPath = propertyPath.Trim();
            target.sourceContext = "RichDocumentSemanticBinding";
            target.NormalizeInPlace();
            return target;
        }

        public static PungentAuthoringTarget CreateObjectTarget(UnityEngine.Object unityObject, string label = null)
        {
            if (unityObject == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrWhiteSpace(guid))
                    return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.AssetGuid, guid, label ?? unityObject.name, PungentRichDocumentProvider.Id);
            }

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.SceneObjectGlobalId, globalId.ToString(), label ?? unityObject.name, PungentRichDocumentProvider.Id);
        }

        public static void SetBindingTarget(PungentRichDocument document, string bindingId, PungentAuthoringTarget target)
        {
            PungentRichDocumentSemanticBinding binding = FindBinding(document, bindingId);
            if (binding == null || target == null)
                return;

            binding.target = target;
            PungentAuthoringBindingLink link = CreateBindingLink(document, binding);
            binding.bindingLinkId = link.id;
            binding.bindingAdapterId = link.adapterId;
            binding.bindingEndpointId = link.endpointId;
            binding.bindingValueType = link.valueType;
            binding.bindingPath = link.bindingPath;
            binding.bindingSlot = link.bindingSlot;
            binding.bindingSlot.role = binding.kind == PungentRichDocumentSemanticKind.Token
                ? PungentAuthoringBindingSlotRole.RichDocumentToken
                : PungentAuthoringBindingSlotRole.RichDocumentSemanticSpan;
            binding.NormalizeInPlace();
            AddDocumentTarget(document, target);
        }

        public static void SetBindingEndpoint(PungentRichDocument document, string bindingId, PungentAuthoringBindingEndpoint endpoint)
        {
            PungentRichDocumentSemanticBinding binding = FindBinding(document, bindingId);
            if (binding == null || endpoint == null || endpoint.target == null)
                return;

            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromEndpoint(
                endpoint,
                "rich-documents",
                document == null ? string.Empty : document.id,
                binding.id,
                binding.kind.ToString(),
                PungentAuthoringBindingLinkDirection.TwoWay);

            binding.target = link.target;
            binding.bindingLinkId = link.id;
            binding.bindingAdapterId = link.adapterId;
            binding.bindingEndpointId = link.endpointId;
            binding.bindingValueType = link.valueType;
            binding.bindingPath = link.bindingPath;
            binding.bindingSlot = link.bindingSlot;
            binding.bindingSlot.role = binding.kind == PungentRichDocumentSemanticKind.Token
                ? PungentAuthoringBindingSlotRole.RichDocumentToken
                : PungentAuthoringBindingSlotRole.RichDocumentSemanticSpan;
            binding.NormalizeInPlace();
            AddDocumentTarget(document, binding.target);
        }

        public static PungentRichDocumentSemanticBinding SetReferenceExpressionEndpoint(
            PungentRichDocument document,
            PungentRichDocumentBindingExpression expression,
            PungentAuthoringBindingEndpoint endpoint)
        {
            if (document == null || expression == null || endpoint == null || endpoint.target == null)
                return null;

            if (document.semanticBindings == null)
                document.semanticBindings = new List<PungentRichDocumentSemanticBinding>();

            PungentRichDocumentSemanticBinding binding = FindExpressionBinding(document, expression);
            if (binding == null)
            {
                binding = new PungentRichDocumentSemanticBinding
                {
                    id = expression.id,
                    kind = PungentRichDocumentSemanticKind.Token
                };
                document.semanticBindings.Add(binding);
            }

            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromEndpoint(
                endpoint,
                "rich-documents",
                document.id,
                expression.id,
                "ref",
                PungentAuthoringBindingLinkDirection.TwoWay);

            binding.kind = PungentRichDocumentSemanticKind.Token;
            binding.label = "Reference: " + expression.targetName;
            binding.sourceText = expression.rawText;
            binding.bindingExpression = expression.rawText;
            binding.target = link.target;
            binding.bindingLinkId = link.id;
            binding.bindingAdapterId = link.adapterId;
            binding.bindingEndpointId = link.endpointId;
            binding.bindingValueType = link.valueType;
            binding.bindingPath = link.bindingPath;
            binding.bindingSlot = link.bindingSlot;
            binding.bindingSlot.role = PungentAuthoringBindingSlotRole.RichDocumentToken;
            binding.bindingSlot.elementId = expression.id;
            binding.bindingSlot.fieldKey = "ref";
            binding.NormalizeInPlace();

            AddDocumentTarget(document, binding.target);
            return binding;
        }

        public static PungentAuthoringBindingLink CreateBindingLink(PungentRichDocument document, PungentRichDocumentSemanticBinding binding)
        {
            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromTarget(
                binding == null ? null : binding.target,
                "rich-documents",
                document == null ? string.Empty : document.id,
                binding == null ? string.Empty : binding.id,
                binding == null ? string.Empty : binding.kind.ToString(),
                PungentAuthoringBindingLinkDirection.TwoWay,
                binding == null ? string.Empty : binding.bindingAdapterId,
                string.Empty,
                binding == null ? string.Empty : binding.bindingEndpointId,
                binding == null ? string.Empty : binding.label,
                binding == null ? PungentAuthoringBindingValueType.Unknown : binding.bindingValueType);

            if (binding != null && !string.IsNullOrWhiteSpace(binding.bindingLinkId))
                link.id = binding.bindingLinkId;
            if (binding != null)
            {
                if (binding.bindingPath != null && binding.bindingPath.HasPath)
                    link.bindingPath = binding.bindingPath;
                if (binding.bindingSlot != null)
                {
                    link.bindingSlot = binding.bindingSlot;
                    link.bindingSlot.role = binding.kind == PungentRichDocumentSemanticKind.Token
                        ? PungentAuthoringBindingSlotRole.RichDocumentToken
                        : PungentAuthoringBindingSlotRole.RichDocumentSemanticSpan;
                    link.bindingSlot.elementId = binding.id;
                    link.bindingSlot.fieldKey = binding.kind.ToString();
                }
                link.runtimeAdapterId = binding.runtimeAdapterId;
                link.runtimeEnabled = binding.runtimeBindingEnabled;
                link.notes = "Exported from a Rich Document semantic binding. Runtime execution requires a project adapter.";
            }

            link.NormalizeInPlace();
            return link;
        }

        public static PungentAuthoringBindingPlan BuildRuntimePlan(PungentRichDocument document)
        {
            PungentAuthoringBindingPlan plan = new PungentAuthoringBindingPlan
            {
                displayName = document == null ? "Rich Document Binding Plan" : "Rich Document Binding Plan: " + document.title,
                sourceInterfaceId = "rich-documents",
                sourceItemId = document == null ? string.Empty : document.id,
                sourceLabel = document == null ? string.Empty : document.title
            };

            foreach (PungentRichDocumentSemanticBinding binding in document?.semanticBindings ?? new List<PungentRichDocumentSemanticBinding>())
            {
                if (binding == null || binding.target == null || !binding.target.HasTarget)
                    continue;

                PungentAuthoringBindingLink link = CreateBindingLink(document, binding);
                link.runtimeEnabled = binding.runtimeBindingEnabled;
                link.runtimeAdapterId = binding.runtimeAdapterId;
                link.notes = "Exported from Rich Documents. Runtime execution requires a project adapter with a matching runtimeAdapterId.";
                link.NormalizeInPlace();
                plan.links.Add(link);
            }

            plan.NormalizeInPlace();
            return plan;
        }

        public static List<string> GetStringPropertyPaths(UnityEngine.Object unityObject)
        {
            List<string> paths = new List<string>();
            if (unityObject == null)
                return paths;

            SerializedObject serializedObject;
            try
            {
                serializedObject = new SerializedObject(unityObject);
            }
            catch
            {
                return paths;
            }

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                    continue;
                if (iterator.propertyPath == "m_Name" || iterator.propertyPath == "m_TagString")
                    continue;
                if (iterator.propertyType == SerializedPropertyType.String)
                    paths.Add(iterator.propertyPath);
            }

            return paths;
        }

        private static void MergeFields(PungentRichDocumentSemanticBinding binding, IEnumerable<PungentRichDocumentSemanticField> fields)
        {
            if (binding.fields == null)
                binding.fields = new List<PungentRichDocumentSemanticField>();

            foreach (PungentRichDocumentSemanticField field in fields ?? Enumerable.Empty<PungentRichDocumentSemanticField>())
            {
                if (field == null || string.IsNullOrWhiteSpace(field.key))
                    continue;

                if (string.IsNullOrWhiteSpace(binding.GetField(field.key)))
                    binding.SetField(field.key, field.value);
            }
        }

        private static string BuildLabel(PungentRichDocumentSemanticSpan span)
        {
            if (span == null)
                return string.Empty;

            string key = span.GetField("key");
            if (string.IsNullOrWhiteSpace(key) && span.kind == PungentRichDocumentSemanticKind.CustomInsertion)
                key = span.GetField(PungentRichDocumentInsertionDefinitionRegistry.DefinitionLabelFieldKey);
            if (string.IsNullOrWhiteSpace(key))
                key = span.GetField("copyKey");
            if (string.IsNullOrWhiteSpace(key))
                key = span.GetField("objectiveId");
            if (string.IsNullOrWhiteSpace(key))
                key = span.GetField("stepId");
            if (string.IsNullOrWhiteSpace(key))
                key = span.GetField("speaker");

            return string.IsNullOrWhiteSpace(key)
                ? PungentRichDocumentSemanticParser.DisplayName(span.kind)
                : PungentRichDocumentSemanticParser.DisplayName(span.kind) + ": " + key.Trim();
        }

        private static void AddDocumentTarget(PungentRichDocument document, PungentAuthoringTarget target)
        {
            if (document == null || target == null || !target.HasTarget)
                return;

            if (document.targets == null)
                document.targets = new List<PungentAuthoringTarget>();

            bool exists = document.targets.Any(candidate =>
                candidate != null &&
                candidate.targetKind == target.targetKind &&
                string.Equals(candidate.rawValue, target.rawValue, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.contextId, target.contextId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.propertyPath, target.propertyPath, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                document.targets.Add(target);
        }

        private static string GetStableObjectContext(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
                return string.Empty;

            if (unityObject is Component || unityObject is GameObject)
                return GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();

            string assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrWhiteSpace(guid))
                    return guid;
            }

            return GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();
        }
    }

    public static class PungentRichDocumentBindingExpressionResolver
    {
        public static bool TryResolveReferenceExpression(
            PungentRichDocumentBindingExpression expression,
            IEnumerable<UnityEngine.Object> candidateTargets,
            out PungentAuthoringBindingPath path,
            out string reason)
        {
            path = null;
            reason = string.Empty;
            if (expression == null || expression.kind != PungentRichDocumentBindingExpressionKind.Reference)
            {
                reason = "Expression is not a reference token.";
                return false;
            }

            foreach (UnityEngine.Object candidate in candidateTargets ?? new UnityEngine.Object[0])
            {
                if (candidate == null)
                    continue;
                if (!MatchesTargetName(candidate, expression.targetName))
                    continue;

                PungentAuthoringBindingDiscoverySnapshot snapshot = PungentAuthoringBindingDiscoveryService.BuildSnapshot(candidate, "RichDocumentBindingExpression");
                path = SelectBestPath(snapshot, expression);
                if (path != null)
                    return true;
            }

            reason = "No candidate target matched " + expression.targetName + ".";
            return false;
        }

        public static bool TryResolveCustomReference(
            PungentRichDocumentBindingExpression expression,
            PungentRichDocumentInsertionDefinitionDatabase database,
            out string displayText)
        {
            displayText = string.Empty;
            if (expression == null || expression.kind != PungentRichDocumentBindingExpressionKind.CustomReference)
                return false;

            displayText = PungentRichDocumentBindingExpressionParser.ResolveCustomText(expression.customId, database);
            return !string.IsNullOrWhiteSpace(displayText);
        }

        private static bool MatchesTargetName(UnityEngine.Object target, string targetName)
        {
            if (target == null || string.IsNullOrWhiteSpace(targetName))
                return false;

            string clean = targetName.Trim();
            return string.Equals(target.name, clean, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(target.GetType().Name, clean, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(target.GetType().FullName, clean, StringComparison.OrdinalIgnoreCase);
        }

        private static PungentAuthoringBindingPath SelectBestPath(PungentAuthoringBindingDiscoverySnapshot snapshot, PungentRichDocumentBindingExpression expression)
        {
            if (snapshot == null || expression == null)
                return null;

            List<PungentAuthoringBindingPath> paths = snapshot.paths ?? new List<PungentAuthoringBindingPath>();
            if (expression.pathSegments == null || expression.pathSegments.Count == 0)
                return paths.FirstOrDefault() ?? new PungentAuthoringBindingPath
                {
                    displayName = snapshot.rootLabel,
                    rootTarget = snapshot.rootTarget,
                    rootLabel = snapshot.rootLabel,
                    valueType = PungentAuthoringBindingValueType.UnityObject
                };

            string leaf = expression.pathSegments[expression.pathSegments.Count - 1];
            PungentAuthoringBindingPath exact = paths.FirstOrDefault(path =>
                path != null &&
                (string.Equals(path.propertyPath, leaf, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path.displayName, leaf, StringComparison.OrdinalIgnoreCase) ||
                 EndsWithPathSegment(path.propertyPath, leaf)));
            if (exact != null)
                return exact;

            return paths.FirstOrDefault(path =>
                path != null &&
                expression.pathSegments.All(segment =>
                    ContainsIgnoreCase(path.displayName, segment) ||
                    ContainsIgnoreCase(path.propertyPath, segment)));
        }

        private static bool EndsWithPathSegment(string path, string segment)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(segment))
                return false;

            return path.EndsWith("." + segment.Trim(), StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith("/" + segment.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(string value, string part)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(part) &&
                   value.IndexOf(part.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
#endif
}
