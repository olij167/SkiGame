#if UNITY_EDITOR
namespace NOT_Lonely.Weatherade
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SnowCoverage))]
    public class SnowCoverage_editor : Editor
    {
        private SnowCoverage snowCoverage;

        //Main Settings
        private SerializedProperty depthTextureResolution;
        private SerializedProperty areaSize;
        private SerializedProperty areaDepth;
        private SerializedProperty texVSM;
        private SerializedProperty texBlured;
        private SerializedProperty texRGBA;
        private SerializedProperty depthTexture;
        private SerializedProperty depthCopyMtl;
        private SerializedProperty blurKernelSize;
        private SerializedProperty depthLayerMask;
        private SerializedProperty depthTextureFormat;
        private SerializedProperty coverageAreaFalloffHardness;
        private SerializedProperty useFollowTarget;
        private SerializedProperty followTarget;
        private SerializedProperty forcePositionUpdate;
        private SerializedProperty targetPositionOffsetY;
        private SerializedProperty updateRate;
        private SerializedProperty updateDistanceThreshold;

        //Basic Settings
        private SerializedProperty coverage;
        private SerializedProperty coverageAmount;
        private SerializedProperty paintableCoverage;
        private SerializedProperty stochastic;
        private SerializedProperty useAveragedNormals;

        private SerializedProperty coverageTex0;
        private SerializedProperty coverageTex1;
        private SerializedProperty coverageTex2;

        private SerializedProperty coverageColor;
        private SerializedProperty coverageColor1;
        private SerializedProperty coverageColor2;

        private SerializedProperty cov0Smoothness;
        private SerializedProperty cov1Smoothness;
        private SerializedProperty cov2Smoothness;

        private SerializedProperty coverageNormalScale0;
        private SerializedProperty coverageNormalScale1;
        private SerializedProperty coverageNormalScale2;

        private SerializedProperty coverageTiling;
        private SerializedProperty coverageTiling1;
        private SerializedProperty coverageTiling2;

        private SerializedProperty covTriBlendContrast;
        private SerializedProperty emissionMasking;
        private SerializedProperty maskByAlpha;
        private SerializedProperty coverageNormalsOverlay;

        //Detail Map
        private SerializedProperty useCoverageDetail;
        private SerializedProperty coverageDetailTex;
        private SerializedProperty detailTiling;
        private SerializedProperty detailTexRemap;
        private SerializedProperty detailNormalScale;
        private SerializedProperty detailDistance;

        //Tessellation
        private SerializedProperty tessellation;
        private SerializedProperty tessEdgeL;
        private SerializedProperty tessFactorSnow;
        private SerializedProperty tessMaxDisp;
        private SerializedProperty tessSnowdriftRange;

        //Area Mask
        private SerializedProperty coverageAreaBias;
        private SerializedProperty coverageLeakReduction;
        private SerializedProperty coverageAreaMaskRange;
        private SerializedProperty precipitationDirOffset;
        private SerializedProperty precipitationDirRange;

        //Displacement
        private SerializedProperty displacement;

        private SerializedProperty heightMap0Contrast;
        private SerializedProperty heightMap1Contrast;
        private SerializedProperty heightMap2Contrast;

        private SerializedProperty coverageDisplacement;
        private SerializedProperty coverageDisplacement1;
        private SerializedProperty coverageDisplacement2;

        private SerializedProperty heightMap0LOD;
        private SerializedProperty heightMap1LOD;
        private SerializedProperty heightMap2LOD;

        private SerializedProperty coverageDisplacementOffset;

        //Traces
        private SerializedProperty traces;

        private SerializedProperty tracesBaseBlend0;
        private SerializedProperty tracesBaseBlend1;
        private SerializedProperty tracesBaseBlend2;

        private SerializedProperty tracesNormalScale;
        private SerializedProperty tracesNormalScale1;
        private SerializedProperty tracesNormalScale2;

        private SerializedProperty tracesColor;
        private SerializedProperty tracesColor1;
        private SerializedProperty tracesColor2;

        private SerializedProperty tracesColorBlendRange;
        private SerializedProperty tracesColorBlendRange1;
        private SerializedProperty tracesColorBlendRange2;

        private SerializedProperty traceDetail;
        private SerializedProperty traceDetailTex;
        private SerializedProperty traceDetailTiling;
        private SerializedProperty traceDetailNormalScale;
        private SerializedProperty traceDetailIntensity;

        //Blend by Normals
        private SerializedProperty blendByNormalsStrength;
        private SerializedProperty blendByNormalsPower;

        //Distance Fade
        private SerializedProperty distanceFadeStart;
        private SerializedProperty distanceFadeFalloff;

        //Sparkle and SSS
        private SerializedProperty sss;
        private SerializedProperty sss_intensity;
        private SerializedProperty colorEnhance;

        private SerializedProperty enhanceRemap0;
        private SerializedProperty enhanceRemap1;
        private SerializedProperty enhanceRemap2;

        private SerializedProperty sssMaskRemap0;
        private SerializedProperty sssMaskRemap1;
        private SerializedProperty sssMaskRemap2;

        private SerializedProperty highlightBrightness0;
        private SerializedProperty highlightBrightness1;
        private SerializedProperty highlightBrightness2;

        private SerializedProperty sparkle;
        private SerializedProperty sparkleTex;
        private SerializedProperty sparklesAmount;
        private SerializedProperty sparkleDistFalloff;
        private SerializedProperty sparkleTexSS;
        private SerializedProperty sparkleTexLS;
        private SerializedProperty localSparkleTiling;
        private SerializedProperty screenSpaceSparklesTiling; 
        private SerializedProperty sparklesBrightness;
        private SerializedProperty sparklesLightmapMaskPower;
        private SerializedProperty sparklesHighlightMaskExpansion;
        //------------------------

        private SerializedProperty depthTexProps;
        private SerializedProperty surfaceProps;
        private SerializedProperty basicSettingsFoldout;
        private SerializedProperty detailMapFoldout;
        private SerializedProperty areaMaskFoldout;
        private SerializedProperty tessellationFoldout;
        private SerializedProperty displacementFoldout;
        private SerializedProperty tracesFoldout;
        private SerializedProperty blendByNormalFoldout;
        private SerializedProperty distanceFadeFoldout;
        private SerializedProperty sparklesFoldout;

        private SerializedProperty foldout_basic_layer0;
        private SerializedProperty foldout_basic_layer1;
        private SerializedProperty foldout_basic_layer2;
        private SerializedProperty foldout_displacement_layer0;
        private SerializedProperty foldout_displacement_layer1;
        private SerializedProperty foldout_displacement_layer2;
        private SerializedProperty foldout_traces_layer0;
        private SerializedProperty foldout_traces_layer1;
        private SerializedProperty foldout_traces_layer2;
        private SerializedProperty foldout_sss_layer0;
        private SerializedProperty foldout_sss_layer1;
        private SerializedProperty foldout_sss_layer2;

        private GUIContent areaSizeLabel = new GUIContent("Horizontal Size", "The local horizontal size of the snow/rain area.");
        private GUIContent areaDepthLabel = new GUIContent("Depth", "The local depth of the snow/rain area.");
        private GUIContent depthLayerMaskLabel = new GUIContent("Affected Layers", "Objects on these layers will be visible to the Weatherade Coverage Instance, when it builds the coverage mask.");
        private GUIContent depthTextureFormatLabel = new GUIContent("Depth Texture Format", "RGHalf is the most cheap option, but can provide banding artifacts at the coverage transitions. " +
            "RGBA options are needed if you want to use the SRS Particle System with the collision mode set to Global.");
        private GUIContent depthTextureResolutionLabel = new GUIContent("Depth Texture Resolution", "The depth texture resolution affects the quality of the effect.");
        private GUIContent blurKernelSizeLabel = new GUIContent("Blur Kernel Size", "The larger the value, the more blur will be applied to the coverage mask. It also affects performance accordingly.");
        private GUIContent coverageAreaFalloffHardnessLabel = new GUIContent("Area Falloff Hardness", "How hard the coverage area border will be. Values lower than 1 will add a gradient from the area center to the borders.");
        private GUIContent useFollowTargetLabel = new GUIContent(
            "Follow Target", "Use an object that this coverage area will follow." +
            "\nIf the object is not specified, then the first found camera with the 'MainCamera' tag will be used." +
            "\nThe coverage area will remain in place if the checkbox is disabled, or the object is not specified and the camera is not found.");
        private GUIContent forcePositionUpdateLabel = new GUIContent(
            "Force Position Update", "Try updating the position, even if the 'Follow Target' is not specified and has not been found. Useful if you want to change the 'Follow Target' at runtime.");
        private GUIContent followTargetLabel = new GUIContent("Follow Target", "The object that this coverage area will follow. " +
            "If the object is not specified, then a first found camera with the 'MainCamera' tag will be used." +
            "If no camera found, the coverage area will remain in place.");
        private GUIContent targetPosOffsetYLabel = new GUIContent("Offset", "The position offset from the 'Follow Target'.");
        private GUIContent updateRateLabel = new GUIContent("Check Interval", "Interval in seconds between 'Follow Target' and volume distance checks. If set to 0, then the check will be performed every frame.");
        private GUIContent updateDistanceThresholdLabel = new GUIContent("Distance Threshold", 
            "How far the 'Follow Target' object must move from the center of the volume (including 'Offset') to update the volume's position. " +
            "Example: 0 - update every 'Check Interval', 0.5 - update, when the 'Follow Target' is halfway from the volume's center.");
        private Texture2D cover;

        private void OnEnable()
        {
            //Main Settings
            areaSize = serializedObject.FindProperty("areaSize");
            areaDepth = serializedObject.FindProperty("areaDepth");
            depthLayerMask = serializedObject.FindProperty("depthLayerMask");
            depthTextureFormat = serializedObject.FindProperty("depthTextureFormat");
            depthTextureResolution = serializedObject.FindProperty("depthTextureResolution");
            blurKernelSize = serializedObject.FindProperty("blurKernelSize");
            coverageAreaFalloffHardness = serializedObject.FindProperty("coverageAreaFalloffHardness");
            useFollowTarget = serializedObject.FindProperty("useFollowTarget");
            forcePositionUpdate = serializedObject.FindProperty("forcePositionUpdate");
            followTarget = serializedObject.FindProperty("followTarget");
            targetPositionOffsetY = serializedObject.FindProperty("targetPositionOffsetY");
            updateRate = serializedObject.FindProperty("updateRate");
            updateDistanceThreshold = serializedObject.FindProperty("updateDistanceThreshold");

            //Basic Settings
            coverage = serializedObject.FindProperty("coverage");
            coverageAmount = serializedObject.FindProperty("coverageAmount");
            paintableCoverage = serializedObject.FindProperty("paintableCoverage");
            useAveragedNormals = serializedObject.FindProperty("useAveragedNormals");
            stochastic = serializedObject.FindProperty("stochastic");

            coverageTex0 = serializedObject.FindProperty("coverageTex0");
            coverageTex1 = serializedObject.FindProperty("coverageTex1");
            coverageTex2 = serializedObject.FindProperty("coverageTex2");

            coverageColor = serializedObject.FindProperty("coverageColor");
            coverageColor1 = serializedObject.FindProperty("coverageColor1");
            coverageColor2 = serializedObject.FindProperty("coverageColor2");

            cov0Smoothness = serializedObject.FindProperty("cov0Smoothness");
            cov1Smoothness = serializedObject.FindProperty("cov1Smoothness");
            cov2Smoothness = serializedObject.FindProperty("cov2Smoothness");

            coverageNormalScale0 = serializedObject.FindProperty("coverageNormalScale0");
            coverageNormalScale1 = serializedObject.FindProperty("coverageNormalScale1");
            coverageNormalScale2 = serializedObject.FindProperty("coverageNormalScale2");
            
            coverageTiling = serializedObject.FindProperty("coverageTiling");
            coverageTiling1 = serializedObject.FindProperty("coverageTiling1");
            coverageTiling2 = serializedObject.FindProperty("coverageTiling2");

            covTriBlendContrast = serializedObject.FindProperty("covTriBlendContrast");
            emissionMasking = serializedObject.FindProperty("emissionMasking");
            coverageNormalsOverlay = serializedObject.FindProperty("coverageNormalsOverlay");
            //maskByAlpha = serializedObject.FindProperty("maskByAlpha");
            //baseCoverageNormalsBlend = serializedObject.FindProperty("baseCoverageNormalsBlend");

            //Detail Map
            useCoverageDetail = serializedObject.FindProperty("useCoverageDetail");
            coverageDetailTex = serializedObject.FindProperty("coverageDetailTex");
            detailTiling = serializedObject.FindProperty("detailTiling");
            detailTexRemap = serializedObject.FindProperty("detailTexRemap");
            detailNormalScale = serializedObject.FindProperty("detailNormalScale");
            detailDistance = serializedObject.FindProperty("detailDistance");

            //Area Mask
            coverageAreaBias = serializedObject.FindProperty("coverageAreaBias");
            coverageLeakReduction = serializedObject.FindProperty("coverageLeakReduction");
            coverageAreaMaskRange = serializedObject.FindProperty("coverageAreaMaskRange");
            precipitationDirOffset = serializedObject.FindProperty("precipitationDirOffset");
            precipitationDirRange = serializedObject.FindProperty("precipitationDirRange");

            //Tessellation
            tessellation = serializedObject.FindProperty("tessellation");
            tessEdgeL = serializedObject.FindProperty("tessEdgeL");
            tessFactorSnow = serializedObject.FindProperty("tessFactorSnow");
            tessMaxDisp = serializedObject.FindProperty("tessMaxDisp");
            tessSnowdriftRange = serializedObject.FindProperty("tessSnowdriftRange");

            //Displacement
            displacement = serializedObject.FindProperty("displacement");

            heightMap0Contrast = serializedObject.FindProperty("heightMap0Contrast");
            heightMap1Contrast = serializedObject.FindProperty("heightMap1Contrast");
            heightMap2Contrast = serializedObject.FindProperty("heightMap2Contrast");

            heightMap0LOD = serializedObject.FindProperty("heightMap0LOD");
            heightMap1LOD = serializedObject.FindProperty("heightMap1LOD");
            heightMap2LOD = serializedObject.FindProperty("heightMap2LOD");

            coverageDisplacement = serializedObject.FindProperty("coverageDisplacement");
            coverageDisplacement1 = serializedObject.FindProperty("coverageDisplacement1");
            coverageDisplacement2 = serializedObject.FindProperty("coverageDisplacement2");

            coverageDisplacementOffset = serializedObject.FindProperty("coverageDisplacementOffset");           

            //Traces
            traces = serializedObject.FindProperty("traces");

            tracesBaseBlend0 = serializedObject.FindProperty("tracesBaseBlend0");
            tracesBaseBlend1 = serializedObject.FindProperty("tracesBaseBlend1");
            tracesBaseBlend2 = serializedObject.FindProperty("tracesBaseBlend2");

            tracesNormalScale = serializedObject.FindProperty("tracesNormalScale");
            tracesNormalScale1 = serializedObject.FindProperty("tracesNormalScale1");
            tracesNormalScale2 = serializedObject.FindProperty("tracesNormalScale2");

            tracesColor = serializedObject.FindProperty("tracesColor");
            tracesColor1 = serializedObject.FindProperty("tracesColor1");
            tracesColor2 = serializedObject.FindProperty("tracesColor2");

            tracesColorBlendRange = serializedObject.FindProperty("tracesColorBlendRange");
            tracesColorBlendRange1 = serializedObject.FindProperty("tracesColorBlendRange1");
            tracesColorBlendRange2 = serializedObject.FindProperty("tracesColorBlendRange2");

            traceDetail = serializedObject.FindProperty("traceDetail");
            traceDetailTex = serializedObject.FindProperty("traceDetailTex");
            traceDetailTiling = serializedObject.FindProperty("traceDetailTiling");
            traceDetailNormalScale = serializedObject.FindProperty("traceDetailNormalScale");
            traceDetailIntensity = serializedObject.FindProperty("traceDetailIntensity");

            //Blend by Normals
            blendByNormalsStrength = serializedObject.FindProperty("blendByNormalsStrength");
            blendByNormalsPower = serializedObject.FindProperty("blendByNormalsPower");

            //Distance Fade
            distanceFadeStart = serializedObject.FindProperty("distanceFadeStart");
            distanceFadeFalloff = serializedObject.FindProperty("distanceFadeFalloff");

            //Sparkle
            sparkle = serializedObject.FindProperty("sparkle");
            sss = serializedObject.FindProperty("sss");
            sss_intensity = serializedObject.FindProperty("sss_intensity");
            colorEnhance = serializedObject.FindProperty("colorEnhance");

            enhanceRemap0 = serializedObject.FindProperty("enhanceRemap0");
            enhanceRemap1 = serializedObject.FindProperty("enhanceRemap1");
            enhanceRemap2 = serializedObject.FindProperty("enhanceRemap2");

            sssMaskRemap0 = serializedObject.FindProperty("sssMaskRemap0");
            sssMaskRemap1 = serializedObject.FindProperty("sssMaskRemap1");
            sssMaskRemap2 = serializedObject.FindProperty("sssMaskRemap2");

            sparkleTex = serializedObject.FindProperty("sparkleTex");
            sparklesAmount = serializedObject.FindProperty("sparklesAmount");
            sparkleDistFalloff = serializedObject.FindProperty("sparkleDistFalloff");
            sparkleTexSS = serializedObject.FindProperty("sparkleTexSS");
            sparkleTexLS = serializedObject.FindProperty("sparkleTexLS");
            localSparkleTiling = serializedObject.FindProperty("localSparkleTiling");
            screenSpaceSparklesTiling = serializedObject.FindProperty("screenSpaceSparklesTiling");
            sparklesBrightness = serializedObject.FindProperty("sparklesBrightness");
            sparklesLightmapMaskPower = serializedObject.FindProperty("sparklesLightmapMaskPower");
            sparklesHighlightMaskExpansion = serializedObject.FindProperty("sparklesHighlightMaskExpansion");

            highlightBrightness0 = serializedObject.FindProperty("highlightBrightness0");
            highlightBrightness1 = serializedObject.FindProperty("highlightBrightness1");
            highlightBrightness2 = serializedObject.FindProperty("highlightBrightness2");
            //-----------------------------

            texVSM = serializedObject.FindProperty("texVSM");
            depthTexture = serializedObject.FindProperty("depthTexture");
            depthCopyMtl = serializedObject.FindProperty("depthCopyMtl");
            texBlured = serializedObject.FindProperty("texBlured");
            texRGBA = serializedObject.FindProperty("texRGBA");

            depthTexProps = serializedObject.FindProperty("depthTexProps");
            surfaceProps = serializedObject.FindProperty("surfaceProps");

            basicSettingsFoldout = serializedObject.FindProperty("basicSettingsFoldout");
            detailMapFoldout = serializedObject.FindProperty("detailMapFoldout");
            areaMaskFoldout = serializedObject.FindProperty("areaMaskFoldout");
            tessellationFoldout = serializedObject.FindProperty("tessellationFoldout");
            displacementFoldout = serializedObject.FindProperty("displacementFoldout");
            tracesFoldout = serializedObject.FindProperty("tracesFoldout");
            blendByNormalFoldout = serializedObject.FindProperty("blendByNormalFoldout");
            distanceFadeFoldout = serializedObject.FindProperty("distanceFadeFoldout");
            sparklesFoldout = serializedObject.FindProperty("sparklesFoldout");

            foldout_basic_layer0 = serializedObject.FindProperty("foldout_basic_layer0");
            foldout_basic_layer1 = serializedObject.FindProperty("foldout_basic_layer1");
            foldout_basic_layer2 = serializedObject.FindProperty("foldout_basic_layer2");
            foldout_displacement_layer0 = serializedObject.FindProperty("foldout_displacement_layer0");
            foldout_displacement_layer1 = serializedObject.FindProperty("foldout_displacement_layer1");
            foldout_displacement_layer2 = serializedObject.FindProperty("foldout_displacement_layer2");
            foldout_traces_layer0 = serializedObject.FindProperty("foldout_traces_layer0");
            foldout_traces_layer1 = serializedObject.FindProperty("foldout_traces_layer1");
            foldout_traces_layer2 = serializedObject.FindProperty("foldout_traces_layer2");
            foldout_sss_layer0 = serializedObject.FindProperty("foldout_sss_layer0");
            foldout_sss_layer1 = serializedObject.FindProperty("foldout_sss_layer1");
            foldout_sss_layer2 = serializedObject.FindProperty("foldout_sss_layer2");

            cover = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/NOT_Lonely/Weatherade SRS/UI/SnowCover.png", typeof(Texture2D));

            SceneView.duringSceneGui += SceneViewHandles;
            Undo.undoRedoPerformed += OnUndoRedoCallback;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui += SceneViewHandles;
            Undo.undoRedoPerformed -= OnUndoRedoCallback;
        }

        void OnUndoRedoCallback()
        {
            if (snowCoverage != null) snowCoverage.ValidateValues();
        }

        public override void OnInspectorGUI()
        {
            snowCoverage = target as SnowCoverage;
            if (NL_Styles.lineB == null || NL_Styles.lineB.normal.background == null) NL_Styles.GetStyles();

            EditorGUI.BeginChangeCheck();

            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            float imageWidth = inspectorWidth - 40;
            float imageHeight = imageWidth * cover.height / cover.width;
            Rect rect = GUILayoutUtility.GetRect(imageWidth, imageHeight);
            GUI.DrawTexture(rect, cover, ScaleMode.ScaleToFit);

            NL_Utilities.DrawCenteredBoldHeader("WEATHERADE SNOW COVERAGE");

            float currentInspectorWidth = EditorGUIUtility.currentViewWidth - 24;
            float offset = currentInspectorWidth - (currentInspectorWidth / 1.6f);

            if (NL_Utilities.DrawFoldout(depthTexProps, "MAIN SETTINGS"))
            {
                GUILayout.BeginVertical(NL_Styles.lineA);
                EditorGUILayout.PropertyField(areaSize, areaSizeLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(NL_Styles.lineB);
                EditorGUILayout.PropertyField(areaDepth, areaDepthLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(NL_Styles.lineA);
                EditorGUILayout.PropertyField(depthLayerMask, depthLayerMaskLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(NL_Styles.lineB);
                EditorGUILayout.PropertyField(depthTextureFormat, depthTextureFormatLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(NL_Styles.lineA);
                EditorGUILayout.PropertyField(depthTextureResolution, depthTextureResolutionLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(NL_Styles.lineB);
                EditorGUILayout.PropertyField(blurKernelSize, blurKernelSizeLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(NL_Styles.lineA);
                EditorGUILayout.PropertyField(coverageAreaFalloffHardness, coverageAreaFalloffHardnessLabel);
                GUILayout.EndVertical();

                //Follow target
                GUILayout.BeginHorizontal(NL_Styles.lineB);
                EditorGUILayout.PropertyField(useFollowTarget, useFollowTargetLabel);
                EditorGUILayout.PropertyField(followTarget, new GUIContent());
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical(NL_Styles.lineB);
                if (useFollowTarget.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(targetPositionOffsetY, targetPosOffsetYLabel);
                    EditorGUILayout.PropertyField(forcePositionUpdate, forcePositionUpdateLabel);
                    EditorGUILayout.PropertyField(updateRate, updateRateLabel);
                    EditorGUILayout.PropertyField(updateDistanceThreshold, updateDistanceThresholdLabel);
                    EditorGUI.indentLevel--;
                }

                GUILayout.EndVertical();

                GUILayout.Space(1);
                if (GUILayout.Button(new GUIContent("Update", "Press this button if you changed one of the following properties: 'Affected Layers', 'Depth Texture Format', 'Depth Texture Resolution', 'Blur Kernel Size'.")))
                {
                    snowCoverage.Init();
                    EditorUtility.SetDirty(snowCoverage);
                }

                GUILayout.Space(10);
            }

            GUILayout.Space(1);

            if (NL_Utilities.DrawFoldout(surfaceProps, "GLOBAL SURFACE SETTINGS"))
            {
                GUILayout.BeginHorizontal(NL_Styles.lineB);
                EditorGUILayout.PropertyField(coverage, NL_Styles.coverageText);
                GUILayout.EndHorizontal();

                NL_Utilities.BeginUICategory("BASIC SETTINGS", NL_Styles.lineB, NL_Styles.foldoutSub, basicSettingsFoldout);
                if (basicSettingsFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(paintableCoverage, NL_Styles.paintableCoverageText);
                    EditorGUILayout.PropertyField(useAveragedNormals, NL_Styles.useAveragedNormalsText);
                    EditorGUILayout.PropertyField(stochastic, NL_Styles.stochasticText);

                    NL_Utilities.EndUICategory(2);
                    GUILayout.BeginVertical(NL_Styles.lineA);
                    NL_Utilities.BeginUICategory("Layer 0", NL_Styles.lineA, null, foldout_basic_layer0);
                    if (foldout_basic_layer0.boolValue)
                    {
                        EditorGUILayout.PropertyField(coverageTex0, NL_Styles.coverageTex0Text);
                        EditorGUILayout.PropertyField(coverageTiling, NL_Styles.coverageTilingText);
                        EditorGUILayout.PropertyField(coverageColor, NL_Styles.coverageColorText);
                        cov0Smoothness.vector2Value = DrawRangeSlider(cov0Smoothness.vector2Value, NL_Styles.cov0SmoothnessText, offset);
                        EditorGUILayout.PropertyField(coverageNormalScale0, NL_Styles.coverageNormalScale0Text);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 1", NL_Styles.lineA, null, foldout_basic_layer1);
                    if (foldout_basic_layer1.boolValue)
                    {
                        EditorGUILayout.PropertyField(coverageTex1, NL_Styles.coverageTex0Text);
                        EditorGUILayout.PropertyField(coverageTiling1, NL_Styles.coverageTilingText);
                        EditorGUILayout.PropertyField(coverageColor1, NL_Styles.coverageColorText);
                        cov1Smoothness.vector2Value = DrawRangeSlider(cov1Smoothness.vector2Value, NL_Styles.cov0SmoothnessText, offset);
                        EditorGUILayout.PropertyField(coverageNormalScale1, NL_Styles.coverageNormalScale0Text);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 2", NL_Styles.lineA, null, foldout_basic_layer2);
                    if (foldout_basic_layer2.boolValue)
                    {
                        EditorGUILayout.PropertyField(coverageTex2, NL_Styles.coverageTex0Text);
                        EditorGUILayout.PropertyField(coverageTiling2, NL_Styles.coverageTilingText);
                        EditorGUILayout.PropertyField(coverageColor2, NL_Styles.coverageColorText);
                        cov2Smoothness.vector2Value = DrawRangeSlider(cov2Smoothness.vector2Value, NL_Styles.cov0SmoothnessText, offset);
                        EditorGUILayout.PropertyField(coverageNormalScale2, NL_Styles.coverageNormalScale0Text);
                    }
                    NL_Utilities.EndUICategory(2);

                    GUILayout.BeginVertical(NL_Styles.lineB);
                    EditorGUILayout.PropertyField(coverageAmount, NL_Styles.coverageAmountText);
                    EditorGUILayout.PropertyField(covTriBlendContrast, NL_Styles.covTriBlendContrastText);
                    EditorGUILayout.PropertyField(emissionMasking, NL_Styles.emissionMaskingText);
                    //EditorGUILayout.PropertyField(maskByAlpha, NL_Styles.maskCoverageByAlphaText);
                    //EditorGUILayout.PropertyField(baseCoverageNormalsBlend, NL_Styles.baseCoverageNormalsBlendText);
                    EditorGUILayout.PropertyField(coverageNormalsOverlay, NL_Styles.coverageNormalsOverlayText);
                    GUILayout.EndVertical();
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("DETAIL MAP", NL_Styles.lineB, NL_Styles.foldoutSub, detailMapFoldout);
                if (detailMapFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(useCoverageDetail, NL_Styles.useCoverageDetailText);
                    EditorGUILayout.PropertyField(coverageDetailTex, NL_Styles.coverageDetailTexText);
                    EditorGUILayout.PropertyField(detailTiling, NL_Styles.detailTilingText);
                    detailTexRemap.vector2Value = DrawRangeSlider(detailTexRemap.vector2Value, NL_Styles.detailTexRemapText, offset);
                    EditorGUILayout.PropertyField(detailNormalScale, NL_Styles.detailNormalScaleText);
                    EditorGUILayout.PropertyField(detailDistance, NL_Styles.detailDistanceText);
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("AREA MASK", NL_Styles.lineB, NL_Styles.foldoutSub, areaMaskFoldout);
                if (areaMaskFoldout.boolValue)
                {
                    //coverageAreaMaskRange.vector2Value = DrawRangeSlider(coverageAreaMaskRange.vector2Value, NL_Styles.coverageAreaMaskRangeText, offset);
                    EditorGUILayout.PropertyField(coverageAreaMaskRange, NL_Styles.coverageAreaMaskRangeText);
                    EditorGUILayout.PropertyField(coverageAreaBias, NL_Styles.coverageAreaBiasText);
                    EditorGUILayout.PropertyField(coverageLeakReduction, NL_Styles.coverageLeakReductionText);
                    EditorGUILayout.PropertyField(precipitationDirOffset, NL_Styles.precipitationDirOffsetText);
                    precipitationDirRange.vector2Value = DrawRangeSlider(precipitationDirRange.vector2Value, NL_Styles.precipitationDirRangeText, offset);
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("TESSELLATION", NL_Styles.lineB, NL_Styles.foldoutSub, tessellationFoldout);
                if (tessellationFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(tessellation, NL_Styles.tessellationText);
                    EditorGUILayout.PropertyField(tessEdgeL, NL_Styles.tessEdgeLText);
                    EditorGUILayout.PropertyField(tessFactorSnow, NL_Styles.tessFactorSnowText);
                    tessSnowdriftRange.vector2Value = DrawRangeSlider(tessSnowdriftRange.vector2Value, NL_Styles.tessSnowdriftRangeText, offset);
                    EditorGUILayout.PropertyField(tessMaxDisp, NL_Styles.tessMaxDispText);
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("DISPLACEMENT", NL_Styles.lineB, NL_Styles.foldoutSub, displacementFoldout);
                if (displacementFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(displacement, NL_Styles.displacementText);

                    NL_Utilities.EndUICategory(2);
                    GUILayout.BeginVertical(NL_Styles.lineA);
                    NL_Utilities.BeginUICategory("Layer 0", NL_Styles.lineA, null, foldout_displacement_layer0);
                    if (foldout_displacement_layer0.boolValue)
                    {
                        heightMap0Contrast.vector2Value = DrawRangeSlider(heightMap0Contrast.vector2Value, NL_Styles.heightMap0ContrastText, offset);
                        EditorGUILayout.PropertyField(heightMap0LOD, NL_Styles.heightMap0LODText);
                        EditorGUILayout.PropertyField(coverageDisplacement, NL_Styles.coverageDisplacementText);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 1", NL_Styles.lineA, null, foldout_displacement_layer1);
                    if (foldout_displacement_layer1.boolValue)
                    {
                        heightMap1Contrast.vector2Value = DrawRangeSlider(heightMap1Contrast.vector2Value, NL_Styles.heightMap0ContrastText, offset);
                        EditorGUILayout.PropertyField(heightMap1LOD, NL_Styles.heightMap0LODText);
                        EditorGUILayout.PropertyField(coverageDisplacement1, NL_Styles.coverageDisplacementText);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 2", NL_Styles.lineA, null, foldout_displacement_layer2);
                    if (foldout_displacement_layer2.boolValue)
                    {
                        heightMap2Contrast.vector2Value = DrawRangeSlider(heightMap2Contrast.vector2Value, NL_Styles.heightMap0ContrastText, offset);
                        EditorGUILayout.PropertyField(heightMap2LOD, NL_Styles.heightMap0LODText);
                        EditorGUILayout.PropertyField(coverageDisplacement2, NL_Styles.coverageDisplacementText);
                    }
                    NL_Utilities.EndUICategory(2);

                    GUILayout.BeginVertical(NL_Styles.lineB);
                    EditorGUILayout.PropertyField(coverageDisplacementOffset, NL_Styles.coverageDisplacementOffsetText);
                    GUILayout.EndVertical();
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("TRACES", NL_Styles.lineB, NL_Styles.foldoutSub, tracesFoldout);
                if (tracesFoldout.boolValue)
                {
                    if (!snowCoverage.hasTracesComponent)
                    {
                        EditorGUILayout.HelpBox("There's no SRS_TracesRenderer component on this gameobject. Please add one to use this feature.", MessageType.Warning);
                        if(GUILayout.Button("Add Now")) snowCoverage.gameObject.AddComponent<SRS_TraceMaskGenerator>();
                    }
                    EditorGUILayout.PropertyField(traces, NL_Styles.tracesText);

                    NL_Utilities.EndUICategory(2);
                    GUILayout.BeginVertical(NL_Styles.lineA);
                    NL_Utilities.BeginUICategory("Layer 0", NL_Styles.lineA, null, foldout_traces_layer0);
                    if (foldout_traces_layer0.boolValue)
                    {
                        EditorGUILayout.PropertyField(tracesBaseBlend0, NL_Styles.tracesBaseBlend0Text);
                        EditorGUILayout.PropertyField(tracesColor, NL_Styles.tracesColorText);
                        EditorGUI.indentLevel++;
                        tracesColorBlendRange.vector2Value = DrawRangeSlider(tracesColorBlendRange.vector2Value, NL_Styles.tracesColorBlendRangeText, offset);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.PropertyField(tracesNormalScale, NL_Styles.tracesNormalScaleText);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 1", NL_Styles.lineA, null, foldout_traces_layer1);
                    if (foldout_traces_layer1.boolValue)
                    {
                        EditorGUILayout.PropertyField(tracesBaseBlend1, NL_Styles.tracesBaseBlend0Text);
                        EditorGUILayout.PropertyField(tracesColor1, NL_Styles.tracesColorText);
                        EditorGUI.indentLevel++;
                        tracesColorBlendRange1.vector2Value = DrawRangeSlider(tracesColorBlendRange1.vector2Value, NL_Styles.tracesColorBlendRangeText, offset);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.PropertyField(tracesNormalScale1, NL_Styles.tracesNormalScaleText);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 2", NL_Styles.lineA, null, foldout_traces_layer2);
                    if (foldout_traces_layer2.boolValue)
                    {
                        EditorGUILayout.PropertyField(tracesBaseBlend2, NL_Styles.tracesBaseBlend0Text);
                        EditorGUILayout.PropertyField(tracesColor2, NL_Styles.tracesColorText);
                        EditorGUI.indentLevel++;
                        tracesColorBlendRange2.vector2Value = DrawRangeSlider(tracesColorBlendRange2.vector2Value, NL_Styles.tracesColorBlendRangeText, offset);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.PropertyField(tracesNormalScale2, NL_Styles.tracesNormalScaleText);
                    }
                    NL_Utilities.EndUICategory(2);

                    GUILayout.BeginVertical(NL_Styles.lineB);
                    EditorGUILayout.PropertyField(traceDetail, NL_Styles.traceDetailText);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(traceDetailTex, new GUIContent("Detail Texture"));
                    EditorGUILayout.PropertyField(traceDetailTiling, new GUIContent("Tiling"));
                    EditorGUILayout.PropertyField(traceDetailNormalScale, new GUIContent("Normal Scale"));
                    EditorGUILayout.PropertyField(traceDetailIntensity, new GUIContent("Details Intensity"));
                    EditorGUI.indentLevel--;
                    GUILayout.EndVertical();
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("BLEND BY NORMALS", NL_Styles.lineB, NL_Styles.foldoutSub, blendByNormalFoldout);
                if (blendByNormalFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(blendByNormalsStrength, NL_Styles.blendByNormalsStrengthText);
                    EditorGUILayout.PropertyField(blendByNormalsPower, NL_Styles.blendByNormalsPowerText);
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("DISTANCE FADE", NL_Styles.lineB, NL_Styles.foldoutSub, distanceFadeFoldout);
                if (distanceFadeFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(distanceFadeStart, NL_Styles.distanceFadeStartText);
                    EditorGUILayout.PropertyField(distanceFadeFalloff, NL_Styles.distanceFadeFalloffText);
                }
                NL_Utilities.EndUICategory();

                NL_Utilities.BeginUICategory("SPARKLE AND SSS", NL_Styles.lineB, NL_Styles.foldoutSub, sparklesFoldout);
                if (sparklesFoldout.boolValue)
                {
                    EditorGUILayout.PropertyField(sss, NL_Styles.sssText);
                    EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(sss_intensity, NL_Styles.sss_intensityText);
                    EditorGUI.indentLevel--;

                    EditorGUILayout.PropertyField(sparkle, NL_Styles.sparkleText);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(sparkleTex, NL_Styles.sparkleTexText);
                    EditorGUILayout.PropertyField(sparklesAmount, NL_Styles.sparklesAmountText);
                    EditorGUILayout.PropertyField(sparklesBrightness, NL_Styles.sparklesBrightnessText);
                    EditorGUILayout.PropertyField(sparkleDistFalloff, NL_Styles.sparkleDistFalloffText);
                    if (sparkleTex.objectReferenceValue == null && sparkleTexLS.enumValueIndex == 1)
                        EditorGUILayout.HelpBox("'Sparkle Mask' texure is not provided. Set it or consider using 'Main Coverage Tex Alpha'.", MessageType.Warning);
                    EditorGUILayout.PropertyField(sparkleTexLS, NL_Styles.sparkleTexLSText);
                    EditorGUI.BeginDisabledGroup(sparkleTexLS.enumValueIndex == 0);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(localSparkleTiling, NL_Styles.localSparkleTilingText);
                    EditorGUI.indentLevel--;
                    EditorGUI.EndDisabledGroup();
                    if (sparkleTex.objectReferenceValue == null && sparkleTexSS.enumValueIndex == 1)
                        EditorGUILayout.HelpBox("'Sparkle Mask' texure is not provided. Set it or consider using 'Main Coverage Tex Alpha'.", MessageType.Warning);
                    EditorGUILayout.PropertyField(sparkleTexSS, NL_Styles.sparkleTexSSText);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(screenSpaceSparklesTiling, NL_Styles.screenSpaceSparklesTilingText);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.PropertyField(sparklesHighlightMaskExpansion, NL_Styles.sparkleHighlightMaskExpText);
                    EditorGUILayout.PropertyField(sparklesLightmapMaskPower, NL_Styles.sparkleLightmapMaskPowText);
                    EditorGUI.indentLevel--;

                    EditorGUILayout.PropertyField(colorEnhance, NL_Styles.colorEnhanceText);

                    NL_Utilities.EndUICategory(2);
                    GUILayout.BeginVertical(NL_Styles.lineA);
                    NL_Utilities.BeginUICategory("Layer 0", NL_Styles.lineA, null, foldout_sss_layer0);
                    if (foldout_sss_layer0.boolValue)
                    {
                        EditorGUILayout.PropertyField(highlightBrightness0, NL_Styles.highlightBrightness0Text);
                        sssMaskRemap0.vector2Value = DrawRangeSlider(sssMaskRemap0.vector2Value, NL_Styles.sssMaskRemap0Text, offset);
                        enhanceRemap0.vector2Value = DrawRangeSlider(enhanceRemap0.vector2Value, NL_Styles.enhanceRemap0Text, offset);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 1", NL_Styles.lineA, null, foldout_sss_layer1);
                    if (foldout_sss_layer1.boolValue)
                    {
                        EditorGUILayout.PropertyField(highlightBrightness1, NL_Styles.highlightBrightness0Text);
                        sssMaskRemap1.vector2Value = DrawRangeSlider(sssMaskRemap1.vector2Value, NL_Styles.sssMaskRemap0Text, offset);
                        enhanceRemap1.vector2Value = DrawRangeSlider(enhanceRemap1.vector2Value, NL_Styles.enhanceRemap0Text, offset);
                    }

                    NL_Utilities.EndUICategory(2);
                    NL_Utilities.BeginUICategory("Layer 2", NL_Styles.lineA, null, foldout_sss_layer2);
                    if (foldout_sss_layer2.boolValue)
                    {
                        EditorGUILayout.PropertyField(highlightBrightness2, NL_Styles.highlightBrightness0Text);
                        sssMaskRemap2.vector2Value = DrawRangeSlider(sssMaskRemap2.vector2Value, NL_Styles.sssMaskRemap0Text, offset);
                        enhanceRemap2.vector2Value = DrawRangeSlider(enhanceRemap2.vector2Value, NL_Styles.enhanceRemap0Text, offset);
                    }
                    NL_Utilities.EndUICategory(0);
                }
                NL_Utilities.EndUICategory();
            }

            Undo.RecordObject(snowCoverage, "Weatherade Coverage Value Changed");

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                snowCoverage.ValidateValues();

                snowCoverage.CalculateTargetPosOffsetsXZ();

                if (!Application.isPlaying)
                {
                    if (followTarget.objectReferenceValue != null && (followTarget.objectReferenceValue != lastFollowTarget || snowCoverage.targetPosOffset != lastOffset)) snowCoverage.UpdateSRSPosition();

                    lastFollowTarget = followTarget.objectReferenceValue as Transform;
                    lastOffset = snowCoverage.targetPosOffset;
                }
            }
        }

        private Vector2 DrawRangeSlider(Vector2 sliderMinMax, GUIContent label, float labelOffset, float minLimit = 0, float maxLimit = 1)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            GUILayout.Space(2);
            sliderMinMax.x = EditorGUILayout.FloatField(sliderMinMax.x, GUILayout.MaxWidth(50));
            EditorGUILayout.MinMaxSlider(ref sliderMinMax.x, ref sliderMinMax.y, minLimit, maxLimit);
            sliderMinMax.y = EditorGUILayout.FloatField(sliderMinMax.y, GUILayout.MaxWidth(50));

            GUILayout.EndHorizontal();

            return sliderMinMax;
        }

        private Vector3 lastOffset;
        private Transform lastFollowTarget;
        private void SceneViewHandles(SceneView sceneView)
        {


            //snowCoverage.UpdateSRSPosition();
        }

        private void OnSceneGUI()
        {
            if (snowCoverage == null || !snowCoverage.enabled || !Selection.Contains(snowCoverage.gameObject)) return;

        }

    }
}
#endif
