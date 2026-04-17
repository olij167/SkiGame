#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public class FontPreviewWindow : EditorWindow
{
    private const string DefaultPreviewText = "The quick brown fox jumps over the lazy dog 0123456789";

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

    private string searchQuery = string.Empty;
    private string previewText = DefaultPreviewText;
    private int previewFontSize = 36;
    private float previewHeight = 96f;
    private Color previewTextColor = Color.white;
    private Color previewBackgroundColor = new Color(0.14f, 0.14f, 0.14f, 1f);

    private Vector2 scrollPosition;
    private bool sortAscending = true;
    private bool showUnityFonts = true;
    private bool showTMPFonts = true;

    private GUIStyle headerStyle;
    private GUIStyle metaStyle;
    private GUIStyle pathStyle;
    private GUIStyle toolbarSearchStyle;

    [MenuItem("Tools/Font Preview")]
    public static void ShowWindow()
    {
        FontPreviewWindow window = GetWindow<FontPreviewWindow>("Font Preview");
        window.minSize = new Vector2(540f, 360f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshFontList();
    }

    private void OnDisable()
    {
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

            if (!previewCache.ContainsKey(key))
            {
                Texture2D built = BuildPreviewTexture(job.entry, job.width, job.height);
                if (built != null)
                    previewCache[key] = built;
            }

            queuedPreviewKeys.Remove(key);
            buildsThisFrame++;
        }

        if (buildsThisFrame > 0)
            Repaint();
    }

    private void BuildStyles()
    {
        if (EditorStyles.label == null || EditorStyles.boldLabel == null || EditorStyles.miniLabel == null)
            return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            wordWrap = false
        };

        metaStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            richText = true,
            wordWrap = false
        };

        pathStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = false,
            clipping = TextClipping.Clip
        };

        toolbarSearchStyle = GUI.skin != null
            ? new GUIStyle(GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.textField)
            : new GUIStyle(EditorStyles.textField);
    }

    private void OnGUI()
    {
        if (headerStyle == null || metaStyle == null || pathStyle == null || toolbarSearchStyle == null)
        {
            BuildStyles();

            if (headerStyle == null || metaStyle == null || pathStyle == null || toolbarSearchStyle == null)
                return;
        }

        DrawToolbar();
        DrawControls();
        DrawList();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh Fonts", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                RefreshFontList();
            }

            if (GUILayout.Button("Reset Text", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                previewText = DefaultPreviewText;
                RebuildAllPreviews();
            }

            if (GUILayout.Button(sortAscending ? "A→Z" : "Z→A", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                sortAscending = !sortAscending;
                ApplyFilters();
            }

            GUILayout.Space(8);

            string newSearch = GUILayout.TextField(searchQuery, toolbarSearchStyle, GUILayout.MinWidth(180));
            if (newSearch != searchQuery)
            {
                searchQuery = newSearch;
                ApplyFilters();
            }

            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    searchQuery = string.Empty;
                    GUI.FocusControl(null);
                    ApplyFilters();
                }
            }
        }
    }

    private double nextPreviewRefreshTime;
    private bool previewSettingsDirty;
    private const double PreviewRefreshDelay = 0.2d;

    private void DrawControls()
    {
        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview Settings", EditorStyles.boldLabel);

            previewText = EditorGUILayout.TextArea(previewText, GUILayout.MinHeight(50f));
            previewFontSize = EditorGUILayout.IntSlider("Font Size", previewFontSize, 12, 96);
            previewHeight = EditorGUILayout.Slider("Preview Height", previewHeight, 56f, 180f);
            previewTextColor = EditorGUILayout.ColorField("Text Color", previewTextColor);
            previewBackgroundColor = EditorGUILayout.ColorField("Background", previewBackgroundColor);

            using (new EditorGUILayout.HorizontalScope())
            {
                showUnityFonts = EditorGUILayout.ToggleLeft("Show Unity Fonts", showUnityFonts, GUILayout.Width(130));
                showTMPFonts = EditorGUILayout.ToggleLeft("Show TMP Fonts", showTMPFonts, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Found: {filteredFonts.Count}", metaStyle, GUILayout.Width(70));
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            ApplyFilters();
            previewSettingsDirty = true;
            nextPreviewRefreshTime = EditorApplication.timeSinceStartup + PreviewRefreshDelay;
        }
    }

    private const float CardSpacing = 6f;
    private float TotalRowHeight => previewHeight + 52f + CardSpacing;

    private void DrawList()
    {
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

    private void DrawFontCardAbsolute(FontEntry entry, Rect outerRect)
    {
        GUI.Box(outerRect, GUIContent.none, EditorStyles.helpBox);

        Rect contentRect = new Rect(outerRect.x + 6f, outerRect.y + 6f, outerRect.width - 12f, outerRect.height - 12f);

        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width - 90f, 18f);
        Rect typeRect = new Rect(contentRect.xMax - 82f, contentRect.y, 36f, 18f);
        Rect pingRect = new Rect(contentRect.xMax - 42f, contentRect.y - 1f, 42f, 20f);
        Rect pathRect = new Rect(contentRect.x, contentRect.y + 18f, contentRect.width, 16f);
        Rect previewRect = new Rect(contentRect.x, contentRect.y + 36f, contentRect.width, previewHeight);

        GUI.Label(headerRect, entry.displayName, headerStyle);
        GUI.Label(typeRect, entry.isTMP ? "TMP" : "Font", metaStyle);

        if (GUI.Button(pingRect, "Ping"))
        {
            EditorGUIUtility.PingObject(entry.asset);
            Selection.activeObject = entry.asset;
        }

        GUI.Label(pathRect, entry.assetPath, pathStyle);

        DrawPreviewTexture(entry, previewRect);
    }

    private void DrawFontCard(FontEntry entry)
    {
        Rect outerRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(entry.displayName, headerStyle);
            GUILayout.FlexibleSpace();

            string typeLabel = entry.isTMP ? "TMP" : "Font";
            EditorGUILayout.LabelField(typeLabel, metaStyle, GUILayout.Width(36));

            if (GUILayout.Button("Ping", GUILayout.Width(44)))
            {
                EditorGUIUtility.PingObject(entry.asset);
                Selection.activeObject = entry.asset;
            }
        }

        EditorGUILayout.LabelField(entry.assetPath, pathStyle);

        Rect previewRect = GUILayoutUtility.GetRect(10f, 10000f, previewHeight, previewHeight);
        DrawPreviewTexture(entry, previewRect);

        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewTexture(FontEntry entry, Rect rect)
    {
        EditorGUI.DrawRect(rect, previewBackgroundColor);

        Texture2D preview = GetOrBuildPreview(entry, Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));

        if (preview != null)
        {
            GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, true);
        }
        else
        {
            EditorGUI.LabelField(rect, "Preview unavailable", EditorStyles.centeredGreyMiniLabel);
        }

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

        ApplyFilters();
        RebuildAllPreviews();
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
        var keysToRemove = new List<string>();

        foreach (var kvp in previewCache)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value);

            keysToRemove.Add(kvp.Key);
        }

        foreach (string key in keysToRemove)
            previewCache.Remove(key);

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
    }

    private string BuildCacheKey(FontEntry entry, int width, int height)
    {
        return $"{entry.guid}_{width}_{height}_{previewFontSize}_{previewHeight}_{previewText}_{previewTextColor}_{previewBackgroundColor}";
    }

    private readonly Queue<(FontEntry entry, int width, int height)> previewBuildQueue = new();
    private readonly HashSet<string> queuedPreviewKeys = new();
    private const int MaxPreviewBuildsPerUpdate = 2;

    private Texture2D GetOrBuildPreview(FontEntry entry, int width, int height)
    {
        width = Mathf.Max(64, width);
        height = Mathf.Max(48, height);

        string key = BuildCacheKey(entry, width, height);

        if (previewCache.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;

        if (!queuedPreviewKeys.Contains(key))
        {
            queuedPreviewKeys.Add(key);
            previewBuildQueue.Enqueue((entry, width, height));
        }

        return null;
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

                MeshRenderer r = go.GetComponent<MeshRenderer>();
                if (r != null)
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

                MeshRenderer r = go.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (font.material != null)
                        r.sharedMaterial = font.material;
                }
            }

            previewUtility.AddSingleGO(root);

            Rect previewRect = new Rect(0, 0, width, height);
            previewUtility.BeginPreview(previewRect, GUIStyle.none);
            previewUtility.Render();
            Texture result = previewUtility.EndPreview();

            Texture2D finalTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = result as RenderTexture;
            finalTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            finalTexture.Apply();
            RenderTexture.active = null;

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
}
#endif