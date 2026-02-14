#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class CustomizationOptionGeneratorWindow : EditorWindow
{
    private CharacterCustomizer sourceCustomizer;
    private CustomizationCatalogSO targetCatalog;

    private string outputFolder = "Assets/Customization/GeneratedOptions";
    private int defaultCost = 0;
    private bool overwriteIfExists = false;

    [MenuItem("SkiGame/Customization/Generate Options From CharacterCustomizer")]
    public static void Open()
    {
        GetWindow<CustomizationOptionGeneratorWindow>("Customization Option Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sourceCustomizer = (CharacterCustomizer)EditorGUILayout.ObjectField("CharacterCustomizer", sourceCustomizer, typeof(CharacterCustomizer), true);
        targetCatalog = (CustomizationCatalogSO)EditorGUILayout.ObjectField("Target Catalog", targetCatalog, typeof(CustomizationCatalogSO), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        defaultCost = EditorGUILayout.IntField("Default Cost", defaultCost);
        overwriteIfExists = EditorGUILayout.Toggle("Overwrite If Exists", overwriteIfExists);

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(sourceCustomizer == null || targetCatalog == null))
        {
            if (GUILayout.Button("Generate Skin Pattern Options"))
                GenerateSkinPatternOptions();

            if (GUILayout.Button("Generate Eye Icon Options"))
                GenerateEyeOptions();
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Select a scene instance or prefab instance with CharacterCustomizer assigned.\n" +
            "This tool reads its serialized arrays 'skinPatterns' and 'eyeOptions' even though they are private.",
            MessageType.Info);
    }

    private void GenerateSkinPatternOptions()
    {
        EnsureFolder(outputFolder);

        var so = new SerializedObject(sourceCustomizer);
        var arr = so.FindProperty("skinPatterns");
        if (arr == null || !arr.isArray)
        {
            Debug.LogError("Could not find serialized property 'skinPatterns' on CharacterCustomizer.");
            return;
        }

        for (int i = 0; i < arr.arraySize; i++)
        {
            var texProp = arr.GetArrayElementAtIndex(i);
            var tex = texProp.objectReferenceValue as Texture;
            if (tex == null) continue;

            string baseName = tex.name;
            string id = MakeId("skinpattern", baseName);
            string assetPath = $"{outputFolder}/{id}.asset";

            var opt = LoadOrCreate(assetPath);
            if (opt == null) continue;

            opt.id = id;
            opt.type = CustomizationOptionType.SkinPattern;
            opt.displayName = Nicify(baseName);
            opt.description = "";
            opt.icon = null;
            opt.cost = defaultCost;
            opt.customizerIndex = i;
            opt.gearProfile = null;

            EditorUtility.SetDirty(opt);
            AddToCatalog(targetCatalog, opt);
        }

        FinalizeCatalog();
    }

    private void GenerateEyeOptions()
    {
        EnsureFolder(outputFolder);

        var so = new SerializedObject(sourceCustomizer);
        var arr = so.FindProperty("eyeOptions");
        if (arr == null || !arr.isArray)
        {
            Debug.LogError("Could not find serialized property 'eyeOptions' on CharacterCustomizer.");
            return;
        }

        for (int i = 0; i < arr.arraySize; i++)
        {
            var spriteProp = arr.GetArrayElementAtIndex(i);
            var sprite = spriteProp.objectReferenceValue as Sprite;
            if (sprite == null) continue;

            string baseName = sprite.name;
            string id = MakeId("eyeicon", baseName);
            string assetPath = $"{outputFolder}/{id}.asset";

            var opt = LoadOrCreate(assetPath);
            if (opt == null) continue;

            opt.id = id;
            opt.type = CustomizationOptionType.EyeIcon;
            opt.displayName = Nicify(baseName);
            opt.description = "";
            opt.icon = sprite;
            opt.cost = defaultCost;
            opt.customizerIndex = i;
            opt.gearProfile = null;

            EditorUtility.SetDirty(opt);
            AddToCatalog(targetCatalog, opt);
        }

        FinalizeCatalog();
    }

    private CustomizationOptionSO LoadOrCreate(string assetPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<CustomizationOptionSO>(assetPath);
        if (existing != null)
        {
            if (!overwriteIfExists)
                return existing;

            return existing;
        }

        var opt = CreateInstance<CustomizationOptionSO>();
        AssetDatabase.CreateAsset(opt, assetPath);
        return opt;
    }

    private void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        // Create nested folders
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private void AddToCatalog(CustomizationCatalogSO catalog, CustomizationOptionSO opt)
    {
        if (catalog.options == null)
            catalog.options = new System.Collections.Generic.List<CustomizationOptionSO>();

        if (!catalog.options.Contains(opt))
            catalog.options.Add(opt);
    }

    private void FinalizeCatalog()
    {
        EditorUtility.SetDirty(targetCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CustomizationOptionGenerator] Generation complete.");
    }

    private static string MakeId(string prefix, string name)
    {
        name = name.ToLowerInvariant();
        name = Regex.Replace(name, @"[^a-z0-9]+", "_");
        name = Regex.Replace(name, @"_+", "_").Trim('_');
        return $"{prefix}_{name}";
    }

    private static string Nicify(string name)
    {
        // Adds spaces between camel-case / underscores
        string s = ObjectNames.NicifyVariableName(name);
        s = s.Replace("_", " ");
        return s.Trim();
    }
}
#endif
