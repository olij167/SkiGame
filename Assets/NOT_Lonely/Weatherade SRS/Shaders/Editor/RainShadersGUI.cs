using NOT_Lonely.TotalBrush;
using NOT_Lonely.Weatherade;
using NOT_Lonely.Weatherade.ShaderGUI;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RainShadersGUI : ShaderGUI
{
    public MaterialEditor materialEditor;
    public Material material;
    public MaterialProperty[] props;
    public bool m_inspectorInitiated = false;
    public float currentInspectorWidth;

    #region Foldouts
    private CommonGUI.Foldout foldout_standardShaderProps = new CommonGUI.Foldout() { saveName = nameof(foldout_standardShaderProps) };
    private CommonGUI.Foldout foldout_coverageProps = new CommonGUI.Foldout() { saveName = nameof(foldout_coverageProps) };
    private CommonGUI.Foldout foldout_masks = new CommonGUI.Foldout() { saveName = nameof(foldout_masks) };
    private CommonGUI.Foldout foldout_wetness = new CommonGUI.Foldout() { saveName = nameof(foldout_wetness) };
    private CommonGUI.Foldout foldout_areaMask = new CommonGUI.Foldout() { saveName = nameof(foldout_areaMask) };
    private CommonGUI.Foldout foldout_puddles = new CommonGUI.Foldout() { saveName = nameof(foldout_puddles) };
    private CommonGUI.Foldout foldout_ripplesAndSpots = new CommonGUI.Foldout() { saveName = nameof(foldout_ripplesAndSpots) };
    private CommonGUI.Foldout foldout_drips = new CommonGUI.Foldout() { saveName = nameof(foldout_drips) };
    private CommonGUI.Foldout foldout_blendByNormals = new CommonGUI.Foldout() { saveName = nameof(foldout_blendByNormals) };
    private CommonGUI.Foldout foldout_distanceFade = new CommonGUI.Foldout() { saveName = nameof(foldout_distanceFade) };
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

    //Wetness
    public CommonGUI.FloatOverridable wetnessAmount = new CommonGUI.FloatOverridable() { propName = "_WetnessAmount" };
    public CommonGUI.ColorOverridable wetColor = new CommonGUI.ColorOverridable() { propName = "_WetColor" };

    //Puddles
    public CommonGUI.FloatOverridable puddlesAmount = new CommonGUI.FloatOverridable() { propName = "_PuddlesAmount" };
    public CommonGUI.FloatOverridable puddlesMult = new CommonGUI.FloatOverridable() { propName = "_PuddlesMult" };
    public CommonGUI.Vector2DOverridable puddlesRange = new CommonGUI.Vector2DOverridable() { propName = "_PuddlesRange" };
    public CommonGUI.FloatOverridable puddlesTiling = new CommonGUI.FloatOverridable() { propName = "_PuddlesTiling" };
    public CommonGUI.FloatOverridable puddlesSlope = new CommonGUI.FloatOverridable() { propName = "_PuddlesSlope" };

    //Ripples and Spots
    public CommonGUI.ToggleOverridable ripples = new CommonGUI.ToggleOverridable() { propName = "_Ripples", keywordName = "_RIPPLES_ON" };
    public CommonGUI.FloatOverridable ripplesAmount = new CommonGUI.FloatOverridable() { propName = "_RipplesAmount" };
    public CommonGUI.FloatOverridable ripplesIntensity = new CommonGUI.FloatOverridable() { propName = "_RipplesIntensity" };
    public CommonGUI.FloatOverridable ripplesFPS = new CommonGUI.FloatOverridable() { propName = "_RipplesFPS" };
    public CommonGUI.FloatOverridable ripplesTiling = new CommonGUI.FloatOverridable() { propName = "_RipplesTiling" };
    public CommonGUI.FloatOverridable spotsIntensity = new CommonGUI.FloatOverridable() { propName = "_SpotsIntensity" };
    public CommonGUI.FloatOverridable spotsAmount = new CommonGUI.FloatOverridable() { propName = "_SpotsAmount" };

    //Drips
    public CommonGUI.ToggleOverridable drips = new CommonGUI.ToggleOverridable() { propName = "_Drips", keywordName = "_DRIPS_ON" };
    public CommonGUI.FloatOverridable dripsIntensity = new CommonGUI.FloatOverridable() { propName = "_DripsIntensity" };
    public CommonGUI.FloatOverridable dripsSpeed = new CommonGUI.FloatOverridable() { propName = "_DripsSpeed" };
    public CommonGUI.Vector2DOverridable dripsTiling = new CommonGUI.Vector2DOverridable() { propName = "_DripsTiling" };
    public CommonGUI.FloatOverridable distortionAmount = new CommonGUI.FloatOverridable() { propName = "_DistortionAmount" };
    public CommonGUI.FloatOverridable distortionTiling = new CommonGUI.FloatOverridable() { propName = "_DistortionTiling" };
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

        //Wetness
        CommonGUI.InitOverridable(wetnessAmount, materialEditor, props);
        CommonGUI.InitOverridable(wetColor, materialEditor, props);

        //Puddles
        CommonGUI.InitOverridable(puddlesAmount, materialEditor, props);
        CommonGUI.InitOverridable(puddlesMult, materialEditor, props);
        CommonGUI.InitOverridable(puddlesRange, materialEditor, props);
        CommonGUI.InitOverridable(puddlesTiling, materialEditor, props);
        CommonGUI.InitOverridable(puddlesSlope, materialEditor, props);

        //Ripples and Spots
        CommonGUI.InitOverridable(ripples, materialEditor, props);
        CommonGUI.InitOverridable(ripplesAmount, materialEditor, props);
        CommonGUI.InitOverridable(ripplesIntensity, materialEditor, props);
        CommonGUI.InitOverridable(ripplesFPS, materialEditor, props);
        CommonGUI.InitOverridable(ripplesTiling, materialEditor, props);
        CommonGUI.InitOverridable(spotsIntensity, materialEditor, props);
        CommonGUI.InitOverridable(spotsAmount, materialEditor, props);

        //Drips
        CommonGUI.InitOverridable(drips, materialEditor, props);
        CommonGUI.InitOverridable(dripsIntensity, materialEditor, props);
        CommonGUI.InitOverridable(dripsSpeed, materialEditor, props);
        CommonGUI.InitOverridable(dripsTiling, materialEditor, props);
        CommonGUI.InitOverridable(distortionAmount, materialEditor, props);
        CommonGUI.InitOverridable(distortionTiling, materialEditor, props);

        #endregion
    }

    private void InitFoldouts()
    {
        CommonGUI.InitFoldout(foldout_standardShaderProps, "STANDARD SHADER PROPERTIES", NL_Styles.header, nameof(foldout_standardShaderProps), true, true);
        CommonGUI.InitFoldout(foldout_coverageProps, "COVERAGE PROPERTIES", NL_Styles.header, nameof(foldout_coverageProps), true, true);

        CommonGUI.InitFoldout(foldout_masks, "MASKS", NL_Styles.header1, nameof(foldout_masks));
        CommonGUI.InitFoldout(foldout_wetness, "WETNESS", NL_Styles.header1, nameof(foldout_wetness));
        CommonGUI.InitFoldout(foldout_areaMask, "AREA MASK", NL_Styles.header1, nameof(foldout_areaMask));
        CommonGUI.InitFoldout(foldout_puddles, "PUDDLES", NL_Styles.header1, nameof(foldout_puddles));
        CommonGUI.InitFoldout(foldout_ripplesAndSpots, "RIPPLES AND SPOTS", NL_Styles.header1, nameof(foldout_ripplesAndSpots));
        CommonGUI.InitFoldout(foldout_drips, "DRIPS", NL_Styles.header1, nameof(foldout_drips));
        CommonGUI.InitFoldout(foldout_blendByNormals, "BLEND BY NORMALS", NL_Styles.header1, nameof(foldout_blendByNormals));
        CommonGUI.InitFoldout(foldout_distanceFade, "DISTANCE FADE", NL_Styles.header1, nameof(foldout_distanceFade));
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
            if (RainCoverage.instance == null)
            {
                EditorGUILayout.HelpBox("There's no Rain Coverage instance in the scene. Please add one to make the rain shaders work correctly.", MessageType.Warning);
                if (GUILayout.Button("Fix Now"))
                {
                    RainCoverage.CreateNewRainCoverageInstance();
                    RainCoverage.UpdateMtl(material);
                }
            }
            else
            {
                GUILayout.Space(5);
                GUILayout.BeginHorizontal(NL_Styles.lineB);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select Global Coverage Instance", GUILayout.MaxWidth(200))) Selection.activeGameObject = RainCoverage.instance.gameObject;
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
                if (CommonGUI.DrawFoldout(foldout_masks))
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
                    }

                    CommonGUI.ToggleValueOverride(material, stochastic, out stochastic.localVal);

                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (CommonGUI.DrawFoldout(foldout_wetness))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.ColorValueOverride(wetColor, out wetColor.localVal);
                    CommonGUI.FloatValueOverride(wetnessAmount, out wetnessAmount.localVal);
                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (CommonGUI.DrawFoldout(foldout_puddles))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.FloatValueOverride(puddlesAmount, out puddlesAmount.localVal);
                    CommonGUI.FloatValueOverride(puddlesMult, out puddlesMult.localVal);
                    CommonGUI.RangeValueOverride(puddlesRange, out puddlesRange.localVal);
                    CommonGUI.FloatValueOverride(puddlesTiling, out puddlesTiling.localVal, new Vector2(0, float.PositiveInfinity));
                    CommonGUI.FloatValueOverride(puddlesSlope, out puddlesSlope.localVal);
                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (CommonGUI.DrawFoldout(foldout_ripplesAndSpots))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.ToggleValueOverride(material, ripples, out ripples.localVal);
                    CommonGUI.FloatValueOverride(ripplesAmount, out ripplesAmount.localVal, new Vector2(0, 15), true);
                    CommonGUI.FloatValueOverride(ripplesIntensity, out ripplesIntensity.localVal, new Vector2(0, float.PositiveInfinity));
                    CommonGUI.FloatValueOverride(ripplesFPS, out ripplesFPS.localVal, new Vector2(0, 120), true);
                    CommonGUI.FloatValueOverride(ripplesTiling, out ripplesTiling.localVal, new Vector2(0, float.PositiveInfinity));
                    CommonGUI.FloatValueOverride(spotsIntensity, out spotsIntensity.localVal);
                    if (spotsIntensity.prop.floatValue > 0)
                        CommonGUI.FloatValueOverride(spotsAmount, out spotsAmount.localVal);
                    NL_Utilities.EndUICategory();
                }
                else GUILayout.Space(5);

                if (CommonGUI.DrawFoldout(foldout_drips))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.ToggleValueOverride(material, drips, out drips.localVal);
                    CommonGUI.FloatValueOverride(dripsIntensity, out dripsIntensity.localVal);
                    CommonGUI.FloatValueOverride(dripsSpeed, out dripsSpeed.localVal);
                    CommonGUI.Vector2DValueOverride(m_inspectorInitiated, dripsTiling, out dripsTiling.localVal);
                    CommonGUI.FloatValueOverride(distortionAmount, out distortionAmount.localVal, new Vector2(0, float.PositiveInfinity));
                    if (distortionAmount.prop.floatValue > 0)
                        CommonGUI.FloatValueOverride(distortionTiling, out distortionTiling.localVal, new Vector2(0, float.PositiveInfinity), false, 1);
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

                if (CommonGUI.DrawFoldout(foldout_blendByNormals))
                {
                    GUILayout.BeginVertical(NL_Styles.lineB);
                    CommonGUI.FloatValueOverride(blendByNormalsStrength, out blendByNormalsStrength.localVal, new Vector2(0, float.PositiveInfinity));
                    CommonGUI.FloatValueOverride(blendByNormalsPower, out blendByNormalsPower.localVal, new Vector2(0, 1));
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

        //Rain
        CommonGUI.SetKeyword(material, ripples);
        CommonGUI.SetKeyword(material, drips);
    }
}
