using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SkiAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkiController controller;
    [SerializeField] private SkierAudioConfigSO config;
    [SerializeField] private AudioInteractionMatrixSO interactionMatrix;
    [SerializeField] private TerrainAudioMaterialProfileSO terrainProfile;

    [Header("Loop Sources")]
    [FormerlySerializedAs("snowLoop")]
    [SerializeField] private AudioSource baseSkiSource;
    [FormerlySerializedAs("carveLoop")]
    [SerializeField] private AudioSource carveSource;
    [FormerlySerializedAs("windLoop")]
    [SerializeField] private AudioSource windSource;
    [FormerlySerializedAs("poleLoop")]
    [SerializeField] private AudioSource poleDragSource;
    [SerializeField] private AudioSource bodyDragSource;
    [SerializeField] private AudioSource oneShotSource;

    [Header("Internal Materials")]
    [SerializeField] private AudioSurfaceMaterialSO skiEdgeMaterial;
    [SerializeField] private AudioSurfaceMaterialSO skiBaseMaterial;
    [SerializeField] private AudioSurfaceMaterialSO poleTipMaterial;
    [SerializeField] private AudioSurfaceMaterialSO bodyMaterial;

    [Header("LOD")]
    [SerializeField] private bool isNpc;
    [SerializeField] private Transform listenerTarget;

    [Header("Legacy Fallback")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Min(0.1f)] private float legacyMaxSpeedForAudio = 30f;
    [SerializeField, Min(1f)] private float legacyMaxCarveAngleForAudio = 45f;
    [SerializeField] private AnimationCurve legacySpeedToSnowVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve legacySpeedToSnowPitch = AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f);
    [SerializeField] private AnimationCurve legacySpeedToWindVolume = AnimationCurve.Linear(0.2f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve legacySpeedToWindPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.4f);
    [SerializeField] private AnimationCurve legacyCarveToVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve legacyCarveToPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.1f);
    [SerializeField] private AnimationCurve legacyPoleDragToVolume = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);
    [SerializeField] private AnimationCurve legacyPoleSpeedToPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.1f);
    [FormerlySerializedAs("polePlantClip")]
    [SerializeField] private AudioClip legacyPolePlantClip;
    [FormerlySerializedAs("poleReleaseClip")]
    [SerializeField] private AudioClip legacyPoleReleaseClip;
    [SerializeField, Min(0f)] private float legacyVolumeLerpSpeed = 8f;
    [SerializeField, Min(0f)] private float legacyPitchLerpSpeed = 8f;

    private readonly System.Random _rng = new System.Random();

    private SkierAudioTelemetry _telemetry;
    private SkierAudioTelemetry _previousTelemetry;
    private SkiController.PoleStrokePhase _prevPolePhase;
    private float _lastCarveAccentTime = -999f;
    private float _lastCarveSign;
    private float _baseDuckAmount;
    private float _lastNpcUpdateTime = -999f;
    private bool _muteForNpcDistance;
    private float _airborneStartTime = -1f;
    private float _maxAirborneDownSpeed;
    private bool _stackedThisFrameFromEvent;
    private float _stackSeverityFromEvent;

    public SkierAudioTelemetry Telemetry => _telemetry;
    public AudioSurfaceMaterialSO SkiEdgeMaterial => skiEdgeMaterial;
    public AudioSurfaceMaterialSO SkiBaseMaterial => skiBaseMaterial;
    public AudioSurfaceMaterialSO PoleTipMaterial => poleTipMaterial;
    public AudioSurfaceMaterialSO BodyMaterial => bodyMaterial;
    public TerrainAudioMaterialProfileSO TerrainProfile => terrainProfile;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<SkiController>();

        ConfigureLoopSource(baseSkiSource, GetBaseSkiLoopClip());
        ConfigureLoopSource(carveSource, GetCarveLoopClip());
        ConfigureLoopSource(windSource, GetWindLoopClip());
        ConfigureLoopSource(poleDragSource, GetPoleLoopClip());
        ConfigureLoopSource(bodyDragSource, GetBodyDragLoopClip());

        if (controller != null)
        {
            _prevPolePhase = controller.CurrentPolePhase;
            _telemetry.ReadFrom(controller, config);
            _previousTelemetry = _telemetry;
        }
    }

    private void OnEnable()
    {
        if (controller != null)
            controller.OnStacked += HandleStacked;
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.OnStacked -= HandleStacked;
    }

    private void Update()
    {
        if (controller == null)
            return;

        if (ShouldThrottleNpcUpdate())
            return;

        UpdateTelemetry();
        UpdateLoopLayers();
        UpdateLandingAndStackTransients();

        _prevPolePhase = _telemetry.polePhase;
        _previousTelemetry = _telemetry;
        _stackedThisFrameFromEvent = false;
        _stackSeverityFromEvent = 0f;
    }

    private void HandleStacked(SkiController.StackEventInfo info)
    {
        _stackedThisFrameFromEvent = true;
        _stackSeverityFromEvent = info.severity01;
    }

    private bool ShouldThrottleNpcUpdate()
    {
        if (!isNpc || config == null || !config.EnableNpcDistanceMute)
            return false;

        Transform listener = listenerTarget != null
            ? listenerTarget
            : (Camera.main != null ? Camera.main.transform : null);

        if (listener != null)
        {
            float sqrDistance = (listener.position - transform.position).sqrMagnitude;
            _muteForNpcDistance = sqrDistance > config.NpcMuteDistance * config.NpcMuteDistance;
        }
        else
        {
            _muteForNpcDistance = false;
        }

        float interval = Mathf.Max(0.01f, config.NpcUpdateInterval);
        if (Time.time - _lastNpcUpdateTime < interval)
            return true;

        _lastNpcUpdateTime = Time.time;
        return false;
    }

    public void UpdateTelemetry()
    {
        _telemetry = _previousTelemetry;
        _telemetry.ReadFrom(controller, config);
        _telemetry.didStackThisFrame = _stackedThisFrameFromEvent;

        if (!_telemetry.groundedState)
        {
            if (_previousTelemetry.groundedState)
            {
                _airborneStartTime = Time.time;
                _maxAirborneDownSpeed = 0f;
            }

            _maxAirborneDownSpeed = Mathf.Max(_maxAirborneDownSpeed, Mathf.Max(0f, -_telemetry.verticalSpeed));
        }

        bool didLand = !_previousTelemetry.groundedState && _telemetry.groundedState;
        _telemetry.didLandThisFrame = didLand;

        if (didLand)
        {
            float airTime = _airborneStartTime >= 0f ? Mathf.Max(0f, Time.time - _airborneStartTime) : 0f;
            float minAirTime = config != null ? config.LandingMinAirTime : 0.08f;
            float minDownSpeed = config != null ? config.LandingMinDownSpeed : 1.5f;
            bool validLanding = airTime >= minAirTime || _maxAirborneDownSpeed >= minDownSpeed;
            _telemetry.didLandThisFrame = validLanding;

            float hardDown = config != null ? Mathf.Max(minDownSpeed + 0.01f, config.HardLandingDownSpeed) : 9f;
            _telemetry.landingSeverity = validLanding
                ? Mathf.Clamp01(Mathf.InverseLerp(minDownSpeed, hardDown, _maxAirborneDownSpeed))
                : 0f;
        }
        else
        {
            _telemetry.landingSeverity = 0f;
        }
    }

    public void UpdateLoopLayers()
    {
        UpdateBaseSkiLayer();
        UpdateCarveLayer();
        UpdateWindLayer();
        UpdatePoleLayer();
        UpdateBodyDragLayer();
    }

    public void UpdateBaseSkiLayer()
    {
        if (baseSkiSource == null)
            return;

        float targetVolume = _telemetry.groundedState
            ? EvaluateCurve(GetBaseVolumeCurve(), _telemetry.normalizedSpeed)
            : 0f;

        if (_telemetry.carve01 > 0f)
        {
            float duckTarget = EvaluateCurve(GetBaseDuckCurve(), _telemetry.carve01);
            _baseDuckAmount = Mathf.MoveTowards(_baseDuckAmount, duckTarget, GetBaseDuckRecoverSpeed() * Time.deltaTime);
            targetVolume *= 1f - Mathf.Clamp01(_baseDuckAmount);
        }
        else
        {
            _baseDuckAmount = Mathf.MoveTowards(_baseDuckAmount, 0f, GetBaseDuckRecoverSpeed() * Time.deltaTime);
            targetVolume *= 1f - Mathf.Clamp01(_baseDuckAmount);
        }

        if (_muteForNpcDistance)
            targetVolume = 0f;

        float targetPitch = EvaluateCurve(GetBasePitchCurve(), _telemetry.normalizedSpeed);
        Apply(baseSkiSource, targetVolume, targetPitch);
    }

    public void UpdateCarveLayer()
    {
        if (carveSource == null)
            return;

        float carve01 = _telemetry.groundedState ? _telemetry.carve01 : 0f;
        float targetVolume = EvaluateCurve(GetCarveVolumeCurve(), carve01) *
                             EvaluateCurve(GetBaseVolumeCurve(), _telemetry.normalizedSpeed);

        if (_muteForNpcDistance)
            targetVolume = 0f;

        float targetPitch = EvaluateCurve(GetCarvePitchCurve(), carve01);
        Apply(carveSource, targetVolume, targetPitch);

        if (Mathf.Abs(_telemetry.signedCarve) > 0.01f)
            carveSource.panStereo = Mathf.Clamp(Mathf.Sign(_telemetry.signedCarve) * 0.7f, -1f, 1f);
        else
            carveSource.panStereo = Mathf.MoveTowards(carveSource.panStereo, 0f, Time.deltaTime * 2f);

        if (ShouldTriggerCarveEntryAccent())
            TriggerCarveEntryAccent();
    }

    public void UpdateWindLayer()
    {
        if (windSource == null)
            return;

        float targetVolume = EvaluateCurve(GetWindVolumeCurve(), _telemetry.normalizedSpeed);
        if (_telemetry.groundedState)
            targetVolume *= 0.8f;
        if (_muteForNpcDistance)
            targetVolume = 0f;

        float targetPitch = EvaluateCurve(GetWindPitchCurve(), _telemetry.normalizedSpeed);
        Apply(windSource, targetVolume, targetPitch);
    }

    public void UpdatePoleLayer()
    {
        if (poleDragSource == null)
            return;

        float targetVolume = EvaluateCurve(GetPoleVolumeCurve(), _telemetry.poleDragAmount);
        if (_muteForNpcDistance)
            targetVolume = 0f;

        float targetPitch = EvaluateCurve(GetPolePitchCurve(), _telemetry.normalizedSpeed);
        Apply(poleDragSource, targetVolume, targetPitch);

        if (_prevPolePhase == SkiController.PoleStrokePhase.Idle &&
            _telemetry.polePhase == SkiController.PoleStrokePhase.Entry &&
            (_telemetry.leftPoleContact || _telemetry.rightPoleContact))
        {
            AudioClipSetSO plantSet = GetPolePlantClipSet();
            if (plantSet != null)
                PlayOneShot(plantSet, _telemetry.normalizedSpeed);
            else
                PlayOneShot(legacyPolePlantClip, _telemetry.normalizedSpeed);
        }

        if (_prevPolePhase == SkiController.PoleStrokePhase.Drag &&
            _telemetry.polePhase == SkiController.PoleStrokePhase.FollowThrough)
        {
            AudioClipSetSO releaseSet = GetPoleReleaseClipSet();
            if (releaseSet != null)
                PlayOneShot(releaseSet, _telemetry.normalizedSpeed);
            else
                PlayOneShot(legacyPoleReleaseClip, _telemetry.normalizedSpeed);
        }
    }

    public void UpdateBodyDragLayer()
    {
        if (bodyDragSource == null)
            return;

        float speed01 = Mathf.Clamp01(_telemetry.planarSpeed / Mathf.Max(0.1f, GetMaxSpeedForAudio()));
        bool shouldPlay = _telemetry.stackedState && _telemetry.planarSpeed >= GetStackMinBodyDragSpeed();
        float targetVolume = shouldPlay ? EvaluateCurve(GetStackVolumeCurve(), Mathf.Max(speed01, _stackSeverityFromEvent)) : 0f;
        if (_muteForNpcDistance)
            targetVolume = 0f;

        float targetPitch = EvaluateCurve(GetBodyDragPitchCurve(), speed01);
        Apply(bodyDragSource, targetVolume, targetPitch);
    }

    public void UpdateLandingAndStackTransients()
    {
        if (_telemetry.didLandThisFrame)
            TriggerLandingAccent();

        if (_telemetry.didStackThisFrame)
            TriggerStackAccent();
    }

    public bool ShouldTriggerCarveEntryAccent()
    {
        if (!_telemetry.groundedState)
            return false;

        float threshold = config != null ? config.CarveEntryThreshold : 0.55f;
        bool crossedThreshold = _previousTelemetry.carve01 < threshold && _telemetry.carve01 >= threshold;
        bool signFlip = DidCarveSignFlip() && _telemetry.carve01 >= threshold * 0.8f;
        bool cooldownReady = (Time.time - _lastCarveAccentTime) >= (config != null ? config.CarveEntryCooldown : 0.18f);
        return cooldownReady && (crossedThreshold || signFlip);
    }

    public bool DidCarveSignFlip()
    {
        float currentSign = Mathf.Abs(_telemetry.signedCarve) > 0.01f ? Mathf.Sign(_telemetry.signedCarve) : 0f;
        float previousSign = Mathf.Abs(_previousTelemetry.signedCarve) > 0.01f ? Mathf.Sign(_previousTelemetry.signedCarve) : _lastCarveSign;
        bool flipped = currentSign != 0f && previousSign != 0f && currentSign != previousSign;
        if (currentSign != 0f)
            _lastCarveSign = currentSign;
        return flipped;
    }

    public void TriggerCarveEntryAccent()
    {
        _lastCarveAccentTime = Time.time;
        _baseDuckAmount = Mathf.Max(_baseDuckAmount, 0.5f);
        PlayOneShot(GetCarveEntryClipSet(), Mathf.Clamp01(_telemetry.carve01));
    }

    public void TriggerLandingAccent()
    {
        AudioClipSetSO clipSet = _telemetry.landingSeverity >= 0.55f
            ? GetLandingHardClipSet()
            : GetLandingSoftClipSet();

        PlayOneShot(clipSet, _telemetry.landingSeverity);
    }

    public void TriggerStackAccent()
    {
        PlayOneShot(GetStackImpactClipSet(), Mathf.Clamp01(_stackSeverityFromEvent));
    }

    public void PlayOneShot(AudioClipSetSO clipSet, float intensity01)
    {
        if (oneShotSource == null || clipSet == null)
            return;

        int clipIndex = clipSet.GetNextClip(_rng, Time.time);
        if (clipIndex < 0 || clipIndex >= clipSet.Clips.Length)
            return;

        AudioClip clip = clipSet.Clips[clipIndex].clip;
        if (clip == null)
            return;

        oneShotSource.pitch = Mathf.Lerp(clipSet.PitchRange.x, clipSet.PitchRange.y, Mathf.Clamp01(intensity01));
        oneShotSource.volume = Mathf.Lerp(clipSet.VolumeRange.x, clipSet.VolumeRange.y, Mathf.Clamp01(intensity01));
        oneShotSource.PlayOneShot(clip);
        clipSet.NotifyPlayed(clipIndex, Time.time);
    }

    public void PlayOneShot(AudioClip clip, float intensity01)
    {
        if (clip == null || oneShotSource == null)
            return;

        oneShotSource.pitch = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(intensity01));
        oneShotSource.PlayOneShot(clip);
    }

    public void PlayFootstep(Vector3 worldPosition, float footSpeed = 1f)
    {
        if (footstepClip == null || oneShotSource == null)
            return;

        oneShotSource.transform.position = worldPosition;
        oneShotSource.pitch = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(footSpeed));
        oneShotSource.PlayOneShot(footstepClip);
    }

    private void ConfigureLoopSource(AudioSource source, AudioClip desiredClip)
    {
        if (source == null)
            return;

        if (desiredClip != null)
            source.clip = desiredClip;

        source.loop = true;
        source.playOnAwake = false;
        if (source.clip != null && !source.isPlaying)
            source.Play();
    }

    private void Apply(AudioSource src, float targetVol, float targetPitch)
    {
        if (src == null)
            return;

        float dt = Time.deltaTime;
        src.volume = Mathf.MoveTowards(src.volume, targetVol, GetVolumeLerpSpeed() * dt);
        src.pitch = Mathf.MoveTowards(src.pitch, targetPitch, GetPitchLerpSpeed() * dt);
    }

    private float EvaluateCurve(AnimationCurve curve, float t)
    {
        return curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : 0f;
    }

    private float GetMaxSpeedForAudio() => config != null ? config.MaxSpeedForAudio : legacyMaxSpeedForAudio;
    private float GetVolumeLerpSpeed() => config != null ? config.VolumeLerpSpeed : legacyVolumeLerpSpeed;
    private float GetPitchLerpSpeed() => config != null ? config.PitchLerpSpeed : legacyPitchLerpSpeed;
    private float GetBaseDuckRecoverSpeed() => config != null ? config.BaseDuckRecoverSpeed : 3.5f;
    private float GetStackMinBodyDragSpeed() => config != null ? config.StackMinBodyDragSpeed : 2f;

    private AnimationCurve GetBaseVolumeCurve() => config != null ? config.SpeedToBaseVolume : legacySpeedToSnowVolume;
    private AnimationCurve GetBasePitchCurve() => config != null ? config.SpeedToBasePitch : legacySpeedToSnowPitch;
    private AnimationCurve GetWindVolumeCurve() => config != null ? config.SpeedToWindVolume : legacySpeedToWindVolume;
    private AnimationCurve GetWindPitchCurve() => config != null ? config.SpeedToWindPitch : legacySpeedToWindPitch;
    private AnimationCurve GetCarveVolumeCurve() => config != null ? config.Carve01ToVolume : legacyCarveToVolume;
    private AnimationCurve GetCarvePitchCurve() => config != null ? config.Carve01ToPitch : legacyCarveToPitch;
    private AnimationCurve GetBaseDuckCurve() => config != null ? config.Carve01ToBaseDuck : AnimationCurve.Linear(0f, 0f, 1f, 0.35f);
    private AnimationCurve GetPoleVolumeCurve() => config != null ? config.PoleDragAmountToVolume : legacyPoleDragToVolume;
    private AnimationCurve GetPolePitchCurve() => config != null ? config.SpeedToPolePitch : legacyPoleSpeedToPitch;
    private AnimationCurve GetStackVolumeCurve() => config != null ? config.StackSeverityToVolume : AnimationCurve.Linear(0f, 0.35f, 1f, 1f);
    private AnimationCurve GetBodyDragPitchCurve() => config != null ? config.BodyDragSpeedToPitch : AnimationCurve.Linear(0f, 0.85f, 1f, 1.1f);

    private AudioClip GetBaseSkiLoopClip() => config != null ? config.BaseSkiLoopClip : (baseSkiSource != null ? baseSkiSource.clip : null);
    private AudioClip GetCarveLoopClip() => config != null ? config.CarveLoopClip : (carveSource != null ? carveSource.clip : null);
    private AudioClip GetWindLoopClip() => config != null ? config.WindLoopClip : (windSource != null ? windSource.clip : null);
    private AudioClip GetPoleLoopClip() => config != null ? config.PoleDragLoopClip : (poleDragSource != null ? poleDragSource.clip : null);
    private AudioClip GetBodyDragLoopClip() => config != null ? config.BodyDragLoopClip : (bodyDragSource != null ? bodyDragSource.clip : null);

    private AudioClipSetSO GetCarveEntryClipSet() => config != null ? config.CarveEntryAccentClipSet : null;
    private AudioClipSetSO GetPolePlantClipSet() => config != null ? config.PolePlantAccentClipSet : null;
    private AudioClipSetSO GetPoleReleaseClipSet() => config != null ? config.PoleReleaseAccentClipSet : null;
    private AudioClipSetSO GetLandingSoftClipSet() => config != null ? config.LandingSoftAccentClipSet : null;
    private AudioClipSetSO GetLandingHardClipSet() => config != null ? config.LandingHardAccentClipSet : null;
    private AudioClipSetSO GetStackImpactClipSet() => config != null ? config.StackImpactAccentClipSet : null;
}
