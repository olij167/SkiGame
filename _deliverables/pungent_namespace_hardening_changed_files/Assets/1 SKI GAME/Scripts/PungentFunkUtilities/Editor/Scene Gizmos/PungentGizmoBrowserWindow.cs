using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentGizmoBrowserWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.SceneGizmoBrowser.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefIncludeInactive = PrefPrefix + "IncludeInactive";

        private readonly List<PungentSceneGizmoSource> _cachedSources = new List<PungentSceneGizmoSource>();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _includeInactive = true;
        private string _status = "Ready.";
        private bool _cacheDirty = true;

        [MenuItem("Tools/Utilities/Scene/Scene Gizmo Browser", priority = 940)]
        public static void Open()
        {
            PungentGizmoBrowserWindow window = GetWindow<PungentGizmoBrowserWindow>("Scene Gizmos");
            window.minSize = new Vector2(440f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _includeInactive = UtilityWindowPrefs.GetBool(PrefIncludeInactive, true);
            EditorApplication.hierarchyChanged += MarkCacheDirty;
            RebuildCache();
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetBool(PrefIncludeInactive, _includeInactive);
            EditorApplication.hierarchyChanged -= MarkCacheDirty;
        }

        private void OnGUI()
        {
            if (_cacheDirty)
                RebuildCache();

            UtilityWindowTheme.Header("Scene Gizmo Browser", "Find and manage generic PungentSceneGizmoSource components in the current scene.", _status);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search", GUILayout.Width(52f));
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    _includeInactive = GUILayout.Toggle(_includeInactive, "Inactive", EditorStyles.toolbarButton, GUILayout.Width(78f));
                    if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64f)))
                        RebuildCache();
                }

                if (Selection.activeGameObject != null && GUILayout.Button("Add Gizmo Source To Selection"))
                {
                    int added = 0;
                    foreach (GameObject go in Selection.gameObjects)
                    {
                        if (go != null && go.GetComponent<PungentSceneGizmoSource>() == null)
                        {
                            Undo.AddComponent<PungentSceneGizmoSource>(go);
                            added++;
                        }
                    }

                    _status = added == 0 ? "Selection already has gizmo sources." : $"Added {added} gizmo source{(added == 1 ? string.Empty : "s")}.";
                    RebuildCache();
                }
            }

            List<PungentSceneGizmoSource> visibleSources = FilterSources();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Sources", UtilityWindowTheme.Teal, $"{visibleSources.Count}/{_cachedSources.Count} shown");

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                for (int i = 0; i < visibleSources.Count; i++)
                    DrawSource(visibleSources[i]);
                EditorGUILayout.EndScrollView();
            }
        }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;
            Repaint();
        }

        private void RebuildCache()
        {
            _cachedSources.Clear();
            PungentSceneGizmoSource[] sources = Resources.FindObjectsOfTypeAll<PungentSceneGizmoSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                PungentSceneGizmoSource source = sources[i];
                if (source == null || source.gameObject == null || EditorUtility.IsPersistent(source.gameObject))
                    continue;

                _cachedSources.Add(source);
            }

            _cachedSources.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase));
            _cacheDirty = false;
            _status = $"Cached {_cachedSources.Count} gizmo source{(_cachedSources.Count == 1 ? string.Empty : "s")}.";
        }

        private List<PungentSceneGizmoSource> FilterSources()
        {
            string q = string.IsNullOrWhiteSpace(_search) ? string.Empty : _search.Trim();
            List<PungentSceneGizmoSource> results = new List<PungentSceneGizmoSource>();

            for (int i = 0; i < _cachedSources.Count; i++)
            {
                PungentSceneGizmoSource source = _cachedSources[i];
                if (source == null || source.gameObject == null)
                    continue;

                if (!_includeInactive && !source.gameObject.activeInHierarchy)
                    continue;

                if (!string.IsNullOrEmpty(q) && source.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                results.Add(source);
            }

            return results;
        }

        private void DrawSource(PungentSceneGizmoSource source)
        {
            if (source == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(source.drawInScene ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, 0.14f, 0.07f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(source, typeof(PungentSceneGizmoSource), true);
                    if (GUILayout.Button(source.drawInScene ? "Hide" : "Show", GUILayout.Width(52f)))
                    {
                        Undo.RecordObject(source, "Toggle Scene Gizmo Source");
                        source.drawInScene = !source.drawInScene;
                        EditorUtility.SetDirty(source);
                        RepaintActiveSceneView();
                    }
                    if (GUILayout.Button("Select", GUILayout.Width(56f)))
                        Selection.activeObject = source.gameObject;
                }

                int totalRules = source.rules != null ? source.rules.Count : 0;
                int enabledRules = source.rules != null ? source.rules.Count(r => r != null && r.enabled) : 0;
                EditorGUILayout.LabelField($"Rules: {enabledRules}/{totalRules}", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private static void RepaintActiveSceneView()
        {
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Repaint();
            else
                SceneView.RepaintAll();
        }
    }
    #endif

}