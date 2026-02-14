using UnityEngine;

public class WearableAttachment : MonoBehaviour
{
    [Header("Mount")]
    [Tooltip(
        "A child transform inside the wearable prefab that represents the anchor point/orientation.\n" +
        "Author this in the prefab so it sits where the character anchor should land.\n\n" +
        "Runtime alignment will place this mount exactly on the character's anchor.")]
    public Transform mount;

    [Header("Visuals")]
    [Tooltip("All renderers on this wearable that should have their color/material changed.")]
    public Renderer[] renderers;

    [Header("Shader Properties")]
    [Tooltip("Color property used by your URP material (e.g., _BaseColor or Base_Colour).")]
    public string colorPropertyName = "_BaseColor";

    [Tooltip("Texture property used by your URP material (e.g., _BaseMap or _MainTex).")]
    public string texturePropertyName = "_BaseMap";

    private int _colorPropertyId;
    private int _texturePropertyId;

    private void Awake()
    {
        if (!string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);

        if (!string.IsNullOrEmpty(texturePropertyName))
            _texturePropertyId = Shader.PropertyToID(texturePropertyName);

        // If the prefab author forgot to assign mount explicitly, try to auto-resolve.
        if (mount == null)
            mount = FindMountChild(transform);
    }

    private static Transform FindMountChild(Transform root)
    {
        // Preferred conventional names
        var t = root.Find("Mount");
        if (t != null) return t;

        t = root.Find("mount");
        if (t != null) return t;

        t = root.Find("Offset");
        if (t != null) return t;

        t = root.Find("offset");
        if (t != null) return t;

        // As a last resort, scan children for any child named Mount-like
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
            var n = child.name;
            if (n == "Mount" || n == "mount" || n == "Offset" || n == "offset")
                return child;
        }

        return null;
    }

    public void SetColor(Color color)
    {
        if (renderers == null || renderers.Length == 0) return;

        if (_colorPropertyId == 0 && !string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mat = r.material;
            if (mat != null && mat.HasProperty(_colorPropertyId))
                mat.SetColor(_colorPropertyId, color);
        }
    }

    public void SetTexture(Texture tex)
    {
        if (renderers == null || renderers.Length == 0) return;

        if (_texturePropertyId == 0 && !string.IsNullOrEmpty(texturePropertyName))
            _texturePropertyId = Shader.PropertyToID(texturePropertyName);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mat = r.material;
            if (mat != null && mat.HasProperty(_texturePropertyId))
                mat.SetTexture(_texturePropertyId, tex);
        }
    }

    public void SetMaterial(Material mat)
    {
        if (renderers == null || renderers.Length == 0 || mat == null) return;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.material = mat;
        }
    }
}
