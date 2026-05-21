using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public enum PungentTooltipNoteScope
    {
        ComponentType,
        ComponentInstance,
        PrefabAsset,
        SceneObject,
        Asset,
        SerializedProperty
    }

    public enum PungentTooltipNoteVisibility
    {
        BrowserOnly,
        InspectorHeader,
        SelectionCompanion,
        SceneBadgeSelected,
        SceneBadgeAlways
    }

    public enum PungentTooltipNotePriority
    {
        Info,
        Todo,
        Warning,
        Critical
    }

    [Serializable]
    public sealed class PungentTooltipNote
    {
        public string id = Guid.NewGuid().ToString("N");
        public PungentTooltipNoteScope scope = PungentTooltipNoteScope.SerializedProperty;
        public PungentTooltipNoteVisibility visibility = PungentTooltipNoteVisibility.InspectorHeader;
        public PungentTooltipNotePriority priority = PungentTooltipNotePriority.Info;
        public string targetName;
        public string targetTypeName;
        public string targetGlobalId;
        public string assetPath;
        public string propertyPath;
        public string title = "New Note";
        public string body = string.Empty;
        public string tags = string.Empty;
        public string category = "General";
        public bool enabled = true;
        public bool richText = true;
        public long createdTicks = DateTime.UtcNow.Ticks;
        public long updatedTicks = DateTime.UtcNow.Ticks;
    }

    public sealed class PungentTooltipNoteDatabase : ScriptableObject
    {
        public List<PungentTooltipNote> notes = new List<PungentTooltipNote>();
    }

    [InitializeOnLoad]
    public static class PungentTooltipNoteMenuHooks
    {
        static PungentTooltipNoteMenuHooks()
        {
            EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
            Editor.finishedDefaultHeaderGUI -= OnFinishedDefaultHeaderGUI;
            Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            if (property == null || property.serializedObject == null)
                return;

            UnityEngine.Object targetObject = property.serializedObject.targetObject;
            string propertyPath = property.propertyPath;
            string propertyName = property.displayName;

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("PungentFunk/Add Tooltip Note"), false, () =>
            {
                PungentTooltipNotesBrowserWindow.OpenForTarget(targetObject, propertyPath, propertyName, true);
            });
            menu.AddItem(new GUIContent("PungentFunk/View Tooltip Notes"), false, () =>
            {
                PungentTooltipNotesBrowserWindow.OpenForTarget(targetObject, propertyPath, propertyName, false);
            });
            menu.AddItem(new GUIContent("PungentFunk/Copy Property Path"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = propertyPath;
            });
        }

        private static void OnFinishedDefaultHeaderGUI(Editor editor)
        {
            if (editor == null || editor.targets == null || editor.targets.Length == 0)
                return;

            List<PungentTooltipNote> notes = new List<PungentTooltipNote>();
            foreach (UnityEngine.Object target in editor.targets)
                notes.AddRange(PungentTooltipNotesBrowserWindow.FindVisibleNotesFor(target, true));

            notes = notes.Where(n => n.visibility == PungentTooltipNoteVisibility.InspectorHeader || n.visibility == PungentTooltipNoteVisibility.SelectionCompanion).Take(4).ToList();
            if (notes.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(GetPriorityColor(notes[0].priority), 0.14f, 0.08f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Pungent Notes", EditorStyles.boldLabel);
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(48f)))
                        PungentTooltipNotesBrowserWindow.OpenForTarget(editor.target, string.Empty, string.Empty, false);
                }

                foreach (PungentTooltipNote note in notes)
                    EditorGUILayout.LabelField("• " + note.title, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (sceneView == null)
                return;

            IEnumerable<PungentTooltipNote> notes = PungentTooltipNotesBrowserWindow.Database().notes.Where(n => n != null && n.enabled && (n.visibility == PungentTooltipNoteVisibility.SceneBadgeAlways || n.visibility == PungentTooltipNoteVisibility.SceneBadgeSelected));
            foreach (PungentTooltipNote note in notes)
            {
                UnityEngine.Object target = PungentTooltipNotesBrowserWindow.ResolveTarget(note);
                GameObject go = GetGameObject(target);
                if (go == null)
                    continue;

                if (note.visibility == PungentTooltipNoteVisibility.SceneBadgeSelected && !Selection.gameObjects.Contains(go))
                    continue;

                Vector3 position = go.transform.position + Vector3.up * 1.25f;
                Handles.color = GetPriorityColor(note.priority);
                Handles.DrawSolidDisc(position, sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward, HandleUtility.GetHandleSize(position) * 0.045f);
                Handles.Label(position + Vector3.up * HandleUtility.GetHandleSize(position) * 0.08f, new GUIContent("📝 " + note.title));
            }
        }

        private static GameObject GetGameObject(UnityEngine.Object target)
        {
            if (target is GameObject go)
                return go;
            if (target is Component component)
                return component.gameObject;
            return null;
        }

        private static Color GetPriorityColor(PungentTooltipNotePriority priority)
        {
            switch (priority)
            {
                case PungentTooltipNotePriority.Todo: return UtilityWindowTheme.Cyan;
                case PungentTooltipNotePriority.Warning: return UtilityWindowTheme.Amber;
                case PungentTooltipNotePriority.Critical: return UtilityWindowTheme.Red;
                default: return UtilityWindowTheme.Teal;
            }
        }
    }

    public sealed class PungentTooltipNotesBrowserWindow : EditorWindow
    {
        private const string DatabasePath = "Assets/PungentFunkUtilitiesData/TooltipNotes/PungentTooltipNotes.asset";
        private Vector2 _listScroll;
        private Vector2 _editorScroll;
        private string _search = string.Empty;
        private bool _selectionOnly;
        private bool _showPreview = true;
        private PungentTooltipNote _editing;
        private UnityEngine.Object _contextObject;
        private string _contextPropertyPath;
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/Components/Tooltip Notes Browser", priority = 1040)]
        public static void Open()
        {
            PungentTooltipNotesBrowserWindow window = GetWindow<PungentTooltipNotesBrowserWindow>("Tooltip Notes");
            window.minSize = new Vector2(680f, 420f);
            window.Show();
        }

        public static void OpenForProperty(SerializedProperty property, bool createNote)
        {
            if (property == null || property.serializedObject == null)
                return;

            OpenForTarget(property.serializedObject.targetObject, property.propertyPath, property.displayName, createNote);
        }

        public static void OpenForTarget(UnityEngine.Object targetObject, string propertyPath, string propertyName, bool createNote)
        {
            PungentTooltipNotesBrowserWindow window = GetWindow<PungentTooltipNotesBrowserWindow>("Tooltip Notes");
            window.minSize = new Vector2(680f, 420f);
            window._contextObject = targetObject;
            window._contextPropertyPath = propertyPath ?? string.Empty;
            window._selectionOnly = true;
            window.Show();

            if (createNote)
            {
                PungentTooltipNote note = CreateNoteFor(window._contextObject, window._contextPropertyPath, string.IsNullOrEmpty(propertyPath) ? GuessScope(targetObject) : PungentTooltipNoteScope.SerializedProperty);
                note.title = ObjectNames.NicifyVariableName(string.IsNullOrEmpty(propertyName) ? window._contextPropertyPath : propertyName);
                Database().notes.Add(note);
                SaveDatabase();
                window._editing = note;
                window._status = "Created note for " + note.targetName + ".";
            }
        }

        public static PungentTooltipNoteDatabase Database()
        {
            PungentTooltipNoteDatabase db = AssetDatabase.LoadAssetAtPath<PungentTooltipNoteDatabase>(DatabasePath);
            if (db != null)
                return db;

            string folder = "Assets/PungentFunkUtilitiesData";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "PungentFunkUtilitiesData");
            string sub = "Assets/PungentFunkUtilitiesData/TooltipNotes";
            if (!AssetDatabase.IsValidFolder(sub))
                AssetDatabase.CreateFolder(folder, "TooltipNotes");

            db = CreateInstance<PungentTooltipNoteDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            return db;
        }

        public static void SaveDatabase()
        {
            PungentTooltipNoteDatabase db = Database();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }

        public static List<PungentTooltipNote> FindVisibleNotesFor(UnityEngine.Object target, bool includeTypeNotes)
        {
            string id = TryGetGlobalId(target);
            string type = target != null ? target.GetType().FullName : string.Empty;
            return Database().notes.Where(n => n != null && n.enabled && MatchesObject(n, target, id, type, includeTypeNotes)).ToList();
        }

        public static UnityEngine.Object ResolveTarget(PungentTooltipNote note)
        {
            if (note == null)
                return null;

            if (!string.IsNullOrEmpty(note.targetGlobalId) && GlobalObjectId.TryParse(note.targetGlobalId, out GlobalObjectId globalId))
            {
                UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (obj != null)
                    return obj;
            }

            if (!string.IsNullOrEmpty(note.assetPath))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(note.assetPath);

            return null;
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header("Tooltip Notes", "Attach non-destructive notes to objects, components, assets, and fields. Notes can appear in inspectors and as SceneView badges.", _status);
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawNotesList(GUILayout.MinWidth(300f));
                DrawEditor(GUILayout.Width(Mathf.Clamp(position.width * 0.46f, 300f, 520f)));
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(52f));
                _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                _selectionOnly = GUILayout.Toggle(_selectionOnly, "Selection", EditorStyles.toolbarButton, GUILayout.Width(82f));
                _showPreview = GUILayout.Toggle(_showPreview, "Preview", EditorStyles.toolbarButton, GUILayout.Width(70f));
                if (UtilityWindowTheme.TintedButton("New Selection Note", UtilityWindowTheme.Green, GUILayout.Width(140f)))
                    CreateSelectionNote();
            }
        }

        private void DrawNotesList(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                List<PungentTooltipNote> notes = QueryNotes().ToList();
                UtilityWindowTheme.SectionTitle("Notes", UtilityWindowTheme.Teal, notes.Count + " shown");
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                foreach (PungentTooltipNote note in notes)
                    DrawNoteCard(note);
                EditorGUILayout.EndScrollView();
            }
        }

        private IEnumerable<PungentTooltipNote> QueryNotes()
        {
            IEnumerable<PungentTooltipNote> query = Database().notes.Where(n => n != null);

            if (_selectionOnly)
            {
                UnityEngine.Object target = _contextObject != null ? _contextObject : Selection.activeObject;
                string selectedId = TryGetGlobalId(target);
                string selectedType = target != null ? target.GetType().FullName : string.Empty;
                query = query.Where(n => MatchesObject(n, target, selectedId, selectedType, true));
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string lower = _search.Trim().ToLowerInvariant();
                query = query.Where(n => Contains(n.title, lower) || Contains(n.body, lower) || Contains(n.tags, lower) || Contains(n.targetName, lower) || Contains(n.propertyPath, lower) || Contains(n.category, lower));
            }

            return query.OrderByDescending(n => n.priority).ThenBy(n => n.category).ThenBy(n => n.targetName).ThenBy(n => n.propertyPath);
        }

        private void DrawNoteCard(PungentTooltipNote note)
        {
            Color tint = GetPriorityColor(note.priority);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(note.enabled ? tint : UtilityWindowTheme.Neutral, 0.13f, 0.06f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(note.title, EditorStyles.boldLabel))
                        _editing = note;
                    UtilityWindowTheme.CountPill(GetScopeLabel(note.scope), tint, 126f);
                    if (GUILayout.Button("Edit", GUILayout.Width(48f)))
                        _editing = note;
                    if (GUILayout.Button("X", GUILayout.Width(24f)) && EditorUtility.DisplayDialog("Delete Tooltip Note", "Delete this note?", "Delete", "Cancel"))
                    {
                        Database().notes.Remove(note);
                        SaveDatabase();
                        if (_editing == note)
                            _editing = null;
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.LabelField(note.targetName + (string.IsNullOrEmpty(note.propertyPath) ? string.Empty : " › " + note.propertyPath), UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(note.body))
                    EditorGUILayout.LabelField(StripRichText(note.body), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawEditor(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple), options))
            {
                UtilityWindowTheme.SectionTitle("Note Editor", UtilityWindowTheme.Purple, _editing != null ? _editing.priority.ToString() : "No note");
                if (_editing == null)
                {
                    EditorGUILayout.HelpBox("Select a note to edit it, or create a note from the current selection / field context menu.", MessageType.Info);
                    return;
                }

                _editorScroll = EditorGUILayout.BeginScrollView(_editorScroll);
                _editing.enabled = EditorGUILayout.Toggle("Enabled", _editing.enabled);
                _editing.scope = (PungentTooltipNoteScope)EditorGUILayout.EnumPopup(new GUIContent("Linked To", "Controls the target matching behaviour."), _editing.scope);
                EditorGUILayout.LabelField("Meaning", GetScopeLabel(_editing.scope), UtilityWindowTheme.MutedMiniLabelStyle);
                _editing.visibility = (PungentTooltipNoteVisibility)EditorGUILayout.EnumPopup("Visibility", _editing.visibility);
                _editing.priority = (PungentTooltipNotePriority)EditorGUILayout.EnumPopup("Priority", _editing.priority);
                _editing.title = EditorGUILayout.TextField("Title", _editing.title);
                _editing.category = EditorGUILayout.TextField("Category", _editing.category);
                _editing.tags = EditorGUILayout.TextField("Tags", _editing.tags);
                _editing.richText = EditorGUILayout.Toggle("Rich Text", _editing.richText);

                DrawRichTextToolbar(_editing);
                _editing.body = EditorGUILayout.TextArea(_editing.body, GUILayout.MinHeight(118f));
                if (_showPreview)
                {
                    GUIStyle previewStyle = new GUIStyle(EditorStyles.helpBox) { richText = _editing.richText, wordWrap = true };
                    EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(_editing.body, previewStyle, GUILayout.MinHeight(50f));
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_editing.targetName, UtilityWindowTheme.PathLabelStyle);
                EditorGUILayout.LabelField(_editing.targetTypeName, UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrEmpty(_editing.propertyPath))
                    EditorGUILayout.SelectableLabel(_editing.propertyPath, UtilityWindowTheme.PathLabelStyle, GUILayout.Height(18f));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Save", UtilityWindowTheme.Green, GUILayout.Width(70f)))
                    {
                        _editing.updatedTicks = DateTime.UtcNow.Ticks;
                        SaveDatabase();
                        _status = "Saved note.";
                    }
                    if (GUILayout.Button("Ping Target", GUILayout.Width(88f)))
                    {
                        UnityEngine.Object target = ResolveTarget(_editing);
                        if (target != null)
                            EditorGUIUtility.PingObject(target);
                    }
                    if (GUILayout.Button("Stop Editing", GUILayout.Width(96f)))
                        _editing = null;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawRichTextToolbar(PungentTooltipNote note)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("B", EditorStyles.miniButton, GUILayout.Width(28f))) note.body += "<b>bold</b>";
                if (GUILayout.Button("I", EditorStyles.miniButton, GUILayout.Width(28f))) note.body += "<i>italic</i>";
                if (GUILayout.Button("Warn", EditorStyles.miniButton, GUILayout.Width(48f))) note.body += "<color=#FFB84D>warning</color>";
                if (GUILayout.Button("Code", EditorStyles.miniButton, GUILayout.Width(44f))) note.body += "<b><color=#9EEBFF>code</color></b>";
                if (GUILayout.Button("Bullet", EditorStyles.miniButton, GUILayout.Width(50f))) note.body += "\n• ";
            }
        }

        private void CreateSelectionNote()
        {
            UnityEngine.Object target = Selection.activeObject;
            if (target == null)
            {
                _status = "Select an object, component, prefab, or asset first.";
                return;
            }

            PungentTooltipNote note = CreateNoteFor(target, string.Empty, GuessScope(target));
            Database().notes.Add(note);
            SaveDatabase();
            _editing = note;
            _status = "Created selection note.";
        }

        private static PungentTooltipNote CreateNoteFor(UnityEngine.Object target, string propertyPath, PungentTooltipNoteScope scope)
        {
            string assetPath = target != null ? AssetDatabase.GetAssetPath(target) : string.Empty;
            return new PungentTooltipNote
            {
                scope = scope,
                targetName = target != null ? target.name : "Unknown Target",
                targetTypeName = target != null ? target.GetType().FullName : string.Empty,
                targetGlobalId = TryGetGlobalId(target),
                assetPath = assetPath,
                propertyPath = propertyPath ?? string.Empty,
                title = string.IsNullOrEmpty(propertyPath) ? "New Note" : ObjectNames.NicifyVariableName(propertyPath.Split('.').Last()),
                body = string.Empty,
                visibility = string.IsNullOrEmpty(propertyPath) ? PungentTooltipNoteVisibility.InspectorHeader : PungentTooltipNoteVisibility.SelectionCompanion
            };
        }

        private static PungentTooltipNoteScope GuessScope(UnityEngine.Object target)
        {
            if (target is Component)
                return PungentTooltipNoteScope.ComponentInstance;
            if (target is GameObject)
                return PungentTooltipNoteScope.SceneObject;
            string path = target != null ? AssetDatabase.GetAssetPath(target) : string.Empty;
            return string.IsNullOrEmpty(path) ? PungentTooltipNoteScope.SceneObject : PungentTooltipNoteScope.Asset;
        }

        private static bool MatchesObject(PungentTooltipNote note, UnityEngine.Object target, string selectedId, string selectedType, bool includeTypeNotes)
        {
            if (note == null || target == null)
                return false;

            if (!string.IsNullOrEmpty(note.targetGlobalId) && string.Equals(note.targetGlobalId, selectedId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (includeTypeNotes && note.scope == PungentTooltipNoteScope.ComponentType && string.Equals(note.targetTypeName, selectedType, StringComparison.OrdinalIgnoreCase))
                return true;

            if (note.scope == PungentTooltipNoteScope.Asset)
            {
                string path = AssetDatabase.GetAssetPath(target);
                return !string.IsNullOrEmpty(path) && string.Equals(path, note.assetPath, StringComparison.OrdinalIgnoreCase);
            }

            return false;
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

        private static string GetScopeLabel(PungentTooltipNoteScope scope)
        {
            switch (scope)
            {
                case PungentTooltipNoteScope.ComponentType: return "All components of this type";
                case PungentTooltipNoteScope.ComponentInstance: return "This exact component";
                case PungentTooltipNoteScope.PrefabAsset: return "This prefab asset";
                case PungentTooltipNoteScope.SceneObject: return "This GameObject";
                case PungentTooltipNoteScope.Asset: return "This asset";
                case PungentTooltipNoteScope.SerializedProperty: return "This exact field / parameter";
                default: return scope.ToString();
            }
        }

        private static Color GetPriorityColor(PungentTooltipNotePriority priority)
        {
            switch (priority)
            {
                case PungentTooltipNotePriority.Todo: return UtilityWindowTheme.Cyan;
                case PungentTooltipNotePriority.Warning: return UtilityWindowTheme.Amber;
                case PungentTooltipNotePriority.Critical: return UtilityWindowTheme.Red;
                default: return UtilityWindowTheme.Teal;
            }
        }

        private static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("<b>", string.Empty).Replace("</b>", string.Empty).Replace("<i>", string.Empty).Replace("</i>", string.Empty);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
    #endif

}