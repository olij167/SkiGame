/// FIX Allocating
namespace RaycastPro.RaySensors2D
{
    using UnityEngine;

#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif

    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(CurveRay2D))]
    public sealed class CurveRay2D : PathRay2D, IRadius
    {
        public int segments = 16;
        [SerializeField] public float radius = 0f;
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0,value);
        }
        public AnimationCurve clumpX = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve clumpY = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        protected override void OnCast()
        {
            UpdatePath();
            if (pathCast)
            {
                DetectIndex = AdvancePathCast(out hit, radius);
                isDetect = FilterCheck(hit);
            }
        }
        private float step;
        
        private Vector3 curve;

        protected override void UpdatePath()
        {
            PathPoints.Clear();
            step = direction.x / segments;
            for (int i = 0; i <= segments; i++)
            {
                step = (float) i / segments;
                curve.x = clumpX.Evaluate(step) * direction.x;
                curve.y = clumpY.Evaluate(step) * direction.y;
                PathPoints.Add(transform.position + (local ? transform.TransformDirection(curve) : curve));
            }
        }

#if UNITY_EDITOR
        internal override string Info => "A highly artistic, procedural path sensor that allows for the visual design of complex trajectories using AnimationCurves. It generates a path by independently evaluating two separate curves for the X and Y axes, enabling the creation of a vast array of custom shapes like spirals, waves, or S-curves. The entire generated path can be oriented either in world space or relative to the sensor's local transform. After constructing the custom path, it performs a cast along the full geometry to detect collisions, making it perfect for creating unique, stylized projectile movements or elaborate detection patterns."
                                         +HAccurate+HDirectional+HPathRay+HIRadius;
        internal override void OnGizmos()
        {

            EditorUpdate();
            AdvancePathDraw(radius, true);
            if (hit) DrawNormal(hit.point.ToDepth(z), hit.normal, hit.transform.name);
            DrawNormalFilter();
        }
        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                DirectionField(_so);
                PropertyMaxIntField(_so.FindProperty(nameof(segments)), CSegments.ToContent(TSegments), 1);
                EditorGUILayout.CurveField(_so.FindProperty(nameof(clumpX)), RCProEditor.Aqua, new Rect(0, 0, 1, 1), CClumpX.ToContent(CClumpX));
                EditorGUILayout.CurveField(_so.FindProperty(nameof(clumpY)), RCProEditor.Aqua, new Rect(0, 0, 1, 1), CClumpY.ToContent(CClumpY));
                StartRadiusField(_so);
                RadiusField(_so);
            }

            if (hasGeneral) PathRayGeneralField(_so);
            if (hasEvents) EventField(_so);
            if (hasInfo) HitInformationField();
        }
#endif
    }
}