namespace NOT_Lonely
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    public class SimpleGun : MonoBehaviour
    {
        public Rigidbody projectileTemplate;
        public float impulsePower = 10;
        public float torquePower = 10;
        public float projectileSizeMul = 0.5f;

        [Range(0, 1)] public float sfxVolume = 0.5f;
        private AudioSource aSource;
        
#if ENABLE_INPUT_SYSTEM
        private InputAction fireAction;
#endif
        void Awake()
        {
            aSource = GetComponent<AudioSource>();

#if ENABLE_INPUT_SYSTEM
            fireAction = new InputAction("Fire", InputActionType.Button);
            fireAction.AddBinding("<Mouse>/leftButton");
#endif
        }
        
#if ENABLE_INPUT_SYSTEM
        void OnEnable()
        {
            fireAction.Enable();
            fireAction.performed += OnFire;
        }

        void OnDisable()
        {
            fireAction.Disable();
            fireAction.performed -= OnFire;
        }
        
        private void OnFire(InputAction.CallbackContext context)
        {
            Fire();
        }
#else
        void Update()
        {
            if (Input.GetMouseButtonDown(0))
                Fire();
        }
#endif

        private void Fire()
        {
            Rigidbody projectile = Instantiate(projectileTemplate, transform.position, transform.rotation);
            projectile.transform.localScale = Vector3.one * projectileSizeMul;
            projectile.AddForce(transform.forward * impulsePower, ForceMode.Impulse);
            Vector3 torque = Random.insideUnitSphere * torquePower;
            projectile.AddTorque(torque);

            if (aSource != null) aSource.PlayOneShot(aSource.clip, sfxVolume);
        }
    }
}
