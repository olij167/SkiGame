using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First-pass procedural limb visualiser for skiers.
/// - Draws 3-point line-rendered arms and legs.
/// - Uses skiing equipment as limb endpoints while skiing.
/// - Switches to a simple procedural walk cycle while in walking mode.
/// - Can optionally apply a small spring-smoothed suspension offset to a visual root.
/// This is intentionally visual-only and should not drive locomotion physics.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1200)]
public sealed class SkierLimbLineVisual : MonoBehaviour
{
    private enum LimbKind
    {
        Arm,
        Leg
    }

    [Header("Core References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private Rigidbody targetRigidbody;

    [Tooltip("Optional visual root to offset slightly for ski suspension. Leave null to disable body suspension.")]
    [SerializeField] private Transform suspensionVisualRoot;

    [Header("Optional Explicit Anchors")]
    [SerializeField] private Transform leftShoulderAnchor;
    [SerializeField] private Transform rightShoulderAnchor;
    [SerializeField] private Transform leftHipAnchor;
    [SerializeField] private Transform rightHipAnchor;

    [Tooltip("Optional endpoint near the left hand / pole grip. Falls back to PoleContact.PoleRoot.")]
    [SerializeField] private Transform leftPoleGripAnchor;
    [Tooltip("Optional endpoint near the right hand / pole grip. Falls back to PoleContact.PoleRoot.")]
    [SerializeField] private Transform rightPoleGripAnchor;
    [Tooltip("Optional endpoint near the left ski binding / boot position. Falls back to the left ski transform.")]
    [SerializeField] private Transform leftSkiBindingAnchor;
    [Tooltip("Optional endpoint near the right ski binding / boot position. Falls back to the right ski transform.")]
    [SerializeField] private Transform rightSkiBindingAnchor;

    [Header("Fallback Body Anchor Offsets")]
    [SerializeField] private Vector3 leftShoulderLocalOffset = new Vector3(-0.22f, 0.18f, 0.02f);
    [SerializeField] private Vector3 rightShoulderLocalOffset = new Vector3(0.22f, 0.18f, 0.02f);
    [SerializeField] private Vector3 leftHipLocalOffset = new Vector3(-0.16f, -0.26f, 0.02f);
    [SerializeField] private Vector3 rightHipLocalOffset = new Vector3(0.16f, -0.26f, 0.02f);

    [Header("Walk Cycle Offsets")]
    [SerializeField] private float walkFootSpacing = 0.18f;
    [SerializeField] private float walkFootForwardStride = 0.20f;
    [SerializeField] private float walkFootLift = 0.07f;
    [SerializeField] private float walkHandSpacing = 0.24f;
    [SerializeField] private float walkHandForwardStride = 0.16f;
    [SerializeField] private float walkHandLift = 0.05f;
    [SerializeField] private float walkCycleSpeed = 5.5f;
    [SerializeField] private float walkIdleReturnSpeed = 10f;
    [SerializeField] private float walkMoveThreshold = 0.08f;
    [SerializeField] private float walkPlanarSpeedForFullCycle = 2.5f;

    [Header("Skiing Bend")]
    [SerializeField] private float armBaseBend = 0.10f;
    [SerializeField] private float armTuckBend = 0.10f;
    [SerializeField] private float armAirBend = 0.06f;
    [SerializeField] private float armPolePhaseInfluence = 0.08f;
    [SerializeField] private float legBaseBend = 0.12f;
    [SerializeField] private float legTuckBend = 0.16f;
    [SerializeField] private float legInputBend = 0.08f;
    [SerializeField] private float legAirStraighten = 0.12f;
    [SerializeField] private float limbOutwardBias = 0.05f;
    [SerializeField] private float movementForwardBias = 0.04f;

    [Header("Walk Bend")]
    [SerializeField] private float walkArmBend = 0.10f;
    [SerializeField] private float walkLegBend = 0.14f;

    [Header("Suspension")]
    [SerializeField] private bool enableSuspension = true;
    [SerializeField] private float suspensionMaxOffset = 0.14f;
    [SerializeField] private float suspensionSpeed = 10f;
    [SerializeField] private float suspensionCompressionFromTuck = 0.05f;
    [SerializeField] private float suspensionCompressionFromTerrain = 0.08f;

    [Header("Feet")]
    [SerializeField] private Transform leftFootVisual;
    [SerializeField] private Transform rightFootVisual;

    [Tooltip("Author-time ski foot anchors. Usually child anchors under the left/right ski roots.")]
    [SerializeField] private Transform leftSkiFootAnchor;
    [SerializeField] private Transform rightSkiFootAnchor;

    [Tooltip("Author-time walk foot anchors. Usually siblings of the skis under the player root.")]
    [SerializeField] private Transform leftWalkFootTarget;
    [SerializeField] private Transform rightWalkFootTarget;

    [Header("Hands")]
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;

    [Tooltip("Author-time ski hand anchors. Usually near the pole grip / hand position while skiing.")]
    [SerializeField] private Transform leftSkiHandAnchor;
    [SerializeField] private Transform rightSkiHandAnchor;

    [Tooltip("Author-time walk hand anchors. Usually siblings of the skis / walk feet under the player root.")]
    [SerializeField] private Transform leftWalkHandTarget;
    [SerializeField] private Transform rightWalkHandTarget;

    [Header("Walk Body Height")]
    [SerializeField] private bool keepBodyHeightRelativeToFeetInWalkMode = true;
    [SerializeField] private float walkBodyHeightFollowSpeed = 14f;

    [Header("Stack Limb Ragdoll")]
    [SerializeField] private bool enableStackLimbRagdoll = true;
    [SerializeField] private float stackBlendInSpeed = 16f;
    [SerializeField] private float stackBlendOutSpeed = 8f;
    [SerializeField] private float stackArmRootLag = 0.08f;
    [SerializeField] private float stackArmEndLag = 0.14f;
    [SerializeField] private float stackLegRootLag = 0.04f;
    [SerializeField] private float stackLegEndLag = 0.08f;
    [SerializeField] private float stackJointExtraBend = 0.12f;
    [SerializeField] private float stackVelocityInfluence = 0.012f;
    [SerializeField] private float stackAngularInfluence = 0.02f;

    [Header("Line Renderer Setup")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float armWidth = 0.035f;
    [SerializeField] private float legWidth = 0.045f;
    [SerializeField] private bool receiveShadows = false;
    [SerializeField] private bool shadowCasting = false;
    [SerializeField] private int sortingOrder = 0;

    [Header("Customization")]
    [SerializeField] private CharacterCustomizer characterCustomizer;
    [SerializeField] private bool routeLimbRenderersThroughCustomizer = true;
    [SerializeField] private string defaultJacketSecondaryChannelId = "secondary";

    [Header("Debug")]
    [SerializeField] private bool autoCreateLineRenderers = true;
    [SerializeField] private bool drawGizmos;
    [SerializeField] private bool previewInEditor = true;

    [SerializeField, HideInInspector] private LineRenderer leftArmLine;
    [SerializeField, HideInInspector] private LineRenderer rightArmLine;
    [SerializeField, HideInInspector] private LineRenderer leftLegLine;
    [SerializeField, HideInInspector] private LineRenderer rightLegLine;

    private enum LimbBindingMode
    {
        None,
        Skin,
        JacketPrimary,
        JacketSecondary
    }

    private LimbBindingMode _leftArmBindingMode = LimbBindingMode.None;
    private LimbBindingMode _rightArmBindingMode = LimbBindingMode.None;
    private LimbBindingMode _leftLegBindingMode = LimbBindingMode.None;
    private LimbBindingMode _rightLegBindingMode = LimbBindingMode.None;

    private WearableAttachment _lastRoutedJacketAttachment;
    private CustomizationOptionSO _lastRoutedJacketOption;
    private int _lastRoutingRendererSignature;
    private bool _lastWalkingMode;

    private Transform _bodyTransform;
    private Transform _headTransform;
    private Transform _leftSkiTransform;
    private Transform _rightSkiTransform;
    private PoleContact _leftPoleContact;
    private PoleContact _rightPoleContact;

    private Vector3 _suspensionBaseLocalPos;
    private Vector3 _suspensionSmoothedLocalPos;
    private float _walkCycleTime;
    private float _walkMotionBlend;
    private Vector3 _lastRootPosition;
    private bool _haveLastRootPosition;

    private float _stackBlend;
    private Vector3 _leftArmRootVel;
    private Vector3 _rightArmRootVel;
    private Vector3 _leftArmEndVel;
    private Vector3 _rightArmEndVel;
    private Vector3 _leftLegRootVel;
    private Vector3 _rightLegRootVel;
    private Vector3 _leftLegEndVel;
    private Vector3 _rightLegEndVel;

    private float _cachedWalkBodyHeightFromSkiMode = 0.92f;
    private bool _footVisualParentsInitialized;
    private Vector3 _leftFootVisualLocalScale = Vector3.one;
    private Vector3 _rightFootVisualLocalScale = Vector3.one;

    private bool _handVisualParentsInitialized;
    private Vector3 _leftHandVisualLocalScale = Vector3.one;
    private Vector3 _rightHandVisualLocalScale = Vector3.one;
    private bool _walkAnchorBaseLocalsInitialized;
    private Vector3 _leftWalkFootBaseLocalPosition;
    private Vector3 _rightWalkFootBaseLocalPosition;
    private Vector3 _leftWalkHandBaseLocalPosition;
    private Vector3 _rightWalkHandBaseLocalPosition;
    private Transform _cachedLeftWalkFootAnchor;
    private Transform _cachedRightWalkFootAnchor;
    private Transform _cachedLeftWalkHandAnchor;
    private Transform _cachedRightWalkHandAnchor;
    private TrickPoseRigSnapshot _previewJointSnapshot;
    private float _previewJointWeight;
    private TrickPoseEntry _runtimeJointPoseEntry;
    private float _runtimeJointPoseWeight;
    private bool _editorPreviewDirty = true;

    private static readonly Vector3[] _lineBuffer = new Vector3[3];

    private void Reset()
    {
        AutoBind();
        EnsureLineRenderers();
        CacheSuspensionBase();
    }

    private void Awake()
    {
        AutoBind();
        EnsureLineRenderers();
        CacheSuspensionBase();
        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();

        if (leftFootVisual != null)
            _leftFootVisualLocalScale = leftFootVisual.localScale;

        if (rightFootVisual != null)
            _rightFootVisualLocalScale = rightFootVisual.localScale;

        if (leftHandVisual != null)
            _leftHandVisualLocalScale = leftHandVisual.localScale;

        if (rightHandVisual != null)
            _rightHandVisualLocalScale = rightHandVisual.localScale;
    }

    private void OnEnable()
    {
        CacheSuspensionBase();
        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();
        _haveLastRootPosition = false;
        _footVisualParentsInitialized = false;
        _handVisualParentsInitialized = false;
        _editorPreviewDirty = true;
    }

    private void OnValidate()
    {
        ApplyLineSettings(leftArmLine, armWidth, "LeftArmLine");
        ApplyLineSettings(rightArmLine, armWidth, "RightArmLine");
        ApplyLineSettings(leftLegLine, legWidth, "LeftLegLine");
        ApplyLineSettings(rightLegLine, legWidth, "RightLegLine");

        _lastRoutedJacketAttachment = null;
        _lastRoutedJacketOption = null;
        _lastRoutingRendererSignature = 0;

        if (!Application.isPlaying && suspensionVisualRoot != null)
            _suspensionBaseLocalPos = suspensionVisualRoot.localPosition;

        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();
        _editorPreviewDirty = true;
    }

    private void OnDisable()
    {
        _stackBlend = 0f;
        _leftArmRootVel = Vector3.zero;
        _rightArmRootVel = Vector3.zero;
        _leftArmEndVel = Vector3.zero;
        _rightArmEndVel = Vector3.zero;
        _leftLegRootVel = Vector3.zero;
        _rightLegRootVel = Vector3.zero;
        _leftLegEndVel = Vector3.zero;
        _rightLegEndVel = Vector3.zero;
    }

    private void OnDestroy()
    {
        OnDisable();
    }

    [ContextMenu("Auto Bind References")]
    public void AutoBind()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>() ?? GetComponentInParent<SkiController>();

        if (walkingController == null)
            walkingController = GetComponent<WalkingController>() ?? GetComponentInParent<WalkingController>();

        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

        if (characterCustomizer == null)
            characterCustomizer = GetComponentInChildren<CharacterCustomizer>(true) ?? GetComponentInParent<CharacterCustomizer>();

        if (skiController != null)
        {
            _bodyTransform = skiController.BodyPoseTransform != null ? skiController.BodyPoseTransform : transform;
            _headTransform = skiController.HeadPoseTransform;
            _leftSkiTransform = skiController.LeftSkiTransform;
            _rightSkiTransform = skiController.RightSkiTransform;
            _leftPoleContact = skiController.LeftPoleContact;
            _rightPoleContact = skiController.RightPoleContact;
        }
        else
        {
            _bodyTransform = transform;
            _headTransform = null;
            _leftSkiTransform = null;
            _rightSkiTransform = null;
            _leftPoleContact = null;
            _rightPoleContact = null;
        }

        if (suspensionVisualRoot == null && _bodyTransform != null)
            suspensionVisualRoot = _bodyTransform;
    }

    public bool HasValidWalkFootTargets()
    {
        return leftWalkFootTarget != null && rightWalkFootTarget != null;
    }

    public bool TryGetBodyLocalAnchor(bool left, bool arm, out Vector3 bodyLocalPosition)
    {
        bodyLocalPosition = Vector3.zero;
        Transform body = GetBodyTransform();
        if (body == null)
            return false;

        Transform explicitAnchor = arm
            ? (left ? leftShoulderAnchor : rightShoulderAnchor)
            : (left ? leftHipAnchor : rightHipAnchor);
        if (explicitAnchor != null)
        {
            bodyLocalPosition = body.InverseTransformPoint(explicitAnchor.position);
            return true;
        }

        bodyLocalPosition = arm
            ? (left ? leftShoulderLocalOffset : rightShoulderLocalOffset)
            : (left ? leftHipLocalOffset : rightHipLocalOffset);
        return true;
    }

    public Vector3 GetWalkFootTargetPosition(bool left)
    {
        Transform t = left ? leftWalkFootTarget : rightWalkFootTarget;
        return t != null ? t.position : Vector3.zero;
    }

    public float GetAverageWalkFootSupportY()
    {
        if (leftWalkFootTarget == null && rightWalkFootTarget == null)
            return transform.position.y;

        bool hasLeft = leftWalkFootTarget != null;
        bool hasRight = rightWalkFootTarget != null;
        float leftY = GetLimbEndpointWorldPosition(true, true, LimbKind.Leg).y;
        float rightY = GetLimbEndpointWorldPosition(false, true, LimbKind.Leg).y;

        if (hasLeft && hasRight)
            return (leftY + rightY) * 0.5f;

        return hasLeft ? leftY : rightY;
    }

    public float GetAverageSkiSupportY()
    {
        Vector3 left = GetLimbEndpointWorldPosition(true, false, LimbKind.Leg);
        Vector3 right = GetLimbEndpointWorldPosition(false, false, LimbKind.Leg);
        return (left.y + right.y) * 0.5f;
    }

    [ContextMenu("Create / Refresh Line Renderers")]
    public void EnsureLineRenderers()
    {
        if (!autoCreateLineRenderers)
            return;

        leftArmLine = EnsureLineRenderer(leftArmLine, "LeftArmLine", armWidth);
        rightArmLine = EnsureLineRenderer(rightArmLine, "RightArmLine", armWidth);
        leftLegLine = EnsureLineRenderer(leftLegLine, "LeftLegLine", legWidth);
        rightLegLine = EnsureLineRenderer(rightLegLine, "RightLegLine", legWidth);

        _lastRoutedJacketAttachment = null;
        _lastRoutedJacketOption = null;
        _lastRoutingRendererSignature = 0;
    }

    public void RefreshEditorPreview()
    {
        if (Application.isPlaying || !previewInEditor)
            return;

        _editorPreviewDirty = false;
        RefreshVisualRepresentation();
    }

    public void SetPreviewJointSnapshot(TrickPoseRigSnapshot snapshot, float weight)
    {
        _previewJointSnapshot = snapshot;
        _previewJointWeight = Mathf.Clamp01(weight);
    }

    public void ClearPreviewJointSnapshot()
    {
        _previewJointSnapshot = null;
        _previewJointWeight = 0f;
    }

    public void SetRuntimeJointPoseEntry(TrickPoseEntry entry, float weight)
    {
        _runtimeJointPoseEntry = entry;
        _runtimeJointPoseWeight = Mathf.Clamp01(weight);
    }

    public bool TryCaptureJointPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation)
    {
        return TryGetResolvedJointControlLocalPose(joint, out localPosition, out localRotation);
    }

    public bool TryGetJointHandlePose(SkierLimbJoint joint, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        Transform body = GetBodyTransform();
        if (body == null || !TryGetResolvedJointControlLocalPose(joint, out Vector3 localPosition, out _))
            return false;

        worldPosition = body.TransformPoint(localPosition);
        worldRotation = body.rotation;
        return true;
    }

    private LineRenderer EnsureLineRenderer(LineRenderer existing, string childName, float width)
    {
        bool createdNew = false;

        if (existing == null)
        {
            Transform child = transform.Find(childName);
            if (child != null)
                existing = child.GetComponent<LineRenderer>();
        }

        if (existing == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            existing = go.AddComponent<LineRenderer>();
            createdNew = true;
        }

        ApplyLineSettings(existing, width, childName, createdNew);
        return existing;
    }

    private void ApplyLineSettings(LineRenderer lr, float width, string childName, bool forceAssignMaterial = false)
    {
        if (lr == null)
            return;

        lr.name = childName;
        lr.positionCount = 3;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.widthMultiplier = width;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sortingOrder = sortingOrder;
        lr.receiveShadows = receiveShadows;
        lr.shadowCastingMode = shadowCasting ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        if (lineMaterial != null && (forceAssignMaterial || lr.sharedMaterial == null))
            lr.sharedMaterial = lineMaterial;
    }

    private void CacheSuspensionBase()
    {
        if (suspensionVisualRoot == null)
            return;

        _suspensionBaseLocalPos = suspensionVisualRoot.localPosition;
        _suspensionSmoothedLocalPos = _suspensionBaseLocalPos;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !previewInEditor)
            return;

        if (!_editorPreviewDirty)
            return;

        RefreshEditorPreview();
#endif
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        RefreshVisualRepresentation();
    }

