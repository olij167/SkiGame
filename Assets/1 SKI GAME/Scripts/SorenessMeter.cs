using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Lightweight "health + stamina" style meter.
/// - 0 = fresh, 1 = very sore.
/// - Exposes a performance multiplier for exertion-related forces only (skate, jump, pole pushes, etc.).
/// - Recovers over time, with optional faster recovery while resting.
/// - Optional visuals: head droop + post-processing intensity.
/// </summary>
[DisallowMultipleComponent]
public class SorenessMeter : MonoBehaviour
{
    [Header("State")]
    [SerializeField, Range(0f, 1f)]
    private float soreness01 = 0f;

    [Tooltip("If true, recovery uses the 'Rest Recovery' rate instead of the normal rate.")]
    [SerializeField]
    private bool isResting = false;

    [Header("Recovery")]
    [Tooltip("Soreness recovered per second during normal play (when not resting).")]
    [SerializeField] private float recoveryPerSecond = 0.02f;

    [Tooltip("Soreness recovered per second while resting.")]
    [SerializeField] private float restRecoveryPerSecond = 0.08f;

    [Tooltip("Seconds after adding soreness before recovery starts again (prevents immediate regen while exerting).")]
    [SerializeField] private float recoveryDelayAfterIncrease = 0.35f;

