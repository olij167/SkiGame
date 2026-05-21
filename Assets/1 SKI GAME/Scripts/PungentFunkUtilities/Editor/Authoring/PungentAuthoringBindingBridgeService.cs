using System;
using System.IO;
using PungentFunk.Utilities.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public sealed class PungentAuthoringBindingLinkPreview
    {
        public PungentAuthoringBindingLink link;
        public PungentAuthoringBindingPreview endpointPreview;
        public string readiness = string.Empty;
        public string disabledReason = string.Empty;
        public bool canRead;
        public bool canApply;
        public bool runtimeExportable;
    }

    public static class PungentAuthoringBindingBridgeService
    {
        public static PungentAuthoringBindingLink CreateLinkFromEndpoint(
            PungentAuthoringBindingEndpoint endpoint,
            string sourceInterfaceId,
            string sourceItemId,
            string sourceElementId,
            string sourceFieldKey,
            PungentAuthoringBindingLinkDirection direction = PungentAuthoringBindingLinkDirection.TwoWay)
        {
            if (endpoint == null)
                return CreateEmptyLink(sourceInterfaceId, sourceItemId, sourceElementId, sourceFieldKey, direction);

            PungentAuthoringBindingLink link = CreateLinkFromTarget(
                endpoint.target,
                sourceInterfaceId,
                sourceItemId,
                sourceElementId,
                sourceFieldKey,
                direction,
                endpoint.adapterId,
                endpoint.adapterDisplayName,
                endpoint.id,
                endpoint.label,
                PungentAuthoringBindingDiscoveryService.ToRuntimeValueType(endpoint));

            if (endpoint.target != null && link.target != null)
            {
                if (string.IsNullOrWhiteSpace(link.target.customKind))
                    link.target.customKind = endpoint.adapterId;
                if (string.IsNullOrWhiteSpace(link.target.label))
                    link.target.label = endpoint.label;
                link.target.NormalizeInPlace();
            }

            link.bindingPath = PungentAuthoringBindingDiscoveryService.CreatePath(endpoint, sourceInterfaceId);
            link.bindingSlot = PungentAuthoringBindingDiscoveryService.CreateSlot(
                SlotRoleForSource(sourceInterfaceId, sourceFieldKey),
                sourceInterfaceId,
                sourceItemId,
                sourceElementId,
                sourceFieldKey,
                endpoint.label,
                link.bindingPath);
            link.NormalizeInPlace();
            return link;
        }

        public static PungentAuthoringBindingLink CreateLinkFromTarget(
            PungentAuthoringTarget target,
            string sourceInterfaceId,
            string sourceItemId,
            string sourceElementId,
            string sourceFieldKey,
            PungentAuthoringBindingLinkDirection direction = PungentAuthoringBindingLinkDirection.TwoWay,
            string adapterId = null,
            string adapterDisplayName = null,
            string endpointId = null,
            string endpointLabel = null,
            PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown)
        {
            PungentAuthoringBindingLink link = CreateEmptyLink(sourceInterfaceId, sourceItemId, sourceElementId, sourceFieldKey, direction);
            link.target = CloneTarget(target);
            link.adapterId = string.IsNullOrWhiteSpace(adapterId) && target != null ? target.customKind : adapterId ?? string.Empty;
            link.adapterDisplayName = adapterDisplayName ?? string.Empty;
            link.endpointId = endpointId ?? string.Empty;
            link.endpointLabel = string.IsNullOrWhiteSpace(endpointLabel) && target != null ? target.label : endpointLabel ?? string.Empty;
            link.valueType = valueType;
            if (string.IsNullOrWhiteSpace(link.displayName))
                link.displayName = !string.IsNullOrWhiteSpace(link.endpointLabel) ? link.endpointLabel : "Binding Link";
            link.bindingPath = CreatePathFromTarget(target, adapterId, adapterDisplayName, endpointId, link.endpointLabel, valueType);
            link.bindingSlot = PungentAuthoringBindingDiscoveryService.CreateSlot(
                SlotRoleForSource(sourceInterfaceId, sourceFieldKey),
                sourceInterfaceId,
                sourceItemId,
                sourceElementId,
                sourceFieldKey,
                link.displayName,
                link.bindingPath);
            link.NormalizeInPlace();
            return link;
        }

        public static PungentAuthoringBindingLinkPreview PreviewLink(PungentAuthoringBindingLink link)
        {
            PungentAuthoringBindingLinkPreview result = new PungentAuthoringBindingLinkPreview
            {
                link = link,
                runtimeExportable = IsRuntimeExportable(link)
            };

            if (link == null)
            {
                result.disabledReason = "Binding link is missing.";
                result.readiness = result.disabledReason;
                return result;
            }

            link.NormalizeInPlace();
            if (!link.HasTarget)
            {
                result.disabledReason = "No target is stored on this binding link.";
                result.readiness = result.disabledReason;
                return result;
            }

            result.endpointPreview = PungentAuthoringBindingApplicationService.Preview(link.target);
            result.canRead = link.CanRead && result.endpointPreview != null && result.endpointPreview.canRead;
            string applyReason = string.Empty;
            result.canApply = link.CanApply && CanApply(link, out applyReason);
            result.disabledReason = result.canRead || result.canApply
                ? string.Empty
                : FirstNonEmpty(applyReason, result.endpointPreview != null ? result.endpointPreview.disabledReason : string.Empty, "Binding link is not ready.");
            result.readiness = BuildReadinessSummary(result);
            return result;
        }

        public static bool CanApply(PungentAuthoringBindingLink link, out string disabledReason)
        {
            disabledReason = string.Empty;
            if (link == null)
            {
                disabledReason = "Binding link is missing.";
                return false;
            }

            if (!link.CanApply)
            {
                disabledReason = "This binding link is read only.";
                return false;
            }

            if (link.target == null || !link.target.HasTarget)
            {
                disabledReason = "No target is stored on this binding link.";
                return false;
            }

            return PungentAuthoringBindingApplicationService.CanApply(link.target, out disabledReason);
        }

        public static PungentAuthoringBindingApplyResult ApplyLink(PungentAuthoringBindingLink link, string value, string undoName = null)
        {
            if (link == null)
            {
                return new PungentAuthoringBindingApplyResult
                {
                    message = "Binding link is missing."
                };
            }

            if (!CanApply(link, out string disabledReason))
            {
                return new PungentAuthoringBindingApplyResult
                {
                    adapterId = link.adapterId,
                    adapterDisplayName = link.adapterDisplayName,
                    targetLabel = link.endpointLabel,
                    message = disabledReason
                };
            }

            return PungentAuthoringBindingApplicationService.Apply(link.target, value, undoName);
        }

        public static string BuildReadinessSummary(PungentAuthoringBindingLink link)
        {
            return BuildReadinessSummary(PreviewLink(link));
        }

        public static bool ExportPlanAsset(PungentAuthoringBindingPlan plan, string assetPath, out PungentAuthoringBindingPlanAsset asset, out string message)
        {
            asset = null;
            message = string.Empty;
            if (plan == null)
            {
                message = "Binding plan is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                message = "Binding plan assets must be saved under Assets.";
                return false;
            }

            plan.NormalizeInPlace();
            if (!EnsureAssetFolder(assetPath, out message))
                return false;

            asset = AssetDatabase.LoadAssetAtPath<PungentAuthoringBindingPlanAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PungentAuthoringBindingPlanAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.plan = plan;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            message = "Exported binding plan asset: " + assetPath;
            return true;
        }

        public static PungentAuthoringBindingValueType ToRuntimeValueType(PungentAuthoringBindingValueKind kind)
        {
            switch (kind)
            {
                case PungentAuthoringBindingValueKind.Text: return PungentAuthoringBindingValueType.Text;
                case PungentAuthoringBindingValueKind.Number: return PungentAuthoringBindingValueType.Number;
                case PungentAuthoringBindingValueKind.Boolean: return PungentAuthoringBindingValueType.Boolean;
                case PungentAuthoringBindingValueKind.Enum: return PungentAuthoringBindingValueType.Enum;
                case PungentAuthoringBindingValueKind.ObjectReference: return PungentAuthoringBindingValueType.ObjectReference;
                case PungentAuthoringBindingValueKind.Json: return PungentAuthoringBindingValueType.Json;
                case PungentAuthoringBindingValueKind.UnityObject: return PungentAuthoringBindingValueType.UnityObject;
                case PungentAuthoringBindingValueKind.AssetReference: return PungentAuthoringBindingValueType.AssetReference;
                case PungentAuthoringBindingValueKind.SceneObjectReference: return PungentAuthoringBindingValueType.SceneObjectReference;
                case PungentAuthoringBindingValueKind.ComponentReference: return PungentAuthoringBindingValueType.ComponentReference;
                case PungentAuthoringBindingValueKind.Sprite: return PungentAuthoringBindingValueType.Sprite;
                case PungentAuthoringBindingValueKind.AudioClip: return PungentAuthoringBindingValueType.AudioClip;
                case PungentAuthoringBindingValueKind.Vector2: return PungentAuthoringBindingValueType.Vector2;
                case PungentAuthoringBindingValueKind.Vector3: return PungentAuthoringBindingValueType.Vector3;
                case PungentAuthoringBindingValueKind.Color: return PungentAuthoringBindingValueType.Color;
                default: return PungentAuthoringBindingValueType.Unknown;
            }
        }

        public static PungentAuthoringBindingValueKind ToEditorValueKind(PungentAuthoringBindingValueType type)
        {
            switch (type)
            {
                case PungentAuthoringBindingValueType.Text: return PungentAuthoringBindingValueKind.Text;
                case PungentAuthoringBindingValueType.Number: return PungentAuthoringBindingValueKind.Number;
                case PungentAuthoringBindingValueType.Boolean: return PungentAuthoringBindingValueKind.Boolean;
                case PungentAuthoringBindingValueType.Enum: return PungentAuthoringBindingValueKind.Enum;
                case PungentAuthoringBindingValueType.ObjectReference: return PungentAuthoringBindingValueKind.ObjectReference;
                case PungentAuthoringBindingValueType.Json: return PungentAuthoringBindingValueKind.Json;
                case PungentAuthoringBindingValueType.UnityObject: return PungentAuthoringBindingValueKind.UnityObject;
                case PungentAuthoringBindingValueType.AssetReference: return PungentAuthoringBindingValueKind.AssetReference;
                case PungentAuthoringBindingValueType.SceneObjectReference: return PungentAuthoringBindingValueKind.SceneObjectReference;
                case PungentAuthoringBindingValueType.ComponentReference: return PungentAuthoringBindingValueKind.ComponentReference;
                case PungentAuthoringBindingValueType.Sprite: return PungentAuthoringBindingValueKind.Sprite;
                case PungentAuthoringBindingValueType.AudioClip: return PungentAuthoringBindingValueKind.AudioClip;
                case PungentAuthoringBindingValueType.Vector2: return PungentAuthoringBindingValueKind.Vector2;
                case PungentAuthoringBindingValueType.Vector3: return PungentAuthoringBindingValueKind.Vector3;
                case PungentAuthoringBindingValueType.Color: return PungentAuthoringBindingValueKind.Color;
                default: return PungentAuthoringBindingValueKind.Unknown;
            }
        }

        private static PungentAuthoringBindingLink CreateEmptyLink(
            string sourceInterfaceId,
            string sourceItemId,
            string sourceElementId,
            string sourceFieldKey,
            PungentAuthoringBindingLinkDirection direction)
        {
            PungentAuthoringBindingLink link = new PungentAuthoringBindingLink
            {
                sourceInterfaceId = sourceInterfaceId ?? string.Empty,
                sourceItemId = sourceItemId ?? string.Empty,
                sourceElementId = sourceElementId ?? string.Empty,
                sourceFieldKey = sourceFieldKey ?? string.Empty,
                direction = direction
            };
            link.NormalizeInPlace();
            return link;
        }

        private static PungentAuthoringTarget CloneTarget(PungentAuthoringTarget source)
        {
            if (source == null)
                return new PungentAuthoringTarget();

            PungentAuthoringTarget target = new PungentAuthoringTarget
            {
                targetKind = source.targetKind,
                customKind = source.customKind,
                label = source.label,
                providerId = source.providerId,
                rawValue = source.rawValue,
                sourceContext = source.sourceContext,
                contextId = source.contextId,
                propertyPath = source.propertyPath
            };
            target.NormalizeInPlace();
            return target;
        }

        private static PungentAuthoringBindingPath CreatePathFromTarget(
            PungentAuthoringTarget target,
            string adapterId,
            string adapterDisplayName,
            string endpointId,
            string endpointLabel,
            PungentAuthoringBindingValueType valueType)
        {
            PungentAuthoringBindingPath path = new PungentAuthoringBindingPath
            {
                id = PungentAuthoringBindingDiscoveryService.StableId(
                    "path",
                    adapterId,
                    endpointId,
                    target == null ? string.Empty : target.contextId,
                    target == null ? string.Empty : target.propertyPath),
                displayName = endpointLabel ?? string.Empty,
                rootTarget = CloneTarget(target),
                rootLabel = target == null ? string.Empty : target.label,
                resolvedTypeName = valueType.ToString(),
                valueType = valueType,
                adapterId = adapterId ?? string.Empty,
                adapterDisplayName = adapterDisplayName ?? string.Empty,
                endpointId = endpointId ?? string.Empty,
                propertyPath = target == null ? string.Empty : target.propertyPath
            };
            path.segments.Add(new PungentAuthoringBindingPathSegment
            {
                kind = PungentAuthoringBindingPathSegmentKind.RootTarget,
                key = target == null ? string.Empty : string.IsNullOrWhiteSpace(target.contextId) ? target.rawValue : target.contextId,
                displayName = target == null ? string.Empty : target.label,
                target = CloneTarget(target),
                valueType = PungentAuthoringBindingValueType.UnityObject,
                order = 0
            });
            if (target != null && !string.IsNullOrWhiteSpace(target.propertyPath))
            {
                path.segments.Add(new PungentAuthoringBindingPathSegment
                {
                    kind = PungentAuthoringBindingPathSegmentKind.Field,
                    key = target.propertyPath,
                    displayName = string.IsNullOrWhiteSpace(endpointLabel) ? target.propertyPath : endpointLabel,
                    valueType = valueType,
                    propertyPath = target.propertyPath,
                    target = CloneTarget(target),
                    order = 1
                });
            }

            path.NormalizeInPlace();
            return path;
        }

        private static PungentAuthoringBindingSlotRole SlotRoleForSource(string sourceInterfaceId, string sourceFieldKey)
        {
            string source = sourceInterfaceId == null ? string.Empty : sourceInterfaceId.Trim();
            if (string.Equals(source, "data-sheets", StringComparison.OrdinalIgnoreCase))
                return PungentAuthoringBindingSlotRole.DataSheetCell;
            if (string.Equals(source, "rich-documents", StringComparison.OrdinalIgnoreCase))
                return PungentAuthoringBindingSlotRole.RichDocumentSemanticSpan;
            if (string.Equals(source, "board-graph", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(sourceFieldKey, "node.body", StringComparison.OrdinalIgnoreCase)
                    ? PungentAuthoringBindingSlotRole.BoardNode
                    : PungentAuthoringBindingSlotRole.BoardParameter;
            }

            return PungentAuthoringBindingSlotRole.Unknown;
        }

        private static bool EnsureAssetFolder(string assetPath, out string message)
        {
            message = string.Empty;
            string directory = Path.GetDirectoryName(assetPath);
            directory = directory == null ? string.Empty : directory.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory) || string.Equals(directory, "Assets", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!directory.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                message = "Binding plan assets must be saved under Assets.";
                return false;
            }

            string current = "Assets";
            string relative = directory.Substring("Assets/".Length);
            string[] folders = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < folders.Length; i++)
            {
                string next = current + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, folders[i]);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        message = "Could not create binding plan folder: " + next;
                        return false;
                    }
                }

                current = next;
            }

            return true;
        }

        private static bool IsRuntimeExportable(PungentAuthoringBindingLink link)
        {
            return link != null &&
                   !string.IsNullOrWhiteSpace(link.runtimeAdapterId) &&
                   link.runtimeEnabled;
        }

        private static string BuildReadinessSummary(PungentAuthoringBindingLinkPreview preview)
        {
            if (preview == null)
                return "Binding readiness: preview has not run.";
            if (preview.link == null)
                return "Binding readiness: missing link.";
            if (!string.IsNullOrWhiteSpace(preview.disabledReason))
                return "Binding readiness: " + preview.disabledReason;

            string read = preview.canRead ? "readable" : "read blocked";
            string apply = preview.canApply ? "apply ready" : "apply blocked";
            string runtime = preview.runtimeExportable ? "runtime adapter set" : "editor only";
            string value = preview.endpointPreview == null || preview.endpointPreview.valueKind == PungentAuthoringBindingValueKind.Unknown
                ? preview.link.valueType.ToString()
                : preview.endpointPreview.valueKind.ToString();
            return "Binding readiness: " + read + ", " + apply + ", " + value + ", " + runtime + ".";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            return string.Empty;
        }
    }
#endif
}
