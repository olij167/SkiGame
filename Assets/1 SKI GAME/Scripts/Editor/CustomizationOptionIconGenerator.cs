#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using PungentFunk.Utilities.Editor.PreviewExport;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ski-game compatibility adapter for the generic PrefabIconGeneratorService.
/// It intentionally keeps the useful game-specific menu items, but the icon rendering queue lives in PrefabIconGeneratorWindow.cs.
/// This adapter uses SerializedObject/reflection-style access so the generic utility package does not need to depend on CustomizationOptionSO directly.
/// </summary>
public static class CustomizationOptionIconGenerator
{
    private const string CustomizationOptionTypeName = "CustomizationOptionSO";
    private const int DefaultSize = 256;

    [MenuItem("Assets/SkiGame/Customization/Generate Icons (Selected Options)", priority = 2000)]
    public static void GenerateSelected()
    {
        List<UnityEngine.Object> options = GetSelectedCustomizationOptions();
        if (options.Count == 0)
        {
            EditorUtility.DisplayDialog("Generate Icons", "Select one or more CustomizationOptionSO assets.", "OK");
            return;
        }

        GenerateFor(options);
    }

    [MenuItem("Assets/SkiGame/Customization/Generate Icons (Selected Options)", true)]
    private static bool ValidateGenerateSelected()
    {
        return GetSelectedCustomizationOptions().Count > 0;
    }

