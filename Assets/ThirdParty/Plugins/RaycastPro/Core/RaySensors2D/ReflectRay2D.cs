namespace RaycastPro.RaySensors2D
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
    using Editor;
#endif

    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(ReflectRay2D))]
    public sealed class ReflectRay2D : PathRay2D, IRadius
    {
        /// <summary>
        /// Get List of reflected hits.
        /// </summary>
        public readonly List<RaycastHit2D> RaycastHits = new List<RaycastHit2D>();

        [Tooltip("The number of ray reflection that will be cut off when reaching it. Negative numbers use free direction length.")]
        public int maxReflect = -1;
        
        [Tooltip("Reflect layer")]
        public LayerMask reflectLayer;

        [SerializeField] private float radius;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0, value);
        }

        private Vector2 point, _direction;
        private Vector3 _tPosition;
        private float distance;
        private RaycastHit2D h;
        private int DI;
        
        protected override void OnCast()
        {
            isDetect = false;
            UpdatePath();
            UpdateLiner();
        }

        protected override void UpdatePath()
        {
            PathPoints.Clear();
            RaycastHits.Clear();
            _tPosition = transform.position;
            point = _tPosition.To2D();
            _direction = Direction;
            PathPoints.Add(_tPosition);
            distance = direction.magnitude;
            DetectIndex = -1;
            DI = -1;
            hit = default;
            var queriesSetting = Physics2D.queriesStartInColliders;
            Physics2D.queriesStartInColliders = false;
            while (true)
            {
                DI++;
                if (maxReflect > 0 && DI+1 > maxReflect) break;
                h = new RaycastHit2D();
                if (radius > 0)
                {
                    h = Physics2D.CircleCast(point, radius,  _direction, distance, reflectLayer.value | detectLayer.value,
                        MinDepth, MaxDepth);
                }
                else
                {
                    h = Physics2D.Raycast(point, _direction, distance, reflectLayer.value | detectLayer.value,
                        MinDepth, MaxDepth); 
                }
                if (h)
                {
                    RaycastHits.Add(h);
                    PathPoints.Add(h.point);
                    if (detectLayer.InLayer(h.transform.gameObject))
                    {
                        DetectIndex = DI;
                        isDetect = FilterCheck(h);
                        break;
                    }
                    distance -= (h.point - point).magnitude;
                    point = h.point;
                    _direction = Vector3.Reflect(_direction, h.normal);
                    continue;
                }
                PathPoints.Add(point + _direction.normalized * distance);
                break;
            }
            Physics2D.queriesStartInColliders = queriesSetting;
        }
#if UNITY_EDITOR
        internal override string Info => "A 2D recursive reflection sensor that simulates a cascading reflective path. It operates by sequentially casting a Raycast (or CircleCast) within a loop. Upon striking a surface on the designated 'reflectLayer', it calculates a new trajectory based on the law of reflection and continues its journey. This process repeats, creating a chain of reflections, until it either strikes a final target on the 'detectLayer', reaches a configurable maximum reflection limit, or travels its full distance. The entire ricochet trajectory is meticulously recorded, making it ideal for creating complex 2D laser puzzles, light-based mechanics, or ricochet simulations."
                                         + HAccurate + HPathRay + HRecursive + HIRadius;

        internal override void OnGizmos()
        {
            EditorUpdate();
            FullPathDraw(radius, true);
            if (!hit) return;
            DrawNormal2D(hit, z);
            DrawNormalFilter();
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                DirectionField(_so);
                RadiusField(_so);
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(maxReflect)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(reflectLayer)), CReflectLayer.ToContent(TReflectLayer));
            }

            if (hasGeneral) GeneralField(_so);
            if (hasEvents) EventField(_so);
            if (hasInfo)
            {
                InformationField(() => RaycastHits.ForEach(r =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(r.transform.name);
                    GUILayout.Label(r.point.ToString());
                    GUILayout.EndHorizontal();
                }));
            }
        }
#endif
    }
}