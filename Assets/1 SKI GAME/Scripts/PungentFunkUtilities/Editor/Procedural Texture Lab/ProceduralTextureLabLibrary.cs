using PungentFunk.Utilities.Generation;
using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private const string TextureDesignerLibraryPath = "ProjectSettings/PungentFunk/TextureDesignerLibrary.json";

        [Serializable]
        private sealed class ProceduralTextureLibrary
        {
            public List<ProceduralTextureProjectRecord> projects = new List<ProceduralTextureProjectRecord>();
        }

        [Serializable]
        private sealed class ProceduralTextureProjectRecord
        {
            public string id;
            public string title;
            public string createdUtc;
            public string updatedUtc;
            public bool archived;
            public string activeIterationId;
            public ProceduralTextureMapIntent intent;
            public List<ProceduralTextureIterationRecord> iterations = new List<ProceduralTextureIterationRecord>();
        }

        [Serializable]
        private sealed class ProceduralTextureIterationRecord
        {
            public string id;
            public string parentId;
            public string label;
            public string createdUtc;
            public TextureDesignWorkflow sourceWorkflow;
            public ProceduralTextureCombinationSettings settings;
            public ProceduralTextureBaseSettings[] bases;
            public int width;
            public int height;
            public float coverage01;
            public float contrast01;
            public float seamScore01;
            public bool flagged;
            public bool archived;
            public bool exported;
            public string exportedAssetPath;
        }

        private ProceduralTextureLibrary _textureLibrary = new ProceduralTextureLibrary();
        private string _activeTextureProjectId;
        private string _lastLibrarySaveStatus = "Library ready.";

        private void LoadTextureLibrary()
        {
            try
            {
                if (File.Exists(TextureDesignerLibraryPath))
                {
                    string json = File.ReadAllText(TextureDesignerLibraryPath);
                    ProceduralTextureLibrary loaded = JsonUtility.FromJson<ProceduralTextureLibrary>(json);
                    if (loaded != null)
                        _textureLibrary = loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Texture Designer library could not be loaded: {exception.Message}");
                _textureLibrary = new ProceduralTextureLibrary();
            }

            EnsureActiveTextureProject();
        }

        private void SaveTextureLibrary()
        {
            try
            {
                EnsureActiveTextureProject();
                string directory = Path.GetDirectoryName(TextureDesignerLibraryPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(TextureDesignerLibraryPath, JsonUtility.ToJson(_textureLibrary, true));
                _lastLibrarySaveStatus = "Library saved.";
            }
            catch (Exception exception)
            {
                _lastLibrarySaveStatus = "Library save failed.";
                Debug.LogWarning($"Texture Designer library could not be saved: {exception.Message}");
            }
        }

        private void AutosaveTextureIteration(string label, bool exported = false, string exportedAssetPath = null)
        {
            EnsureActiveTextureProject();
            ProceduralTextureProjectRecord project = ActiveTextureProject();
            if (project == null)
                return;

            string now = DateTime.UtcNow.ToString("u");
            string parentId = project.activeIterationId;
            var iteration = new ProceduralTextureIterationRecord
            {
                id = Guid.NewGuid().ToString("N"),
                parentId = parentId,
                label = string.IsNullOrWhiteSpace(label) ? "Texture iteration" : label,
                createdUtc = now,
                sourceWorkflow = _activeWorkflow,
                settings = _combination != null ? _combination.Clone() : new ProceduralTextureCombinationSettings(),
                bases = ProceduralTextureCombinationUtility.CloneBases(_bases),
                width = _combination != null ? _combination.width : 0,
                height = _combination != null ? _combination.height : 0,
                coverage01 = _manualOutputCoverage01,
                contrast01 = _manualOutputContrast01,
                seamScore01 = _manualOutputSeamScore01,
                exported = exported,
                exportedAssetPath = exportedAssetPath ?? string.Empty
            };

            project.iterations.Insert(0, iteration);
            project.activeIterationId = iteration.id;
            project.updatedUtc = now;
            project.intent = _mapIntent;
            while (project.iterations.Count > 64)
                project.iterations.RemoveAt(project.iterations.Count - 1);
            SaveTextureLibrary();
        }

        private void EnsureActiveTextureProject()
        {
            if (_textureLibrary == null)
                _textureLibrary = new ProceduralTextureLibrary();
            if (_textureLibrary.projects == null)
                _textureLibrary.projects = new List<ProceduralTextureProjectRecord>();

            ProceduralTextureProjectRecord project = ActiveTextureProject();
            if (project != null)
                return;

            string now = DateTime.UtcNow.ToString("u");
            project = new ProceduralTextureProjectRecord
            {
                id = Guid.NewGuid().ToString("N"),
                title = "Texture Project",
                createdUtc = now,
                updatedUtc = now,
                intent = _mapIntent
            };
            _textureLibrary.projects.Add(project);
            _activeTextureProjectId = project.id;
        }

        private ProceduralTextureProjectRecord ActiveTextureProject()
        {
            if (_textureLibrary?.projects == null)
                return null;
            if (!string.IsNullOrEmpty(_activeTextureProjectId))
            {
                for (int i = 0; i < _textureLibrary.projects.Count; i++)
                {
                    if (_textureLibrary.projects[i] != null && _textureLibrary.projects[i].id == _activeTextureProjectId)
                        return _textureLibrary.projects[i];
                }
            }
            for (int i = 0; i < _textureLibrary.projects.Count; i++)
            {
                if (_textureLibrary.projects[i] != null && !_textureLibrary.projects[i].archived)
                {
                    _activeTextureProjectId = _textureLibrary.projects[i].id;
                    return _textureLibrary.projects[i];
                }
            }
            return null;
        }

        private void DrawTextureLibraryInspector()
        {
            EnsureActiveTextureProject();
            ProceduralTextureProjectRecord project = ActiveTextureProject();
            using (BeginInspectorSection("Library", HistoryTint(), project == null ? "empty" : $"{project.iterations.Count} iteration(s)", TextureButtonTone.Secondary))
            {
                if (project == null)
                {
                    DrawInlineStatus("The internal texture library will be created when you save or interact with a texture.", UtilityWindowTheme.Neutral);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                project.title = EditorGUILayout.TextField("Current Texture", string.IsNullOrWhiteSpace(project.title) ? "Texture Project" : project.title);
                if (EditorGUI.EndChangeCheck())
                {
                    project.updatedUtc = DateTime.UtcNow.ToString("u");
                    SaveTextureLibrary();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Save State", "Save the current interacted texture state to the internal library."), EditorStyles.miniButton))
                        AutosaveTextureIteration("Saved state");
                    if (GUILayout.Button(new GUIContent("New", "Create a new internal texture project."), EditorStyles.miniButton))
                        CreateTextureLibraryProject();
                    if (GUILayout.Button(new GUIContent(project.archived ? "Unarchive" : "Archive", "Archive hides this texture project from the default active project choice."), EditorStyles.miniButton))
                    {
                        project.archived = !project.archived;
                        project.updatedUtc = DateTime.UtcNow.ToString("u");
                        SaveTextureLibrary();
                    }
                }

                DrawInlineStatus($"{_lastLibrarySaveStatus} Autosave stores interacted outputs only; export remains explicit.", UtilityWindowTheme.Neutral);
                int shown = Mathf.Min(3, project.iterations.Count);
                for (int i = 0; i < shown; i++)
                {
                    ProceduralTextureIterationRecord iteration = project.iterations[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(iteration.label, UtilityWindowTheme.MutedMiniLabelStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(ShortTime(iteration.createdUtc), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(88f));
                    }
                }
            }
        }

        private void CreateTextureLibraryProject()
        {
            string now = DateTime.UtcNow.ToString("u");
            var project = new ProceduralTextureProjectRecord
            {
                id = Guid.NewGuid().ToString("N"),
                title = $"Texture Project {_textureLibrary.projects.Count + 1}",
                createdUtc = now,
                updatedUtc = now,
                intent = _mapIntent
            };
            _textureLibrary.projects.Add(project);
            _activeTextureProjectId = project.id;
            SaveTextureLibrary();
            RequestSessionSave();
        }
    }
#endif
}
