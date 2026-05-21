using UnityEngine.Serialization;

namespace RaycastPro.RaySensors
{
    using UnityEngine;

#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif
    
    [AddComponentMenu("RaycastPro/Rey Sensors/"+nameof(PointerRay))]
    public sealed class PointerRay : RaySensor, IRadius
    {
        [Tooltip(
            "Reference camera used to generate the screen-space ray.\n\n" +
            "Example:\n" +
            "• Main Camera in a first-person or third-person setup.\n" +
            "• A custom cinematic camera for editor tools.\n\n" +
            "This camera is used to convert the mouse position into a world-space ray."
        )]
        public Camera mainCamera;
        
        
        [SerializeField]
        private float radius = 0.4f;
        
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0,value);
        }
        
        [Tooltip(
            "When enabled, the ray origin is taken from the camera position instead of the component transform.\n\n" +
            "Example:\n" +
            "• Enabled → FPS-style interaction where ray starts from player's view.\n" +
            "• Disabled → Ray starts from the object itself (turrets, tools, AI sensors).\n\n" +
            "This only affects the ray origin, not the hit direction."
        )]
        public bool cameraBase = false;
        
        [Tooltip(
            "Enables a two-step raycasting process.\n\n" +
            "Step 1:\n" +
            "• A ray is cast from the camera through the mouse position.\n" +
            "• The mouse hit point is detected in the world.\n\n" +
            "Step 2:\n" +
            "• A second ray is cast from the component origin toward that point.\n\n" +
            "Example:\n" +
            "• Enabled → Accurate aiming from weapon muzzle while using mouse position.\n" +
            "• Disabled → Direct ray from origin toward mouse direction.\n\n" +
            "Useful when camera and object are not in the same position."
        )]
        public bool parallaxCorrectedCast = true;
        
        public Camera.MonoOrStereoscopicEye eyeType = Camera.MonoOrStereoscopicEye.Mono;
        private Ray mouseRay;

        public override Vector3 RawTip { get; }
        public override Vector3 Tip => mouseRay.origin + mouseRay.direction * direction.z;
        public override float RayLength => direction.z;
        public override Vector3 Base
        {
            get
            {
                if (cameraBase)
                    return mainCamera.transform.position;
                else
                    return transform.position;
            }
        }

        private Vector3 input;
        private RaycastHit mouseHit;
        private Vector3 secondDir;
        private Vector3 p1, p2;
        private Vector3 pos;
        
        private void Reset()
        {
            mainCamera = Camera.main;
            
            if (!mainCamera)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }
        
        protected override void OnCast()
        {
            if (!mainCamera)
                return;

#if UNITY_EDITOR
            GizmoGate = null;
#endif

            ProcessCast();

#if UNITY_EDITOR
            if (IsPlaying)
                RegisterGizmos();
#endif
        }
        
        Vector3 CalculateSecondDirection()
        {
            if (parallaxCorrectedCast &&
                Physics.Raycast(
                    mouseRay.origin,
                    mouseRay.direction,
                    out mouseHit,
                    direction.z,
                    detectLayer.value,
                    triggerInteraction))
            {
                return mouseHit.point - pos;
            }

            return (mouseRay.origin + mouseRay.direction * direction.z) - pos;
        }
        void PerformPhysicsCast()
        {
            if (radius > 0f)
            {
                Physics.SphereCast(
                    pos,
                    radius,
                    secondDir,
                    out hit,
                    direction.z,
                    detectLayer.value,
                    triggerInteraction);
            }
            else
            {
                Physics.Raycast(
                    pos,
                    secondDir,
                    out hit,
                    direction.z,
                    detectLayer.value,
                    triggerInteraction);
            }
        }

        void ProcessCast()
        {
#if ENABLE_INPUT_SYSTEM
            input = Mouse.current.position.ReadValue();
#else
    input = Input.mousePosition;
#endif

            mouseRay = mainCamera.ScreenPointToRay(input, eyeType);

            pos = Base;
            secondDir = CalculateSecondDirection();

            PerformPhysicsCast();
        }

#if UNITY_EDITOR
        void RegisterGizmos()
        {
            GizmoGate += DrawCastGizmos;
        }
        void DrawCastGizmos()
        {
            GUI.color = HelperColor;

            var mt = cameraBase ? mainCamera.transform : transform;

            Vector3 start = mt.position;
            Vector3 end = IsPlaying
                ? start + secondDir.normalized * direction.z
                : start + mt.forward * direction.z;

            GizmoColor = Performed ? DetectColor : DefaultColor;

            DrawRadiusLines(mt, start, end);

            Handles.DrawWireDisc((start + end) * 0.5f, end - start, radius);
            Handles.DrawWireDisc(end, end - start, radius);

            if (IsPlaying)
                DrawDetectLine(start, end, hit, Performed);

            if (hit.transform)
                DrawNormal(hit);
        }
        void DrawRadiusLines(Transform mt, Vector3 p1, Vector3 p2)
        {
            DrawLineZTest(p1 + mt.right * radius, p2 + mt.right * radius);
            DrawLineZTest(p1 - mt.right * radius, p2 - mt.right * radius);
            DrawLineZTest(p1 + mt.up    * radius, p2 + mt.up    * radius);
            DrawLineZTest(p1 - mt.up    * radius, p2 - mt.up    * radius);
        }
        internal override string Info => "Casts a ray towards the current mouse cursor position, originating from either the main camera or a specified object, and returns the hit information." + HAccurate + HIRadius + HDependent;
        internal override void OnGizmos() => EditorUpdate();

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                BeginHorizontal();
                var propCamera = _so.FindProperty(nameof(mainCamera));
                EditorGUILayout.PropertyField(propCamera);
                if (!mainCamera && GUILayout.Button("Main", GUILayout.Width(50f)))
                {
                    propCamera.objectReferenceValue = Camera.main;
                }
                EndHorizontal();
                DirectionField(_so);
                RadiusField(_so);
                
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(cameraBase)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(parallaxCorrectedCast)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(eyeType)));
            }
            
            if (hasGeneral) GeneralField(_so);
            if (hasEvents) EventField(_so);
            if (hasInfo) InformationField();
        }
#endif
    }
}
