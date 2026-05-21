using PungentFunk.Utilities.Audio;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SkierAudioConfig",
    menuName = "SkiGame/Audio/Skier Audio Config")]
public sealed class SkierAudioConfigSO : ScriptableObject
{
    [Header("Base Ski Layer")]
    [SerializeField] private AudioClip baseSkiLoopClip;
    [SerializeField] private AnimationCurve speedToBaseVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve speedToBasePitch = AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f);

    [Header("Carve Layer")]
    [SerializeField] private AudioClip carveLoopClip;
    [SerializeField] private AudioClipSetSO carveEntryAccentClipSet;
    [SerializeField] private AnimationCurve carve01ToVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve carve01ToPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.1f);
    [SerializeField] private AnimationCurve carve01ToBaseDuck = AnimationCurve.Linear(0f, 0f, 1f, 0.35f);

    [Header("Wind Layer")]
    [SerializeField] private AudioClip windLoopClip;
    [SerializeField] private AnimationCurve speedToWindVolume = AnimationCurve.Linear(0.2f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve speedToWindPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.4f);

    [Header("Pole Layer")]
    [SerializeField] private AudioClip poleDragLoopClip;
    [SerializeField] private AudioClipSetSO polePlantAccentClipSet;
    [SerializeField] private AudioClipSetSO poleReleaseAccentClipSet;
    [SerializeField] private AnimationCurve poleDragAmountToVolume = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);
    [SerializeField] private AnimationCurve speedToPolePitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.1f);

    [Header("Landing / Recontact")]
    [SerializeField] private AudioClipSetSO landingSoftAccentClipSet;
    [SerializeField] private AudioClipSetSO landingHardAccentClipSet;
    [SerializeField] private AnimationCurve landingSeverityToVolume = AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f);

    [Header("Stack / Body Drag")]
    [SerializeField] private AudioClip bodyDragLoopClip;
    [SerializeField] private AudioClipSetSO stackImpactAccentClipSet;
    [SerializeField] private AnimationCurve stackSeverityToVolume = AnimationCurve.Linear(0f, 0.35f, 1f, 1f);
    [SerializeField] private AnimationCurve bodyDragSpeedToPitch = AnimationCurve.Linear(0f, 0.85f, 1f, 1.1f);

    [Header("General Smoothing / Speed")]
    [SerializeField, Min(0.1f)] private float maxSpeedForAudio = 30f;
    [SerializeField, Min(1f)] private float maxCarveAngleForAudio = 45f;
    [SerializeField, Range(0f, 1f)] private float carveEntryThreshold = 0.55f;
    [SerializeField, Min(0f)] private float carveEntryCooldown = 0.18f;
    [SerializeField, Min(0f)] private float volumeLerpSpeed = 8f;
    [SerializeField, Min(0f)] private float pitchLerpSpeed = 8f;
    [SerializeField, Min(0f)] private float baseDuckRecoverSpeed = 3.5f;
    [SerializeField, Min(0f)] private float landingMinAirTime = 0.08f;
    [SerializeField, Min(0f)] private float landingMinDownSpeed = 1.5f;
    [SerializeField, Min(0f)] private float hardLandingDownSpeed = 9f;
    [SerializeField, Min(0f)] private float stackMinBodyDragSpeed = 2f;

    [Header("NPC / LOD")]
    [SerializeField] private bool enableNpcDistanceMute = true;
    [SerializeField, Min(0f)] private float npcMuteDistance = 70f;
    [SerializeField, Min(0f)] private float npcUpdateInterval = 0.08f;

    public AudioClip BaseSkiLoopClip => baseSkiLoopClip;
    public AnimationCurve SpeedToBaseVolume => speedToBaseVolume;
    public AnimationCurve SpeedToBasePitch => speedToBasePitch;
    public AudioClip CarveLoopClip => carveLoopClip;
    public AudioClipSetSO CarveEntryAccentClipSet => carveEntryAccentClipSet;
    public AnimationCurve Carve01ToVolume => carve01ToVolume;
    public AnimationCurve Carve01ToPitch => carve01ToPitch;
    public AnimationCurve Carve01ToBaseDuck => carve01ToBaseDuck;
    public AudioClip WindLoopClip => windLoopClip;
    public AnimationCurve SpeedToWindVolume => speedToWindVolume;
    public AnimationCurve SpeedToWindPitch => speedToWindPitch;
    public AudioClip PoleDragLoopClip => poleDragLoopClip;
    public AudioClipSetSO PolePlantAccentClipSet => polePlantAccentClipSet;
    public AudioClipSetSO PoleReleaseAccentClipSet => poleReleaseAccentClipSet;
    public AnimationCurve PoleDragAmountToVolume => poleDragAmountToVolume;
    public AnimationCurve SpeedToPolePitch => speedToPolePitch;
    public AudioClipSetSO LandingSoftAccentClipSet => landingSoftAccentClipSet;
    public AudioClipSetSO LandingHardAccentClipSet => landingHardAccentClipSet;
    public AnimationCurve LandingSeverityToVolume => landingSeverityToVolume;
    public AudioClip BodyDragLoopClip => bodyDragLoopClip;
    public AudioClipSetSO StackImpactAccentClipSet => stackImpactAccentClipSet;
    public AnimationCurve StackSeverityToVolume => stackSeverityToVolume;
    public AnimationCurve BodyDragSpeedToPitch => bodyDragSpeedToPitch;
    public float MaxSpeedForAudio => maxSpeedForAudio;
    public float MaxCarveAngleForAudio => maxCarveAngleForAudio;
    public float CarveEntryThreshold => carveEntryThreshold;
    public float CarveEntryCooldown => carveEntryCooldown;
    public float VolumeLerpSpeed => volumeLerpSpeed;
    public float PitchLerpSpeed => pitchLerpSpeed;
    public float BaseDuckRecoverSpeed => baseDuckRecoverSpeed;
    public float LandingMinAirTime => landingMinAirTime;
    public float LandingMinDownSpeed => landingMinDownSpeed;
    public float HardLandingDownSpeed => hardLandingDownSpeed;
    public float StackMinBodyDragSpeed => stackMinBodyDragSpeed;
    public bool EnableNpcDistanceMute => enableNpcDistanceMute;
    public float NpcMuteDistance => npcMuteDistance;
    public float NpcUpdateInterval => npcUpdateInterval;
}
