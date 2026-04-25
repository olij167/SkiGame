//using System;
//using System.Reflection;
//using UnityEngine;
//using MoreMountains.Feedbacks;

//[DisallowMultipleComponent]
//[AddComponentMenu("SkiGame/Feedback/Ski Controller FEEL Bridge")]
//public sealed class SkiControllerFeelBridge : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField] private SkiController controller;

//    [Header("Speed (Loop + Milestone)")]
//    [Tooltip("Looped wind/FOV/shake style feedback while moving fast.")]
//    [SerializeField] private MMF_Player fbSpeedLoop;

//    [Tooltip("One-shot when crossing the very-fast threshold (optional).")]
//    [SerializeField] private MMF_Player fbVeryFastMilestone;

//    [Tooltip("Planar speed (m/s) at which the speed loop starts.")]
//    [SerializeField] private float speedLoopStart = 11f;

//    [Tooltip("Planar speed (m/s) at which the speed loop stops (hysteresis).")]
//    [SerializeField] private float speedLoopStop = 9.5f;

//    [Tooltip("Planar speed (m/s) at which intensity reaches 1.")]
//    [SerializeField] private float speedLoopFull = 22f;

//    [Tooltip("Very-fast threshold (m/s) for milestone one-shot.")]
//    [SerializeField] private float veryFastThreshold = 18f;

//    [Tooltip("Minimum time between milestone triggers.")]
//    [SerializeField] private float veryFastCooldown = 1.25f;

//    [Header("Air (Takeoff + Loop + Landing)")]
//    [SerializeField] private MMF_Player fbJumpTakeoff;
//    [SerializeField] private MMF_Player fbAirLoop;
//    [SerializeField] private MMF_Player fbLand;

//    [Tooltip("Upward velocity (m/s) required on grounded->air transition to count as takeoff.")]
//    [SerializeField] private float takeoffMinUpVel = 2.0f;

//    [Tooltip("Minimum time between takeoff triggers (prevents double fire on bouncy edges).")]
//    [SerializeField] private float takeoffCooldown = 0.25f;

//    [Tooltip("Downward velocity (m/s) at which landing intensity starts (use positive number).")]
//    [SerializeField] private float landingMinDownVel = 3.0f;

//    [Tooltip("Downward velocity (m/s) at which landing intensity reaches 1 (use positive number).")]
//    [SerializeField] private float landingMaxDownVel = 12.0f;

//    [Tooltip("If true, landing feedback may trigger when re-grounding onto grind support.")]
//    [SerializeField] private bool allowLandingOnGrind = false;

//    [Header("Stacking (Crash)")]
//    [SerializeField] private MMF_Player fbStackCrash;

//    [Header("Grinding (Loop)")]
//    [SerializeField] private MMF_Player fbGrindLoop;

//    [Header("Poles (Plant + Drag Loop + Release)")]
//    [SerializeField] private MMF_Player fbPolePlant;
//    [SerializeField] private MMF_Player fbPoleDragLoop;
//    [SerializeField] private MMF_Player fbPoleRelease;

//    [Header("Skate Push (One-shot)")]
//    [Tooltip("Triggered when the controller detects a skate push. Uses reflection to read _lastPushTime.")]
//    [SerializeField] private MMF_Player fbSkatePush;

//    [Tooltip("Minimum time between skate push feedback triggers.")]
//    [SerializeField] private float skatePushCooldown = 0.12f;

//    [Header("Loop Responsiveness")]
//    [Tooltip("How quickly loop intensity follows target (higher = snappier).")]
//    [SerializeField] private float loopIntensityLerpSpeed = 10f;

//    [Tooltip("Optional: extra intensity boost from planar acceleration.")]
//    [Range(0f, 1f)]
//    [SerializeField] private float accelInfluence = 0.25f;

//    [Tooltip("Planar accel (m/s^2) that maps to full accel boost.")]
//    [SerializeField] private float accelFull = 10f;

//    [Header("Debug")]
//    [SerializeField] private bool logDebugTransitions = false;

//    // -------------------------
//    // Runtime state
//    // -------------------------
//    private bool _prevGrounded;
//    private bool _prevGrinding;
//    private SkiController.PoleStrokePhase _prevPolePhase;
//    private float _prevPlanarSpeed;

