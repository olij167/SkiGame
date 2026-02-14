using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SkiGearLoadout : MonoBehaviour
{
    [Header("Equipped Gear")]
    [SerializeField] private SkiGearProfileSO equippedSkis;
    [SerializeField] private SkiGearProfileSO equippedPoles;

    [Header("Colours")]
    [SerializeField] private Color skisColor = Color.white;
    [SerializeField] private Color polesColor = Color.white;

    [Header("Patterns (Textures)")]
    [SerializeField] private Texture2D skisPattern;
    [SerializeField] private Texture2D polesPattern;

    public event Action OnTuningChanged;

    public SkiGearProfileSO EquippedSkis => equippedSkis;
    public SkiGearProfileSO EquippedPoles => equippedPoles;

    public void EquipSkis(SkiGearProfileSO skis)
    {
        if (equippedSkis == skis) return;
        equippedSkis = skis;
        OnTuningChanged?.Invoke();
    }

    public void EquipPoles(SkiGearProfileSO poles)
    {
        if (equippedPoles == poles) return;
        equippedPoles = poles;
        OnTuningChanged?.Invoke();
    }

    public void SetSkisColor(Color c)
    {
        if (skisColor == c) return;
        skisColor = c;
        OnTuningChanged?.Invoke();
    }

    public void SetPolesColor(Color c)
    {
        if (polesColor == c) return;
        polesColor = c;
        OnTuningChanged?.Invoke();
    }

    public void SetSkisPattern(Texture2D tex)
    {
        if (skisPattern == tex) return;
        skisPattern = tex;
        OnTuningChanged?.Invoke();
    }

    public void SetPolesPattern(Texture2D tex)
    {
        if (polesPattern == tex) return;
        polesPattern = tex;
        OnTuningChanged?.Invoke();
    }

    public Color GetSkisColor() => skisColor;
    public Color GetPolesColor() => polesColor;

    public Texture2D GetSkisPattern() => skisPattern;
    public Texture2D GetPolesPattern() => polesPattern;

    public SkiGearTuning GetTuning()
    {
        SkiGearTuning t = SkiGearTuning.Default;
        if (equippedSkis != null) t = SkiGearTuning.Multiply(t, equippedSkis.tuning);
        if (equippedPoles != null) t = SkiGearTuning.Multiply(t, equippedPoles.tuning);
        return t;
    }
}
