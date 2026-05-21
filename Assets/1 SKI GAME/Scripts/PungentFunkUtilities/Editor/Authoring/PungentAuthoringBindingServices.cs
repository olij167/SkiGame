using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    // RDE/GUIDED-BINDING MIGRATION NOTE: this is the shared editor-only binding picker/preview/apply layer.
    // CROSS-UTILITY BINDING SURFACE:
    // Rich Documents, Data Sheets, and future BoardGraph binding UX should consume this editor-only layer for
    // endpoint picking, preview, and apply behavior instead of creating separate SerializedProperty pickers.
    // BoardGraph should integrate here when its binding pass arrives; do not introduce a third picker path.
    public enum PungentAuthoringBindingValueKind
    {
        Unknown = 0,
        Text = 10,
        Number = 20,
        Boolean = 30,
        Enum = 40,
        ObjectReference = 50,
        Json = 60,
        UnityObject = 70,
        AssetReference = 80,
        SceneObjectReference = 90,
        ComponentReference = 100,
        Sprite = 110,
        AudioClip = 120,
        Vector2 = 130,
        Vector3 = 140,
        Color = 150
    }

    public sealed class PungentAuthoringBindingEndpoint
    {
        public string id = string.Empty;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string label = string.Empty;
        public string groupLabel = string.Empty;
        public string endpointKind = string.Empty;
        public string tooltip = string.Empty;
        public string helpText = string.Empty;
        public string disabledReason = string.Empty;
        public List<string> compatibleSemanticKinds = new List<string>();
        public PungentAuthoringBindingValueKind valueKind = PungentAuthoringBindingValueKind.Unknown;
        public PungentAuthoringTarget target;
        public bool canRead = true;
        public bool canApply = true;

        public bool HasTarget => target != null && target.HasTarget;
        public bool CanBind => HasTarget && string.IsNullOrWhiteSpace(disabledReason);
    }

    public sealed class PungentAuthoringBindingPreview
    {
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string targetLabel = string.Empty;
        public string currentValue = string.Empty;
        public string warning = string.Empty;
        public string disabledReason = string.Empty;
        public PungentAuthoringBindingValueKind valueKind = PungentAuthoringBindingValueKind.Unknown;
        public bool canRead;
        public bool canApply;
    }

    public sealed class PungentAuthoringBindingApplyResult
    {
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string targetLabel = string.Empty;
        public string message = string.Empty;
        public bool applied;
    }

    public interface IPungentAuthoringBindingAdapter
    {
        string Id { get; }
        string DisplayName { get; }
        IEnumerable<PungentAuthoringBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject);
    }

    public interface IPungentAuthoringBindingPreviewAdapter : IPungentAuthoringBindingAdapter
    {
        bool TryPreview(PungentAuthoringTarget target, out PungentAuthoringBindingPreview preview);
    }

    public interface IPungentAuthoringBindingApplyAdapter : IPungentAuthoringBindingPreviewAdapter
    {
        bool CanApply(PungentAuthoringTarget target, out string disabledReason);
        PungentAuthoringBindingApplyResult Apply(PungentAuthoringTarget target, string value, string undoName = null);
    }

    public class PungentAuthoringGuidedBindingState
    {
        public UnityEngine.Object targetObject;
        public string selectedGroupLabel = string.Empty;
        public string selectedEndpointId = string.Empty;
        public Vector2 scroll;
    }

    public sealed class PungentAuthoringGuidedBindingOptions
    {
        public string contextLabel = "Choose Field";
        public string objectLabel = "Target Object";
        public string componentLabel = "Component";
        public string endpointLabel = "Field";
        public string bindButtonLabel = "Bind Field";
        public string helpText = "Pick an explicit object, component, and compatible field.";
        public bool allowSceneObjects = true;
        public bool showHeader = true;
        public bool showHelp = true;
        public bool showPreview = true;
        public bool showReadiness = true;
        public bool showBindButton = true;
        public List<PungentAuthoringBindingValueKind> allowedValueKinds = new List<PungentAuthoringBindingValueKind>();

        public bool Allows(PungentAuthoringBindingValueKind kind)
        {
            return allowedValueKinds == null ||
                   allowedValueKinds.Count == 0 ||
                   allowedValueKinds.Contains(kind);
        }
    }

    public sealed class PungentAuthoringGuidedBindingResult
    {
        public PungentAuthoringBindingEndpoint selectedEndpoint;
        public PungentAuthoringBindingPreview preview;
        public string disabledReason = string.Empty;
        public bool bindClicked;
        public bool pingClicked;
        public bool copyTargetClicked;

        public bool HasEndpoint => selectedEndpoint != null;
        public bool CanBind => selectedEndpoint != null && selectedEndpoint.CanBind;
    }

    public static class PungentAuthoringBindingDiagnostics
    {
        public static string DescribeEndpoint(PungentAuthoringBindingEndpoint endpoint)
        {
            if (endpoint == null)
                return "No field selected.";

            string path = endpoint.target == null || string.IsNullOrWhiteSpace(endpoint.target.propertyPath)
                ? "No property path"
                : endpoint.target.propertyPath;
            string target = endpoint.target == null || string.IsNullOrWhiteSpace(endpoint.target.contextId)
                ? "No explicit target"
                : "Target ID set";
            string ready = endpoint.CanBind ? "Ready" : string.IsNullOrWhiteSpace(endpoint.disabledReason) ? "Blocked" : "Blocked: " + endpoint.disabledReason;
            return endpoint.valueKind + " | " + path + " | " + target + " | " + ready;
        }

        public static string DescribePreview(PungentAuthoringBindingPreview preview)
        {
            if (preview == null)
                return "Preview not run.";
            if (!string.IsNullOrWhiteSpace(preview.disabledReason))
                return "Blocked: " + preview.disabledReason;

            string read = preview.canRead ? "Reads" : "No read";
            string apply = preview.canApply ? "Writes" : "Read only";
            string value = string.IsNullOrEmpty(preview.currentValue) ? "<empty>" : preview.currentValue;
            return read + " | " + apply + " | " + preview.valueKind + " | Current: " + value;
        }
    }

    public sealed class PungentAuthoringBindingPickerState : PungentAuthoringGuidedBindingState
    {
    }

    public static class PungentAuthoringGuidedBindingView
    {
        public static PungentAuthoringGuidedBindingResult Draw(PungentAuthoringGuidedBindingState state, PungentAuthoringGuidedBindingOptions options = null)
        {
            PungentAuthoringGuidedBindingResult result = new PungentAuthoringGuidedBindingResult();
            if (state == null)
            {
                result.disabledReason = "Binding picker state is missing.";
                EditorGUILayout.HelpBox(result.disabledReason, MessageType.Warning);
                return result;
            }

            options = options ?? new PungentAuthoringGuidedBindingOptions();
            if (options.showHeader && !string.IsNullOrWhiteSpace(options.contextLabel))
                EditorGUILayout.LabelField(options.contextLabel, EditorStyles.boldLabel);
            if (options.showHelp && !string.IsNullOrWhiteSpace(options.helpText))
                EditorGUILayout.HelpBox(options.helpText, MessageType.None);

            EditorGUI.BeginChangeCheck();
            state.targetObject = EditorGUILayout.ObjectField(
                new GUIContent(options.objectLabel, "Choose the explicit object, asset, or component to inspect."),
                state.targetObject,
                typeof(UnityEngine.Object),
                options.allowSceneObjects);
            if (EditorGUI.EndChangeCheck())
            {
                state.selectedGroupLabel = string.Empty;
                state.selectedEndpointId = string.Empty;
            }

            List<PungentAuthoringBindingEndpoint> endpoints = PungentAuthoringBindingAdapterRegistry.GetEndpoints(state.targetObject)
                .Where(endpoint => endpoint != null && options.Allows(endpoint.valueKind))
                .ToList();
            if (endpoints.Count == 0)
            {
                result.disabledReason = state.targetObject == null
                    ? "Pick an object, asset, or component to see fields."
                    : "No compatible fields were found on the explicit target.";
                EditorGUILayout.HelpBox(result.disabledReason, MessageType.Info);
                return result;
            }

            List<string> groups = endpoints
                .Select(GroupLabelFor)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(state.selectedGroupLabel) ||
                    !groups.Any(group => string.Equals(group, state.selectedGroupLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    state.selectedGroupLabel = groups[0];
                    state.selectedEndpointId = string.Empty;
                }

                int groupIndex = Mathf.Max(0, groups.FindIndex(group => string.Equals(group, state.selectedGroupLabel, StringComparison.OrdinalIgnoreCase)));
                int nextGroupIndex = EditorGUILayout.Popup(
                    new GUIContent(options.componentLabel, "Choose the selected object's component or field group."),
                    groupIndex,
                    groups.ToArray());
                nextGroupIndex = Mathf.Clamp(nextGroupIndex, 0, groups.Count - 1);
                if (nextGroupIndex != groupIndex)
                {
                    state.selectedGroupLabel = groups[nextGroupIndex];
                    state.selectedEndpointId = string.Empty;
                }
            }

            List<PungentAuthoringBindingEndpoint> visibleEndpoints = endpoints
                .Where(endpoint => string.IsNullOrWhiteSpace(state.selectedGroupLabel) ||
                                   string.Equals(GroupLabelFor(endpoint), state.selectedGroupLabel, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (visibleEndpoints.Count == 0)
                visibleEndpoints = endpoints;

            if (string.IsNullOrWhiteSpace(state.selectedEndpointId) ||
                !visibleEndpoints.Any(endpoint => string.Equals(endpoint.id, state.selectedEndpointId, StringComparison.OrdinalIgnoreCase)))
            {
                state.selectedEndpointId = visibleEndpoints[0].id;
            }

            string[] endpointLabels = visibleEndpoints.Select(endpoint => EndpointMenuLabel(endpoint, state.selectedGroupLabel)).ToArray();
            int selectedIndex = Mathf.Max(0, visibleEndpoints.FindIndex(endpoint => string.Equals(endpoint.id, state.selectedEndpointId, StringComparison.OrdinalIgnoreCase)));
            selectedIndex = EditorGUILayout.Popup(
                new GUIContent(options.endpointLabel, "Choose the exact field to bind."),
                selectedIndex,
                endpointLabels);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, visibleEndpoints.Count - 1);
            PungentAuthoringBindingEndpoint selected = visibleEndpoints[selectedIndex];
            state.selectedEndpointId = selected.id;
            result.selectedEndpoint = selected;

            if (options.showPreview && selected.target != null)
            {
                result.preview = PungentAuthoringBindingApplicationService.Preview(selected.target);
            }

            DrawSelectedFieldSummary(selected, result.preview);

            if (options.showReadiness)
                DrawReadinessStatus(selected, result.preview);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(options.showBindButton && !selected.CanBind))
                {
                    if (options.showBindButton && GUILayout.Button(new GUIContent(options.bindButtonLabel, selected.CanBind ? "Store this field as the binding target." : selected.disabledReason), EditorStyles.miniButton))
                        result.bindClicked = true;
                }

                using (new EditorGUI.DisabledScope(selected.target == null || !selected.target.HasTarget))
                {
                    if (GUILayout.Button(new GUIContent("Ping", "Ping and select the bound target."), EditorStyles.miniButton, GUILayout.Width(48f)))
                    {
                        result.pingClicked = true;
                        PingTarget(selected.target);
                    }

                    if (GUILayout.Button(new GUIContent("Copy Path", "Copy the target reference path."), EditorStyles.miniButton, GUILayout.Width(74f)))
                    {
                        result.copyTargetClicked = true;
                        EditorGUIUtility.systemCopyBuffer = TargetCopyText(selected.target);
                    }
                }
            }

            return result;
        }

        private static void DrawSelectedFieldSummary(PungentAuthoringBindingEndpoint endpoint, PungentAuthoringBindingPreview preview)
        {
            if (endpoint == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Field", string.IsNullOrWhiteSpace(endpoint.label) ? endpoint.id : endpoint.label, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Value Type", endpoint.valueKind.ToString(), EditorStyles.miniLabel);
                if (endpoint.target != null && !string.IsNullOrWhiteSpace(endpoint.target.propertyPath))
                    EditorGUILayout.LabelField("Property Path", endpoint.target.propertyPath, EditorStyles.miniLabel);
                string current = preview == null || !string.IsNullOrWhiteSpace(preview.disabledReason)
                    ? string.Empty
                    : string.IsNullOrEmpty(preview.currentValue) ? "<empty>" : preview.currentValue;
                if (!string.IsNullOrWhiteSpace(current))
                    EditorGUILayout.LabelField("Current Value", current, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrWhiteSpace(endpoint.disabledReason))
                EditorGUILayout.HelpBox(endpoint.disabledReason, MessageType.Warning);
            if (preview != null && !string.IsNullOrWhiteSpace(preview.disabledReason))
                EditorGUILayout.HelpBox(preview.disabledReason, MessageType.Info);
            else if (preview != null && !string.IsNullOrWhiteSpace(preview.warning))
                EditorGUILayout.HelpBox(preview.warning, MessageType.None);
        }

        private static void DrawReadinessStatus(PungentAuthoringBindingEndpoint endpoint, PungentAuthoringBindingPreview preview)
        {
            EditorGUILayout.LabelField("Status", BindingStatus(endpoint, preview), EditorStyles.wordWrappedMiniLabel);
        }

        private static string BindingStatus(PungentAuthoringBindingEndpoint endpoint, PungentAuthoringBindingPreview preview)
        {
            if (endpoint == null)
                return "No field selected.";
            if (!string.IsNullOrWhiteSpace(endpoint.disabledReason))
                return "Blocked: " + endpoint.disabledReason;
            if (preview != null && !string.IsNullOrWhiteSpace(preview.disabledReason))
                return "Blocked: " + preview.disabledReason;

            bool reads = endpoint.canRead && (preview == null || preview.canRead || string.IsNullOrWhiteSpace(preview.disabledReason));
            bool writes = endpoint.canApply && (preview == null || preview.canApply);
            if (reads && writes)
                return "Ready: reads and writes";
            if (reads)
                return "Ready: reads only";
            if (writes)
                return "Ready: writes only";
            return "Blocked: field cannot read or write.";
        }

        private static string GroupLabelFor(PungentAuthoringBindingEndpoint endpoint)
        {
            if (endpoint == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(endpoint.groupLabel))
                return endpoint.groupLabel;

            string label = endpoint.label ?? string.Empty;
            int slash = label.LastIndexOf(" / ", StringComparison.Ordinal);
            return slash > 0 ? label.Substring(0, slash) : (string.IsNullOrWhiteSpace(endpoint.adapterDisplayName) ? "Field" : endpoint.adapterDisplayName);
        }

        private static string EndpointMenuLabel(PungentAuthoringBindingEndpoint endpoint, string groupLabel)
        {
            if (endpoint == null)
                return string.Empty;

            string label = string.IsNullOrWhiteSpace(endpoint.label) ? endpoint.id ?? string.Empty : endpoint.label;
            if (!string.IsNullOrWhiteSpace(groupLabel) && label.StartsWith(groupLabel + " / ", StringComparison.OrdinalIgnoreCase))
                label = label.Substring(groupLabel.Length + 3);
            label += " (" + endpoint.valueKind + ")";
            return label;
        }

        private static void PingTarget(PungentAuthoringTarget target)
        {
            if (target == null)
                return;

            UnityEngine.Object targetObject;
            string error;
            if (!PungentAuthoringBindingApplicationService.TryResolveUnityTarget(target, out targetObject, out error) || targetObject == null)
                return;

            Selection.activeObject = targetObject;
            EditorGUIUtility.PingObject(targetObject);
        }

        private static string TargetCopyText(PungentAuthoringTarget target)
        {
            if (target == null)
                return string.Empty;

            return target.targetKind + "|" +
                   (target.customKind ?? string.Empty) + "|" +
                   (target.contextId ?? string.Empty) + "|" +
                   (target.rawValue ?? string.Empty) + "|" +
                   (target.propertyPath ?? string.Empty);
        }
    }

    public static class PungentAuthoringBindingPickerGUI
    {
        public static PungentAuthoringBindingEndpoint Draw(PungentAuthoringBindingPickerState state, string objectLabel = "Object")
        {
            PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
            {
                objectLabel = objectLabel,
                showHeader = false,
                showHelp = false,
                showPreview = false,
                showReadiness = false,
                showBindButton = false
            };
            return PungentAuthoringGuidedBindingView.Draw(state, options).selectedEndpoint;
        }
    }

    public sealed class PungentAuthoringBindingDraftRow
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public PungentAuthoringTarget target = new PungentAuthoringTarget();
        public bool include = true;
        public bool pullEnabled = true;
        public bool pushEnabled = true;
    }

    public sealed class PungentAuthoringBindingDraftColumn
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public PungentAuthoringBindingPath path = new PungentAuthoringBindingPath();
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public bool include = true;
        public bool pullEnabled = true;
        public bool pushEnabled = true;
    }

    public sealed class PungentAuthoringBindingDraft
    {
        public string id = string.Empty;
        public string displayName = "Binding Draft";
        public string sourceContext = string.Empty;
        public List<PungentAuthoringBindingDiscoverySnapshot> snapshots = new List<PungentAuthoringBindingDiscoverySnapshot>();
        public List<PungentAuthoringBindingDraftRow> rows = new List<PungentAuthoringBindingDraftRow>();
        public List<PungentAuthoringBindingDraftColumn> columns = new List<PungentAuthoringBindingDraftColumn>();

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Binding Draft" : displayName.Trim();
            sourceContext = sourceContext == null ? string.Empty : sourceContext.Trim();
            snapshots = snapshots ?? new List<PungentAuthoringBindingDiscoverySnapshot>();
            rows = rows ?? new List<PungentAuthoringBindingDraftRow>();
            columns = columns ?? new List<PungentAuthoringBindingDraftColumn>();

            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                if (snapshots[i] == null)
                {
                    snapshots.RemoveAt(i);
                    continue;
                }

                snapshots[i].NormalizeInPlace();
            }

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (rows[i] == null)
                {
                    rows.RemoveAt(i);
                    continue;
                }

                rows[i].id = PungentAuthoringId.Normalize(rows[i].id);
                if (string.IsNullOrWhiteSpace(rows[i].id))
                    rows[i].id = PungentAuthoringId.NewValue();
                rows[i].displayName = string.IsNullOrWhiteSpace(rows[i].displayName) ? "Target" : rows[i].displayName.Trim();
                rows[i].target = rows[i].target ?? new PungentAuthoringTarget();
                rows[i].target.NormalizeInPlace();
            }

            for (int i = columns.Count - 1; i >= 0; i--)
            {
                if (columns[i] == null)
                {
                    columns.RemoveAt(i);
                    continue;
                }

                columns[i].id = PungentAuthoringId.Normalize(columns[i].id);
                if (string.IsNullOrWhiteSpace(columns[i].id))
                    columns[i].id = PungentAuthoringId.NewValue();
                columns[i].displayName = string.IsNullOrWhiteSpace(columns[i].displayName) ? "Field" : columns[i].displayName.Trim();
                columns[i].path = columns[i].path ?? new PungentAuthoringBindingPath();
                columns[i].path.NormalizeInPlace();
                if (columns[i].valueType == PungentAuthoringBindingValueType.Unknown)
                    columns[i].valueType = columns[i].path.valueType;
            }
        }
    }

    public sealed class PungentAuthoringBindingApplyRequest
    {
        public PungentAuthoringBindingLink link;
        public string value = string.Empty;
        public string undoName = string.Empty;
    }

    public static class PungentAuthoringBindingValueCodec
    {
        public static string Format(PungentAuthoringBindingValueType valueType, object value)
        {
            if (value == null)
                return string.Empty;

            if (value is UnityEngine.Object unityObject)
                return FormatUnityObjectReference(unityObject);
            if (value is Vector2 vector2)
                return vector2.x.ToString(CultureInfo.InvariantCulture) + ", " + vector2.y.ToString(CultureInfo.InvariantCulture);
            if (value is Vector3 vector3)
                return vector3.x.ToString(CultureInfo.InvariantCulture) + ", " + vector3.y.ToString(CultureInfo.InvariantCulture) + ", " + vector3.z.ToString(CultureInfo.InvariantCulture);
            if (value is Color color)
                return "#" + ColorUtility.ToHtmlStringRGBA(color);
            if (value is bool boolValue)
                return boolValue ? "true" : "false";
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        public static bool TryParse(PungentAuthoringBindingValueType valueType, string text, out object value, out string error)
        {
            value = text ?? string.Empty;
            error = string.Empty;
            string clean = text == null ? string.Empty : text.Trim();

            switch (valueType)
            {
                case PungentAuthoringBindingValueType.Number:
                    float number;
                    if (!float.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        error = "Value is not a valid number.";
                        return false;
                    }
                    value = number;
                    return true;
                case PungentAuthoringBindingValueType.Boolean:
                    bool boolValue;
                    if (!TryParseBoolean(clean, out boolValue))
                    {
                        error = "Value is not a valid boolean.";
                        return false;
                    }
                    value = boolValue;
                    return true;
                case PungentAuthoringBindingValueType.Vector2:
                    Vector2 vector2;
                    if (!TryParseVector2(clean, out vector2))
                    {
                        error = "Value is not a valid Vector2. Use x, y.";
                        return false;
                    }
                    value = vector2;
                    return true;
                case PungentAuthoringBindingValueType.Vector3:
                    Vector3 vector3;
                    if (!TryParseVector3(clean, out vector3))
                    {
                        error = "Value is not a valid Vector3. Use x, y, z.";
                        return false;
                    }
                    value = vector3;
                    return true;
                case PungentAuthoringBindingValueType.Color:
                    Color color;
                    if (!TryParseColor(clean, out color))
                    {
                        error = "Value is not a valid color. Use #RRGGBB, #RRGGBBAA, or r, g, b, a.";
                        return false;
                    }
                    value = color;
                    return true;
                case PungentAuthoringBindingValueType.ObjectReference:
                case PungentAuthoringBindingValueType.UnityObject:
                case PungentAuthoringBindingValueType.AssetReference:
                case PungentAuthoringBindingValueType.SceneObjectReference:
                case PungentAuthoringBindingValueType.ComponentReference:
                case PungentAuthoringBindingValueType.Sprite:
                case PungentAuthoringBindingValueType.AudioClip:
                    UnityEngine.Object objectReference;
                    if (!TryResolveObjectReferenceValue(clean, out objectReference, out error))
                        return false;
                    value = objectReference;
                    return true;
                default:
                    value = text ?? string.Empty;
                    return true;
            }
        }

        public static string FormatSerializedProperty(SerializedProperty property)
        {
            if (property == null)
                return string.Empty;

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Enum:
                    return property.enumDisplayNames != null &&
                           property.enumValueIndex >= 0 &&
                           property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.ObjectReference:
                    return FormatUnityObjectReference(property.objectReferenceValue);
                case SerializedPropertyType.Vector2:
                    return Format(PungentAuthoringBindingValueType.Vector2, property.vector2Value);
                case SerializedPropertyType.Vector3:
                    return Format(PungentAuthoringBindingValueType.Vector3, property.vector3Value);
                case SerializedPropertyType.Color:
                    return Format(PungentAuthoringBindingValueType.Color, property.colorValue);
                default:
                    return string.Empty;
            }
        }

        public static bool TryWriteSerializedProperty(SerializedProperty property, string value, out string error)
        {
            error = string.Empty;
            if (property == null)
            {
                error = "Bound property could not be found.";
                return false;
            }

            string text = value ?? string.Empty;
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = text;
                    return true;
                case SerializedPropertyType.Integer:
                    int intValue;
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                    {
                        error = "Value is not a valid integer.";
                        return false;
                    }
                    property.intValue = intValue;
                    return true;
                case SerializedPropertyType.Float:
                    float floatValue;
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                    {
                        error = "Value is not a valid number.";
                        return false;
                    }
                    property.floatValue = floatValue;
                    return true;
                case SerializedPropertyType.Boolean:
                    bool boolValue;
                    if (!TryParseBoolean(text, out boolValue))
                    {
                        error = "Value is not a valid boolean.";
                        return false;
                    }
                    property.boolValue = boolValue;
                    return true;
                case SerializedPropertyType.Enum:
                    int enumIndex = Array.FindIndex(property.enumDisplayNames ?? new string[0], option => string.Equals(option, text, StringComparison.OrdinalIgnoreCase));
                    if (enumIndex < 0 && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out enumIndex))
                    {
                        error = "Value is not a valid enum option.";
                        return false;
                    }
                    if (enumIndex < 0 || property.enumDisplayNames == null || enumIndex >= property.enumDisplayNames.Length)
                    {
                        error = "Enum option is outside the valid range.";
                        return false;
                    }
                    property.enumValueIndex = enumIndex;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object objectReference;
                    if (!TryResolveObjectReferenceValue(text, out objectReference, out error))
                        return false;
                    property.objectReferenceValue = objectReference;
                    return true;
                case SerializedPropertyType.Vector2:
                    Vector2 vector2;
                    if (!TryParseVector2(text, out vector2))
                    {
                        error = "Value is not a valid Vector2. Use x, y.";
                        return false;
                    }
                    property.vector2Value = vector2;
                    return true;
                case SerializedPropertyType.Vector3:
                    Vector3 vector3;
                    if (!TryParseVector3(text, out vector3))
                    {
                        error = "Value is not a valid Vector3. Use x, y, z.";
                        return false;
                    }
                    property.vector3Value = vector3;
                    return true;
                case SerializedPropertyType.Color:
                    Color color;
                    if (!TryParseColor(text, out color))
                    {
                        error = "Value is not a valid color. Use #RRGGBB, #RRGGBBAA, or r, g, b, a.";
                        return false;
                    }
                    property.colorValue = color;
                    return true;
                default:
                    error = "This serialized property type is read-only in the generic binding service.";
                    return false;
            }
        }

        public static PungentAuthoringBindingValueType RuntimeValueTypeForSerializedProperty(SerializedProperty property)
        {
            if (property == null)
                return PungentAuthoringBindingValueType.Unknown;

            switch (property.propertyType)
            {
                case SerializedPropertyType.String: return PungentAuthoringBindingValueType.Text;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float: return PungentAuthoringBindingValueType.Number;
                case SerializedPropertyType.Boolean: return PungentAuthoringBindingValueType.Boolean;
                case SerializedPropertyType.Enum: return PungentAuthoringBindingValueType.Enum;
                case SerializedPropertyType.Vector2: return PungentAuthoringBindingValueType.Vector2;
                case SerializedPropertyType.Vector3: return PungentAuthoringBindingValueType.Vector3;
                case SerializedPropertyType.Color: return PungentAuthoringBindingValueType.Color;
                case SerializedPropertyType.ObjectReference: return RuntimeValueTypeForObjectReferenceProperty(property);
                default: return PungentAuthoringBindingValueType.Unknown;
            }
        }

        public static PungentAuthoringBindingValueKind ToEditorValueKind(PungentAuthoringBindingValueType valueType)
        {
            switch (valueType)
            {
                case PungentAuthoringBindingValueType.Text: return PungentAuthoringBindingValueKind.Text;
                case PungentAuthoringBindingValueType.Number: return PungentAuthoringBindingValueKind.Number;
                case PungentAuthoringBindingValueType.Boolean: return PungentAuthoringBindingValueKind.Boolean;
                case PungentAuthoringBindingValueType.Enum: return PungentAuthoringBindingValueKind.Enum;
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
                case PungentAuthoringBindingValueType.ObjectReference: return PungentAuthoringBindingValueKind.ObjectReference;
                default: return PungentAuthoringBindingValueKind.Unknown;
            }
        }

        public static bool IsUnityObjectFacing(PungentAuthoringBindingValueType valueType)
        {
            return valueType == PungentAuthoringBindingValueType.UnityObject ||
                   valueType == PungentAuthoringBindingValueType.ObjectReference ||
                   valueType == PungentAuthoringBindingValueType.AssetReference ||
                   valueType == PungentAuthoringBindingValueType.SceneObjectReference ||
                   valueType == PungentAuthoringBindingValueType.ComponentReference ||
                   valueType == PungentAuthoringBindingValueType.Sprite ||
                   valueType == PungentAuthoringBindingValueType.AudioClip;
        }

        private static PungentAuthoringBindingValueType RuntimeValueTypeForObjectReferenceProperty(SerializedProperty property)
        {
            UnityEngine.Object reference = property.objectReferenceValue;
            if (reference is Sprite)
                return PungentAuthoringBindingValueType.Sprite;
            if (reference is AudioClip)
                return PungentAuthoringBindingValueType.AudioClip;
            if (reference is Component)
                return PungentAuthoringBindingValueType.ComponentReference;

            string propertyType = property.type ?? string.Empty;
            if (propertyType.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0)
                return PungentAuthoringBindingValueType.Sprite;
            if (propertyType.IndexOf("AudioClip", StringComparison.OrdinalIgnoreCase) >= 0)
                return PungentAuthoringBindingValueType.AudioClip;
            if (propertyType.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyType.IndexOf("Behaviour", StringComparison.OrdinalIgnoreCase) >= 0)
                return PungentAuthoringBindingValueType.ComponentReference;

            if (reference != null)
            {
                string path = AssetDatabase.GetAssetPath(reference);
                if (!string.IsNullOrWhiteSpace(path))
                    return PungentAuthoringBindingValueType.AssetReference;
                if (reference is GameObject)
                    return PungentAuthoringBindingValueType.SceneObjectReference;
                return PungentAuthoringBindingValueType.UnityObject;
            }

            return PungentAuthoringBindingValueType.ObjectReference;
        }

        private static string FormatUnityObjectReference(UnityEngine.Object target)
        {
            if (target == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(target);
            string guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrWhiteSpace(guid))
                return target.name + " [" + guid + "]";

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            string globalIdText = globalId.ToString();
            return string.IsNullOrWhiteSpace(globalIdText)
                ? target.name
                : target.name + " [" + globalIdText + "]";
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            if (bool.TryParse(value, out result))
                return true;
            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }
            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }

        private static bool TryParseVector2(string value, out Vector2 result)
        {
            result = Vector2.zero;
            string[] parts = SplitNumericTuple(value);
            if (parts.Length != 2)
                return false;

            float x;
            float y;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                return false;

            result = new Vector2(x, y);
            return true;
        }

        private static bool TryParseVector3(string value, out Vector3 result)
        {
            result = Vector3.zero;
            string[] parts = SplitNumericTuple(value);
            if (parts.Length != 3)
                return false;

            float x;
            float y;
            float z;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                return false;

            result = new Vector3(x, y, z);
            return true;
        }

        private static bool TryParseColor(string value, out Color result)
        {
            result = Color.white;
            string clean = value == null ? string.Empty : value.Trim();
            if (clean.StartsWith("#", StringComparison.Ordinal) && ColorUtility.TryParseHtmlString(clean, out result))
                return true;

            string[] parts = SplitNumericTuple(clean);
            if (parts.Length != 3 && parts.Length != 4)
                return false;

            float r;
            float g;
            float b;
            float a = 1f;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out r) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out g) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                return false;
            if (parts.Length == 4 && !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a))
                return false;

            if (r > 1f || g > 1f || b > 1f || a > 1f)
            {
                r /= 255f;
                g /= 255f;
                b /= 255f;
                a /= 255f;
            }

            result = new Color(r, g, b, a);
            return true;
        }

        private static string[] SplitNumericTuple(string value)
        {
            string clean = (value ?? string.Empty).Trim().Trim('(', ')', '[', ']');
            return clean.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
        }

        private static bool TryResolveObjectReferenceValue(string value, out UnityEngine.Object objectReference, out string error)
        {
            objectReference = null;
            error = string.Empty;

            string text = value == null ? string.Empty : value.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string bracketValue = ExtractBracketValue(text);
            if (!string.IsNullOrWhiteSpace(bracketValue))
                text = bracketValue;

            GlobalObjectId globalId;
            if (GlobalObjectId.TryParse(text, out globalId))
            {
                objectReference = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (objectReference != null)
                    return true;
            }

            string path = text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? text
                : AssetDatabase.GUIDToAssetPath(text);
            if (!string.IsNullOrWhiteSpace(path))
            {
                objectReference = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (objectReference != null)
                    return true;
            }

            error = "Object reference values must be empty, an asset path, an asset GUID, or a GlobalObjectId.";
            return false;
        }

        private static string ExtractBracketValue(string value)
        {
            int open = string.IsNullOrEmpty(value) ? -1 : value.LastIndexOf('[');
            int close = string.IsNullOrEmpty(value) ? -1 : value.LastIndexOf(']');
            if (open < 0 || close <= open)
                return string.Empty;

            return value.Substring(open + 1, close - open - 1).Trim();
        }
    }

    public static class PungentAuthoringBindingDiscoveryService
    {
        public static PungentAuthoringBindingDiscoverySnapshot BuildSnapshot(UnityEngine.Object targetObject, string sourceContext = null)
        {
            PungentAuthoringBindingDiscoverySnapshot snapshot = new PungentAuthoringBindingDiscoverySnapshot
            {
                displayName = targetObject == null ? "Binding Discovery Snapshot" : targetObject.name + " Binding Fields",
                sourceContext = sourceContext ?? string.Empty,
                rootTarget = CreateRootTarget(targetObject, sourceContext),
                rootLabel = targetObject == null ? string.Empty : targetObject.name
            };

            PungentAuthoringBindingPath rootPath = CreateRootObjectPath(targetObject, snapshot.rootTarget, sourceContext);
            if (rootPath != null)
                snapshot.paths.Add(rootPath);

            foreach (PungentAuthoringBindingEndpoint endpoint in PungentAuthoringBindingAdapterRegistry.GetEndpoints(targetObject))
            {
                PungentAuthoringBindingPath path = CreatePath(endpoint, sourceContext);
                if (path != null)
                    snapshot.paths.Add(path);
            }

            snapshot.id = StableId("snapshot", sourceContext, snapshot.rootTarget == null ? string.Empty : snapshot.rootTarget.rawValue, snapshot.rootTarget == null ? string.Empty : snapshot.rootTarget.contextId);
            snapshot.NormalizeInPlace();
            return snapshot;
        }

        public static PungentAuthoringBindingPath CreateRootObjectPath(UnityEngine.Object targetObject, PungentAuthoringTarget rootTarget = null, string sourceContext = null)
        {
            if (targetObject == null)
                return null;

            PungentAuthoringBindingValueType valueType = RuntimeValueTypeForUnityObject(targetObject);
            PungentAuthoringTarget target = rootTarget == null || !rootTarget.HasTarget
                ? CreateRootTarget(targetObject, sourceContext)
                : CloneTarget(rootTarget);
            PungentAuthoringBindingPath path = new PungentAuthoringBindingPath
            {
                id = StableId("root-path", sourceContext, target.rawValue, target.contextId, targetObject.GetType().FullName),
                displayName = targetObject.name,
                rootTarget = target,
                rootLabel = targetObject.name,
                resolvedTypeName = targetObject.GetType().FullName ?? targetObject.GetType().Name,
                valueType = valueType,
                adapterId = "authoring-root-object",
                adapterDisplayName = "Root Object",
                endpointId = "root:" + (target.rawValue ?? string.Empty) + ":" + (target.contextId ?? string.Empty),
                notes = "Stable root object path for binding drafts and runtime plan metadata."
            };
            path.segments.Add(new PungentAuthoringBindingPathSegment
            {
                kind = targetObject is Component
                    ? PungentAuthoringBindingPathSegmentKind.Component
                    : string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(targetObject))
                        ? PungentAuthoringBindingPathSegmentKind.SceneObject
                        : PungentAuthoringBindingPathSegmentKind.Asset,
                key = string.IsNullOrWhiteSpace(target.contextId) ? target.rawValue : target.contextId,
                displayName = targetObject.name,
                typeName = path.resolvedTypeName,
                valueType = valueType,
                target = CloneTarget(target),
                order = 0
            });
            path.NormalizeInPlace();
            return path;
        }

        public static PungentAuthoringBindingPath CreatePath(PungentAuthoringBindingEndpoint endpoint, string sourceContext = null)
        {
            if (endpoint == null)
                return null;

            PungentAuthoringBindingValueType valueType = ToRuntimeValueType(endpoint);
            PungentAuthoringTarget rootTarget = CloneTarget(endpoint.target);
            if (rootTarget != null && string.IsNullOrWhiteSpace(rootTarget.sourceContext))
                rootTarget.sourceContext = sourceContext ?? string.Empty;

            PungentAuthoringBindingPath path = new PungentAuthoringBindingPath
            {
                id = StableId("path", endpoint.adapterId, endpoint.id, endpoint.target == null ? string.Empty : endpoint.target.contextId, endpoint.target == null ? string.Empty : endpoint.target.propertyPath),
                displayName = string.IsNullOrWhiteSpace(endpoint.label) ? endpoint.id : endpoint.label,
                rootTarget = rootTarget,
                rootLabel = endpoint.target == null ? string.Empty : endpoint.target.label,
                resolvedTypeName = endpoint.endpointKind,
                valueType = valueType,
                adapterId = endpoint.adapterId,
                adapterDisplayName = endpoint.adapterDisplayName,
                endpointId = endpoint.id,
                propertyPath = endpoint.target == null ? string.Empty : endpoint.target.propertyPath,
                notes = endpoint.tooltip
            };

            path.segments.Add(new PungentAuthoringBindingPathSegment
            {
                kind = SegmentKindForRoot(endpoint.target),
                key = endpoint.target == null ? string.Empty : FirstNonEmpty(endpoint.target.contextId, endpoint.target.rawValue),
                displayName = endpoint.target == null ? "Target" : endpoint.target.label,
                target = CloneTarget(endpoint.target),
                valueType = PungentAuthoringBindingValueType.UnityObject,
                order = 0
            });
            if (!string.IsNullOrWhiteSpace(endpoint.groupLabel))
            {
                path.segments.Add(new PungentAuthoringBindingPathSegment
                {
                    kind = PungentAuthoringBindingPathSegmentKind.Component,
                    key = endpoint.groupLabel,
                    displayName = endpoint.groupLabel,
                    typeName = endpoint.adapterDisplayName,
                    order = 1
                });
            }
            path.segments.Add(new PungentAuthoringBindingPathSegment
            {
                kind = PungentAuthoringBindingPathSegmentKind.Field,
                key = endpoint.id,
                displayName = string.IsNullOrWhiteSpace(endpoint.label) ? endpoint.id : endpoint.label,
                typeName = endpoint.endpointKind,
                valueType = valueType,
                propertyPath = endpoint.target == null ? string.Empty : endpoint.target.propertyPath,
                target = CloneTarget(endpoint.target),
                order = path.segments.Count
            });

            path.NormalizeInPlace();
            return path;
        }

        public static PungentAuthoringBindingSlot CreateSlot(
            PungentAuthoringBindingSlotRole role,
            string interfaceId,
            string itemId,
            string elementId,
            string fieldKey,
            string displayName,
            PungentAuthoringBindingPath path)
        {
            PungentAuthoringBindingSlot slot = new PungentAuthoringBindingSlot
            {
                id = StableId("slot", role.ToString(), interfaceId, itemId, elementId, fieldKey),
                role = role,
                interfaceId = interfaceId ?? string.Empty,
                itemId = itemId ?? string.Empty,
                elementId = elementId ?? string.Empty,
                fieldKey = fieldKey ?? string.Empty,
                displayName = displayName ?? string.Empty,
                valueType = path == null ? PungentAuthoringBindingValueType.Unknown : path.valueType,
                pathId = path == null ? string.Empty : path.id
            };
            slot.NormalizeInPlace();
            return slot;
        }

        public static PungentAuthoringBindingValueType ToRuntimeValueType(PungentAuthoringBindingEndpoint endpoint)
        {
            if (endpoint == null)
                return PungentAuthoringBindingValueType.Unknown;

            if (endpoint.target != null &&
                endpoint.target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath &&
                TryResolveSerializedProperty(endpoint.target, out SerializedProperty property))
            {
                PungentAuthoringBindingValueType propertyType = PungentAuthoringBindingValueCodec.RuntimeValueTypeForSerializedProperty(property);
                if (propertyType != PungentAuthoringBindingValueType.Unknown)
                    return propertyType;
            }

            return PungentAuthoringBindingBridgeService.ToRuntimeValueType(endpoint.valueKind);
        }

        public static PungentAuthoringBindingValueType RuntimeValueTypeForUnityObject(UnityEngine.Object targetObject)
        {
            if (targetObject == null)
                return PungentAuthoringBindingValueType.Unknown;
            if (targetObject is Sprite)
                return PungentAuthoringBindingValueType.Sprite;
            if (targetObject is AudioClip)
                return PungentAuthoringBindingValueType.AudioClip;
            if (targetObject is Component)
                return PungentAuthoringBindingValueType.ComponentReference;
            if (targetObject is GameObject && string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(targetObject)))
                return PungentAuthoringBindingValueType.SceneObjectReference;
            if (!string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(targetObject)))
                return PungentAuthoringBindingValueType.AssetReference;
            return PungentAuthoringBindingValueType.UnityObject;
        }

        public static string StableId(params string[] parts)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int p = 0; p < parts.Length; p++)
                {
                    string value = parts[p] ?? string.Empty;
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }

                    hash ^= 31;
                    hash *= 16777619;
                }

                return "binding-" + hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        private static PungentAuthoringTarget CreateRootTarget(UnityEngine.Object targetObject, string sourceContext)
        {
            if (targetObject == null)
                return new PungentAuthoringTarget();

            string assetPath = AssetDatabase.GetAssetPath(targetObject);
            PungentAuthoringTarget target;
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                target = PungentAuthoringTarget.Create(
                    PungentAuthoringTargetKind.AssetGuid,
                    AssetDatabase.AssetPathToGUID(assetPath),
                    targetObject.name,
                    "authoring-binding-discovery");
            }
            else
            {
                target = PungentAuthoringTarget.Create(
                    targetObject is Component ? PungentAuthoringTargetKind.ComponentInstanceId : PungentAuthoringTargetKind.SceneObjectGlobalId,
                    GlobalObjectId.GetGlobalObjectIdSlow(targetObject).ToString(),
                    targetObject.name,
                    "authoring-binding-discovery");
            }

            target.sourceContext = sourceContext ?? string.Empty;
            target.NormalizeInPlace();
            return target;
        }

        private static bool TryResolveSerializedProperty(PungentAuthoringTarget target, out SerializedProperty property)
        {
            property = null;
            UnityEngine.Object targetObject;
            SerializedObject serializedObject;
            string error;
            return PungentAuthoringBindingApplicationService.TryResolveSerializedProperty(target, out targetObject, out serializedObject, out property, out error);
        }

        private static PungentAuthoringBindingPathSegmentKind SegmentKindForRoot(PungentAuthoringTarget target)
        {
            if (target == null)
                return PungentAuthoringBindingPathSegmentKind.Unknown;
            if (target.targetKind == PungentAuthoringTargetKind.AssetGuid)
                return PungentAuthoringBindingPathSegmentKind.Asset;
            if (target.targetKind == PungentAuthoringTargetKind.ComponentInstanceId)
                return PungentAuthoringBindingPathSegmentKind.Component;
            if (target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath)
                return PungentAuthoringBindingPathSegmentKind.Property;
            if (target.targetKind == PungentAuthoringTargetKind.SceneObjectGlobalId)
                return PungentAuthoringBindingPathSegmentKind.SceneObject;
            return PungentAuthoringBindingPathSegmentKind.RootTarget;
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

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            return string.Empty;
        }
    }

    public static class PungentAuthoringBindingDraftBuilder
    {
        public static PungentAuthoringBindingDraft BuildForTargets(IEnumerable<UnityEngine.Object> targets, string displayName = null, string sourceContext = null)
        {
            PungentAuthoringBindingDraft draft = new PungentAuthoringBindingDraft
            {
                id = PungentAuthoringBindingDiscoveryService.StableId("draft", displayName, sourceContext, DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Binding Draft" : displayName.Trim(),
                sourceContext = sourceContext ?? string.Empty
            };

            HashSet<int> seenObjects = new HashSet<int>();
            Dictionary<string, PungentAuthoringBindingDraftColumn> columns = new Dictionary<string, PungentAuthoringBindingDraftColumn>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object target in targets ?? new UnityEngine.Object[0])
            {
                if (target == null || !seenObjects.Add(target.GetInstanceID()))
                    continue;

                PungentAuthoringBindingDiscoverySnapshot snapshot = PungentAuthoringBindingDiscoveryService.BuildSnapshot(target, sourceContext);
                draft.snapshots.Add(snapshot);
                draft.rows.Add(new PungentAuthoringBindingDraftRow
                {
                    id = snapshot.id,
                    displayName = target.name,
                    target = snapshot.rootTarget,
                    include = true,
                    pullEnabled = true,
                    pushEnabled = true
                });

                foreach (PungentAuthoringBindingPath path in snapshot.paths ?? new List<PungentAuthoringBindingPath>())
                {
                    if (path == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(path.propertyPath))
                        continue;

                    string key = DraftColumnKey(path);
                    if (string.IsNullOrWhiteSpace(key) || columns.ContainsKey(key))
                        continue;

                    columns.Add(key, new PungentAuthoringBindingDraftColumn
                    {
                        id = path.id,
                        displayName = string.IsNullOrWhiteSpace(path.displayName) ? key : path.displayName,
                        path = path,
                        valueType = path.valueType,
                        include = true,
                        pullEnabled = true,
                        pushEnabled = true
                    });
                }
            }

            draft.columns.AddRange(columns.Values.OrderBy(column => column.displayName, StringComparer.OrdinalIgnoreCase));
            draft.NormalizeInPlace();
            return draft;
        }

        private static string DraftColumnKey(PungentAuthoringBindingPath path)
        {
            if (path == null)
                return string.Empty;

            string group = string.Empty;
            foreach (PungentAuthoringBindingPathSegment segment in path.segments ?? new List<PungentAuthoringBindingPathSegment>())
            {
                if (segment == null || segment.kind != PungentAuthoringBindingPathSegmentKind.Component)
                    continue;

                group = segment.displayName ?? string.Empty;
                int slash = group.LastIndexOf(" / ", StringComparison.Ordinal);
                if (slash >= 0)
                    group = group.Substring(slash + 3);
                break;
            }

            return string.Join("|", new[]
            {
                path.adapterId ?? string.Empty,
                group,
                path.propertyPath ?? string.Empty,
                path.valueType.ToString()
            });
        }
    }

    public static class PungentAuthoringBindingPreviewApplyFacade
    {
        public static PungentAuthoringBindingLink CreateLink(
            PungentAuthoringBindingPath path,
            PungentAuthoringBindingSlot slot,
            PungentAuthoringBindingLinkDirection direction = PungentAuthoringBindingLinkDirection.TwoWay)
        {
            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromTarget(
                path == null ? null : path.rootTarget,
                slot == null ? string.Empty : slot.interfaceId,
                slot == null ? string.Empty : slot.itemId,
                slot == null ? string.Empty : slot.elementId,
                slot == null ? string.Empty : slot.fieldKey,
                direction,
                path == null ? string.Empty : path.adapterId,
                path == null ? string.Empty : path.adapterDisplayName,
                path == null ? string.Empty : path.endpointId,
                path == null ? string.Empty : path.displayName,
                path == null ? PungentAuthoringBindingValueType.Unknown : path.valueType);

            if (path != null)
                link.bindingPath = path;
            if (slot != null)
                link.bindingSlot = slot;
            link.NormalizeInPlace();
            return link;
        }

        public static PungentAuthoringBindingLinkPreview PreviewPath(PungentAuthoringBindingPath path, PungentAuthoringBindingSlot slot = null)
        {
            return PungentAuthoringBindingBridgeService.PreviewLink(CreateLink(path, slot));
        }

        public static PungentAuthoringBindingApplyResult ApplyPath(PungentAuthoringBindingPath path, string value, PungentAuthoringBindingSlot slot = null, string undoName = null)
        {
            return PungentAuthoringBindingBridgeService.ApplyLink(CreateLink(path, slot), value, undoName);
        }

        public static List<PungentAuthoringBindingLinkPreview> PreviewLinks(IEnumerable<PungentAuthoringBindingLink> links)
        {
            List<PungentAuthoringBindingLinkPreview> previews = new List<PungentAuthoringBindingLinkPreview>();
            foreach (PungentAuthoringBindingLink link in links ?? new List<PungentAuthoringBindingLink>())
                previews.Add(PungentAuthoringBindingBridgeService.PreviewLink(link));
            return previews;
        }

        public static List<PungentAuthoringBindingApplyResult> ApplyRequests(IEnumerable<PungentAuthoringBindingApplyRequest> requests)
        {
            List<PungentAuthoringBindingApplyResult> results = new List<PungentAuthoringBindingApplyResult>();
            foreach (PungentAuthoringBindingApplyRequest request in requests ?? new List<PungentAuthoringBindingApplyRequest>())
            {
                if (request == null)
                    continue;
                results.Add(PungentAuthoringBindingBridgeService.ApplyLink(request.link, request.value, request.undoName));
            }

            return results;
        }
    }

    public static class PungentAuthoringBindingAdapterRegistry
    {
        private static readonly List<IPungentAuthoringBindingAdapter> Adapters = new List<IPungentAuthoringBindingAdapter>();

        public static IReadOnlyList<IPungentAuthoringBindingAdapter> RegisteredAdapters =>
            Adapters.Where(adapter => adapter != null).ToList();

        public static IReadOnlyList<IPungentAuthoringBindingPreviewAdapter> PreviewAdapters
        {
            get
            {
                EnsureBuiltInsRegistered();
                return Adapters.OfType<IPungentAuthoringBindingPreviewAdapter>().ToList();
            }
        }

        public static IReadOnlyList<IPungentAuthoringBindingApplyAdapter> ApplyAdapters
        {
            get
            {
                EnsureBuiltInsRegistered();
                return Adapters.OfType<IPungentAuthoringBindingApplyAdapter>().ToList();
            }
        }

        public static void Register(IPungentAuthoringBindingAdapter adapter)
        {
            if (adapter == null || string.IsNullOrWhiteSpace(adapter.Id))
                return;

            Adapters.RemoveAll(existing => existing == null || string.Equals(existing.Id, adapter.Id, StringComparison.OrdinalIgnoreCase));
            Adapters.Add(adapter);
        }

        public static List<PungentAuthoringBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject)
        {
            if (targetObject == null)
                return new List<PungentAuthoringBindingEndpoint>();

            EnsureBuiltInsRegistered();
            return Adapters
                .Where(adapter => adapter != null)
                .SelectMany(adapter => SafeEndpoints(adapter, targetObject))
                .Where(endpoint => endpoint != null && endpoint.HasTarget)
                .GroupBy(endpoint => endpoint.id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(endpoint => endpoint.adapterDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(endpoint => endpoint.label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void EnsureBuiltInsRegistered()
        {
            if (!Adapters.Any(adapter => adapter is PungentSerializedPropertyAuthoringBindingAdapter))
                Register(new PungentSerializedPropertyAuthoringBindingAdapter());
        }

        private static IEnumerable<PungentAuthoringBindingEndpoint> SafeEndpoints(IPungentAuthoringBindingAdapter adapter, UnityEngine.Object targetObject)
        {
            try
            {
                return (adapter.GetEndpoints(targetObject) ?? Enumerable.Empty<PungentAuthoringBindingEndpoint>()).ToList();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Authoring binding adapter '" + adapter.Id + "' failed: " + exception.Message);
                return Enumerable.Empty<PungentAuthoringBindingEndpoint>();
            }
        }
    }

    [InitializeOnLoad]
    internal static class PungentAuthoringBindingBootstrap
    {
        static PungentAuthoringBindingBootstrap()
        {
            PungentAuthoringBindingAdapterRegistry.EnsureBuiltInsRegistered();
        }
    }

    public sealed class PungentSerializedPropertyAuthoringBindingAdapter : IPungentAuthoringBindingApplyAdapter
    {
        public const string AdapterId = "authoring-serialized-property";

        public string Id => AdapterId;
        public string DisplayName => "Serialized Property";

        public IEnumerable<PungentAuthoringBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject)
        {
            foreach (UnityEngine.Object owner in EnumerateExplicitTargets(targetObject))
            {
                if (owner == null)
                    continue;

                SerializedObject serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyPath == "m_Script")
                        continue;

                    PungentAuthoringBindingValueKind kind = ValueKindFor(property);
                    if (kind == PungentAuthoringBindingValueKind.Unknown)
                        continue;

                    string label = BuildLabel(owner, property);
                    PungentAuthoringTarget target = CreateSerializedPropertyTarget(owner, property.propertyPath, label);
                    yield return new PungentAuthoringBindingEndpoint
                    {
                        id = AdapterId + ":" + target.contextId + ":" + target.propertyPath,
                        adapterId = Id,
                        adapterDisplayName = DisplayName,
                        label = label,
                        groupLabel = BuildGroupLabel(owner),
                        endpointKind = property.propertyType.ToString(),
                        tooltip = "Explicit object/component/property binding. No project-wide scan is performed.",
                        helpText = "Use this endpoint when you want a document, sheet, or board value to target this exact serialized property.",
                        valueKind = kind,
                        target = target,
                        canRead = true,
                        canApply = CanApplyKind(kind)
                    };
                }
            }
        }

        public bool TryPreview(PungentAuthoringTarget target, out PungentAuthoringBindingPreview preview)
        {
            preview = new PungentAuthoringBindingPreview
            {
                adapterId = Id,
                adapterDisplayName = DisplayName,
                targetLabel = target == null ? string.Empty : target.label ?? string.Empty
            };

            if (!OwnsTarget(target))
            {
                preview.disabledReason = "Target is not a serialized-property binding.";
                return false;
            }

            UnityEngine.Object targetObject;
            SerializedObject serializedObject;
            SerializedProperty property;
            string error;
            if (!PungentAuthoringBindingApplicationService.TryResolveSerializedProperty(target, out targetObject, out serializedObject, out property, out error))
            {
                preview.disabledReason = error;
                return false;
            }

            preview.valueKind = ValueKindFor(property);
            preview.currentValue = ReadSerializedProperty(property);
            preview.canRead = preview.valueKind != PungentAuthoringBindingValueKind.Unknown;
            preview.canApply = CanApplyKind(preview.valueKind);
            preview.warning = targetObject == null ? string.Empty : "Target: " + targetObject.name;
            return preview.canRead;
        }

        public bool CanApply(PungentAuthoringTarget target, out string disabledReason)
        {
            disabledReason = string.Empty;
            if (!OwnsTarget(target))
            {
                disabledReason = "Target is not a serialized-property binding.";
                return false;
            }

            UnityEngine.Object targetObject;
            SerializedObject serializedObject;
            SerializedProperty property;
            if (!PungentAuthoringBindingApplicationService.TryResolveSerializedProperty(target, out targetObject, out serializedObject, out property, out disabledReason))
                return false;

            PungentAuthoringBindingValueKind kind = ValueKindFor(property);
            if (!CanApplyKind(kind))
            {
                disabledReason = "This serialized property type is not supported by the generic binding service.";
                return false;
            }

            return true;
        }

        public PungentAuthoringBindingApplyResult Apply(PungentAuthoringTarget target, string value, string undoName = null)
        {
            PungentAuthoringBindingApplyResult result = new PungentAuthoringBindingApplyResult
            {
                adapterId = Id,
                adapterDisplayName = DisplayName,
                targetLabel = target == null ? string.Empty : target.label ?? string.Empty
            };

            string disabledReason;
            if (!CanApply(target, out disabledReason))
            {
                result.message = disabledReason;
                return result;
            }

            UnityEngine.Object targetObject;
            SerializedObject serializedObject;
            SerializedProperty property;
            string error;
            if (!PungentAuthoringBindingApplicationService.TryResolveSerializedProperty(target, out targetObject, out serializedObject, out property, out error))
            {
                result.message = error;
                return result;
            }

            Undo.RecordObject(targetObject, string.IsNullOrWhiteSpace(undoName) ? "Apply Authoring Binding" : undoName);
            string applyError;
            if (!TryWriteSerializedProperty(property, value, out applyError))
            {
                result.message = applyError;
                return result;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);

            result.applied = true;
            result.message = "Applied to " + (string.IsNullOrWhiteSpace(target.label) ? targetObject.name : target.label) + ".";
            return result;
        }

        private static IEnumerable<UnityEngine.Object> EnumerateExplicitTargets(UnityEngine.Object targetObject)
        {
            if (targetObject == null)
                yield break;

            GameObject gameObject = targetObject as GameObject;
            if (gameObject != null)
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                    if (component != null)
                        yield return component;
                yield break;
            }

            yield return targetObject;
        }

        private static PungentAuthoringBindingValueKind ValueKindFor(SerializedProperty property)
        {
            return PungentAuthoringBindingValueCodec.ToEditorValueKind(PungentAuthoringBindingValueCodec.RuntimeValueTypeForSerializedProperty(property));
        }

        private static bool CanApplyKind(PungentAuthoringBindingValueKind kind)
        {
            return kind == PungentAuthoringBindingValueKind.Text ||
                   kind == PungentAuthoringBindingValueKind.Number ||
                   kind == PungentAuthoringBindingValueKind.Boolean ||
                   kind == PungentAuthoringBindingValueKind.Enum ||
                   kind == PungentAuthoringBindingValueKind.ObjectReference ||
                   kind == PungentAuthoringBindingValueKind.UnityObject ||
                   kind == PungentAuthoringBindingValueKind.AssetReference ||
                   kind == PungentAuthoringBindingValueKind.SceneObjectReference ||
                   kind == PungentAuthoringBindingValueKind.ComponentReference ||
                   kind == PungentAuthoringBindingValueKind.Sprite ||
                   kind == PungentAuthoringBindingValueKind.AudioClip ||
                   kind == PungentAuthoringBindingValueKind.Vector2 ||
                   kind == PungentAuthoringBindingValueKind.Vector3 ||
                   kind == PungentAuthoringBindingValueKind.Color;
        }

        private static string ReadSerializedProperty(SerializedProperty property)
        {
            return PungentAuthoringBindingValueCodec.FormatSerializedProperty(property);
        }

        private static string ObjectReferenceLabel(UnityEngine.Object target)
        {
            if (target == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(target);
            string guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrWhiteSpace(guid))
                return target.name + " [" + guid + "]";

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            string globalIdText = globalId.ToString();
            return string.IsNullOrWhiteSpace(globalIdText)
                ? target.name
                : target.name + " [" + globalIdText + "]";
        }

        private static bool TryWriteSerializedProperty(SerializedProperty property, string value, out string error)
        {
            return PungentAuthoringBindingValueCodec.TryWriteSerializedProperty(property, value, out error);
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            if (bool.TryParse(value, out result))
                return true;

            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }

        private static bool TryResolveObjectReferenceValue(string value, out UnityEngine.Object objectReference, out string error)
        {
            objectReference = null;
            error = string.Empty;

            string text = value == null ? string.Empty : value.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string bracketValue = ExtractBracketValue(text);
            if (!string.IsNullOrWhiteSpace(bracketValue))
                text = bracketValue;

            GlobalObjectId globalId;
            if (GlobalObjectId.TryParse(text, out globalId))
            {
                objectReference = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (objectReference != null)
                    return true;
            }

            string path = text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? text
                : AssetDatabase.GUIDToAssetPath(text);
            if (!string.IsNullOrWhiteSpace(path))
            {
                objectReference = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (objectReference != null)
                    return true;
            }

            error = "Object reference values must be empty, an asset path, an asset GUID, or a GlobalObjectId.";
            return false;
        }

        private static string ExtractBracketValue(string value)
        {
            int open = string.IsNullOrEmpty(value) ? -1 : value.LastIndexOf('[');
            int close = string.IsNullOrEmpty(value) ? -1 : value.LastIndexOf(']');
            if (open < 0 || close <= open)
                return string.Empty;

            return value.Substring(open + 1, close - open - 1).Trim();
        }

        private static bool OwnsTarget(PungentAuthoringTarget target)
        {
            return target != null &&
                   target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath &&
                   (string.IsNullOrWhiteSpace(target.customKind) || string.Equals(target.customKind, AdapterId, StringComparison.OrdinalIgnoreCase));
        }

        private static PungentAuthoringTarget CreateSerializedPropertyTarget(UnityEngine.Object unityObject, string propertyPath, string label)
        {
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            PungentAuthoringTarget target = PungentAuthoringTarget.Create(
                PungentAuthoringTargetKind.SerializedPropertyPath,
                propertyPath,
                label,
                AdapterId);
            target.contextId = globalId.ToString();
            target.propertyPath = propertyPath;
            target.sourceContext = "GuidedAuthoringBinding";
            target.customKind = AdapterId;
            target.NormalizeInPlace();
            return target;
        }

        private static string BuildLabel(UnityEngine.Object owner, SerializedProperty property)
        {
            string groupLabel = BuildGroupLabel(owner);
            string propertyName = ObjectNames.NicifyVariableName((property.displayName ?? property.propertyPath).Replace("m_", string.Empty));
            return groupLabel + " / " + propertyName;
        }

        private static string BuildGroupLabel(UnityEngine.Object owner)
        {
            string ownerName = owner == null ? "Target" : owner.name;
            string typeName = owner == null ? "Object" : ObjectNames.NicifyVariableName(owner.GetType().Name);
            Component component = owner as Component;
            if (component != null && component.gameObject != null)
            {
                Component[] siblings = component.gameObject.GetComponents(component.GetType());
                if (siblings != null && siblings.Length > 1)
                {
                    int index = Array.IndexOf(siblings, component);
                    if (index >= 0)
                        typeName += " #" + (index + 1).ToString(CultureInfo.InvariantCulture);
                }
            }
            return ownerName + " / " + typeName;
        }
    }

    public static class PungentAuthoringBindingApplicationService
    {
        public static PungentAuthoringBindingPreview Preview(PungentAuthoringTarget target)
        {
            PungentAuthoringBindingPreview fallback = new PungentAuthoringBindingPreview
            {
                targetLabel = target == null ? string.Empty : target.label ?? string.Empty
            };

            if (target == null || !target.HasTarget)
            {
                fallback.disabledReason = "No project binding target is set.";
                return fallback;
            }

            foreach (IPungentAuthoringBindingPreviewAdapter adapter in PungentAuthoringBindingAdapterRegistry.PreviewAdapters)
            {
                if (adapter == null)
                    continue;

                try
                {
                    PungentAuthoringBindingPreview preview;
                    if (adapter.TryPreview(target, out preview) && preview != null)
                    {
                        if (string.IsNullOrWhiteSpace(preview.adapterId))
                            preview.adapterId = adapter.Id;
                        if (string.IsNullOrWhiteSpace(preview.adapterDisplayName))
                            preview.adapterDisplayName = adapter.DisplayName;
                        if (string.IsNullOrWhiteSpace(preview.targetLabel))
                            preview.targetLabel = target.label ?? string.Empty;
                        return preview;
                    }
                }
                catch (Exception exception)
                {
                    fallback.disabledReason = "Binding preview adapter '" + adapter.Id + "' failed: " + exception.Message;
                    return fallback;
                }
            }

            fallback.disabledReason = "No installed binding adapter can preview this target.";
            return fallback;
        }

        public static bool CanApply(PungentAuthoringTarget target, out string disabledReason)
        {
            disabledReason = string.Empty;
            if (target == null || !target.HasTarget)
            {
                disabledReason = "No project binding target is set.";
                return false;
            }

            foreach (IPungentAuthoringBindingApplyAdapter adapter in PungentAuthoringBindingAdapterRegistry.ApplyAdapters)
            {
                if (adapter == null)
                    continue;

                try
                {
                    if (adapter.CanApply(target, out disabledReason))
                        return true;
                }
                catch (Exception exception)
                {
                    disabledReason = "Binding apply adapter '" + adapter.Id + "' failed: " + exception.Message;
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(disabledReason))
                disabledReason = "No installed binding adapter can apply this target.";
            return false;
        }

        public static PungentAuthoringBindingApplyResult Apply(PungentAuthoringTarget target, string value, string undoName = null)
        {
            PungentAuthoringBindingApplyResult fallback = new PungentAuthoringBindingApplyResult
            {
                targetLabel = target == null ? string.Empty : target.label ?? string.Empty
            };

            if (target == null || !target.HasTarget)
            {
                fallback.message = "No project binding target is set.";
                return fallback;
            }

            foreach (IPungentAuthoringBindingApplyAdapter adapter in PungentAuthoringBindingAdapterRegistry.ApplyAdapters)
            {
                if (adapter == null)
                    continue;

                try
                {
                    string disabledReason;
                    if (!adapter.CanApply(target, out disabledReason))
                        continue;

                    PungentAuthoringBindingApplyResult result = adapter.Apply(target, value, undoName);
                    if (result != null)
                        return result;
                }
                catch (Exception exception)
                {
                    fallback.adapterId = adapter.Id;
                    fallback.adapterDisplayName = adapter.DisplayName;
                    fallback.message = "Binding apply adapter failed: " + exception.Message;
                    return fallback;
                }
            }

            fallback.message = "No installed binding adapter can apply this target.";
            return fallback;
        }

        public static bool TryResolveUnityTarget(PungentAuthoringTarget target, out UnityEngine.Object targetObject, out string error)
        {
            targetObject = null;
            error = string.Empty;
            if (target == null)
            {
                error = "Target is missing.";
                return false;
            }

            if (!PungentAuthoringProviderRegistry.TryResolveTarget(target, out targetObject, out error) || targetObject == null)
            {
                error = string.IsNullOrWhiteSpace(error) ? "Bound target could not be resolved." : error;
                return false;
            }

            return true;
        }

        public static bool TryResolveSerializedProperty(
            PungentAuthoringTarget target,
            out UnityEngine.Object targetObject,
            out SerializedObject serializedObject,
            out SerializedProperty property,
            out string error)
        {
            targetObject = null;
            serializedObject = null;
            property = null;
            error = string.Empty;

            if (target == null || target.targetKind != PungentAuthoringTargetKind.SerializedPropertyPath || string.IsNullOrWhiteSpace(target.propertyPath))
            {
                error = "Bound target is missing a serialized property path.";
                return false;
            }

            if (!TryResolveUnityTarget(target, out targetObject, out error))
                return false;

            try
            {
                serializedObject = new SerializedObject(targetObject);
                property = serializedObject.FindProperty(target.propertyPath);
            }
            catch (Exception exception)
            {
                error = "Could not inspect bound target: " + exception.Message;
                return false;
            }

            if (property == null)
            {
                error = "Bound property could not be found: " + target.propertyPath;
                return false;
            }

            return true;
        }
    }
#endif
}
