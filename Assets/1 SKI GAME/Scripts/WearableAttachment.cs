using UnityEngine;

public class WearableAttachment : MonoBehaviour
{
    [Header("Positioning")]
    [Tooltip("Empty child transform that marks where this wearable expects the character's anchor to be.")]
    public Transform offset;

    [Header("Visuals")]
    [Tooltip("All renderers on this wearable that should have their color/material changed.")]
    public Renderer[] renderers;

    [Header("Shader Properties")]
    [Tooltip("Color property used by your URP material (e.g., _BaseColor or Base_Colour).")]
    public string colorPropertyName = "_BaseColor";

    private int _colorPropertyId;

    private void Awake()
    {
        if (!string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);
    }

    public void SetColor(Color color)
    {
        if (renderers == null || renderers.Length == 0) return;
        if (_colorPropertyId == 0 && !string.IsNullOrEmpty(colorPropertyName))
            _colorPropertyId = Shader.PropertyToID(colorPropertyName);

        foreach (var r in renderers)
        {
            if (r == null) continue;

            // r.material gives you an instance per object (good for per-character tweaks)
            var mat = r.material;
            if (mat != null && mat.HasProperty(_colorPropertyId))
            {
                mat.SetColor(_colorPropertyId, color);
            }
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
