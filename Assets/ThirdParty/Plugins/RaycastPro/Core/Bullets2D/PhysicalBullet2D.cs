namespace RaycastPro.Bullets2D
{
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif
    
    [RequireComponent(typeof(Rigidbody2D))]

    [AddComponentMenu("RaycastPro/Bullets/" + nameof(PhysicalBullet2D))]
    public sealed class PhysicalBullet2D : Bullet2D
    {
        [SerializeField] private Rigidbody2D body2D;
        protected override void OnCast()
        {
            if (!body2D) body2D = GetComponent<Rigidbody2D>();

            
            transform.position = raySource.Base;
            transform.right = raySource.TipDirection;

            body2D.angularVelocity = 0;
            body2D.linearVelocity = Vector2.zero;
            
            body2D.AddForce(transform.right * power, forceMode);
        }

        internal override void RuntimeUpdate() => UpdateLifeProcess(GetDelta(timeMode));
        public float power = 1f;

        public ForceMode2D forceMode = ForceMode2D.Force;
        
        // ReSharper disable Unity.PerformanceAnalysis
        public void AddForce(Vector3 direction) => GetComponent<Rigidbody2D>().AddForce(direction * power, forceMode);
#if UNITY_EDITOR
        internal override string Info =>
            "A physics-driven projectile that relies entirely on a Rigidbody component for its movement. It is launched by applying an initial force impulse rather than setting a direct velocity, allowing for realistic interactions with other physics objects. When interacting with 'Planar' modifiers (e.g., mirrors), it preserves its momentum by redirecting its existing linear velocity along the new path, resulting in a physically plausible trajectory change." +
            HAccurate + HDependent;
        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(body2D)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(power)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(forceMode)));
            }

            if (hasGeneral)
            {
                GeneralField(_so);
            }
        }
#endif

    }
}