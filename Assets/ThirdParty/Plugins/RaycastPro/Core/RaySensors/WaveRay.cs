
using UnityEngine.Serialization;

namespace RaycastPro.RaySensors
{
#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif

    using UnityEngine;
    using Random = UnityEngine.Random;
    
    [AddComponentMenu("RaycastPro/Rey Sensors/"+nameof(WaveRay))]
    public sealed class WaveRay : PathRay, IRadius
    {
        public TimeMode timeMode = TimeMode.DeltaTime;
        public int segments = 8;
        [SerializeField] private float radius;
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0,value);
        }
        [Tooltip("Pulse amount in unit of second.")]
        public float waveSpeed = 1f;
        [Tooltip("The bending wave amount based on power slope.\\n\\\n[0: breaked] \\n\\\n[1: normal] \\n\\\n[2: more harmonic]")]
        public float power = 1;
        [Tooltip("Pulse amount in unit of second.")]
        public Vector2 noise;
        public Vector2 scale = new Vector2(0f, 1f);
        public AnimationCurve clumpY = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve clumpX = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float offsetX;
        [FormerlySerializedAs("digitStep")] public int quantizeSteps;
        private float cycle;

        private float Function(float x)
        {
            var v = Mathf.Sin(-cycle + x * Mathf.PI / 8f);

            if (!Mathf.Approximately(power, 1f))
                v = Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), power);

            if (quantizeSteps > 0)
                v = Mathf.Round(v * quantizeSteps) / quantizeSteps;

            return v;
        }

        private float main, absMain, pos, directionY, directionX, time, scaleX, scaleY;
        private Vector3 vec;
        private Vector3 Function3D(float i, float step)
        {
            float posZ = i * step;
            float t = i / segments;

            float dz = Mathf.Abs(direction.z) < 1e-4f ? 1e-4f : direction.z;

            float dirX = direction.x * posZ;
            float dirY = direction.y * posZ / dz;

            float waveX = Function(offsetX + i);
            float waveY = Function(i);

            float noiseX = noise.x != 0f
                ? Mathf.PerlinNoise(i * 0.1f, cycle) * noise.x
                : 0f;

            float noiseY = noise.y != 0f
                ? Mathf.PerlinNoise(i * 0.1f, cycle + 10f) * noise.y
                : 0f;

            float sx = scale.x * clumpX.Evaluate(t) * (waveX + noiseX);
            float sy = scale.y * clumpY.Evaluate(t) * (waveY + noiseY);

            Vector3 localVec = new Vector3(
                sx + dirX,
                sy + dirY,
                posZ
            );

            return transform.position + (local
                ? transform.TransformDirection(localVec)
                : localVec);
        }

        protected override void OnCast()
        {
            UpdatePath();
            if (pathCast) DetectIndex = AdvancePathCast(startRadius, radius);
        }

        private float dt, step;
        protected override void UpdatePath()
        {
            PathPoints.Clear();

            if (segments <= 0)
                return;

            float dt = GetDelta(timeMode);

            cycle += dt * waveSpeed;
            cycle %= Mathf.PI * 2f;

            float step = direction.z / segments;

            for (int i = 0; i <= segments; i++)
                PathPoints.Add(Function3D(i, step));
        }
#if UNITY_EDITOR
        internal override string Info => "Casts a ray along a path defined by a sinusoidal function and returns the hit information." + HAccurate + HDirectional + HPathRay + HIRadius;
        internal override void OnGizmos()
        {
            if (IsSceneView && !IsPlaying) cycle = Time.realtimeSinceStartup*waveSpeed % Mathf.PI*2;
            EditorUpdate();
            AdvancePathDraw(startRadius, radius,  true);
            if (hit.transform) DrawNormal(hit.point, hit.normal, hit.transform.name);
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true, bool hasInfo = true)
        {
            if (hasMain)
            {
                DirectionField(_so);
                PropertyMaxIntField(_so.FindProperty(nameof(segments)), CSegments.ToContent(TSegments), 1);
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(waveSpeed)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(scale)));
                EditorGUILayout.CurveField(_so.FindProperty(nameof(clumpX)), RCProEditor.Aqua, new Rect(0, 0, 1, 1), CClumpX.ToContent(CClumpX));
                EditorGUILayout.CurveField(_so.FindProperty(nameof(clumpY)), RCProEditor.Aqua, new Rect(0, 0, 1, 1), CClumpY.ToContent(CClumpY));
                PropertySliderField(_so.FindProperty(nameof(offsetX)), 0f, Mathf.PI * 2f, "OffsetX".ToContent("Wave OffsetX in unit of \"Radians\"."));
                PropertySliderField(_so.FindProperty(nameof(power)), 0f, 6f, CPower.ToContent(CPower));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(noise)));
                PropertySliderField(_so.FindProperty(nameof(quantizeSteps)), 0, 3, CDigitStep.ToContent(CDigitStep), null);
                StartRadiusField(_so);
                RadiusField(_so);
                PropertyTimeModeField(_so.FindProperty(nameof(timeMode)));
            }

            if (hasGeneral) PathRayGeneralField(_so);

            if (hasEvents) EventField(_so);

            if (hasInfo) InformationField();
        }
#endif
    }
}