//    private bool _speedLoopPlaying;
//    private bool _airLoopPlaying;
//    private bool _grindLoopPlaying;
//    private bool _poleDragLoopPlaying;

//    private float _nextVeryFastTime;
//    private float _nextTakeoffTime;

//    // Smoothed intensities
//    private float _speedLoopIntensitySmoothed;
//    private float _airLoopIntensitySmoothed;
//    private float _grindLoopIntensitySmoothed;
//    private float _poleDragLoopIntensitySmoothed;

//    // Landing severity capture while airborne
//    private float _mostNegativeAirYVel;

//    // Reflection-backed skate push time
//    private FieldInfo _fiLastPushTime;
//    private float _prevLastPushTime;
//    private float _nextSkatePushTime;

//    private void Reset()
//    {
//        controller = GetComponentInParent<SkiController>();
//    }

//    private void Awake()
//    {
//        if (controller == null)
//            controller = GetComponentInParent<SkiController>();

//        CacheReflection();
//    }

//    private void OnEnable()
//    {
//        if (controller != null)
//            controller.OnStacked += HandleStacked;

//        InitializeStateFromController();
//    }

//    private void OnDisable()
//    {
//        if (controller != null)
//            controller.OnStacked -= HandleStacked;

//        StopAllLoops();
//    }

//    private void Update()
//    {
//        if (controller == null)
//            return;

//        Vector3 v = controller.Velocity;
//        bool grounded = controller.IsRiderGrounded;
//        bool grinding = controller.IsGrinding;
//        var polePhase = controller.CurrentPolePhase;

//        // Planar speed projected onto ground plane when grounded (matches “skiing feel” better)
//        Vector3 gn = controller.GroundNormal.sqrMagnitude > 0.0001f ? controller.GroundNormal.normalized : Vector3.up;
//        Vector3 velOnPlane = grounded ? Vector3.ProjectOnPlane(v, gn) : new Vector3(v.x, 0f, v.z);
//        float planarSpeed = velOnPlane.magnitude;

//        // Track airborne vertical severity for landing impact (most negative y vel while airborne)
//        if (!grounded)
//        {
//            if (v.y < _mostNegativeAirYVel)
//                _mostNegativeAirYVel = v.y;
//        }

//        HandleSpeed(planarSpeed);
//        HandleAirState(grounded, grinding, v.y, planarSpeed);
//        HandleGrinding(grinding, planarSpeed);
//        HandlePoles(polePhase, grounded, planarSpeed);
//        HandleSkatePush(planarSpeed);

//        _prevGrounded = grounded;
//        _prevGrinding = grinding;
//        _prevPolePhase = polePhase;
//        _prevPlanarSpeed = planarSpeed;
//    }

//    // -------------------------
//    // Speed (loop intensity follows speed continuously, no replay)
//    // -------------------------
//    private void HandleSpeed(float planarSpeed)
//    {
//        // Milestone one-shot (rising edge)
//        if (_prevPlanarSpeed < veryFastThreshold && planarSpeed >= veryFastThreshold && Time.time >= _nextVeryFastTime)
//        {
//            _nextVeryFastTime = Time.time + veryFastCooldown;
//            PlayOneShot(fbVeryFastMilestone, transform.position, 0.6f, "VeryFastMilestone");
//        }

//        // Loop with hysteresis
//        bool shouldLoop = _speedLoopPlaying
//            ? planarSpeed >= speedLoopStop
//            : planarSpeed >= speedLoopStart;

//        if (!shouldLoop)
//        {
//            if (_speedLoopPlaying)
//            {
//                _speedLoopPlaying = false;
//                _speedLoopIntensitySmoothed = 0f;
//                StopLoop(fbSpeedLoop, transform.position, "SpeedLoop(stop)");
//            }
//            return;
//        }

//        float dt = Mathf.Max(0.0001f, Time.deltaTime);

//        float speed01 = Mathf.InverseLerp(speedLoopStart, speedLoopFull, planarSpeed);
//        speed01 = Mathf.Clamp01(speed01);

//        // Optional acceleration influence makes the loop feel more “alive” without increasing baseline intensity
//        float accel = (planarSpeed - _prevPlanarSpeed) / dt;
//        float accel01 = Mathf.Clamp01(Mathf.Abs(accel) / Mathf.Max(0.01f, accelFull));
//        float accelBoost = Mathf.Lerp(1f, 1f + accelInfluence, accel01);

