using System;
using System.Collections.Generic;
using UnityEngine;

public class WearableAttachment : MonoBehaviour
{
    public const string PrimaryChannelId = "primary";

    [Serializable]
    public class WearableMaterialBinding
    {
        [Tooltip("Renderer whose material slots should be affected.")]
        public Renderer renderer;

        [Tooltip("Material indices within renderer.materials that should be affected.")]
        public List<int> materialIndices = new List<int> { 0 };
    }

    public enum WearableColorTargetType
    {
        Light,
        ParticleSystem,
        TrailRenderer,
        LineRenderer,
        SpriteRenderer
    }

    [Serializable]
    public class WearableComponentColorBinding
    {
        [Tooltip("Which type of component this binding targets.")]
        public WearableColorTargetType targetType = WearableColorTargetType.Light;

        [Tooltip("Assign the GameObject that contains the component to recolour.")]
        public GameObject targetObject;

        [Tooltip("For ParticleSystem: if enabled, updates the Main.startColor.")]
        public bool affectParticleStartColor = true;

        [Tooltip("For TrailRenderer / LineRenderer: if enabled, applies to startColor.")]
        public bool affectStartColor = true;

        [Tooltip("For TrailRenderer / LineRenderer: if enabled, applies to endColor.")]
        public bool affectEndColor = true;
    }

    [Serializable]
    public class WearableColorChannel
    {
        [Tooltip("Stable save key for this channel, e.g. trim / strap / lining.")]
        public string id = "accent";

        [Tooltip("UI-facing label.")]
        public string displayName = "Accent";

        [Tooltip("Optional root to auto-collect renderers from if bindings and legacy renderers are empty.")]
        public Transform targetRoot;

        [Tooltip("Bindings for the exact renderer/material slots affected by this channel.")]
        public List<WearableMaterialBinding> bindings = new List<WearableMaterialBinding>();

        [Tooltip("Optional non-material colour targets for this channel (lights, particles, trails, etc.).")]
        public List<WearableComponentColorBinding> componentBindings = new List<WearableComponentColorBinding>();

        [Tooltip("Legacy fallback: if bindings is empty, these renderers will be used with all materials.")]
        public Renderer[] renderers;

        [Tooltip("Color property used by the shader for this channel.")]
        public string colorPropertyName = "_BaseColor";

        [Tooltip("Default colour when no saved override exists.")]
        public Color defaultColor = Color.white;
    }

    [Header("Mount")]
    [Tooltip(
        "A child transform inside the wearable prefab that represents the anchor point/orientation.\n" +
        "Author this in the prefab so it sits where the character anchor should land.\n\n" +
        "Runtime alignment will place this mount exactly on the character's anchor.")]
    public Transform mount;

    [Header("Primary Visuals")]
    [Tooltip("Preferred authoring: exact renderer/material-slot bindings for the primary channel.")]
    public List<WearableMaterialBinding> primaryBindings = new List<WearableMaterialBinding>();

    [Tooltip("Optional non-material colour targets for the primary channel (lights, particles, trails, etc.).")]
    public List<WearableComponentColorBinding> primaryComponentBindings = new List<WearableComponentColorBinding>();

    [Tooltip("Legacy fallback if primaryBindings is empty.")]
    public Renderer[] renderers;

    [Header("Shader Properties")]
    [Tooltip("Primary colour property used by your material (e.g., _BaseColor or Base_Colour).")]
    public string colorPropertyName = "_BaseColor";

    [Tooltip("Texture property used by your material (e.g., _BaseMap or _MainTex).")]
    public string texturePropertyName = "_BaseMap";

    [Header("Optional Extra Colour Channels")]
    [Tooltip("Only populate this for items that support more than one colour.")]
    public List<WearableColorChannel> extraChannels = new List<WearableColorChannel>();

    private int _colorPropertyId;
    private int _texturePropertyId;

    private void Awake()
    {
        if (!string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);

        if (!string.IsNullOrEmpty(texturePropertyName))
            _texturePropertyId = Shader.PropertyToID(texturePropertyName);

        if (mount == null)
            mount = FindMountChild(transform);
    }

    private static Transform FindMountChild(Transform root)
    {
        var t = root.Find("Mount");
        if (t != null) return t;

        t = root.Find("mount");
        if (t != null) return t;

        t = root.Find("Offset");
        if (t != null) return t;

        t = root.Find("offset");
        if (t != null) return t;

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
            var n = child.name;
            if (n == "Mount" || n == "mount" || n == "Offset" || n == "offset")
                return child;
        }

