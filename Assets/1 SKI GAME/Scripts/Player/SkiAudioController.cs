using UnityEngine;

[DisallowMultipleComponent]
public class SkiAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkiController controller;

    [Header("Looping Layers")]
    [Tooltip("Base snow / ski scrape loop.")]
    public AudioSource snowLoop;

    [Tooltip("Extra harsh carve / edge scrape loop.")]
    public AudioSource carveLoop;

    [Tooltip("Wind rushing past at speed.")]
    public AudioSource windLoop;

    [Tooltip("Continuous pole drag loop when poles are dug in.")]
    public AudioSource poleLoop;

    [Header("Speed Mapping")]
    [Tooltip("Speed at which audio curves hit their max.")]
    public float maxSpeedForAudio = 30f;

    public AnimationCurve speedToSnowVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve speedToSnowPitch = AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f);

    public AnimationCurve speedToWindVolume = AnimationCurve.Linear(0.2f, 0f, 1f, 1f);
    public AnimationCurve speedToWindPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.4f);

    [Header("Carve Mapping")]
    [Tooltip("Maximum angle (deg) between ski direction and velocity that maps to full carve intensity.")]
    public float maxCarveAngleForAudio = 45f;

    [Tooltip("Carve intensity (0..1) -> carve loop volume multiplier.")]
    public AnimationCurve carveToVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Carve intensity (0..1) -> carve loop pitch.")]
    public AnimationCurve carveToPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.1f);

    [Header("Pole Mapping")]
    [Tooltip("0..1 intensity -> pole drag loop volume.")]
    public AnimationCurve poleDragToVolume = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);

    [Tooltip("Speed -> pole drag pitch.")]
    public AnimationCurve poleSpeedToPitch = AnimationCurve.Linear(0f, 0.9f, 1f, 1.1f);

    [Header("One-Shot SFX")]
    [Tooltip("Used for jump/land, pole plant, pole release, footsteps, etc.")]
    public AudioSource oneShotSource;

    public AudioClip polePlantClip;
    public AudioClip poleReleaseClip;
    public AudioClip footstepClip; // for future walking controller

    [Header("Blend / Smoothing")]
    public float volumeLerpSpeed = 8f;
    public float pitchLerpSpeed = 8f;

    private SkiController.PoleStrokePhase _prevPolePhase;
    private bool _prevGrounded;

    private void Awake()
    {
        if (!controller)
            controller = GetComponent<SkiController>();

        InitLoop(snowLoop);
        InitLoop(carveLoop);
        InitLoop(windLoop);
        InitLoop(poleLoop);

        if (controller != null)
        {
            _prevPolePhase = controller.CurrentPolePhase;
            _prevGrounded = controller.IsRiderGrounded;
        }
    }

    private void InitLoop(AudioSource src)
    {
        if (!src) return;
        src.loop = true;
        src.playOnAwake = false;
        if (!src.isPlaying)
            src.Play();
    }

    private void Update()
    {
        if (!controller)
            return;

        // --- Core motion telemetry ---
        Vector3 velocity = controller.Velocity;
        Vector3 groundNormal = controller.GroundNormal;

        Vector3 velOnPlane = Vector3.ProjectOnPlane(velocity, groundNormal);
        float planarSpeed = velOnPlane.magnitude;
        float normSpeed = maxSpeedForAudio > 0f
            ? Mathf.Clamp01(planarSpeed / maxSpeedForAudio)
            : 0f;

        bool grounded = controller.IsRiderGrounded;

        // --- Carve intensity + side (left/right) based on slip angle ---
        float carveAmount = 0f; // 0..1
        float carveSide = 0f; // -1..1 (left/right carve pan)

        Vector3 skiForward = controller.SkiForwardOnPlane;
        if (planarSpeed > 0.1f && skiForward.sqrMagnitude > 0.0001f)
        {
            Vector3 planeDir = velOnPlane.normalized;
            Vector3 skiDir = skiForward.normalized;

            float signedAngle = Vector3.SignedAngle(skiDir, planeDir, groundNormal);
            float angleAbs = Mathf.Abs(signedAngle);
            float maxAngle = Mathf.Max(1f, maxCarveAngleForAudio);

            carveAmount = Mathf.InverseLerp(0f, maxAngle, Mathf.Clamp(angleAbs, 0f, maxAngle));
            carveSide = Mathf.Sign(signedAngle); // -1 left, +1 right (approx)
        }

        // --- Update layers ---
        UpdateSnowLayer(grounded, normSpeed);
        UpdateWindLayer(grounded, normSpeed);
        UpdateCarveLayer(grounded, normSpeed, carveAmount, carveSide);
        UpdatePoleLayer(grounded, normSpeed);

        // --- One-shots ---
        HandlePoleOneShots(grounded);

        _prevPolePhase = controller.CurrentPolePhase;
        _prevGrounded = grounded;
    }

    // ---------------------------------------------------------------------
    // LAYERS
    // ---------------------------------------------------------------------

    private void UpdateSnowLayer(bool grounded, float normSpeed)
    {
        if (!snowLoop) return;

        float targetVol = grounded ? speedToSnowVolume.Evaluate(normSpeed) : 0f;
        float targetPitch = speedToSnowPitch.Evaluate(normSpeed);

        Apply(snowLoop, targetVol, targetPitch);
    }

    private void UpdateWindLayer(bool grounded, float normSpeed)
    {
        if (!windLoop) return;

        float targetVol = speedToWindVolume.Evaluate(normSpeed);
        if (grounded)
            targetVol *= 0.8f; // bias wind louder in air

        float targetPitch = speedToWindPitch.Evaluate(normSpeed);

        Apply(windLoop, targetVol, targetPitch);
    }

    private void UpdateCarveLayer(bool grounded, float normSpeed, float carveAmount, float carveSide)
    {
        if (!carveLoop) return;

        if (!grounded)
            carveAmount = 0f;

        float baseVol = speedToSnowVolume.Evaluate(normSpeed);
        float targetVol = baseVol * carveToVolume.Evaluate(carveAmount);
        float targetPitch = carveToPitch.Evaluate(carveAmount);

        Apply(carveLoop, targetVol, targetPitch);

        // Pan left/right based on carve side
        if (Mathf.Abs(carveSide) > 0.01f)
        {
            carveLoop.panStereo = Mathf.Clamp(carveSide * 0.7f, -1f, 1f);
        }
        else
        {
            carveLoop.panStereo = Mathf.MoveTowards(carveLoop.panStereo, 0f, Time.deltaTime * 2f);
        }
    }

    private void UpdatePoleLayer(bool grounded, float normSpeed)
    {
        if (!poleLoop || controller == null) return;

        float intensity = 0f;

        var phase = controller.CurrentPolePhase;

        bool leftContact = controller.LeftPoleContact != null && controller.LeftPoleContact.IsInContact;
        bool rightContact = controller.RightPoleContact != null && controller.RightPoleContact.IsInContact;
        bool anyContact = leftContact || rightContact;

        // Continuous drag noise only when poles are actually digging into snow
        if (grounded && anyContact && phase == SkiController.PoleStrokePhase.Drag)
        {
            intensity = normSpeed;
        }

        float targetVol = poleDragToVolume.Evaluate(intensity);
        float targetPitch = poleSpeedToPitch.Evaluate(normSpeed);

        Apply(poleLoop, targetVol, targetPitch);
    }

    // ---------------------------------------------------------------------
    // POLE ONE-SHOTS (plant + release)
    // ---------------------------------------------------------------------

    private void HandlePoleOneShots(bool grounded)
    {
        if (!oneShotSource || controller == null)
            return;

        var phase = controller.CurrentPolePhase;

        bool leftContact = controller.LeftPoleContact != null && controller.LeftPoleContact.IsInContact;
        bool rightContact = controller.RightPoleContact != null && controller.RightPoleContact.IsInContact;
        bool anyContact = leftContact || rightContact;

        // Plant: Idle -> Entry while grounded and the pole actually hits snow
        if (_prevPolePhase == SkiController.PoleStrokePhase.Idle &&
            phase == SkiController.PoleStrokePhase.Entry &&
            grounded &&
            anyContact &&
            polePlantClip)
        {
            PlayOneShot(polePlantClip, controller.Velocity.magnitude);
        }

        // Release: Drag -> FollowThrough while grounded
        if (_prevPolePhase == SkiController.PoleStrokePhase.Drag &&
            phase == SkiController.PoleStrokePhase.FollowThrough &&
            grounded &&
            poleReleaseClip)
        {
            PlayOneShot(poleReleaseClip, controller.Velocity.magnitude);
        }
    }

    private void PlayOneShot(AudioClip clip, float speed)
    {
        if (!clip || !oneShotSource)
            return;

        float normSpeed = maxSpeedForAudio > 0f
            ? Mathf.Clamp01(speed / maxSpeedForAudio)
            : 0f;

        oneShotSource.pitch = Mathf.Lerp(0.9f, 1.1f, normSpeed);
        oneShotSource.PlayOneShot(clip);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void Apply(AudioSource src, float targetVol, float targetPitch)
    {
        float dt = Time.deltaTime;
        src.volume = Mathf.MoveTowards(src.volume, targetVol, volumeLerpSpeed * dt);
        src.pitch = Mathf.MoveTowards(src.pitch, targetPitch, pitchLerpSpeed * dt);
    }

    // ---------------------------------------------------------------------
    // FUTURE: footsteps (when skis are off)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Call this from a future walking controller whenever a footstep occurs.
    /// </summary>
    public void PlayFootstep(Vector3 worldPosition, float footSpeed = 1f)
    {
        if (!footstepClip || !oneShotSource)
            return;

        oneShotSource.transform.position = worldPosition;
        oneShotSource.pitch = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(footSpeed));
        oneShotSource.PlayOneShot(footstepClip);
    }
}
