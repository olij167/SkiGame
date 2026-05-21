using UnityEngine.Serialization;

namespace RaycastPro.RaySensors
{
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [HelpURL("https://www.youtube.com/watch?v=OdonhX2GQII")]
    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(ArcRay))]
    public sealed class ArcRay : PathRay, IRadius
    {
        public enum InputType { Velocity, Target }
        public InputType inputType = InputType.Velocity;
        
        [Tooltip("Number of segments to divide the arc path.")]
        public int segments = 8;
        
        [Tooltip("Total duration over which the arc is simulated.")]
        public float elapsedTime = 5f;

        public Transform target;
        
        [FormerlySerializedAs("velocityLocal")] [Tooltip("If enabled, the velocity will be applied in local space.")]
        public bool accelerationLocal;

        [SerializeField, Tooltip("Collision detection radius (will be clamped to non-negative).")]
        private float radius;
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0,value);
        }
        [FormerlySerializedAs("velocity")] [Tooltip("Initial velocity vector for the arc trajectory.")]
        public Vector3 acceleration;

        protected override void OnCast()
        {
            UpdatePath();
            if (pathCast)
            {
                DetectIndex = AdvancePathCast(startRadius, radius);
            }
        }
        /// <summary>
        /// Using in Gizmo and OnCast Separately
        /// </summary>
        /// <returns></returns>
        protected override void UpdatePath()
        {
            PathPoints.Clear();

            switch (inputType)
            {
                case InputType.Velocity:
                {
                    _tPos = transform.position;
                    PathPoints.Add(_tPos);
                    g = accelerationLocal ? transform.TransformDirection(acceleration) : acceleration;
                    _dir = Direction;
                    for (var i = 1; i <= segments; i++)
                    {
                        t = (float) i / segments * elapsedTime;
                        _pos = _tPos + (_dir * t + g * (t * t) / 2);
                        PathPoints.Add(_pos);
                    }

                    break;
                }
                case InputType.Target:
                    if (target == null) break;

                    PathPoints.Clear();

                    Vector3 start = transform.position;
                    Vector3 end = target.position;
                    Vector3 dir = Direction;

                    // محاسبه ارتفاع آرک
                    Vector3 arcHeightVector;

                    if (local)
                    {
                        // ارتفاع آرک به سمت transform.up با مقدار y
                        arcHeightVector = transform.up * dir.y;
                    }
                    else
                    {
                        // ارتفاع آرک به سمت global up
                        arcHeightVector = Vector3.up * dir.y;
                    }

                    // محاسبه انحراف جانبی فقط اگر local false باشد
                    Vector3 lateralOffset = Vector3.zero;
                    if (!local)
                    {
                        lateralOffset = Vector3.right * dir.x;
                    }

                    // اضافه کردن انحراف جانبی به نقطه پایان (به صورت جهانی)
                    Vector3 finalEnd = end + lateralOffset;

                    // محاسبه نقطه میانی با ارتفاع آرک
                    Vector3 mid = Vector3.Lerp(start, finalEnd, 0.5f) + arcHeightVector;

                    for (int i = 0; i <= segments; i++)
                    {
                        float t = (float)i / segments;

                        // منحنی بزیه درجه دو
                        Vector3 point = Mathf.Pow(1 - t, 2) * start +
                                        2 * (1 - t) * t * mid +
                                        Mathf.Pow(t, 2) * finalEnd;

                        PathPoints.Add(point);
                    }
                    break;

            }
        }
        private float t;
        private Vector3 g, _dir, _tPos, _pos;
        
#if UNITY_EDITOR
        internal override string Info => "Casts a ray based on the incoming velocity and returns the hit information." + HAccurate + HDirectional + HPathRay + HIRadius;

        internal override void OnGizmos()
        {
            EditorUpdate();
            
            AdvancePathDraw(startRadius,  radius, true);
            
            if (hit.transform) DrawNormal(hit.point, hit.normal, hit.transform.name);
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                BeginHorizontal();
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(direction)), "Velocity".ToContent(""));
                LocalField(_so.FindProperty(nameof(local)));
                BeginHorizontal();
                
                var prop = _so.FindProperty(nameof(inputType));
                PropertyEnumField(prop, 2,  "Input Type".ToContent("Input Type"), new GUIContent[]
                {
                    "Velocity".ToContent("Velocity"),
                    "Target".ToContent("Target"),
                });
                

                EditorGUILayout.PropertyField(_so.FindProperty(nameof(segments)));
                segments = Mathf.Max(1, segments);
                
                if (inputType == InputType.Velocity)
                {
                    BeginHorizontal();
                    EditorGUILayout.PropertyField(_so.FindProperty(nameof(acceleration)));
                    LocalField(_so.FindProperty(nameof(accelerationLocal)));
                    EndHorizontal();
                    EditorGUILayout.PropertyField(_so.FindProperty(nameof(elapsedTime)));
                }
                else if (inputType == InputType.Target)
                {
                    EditorGUILayout.PropertyField(_so.FindProperty(nameof(target)));
                }
                
                StartRadiusField(_so);
                RadiusField(_so);
            }

            if (hasGeneral) PathRayGeneralField(_so);

            if (hasEvents) EventField(_so);

            if (hasInfo) InformationField();
        }
#endif
    }
}