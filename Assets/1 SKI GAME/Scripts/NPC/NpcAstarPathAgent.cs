using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcAstarPathAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WalkingController walkingController;

    [Tooltip("Optional A* movement/path component (AIPath, RichAI, AILerp, etc).")]
    [SerializeField] private MonoBehaviour astarAgentBehaviour;

    [Header("Fallback Steering")]
    [SerializeField] private float arriveDistance = 1.75f;
    [SerializeField] private float slowRadius = 5f;
    [SerializeField] private bool sprintWhenFar = true;
    [SerializeField] private float sprintDistance = 10f;

    [Header("A* Reflection Settings")]
    [SerializeField] private bool disableAstarTransformMovement = true;

    [Header("Stall Detection")]
    [SerializeField] private float stuckDistanceThreshold = 0.35f;
    [SerializeField] private float stuckTime = 2.25f;
    [SerializeField] private float repathInterval = 0.75f;
    [SerializeField] private bool logBindingWarnings = true;

    private Vector3 _destination;
    private bool _hasDestination;

    private Vector3 _lastProgressPosition;
    private float _lastProgressTime;
    private float _nextRepathTime;
    private bool _isStuck;

    private Type _agentType;
    private PropertyInfo _propDestination;
    private PropertyInfo _propSteeringTarget;
    private PropertyInfo _propDesiredVelocity;
    private PropertyInfo _propReachedDestination;
    private PropertyInfo _propRemainingDistance;
    private PropertyInfo _propCanMove;
    private PropertyInfo _propIsStopped;
    private MethodInfo _methodSearchPath;
    private bool _loggedMissingBindingWarning;
    private bool _loggedSearchPathWarning;

    public bool HasDestination => _hasDestination;
    public Vector3 Destination => _destination;
    public bool IsStuck => _hasDestination && _isStuck;
    public bool HasUsableAstarBinding => astarAgentBehaviour != null && (_propDestination != null || _propSteeringTarget != null || _propDesiredVelocity != null);

    public bool ReachedDestination
    {
        get
        {
            if (!_hasDestination)
                return true;

            if (TryGetBool(_propReachedDestination, out bool reached))
                return reached;

            if (TryGetFloat(_propRemainingDistance, out float remaining))
                return remaining <= arriveDistance;

            return Vector3.Distance(transform.position, _destination) <= arriveDistance;
        }
    }

    private void Awake()
    {
        if (walkingController == null)
            walkingController = GetComponent<WalkingController>();

        CacheAstarReflection();
        ResetProgressTracking();
    }

    private void OnValidate()
    {
        CacheAstarReflection();
    }

    private void Update()
    {
        if (!_hasDestination || walkingController == null)
            return;

        Vector3 steeringTarget = GetSteeringTarget();
        Vector3 planar = Vector3.ProjectOnPlane(steeringTarget - transform.position, Vector3.up);
        float distance = planar.magnitude;

        if (ReachedDestination || distance <= arriveDistance)
        {
            Stop();
            return;
        }

        Vector3 desiredWorldDir = planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.zero;

        walkingController.GetMoveBasis(out Vector3 basisForward, out Vector3 basisRight);

        float x = Vector3.Dot(desiredWorldDir, basisRight);
        float y = Vector3.Dot(desiredWorldDir, basisForward);

        Vector2 move = new Vector2(x, y);
        float mag = 1f;

        if (distance < slowRadius && slowRadius > arriveDistance)
            mag = Mathf.InverseLerp(arriveDistance, slowRadius, distance);

        move = Vector2.ClampMagnitude(move, 1f) * mag;

        bool sprint = sprintWhenFar && distance >= sprintDistance;
        walkingController.SetExternalMove(move, sprint);

        TickProgressTracking();

        if (astarAgentBehaviour != null && Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + repathInterval;
            if (_methodSearchPath != null)
            {
                _methodSearchPath.Invoke(astarAgentBehaviour, null);
            }
            else if (logBindingWarnings && !_loggedSearchPathWarning)
            {
                _loggedSearchPathWarning = true;
                Debug.LogWarning($"[{nameof(NpcAstarPathAgent)}] A* agent on {name} has no SearchPath() method. Falling back to direct steering only.", this);
            }
        }
    }

    public void SetDestination(Vector3 worldDestination)
    {
        _destination = worldDestination;
        _hasDestination = true;
        _isStuck = false;
        ResetProgressTracking();

        PushDestinationToAstar(worldDestination);
    }

    public void Stop()
    {
        _hasDestination = false;
        _isStuck = false;

        if (walkingController != null)
            walkingController.ClearExternalMove();

        if (_propIsStopped != null && astarAgentBehaviour != null)
            SafeSet(_propIsStopped, astarAgentBehaviour, true);
    }

    private void ResetProgressTracking()
    {
        _lastProgressPosition = transform.position;
        _lastProgressTime = Time.time;
        _nextRepathTime = Time.time + repathInterval;
    }

    private void TickProgressTracking()
    {
        Vector3 planarDelta = Vector3.ProjectOnPlane(transform.position - _lastProgressPosition, Vector3.up);

        if (planarDelta.magnitude >= stuckDistanceThreshold)
        {
            _lastProgressPosition = transform.position;
            _lastProgressTime = Time.time;
            _isStuck = false;
            return;
        }

        if (Time.time - _lastProgressTime >= stuckTime)
            _isStuck = true;
    }

    private void CacheAstarReflection()
    {
        _agentType = astarAgentBehaviour != null ? astarAgentBehaviour.GetType() : null;

        _propDestination = null;
        _propSteeringTarget = null;
        _propDesiredVelocity = null;
        _propReachedDestination = null;
        _propRemainingDistance = null;
        _propCanMove = null;
        _propIsStopped = null;
        _methodSearchPath = null;

        if (_agentType == null)
            return;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        _propDestination = _agentType.GetProperty("destination", flags);
        _propSteeringTarget = _agentType.GetProperty("steeringTarget", flags);
        _propDesiredVelocity = _agentType.GetProperty("desiredVelocity", flags);
        _propReachedDestination = _agentType.GetProperty("reachedDestination", flags);
        _propRemainingDistance = _agentType.GetProperty("remainingDistance", flags);
        _propCanMove = _agentType.GetProperty("canMove", flags);
        _propIsStopped = _agentType.GetProperty("isStopped", flags);
        _methodSearchPath = _agentType.GetMethod("SearchPath", flags);

        if (disableAstarTransformMovement && astarAgentBehaviour != null)
        {
            if (_propCanMove != null)
                SafeSet(_propCanMove, astarAgentBehaviour, false);

            if (_propIsStopped != null)
                SafeSet(_propIsStopped, astarAgentBehaviour, false);
        }

        if (logBindingWarnings && !_loggedMissingBindingWarning && !HasUsableAstarBinding)
        {
            _loggedMissingBindingWarning = true;
            Debug.LogWarning($"[{nameof(NpcAstarPathAgent)}] A* bindings on {name} are incomplete. Walking support will use destination fallback steering.", this);
        }
    }

    private void PushDestinationToAstar(Vector3 worldDestination)
    {
        if (astarAgentBehaviour == null)
            return;

        if (_propDestination != null)
            SafeSet(_propDestination, astarAgentBehaviour, worldDestination);

        if (_propIsStopped != null)
            SafeSet(_propIsStopped, astarAgentBehaviour, false);

        _methodSearchPath?.Invoke(astarAgentBehaviour, null);
    }

    private Vector3 GetSteeringTarget()
    {
        if (astarAgentBehaviour != null)
        {
            if (TryGetVector3(_propSteeringTarget, out Vector3 steeringTarget))
                return steeringTarget;

            if (TryGetVector3(_propDesiredVelocity, out Vector3 desiredVelocity))
            {
                Vector3 planarVel = Vector3.ProjectOnPlane(desiredVelocity, Vector3.up);
                if (planarVel.sqrMagnitude > 0.0001f)
                    return transform.position + planarVel.normalized * 2f;
            }
        }

        return _destination;
    }

    private bool TryGetVector3(PropertyInfo prop, out Vector3 value)
    {
        value = default;
        if (prop == null || astarAgentBehaviour == null)
            return false;

        try
        {
            object raw = prop.GetValue(astarAgentBehaviour, null);
            if (raw is Vector3 v)
            {
                value = v;
                return true;
            }
        }
        catch { }

        return false;
    }

    private bool TryGetBool(PropertyInfo prop, out bool value)
    {
        value = false;
        if (prop == null || astarAgentBehaviour == null)
            return false;

        try
        {
            object raw = prop.GetValue(astarAgentBehaviour, null);
            if (raw is bool b)
            {
                value = b;
                return true;
            }
        }
        catch { }

        return false;
    }

    private bool TryGetFloat(PropertyInfo prop, out float value)
    {
        value = 0f;
        if (prop == null || astarAgentBehaviour == null)
            return false;

        try
        {
            object raw = prop.GetValue(astarAgentBehaviour, null);
            if (raw is float f)
            {
                value = f;
                return true;
            }

            if (raw is double d)
            {
                value = (float)d;
                return true;
            }
        }
        catch { }

        return false;
    }

    private static void SafeSet(PropertyInfo prop, object target, object value)
    {
        if (prop == null || target == null || !prop.CanWrite)
            return;

        try
        {
            prop.SetValue(target, value, null);
        }
        catch { }
    }
}
