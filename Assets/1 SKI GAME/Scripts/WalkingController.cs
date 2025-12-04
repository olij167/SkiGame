using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal walk controller + ski toggle:
/// - Hold Player/Interact for toggleHoldDuration to switch between ski & walk mode.
/// - In walk mode: simple walk/run movement.
/// - In ski mode: movement comes from SkiController only.
/// - Skis & poles are reparented to "back" parents in walk mode, and restored to
///   their original parents/poses in ski mode.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class WalkingController : MonoBehaviour
{
    // ----------------------------------------------------------------------
    // REFERENCES
    // ----------------------------------------------------------------------

    [Header("References")]
    [Tooltip("SkiController on the same player. Will be enabled/disabled when switching modes.")]
    [SerializeField] private SkiController skiController;

    [Tooltip("Optional SkiContact scripts for each ski (will be disabled in walk mode).")]
    [SerializeField] private SkiContact leftSkiContact;
    [SerializeField] private SkiContact rightSkiContact;

    [Tooltip("Optional PoleContact scripts for each pole (will be disabled in walk mode).")]
    [SerializeField] private PoleContact leftPoleContact;
    [SerializeField] private PoleContact rightPoleContact;

    [Header("Ski & Pole Transforms")]
    [Tooltip("Visual transform of the left ski.")]
    [SerializeField] private Transform leftSki;
    [Tooltip("Visual transform of the right ski.")]
    [SerializeField] private Transform rightSki;

    [Tooltip("Root/pivot transform for the left pole model (usually poleRoot from PoleContact).")]
    [SerializeField] private Transform leftPoleRoot;
    [Tooltip("Root/pivot transform for the right pole model.")]
    [SerializeField] private Transform rightPoleRoot;

    [Header("On-Back Parents")]
    [Tooltip("Empty GameObject where the left ski should live when 'on back'.")]
    [SerializeField] private Transform leftSkiBackParent;
    [Tooltip("Empty GameObject where the right ski should live when 'on back'.")]
    [SerializeField] private Transform rightSkiBackParent;

    [Tooltip("Empty GameObject where the left pole should live when 'on back'.")]
    [SerializeField] private Transform leftPoleBackParent;
    [Tooltip("Empty GameObject where the right pole should live when 'on back'.")]
    [SerializeField] private Transform rightPoleBackParent;

    [Tooltip("Optional camera transform used for camera-relative walking. If null, uses player forward/right.")]
    [SerializeField] private Transform cameraTransform;

    // ----------------------------------------------------------------------
    // INPUT
    // ----------------------------------------------------------------------

    [Header("Input (New Input System)")]
    [Tooltip("Hold this action for 'toggleHoldDuration' seconds to take off / put on skis (Player/Interact).")]
    [SerializeField] private float toggleHoldDuration = 1.0f;

    private InputSystem_Actions _input;
    private InputSystem_Actions.PlayerActions _player;

    // ----------------------------------------------------------------------
    // WALKING MOVEMENT SETTINGS
    // ----------------------------------------------------------------------

    [Header("Movement")]
    [Tooltip("Maximum walk speed (m/s).")]
    [SerializeField] private float walkSpeed = 3f;

    [Tooltip("Maximum run/sprint speed (m/s).")]
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("Horizontal acceleration (m/s^2).")]
    [SerializeField] private float acceleration = 25f;

    [Tooltip("Horizontal deceleration when no input (m/s^2).")]
    [SerializeField] private float deceleration = 30f;

    [Header("Ski Re-Equip")]
    [Tooltip("Layers to consider as ground when nudging player up for skis.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Minimum clearance above the ground when putting skis back on.")]
    [SerializeField] private float skiGroundClearance = 0.05f;

    [Tooltip("Ray start height above the player when nudging up for skis.")]
    [SerializeField] private float skiGroundRayHeight = 2f;

    [Tooltip("Maximum raycast distance down when nudging up for skis.")]
    [SerializeField] private float skiGroundRayDistance = 5f;

    // ----------------------------------------------------------------------
    // INTERNAL STATE
    // ----------------------------------------------------------------------

    private Rigidbody _rb;

    // Mode flag
    private bool _skisOn = true;

    // Toggle/hold state
    private bool _toggleHeld;
    private bool _toggleTriggeredThisHold;
    private float _toggleHoldStartTime;

    // Original parents / transforms for skis & poles
    private Transform _leftSkiOriginalParent;
    private Transform _rightSkiOriginalParent;
    private Transform _leftPoleOriginalParent;
    private Transform _rightPoleOriginalParent;

    private Vector3 _leftSkiOriginalLocalPos;
    private Vector3 _rightSkiOriginalLocalPos;
    private Vector3 _leftPoleOriginalLocalPos;
    private Vector3 _rightPoleOriginalLocalPos;

    private Quaternion _leftSkiOriginalLocalRot;
    private Quaternion _rightSkiOriginalLocalRot;
    private Quaternion _leftPoleOriginalLocalRot;
    private Quaternion _rightPoleOriginalLocalRot;

    // ----------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Setup input wrapper
        _input = new InputSystem_Actions();
        _player = _input.Player;

        // Cache original ski/pole parents & local transforms
        if (leftSki != null)
        {
            _leftSkiOriginalParent = leftSki.parent;
            _leftSkiOriginalLocalPos = leftSki.localPosition;
            _leftSkiOriginalLocalRot = leftSki.localRotation;
        }

        if (rightSki != null)
        {
            _rightSkiOriginalParent = rightSki.parent;
            _rightSkiOriginalLocalPos = rightSki.localPosition;
            _rightSkiOriginalLocalRot = rightSki.localRotation;
        }

        if (leftPoleRoot != null)
        {
            _leftPoleOriginalParent = leftPoleRoot.parent;
            _leftPoleOriginalLocalPos = leftPoleRoot.localPosition;
            _leftPoleOriginalLocalRot = leftPoleRoot.localRotation;
        }

        if (rightPoleRoot != null)
        {
            _rightPoleOriginalParent = rightPoleRoot.parent;
            _rightPoleOriginalLocalPos = rightPoleRoot.localPosition;
            _rightPoleOriginalLocalRot = rightPoleRoot.localRotation;
        }

        // Initial mode based on SkiController enabled state
        _skisOn = skiController == null || skiController.enabled;
        ApplyModeInitial();
    }

    private void OnEnable()
    {
        _input.Enable();
    }

    private void OnDisable()
    {
        _input.Disable();
    }

    private void Update()
    {
        HandleToggleInput();
    }

    private void FixedUpdate()
    {
        if (!_skisOn)
        {
            ApplyWalkMovement();
        }
    }

    // ----------------------------------------------------------------------
    // MODE TOGGLING
    // ----------------------------------------------------------------------

    private void HandleToggleInput()
    {
        // We use Player/Interact as the "take off / put on skis" hold action.
        bool interactPressed = _player.Interact.IsPressed();

        if (interactPressed)
        {
            if (!_toggleHeld)
            {
                _toggleHeld = true;
                _toggleTriggeredThisHold = false;
                _toggleHoldStartTime = Time.time;
            }

            if (!_toggleTriggeredThisHold &&
                Time.time - _toggleHoldStartTime >= toggleHoldDuration)
            {
                ToggleSkisMode();
                _toggleTriggeredThisHold = true;
            }
        }
        else
        {
            _toggleHeld = false;
            _toggleTriggeredThisHold = false;
        }
    }

    private void ToggleSkisMode()
    {
        if (_skisOn)
        {
            EnterWalkMode();
        }
        else
        {
            EnterSkiMode();
        }
    }

    private void ApplyModeInitial()
    {
        if (_skisOn)
        {
            NudgeUpForSkis();
            ForceSkiPose();
            EnableSkiSystems(true);
        }
        else
        {
            MoveSkisToBack();
            EnableSkiSystems(false);
        }
    }

    private void EnterWalkMode()
    {
        _skisOn = false;

        // Kill sliding when entering walk mode but preserve vertical motion.
        Vector3 vel = _rb.linearVelocity;
        vel.x = 0f;
        vel.z = 0f;
        _rb.linearVelocity = vel;

        MoveSkisToBack();
        EnableSkiSystems(false);
    }

    private void EnterSkiMode()
    {
        _skisOn = true;

        // When going back to skiing, ensure we're slightly above the snow
        // so the skis don't clip, then restore their idle pose.
        NudgeUpForSkis();
        ForceSkiPose();
        EnableSkiSystems(true);
    }

    private void EnableSkiSystems(bool enabled)
    {
        if (skiController != null)
            skiController.enabled = enabled;

        if (leftSkiContact != null)
            leftSkiContact.enabled = enabled;

        if (rightSkiContact != null)
            rightSkiContact.enabled = enabled;

        if (leftPoleContact != null)
            leftPoleContact.enabled = enabled;

        if (rightPoleContact != null)
            rightPoleContact.enabled = enabled;
    }

    // Gently nudge the player up so skis are not embedded in the ground.
    private void NudgeUpForSkis()
    {
        Vector3 origin = transform.position + Vector3.up * skiGroundRayHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, skiGroundRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            float desiredY = hit.point.y + skiGroundClearance;
            Vector3 pos = transform.position;
            if (pos.y < desiredY)
            {
                pos.y = desiredY;
                transform.position = pos;
            }
        }
        else
        {
            // Fallback: small upward nudge.
            transform.position += Vector3.up * skiGroundClearance;
        }
    }

    // ----------------------------------------------------------------------
    // SKI / POLE PLACEMENT
    // ----------------------------------------------------------------------

    private void MoveSkisToBack()
    {
        // Reparent skis and poles to their "on back" parents.
        if (leftSki != null && leftSkiBackParent != null)
        {
            leftSki.SetParent(leftSkiBackParent, worldPositionStays: false);
            leftSki.localPosition = Vector3.zero;
            leftSki.localRotation = Quaternion.identity;
        }

        if (rightSki != null && rightSkiBackParent != null)
        {
            rightSki.SetParent(rightSkiBackParent, worldPositionStays: false);
            rightSki.localPosition = Vector3.zero;
            rightSki.localRotation = Quaternion.identity;
        }

        if (leftPoleRoot != null && leftPoleBackParent != null)
        {
            leftPoleRoot.SetParent(leftPoleBackParent, worldPositionStays: false);
            leftPoleRoot.localPosition = Vector3.zero;
            leftPoleRoot.localRotation = Quaternion.identity;
        }

        if (rightPoleRoot != null && rightPoleBackParent != null)
        {
            rightPoleRoot.SetParent(rightPoleBackParent, worldPositionStays: false);
            rightPoleRoot.localPosition = Vector3.zero;
            rightPoleRoot.localRotation = Quaternion.identity;
        }
    }

    private void ForceSkiPose()
    {
        // Restore original parents and their cached idle local pose.
        if (leftSki != null && _leftSkiOriginalParent != null)
        {
            leftSki.SetParent(_leftSkiOriginalParent, worldPositionStays: false);
            leftSki.localPosition = _leftSkiOriginalLocalPos;
            leftSki.localRotation = _leftSkiOriginalLocalRot;
        }

        if (rightSki != null && _rightSkiOriginalParent != null)
        {
            rightSki.SetParent(_rightSkiOriginalParent, worldPositionStays: false);
            rightSki.localPosition = _rightSkiOriginalLocalPos;
            rightSki.localRotation = _rightSkiOriginalLocalRot;
        }

        if (leftPoleRoot != null && _leftPoleOriginalParent != null)
        {
            leftPoleRoot.SetParent(_leftPoleOriginalParent, worldPositionStays: false);
            leftPoleRoot.localPosition = _leftPoleOriginalLocalPos;
            leftPoleRoot.localRotation = _leftPoleOriginalLocalRot;
        }

        if (rightPoleRoot != null && _rightPoleOriginalParent != null)
        {
            rightPoleRoot.SetParent(_rightPoleOriginalParent, worldPositionStays: false);
            rightPoleRoot.localPosition = _rightPoleOriginalLocalPos;
            rightPoleRoot.localRotation = _rightPoleOriginalLocalRot;
        }
    }

    // ----------------------------------------------------------------------
    // WALK MOVEMENT
    // ----------------------------------------------------------------------

    private void ApplyWalkMovement()
    {
        Vector2 moveInput = _player.Move.ReadValue<Vector2>();
        bool sprintHeld = _player.Sprint.IsPressed();

        Vector3 forward, right;

        if (cameraTransform != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (camForward.sqrMagnitude < 0.0001f) camForward = transform.forward;
            camForward.Normalize();

            Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up);
            if (camRight.sqrMagnitude < 0.0001f) camRight = transform.right;
            camRight.Normalize();

            forward = camForward;
            right = camRight;
        }
        else
        {
            forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
        }

        Vector3 desiredDir = forward * moveInput.y + right * moveInput.x;
        float inputMagnitude = desiredDir.magnitude;
        if (inputMagnitude > 1f) desiredDir /= inputMagnitude;

        float targetSpeed = (sprintHeld ? runSpeed : walkSpeed) * inputMagnitude;

        Vector3 vel = _rb.linearVelocity;
        Vector3 velHorizontal = new Vector3(vel.x, 0f, vel.z);
        Vector3 targetVel = desiredDir * targetSpeed;
        Vector3 velDelta = targetVel - velHorizontal;

        float maxAccel = (targetSpeed > 0.01f) ? acceleration : deceleration;

        // Clamp acceleration to avoid crazy forces at low frame rates.
        Vector3 accelStep = velDelta / Time.fixedDeltaTime;
        float accelMag = accelStep.magnitude;
        if (accelMag > maxAccel)
        {
            accelStep = accelStep.normalized * maxAccel;
        }

        _rb.AddForce(new Vector3(accelStep.x, 0f, accelStep.z), ForceMode.Acceleration);

        // Rotate body toward movement direction (only when moving).
        if (desiredDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime);
        }
    }
}