    private void RefreshVisualRepresentation()
    {
        if (skiController == null)
        {
            AutoBind();
            if (skiController == null)
                return;
        }

        if (autoCreateLineRenderers)
            EnsureLineRenderers();

        RefreshLimbRendererRouting();

        bool walkingMode = walkingController != null && walkingController.IsWalkingMode;

        if (!_footVisualParentsInitialized || !_handVisualParentsInitialized || _lastWalkingMode != walkingMode)
        {
            RefreshFootVisualParents(walkingMode);
            RefreshHandVisualParents(walkingMode);
            _footVisualParentsInitialized = true;
            _handVisualParentsInitialized = true;
            _lastWalkingMode = walkingMode;
        }

        if (!walkingMode)
            CacheBodyHeightFromCurrentSkiSupport();

        UpdateWalkCycle(walkingMode);
        UpdateSuspension(walkingMode);
        UpdateWalkBodyHeight(walkingMode);
        UpdateStackBlend();

        DrawArm(true, walkingMode, leftArmLine);
        DrawArm(false, walkingMode, rightArmLine);
        DrawLeg(true, walkingMode, leftLegLine);
        DrawLeg(false, walkingMode, rightLegLine);
    }

    private void RefreshFootVisualParents(bool walkingMode)
    {
        AttachFootVisual(leftFootVisual, GetActiveLimbAnchor(true, walkingMode, LimbKind.Leg), true);
        AttachFootVisual(rightFootVisual, GetActiveLimbAnchor(false, walkingMode, LimbKind.Leg), false);
    }

