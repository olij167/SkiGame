namespace RaycastPro.Detectors2D
{
    using System;
    using UnityEngine;

#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif

    [AddComponentMenu("RaycastPro/Detectors/"+nameof(RangeDetector2D))]
    public sealed class RangeDetector2D : ColliderDetector2D, IRadius, IPulse
    {
        [SerializeField] private float radius = 2f;

        [SerializeField] private bool local = true;
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0,value);
        }
        public float minRadius = .4f;
        public float arcAngle = 360;
        [SerializeField] private bool limited;
        [SerializeField] private int limitCount = 3;
        public bool Limited
        {
            get => limited;
            set
            {
                limited = value;
                if (value)
                {
                    colliders = new Collider2D[limitCount];
                }
            }
        }
        public int LimitCount
        {
            get => limitCount;
            set
            {
                limitCount = Mathf.Max(0,value);
                colliders = new Collider2D[limitCount];
            }
        }

        [SerializeField] private Collider2D[] colliders = Array.Empty<Collider2D>();
        
        private Collider2D nearestMember;
        private Collider2D furthestMember;

        /// <summary>
        /// Get The calculated nearest member (Optimized for get in update)
        /// </summary>
        public Collider2D NearestMember => nearestMember;
        public Collider2D FurthestMember => FurthestMember;

        private Vector3 _tPos;
        protected override void OnCast()
        {
            PreviousColliders = DetectedColliders.ToArray();
            
#if UNITY_EDITOR
            CleanGate();
#endif

            _tPos = transform.position;
            if (limited)
            {
                for (var i = 0; i < colliders.Length; i++) colliders[i] = null;
                Physics2D.OverlapCircleNonAlloc(_tPos, radius, colliders, detectLayer.value, MinDepth, MaxDepth);    
            }
            else
            {
                colliders = Physics2D.OverlapCircleAll(_tPos, radius, detectLayer.value, MinDepth, MaxDepth);
            }

            Clear();

            tempAngle = local ? transform.right : Vector3.right;
            _tDis = Mathf.Infinity;
            foreach (var c in colliders)
            {
                if (!TagPass(c)) continue;
                if (IsIgnoreSolver)
                {
#if UNITY_EDITOR
                    PassColliderGate(c);
#endif
                    DetectedColliders.Add(c);
                    
                    continue;
                }
                TDP = DetectFunction(c);
                angle = Vector2.Angle(tempAngle, TDP-_tPos);
                if (angle > arcAngle/2) continue;
                _distance = (_tPos - TDP).sqrMagnitude;
                if (_distance > radius*radius) continue;
                if (_distance < minRadius*minRadius) continue;
                _blockHit = Physics2D.Linecast(_tPos, TDP, blockLayer.value, MinDepth, MaxDepth);
                
                Debug.Log(_blockHit);
#if UNITY_EDITOR
                PassGate(c, TDP, _blockHit);
#endif
                if (!_blockHit || _blockHit.transform == c.transform)
                {
                    if (_distance <= _tDis)
                    {
                        _tDis = _distance;
                        nearestMember = c;
                    }

                    ColliderPass(c);
                }
            }
            EventPass();
        }

        private float _distance, angle;
        private Vector3 tempAngle;
        
#if UNITY_EDITOR
        internal override string Info => "A sophisticated 2D arc sector sensor that defines a detection area shaped like an annular wedge (a slice of a donut). It operates using a highly-efficient, two-phase process: a broad-phase OverlapCircle to gather potential candidates, followed by a meticulous narrow-phase that filters targets based on three criteria: if they fall within the specified 'arcAngle', if they are located between the 'minRadius' and 'maxRadius', and if they have a clear line-of-sight (unobstructed by the 'blockLayer'). Beyond just providing a list of valid targets, it also identifies the single nearest member. This component is further enhanced with a performance-optimizing non-allocating mode and support for both world-space and local-space orientation."
                                         +HAccurate+HCDetector+HLOS_Solver+HIRadius+HIPulse+HINonAllocator;
        private Color col;
        internal override void OnGizmos()
        {
            EditorUpdate();

            Handles.color = DetectedColliders.Count > 0 ? DetectColor : DefaultColor;
            Handles.DrawWireDisc(transform.position, Vector3.forward, radius);
            col = Handles.color;
            col.a = .4f;
            Handles.color = col;
            DrawDepthCircle(radius);
            if (IsIgnoreSolver) return;
            DrawFocusLine();
            tempAngle = local ? transform.right.To2D() : Vector2.right;
            col.a = .1f;
            Handles.color = col;
            DrawSolidArc(transform.position, Vector3.forward, tempAngle, arcAngle, radius);
            col = HelperColor;
            col.a = .2f;
            Handles.color = col;
            DrawSolidArc(transform.position, Vector3.forward, tempAngle, arcAngle, minRadius);
        }
        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                BeginHorizontal();
                RadiusField(_so);
                LocalField(_so.FindProperty("local"));
                EndHorizontal();
                GUI.enabled = !IsIgnoreSolver;
                RadiusField(_so, nameof(minRadius), CMinRadius.ToContent(TMinRadius));
                PropertySliderField(_so.FindProperty(nameof(arcAngle)), 0f, 360f, CArcAngle.ToContent(CArcAngle));
                GUI.enabled = true;
            }

            if (hasGeneral)
            {
                GeneralField(_so);
                NonAllocatorField(_so, _so.FindProperty(nameof(colliders)));
                BaseField(_so);
                SolverField(_so);
                IgnoreListField(_so);
            }

            if (hasEvents)
            {
                EventField(_so);
                if (EventFoldout) RCProEditor.EventField(_so, CEventNames);
            }

            if (hasInfo) InformationField(PanelGate);
        }
#endif
    }
}
