#if UNITY_EDITOR
using System.Collections.Generic;
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

    // Migration toggles
    private bool migOnlyFillMissing = true;
    private bool migDryRun = true;
    private bool migSyncEyeIconToIcon = true;

    [MenuItem("SkiGame/Customization/Generate Options From CharacterCustomizer")]
    public static void Open()
    {
        GetWindow<CustomizationOptionGeneratorWindow>("Customization Option Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sourceCustomizer = (CharacterCustomizer)EditorGUILayout.ObjectField(
            "CharacterCustomizer", sourceCustomizer, typeof(CharacterCustomizer), true);

        targetCatalog = (CustomizationCatalogSO)EditorGUILayout.ObjectField(
            "Target Catalog", targetCatalog, typeof(CustomizationCatalogSO), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        defaultCost = EditorGUILayout.IntField("Default Cost", defaultCost);
        overwriteIfExists = EditorGUILayout.Toggle("Overwrite If Exists", overwriteIfExists);

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(sourceCustomizer == null || targetCatalog == null))
        {
            EditorGUILayout.LabelField("Generators", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Skin Pattern Options"))
                GenerateSkinPatternOptions();

            if (GUILayout.Button("Generate Eye Icon Options"))
                GenerateEyeOptions();

            if (GUILayout.Button("Generate Hat Options"))
                GenerateWearableOptions("hatPrefabs", CustomizationOptionType.Hat);

            if (GUILayout.Button("Generate Jacket Options"))
                GenerateWearableOptions("jacketPrefabs", CustomizationOptionType.Jacket);
        }

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("Migration", EditorStyles.boldLabel);

        migOnlyFillMissing = EditorGUILayout.ToggleLeft("Only Fill Missing", migOnlyFillMissing);
        migDryRun = EditorGUILayout.ToggleLeft("Dry Run (log only)", migDryRun);
        migSyncEyeIconToIcon = EditorGUILayout.ToggleLeft("Sync Eye icon -> Option.icon", migSyncEyeIconToIcon);

        using (new EditorGUI.DisabledScope(sourceCustomizer == null || targetCatalog == null))
        {
            if (GUILayout.Button("Migrate Existing Catalog Options (populate payload refs)"))
                MigrateCatalogPayloads();
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "This tool reads CharacterCustomizer's private arrays via SerializedObject:\n" +
            " - skinPatterns (Texture[])\n" +
            " - eyeOptions (Sprite[])\n" +
            " - hatPrefabs (GameObject[])\n" +
            " - jacketPrefabs (GameObject[])\n\n" +
            "Migration populates CustomizationOptionSO payload fields based on customizerIndex.\n" +
            "Prerequisite: CustomizationOptionSO must include skinPatternTexture/eyeSprite/hatPrefab/jacketPrefab fields.",
            MessageType.Info);
    }

    // ---------------------------
    // GENERATORS
    // ---------------------------

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

            Undo.RecordObject(opt, "Generate Customization Option");

            opt.id = id;
            opt.type = CustomizationOptionType.SkinPattern;
            opt.displayName = Nicify(baseName);
            opt.description = "";
            opt.cost = defaultCost;

            // legacy mapping
            opt.customizerIndex = i;
            opt.gearProfile = null;

            // new preferred payload
            opt.skinPatternTexture = tex;

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

            Undo.RecordObject(opt, "Generate Customization Option");

            opt.id = id;
            opt.type = CustomizationOptionType.EyeIcon;
            opt.displayName = Nicify(baseName);
            opt.description = "";
            opt.cost = defaultCost;

            // UI
            opt.icon = sprite;

            // legacy mapping
            opt.customizerIndex = i;
            opt.gearProfile = null;

            // new preferred payload
            opt.eyeSprite = sprite;

            EditorUtility.SetDirty(opt);
            AddToCatalog(targetCatalog, opt);
        }

        FinalizeCatalog();
    }

    private void GenerateWearableOptions(string serializedArrayName, CustomizationOptionType type)
    {
        EnsureFolder(outputFolder);

        var so = new SerializedObject(sourceCustomizer);
        var arr = so.FindProperty(serializedArrayName);
        if (arr == null || !arr.isArray)
        {
            Debug.LogError($"Could not find serialized property '{serializedArrayName}' on CharacterCustomizer.");
            return;
        }

        for (int i = 0; i < arr.arraySize; i++)
        {
            var p = arr.GetArrayElementAtIndex(i);
            var prefab = p.objectReferenceValue as GameObject;
            if (prefab == null) continue;

            string baseName = prefab.name;
            string id = MakeId(type == CustomizationOptionType.Hat ? "hat" : "jacket", baseName);
            string assetPath = $"{outputFolder}/{id}.asset";

            var opt = LoadOrCreate(assetPath);
            if (opt == null) continue;

            Undo.RecordObject(opt, "Generate Customization Option");

            opt.id = id;
            opt.type = type;
            opt.displayName = Nicify(baseName);
            opt.description = "";
            opt.cost = defaultCost;

            // legacy mapping
            opt.customizerIndex = i;
            opt.gearProfile = null;

            // new preferred payload
            if (type == CustomizationOptionType.Hat) opt.hatPrefab = prefab;
            else opt.jacketPrefab = prefab;

            EditorUtility.SetDirty(opt);
            AddToCatalog(targetCatalog, opt);
        }

        FinalizeCatalog();
    }

    // ---------------------------
    // MIGRATION
    // ---------------------------

    private void MigrateCatalogPayloads()
    {
        if (targetCatalog == null || sourceCustomizer == null) return;

        // Pull arrays from CharacterCustomizer (private serialized)
        var so = new SerializedObject(sourceCustomizer);

        var skinPatternsProp = so.FindProperty("skinPatterns");
        var eyeOptionsProp = so.FindProperty("eyeOptions");
        var hatPrefabsProp = so.FindProperty("hatPrefabs");
        var jacketPrefabsProp = so.FindProperty("jacketPrefabs");

        if (skinPatternsProp == null || !skinPatternsProp.isArray)
            Debug.LogWarning("Migration: 'skinPatterns' not found or not array.");
        if (eyeOptionsProp == null || !eyeOptionsProp.isArray)
            Debug.LogWarning("Migration: 'eyeOptions' not found or not array.");
        if (hatPrefabsProp == null || !hatPrefabsProp.isArray)
            Debug.LogWarning("Migration: 'hatPrefabs' not found or not array.");
        if (jacketPrefabsProp == null || !jacketPrefabsProp.isArray)
            Debug.LogWarning("Migration: 'jacketPrefabs' not found or not array.");

        int visited = 0;
        int updated = 0;

        var opts = targetCatalog.options;
        if (opts == null)
        {
            Debug.LogWarning("Migration: targetCatalog.options is null.");
            return;
        }

        for (int i = 0; i < opts.Count; i++)
        {
            var opt = opts[i];
            if (opt == null) continue;

            visited++;

            // Skip "None" pseudo-options or any option with no index mapping
            int idx = opt.customizerIndex;
            if (idx < 0) continue;

            bool changed = false;

            switch (opt.type)
            {
                case CustomizationOptionType.SkinPattern:
                    {
                        var tex = GetArrayObject<Texture>(skinPatternsProp, idx);
                        if (tex != null && (!migOnlyFillMissing || opt.skinPatternTexture == null))
                        {
                            changed |= Assign(opt, "skinPatternTexture", tex);
                            if (!migDryRun) opt.skinPatternTexture = tex;
                        }
                        break;
                    }

                case CustomizationOptionType.EyeIcon:
                    {
                        var sprite = GetArrayObject<Sprite>(eyeOptionsProp, idx);
                        if (sprite != null)
                        {
                            if (!migOnlyFillMissing || opt.eyeSprite == null)
                            {
                                changed |= Assign(opt, "eyeSprite", sprite);
                                if (!migDryRun) opt.eyeSprite = sprite;
                            }

                            if (migSyncEyeIconToIcon && (!migOnlyFillMissing || opt.icon == null))
                            {
                                changed |= Assign(opt, "icon", sprite);
                                if (!migDryRun) opt.icon = sprite;
                            }
                        }
                        break;
                    }

                case CustomizationOptionType.Hat:
                    {
                        var prefab = GetArrayObject<GameObject>(hatPrefabsProp, idx);
                        if (prefab != null && (!migOnlyFillMissing || opt.hatPrefab == null))
                        {
                            changed |= Assign(opt, "hatPrefab", prefab);
                            if (!migDryRun) opt.hatPrefab = prefab;
                        }
                        break;
                    }

                case CustomizationOptionType.Jacket:
                    {
                        var prefab = GetArrayObject<GameObject>(jacketPrefabsProp, idx);
                        if (prefab != null && (!migOnlyFillMissing || opt.jacketPrefab == null))
                        {
                            changed |= Assign(opt, "jacketPrefab", prefab);
                            if (!migDryRun) opt.jacketPrefab = prefab;
                        }
                        break;
                    }
            }

            if (changed)
            {
                updated++;
                if (!migDryRun)
                {
                    Undo.RecordObject(opt, "Migrate Customization Option Payloads");
                    EditorUtility.SetDirty(opt);
                }
            }
        }

        if (!migDryRun)
        {
            EditorUtility.SetDirty(targetCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[CustomizationOptionGenerator] Migration complete. Visited={visited}, Updated={updated}, DryRun={migDryRun}, OnlyFillMissing={migOnlyFillMissing}");
    }

    // returns true when we *would* assign (for logging parity)
    private bool Assign(Object opt, string field, Object value)
    {
        if (migDryRun)
        {
            Debug.Log($"[MIGRATE] Would set {opt.name}.{field} = {value.name}");
            return true;
        }
        return true;
    }

    private static T GetArrayObject<T>(SerializedProperty arrayProp, int index) where T : Object
    {
        if (arrayProp == null || !arrayProp.isArray) return null;
        if (index < 0 || index >= arrayProp.arraySize) return null;
        return arrayProp.GetArrayElementAtIndex(index).objectReferenceValue as T;
    }

    // ---------------------------
    // ASSET HELPERS
    // ---------------------------

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
            catalog.options = new List<CustomizationOptionSO>();

        if (!catalog.options.Contains(opt))
            catalog.options.Add(opt);
    }

    private void FinalizeCatalog()
    {
        EditorUtility.SetDirty(targetCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CustomizationOptionGenerator] Done.");
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
        string s = ObjectNames.NicifyVariableName(name);
        s = s.Replace("_", " ");
        return s.Trim();
    }
}
#endif