    [MenuItem("SkiGame/Customization/Generate Icons (All Options)", priority = 2000)]
    public static void GenerateAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:" + CustomizationOptionTypeName);
        List<UnityEngine.Object> options = new List<UnityEngine.Object>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object option = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (IsCustomizationOption(option))
                options.Add(option);
        }

        GenerateFor(options);
    }

    private static List<UnityEngine.Object> GetSelectedCustomizationOptions()
    {
        List<UnityEngine.Object> options = new List<UnityEngine.Object>();
        UnityEngine.Object[] selected = Selection.objects;
        for (int i = 0; i < selected.Length; i++)
        {
            if (IsCustomizationOption(selected[i]))
                options.Add(selected[i]);
        }
        return options;
    }

    private static bool IsCustomizationOption(UnityEngine.Object asset)
    {
        return asset != null && asset.GetType().Name == CustomizationOptionTypeName;
    }

    private static void GenerateFor(IList<UnityEngine.Object> options)
    {
        if (options == null || options.Count == 0)
        {
            EditorUtility.DisplayDialog("Generate Icons", "No CustomizationOptionSO assets were found.", "OK");
            return;
        }

        int queued = 0;
        for (int i = 0; i < options.Count; i++)
        {
            UnityEngine.Object option = options[i];
            if (option == null || !IsPrefabBasedOption(option))
                continue;

            GameObject prefab = ResolvePrefab(option);
            if (prefab == null)
            {
                Debug.LogWarning("[CustomizationIconGenerator] No prefab resolved for " + option.name + ".", option);
                continue;
            }

            string optionPath = AssetDatabase.GetAssetPath(option);
            string folder = Path.GetDirectoryName(optionPath);
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogWarning("[CustomizationIconGenerator] Could not resolve output folder for " + option.name + ".", option);
                continue;
            }

            folder = folder.Replace('\\', '/');
            string outputPath = folder + "/" + SanitizeFileName(option.name) + "_Icon.png";
            UnityEngine.Object capturedOption = option;
            PrefabIconGeneratorService.Enqueue(new PrefabIconGeneratorService.IconJob(
                prefab,
                outputPath,
                DefaultSize,
                true,
                delegate (string path) { AssignSprite(capturedOption, path); }));
            queued++;
        }

        if (queued == 0)
            EditorUtility.DisplayDialog("Generate Icons", "No prefab-based customization options could be queued.", "OK");
        else
            Debug.Log("[CustomizationIconGenerator] Queued " + queued + " customization icon job(s).");
    }

    private static bool IsPrefabBasedOption(UnityEngine.Object option)
    {
        string typeName = GetOptionTypeName(option);
        return typeName == "Skis" ||
               typeName == "Poles" ||
               typeName == "Hat" ||
               typeName == "Jacket" ||
               typeName == "Gloves" ||
               typeName == "Boots" ||
               typeName == "Accessory";
    }

    private static string GetOptionTypeName(UnityEngine.Object option)
    {
        if (option == null)
            return string.Empty;

        SerializedObject serialized = new SerializedObject(option);
        SerializedProperty typeProperty = serialized.FindProperty("type");
        if (typeProperty == null || typeProperty.propertyType != SerializedPropertyType.Enum)
            return string.Empty;

        string[] names = typeProperty.enumDisplayNames;
        if (names == null || typeProperty.enumValueIndex < 0 || typeProperty.enumValueIndex >= names.Length)
            return string.Empty;

        return names[typeProperty.enumValueIndex].Replace(" ", string.Empty);
    }

    private static GameObject ResolvePrefab(UnityEngine.Object option)
    {
        if (option == null)
            return null;

        string typeName = GetOptionTypeName(option);
        SerializedObject serialized = new SerializedObject(option);

        if (typeName == "Hat")
            return GetGameObjectReference(serialized, "hatPrefab");
        if (typeName == "Jacket")
            return GetGameObjectReference(serialized, "jacketPrefab");
        if (typeName == "Gloves")
            return GetGameObjectReference(serialized, "glovePrefab");
        if (typeName == "Boots")
            return GetGameObjectReference(serialized, "bootPrefab");
        if (typeName == "Accessory")
            return GetGameObjectReference(serialized, "accessoryPrefab");
        if (typeName == "Skis")
            return ResolvePrefabFromGearProfile(GetObjectReference(serialized, "gearProfile"), "ski");
        if (typeName == "Poles")
            return ResolvePrefabFromGearProfile(GetObjectReference(serialized, "gearProfile"), "pole");

        return FindFirstGameObjectReference(serialized);
    }

    private static GameObject ResolvePrefabFromGearProfile(UnityEngine.Object gearProfile, string preferredKind)
    {
        if (gearProfile == null)
            return null;

        SerializedObject serialized = new SerializedObject(gearProfile);
        string[] preferredProperties = preferredKind == "ski"
            ? new[] { "skisPrefab", "skiPrefab", "prefab", "modelPrefab", "visualPrefab" }
            : new[] { "polesPrefab", "polePrefab", "prefab", "modelPrefab", "visualPrefab" };

        for (int i = 0; i < preferredProperties.Length; i++)
        {
            GameObject prefab = GetGameObjectReference(serialized, preferredProperties[i]);
            if (prefab != null)
                return prefab;
        }

        return FindFirstGameObjectReference(serialized);
    }

    private static UnityEngine.Object GetObjectReference(SerializedObject serialized, string propertyName)
    {
        if (serialized == null || string.IsNullOrEmpty(propertyName))
            return null;

        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            return null;

        return property.objectReferenceValue;
    }

    private static GameObject GetGameObjectReference(SerializedObject serialized, string propertyName)
    {
        return GetObjectReference(serialized, propertyName) as GameObject;
    }

    private static GameObject FindFirstGameObjectReference(SerializedObject serialized)
    {
        if (serialized == null)
            return null;

        SerializedProperty iterator = serialized.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            GameObject prefab = iterator.objectReferenceValue as GameObject;
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private static void AssignSprite(UnityEngine.Object option, string path)
    {
        if (option == null || string.IsNullOrEmpty(path))
            return;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        if (sprite == null)
        {
            Debug.LogWarning("[CustomizationIconGenerator] Failed to load generated Sprite at: " + path, option);
            return;
        }

        SerializedObject serialized = new SerializedObject(option);
        SerializedProperty iconProperty = serialized.FindProperty("icon");
        if (iconProperty == null || iconProperty.propertyType != SerializedPropertyType.ObjectReference)
        {
            Debug.LogWarning("[CustomizationIconGenerator] " + option.name + " does not expose an object-reference 'icon' property.", option);
            return;
        }

        Undo.RecordObject(option, "Assign Generated Customization Icon");
        iconProperty.objectReferenceValue = sprite;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(option);
        AssetDatabase.SaveAssets();
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Icon";

        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            value = value.Replace(invalid[i], '_');

        return value.Replace(" ", "_");
    }
}
#endif
