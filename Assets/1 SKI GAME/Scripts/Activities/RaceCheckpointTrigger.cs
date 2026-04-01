using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class RaceCheckpointTrigger : MonoBehaviour
{
    [SerializeField] private RaceCourseLine owner;
    [SerializeField] private int checkpointIndex = -1;

    public void Initialize(RaceCourseLine raceOwner, int index)
    {
        owner = raceOwner;
        checkpointIndex = index;
    }

    private void Awake()
    {
        AutoBindIfNeeded();
    }

    private void OnEnable()
    {
        AutoBindIfNeeded();
    }

    private void AutoBindIfNeeded()
    {
        if (owner == null)
            owner = GetComponentInParent<RaceCourseLine>();

        if (checkpointIndex < 0 && transform.parent != null)
            checkpointIndex = transform.GetSiblingIndex();
    }

    private void OnTriggerEnter(Collider other)
    {
        AutoBindIfNeeded();

        if (owner == null || checkpointIndex < 0)
            return;

        owner.TryConsumeCheckpointTrigger(checkpointIndex, other);
    }
}