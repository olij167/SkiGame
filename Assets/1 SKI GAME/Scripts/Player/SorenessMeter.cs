using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SorenessMeter : MonoBehaviour
{
    [Header("State")]
    [SerializeField, Range(0f, 1f)]
    private float soreness01 = 0f;

    [Tooltip("Extra exhaustion beyond full soreness. 0 = at the normal max soreness threshold, 1 = blackout threshold.")]
    [SerializeField, Range(0f, 1f)]
    private float blackoutDepth01 = 0f;

    [Tooltip("If true, recovery uses the 'Rest Recovery' rate instead of the normal rate.")]
    [SerializeField]
    private bool isResting = false;

    [Header("Recovery")]
    [Tooltip("Soreness recovered per second during normal play (when not resting).")]
    [SerializeField] private float recoveryPerSecond = 0.02f;

    [Tooltip("Soreness recovered per second while resting.")]
    [SerializeField] private float restRecoveryPerSecond = 0.08f;

    [Tooltip("Seconds after adding soreness before recovery starts again.")]
    [SerializeField] private float recoveryDelayAfterIncrease = 0.35f;

    [Header("Performance")]
    [Tooltip("Maps effective soreness (x:0..1) to performance multiplier (y).")]
    [SerializeField]
    private AnimationCurve performanceBySoreness = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.55f)
    );

    [Tooltip("Clamp floor for performance multiplier.")]
    [SerializeField] private float minPerformanceMult = 0.45f;

    [Header("Visuals (Optional)")]
    [Tooltip("Optional URP Volume used to convey soreness. Set this profile up as your 'max soreness / near blackout' look.")]
    [SerializeField] private Volume postProcessVolume;

    [Tooltip("Maps visual intensity (x:0..1) to post-process Volume weight (y:0..1).")]
    [SerializeField]
    private AnimationCurve postFXWeightBySoreness = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Tooltip("Maximum weight applied to the post-process Volume at full intensity.")]
    [SerializeField, Range(0f, 1f)] private float postFXMaxWeight = 1f;

    [Tooltip("How quickly the post-process effect ramps toward the target weight.")]
    [SerializeField] private float postFXWeightLerpSpeed = 4f;

    [Header("Critical Warning")]
    [Tooltip("Start pulsing the soreness post FX once soreness reaches this level.")]
    [SerializeField, Range(0f, 1f)] private float criticalWarningStartSoreness01 = 0.75f;

    [Tooltip("Pulse speed for near-blackout visuals.")]
    [SerializeField] private float warningPulseSpeed = 1.15f;

    [Tooltip("Pulse amplitude applied near blackout.")]
    [SerializeField, Range(0f, 0.25f)] private float warningPulseAmplitude = 0.08f;

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

    [Tooltip("Local position offset applied at maximum visual intensity.")]
    [SerializeField] private Vector3 headLocalPosOffsetAtMax = new Vector3(0f, -0.05f, 0.03f);

    [Tooltip("Local rotation (Euler) offset applied at maximum visual intensity.")]
    [SerializeField] private Vector3 headLocalEulerOffsetAtMax = new Vector3(12f, 0f, 0f);

    [Tooltip("Smoothing speed for head droop.")]
    [SerializeField] private float headDroopLerpSpeed = 8f;

    [Tooltip("Optional shaping for head droop amount vs visual intensity.")]
    [SerializeField]
    private AnimationCurve headDroopBySoreness = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Header("Blackout")]
    [SerializeField] private bool autoBlackoutAtCriticalLimit = true;
    [SerializeField] private PlayerBlackoutTransition blackoutTransition;

    [Header("Runtime Overrides")]
    [Tooltip("If false, soreness will not recover over time.")]
    [SerializeField] private bool recoveryEnabled = true;

    private float _recoveryBlockedUntil;
    private Vector3 _headLocalPosStart;
    private Quaternion _headLocalRotStart;
    private bool _headCached;
    private float _visualSorenessSmoothed;
    private bool _blackoutTriggered;

    public float Soreness01 => Mathf.Clamp01(soreness01);

    /// <summary>
    ///  1 = fresh, 0 = max normal soreness, -1 = blackout threshold.
    /// </summary>
    public float ConditionSigned => Mathf.Clamp((1f - soreness01) - blackoutDepth01, -1f, 1f);

    /// <summary>
    /// 0 = not in blackout range, 1 = blackout threshold reached.
    /// </summary>
    public float BlackoutProgress01 => Mathf.Clamp01(blackoutDepth01);

    public bool IsResting
    {
        get => isResting;
        set => isResting = value;
    }

    public bool RecoveryEnabled
    {
        get => recoveryEnabled;
        set => recoveryEnabled = value;
    }

    public float PerformanceMult
    {
        get
        {
            float effectiveSoreness = GetEffectiveSorenessForGameplay();
            float m = performanceBySoreness != null
                ? performanceBySoreness.Evaluate(effectiveSoreness)
                : (1f - 0.5f * effectiveSoreness);

            return Mathf.Clamp(m, minPerformanceMult, 1f);
        }
    }

    public bool IsFullyRecovered(float threshold01 = 0.01f)
    {
        float threshold = Mathf.Clamp01(threshold01);
        return soreness01 <= threshold && blackoutDepth01 <= threshold;
    }

    public float GetRecoveryRatePerSecond(bool resting)
    {
        return resting ? restRecoveryPerSecond : recoveryPerSecond;
    }

    public float EstimateSecondsUntilRecovered(bool assumeResting, float targetSoreness01 = 0f)
    {
        float target = Mathf.Clamp01(targetSoreness01);
        float remaining = Mathf.Max(0f, blackoutDepth01 + Mathf.Max(0f, soreness01 - target));
        float rate = GetRecoveryRatePerSecond(assumeResting);

        if (rate <= 0f)
            return float.PositiveInfinity;

        return remaining / rate;
    }

    public void AddExertion(float sorenessDelta)
    {
        if (sorenessDelta <= 0f)
            return;

        ApplySorenessIncrease(sorenessDelta);
    }

    public void AddImpact(float severity01)
    {
        float s = Mathf.Clamp01(severity01);
        float shaped = impactSeverityCurve != null ? Mathf.Clamp01(impactSeverityCurve.Evaluate(s)) : s;
        float delta = Mathf.Lerp(impactSorenessMin, impactSorenessMax, shaped);
        ApplySorenessIncrease(delta);
    }

    public void ResetSoreness()
    {
        soreness01 = 0f;
        blackoutDepth01 = 0f;
        _recoveryBlockedUntil = 0f;
        _visualSorenessSmoothed = 0f;
        _blackoutTriggered = false;

        if (postProcessVolume != null)
            postProcessVolume.weight = 0f;

        if (_headCached && headTransform != null)
        {
            headTransform.localPosition = _headLocalPosStart;
            headTransform.localRotation = _headLocalRotStart;
        }
    }

    private void Awake()
    {
        CacheHeadBaseline();
        ResolveReferences();
        _visualSorenessSmoothed = GetVisualIntensity01();
    }

    private void OnEnable()
    {
        CacheHeadBaseline();
        ResolveReferences();
    }

    private void Update()
    {
        UpdatePostFX();
        TryTriggerBlackout();
    }

    private void FixedUpdate()
    {
        if (!recoveryEnabled)
            return;

        if (Time.time < _recoveryBlockedUntil)
            return;

        float rate = isResting ? restRecoveryPerSecond : recoveryPerSecond;
        if (rate <= 0f)
            return;

        float delta = rate * Time.fixedDeltaTime;

        if (blackoutDepth01 > 0f)
        {
            float recoveredCritical = Mathf.Min(delta, blackoutDepth01);
            blackoutDepth01 -= recoveredCritical;
            delta -= recoveredCritical;
        }

        if (delta > 0f && soreness01 > 0f)
            soreness01 = Mathf.MoveTowards(soreness01, 0f, delta);

        if (ConditionSigned > -0.95f)
            _blackoutTriggered = false;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        float target = GetVisualIntensity01();

        float speed = Mathf.Max(0.01f, Mathf.Max(headDroopLerpSpeed, postFXWeightLerpSpeed));
        float k = 1f - Mathf.Exp(-speed * dt);
        _visualSorenessSmoothed = Mathf.Lerp(_visualSorenessSmoothed, target, k);

        ApplyHeadDroop(_visualSorenessSmoothed, dt);
    }

    private void ApplySorenessIncrease(float delta)
    {
        float remainingNormal = Mathf.Max(0f, 1f - soreness01);
        float normalAdded = Mathf.Min(remainingNormal, delta);

        soreness01 = Mathf.Clamp01(soreness01 + normalAdded);

        float overflow = Mathf.Max(0f, delta - normalAdded);
        if (overflow > 0f)
            blackoutDepth01 = Mathf.Clamp01(blackoutDepth01 + overflow);

        _recoveryBlockedUntil = Time.time + Mathf.Max(0f, recoveryDelayAfterIncrease);
    }

    private float GetEffectiveSorenessForGameplay()
    {
        if (blackoutDepth01 <= 0f)
            return soreness01;

        return Mathf.Clamp01(Mathf.Lerp(soreness01, 1f, blackoutDepth01));
    }

    private float GetVisualIntensity01()
    {
        float normal = soreness01;
        float warning = Mathf.InverseLerp(criticalWarningStartSoreness01, 1f, soreness01);
        float critical = blackoutDepth01;

        float intensity = Mathf.Max(normal, Mathf.Lerp(warning, 1f, critical));

        float pulseDriver = Mathf.Max(warning, critical);
        if (pulseDriver > 0.001f && warningPulseAmplitude > 0f && warningPulseSpeed > 0f)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * warningPulseSpeed * Mathf.PI * 2f) * (warningPulseAmplitude * pulseDriver);
            intensity *= pulse;
        }

        return Mathf.Clamp01(intensity);
    }

    private void TryTriggerBlackout()
    {
        if (!autoBlackoutAtCriticalLimit || _blackoutTriggered || ConditionSigned > -0.999f)
            return;

        ResolveReferences();

        if (blackoutTransition == null)
            return;

        if (blackoutTransition.TryRespawnAtNearestResort(out _))
            _blackoutTriggered = true;
    }

    private void UpdatePostFX()
    {
        if (postProcessVolume == null)
            return;

        float t = GetVisualIntensity01();
        float w01 = postFXWeightBySoreness != null ? Mathf.Clamp01(postFXWeightBySoreness.Evaluate(t)) : t;
        float target = Mathf.Clamp01(w01) * postFXMaxWeight;

        postProcessVolume.weight = Mathf.MoveTowards(
            postProcessVolume.weight,
            target,
            postFXWeightLerpSpeed * Time.deltaTime
        );
    }

    private void ApplyHeadDroop(float tVisual, float dt)
    {
        if (!_headCached || headTransform == null)
            return;

        float shaped = headDroopBySoreness != null ? Mathf.Clamp01(headDroopBySoreness.Evaluate(tVisual)) : Mathf.Clamp01(tVisual);

        Vector3 posTarget = _headLocalPosStart + headLocalPosOffsetAtMax * shaped;
        Quaternion rotTarget = _headLocalRotStart * Quaternion.Euler(headLocalEulerOffsetAtMax * shaped);

        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, headDroopLerpSpeed) * dt);
        headTransform.localPosition = Vector3.Lerp(headTransform.localPosition, posTarget, k);
        headTransform.localRotation = Quaternion.Slerp(headTransform.localRotation, rotTarget, k);
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

    private void ResolveReferences()
    {
        if (blackoutTransition == null)
            blackoutTransition = GetComponent<PlayerBlackoutTransition>() ?? FindObjectOfType<PlayerBlackoutTransition>();
    }
}