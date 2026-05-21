using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Content;

namespace PungentFunk.Utilities.Editor.Content
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public sealed class NameGeneratorWindow : EditorWindow
    {
        private enum GenerationMode
        {
            RandomFromLists,
            PrefixSuffix,
            Template
        }

        [Serializable]
        private sealed class TokenBinding
        {
            public string token = "token";
            public NameList source;
        }

        private const string PrefPrefix = "PungentFunk.Utilities.NameGenerator.";
        private const float MinPanelHeight = 90f;

        private readonly List<NameList> _sourceLists = new List<NameList>();
        private readonly List<MadLibNameList> _madLibSources = new List<MadLibNameList>();
        private readonly List<TokenBinding> _tokenBindings = new List<TokenBinding>();
        private readonly List<string> _results = new List<string>();

        private Vector2 _mainScroll;
        private Vector2 _sourceScroll;
        private Vector2 _resultScroll;
        private string _status = "Ready.";
        private string _filter = string.Empty;
        private string _template = "{first} {last}";
        private string _separator = " ";
        private string _assetSaveFolder = "Assets";
        private int _count = 25;
        private int _seed = 12345;
        private bool _useSeed;
        private bool _unique = true;
        private bool _trimResults = true;
        private bool _titleCaseResults;
        private bool _sortResults;
        private bool _appendResults;
        private bool _sourcesExpanded = true;
        private bool _generationExpanded = true;
        private bool _resultsExpanded = true;
        private bool _maintenanceExpanded;
        private float _sourcesHeight = 210f;
        private float _generationHeight = 190f;
        private float _resultsHeight = 300f;
        private GenerationMode _mode = GenerationMode.RandomFromLists;

        [MenuItem("Tools/Utilities/Generation/Name Generator")]
        public static void Open()
        {
            GetWindow<NameGeneratorWindow>("Name Generator");
        }

        private void OnEnable()
        {
            minSize = new Vector2(520f, 420f);
            LoadPrefs();

            if (_tokenBindings.Count == 0)
            {
                _tokenBindings.Add(new TokenBinding { token = "first" });
                _tokenBindings.Add(new TokenBinding { token = "last" });
            }
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Name Generator",
                "Generate names from reusable ScriptableObject lists, mad-lib prefix/suffix sets, or tokenised templates.",
                _status);

            DrawToolbar();

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawSourcesSection();
            DrawGenerationSection();
            DrawResultsSection();
            DrawMaintenanceSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Load Selection", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                    AddSelectedAssets();

                if (GUILayout.Button("Find Project Lists", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                    FindProjectLists();

                if (GUILayout.Button("Clear Sources", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    ClearSources();

                GUILayout.FlexibleSpace();
                GUILayout.Label("Filter", EditorStyles.miniLabel, GUILayout.Width(34f));
                _filter = EditorGUILayout.TextField(_filter, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.Width(190f));
            }
        }

        private void DrawSourcesSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                DrawFoldoutHeader(ref _sourcesExpanded, "Sources", UtilityWindowTheme.Blue, $"{_sourceLists.Count} lists / {_madLibSources.Count} mad-lib");
                if (!_sourcesExpanded)
                    return;

                _sourceScroll = EditorGUILayout.BeginScrollView(_sourceScroll, GUILayout.Height(_sourcesHeight));

                EditorGUILayout.LabelField("Name Lists", UtilityWindowTheme.SectionHeaderStyle);
                DrawNameListRows();

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Mad-Lib Lists", UtilityWindowTheme.SectionHeaderStyle);
                DrawMadLibRows();

                EditorGUILayout.EndScrollView();
                UtilityWindowTheme.VerticalResizeHandle(ref _sourcesHeight, MinPanelHeight, Mathf.Max(MinPanelHeight, position.height - 180f), SavePrefs);
            }
        }

        private void DrawNameListRows()
        {
            for (int i = _sourceLists.Count - 1; i >= 0; i--)
            {
                NameList list = _sourceLists[i];
                if (!PassesFilter(list == null ? string.Empty : list.name))
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    _sourceLists[i] = (NameList)EditorGUILayout.ObjectField(list, typeof(NameList), false);
                    GUILayout.Label(list == null ? "0" : list.Count.ToString(), UtilityWindowTheme.CountPillStyle, GUILayout.Width(44f));

                    if (GUILayout.Button("Ping", GUILayout.Width(48f)) && list != null)
                        EditorGUIUtility.PingObject(list);

                    if (GUILayout.Button("−", GUILayout.Width(26f)))
                        _sourceLists.RemoveAt(i);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add List Slot"))
                    _sourceLists.Add(null);

                if (GUILayout.Button("Add Selected Name Lists"))
                    AddSelectedNameListsOnly();
            }
        }

        private void DrawMadLibRows()
        {
            for (int i = _madLibSources.Count - 1; i >= 0; i--)
            {
                MadLibNameList list = _madLibSources[i];
                if (!PassesFilter(list == null ? string.Empty : list.name))
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    _madLibSources[i] = (MadLibNameList)EditorGUILayout.ObjectField(list, typeof(MadLibNameList), false);
                    string counts = list == null ? "0/0" : $"{SafeCount(list.prefixList)}/{SafeCount(list.suffixList)}";
                    GUILayout.Label(counts, UtilityWindowTheme.CountPillStyle, GUILayout.Width(56f));

                    if (GUILayout.Button("Ping", GUILayout.Width(48f)) && list != null)
                        EditorGUIUtility.PingObject(list);

                    if (GUILayout.Button("−", GUILayout.Width(26f)))
                        _madLibSources.RemoveAt(i);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Mad-Lib Slot"))
                    _madLibSources.Add(null);

                if (GUILayout.Button("Add Selected Mad-Lib Lists"))
                    AddSelectedMadLibListsOnly();
            }
        }

        private void DrawGenerationSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                DrawFoldoutHeader(ref _generationExpanded, "Generation", UtilityWindowTheme.Teal, _mode.ToString());
                if (!_generationExpanded)
                    return;

                using (new EditorGUILayout.VerticalScope(GUILayout.Height(_generationHeight)))
                {
                    _mode = (GenerationMode)EditorGUILayout.EnumPopup("Mode", _mode);
                    _count = Mathf.Max(1, EditorGUILayout.IntField("Count", _count));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _useSeed = EditorGUILayout.Toggle("Use Seed", _useSeed);
                        using (new EditorGUI.DisabledScope(!_useSeed))
                            _seed = EditorGUILayout.IntField(_seed);
                    }

                    _unique = EditorGUILayout.Toggle("Unique Results", _unique);
                    _appendResults = EditorGUILayout.Toggle("Append To Existing Results", _appendResults);
                    _trimResults = EditorGUILayout.Toggle("Trim Results", _trimResults);
                    _titleCaseResults = EditorGUILayout.Toggle("Title Case Results", _titleCaseResults);
                    _sortResults = EditorGUILayout.Toggle("Sort Results", _sortResults);

                    if (_mode == GenerationMode.RandomFromLists)
                        _separator = EditorGUILayout.TextField("Join Separator", _separator);

                    if (_mode == GenerationMode.Template)
                        DrawTemplateControls();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Generate", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                            Generate();

                        if (GUILayout.Button("Clear Results", GUILayout.Height(28f)))
                        {
                            _results.Clear();
                            _status = "Cleared results.";
                        }
                    }
                }

                UtilityWindowTheme.VerticalResizeHandle(ref _generationHeight, 135f, Mathf.Max(135f, position.height - 170f), SavePrefs);
            }
        }

        private void DrawTemplateControls()
        {
            EditorGUILayout.Space(4f);
            _template = EditorGUILayout.TextField("Template", _template);
            EditorGUILayout.LabelField("Use tokens like {first}, {last}, {place}. Bind each token to a NameList below.", UtilityWindowTheme.MutedMiniLabelStyle);

            for (int i = _tokenBindings.Count - 1; i >= 0; i--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _tokenBindings[i].token = EditorGUILayout.TextField(_tokenBindings[i].token, GUILayout.Width(120f));
                    _tokenBindings[i].source = (NameList)EditorGUILayout.ObjectField(_tokenBindings[i].source, typeof(NameList), false);
                    if (GUILayout.Button("−", GUILayout.Width(26f)))
                        _tokenBindings.RemoveAt(i);
                }
            }

            if (GUILayout.Button("Add Token Binding"))
                _tokenBindings.Add(new TokenBinding { token = "token" + (_tokenBindings.Count + 1) });
        }

        private void DrawResultsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                DrawFoldoutHeader(ref _resultsExpanded, "Results", UtilityWindowTheme.Purple, _results.Count.ToString());
                if (!_resultsExpanded)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy All"))
                        EditorGUIUtility.systemCopyBuffer = string.Join(Environment.NewLine, _results.ToArray());

                    if (GUILayout.Button("Save As NameList Asset"))
                        SaveResultsAsNameListAsset();

                    _assetSaveFolder = EditorGUILayout.TextField(_assetSaveFolder);
                    if (GUILayout.Button("…", GUILayout.Width(30f)))
                        PickSaveFolder();
                }

                _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll, GUILayout.Height(_resultsHeight));
                for (int i = 0; i < _results.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.SelectableLabel(_results[i], GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        if (GUILayout.Button("Copy", GUILayout.Width(52f)))
                            EditorGUIUtility.systemCopyBuffer = _results[i];
                        if (GUILayout.Button("−", GUILayout.Width(26f)))
                        {
                            _results.RemoveAt(i);
                            i--;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                UtilityWindowTheme.VerticalResizeHandle(ref _resultsHeight, 120f, Mathf.Max(120f, position.height - 160f), SavePrefs);
            }
        }

        private void DrawMaintenanceSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                DrawFoldoutHeader(ref _maintenanceExpanded, "Source Maintenance", UtilityWindowTheme.Amber, "selected sources");
                if (!_maintenanceExpanded)
                    return;

                EditorGUILayout.LabelField("These actions modify the assigned source assets. They are undoable and mark the assets dirty.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Normalize Source Lists"))
                        NormalizeSources(false);

                    if (GUILayout.Button("Normalize + Sort Source Lists"))
                        NormalizeSources(true);

                    if (GUILayout.Button("Remove Source Duplicates"))
                        RemoveSourceDuplicates();
                }
            }
        }

        private void DrawFoldoutHeader(ref bool expanded, string title, Color tint, string pill)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, title, true, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(pill, tint);
            }
        }

        private void Generate()
        {
            if (!_appendResults)
                _results.Clear();

            System.Random rng = _useSeed ? new System.Random(_seed) : null;
            var seen = new HashSet<string>(_results, StringComparer.OrdinalIgnoreCase);
            int generated = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(_count * 40, 100);

            while (generated < _count && attempts < maxAttempts)
            {
                attempts++;
                string value = GenerateOne(rng);
                value = CleanResult(value);

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (_unique && seen.Contains(value))
                    continue;

                _results.Add(value);
                seen.Add(value);
                generated++;
            }

            if (_sortResults)
                _results.Sort(StringComparer.CurrentCultureIgnoreCase);

            _status = $"Generated {generated} result(s).";
            SavePrefs();
        }

        private string GenerateOne(System.Random rng)
        {
            switch (_mode)
            {
                case GenerationMode.PrefixSuffix:
                    return GenerateFromMadLib(rng);
                case GenerationMode.Template:
                    return GenerateFromTemplate(rng);
                default:
                    return GenerateFromLists(rng);
            }
        }

        private string GenerateFromLists(System.Random rng)
        {
            List<NameList> valid = GetValidNameLists();
            if (valid.Count == 0)
                return string.Empty;

            if (valid.Count == 1)
                return valid[0].GetRandomName(rng);

            var parts = new List<string>();
            for (int i = 0; i < valid.Count; i++)
            {
                string part = valid[i].GetRandomName(rng);
                if (!string.IsNullOrWhiteSpace(part))
                    parts.Add(part);
            }

            return string.Join(_separator ?? " ", parts.ToArray());
        }

        private string GenerateFromMadLib(System.Random rng)
        {
            List<MadLibNameList> valid = GetValidMadLibLists();
            if (valid.Count == 0)
                return string.Empty;

            int index = rng != null ? rng.Next(0, valid.Count) : UnityEngine.Random.Range(0, valid.Count);
            return valid[index].GenerateName(rng);
        }

        private string GenerateFromTemplate(System.Random rng)
        {
            string output = string.IsNullOrWhiteSpace(_template) ? "{first} {last}" : _template;
            for (int i = 0; i < _tokenBindings.Count; i++)
            {
                TokenBinding binding = _tokenBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.token) || binding.source == null)
                    continue;

                string token = "{" + binding.token.Trim().Trim('{', '}') + "}";
                output = output.Replace(token, binding.source.GetRandomName(rng));
            }

            return output;
        }

        private string CleanResult(string value)
        {
            if (value == null)
                return string.Empty;

            if (_trimResults)
                value = CollapseWhitespace(value.Trim());

            if (_titleCaseResults)
                value = ToTitleCase(value);

            return value;
        }

        private void SaveResultsAsNameListAsset()
        {
            if (_results.Count == 0)
            {
                EditorUtility.DisplayDialog("Name Generator", "There are no generated results to save.", "OK");
                return;
            }

            string folder = string.IsNullOrWhiteSpace(_assetSaveFolder) ? "Assets" : _assetSaveFolder;
            if (!AssetDatabase.IsValidFolder(folder))
                folder = "Assets";

            string path = EditorUtility.SaveFilePanelInProject("Save Generated Name List", "Generated Name List", "asset", "Choose where to save the generated names.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            NameList asset = CreateInstance<NameList>();
            asset.displayName = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.category = "Generated";
            asset.nameList = new List<string>(_results);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            _status = "Saved generated NameList asset.";
        }

        private void PickSaveFolder()
        {
            string absolute = EditorUtility.OpenFolderPanel("Name List Save Folder", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute))
                return;

            absolute = absolute.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (absolute.StartsWith(dataPath, StringComparison.Ordinal))
                _assetSaveFolder = "Assets" + absolute.Substring(dataPath.Length);
            else
                EditorUtility.DisplayDialog("Invalid Folder", "Please choose a folder inside this Unity project's Assets folder.", "OK");
        }

        private void AddSelectedAssets()
        {
            AddSelectedNameListsOnly();
            AddSelectedMadLibListsOnly();
        }

        private void AddSelectedNameListsOnly()
        {
            foreach (UnityEngine.Object obj in Selection.objects)
            {
                NameList list = obj as NameList;
                if (list != null && !_sourceLists.Contains(list))
                    _sourceLists.Add(list);
            }

            _status = "Added selected NameList assets.";
        }

        private void AddSelectedMadLibListsOnly()
        {
            foreach (UnityEngine.Object obj in Selection.objects)
            {
                MadLibNameList list = obj as MadLibNameList;
                if (list != null && !_madLibSources.Contains(list))
                    _madLibSources.Add(list);
            }

            _status = "Added selected Mad-Lib assets.";
        }

        private void FindProjectLists()
        {
            int added = 0;
            string[] nameGuids = AssetDatabase.FindAssets("t:NameList");
            foreach (string guid in nameGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                NameList list = AssetDatabase.LoadAssetAtPath<NameList>(path);
                if (list != null && !_sourceLists.Contains(list))
                {
                    _sourceLists.Add(list);
                    added++;
                }
            }

            string[] madLibGuids = AssetDatabase.FindAssets("t:MadLibNameList");
            foreach (string guid in madLibGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MadLibNameList list = AssetDatabase.LoadAssetAtPath<MadLibNameList>(path);
                if (list != null && !_madLibSources.Contains(list))
                {
                    _madLibSources.Add(list);
                    added++;
                }
            }

            _status = $"Found {added} new source asset(s).";
        }

        private void ClearSources()
        {
            _sourceLists.Clear();
            _madLibSources.Clear();
            _status = "Cleared sources.";
        }

        private void NormalizeSources(bool sort)
        {
            int changed = 0;
            foreach (NameList list in _sourceLists)
            {
                if (list == null)
                    continue;
                Undo.RecordObject(list, "Normalize Name List");
                changed += list.NormalizeNames(true, true, sort);
                EditorUtility.SetDirty(list);
            }

            foreach (MadLibNameList list in _madLibSources)
            {
                if (list == null)
                    continue;
                Undo.RecordObject(list, "Normalize Mad-Lib Name List");
                changed += list.NormalizeAll(sort);
                EditorUtility.SetDirty(list);
            }

            AssetDatabase.SaveAssets();
            _status = $"Normalized {changed} entr{(changed == 1 ? "y" : "ies")}.";
        }

        private void RemoveSourceDuplicates()
        {
            int removed = 0;
            foreach (NameList list in _sourceLists)
            {
                if (list == null)
                    continue;
                Undo.RecordObject(list, "Remove Duplicate Names");
                removed += list.RemoveDuplicateNames(true, true);
                EditorUtility.SetDirty(list);
            }

            foreach (MadLibNameList list in _madLibSources)
            {
                if (list == null)
                    continue;
                Undo.RecordObject(list, "Remove Duplicate Mad-Lib Names");
                removed += list.RemoveDuplicateNames();
                EditorUtility.SetDirty(list);
            }

            AssetDatabase.SaveAssets();
            _status = $"Removed {removed} duplicate entr{(removed == 1 ? "y" : "ies")}.";
        }

        private List<NameList> GetValidNameLists()
        {
            var valid = new List<NameList>();
            foreach (NameList list in _sourceLists)
            {
                if (list != null && list.HasNames)
                    valid.Add(list);
            }
            return valid;
        }

        private List<MadLibNameList> GetValidMadLibLists()
        {
            var valid = new List<MadLibNameList>();
            foreach (MadLibNameList list in _madLibSources)
            {
                if (list != null && SafeCount(list.prefixList) > 0 && SafeCount(list.suffixList) > 0)
                    valid.Add(list);
            }
            return valid;
        }

        private bool PassesFilter(string value)
        {
            return string.IsNullOrWhiteSpace(_filter) || (!string.IsNullOrEmpty(value) && value.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int SafeCount<T>(List<T> list) => list == null ? 0 : list.Count;

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            bool previousWhitespace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    if (!previousWhitespace)
                        builder.Append(' ');
                    previousWhitespace = true;
                }
                else
                {
                    builder.Append(c);
                    previousWhitespace = false;
                }
            }
            return builder.ToString();
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 1)
                    parts[i] = char.ToUpperInvariant(part[0]).ToString();
                else
                    parts[i] = char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant();
            }
            return string.Join(" ", parts);
        }

        private void LoadPrefs()
        {
            _mode = (GenerationMode)UtilityWindowPrefs.GetInt(PrefPrefix + "Mode", 0);
            _count = UtilityWindowPrefs.GetInt(PrefPrefix + "Count", 25);
            _seed = UtilityWindowPrefs.GetInt(PrefPrefix + "Seed", 12345);
            _useSeed = UtilityWindowPrefs.GetBool(PrefPrefix + "UseSeed", false);
            _unique = UtilityWindowPrefs.GetBool(PrefPrefix + "Unique", true);
            _trimResults = UtilityWindowPrefs.GetBool(PrefPrefix + "TrimResults", true);
            _titleCaseResults = UtilityWindowPrefs.GetBool(PrefPrefix + "TitleCaseResults", false);
            _sortResults = UtilityWindowPrefs.GetBool(PrefPrefix + "SortResults", false);
            _appendResults = UtilityWindowPrefs.GetBool(PrefPrefix + "AppendResults", false);
            _template = UtilityWindowPrefs.GetString(PrefPrefix + "Template", "{first} {last}");
            _separator = UtilityWindowPrefs.GetString(PrefPrefix + "Separator", " ");
            _assetSaveFolder = UtilityWindowPrefs.GetString(PrefPrefix + "AssetSaveFolder", "Assets");
            _sourcesExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "SourcesExpanded", true);
            _generationExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "GenerationExpanded", true);
            _resultsExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "ResultsExpanded", true);
            _maintenanceExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "MaintenanceExpanded", false);
            _sourcesHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "SourcesHeight", 210f);
            _generationHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "GenerationHeight", 190f);
            _resultsHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "ResultsHeight", 300f);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetInt(PrefPrefix + "Mode", (int)_mode);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Count", _count);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Seed", _seed);
            UtilityWindowPrefs.SetBool(PrefPrefix + "UseSeed", _useSeed);
            UtilityWindowPrefs.SetBool(PrefPrefix + "Unique", _unique);
            UtilityWindowPrefs.SetBool(PrefPrefix + "TrimResults", _trimResults);
            UtilityWindowPrefs.SetBool(PrefPrefix + "TitleCaseResults", _titleCaseResults);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SortResults", _sortResults);
            UtilityWindowPrefs.SetBool(PrefPrefix + "AppendResults", _appendResults);
            UtilityWindowPrefs.SetString(PrefPrefix + "Template", _template);
            UtilityWindowPrefs.SetString(PrefPrefix + "Separator", _separator);
            UtilityWindowPrefs.SetString(PrefPrefix + "AssetSaveFolder", _assetSaveFolder);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SourcesExpanded", _sourcesExpanded);
            UtilityWindowPrefs.SetBool(PrefPrefix + "GenerationExpanded", _generationExpanded);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ResultsExpanded", _resultsExpanded);
            UtilityWindowPrefs.SetBool(PrefPrefix + "MaintenanceExpanded", _maintenanceExpanded);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "SourcesHeight", _sourcesHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "GenerationHeight", _generationHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "ResultsHeight", _resultsHeight);
        }
    }
    #endif

}