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

    private List<GameObject> gloveSourcePrefabs = new List<GameObject>();
    private List<GameObject> bootSourcePrefabs = new List<GameObject>();
    private Vector2 generatorScroll;

    // Migration toggles
    private bool migOnlyFillMissing = true;
    private bool migDryRun = true;
    private bool migSyncEyeIconToIcon = true;

    // Catalog maintenance
    private bool catalogScanIncludePackages = false;

    [MenuItem("SkiGame/Customization/Generate Options From CharacterCustomizer")]
    public static void Open()
    {
        GetWindow<CustomizationOptionGeneratorWindow>("Customization Option Generator");
    }

    private void OnGUI()
    {
        generatorScroll = EditorGUILayout.BeginScrollView(generatorScroll);

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
        DrawGeneratorSection();

        EditorGUILayout.Space(14);
        DrawMigrationSection();

        EditorGUILayout.Space(14);
        DrawCatalogMaintenanceSection();

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Supported generation sources:\n" +
            " - CharacterCustomizer.skinPatterns -> Skin Pattern options\n" +
            " - CharacterCustomizer.eyeOptions -> Eye Icon options\n" +
            " - CharacterCustomizer.hatPrefabs -> Hat options\n" +
            " - CharacterCustomizer.jacketPrefabs -> Jacket options\n" +
            " - CharacterCustomizer.accessoryPrefabs -> Accessory options\n" +
            " - CharacterCustomizer.glovePrefabs -> Glove options\n" +
            " - CharacterCustomizer.bootPrefabs -> Boot options\n" +
            " - Current selection / manual lists -> optional prefab authoring shortcuts\n\n" +
            "Migration can backfill payload refs from serialized CharacterCustomizer arrays for skin patterns, eye icons, hats, jackets, accessories, gloves, and boots.\n" +
            "Gloves and boots now follow the same wearable customizerIndex flow as the other wearable prefab categories.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawGeneratorSection()
    {
        using (new EditorGUI.DisabledScope(targetCatalog == null))
        {
            EditorGUILayout.LabelField("Generators", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(sourceCustomizer == null))
            {
                if (GUILayout.Button("Generate Skin Pattern Options"))
                    GenerateSkinPatternOptions();

                if (GUILayout.Button("Generate Eye Icon Options"))
                    GenerateEyeOptions();

                if (GUILayout.Button("Generate Hat Options"))
                    GenerateWearableOptions("hatPrefabs", CustomizationOptionType.Hat);

                if (GUILayout.Button("Generate Jacket Options"))
                    GenerateWearableOptions("jacketPrefabs", CustomizationOptionType.Jacket);

                if (GUILayout.Button("Generate Accessory Options"))
                    GenerateWearableOptions("accessoryPrefabs", CustomizationOptionType.Accessory);

                if (GUILayout.Button("Generate Glove Options"))
                    GenerateWearableOptions("glovePrefabs", CustomizationOptionType.Gloves);

                if (GUILayout.Button("Generate Boot Options"))
                    GenerateWearableOptions("bootPrefabs", CustomizationOptionType.Boots);
            }

            if (GUILayout.Button("Generate Accessory Options From Selected Prefabs"))
                GenerateOptionsFromSelection(CustomizationOptionType.Accessory);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Optional Manual Prefab Sources", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gloves and boots now auto-generate from CharacterCustomizer when those arrays are populated. These manual lists and selection-based actions are still useful for quick authoring before wiring prefabs into the source customizer.",
                MessageType.None);

            DrawManualPrefabList("Glove Prefabs", gloveSourcePrefabs);
            if (GUILayout.Button("Generate Glove Options From Manual List"))
                GenerateOptionsFromManualList(gloveSourcePrefabs, CustomizationOptionType.Gloves);
            if (GUILayout.Button("Generate Glove Options From Selected Prefabs"))
                GenerateOptionsFromSelection(CustomizationOptionType.Gloves);

            EditorGUILayout.Space(6);
            DrawManualPrefabList("Boot Prefabs", bootSourcePrefabs);
            if (GUILayout.Button("Generate Boot Options From Manual List"))
                GenerateOptionsFromManualList(bootSourcePrefabs, CustomizationOptionType.Boots);
            if (GUILayout.Button("Generate Boot Options From Selected Prefabs"))
                GenerateOptionsFromSelection(CustomizationOptionType.Boots);
        }
    }

    private void DrawMigrationSection()
    {
        EditorGUILayout.LabelField("Migration", EditorStyles.boldLabel);

        migOnlyFillMissing = EditorGUILayout.ToggleLeft("Only Fill Missing", migOnlyFillMissing);
        migDryRun = EditorGUILayout.ToggleLeft("Dry Run (log only)", migDryRun);
        migSyncEyeIconToIcon = EditorGUILayout.ToggleLeft("Sync Eye icon -> Option.icon", migSyncEyeIconToIcon);

        using (new EditorGUI.DisabledScope(sourceCustomizer == null || targetCatalog == null))
        {
            if (GUILayout.Button("Migrate Existing Catalog Options (populate payload refs)"))
                MigrateCatalogPayloads();
        }
    }

    private void DrawCatalogMaintenanceSection()
    {
        EditorGUILayout.LabelField("Catalog Maintenance", EditorStyles.boldLabel);

        catalogScanIncludePackages = EditorGUILayout.ToggleLeft(
            "Include Packages Folder",
            catalogScanIncludePackages);

        using (new EditorGUI.DisabledScope(targetCatalog == null))
        {
            if (GUILayout.Button("Find Project Options And Add Missing To Target Catalog"))
                FindAndAddMissingOptionsToCatalog();

            if (GUILayout.Button("Validate / Repair customizerIndex Values For Target Catalog"))
                ValidateAndRepairCustomizerIndices();
        }
    }

    private void DrawManualPrefabList(string label, List<GameObject> prefabs)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        int desiredCount = Mathf.Max(0, EditorGUILayout.IntField("Count", prefabs.Count));
        while (prefabs.Count < desiredCount)
            prefabs.Add(null);
        while (prefabs.Count > desiredCount)
            prefabs.RemoveAt(prefabs.Count - 1);

        for (int i = 0; i < prefabs.Count; i++)
        {
            prefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                $"Prefab {i + 1}",
                prefabs[i],
                typeof(GameObject),
                false);
        }
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
            opt.customizerIndex = i;
            opt.gearProfile = null;
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
            opt.icon = sprite;
            opt.customizerIndex = i;
            opt.gearProfile = null;
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

            GenerateOptionFromPrefab(prefab, type, i);
        }

        FinalizeCatalog();
    }

    private void GenerateOptionsFromManualList(List<GameObject> prefabs, CustomizationOptionType type)
    {
        EnsureFolder(outputFolder);

        int generated = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null)
                continue;

            GenerateOptionFromPrefab(prefabs[i], type, -1);
            generated++;
        }

        if (generated == 0)
        {
            Debug.LogWarning($"[CustomizationOptionGenerator] No prefabs supplied for {type} generation.");
            return;
        }

        FinalizeCatalog();
    }

    private void GenerateOptionsFromSelection(CustomizationOptionType type)
    {
        EnsureFolder(outputFolder);

        Object[] selected = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
        int generated = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            var prefab = selected[i] as GameObject;
            if (prefab == null)
                continue;

            GenerateOptionFromPrefab(prefab, type, -1);
            generated++;
        }

        if (generated == 0)
        {
            Debug.LogWarning($"[CustomizationOptionGenerator] No GameObject prefabs selected for {type} generation.");
            return;
        }

        FinalizeCatalog();
    }

    private void GenerateOptionFromPrefab(GameObject prefab, CustomizationOptionType type, int customizerIndex)
    {
        if (prefab == null)
            return;

        var opt = FindExistingOptionForPrefab(targetCatalog, type, prefab);

        string id = MakeId(GetIdPrefix(type), prefab.name);
        string assetPath = $"{outputFolder}/{id}.asset";

        if (opt == null)
            opt = LoadOrCreate(assetPath);
        if (opt == null)
            return;

        Undo.RecordObject(opt, "Generate Customization Option");

        int resolvedCustomizerIndex = ShouldUseCustomizerIndex(type)
            ? ResolveCustomizerIndexForPrefab(type, prefab, customizerIndex)
            : -1;

        opt.id = id;
        opt.type = type;
        opt.displayName = Nicify(prefab.name);
        opt.description = "";
        opt.cost = defaultCost;
        opt.customizerIndex = resolvedCustomizerIndex;
        opt.gearProfile = null;

        AssignPrefabPayload(opt, type, prefab);

        EditorUtility.SetDirty(opt);
        AddToCatalog(targetCatalog, opt);
    }

    private CustomizationOptionSO FindExistingOptionForPrefab(CustomizationCatalogSO catalog, CustomizationOptionType type, GameObject prefab)
    {
        if (catalog == null || catalog.options == null || prefab == null)
            return null;

        for (int i = 0; i < catalog.options.Count; i++)
        {
            var option = catalog.options[i];
            if (option == null || option.type != type)
                continue;

            if (GetPrefabPayload(option, type) == prefab)
                return option;
        }

        return null;
    }

    private GameObject GetPrefabPayload(CustomizationOptionSO option, CustomizationOptionType type)
    {
        if (option == null)
            return null;

        switch (type)
        {
            case CustomizationOptionType.Hat:
                return option.hatPrefab;

            case CustomizationOptionType.Jacket:
                return option.jacketPrefab;

            case CustomizationOptionType.Accessory:
                return option.accessoryPrefab;

            case CustomizationOptionType.Gloves:
                return option.glovePrefab;

            case CustomizationOptionType.Boots:
                return option.bootPrefab;

            default:
                return null;
        }
    }

    private static void AssignPrefabPayload(CustomizationOptionSO opt, CustomizationOptionType type, GameObject prefab)
    {
        switch (type)
        {
            case CustomizationOptionType.Hat:
                opt.hatPrefab = prefab;
                break;

            case CustomizationOptionType.Jacket:
                opt.jacketPrefab = prefab;
                break;

            case CustomizationOptionType.Accessory:
                opt.accessoryPrefab = prefab;
                break;

            case CustomizationOptionType.Gloves:
                opt.glovePrefab = prefab;
                break;

            case CustomizationOptionType.Boots:
                opt.bootPrefab = prefab;
                break;
        }
    }

    private static bool ShouldUseCustomizerIndex(CustomizationOptionType type)
    {
        switch (type)
        {
            case CustomizationOptionType.SkinPattern:
            case CustomizationOptionType.EyeIcon:
            case CustomizationOptionType.Hat:
            case CustomizationOptionType.Jacket:
            case CustomizationOptionType.Accessory:
            case CustomizationOptionType.Gloves:
            case CustomizationOptionType.Boots:
                return true;

            default:
                return false;
        }
    }

    private static string GetIdPrefix(CustomizationOptionType type)
    {
        switch (type)
        {
            case CustomizationOptionType.Hat:
                return "hat";

            case CustomizationOptionType.Jacket:
                return "jacket";

            case CustomizationOptionType.Accessory:
                return "accessory";

            case CustomizationOptionType.Gloves:
                return "glove";

            case CustomizationOptionType.Boots:
                return "boot";

            case CustomizationOptionType.SkinPattern:
                return "skinpattern";

            case CustomizationOptionType.EyeIcon:
                return "eyeicon";

            default:
                return "customization";
        }
    }

    // ---------------------------
    // MIGRATION
    // ---------------------------

    private void MigrateCatalogPayloads()
    {
        if (targetCatalog == null || sourceCustomizer == null) return;

        var so = new SerializedObject(sourceCustomizer);

        var skinPatternsProp = so.FindProperty("skinPatterns");
        var eyeOptionsProp = so.FindProperty("eyeOptions");
        var hatPrefabsProp = so.FindProperty("hatPrefabs");
        var jacketPrefabsProp = so.FindProperty("jacketPrefabs");
        var accessoryPrefabsProp = so.FindProperty("accessoryPrefabs");
        var glovePrefabsProp = so.FindProperty("glovePrefabs");
        var bootPrefabsProp = so.FindProperty("bootPrefabs");

        if (skinPatternsProp == null || !skinPatternsProp.isArray)
            Debug.LogWarning("Migration: 'skinPatterns' not found or not array.");
        if (eyeOptionsProp == null || !eyeOptionsProp.isArray)
            Debug.LogWarning("Migration: 'eyeOptions' not found or not array.");
        if (hatPrefabsProp == null || !hatPrefabsProp.isArray)
            Debug.LogWarning("Migration: 'hatPrefabs' not found or not array.");
        if (jacketPrefabsProp == null || !jacketPrefabsProp.isArray)
            Debug.LogWarning("Migration: 'jacketPrefabs' not found or not array.");
        if (accessoryPrefabsProp == null || !accessoryPrefabsProp.isArray)
            Debug.LogWarning("Migration: 'accessoryPrefabs' not found or not array.");
        if (glovePrefabsProp == null || !glovePrefabsProp.isArray)
            Debug.LogWarning("Migration: 'glovePrefabs' not found or not array.");
        if (bootPrefabsProp == null || !bootPrefabsProp.isArray)
            Debug.LogWarning("Migration: 'bootPrefabs' not found or not array.");

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

                case CustomizationOptionType.Accessory:
                    {
                        var prefab = GetArrayObject<GameObject>(accessoryPrefabsProp, idx);
                        if (prefab != null && (!migOnlyFillMissing || opt.accessoryPrefab == null))
                        {
                            changed |= Assign(opt, "accessoryPrefab", prefab);
                            if (!migDryRun) opt.accessoryPrefab = prefab;
                        }
                        break;
                    }

                case CustomizationOptionType.Gloves:
                    {
                        var prefab = GetArrayObject<GameObject>(glovePrefabsProp, idx);
                        if (prefab != null && (!migOnlyFillMissing || opt.glovePrefab == null))
                        {
                            changed |= Assign(opt, "glovePrefab", prefab);
                            if (!migDryRun) opt.glovePrefab = prefab;
                        }
                        break;
                    }

                case CustomizationOptionType.Boots:
                    {
                        var prefab = GetArrayObject<GameObject>(bootPrefabsProp, idx);
                        if (prefab != null && (!migOnlyFillMissing || opt.bootPrefab == null))
                        {
                            changed |= Assign(opt, "bootPrefab", prefab);
                            if (!migDryRun) opt.bootPrefab = prefab;
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
    // CATALOG MAINTENANCE
    // ---------------------------

    private void FindAndAddMissingOptionsToCatalog()
    {
        if (targetCatalog == null)
        {
            Debug.LogWarning("[CustomizationOptionGenerator] No target catalog assigned.");
            return;
        }

        string[] searchFolders = catalogScanIncludePackages
            ? null
            : new[] { "Assets" };

        string[] guids = AssetDatabase.FindAssets("t:CustomizationOptionSO", searchFolders);

        if (targetCatalog.options == null)
            targetCatalog.options = new List<CustomizationOptionSO>();

        Undo.RecordObject(targetCatalog, "Add Missing Customization Options To Catalog");

        int found = 0;
        int added = 0;
        int skippedNull = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var opt = AssetDatabase.LoadAssetAtPath<CustomizationOptionSO>(path);
            if (opt == null)
            {
                skippedNull++;
                continue;
            }

            found++;

            if (!targetCatalog.options.Contains(opt))
            {
                targetCatalog.options.Add(opt);
                added++;
            }
        }

        targetCatalog.options.Sort(CompareOptionsForCatalogOrder);

        EditorUtility.SetDirty(targetCatalog);

        int repaired = RebuildCustomizerIndicesForCatalog(targetCatalog, logResults: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[CustomizationOptionGenerator] Catalog scan complete. " +
            $"Found={found}, Added={added}, NullSkipped={skippedNull}, IndexesRepaired={repaired}");
    }

    private void ValidateAndRepairCustomizerIndices()
    {
        if (targetCatalog == null)
        {
            Debug.LogWarning("[CustomizationOptionGenerator] No target catalog assigned.");
            return;
        }

        if (targetCatalog.options == null)
        {
            targetCatalog.options = new List<CustomizationOptionSO>();
        }

        Undo.RecordObject(targetCatalog, "Validate Customizer Indices");

        targetCatalog.options.Sort(CompareOptionsForCatalogOrder);

        int repaired = RebuildCustomizerIndicesForCatalog(targetCatalog, logResults: true);

        EditorUtility.SetDirty(targetCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            repaired > 0
                ? $"[CustomizationOptionGenerator] customizerIndex validation complete. Repaired {repaired} option(s)."
                : "[CustomizationOptionGenerator] customizerIndex validation complete. No changes were needed.");
    }

    private int RebuildCustomizerIndicesForCatalog(CustomizationCatalogSO catalog, bool logResults)
    {
        if (catalog == null || catalog.options == null)
            return 0;

        int repairedCount = 0;

        int skinIndex = 0;

        for (int i = 0; i < catalog.options.Count; i++)
        {
            var opt = catalog.options[i];
            if (opt == null)
                continue;

            int desiredIndex = GetDesiredCustomizerIndex(
                opt,
                ref skinIndex);

            if (opt.customizerIndex != desiredIndex)
            {
                Undo.RecordObject(opt, "Repair Customizer Index");
                int previous = opt.customizerIndex;
                opt.customizerIndex = desiredIndex;
                EditorUtility.SetDirty(opt);
                repairedCount++;

                if (logResults)
                {
                    Debug.Log(
                        $"[CustomizationOptionGenerator] Repaired {opt.name} ({opt.type}) " +
                        $"customizerIndex {previous} -> {desiredIndex}");
                }
            }
        }

        return repairedCount;
    }

    private int GetDesiredCustomizerIndex(
        CustomizationOptionSO opt,
        ref int skinIndex)
    {
        switch (opt.type)
        {
            case CustomizationOptionType.SkinPattern:
                return skinIndex++;

            case CustomizationOptionType.EyeIcon:
                return ResolveCustomizerIndexForEyeOption(opt);

            case CustomizationOptionType.Hat:
            case CustomizationOptionType.Jacket:
            case CustomizationOptionType.Accessory:
            case CustomizationOptionType.Gloves:
            case CustomizationOptionType.Boots:
                return ResolveCustomizerIndexForOption(opt);

            default:
                return -1;
        }
    }

    private int ResolveCustomizerIndexForOption(CustomizationOptionSO option)
    {
        if (option == null)
            return -1;

        switch (option.type)
        {
            case CustomizationOptionType.EyeIcon:
                return ResolveCustomizerIndexForEyeOption(option);

            case CustomizationOptionType.Hat:
                return ResolveCustomizerIndexForPrefab(option.type, option.hatPrefab, option.customizerIndex);

            case CustomizationOptionType.Jacket:
                return ResolveCustomizerIndexForPrefab(option.type, option.jacketPrefab, option.customizerIndex);

            case CustomizationOptionType.Accessory:
                return ResolveCustomizerIndexForPrefab(option.type, option.accessoryPrefab, option.customizerIndex);

            case CustomizationOptionType.Gloves:
                return ResolveCustomizerIndexForPrefab(option.type, option.glovePrefab, option.customizerIndex);

            case CustomizationOptionType.Boots:
                return ResolveCustomizerIndexForPrefab(option.type, option.bootPrefab, option.customizerIndex);

            default:
                return -1;
        }
    }

    private int ResolveCustomizerIndexForEyeOption(CustomizationOptionSO option)
    {
        if (option == null)
            return -1;

        Sprite sourceSprite = option.eyeSprite != null ? option.eyeSprite : option.icon;
        return ResolveCustomizerIndexForEyeSprite(sourceSprite, option.customizerIndex);
    }

    private int ResolveCustomizerIndexForEyeSprite(Sprite sprite, int fallbackIndex = -1)
    {
        if (sprite == null)
            return -1;

        var eyeOptions = GetCustomizerEyeOptions();
        if (eyeOptions == null)
            return sourceCustomizer == null ? fallbackIndex : -1;

        for (int i = 0; i < eyeOptions.Length; i++)
        {
            if (eyeOptions[i] == sprite)
                return i;
        }

        return -1;
    }

    private int ResolveCustomizerIndexForPrefab(CustomizationOptionType type, GameObject prefab, int fallbackIndex = -1)
    {
        if (!ShouldUseCustomizerIndex(type))
            return -1;

        if (prefab == null)
            return -1;

        var prefabs = GetCustomizerPrefabArray(type);
        if (prefabs == null)
            return sourceCustomizer == null ? fallbackIndex : -1;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == prefab)
                return i;
        }

        return -1;
    }

    private Sprite[] GetCustomizerEyeOptions()
    {
        if (sourceCustomizer == null)
            return null;

        var so = new SerializedObject(sourceCustomizer);
        var arrayProperty = so.FindProperty("eyeOptions");
        if (arrayProperty == null || !arrayProperty.isArray)
            return null;

        var eyeOptions = new Sprite[arrayProperty.arraySize];
        for (int i = 0; i < arrayProperty.arraySize; i++)
            eyeOptions[i] = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;

        return eyeOptions;
    }

    private GameObject[] GetCustomizerPrefabArray(CustomizationOptionType type)
    {
        if (sourceCustomizer == null)
            return null;

        var so = new SerializedObject(sourceCustomizer);
        string propertyName = GetPrefabArrayPropertyName(type);
        if (string.IsNullOrEmpty(propertyName))
            return null;

        var arrayProperty = so.FindProperty(propertyName);
        if (arrayProperty == null || !arrayProperty.isArray)
            return null;

        var prefabs = new GameObject[arrayProperty.arraySize];
        for (int i = 0; i < arrayProperty.arraySize; i++)
            prefabs[i] = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;

        return prefabs;
    }

    private static string GetPrefabArrayPropertyName(CustomizationOptionType type)
    {
        switch (type)
        {
            case CustomizationOptionType.Hat:
                return "hatPrefabs";

            case CustomizationOptionType.Jacket:
                return "jacketPrefabs";

            case CustomizationOptionType.Accessory:
                return "accessoryPrefabs";

            case CustomizationOptionType.Gloves:
                return "glovePrefabs";

            case CustomizationOptionType.Boots:
                return "bootPrefabs";

            default:
                return null;
        }
    }

    private static int CompareOptionsForCatalogOrder(CustomizationOptionSO a, CustomizationOptionSO b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int typeCompare = a.type.CompareTo(b.type);
        if (typeCompare != 0) return typeCompare;

        string aKey = GetStableOptionSortKey(a);
        string bKey = GetStableOptionSortKey(b);
        return string.Compare(aKey, bKey, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStableOptionSortKey(CustomizationOptionSO opt)
    {
        if (opt == null) return "~";

        if (!string.IsNullOrWhiteSpace(opt.id))
            return opt.id;

        string path = AssetDatabase.GetAssetPath(opt);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        if (!string.IsNullOrWhiteSpace(opt.displayName))
            return opt.displayName;

        return opt.name;
    }

    // ---------------------------
    // ASSET HELPERS
    // ---------------------------

    private CustomizationOptionSO LoadOrCreate(string assetPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<CustomizationOptionSO>(assetPath);
        if (existing != null)
            return existing;

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
        targetCatalog.options.Sort(CompareOptionsForCatalogOrder);
        RebuildCustomizerIndicesForCatalog(targetCatalog, logResults: false);

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
