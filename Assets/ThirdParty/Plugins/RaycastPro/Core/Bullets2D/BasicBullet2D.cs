namespace RaycastPro.Bullets2D
{
    using UnityEngine;
    using Planers2D;
    using RaySensors2D;

    
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [AddComponentMenu("RaycastPro/Bullets/" + nameof(BasicBullet2D))]
    public sealed class BasicBullet2D : Bullet2D
    {
        protected override void OnCast()
        {
            if (raySource && raySource is RaySensor2D _r)
            {
                transform.position = _r.Base;
                transform.right = _r.Direction;
            }
            else
            {
                transform.position = caster.transform.position;
                transform.right = caster.transform.right;
            }
        }

        internal override void RuntimeUpdate()
        {
            var delta = GetDelta(timeMode);
            var _forward = transform.right; // IN 2D forward is right
            transform.position += _forward * (speed * delta);
            UpdateLifeProcess(delta);
            if (collisionRay) CollisionRun(delta);
        }
#if UNITY_EDITOR
        internal override string Info => "A fundamental bullet type designed for simple projectile mechanics. It propels itself forward at a defined speed and includes basic collision detection via an attached ray sensor. Upon impact, the bullet resets its position and orientation to the collision point and direction, respectively, effectively simulating a raycast-like interaction rather than a physical projectile bounce or destruction." +
                                         HAccurate + HDependent;
        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(speed)), CSpeed.ToContent(CSpeed));
            }
            
            if (hasGeneral) GeneralField(_so);

            if (hasEvents) EventField(_so);

            if (hasInfo) InformationField();
        }
#endif
    }
}