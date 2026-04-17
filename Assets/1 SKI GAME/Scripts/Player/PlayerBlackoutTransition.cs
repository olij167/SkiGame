using System.Collections;
using SkiGame.Progression;
using TimeWeather;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerBlackoutTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private SorenessMeter sorenessMeter;
    [SerializeField] private TimeController timeController;
    [SerializeField] private Volume blackoutVolume;
    [SerializeField] private PlayerStatsTracker playerStatsTracker;

    [SerializeField] private AutoSkiApproachDriver autoSkiApproachDriver;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private LiftRider liftRider;
    [SerializeField] private SkiResortStateController skiResortStateController;

    [Header("Fade")]
    [SerializeField] private float fadeInSeconds = 0.45f;
    [SerializeField] private float holdBlackSeconds = 0.18f;
    [SerializeField] private float fadeOutSeconds = 0.55f;

    [SerializeField]
    private AnimationCurve fadeInCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [SerializeField]
    private AnimationCurve fadeOutCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Header("Wake Up")]
    [SerializeField] private bool advanceToNextMorning = true;
    [SerializeField] private float nextMorningHour = 7f;
    [SerializeField] private bool fullyRecoverOnWake = true;

    private Coroutine _transitionRoutine;

    public bool IsTransitionActive => _transitionRoutine != null;

    private void Reset()
    {
        if (skiController == null) skiController = GetComponent<SkiController>();
        if (sorenessMeter == null) sorenessMeter = GetComponent<SorenessMeter>();
        if (playerStatsTracker == null) playerStatsTracker = GetComponent<PlayerStatsTracker>();
        if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
        if (autoSkiApproachDriver == null) autoSkiApproachDriver = GetComponent<AutoSkiApproachDriver>();
        if (walkingController == null) walkingController = GetComponent<WalkingController>();
        if (liftRider == null) liftRider = GetComponent<LiftRider>();
        if (skiResortStateController == null) skiResortStateController = FindObjectOfType<SkiResortStateController>();
    }

    private void Awake()
    {
        ResolveReferences();

        if (blackoutVolume != null)
            blackoutVolume.weight = 0f;
    }

    private void OnDisable()
    {
        if (blackoutVolume != null)
            blackoutVolume.weight = 0f;

        _transitionRoutine = null;
    }

    public bool TryRespawnAtNearestResort(out string message)
    {
        ResolveReferences();

        if (_transitionRoutine != null)
        {
            message = "Blackout transition already in progress.";
            return false;
        }

        if (skiController == null)
        {
            message = "Player not found.";
            return false;
        }

        if (!TryFindBestResortExit(skiController.transform.position, out Transform exitPoint))
        {
            message = "No ski resort exit point was found.";
            return false;
        }

        _transitionRoutine = StartCoroutine(CoRespawnAtResort(exitPoint.position, exitPoint.rotation));
        message = "You blacked out and woke up back at the resort.";
        return true;
    }

    private IEnumerator CoRespawnAtResort(Vector3 wakePosition, Quaternion wakeRotation)
    {
        ResolveReferences();

        // Stop any movement/interaction systems that may still be steering the player
        // before we perform the blackout teleport.
        ClearBlackoutRespawnDrivers();

        yield return FadeVolumeTo(1f, fadeInSeconds, fadeInCurve);

        // Clear again while fully black in case another system re-applied control during the fade.
        ClearBlackoutRespawnDrivers();

        if (fullyRecoverOnWake && sorenessMeter != null)
            sorenessMeter.ResetSoreness();

        if (skiController != null)
            skiController.TeleportToSpawn(wakePosition, wakeRotation, snapToGround: true);

        if (playerStatsTracker != null)
            playerStatsTracker.ResetBaseline();

        if (advanceToNextMorning && timeController != null)
            timeController.AdvanceToNextDayAt(nextMorningHour);

        if (holdBlackSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdBlackSeconds);

        yield return FadeVolumeTo(0f, fadeOutSeconds, fadeOutCurve);
        _transitionRoutine = null;
    }

    private IEnumerator FadeVolumeTo(float targetWeight, float duration, AnimationCurve curve)
    {
        if (blackoutVolume == null)
        {
            yield return null;
            yield break;
        }

        float start = blackoutVolume.weight;
        float elapsed = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float shaped = curve != null ? Mathf.Clamp01(curve.Evaluate(t)) : t;
            blackoutVolume.weight = Mathf.Lerp(start, targetWeight, shaped);
            yield return null;
        }

        blackoutVolume.weight = targetWeight;
    }

    private bool TryFindBestResortExit(Vector3 fromPosition, out Transform exitPoint)
    {
        exitPoint = null;

        SkiResortAccessManager resortAccessManager = SkiResortAccessManager.Instance != null
            ? SkiResortAccessManager.Instance
            : FindObjectOfType<SkiResortAccessManager>();

        if (resortAccessManager != null &&
            resortAccessManager.TryGetClosestActiveResort(fromPosition, out _, out Transform activeResortPoint) &&
            activeResortPoint != null)
        {
            exitPoint = activeResortPoint;
            return true;
        }

        SkiResortZone[] zones = FindObjectsOfType<SkiResortZone>(includeInactive: false);
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < zones.Length; i++)
        {
            SkiResortZone zone = zones[i];
            Transform resortPoint = zone != null ? zone.RespawnPoint : null;
            if (resortPoint == null)
                continue;

            float sqr = (resortPoint.position - fromPosition).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                exitPoint = resortPoint;
            }
        }

        return exitPoint != null;
    }

    private void ResolveReferences()
    {
        if (skiController == null) skiController = GetComponent<SkiController>() ?? FindObjectOfType<SkiController>();
        if (sorenessMeter == null) sorenessMeter = GetComponent<SorenessMeter>() ?? FindObjectOfType<SorenessMeter>();
        if (playerStatsTracker == null) playerStatsTracker = GetComponent<PlayerStatsTracker>() ?? FindObjectOfType<PlayerStatsTracker>();
        if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();

        if (autoSkiApproachDriver == null) autoSkiApproachDriver = GetComponent<AutoSkiApproachDriver>() ?? FindObjectOfType<AutoSkiApproachDriver>();
        if (walkingController == null) walkingController = GetComponent<WalkingController>() ?? FindObjectOfType<WalkingController>();
        if (liftRider == null) liftRider = GetComponent<LiftRider>() ?? FindObjectOfType<LiftRider>();
        if (skiResortStateController == null) skiResortStateController = FindObjectOfType<SkiResortStateController>();
    }

    private void ClearBlackoutRespawnDrivers()
    {
        if (autoSkiApproachDriver != null)
            autoSkiApproachDriver.StopApproach(null);

        if (walkingController != null)
            walkingController.ClearExternalMove();

        if (liftRider != null && liftRider.CurrentCarrier != null)
            liftRider.CurrentCarrier.DetachRider(liftRider);

        if (skiResortStateController != null)
            skiResortStateController.CancelEnter();

        if (skiController != null)
            skiController.ClearExternalInputSource();
    }
}
