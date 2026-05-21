namespace RaycastPro.Casters
{
    using RaySensors;
    using UnityEngine;
    using Bullets;

#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif
    
    [AddComponentMenu("RaycastPro/Casters/" + nameof(BasicCaster))]
    public sealed class BasicCaster : GunCaster<Bullet, Collider, RaySensor>
    {
        [SerializeField]
        [Tooltip("Automatically, this ray will shoot along the LocalDirection and source BasePoint location.")]
        public RaySensor raySource;
        
        // ReSharper disable Unity.PerformanceAnalysis
        public override void Cast(int _bulletIndex)
        {
#if UNITY_EDITOR
            alphaCharge = AlphaLifeTime;
#endif
            if (AmmoCheck())
            {
                BulletCast(_bulletIndex, raySource);
            }
        }

#if UNITY_EDITOR
        internal override string Info => "A fundamental implementation of the GunCaster framework, designed for single-barrel weapons. It utilizes a single RaySensor to define the precise origin and direction for projectile instantiation, making it ideal for creating straightforward firearms like pistols or basic rifles that fire from a single, consistent point."
                                         + HAccurate + HDependent;

        private Vector3 _p1, _p2;
        internal override void OnGizmos()
        {
            if (raySource)
            {
                _p1 = raySource.Base;
                _p2 = _p1 + raySource.TipDirection;
                DrawCapLine(_p1, _p2);
            }
        }
        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(raySource)));
                
                GunField(_so);
            }
            if (hasGeneral) GeneralField(_so);

            if (hasEvents) 
            {
                EventFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(EventFoldout, CEvents.ToContent(TEvents),
                    RCProEditor.HeaderFoldout);
                EditorGUILayout.EndFoldoutHeaderGroup();
                if (EventFoldout) RCProEditor.EventField(_so, events);
            }
            if (hasInfo) InformationField();
        }
        private readonly string[] events = new[] {nameof(onCast), nameof(onReload), nameof(onRate)};
#endif
    }
}