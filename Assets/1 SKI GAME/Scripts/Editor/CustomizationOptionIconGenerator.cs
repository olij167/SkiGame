#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class CustomizationOptionIconGenerator
{
    private const int DefaultSize = 256;

    [MenuItem("Assets/SkiGame/Customization/Generate Icons (Selected Options)", priority = 2000)]
    public static void GenerateSelected()
    {
        var options = Selection.GetFiltered<CustomizationOptionSO>(SelectionMode.Assets);
        if (options == null || options.Length == 0)
        {
            EditorUtility.DisplayDialog("Generate Icons", "Select one or more CustomizationOptionSO assets.", "OK");
            return;
        }

        GenerateFor(options);
    }

    [MenuItem("SkiGame/Customization/Generate Icons (All Options)", priority = 2000)]
    public static void GenerateAll()
    {
        var guids = AssetDatabase.FindAssets("t:CustomizationOptionSO");
        var options = new CustomizationOptionSO[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            options[i] = AssetDatabase.LoadAssetAtPath<CustomizationOptionSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

        GenerateFor(options);
    }

    private static void GenerateFor(CustomizationOptionSO[] options)
    {
        int size = DefaultSize;

        foreach (var opt in options)
        {
            if (opt == null) continue;

            // Only prefab-based types requested
            if (opt.type != CustomizationOptionType.Skis &&
                opt.type != CustomizationOptionType.Poles &&
                opt.type != CustomizationOptionType.Hat &&
                opt.type != CustomizationOptionType.Jacket)
                continue;

            var prefab = ResolvePrefab(opt);
            if (prefab == null)
            {
                Debug.LogWarning($"[IconGen] No prefab resolved for {opt.name} ({opt.type}).");
                continue;
            }

            var png = GetPrefabPreviewPng(prefab, size, size);
            if (png == null || png.Length == 0)
            {
                Debug.LogWarning($"[IconGen] Failed to render preview for prefab {prefab.name} (option {opt.name}).");
                continue;
            }

            var optPath = AssetDatabase.GetAssetPath(opt);
            var folder = Path.GetDirectoryName(optPath).Replace('\\', '/');
            var outPath = $"{folder}/{opt.name}_Icon.png";

            // Write PNG
            File.WriteAllBytes(outPath, png);

            // Import & configure as sprite (synchronous)
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            if (importer != null)
            {
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

            // Load sprite and assign via SerializedObject (most reliable)
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
            if (sprite == null)
            {
                // One more sync import pass in case the reimport queued
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
            }

            if (sprite != null)
            {
                var so = new SerializedObject(opt);
                so.FindProperty("icon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(opt);
            }
            else
            {
                Debug.LogWarning($"[IconGen] Failed to load Sprite at: {outPath} (option {opt.name})");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject ResolvePrefab(CustomizationOptionSO opt)
    {
        if (opt == null) return null;

        switch (opt.type)
        {
            case CustomizationOptionType.Hat:
                return opt.hatPrefab;

            case CustomizationOptionType.Jacket:
                return opt.jacketPrefab;

            case CustomizationOptionType.Skis:
                return ResolvePrefabFromGearProfile(opt.gearProfile, preferName: "ski");

            case CustomizationOptionType.Poles:
                return ResolvePrefabFromGearProfile(opt.gearProfile, preferName: "pole");
        }

        return null;
    }

    // Looks for likely prefab fields, then falls back to first GameObject reference.
    private static GameObject ResolvePrefabFromGearProfile(UnityEngine.Object gearProfile, string preferName)
    {
        if (gearProfile == null) return null;

        var so = new SerializedObject(gearProfile);

        string[] preferredProps =
        {
            preferName == "ski" ? "skisPrefab" : "polesPrefab",
            preferName == "ski" ? "skiPrefab" : "polePrefab",
            "prefab",
            "modelPrefab",
            "visualPrefab",
        };

        foreach (var p in preferredProps)
        {
            var sp = so.FindProperty(p);
            if (sp != null && sp.propertyType == SerializedPropertyType.ObjectReference)
            {
                var go = sp.objectReferenceValue as GameObject;
                if (go != null) return go;
            }
        }

        var it = so.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

            var go = it.objectReferenceValue as GameObject;
            if (go != null) return go;
        }

        return null;
    }

    // Uses Unity's project-window preview renderer (AssetPreview) for perfect framing.
    private static byte[] GetPrefabPreviewPng(GameObject prefab, int width, int height)
    {
        if (prefab == null) return null;

        AssetPreview.SetPreviewTextureCacheSize(2048);

        Texture2D previewTex = null;

        // AssetPreview is async; poll briefly.
        const int maxTries = 80;
        for (int i = 0; i < maxTries; i++)
        {
            previewTex = AssetPreview.GetAssetPreview(prefab);
            if (previewTex != null) break;

            if (i > 20)
            {
                // lower fidelity fallback
                previewTex = AssetPreview.GetMiniThumbnail(prefab) as Texture2D;
                if (previewTex != null) break;
            }

            Thread.Sleep(25);
        }

        if (previewTex == null)
            return null;

        // Normalize to requested size (previews vary)
        var scaled = ScaleTexture(previewTex, width, height);

        // Ensure truly transparent pixels stay transparent (no halo background)
        ClearNearTransparentPixels(scaled, 0.02f);

        var bytes = scaled.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(scaled);
        return bytes;
    }

    private static Texture2D ScaleTexture(Texture2D src, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;

        Graphics.Blit(src, rt);
        RenderTexture.active = rt;

        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        dst.Apply(false, false);

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return dst;
    }

    private static void ClearNearTransparentPixels(Texture2D tex, float alphaCutoff)
    {
        if (tex == null) return;

        var pixels = tex.GetPixels32();
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
