#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for SkiController that groups parameters into logical
/// sections, uses Vector2-style controls for min/max ranges, and only uses
/// sliders where they really help (0-1 factors, tiny offsets, etc.).
/// </summary>
[CustomEditor(typeof(SkiController))]
public class SkiControllerEditor : Editor
{
    // Foldout toggles
    private bool _showReferences = true;
    private bool _showLeanStance = true;
    private bool _showGrounding = true;
    private bool _showDownhillFriction = true;
    private bool _showSkatingPoles = true;
    private bool _showLandingOrientation = true;
    private bool _showAirJump = true;
    private bool _showRuntime = true;

    // References
    private SerializedProperty bodyTransform;
    private SerializedProperty leftSki;
    private SerializedProperty leftSkiContact;
    private SerializedProperty rightSki;
    private SerializedProperty rightSkiContact;

    // Input
    private SerializedProperty leftSkiAction;
    private SerializedProperty rightSkiAction;
    private SerializedProperty leanAction;
    private SerializedProperty polesAction;
    private SerializedProperty jumpAction;

    // Lean & stance
    private SerializedProperty forwardLeanLerpSpeed;
    private SerializedProperty stanceLerpSpeed;
    private SerializedProperty skiVisualLerpSpeed;
    private SerializedProperty followerStanceLerpSpeed;
    private SerializedProperty maxSkiOffset;
    private SerializedProperty maxSkiEdgeAngle;
    private SerializedProperty maxForwardLeanAngle;
    private SerializedProperty maxSideLeanAngle;

    // Grounding / suspension
    private SerializedProperty groundLayers;
    private SerializedProperty groundCheckRadius;
    private SerializedProperty groundCheckHeight;
    private SerializedProperty groundCheckDistance;
    private SerializedProperty skiHeightOffset;
    private SerializedProperty skiSuspensionLerp;

    // Downhill / friction / carve
    private SerializedProperty downhillAccelMin;
    private SerializedProperty downhillAccelMax;
    private SerializedProperty minSlopeAngleForDownhill;
    private SerializedProperty minAlignmentForDownhill;
    private SerializedProperty forwardFriction;
    private SerializedProperty sideFriction;
    private SerializedProperty normalKillStrength;
    private SerializedProperty carveSteerStrength;
    private SerializedProperty minCarveSpeed;
    private SerializedProperty maxCarveSpeed;
    private SerializedProperty lowSpeedMaxYaw;
    private SerializedProperty minSpeedForSkateYaw;

    // Skating
    private SerializedProperty minForwardLeanForPush;
    private SerializedProperty skateImpulse;
    private SerializedProperty skateCooldown;
    private SerializedProperty skateMaxEffectiveSpeed;
    private SerializedProperty skateYawPerPush;

    // Poles
    private SerializedProperty poleTapThreshold;
    private SerializedProperty poleImpulse;
    private SerializedProperty poleMaxSpeed;
    private SerializedProperty poleBrakeStrength;

    // Landing / stack
    private SerializedProperty maxLandingTiltAngle;
    private SerializedProperty maxLandingMisalignmentAngle;
    private SerializedProperty minLandingSpeedForStackCheck;
    private SerializedProperty stackTorqueImpulse;

    // Orientation
    private SerializedProperty groundTurnSpeed;

    // Air + jump
    private SerializedProperty airYawTurnSpeed;
    private SerializedProperty airPitchTurnSpeed;
    private SerializedProperty jumpForceRange;
    private SerializedProperty jumpChargeTime;

