using UnityEngine;

[DisallowMultipleComponent]
public class AutoSkiApproachDriver : MonoBehaviour, ISkiInputSource
{
    [Header("Steering")]
    [SerializeField] private float turnSensitivity = 2.25f;
    [SerializeField] private float turnDeadZone = 0.06f;
    [SerializeField] private float maxCruiseSpeed = 7.5f;
    [SerializeField] private float minCruiseSpeed = 2.2f;
    [SerializeField] private float brakeLeanStrength = 0.85f;
    [SerializeField] private float userCancelLegThreshold = 0.12f;
    [SerializeField] private float userCancelLeanThreshold = 0.12f;

    [SerializeField] private float targetSlowRadius = 3.2f;
    [SerializeField] private float targetStopRadiusMultiplier = 1.8f;
    [SerializeField] private float reverseBrakeFacingDot = -0.15f;
    [SerializeField] private float nearTargetBrakeStrength = 0.95f;

    [Header("Poles")]
    [SerializeField] private float polesMaxSpeed = 5f;
    [SerializeField] private float polesMinFacingDot = 0.55f;

    private SkiController _skiController;
    private InputSystem_Actions _input;
    private InputSystem_Actions.PlayerActions _player;

    private MonoBehaviour _owner;
    private bool _active;
    private Vector3 _targetPosition;
    private float _strength01 = 1f;
    private float _arriveDistance = 1f;
    private bool _allowPoles = true;
    private bool _cancelRequested;

    public bool IsActive => _active;
    public bool CancelRequested => _cancelRequested;

    private void Awake()
    {
        _skiController = GetComponent<SkiController>();
        _input = new InputSystem_Actions();
        _player = _input.Player;
    }

    private void OnEnable()
    {
        _input?.Enable();
    }

    private void OnDisable()
    {
        _input?.Disable();
        StopApproach(null);
    }

    private void Update()
    {
        if (!_active)
            return;

        _cancelRequested = HasUserOverrideInput();
    }

    public void BeginApproach(MonoBehaviour owner, Vector3 targetPosition, float strength01 = 1f, float arriveDistance = 1f, bool allowPoles = true)
    {
        if (_skiController == null)
            _skiController = GetComponent<SkiController>();

        _owner = owner;
        _active = _skiController != null;
        _targetPosition = targetPosition;
        _strength01 = Mathf.Clamp01(strength01);
        _arriveDistance = Mathf.Max(0.05f, arriveDistance);
        _allowPoles = allowPoles;
        _cancelRequested = false;

        if (_active)
            _skiController.SetExternalInputSource(this);
    }

    public void UpdateApproachTarget(Vector3 targetPosition, float strength01 = 1f, float arriveDistance = 1f, bool allowPoles = true)
    {
        _targetPosition = targetPosition;
        _strength01 = Mathf.Clamp01(strength01);
        _arriveDistance = Mathf.Max(0.05f, arriveDistance);
        _allowPoles = allowPoles;
    }

    public void StopApproach(MonoBehaviour owner)
    {
        if (owner != null && _owner != null && _owner != owner)
            return;

        if (_skiController != null)
            _skiController.ClearExternalInputSource();

        _owner = null;
        _active = false;
        _cancelRequested = false;
    }

    public bool HasReachedTarget()
    {
        Vector3 to = _targetPosition - transform.position;
        to.y = 0f;
        return to.magnitude <= _arriveDistance;
    }

    public bool HasInput()
    {
        return _active &&
               !_cancelRequested &&
               _skiController != null &&
               _skiController.enabled &&
               !HasReachedTarget();
    }

    public SkiInputFrame GetSkiInput()
    {
        if (!HasInput())
            return SkiInputFrame.Neutral;

        Vector3 groundNormal = _skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? _skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 toTargetWorld = _targetPosition - transform.position;
        toTargetWorld = Vector3.ProjectOnPlane(toTargetWorld, groundNormal);

        float distanceToTarget = toTargetWorld.magnitude;
        if (distanceToTarget <= 0.0001f)
            return SkiInputFrame.Neutral;

        Vector3 desiredDirection = toTargetWorld / distanceToTarget;
        Vector3 localDesired = transform.InverseTransformDirection(desiredDirection);

        float turnSignal = Mathf.Clamp(localDesired.x * turnSensitivity, -1f, 1f);
        float forwardSignal = Mathf.Clamp(localDesired.z, -1f, 1f);

        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(_skiController.Velocity, groundNormal);
        float speed = planarVelocity.magnitude;
        float facingDot = Vector3.Dot(planarForward, desiredDirection);

        float distanceSlow01 = Mathf.Clamp01(distanceToTarget / Mathf.Max(_arriveDistance, targetSlowRadius));
        float desiredSpeed = Mathf.Lerp(minCruiseSpeed, maxCruiseSpeed, _strength01);
        desiredSpeed *= Mathf.Lerp(0.20f, 1f, distanceSlow01);
        desiredSpeed *= Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(-0.25f, 0.95f, facingDot));

        float lean = Mathf.Clamp((desiredSpeed - speed) / 5.5f, -0.75f, 0.95f);

        if (facingDot < reverseBrakeFacingDot)
            lean = Mathf.Min(lean, -0.65f);

        if (distanceToTarget <= _arriveDistance * targetStopRadiusMultiplier)
            lean = Mathf.Min(lean, -nearTargetBrakeStrength);

        float brake01 = 0f;
        if (speed > desiredSpeed)
        {
            brake01 = Mathf.InverseLerp(desiredSpeed, desiredSpeed + 3f, speed);
            lean -= brake01 * brakeLeanStrength;
        }

        float leftLeg = 0f;
        float rightLeg = 0f;

        if ((brake01 > 0.18f || distanceToTarget <= _arriveDistance * targetStopRadiusMultiplier) &&
            Mathf.Abs(turnSignal) < 0.22f)
        {
            leftLeg = Mathf.Max(brake01, 0.45f);
            rightLeg = Mathf.Max(brake01, 0.45f);
        }
        else if (turnSignal > turnDeadZone)
        {
            rightLeg = Mathf.Clamp01(Mathf.Abs(turnSignal));
        }
        else if (turnSignal < -turnDeadZone)
        {
            leftLeg = Mathf.Clamp01(Mathf.Abs(turnSignal));
        }

        bool polesHeld = _allowPoles &&
                         distanceToTarget > _arriveDistance * 2.25f &&
                         speed <= polesMaxSpeed &&
                         facingDot >= polesMinFacingDot &&
                         lean > 0.18f;

        return new SkiInputFrame
        {
            leftLeg01 = leftLeg,
            rightLeg01 = rightLeg,
            lean01 = Mathf.Clamp(lean + (forwardSignal * 0.10f), -1f, 1f),
            polesHeld = polesHeld,
            jumpHeld = false,
            jumpPressedThisFrame = false,
            jumpReleasedThisFrame = false
        };
    }

    private bool HasUserOverrideInput()
    {
        if (_input == null)
            return false;

        float leftLeg = Mathf.Abs(_player.LeftSki.ReadValue<float>());
        float rightLeg = Mathf.Abs(_player.RightSki.ReadValue<float>());
        float lean = Mathf.Abs(_player.Lean.ReadValue<float>());

        return leftLeg >= userCancelLegThreshold ||
               rightLeg >= userCancelLegThreshold ||
               lean >= userCancelLeanThreshold ||
               _player.Poles.IsPressed() ||
               _player.Jump.IsPressed() ||
               _player.EquipSkis.IsPressed();
    }
}