//        float targetIntensity = Mathf.Clamp01(speed01 * accelBoost);

//        _speedLoopIntensitySmoothed = Mathf.MoveTowards(_speedLoopIntensitySmoothed, targetIntensity, loopIntensityLerpSpeed * dt);

//        if (!_speedLoopPlaying)
//        {
//            _speedLoopPlaying = true;
//            PlayLoop(fbSpeedLoop, transform.position, _speedLoopIntensitySmoothed, "SpeedLoop(start)");
//        }
//        else
//        {
//            if (fbSpeedLoop != null)
//                fbSpeedLoop.FeedbacksIntensity = _speedLoopIntensitySmoothed;
//        }
//    }

//    // -------------------------
//    // Air (loop intensity follows airtime/vertical motion continuously)
//    // -------------------------
//    private void HandleAirState(bool grounded, bool grinding, float yVel, float planarSpeed)
//    {
//        // Takeoff detection: grounded -> not grounded
//        if (_prevGrounded && !grounded)
//        {
//            if (yVel >= takeoffMinUpVel && Time.time >= _nextTakeoffTime)
//            {
//                _nextTakeoffTime = Time.time + takeoffCooldown;
//                PlayOneShot(fbJumpTakeoff, transform.position, 0.7f, "JumpTakeoff");
//            }

//            _airLoopIntensitySmoothed = 0f;
//            _mostNegativeAirYVel = 0f;

//            if (fbAirLoop != null)
//            {
//                _airLoopPlaying = true;
//                PlayLoop(fbAirLoop, transform.position, 0.25f, "AirLoop(start)");
//            }
//        }

//        // While airborne, gently drive intensity from vertical motion + a touch of speed
//        if (!grounded && _airLoopPlaying && fbAirLoop != null)
//        {
//            float dt = Mathf.Max(0.0001f, Time.deltaTime);

//            float v01 = Mathf.Clamp01(Mathf.Abs(yVel) / 12f);
//            float s01 = Mathf.Clamp01(planarSpeed / 18f);

//            // Keep subtle: base 0.25, plus up to ~0.5 extra
//            float target = 0.25f + 0.35f * v01 + 0.15f * s01;
//            target = Mathf.Clamp01(target);

//            _airLoopIntensitySmoothed = Mathf.MoveTowards(_airLoopIntensitySmoothed, target, loopIntensityLerpSpeed * dt);
//            fbAirLoop.FeedbacksIntensity = _airLoopIntensitySmoothed;
//        }

//        // Landing detection: not grounded -> grounded
//        if (!_prevGrounded && grounded)
//        {
//            bool landedFromGrindSupport = grinding || _prevGrinding;
//            if (!landedFromGrindSupport || allowLandingOnGrind)
//            {
//                float downVel = Mathf.Max(0f, -_mostNegativeAirYVel); // positive magnitude
//                float intensity = Mathf.InverseLerp(landingMinDownVel, landingMaxDownVel, downVel);
//                intensity = Mathf.Clamp01(intensity);

//                // Softer landings overall
//                intensity = Mathf.Min(intensity, 0.75f);

//                // Avoid micro noise
//                if (intensity > 0.12f)
//                    PlayOneShot(fbLand, transform.position, intensity, $"Land(intensity={intensity:0.00})");
//            }

//            if (_airLoopPlaying)
//            {
//                _airLoopPlaying = false;
//                _airLoopIntensitySmoothed = 0f;
//                StopLoop(fbAirLoop, transform.position, "AirLoop(stop)");
//            }
//        }
//    }

//    // -------------------------
//    // Grinding (loop intensity follows GrindStrength01 continuously)
//    // -------------------------
//    private void HandleGrinding(bool grinding, float planarSpeed)
//    {
//        if (!_prevGrinding && grinding)
//        {
//            _grindLoopIntensitySmoothed = 0f;
//            _grindLoopPlaying = true;

//            float startIntensity = Mathf.Clamp01(controller.GrindStrength01);
//            startIntensity = Mathf.Min(startIntensity, 0.85f);
//            PlayLoop(fbGrindLoop, transform.position, startIntensity, "GrindLoop(start)");
//            _grindLoopIntensitySmoothed = startIntensity;
//            return;
//        }

