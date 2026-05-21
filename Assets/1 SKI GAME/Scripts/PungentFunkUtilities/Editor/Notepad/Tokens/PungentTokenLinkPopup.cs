namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentTokenLinkPopup : EditorWindow
    {
        private UnityEngine.Object _target;
        private string _propertyPath = string.Empty;
        private string _targetLabel = string.Empty;
        private string _tokenKey = string.Empty;
        private string _newTokenKey = string.Empty;
        private Vector2 _scroll;

        public static void Open(UnityEngine.Object target, string propertyPath = null, string targetLabel = null)
        {
            PungentTokenLinkPopup window = CreateInstance<PungentTokenLinkPopup>();
            window.titleContent = new GUIContent("Link Token");
            window._target = target;
            window._propertyPath = propertyPath ?? string.Empty;
            window._targetLabel = string.IsNullOrWhiteSpace(targetLabel) ? (target != null ? target.name : "Target") : targetLabel;
            window.minSize = new Vector2(420f, 320f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            PungentTokenStorage.EnsureLoaded();
            UtilityWindowTheme.Header("Link Token", "Create metadata-only token bindings for the selected editor target.", _targetLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Target", _targetLabel, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(_propertyPath))
                EditorGUILayout.LabelField("Property", _propertyPath, UtilityWindowTheme.PathLabelStyle);

            _tokenKey = DrawTokenPopup("Token", _tokenKey);
            using (new EditorGUILayout.HorizontalScope())
            {
                _newTokenKey = EditorGUILayout.TextField("Create Token", _newTokenKey);
                if (GUILayout.Button("Create", GUILayout.Width(64f)) && PungentTokenParser.IsValidKey(_newTokenKey))
                {
                    PungentTokenDefinition token = PungentTokenStorage.Database.AddToken(_newTokenKey);
                    _tokenKey = token.key;
                    _newTokenKey = string.Empty;
                }
            }

            var existing = PungentTokenContextResolver.GetBindingsForTarget(_target, _propertyPath);
            UtilityWindowTheme.SectionTitle("Existing Bindings", UtilityWindowTheme.Teal, existing.Count + " links");
            foreach (PungentTokenBinding binding in existing)
                EditorGUILayout.LabelField("{" + binding.tokenKey + "} - " + binding.label, UtilityWindowTheme.PathLabelStyle);

            GUI.enabled = !string.IsNullOrWhiteSpace(_tokenKey);
            if (UtilityWindowTheme.TintedButton("Link Token", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                Link();
            GUI.enabled = true;

            if (GUILayout.Button("Open Token Validator"))
                PungentTokenValidatorWindow.Open();
            EditorGUILayout.EndScrollView();
        }

        private void Link()
        {
            PungentTokenBinding binding = PungentTokenContextResolver.CreateBindingTemplate(_tokenKey, _target, _propertyPath, _targetLabel);
            if (binding == null)
                return;
            if (PungentTokenContextResolver.BindingExists(binding))
            {
                EditorUtility.DisplayDialog("Link Token", "This token is already linked to the target.", "OK");
                return;
            }
            PungentTokenStorage.Database.AddBinding(binding);
            Close();
        }

        private static string DrawTokenPopup(string label, string current)
        {
            var keys = PungentTokenStorage.Database.tokens.Where(t => t != null && !t.archived).Select(t => t.key).OrderBy(k => k).ToList();
            if (keys.Count == 0)
                return string.Empty;
            int index = Math.Max(0, keys.FindIndex(k => string.Equals(k, current, StringComparison.OrdinalIgnoreCase)));
            int next = EditorGUILayout.Popup(label, index, keys.Select(k => "{" + k + "}").ToArray());
            return next >= 0 && next < keys.Count ? keys[next] : string.Empty;
        }
    }
#endif
}