    private void AttachFootVisual(Transform footVisual, Transform anchor, bool left)
    {
        if (footVisual == null || anchor == null)
            return;

        if (footVisual.parent != anchor)
            footVisual.SetParent(anchor, false);

        footVisual.localPosition = Vector3.zero;
        footVisual.localRotation = Quaternion.identity;
        footVisual.localScale = left ? _leftFootVisualLocalScale : _rightFootVisualLocalScale;
    }

    private void RefreshHandVisualParents(bool walkingMode)
    {
        AttachHandVisual(leftHandVisual, GetActiveLimbAnchor(true, walkingMode, LimbKind.Arm), true);
        AttachHandVisual(rightHandVisual, GetActiveLimbAnchor(false, walkingMode, LimbKind.Arm), false);
    }

    private void AttachHandVisual(Transform handVisual, Transform anchor, bool left)
    {
        if (handVisual == null || anchor == null)
            return;

        if (handVisual.parent != anchor)
            handVisual.SetParent(anchor, false);

        handVisual.localPosition = Vector3.zero;
        handVisual.localRotation = Quaternion.identity;
        handVisual.localScale = left ? _leftHandVisualLocalScale : _rightHandVisualLocalScale;
    }

    private Transform GetActiveHandAnchor(bool left, bool walkingMode) => GetActiveLimbAnchor(left, walkingMode, LimbKind.Arm);

    private Transform GetActiveFootAnchor(bool left, bool walkingMode) => GetActiveLimbAnchor(left, walkingMode, LimbKind.Leg);

    private Transform GetActiveLimbAnchor(bool left, bool walkingMode, LimbKind limbKind)
    {
        if (walkingMode)
        {
            if (limbKind == LimbKind.Leg)
                return left ? leftWalkFootTarget : rightWalkFootTarget;

            return left ? leftWalkHandTarget : rightWalkHandTarget;
        }

        if (limbKind == LimbKind.Leg)
        {
            Transform skiFootAnchor = left ? leftSkiFootAnchor : rightSkiFootAnchor;
            if (skiFootAnchor != null)
                return skiFootAnchor;

            Transform skiBindingAnchor = left ? leftSkiBindingAnchor : rightSkiBindingAnchor;
            if (skiBindingAnchor != null)
                return skiBindingAnchor;

            return left ? _leftSkiTransform : _rightSkiTransform;
        }

        Transform skiHandAnchor = left ? leftSkiHandAnchor : rightSkiHandAnchor;
        if (skiHandAnchor != null)
            return skiHandAnchor;

        Transform poleGripAnchor = left ? leftPoleGripAnchor : rightPoleGripAnchor;
        if (poleGripAnchor != null)
            return poleGripAnchor;

        PoleContact pole = left ? _leftPoleContact : _rightPoleContact;
        return pole != null ? pole.PoleRoot : null;
    }

