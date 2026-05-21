using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public enum PaletteApplyTargetKind
    {
        RendererMaterial,
        MaterialAsset,
        SpriteRenderer,
        UiGraphic,
        TmpText
    }

    public class PaletteApplyTarget
    {
        public UnityEngine.Object targetObject;
        public GameObject gameObject;
        public PaletteApplyTargetKind kind;
        public string description;
        public string serializedColorProperty;
        public Renderer renderer;
        public Material material;
        public Component component;
    }

    public static class PaletteApplyUtility
    {
        public static List<PaletteApplyTarget> ScanSelection(bool includeRenderers, bool includeMaterialAssets, bool includeSpriteRenderers, bool includeUiGraphics, bool includeTmpText)
        {
            var results = new List<PaletteApplyTarget>();

            if (includeMaterialAssets)
            {
                UnityEngine.Object[] objects = Selection.objects;
                var seenMaterials = new HashSet<int>();
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] is Material material && seenMaterials.Add(material.GetInstanceID()))
                    {
                        results.Add(new PaletteApplyTarget
                        {
                            targetObject = material,
                            material = material,
                            kind = PaletteApplyTargetKind.MaterialAsset,
                            description = $"Selected material asset: {AssetDatabase.GetAssetPath(material)}"
                        });
                    }
                }
            }

            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return results;

            var seen = new HashSet<int>();
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject root = selected[i];
                if (root == null)
                    continue;

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    GameObject go = transforms[t].gameObject;
                    if (go == null || !seen.Add(go.GetInstanceID()))
                        continue;

                    if (includeRenderers)
                    {
                        Renderer renderer = go.GetComponent<Renderer>();
                        if (renderer != null && !(renderer is SpriteRenderer))
                        {
                            results.Add(new PaletteApplyTarget
                            {
                                targetObject = renderer,
                                gameObject = go,
                                kind = PaletteApplyTargetKind.RendererMaterial,
                                renderer = renderer,
                                description = $"Renderer material: {GetPath(go)}"
                            });
                        }
                    }

                    if (includeSpriteRenderers)
                    {
                        SpriteRenderer sprite = go.GetComponent<SpriteRenderer>();
                        if (sprite != null)
                        {
                            results.Add(new PaletteApplyTarget
                            {
                                targetObject = sprite,
                                gameObject = go,
                                kind = PaletteApplyTargetKind.SpriteRenderer,
                                component = sprite,
                                serializedColorProperty = "m_Color",
                                description = $"SpriteRenderer colour: {GetPath(go)}"
                            });
                        }
                    }

                    Component[] components = go.GetComponents<Component>();
                    for (int c = 0; c < components.Length; c++)
                    {
                        Component component = components[c];
                        if (component == null)
                            continue;

                        Type type = component.GetType();
                        if (includeUiGraphics && IsTypeOrBaseNamed(type, "UnityEngine.UI.Graphic"))
                        {
                            results.Add(new PaletteApplyTarget
                            {
                                targetObject = component,
                                gameObject = go,
                                kind = PaletteApplyTargetKind.UiGraphic,
                                component = component,
                                serializedColorProperty = "m_Color",
                                description = $"UI Graphic colour: {GetPath(go)} ({type.Name})"
                            });
                        }

                        if (includeTmpText && IsTypeOrBaseNamed(type, "TMPro.TMP_Text"))
                        {
                            results.Add(new PaletteApplyTarget
                            {
                                targetObject = component,
                                gameObject = go,
                                kind = PaletteApplyTargetKind.TmpText,
                                component = component,
                                serializedColorProperty = "m_fontColor",
                                description = $"TMP text colour: {GetPath(go)} ({type.Name})"
                            });
                        }
                    }
                }
            }

            return results;
        }

        public static int ApplyColor(List<PaletteApplyTarget> targets, Color color, bool modifySharedMaterials, string materialColorProperty)
        {
            if (targets == null || targets.Count == 0)
                return 0;

            int changed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                PaletteApplyTarget target = targets[i];
                if (target == null || target.targetObject == null)
                    continue;

                switch (target.kind)
                {
                    case PaletteApplyTargetKind.RendererMaterial:
                        changed += ApplyToRenderer(target.renderer, color, modifySharedMaterials, materialColorProperty);
                        break;
                    case PaletteApplyTargetKind.MaterialAsset:
                        if (ApplyToMaterial(target.material, color, materialColorProperty))
                            changed++;
                        break;
                    case PaletteApplyTargetKind.SpriteRenderer:
                    case PaletteApplyTargetKind.UiGraphic:
                    case PaletteApplyTargetKind.TmpText:
                        if (ApplyToSerializedColor(target.component, target.serializedColorProperty, color))
                            changed++;
                        break;
                }
            }

            return changed;
        }

        private static bool ApplyToMaterial(Material material, Color color, string preferredProperty)
        {
            if (material == null)
                return false;

            Undo.RecordObject(material, "Apply Palette Colour");
            string property = ResolveMaterialColorProperty(material, preferredProperty);
            if (!string.IsNullOrEmpty(property))
                material.SetColor(property, color);
            else
                material.color = color;

            EditorUtility.SetDirty(material);
            return true;
        }

        private static int ApplyToRenderer(Renderer renderer, Color color, bool modifySharedMaterials, string preferredProperty)
        {
            if (renderer == null)
                return 0;

            Material[] materials = modifySharedMaterials ? renderer.sharedMaterials : renderer.materials;
            if (materials == null || materials.Length == 0)
                return 0;

            int changed = 0;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                if (ApplyToMaterial(material, color, preferredProperty))
                    changed++;
            }

            return changed;
        }

        private static bool ApplyToSerializedColor(Component component, string propertyName, Color color)
        {
            if (component == null || string.IsNullOrEmpty(propertyName))
                return false;

            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Color)
                return false;

            Undo.RecordObject(component, "Apply Palette Colour");
            property.colorValue = color;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return true;
        }

        private static string ResolveMaterialColorProperty(Material material, string preferredProperty)
        {
            if (material == null)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredProperty) && material.HasProperty(preferredProperty))
                return preferredProperty;
            if (material.HasProperty("_BaseColor"))
                return "_BaseColor";
            if (material.HasProperty("_Color"))
                return "_Color";
            return null;
        }

        private static bool IsTypeOrBaseNamed(Type type, string fullName)
        {
            while (type != null)
            {
                if (string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        private static string GetPath(GameObject go)
        {
            if (go == null)
                return string.Empty;

            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
    #endif

}