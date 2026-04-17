using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class RescueCompanionSignal : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private SkiController skiController;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private float attractRange = 18f;
    [SerializeField] private float stopDistance = 3.25f;
    [SerializeField] private float repathInterval = 0.4f;

    private RescueCasualtyTarget _target;
    private float _nextRepathAt;

    public void Initialize(RescueCasualtyTarget target)
    {
        _target = target;
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (skiController == null)
            skiController = GetComponentInChildren<SkiController>();

        if (walkingController == null)
            walkingController = GetComponentInChildren<WalkingController>();

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(ForceHealthyCompanionPresentationNextFrame());
    }

    private IEnumerator ForceHealthyCompanionPresentationNextFrame()
    {
        yield return null;
        ForceHealthyCompanionPresentation();
    }

    private void ForceHealthyCompanionPresentation()
    {
        if (walkingController != null)
            walkingController.ForceEnterWalkMode();
        else if (skiController != null)
            skiController.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = navAgent != null;
            rb.useGravity = navAgent == null;
            rb.detectCollisions = true;
        }
    }

    private void Update()
    {
        if (_target == null || _target.IsTransportLocked)
        {
            StopMoving();
            return;
        }

        if (_target.TargetKind != RescueTargetKind.CompanionPassenger &&
            _target.TargetKind != RescueTargetKind.StrandedPassenger)
        {
            StopMoving();
            return;
        }

        RescueService service = _target.Service;
        if (service == null || !service.HasActiveMission)
        {
            StopMoving();
            return;
        }

        SnowmobileController snowmobile = service.ActiveSnowmobile;
        if (snowmobile == null || !snowmobile.IsMounted)
        {
            StopMoving();
            return;
        }

        Vector3 destination = snowmobile.transform.position;
        float distance = Vector3.Distance(transform.position, destination);

        if (distance > attractRange || distance <= stopDistance || navAgent == null)
        {
            StopMoving();
            return;
        }

        if (Time.time < _nextRepathAt)
            return;

        _nextRepathAt = Time.time + repathInterval;

        NavMeshPath path = new NavMeshPath();
        bool hasPath = navAgent.CalculatePath(destination, path);
        if (!hasPath || path.status != NavMeshPathStatus.PathComplete)
        {
            StopMoving();
            return;
        }

        navAgent.isStopped = false;
        navAgent.stoppingDistance = stopDistance;
        navAgent.SetDestination(destination);
    }

    private void StopMoving()
    {
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
    }
}