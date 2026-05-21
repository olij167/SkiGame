namespace RaycastPro.RaySensors2D
{
    using System;
    using System.Linq;
    using UnityEngine;
#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif

    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(ChainRay2D))]
    public sealed class ChainRay2D : PathRay2D, IRadius
    {
        public ChainReference chainReference = ChainReference.Point;
        
        [SerializeField] private float radius = .1f;
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0,value);
        }
        public Vector2[] chainPoints = {Vector2.right, Vector2.up};
        public Transform[] targets = Array.Empty<Transform>();

        public bool relative;
        
        private Vector2 sum;
        private int i, j;
        internal void ToRelative()
        {
            PathPoints.Clear();
            PathPoints.Add(transform.position);
            for (i = 0; i < chainPoints.Length; i++)
            {
                sum = Vector2.zero;
                for (j = 0; j <= i; j++) sum += chainPoints[j];
                if (local) sum = transform.TransformPoint(sum);
                PathPoints.Add(sum.ToDepth());
            }
        }
        protected override void OnCast()
        {
            UpdatePath();
            if (pathCast)
            {
                DetectIndex = AdvancePathCast(out hit, radius);
                isDetect = hit && FilterCheck(hit);
            }
        }

        protected override void UpdatePath()
        {
            PathPoints.Clear();
            PathPoints.Add(Position2D);
            switch (chainReference)
            {
                case ChainReference.Point:
                    if (relative)
                    {
                        ToRelative();
                    }
                    else
                    {
                        foreach (var _cP in chainPoints)
                        {
                            PathPoints.Add(transform.TransformPoint(_cP));
                        }
                    }
                    break;
                
                case ChainReference.Transform:
                    
                    PathPoints.Clear();
                    foreach (var t in targets)
                    {
                        if (t) PathPoints.Add(t.position.To2D());
                        
                    }
                    break;
            }
        }

#if UNITY_EDITOR
        internal override string Info => "A highly versatile, procedural path sensor that constructs a multi-point trajectory from a configurable source and then performs a cast along the resulting geometry. It operates in two main modes based on its data source: In 'Transform' mode, it simply connects an array of specified Transforms, creating a direct 'connect-the-dots' path. In 'Point' mode, it uses an array of Vector2 offsets. This can be interpreted either as absolute local offsets from the sensor's origin, or as a cumulative 'chain' where each vector is added to the previous point, creating a sequence of connected segments. The entire generated path can also be oriented relative to the sensor's local transform, making it ideal for creating complex, custom-shaped detection patterns that can move and rotate dynamically."
                                         + HAccurate + HPathRay + HIRadius + HScalable;
        internal override void OnGizmos()
        {
            EditorUpdate();

            AdvancePathDraw(radius, true);
            DrawNormal2D(hit, z);
            DrawNormalFilter();
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                PropertyEnumField(_so.FindProperty(nameof(chainReference)), 2, CReferenceType.ToContent(TReferenceType), new GUIContent[]
                    {"Transform".ToContent("Can adjust game object's position as chain reference."), "Point".ToContent("Adjust Points as regular vector2 positions with a relative mode option.")}
                );
                BeginVerticalBox();
                if (chainReference == ChainReference.Point)
                {
                    RCProEditor.DrawSerializedList(_so.FindProperty(nameof(chainPoints)));

                    EditorGUILayout.PropertyField(_so.FindProperty(nameof(relative)),
                        CRelative.ToContent(TRelative), relative);
                }
                else RCProEditor.DrawSerializedList(_so.FindProperty(nameof(targets)));
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