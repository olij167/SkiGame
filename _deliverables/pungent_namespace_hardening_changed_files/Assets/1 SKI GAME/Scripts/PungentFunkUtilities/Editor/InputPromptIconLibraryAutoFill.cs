using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.UI
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Utility-suite version of the input prompt icon populator.
    /// It intentionally avoids compile-time dependency on a specific project InputPromptIconLibrary type and writes by serialized field names.
    /// </summary>
    public sealed class InputPromptIconLibraryAutoFill : EditorWindow
    {
        private const string LibraryTypeName = "InputPromptIconLibrary";
        private const string DefaultAssetName = "InputPromptIconLibrary_Default";
        private const string DefaultSearchRoot = "Assets";

        private const string PrefPrefix = "GenericUtility.InputIconLibraryPopulator.";
        private const string PrefSearchRoot = PrefPrefix + "SearchRoot";
        private const string PrefIconPackQuery = PrefPrefix + "IconPackQuery";
        private const string PrefClearExisting = PrefPrefix + "ClearExisting";
        private const string PrefOnlyFillMissing = PrefPrefix + "OnlyFillMissing";
        private const string PrefIncludePackages = PrefPrefix + "IncludePackages";
        private const string PrefConfigHeight = PrefPrefix + "ConfigHeight";
        private const string PrefPreviewHeight = PrefPrefix + "PreviewHeight";

        private sealed class EntryData
        {
            public string Key;
            public string FileName;
            public string Fallback;
        }

        private sealed class PopulateReport
        {
            public int Added;
            public int Updated;
            public int SkippedExisting;
            public int Missing;
            public readonly List<string> MissingFiles = new List<string>();
        }

        private Object _library;
        private string _searchRoot = DefaultSearchRoot;
        private string _iconPackQuery = "GameInputControllerIconsFree";
        private bool _clearExisting = true;
        private bool _onlyFillMissing = false;
        private bool _includePackages = false;
        private float _configHeight = 170f;
        private float _previewHeight = 260f;
        private Vector2 _mainScroll;
        private Vector2 _configScroll;
        private Vector2 _previewScroll;
        private string _status = "Ready.";
        private PopulateReport _lastReport;

        [MenuItem("Tools/Utilities/Input/Populate Input Prompt Icon Library", priority = 1100)]
        public static void OpenWindow()
        {
            InputPromptIconLibraryAutoFill window = GetWindow<InputPromptIconLibraryAutoFill>("Input Icon Library");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        [MenuItem("Tools/Utilities/Input/Populate Selected Input Prompt Library", priority = 1101)]
        private static void PopulateSelectedLibrary()
        {
            Object selected = Selection.activeObject;
            if (!IsInputPromptIconLibrary(selected))
            {
                EditorUtility.DisplayDialog("Input Prompt Library", "Select an InputPromptIconLibrary asset first.", "OK");
                return;
            }

            PopulateLibrary(selected, new PopulateOptions(), showDialog: true);
        }

        [MenuItem("Tools/Utilities/Input/Populate Selected Input Prompt Library", true)]
        private static bool ValidatePopulateSelectedLibrary()
        {
            return IsInputPromptIconLibrary(Selection.activeObject);
        }

        [MenuItem("Tools/Utilities/Input/Populate Default Input Prompt Library", priority = 1102)]
        private static void PopulateDefaultLibrary()
        {
            Object library = FindDefaultLibrary();
            if (library == null)
            {
                EditorUtility.DisplayDialog(
                    "Input Prompt Library",
                    "Could not find a default InputPromptIconLibrary asset. Expected either InputPromptIconLibrary_Default.asset or InputPromptIconLibrary.asset.",
                    "OK");
                return;
            }

            PopulateLibrary(library, new PopulateOptions(), showDialog: true);
        }

        private void OnEnable()
        {
            _searchRoot = UtilityWindowPrefs.GetString(PrefSearchRoot, _searchRoot);
            _iconPackQuery = UtilityWindowPrefs.GetString(PrefIconPackQuery, _iconPackQuery);
            _clearExisting = UtilityWindowPrefs.GetBool(PrefClearExisting, _clearExisting);
            _onlyFillMissing = UtilityWindowPrefs.GetBool(PrefOnlyFillMissing, _onlyFillMissing);
            _includePackages = UtilityWindowPrefs.GetBool(PrefIncludePackages, _includePackages);
            _configHeight = UtilityWindowPrefs.GetFloat(PrefConfigHeight, _configHeight);
            _previewHeight = UtilityWindowPrefs.GetFloat(PrefPreviewHeight, _previewHeight);

            if (_library == null && IsInputPromptIconLibrary(Selection.activeObject))
                _library = Selection.activeObject;
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearchRoot, _searchRoot);
            UtilityWindowPrefs.SetString(PrefIconPackQuery, _iconPackQuery);
            UtilityWindowPrefs.SetBool(PrefClearExisting, _clearExisting);
            UtilityWindowPrefs.SetBool(PrefOnlyFillMissing, _onlyFillMissing);
            UtilityWindowPrefs.SetBool(PrefIncludePackages, _includePackages);
            UtilityWindowPrefs.SetFloat(PrefConfigHeight, _configHeight);
            UtilityWindowPrefs.SetFloat(PrefPreviewHeight, _previewHeight);
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Input Prompt Icon Library Populator",
                "Populates an InputPromptIconLibrary-style asset by matching known input keys to sprite filenames.",
                _status);

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawConfigPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _configHeight, 120f, 360f, SavePrefs);
            DrawActionPanel();
            DrawPreviewPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _previewHeight, 150f, 520f, SavePrefs);
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawConfigPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Configuration", UtilityWindowTheme.Blue);
                _configScroll = EditorGUILayout.BeginScrollView(_configScroll, GUILayout.Height(_configHeight));

                _library = EditorGUILayout.ObjectField(new GUIContent("Target Library", "InputPromptIconLibrary asset to populate."), _library, typeof(Object), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _searchRoot = EditorGUILayout.TextField(new GUIContent("Search Root", "Preferred folder to search for input icon sprites."), _searchRoot);
                    if (GUILayout.Button("Assets", GUILayout.Width(58f)))
                        _searchRoot = "Assets";
                }

                _iconPackQuery = EditorGUILayout.TextField(new GUIContent("Icon Pack Query", "Optional asset/folder query used to discover icon-pack folders."), _iconPackQuery);
                _clearExisting = EditorGUILayout.ToggleLeft(new GUIContent("Clear Existing Entries First", "Preserves the original behaviour: rebuild the entries array from scratch."), _clearExisting);
                using (new EditorGUI.DisabledScope(_clearExisting))
                    _onlyFillMissing = EditorGUILayout.ToggleLeft(new GUIContent("Only Fill Missing Keys", "When not clearing first, skip keys already present in the library."), _onlyFillMissing);
                _includePackages = EditorGUILayout.ToggleLeft(new GUIContent("Search Packages Too", "If enabled, searches all project/package assets. Slower, but useful if the icon pack lives in Packages."), _includePackages);

                if (!IsInputPromptIconLibrary(_library))
                    EditorGUILayout.HelpBox("Assign an asset whose type name is InputPromptIconLibrary.", MessageType.Warning);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawActionPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use Selected", GUILayout.Height(24f)))
                    {
                        if (IsInputPromptIconLibrary(Selection.activeObject))
                            _library = Selection.activeObject;
                        else
                            EditorUtility.DisplayDialog("Input Prompt Library", "The selected asset is not an InputPromptIconLibrary.", "OK");
                    }

                    if (GUILayout.Button("Find Default", GUILayout.Height(24f)))
                    {
                        _library = FindDefaultLibrary();
                        _status = _library != null ? $"Found default library: {_library.name}" : "Default library not found.";
                    }

                    using (new EditorGUI.DisabledScope(!IsInputPromptIconLibrary(_library)))
                    {
                        if (GUILayout.Button("Populate", GUILayout.Height(24f)))
                        {
                            PopulateOptions options = BuildOptionsFromWindow();
                            _lastReport = PopulateLibrary(_library, options, showDialog: false);
                            _status = _lastReport != null
                                ? $"Added={_lastReport.Added}, Updated={_lastReport.Updated}, Missing={_lastReport.Missing}"
                                : "Populate failed.";
                        }
                    }
                }
            }
        }

        private void DrawPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Default Entry Map", UtilityWindowTheme.Purple, BuildEntries().Count.ToString());
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(_previewHeight));

                if (_lastReport != null)
                {
                    EditorGUILayout.LabelField($"Last populate: Added={_lastReport.Added}, Updated={_lastReport.Updated}, SkippedExisting={_lastReport.SkippedExisting}, Missing={_lastReport.Missing}", UtilityWindowTheme.MutedMiniLabelStyle);
                    if (_lastReport.MissingFiles.Count > 0)
                    {
                        EditorGUILayout.Space(4f);
                        EditorGUILayout.LabelField("Missing sprite matches", EditorStyles.boldLabel);
                        for (int i = 0; i < Mathf.Min(_lastReport.MissingFiles.Count, 80); i++)
                            EditorGUILayout.LabelField(_lastReport.MissingFiles[i], UtilityWindowTheme.PathLabelStyle);
                        if (_lastReport.MissingFiles.Count > 80)
                            EditorGUILayout.LabelField($"...and {_lastReport.MissingFiles.Count - 80} more.", UtilityWindowTheme.MutedMiniLabelStyle);
                        EditorGUILayout.Space(8f);
                    }
                }

                List<EntryData> entries = BuildEntries();
                for (int i = 0; i < entries.Count; i++)
                    EditorGUILayout.LabelField($"{entries[i].Key}  ->  {entries[i].FileName}  ({entries[i].Fallback})", UtilityWindowTheme.PathLabelStyle);

                EditorGUILayout.EndScrollView();
            }
        }

        private PopulateOptions BuildOptionsFromWindow()
        {
            return new PopulateOptions
            {
                SearchRoot = _searchRoot,
                IconPackQuery = _iconPackQuery,
                ClearExisting = _clearExisting,
                OnlyFillMissing = _onlyFillMissing,
                IncludePackages = _includePackages
            };
        }

        private static PopulateReport PopulateLibrary(Object library, PopulateOptions options, bool showDialog)
        {
            if (!IsInputPromptIconLibrary(library))
                return null;

            string libraryPath = AssetDatabase.GetAssetPath(library);
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                Debug.LogError("Could not resolve asset path for InputPromptIconLibrary.");
                return null;
            }

            string[] searchRoots = ResolveSearchRoots(options);
            Dictionary<string, Sprite> spriteLookup = BuildSpriteLookup(searchRoots, options.IncludePackages);
            List<EntryData> entries = BuildEntries();
            PopulateReport report = new PopulateReport();

            SerializedObject so = new SerializedObject(library);
            SerializedProperty entriesProp = so.FindProperty("entries");
            if (entriesProp == null || !entriesProp.isArray)
            {
                Debug.LogError("Could not find array field 'entries' on InputPromptIconLibrary.");
                return null;
            }

            if (options.ClearExisting)
                entriesProp.ClearArray();

            for (int i = 0; i < entries.Count; i++)
            {
                EntryData entry = entries[i];
                if (!spriteLookup.TryGetValue(entry.FileName, out Sprite sprite) || sprite == null)
                {
                    report.Missing++;
                    report.MissingFiles.Add($"{entry.Key} -> {entry.FileName}");
                    continue;
                }

                int existingIndex = FindEntryIndex(entriesProp, entry.Key);
                if (existingIndex >= 0 && options.OnlyFillMissing)
                {
                    report.SkippedExisting++;
                    continue;
                }

                SerializedProperty element;
                if (existingIndex >= 0)
                {
                    element = entriesProp.GetArrayElementAtIndex(existingIndex);
                    report.Updated++;
                }
                else
                {
                    int index = entriesProp.arraySize;
                    entriesProp.InsertArrayElementAtIndex(index);
                    element = entriesProp.GetArrayElementAtIndex(index);
                    report.Added++;
                }

                SetEntry(element, entry, sprite);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            string summary = $"Populated '{library.name}'. Added={report.Added}, Updated={report.Updated}, SkippedExisting={report.SkippedExisting}, Missing={report.Missing}";
            if (report.MissingFiles.Count > 0)
                Debug.LogWarning(summary + "\nMissing:\n" + string.Join("\n", report.MissingFiles));
            else
                Debug.Log(summary);

            if (showDialog)
                EditorUtility.DisplayDialog("Input Prompt Library", summary, "OK");

            return report;
        }

        private static void SetEntry(SerializedProperty element, EntryData entry, Sprite sprite)
        {
            SerializedProperty keyProp = element.FindPropertyRelative("key");
            SerializedProperty spriteProp = element.FindPropertyRelative("sprite");
            SerializedProperty fallbackProp = element.FindPropertyRelative("textFallback");

            if (keyProp != null)
                keyProp.stringValue = entry.Key;
            if (spriteProp != null)
                spriteProp.objectReferenceValue = sprite;
            if (fallbackProp != null)
                fallbackProp.stringValue = entry.Fallback;
        }

        private static int FindEntryIndex(SerializedProperty entriesProp, string key)
        {
            if (entriesProp == null || !entriesProp.isArray)
                return -1;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty element = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty keyProp = element.FindPropertyRelative("key");
                if (keyProp != null && string.Equals(keyProp.stringValue, key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static Object FindDefaultLibrary()
        {
            string[] preferredNames = { DefaultAssetName, "InputPromptIconLibrary" };
            for (int n = 0; n < preferredNames.Length; n++)
            {
                string[] guids = AssetDatabase.FindAssets($"t:{LibraryTypeName} {preferredNames[n]}");
                Object fallbackMatch = null;
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Object candidate = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (!IsInputPromptIconLibrary(candidate))
                        continue;

                    if (!string.Equals(candidate.name, preferredNames[n], StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (fallbackMatch == null)
                        fallbackMatch = candidate;

                    if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                        return candidate;
                }

                if (fallbackMatch != null)
                    return fallbackMatch;
            }

            return null;
        }

        private static bool IsInputPromptIconLibrary(Object obj)
        {
            return obj != null && string.Equals(obj.GetType().Name, LibraryTypeName, StringComparison.Ordinal);
        }

        private static string[] ResolveSearchRoots(PopulateOptions options)
        {
            List<string> roots = new List<string>();

            if (!string.IsNullOrWhiteSpace(options.SearchRoot) && AssetDatabase.IsValidFolder(options.SearchRoot))
                roots.Add(options.SearchRoot);

            if (!string.IsNullOrWhiteSpace(options.IconPackQuery))
            {
                string[] iconPackGuids = AssetDatabase.FindAssets(options.IconPackQuery);
                for (int i = 0; i < iconPackGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(iconPackGuids[i]);
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path)?.Replace("\\", "/");
                    if (!string.IsNullOrWhiteSpace(dir) && AssetDatabase.IsValidFolder(dir) && !roots.Contains(dir))
                        roots.Add(dir);
                }
            }

            if (roots.Count == 0 && AssetDatabase.IsValidFolder(DefaultSearchRoot))
                roots.Add(DefaultSearchRoot);
            if (roots.Count == 0)
                roots.Add("Assets");

            return roots.ToArray();
        }

        private static Dictionary<string, Sprite> BuildSpriteLookup(string[] searchRoots, bool includePackages)
        {
            Dictionary<string, Sprite> lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            string[] guids = includePackages ? AssetDatabase.FindAssets("t:Sprite") : AssetDatabase.FindAssets("t:Sprite", searchRoots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;

                string fileName = Path.GetFileName(path);
                if (!lookup.ContainsKey(fileName))
                    lookup.Add(fileName, sprite);
            }

            return lookup;
        }

        private static List<EntryData> BuildEntries()
        {
            List<EntryData> entries = new List<EntryData>
            {
                Make("keyboard:shift", "shift.png", "Shift"),
                Make("keyboard:ctrl", "ctrl.png", "Ctrl"),
                Make("keyboard:alt", "alt.png", "Alt"),
                Make("keyboard:esc", "esc.png", "Esc"),
                Make("keyboard:space", "space.png", "Space"),
                Make("keyboard:enter", "enter.png", "Enter"),
                Make("keyboard:tab", "tab.png", "Tab"),
                Make("keyboard:pause", "pause.png", "Pause"),
                Make("keyboard:arrow-up", "arrow-up.png", "Up"),
                Make("keyboard:arrow-down", "arrow-down.png", "Down"),
                Make("keyboard:arrow-left", "arrow-left.png", "Left"),
                Make("keyboard:arrow-right", "arrow-right.png", "Right"),
                Make("mouse:left", "mouse-left.png", "LMB"),
                Make("mouse:right", "mouse-right.png", "RMB"),
                Make("mouse:middle", "mouse-middle.png", "MMB"),
                Make("mouse:move-hor", "mouse-move-hor.png", "Mouse X"),
                Make("mouse:move-vert", "mouse-move-vert.png", "Mouse Y")
            };

            for (char c = 'a'; c <= 'z'; c++)
            {
                string letter = c.ToString();
                entries.Add(Make($"keyboard:{letter}", $"{letter}.png", letter.ToUpperInvariant()));
            }

            for (int i = 0; i <= 9; i++)
            {
                string digit = i.ToString();
                entries.Add(Make($"keyboard:{digit}", $"{digit}.png", digit));
            }

            return entries;
        }

        private static EntryData Make(string key, string fileName, string fallback)
        {
            return new EntryData { Key = key, FileName = fileName, Fallback = fallback };
        }

        private sealed class PopulateOptions
        {
            public string SearchRoot = DefaultSearchRoot;
            public string IconPackQuery = "GameInputControllerIconsFree";
            public bool ClearExisting = true;
            public bool OnlyFillMissing = false;
            public bool IncludePackages = false;
        }
    }
    #endif

}