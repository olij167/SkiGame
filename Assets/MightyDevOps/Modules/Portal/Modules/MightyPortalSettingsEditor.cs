#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;

namespace MightyPortal
{

    // [CustomEditor(typeof(PortalSettings))]
    public class PortalSettingsEditor : EditorWindow
    {
        //         MightyPortalSettings settings;
        //         Texture2D tLogoBackground, tLogo, taLogo, pfLogo;
        //         Vector2 scrollPos;
        //         string taText = "Turn your Scene into an amazing, interactive Atlas with landmarks, birdseye views, and a powerful task management system!\n\nKeep organized, save time, and never get lost in your own scene again!",
        //  pfText = "We fixed the F key!  Remember that time you pressed F and you went further away, not closer?  Get to that object instantly with several levels of zoom, rotation and tilt.\n\nWill also perfectly align with a World Space Canvas so that it is straight in front of you, making alignment a far more pleasant experience.";

        //         public static EditorWindow window;

        //         void OnEnable()
        //         {
        //             settings = Resources.Load("PortalSettingsSO") as MightyPortalSettings;
        //             tLogo = Resources.Load<Texture2D>("Textures/PortalLogo");
        //             taLogo = Resources.Load<Texture2D>("Textures/TaskAtlasLogo");
        //             pfLogo = Resources.Load<Texture2D>("Textures/PerfectFLogo");

        //         }
        //         static Vector2 sv;
        //         public void OnGUI()
        //         {
        //             if (settings == null) OnEnable();


        //             EditorGUI.BeginChangeCheck();
        //             Material m = new Material(Shader.Find("Specular"));

        //             sv = GUILayout.BeginScrollView(sv);
        //             {
        //                 GUILayout.BeginHorizontal();
        //                 GUILayout.FlexibleSpace();
        //                 GUILayout.Box(tLogo, GUILayout.Width(320), GUILayout.Height(193));
        //                 GUILayout.FlexibleSpace();
        //                 GUILayout.EndHorizontal();

        //                 GUILayout.BeginVertical();

        //                 //scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(EditorGUIUtility.currentViewWidth - 4), GUILayout.Height(EditorGUIUtility.heig - 120));

        //                 EditorGUILayout.HelpBox("Is Mighty Portal Active?", MessageType.None);
        //                 settings.isActive = EditorGUILayout.Toggle("Active", settings.isActive);

        //                 EditorGUILayout.HelpBox("Choose which layers can be targetted by the Raycast", MessageType.None);

        //                 var layersSelection = EditorGUILayout.MaskField("Target Layers", LayerMaskToField(settings.layerMask), InternalEditorUtility.layers);
        //                 EditorGUILayout.HelpBox("Toggle Mode On means you hit the Enable keys and release, press Escape or complete travel to disable Scene Pilot.  Off means you hold down the Enable Keys and releasing them will disable.  If Scene Pilot keeps disabling (happens with resource intense situations), try Toggle Mode on!", MessageType.None);
        //                 settings.toggleMode = EditorGUILayout.Toggle("Toggle Mode", settings.toggleMode);
        //                 EditorGUILayout.HelpBox("When adjusting the final placement, should the mouse be inverted?", MessageType.None);
        //                 settings.invertMovement = EditorGUILayout.Toggle("Invert Movement", settings.invertMovement);
        //                 settings.invertRotation = EditorGUILayout.Toggle("Invert Rotation", settings.invertRotation);
        //                 settings.invertScrolWheel = EditorGUILayout.Toggle("Invert Scrollwheel", settings.invertScrolWheel);
        //                 EditorGUILayout.HelpBox("Which key(s) trigger Scene Pilot to start?", MessageType.None);
        //                 settings.keyStartA = (EventModifiers)EditorGUILayout.EnumPopup("Start Key 1", settings.keyStartA);
        //                 settings.keyStartB = (EventModifiers)EditorGUILayout.EnumPopup("Start Key 2", settings.keyStartB);
        //                 EditorGUILayout.HelpBox("Which key allows for precision movement?", MessageType.None);
        //                 settings.keyPrecision = (EventModifiers)EditorGUILayout.EnumPopup("Precision Key", settings.keyPrecision);
        //                 EditorGUILayout.HelpBox("How big should the preview images be?", MessageType.None);
        //                 settings.hoverBoxSize = EditorGUILayout.IntSlider("When Hovering", settings.hoverBoxSize, 0, 600);
        //                 settings.adjustBoxSize = EditorGUILayout.IntSlider("When Adjusting View", settings.adjustBoxSize, 0, 600);
        //                 EditorGUILayout.HelpBox("How transparent should the preview images be?", MessageType.None);
        //                 settings.hoverAlpha = EditorGUILayout.Slider("When Hovering", settings.hoverAlpha, 0f, 1f);
        //                 settings.adjustAlpha = EditorGUILayout.Slider("When Adjusting View", settings.adjustAlpha, 0f, 1f);
        //                 EditorGUILayout.HelpBox("Increase Performance?", MessageType.None);
        //                 settings.animateBoxes = EditorGUILayout.Toggle("Animate Preview", settings.animateBoxes);
        //                 settings.UseShader = EditorGUILayout.Toggle("Use Sphere Shader", settings.UseShader);
        //                 settings.shaderBrightness = EditorGUILayout.Slider("Shader Brightness", settings.shaderBrightness, -1f, 1f);
        //                 EditorGUILayout.HelpBox("Set how long the Undo Flight button will appear for.  Set to 0 to disable.", MessageType.None);
        //                 settings.undoTimerMax = EditorGUILayout.IntSlider("Undo Timer Seconds", settings.undoTimerMax, 0, 60);
        //                 EditorGUILayout.HelpBox("Warn me when I hover outside of the Scene Window?", MessageType.None);
        //                 settings.showOOB = EditorGUILayout.Toggle("Show warning", settings.showOOB);



