using System;
using System.Collections.Generic;
using UnityEngine;

public class WearableAttachment : MonoBehaviour
{
    public const string PrimaryChannelId = "primary";
    private static readonly string[] ColorPropCandidates = { "_BaseColor", "_Color", "Base_Colour", "BaseColor" };

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

    private Color _lastPrimaryColor = Color.white;
    private readonly Dictionary<string, Color> _lastChannelColors = new Dictionary<string, Color>();

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
        _lastPrimaryColor = color;

        if (_colorPropertyId == 0 && !string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);

        ApplyColorToBindings(primaryBindings, ResolvePrimaryRenderers(), colorPropertyName, _colorPropertyId, color);
        ApplyComponentColorBindings(primaryComponentBindings, color);
    }

    public void SetChannelColor(string channelId, Color color)
    {
        if (string.IsNullOrEmpty(channelId) || string.Equals(channelId, PrimaryChannelId, StringComparison.Ordinal))
        {
            SetColor(color);
            return;
        }

        _lastChannelColors[channelId] = color;

        var channel = GetOrCreateRuntimeChannel(channelId, color);

        int propId = 0;
        if (!string.IsNullOrEmpty(channel.colorPropertyName))
            propId = Shader.PropertyToID(channel.colorPropertyName);

        ApplyColorToChannel(channel, propId, color);
        ApplyComponentColorBindings(channel.componentBindings, color);
    }

    public void AddPrimaryRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (renderers == null || renderers.Length == 0)
        {
            renderers = new[] { renderer };
            SetColor(_lastPrimaryColor);
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == renderer)
                return;
        }

        Renderer[] newArray = new Renderer[renderers.Length + 1];
        for (int i = 0; i < renderers.Length; i++)
            newArray[i] = renderers[i];

        newArray[renderers.Length] = renderer;
        renderers = newArray;

        SetColor(_lastPrimaryColor);
    }

    public void RemovePrimaryRenderer(Renderer renderer)
    {
        if (renderer == null || renderers == null || renderers.Length == 0)
            return;

        int removeIndex = -1;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == renderer)
            {
                removeIndex = i;
                break;
            }
        }

        if (removeIndex < 0)
            return;

        if (renderers.Length == 1)
        {
            renderers = Array.Empty<Renderer>();
            return;
        }

        Renderer[] newArray = new Renderer[renderers.Length - 1];
        int dst = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (i == removeIndex)
                continue;

            newArray[dst++] = renderers[i];
        }

        renderers = newArray;
    }

    public void AddExtraRenderer(string channelId, Renderer renderer)
    {
        if (renderer == null || string.IsNullOrWhiteSpace(channelId))
            return;

        var channel = GetOrCreateRuntimeChannel(channelId, _lastChannelColors.TryGetValue(channelId, out var savedColor) ? savedColor : Color.white);

        if (channel.renderers == null || channel.renderers.Length == 0)
        {
            channel.renderers = new[] { renderer };
            ReapplyChannelColor(channelId);
            return;
        }

        for (int i = 0; i < channel.renderers.Length; i++)
        {
            if (channel.renderers[i] == renderer)
                return;
        }

        Renderer[] newArray = new Renderer[channel.renderers.Length + 1];
        for (int i = 0; i < channel.renderers.Length; i++)
            newArray[i] = channel.renderers[i];

        newArray[channel.renderers.Length] = renderer;
        channel.renderers = newArray;

        ReapplyChannelColor(channelId);
    }

    private WearableColorChannel GetOrCreateRuntimeChannel(string channelId, Color defaultColor)
    {
        var channel = GetChannel(channelId);
        if (channel != null)
            return channel;

        if (extraChannels == null)
            extraChannels = new List<WearableColorChannel>();

        channel = new WearableColorChannel
        {
            id = channelId,
            displayName = channelId,
            colorPropertyName = colorPropertyName,
            defaultColor = defaultColor,
            renderers = Array.Empty<Renderer>()
        };

        extraChannels.Add(channel);
        return channel;
    }

    public void RemoveExtraRenderer(string channelId, Renderer renderer)
    {
        if (renderer == null || string.IsNullOrWhiteSpace(channelId))
            return;

        var channel = GetChannel(channelId);
        if (channel == null || channel.renderers == null || channel.renderers.Length == 0)
            return;

        int removeIndex = -1;
        for (int i = 0; i < channel.renderers.Length; i++)
        {
            if (channel.renderers[i] == renderer)
            {
                removeIndex = i;
                break;
            }
        }

        if (removeIndex < 0)
            return;

        if (channel.renderers.Length == 1)
        {
            channel.renderers = Array.Empty<Renderer>();
            return;
        }

        Renderer[] newArray = new Renderer[channel.renderers.Length - 1];
        int dst = 0;
        for (int i = 0; i < channel.renderers.Length; i++)
        {
            if (i == removeIndex)
                continue;

            newArray[dst++] = channel.renderers[i];
        }

        channel.renderers = newArray;
    }

    private void ReapplyChannelColor(string channelId)
    {
        if (!_lastChannelColors.TryGetValue(channelId, out var color))
        {
            var channel = GetChannel(channelId);
            color = channel != null ? channel.defaultColor : Color.white;
        }

        SetChannelColor(channelId, color);
    }

    public void SetTexture(Texture tex)
    {
        if (_texturePropertyId == 0 && !string.IsNullOrEmpty(texturePropertyName))
            _texturePropertyId = Shader.PropertyToID(texturePropertyName);

        ApplyTextureToBindings(primaryBindings, ResolvePrimaryRenderers(), _texturePropertyId, tex);
    }

    public void SetMaterial(Material mat)
    {
        if (mat == null)
            return;

        ApplyMaterialToBindings(primaryBindings, ResolvePrimaryRenderers(), mat);
    }

    private void ApplyColorToChannel(WearableColorChannel channel, int colorPropertyId, Color color)
    {
        if (channel == null)
            return;

        var resolved = ResolveRenderers(channel);
        ApplyColorToBindings(channel.bindings, resolved, channel.colorPropertyName, colorPropertyId, color);
    }

    private static Renderer[] ResolveRenderers(WearableColorChannel channel)
    {
        if (channel == null)
            return Array.Empty<Renderer>();

        if (channel.renderers != null && channel.renderers.Length > 0)
            return DeduplicateRenderers(channel.renderers);

        if (channel.targetRoot != null)
            return GetLocalRenderers(channel.targetRoot);

        return Array.Empty<Renderer>();
    }

    public bool HasPrimaryVisualTargets()
    {
        if (HasValidBindings(primaryBindings))
            return true;

        return ResolvePrimaryRenderers().Length > 0 || (primaryComponentBindings != null && primaryComponentBindings.Count > 0);
    }

    private Renderer[] ResolvePrimaryRenderers()
    {
        if (renderers != null && renderers.Length > 0)
            return DeduplicateRenderers(renderers);

        return GetLocalRenderers(transform);
    }

    private static bool HasValidBindings(List<WearableMaterialBinding> bindings)
    {
        if (bindings == null)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding != null && binding.renderer != null)
                return true;
        }

        return false;
    }

    private static Renderer[] GetLocalRenderers(Transform root)
    {
        if (root == null)
            return Array.Empty<Renderer>();

        return FilterLocalRenderers(root, root.GetComponentsInChildren<Renderer>(true));
    }

    private static Renderer[] DeduplicateRenderers(Renderer[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
            return Array.Empty<Renderer>();

        var filtered = new List<Renderer>(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            var renderer = candidates[i];
            if (renderer == null || filtered.Contains(renderer))
                continue;

            filtered.Add(renderer);
        }

        return filtered.ToArray();
    }

    private static Renderer[] FilterLocalRenderers(Transform root, Renderer[] candidates, bool includeFallbackChildrenWhenEmpty = false)
    {
        Transform effectiveRoot = root;
        if (effectiveRoot == null && candidates != null)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                {
                    effectiveRoot = candidates[i].transform.root;
                    break;
                }
            }
        }

        var filtered = new List<Renderer>();
        if (candidates != null)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                var renderer = candidates[i];
                if (renderer == null)
                    continue;

                if (effectiveRoot != null && !renderer.transform.IsChildOf(effectiveRoot))
                    continue;

                if (!filtered.Contains(renderer))
                    filtered.Add(renderer);
            }
        }

        if (filtered.Count == 0 && includeFallbackChildrenWhenEmpty && effectiveRoot != null)
        {
            var localChildren = effectiveRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < localChildren.Length; i++)
            {
                var renderer = localChildren[i];
                if (renderer == null)
                    continue;

                if (!filtered.Contains(renderer))
                    filtered.Add(renderer);
            }
        }

        return filtered.ToArray();
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

    private static void ApplyColorToBindings(List<WearableMaterialBinding> bindings, Renderer[] legacyRenderers, string configuredPropertyName, int colorPropertyId, Color color)
    {
        HashSet<Renderer> bindingRenderers = null;

        if (bindings != null && bindings.Count > 0)
        {
            bindingRenderers = new HashSet<Renderer>();
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.renderer == null)
                    continue;

                bindingRenderers.Add(binding.renderer);

                var mats = binding.renderer.materials;
                if (mats == null || mats.Length == 0)
                    continue;

                var indices = GetValidMaterialIndices(binding.materialIndices, mats.Length);
                bool changed = false;

                for (int j = 0; j < indices.Count; j++)
                {
                    int idx = indices[j];
                    var mat = mats[idx];
                    if (ApplyColorToMaterial(mat, color, configuredPropertyName, colorPropertyId))
                    {
                        mats[idx] = mat;
                        changed = true;
                    }
                }

                if (changed)
                    binding.renderer.materials = mats;
            }
        }

        if (legacyRenderers == null || legacyRenderers.Length == 0)
            return;

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            var r = legacyRenderers[i];
            if (r == null || (bindingRenderers != null && bindingRenderers.Contains(r)))
                continue;

            var mats = r.materials;
            if (mats == null || mats.Length == 0)
                continue;

            bool changed = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (ApplyColorToMaterial(mat, color, configuredPropertyName, colorPropertyId))
                {
                    mats[m] = mat;
                    changed = true;
                }
            }

            if (changed)
                r.materials = mats;
        }
    }

    private static bool ApplyColorToMaterial(Material mat, Color color, string configuredPropertyName, int configuredPropertyId)
    {
        if (mat == null)
            return false;

        bool changed = false;

        if (!string.IsNullOrEmpty(configuredPropertyName) && mat.HasProperty(configuredPropertyName))
        {
            mat.SetColor(configuredPropertyId != 0 ? configuredPropertyId : Shader.PropertyToID(configuredPropertyName), color);
            changed = true;
        }

        for (int i = 0; i < ColorPropCandidates.Length; i++)
        {
            string propertyName = ColorPropCandidates[i];
            if (string.IsNullOrEmpty(propertyName) || propertyName == configuredPropertyName || !mat.HasProperty(propertyName))
                continue;

            mat.SetColor(propertyName, color);
            changed = true;
        }

        return changed;
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
