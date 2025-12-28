using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu (fileName = "Lighting Preset", menuName ="Lighting Preset")]
public class LightingPreset : ScriptableObject
{
    public Gradient ambientColour; // atmospheric colour - set based on weather
    public Gradient directionalColour; // sun colour - set based on time
    public Gradient fogColour; // set based on weather
}
