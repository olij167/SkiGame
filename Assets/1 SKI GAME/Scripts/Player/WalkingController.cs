using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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

    [Header("Jump")]
    [Tooltip("Upward jump speed when walking.")]
    [SerializeField] private float jumpForce = 5f;

    [Tooltip("Distance below the player to check for ground when walking.")]
    [SerializeField] private float groundCheckDistance = 0.5f;

    [Tooltip("Radius of the ground check sphere.")]
    [SerializeField] private float groundCheckRadius = 0.3f;

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

    /// <summary>
    /// True when the player is currently in ski mode (SkiController-driven).
    /// Exposed for VFX/audio systems that need to suppress ski-specific effects while walking.
    /// </summary>
    public bool SkisOn => _skisOn;

    /// <summary>
    /// True when the player is currently walking (WalkingController-driven).
    /// </summary>
    public bool IsWalkingMode => !_skisOn;

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

    // Grounded / jump state (walk mode only)
    private bool _isGrounded;
    private bool _jumpQueued;

    // --- Raw user input capture (ignores external auto-walk overrides) ---
    private Vector2 _lastUserMoveRaw;
    private bool _lastUserSprintRaw;

    private bool _runtimeSetupComplete;
    private readonly Dictionary<Collider, bool> _equipmentColliderEnabledStates = new Dictionary<Collider, bool>();

    private bool _walkPresentationKeepsSkisEquipped;

    public Vector2 LastUserMoveRaw => _lastUserMoveRaw;
    public float LastUserMoveMagnitude => _lastUserMoveRaw.magnitude;
    public bool LastUserSprintRaw => _lastUserSprintRaw;

    /// <summary>
    /// Returns true if the player is currently providing movement input (raw input, not external override).
    /// </summary>
    public bool IsUserTryingToMove(float threshold = 0.15f)
    {
        return _lastUserMoveRaw.sqrMagnitude >= (threshold * threshold);
    }

    [Header("Runtime Overrides")]
    [SerializeField] private bool controlsEnabled = true;

    private bool _externalMoveActive;
    private Vector2 _externalMove;
    private bool _externalSprint;
    private bool _externalWorldMoveActive;
    private Vector3 _externalWorldMoveDirection;
    private float _externalWorldMoveStrength = 1f;
    private float _externalWorldMoveFacingGateDot = -1f;

    public bool ControlsEnabled
    {
        get => controlsEnabled;
        set => controlsEnabled = value;
    }

    public bool ExternalMoveActive => _externalMoveActive;

    public void SetExternalMove(Vector2 move01, bool sprint = false)
    {
        _externalMoveActive = true;
        _externalWorldMoveActive = false;
        _externalMove = Vector2.ClampMagnitude(move01, 1f);
        _externalSprint = sprint;
    }

    public void SetExternalMoveToward(Vector3 worldDirection, float strength01 = 1f, bool sprint = false, float facingGateDot = 0.35f)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            ClearExternalMove();
            return;
        }

        _externalMoveActive = false;
        _externalWorldMoveActive = true;
        _externalWorldMoveDirection = planarDirection.normalized;
        _externalWorldMoveStrength = Mathf.Clamp01(strength01);
        _externalWorldMoveFacingGateDot = Mathf.Clamp(facingGateDot, -1f, 0.999f);
        _externalSprint = sprint;
    }

    public void ClearExternalMove()
    {
        _externalMoveActive = false;
        _externalWorldMoveActive = false;
        _externalMove = Vector2.zero;
        _externalWorldMoveDirection = Vector3.zero;
        _externalWorldMoveStrength = 1f;
        _externalWorldMoveFacingGateDot = -1f;
        _externalSprint = false;
    }

    public void ForceEnterWalkMode()
    {
        EnsureRuntimeSetup();

        if (_skisOn)
            EnterWalkMode();
        else
            RefreshEquipmentPresentation();
    }

    public void ForceEnterSkiMode()
    {
        EnsureRuntimeSetup();

        if (!_skisOn)
            EnterSkiMode();
        else
            RefreshEquipmentPresentation();
    }

    public void SetWalkPresentationKeepsSkisEquipped(bool keepSkisEquipped)
    {
        EnsureRuntimeSetup();
        _walkPresentationKeepsSkisEquipped = keepSkisEquipped;
        RefreshEquipmentPresentation();
    }

    public void GetMoveBasis(out Vector3 forward, out Vector3 right)
    {
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
    }

    // ----------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------------------

    private void Awake()
    {
        EnsureRuntimeSetup();
        ApplyModeInitial();
    }

    private void EnsureRuntimeSetup()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        if (_input == null)
        {
            _input = new InputSystem_Actions();
            _player = _input.Player;
        }

        if (_runtimeSetupComplete)
            return;

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

        _skisOn = skiController == null || skiController.enabled;
        _runtimeSetupComplete = true;
    }

    private void OnEnable()
    {
        EnsureRuntimeSetup();
        _input.Enable();
    }

    private void OnDisable()
    {
        if (_input != null)
            _input.Disable();
    }

    private void Update()
    {
        if (!controlsEnabled)
            return;

        HandleToggleInput();
        HandleJumpInput();
    }

    private void FixedUpdate()
    {
        if (!controlsEnabled)
            return;

        if (!_skisOn)
        {
            UpdateGroundedState();
            TryProcessJump();
            ApplyWalkMovement();
        }
    }

    // ----------------------------------------------------------------------
    // MODE TOGGLING
    // ----------------------------------------------------------------------

    private void HandleToggleInput()
    {
        // We use Player/Interact as the "take off / put on skis" hold action.
        bool interactPressed = _player.EquipSkis.IsPressed();

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
        _walkPresentationKeepsSkisEquipped = false;

        if (_skisOn)
            EnterWalkMode();
        else
            EnterSkiMode();
    }

    private void ApplyModeInitial()
    {
        if (_skisOn)
        {
            NudgeUpForSkis();
            EnableSkiSystems(true);
        }
        else
        {
            EnableSkiSystems(false);
        }

        RefreshEquipmentPresentation();
    }

    private void EnterWalkMode()
    {
        if (skiController != null)
            skiController.ResetStackStateSilently(snapUpright: true);

        if (_rb.useGravity != true) _rb.useGravity = true;
        _skisOn = false;

        Vector3 vel = _rb.linearVelocity;
        vel.x = 0f;
        vel.z = 0f;
        _rb.linearVelocity = vel;

        EnableSkiSystems(false);
        RefreshEquipmentPresentation();
    }

    private void EnterSkiMode()
    {
        _skisOn = true;
        _walkPresentationKeepsSkisEquipped = false;

        if (_rb.useGravity == true) _rb.useGravity = false;

        NudgeUpForSkis();

        if (skiController != null)
            skiController.SnapToGroundClearance(resetDownwardVelocity: true);

        EnableSkiSystems(true);
        RefreshEquipmentPresentation();
    }

    private void EnableSkiSystems(bool enabled)
    {
        SetEquipmentCollidersEnabled(enabled);

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

    public void RefreshEquipmentPresentation()
    {
        EnsureRuntimeSetup();

        if (_skisOn)
        {
            ForceSkiPose();
            return;
        }

        if (_walkPresentationKeepsSkisEquipped)
            ForceSkiPose();
        else
            MoveSkisToBack();
    }

    private void SetEquipmentCollidersEnabled(bool enabled)
    {
        SetCollidersEnabledForRoot(leftSki, enabled);
        SetCollidersEnabledForRoot(rightSki, enabled);
        SetCollidersEnabledForRoot(leftPoleRoot, enabled);
        SetCollidersEnabledForRoot(rightPoleRoot, enabled);
    }

    private void SetCollidersEnabledForRoot(Transform root, bool enabled)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
                continue;

            if (!_equipmentColliderEnabledStates.ContainsKey(c))
                _equipmentColliderEnabledStates[c] = c.enabled;

            if (enabled)
            {
                if (_equipmentColliderEnabledStates.TryGetValue(c, out bool wasEnabled))
                    c.enabled = wasEnabled;
                else
                    c.enabled = true;
            }
            else
            {
                c.enabled = false;
            }
        }
    }

    // Gently nudge the player up so skis are not embedded in the ground.
    // This version considers BOTH ski positions (much more robust on slopes and during transitions).
    private void NudgeUpForSkis()
    {
        // If skis are missing, fall back to body-based ray.
        bool hasAnySki = (leftSki != null) || (rightSki != null);

        float requiredRootY = transform.position.y;
        bool foundGround = false;

        // Helper to compute a root Y that keeps a given point (ski) above ground + clearance.
        void ConsiderPoint(Transform t)
        {
            if (t == null) return;

            Vector3 rayOrigin = t.position + Vector3.up * skiGroundRayHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, skiGroundRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                foundGround = true;

                // We want: t.position.y >= hit.point.y + clearance
                // RootY must shift by the same delta as the ski point.
                float skiToRootOffsetY = t.position.y - transform.position.y;
                float desiredRootYForThisSki = (hit.point.y + skiGroundClearance) - skiToRootOffsetY;

                if (desiredRootYForThisSki > requiredRootY)
                    requiredRootY = desiredRootYForThisSki;
            }
        }

        if (hasAnySki)
        {
            ConsiderPoint(leftSki);
            ConsiderPoint(rightSki);
        }

        // Fallback: body-based ray if no skis or no hits.
        if (!foundGround)
        {
            Vector3 origin = transform.position + Vector3.up * skiGroundRayHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, skiGroundRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                requiredRootY = Mathf.Max(requiredRootY, hit.point.y + skiGroundClearance);
                foundGround = true;
            }
        }

        if (foundGround)
        {
            Vector3 pos = _rb != null ? _rb.position : transform.position;
            if (pos.y < requiredRootY)
            {
                pos.y = requiredRootY;

                if (_rb != null)
                    _rb.MovePosition(pos);
                else
                    transform.position = pos;
            }
        }
        else
        {
            // Last resort: small upward nudge.
            Vector3 pos = _rb != null ? _rb.position : transform.position;
            pos += Vector3.up * skiGroundClearance;

            if (_rb != null)
                _rb.MovePosition(pos);
            else
                transform.position = pos;
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
        // Read raw user input first (used for cancel / intent detection).
        Vector2 rawInput = _player.Move.ReadValue<Vector2>();
        bool rawSprint = _player.Sprint.IsPressed();

        _lastUserMoveRaw = rawInput;
        _lastUserSprintRaw = rawSprint;

        bool usingExternalWorldMove = _externalWorldMoveActive &&
                                      _externalWorldMoveDirection.sqrMagnitude > 0.0001f;

        Vector3 desiredDir;
        float inputMagnitude;
        bool sprintHeld;

        if (usingExternalWorldMove)
        {
            desiredDir = _externalWorldMoveDirection;
            inputMagnitude = _externalWorldMoveStrength;

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (currentForward.sqrMagnitude > 0.0001f)
            {
                currentForward.Normalize();
                float facingDot = Vector3.Dot(currentForward, desiredDir);
                float facing01 = Mathf.InverseLerp(_externalWorldMoveFacingGateDot, 1f, facingDot);
                inputMagnitude *= Mathf.Clamp01(facing01);
            }

            sprintHeld = _externalSprint;
        }
        else
        {
            // External override wins for movement, but raw input remains available.
            Vector2 moveInput = _externalMoveActive ? _externalMove : rawInput;
            sprintHeld = _externalMoveActive ? _externalSprint : rawSprint;

            // Use the same basis we expose for auto-walk.
            GetMoveBasis(out Vector3 forward, out Vector3 right);

            desiredDir = forward * moveInput.y + right * moveInput.x;
            inputMagnitude = desiredDir.magnitude;
            if (inputMagnitude > 1f) desiredDir /= inputMagnitude;
        }

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

    // ----------------------------------------------------------------------
    // JUMP MOVEMENT
    // ----------------------------------------------------------------------
    private void HandleJumpInput()
    {
        // Only care about jump when in walk mode
        if (_skisOn)
            return;

        if (_player.Jump.WasPressedThisFrame())
        {
            _jumpQueued = true;
        }
    }

    private void UpdateGroundedState()
    {
        // Very simple sphere cast ground check under the player
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        _isGrounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void TryProcessJump()
    {
        if (!_jumpQueued)
            return;

        _jumpQueued = false;

        if (!_isGrounded)
            return;

        // Preserve horizontal velocity, override vertical with jumpForce
        Vector3 vel = _rb.linearVelocity;
        vel.y = jumpForce;
        _rb.linearVelocity = vel;
    }

}
