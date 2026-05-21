using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public enum TextureArrayFallbackMode
    {
        White,
        Black,
        FlatNormal,
        Transparent
    }

    public sealed class TextureArrayBakeResult
    {
        public Texture2DArray TextureArray;
        public string AssetPath;
        public int SourceCount;
        public int MissingCount;
        public string[] SourceNames;
    }

    public static class TextureArrayBakerUtility
    {
        public static readonly string[] DefaultAlbedoProperties = { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_Diffuse" };
        public static readonly string[] DefaultNormalProperties = { "_BumpMap", "_NormalMap", "_Normal", "_NormalTex" };

        public static TextureArrayBakeResult BakeTextureArray(
            IList<Texture2D> sources,
            string assetPath,
            int width,
            int height,
            bool sRgb,
            bool mipChain,
            TextureArrayFallbackMode fallbackMode,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            FilterMode filterMode = FilterMode.Bilinear,
            int anisoLevel = 2)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));
            if (sources.Count == 0)
                throw new ArgumentException("At least one source texture is required.", nameof(sources));
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("A valid asset path is required.", nameof(assetPath));

            width = Mathf.Max(4, width);
            height = Mathf.Max(4, height);
            assetPath = assetPath.Replace("\\", "/");
            EnsureAssetFolder(Path.GetDirectoryName(assetPath));

            var textureArray = new Texture2DArray(width, height, sources.Count, TextureFormat.RGBA32, mipChain, !sRgb)
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                wrapMode = wrapMode,
                filterMode = filterMode,
                anisoLevel = Mathf.Max(0, anisoLevel)
            };

            Texture2D fallback = CreateFallbackTexture(width, height, fallbackMode, sRgb);
            int missingCount = 0;
            string[] names = new string[sources.Count];

            try
            {
                for (int layer = 0; layer < sources.Count; layer++)
                {
                    Texture2D source = sources[layer];
                    if (source == null)
                    {
                        source = fallback;
                        missingCount++;
                        names[layer] = $"Fallback {fallbackMode}";
                    }
                    else
                    {
                        names[layer] = source.name;
                    }

                    Texture2D readable = CreateReadableCopy(source, width, height, sRgb);
                    try
                    {
                        textureArray.SetPixels(readable.GetPixels(0), layer, 0);
                    }
                    finally
                    {
                        if (readable != source && readable != fallback)
                            Object.DestroyImmediate(readable);
                    }
                }

                textureArray.Apply(mipChain, false);

                Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(textureArray, assetPath);
                }
                else
                {
                    EditorUtility.CopySerialized(textureArray, existing);
                    Object.DestroyImmediate(textureArray);
                    textureArray = existing;
                }

                EditorUtility.SetDirty(textureArray);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new TextureArrayBakeResult
                {
                    TextureArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath),
                    AssetPath = assetPath,
                    SourceCount = sources.Count,
                    MissingCount = missingCount,
                    SourceNames = names
                };
            }
            finally
            {
                Object.DestroyImmediate(fallback);
            }
        }

        public static List<Texture2D> GatherTexturesFromMaterials(IList<Material> materials, string propertyList, string[] fallbackProperties = null)
        {
            var results = new List<Texture2D>();
            if (materials == null)
                return results;

            string[] properties = ParsePropertyList(propertyList, fallbackProperties);
            for (int i = 0; i < materials.Count; i++)
                results.Add(FindTexture(materials[i], properties));
            return results;
        }

        public static Texture2D FindTexture(Material material, string[] propertyNames)
        {
            if (material == null || propertyNames == null)
                return null;

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (string.IsNullOrWhiteSpace(propertyName))
                    continue;
                if (!material.HasProperty(propertyName))
                    continue;

                Texture texture = material.GetTexture(propertyName);
                if (texture is Texture2D texture2D)
                    return texture2D;
            }

            return null;
        }

        public static string[] ParsePropertyList(string propertyList, string[] fallbackProperties)
        {
            if (string.IsNullOrWhiteSpace(propertyList))
                return fallbackProperties ?? Array.Empty<string>();

            string[] raw = propertyList.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return raw.Length > 0 ? raw : (fallbackProperties ?? Array.Empty<string>());
        }

        public static Texture2D CreateReadableCopy(Texture source, int width, int height, bool sRgb)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            RenderTexture previous = RenderTexture.active;
            RenderTextureReadWrite rw = sRgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, rw);
            rt.filterMode = FilterMode.Bilinear;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var result = new Texture2D(width, height, TextureFormat.RGBA32, false, !sRgb)
                {
                    name = source.name + "_ReadableCopy",
                    wrapMode = source.wrapMode,
                    filterMode = source.filterMode,
                    anisoLevel = source.anisoLevel
                };
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static Texture2D CreateFallbackTexture(int width, int height, TextureArrayFallbackMode mode, bool sRgb)
        {
            width = Mathf.Max(4, width);
            height = Mathf.Max(4, height);
            Color color = mode switch
            {
                TextureArrayFallbackMode.Black => Color.black,
                TextureArrayFallbackMode.FlatNormal => new Color(0.5f, 0.5f, 1f, 1f),
                TextureArrayFallbackMode.Transparent => new Color(0f, 0f, 0f, 0f),
                _ => Color.white
            };

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !sRgb)
            {
                name = $"Fallback_{mode}",
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || folder == "Assets")
                return;

            folder = folder.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
    #endif

}