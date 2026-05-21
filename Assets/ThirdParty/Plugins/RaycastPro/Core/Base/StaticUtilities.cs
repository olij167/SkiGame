#if UNITY_EDITOR

using RaycastPro.Editor;
#endif
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RaycastPro
{
    public static class StaticUtilities
    {
        private static string[] _areaNames;
        private static int[] _areaValues;

        private static void CacheAreas()
        {
            if (_areaNames != null) return;

            _areaNames = NavMesh.GetAreaNames();
            _areaValues = new int[_areaNames.Length];

            for (int i = 0; i < _areaNames.Length; i++)
            {
                int areaIndex = NavMesh.GetAreaFromName(_areaNames[i]);
                _areaValues[i] = 1 << areaIndex;
            }
        }

        #if UNITY_EDITOR
        public static void Draw(
            SerializedObject so,
            string propertyName,
            string label = "Area Mask")
        {
            CacheAreas();

            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUILayout.HelpBox(
                    $"NavMesh AreaMask: Property '{propertyName}' not found or is not int.",
                    MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();

            int newMask = EditorGUILayout.MaskField(
                label,
                prop.intValue,
                _areaNames
            );

            if (EditorGUI.EndChangeCheck())
            {
                prop.intValue = newMask;
                so.ApplyModifiedProperties();
            }
        }
        #endif

        public static class RenderPipelineUtil
        {
            public enum Pipeline
            {
                BuiltIn,
                URP,
                HDRP
            }

            public static Pipeline Current
            {
                get
                {
                    var asset = GraphicsSettings.currentRenderPipeline;
                    if (!asset) return Pipeline.BuiltIn;

                    var type = asset.GetType().Name;
                    if (type.Contains("HDRender")) return Pipeline.HDRP;
                    if (type.Contains("Universal")) return Pipeline.URP;

                    return Pipeline.BuiltIn;
                }
            }
        }

        public static class LinerMaterialFactory
        {
            private static Material _cached;

            public static Material Get()
            {
                if (_cached) return _cached;

                Shader shader = FindBestShader();
                _cached = new Material(shader)
                {
                    name = $"RCPro_Liner_{RenderPipelineUtil.Current}"
                };

                SetupMaterial(_cached);
                return _cached;
            }

            private static Shader FindBestShader()
            {
                switch (RenderPipelineUtil.Current)
                {
                    case RenderPipelineUtil.Pipeline.URP:
                        return Shader.Find("Universal Render Pipeline/Unlit")
                               ?? Shader.Find("Unlit/Color");

                    case RenderPipelineUtil.Pipeline.HDRP:
                        return Shader.Find("HDRP/Unlit")
                               ?? Shader.Find("Unlit/Color");

                    default:
                        return Shader.Find("Unlit/Color");
                }
            }

            private static void SetupMaterial(Material mat)
            {
                mat.renderQueue = (int) RenderQueue.Transparent;

                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);

                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", Color.white);

                // Emission Safe Enable
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.white * 1.2f);
                }
            }
        }

        public static class LinerProfile
        {
            public static void Apply(LineRenderer liner, float maxWidth)
            {
                if (!liner) return;

                liner.material = LinerMaterialFactory.Get();

                liner.alignment = LineAlignment.View;
                liner.textureMode = LineTextureMode.Stretch;
                liner.useWorldSpace = true;

                liner.shadowCastingMode = ShadowCastingMode.Off;
                liner.receiveShadows = false;

                liner.numCornerVertices = 6;
                liner.numCapVertices = 6;

                liner.startWidth = Mathf.Min(maxWidth, 0.06f);
                liner.endWidth = Mathf.Min(maxWidth, 0.02f);


                // --- Bell Curve : 0 → 1 → 0 ---
                var curve = new AnimationCurve();

                var k0 = new Keyframe(0f, 0f);
                var k1 = new Keyframe(0.5f, .2f);
                var k2 = new Keyframe(1f, 0f);

                // Tangents = سینمایی و نرم
                k0.outTangent = 0f;
                k1.inTangent = 0f;
                k1.outTangent = 0f;
                k2.inTangent = 0f;

                curve.AddKey(k0);
                curve.AddKey(k1);
                curve.AddKey(k2);

                curve.SmoothTangents(1, 0.5f);

                liner.widthCurve = curve;
                // --------------------------------
#if UNITY_EDITOR

                liner.colorGradient = new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(RCProEditor.Aqua, 0f),
                        new GradientColorKey(RCProEditor.Violet, 0.4f),
                        new GradientColorKey(RCProEditor.Violet, 0.6f),
                        new GradientColorKey(RCProEditor.Aqua, 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    }
                };
#endif
            }
        }
    }
}