using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Debugging;

namespace PungentFunk.Utilities.Editor.Debugging
{
#if UNITY_EDITOR
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public sealed partial class DebugControlWindow
    {
        // Reflection helpers and component snapshot formatting utilities.

        private static MethodInfo FindMethod(Type type, string methodName)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
                return null;

            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo method = t.GetMethod(methodName, InstanceFlags);
                if (method != null)
                    return method;
            }

            return null;
        }

        private static List<FieldInfo> GetDebugBoolFields(Type type)
        {
            var fields = new List<FieldInfo>();
            foreach (FieldInfo field in EnumerateFields(type, includeStatic: false))
            {
                if (field.FieldType != typeof(bool))
                    continue;

                if (!IsUnitySerializedField(field))
                    continue;

                if (LooksLikeDebugToggle(field))
                    fields.Add(field);
            }

            return fields;
        }

        private static List<MethodInfo> GetContextMenuMethods(Type type)
        {
            var methods = new List<MethodInfo>();
            foreach (MethodInfo method in EnumerateMethods(type, includeStatic: false))
            {
                if (method.GetParameters().Length != 0)
                    continue;

                if (method.GetCustomAttributes(typeof(ContextMenu), true).Length > 0)
                    methods.Add(method);
            }

            return methods;
        }

        private static bool IsProjectRuntimeAssembly(Assembly assembly)
        {
            string name = assembly != null ? assembly.GetName().Name : string.Empty;
            return string.Equals(name, "Assembly-CSharp", StringComparison.Ordinal)
                   || string.Equals(name, "Assembly-CSharp-firstpass", StringComparison.Ordinal);
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static IEnumerable<FieldInfo> EnumerateFields(Type type, bool includeStatic)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                BindingFlags flags = includeStatic ? StaticFlags : InstanceFlags;
                foreach (FieldInfo field in t.GetFields(flags | BindingFlags.DeclaredOnly))
                    yield return field;
            }
        }

        private static IEnumerable<MethodInfo> EnumerateMethods(Type type, bool includeStatic)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                BindingFlags flags = includeStatic ? StaticFlags : InstanceFlags;
                foreach (MethodInfo method in t.GetMethods(flags | BindingFlags.DeclaredOnly))
                    yield return method;
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsLiteral || field.IsInitOnly)
                return false;

            if (field.IsNotSerialized)
                return false;

            if (field.IsPublic)
                return true;

            return field.GetCustomAttributes(typeof(SerializeField), true).Length > 0;
        }

        private static bool LooksLikeDebugToggle(FieldInfo field)
        {
            string name = field.Name ?? string.Empty;
            if (ContainsDebugToken(name))
                return true;

            if (name.IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (name.IndexOf("show", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (object attr in field.GetCustomAttributes(true))
            {
                if (attr is HeaderAttribute header && !string.IsNullOrEmpty(header.header) && ContainsDebugToken(header.header))
                    return true;

                if (attr is TooltipAttribute tooltip && !string.IsNullOrEmpty(tooltip.tooltip) && ContainsDebugToken(tooltip.tooltip))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeStaticDebugToggle(Type type, FieldInfo field)
        {
            if (field == null || field.FieldType != typeof(bool))
                return false;

            string full = $"{type.Name}.{field.Name}";
            return ContainsDebugToken(full) || field.Name.Equals("LogEvents", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsDebugToken(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("verbose", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("trace", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static BoolToggleCategory ClassifyBoolField(FieldInfo field)
        {
            if (field == null)
                return BoolToggleCategory.Other;

            string text = BuildFieldSearchText(field);
            if (text.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Gizmo;

            if (text.IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Gizmo;

            if (text.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("verbose", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("trace", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Log;

            if (text.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("probe", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Diagnostic;

            if (text.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Debug;

            return BoolToggleCategory.Other;
        }

        private static string BuildFieldSearchText(FieldInfo field)
        {
            var sb = new StringBuilder(field.Name ?? string.Empty);
            foreach (object attr in field.GetCustomAttributes(true))
            {
                if (attr is HeaderAttribute header && !string.IsNullOrEmpty(header.header))
                    sb.Append(' ').Append(header.header);
                else if (attr is TooltipAttribute tooltip && !string.IsNullOrEmpty(tooltip.tooltip))
                    sb.Append(' ').Append(tooltip.tooltip);
            }
            return sb.ToString();
        }

        private static string GetBoolSortKey(FieldInfo field)
        {
            BoolToggleCategory category = ClassifyBoolField(field);
            int categorySort = category == BoolToggleCategory.Gizmo ? 3 : category == BoolToggleCategory.Log ? 2 : category == BoolToggleCategory.Debug ? 1 : category == BoolToggleCategory.Diagnostic ? 4 : 5;
            return categorySort.ToString("00") + ":" + (field != null ? field.Name : string.Empty);
        }

        private static string GetContextMenuName(MethodInfo method)
        {
            if (method == null)
                return string.Empty;

            object[] attrs = method.GetCustomAttributes(typeof(ContextMenu), true);
            if (attrs.Length > 0 && attrs[0] is ContextMenu menu && !string.IsNullOrEmpty(menu.menuItem))
                return menu.menuItem;

            return ObjectNames.NicifyVariableName(method.Name);
        }

        private void CopySerializedSnapshot(UnityEngine.Object target, bool debugOnly)
        {
            if (target == null)
                return;

            string text = BuildSerializedSnapshot(target, debugOnly);
            EditorGUIUtility.systemCopyBuffer = text;
            _status = debugOnly ? $"Copied debug serialized vars for {target.name}." : $"Copied serialized vars for {target.name}.";
            Debug.Log($"[DebugControlCenter] Copied {(debugOnly ? "debug " : string.Empty)}serialized snapshot for {target.name} to clipboard.", target);
        }

        private void CopyReflectionSnapshot(UnityEngine.Object target)
        {
            if (target == null)
                return;

            string text = BuildReflectionSnapshot(target);
            EditorGUIUtility.systemCopyBuffer = text;
            _status = $"Copied full field snapshot for {target.name}.";
            Debug.Log($"[DebugControlCenter] Copied full field snapshot for {target.name} to clipboard.", target);
        }

        private void CopyObjectSnapshot(GameObject go)
        {
            if (go == null)
                return;

            var sb = new StringBuilder(8192);
            sb.AppendLine($"GameObject snapshot: {go.name}");
            sb.AppendLine($"Path: {GetTransformPath(go.transform)}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"ActiveSelf: {go.activeSelf}");
            sb.AppendLine($"ActiveInHierarchy: {go.activeInHierarchy}");
            sb.AppendLine($"Layer: {LayerMask.LayerToName(go.layer)} ({go.layer})");
            sb.AppendLine($"Tag: {go.tag}");
            sb.AppendLine();

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    sb.AppendLine($"Component[{i}] = <missing script>");
                    continue;
                }

                sb.AppendLine($"--- Component[{i}]: {component.GetType().Name} ---");
                if (component is MonoBehaviour mb)
                    sb.AppendLine(BuildSerializedSnapshot(mb, debugOnly: false));
                else
                    sb.AppendLine(component.ToString());
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            _status = $"Copied GameObject snapshot for {go.name}.";
            Debug.Log($"[DebugControlCenter] Copied GameObject snapshot for {go.name} to clipboard.", go);
        }

        private static string BuildSerializedSnapshot(UnityEngine.Object target, bool debugOnly)
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine($"Serialized snapshot: {target.name} ({target.GetType().Name})");
            if (target is Component component)
                sb.AppendLine($"Path: {GetTransformPath(component.transform)}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(debugOnly ? "Mode: debug/log/gizmo-looking serialized properties only" : "Mode: all visible serialized properties");
            sb.AppendLine();

            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyPath == "m_Script")
                    continue;

                if (debugOnly && !ContainsDebugToken(prop.displayName) && !ContainsDebugToken(prop.name) && !ContainsDebugToken(prop.propertyPath))
                    continue;

                sb.AppendLine($"{prop.propertyPath} = {SerializedPropertyToString(prop)}");
            }

            return sb.ToString();
        }

        private static string BuildReflectionSnapshot(UnityEngine.Object target)
        {
            var sb = new StringBuilder(8192);
            sb.AppendLine($"Full field snapshot: {target.name} ({target.GetType().Name})");
            if (target is Component component)
                sb.AppendLine($"Path: {GetTransformPath(component.transform)}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Mode: instance fields via reflection. Static fields, compiler fields, delegates, and events are skipped.");
            sb.AppendLine();

            foreach (FieldInfo field in EnumerateFields(target.GetType(), includeStatic: false).OrderBy(f => f.MetadataToken))
            {
                if (field.IsStatic || field.IsLiteral)
                    continue;

                if (field.Name.Contains("k__BackingField"))
                    continue;

                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    continue;

                try
                {
                    object value = field.GetValue(target);
                    string prefix = field.IsPublic ? "public" : field.IsFamily ? "protected" : "private";
                    string serialized = IsUnitySerializedField(field) ? "serialized" : "runtime";
                    sb.AppendLine($"{prefix} {serialized} {field.FieldType.Name} {field.Name} = {FormatValue(value)}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"{field.FieldType.Name} {field.Name} = <error: {ex.Message}>");
                }
            }

            return sb.ToString();
        }

        private static string SerializedPropertyToString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("0.#####");
                case SerializedPropertyType.String:
                    return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})" : "null";
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString("F3");
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString("F3");
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString("F3");
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Character:
                    return prop.intValue.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return $"AnimationCurve(keys={prop.animationCurveValue?.keys.Length ?? 0})";
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString("F3");
                case SerializedPropertyType.ExposedReference:
                    return prop.exposedReferenceValue != null ? prop.exposedReferenceValue.name : "null";
                case SerializedPropertyType.FixedBufferSize:
                    return prop.fixedBufferSize.ToString();
                case SerializedPropertyType.Vector2Int:
                    return prop.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return prop.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return prop.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return prop.boundsIntValue.ToString();
                default:
                    if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
                        return $"Array(size={prop.arraySize})";
                    return $"<{prop.propertyType}>";
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string s)
                return $"\"{s}\"";

            if (value is UnityEngine.Object obj)
                return obj != null ? $"{obj.name} ({obj.GetType().Name})" : "null UnityObject";

            if (value is Vector2 v2)
                return v2.ToString("F3");

            if (value is Vector3 v3)
                return v3.ToString("F3");

            if (value is Vector4 v4)
                return v4.ToString("F3");

            if (value is Quaternion q)
                return q.eulerAngles.ToString("F3");

            if (value is Color c)
                return c.ToString();

            if (value is IDictionary dict)
                return $"Dictionary(count={dict.Count})";

            if (value is IEnumerable enumerable && !(value is string))
            {
                var items = new List<string>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count < 8)
                        items.Add(FormatValueBrief(item));
                    count++;
                }
                return $"Enumerable(count={count}, first=[{string.Join(", ", items)}])";
            }

            return value.ToString();
        }

        private static string FormatValueBrief(object value)
        {
            if (value == null)
                return "null";

            if (value is UnityEngine.Object obj)
                return obj != null ? obj.name : "null UnityObject";

            if (value is Vector3 v3)
                return v3.ToString("F2");

            if (value is Vector2 v2)
                return v2.ToString("F2");

            string text = value.ToString();
            if (text.Length > 48)
                text = text.Substring(0, 48) + "…";
            return text;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<no transform>";

            var names = new Stack<string>();
            Transform t = transform;
            while (t != null)
            {
                names.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", names.ToArray());
        }

    }
#endif
}