    private void OnEnable()
    {
        // References
        bodyTransform = serializedObject.FindProperty("bodyTransform");
        leftSki = serializedObject.FindProperty("leftSki");
        leftSkiContact = serializedObject.FindProperty("leftSkiContact");
        rightSki = serializedObject.FindProperty("rightSki");
        rightSkiContact = serializedObject.FindProperty("rightSkiContact");

        // Input
        leftSkiAction = serializedObject.FindProperty("leftSkiAction");
        rightSkiAction = serializedObject.FindProperty("rightSkiAction");
        leanAction = serializedObject.FindProperty("leanAction");
        polesAction = serializedObject.FindProperty("polesAction");
        jumpAction = serializedObject.FindProperty("jumpAction");

        // Lean & stance
        forwardLeanLerpSpeed = serializedObject.FindProperty("forwardLeanLerpSpeed");
        stanceLerpSpeed = serializedObject.FindProperty("stanceLerpSpeed");
        skiVisualLerpSpeed = serializedObject.FindProperty("skiVisualLerpSpeed");
        followerStanceLerpSpeed = serializedObject.FindProperty("followerStanceLerpSpeed");
        maxSkiOffset = serializedObject.FindProperty("maxSkiOffset");
        maxSkiEdgeAngle = serializedObject.FindProperty("maxSkiEdgeAngle");
        maxForwardLeanAngle = serializedObject.FindProperty("maxForwardLeanAngle");
        maxSideLeanAngle = serializedObject.FindProperty("maxSideLeanAngle");

        // Grounding / suspension
        groundLayers = serializedObject.FindProperty("groundLayers");
        groundCheckRadius = serializedObject.FindProperty("groundCheckRadius");
        groundCheckHeight = serializedObject.FindProperty("groundCheckHeight");
        groundCheckDistance = serializedObject.FindProperty("groundCheckDistance");
        skiHeightOffset = serializedObject.FindProperty("skiHeightOffset");
        skiSuspensionLerp = serializedObject.FindProperty("skiSuspensionLerp");

        // Downhill / friction / carve
        downhillAccelMin = serializedObject.FindProperty("downhillAccelMin");
        downhillAccelMax = serializedObject.FindProperty("downhillAccelMax");
        minSlopeAngleForDownhill = serializedObject.FindProperty("minSlopeAngleForDownhill");
        minAlignmentForDownhill = serializedObject.FindProperty("minAlignmentForDownhill");
        forwardFriction = serializedObject.FindProperty("forwardFriction");
        sideFriction = serializedObject.FindProperty("sideFriction");
        normalKillStrength = serializedObject.FindProperty("normalKillStrength");
        carveSteerStrength = serializedObject.FindProperty("carveSteerStrength");
        minCarveSpeed = serializedObject.FindProperty("minCarveSpeed");
        maxCarveSpeed = serializedObject.FindProperty("maxCarveSpeed");
        lowSpeedMaxYaw = serializedObject.FindProperty("lowSpeedMaxYaw");
        minSpeedForSkateYaw = serializedObject.FindProperty("minSpeedForSkateYaw");

        // Skating
        minForwardLeanForPush = serializedObject.FindProperty("minForwardLeanForPush");
        skateImpulse = serializedObject.FindProperty("skateImpulse");
        skateCooldown = serializedObject.FindProperty("skateCooldown");
        skateMaxEffectiveSpeed = serializedObject.FindProperty("skateMaxEffectiveSpeed");
        skateYawPerPush = serializedObject.FindProperty("skateYawPerPush");

        // Poles
        poleTapThreshold = serializedObject.FindProperty("poleTapThreshold");
        poleImpulse = serializedObject.FindProperty("poleImpulse");
        poleMaxSpeed = serializedObject.FindProperty("poleMaxSpeed");
        poleBrakeStrength = serializedObject.FindProperty("poleBrakeStrength");

        // Landing / stack
        maxLandingTiltAngle = serializedObject.FindProperty("maxLandingTiltAngle");
        maxLandingMisalignmentAngle = serializedObject.FindProperty("maxLandingMisalignmentAngle");
        minLandingSpeedForStackCheck = serializedObject.FindProperty("minLandingSpeedForStackCheck");
        stackTorqueImpulse = serializedObject.FindProperty("stackTorqueImpulse");

        // Orientation
        groundTurnSpeed = serializedObject.FindProperty("groundTurnSpeed");

        // Air + jump
        airYawTurnSpeed = serializedObject.FindProperty("airYawTurnSpeed");
        airPitchTurnSpeed = serializedObject.FindProperty("airPitchTurnSpeed");
        jumpForceRange = serializedObject.FindProperty("jumpForceRange");
        jumpChargeTime = serializedObject.FindProperty("jumpChargeTime");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawReferencesSection();
        EditorGUILayout.Space(6);

        DrawLeanStanceSection();
        EditorGUILayout.Space(6);

        DrawGroundingSection();
        EditorGUILayout.Space(6);

        DrawDownhillFrictionSection();
        EditorGUILayout.Space(6);

        DrawSkatingPolesSection();
        EditorGUILayout.Space(6);

        DrawLandingOrientationSection();
        EditorGUILayout.Space(6);

        DrawAirJumpSection();
        EditorGUILayout.Space(6);

        DrawRuntimeSection();

        serializedObject.ApplyModifiedProperties();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private void DrawMinMaxVector2(string label,
                                   SerializedProperty minProp,
                                   SerializedProperty maxProp,
                                   float minLimit,
                                   float maxLimit)
    {
        Vector2 value = new Vector2(minProp.floatValue, maxProp.floatValue);
        EditorGUI.BeginChangeCheck();
        value = EditorGUILayout.Vector2Field(label, value);
        if (EditorGUI.EndChangeCheck())
        {
            value.x = Mathf.Clamp(value.x, minLimit, maxLimit);
            value.y = Mathf.Clamp(value.y, minLimit, maxLimit);
            if (value.y < value.x)
                value.y = value.x;

            minProp.floatValue = value.x;
            maxProp.floatValue = value.y;
        }
    }

    // ----------------------------------------------------------------------
    // Sections
    // ----------------------------------------------------------------------

    private void DrawReferencesSection()
    {
        _showReferences = EditorGUILayout.BeginFoldoutHeaderGroup(_showReferences, "References & Input");
        if (_showReferences)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Transforms", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(bodyTransform);
            EditorGUILayout.PropertyField(leftSki);
            EditorGUILayout.PropertyField(leftSkiContact);
            EditorGUILayout.PropertyField(rightSki);
            EditorGUILayout.PropertyField(rightSkiContact);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Input (New Input System)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(leftSkiAction);
            EditorGUILayout.PropertyField(rightSkiAction);
            EditorGUILayout.PropertyField(leanAction);
            EditorGUILayout.PropertyField(polesAction);
            EditorGUILayout.PropertyField(jumpAction);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawLeanStanceSection()
    {
        _showLeanStance = EditorGUILayout.BeginFoldoutHeaderGroup(_showLeanStance, "Lean & Stance");
        if (_showLeanStance)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Smoothing", EditorStyles.miniBoldLabel);
            forwardLeanLerpSpeed.floatValue =
                EditorGUILayout.FloatField("Forward Lean Lerp Speed", forwardLeanLerpSpeed.floatValue);
            stanceLerpSpeed.floatValue =
                EditorGUILayout.FloatField("Stance Lerp Speed", stanceLerpSpeed.floatValue);
            followerStanceLerpSpeed.floatValue =
                EditorGUILayout.FloatField("Follower Stance Lerp Speed", followerStanceLerpSpeed.floatValue);
            skiVisualLerpSpeed.floatValue =
                EditorGUILayout.FloatField("Ski Visual Lerp Speed", skiVisualLerpSpeed.floatValue);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Range Limits", EditorStyles.miniBoldLabel);
            maxSkiOffset.floatValue =
                EditorGUILayout.Slider("Max Ski Offset (m)", maxSkiOffset.floatValue, 0f, 1f);
            maxSkiEdgeAngle.floatValue =
                EditorGUILayout.FloatField("Max Ski Edge Angle", maxSkiEdgeAngle.floatValue);
            maxForwardLeanAngle.floatValue =
                EditorGUILayout.FloatField("Max Forward Lean Angle", maxForwardLeanAngle.floatValue);
            maxSideLeanAngle.floatValue =
                EditorGUILayout.FloatField("Max Side Lean Angle", maxSideLeanAngle.floatValue);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawGroundingSection()
    {
        _showGrounding = EditorGUILayout.BeginFoldoutHeaderGroup(_showGrounding, "Grounding & Suspension");
        if (_showGrounding)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(groundLayers);

            groundCheckRadius.floatValue =
                EditorGUILayout.FloatField("Ground Check Radius", groundCheckRadius.floatValue);
            groundCheckHeight.floatValue =
                EditorGUILayout.FloatField("Ground Check Height", groundCheckHeight.floatValue);
            groundCheckDistance.floatValue =
                EditorGUILayout.FloatField("Ground Check Distance", groundCheckDistance.floatValue);

            EditorGUILayout.Space(4);
            skiHeightOffset.floatValue =
                EditorGUILayout.Slider("Ski Height Offset", skiHeightOffset.floatValue, 0f, 0.2f);
            skiSuspensionLerp.floatValue =
                EditorGUILayout.FloatField("Ski Suspension Lerp", skiSuspensionLerp.floatValue);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawDownhillFrictionSection()
    {
        _showDownhillFriction = EditorGUILayout.BeginFoldoutHeaderGroup(_showDownhillFriction, "Downhill, Friction & Carve");
        if (_showDownhillFriction)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Downhill Acceleration", EditorStyles.miniBoldLabel);
            DrawMinMaxVector2("Accel Min/Max", downhillAccelMin, downhillAccelMax, 0f, 40f);
            minSlopeAngleForDownhill.floatValue =
                EditorGUILayout.FloatField("Min Slope Angle", minSlopeAngleForDownhill.floatValue);
            minAlignmentForDownhill.floatValue =
                EditorGUILayout.Slider("Min Alignment (dot)", minAlignmentForDownhill.floatValue, 0f, 1f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Friction", EditorStyles.miniBoldLabel);
            forwardFriction.floatValue =
                EditorGUILayout.FloatField("Forward Friction", forwardFriction.floatValue);
            sideFriction.floatValue =
                EditorGUILayout.FloatField("Side Friction", sideFriction.floatValue);
            normalKillStrength.floatValue =
                EditorGUILayout.FloatField("Normal Kill Strength", normalKillStrength.floatValue);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Carve Steering & Speeds", EditorStyles.miniBoldLabel);
            carveSteerStrength.floatValue =
                EditorGUILayout.FloatField("Carve Steer Strength", carveSteerStrength.floatValue);
            DrawMinMaxVector2("Carve Speed Min/Max", minCarveSpeed, maxCarveSpeed, 0f, 30f);
            lowSpeedMaxYaw.floatValue =
                EditorGUILayout.FloatField("Low Speed Max Yaw", lowSpeedMaxYaw.floatValue);
            minSpeedForSkateYaw.floatValue =
                EditorGUILayout.FloatField("Min Speed For Skate Yaw", minSpeedForSkateYaw.floatValue);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSkatingPolesSection()
    {
        _showSkatingPoles = EditorGUILayout.BeginFoldoutHeaderGroup(_showSkatingPoles, "Skating & Poles");
        if (_showSkatingPoles)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Skating", EditorStyles.miniBoldLabel);
            minForwardLeanForPush.floatValue =
                EditorGUILayout.Slider("Min Forward Lean For Push", minForwardLeanForPush.floatValue, 0f, 1f);
            skateImpulse.floatValue =
                EditorGUILayout.FloatField("Skate Impulse", skateImpulse.floatValue);
            skateCooldown.floatValue =
                EditorGUILayout.FloatField("Skate Cooldown", skateCooldown.floatValue);
            skateMaxEffectiveSpeed.floatValue =
                EditorGUILayout.FloatField("Skate Max Effective Speed", skateMaxEffectiveSpeed.floatValue);
            skateYawPerPush.floatValue =
                EditorGUILayout.FloatField("Skate Yaw Per Push", skateYawPerPush.floatValue);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Poles", EditorStyles.miniBoldLabel);
            poleTapThreshold.floatValue =
                EditorGUILayout.FloatField("Tap Threshold", poleTapThreshold.floatValue);
            poleImpulse.floatValue =
                EditorGUILayout.FloatField("Pole Impulse", poleImpulse.floatValue);
            poleMaxSpeed.floatValue =
                EditorGUILayout.FloatField("Pole Max Speed", poleMaxSpeed.floatValue);
            poleBrakeStrength.floatValue =
                EditorGUILayout.FloatField("Pole Brake Strength", poleBrakeStrength.floatValue);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawLandingOrientationSection()
    {
        _showLandingOrientation = EditorGUILayout.BeginFoldoutHeaderGroup(_showLandingOrientation, "Landing & Orientation");
        if (_showLandingOrientation)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Landing / Stack", EditorStyles.miniBoldLabel);
            maxLandingTiltAngle.floatValue =
                EditorGUILayout.FloatField("Max Landing Tilt Angle", maxLandingTiltAngle.floatValue);
            maxLandingMisalignmentAngle.floatValue =
                EditorGUILayout.FloatField("Max Landing Misalignment", maxLandingMisalignmentAngle.floatValue);
            minLandingSpeedForStackCheck.floatValue =
                EditorGUILayout.FloatField("Min Landing Speed For Stack", minLandingSpeedForStackCheck.floatValue);
            stackTorqueImpulse.floatValue =
                EditorGUILayout.FloatField("Stack Torque Impulse", stackTorqueImpulse.floatValue);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Ground Orientation", EditorStyles.miniBoldLabel);
            groundTurnSpeed.floatValue =
                EditorGUILayout.FloatField("Ground Turn Speed", groundTurnSpeed.floatValue);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawAirJumpSection()
    {
        _showAirJump = EditorGUILayout.BeginFoldoutHeaderGroup(_showAirJump, "Air Control & Jump");
        if (_showAirJump)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Air Control", EditorStyles.miniBoldLabel);
            airYawTurnSpeed.floatValue =
                EditorGUILayout.FloatField("Air Yaw Turn Speed", airYawTurnSpeed.floatValue);
            airPitchTurnSpeed.floatValue =
                EditorGUILayout.FloatField("Air Pitch Turn Speed", airPitchTurnSpeed.floatValue);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Jump", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(jumpForceRange, new GUIContent("Jump Force (min/max)"));
            jumpChargeTime.floatValue =
                EditorGUILayout.FloatField("Jump Charge Time", jumpChargeTime.floatValue);

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawRuntimeSection()
    {
        _showRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(_showRuntime, "Runtime Info (read-only)");
        if (_showRuntime)
        {
            EditorGUI.indentLevel++;

            var controller = (SkiController)target;
            var rb = controller.GetComponent<Rigidbody>();

            if (rb != null)
            {
                float speed = rb.linearVelocity.magnitude;
                Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
                float planarSpeed = planar.magnitude;

                EditorGUILayout.LabelField("World Speed", speed.ToString("F2") + " m/s");
                EditorGUILayout.LabelField("Planar Speed", planarSpeed.ToString("F2") + " m/s");
            }
            else
            {
                EditorGUILayout.HelpBox("No Rigidbody found on this GameObject.", MessageType.Info);
            }

            EditorGUILayout.LabelField("Is Stacked", controller.IsStacked ? "Yes" : "No");

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif
