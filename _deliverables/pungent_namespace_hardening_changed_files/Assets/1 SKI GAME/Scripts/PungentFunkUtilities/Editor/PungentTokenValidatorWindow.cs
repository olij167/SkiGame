using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentTokenValidatorWindow : EditorWindow
    {
        private sealed class TokenDefinition
        {
            public string key;
            public string description;
            public string previewValue;
        }

        private readonly List<TokenDefinition> _tokens = new List<TokenDefinition>
        {
            new TokenDefinition { key = "playerName", description = "Example user/player name", previewValue = "Alex" },
            new TokenDefinition { key = "objectName", description = "Selected object or target name", previewValue = "Crate" },
            new TokenDefinition { key = "locationName", description = "Location / anchor label", previewValue = "Workshop" }
        };

        private string _text = "Hello {playerName}, inspect {objectName}.";
        private string _newToken = string.Empty;
        private string _scanFilter = "t:TextAsset";
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private readonly List<string> _scanResults = new List<string>();
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/Generation/Token Validator", priority = 1321)]
        public static void Open()
        {
            PungentTokenValidatorWindow window = GetWindow<PungentTokenValidatorWindow>("Token Validator");
            window.minSize = new Vector2(700f, 420f);
            window.Show();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header("Token Validator", "Configure reusable brace tokens, validate strings/assets, and preview token replacement without adding project-specific code.", _status);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSchemaPanel(GUILayout.Width(Mathf.Clamp(position.width * 0.38f, 270f, 380f)));
                DrawValidationPanel(GUILayout.MinWidth(360f));
            }
        }

        private void DrawSchemaPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue), options))
            {
                UtilityWindowTheme.SectionTitle("Token Schema", UtilityWindowTheme.Blue, _tokens.Count + " tokens");
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newToken = EditorGUILayout.TextField(_newToken);
                    if (GUILayout.Button("Add", GUILayout.Width(46f)) && !string.IsNullOrWhiteSpace(_newToken))
                    {
                        string clean = _newToken.Trim().Trim('{', '}');
                        if (_tokens.All(t => t.key != clean))
                            _tokens.Add(new TokenDefinition { key = clean, description = "Custom token", previewValue = clean });
                        _newToken = string.Empty;
                    }
                }

                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                for (int i = 0; i < _tokens.Count; i++)
                {
                    TokenDefinition token = _tokens[i];
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 2)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            token.key = EditorGUILayout.TextField(token.key);
                            if (GUILayout.Button("Copy", GUILayout.Width(46f)))
                                EditorGUIUtility.systemCopyBuffer = "{" + token.key + "}";
                            if (GUILayout.Button("X", GUILayout.Width(24f)))
                            {
                                _tokens.RemoveAt(i);
                                GUIUtility.ExitGUI();
                            }
                        }
                        token.description = EditorGUILayout.TextField("Description", token.description);
                        token.previewValue = EditorGUILayout.TextField("Preview", token.previewValue);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawValidationPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                UtilityWindowTheme.SectionTitle("Validate & Preview", UtilityWindowTheme.Teal);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load Selected TextAsset", GUILayout.Width(150f)))
                        LoadSelectedText();
                    if (GUILayout.Button("Validate Selection Assets", GUILayout.Width(154f)))
                        ValidateObjects(Selection.objects);
                    _scanFilter = EditorGUILayout.TextField(_scanFilter);
                    if (GUILayout.Button("Scan Project", GUILayout.Width(88f)))
                        ScanProject();
                }

                _text = EditorGUILayout.TextArea(_text, GUILayout.MinHeight(120f));
                DrawResults(_text);

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(ResolvePreview(_text), MessageType.None);

                if (_scanResults.Count > 0)
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
                    _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUILayout.MinHeight(80f));
                    foreach (string result in _scanResults.Take(100))
                        EditorGUILayout.LabelField(result, UtilityWindowTheme.PathLabelStyle);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawResults(string text)
        {
            HashSet<string> allowed = new HashSet<string>(_tokens.Select(t => t.key).Where(t => !string.IsNullOrEmpty(t)));
            MatchCollection matches = Regex.Matches(text ?? string.Empty, "\\{([^{}]+)\\}");
            List<string> unknown = new List<string>();
            foreach (Match match in matches)
            {
                string token = match.Groups[1].Value.Trim();
                if (!allowed.Contains(token))
                    unknown.Add(token);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(unknown.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 0.12f, 0.05f)))
            {
                UtilityWindowTheme.SectionTitle("Results", unknown.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, matches.Count + " tokens");
                if (unknown.Count == 0)
                    EditorGUILayout.HelpBox("All discovered tokens are allowed.", MessageType.Info);
                else
                    foreach (string token in unknown.Distinct())
                        EditorGUILayout.LabelField("Unknown: {" + token + "}", UtilityWindowTheme.PathLabelStyle);
            }
        }

        private string ResolvePreview(string text)
        {
            string result = text ?? string.Empty;
            foreach (TokenDefinition token in _tokens)
            {
                if (!string.IsNullOrWhiteSpace(token.key))
                    result = result.Replace("{" + token.key + "}", token.previewValue ?? string.Empty);
            }
            return result;
        }

        private void LoadSelectedText()
        {
            TextAsset asset = Selection.activeObject as TextAsset;
            if (asset == null)
            {
                _status = "Select a TextAsset first.";
                return;
            }
            _text = asset.text;
            _status = "Loaded " + asset.name + ".";
        }

        private void ValidateObjects(Object[] objects)
        {
            _scanResults.Clear();
            foreach (Object obj in objects)
            {
                TextAsset text = obj as TextAsset;
                if (text == null)
                    continue;
                AddValidationResult(AssetDatabase.GetAssetPath(text), text.text);
            }
            _status = "Validated " + _scanResults.Count + " selected result(s).";
        }

        private void ScanProject()
        {
            _scanResults.Clear();
            string[] guids = AssetDatabase.FindAssets(string.IsNullOrWhiteSpace(_scanFilter) ? "t:TextAsset" : _scanFilter);
            foreach (string guid in guids.Take(500))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null)
                    AddValidationResult(path, asset.text);
            }
            _status = "Scanned " + guids.Length + " asset(s).";
        }

        private void AddValidationResult(string path, string text)
        {
            HashSet<string> allowed = new HashSet<string>(_tokens.Select(t => t.key).Where(t => !string.IsNullOrEmpty(t)));
            List<string> unknown = Regex.Matches(text ?? string.Empty, "\\{([^{}]+)\\}").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Where(t => !allowed.Contains(t)).Distinct().ToList();
            if (unknown.Count > 0)
                _scanResults.Add(path + " -> unknown: " + string.Join(", ", unknown));
            else if (Regex.IsMatch(text ?? string.Empty, "\\{([^{}]+)\\}"))
                _scanResults.Add(path + " -> OK");
        }
    }
    #endif

}