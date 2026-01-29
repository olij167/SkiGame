using System;
using UnityEngine;

/// <summary>
/// Attach to the same GameObject as SkiController.
/// Holds currently equipped skis + poles, and exposes merged tuning.
/// </summary>
[DisallowMultipleComponent]
public class SkiGearLoadout : MonoBehaviour
{
    [Header("Equipped Gear")]
    [SerializeField] private SkiGearProfileSO equippedSkis;
    [SerializeField] private SkiGearProfileSO equippedPoles;

    private string lastSkis;
    private string lastPoles;
    /// <summary>Raised when gear changes so SkiController can refresh cached tuning.</summary>
    public event Action OnTuningChanged;

    public SkiGearProfileSO EquippedSkis => equippedSkis;
    public SkiGearProfileSO EquippedPoles => equippedPoles;

    public void EquipSkis(SkiGearProfileSO skis)
    {
        if (equippedSkis == skis) return;
        equippedSkis = skis;
        lastSkis = (skis != null) ? skis.displayName : null;
        OnTuningChanged?.Invoke();
    }

    public void EquipPoles(SkiGearProfileSO poles)
    {
        if (equippedPoles == poles) return;
        equippedPoles = poles;
        lastPoles = (poles != null) ? poles.displayName : null;
        OnTuningChanged?.Invoke();
    }

    [ContextMenu("Equip Gear")]
    public void EquipGear()
    {
        // If references changed in the inspector (without calling EquipSkis/EquipPoles),
        // this will correctly detect and apply them.
        string skisName = (equippedSkis != null) ? equippedSkis.displayName : null;
        string polesName = (equippedPoles != null) ? equippedPoles.displayName : null;


        if (skisName != lastSkis)
        {
            lastSkis = skisName;
        }

        if (polesName != lastPoles)
        {
            lastPoles = polesName;
        }

        // If the reference changed, we want the controller to refresh.
        // If the reference didn't change, we STILL want a refresh (common case: tuning edited on the same SO).
        OnTuningChanged?.Invoke();
    }

    public SkiGearTuning GetTuning()
    {
        // Skis dominate most movement; poles typically only affect poleImpulseMul,
        // but we keep it flexible by allowing both to contribute.
        SkiGearTuning t = SkiGearTuning.Default;

        if (equippedSkis != null)
            t = SkiGearTuning.Multiply(t, equippedSkis.tuning);

        if (equippedPoles != null)
            t = SkiGearTuning.Multiply(t, equippedPoles.tuning);

        return t;
    }
}
