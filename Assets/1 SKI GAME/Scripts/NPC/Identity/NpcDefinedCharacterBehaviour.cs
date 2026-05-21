using UnityEngine;

public enum NpcDefinedCharacterMode
{
    UseGenericNpcBrain,
    StayAtSpawn,
    SocialOnlyAtSpawn,
    WalkToTargetThenIdle,
    WalkToTargetThenDeactivate,
    DeactivateOnStart,
    ManualOnly
}

[DisallowMultipleComponent]
public sealed class NpcDefinedCharacterBehaviour : MonoBehaviour
{
    [Header("Defined Character Behaviour")]
    [SerializeField] private NpcDefinedCharacterMode mode = NpcDefinedCharacterMode.StayAtSpawn;
    [SerializeField] private Transform target;
    [SerializeField] private bool allowGenericSkiBrain;
    [SerializeField] private bool allowSocialLoiter = true;
    [SerializeField] private bool allowPopulationBorrowing;
    [SerializeField] private float arrivalDistance = 2.5f;
    [SerializeField] private bool applyOnStart = true;

    private NpcSkierBrain _brain;
    private WalkingController _walking;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private bool _arrived;

    public NpcDefinedCharacterMode Mode => mode;
    public bool AllowGenericSkiBrain => allowGenericSkiBrain || mode == NpcDefinedCharacterMode.UseGenericNpcBrain;
    public bool AllowSocialLoiter => allowSocialLoiter;
    public bool AllowPopulationBorrowing => allowPopulationBorrowing;

    private void Awake()
    {
        _brain = GetComponent<NpcSkierBrain>();
        _walking = GetComponent<WalkingController>();
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
    }

    private void Start()
    {
        if (applyOnStart)
            ApplyInitialMode();
    }

    public void ApplyInitialMode()
    {
        switch (mode)
        {
            case NpcDefinedCharacterMode.UseGenericNpcBrain:
                break;

            case NpcDefinedCharacterMode.DeactivateOnStart:
                gameObject.SetActive(false);
                break;

            case NpcDefinedCharacterMode.StayAtSpawn:
            case NpcDefinedCharacterMode.SocialOnlyAtSpawn:
            case NpcDefinedCharacterMode.ManualOnly:
                _brain?.PauseGenericBehaviourForDefinedCharacter();
                _walking?.ClearExternalMove();
                transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
                break;

            case NpcDefinedCharacterMode.WalkToTargetThenIdle:
            case NpcDefinedCharacterMode.WalkToTargetThenDeactivate:
                _brain?.PauseGenericBehaviourForDefinedCharacter();
                break;
        }
    }

    private void Update()
    {
        if (_arrived || target == null)
            return;

        if (mode != NpcDefinedCharacterMode.WalkToTargetThenIdle &&
            mode != NpcDefinedCharacterMode.WalkToTargetThenDeactivate)
            return;

        Vector3 toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (toTarget.magnitude <= arrivalDistance)
        {
            _arrived = true;
            _walking?.ClearExternalMove();

            if (mode == NpcDefinedCharacterMode.WalkToTargetThenDeactivate)
                gameObject.SetActive(false);

            return;
        }

        _walking?.ForceEnterWalkMode();
        _walking?.SetExternalMoveToward(toTarget.normalized, 0.65f, sprint: false, facingGateDot: -1f);
    }
}