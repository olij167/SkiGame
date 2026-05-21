using System.Collections.Generic;
using UnityEngine.Serialization;

namespace RaycastPro.RaySensors
{
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [IsNew]
    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(BounceRay))]
    public sealed class BounceRay : PathRay, IRadius
    {
        [Header("Bounce Settings")] public int maxBounces = 3;

        [Header("Path Quality")]
        [Tooltip(
            "Controls trajectory subdivision density.\n" +
            "Higher values produce smoother and more accurate paths at higher cost."
        )]
        [Range(0.1f, 10f)]
        public float segmentDensity = 0.5f;

        [Range(0f, 2f)] public float elasticity = 0.7f; // energy preserved on bounce

        [Range(0f, 1f)] public float friction = 0.9f; // tangential energy loss

        public Vector3 acceleration; // gravity or custom force
        public bool accelerationLocal;

        [Header("Time")] public float elapsedTime = 6f;

        [Header("Collision")] public LayerMask bonusLayer;

        [SerializeField] private float radius;
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0, value);
        }

        protected override void OnCast()
        {
            UpdatePath();
            if (pathCast)
                DetectIndex = AdvancePathCast(startRadius, radius);
        }

        protected override void UpdatePath()
        {
            PathPoints.Clear();

            var pos = transform.position;
            var vel = Direction;

            float timeLeft = elapsedTime;

            for (int bounce = 0; bounce <= maxBounces && timeLeft > 0f; bounce++)
            {
                if (!SimulateSegment(ref pos, ref vel, ref timeLeft))
                    break;
            }
        }

        private bool SimulateSegment(ref Vector3 pos, ref Vector3 vel, ref float timeLeft)
        {
            PathPoints.Add(pos);

            // resolve acceleration space correctly
            var acc = accelerationLocal
                ? transform.TransformDirection(acceleration)
                : acceleration;

            int segments = Mathf.Clamp(
                Mathf.CeilToInt(vel.magnitude * timeLeft * segmentDensity),
                6,
                128
            );

            float dt = timeLeft / segments;

            for (int i = 0; i < segments; i++)
            {
                Vector3 prevPos = pos;

                // integrate (correct space)
                pos += vel * dt + acc * (dt * dt * 0.5f);
                vel += acc * dt;

                Vector3 dir = pos - prevPos;
                float dist = dir.magnitude;

                if (dist <= Mathf.Epsilon)
                    continue;

                RaycastHit hit;
                bool collided = Physics.Raycast(prevPos, dir.normalized, out hit, dist, bonusLayer, triggerInteraction);

                if (collided)
                {
                    pos = hit.point;
                    PathPoints.Add(pos);

                    float ratio = hit.distance / dist;
                    float consumedTime = dt * ratio;
                    timeLeft -= consumedTime;

                    vel = Vector3.Reflect(vel, hit.normal) * elasticity;
                    vel *= friction;

                    return true;
                }

                PathPoints.Add(pos);
                timeLeft -= dt;

                if (timeLeft <= 0f)
                    break;
            }

            return false;
        }

        private float t;
        private Vector3 g, _dir, _tPos, _pos;

#if UNITY_EDITOR
        internal override string Info =>
            "A physics-driven bouncing ray that predicts a dynamic trajectory over time by incrementally simulating motion with velocity, acceleration, and surface interaction, subdividing the path into adaptive segments based on speed and duration to ensure accuracy, reflecting velocity on impact using configurable elasticity and friction, supporting multiple consecutive bounces within a defined time budget, and generating a continuous path suitable for advanced trajectory visualization, predictive aiming, ricochet mechanics, and editor-safe gizmo rendering."
    + HAccurate + HDirectional + HPathRay + HIRadius;

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
                EndHorizontal();
                BeginHorizontal();
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(acceleration)));
                LocalField(_so.FindProperty(nameof(accelerationLocal)));
                EndHorizontal();
                BeginVerticalBox();
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(bonusLayer)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(elapsedTime)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(segmentDensity)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(maxBounces)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(elasticity)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(friction)));
                EndVertical();
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(startRadius)));
                RadiusField(_so);
            }

            if (hasGeneral) PathRayGeneralField(_so);

            if (hasEvents) EventField(_so);

            if (hasInfo) InformationField();
        }
#endif
    }
}