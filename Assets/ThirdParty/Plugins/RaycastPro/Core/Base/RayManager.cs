using System;
using System.Collections.Generic;

namespace RaycastPro
{
    using System.Linq;
    using UnityEngine;
#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif

    [ExecuteInEditMode]
    [AddComponentMenu("RaycastPro/Utility/" + nameof(RayManager))]
    public sealed class RayManager : RaycastCore
    {
        [SerializeField] private RaycastCore[] cores;

        [SerializeField] private bool[] Foldouts = Array.Empty<bool>();

        public override bool Performed
        {
            get => cores.All(r => r.Performed);
            protected set { }
        }

        [ExecuteAlways]
        protected void OnTransformChildrenChanged()
        {
            Refresh();
        }
        protected void Refresh()
        {
            cores = GetComponentsInChildren<RaycastCore>(true).Where(c => !(c is RayManager)).ToArray();
            Array.Resize(ref Foldouts, cores.Length);
        }
        protected void Reset()
        {
            Refresh();
            
            styleH = new GUIStyle
            {
                margin = new RectOffset(0, 0, 4, 4),
                padding = new RectOffset(0, 0, 2, 4),
                stretchWidth = false,
                wordWrap = true,
            };

            styleM = new GUIStyle
            {
                margin = new RectOffset(1, 1, 4, 4),
                padding = new RectOffset(5, 5, 4, 4),
                alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
        }

        private GUIStyle styleH, styleM;
        protected override void OnCast()
        {
            
        }
        


#if UNITY_EDITOR
        

        internal override string Info =>
            "Ray Manager is a control and orchestration component that automatically detects and manages all RaycastCore components in its child hierarchy.\n\n" +
            "It does not perform raycasts itself. Instead, it acts as a centralized controller, allowing you to enable, disable, test, debug, and visualize multiple rays from a single Inspector panel.\n\n" +
            "Use this component when you need to coordinate or debug multiple ray-based systems together, such as layered detection, compound ray logic, or grouped ray behaviors."
            + HUtility + HDependent;
        
        internal override void OnGizmos()
        { }
        
        [SerializeField]
        private bool showMain = true;
        [SerializeField]
        private bool showGeneral = false;

        private int index;

        #region Init

        SerializedObject[] coreSOs;
        static GUIStyle foldoutStyle;
        static GUIStyle coreHeaderStyle;
        static GUIStyle toggleButtonStyle;
        static GUIStyle foldoutPanelStyle;
        void InitEditorPanel()
        {
            if (foldoutStyle == null)
            {
                foldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    margin = new RectOffset(6, 6, 0, 0),
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (coreSOs == null || coreSOs.Length != cores.Length)
            {
                coreSOs = new SerializedObject[cores.Length];
                for (int i = 0; i < cores.Length; i++)
                    coreSOs[i] = new SerializedObject(cores[i]);
            }

            // فقط برای متن وسط‌چین Foldout
            if (coreHeaderStyle == null)
            {
                coreHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 24
                };
            }

            // پنل داخلی بدون بک‌گراند
            if (foldoutPanelStyle == null)
            {
                foldoutPanelStyle = new GUIStyle
                {
                    padding = new RectOffset(10, 10, 6, 8)
                };
            }

            if (toggleButtonStyle == null)
            {
                toggleButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fixedHeight = 20,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        #endregion
     


internal override void EditorPanel(
    SerializedObject _so,
    bool hasMain = true,
    bool hasGeneral = true,
    bool hasEvents = true,
    bool hasInfo = true)
{
    InitEditorPanel();

    // Global Toggles
    BeginVerticalBox();
    EditorGUILayout.PropertyField(_so.FindProperty(nameof(showMain)));
    EditorGUILayout.PropertyField(_so.FindProperty(nameof(showGeneral)));
    EndVertical();

    for (int i = 0; i < cores.Length; i++)
    {
        var core   = cores[i];
        var coreSO = coreSOs[i];

        BeginVerticalBox();

        // =========================
        // Header (NO background)
        // =========================
        Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(26));

        GUILayout.Space(6);

        // Foldout
        Foldouts[i] = EditorGUILayout.Foldout(
            Foldouts[i],
            core.name.ToContent(RCProEditor.GetInfo(core)),
            true,
            foldoutStyle
        );

        GUILayout.FlexibleSpace();

        // =========================
        // Active Button
        // =========================
        GUI.backgroundColor = core.gameObject.activeSelf
            ? new Color(0.25f, 0.55f, 0.25f)
            : new Color(0.35f, 0.2f, 0.2f);

        if (GUILayout.Button(
                core.gameObject.activeSelf ? "Active" : "Inactive",
                GUILayout.Width(70)))
        {
            Undo.RecordObject(core.gameObject, "Toggle Core Active");
            core.gameObject.SetActive(!core.gameObject.activeSelf);
        }

        // =========================
        // Enabled Button
        // =========================
        GUI.backgroundColor = core.enabled
            ? new Color(0.25f, 0.45f, 0.65f)
            : new Color(0.25f, 0.25f, 0.25f);

        if (GUILayout.Button(
                core.enabled ? "Enabled" : "Disabled",
                GUILayout.Width(75)))
        {
            Undo.RecordObject(core, "Toggle Core Enabled");
            core.enabled = !core.enabled;
        }

        GUI.backgroundColor = Color.white;

        // =========================
        // Gizmos
        // =========================
        if (core.gameObject.activeInHierarchy)
        {
            var gizmosProp = coreSO.FindProperty("gizmosUpdate");

            if (GUILayout.Button(core.gizmosUpdate.ToString(), GUILayout.Width(70)))
            {
                coreSO.Update();
                gizmosProp.enumValueIndex =
                    (gizmosProp.enumValueIndex + 1) % gizmosProp.enumNames.Length;
                coreSO.ApplyModifiedProperties();
            }
        }
        else
        {
            GUILayout.Box("Off", RCProEditor.BoxStyle, GUILayout.Width(70), GUILayout.Height(20));
        }

        // =========================
        // Cast Button
        // =========================
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor =
            (core.Performed ? DetectColor : BlockColor).Alpha(0.45f);

        if (GUILayout.Button("Cast", GUILayout.Width(60)))
            core.TestCast();

        GUI.backgroundColor = prevColor;

        EditorGUILayout.EndHorizontal();

        // =========================
        // Divider Line
        // =========================
        Rect lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(
            lineRect,
            EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(0f, 0f, 0f, 0.15f)
        );

        // =========================
        // Inner Inspector
        // =========================
        if (Foldouts[i])
        {
            coreSO.Update();
            EditorGUI.BeginChangeCheck();

            core.EditorPanel(coreSO, showMain, showGeneral, false, false);

            if (EditorGUI.EndChangeCheck())
                coreSO.ApplyModifiedProperties();
        }

        EndVertical();
    }
}
#endif
    }
}