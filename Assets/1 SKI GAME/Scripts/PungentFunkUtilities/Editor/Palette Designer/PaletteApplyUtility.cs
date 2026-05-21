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

    public class PaletteApplyReport
    {
        public int considered;
        public int included;
        public int omitted;
        public int changed;
        public int skipped;
        public int failed;
        public List<string> messages = new List<string>();

        public string Summary
        {
            get
            {
                string scope = included > 0 || omitted > 0 ? $"Included {included}, omitted {omitted}. " : string.Empty;
                return scope + $"Changed {changed}, skipped {skipped}, failed {failed}.";
            }
        }
    }

    public class PaletteApplyResolvedTarget
    {
        public PaletteApplyTarget target;
        public bool included = true;
        public bool hasColor = true;
        public Color color;
        public string colorLabel;
        public string skipReason;
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
            return ApplyColorDetailed(targets, color, modifySharedMaterials, materialColorProperty).changed;
        }

        public static PaletteApplyReport ApplyColorDetailed(List<PaletteApplyTarget> targets, Color color, bool modifySharedMaterials, string materialColorProperty)
        {
            if (targets == null || targets.Count == 0)
                return new PaletteApplyReport();

            var report = new PaletteApplyReport { considered = targets.Count, included = targets.Count };
            for (int i = 0; i < targets.Count; i++)
            {
                PaletteApplyTarget target = targets[i];
                if (target == null || target.targetObject == null)
                {
                    report.skipped++;
                    continue;
                }

                try
                {
                    int changedForTarget = ApplyToTarget(target, color, modifySharedMaterials, materialColorProperty);

                    if (changedForTarget > 0)
                        report.changed += changedForTarget;
                    else
                        report.skipped++;
                }
                catch (Exception ex)
                {
                    report.failed++;
                    if (report.messages.Count < 8)
                        report.messages.Add($"{target.description}: {ex.Message}");
                }
            }

            return report;
        }

        public static PaletteApplyReport ApplyMappedColorsDetailed(List<PaletteApplyResolvedTarget> mappedTargets, bool modifySharedMaterials, string materialColorProperty)
        {
            if (mappedTargets == null || mappedTargets.Count == 0)
                return new PaletteApplyReport();

            var report = new PaletteApplyReport { considered = mappedTargets.Count };
            for (int i = 0; i < mappedTargets.Count; i++)
            {
                PaletteApplyResolvedTarget mapped = mappedTargets[i];
                if (mapped == null || !mapped.included)
                {
                    report.omitted++;
                    continue;
                }

                report.included++;
                PaletteApplyTarget target = mapped.target;
                if (target == null || target.targetObject == null)
                {
                    report.skipped++;
                    continue;
                }

                if (!mapped.hasColor)
                {
                    report.skipped++;
                    if (report.messages.Count < 8)
                    {
                        string reason = string.IsNullOrWhiteSpace(mapped.skipReason) ? "No palette colour resolved." : mapped.skipReason;
                        report.messages.Add($"{target.description}: {reason}");
                    }
                    continue;
                }

                try
                {
                    int changedForTarget = ApplyToTarget(target, mapped.color, modifySharedMaterials, materialColorProperty);
                    if (changedForTarget > 0)
                        report.changed += changedForTarget;
                    else
                        report.skipped++;
                }
                catch (Exception ex)
                {
                    report.failed++;
                    if (report.messages.Count < 8)
                        report.messages.Add($"{target.description}: {ex.Message}");
                }
            }

            return report;
        }

        private static int ApplyToTarget(PaletteApplyTarget target, Color color, bool modifySharedMaterials, string materialColorProperty)
        {
            if (target == null)
                return 0;

            switch (target.kind)
            {
                case PaletteApplyTargetKind.RendererMaterial:
                    return ApplyToRenderer(target.renderer, color, modifySharedMaterials, materialColorProperty);
                case PaletteApplyTargetKind.MaterialAsset:
                    return ApplyToMaterial(target.material, color, materialColorProperty) ? 1 : 0;
                case PaletteApplyTargetKind.SpriteRenderer:
                case PaletteApplyTargetKind.UiGraphic:
                case PaletteApplyTargetKind.TmpText:
                    return ApplyToSerializedColor(target.component, target.serializedColorProperty, color) ? 1 : 0;
                default:
                    return 0;
            }
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