        return null;
    }

    public IReadOnlyList<WearableColorChannel> GetExtraChannels() => extraChannels;

    public WearableColorChannel GetChannel(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
            return null;

        if (string.Equals(channelId, PrimaryChannelId, StringComparison.Ordinal))
            return null;

        if (extraChannels == null)
            return null;

        for (int i = 0; i < extraChannels.Count; i++)
        {
            var channel = extraChannels[i];
            if (channel == null || string.IsNullOrEmpty(channel.id))
                continue;

            if (string.Equals(channel.id, channelId, StringComparison.Ordinal))
                return channel;
        }

        return null;
    }

    public void SetColor(Color color)
    {
        if (_colorPropertyId == 0 && !string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);

        ApplyColorToBindings(primaryBindings, renderers, _colorPropertyId, color);
        ApplyComponentColorBindings(primaryComponentBindings, color);
    }

    public void SetChannelColor(string channelId, Color color)
    {
        if (string.IsNullOrEmpty(channelId) || string.Equals(channelId, PrimaryChannelId, StringComparison.Ordinal))
        {
            SetColor(color);
            return;
        }

        var channel = GetChannel(channelId);
        if (channel == null)
            return;

        int propId = 0;
        if (!string.IsNullOrEmpty(channel.colorPropertyName))
            propId = Shader.PropertyToID(channel.colorPropertyName);

        ApplyColorToChannel(channel, propId, color);
        ApplyComponentColorBindings(channel.componentBindings, color);
    }

    public void SetTexture(Texture tex)
    {
        if (_texturePropertyId == 0 && !string.IsNullOrEmpty(texturePropertyName))
            _texturePropertyId = Shader.PropertyToID(texturePropertyName);

        ApplyTextureToBindings(primaryBindings, renderers, _texturePropertyId, tex);
    }

    public void SetMaterial(Material mat)
    {
        if (mat == null)
            return;

        ApplyMaterialToBindings(primaryBindings, renderers, mat);
    }

    private void ApplyColorToChannel(WearableColorChannel channel, int colorPropertyId, Color color)
    {
        if (channel == null)
            return;

        if (channel.bindings != null && channel.bindings.Count > 0)
        {
            ApplyColorToBindings(channel.bindings, null, colorPropertyId, color);
            return;
        }

        var resolved = ResolveRenderers(channel);
        ApplyColorToBindings(null, resolved, colorPropertyId, color);
    }

    private static Renderer[] ResolveRenderers(WearableColorChannel channel)
    {
        if (channel == null)
            return Array.Empty<Renderer>();

        if (channel.renderers != null && channel.renderers.Length > 0)
            return channel.renderers;

        if (channel.targetRoot != null)
            return channel.targetRoot.GetComponentsInChildren<Renderer>(true);

        return Array.Empty<Renderer>();
    }

    private static List<int> GetValidMaterialIndices(List<int> rawIndices, int materialCount)
    {
        var result = new List<int>();

        if (materialCount <= 0)
            return result;

        if (rawIndices == null || rawIndices.Count == 0)
        {
            result.Add(0);
            return result;
        }

        for (int i = 0; i < rawIndices.Count; i++)
        {
            int index = rawIndices[i];
            if (index >= 0 && index < materialCount && !result.Contains(index))
                result.Add(index);
        }

        if (result.Count == 0)
            result.Add(0);

        return result;
    }

    private static void ApplyColorToBindings(List<WearableMaterialBinding> bindings, Renderer[] legacyRenderers, int colorPropertyId, Color color)
    {
        if (colorPropertyId == 0)
            return;

        if (bindings != null && bindings.Count > 0)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.renderer == null)
                    continue;

                var mats = binding.renderer.materials;
                if (mats == null || mats.Length == 0)
                    continue;

                var indices = GetValidMaterialIndices(binding.materialIndices, mats.Length);
                bool changed = false;

                for (int j = 0; j < indices.Count; j++)
                {
                    int idx = indices[j];
                    var mat = mats[idx];
                    if (mat != null && mat.HasProperty(colorPropertyId))
                    {
                        mat.SetColor(colorPropertyId, color);
                        mats[idx] = mat;
                        changed = true;
                    }
                }

                if (changed)
                    binding.renderer.materials = mats;
            }

            return;
        }

        if (legacyRenderers == null || legacyRenderers.Length == 0)
            return;

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            var r = legacyRenderers[i];
            if (r == null)
                continue;

            var mats = r.materials;
            if (mats == null || mats.Length == 0)
                continue;

            bool changed = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat != null && mat.HasProperty(colorPropertyId))
                {
                    mat.SetColor(colorPropertyId, color);
                    mats[m] = mat;
                    changed = true;
                }
            }

            if (changed)
                r.materials = mats;
        }
    }

    private static void ApplyTextureToBindings(List<WearableMaterialBinding> bindings, Renderer[] legacyRenderers, int texturePropertyId, Texture tex)
    {
        if (texturePropertyId == 0)
            return;

        if (bindings != null && bindings.Count > 0)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.renderer == null)
                    continue;

                var mats = binding.renderer.materials;
                if (mats == null || mats.Length == 0)
                    continue;

                var indices = GetValidMaterialIndices(binding.materialIndices, mats.Length);
                bool changed = false;

                for (int j = 0; j < indices.Count; j++)
                {
                    int idx = indices[j];
                    var mat = mats[idx];
                    if (mat != null && mat.HasProperty(texturePropertyId))
                    {
                        mat.SetTexture(texturePropertyId, tex);
                        mats[idx] = mat;
                        changed = true;
                    }
                }

                if (changed)
                    binding.renderer.materials = mats;
            }

            return;
        }

        if (legacyRenderers == null || legacyRenderers.Length == 0)
            return;

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            var r = legacyRenderers[i];
            if (r == null)
                continue;

            var mats = r.materials;
            if (mats == null || mats.Length == 0)
                continue;

            bool changed = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat != null && mat.HasProperty(texturePropertyId))
                {
                    mat.SetTexture(texturePropertyId, tex);
                    mats[m] = mat;
                    changed = true;
                }
            }

            if (changed)
                r.materials = mats;
        }
    }

    private static void ApplyMaterialToBindings(List<WearableMaterialBinding> bindings, Renderer[] legacyRenderers, Material material)
    {
        if (material == null)
            return;

        if (bindings != null && bindings.Count > 0)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.renderer == null)
                    continue;

                var mats = binding.renderer.materials;
                if (mats == null || mats.Length == 0)
                    continue;

                var indices = GetValidMaterialIndices(binding.materialIndices, mats.Length);
                bool changed = false;

                for (int j = 0; j < indices.Count; j++)
                {
                    int idx = indices[j];
                    mats[idx] = material;
                    changed = true;
                }

                if (changed)
                    binding.renderer.materials = mats;
            }

            return;
        }

        if (legacyRenderers == null || legacyRenderers.Length == 0)
            return;

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            var r = legacyRenderers[i];
            if (r == null)
                continue;

            var mats = r.materials;
            if (mats == null || mats.Length == 0)
                continue;

            for (int m = 0; m < mats.Length; m++)
                mats[m] = material;

            r.materials = mats;
        }
    }

    private static void ApplyComponentColorBindings(List<WearableComponentColorBinding> bindings, Color color)
    {
        if (bindings == null || bindings.Count == 0)
            return;

        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding == null || binding.targetObject == null)
                continue;

            switch (binding.targetType)
            {
                case WearableColorTargetType.Light:
                    {
                        var light = binding.targetObject.GetComponent<Light>();
                        if (light != null)
                            light.color = color;
                        break;
                    }

                case WearableColorTargetType.ParticleSystem:
                    {
                        var ps = binding.targetObject.GetComponent<ParticleSystem>();
                        if (ps != null && binding.affectParticleStartColor)
                        {
                            var main = ps.main;
                            main.startColor = color;
                        }
                        break;
                    }

                case WearableColorTargetType.TrailRenderer:
                    {
                        var trail = binding.targetObject.GetComponent<TrailRenderer>();
                        if (trail != null)
                        {
                            if (binding.affectStartColor)
                                trail.startColor = color;
                            if (binding.affectEndColor)
                                trail.endColor = color;
                        }
                        break;
                    }

                case WearableColorTargetType.LineRenderer:
                    {
                        var line = binding.targetObject.GetComponent<LineRenderer>();
                        if (line != null)
                        {
                            if (binding.affectStartColor)
                                line.startColor = color;
                            if (binding.affectEndColor)
                                line.endColor = color;
                        }
                        break;
                    }

                case WearableColorTargetType.SpriteRenderer:
                    {
                        var sprite = binding.targetObject.GetComponent<SpriteRenderer>();
                        if (sprite != null)
                            sprite.color = color;
                        break;
                    }
            }
        }
    }
}