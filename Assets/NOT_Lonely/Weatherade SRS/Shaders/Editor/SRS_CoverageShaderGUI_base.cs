namespace NOT_Lonely.Weatherade.ShaderGUI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.Rendering;
    using UnityEngine;

    public class CommonGUI : ShaderGUI
    {
        public static readonly string standardPropsFoldoutName = "STANDARD SHADER PROPERTIES";
        public static readonly string coveragePropsFoldoutName = "COVERAGE PROPERTIES";

        public static bool unityStandardShaderProps = true;
        public static bool coverageProperties = true;

        public class Foldout
        {
            public bool state = false;
            public string header = "Foldout";
            public GUIStyle style;
            public float space = 0;
            public string saveName = "srs_foldout";
        }

        public class OverridableBase<LocalVal>
        {
            ///<summary> Material </summary>
            public Material mtl;
            ///<summary> Material editor </summary>
            public MaterialEditor mtlEditor;
            ///<summary> Material property which is used in the shader </summary>
            public MaterialProperty prop;
            ///<summary> Name of the property used in the shader </summary>
            public string propName;
            ///<summary> Name of the override value used in the shader </summary>
            public string ovrdName;
            ///<summary> Override state of the property </summary>
            public bool ovrd;
            ///<summary> Flag value that indicates that current material has an override for the value </summary>
            public bool hasLocalVal;
            ///<summary> Local value </summary>
            public LocalVal localVal;
            ///<summary> GUI label for the property displayed in the inspector. </summary>
            public GUIContent label;
        }

        public class ToggleOverridable : OverridableBase<bool>
        {
            public string keywordName;
        }
        public class TextureOverridable : OverridableBase<Texture> { }
        public class Texture2DArrayOverridable : OverridableBase<Texture2DArray> { }
        public class ColorOverridable : OverridableBase<Color> { }
        public class FloatOverridable : OverridableBase<float> { }
        public class Vector2DOverridable : OverridableBase<Vector2>
        {
            ///<summary> Temp Vector2D value. Used to make it save the local value properly </summary>
            public Vector2 tempVal;
        }

        /// <summary>
        /// Fill in all fileds of the overridable.
        /// </summary>
        /// <typeparam name="LocalVal"></typeparam>
        /// <param name="overridable">Overridable to be filled in.</param>
        /// <param name="mtlEditor">Material editor.</param>
        /// <param name="props">Material properties array.</param>
        /// <param name="label">Implicit GUIContent. If not provided, it will try to find it by the material property name or just use a bare material property name.</param>
        public static void InitOverridable<LocalVal>(OverridableBase<LocalVal> overridable, MaterialEditor mtlEditor, MaterialProperty[] props, GUIContent label = null)
        {
            overridable.mtl = mtlEditor.target as Material;
            overridable.mtlEditor = mtlEditor; 
            overridable.prop = FindProperty(overridable.propName, props, false);
            overridable.ovrdName = $"_{overridable.propName.Substring(1)}Override";

            if (label == null)
            {
                string labelFieldName = $"{char.ToLower(overridable.propName[1])}{overridable.propName.Substring(2)}Text";

                Type type = typeof(NL_Styles);
                FieldInfo field = type.GetField(labelFieldName, BindingFlags.Public | BindingFlags.Static);

                if (field != null) overridable.label = (GUIContent)field.GetValue(null);
                else overridable.label = new GUIContent(overridable.propName);
            }
            else
            {
                overridable.label = label;
            }
        }

        public static void InitFoldout(Foldout foldout, string header, GUIStyle style, string saveName, bool centered = false, bool defaultState = false)
        {
            if (EditorPrefs.HasKey(saveName)) foldout.state = EditorPrefs.GetBool(saveName, defaultState);
            else
            {
                foldout.state = defaultState;
                EditorPrefs.SetBool(saveName, foldout.state);
            }
            foldout.state = EditorPrefs.GetBool(saveName, defaultState);
            foldout.header = header;
            foldout.style = style;
            foldout.space = centered ? 0 : 17;
        }

        public static bool DrawFoldout(Foldout foldout, float space = 0)
        {
            GUILayout.BeginHorizontal(foldout.style);

            if(foldout.space == 0) NL_Utilities.CalcFoldoutSpace(foldout.header);
            else GUILayout.Space(foldout.space);

            EditorGUI.BeginChangeCheck();
            foldout.state = EditorGUILayout.Foldout(foldout.state, foldout.header, true);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(foldout.saveName, foldout.state);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(space);

            return foldout.state;
        }

        /// <summary>
        /// Manual keyword set.
        /// </summary>
        /// <param name="m">Material, where the keyword will be set.</param>
        /// <param name="keyword">Keyword name.</param>
        /// <param name="state">Keyword state to be set.</param>
        public static void SetKeyword(Material m, string keyword, bool state)
        {
            if (state)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
        }

        /// <summary>
        /// Automatically sets the keyword using the overriable's data.
        /// </summary>
        /// <param name="overridable">Overridable toggle.</param>
        public static void SetKeyword(Material mtl, ToggleOverridable overridable)
        {
            if (mtl.HasProperty(overridable.ovrdName) && mtl.GetFloat(overridable.ovrdName) == 1)
                SetKeyword(mtl, overridable.keywordName, mtl.GetFloat(GetPropName(overridable.ovrdName)) == 1);
        }

        /// <summary>Overrides a global Texture property by the local one in the current material.</summary>
        /// <param name="overridable">The overridable class, that contains all the neccessary properties.</param>
        /// <param name="val">output local value.</param>
        /// <param name="indentation">an optional indent level of the property in the inspector GUI.</param>
        public static void TextureValueOverride(TextureOverridable overridable, out Texture val, int indentation = 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(4 + indentation);

            if (overridable.mtl.HasFloat(overridable.ovrdName)) overridable.ovrd = overridable.mtl.GetFloat(overridable.ovrdName) == 1;
            overridable.ovrd = EditorGUILayout.Toggle(new GUIContent(), overridable.ovrd, GUILayout.MaxWidth(16));

            if (overridable.ovrd)
            {
                if (overridable.hasLocalVal)
                {
                    overridable.prop.textureValue = overridable.localVal;
                    overridable.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!overridable.ovrd);

            EditorGUILayout.PrefixLabel(overridable.label);
            EditorGUILayout.LabelField("", GUILayout.Width(0));
            overridable.mtlEditor.TexturePropertySingleLine(new GUIContent(), overridable.prop);

            if (overridable.ovrd)
            {
                overridable.localVal = overridable.prop.textureValue;
                overridable.hasLocalVal = true;
            }

            overridable.mtl.SetFloat(overridable.ovrdName, overridable.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            //hasVal = overridable.hasLocalVal;
            val = overridable.localVal;
        }

        public static void Texture2DArrayValueOverride(Texture2DArrayOverridable overridable, out Texture2DArray val, int indentation = 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(4 + indentation);

            if (overridable.mtl.HasFloat(overridable.ovrdName)) overridable.ovrd = overridable.mtl.GetFloat(overridable.ovrdName) == 1;
            overridable.ovrd = EditorGUILayout.Toggle(new GUIContent(), overridable.ovrd, GUILayout.MaxWidth(16));

            if (overridable.ovrd)
            {
                if (overridable.hasLocalVal)
                {
                    overridable.prop.textureValue = overridable.localVal;
                    overridable.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!overridable.ovrd);

            EditorGUILayout.PrefixLabel(overridable.label);
            EditorGUILayout.LabelField("", GUILayout.Width(0));
            overridable.mtlEditor.TexturePropertySingleLine(new GUIContent(), overridable.prop);

            if (overridable.ovrd)
            {
                overridable.localVal = overridable.prop.textureValue as Texture2DArray;
                overridable.hasLocalVal = true;
            }

            overridable.mtl.SetFloat(overridable.ovrdName, overridable.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            //hasVal = overridable.hasLocalVal;
            val = overridable.localVal;
        }

        ///<summary> Overrides a global Color property by the local one in the current material.</summary>
        /// <param name="overridable">The overridable class, that contains all the neccessary properties.</param>
        /// <param name="val">output local value.</param>
        /// <param name="indentation">an optional indent level of the property in the inspector GUI.</param>
        public static void ColorValueOverride(ColorOverridable overridable, out Color val, int indentation = 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(4);

            if (overridable.mtl.HasFloat(overridable.ovrdName)) overridable.ovrd = overridable.mtl.GetFloat(overridable.ovrdName) == 1;
            overridable.ovrd = EditorGUILayout.Toggle(new GUIContent(), overridable.ovrd, GUILayout.MaxWidth(16));

            if (overridable.ovrd)
            {
                if (overridable.hasLocalVal)
                {
                    overridable.prop.colorValue = overridable.localVal;
                    overridable.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!overridable.ovrd);
            overridable.mtlEditor.ShaderProperty(overridable.prop, overridable.label, indentation);

            if (overridable.ovrd)
            {
                overridable.localVal = overridable.prop.colorValue;
                overridable.hasLocalVal = true;
            }

            overridable.mtl.SetFloat(overridable.ovrdName, overridable.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            //hasVal = overridable.hasLocalVal;
            val = overridable.localVal;
        }

        ///<summary> Overrides a global Toggle(float) property by the local one in the current material. 
        ///Ussually used for keyword switching. 
        ///A keyword state itself must be set in the ValidateMaterial method.</summary>
        /// <param name="overridable">The overridable class, that contains all the neccessary properties.</param>
        /// <param name="val">output local value.</param>
        /// <param name="indentation">an optional indent level of the property in the inspector GUI.</param>
        public static void ToggleValueOverride(Material material, ToggleOverridable toggleProp, out bool val, int indentation = 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(4);

            if (material.HasFloat(toggleProp.ovrdName)) toggleProp.ovrd = material.GetFloat(toggleProp.ovrdName) == 1;
            toggleProp.ovrd = EditorGUILayout.Toggle(new GUIContent(), toggleProp.ovrd, GUILayout.MaxWidth(16));

            if (toggleProp.ovrd)
            {
                if (toggleProp.hasLocalVal)
                {
                    toggleProp.prop.floatValue = toggleProp.localVal ? 1 : 0;
                    toggleProp.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!toggleProp.ovrd);
            EditorGUILayout.PrefixLabel(toggleProp.label);
            GUILayout.Space(2);

            if (toggleProp.ovrd)
            {
                toggleProp.localVal = toggleProp.prop.floatValue == 1;
                toggleProp.localVal = EditorGUILayout.Toggle(toggleProp.localVal);
                toggleProp.hasLocalVal = true;
            }
            else
            {
                EditorGUILayout.Toggle(toggleProp.prop.floatValue == 1);
            }

            material.SetFloat(toggleProp.ovrdName, toggleProp.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            //hasVal = toggleProp.hasLocalVal;
            val = toggleProp.localVal;

            if (toggleProp.ovrd) toggleProp.prop.floatValue = val ? 1 : 0;
        }

        ///<summary> Overrides a global float property by the local one in the current material.</summary>
        /// <param name="overridable">The overridable class, that contains all the neccessary properties.</param>
        /// <param name="hasVal">output 'hasLocalValue' flag.</param>
        /// <param name="val">output local value.</param>
        /// <param name="limits">an optional min-max limits to clamp the float value.</param>
        /// <param name="indentation">an optional indent level of the property in the inspector GUI.</param>
        public static void FloatValueOverride(FloatOverridable overridable, out float val, Vector2 limits = new Vector2(), bool roundToInt = false, int indentation = 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(4);

            if (overridable.mtl.HasFloat(overridable.ovrdName)) overridable.ovrd = overridable.mtl.GetFloat(overridable.ovrdName) == 1;
            overridable.ovrd = EditorGUILayout.Toggle(new GUIContent(), overridable.ovrd, GUILayout.MaxWidth(16));

            if (roundToInt) overridable.localVal = Mathf.CeilToInt(overridable.localVal);

            if (limits != Vector2.zero) overridable.localVal = Mathf.Clamp(overridable.localVal, limits.x, limits.y);

            if (overridable.ovrd)
            {
                if (overridable.hasLocalVal)
                {
                    overridable.prop.floatValue = overridable.localVal;
                    overridable.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!overridable.ovrd);

            if (overridable.ovrd) overridable.mtl.SetFloat(overridable.prop.name, overridable.prop.floatValue);
            overridable.mtlEditor.ShaderProperty(overridable.prop, overridable.label, indentation);

            if (overridable.ovrd)
            {
                overridable.localVal = overridable.prop.floatValue;
                overridable.hasLocalVal = true;
            }

            overridable.mtl.SetFloat(overridable.ovrdName, overridable.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            //hasVal = overridable.hasLocalVal;
            val = overridable.localVal;
        }

        ///<summary> Overrides a range (Vector2D) property by the local one in the current material.</summary>
        /// <param name="overridable">The overridable class, that contains all the neccessary properties.</param>
        /// <param name="val">output local value.</param>
        /// <param name="indentation">an optional indent level of the property in the inspector GUI.</param>
        public static void RangeValueOverride(Vector2DOverridable overridable, out Vector2 val, int indentation = 0)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            GUILayout.Space(4);

            if (overridable.mtl.HasFloat(overridable.ovrdName)) overridable.ovrd = overridable.mtl.GetFloat(overridable.ovrdName) == 1;
            overridable.ovrd = EditorGUILayout.Toggle(new GUIContent(), overridable.ovrd, GUILayout.MaxWidth(18));

            if (overridable.ovrd)
            {
                if (overridable.hasLocalVal)
                {
                    overridable.prop.vectorValue = overridable.prop.vectorValue;
                    overridable.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!overridable.ovrd);
            GUILayout.Space(indentation - 5);
            if (overridable.ovrd) overridable.mtl.SetVector(overridable.prop.name, overridable.prop.vectorValue);
            overridable.mtlEditor.MinMaxSliderWithFloats(overridable.prop, 0, 1, overridable.label);

            if (overridable.ovrd)
            {
                overridable.localVal = overridable.prop.vectorValue;
                overridable.hasLocalVal = true;
            }

            overridable.mtl.SetFloat(overridable.ovrdName, overridable.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.EndVertical();

            val = overridable.localVal;
        }

        ///<summary> Overrides a global Vector2D property by the local one in the current material.</summary>
        /// <param name="material">The material.</param>
        /// <param name="inspectorInitiated">Inspector initialization state.</param>
        /// <param name="overridable">The overridable class, that contains all the neccessary properties.</param>
        /// <param name="val">output local value.</param>
        /// <param name="indentation">an optional indent level of the property in the inspector GUI.</param>
        public static void Vector2DValueOverride(bool inspectorInitiated, Vector2DOverridable overridable, out Vector2 val, int indentation = 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(4);

            if (overridable.mtl.HasFloat(overridable.ovrdName)) overridable.ovrd = overridable.mtl.GetFloat(overridable.ovrdName) == 1;
            overridable.ovrd = EditorGUILayout.Toggle(new GUIContent(), overridable.ovrd, GUILayout.MaxWidth(18));

            if (overridable.ovrd)
            {
                if (overridable.hasLocalVal)
                {
                    overridable.prop.vectorValue = overridable.localVal;
                    overridable.hasLocalVal = false;
                }
            }

            EditorGUI.BeginDisabledGroup(!overridable.ovrd);

            GUILayout.BeginHorizontal();
            GUILayout.Space(indentation - 2);
            EditorGUILayout.PrefixLabel(overridable.label);

            GUILayout.Space(2);

            if (!inspectorInitiated)
                overridable.tempVal = overridable.prop.vectorValue;

            overridable.tempVal = EditorGUILayout.Vector2Field("", overridable.tempVal);
            overridable.tempVal.x = (float)Math.Round(overridable.tempVal.x, 3);
            overridable.tempVal.y = (float)Math.Round(overridable.tempVal.y, 3);

            overridable.mtl.SetVector(overridable.prop.name, overridable.tempVal);

            GUILayout.EndHorizontal();

            if (overridable.ovrd)
            {
                overridable.localVal = overridable.prop.vectorValue;
                overridable.hasLocalVal = true;
            }

            overridable.mtl.SetFloat(overridable.ovrdName, overridable.ovrd ? 1 : 0);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            //hasVal = overridable.hasLocalVal;
            val = overridable.localVal;
            //modifiedTempVal = tempVal;
        }

        public static string GetPropName(string ovrdName)
        {
            return ovrdName.Replace("Override", "");
        }
    }
}
