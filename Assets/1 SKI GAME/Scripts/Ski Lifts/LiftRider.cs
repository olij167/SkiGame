using SkiGame.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.Progression;
using SkiGame.UI;

[DefaultExecutionOrder(1100)]
public class LiftRider : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Dependencies")]
    public Rigidbody rb;
    public SkiController skiController;
    public WalkingController walkingController;

    [Header("Detach settings")]
    public float detachForwardImpulse = 3f;
    public float detachDownOffset = 0.2f;

    [Header("Lift detection")]
    [SerializeField] private LayerMask liftCarrierLayers = ~0;

    [Tooltip("Radius around the player used to search for nearby lift carriers.")]
    [SerializeField] private float liftDetectionRadius = 3.5f;

    [Tooltip("Optional point used for lift detection (e.g. hips/chest). If null, uses this transform.position.")]
    [SerializeField] private Transform liftDetectPoint;

    [Tooltip("If true, while the button is held we keep trying to attach every physics frame.")]
    [SerializeField] private bool holdToAttach = true;

    [Tooltip("If you tap slightly early, we still try to attach for this long (seconds).")]
    [SerializeField] private float attachInputBuffer = 0.20f;

    [Tooltip("How often (seconds) we attempt to attach while held/buffered.")]
    [SerializeField] private float attachAttemptInterval = 0.05f;

    [Header("Chair Follow")]
    [SerializeField] private bool hardFollowChairAttachPoint = true;
    [SerializeField] private bool useRigidbodyPositionForChairFollow = true;
    [SerializeField] private float maxChairAttachErrorBeforeSnap = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool logLiftAttachDebug;
    [SerializeField] private bool drawLiftAttachDebugGizmos;
    [SerializeField] private bool logAttachedChairFollowDebug;
    [SerializeField, Min(0.1f)] private float attachedChairFollowLogInterval = 1f;

    [Header("Input")]
    [Tooltip("Input action used to attach/detach from lifts (e.g. Player/Interact).")]
    [SerializeField] private InputActionReference liftInput;

    private LiftCarrier currentCarrier;
    private bool isAttached;
    private bool isChairMode; // true if attached to chair, false if T-bar
    private bool liftInputHeld;
    private bool _blockAttachUntilRelease;

    private float _attachBufferUntilTime;
    private float _nextAttachAttemptTime;
    private float _nextAttachedFollowLogTime;

    // Non-alloc overlap cache
    private readonly Collider[] _overlapHits = new Collider[32];

    private bool _prevKinematic;
    private bool _prevDetectCollisions;
    private bool _prevSkiEnabled;
    private bool _prevWalkEnabled;
    private bool _prevWalkControlsEnabled = true;

    private SkierLimbLineVisual _limbVisual;

    private LiftBoardGate _nearbyBoardGate;

    private LiftBoardGate _activeQueueGate;
    private LiftLine _authorizedBoardLine;
    private bool _queuedViaAutoBoarding;
    private bool _hadPreferredModeBeforeQueue;
    private bool _preferredSkiModeBeforeQueue = true;

    public LiftBoardGate NearbyBoardGate => _nearbyBoardGate;

    /// <summary>
    /// True while the rider is currently attached to a lift carrier (chair or T-bar).
    /// Exposed for VFX/audio gating.
    /// </summary>
    public bool IsAttached => isAttached;

    /// <summary>
    /// True if the currently attached carrier is a chair.
    /// </summary>
    public bool IsChairMode => isAttached && isChairMode;

    /// <summary>
    /// True if the currently attached carrier is a T-bar.
    /// </summary>
    public bool IsTBarMode => isAttached && !isChairMode;
    public LiftLine CurrentLiftLine => currentCarrier != null ? currentCarrier.line : null;

    public LiftCarrier CurrentCarrier => currentCarrier;
    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        CacheRidePoseReferences();
    }

    private void OnEnable()
    {
        if (!IsNpcRider())
            WorldInteractionPromptRegistry.Register(this);

        if (IsNpcRider())
            return;

        if (liftInput != null && liftInput.action != null)
        {
            liftInput.action.started += OnLiftInput;
            liftInput.action.performed += OnLiftInput;
            liftInput.action.canceled += OnLiftInput;
            liftInput.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (!IsNpcRider())
            WorldInteractionPromptRegistry.Unregister(this);

        if (IsNpcRider())
            return;

        if (liftInput != null && liftInput.action != null)
        {
            liftInput.action.started -= OnLiftInput;
            liftInput.action.performed -= OnLiftInput;
            liftInput.action.canceled -= OnLiftInput;
            liftInput.action.Disable();
        }
    }

    public void SetNearbyBoardGate(LiftBoardGate gate)
    {
        _nearbyBoardGate = gate;
    }

    /// <summary>
    /// Callback from the Input System for the configured lift input action.
    /// This maps the action phases onto the existing SetLiftInput logic.
    /// </summary>
    private void OnLiftInput(InputAction.CallbackContext context)
    {
        bool isPressed = context.ReadValueAsButton();
        bool wasPressedThisFrame = context.started;

        if (wasPressedThisFrame)
        {
            // Allow "press slightly early" attachment
            _attachBufferUntilTime = Time.time + attachInputBuffer;
        }

        SetLiftInput(isPressed, wasPressedThisFrame);
    }

    /// <summary>
    /// Core logic for how lift input affects attachment/detachment.
    /// Now usually driven by OnLiftInput, but you can still call it externally if needed.
    /// </summary>
    public void SetLiftInput(bool isPressed, bool wasPressedThisFrame)
    {
        liftInputHeld = isPressed;

        // When the player releases the button, allow attaching again.
        if (!liftInputHeld)
            _blockAttachUntilRelease = false;

        // Chair logic: toggle detach on press
        if (isAttached && isChairMode)
        {
            if (wasPressedThisFrame)
            {
                RequestDetach();

                // Prevent immediately re-attaching to the same chair while the button remains held.
                _blockAttachUntilRelease = true;
            }

            return; // important: don’t fall through into attach logic while attached
        }

        // T-bar logic: must hold; release detaches
        if (isAttached && !isChairMode)
        {
            if (!liftInputHeld)
                RequestDetach();

            return;
        }

        // Not attached: allow attaching, unless we're blocking until release (post-chair detach)
        if (!isAttached && !_blockAttachUntilRelease)
        {
            if (wasPressedThisFrame)
            {
                if (ShouldUseGateBoarding())
                {
                    _nearbyBoardGate.TryJoinQueue(this);
                    return;
                }

                TryAttachToNearbyCarrier();
            }
        }
    }

    private void FixedUpdate()
    {
        // Attachment assist: while held or buffered, keep attempting to attach.
        if (!isAttached)
        {
            if (_activeQueueGate != null && _queuedViaAutoBoarding)
                UpdateAutoQueueMovement();

            if (!_blockAttachUntilRelease)
            {
                bool shouldTryAttach =
                    (holdToAttach && liftInputHeld) ||
                    (Time.time <= _attachBufferUntilTime);

                if (ShouldUseGateBoarding())
                {
                    if (shouldTryAttach && !_nearbyBoardGate.IsQueued(this))
                        _nearbyBoardGate.TryJoinQueue(this);
                }
                else if (shouldTryAttach && Time.time >= _nextAttachAttemptTime)
                {
                    _nextAttachAttemptTime = Time.time + attachAttemptInterval;
                    TryAttachToNearbyCarrier();
                }
            }
        }

        // For T-bar, we don't parent; we apply a pull along cable while attached
        if (isAttached && !isChairMode && currentCarrier != null)
        {
            Vector3 forward = currentCarrier.transform.forward;
            // Pull uphill along cable, but let SkiController do terrain work
            Vector3 targetVel = forward * (currentCarrier.line.bandSpeed * 0.9f);
            Vector3 vel = rb.linearVelocity;
            Vector3 velChange = targetVel - vel;

            // Only project along cable direction on XZ to avoid fighting vertical
            velChange = Vector3.ProjectOnPlane(velChange, Vector3.up);
            rb.AddForce(velChange, ForceMode.Acceleration);
        }

        if (logAttachedChairFollowDebug && isAttached && isChairMode && currentCarrier != null && Time.time >= _nextAttachedFollowLogTime)
        {
            _nextAttachedFollowLogTime = Time.time + attachedChairFollowLogInterval;
            LogLiftAttachState("Attached chair follow tick");
        }
    }

    private void LateUpdate()
    {
        HardFollowChairAttachPointIfNeeded();
    }

    private void HardFollowChairAttachPointIfNeeded()
    {
        if (!hardFollowChairAttachPoint || !isAttached || !isChairMode || currentCarrier == null || currentCarrier.attachPoint == null)
            return;

        Transform attach = currentCarrier.attachPoint;
        float attachError = Vector3.Distance(transform.position, attach.position);
        bool shouldLogError = logLiftAttachDebug && attachError > maxChairAttachErrorBeforeSnap;

        if (rb != null && rb.isKinematic && useRigidbodyPositionForChairFollow)
        {
            rb.position = attach.position;
            rb.rotation = attach.rotation;
        }
        else
        {
            transform.SetPositionAndRotation(attach.position, attach.rotation);
        }

        if (transform.parent == attach)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        if (shouldLogError)
            Debug.Log($"[LiftRider] {name}: Chair attach error {attachError:0.000} exceeded {maxChairAttachErrorBeforeSnap:0.000}; hard-snapped to attach point.", this);
    }

    /// <summary>
    /// Searches for a LiftCarrier within liftDetectionRadius and attaches if possible.
    /// This is only called when the lift input is pressed and the rider is not already attached.
    /// </summary>
    private void TryAttachToNearbyCarrier()
    {
        Vector3 center = liftDetectPoint ? liftDetectPoint.position : transform.position;

        // Non-alloc overlap (fast, no GC). Explicitly include triggers.
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            liftDetectionRadius,
            _overlapHits,
            liftCarrierLayers,
            QueryTriggerInteraction.Collide
        );

        if (hitCount <= 0)
            return;

        LiftCarrier best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _overlapHits[i];
            if (!c) continue;

            var carrier = c.GetComponentInParent<LiftCarrier>();
            if (!carrier) continue;

            if (!carrier.CanAttach(this))
                continue;

            // Score: prefer closer + in front (reduces attaching to carriers behind you)
            Vector3 toAttach = carrier.attachPoint.position - center;
            float dist = toAttach.magnitude;
            if (dist < 0.001f) dist = 0.001f;

            Vector3 toAttachDir = toAttach / dist;
            float facing = Vector3.Dot(transform.forward, toAttachDir); // -1..1
            float score = (facing * 2f) - dist; // tune weights as needed

            if (score > bestScore)
            {
                bestScore = score;
                best = carrier;
            }
        }

        if (best != null)
        {
            bool attached = best.AttachRider(this);

            // Clear buffer only if we actually attached
            if (attached)
                _attachBufferUntilTime = 0f;
        }
    }

    private void CacheRidePoseReferences()
    {
        if (_limbVisual == null)
            _limbVisual = GetComponentInChildren<SkierLimbLineVisual>(true);

        if (walkingController == null)
            walkingController = GetComponent<WalkingController>();

        if (skiController == null)
            skiController = GetComponent<SkiController>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    private void ApplyChairLiftSeatedPose()
    {
        CacheRidePoseReferences();

        if (walkingController != null)
        {
            _prevWalkControlsEnabled = walkingController.ControlsEnabled;

            // Use walk-mode limb anchors for the seated pose, but keep the ski equipment visible.
            walkingController.enabled = true;
            walkingController.SetWalkPresentationKeepsSkisEquipped(true);
            walkingController.ForceEnterWalkMode();
            walkingController.ClearExternalMove();
            walkingController.ControlsEnabled = false;
            walkingController.SetRiderPoseActive(true);
        }

        if (_limbVisual != null)
            _limbVisual.SetRiderPoseOverride(SkierLimbLineVisual.RiderPoseMode.LiftSeated);
    }

    private void ClearChairLiftSeatedPose()
    {
        CacheRidePoseReferences();

        if (_limbVisual != null)
            _limbVisual.ClearRiderPoseOverride();

        if (walkingController != null)
        {
            walkingController.SetRiderPoseActive(false);
            walkingController.ControlsEnabled = _prevWalkControlsEnabled;
            walkingController.SetWalkPresentationKeepsSkisEquipped(false);
        }
    }

    public void OnAttachedToCarrier(LiftCarrier carrier)
    {
        Vector3 beforeParentPosition = transform.position;
        currentCarrier = carrier;
        isAttached = true;
        isChairMode = (carrier.mode == LiftCarrierMode.Chair);
        GameAudio.PlayWorld(isChairMode ? GameAudioCueId.LiftAttachChair : GameAudioCueId.LiftAttachTBar, transform.position);

        if (walkingController != null)
            walkingController.ClearExternalMove();

        AutoSkiApproachDriver skiApproach = GetComponent<AutoSkiApproachDriver>();
        if (skiApproach != null)
            skiApproach.StopApproach(this);

        if (walkingController != null)
        {
            walkingController.SetWalkPresentationKeepsSkisEquipped(false);
            walkingController.RefreshEquipmentPresentation();
        }

        _activeQueueGate = null;
        _queuedViaAutoBoarding = false;

        string liftId = null;
        if (carrier != null && carrier.line != null)
            liftId = carrier.line.gameObject.name; // stable enough if LiftLine names are unique
        else if (carrier != null)
            liftId = carrier.gameObject.name;

        if (IsPlayerControlled())
            RegisterLiftUsed(liftId);

        if (isChairMode)
        {
            _prevWalkControlsEnabled = walkingController != null
    ? walkingController.ControlsEnabled
    : true;

            // Cache previous states so detach restores correctly
            if (_hadPreferredModeBeforeQueue)
            {
                _prevSkiEnabled = _preferredSkiModeBeforeQueue;
                _prevWalkEnabled = !_preferredSkiModeBeforeQueue;
                _hadPreferredModeBeforeQueue = false;
            }
            else
            {
                _prevSkiEnabled = (skiController != null && skiController.enabled);
                _prevWalkEnabled = (walkingController != null && walkingController.enabled);
            }

            if (rb)
            {
                _prevKinematic = rb.isKinematic;
                _prevDetectCollisions = rb.detectCollisions;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            ApplyChairLiftSeatedPose();

            transform.SetParent(carrier.attachPoint, true);
            if (logLiftAttachDebug)
                Debug.Log($"[LiftRider] {name}: Chair attach before local zero carrier='{carrier.name}' attachPoint='{(carrier.attachPoint != null ? carrier.attachPoint.name : "none")}' attachWorld={(carrier.attachPoint != null ? carrier.attachPoint.position.ToString("F3") : "none")} riderBefore={beforeParentPosition.ToString("F3")}", this);

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            LogLiftAttachState("Chair attach after local zero");

            if (skiController) skiController.enabled = false;
            if (walkingController) walkingController.enabled = false;
        }
        else
        {
            // T-bar: keep physics, just gently snap horizontally behind bar
            //rb.isKinematic = false;
            transform.SetParent(null);

            if (walkingController) walkingController.enabled = false;
            if (skiController) skiController.enabled = true; // we assume we're skiing

            // Optionally align facing direction to cable
            Vector3 fwd = carrier.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }
    }

    public void OnDetachedFromCarrier(LiftCarrier carrier)
    {
        if (carrier != currentCarrier) return;
        GameAudio.PlayWorld(GameAudioCueId.LiftDetach, transform.position);
        // Common detach behaviour
        transform.SetParent(null);

        if (rb)
        {
            rb.isKinematic = _prevKinematic;
            rb.detectCollisions = _prevDetectCollisions;

            rb.angularVelocity = Vector3.zero;

            // Give a predictable launch aligned to the cable direction (horizontal only)
            Vector3 fwd = carrier.transform.forward;
            fwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;

            // Preserve any existing horizontal motion lightly, but ensure we don't "dead drop"
            Vector3 horiz = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            rb.linearVelocity = horiz + fwd * detachForwardImpulse;
        }

        if (isChairMode)
            ClearChairLiftSeatedPose();

        // Restore prior controller state
        if (IsPlayerControlled())
        {
            if (walkingController)
            {
                walkingController.enabled = true;
                walkingController.ControlsEnabled = _prevWalkControlsEnabled;
                walkingController.SetWalkPresentationKeepsSkisEquipped(false);
                walkingController.SetRiderPoseActive(false);
                walkingController.ForceEnterSkiMode();
            }

            if (skiController)
                skiController.enabled = true;

            if (walkingController)
                walkingController.enabled = true;
        }
        else
        {
            if (walkingController)
            {
                walkingController.enabled = true;
                walkingController.ControlsEnabled = _prevWalkControlsEnabled;
                walkingController.SetWalkPresentationKeepsSkisEquipped(false);
                walkingController.SetRiderPoseActive(false);

                if (_prevSkiEnabled)
                    walkingController.ForceEnterSkiMode();
                else
                    walkingController.ForceEnterWalkMode();
            }

            if (skiController)
                skiController.enabled = _prevSkiEnabled;

            if (walkingController)
                walkingController.enabled = _prevWalkEnabled;
        }

        // Nudge down a bit so we don't hover
        transform.position += Vector3.down * detachDownOffset;

        isAttached = false;
        currentCarrier = null;

    }

    private void CancelQueueAutoBoarding()
    {
        LiftBoardGate gate = _activeQueueGate;
        if (gate == null)
            return;

        gate.LeaveQueue(this);

        AutoSkiApproachDriver skiApproach = GetComponent<AutoSkiApproachDriver>();
        if (skiApproach != null)
            skiApproach.StopApproach(this);

        if (walkingController != null)
        {
            walkingController.ClearExternalMove();
            walkingController.SetWalkPresentationKeepsSkisEquipped(false);
            walkingController.ForceEnterSkiMode();
        }

        _queuedViaAutoBoarding = false;
    }

    private void RequestDetach()
    {
        if (currentCarrier != null)
        {
            currentCarrier.DetachRider(this);
        }
    }

    private static void RegisterLiftUsed(string liftId)
    {
        var mgr = PlayerStatsManager.Instance;
        if (mgr == null) return;

        if (mgr.Profile == null)
            mgr.Load();

        var p = mgr.Profile;
        if (p == null) return;

        p.lifetime.totalLiftsUsed++;
        p.session.liftsUsed++;

        if (!string.IsNullOrEmpty(liftId))
        {
            p.IncrementLiftRideCount(liftId, session: true);
            p.IncrementLiftRideCount(liftId, session: false);
        }
    }

    private bool ShouldSuppressDirectCarrierPrompt(LiftCarrier carrier)
    {
        if (carrier == null)
            return false;

        if (!IsPlayerControlled())
            return false;

        LiftLine line = carrier.line;
        if (line == null)
            return false;

        // If this lift line now requires gate-based boarding for players,
        // never show the legacy direct-to-carrier prompt.
        return line.HasPlayerBoardingGates;
    }

    private LiftCarrier FindBestPromptCarrier()
    {
        LiftCarrier carrier = FindBestNearbyCarrierForPrompt();
        if (ShouldSuppressDirectCarrierPrompt(carrier))
            return null;

        return carrier;
    }

    private LiftCarrier FindBestNearbyCarrierForPrompt()
    {
        Vector3 center = liftDetectPoint ? liftDetectPoint.position : transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            liftDetectionRadius,
            _overlapHits,
            liftCarrierLayers,
            QueryTriggerInteraction.Collide
        );

        if (hitCount <= 0)
            return null;

        LiftCarrier best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _overlapHits[i];
            if (!c) continue;

            var carrier = c.GetComponentInParent<LiftCarrier>();
            if (!carrier) continue;
            if (!carrier.CanAttach(this)) continue;

            Vector3 toAttach = carrier.attachPoint.position - center;
            float dist = toAttach.magnitude;
            if (dist < 0.001f) dist = 0.001f;

            Vector3 toAttachDir = toAttach / dist;
            float facing = Vector3.Dot(transform.forward, toAttachDir);
            float score = (facing * 2f) - dist;

            if (score > bestScore)
            {
                bestScore = score;
                best = carrier;
            }
        }

        return best;
    }

    public bool IsNpcRider()
    {
        if (GetComponentInParent<NpcSkierBrain>() != null)
            return true;

        NpcIdentity identity = GetComponentInParent<NpcIdentity>();
        if (identity != null && !IsLayerNamed(gameObject, "Player") && !IsLayerNamed(transform.root.gameObject, "Player"))
            return true;

        if (IsLayerNamed(gameObject, "NPC") || IsLayerNamed(transform.root.gameObject, "NPC"))
            return true;

        return HasTagSafe(gameObject, "NPC") || HasTagSafe(transform.root.gameObject, "NPC");
    }

    public bool IsPlayerControlled()
    {
        if (IsNpcRider())
            return false;

        if (IsLayerNamed(gameObject, "Player") || IsLayerNamed(transform.root.gameObject, "Player"))
            return true;

        return HasTagSafe(gameObject, "Player") || HasTagSafe(transform.root.gameObject, "Player");
    }

    private bool IsPlayerPromptSource()
    {
        if (IsNpcRider())
            return false;

        return IsPlayerControlled();
    }

    public bool IsPromptAvailable
    {
        get
        {
            if (!IsPlayerPromptSource())
                return false;

            if (isAttached)
                return true;

            if (_nearbyBoardGate != null && _nearbyBoardGate.ShouldInterceptPlayerAttach(this))
                return true;

            return FindBestPromptCarrier() != null;
        }
    }

    public string PromptActionText => "Interact";

    public string PromptDescriptionText
    {
        get
        {
            if (isAttached)
                return isChairMode ? "Leave Lift" : "Release T-Bar";

            if (_nearbyBoardGate != null && _nearbyBoardGate.ShouldInterceptPlayerAttach(this))
            {
                if (_nearbyBoardGate.IsQueued(this))
                {
                    int idx = _nearbyBoardGate.GetQueueIndex(this);
                    return idx <= 0 ? "Boarding Lift" : $"Joining Queue ({idx + 1})";
                }

                return "Join Lift Queue";
            }

            var carrier = FindBestPromptCarrier();
            if (carrier == null)
                return string.Empty;

            var line = carrier.line;
            if (line != null)
            {
                var passMgr = SkiPassManager.Instance;

                if (passMgr != null && !passMgr.CanUseLift(line))
                {
                    string requiredName = line.GetRequiredPassDisplayName();
                    return $"Upgrade your pass to {requiredName} at the kiosk to use this ski lift";
                }

                if (!string.IsNullOrWhiteSpace(line.name))
                    return $"Board {line.name}";
            }

            return carrier.mode == LiftCarrierMode.Chair ? "Board Lift" : "Grab T-Bar";
        }
    }

    private bool ShouldUseGateBoarding()
    {
        return IsPlayerControlled() &&
               _nearbyBoardGate != null &&
               _nearbyBoardGate.ShouldInterceptPlayerAttach(this);
    }

    public bool RequiresBoardAuthorization(LiftLine line)
    {
        return IsPlayerControlled() &&
               line != null &&
               line.HasPlayerBoardingGates;
    }

    public bool HasBoardAuthorizationFor(LiftLine line)
    {
        return line != null && _authorizedBoardLine == line;
    }

    public void GrantBoardAuthorization(LiftBoardGate gate)
    {
        _authorizedBoardLine = gate != null ? gate.line : null;
    }

    public void ConsumeBoardAuthorization(LiftLine line)
    {
        if (_authorizedBoardLine == line)
            _authorizedBoardLine = null;
    }

    public void OnJoinedLiftQueue(LiftBoardGate gate)
    {
        _activeQueueGate = gate;
        GameAudio.PlayWorld(GameAudioCueId.LiftQueueJoin, gate != null ? gate.transform.position : transform.position, 0.85f);

        if (!IsPlayerControlled())
            return;

        _queuedViaAutoBoarding = true;

        if (walkingController != null)
        {
            _preferredSkiModeBeforeQueue = walkingController.SkisOn;
            _hadPreferredModeBeforeQueue = true;
            walkingController.ForceEnterSkiMode();
            walkingController.ClearExternalMove();
        }
        else if (skiController != null)
        {
            _preferredSkiModeBeforeQueue = skiController.enabled;
            _hadPreferredModeBeforeQueue = true;
            skiController.enabled = true;
        }
    }

    public void OnLeftLiftQueue(LiftBoardGate gate)
    {
        if (_activeQueueGate == gate)
            _activeQueueGate = null;

        GameAudio.PlayWorld(GameAudioCueId.LiftQueueLeave, gate != null ? gate.transform.position : transform.position, 0.8f);

        AutoSkiApproachDriver skiApproach = GetComponent<AutoSkiApproachDriver>();
        if (skiApproach != null)
            skiApproach.StopApproach(this);

        if (walkingController != null)
        {
            walkingController.ClearExternalMove();
            walkingController.SetWalkPresentationKeepsSkisEquipped(false);

            if (!isAttached)
                walkingController.ForceEnterSkiMode();
        }

        _queuedViaAutoBoarding = false;

        if (gate == null || gate.line == null || _authorizedBoardLine == gate.line)
            _authorizedBoardLine = null;
    }

    private void UpdateAutoQueueMovement()
    {
        if (_activeQueueGate == null || skiController == null || currentCarrier != null)
            return;

        AutoSkiApproachDriver skiApproach = GetComponent<AutoSkiApproachDriver>();
        if (skiApproach == null)
            skiApproach = gameObject.AddComponent<AutoSkiApproachDriver>();

        if (skiApproach.CancelRequested)
        {
            CancelQueueAutoBoarding();
            return;
        }

        int queueIndex = _activeQueueGate.GetQueueIndex(this);
        if (queueIndex < 0)
        {
            skiApproach.StopApproach(this);
            return;
        }

        Vector3 target = _activeQueueGate.GetQueueTargetPosition(this);
        Vector3 to = target - skiController.transform.position;
        to.y = 0f;

        float distance = to.magnitude;
        float arriveDistance = 0.65f;

        if (distance <= arriveDistance)
        {
            skiApproach.StopApproach(this);
            return;
        }

        walkingController?.ForceEnterSkiMode();
        skiApproach.BeginApproach(this, target, 0.82f, arriveDistance, allowPoles: distance > 2.5f);
        skiApproach.UpdateApproachTarget(target, 0.82f, arriveDistance, allowPoles: distance > 2.5f);
    }

    public bool PromptUsesHold => false;
    public float PromptHoldDuration => 0f;

    public Vector3 PromptWorldPosition
    {
        get
        {
            if (_nearbyBoardGate != null && _nearbyBoardGate.boardingPoint != null)
                return _nearbyBoardGate.boardingPoint.position;

            if (currentCarrier != null)
                return currentCarrier.attachPoint != null ? currentCarrier.attachPoint.position : currentCarrier.transform.position;

            var carrier = FindBestPromptCarrier();
            if (carrier != null)
                return carrier.attachPoint != null ? carrier.attachPoint.position : carrier.transform.position;

            return transform.position;
        }
    }

    public int PromptPriority => isAttached ? 100 : 60;

    [ContextMenu("Print Lift Attach Debug State")]
    private void PrintLiftAttachDebugState()
    {
        LogLiftAttachState("Context menu");
    }

    [ContextMenu("Print Rider Classification Debug State")]
    private void PrintRiderClassificationDebugState()
    {
        Debug.Log(
            $"[LiftRider] {name}: rider classification\n" +
            $"isNpc={IsNpcRider()} isPlayerControlled={IsPlayerControlled()} promptSource={IsPlayerPromptSource()}\n" +
            $"objectLayer={LayerMask.LayerToName(gameObject.layer)} rootLayer={LayerMask.LayerToName(transform.root.gameObject.layer)} tag={tag} rootTag={transform.root.tag}\n" +
            $"hasNpcBrain={GetComponentInParent<NpcSkierBrain>() != null} hasNpcIdentity={GetComponentInParent<NpcIdentity>() != null} liftInput={(liftInput != null ? liftInput.name : "none")}",
            this);
    }

    private void LogLiftAttachState(string reason)
    {
        if (!logLiftAttachDebug && reason != "Context menu")
            return;

        Transform attach = currentCarrier != null ? currentCarrier.attachPoint : null;
        float attachError = attach != null ? Vector3.Distance(transform.position, attach.position) : -1f;
        Debug.Log(
            $"[LiftRider] {name}: {reason}\n" +
            $"attached={isAttached} chair={isChairMode} carrier={(currentCarrier != null ? currentCarrier.name : "none")} attachPoint={(attach != null ? attach.name : "none")} parent={(transform.parent != null ? transform.parent.name : "none")}\n" +
            $"isNpc={IsNpcRider()} isPlayerControlled={IsPlayerControlled()} nearbyGate={(_nearbyBoardGate != null ? _nearbyBoardGate.name : "none")} activeQueueGate={(_activeQueueGate != null ? _activeQueueGate.name : "none")} authorizedLine={(_authorizedBoardLine != null ? _authorizedBoardLine.name : "none")}\n" +
            $"riderWorld={transform.position.ToString("F3")} riderLocal={transform.localPosition.ToString("F3")} attachWorld={(attach != null ? attach.position.ToString("F3") : "none")} attachError={attachError:0.000}\n" +
            $"rbKinematic={(rb != null && rb.isKinematic)} rbDetectCollisions={(rb != null && rb.detectCollisions)}",
            this);
    }

    private static bool IsLayerNamed(GameObject go, string layerName)
    {
        if (go == null || string.IsNullOrWhiteSpace(layerName))
            return false;

        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 && go.layer == layer;
    }

    private static bool HasTagSafe(GameObject go, string tagName)
    {
        if (go == null || string.IsNullOrWhiteSpace(tagName))
            return false;

        try
        {
            return go.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualise the detection radius in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, liftDetectionRadius);

        if (drawLiftAttachDebugGizmos && currentCarrier != null && currentCarrier.attachPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(currentCarrier.attachPoint.position, 0.2f);
            Gizmos.DrawLine(transform.position, currentCarrier.attachPoint.position);
        }
    }
#endif
}
