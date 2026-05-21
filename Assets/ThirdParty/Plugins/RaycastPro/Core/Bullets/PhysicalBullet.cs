namespace RaycastPro.Bullets
{
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [RequireComponent(typeof(Rigidbody))]

    [AddComponentMenu("RaycastPro/Bullets/" + nameof(PhysicalBullet))]
    public sealed class PhysicalBullet : Bullet
    {
        public float power = 1f;

        public ForceMode forceMode = ForceMode.Force;

        [SerializeField] private Rigidbody body;

        private float _dt;

        internal override void RuntimeUpdate()
        {
            _dt = GetDelta(timeMode);
            UpdateLifeProcess(_dt);
            if (collisionRay) CollisionRun(_dt);
        }

        protected override void CollisionBehaviour()
        {
            body.position = collisionRay.cloneRaySensor.Base;
            body.linearVelocity = collisionRay.cloneRaySensor.Direction.normalized * body.linearVelocity.magnitude;
        }

        protected override void OnCast()
        {
            if (!body) body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = raySource.Base;
            transform.forward = raySource.TipDirection.normalized;
            body.AddForce(transform.forward * power, forceMode);
        }
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
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(body)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(power)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(forceMode)));
            }

            if (hasGeneral) GeneralField(_so);

            if (hasEvents) EventField(_so);

            if (hasInfo) InformationField();
        }
#endif


    }
}