//        if (_prevGrinding && !grinding)
//        {
//            if (_grindLoopPlaying)
//            {
//                _grindLoopPlaying = false;
//                _grindLoopIntensitySmoothed = 0f;
//                StopLoop(fbGrindLoop, transform.position, "GrindLoop(stop)");
//            }
//            return;
//        }

//        if (grinding && _grindLoopPlaying && fbGrindLoop != null)
//        {
//            float dt = Mathf.Max(0.0001f, Time.deltaTime);

//            // Primary: grind strength. Secondary: a touch of speed (optional).
//            float g01 = Mathf.Clamp01(controller.GrindStrength01);
//            float s01 = Mathf.Clamp01(planarSpeed / 20f);

//            float target = Mathf.Clamp01(0.15f + 0.70f * g01 + 0.15f * s01);

//            _grindLoopIntensitySmoothed = Mathf.MoveTowards(_grindLoopIntensitySmoothed, target, loopIntensityLerpSpeed * dt);
//            fbGrindLoop.FeedbacksIntensity = _grindLoopIntensitySmoothed;
//        }
//    }

//    // -------------------------
//    // Poles (drag loop intensity follows speed continuously)
//    // -------------------------
//    private void HandlePoles(SkiController.PoleStrokePhase phase, bool grounded, float planarSpeed)
//    {
//        if (!grounded)
//        {
//            if (_poleDragLoopPlaying)
//            {
//                _poleDragLoopPlaying = false;
//                _poleDragLoopIntensitySmoothed = 0f;
//                StopLoop(fbPoleDragLoop, transform.position, "PoleDragLoop(stop-air)");
//            }
//            return;
//        }

//        // Update drag loop intensity while dragging
//        if (_poleDragLoopPlaying && phase == SkiController.PoleStrokePhase.Drag && fbPoleDragLoop != null)
//        {
//            float dt = Mathf.Max(0.0001f, Time.deltaTime);
//            float s01 = Mathf.Clamp01(planarSpeed / 10f);

//            // Subtle: base 0.2 then up to ~0.6
//            float target = Mathf.Clamp01(0.20f + 0.60f * s01);
//            _poleDragLoopIntensitySmoothed = Mathf.MoveTowards(_poleDragLoopIntensitySmoothed, target, loopIntensityLerpSpeed * dt);
//            fbPoleDragLoop.FeedbacksIntensity = _poleDragLoopIntensitySmoothed;
//        }

//        if (_prevPolePhase == phase)
//            return;

//        // Transition hooks
//        if (_prevPolePhase == SkiController.PoleStrokePhase.Idle && phase == SkiController.PoleStrokePhase.Entry)
//        {
//            PlayOneShot(fbPolePlant, transform.position, 0.65f, "PolePlant");
//        }

//        if (_prevPolePhase == SkiController.PoleStrokePhase.Entry && phase == SkiController.PoleStrokePhase.Drag)
//        {
//            _poleDragLoopPlaying = true;
//            _poleDragLoopIntensitySmoothed = 0.25f;
//            PlayLoop(fbPoleDragLoop, transform.position, _poleDragLoopIntensitySmoothed, "PoleDragLoop(start)");
//        }

//        // Release cases
//        if (_prevPolePhase == SkiController.PoleStrokePhase.Drag &&
//            (phase == SkiController.PoleStrokePhase.FollowThrough || phase == SkiController.PoleStrokePhase.Idle))
//        {
//            if (_poleDragLoopPlaying)
//            {
//                _poleDragLoopPlaying = false;
//                _poleDragLoopIntensitySmoothed = 0f;
//                StopLoop(fbPoleDragLoop, transform.position, "PoleDragLoop(stop)");
//            }

//            PlayOneShot(fbPoleRelease, transform.position, 0.55f, "PoleRelease");
//        }

//        if (logDebugTransitions)
//            Debug.Log($"[FEEL] Pole phase {_prevPolePhase} -> {phase}", this);
//    }

//    // -------------------------
//    // Stacking
//    // -------------------------
//    private void HandleStacked(SkiController.StackEventInfo info)
//    {
//        float intensity = Mathf.Clamp01(info.severity01);

//        // Keep crashes impactful but not cartoonish
//        intensity = Mathf.Min(intensity, 0.85f);

