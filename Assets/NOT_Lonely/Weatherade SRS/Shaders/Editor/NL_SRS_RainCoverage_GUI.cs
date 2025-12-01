namespace NOT_Lonely.Weatherade.ShaderGUI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using NOT_Lonely.Weatherade;
    using NOT_Lonely.TotalBrush;
    using UnityEditor.Rendering.Universal.ShaderGUI;
    using UnityEditor.Rendering;

    public class NL_SRS_RainCoverage_GUI : BaseShaderGUI
    {
        private RainShadersGUI rainCoverageGUI;

        #region StandardLitShaderGUI
        static readonly string[] workflowModeNames = Enum.GetNames(typeof(LitGUI.WorkflowMode));

        private LitGUI.LitProperties litProperties;
        private URP_LitDetailGUI.LitProperties litDetailProperties;

        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            materialScopesList.RegisterHeaderScope(URP_LitDetailGUI.Styles.detailInputs, Expandable.Details, _ => URP_LitDetailGUI.DoDetailArea(litDetailProperties, materialEditor));
        }

        // collect properties from the material properties
        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            litProperties = new LitGUI.LitProperties(properties);
            litDetailProperties = new URP_LitDetailGUI.LitProperties(properties);
        }

        // material changed check
        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, URP_LitDetailGUI.SetMaterialKeywords);

            //SRS
            if(rainCoverageGUI == null) rainCoverageGUI = new RainShadersGUI();
            rainCoverageGUI.SetCoverageMaterialKeywords(material);
            SnowCoverage.UpdateMtl(material);
            //
        }

        // material main surface options
        public override void DrawSurfaceOptions(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            if (litProperties.workflowMode != null)
                DoPopup(LitGUI.Styles.workflowModeText, litProperties.workflowMode, workflowModeNames);

            base.DrawSurfaceOptions(material);
        }

        // material main surface inputs
        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            LitGUI.Inputs(litProperties, materialEditor, material);
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        // material main advanced options
        public override void DrawAdvancedOptions(Material material)
        {
            if (litProperties.reflections != null && litProperties.highlights != null)
            {
                materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
                materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
            }

            base.DrawAdvancedOptions(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            // _Emission property is lost after assigning Standard shader to the material
            // thus transfer it before assigning the new shader
            if (material.HasColor("_Emission"))
            {
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }
            
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            //SRS: pass the _MainTex from BiRP to URP
            Texture albedoTex = material.GetTexture("_MainTex");
            if (albedoTex != null)
            {
                material.SetTexture("_BaseMap", albedoTex);
            }

            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
            {
                SetupMaterialBlendMode(material);
                return;
            }

            SurfaceType surfaceType = SurfaceType.Opaque;
            BlendMode blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/"))
            {
                surfaceType = SurfaceType.Opaque;
                material.SetFloat("_AlphaClip", 1);
            }
            else if (oldShader.name.Contains("/Transparent/"))
            {
                // NOTE: legacy shaders did not provide physically based transparency
                // therefore Fade mode
                surfaceType = SurfaceType.Transparent;
                blendMode = BlendMode.Alpha;
            }
            material.SetFloat("_Blend", (float)blendMode);

            material.SetFloat("_Surface", (float)surfaceType);
            if (surfaceType == SurfaceType.Opaque)
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (oldShader.name.Equals("Standard (Specular setup)"))
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Specular);
                Texture texture = material.GetTexture("_SpecGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            else
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
                Texture texture = material.GetTexture("_MetallicGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
        }
        #endregion

        void InitCoverageGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (rainCoverageGUI == null) rainCoverageGUI = new RainShadersGUI();
            rainCoverageGUI.materialEditor = materialEditor;
            rainCoverageGUI.props = properties;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (NL_Styles.lineB == null || NL_Styles.lineB.normal.background == null) NL_Styles.GetStyles();

            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            GUILayout.BeginHorizontal(NL_Styles.header);
            NL_Utilities.CalcFoldoutSpace(CommonGUI.standardPropsFoldoutName);
            CommonGUI.unityStandardShaderProps = EditorGUILayout.Foldout(CommonGUI.unityStandardShaderProps, CommonGUI.standardPropsFoldoutName, true);
            GUILayout.EndHorizontal();

            if (CommonGUI.unityStandardShaderProps)
            {
                GUILayout.Space(5);
                base.OnGUI(materialEditor, properties);
            }

            InitCoverageGUI(materialEditor, properties);
            rainCoverageGUI.DrawCoverageGUI(materialEditor);
        }
    }
}
