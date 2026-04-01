using UnityEngine;
using SkiGame.Activities;

[DisallowMultipleComponent]
[RequireComponent(typeof(SkiController))]
public sealed class RaceCourseNpcRacer : MonoBehaviour, ISkiInputSource
{
    [Header("References")]
    [SerializeField] private SkiController skiController;

    [Header("Tuning")]
    [SerializeField] private float baseCruiseSpeed = 12f;
    [SerializeField] private float minLookaheadMeters = 8f;
    [SerializeField] private float maxLookaheadMeters = 24f;
    [SerializeField] private float speedToLookahead = 0.65f;
    [SerializeField] private float turnSensitivity = 2f;
    [SerializeField] private float turnDeadZone = 0.08f;
    [SerializeField] private float neutralLeanForwardBias = 0.18f;
    [SerializeField] private float brakeLeanStrength = 0.95f;
    [SerializeField] private float finishDistanceRemainingMeters = 8f;
    [SerializeField] private float finishCenterlineToleranceMeters = 14f;

    private RaceCourseLine _course;
    private int _leagueNumber;
    private bool _armed;
    private bool _finished;
    private float _elapsed;
    private float _distanceAlong;
    private float _distanceToCenter;
    private float _finishTime;

    public bool IsFinished => _finished;
    public float FinishTimeSeconds => _finishTime;
    public float DistanceAlong => _distanceAlong;

    private void Reset()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>();
    }

    private void Awake()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>();
    }

    public void BeginRace(RaceCourseLine course, int leagueNumber)
    {
        _course = course;
        _leagueNumber = Mathf.Max(1, leagueNumber);
        _armed = true;
        _finished = false;
        _elapsed = 0f;
        _distanceAlong = 0f;
        _distanceToCenter = 0f;
        _finishTime = 0f;

        if (skiController != null)
            skiController.SetExternalInputSource(this);
    }

    public void StopRace()
    {
        _armed = false;

        if (skiController != null)
            skiController.ClearExternalInputSource();
    }

    private void Update()
    {
        if (!_armed || _finished || _course == null)
            return;

        _elapsed += Time.deltaTime;

        if (_course.TryProjectPointOntoCourse(transform.position, out _distanceAlong, out _distanceToCenter, out _))
        {
            float remaining = Mathf.Max(0f, _course.TotalLengthMeters - _distanceAlong);
            if (remaining <= finishDistanceRemainingMeters && _distanceToCenter <= finishCenterlineToleranceMeters)
            {
                _finished = true;
                _finishTime = _elapsed;
                StopRace();

                if (RaceActivityService.Instance != null)
                    RaceActivityService.Instance.NotifyNpcFinished(this);
            }
        }
    }

    public bool HasInput()
    {
        return _armed && !_finished && _course != null && _course.IsRaceInProgress;
    }

    public SkiInputFrame GetSkiInput()
    {
        if (!HasInput())
            return SkiInputFrame.Neutral;

        Vector3 groundNormal = skiController != null && skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude <= 0.0001f)
            downhill = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        downhill.Normalize();

        Vector3 planarVelocity = skiController != null
            ? Vector3.ProjectOnPlane(skiController.Velocity, groundNormal)
            : Vector3.zero;
        float speed = planarVelocity.magnitude;

        float speedMultiplier = 1f;
        var league = _course.GetLeague(_leagueNumber);
        if (league != null)
            speedMultiplier = Mathf.Max(0.1f, league.npcSpeedMultiplier);

        float lookahead = Mathf.Clamp(minLookaheadMeters + speed * speedToLookahead, minLookaheadMeters, maxLookaheadMeters);
        Vector3 lookaheadPoint = _course.SamplePointAtDistance(Mathf.Clamp(_distanceAlong + lookahead, 0f, _course.TotalLengthMeters));
        Vector3 desiredDirection = Vector3.ProjectOnPlane(lookaheadPoint - transform.position, groundNormal);

        if (desiredDirection.sqrMagnitude <= 0.0001f)
            desiredDirection = downhill;

        desiredDirection.Normalize();

        Vector3 localDesired = transform.InverseTransformDirection(desiredDirection);
        float turnSignal = Mathf.Clamp(localDesired.x * turnSensitivity, -1f, 1f);
        float forwardSignal = Mathf.Clamp(localDesired.z, -1f, 1f);

        float desiredSpeed = baseCruiseSpeed * speedMultiplier;
        float speedError = desiredSpeed - speed;
        float lean = Mathf.Clamp((speedError / 8f) + (neutralLeanForwardBias * forwardSignal), -1f, 1f);

        float brake01 = 0f;
        if (speed > desiredSpeed)
        {
            brake01 = Mathf.InverseLerp(desiredSpeed, desiredSpeed + 6f, speed);
            lean -= brake01 * brakeLeanStrength;
        }

        float leftLeg = 0f;
        float rightLeg = 0f;

        if (brake01 > 0.2f)
        {
            float wedge = Mathf.Clamp01(brake01 + (Mathf.Abs(turnSignal) * 0.25f));
            leftLeg = wedge;
            rightLeg = wedge;
        }
        else if (turnSignal > turnDeadZone)
        {
            rightLeg = Mathf.Clamp01(Mathf.Abs(turnSignal));
        }
        else if (turnSignal < -turnDeadZone)
        {
            leftLeg = Mathf.Clamp01(Mathf.Abs(turnSignal));
        }

        return new SkiInputFrame
        {
            leftLeg01 = leftLeg,
            rightLeg01 = rightLeg,
            lean01 = lean,
            polesHeld = speed < desiredSpeed * 0.75f
        };
    }
}