    private void UpdateWalkCycle(bool walkingMode)
    {
        if (!_walkAnchorBaseLocalsInitialized)
            CacheWalkAnchorBaseLocals();

        float dt = GetVisualDeltaTime();
        if (dt <= 0f)
            return;

        float moveAmount = 0f;

        if (walkingMode && walkingController != null)
        {
            float inputAmount = walkingController.LastUserMoveMagnitude;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(GetVelocity(), Vector3.up);
            float speedAmount = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, walkPlanarSpeedForFullCycle));

            moveAmount = Mathf.Max(inputAmount, speedAmount);
        }

        float targetBlend = (walkingMode && moveAmount > walkMoveThreshold) ? moveAmount : 0f;
        float blendSpeed = targetBlend > _walkMotionBlend ? walkCycleSpeed : walkIdleReturnSpeed;
        float blendT = 1f - Mathf.Exp(-blendSpeed * dt);
        _walkMotionBlend = Mathf.Lerp(_walkMotionBlend, targetBlend, blendT);

        if (_walkMotionBlend > 0.001f)
            _walkCycleTime += dt * walkCycleSpeed * _walkMotionBlend;

        AnimateWalkAnchors(walkingMode, dt);
    }

    private void UpdateSuspension(bool walkingMode)
    {
        if (suspensionVisualRoot == null)
            return;

        Vector3 targetLocalPos = _suspensionBaseLocalPos;

        if (!walkingMode && enableSuspension)
        {
            Vector3 up = GetBodyUp();
            float compression = 0f;

            bool leftGrounded = skiController.LeftSkiContactRef != null && skiController.LeftSkiContactRef.IsGrounded;
            bool rightGrounded = skiController.RightSkiContactRef != null && skiController.RightSkiContactRef.IsGrounded;
            if (leftGrounded || rightGrounded)
            {
                float terrainCompression = Mathf.Clamp01(Vector3.Angle(GetGroundNormal(), Vector3.up) / 50f);
                compression += terrainCompression * suspensionCompressionFromTerrain;
                compression += skiController.Tuck01 * suspensionCompressionFromTuck;
            }

            compression = Mathf.Clamp(compression, -suspensionMaxOffset, suspensionMaxOffset);
            Vector3 localOffset = suspensionVisualRoot.parent != null
                ? suspensionVisualRoot.parent.InverseTransformDirection(-up * compression)
                : (-up * compression);
            targetLocalPos += localOffset;
        }

        float lerpT = 1f - Mathf.Exp(-suspensionSpeed * GetVisualDeltaTime());
        _suspensionSmoothedLocalPos = Vector3.Lerp(_suspensionSmoothedLocalPos, targetLocalPos, lerpT);
        suspensionVisualRoot.localPosition = _suspensionSmoothedLocalPos;
    }

    private void CacheBodyHeightFromCurrentSkiSupport()
    {
        Transform body = GetBodyTransform();
        if (body == null)
            return;

        float supportY = GetAverageSkiSupportY();
        float offset = body.position.y - supportY;

        if (offset > 0.05f)
            _cachedWalkBodyHeightFromSkiMode = offset;
    }

    private void UpdateWalkBodyHeight(bool walkingMode)
    {
        if (suspensionVisualRoot == null)
            return;

        if (!keepBodyHeightRelativeToFeetInWalkMode)
            return;

        if (!walkingMode)
            return;

        if (!HasValidWalkFootTargets())
            return;

        Transform parent = suspensionVisualRoot.parent;
        if (parent == null)
            return;

        float supportY = GetAverageWalkFootSupportY();
        float desiredWorldBodyY = supportY + _cachedWalkBodyHeightFromSkiMode;

        Vector3 desiredWorldPos = suspensionVisualRoot.position;
        desiredWorldPos.y = desiredWorldBodyY;

        Vector3 desiredLocalPos = parent.InverseTransformPoint(desiredWorldPos);

        Vector3 targetLocalPos = _suspensionSmoothedLocalPos;
        targetLocalPos.y = desiredLocalPos.y;

        float t = 1f - Mathf.Exp(-walkBodyHeightFollowSpeed * GetVisualDeltaTime());
        _suspensionSmoothedLocalPos = Vector3.Lerp(_suspensionSmoothedLocalPos, targetLocalPos, t);
        suspensionVisualRoot.localPosition = _suspensionSmoothedLocalPos;
    }

    private void DrawArm(bool left, bool walkingMode, LineRenderer lr)
    {
        if (lr == null)
            return;

        Vector3 root = GetShoulderPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, walkingMode, LimbKind.Arm);

        ApplyStackRagdoll(ref root, ref end, LimbKind.Arm, left);

        Vector3 bend = ResolveJointWorldPosition(left ? SkierLimbJoint.LeftElbow : SkierLimbJoint.RightElbow, root, end, walkingMode);

        if (_stackBlend > 0.001f)
        {
            Vector3 dir = end - root;
            Vector3 side = Vector3.Cross(GetBodyUp(), dir.normalized);
            if (side.sqrMagnitude < 0.0001f)
                side = GetBodyRight() * (left ? -1f : 1f);

            bend += side.normalized * (stackJointExtraBend * _stackBlend * (left ? -1f : 1f));
        }

        SetLinePositions(lr, root, bend, end);
    }

    private void DrawLeg(bool left, bool walkingMode, LineRenderer lr)
    {
        if (lr == null)
            return;

        Vector3 root = GetHipPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, walkingMode, LimbKind.Leg);

        ApplyStackRagdoll(ref root, ref end, LimbKind.Leg, left);

        Vector3 bend = ResolveJointWorldPosition(left ? SkierLimbJoint.LeftKnee : SkierLimbJoint.RightKnee, root, end, walkingMode);

        if (_stackBlend > 0.001f)
        {
            Vector3 dir = end - root;
            Vector3 side = Vector3.Cross(GetBodyUp(), dir.normalized);
            if (side.sqrMagnitude < 0.0001f)
                side = GetBodyRight() * (left ? -1f : 1f);

            bend += side.normalized * (stackJointExtraBend * 0.8f * _stackBlend * (left ? -1f : 1f));
        }

        SetLinePositions(lr, root, bend, end);
    }

    private void UpdateStackBlend()
    {
        if (!enableStackLimbRagdoll || skiController == null)
        {
            _stackBlend = 0f;
            return;
        }

        float target = skiController.IsStacked ? 1f : 0f;
        float speed = target > _stackBlend ? stackBlendInSpeed : stackBlendOutSpeed;
        _stackBlend = Mathf.MoveTowards(_stackBlend, target, speed * GetVisualDeltaTime());
    }

    private void ApplyStackRagdoll(ref Vector3 root, ref Vector3 end, LimbKind limbKind, bool left)
    {
        if (_stackBlend <= 0.001f)
            return;

        Vector3 linearVel = GetVelocity();
        Vector3 angularVel = targetRigidbody != null ? targetRigidbody.angularVelocity : Vector3.zero;

        Vector3 dragOffset = (-linearVel * stackVelocityInfluence) + (-angularVel * stackAngularInfluence);
        Vector3 bodyUp = GetBodyUp();
        dragOffset = Vector3.ProjectOnPlane(dragOffset, bodyUp) + Vector3.Project(dragOffset, Vector3.up) * 0.35f;

        float rootLag = limbKind == LimbKind.Arm ? stackArmRootLag : stackLegRootLag;
        float endLag = limbKind == LimbKind.Arm ? stackArmEndLag : stackLegEndLag;

        ref Vector3 rootVel = ref GetRootVelocityRef(limbKind, left);
        ref Vector3 endVel = ref GetEndVelocityRef(limbKind, left);

        Vector3 ragdollRootTarget = root + dragOffset * rootLag;
        Vector3 ragdollEndTarget = end + dragOffset * endLag;

        root = Vector3.SmoothDamp(root, ragdollRootTarget, ref rootVel, 0.06f);
        end = Vector3.SmoothDamp(end, ragdollEndTarget, ref endVel, 0.08f);
    }

    private ref Vector3 GetRootVelocityRef(LimbKind limbKind, bool left)
    {
        if (limbKind == LimbKind.Arm)
            return ref (left ? ref _leftArmRootVel : ref _rightArmRootVel);

        return ref (left ? ref _leftLegRootVel : ref _rightLegRootVel);
    }

    private ref Vector3 GetEndVelocityRef(LimbKind limbKind, bool left)
    {
        if (limbKind == LimbKind.Arm)
            return ref (left ? ref _leftArmEndVel : ref _rightArmEndVel);

        return ref (left ? ref _leftLegEndVel : ref _rightLegEndVel);
    }

    private void SetLinePositions(LineRenderer lr, Vector3 a, Vector3 b, Vector3 c)
    {
        _lineBuffer[0] = a;
        _lineBuffer[1] = b;
        _lineBuffer[2] = c;
        lr.SetPositions(_lineBuffer);
        if (!lr.enabled)
            lr.enabled = true;
    }

    private void RefreshLimbRendererRouting()
    {
        if (!routeLimbRenderersThroughCustomizer || characterCustomizer == null)
            return;

        WearableAttachment jacketAttachment = characterCustomizer.GetCurrentJacketAttachment();
        CustomizationOptionSO jacketOption = characterCustomizer.GetCurrentJacketOption();
        int currentRendererSignature = ComputeRoutingRendererSignature();

        bool routingChanged =
            jacketAttachment != _lastRoutedJacketAttachment ||
            jacketOption != _lastRoutedJacketOption ||
            currentRendererSignature != _lastRoutingRendererSignature;

        if (!routingChanged)
            return;

        ClearAllRendererRouting();

        if (jacketAttachment == null || jacketOption == null)
        {
            RouteLimbVisualGroupToDefault(true, LimbKind.Arm, ref _leftArmBindingMode);
            RouteLimbVisualGroupToDefault(false, LimbKind.Arm, ref _rightArmBindingMode);
            RouteLimbVisualGroupToDefault(true, LimbKind.Leg, ref _leftLegBindingMode);
            RouteLimbVisualGroupToDefault(false, LimbKind.Leg, ref _rightLegBindingMode);
        }
        else
        {
            RouteLimbVisualGroupToJacketOrAttachment(true, LimbKind.Arm, jacketAttachment, jacketOption, ref _leftArmBindingMode);
            RouteLimbVisualGroupToJacketOrAttachment(false, LimbKind.Arm, jacketAttachment, jacketOption, ref _rightArmBindingMode);
            RouteLimbVisualGroupToJacketOrAttachment(true, LimbKind.Leg, jacketAttachment, jacketOption, ref _leftLegBindingMode);
            RouteLimbVisualGroupToJacketOrAttachment(false, LimbKind.Leg, jacketAttachment, jacketOption, ref _rightLegBindingMode);
        }

        _lastRoutedJacketAttachment = jacketAttachment;
        _lastRoutedJacketOption = jacketOption;
        _lastRoutingRendererSignature = currentRendererSignature;
    }

    private void ClearAllRendererRouting()
    {
        UnrouteLimbVisualGroup(true, LimbKind.Arm, ref _leftArmBindingMode);
        UnrouteLimbVisualGroup(false, LimbKind.Arm, ref _rightArmBindingMode);
        UnrouteLimbVisualGroup(true, LimbKind.Leg, ref _leftLegBindingMode);
        UnrouteLimbVisualGroup(false, LimbKind.Leg, ref _rightLegBindingMode);
    }

    private void RouteLimbVisualGroupToSkin(bool left, LimbKind limbKind, ref LimbBindingMode mode)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        if (renderers.Count == 0)
            return;

        for (int i = 0; i < renderers.Count; i++)
            RouteRendererToSkin(renderers[i]);

        mode = LimbBindingMode.Skin;
    }

    private void RouteLimbVisualGroupToDefault(bool left, LimbKind limbKind, ref LimbBindingMode mode)
    {
        RouteLimbVisualGroupToSkin(left, limbKind, ref mode);
    }

    private void RouteLimbVisualGroupToJacketOrAttachment(bool left, LimbKind limbKind, WearableAttachment jacketAttachment, CustomizationOptionSO jacketOption, ref LimbBindingMode mode)
    {
        RouteLimbVisualGroup(left, limbKind, jacketAttachment, jacketOption, ref mode);
    }

    private void RouteLimbVisualGroup(bool left, LimbKind limbKind, WearableAttachment jacketAttachment, CustomizationOptionSO jacketOption, ref LimbBindingMode mode)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        if (renderers.Count == 0 || jacketAttachment == null || jacketOption == null)
            return;

        JacketLimbRouteTarget target = GetLimbRouteTarget(left, limbKind);
        LimbWearableColourSource colourSource = jacketOption.GetLimbColourSource(target);
        string secondaryChannelId = jacketOption.GetResolvedLimbSecondaryChannelId(defaultJacketSecondaryChannelId);

        for (int i = 0; i < renderers.Count; i++)
        {
            RouteRendererToJacket(renderers[i], jacketAttachment, colourSource, secondaryChannelId);
        }

        mode = colourSource == LimbWearableColourSource.Secondary
            ? LimbBindingMode.JacketSecondary
            : LimbBindingMode.JacketPrimary;
    }

    private void UnrouteLimbVisualGroup(bool left, LimbKind limbKind, ref LimbBindingMode mode)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        for (int i = 0; i < renderers.Count; i++)
            UnrouteRenderer(renderers[i]);

        mode = LimbBindingMode.None;
    }

    private List<Renderer> GetRoutedRenderers(bool left, LimbKind limbKind)
    {
        var renderers = new List<Renderer>(5);

        Renderer lineRenderer = GetLineRendererForLimb(left, limbKind);
        if (lineRenderer != null)
            renderers.Add(lineRenderer);

        Transform visualRoot = GetVisualRootForLimb(left, limbKind);
        if (visualRoot == null)
            return renderers;

        var childRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < childRenderers.Length; i++)
        {
            Renderer renderer = childRenderers[i];
            if (renderer == null)
                continue;

            // Gloves / boots now live as wearable attachments under the hand/foot visual hierarchy.
            // Exclude any renderer owned by a nested wearable so limb skin/jacket routing only affects
            // the procedural limb visuals themselves.
            var owningWearable = renderer.GetComponentInParent<WearableAttachment>();
            if (owningWearable != null)
                continue;

            if (!renderers.Contains(renderer))
                renderers.Add(renderer);
        }

        return renderers;
    }

    private int ComputeRoutingRendererSignature()
    {
        int signature = 17;
        signature = UpdateRendererSignature(signature, true, LimbKind.Arm);
        signature = UpdateRendererSignature(signature, false, LimbKind.Arm);
        signature = UpdateRendererSignature(signature, true, LimbKind.Leg);
        signature = UpdateRendererSignature(signature, false, LimbKind.Leg);
        return signature;
    }

    private int UpdateRendererSignature(int signature, bool left, LimbKind limbKind)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        unchecked
        {
            signature = (signature * 31) + renderers.Count;
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                signature = (signature * 31) + (renderer != null ? renderer.GetInstanceID() : 0);
                if (renderer != null)
                    signature = (signature * 31) + renderer.sharedMaterials.Length;
            }
        }

        return signature;
    }

    private Renderer GetLineRendererForLimb(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return left ? leftArmLine : rightArmLine;

        return left ? leftLegLine : rightLegLine;
    }

    private Transform GetVisualRootForLimb(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return left ? leftHandVisual : rightHandVisual;

        return left ? leftFootVisual : rightFootVisual;
    }

    private static JacketLimbRouteTarget GetLimbRouteTarget(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return left ? JacketLimbRouteTarget.LeftArm : JacketLimbRouteTarget.RightArm;

        return left ? JacketLimbRouteTarget.LeftLeg : JacketLimbRouteTarget.RightLeg;
    }

    private void RouteRendererToSkin(Renderer renderer)
    {
        if (renderer == null || characterCustomizer == null)
            return;

        characterCustomizer.AddSkinRenderer(renderer);
    }

    private static void RouteRendererToJacket(Renderer renderer, WearableAttachment jacketAttachment, LimbWearableColourSource colourSource, string secondaryChannelId)
    {
        if (renderer == null || jacketAttachment == null)
            return;

        if (colourSource == LimbWearableColourSource.Secondary)
            jacketAttachment.AddExtraRenderer(secondaryChannelId, renderer);
        else
            jacketAttachment.AddPrimaryRenderer(renderer);
    }

    private void UnrouteRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (characterCustomizer != null)
            characterCustomizer.RemoveSkinRenderer(renderer);

        WearableAttachment jacketAttachment = _lastRoutedJacketAttachment;
        CustomizationOptionSO jacketOption = _lastRoutedJacketOption;

        if (jacketAttachment != null)
        {
            jacketAttachment.RemovePrimaryRenderer(renderer);

            string secondaryChannelId = jacketOption != null
                ? jacketOption.GetResolvedLimbSecondaryChannelId(defaultJacketSecondaryChannelId)
                : defaultJacketSecondaryChannelId;

            jacketAttachment.RemoveExtraRenderer(secondaryChannelId, renderer);
        }
    }

    private Vector3 ComputeArmJoint(bool left, Vector3 root, Vector3 end, bool walkingMode)
    {
        Vector3 up = GetBodyUp();
        Vector3 forward = GetBodyForward();
        Vector3 right = GetBodyRight();
        Vector3 dir = end - root;
        Vector3 mid = root + dir * 0.5f;

        float bendAmount;
        if (walkingMode)
        {
            bendAmount = walkArmBend;
        }
        else
        {
            float polePhase01 = GetPolePhaseNormalized();
            bendAmount = armBaseBend;
            bendAmount += skiController.Tuck01 * armTuckBend;
            bendAmount += (skiController.IsAirborne ? 1f : 0f) * armAirBend;
            bendAmount += polePhase01 * armPolePhaseInfluence;
        }

        float sideSign = left ? -1f : 1f;
        Vector3 bendAxis = (-forward * 0.8f) + (right * sideSign * 0.45f) + (-up * 0.2f);

        if (walkingMode && _walkMotionBlend > 0.001f)
        {
            float phase = Mathf.Sin(_walkCycleTime + (left ? 0f : Mathf.PI));
            bendAxis += forward * (phase * 0.35f * _walkMotionBlend);
        }

        bendAxis = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(bendAxis, dir), right * sideSign);
        return mid + bendAxis * bendAmount;
    }

    private Vector3 ComputeLegJoint(bool left, Vector3 root, Vector3 end, bool walkingMode)
    {
        Vector3 forward = GetBodyForward();
        Vector3 right = GetBodyRight();
        Vector3 up = GetBodyUp();
        Vector3 dir = end - root;
        Vector3 mid = root + dir * 0.5f;

        float bendAmount;
        if (walkingMode)
        {
            bendAmount = walkLegBend;
            if (_walkMotionBlend > 0.001f)
                bendAmount += Mathf.Abs(Mathf.Sin(_walkCycleTime + (left ? 0f : Mathf.PI))) * 0.04f * _walkMotionBlend;
        }
        else
        {
            float legInput = left ? skiController.LeftLegInput : skiController.RightLegInput;
            bendAmount = legBaseBend;
            bendAmount += skiController.Tuck01 * legTuckBend;
            bendAmount += legInput * legInputBend;
            bendAmount -= (skiController.IsAirborne ? 1f : 0f) * legAirStraighten;
            bendAmount = Mathf.Max(0.03f, bendAmount);
        }

        float sideSign = left ? -1f : 1f;
        Vector3 movement = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(GetVelocity(), up), forward);

        Vector3 bendAxis = forward + (movement * movementForwardBias) + (right * sideSign * (0.18f + limbOutwardBias));

        if (walkingMode && _walkMotionBlend > 0.001f)
        {
            float phase = Mathf.Sin(_walkCycleTime + (left ? 0f : Mathf.PI));
            bendAxis += forward * (Mathf.Abs(phase) * 0.35f * _walkMotionBlend);
            bendAxis += up * (-Mathf.Abs(phase) * 0.10f * _walkMotionBlend);
        }

        bendAxis = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(bendAxis, dir), forward);
        return mid + bendAxis * bendAmount;
    }

    private Vector3 ResolveJointWorldPosition(SkierLimbJoint joint, Vector3 root, Vector3 end, bool walkingMode)
    {
        if (!TryGetResolvedJointControlLocalPose(joint, out Vector3 localPosition, out _, out bool hasAuthoredOverride))
            return GetProceduralJointWorldPosition(joint, root, end, walkingMode);

        Transform body = GetBodyTransform();
        if (body == null)
            return GetProceduralJointWorldPosition(joint, root, end, walkingMode);

        Vector3 controlWorldPosition = body.TransformPoint(localPosition);
        return hasAuthoredOverride
            ? controlWorldPosition
            : GetProceduralJointWorldPosition(joint, root, end, walkingMode);
    }

    private bool TryGetResolvedJointControlLocalPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation)
    {
        return TryGetResolvedJointControlLocalPose(joint, out localPosition, out localRotation, out _);
    }

    private bool TryGetResolvedJointControlLocalPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation, out bool hasAuthoredOverride)
    {
        if (!TryBuildProceduralJointControlLocalPose(joint, out Vector3 proceduralLocalPosition, out Quaternion proceduralLocalRotation))
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            hasAuthoredOverride = false;
            return false;
        }

        localPosition = proceduralLocalPosition;
        localRotation = proceduralLocalRotation;
        hasAuthoredOverride = false;

        TrickPoseRigSnapshot.PartState previewState = GetPreviewJointState(joint);
        if (previewState.hasValue)
        {
            float t = Mathf.Clamp01(_previewJointWeight);
            localPosition = Vector3.Lerp(proceduralLocalPosition, previewState.localPosition, t);
            localRotation = Quaternion.identity;
            hasAuthoredOverride = true;
            return true;
        }

        PosePartTransformData runtimePose = GetRuntimeJointPose(joint);
        if (runtimePose != null && runtimePose.enabled)
        {
            float t = Mathf.Clamp01(_runtimeJointPoseWeight * Mathf.Clamp01(runtimePose.weight));
            localPosition = Vector3.Lerp(proceduralLocalPosition, runtimePose.localPosition, t);
            localRotation = Quaternion.identity;
            hasAuthoredOverride = true;
        }

        return true;
    }

    private bool TryBuildProceduralJointControlLocalPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation)
    {
        Transform body = GetBodyTransform();
        if (body == null)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            return false;
        }

        bool walkingMode = walkingController != null && walkingController.IsWalkingMode;
        bool left = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.LeftKnee;
        bool arm = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.RightElbow;
        Vector3 root = arm ? GetShoulderPosition(left) : GetHipPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, walkingMode, arm ? LimbKind.Arm : LimbKind.Leg);
        Vector3 jointWorld = arm
            ? ComputeArmJoint(left, root, end, walkingMode)
            : ComputeLegJoint(left, root, end, walkingMode);
        Vector3 segment = end - root;
        Vector3 axis = Vector3.ProjectOnPlane(jointWorld - (root + segment * 0.5f), segment.normalized);
        if (axis.sqrMagnitude < 0.0001f)
            axis = arm ? GetBodyRight() * (left ? -1f : 1f) : GetBodyForward();

        localPosition = body.InverseTransformPoint(jointWorld);
        localRotation = Quaternion.identity;
        return true;
    }

    private Vector3 GetProceduralJointWorldPosition(SkierLimbJoint joint, Vector3 root, Vector3 end, bool walkingMode)
    {
        return joint switch
        {
            SkierLimbJoint.LeftElbow => ComputeArmJoint(true, root, end, walkingMode),
            SkierLimbJoint.RightElbow => ComputeArmJoint(false, root, end, walkingMode),
            SkierLimbJoint.LeftKnee => ComputeLegJoint(true, root, end, walkingMode),
            SkierLimbJoint.RightKnee => ComputeLegJoint(false, root, end, walkingMode),
            _ => root + (end - root) * 0.5f
        };
    }

    private Vector3 BuildJointWorldPositionFromControl(SkierLimbJoint joint, Vector3 root, Vector3 end, Vector3 controlWorldPosition, bool walkingMode)
    {
        Vector3 segment = end - root;
        float segmentLength = Mathf.Max(0.0001f, segment.magnitude);
        Vector3 segmentDir = segment / segmentLength;
        float along = Mathf.Clamp(Vector3.Dot(controlWorldPosition - root, segmentDir), 0f, segmentLength);
        Vector3 onSegment = root + segmentDir * along;

        Vector3 bendAxis = Vector3.ProjectOnPlane(controlWorldPosition - onSegment, segmentDir);
        if (bendAxis.sqrMagnitude < 0.0001f)
            bendAxis = Vector3.ProjectOnPlane(GetProceduralJointWorldPosition(joint, root, end, walkingMode) - onSegment, segmentDir);

        bool left = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.LeftKnee;
        Vector3 fallbackAxis = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.RightElbow
            ? GetBodyRight() * (left ? -1f : 1f)
            : GetBodyForward();
        float bendDistance = Vector3.ProjectOnPlane(controlWorldPosition - onSegment, segmentDir).magnitude;
        if (bendDistance <= 0.0001f)
            return onSegment;

        bendAxis = SafeNormalizeOrFallback(bendAxis, fallbackAxis);
        return onSegment + bendAxis * bendDistance;
    }

    private TrickPoseRigSnapshot.PartState GetPreviewJointState(SkierLimbJoint joint)
    {
        if (_previewJointSnapshot == null)
            return default;

        return joint switch
        {
            SkierLimbJoint.LeftElbow => _previewJointSnapshot.leftElbow,
            SkierLimbJoint.RightElbow => _previewJointSnapshot.rightElbow,
            SkierLimbJoint.LeftKnee => _previewJointSnapshot.leftKnee,
            SkierLimbJoint.RightKnee => _previewJointSnapshot.rightKnee,
            _ => default
        };
    }

    private PosePartTransformData GetRuntimeJointPose(SkierLimbJoint joint)
    {
        if (_runtimeJointPoseEntry == null)
            return null;

        return joint switch
        {
            SkierLimbJoint.LeftElbow => _runtimeJointPoseEntry.leftElbowPose,
            SkierLimbJoint.RightElbow => _runtimeJointPoseEntry.rightElbowPose,
            SkierLimbJoint.LeftKnee => _runtimeJointPoseEntry.leftKneePose,
            SkierLimbJoint.RightKnee => _runtimeJointPoseEntry.rightKneePose,
            _ => null
        };
    }

    private Vector3 GetShoulderPosition(bool left)
    {
        Transform explicitAnchor = left ? leftShoulderAnchor : rightShoulderAnchor;
        if (explicitAnchor != null)
            return explicitAnchor.position;

        Transform body = GetBodyTransform();
        Vector3 fallback = left ? leftShoulderLocalOffset : rightShoulderLocalOffset;
        return body.TransformPoint(fallback);
    }

    private Vector3 GetHipPosition(bool left)
    {
        Transform explicitAnchor = left ? leftHipAnchor : rightHipAnchor;
        if (explicitAnchor != null)
            return explicitAnchor.position;

        Transform body = GetBodyTransform();
        Vector3 fallback = left ? leftHipLocalOffset : rightHipLocalOffset;
        return body.TransformPoint(fallback);
    }

    private void CacheWalkAnchorBaseLocals()
    {
        CacheWalkAnchorBaseLocal(leftWalkFootTarget, ref _cachedLeftWalkFootAnchor, ref _leftWalkFootBaseLocalPosition);
        CacheWalkAnchorBaseLocal(rightWalkFootTarget, ref _cachedRightWalkFootAnchor, ref _rightWalkFootBaseLocalPosition);
        CacheWalkAnchorBaseLocal(leftWalkHandTarget, ref _cachedLeftWalkHandAnchor, ref _leftWalkHandBaseLocalPosition);
        CacheWalkAnchorBaseLocal(rightWalkHandTarget, ref _cachedRightWalkHandAnchor, ref _rightWalkHandBaseLocalPosition);
        _walkAnchorBaseLocalsInitialized = true;
    }

    private void ResetWalkAnchorBaseCache()
    {
        _walkAnchorBaseLocalsInitialized = false;
        _cachedLeftWalkFootAnchor = null;
        _cachedRightWalkFootAnchor = null;
        _cachedLeftWalkHandAnchor = null;
        _cachedRightWalkHandAnchor = null;
    }

    private void CacheWalkAnchorBaseLocal(Transform anchor, ref Transform cachedAnchor, ref Vector3 cachedLocalPosition)
    {
        if (anchor != null && cachedAnchor != anchor)
        {
            cachedLocalPosition = anchor.localPosition;
            cachedAnchor = anchor;
        }
        else if (anchor == null)
        {
            cachedAnchor = null;
        }
    }

    private void AnimateWalkAnchors(bool walkingMode, float dt)
    {
        if (!_walkAnchorBaseLocalsInitialized)
            return;

        UpdateWalkFootAnchor(leftWalkFootTarget, _leftWalkFootBaseLocalPosition, true, walkingMode, dt);
        UpdateWalkFootAnchor(rightWalkFootTarget, _rightWalkFootBaseLocalPosition, false, walkingMode, dt);
        UpdateWalkHandAnchor(leftWalkHandTarget, _leftWalkHandBaseLocalPosition, true, walkingMode, dt);
        UpdateWalkHandAnchor(rightWalkHandTarget, _rightWalkHandBaseLocalPosition, false, walkingMode, dt);
    }

    private void UpdateWalkFootAnchor(Transform anchor, Vector3 baseLocalPosition, bool left, bool walkingMode, float dt)
    {
        if (anchor == null)
            return;

        Vector3 targetLocalPosition = baseLocalPosition;
        if (walkingMode && _walkMotionBlend > 0.001f)
        {
            float phase = _walkCycleTime + (left ? 0f : Mathf.PI);
            float stride = Mathf.Sin(phase);
            float lift = Mathf.Max(0f, Mathf.Sin(phase));
            Vector3 localForward = GetAnchorParentLocalDirection(anchor, GetBodyForward(), Vector3.forward);
            Vector3 localUp = GetAnchorParentLocalDirection(anchor, GetBodyUp(), Vector3.up);
            targetLocalPosition += localForward * (stride * walkFootForwardStride * _walkMotionBlend);
            targetLocalPosition += localUp * (lift * walkFootLift * _walkMotionBlend);
        }

        float followSpeed = walkingMode && _walkMotionBlend > 0.001f ? walkCycleSpeed : walkIdleReturnSpeed;
        float t = 1f - Mathf.Exp(-followSpeed * dt);
        anchor.localPosition = Vector3.Lerp(anchor.localPosition, targetLocalPosition, t);
    }

    private void UpdateWalkHandAnchor(Transform anchor, Vector3 baseLocalPosition, bool left, bool walkingMode, float dt)
    {
        if (anchor == null)
            return;

        Vector3 targetLocalPosition = baseLocalPosition;
        if (walkingMode && _walkMotionBlend > 0.001f)
        {
            float phase = _walkCycleTime + (left ? Mathf.PI : 0f);
            float stride = Mathf.Sin(phase);
            float lift = Mathf.Max(0f, Mathf.Sin(phase));
            Vector3 localForward = GetAnchorParentLocalDirection(anchor, GetBodyForward(), Vector3.forward);
            Vector3 localUp = GetAnchorParentLocalDirection(anchor, GetBodyUp(), Vector3.up);
            targetLocalPosition += localForward * (stride * walkHandForwardStride * _walkMotionBlend);
            targetLocalPosition += localUp * (lift * walkHandLift * _walkMotionBlend);
        }

        float followSpeed = walkingMode && _walkMotionBlend > 0.001f ? walkCycleSpeed : walkIdleReturnSpeed;
        float t = 1f - Mathf.Exp(-followSpeed * dt);
        anchor.localPosition = Vector3.Lerp(anchor.localPosition, targetLocalPosition, t);
    }

    private Vector3 GetLimbEndpointWorldPosition(bool left, bool walkingMode, LimbKind limbKind)
    {
        Transform anchor = GetActiveLimbAnchor(left, walkingMode, limbKind);
        if (anchor != null)
            return anchor.position;

        return GetFallbackLimbEndpointWorldPosition(left, limbKind);
    }

    private Vector3 GetFallbackLimbEndpointWorldPosition(bool left, LimbKind limbKind)
    {
        Vector3 right = GetBodyRight();
        Vector3 up = GetBodyUp();

        if (limbKind == LimbKind.Leg)
        {
            Transform body = GetBodyTransform();
            return body.position + right * (left ? -0.12f : 0.12f) + up * -0.85f;
        }

        Transform head = _headTransform != null ? _headTransform : GetBodyTransform();
        return head.position + right * (left ? -0.18f : 0.18f) + up * -0.10f;
    }

    private Vector3 GetAnchorParentLocalDirection(Transform anchor, Vector3 worldDirection, Vector3 fallbackLocalDirection)
    {
        Transform parent = anchor != null ? anchor.parent : null;
        if (parent == null)
            return fallbackLocalDirection;

        Vector3 localDirection = parent.InverseTransformDirection(worldDirection);
        return SafeNormalizeOrFallback(localDirection, fallbackLocalDirection);
    }

    private Transform GetBodyTransform()
    {
        if (_bodyTransform == null)
            _bodyTransform = skiController != null && skiController.BodyPoseTransform != null ? skiController.BodyPoseTransform : transform;
        return _bodyTransform;
    }

    private Vector3 GetGroundNormal()
    {
        if (skiController == null)
            return Vector3.up;

        Vector3 groundNormal = skiController.GroundNormal;
        return groundNormal.sqrMagnitude > 0.0001f ? groundNormal.normalized : Vector3.up;
    }

    private Vector3 GetBodyUp()
    {
        if (skiController != null && !skiController.IsAirborne)
            return GetGroundNormal();

        Transform body = GetBodyTransform();
        return body != null ? body.up : transform.up;
    }

    private Vector3 GetBodyForward()
    {
        Transform body = GetBodyTransform();
        Vector3 up = GetBodyUp();
        Vector3 forward = body != null ? body.forward : transform.forward;
        forward = Vector3.ProjectOnPlane(forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            Vector3 velocityPlanar = Vector3.ProjectOnPlane(GetVelocity(), up);
            if (velocityPlanar.sqrMagnitude > 0.0001f)
                forward = velocityPlanar;
            else
                forward = Vector3.ProjectOnPlane(transform.forward, up);
        }

        return forward.normalized;
    }

    private Vector3 GetBodyRight()
    {
        Vector3 up = GetBodyUp();
        Vector3 forward = GetBodyForward();
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = transform.right;
        return right.normalized;
    }

    private Vector3 GetVelocity()
    {
        if (Application.isPlaying)
        {
            if (skiController != null)
            {
                try
                {
                    return skiController.Velocity;
                }
                catch
                {
                    // Fall through to other velocity sources.
                }
            }

            if (targetRigidbody != null)
                return targetRigidbody.linearVelocity;
        }
        else
        {
            if (targetRigidbody != null)
            {
                try
                {
                    return targetRigidbody.linearVelocity;
                }
                catch
                {
                    // Ignore and fall back to transform delta.
                }
            }
        }

        Vector3 current = transform.position;
        if (!_haveLastRootPosition)
        {
            _lastRootPosition = current;
            _haveLastRootPosition = true;
            return Vector3.zero;
        }

        float dt = GetVisualDeltaTime();
        if (dt <= 0f)
            return Vector3.zero;

        Vector3 velocity = (current - _lastRootPosition) / dt;
        _lastRootPosition = current;
        return velocity;
    }

    private float GetPolePhaseNormalized()
    {
        if (skiController == null)
            return 0f;

        switch (skiController.CurrentPolePhase)
        {
            case SkiController.PoleStrokePhase.Entry:
                return 0.33f;
            case SkiController.PoleStrokePhase.Drag:
                return 0.66f;
            case SkiController.PoleStrokePhase.FollowThrough:
                return 1f;
            default:
                return 0f;
        }
    }

    private static Vector3 SafeNormalizeOrFallback(Vector3 vector, Vector3 fallback)
    {
        if (vector.sqrMagnitude > 0.0001f)
            return vector.normalized;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;
        return Vector3.right;
    }

    private float GetVisualDeltaTime()
    {
        if (Application.isPlaying)
            return Time.deltaTime;

#if UNITY_EDITOR
        return UnityEditor.EditorApplication.isPaused ? 0f : (1f / 60f);
#else
    return 0f;
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        AutoBind();

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(GetShoulderPosition(true), 0.025f);
        Gizmos.DrawSphere(GetShoulderPosition(false), 0.025f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetHipPosition(true), 0.025f);
        Gizmos.DrawSphere(GetHipPosition(false), 0.025f);
    }
}
