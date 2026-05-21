using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.PreviewExport
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Generic, non-blocking prefab preview-to-PNG icon generator. Uses an EditorApplication.update queue instead of Thread.Sleep polling.
    /// </summary>
    public sealed class PrefabIconGeneratorWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtility.PrefabIconGenerator.";
        private const string PrefOutputFolder = PrefPrefix + "OutputFolder";
        private const string PrefSize = PrefPrefix + "Size";
        private const string PrefSuffix = PrefPrefix + "Suffix";
        private const string PrefConfigureSprite = PrefPrefix + "ConfigureSprite";
        private const string PrefOverwrite = PrefPrefix + "Overwrite";

        private string _outputFolder;
        private int _size;
        private string _suffix;
        private bool _configureSprite;
        private bool _overwrite;
        private Vector2 _scroll;
        private double _nextAllowedRepaintTime;

        public static void Open()
        {
            PrefabIconGeneratorWindow window = GetWindow<PrefabIconGeneratorWindow>("Prefab Icons");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            _outputFolder = UtilityWindowPrefs.GetString(PrefOutputFolder, "Assets/Generated Icons");
            _size = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSize, 256), 32, 1024);
            _suffix = UtilityWindowPrefs.GetString(PrefSuffix, "_Icon");
            _configureSprite = UtilityWindowPrefs.GetBool(PrefConfigureSprite, true);
            _overwrite = UtilityWindowPrefs.GetBool(PrefOverwrite, true);
            PrefabIconGeneratorService.StatusChanged += OnServiceStatusChanged;
        }

        private void OnDisable()
        {
            PrefabIconGeneratorService.StatusChanged -= OnServiceStatusChanged;
        }

        private void OnGUI()
        {
            GameObject[] prefabs = GetSelectedPrefabAssets();
            UtilityWindowTheme.Header(
                "Prefab Icon Generator",
                "Generate transparent PNG icons from selected prefab assets using Unity's AssetPreview renderer. Jobs are queued over editor updates so the window does not freeze.",
                PrefabIconGeneratorService.IsRunning ? "generating" : $"{prefabs.Length} selected");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Selected Prefabs", UtilityWindowTheme.Blue, prefabs.Length.ToString());
                if (prefabs.Length == 0)
                    EditorGUILayout.LabelField("Select one or more prefab assets in the Project view.", UtilityWindowTheme.MutedMiniLabelStyle);
                else
                {
                    int shown = Mathf.Min(prefabs.Length, 8);
                    for (int i = 0; i < shown; i++)
                        EditorGUILayout.LabelField(prefabs[i].name, UtilityWindowTheme.CardLabelStyle);
                    if (prefabs.Length > shown)
                        EditorGUILayout.LabelField($"+ {prefabs.Length - shown} more", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("Output", UtilityWindowTheme.Cyan, "persistent");
                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _outputFolder = EditorGUILayout.TextField(new GUIContent("Folder", "Project-relative output folder, for example Assets/Generated Icons."), _outputFolder);
                    if (GUILayout.Button("Pick", GUILayout.Width(48f)))
                        PickOutputFolder();
                }

                _size = EditorGUILayout.IntPopup("Size", _size, new[] { "64", "128", "256", "512", "1024" }, new[] { 64, 128, 256, 512, 1024 });
                _suffix = EditorGUILayout.TextField(new GUIContent("Filename Suffix", "Added after each prefab name before .png."), _suffix);
                _configureSprite = EditorGUILayout.ToggleLeft(new GUIContent("Import As Sprite", "Configure generated PNGs as single sprites with alpha transparency."), _configureSprite);
                _overwrite = EditorGUILayout.ToggleLeft(new GUIContent("Overwrite Existing Files", "When disabled, Unity generates a unique path instead of replacing an existing icon."), _overwrite);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Actions", UtilityWindowTheme.Green, PrefabIconGeneratorService.PendingCount.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(prefabs.Length == 0 || PrefabIconGeneratorService.IsRunning))
                    {
                        if (UtilityWindowTheme.TintedButton("Generate Icons", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                            QueueSelected(prefabs);
                    }

                    using (new EditorGUI.DisabledScope(!PrefabIconGeneratorService.IsRunning))
                    {
                        if (UtilityWindowTheme.TintedButton("Cancel Queue", UtilityWindowTheme.Red, GUILayout.Height(28f), GUILayout.Width(120f)))
                            PrefabIconGeneratorService.Cancel();
                    }
                }

                EditorGUILayout.LabelField(PrefabIconGeneratorService.Status, UtilityWindowTheme.MutedMiniLabelStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnServiceStatusChanged()
        {
            PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(this, ref _nextAllowedRepaintTime, 0.10d);
        }

        private void QueueSelected(GameObject[] prefabs)
        {
            EnsureFolderExists(_outputFolder);
            foreach (GameObject prefab in prefabs)
            {
                string safeName = SanitizeFileName(prefab.name + _suffix);
                string path = $"{_outputFolder.TrimEnd('/')}/{safeName}.png";
                if (!_overwrite)
                    path = AssetDatabase.GenerateUniqueAssetPath(path);

                PrefabIconGeneratorService.Enqueue(new PrefabIconGeneratorService.IconJob(prefab, path, _size, _configureSprite));
            }
        }

        private static GameObject[] GetSelectedPrefabAssets()
        {
            List<GameObject> result = new List<GameObject>();
            foreach (GameObject go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            {
                if (go == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.NotAPrefab)
                    result.Add(go);
            }
            return result.ToArray();
        }

        private void PickOutputFolder()
        {
            string absolute = EditorUtility.OpenFolderPanel("Icon Output Folder", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute))
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(absolute);
            if (!full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Choose a folder inside this Unity project.", "OK");
                return;
            }

            _outputFolder = full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefOutputFolder, _outputFolder);
            UtilityWindowPrefs.SetInt(PrefSize, _size);
            UtilityWindowPrefs.SetString(PrefSuffix, _suffix);
            UtilityWindowPrefs.SetBool(PrefConfigureSprite, _configureSprite);
            UtilityWindowPrefs.SetBool(PrefOverwrite, _overwrite);
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath))
                return;

            assetFolderPath = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            string[] parts = assetFolderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Output folder must be inside Assets.");

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(" ", "_");
        }
    }

    public static class PrefabIconGeneratorService
    {
        private const int MaxAttemptsBeforeMiniFallback = 80;
        private static readonly Queue<IconJob> Jobs = new Queue<IconJob>();
        private static IconJob _current;
        private static int _attempts;

        public static bool IsRunning => _current != null || Jobs.Count > 0;
        public static int PendingCount => Jobs.Count + (_current != null ? 1 : 0);
        public static string Status { get; private set; } = "Idle.";
        public static event Action StatusChanged;

        public sealed class IconJob
        {
            public GameObject Prefab;
            public string OutputPath;
            public int Size;
            public bool ConfigureAsSprite;
            public Action<string> OnCompleted;

            public IconJob(GameObject prefab, string outputPath, int size, bool configureAsSprite, Action<string> onCompleted = null)
            {
                Prefab = prefab;
                OutputPath = outputPath;
                Size = Mathf.Clamp(size, 32, 1024);
                ConfigureAsSprite = configureAsSprite;
                OnCompleted = onCompleted;
            }
        }

        public static void Enqueue(IconJob job)
        {
            if (job == null || job.Prefab == null || string.IsNullOrWhiteSpace(job.OutputPath))
                return;

            Jobs.Enqueue(job);
            SetStatus($"Queued {Jobs.Count} icon job(s).");
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;
        }

        public static void Cancel()
        {
            Jobs.Clear();
            _current = null;
            _attempts = 0;
            SetStatus("Cancelled.");
            EditorUtility.ClearProgressBar();
            EditorApplication.update -= ProcessQueue;
        }

        private static void ProcessQueue()
        {
            if (_current == null)
            {
                if (Jobs.Count == 0)
                {
                    FinishQueue();
                    return;
                }

                _current = Jobs.Dequeue();
                _attempts = 0;
                AssetPreview.SetPreviewTextureCacheSize(2048);
            }

            if (_current.Prefab == null)
            {
                _current = null;
                return;
            }

            float progress = Jobs.Count <= 0 ? 0.9f : 0.5f;
            EditorUtility.DisplayProgressBar("Generating Prefab Icons", _current.Prefab.name, progress);
            SetStatus($"Generating {_current.Prefab.name} ({PendingCount} remaining).");

            Texture2D preview = AssetPreview.GetAssetPreview(_current.Prefab);
            if (preview == null && _attempts >= MaxAttemptsBeforeMiniFallback)
                preview = AssetPreview.GetMiniThumbnail(_current.Prefab) as Texture2D;

            _attempts++;
            if (preview == null)
                return;

            try
            {
                WritePreview(_current, preview);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"Failed: {ex.Message}");
            }

            _current = null;
        }

        private static void FinishQueue()
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetStatus("Idle.");
            EditorApplication.update -= ProcessQueue;
        }

        private static void WritePreview(IconJob job, Texture2D preview)
        {
            Texture2D scaled = ScaleTexture(preview, job.Size, job.Size);
            ClearNearTransparentPixels(scaled, 0.02f);
            File.WriteAllBytes(job.OutputPath, scaled.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(scaled);

            AssetDatabase.ImportAsset(job.OutputPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (job.ConfigureAsSprite)
                ConfigureSpriteImporter(job.OutputPath);

            job.OnCompleted?.Invoke(job.OutputPath);
            Debug.Log($"[PrefabIconGenerator] Wrote {job.OutputPath}", job.Prefab);
            SetStatus($"Generated {Path.GetFileName(job.OutputPath)} ({PendingCount} remaining).");
        }

        private static void SetStatus(string status)
        {
            if (string.Equals(Status, status, StringComparison.Ordinal))
                return;

            Status = status;
            StatusChanged?.Invoke();
        }

        private static void ConfigureSpriteImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Texture2D ScaleTexture(Texture2D src, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            Texture2D dst = new Texture2D(width, height, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            dst.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        private static void ClearNearTransparentPixels(Texture2D tex, float alphaCutoff)
        {
            if (tex == null)
                return;

            Color32[] pixels = tex.GetPixels32();
            byte cut = (byte)(alphaCutoff * 255f);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= cut)
                    pixels[i] = new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }
    }
    #endif

}
