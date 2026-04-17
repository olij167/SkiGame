using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SkiGame.UI;

public static class InputPromptIconLibraryAutoFill
{
    private const string DefaultAssetName = "InputPromptIconLibrary_Default";
    private const string DefaultSearchRoot = "Assets/1 SKI GAME";

    private sealed class EntryData
    {
        public string Key;
        public string FileName;
        public string Fallback;
    }

    [MenuItem("Tools/Ski Game/UI/Populate Selected Input Prompt Library")]
    private static void PopulateSelectedLibrary()
    {
        if (Selection.activeObject is not InputPromptIconLibrary library)
        {
            EditorUtility.DisplayDialog(
                "Input Prompt Library",
                "Select an InputPromptIconLibrary asset first.",
                "OK");
            return;
        }

        PopulateLibrary(library);
    }

    [MenuItem("Tools/Ski Game/UI/Populate Default Input Prompt Library")]
    private static void PopulateDefaultLibrary()
    {
        string[] preferredNames =
        {
        "InputPromptIconLibrary_Default",
        "InputPromptIconLibrary"
    };

        InputPromptIconLibrary library = null;

        for (int n = 0; n < preferredNames.Length && library == null; n++)
        {
            string assetName = preferredNames[n];
            string[] guids = AssetDatabase.FindAssets($"t:InputPromptIconLibrary {assetName}");

            InputPromptIconLibrary bestMatch = null;
            InputPromptIconLibrary fallbackMatch = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var candidate = AssetDatabase.LoadAssetAtPath<InputPromptIconLibrary>(path);
                if (candidate == null)
                    continue;

                if (!string.Equals(candidate.name, assetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (fallbackMatch == null)
                    fallbackMatch = candidate;

                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.EndsWith("/Resources", StringComparison.OrdinalIgnoreCase))
                {
                    bestMatch = candidate;
                    break;
                }
            }

            library = bestMatch != null ? bestMatch : fallbackMatch;
        }

        if (library == null)
        {
            EditorUtility.DisplayDialog(
                "Input Prompt Library",
                "Could not find a default InputPromptIconLibrary asset. Expected either " +
                "'InputPromptIconLibrary_Default.asset' or 'InputPromptIconLibrary.asset'.",
                "OK");
            return;
        }

        PopulateLibrary(library);
    }

    private static void PopulateLibrary(InputPromptIconLibrary library)
    {
        if (library == null)
            return;

        string libraryPath = AssetDatabase.GetAssetPath(library);
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            Debug.LogError("Could not resolve asset path for InputPromptIconLibrary.");
            return;
        }

        string[] searchRoots = ResolveSearchRoots();
        var spriteLookup = BuildSpriteLookup(searchRoots);
        var entries = BuildEntries();

        var so = new SerializedObject(library);
        var entriesProp = so.FindProperty("entries");
        if (entriesProp == null)
        {
            Debug.LogError("Could not find 'entries' serialized field on InputPromptIconLibrary.");
            return;
        }

        entriesProp.ClearArray();

        int added = 0;
        int missing = 0;
        List<string> missingFiles = new();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!spriteLookup.TryGetValue(entry.FileName, out var sprite) || sprite == null)
            {
                missing++;
                missingFiles.Add($"{entry.Key} -> {entry.FileName}");
                continue;
            }

            int index = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(index);

            var element = entriesProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("key").stringValue = entry.Key;
            element.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            element.FindPropertyRelative("textFallback").stringValue = entry.Fallback;

            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        string summary =
            $"Populated '{library.name}' with {added} entries.\n" +
            $"Missing matches: {missing}";

        if (missingFiles.Count > 0)
            Debug.LogWarning(summary + "\nMissing:\n" + string.Join("\n", missingFiles));
        else
            Debug.Log(summary);

        EditorUtility.DisplayDialog("Input Prompt Library", summary, "OK");
    }

    private static string[] ResolveSearchRoots()
    {
        List<string> roots = new();

        if (AssetDatabase.IsValidFolder(DefaultSearchRoot))
            roots.Add(DefaultSearchRoot);

        string[] iconPackGuids = AssetDatabase.FindAssets("GameInputControllerIconsFree");
        for (int i = 0; i < iconPackGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(iconPackGuids[i]);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(dir) && AssetDatabase.IsValidFolder(dir) && !roots.Contains(dir))
                roots.Add(dir);
        }

        if (roots.Count == 0)
            roots.Add("Assets");

        return roots.ToArray();
    }

    private static Dictionary<string, Sprite> BuildSpriteLookup(string[] searchRoots)
    {
        var lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        string[] guids = AssetDatabase.FindAssets("t:Sprite", searchRoots);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                continue;

            string fileName = Path.GetFileName(path);
            if (!lookup.ContainsKey(fileName))
                lookup.Add(fileName, sprite);
        }

        return lookup;
    }

    private static List<EntryData> BuildEntries()
    {
        var entries = new List<EntryData>
        {
            Make("keyboard:shift", "shift.png", "Shift"),
            Make("keyboard:ctrl", "ctrl.png", "Ctrl"),
            Make("keyboard:alt", "alt.png", "Alt"),
            Make("keyboard:esc", "esc.png", "Esc"),
            Make("keyboard:space", "space.png", "Space"),
            Make("keyboard:enter", "enter.png", "Enter"),
            Make("keyboard:tab", "tab.png", "Tab"),
            Make("keyboard:pause", "pause.png", "Pause"),

            Make("keyboard:arrow-up", "arrow-up.png", "Up"),
            Make("keyboard:arrow-down", "arrow-down.png", "Down"),
            Make("keyboard:arrow-left", "arrow-left.png", "Left"),
            Make("keyboard:arrow-right", "arrow-right.png", "Right"),

            Make("mouse:left", "mouse-left.png", "LMB"),
            Make("mouse:right", "mouse-right.png", "RMB"),
            Make("mouse:middle", "mouse-middle.png", "MMB"),
            Make("mouse:move-hor", "mouse-move-hor.png", "Mouse X"),
            Make("mouse:move-vert", "mouse-move-vert.png", "Mouse Y")
        };

        for (char c = 'a'; c <= 'z'; c++)
        {
            string letter = c.ToString();
            entries.Add(Make($"keyboard:{letter}", $"{letter}.png", letter.ToUpperInvariant()));
        }

        for (int i = 0; i <= 9; i++)
        {
            string digit = i.ToString();
            entries.Add(Make($"keyboard:{digit}", $"{digit}.png", digit));
        }

        return entries;
    }

    private static EntryData Make(string key, string fileName, string fallback)
    {
        return new EntryData
        {
            Key = key,
            FileName = fileName,
            Fallback = fallback
        };
    }
}