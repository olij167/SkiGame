using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TrickPoseProfile", menuName = "Ski Game/Trick Pose Profile")]
public class TrickPoseProfileSO : ScriptableObject
{
    public List<TrickPoseEntry> entries = new List<TrickPoseEntry>();
    [Min(0f)] public float defaultBlendInSpeed = 8f;
    [Min(0f)] public float defaultBlendOutSpeed = 8f;
    [Min(0f)] public float previewLerpSpeed = 10f;
}
