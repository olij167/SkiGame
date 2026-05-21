using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
    #if UNITY_EDITOR
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public static class PungentPaletteStorageUtility
    {

        public static PungentColourPaletteSO CreatePaletteAsset(string assetName = "Pungent Colour Palette")
        {
            string folder = GetPreferredAssetFolder();
            var palette = ScriptableObject.CreateInstance<PungentColourPaletteSO>();
            palette.paletteName = string.IsNullOrWhiteSpace(assetName) ? "Pungent Colour Palette" : assetName;
            palette.EnsureMetadata();
            palette.swatches.Add(new PaletteSwatch("Background", new Color(0.07f, 0.09f, 0.14f), PaletteSwatchRole.Background));
            palette.swatches.Add(new PaletteSwatch("Panel", new Color(0.12f, 0.16f, 0.23f), PaletteSwatchRole.Panel));
            palette.swatches.Add(new PaletteSwatch("Text", new Color(0.94f, 0.96f, 1f), PaletteSwatchRole.Text));
            palette.swatches.Add(new PaletteSwatch("Accent", new Color(0.22f, 0.63f, 1f), PaletteSwatchRole.Accent));

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SanitizeFileName(palette.paletteName)}.asset");
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = palette;
            return palette;
        }

        public static PungentColourPaletteSO SaveAsAsset(PungentColourPaletteSO source, string title = "Save Palette As")
        {
            if (source == null)
                return null;

            string folder = GetPreferredAssetFolder(source);
            string defaultName = SanitizeFileName(string.IsNullOrWhiteSpace(source.paletteName) ? source.name : source.paletteName);
            string path = EditorUtility.SaveFilePanelInProject(title, defaultName, "asset", "Choose where to save this colour palette.", folder);
            if (string.IsNullOrEmpty(path))
                return null;

            PungentColourPaletteSO asset = ScriptableObject.Instantiate(source);
            asset.paletteName = string.IsNullOrWhiteSpace(asset.paletteName) ? Path.GetFileNameWithoutExtension(path) : asset.paletteName;
            asset.EnsureMetadata();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            return asset;
        }

        public static PungentColourPaletteSO DuplicateAsset(PungentColourPaletteSO source)
        {
            if (source == null)
                return null;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
                return SaveAsAsset(source, "Save Palette Copy");

            string duplicatePath = Path.Combine(Path.GetDirectoryName(sourcePath), Path.GetFileNameWithoutExtension(sourcePath) + " Copy.asset").Replace("\\", "/");
            string path = AssetDatabase.GenerateUniqueAssetPath(duplicatePath);
            if (!AssetDatabase.CopyAsset(sourcePath, path))
                return null;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PungentColourPaletteSO duplicate = AssetDatabase.LoadAssetAtPath<PungentColourPaletteSO>(path);
            if (duplicate != null)
            {
                Undo.RecordObject(duplicate, "Duplicate Palette");
                duplicate.paletteName = source.paletteName + " Copy";
                duplicate.EnsureMetadata();
                EditorUtility.SetDirty(duplicate);
                Selection.activeObject = duplicate;
            }
            return duplicate;
        }

        public static void SaveExisting(PungentColourPaletteSO palette)
        {
            if (palette == null)
                return;

            palette.EnsureMetadata();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
        }

        public static void CopyPaletteJsonToClipboard(PungentColourPaletteSO palette)
        {
            if (palette == null)
                return;
            EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(palette, true);
        }

        public static void CopySwatchValuesToClipboard(PungentColourPaletteSO palette)
        {
            if (palette == null || palette.swatches == null)
                return;

            var writer = new StringWriter();
            for (int i = 0; i < palette.swatches.Count; i++)
            {
                PaletteSwatch swatch = palette.swatches[i];
                if (swatch == null)
                    continue;
                writer.WriteLine($"{swatch.name}\t{swatch.role}\t{ColourConversionUtility.ToHexRGBA(swatch.color)}\t{ColourConversionUtility.FormatRGB(swatch.color)}\t{ColourConversionUtility.FormatHSV(swatch.color)}");
            }
            EditorGUIUtility.systemCopyBuffer = writer.ToString();
        }

        private static string GetPreferredAssetFolder(Object context = null)
        {
            if (context != null)
            {
                string path = AssetDatabase.GetAssetPath(context);
                if (!string.IsNullOrEmpty(path))
                {
                    string folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                    if (IsProjectAssetFolder(folder))
                        return folder.Replace("\\", "/");
                }
            }

            Object selected = Selection.activeObject;
            if (selected != null)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path))
                {
                    string folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                    if (IsProjectAssetFolder(folder))
                        return folder.Replace("\\", "/");
                }
            }

            return "Assets";
        }

        private static bool IsProjectAssetFolder(string folder)
        {
            return !string.IsNullOrEmpty(folder) && folder.Replace("\\", "/").StartsWith("Assets");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Pungent Colour Palette";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name.Trim();
        }
    }
    #endif

}