    [Header("Performance")]
    [Tooltip("Maps soreness (x:0..1) to performance multiplier (y).")]
    [SerializeField]
    private AnimationCurve performanceBySoreness = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.55f)
    );

    [Tooltip("Clamp floor for performance multiplier.")]
    [SerializeField] private float minPerformanceMult = 0.45f;

    [Header("Visuals (Optional)")]
    [Tooltip("Optional URP Volume used to convey soreness. Set up the Volume's profile at 'full soreness', and this script will drive its weight.")]
    [SerializeField] private Volume postProcessVolume;

    [Tooltip("Maps soreness (x:0..1) to post-process Volume weight (y:0..1).")]
    [SerializeField]
    private AnimationCurve postFXWeightBySoreness = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Tooltip("Maximum weight applied to the post-process Volume at full soreness.")]
    [SerializeField, Range(0f, 1f)] private float postFXMaxWeight = 1f;

    [Tooltip("How quickly the post-process effect ramps toward the target weight.")]
    [SerializeField] private float postFXWeightLerpSpeed = 4f;

    [Header("Impact (Stacks/Falls)")]
    [Tooltip("Minimum soreness added for a very minor stack (severity ~0).")]
    [SerializeField] private float impactSorenessMin = 0.05f;

    [Tooltip("Maximum soreness added for a severe stack (severity ~1).")]
    [SerializeField] private float impactSorenessMax = 0.35f;

    [Tooltip("Optional shaping for impact severity (x:0..1) -> (y:0..1).")]
    [SerializeField]
    private AnimationCurve impactSeverityCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Header("Visuals - Head Droop")]
    [Tooltip("Optional: assign the player's head transform (or a top-of-spine transform) to droop as soreness increases.")]
    [SerializeField] private Transform headTransform;

    [Tooltip("Local position offset applied at 100% soreness (forward & down, typically).")]
    [SerializeField] private Vector3 headLocalPosOffsetAtMax = new Vector3(0f, -0.05f, 0.03f);

    [Tooltip("Local rotation (Euler) offset applied at 100% soreness (pitch down, typically).")]
    [SerializeField] private Vector3 headLocalEulerOffsetAtMax = new Vector3(12f, 0f, 0f);

    [Tooltip("Smoothing speed for head droop (higher = snappier).")]
    [SerializeField] private float headDroopLerpSpeed = 8f;

    [Tooltip("Optional shaping for head droop amount vs soreness (x:0..1 -> y:0..1).")]
    [SerializeField]
    private AnimationCurve headDroopBySoreness = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Header("Runtime Overrides")]
    [Tooltip("If false, soreness will not recover over time (FixedUpdate regen is suppressed).")]
    [SerializeField] private bool recoveryEnabled = true;

    private float _recoveryBlockedUntil;

    // Head droop cached baseline.
    private Vector3 _headLocalPosStart;
    private Quaternion _headLocalRotStart;
    private bool _headCached;

    // Smoothed visual driver.
    private float _visualSorenessSmoothed;

    public float Soreness01 => soreness01;

    public bool IsResting
    {
        get => isResting;
        set => isResting = value;
    }

    /// <summary>
    /// Enables/disables recovery ticking in FixedUpdate. Useful for zones like the ski resort.
    /// </summary>
    public bool RecoveryEnabled
    {
        get => recoveryEnabled;
        set => recoveryEnabled = value;
    }

    public float PerformanceMult
    {
        get
        {
            float t = Mathf.Clamp01(soreness01);
            float m = performanceBySoreness != null ? performanceBySoreness.Evaluate(t) : (1f - 0.5f * t);
            return Mathf.Clamp(m, minPerformanceMult, 1f);
        }
    }

    /// <summary>
    /// Adds soreness directly (expected to be a small delta).
    /// </summary>
    public void AddExertion(float sorenessDelta)
    {
        if (sorenessDelta <= 0f)
            return;

        soreness01 = Mathf.Clamp01(soreness01 + sorenessDelta);
        _recoveryBlockedUntil = Time.time + Mathf.Max(0f, recoveryDelayAfterIncrease);
    }

    /// <summary>
    /// Adds soreness based on an impact severity in [0..1] (e.g., stack/fall).
    /// </summary>
    public void AddImpact(float severity01)
    {
        float s = Mathf.Clamp01(severity01);
        float shaped = impactSeverityCurve != null ? Mathf.Clamp01(impactSeverityCurve.Evaluate(s)) : s;
        float delta = Mathf.Lerp(impactSorenessMin, impactSorenessMax, shaped);
        AddExertion(delta);
    }

    private void Awake()
    {
        CacheHeadBaseline();
        CachePostFx();
        _visualSorenessSmoothed = soreness01;
    }

    private void OnEnable()
    {
        CacheHeadBaseline();
        CachePostFx();
    }

    private void CacheHeadBaseline()
    {
        if (headTransform == null)
        {
            _headCached = false;
            return;
        }

        _headLocalPosStart = headTransform.localPosition;
        _headLocalRotStart = headTransform.localRotation;
        _headCached = true;
    }

    private void CachePostFx()
    {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        _volume = null;
        _vig = null;
        _ca = null;
        _grain = null;
        _color = null;

        // We keep this as UnityEngine.Object so the script doesn't hard-fail if URP types aren't present at edit-time.
        _volume = postProcessVolumeObject as Volume;
        if (_volume == null || _volume.profile == null)
            return;

        _volume.profile.TryGet(out _vig);
        _volume.profile.TryGet(out _ca);
        _volume.profile.TryGet(out _grain);
        _volume.profile.TryGet(out _color);
#endif
    }

    private void Update()
    {
        UpdatePostFX();
    }

    private void UpdatePostFX()
    {
        if (postProcessVolume == null)
            return;

        float t = Mathf.Clamp01(soreness01);
        float w01 = postFXWeightBySoreness != null ? Mathf.Clamp01(postFXWeightBySoreness.Evaluate(t)) : t;
        float target = Mathf.Clamp01(w01) * postFXMaxWeight;

        // Smooth ramp to reduce visual flicker if soreness changes quickly.
        postProcessVolume.weight = Mathf.MoveTowards(
            postProcessVolume.weight,
            target,
            postFXWeightLerpSpeed * Time.deltaTime
        );
    }

    private void FixedUpdate()
    {
        if (!recoveryEnabled)
            return;

        if (Time.time >= _recoveryBlockedUntil)
        {
            float rate = isResting ? restRecoveryPerSecond : recoveryPerSecond;
            if (rate > 0f)
            {
                float dt = Time.fixedDeltaTime;
                soreness01 = Mathf.MoveTowards(soreness01, 0f, rate * dt);
            }
        }
    }

    private void LateUpdate()
    {
        // Smooth the driver so visuals don't “buzz” if soreness is updated in bursts.
        float dt = Time.deltaTime;
        float target = Mathf.Clamp01(soreness01);

        // Separate smoothing rates for head vs post FX feels better; we keep one smoothed value
        // but bias towards the faster of the two.
        float speed = Mathf.Max(0.01f, Mathf.Max(headDroopLerpSpeed, postFXWeightLerpSpeed));
        float k = 1f - Mathf.Exp(-speed * dt);
        _visualSorenessSmoothed = Mathf.Lerp(_visualSorenessSmoothed, target, k);

        ApplyHeadDroop(_visualSorenessSmoothed, dt);
    }

    private void ApplyHeadDroop(float tSoreness, float dt)
    {
        if (!_headCached || headTransform == null)
            return;

        float t = Mathf.Clamp01(tSoreness);
        float shaped = headDroopBySoreness != null ? Mathf.Clamp01(headDroopBySoreness.Evaluate(t)) : t;

        Vector3 posTarget = _headLocalPosStart + headLocalPosOffsetAtMax * shaped;
        Quaternion rotTarget = _headLocalRotStart * Quaternion.Euler(headLocalEulerOffsetAtMax * shaped);

        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, headDroopLerpSpeed) * dt);
        headTransform.localPosition = Vector3.Lerp(headTransform.localPosition, posTarget, k);
        headTransform.localRotation = Quaternion.Slerp(headTransform.localRotation, rotTarget, k);
    }

    private void ApplyPostFx(float tSoreness, float dt)
    {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (_volume == null || _volume.profile == null)
            return;

        float t = Mathf.Clamp01(tSoreness);
        float shaped = postFxBySoreness != null ? Mathf.Clamp01(postFxBySoreness.Evaluate(t)) : t;

        // Smooth the shaped intensity as well (separate from the global smoothing).
        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, postFxLerpSpeed) * dt);

        if (_vig != null)
        {
            float target = Mathf.Clamp01(vignetteIntensityAtMax * shaped);
            _vig.intensity.value = Mathf.Lerp(_vig.intensity.value, target, k);
        }

        if (_ca != null)
        {
            float target = Mathf.Clamp01(chromaticAberrationAtMax * shaped);
            _ca.intensity.value = Mathf.Lerp(_ca.intensity.value, target, k);
        }

        if (_grain != null)
        {
            float target = Mathf.Clamp01(filmGrainAtMax * shaped);
            _grain.intensity.value = Mathf.Lerp(_grain.intensity.value, target, k);
        }

        if (_color != null)
        {
            float satTarget = Mathf.Lerp(0f, saturationAtMax, shaped);
            float conTarget = Mathf.Lerp(0f, contrastAtMax, shaped);

            _color.saturation.value = Mathf.Lerp(_color.saturation.value, satTarget, k);
            _color.contrast.value = Mathf.Lerp(_color.contrast.value, conTarget, k);
        }
#endif
    }
}
