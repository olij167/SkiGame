using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.PreviewExport
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    #if TMP_PRESENT
    using TMPro;
    #endif
    using UnityEditor;
    using UnityEngine;

    public class FontPreviewWindow : EditorWindow
    {
        private const string DefaultPreviewText = "The quick brown fox jumps over the lazy dog 0123456789";
        private const string PrefPrefix = "GenericUtilities.FontPreview.";
        private const int MaxPreviewBuildsPerUpdate = 2;
        private const int MaxPreviewCacheEntries = 96;
        private const double PreviewRefreshDelay = 0.2d;
        private const float CardSpacing = 6f;
        private const int PreviewWidthBucket = 64;

        [Serializable]
        private class FontEntry
        {
            public string guid;
            public string assetPath;
            public string displayName;
            public UnityEngine.Object asset;
            public bool isTMP;
        }

        private readonly List<FontEntry> allFonts = new();
        private readonly List<FontEntry> filteredFonts = new();
        private readonly Dictionary<string, Texture2D> previewCache = new();
        private readonly Dictionary<string, int> previewCacheAccessOrder = new();
        private readonly Queue<(FontEntry entry, int width, int height, int generation)> previewBuildQueue = new();
        private readonly HashSet<string> queuedPreviewKeys = new();

        private string searchQuery = string.Empty;
        private string previewText = DefaultPreviewText;
        private int previewFontSize = 36;
        private float previewHeight = 96f;
        private Color previewTextColor = Color.white;
        private Color previewBackgroundColor = new Color(0.14f, 0.14f, 0.14f, 1f);

        private Vector2 scrollPosition;
        private Vector2 controlsScrollPosition;
        private float previewSettingsPanelHeight = 210f;
        private bool sortAscending = true;
        private bool showUnityFonts = true;
        private bool showTMPFonts = true;
        private double nextPreviewRefreshTime;
        private bool previewSettingsDirty;
        private int previewGenerationId;
        private int previewTouchCounter;
        private string status = "Ready";

        private GUIStyle headerStyle;
        private GUIStyle metaStyle;
        private GUIStyle pathStyle;

        private float TotalRowHeight => previewHeight + 56f + CardSpacing;

        [MenuItem("Tools/Utilities/Assets/Font Preview")]
        public static void ShowWindow()
        {
            FontPreviewWindow window = GetWindow<FontPreviewWindow>("Font Preview");
            window.minSize = new Vector2(600f, 420f);
            window.Show();
        }

        [MenuItem("Tools/Font Preview", priority = 9000)]
        public static void ShowLegacyWindow()
        {
            ShowWindow();
        }

        private void OnEnable()
        {
            LoadPrefs();
            RefreshFontList();
        }

        private void OnDisable()
        {
            SavePrefs();
            ClearPreviewCache();
        }

        private void Update()
        {
            if (previewSettingsDirty && EditorApplication.timeSinceStartup >= nextPreviewRefreshTime)
            {
                previewSettingsDirty = false;
                RebuildAllPreviews();
            }

            int buildsThisFrame = 0;
            while (previewBuildQueue.Count > 0 && buildsThisFrame < MaxPreviewBuildsPerUpdate)
            {
                var job = previewBuildQueue.Dequeue();
                string key = BuildCacheKey(job.entry, job.width, job.height);

                if (job.generation != previewGenerationId)
                {
                    queuedPreviewKeys.Remove(key);
                    continue;
                }

                if (!previewCache.ContainsKey(key))
                {
                    Texture2D built = BuildPreviewTexture(job.entry, job.width, job.height);
                    if (built != null)
                    {
                        previewCache[key] = built;
                        TouchPreviewKey(key);
                        TrimPreviewCacheIfNeeded();
                    }
                }
                else
                {
                    TouchPreviewKey(key);
                }

                queuedPreviewKeys.Remove(key);
                buildsThisFrame++;
            }

            if (buildsThisFrame > 0)
                Repaint();
        }

        private void BuildStyles()
        {
            UtilityWindowTheme.EnsureStyles();

            headerStyle = new GUIStyle(UtilityWindowTheme.CardLabelStyle)
            {
                fontSize = 12,
                wordWrap = false
            };

            metaStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };

            pathStyle = new GUIStyle(UtilityWindowTheme.PathLabelStyle)
            {
                wordWrap = false,
                clipping = TextClipping.Clip
            };
        }

        private void OnGUI()
        {
            if (headerStyle == null || metaStyle == null || pathStyle == null)
            {
                BuildStyles();
                if (headerStyle == null || metaStyle == null || pathStyle == null)
                    return;
            }

            UtilityWindowTheme.Header(
                "Font Preview",
                "Browse, filter, and visually compare Unity Font and TextMeshPro font assets with a shared preview string.",
                status);

            DrawToolbar();
            DrawControls();
            UtilityWindowTheme.VerticalResizeHandle(
                ref previewSettingsPanelHeight,
                118f,
                Mathf.Max(118f, position.height - 250f),
                SavePrefs,
                "Drag to resize the preview settings panel.");
            DrawList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.20f, 0.10f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Refresh Fonts", UtilityWindowTheme.Blue, GUILayout.Width(104f)))
                        RefreshFontList();

                    if (UtilityWindowTheme.TintedButton("Reset Text", UtilityWindowTheme.Neutral, GUILayout.Width(84f)))
                    {
                        previewText = DefaultPreviewText;
                        SavePrefs();
                        RebuildAllPreviews();
                    }

                    if (UtilityWindowTheme.TintedButton(sortAscending ? "A → Z" : "Z → A", UtilityWindowTheme.Purple, GUILayout.Width(64f)))
                    {
                        sortAscending = !sortAscending;
                        SavePrefs();
                        ApplyFilters();
                    }

                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField("Search", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(44f));

                    string newSearch = GUILayout.TextField(searchQuery, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(180f));
                    if (newSearch != searchQuery)
                    {
                        searchQuery = newSearch;
                        SavePrefs();
                        ApplyFilters();
                    }

                    if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? EditorStyles.toolbarButton, GUILayout.Width(20f)))
                    {
                        if (!string.IsNullOrEmpty(searchQuery))
                        {
                            searchQuery = string.Empty;
                            GUI.FocusControl(null);
                            SavePrefs();
                            ApplyFilters();
                        }
                    }

                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"Shown: {filteredFonts.Count}", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill($"Total: {allFonts.Count}", UtilityWindowTheme.Teal);
                }
            }
        }

        private void DrawControls()
        {
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.18f, 0.09f), GUILayout.Height(previewSettingsPanelHeight)))
            {
                UtilityWindowTheme.SectionTitle("Preview Settings", UtilityWindowTheme.Teal, $"Cache: {previewCache.Count}/{MaxPreviewCacheEntries}");

                controlsScrollPosition = EditorGUILayout.BeginScrollView(controlsScrollPosition);

                previewText = EditorGUILayout.TextArea(previewText, GUILayout.MinHeight(50f));
                previewFontSize = EditorGUILayout.IntSlider(new GUIContent("Font Size", "Preview font size. Large values cost more to render."), previewFontSize, 12, 96);
                previewHeight = EditorGUILayout.Slider(new GUIContent("Preview Height", "Height of each generated preview texture."), previewHeight, 56f, 180f);
                previewTextColor = EditorGUILayout.ColorField(new GUIContent("Text Color", "Text color used in generated previews."), previewTextColor);
                previewBackgroundColor = EditorGUILayout.ColorField(new GUIContent("Background", "Background color used in generated previews."), previewBackgroundColor);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool unityFonts = showUnityFonts;
                    bool tmpFonts = showTMPFonts;
                    UtilityWindowTheme.ToolbarToggle(ref unityFonts, "Unity Fonts", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.ToolbarToggle(ref tmpFonts, "TMP Fonts", UtilityWindowTheme.Purple);
                    showUnityFonts = unityFonts;
                    showTMPFonts = tmpFonts;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Previews are queued and rendered incrementally to avoid editor stalls.", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                EditorGUILayout.EndScrollView();
            }

            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
                ApplyFilters();
                previewSettingsDirty = true;
                nextPreviewRefreshTime = EditorApplication.timeSinceStartup + PreviewRefreshDelay;
                status = "Preview settings changed";
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.14f, 0.07f)))
            {
                UtilityWindowTheme.SectionTitle("Fonts", UtilityWindowTheme.Blue, filteredFonts.Count == 0 ? "No matches" : $"{filteredFonts.Count} visible");

                if (filteredFonts.Count == 0)
                {
                    EditorGUILayout.HelpBox("No matching fonts found in the project.", MessageType.Info);
                    return;
                }

                float rowHeight = TotalRowHeight;
                float totalHeight = filteredFonts.Count * rowHeight;

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                Rect viewport = GUILayoutUtility.GetRect(0, totalHeight, GUILayout.ExpandWidth(true));

                float viewTop = scrollPosition.y;
                float viewBottom = scrollPosition.y + position.height;

                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(viewTop / rowHeight) - 2);
                int lastVisible = Mathf.Min(filteredFonts.Count - 1, Mathf.CeilToInt(viewBottom / rowHeight) + 2);

                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    Rect rowRect = new Rect(viewport.x, viewport.y + i * rowHeight, viewport.width, rowHeight - CardSpacing);
                    DrawFontCardAbsolute(filteredFonts[i], rowRect);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFontCardAbsolute(FontEntry entry, Rect outerRect)
        {
            GUI.Box(outerRect, GUIContent.none, UtilityWindowTheme.PanelStyle(entry.isTMP ? UtilityWindowTheme.Purple : UtilityWindowTheme.Blue, 0.12f, 0.06f, 6, 2));

            Rect contentRect = new Rect(outerRect.x + 7f, outerRect.y + 6f, outerRect.width - 14f, outerRect.height - 12f);
            Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width - 244f, 18f);
            Rect typeRect = new Rect(contentRect.xMax - 238f, contentRect.y, 44f, 18f);
            Rect selectRect = new Rect(contentRect.xMax - 190f, contentRect.y - 1f, 48f, 20f);
            Rect pingRect = new Rect(contentRect.xMax - 138f, contentRect.y - 1f, 42f, 20f);
            Rect copyNameRect = new Rect(contentRect.xMax - 92f, contentRect.y - 1f, 42f, 20f);
            Rect copyPathRect = new Rect(contentRect.xMax - 46f, contentRect.y - 1f, 46f, 20f);
            Rect pathRect = new Rect(contentRect.x, contentRect.y + 20f, contentRect.width, 16f);
            Rect previewRect = new Rect(contentRect.x, contentRect.y + 40f, contentRect.width, previewHeight);

            GUI.Label(headerRect, entry.displayName, headerStyle);
            GUI.Label(typeRect, entry.isTMP ? "TMP" : "Font", metaStyle);

            if (GUI.Button(selectRect, "Select"))
                Selection.activeObject = entry.asset;

            if (GUI.Button(pingRect, "Ping"))
            {
                Selection.activeObject = entry.asset;
                EditorGUIUtility.PingObject(entry.asset);
            }

            if (GUI.Button(copyNameRect, "Name"))
            {
                EditorGUIUtility.systemCopyBuffer = entry.displayName;
                status = "Copied font name";
            }

            if (GUI.Button(copyPathRect, "Path"))
            {
                EditorGUIUtility.systemCopyBuffer = entry.assetPath;
                status = "Copied font path";
            }

            GUI.Label(pathRect, entry.assetPath, pathStyle);
            DrawPreviewTexture(entry, previewRect);
        }

        private void DrawPreviewTexture(FontEntry entry, Rect rect)
        {
            EditorGUI.DrawRect(rect, previewBackgroundColor);

            Texture2D preview = GetOrBuildPreview(entry, Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));
            if (preview != null)
                GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, true);
            else
                EditorGUI.LabelField(rect, "Preview queued...", EditorStyles.centeredGreyMiniLabel);

            GUI.Box(rect, GUIContent.none);
        }

        private void RefreshFontList()
        {
            allFonts.Clear();
            HashSet<string> seen = new();

            foreach (string guid in AssetDatabase.FindAssets("t:Font"))
            {
                if (!seen.Add(guid))
                    continue;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null)
                    continue;

                allFonts.Add(new FontEntry
                {
                    guid = guid,
                    assetPath = path,
                    displayName = font.name,
                    asset = font,
                    isTMP = false
                });
            }

            #if TMP_PRESENT
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                if (!seen.Add(guid))
                    continue;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                    continue;

                allFonts.Add(new FontEntry
                {
                    guid = guid,
                    assetPath = path,
                    displayName = font.name,
                    asset = font,
                    isTMP = true
                });
            }
            #endif

            ApplyFilters();
            RebuildAllPreviews();
            status = $"Loaded {allFonts.Count} font asset(s)";
        }

        private void ApplyFilters()
        {
            filteredFonts.Clear();
            IEnumerable<FontEntry> query = allFonts;

            if (!showUnityFonts)
                query = query.Where(f => f.isTMP);

            if (!showTMPFonts)
                query = query.Where(f => !f.isTMP);

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string lower = searchQuery.Trim().ToLowerInvariant();
                query = query.Where(f =>
                    (!string.IsNullOrEmpty(f.displayName) && f.displayName.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(f.assetPath) && f.assetPath.ToLowerInvariant().Contains(lower)));
            }

            query = sortAscending
                ? query.OrderBy(f => f.displayName, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(f => f.displayName, StringComparer.OrdinalIgnoreCase);

            filteredFonts.AddRange(query);
            Repaint();
        }

        private void RebuildAllPreviews()
        {
            foreach (Texture2D tex in previewCache.Values)
            {
                if (tex != null)
                    DestroyImmediate(tex);
            }

            previewCache.Clear();
            previewCacheAccessOrder.Clear();
            previewBuildQueue.Clear();
            queuedPreviewKeys.Clear();
            previewGenerationId++;
            Repaint();
        }

        private void ClearPreviewCache()
        {
            foreach (Texture2D tex in previewCache.Values)
            {
                if (tex != null)
                    DestroyImmediate(tex);
            }

            previewCache.Clear();
            previewCacheAccessOrder.Clear();
            previewBuildQueue.Clear();
            queuedPreviewKeys.Clear();
            previewGenerationId++;
        }

        private string BuildCacheKey(FontEntry entry, int width, int height)
        {
            return $"{entry.guid}_{width}_{height}_{previewFontSize}_{Mathf.RoundToInt(previewHeight)}_{(previewText != null ? previewText.GetHashCode() : 0)}_{ColorUtility.ToHtmlStringRGBA(previewTextColor)}_{ColorUtility.ToHtmlStringRGBA(previewBackgroundColor)}";
        }

        private Texture2D GetOrBuildPreview(FontEntry entry, int width, int height)
        {
            width = QuantizePreviewWidth(Mathf.Max(64, width));
            height = Mathf.Max(48, height);

            string key = BuildCacheKey(entry, width, height);
            if (previewCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                TouchPreviewKey(key);
                return cached;
            }

            if (!queuedPreviewKeys.Contains(key))
            {
                queuedPreviewKeys.Add(key);
                previewBuildQueue.Enqueue((entry, width, height, previewGenerationId));
            }

            return null;
        }

        private void TrimPreviewCacheIfNeeded()
        {
            if (previewCache.Count <= MaxPreviewCacheEntries)
                return;

            int removeCount = previewCache.Count - MaxPreviewCacheEntries;
            foreach (string key in previewCacheAccessOrder.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList())
            {
                if (removeCount <= 0)
                    break;

                if (previewCache.TryGetValue(key, out Texture2D tex) && tex != null)
                    DestroyImmediate(tex);

                previewCache.Remove(key);
                previewCacheAccessOrder.Remove(key);
                queuedPreviewKeys.Remove(key);
                removeCount--;
            }
        }

        private void TouchPreviewKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            previewTouchCounter++;
            previewCacheAccessOrder[key] = previewTouchCounter;
        }

        private static int QuantizePreviewWidth(int width)
        {
            return Mathf.Max(64, Mathf.CeilToInt(width / (float)PreviewWidthBucket) * PreviewWidthBucket);
        }

        private Texture2D BuildPreviewTexture(FontEntry entry, int width, int height)
        {
            string safeText = string.IsNullOrWhiteSpace(previewText) ? DefaultPreviewText : previewText;
            PreviewRenderUtility previewUtility = new PreviewRenderUtility();
            previewUtility.camera.orthographic = true;
            previewUtility.camera.orthographicSize = 2.2f;
            previewUtility.camera.nearClipPlane = 0.1f;
            previewUtility.camera.farClipPlane = 100f;
            previewUtility.camera.transform.position = new Vector3(0f, 0f, -10f);
            previewUtility.camera.transform.rotation = Quaternion.identity;
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.backgroundColor = previewBackgroundColor;

            GameObject root = new GameObject("FontPreviewRoot");
            root.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                if (entry.isTMP)
                {
                    #if TMP_PRESENT
                    TMP_FontAsset tmpFont = entry.asset as TMP_FontAsset;
                    if (tmpFont == null)
                        return null;

                    GameObject go = new GameObject("TMPPreview");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    go.transform.SetParent(root.transform, false);

                    TextMeshPro tmp = go.AddComponent<TextMeshPro>();
                    tmp.font = tmpFont;
                    tmp.fontSharedMaterial = tmpFont.material;
                    tmp.text = safeText;
                    tmp.fontSize = previewFontSize;
                    tmp.color = previewTextColor;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableWordWrapping = true;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.rectTransform.sizeDelta = new Vector2(8.0f, 3.0f);

                    MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    #else
                    return null;
                    #endif
                }
                else
                {
                    Font font = entry.asset as Font;
                    if (font == null)
                        return null;

                    GameObject go = new GameObject("FontPreview");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    go.transform.SetParent(root.transform, false);

                    TextMesh textMesh = go.AddComponent<TextMesh>();
                    textMesh.font = font;
                    textMesh.text = safeText;
                    textMesh.fontSize = 100;
                    textMesh.characterSize = Mathf.Max(0.02f, previewFontSize / 480f);
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.color = previewTextColor;

                    MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        if (font.material != null)
                            renderer.sharedMaterial = font.material;
                    }
                }

                previewUtility.AddSingleGO(root);

                Rect previewRect = new Rect(0, 0, width, height);
                previewUtility.BeginPreview(previewRect, GUIStyle.none);
                previewUtility.Render();
                Texture result = previewUtility.EndPreview();

                Texture2D finalTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = result as RenderTexture;
                finalTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                finalTexture.Apply();
                RenderTexture.active = previous;

                finalTexture.hideFlags = HideFlags.HideAndDontSave;
                return finalTexture;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Font preview failed for {entry.displayName}: {e.Message}");
                return null;
            }
            finally
            {
                previewUtility.Cleanup();
                if (root != null)
                    DestroyImmediate(root);
            }
        }

        private void LoadPrefs()
        {
            searchQuery = UtilityWindowPrefs.GetString(PrefPrefix + "Search", string.Empty);
            previewText = UtilityWindowPrefs.GetString(PrefPrefix + "PreviewText", DefaultPreviewText);
            previewFontSize = UtilityWindowPrefs.GetInt(PrefPrefix + "FontSize", previewFontSize);
            previewHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "PreviewHeight", previewHeight);
            previewTextColor = UtilityWindowPrefs.GetColor(PrefPrefix + "TextColor", previewTextColor);
            previewBackgroundColor = UtilityWindowPrefs.GetColor(PrefPrefix + "BackgroundColor", previewBackgroundColor);
            sortAscending = UtilityWindowPrefs.GetBool(PrefPrefix + "SortAscending", sortAscending);
            showUnityFonts = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowUnityFonts", showUnityFonts);
            showTMPFonts = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowTMPFonts", showTMPFonts);
            previewSettingsPanelHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "PreviewSettingsPanelHeight", previewSettingsPanelHeight);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefPrefix + "Search", searchQuery);
            UtilityWindowPrefs.SetString(PrefPrefix + "PreviewText", previewText);
            UtilityWindowPrefs.SetInt(PrefPrefix + "FontSize", previewFontSize);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "PreviewHeight", previewHeight);
            UtilityWindowPrefs.SetColor(PrefPrefix + "TextColor", previewTextColor);
            UtilityWindowPrefs.SetColor(PrefPrefix + "BackgroundColor", previewBackgroundColor);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SortAscending", sortAscending);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowUnityFonts", showUnityFonts);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowTMPFonts", showTMPFonts);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "PreviewSettingsPanelHeight", previewSettingsPanelHeight);
        }
    }
    #endif

}