        //                 if (EditorGUI.EndChangeCheck())
        //                 {
        //                     settings.layerMask = FieldToLayerMask(layersSelection);
        //                     EditorUtility.SetDirty(settings);
        //                     //AssetDatabase.SaveAssets();
        //                     //AssetDatabase.Refresh();
        //                 }

        //                 GUILayout.Space(16);
        //                 GUIStyle s = new GUIStyle();
        //                 s.fontSize = 14; s.fontStyle = FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter;
        //                 GUILayout.Label("Other Assets from ShrinkRay Entertainment!", s);
        //                 GUILayout.Space(16);

        //                 GUILayout.BeginHorizontal();
        //                 GUILayout.FlexibleSpace();
        //                 s.fontSize = 12; s.fontStyle = FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter;
        //                 GUILayout.Button(taLogo, GUIStyle.none, GUILayout.Width(256), GUILayout.Height(217));
        //                 // GUILayout.BeginVertical();
        //                 GUILayout.FlexibleSpace();
        //                 GUILayout.EndHorizontal();
        //                 EditorGUILayout.HelpBox(taText, MessageType.None);
        //                 if (GUILayout.Button("GET IT HERE"))
        //                 {
        //                     Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/task-atlas-185959?aid=1011lf9gY&pubref=ep");
        //                 }

        //                 GUILayout.Space(16);
        //                 GUILayout.BeginHorizontal();
        //                 GUILayout.FlexibleSpace();
        //                 s.fontSize = 12; s.fontStyle = FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter;
        //                 GUILayout.Button(pfLogo, GUIStyle.none, GUILayout.Width(128), GUILayout.Height(128));
        //                 // GUILayout.BeginVertical();
        //                 GUILayout.FlexibleSpace();
        //                 GUILayout.EndHorizontal();
        //                 EditorGUILayout.HelpBox(pfText, MessageType.None);
        //                 if (GUILayout.Button("GET IT HERE"))
        //                 {
        //                     Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/perfect-f-177783?aid=1011lf9gY&pubref=ep");
        //                 }


        //                 //EditorGUILayout.EndScrollView();

        //                 GUILayout.EndVertical();
        //             }
        //             GUILayout.EndScrollView();
        //         }

        //         private LayerMask FieldToLayerMask(int field)
        //         {
        //             LayerMask mask = 0;
        //             var layers = InternalEditorUtility.layers;
        //             for (int c = 0; c < layers.Length; c++)
        //             {
        //                 if ((field & (1 << c)) != 0)
        //                 {
        //                     mask |= 1 << LayerMask.NameToLayer(layers[c]);
        //                 }
        //             }
        //             return mask;
        //         }

        //         private int LayerMaskToField(LayerMask mask)
        //         {
        //             int field = 0;
        //             var layers = InternalEditorUtility.layers;
        //             for (int c = 0; c < layers.Length; c++)
        //             {
        //                 if ((mask & (1 << LayerMask.NameToLayer(layers[c]))) != 0)
        //                 {
        //                     field |= 1 << c;
        //                 }
        //             }
        //             return field;
        //         }

    }
}
//
#endif