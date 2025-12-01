using NOT_Lonely.TotalBrush;
using NOT_Lonely.Weatherade;
using NOT_Lonely.Weatherade.ShaderGUI;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class SnowShadersGUI : ShaderGUI
{
    public MaterialEditor materialEditor;
    public Material material;
    public MaterialProperty[] props;
    public bool m_inspectorInitiated = false;
    public float currentInspectorWidth;

    #region Foldouts
    private CommonGUI.Foldout foldout_standardShaderProps = new CommonGUI.Foldout() { saveName = nameof(foldout_standardShaderProps) };
    private CommonGUI.Foldout foldout_coverageProps = new CommonGUI.Foldout() { saveName = nameof(foldout_coverageProps) };
    private CommonGUI.Foldout foldout_basicSettings = new CommonGUI.Foldout() { saveName = nameof(foldout_basicSettings) };
    private CommonGUI.Foldout foldout_detailMap = new CommonGUI.Foldout() { saveName = nameof(foldout_detailMap) };
    private CommonGUI.Foldout foldout_areaMask = new CommonGUI.Foldout() { saveName = nameof(foldout_areaMask) };
    private CommonGUI.Foldout foldout_tessellation = new CommonGUI.Foldout() { saveName = nameof(foldout_tessellation) };
    private CommonGUI.Foldout foldout_displacement = new CommonGUI.Foldout() { saveName = nameof(foldout_displacement) };
    private CommonGUI.Foldout foldout_traces = new CommonGUI.Foldout() { saveName = nameof(foldout_traces) };
    private CommonGUI.Foldout foldout_blendByNormals = new CommonGUI.Foldout() { saveName = nameof(foldout_blendByNormals) };
    private CommonGUI.Foldout foldout_distanceFade = new CommonGUI.Foldout() { saveName = nameof(foldout_distanceFade) };
    private CommonGUI.Foldout foldout_sparkleAndSss = new CommonGUI.Foldout() { saveName = nameof(foldout_sparkleAndSss) };

    private CommonGUI.Foldout foldout_basic_layer0 = new CommonGUI.Foldout() { saveName = nameof(foldout_basic_layer0) };
    private CommonGUI.Foldout foldout_basic_layer1 = new CommonGUI.Foldout() { saveName = nameof(foldout_basic_layer1) };
    private CommonGUI.Foldout foldout_basic_layer2 = new CommonGUI.Foldout() { saveName = nameof(foldout_basic_layer2) };
    private CommonGUI.Foldout foldout_displacement_layer0 = new CommonGUI.Foldout() { saveName = nameof(foldout_displacement_layer0) };
    private CommonGUI.Foldout foldout_displacement_layer1 = new CommonGUI.Foldout() { saveName = nameof(foldout_displacement_layer1) };
    private CommonGUI.Foldout foldout_displacement_layer2 = new CommonGUI.Foldout() { saveName = nameof(foldout_displacement_layer2) };

    private CommonGUI.Foldout foldout_traces_layer0 = new CommonGUI.Foldout() { saveName = nameof(foldout_traces_layer0) };
    private CommonGUI.Foldout foldout_traces_layer1 = new CommonGUI.Foldout() { saveName = nameof(foldout_traces_layer1) };
    private CommonGUI.Foldout foldout_traces_layer2 = new CommonGUI.Foldout() { saveName = nameof(foldout_traces_layer2) };
    private CommonGUI.Foldout foldout_sss_layer0 = new CommonGUI.Foldout() { saveName = nameof(foldout_sss_layer0) };
    private CommonGUI.Foldout foldout_sss_layer1 = new CommonGUI.Foldout() { saveName = nameof(foldout_sss_layer1) };
    private CommonGUI.Foldout foldout_sss_layer2 = new CommonGUI.Foldout() { saveName = nameof(foldout_sss_layer2) };
    #endregion

    public enum SurfaceType
    {
        Mesh,
        Terrain
    }

    public SurfaceType surfaceType = SurfaceType.Mesh;

    #region CoverageProperties

    //Common
    public CommonGUI.ToggleOverridable coverage = new CommonGUI.ToggleOverridable() { propName = "_Coverage", keywordName = "_COVERAGE_ON" };
    public CommonGUI.ToggleOverridable stochastic = new CommonGUI.ToggleOverridable() { propName = "_Stochastic", keywordName = "_STOCHASTIC_ON" };
    public CommonGUI.ToggleOverridable useAveragedNormals = new CommonGUI.ToggleOverridable() { propName = "_UseAveragedNormals", keywordName = "_USE_AVERAGED_NORMALS" };
    public CommonGUI.ToggleOverridable paintableCoverage = new CommonGUI.ToggleOverridable() { propName = "_PaintableCoverage", keywordName = "_PAINTABLE_COVERAGE_ON" };
    public CommonGUI.FloatOverridable distanceFadeStart = new CommonGUI.FloatOverridable() { propName = "_DistanceFadeStart" };
    public CommonGUI.FloatOverridable distanceFadeFalloff = new CommonGUI.FloatOverridable() { propName = "_DistanceFadeFalloff" };
    public CommonGUI.FloatOverridable coverageAreaBias = new CommonGUI.FloatOverridable() { propName = "_CoverageAreaBias" };
    public CommonGUI.FloatOverridable coverageLeakReduction = new CommonGUI.FloatOverridable() { propName = "_CoverageLeakReduction" };
    public CommonGUI.FloatOverridable coverageAreaMaskRange = new CommonGUI.FloatOverridable() { propName = "_CoverageAreaMaskRange" };
    public CommonGUI.FloatOverridable precipitationDirOffset = new CommonGUI.FloatOverridable() { propName = "_PrecipitationDirOffset" };
    public CommonGUI.Vector2DOverridable precipitationDirRange = new CommonGUI.Vector2DOverridable() { propName = "_PrecipitationDirRange" };
    public CommonGUI.FloatOverridable blendByNormalsStrength = new CommonGUI.FloatOverridable() { propName = "_BlendByNormalsStrength" };
    public CommonGUI.FloatOverridable blendByNormalsPower = new CommonGUI.FloatOverridable() { propName = "_BlendByNormalsPower" };
    //

    //Basic Settings
    public CommonGUI.ToggleOverridable threeTexMode = new CommonGUI.ToggleOverridable() { propName = "_ThreeTexMode", keywordName = "_THREE_TEX_MODE" };
    public CommonGUI.FloatOverridable coverageAmount = new CommonGUI.FloatOverridable() { propName = "_CoverageAmount" };
    public CommonGUI.FloatOverridable covTriBlendContrast = new CommonGUI.FloatOverridable() { propName = "_CovTriBlendContrast" };
    public CommonGUI.TextureOverridable coverageTex0 = new CommonGUI.TextureOverridable() { propName = "_CoverageTex0" };
    public CommonGUI.TextureOverridable coverageTex1 = new CommonGUI.TextureOverridable() { propName = "_CoverageTex1" };
    public CommonGUI.TextureOverridable coverageTex2 = new CommonGUI.TextureOverridable() { propName = "_CoverageTex2" };
    public CommonGUI.FloatOverridable coverageTiling = new CommonGUI.FloatOverridable() { propName = "_CoverageTiling" };
    public CommonGUI.FloatOverridable coverageTiling1 = new CommonGUI.FloatOverridable() { propName = "_CoverageTiling1" };
    public CommonGUI.FloatOverridable coverageTiling2 = new CommonGUI.FloatOverridable() { propName = "_CoverageTiling2" };
    public CommonGUI.ColorOverridable coverageColor = new CommonGUI.ColorOverridable() { propName = "_CoverageColor" };
    public CommonGUI.ColorOverridable coverageColor1 = new CommonGUI.ColorOverridable() { propName = "_CoverageColor1" };
    public CommonGUI.ColorOverridable coverageColor2 = new CommonGUI.ColorOverridable() { propName = "_CoverageColor2" };
    public CommonGUI.Vector2DOverridable cov0Smoothness = new CommonGUI.Vector2DOverridable() { propName = "_Cov0Smoothness" };
    public CommonGUI.Vector2DOverridable cov1Smoothness = new CommonGUI.Vector2DOverridable() { propName = "_Cov1Smoothness" };
    public CommonGUI.Vector2DOverridable cov2Smoothness = new CommonGUI.Vector2DOverridable() { propName = "_Cov2Smoothness" };
    public CommonGUI.FloatOverridable coverageNormalScale0 = new CommonGUI.FloatOverridable() { propName = "_CoverageNormalScale0" };
    public CommonGUI.FloatOverridable coverageNormalScale1 = new CommonGUI.FloatOverridable() { propName = "_CoverageNormalScale1" };
    public CommonGUI.FloatOverridable coverageNormalScale2 = new CommonGUI.FloatOverridable() { propName = "_CoverageNormalScale2" };

    public CommonGUI.FloatOverridable coverageNormalsOverlay = new CommonGUI.FloatOverridable() { propName = "_CoverageNormalsOverlay" };

    //Coverage detail
    public CommonGUI.ToggleOverridable useCoverageDetail = new CommonGUI.ToggleOverridable() { propName = "_UseCoverageDetail", keywordName = "_USE_COVERAGE_DETAIL" };
    public CommonGUI.TextureOverridable coverageDetailTex = new CommonGUI.TextureOverridable() { propName = "_CoverageDetailTex" };
    public CommonGUI.FloatOverridable detailTiling = new CommonGUI.FloatOverridable() { propName = "_DetailTiling" };
    public CommonGUI.FloatOverridable detailDistance = new CommonGUI.FloatOverridable() { propName = "_DetailDistance" };
    public CommonGUI.Vector2DOverridable detailTexRemap = new CommonGUI.Vector2DOverridable() { propName = "_DetailTexRemap" };
    public CommonGUI.FloatOverridable detailNormalScale = new CommonGUI.FloatOverridable() { propName = "_DetailNormalScale" };

    //Traces
    public CommonGUI.ToggleOverridable traces = new CommonGUI.ToggleOverridable() { propName = "_Traces", keywordName = "_TRACES_ON" };
    public CommonGUI.ToggleOverridable traceDetail = new CommonGUI.ToggleOverridable() { propName = "_TraceDetail", keywordName = "_TRACE_DETAIL" };
    public CommonGUI.ColorOverridable tracesColor = new CommonGUI.ColorOverridable() { propName = "_TracesColor" };
    public CommonGUI.ColorOverridable tracesColor1 = new CommonGUI.ColorOverridable() { propName = "_TracesColor1" };
    public CommonGUI.ColorOverridable tracesColor2 = new CommonGUI.ColorOverridable() { propName = "_TracesColor2" };
    public CommonGUI.FloatOverridable tracesBaseBlend0 = new CommonGUI.FloatOverridable() { propName = "_TracesBaseBlend0" };
    public CommonGUI.FloatOverridable tracesBaseBlend1 = new CommonGUI.FloatOverridable() { propName = "_TracesBaseBlend1" };
    public CommonGUI.FloatOverridable tracesBaseBlend2 = new CommonGUI.FloatOverridable() { propName = "_TracesBaseBlend2" };
    public CommonGUI.Vector2DOverridable tracesColorBlendRange = new CommonGUI.Vector2DOverridable() { propName = "_TracesColorBlendRange" };
    public CommonGUI.Vector2DOverridable tracesColorBlendRange1 = new CommonGUI.Vector2DOverridable() { propName = "_TracesColorBlendRange1" };
    public CommonGUI.Vector2DOverridable tracesColorBlendRange2 = new CommonGUI.Vector2DOverridable() { propName = "_TracesColorBlendRange2" };
    public CommonGUI.FloatOverridable tracesNormalScale = new CommonGUI.FloatOverridable() { propName = "_TracesNormalScale" };
    public CommonGUI.FloatOverridable tracesNormalScale1 = new CommonGUI.FloatOverridable() { propName = "_TracesNormalScale1" };
    public CommonGUI.FloatOverridable tracesNormalScale2 = new CommonGUI.FloatOverridable() { propName = "_TracesNormalScale2" };
    public CommonGUI.FloatOverridable tracesBlendFactor = new CommonGUI.FloatOverridable() { propName = "_TracesBlendFactor" };

    //Tessellation
    public CommonGUI.ToggleOverridable tessellation = new CommonGUI.ToggleOverridable() { propName = "_Tessellation" };
    public CommonGUI.FloatOverridable tessEdgeL = new CommonGUI.FloatOverridable() { propName = "_TessEdgeL" };
    public CommonGUI.FloatOverridable tessFactorSnow = new CommonGUI.FloatOverridable() { propName = "_TessFactorSnow" };
    public CommonGUI.Vector2DOverridable tessSnowdriftRange = new CommonGUI.Vector2DOverridable() { propName = "_TessSnowdriftRange" };
    public CommonGUI.FloatOverridable tessMaxDisp = new CommonGUI.FloatOverridable() { propName = "_TessMaxDisp" };

    //Displacement
    public CommonGUI.ToggleOverridable displacement = new CommonGUI.ToggleOverridable() { propName = "_Displacement", keywordName = "_DISPLACEMENT_ON" };
    public CommonGUI.FloatOverridable coverageDisplacement = new CommonGUI.FloatOverridable() { propName = "_CoverageDisplacement" };
    public CommonGUI.FloatOverridable coverageDisplacement1 = new CommonGUI.FloatOverridable() { propName = "_CoverageDisplacement1" };
    public CommonGUI.FloatOverridable coverageDisplacement2 = new CommonGUI.FloatOverridable() { propName = "_CoverageDisplacement2" };
    public CommonGUI.FloatOverridable coverageDisplacementOffset = new CommonGUI.FloatOverridable() { propName = "_CoverageDisplacementOffset" };
    public CommonGUI.Vector2DOverridable heightMap0Contrast = new CommonGUI.Vector2DOverridable() { propName = "_HeightMap0Contrast" };
    public CommonGUI.Vector2DOverridable heightMap1Contrast = new CommonGUI.Vector2DOverridable() { propName = "_HeightMap1Contrast" };
    public CommonGUI.Vector2DOverridable heightMap2Contrast = new CommonGUI.Vector2DOverridable() { propName = "_HeightMap2Contrast" };
    public CommonGUI.FloatOverridable heightMap0LOD = new CommonGUI.FloatOverridable() { propName = "_HeightMap0LOD" };
    public CommonGUI.FloatOverridable heightMap1LOD = new CommonGUI.FloatOverridable() { propName = "_HeightMap1LOD" };
    public CommonGUI.FloatOverridable heightMap2LOD = new CommonGUI.FloatOverridable() { propName = "_HeightMap2LOD" };

    //Sparkle
    public CommonGUI.ToggleOverridable sparkle = new CommonGUI.ToggleOverridable() { propName = "_Sparkle", keywordName = "_SPARKLE_ON" };
    public CommonGUI.ToggleOverridable sss = new CommonGUI.ToggleOverridable() { propName = "_Sss", keywordName = "_SSS_ON" };
    public CommonGUI.Vector2DOverridable sssMaskRemap0 = new CommonGUI.Vector2DOverridable() { propName = "_SssMaskRemap0" };
    public CommonGUI.Vector2DOverridable sssMaskRemap1 = new CommonGUI.Vector2DOverridable() { propName = "_SssMaskRemap1" };
    public CommonGUI.Vector2DOverridable sssMaskRemap2 = new CommonGUI.Vector2DOverridable() { propName = "_SssMaskRemap2" };
    public CommonGUI.FloatOverridable colorEnhance = new CommonGUI.FloatOverridable() { propName = "_ColorEnhance" };
    public CommonGUI.Vector2DOverridable enhanceRemap0 = new CommonGUI.Vector2DOverridable() { propName = "_EnhanceRemap0" };
    public CommonGUI.Vector2DOverridable enhanceRemap1 = new CommonGUI.Vector2DOverridable() { propName = "_EnhanceRemap1" };
    public CommonGUI.Vector2DOverridable enhanceRemap2 = new CommonGUI.Vector2DOverridable() { propName = "_EnhanceRemap2" };
    public CommonGUI.FloatOverridable highlightBrightness0 = new CommonGUI.FloatOverridable() { propName = "_HighlightBrightness0" };
    public CommonGUI.FloatOverridable highlightBrightness1 = new CommonGUI.FloatOverridable() { propName = "_HighlightBrightness1" };
    public CommonGUI.FloatOverridable highlightBrightness2 = new CommonGUI.FloatOverridable() { propName = "_HighlightBrightness2" };

    public CommonGUI.FloatOverridable emissionMasking = new CommonGUI.FloatOverridable() { propName = "_EmissionMasking" };
    public CommonGUI.FloatOverridable maskCoverageByAlpha = new CommonGUI.FloatOverridable() { propName = "_MmaskCoverageByAlpha" };
    #endregion

    public void FindProps()
    {
        #region CoveragePropsFind

        //Common
        CommonGUI.InitOverridable(coverage, materialEditor, props);
        CommonGUI.InitOverridable(stochastic, materialEditor, props);
        CommonGUI.InitOverridable(useAveragedNormals, materialEditor, props);
        CommonGUI.InitOverridable(paintableCoverage, materialEditor, props);
        CommonGUI.InitOverridable(distanceFadeStart, materialEditor, props);
        CommonGUI.InitOverridable(distanceFadeFalloff, materialEditor, props);
        CommonGUI.InitOverridable(coverageAreaBias, materialEditor, props);
        CommonGUI.InitOverridable(coverageLeakReduction, materialEditor, props);
        CommonGUI.InitOverridable(coverageAreaMaskRange, materialEditor, props);
        CommonGUI.InitOverridable(precipitationDirOffset, materialEditor, props);
        CommonGUI.InitOverridable(precipitationDirRange, materialEditor, props);
        CommonGUI.InitOverridable(blendByNormalsStrength, materialEditor, props);
        CommonGUI.InitOverridable(blendByNormalsPower, materialEditor, props);

        //Basic settings
        CommonGUI.InitOverridable(threeTexMode, materialEditor, props);
        CommonGUI.InitOverridable(coverageAmount, materialEditor, props);
        CommonGUI.InitOverridable(covTriBlendContrast, materialEditor, props);
        CommonGUI.InitOverridable(coverageTex0, materialEditor, props);
        CommonGUI.InitOverridable(coverageTex1, materialEditor, props, NL_Styles.coverageTex0Text);
        CommonGUI.InitOverridable(coverageTex2, materialEditor, props, NL_Styles.coverageTex0Text);
        CommonGUI.InitOverridable(coverageTiling, materialEditor, props);
        CommonGUI.InitOverridable(coverageTiling1, materialEditor, props, NL_Styles.coverageTilingText);
        CommonGUI.InitOverridable(coverageTiling2, materialEditor, props, NL_Styles.coverageTilingText);
        CommonGUI.InitOverridable(heightMap0Contrast, materialEditor, props);
        CommonGUI.InitOverridable(heightMap1Contrast, materialEditor, props, NL_Styles.heightMap0ContrastText);
        CommonGUI.InitOverridable(heightMap2Contrast, materialEditor, props, NL_Styles.heightMap0ContrastText);
        CommonGUI.InitOverridable(coverageNormalScale0, materialEditor, props);
        CommonGUI.InitOverridable(coverageNormalScale1, materialEditor, props, NL_Styles.coverageNormalScale0Text);
        CommonGUI.InitOverridable(coverageNormalScale2, materialEditor, props, NL_Styles.coverageNormalScale0Text);
        CommonGUI.InitOverridable(cov0Smoothness, materialEditor, props);
        CommonGUI.InitOverridable(cov1Smoothness, materialEditor, props, NL_Styles.cov0SmoothnessText);
        CommonGUI.InitOverridable(cov2Smoothness, materialEditor, props, NL_Styles.cov0SmoothnessText);
        CommonGUI.InitOverridable(coverageColor, materialEditor, props);
        CommonGUI.InitOverridable(coverageColor1, materialEditor, props, NL_Styles.coverageColorText);
        CommonGUI.InitOverridable(coverageColor2, materialEditor, props, NL_Styles.coverageColorText);
        CommonGUI.InitOverridable(enhanceRemap0, materialEditor, props);
        CommonGUI.InitOverridable(enhanceRemap1, materialEditor, props, NL_Styles.enhanceRemap0Text);
        CommonGUI.InitOverridable(enhanceRemap2, materialEditor, props, NL_Styles.enhanceRemap0Text);

        CommonGUI.InitOverridable(coverageNormalsOverlay, materialEditor, props);

        //Coverage Detail
        CommonGUI.InitOverridable(useCoverageDetail, materialEditor, props);
        CommonGUI.InitOverridable(coverageDetailTex, materialEditor, props);
        CommonGUI.InitOverridable(detailTiling, materialEditor, props);
        CommonGUI.InitOverridable(detailDistance, materialEditor, props);
        CommonGUI.InitOverridable(detailTexRemap, materialEditor, props);
        CommonGUI.InitOverridable(detailNormalScale, materialEditor, props);

        //Traces
        CommonGUI.InitOverridable(traces, materialEditor, props);
        CommonGUI.InitOverridable(traceDetail, materialEditor, props);
        CommonGUI.InitOverridable(tracesColor, materialEditor, props);
        CommonGUI.InitOverridable(tracesColor1, materialEditor, props, NL_Styles.tracesColorText);
        CommonGUI.InitOverridable(tracesColor2, materialEditor, props, NL_Styles.tracesColorText);
        CommonGUI.InitOverridable(tracesBaseBlend0, materialEditor, props);
        CommonGUI.InitOverridable(tracesBaseBlend1, materialEditor, props, NL_Styles.tracesBaseBlend0Text);
        CommonGUI.InitOverridable(tracesBaseBlend2, materialEditor, props, NL_Styles.tracesBaseBlend0Text);
        CommonGUI.InitOverridable(tracesColorBlendRange, materialEditor, props);
        CommonGUI.InitOverridable(tracesColorBlendRange1, materialEditor, props, NL_Styles.tracesColorBlendRangeText);
        CommonGUI.InitOverridable(tracesColorBlendRange2, materialEditor, props, NL_Styles.tracesColorBlendRangeText);
        CommonGUI.InitOverridable(tracesNormalScale, materialEditor, props);
        CommonGUI.InitOverridable(tracesNormalScale1, materialEditor, props, NL_Styles.tracesNormalScaleText);
        CommonGUI.InitOverridable(tracesNormalScale2, materialEditor, props, NL_Styles.tracesNormalScaleText);
        CommonGUI.InitOverridable(tracesBlendFactor, materialEditor, props);
        CommonGUI.InitOverridable(tessellation, materialEditor, props);
        CommonGUI.InitOverridable(tessEdgeL, materialEditor, props);
        CommonGUI.InitOverridable(tessFactorSnow, materialEditor, props);
        CommonGUI.InitOverridable(tessSnowdriftRange, materialEditor, props);
        CommonGUI.InitOverridable(tessMaxDisp, materialEditor, props);

        //Displacement
        CommonGUI.InitOverridable(displacement, materialEditor, props);
        CommonGUI.InitOverridable(coverageDisplacement, materialEditor, props);
        CommonGUI.InitOverridable(coverageDisplacement1, materialEditor, props, NL_Styles.coverageDisplacementText);
        CommonGUI.InitOverridable(coverageDisplacement2, materialEditor, props, NL_Styles.coverageDisplacementText);
        CommonGUI.InitOverridable(coverageDisplacementOffset, materialEditor, props);
        CommonGUI.InitOverridable(heightMap0LOD, materialEditor, props);
        CommonGUI.InitOverridable(heightMap1LOD, materialEditor, props, NL_Styles.heightMap0LODText);
        CommonGUI.InitOverridable(heightMap2LOD, materialEditor, props, NL_Styles.heightMap0LODText);

        //Sparkle and SSS
        CommonGUI.InitOverridable(sparkle, materialEditor, props);
        CommonGUI.InitOverridable(sss, materialEditor, props);

        CommonGUI.InitOverridable(sssMaskRemap0, materialEditor, props);
        CommonGUI.InitOverridable(sssMaskRemap1, materialEditor, props, NL_Styles.sssMaskRemap0Text);
        CommonGUI.InitOverridable(sssMaskRemap2, materialEditor, props, NL_Styles.sssMaskRemap0Text);

        CommonGUI.InitOverridable(colorEnhance, materialEditor, props);
        CommonGUI.InitOverridable(highlightBrightness0, materialEditor, props);
        CommonGUI.InitOverridable(highlightBrightness1, materialEditor, props, NL_Styles.highlightBrightness0Text);
        CommonGUI.InitOverridable(highlightBrightness2, materialEditor, props, NL_Styles.highlightBrightness0Text);
        CommonGUI.InitOverridable(emissionMasking, materialEditor, props);
        CommonGUI.InitOverridable(maskCoverageByAlpha, materialEditor, props);

        #endregion
    }

    private void InitFoldouts()
    {
        CommonGUI.InitFoldout(foldout_standardShaderProps, "STANDARD SHADER PROPERTIES", NL_Styles.header, nameof(foldout_standardShaderProps), true, true);
        CommonGUI.InitFoldout(foldout_coverageProps, "COVERAGE PROPERTIES", NL_Styles.header, nameof(foldout_coverageProps), true, true);

        CommonGUI.InitFoldout(foldout_basicSettings, "BASIC SETTINGS", NL_Styles.header1, nameof(foldout_basicSettings));
        CommonGUI.InitFoldout(foldout_detailMap, "DETAIL MAP", NL_Styles.header1, nameof(foldout_detailMap));
        CommonGUI.InitFoldout(foldout_areaMask, "AREA MASK", NL_Styles.header1, nameof(foldout_areaMask));
        CommonGUI.InitFoldout(foldout_tessellation, "TESSELLATION", NL_Styles.header1, nameof(foldout_tessellation));
        CommonGUI.InitFoldout(foldout_displacement, "DISPLACEMENT", NL_Styles.header1, nameof(foldout_displacement));
        CommonGUI.InitFoldout(foldout_traces, "TRACES", NL_Styles.header1, nameof(foldout_traces));
        CommonGUI.InitFoldout(foldout_blendByNormals, "BLEND BY NORMALS", NL_Styles.header1, nameof(foldout_blendByNormals));
        CommonGUI.InitFoldout(foldout_distanceFade, "DISTANCE FADE", NL_Styles.header1, nameof(foldout_distanceFade));
        CommonGUI.InitFoldout(foldout_sparkleAndSss, "SPARKLE AND SSS", NL_Styles.header1, nameof(foldout_sparkleAndSss));

        CommonGUI.InitFoldout(foldout_basic_layer0, "Layer 0", NL_Styles.header2, nameof(foldout_basic_layer0));
        CommonGUI.InitFoldout(foldout_basic_layer1, "Layer 1", NL_Styles.header2, nameof(foldout_basic_layer1));
        CommonGUI.InitFoldout(foldout_basic_layer2, "Layer 2", NL_Styles.header2, nameof(foldout_basic_layer2));

        CommonGUI.InitFoldout(foldout_displacement_layer0, "Layer 0", NL_Styles.header2, nameof(foldout_displacement_layer0));
        CommonGUI.InitFoldout(foldout_displacement_layer1, "Layer 1", NL_Styles.header2, nameof(foldout_displacement_layer1));
        CommonGUI.InitFoldout(foldout_displacement_layer2, "Layer 2", NL_Styles.header2, nameof(foldout_displacement_layer2));

        CommonGUI.InitFoldout(foldout_traces_layer0, "Layer 0", NL_Styles.header2, nameof(foldout_traces_layer0));
        CommonGUI.InitFoldout(foldout_traces_layer1, "Layer 1", NL_Styles.header2, nameof(foldout_traces_layer1));
        CommonGUI.InitFoldout(foldout_traces_layer2, "Layer 2", NL_Styles.header2, nameof(foldout_traces_layer2));

        CommonGUI.InitFoldout(foldout_sss_layer0, "Layer 0", NL_Styles.header2, nameof(foldout_sss_layer0));
        CommonGUI.InitFoldout(foldout_sss_layer1, "Layer 1", NL_Styles.header2, nameof(foldout_sss_layer1));
        CommonGUI.InitFoldout(foldout_sss_layer2, "Layer 2", NL_Styles.header2, nameof(foldout_sss_layer2));
    }

    public void DrawCoverageGUI(MaterialEditor mEditor)
    {
        materialEditor = mEditor;
        material = materialEditor.target as Material;

        FindProps();

        surfaceType = material.GetTag("TerrainCompatible", false, "false") != "false" ? SurfaceType.Terrain : SurfaceType.Mesh;

        if (!m_inspectorInitiated)
        {
            InitFoldouts();
        }

        EditorGUILayout.Space(1);

        if (CommonGUI.DrawFoldout(foldout_coverageProps))
        {
            if (SnowCoverage.instance == null)
            {
                EditorGUILayout.HelpBox("There's no Snow Coverage instance in the scene. Please add one to make the snow shaders work correctly.", MessageType.Warning);
                if (GUILayout.Button("Fix Now"))
                {
                    SnowCoverage.CreateNewSnowCoverageInstance();
                    SnowCoverage.UpdateMtl(material);
                }
            }
            else
            {
                GUILayout.Space(5);
                GUILayout.BeginHorizontal(NL_Styles.lineB);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select Global Coverage Instance", GUILayout.MaxWidth(200))) Selection.activeGameObject = SnowCoverage.instance.gameObject;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }

            GUILayout.BeginHorizontal(NL_Styles.lineB);
            CommonGUI.ToggleValueOverride(material, coverage, out coverage.localVal);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (coverage.prop.floatValue == 1)//coverage switch
            {
                if (CommonGUI.DrawFoldout(foldout_basicSettings))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    GUILayout.BeginHorizontal();
                    CommonGUI.ToggleValueOverride(material, paintableCoverage, out paintableCoverage.localVal);

                    EditorGUI.BeginDisabledGroup(paintableCoverage.prop.floatValue == 0);
                    if (GUILayout.Button("Open Total Brush", GUILayout.MaxWidth(120))) NL_TotalBrush.OpenWindowExternal(material.shader.name.Contains("Terrain") ? NL_TotalBrush.Mode.Terrain : NL_TotalBrush.Mode.Mesh);
                    EditorGUI.EndDisabledGroup();
                    GUILayout.EndHorizontal();
                    if (paintableCoverage.prop.floatValue == 1 && materialEditor.IsInstancingEnabled())
                        EditorGUILayout.HelpBox("GPU Instancing is enabled on this material, this will break vertex colors on different objects. Consider disable instancing if you want to use vertex colors.", MessageType.Warning);

                    if(surfaceType == SurfaceType.Mesh)
                    {
                        GUILayout.BeginHorizontal();
                        CommonGUI.ToggleValueOverride(material, useAveragedNormals, out useAveragedNormals.localVal);

                        EditorGUI.BeginDisabledGroup(useAveragedNormals.prop.floatValue == 0);
                        if (GUILayout.Button("Average Normals", GUILayout.MaxWidth(120))) NL_TotalBrush.AverageNormals();
                        EditorGUI.EndDisabledGroup();
                        GUILayout.EndHorizontal();
                    }else CommonGUI.ToggleValueOverride(material, stochastic, out stochastic.localVal);
                        
                    if (paintableCoverage.prop.floatValue == 0) threeTexMode.localVal = false;

                    if (threeTexMode.prop.floatValue == 1)
                    {
                        NL_Utilities.EndUICategory(0);
                        GUILayout.BeginVertical(NL_Styles.lineA);
                    }

                    EditorGUI.BeginDisabledGroup(paintableCoverage.prop.floatValue == 0);
                    CommonGUI.ToggleValueOverride(material, threeTexMode, out threeTexMode.localVal);
                    EditorGUI.EndDisabledGroup();

                    if (threeTexMode.prop.floatValue == 1 && paintableCoverage.prop.floatValue == 1)
                    {
                        if (CommonGUI.DrawFoldout(foldout_basic_layer0, 2))
                        {
                            CommonGUI.TextureValueOverride(coverageTex0, out coverageTex0.localVal);
                            CommonGUI.FloatValueOverride(coverageTiling, out coverageTiling.localVal);
                            CommonGUI.ColorValueOverride(coverageColor, out coverageColor.localVal);
                            CommonGUI.RangeValueOverride(cov0Smoothness, out cov0Smoothness.localVal);
                            CommonGUI.FloatValueOverride(coverageNormalScale0, out coverageNormalScale0.localVal);
                        }

                        if (CommonGUI.DrawFoldout(foldout_basic_layer1, 2))
                        {
                            CommonGUI.TextureValueOverride(coverageTex1, out coverageTex1.localVal);
                            CommonGUI.FloatValueOverride(coverageTiling1, out coverageTiling1.localVal);
                            CommonGUI.ColorValueOverride(coverageColor1, out coverageColor1.localVal);
                            CommonGUI.RangeValueOverride(cov1Smoothness, out cov1Smoothness.localVal);
                            CommonGUI.FloatValueOverride(coverageNormalScale1, out coverageNormalScale1.localVal);
                        }

                        if (CommonGUI.DrawFoldout(foldout_basic_layer2, 2))
                        {
                            GUILayout.Space(2);
                            CommonGUI.TextureValueOverride(coverageTex2, out coverageTex2.localVal);
                            CommonGUI.FloatValueOverride(coverageTiling2, out coverageTiling2.localVal);
                            CommonGUI.ColorValueOverride(coverageColor2, out coverageColor2.localVal);
                            CommonGUI.RangeValueOverride(cov2Smoothness, out cov2Smoothness.localVal);
                            CommonGUI.FloatValueOverride(coverageNormalScale2, out coverageNormalScale2.localVal);
                        }

                        NL_Utilities.EndUICategory(0);

                        GUILayout.BeginVertical(NL_Styles.lineB);
                    }
                    else
                    {
                        CommonGUI.TextureValueOverride(coverageTex0, out coverageTex0.localVal);
                        CommonGUI.FloatValueOverride(coverageTiling, out coverageTiling.localVal);
                        CommonGUI.ColorValueOverride(coverageColor, out coverageColor.localVal);
                        CommonGUI.RangeValueOverride(cov0Smoothness, out cov0Smoothness.localVal);
                        CommonGUI.FloatValueOverride(coverageNormalScale0, out coverageNormalScale0.localVal);
                    }

                    CommonGUI.FloatValueOverride(coverageAmount, out coverageAmount.localVal);
                    if(surfaceType == SurfaceType.Mesh) CommonGUI.FloatValueOverride(covTriBlendContrast, out covTriBlendContrast.localVal, new Vector2(2, 128), true);

                    if (emissionMasking.prop != null)
                        CommonGUI.FloatValueOverride(emissionMasking, out emissionMasking.localVal);
                    CommonGUI.FloatValueOverride(coverageNormalsOverlay, out coverageNormalsOverlay.localVal);

                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (CommonGUI.DrawFoldout(foldout_detailMap))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.ToggleValueOverride(material, useCoverageDetail, out useCoverageDetail.localVal);
                    if (useCoverageDetail.prop.floatValue == 1)
                    {
                        CommonGUI.TextureValueOverride(coverageDetailTex, out coverageDetailTex.localVal);
                        CommonGUI.FloatValueOverride(detailTiling, out detailTiling.localVal, new Vector2(0.001f, float.PositiveInfinity));
                        CommonGUI.RangeValueOverride(detailTexRemap, out detailTexRemap.localVal);
                        CommonGUI.FloatValueOverride(detailNormalScale, out detailNormalScale.localVal);
                        CommonGUI.FloatValueOverride(detailDistance, out detailDistance.localVal, new Vector2(0, float.PositiveInfinity));
                    }
                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (CommonGUI.DrawFoldout(foldout_areaMask))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.FloatValueOverride(coverageAreaMaskRange, out coverageAreaMaskRange.localVal);
                    CommonGUI.FloatValueOverride(coverageAreaBias, out coverageAreaBias.localVal);
                    CommonGUI.FloatValueOverride(coverageLeakReduction, out coverageLeakReduction.localVal);
                    CommonGUI.FloatValueOverride(precipitationDirOffset, out precipitationDirOffset.localVal);
                    CommonGUI.RangeValueOverride(precipitationDirRange, out precipitationDirRange.localVal);
                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (material.HasFloat("_TessellationOverride"))
                {
                    if (CommonGUI.DrawFoldout(foldout_tessellation))
                    {
                        GUILayout.BeginVertical(NL_Styles.lineB);
                        CommonGUI.ToggleValueOverride(material, tessellation, out tessellation.localVal);

                        if (tessellation.prop.floatValue == 1)
                        {
                            CommonGUI.FloatValueOverride(tessEdgeL, out tessEdgeL.localVal);
                            CommonGUI.FloatValueOverride(tessFactorSnow, out tessFactorSnow.localVal);
                            CommonGUI.RangeValueOverride(tessSnowdriftRange, out tessSnowdriftRange.localVal);
                            CommonGUI.FloatValueOverride(tessMaxDisp, out tessMaxDisp.localVal);
                        }
                        NL_Utilities.EndUICategory();
                    }
                    else GUILayout.Space(5);
                }

                if (coverageDisplacement != null)
                {
                    if (CommonGUI.DrawFoldout(foldout_displacement))
                    {
                        GUILayout.BeginVertical(NL_Styles.lineB);
                        CommonGUI.ToggleValueOverride(material, displacement, out displacement.localVal);
                        if (displacement.prop.floatValue == 1)
                        {
                            if (threeTexMode.prop.floatValue == 1)
                            {
                                NL_Utilities.EndUICategory(2);
                                GUILayout.BeginVertical(NL_Styles.lineA);

                                if (CommonGUI.DrawFoldout(foldout_displacement_layer0, 2))
                                {
                                    CommonGUI.RangeValueOverride(heightMap0Contrast, out heightMap0Contrast.localVal);
                                    CommonGUI.FloatValueOverride(heightMap0LOD, out heightMap0LOD.localVal, new Vector2(0, 6), true);
                                    CommonGUI.FloatValueOverride(coverageDisplacement, out coverageDisplacement.localVal, new Vector2(0, float.PositiveInfinity));
                                }

                                if (CommonGUI.DrawFoldout(foldout_displacement_layer1, 2))
                                {
                                    CommonGUI.RangeValueOverride(heightMap1Contrast, out heightMap1Contrast.localVal);
                                    CommonGUI.FloatValueOverride(heightMap1LOD, out heightMap1LOD.localVal, new Vector2(0, 6), true);
                                    CommonGUI.FloatValueOverride(coverageDisplacement1, out coverageDisplacement1.localVal, new Vector2(0, float.PositiveInfinity));
                                }

                                if (CommonGUI.DrawFoldout(foldout_displacement_layer2, 2))
                                {
                                    CommonGUI.RangeValueOverride(heightMap2Contrast, out heightMap2Contrast.localVal);
                                    CommonGUI.FloatValueOverride(heightMap2LOD, out heightMap2LOD.localVal, new Vector2(0, 6), true);
                                    CommonGUI.FloatValueOverride(coverageDisplacement2, out coverageDisplacement2.localVal, new Vector2(0, float.PositiveInfinity));
                                }

                                NL_Utilities.EndUICategory(0);

                                GUILayout.BeginVertical(NL_Styles.lineB);
                            }
                            else
                            {
                                CommonGUI.RangeValueOverride(heightMap0Contrast, out heightMap0Contrast.localVal);
                                CommonGUI.FloatValueOverride(heightMap0LOD, out heightMap0LOD.localVal, new Vector2(0, 6), true);
                                CommonGUI.FloatValueOverride(coverageDisplacement, out coverageDisplacement.localVal, new Vector2(0, float.PositiveInfinity));
                            }
                            CommonGUI.FloatValueOverride(coverageDisplacementOffset, out coverageDisplacementOffset.localVal);
                        }
                        NL_Utilities.EndUICategory();
                    }
                    else GUILayout.Space(5);
                }

                if (material.HasFloat("_TracesOverride"))
                {
                    if (CommonGUI.DrawFoldout(foldout_traces))
                    {
                        GUILayout.BeginVertical(NL_Styles.lineB);
                        CommonGUI.ToggleValueOverride(material, traces, out traces.localVal);
                        if (traces.prop.floatValue == 1)
                        {
                            if (threeTexMode.prop.floatValue == 1)
                            {
                                NL_Utilities.EndUICategory(2);
                                GUILayout.BeginVertical(NL_Styles.lineA);

                                if (CommonGUI.DrawFoldout(foldout_traces_layer0, 2))
                                {
                                    CommonGUI.FloatValueOverride(tracesBaseBlend0, out tracesBaseBlend0.localVal);
                                    CommonGUI.ColorValueOverride(tracesColor, out tracesColor.localVal);
                                    CommonGUI.RangeValueOverride(tracesColorBlendRange, out tracesColorBlendRange.localVal);
                                    CommonGUI.FloatValueOverride(tracesNormalScale, out tracesNormalScale.localVal);
                                }

                                if (CommonGUI.DrawFoldout(foldout_traces_layer1, 2))
                                {
                                    CommonGUI.FloatValueOverride(tracesBaseBlend1, out tracesBaseBlend1.localVal);
                                    CommonGUI.ColorValueOverride(tracesColor1, out tracesColor1.localVal);
                                    CommonGUI.RangeValueOverride(tracesColorBlendRange1, out tracesColorBlendRange1.localVal);
                                    CommonGUI.FloatValueOverride(tracesNormalScale1, out tracesNormalScale1.localVal);
                                }

                                if (CommonGUI.DrawFoldout(foldout_traces_layer2, 2))
                                {
                                    CommonGUI.FloatValueOverride(tracesBaseBlend2, out tracesBaseBlend2.localVal);
                                    CommonGUI.ColorValueOverride(tracesColor2, out tracesColor2.localVal);
                                    CommonGUI.RangeValueOverride(tracesColorBlendRange2, out tracesColorBlendRange2.localVal);
                                    CommonGUI.FloatValueOverride(tracesNormalScale2, out tracesNormalScale2.localVal);
                                }

                                NL_Utilities.EndUICategory(0);

                                GUILayout.BeginVertical(NL_Styles.lineB);
                            }
                            else
                            {
                                CommonGUI.FloatValueOverride(tracesBaseBlend0, out tracesBaseBlend0.localVal);
                                CommonGUI.ColorValueOverride(tracesColor, out tracesColor.localVal);
                                CommonGUI.RangeValueOverride(tracesColorBlendRange, out tracesColorBlendRange.localVal);
                                CommonGUI.FloatValueOverride(tracesNormalScale, out tracesNormalScale.localVal);
                            }

                            CommonGUI.ToggleValueOverride(material, traceDetail, out traceDetail.localVal);
                        }
                        NL_Utilities.EndUICategory();
                    }
                    else GUILayout.Space(5);
                }

                if (CommonGUI.DrawFoldout(foldout_blendByNormals))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.FloatValueOverride(blendByNormalsStrength, out blendByNormalsStrength.localVal, new Vector2(0, float.PositiveInfinity));
                    CommonGUI.FloatValueOverride(blendByNormalsPower, out blendByNormalsPower.localVal, new Vector2(0, float.PositiveInfinity));
                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (distanceFadeStart.prop != null)
                {
                    if (CommonGUI.DrawFoldout(foldout_distanceFade))
                    {
                        GUILayout.BeginVertical(NL_Styles.lineB);
                        CommonGUI.FloatValueOverride(distanceFadeStart, out distanceFadeStart.localVal, new Vector2(0, float.PositiveInfinity));
                        CommonGUI.FloatValueOverride(distanceFadeFalloff, out distanceFadeFalloff.localVal, new Vector2(0, float.PositiveInfinity));
                        NL_Utilities.EndUICategory();
                    }
                    else GUILayout.Space(5);
                }

                if (CommonGUI.DrawFoldout(foldout_sparkleAndSss))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.ToggleValueOverride(material, sparkle, out sparkle.localVal);
                    CommonGUI.ToggleValueOverride(material, sss, out sss.localVal);
                    CommonGUI.FloatValueOverride(colorEnhance, out colorEnhance.localVal, new Vector2(0, float.PositiveInfinity));

                    if (threeTexMode.prop.floatValue == 1)
                    {
                        NL_Utilities.EndUICategory(2);
                        GUILayout.BeginVertical(NL_Styles.lineA);

                        if (CommonGUI.DrawFoldout(foldout_sss_layer0, 2))
                        {
                            CommonGUI.FloatValueOverride(highlightBrightness0, out highlightBrightness0.localVal, new Vector2(0, float.PositiveInfinity));
                            if (sss.prop.floatValue == 1) CommonGUI.RangeValueOverride(sssMaskRemap0, out sssMaskRemap0.localVal);
                            CommonGUI.RangeValueOverride(enhanceRemap0, out enhanceRemap0.localVal);

                        }

                        if (CommonGUI.DrawFoldout(foldout_sss_layer1, 2))
                        {
                            CommonGUI.FloatValueOverride(highlightBrightness1, out highlightBrightness1.localVal, new Vector2(0, float.PositiveInfinity));
                            if (sss.prop.floatValue == 1) CommonGUI.RangeValueOverride(sssMaskRemap1, out sssMaskRemap1.localVal);
                            CommonGUI.RangeValueOverride(enhanceRemap1, out enhanceRemap1.localVal);
                        }

                        if (CommonGUI.DrawFoldout(foldout_sss_layer2, 2))
                        {
                            CommonGUI.FloatValueOverride(highlightBrightness2, out highlightBrightness2.localVal, new Vector2(0, float.PositiveInfinity));
                            if (sss.prop.floatValue == 1) CommonGUI.RangeValueOverride(sssMaskRemap2, out sssMaskRemap2.localVal);
                            CommonGUI.RangeValueOverride(enhanceRemap2, out enhanceRemap2.localVal);
                        }

                        NL_Utilities.EndUICategory(0);

                        GUILayout.BeginVertical(NL_Styles.lineB);
                    }
                    else
                    {
                        CommonGUI.RangeValueOverride(enhanceRemap0, out enhanceRemap0.localVal);
                        CommonGUI.FloatValueOverride(highlightBrightness0, out highlightBrightness0.localVal, new Vector2(0, float.PositiveInfinity));
                        if (sss.prop.floatValue == 1) CommonGUI.RangeValueOverride(sssMaskRemap0, out sssMaskRemap0.localVal);
                    }

                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);
            }
        }

        m_inspectorInitiated = true;// set 'init' flag right after the first GUI update to prevent calling things that need to be called only once
    }

    public void SetCoverageMaterialKeywords(Material material)
    {
        //Common
        if (material.HasProperty("_BumpMap"))
            CommonGUI.SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") ||
                (material.HasProperty("_CoverageTex0") && material.GetTexture("_CoverageTex0")) ||
                (material.HasProperty("_PrimaryMasks") && material.GetTexture("_PrimaryMasks")));

        CommonGUI.SetKeyword(material, coverage);
        CommonGUI.SetKeyword(material, stochastic);
        CommonGUI.SetKeyword(material, paintableCoverage);
        CommonGUI.SetKeyword(material, useAveragedNormals);

        //Snow
        CommonGUI.SetKeyword(material, threeTexMode);
        CommonGUI.SetKeyword(material, useCoverageDetail);
        CommonGUI.SetKeyword(material, displacement);
        CommonGUI.SetKeyword(material, traces);
        CommonGUI.SetKeyword(material, traceDetail);
        CommonGUI.SetKeyword(material, sparkle);
        CommonGUI.SetKeyword(material, sss);

        if (material.HasProperty("_TessellationOverride") && material.GetFloat("_TessellationOverride") == 1)
        {
            Shader simpleShader = Shader.Find("NOT_Lonely/Weatherade/Snow Coverage");
            Shader tessShader = Shader.Find("Hidden/NOT_Lonely/Weatherade/Snow Coverage (Tessellation)");
            Shader terrainSimpleShader = Shader.Find("NOT_Lonely/Weatherade/Snow Coverage (Terrain)");
            Shader terrainTessShader = Shader.Find("Hidden/NOT_Lonely/Weatherade/Snow Coverage (Terrain-Tessellation)");

            if (material.GetFloat("_Tessellation") == 1)
            {
                if (material.shader == simpleShader) material.shader = tessShader;
                if (material.shader == terrainSimpleShader) material.shader = terrainTessShader;
            }
            else
            {
                if (material.shader == tessShader) material.shader = simpleShader;
                if (material.shader == terrainTessShader) material.shader = terrainSimpleShader;
            }
        }
    }
}