//        PlayOneShot(fbStackCrash, info.position, intensity, $"StackCrash({info.reason}, {intensity:0.00})");
//    }

//    // -------------------------
//    // Skate push via reflection
//    // -------------------------
//    private void HandleSkatePush(float planarSpeed)
//    {
//        if (fbSkatePush == null || controller == null || _fiLastPushTime == null)
//            return;

//        float t = (float)_fiLastPushTime.GetValue(controller);

//        if (t > _prevLastPushTime + 0.0001f)
//        {
//            _prevLastPushTime = t;

//            if (Time.time >= _nextSkatePushTime)
//            {
//                _nextSkatePushTime = Time.time + skatePushCooldown;

//                float intensity = Mathf.Clamp01(planarSpeed / 10f);
//                PlayOneShot(fbSkatePush, transform.position, Mathf.Max(0.30f, 0.60f * intensity), "SkatePush");
//            }
//        }
//    }

//    // -------------------------
//    // FEEL helpers
//    // -------------------------
//    private void PlayOneShot(MMF_Player player, Vector3 position, float intensity, string tag)
//    {
//        if (player == null)
//            return;

//        player.FeedbacksIntensity = intensity;
//        player.PlayFeedbacks(position, intensity);

//        if (logDebugTransitions)
//            Debug.Log($"[FEEL] Play {tag}", this);
//    }

//    private void PlayLoop(MMF_Player player, Vector3 position, float intensity, string tag)
//    {
//        if (player == null)
//            return;

//        player.FeedbacksIntensity = intensity;
//        player.PlayFeedbacks(position, intensity);

//        if (logDebugTransitions)
//            Debug.Log($"[FEEL] Loop {tag}", this);
//    }

//    private void StopLoop(MMF_Player player, Vector3 position, string tag)
//    {
//        if (player == null)
//            return;

//        player.StopFeedbacks(position);

//        if (logDebugTransitions)
//            Debug.Log($"[FEEL] Stop {tag}", this);
//    }

//    private void StopAllLoops()
//    {
//        _speedLoopPlaying = false;
//        _airLoopPlaying = false;
//        _grindLoopPlaying = false;
//        _poleDragLoopPlaying = false;

//        _speedLoopIntensitySmoothed = 0f;
//        _airLoopIntensitySmoothed = 0f;
//        _grindLoopIntensitySmoothed = 0f;
//        _poleDragLoopIntensitySmoothed = 0f;

//        StopLoop(fbSpeedLoop, transform.position, "SpeedLoop(stop-all)");
//        StopLoop(fbAirLoop, transform.position, "AirLoop(stop-all)");
//        StopLoop(fbGrindLoop, transform.position, "GrindLoop(stop-all)");
//        StopLoop(fbPoleDragLoop, transform.position, "PoleDragLoop(stop-all)");
//    }

//    private void InitializeStateFromController()
//    {
//        if (controller == null)
//            return;

//        _prevGrounded = controller.IsRiderGrounded;
//        _prevGrinding = controller.IsGrinding;
//        _prevPolePhase = controller.CurrentPolePhase;

//        Vector3 v = controller.Velocity;
//        float planarSpeed = new Vector2(v.x, v.z).magnitude;
//        _prevPlanarSpeed = planarSpeed;

//        _mostNegativeAirYVel = 0f;

//        _speedLoopPlaying = false;
//        _airLoopPlaying = false;
//        _grindLoopPlaying = false;
//        _poleDragLoopPlaying = false;

//        _speedLoopIntensitySmoothed = 0f;
//        _airLoopIntensitySmoothed = 0f;
//        _grindLoopIntensitySmoothed = 0f;
//        _poleDragLoopIntensitySmoothed = 0f;

//        if (_fiLastPushTime != null)
//            _prevLastPushTime = (float)_fiLastPushTime.GetValue(controller);
//    }

//    private void CacheReflection()
//    {
//        if (controller == null)
//            return;

//        _fiLastPushTime = typeof(SkiController).GetField("_lastPushTime", BindingFlags.Instance | BindingFlags.NonPublic);
//        if (_fiLastPushTime == null && logDebugTransitions)
//            Debug.LogWarning("[FEEL] Could not reflect SkiController._lastPushTime. Skate push feedback won't trigger.", this);
//    }
//}
