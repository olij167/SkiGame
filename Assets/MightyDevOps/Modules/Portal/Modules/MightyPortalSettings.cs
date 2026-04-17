#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MightyPortal
{
    public class MightyPortalSettings : ScriptableObject
    {
        [SerializeField]
        public LayerMask layerMask = 311;
        [SerializeField]
        public bool invertMovement = true, invertRotation = true, invertScrolWheel = true, toggleMode = true;
        [SerializeField]
        public EventModifiers keyStartA = EventModifiers.Shift, keyStartB = EventModifiers.Control, keyPrecision = EventModifiers.Alt;
        [SerializeField]
        public int hoverBoxSize = 150, adjustBoxSize = 300;
        [SerializeField]
        public float hoverAlpha = .75f, adjustAlpha = 1f;
        [SerializeField]
        public bool animateBoxes = true;
        [SerializeField]
        public bool showOOB = true;
        [SerializeField]
        public int undoTimerMax = 30;
        [SerializeField]
        public bool UseShader = true;
        [SerializeField]
        public float shaderBrightness = 0.0f;
        [SerializeField]
        public bool isActive = true;

        public bool isDeleted = false;

        private void OnDestroy()
        {
            isDeleted = true;
        }

        // public static void Save()
        // {

        //     PortalSettings asset = ScriptableObject.CreateInstance<PortalSettings>();

        //     AssetDatabase.CreateAsset(asset, "Assets/PortalSettingsSO");
        //     AssetDatabase.SaveAssets();
        // }
    }
}
#endif