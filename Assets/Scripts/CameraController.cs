using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Generic third-person orbit camera using the new Input System.
/// - Assign a target Transform (or tag something "Player" and leave empty).
/// - Assign an InputActionReference (Vector2) for look input (e.g. Player/Look).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("What the camera orbits around. If left null, will try to find an object tagged 'Player' at Start.")]
    [SerializeField] private Transform target;

    [Header("Input (New Input System)")]
    [Tooltip("InputActionReference providing a Vector2 look input (e.g. from a 'Look' action).")]
    [SerializeField] private InputActionReference lookAction;

    [Header("Orbit")]
    public float distance = 5f;
    public float height = 2f;
    public float sensitivityX = 150f;
    public float sensitivityY = 100f;
    public float minPitch = -30f;
    public float maxPitch = 70f;
    public float smoothTime = 0.05f;

    [Header("Collision")]
    public LayerMask collisionLayers = ~0;
    public float collisionRadius = 0.2f;

    private float _yaw;
    private float _pitch;
    private Vector3 _camVelocity;

    /// <summary>
    /// Allows other scripts to set the camera target at runtime.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            _yaw = target.eulerAngles.y;
        }
    }

    private void OnEnable()
    {
        // Enable look action if provided
        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Disable();
        }
    }

    private void Start()
    {
        // Auto-find a target if none assigned
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        if (target != null)
        {
            _yaw = target.eulerAngles.y;
        }
    }

    private void Update()
    {
        Vector2 look = Vector2.zero;

        if (lookAction != null && lookAction.action != null)
        {
            look = lookAction.action.ReadValue<Vector2>();
        }

        _yaw += look.x * sensitivityX * Time.deltaTime;
        _pitch -= look.y * sensitivityY * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 targetPos = target.position + Vector3.up * height;
        Vector3 desiredPos = targetPos - rot * Vector3.forward * distance;

        // Simple collision: pull camera closer if blocked
        Vector3 toCam = desiredPos - targetPos;
        float distanceToCam = toCam.magnitude;

        if (distanceToCam > 0.01f)
        {
            if (Physics.SphereCast(targetPos, collisionRadius,
                                   toCam.normalized,
                                   out RaycastHit hit,
                                   distanceToCam,
                                   collisionLayers,
                                   QueryTriggerInteraction.Ignore))
            {
                desiredPos = targetPos + toCam.normalized * Mathf.Max(hit.distance - 0.05f, 0f);
            }
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _camVelocity, smoothTime);
        transform.rotation = rot;
    }
}
