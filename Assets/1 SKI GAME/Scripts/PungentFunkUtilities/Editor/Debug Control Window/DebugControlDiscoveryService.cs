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
        // Explicit scene/static debug discovery and scan-cache invalidation logic.

        private void Refresh()
        {
            _components.Clear();
            _staticBoolToggles.Clear();

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                GameObject go = behaviour.gameObject;
                if (go == null)
                    continue;

                if (EditorUtility.IsPersistent(go))
                    continue;

                if (!_includeInactive && !go.activeInHierarchy)
                    continue;

                Type type = behaviour.GetType();
                var info = new DebugComponentInfo
                {
                    component = behaviour,
                    objectPath = GetTransformPath(go.transform)
                };

                info.boolToggles.AddRange(GetDebugBoolFields(type));
                info.contextActions.AddRange(GetContextMenuMethods(type));

                if (!_showOnlyDebuggable || info.boolToggles.Count > 0 || info.contextActions.Count > 0)
                    _components.Add(info);
            }

            ScanStaticDebugFields();
            ResolveScheduledReferences();

            _lastRefreshTime = EditorApplication.timeSinceStartup;
            _scanCacheDirty = false;
            _status = $"Found {_components.Count} components, {_staticBoolToggles.Count} static toggles.";

            RequestRepaintThrottled();
        }

        private bool PassesFilters(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return false;

            if (!_includeInactive && !info.component.gameObject.activeInHierarchy)
                return false;

            if (_selectedHierarchyOnly && !IsUnderCurrentSelection(info.component.gameObject))
                return false;

            if (_showOnlyDebuggable && info.boolToggles.Count == 0 && info.contextActions.Count == 0)
                return false;

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string s = _search.Trim();
            return info.component.GetType().Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                   || info.objectPath.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                   || info.boolToggles.Any(f => f.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 || ClassifyBoolField(f).ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                   || info.contextActions.Any(m => GetContextMenuName(m).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ScanStaticDebugFields()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!IsProjectRuntimeAssembly(assembly))
                    continue;

                foreach (Type type in GetTypesSafely(assembly))
                {
                    if (type == null || type.IsGenericTypeDefinition)
                        continue;

                    foreach (FieldInfo field in type.GetFields(StaticFlags))
                    {
                        if (field.FieldType != typeof(bool))
                            continue;

                        if (!LooksLikeStaticDebugToggle(type, field))
                            continue;

                        _staticBoolToggles.Add(new StaticDebugFieldInfo { declaringType = type, field = field });
                    }
                }
            }

            _staticBoolToggles.Sort((a, b) => string.Compare($"{a.declaringType.Name}.{a.field.Name}", $"{b.declaringType.Name}.{b.field.Name}", StringComparison.OrdinalIgnoreCase));
        }

    }
#endif
}
