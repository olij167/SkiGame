using UnityEngine.Serialization;

namespace NOT_Lonely
{
    using NOT_Lonely.Weatherade;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    public class SimpleFPController : MonoBehaviour
    {
        [Header("MOUSE LOOK")]
        public Vector2 mouseSensitivity = new Vector2(80, 80);
        public Vector2 verticalLookLimit = new Vector2(-85, 85);
        public float smooth = 0.5f;

        private float xRot;
        public Transform cam;

        [Header("MOVEMENT")]
        public bool physicsController = false;
        public float walkSpeed = 1;
        public float runSpeed = 3;
        private float speed = 1;

#if ENABLE_INPUT_SYSTEM
        public Key forwardKey = Key.W;
        public Key backwardKey = Key.S;
        public Key strafeLeftKey = Key.A;
        public Key strafeRightKey = Key.D;
        public Key runKey = Key.LeftShift;
#else
        public KeyCode forward = KeyCode.W;
        public KeyCode backward = KeyCode.S;
        public KeyCode strafeLeft = KeyCode.A;
        public KeyCode strafeRight = KeyCode.D;
        public KeyCode run = KeyCode.LeftShift;
#endif

        [Header("SIGHT")]
        public bool sight = true;
        public GameObject sightPrefab;
        public bool hideCursor = false;

        private bool forwardMove = false;
        private bool backwardMove = false;
        private bool leftMove = false;
        private bool rightMove = false;

        private CharacterController controller;
        private Animator camAnimator;
        private string lastAnim;
        private string curAnim;
        private float normalizedSpeed;
        
        private float mouseX;
        private float mouseY;
        
        private Vector3 prevPos;
        private float delta;

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        void Start()
        {
            controller = GetComponent<CharacterController>();
            camAnimator = GetComponentInChildren<Camera>().GetComponent<Animator>();

            if (hideCursor)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }


            if (sight)
            {
                GameObject sightObj = Instantiate(sightPrefab);
                sightObj.transform.SetParent(transform.parent);
            }
        }

        void Update()
        {
            CameraLook();

            PlayerMove();
        }

        float refVelX;
        float refVelY;
        float xRotSmooth;
        float yRotSmooth;
        
        void CameraLook()
        {
#if ENABLE_INPUT_SYSTEM
            mouseX = Mouse.current.delta.ReadValue().x * 0.01f * mouseSensitivity.x;
            mouseY = Mouse.current.delta.ReadValue().y * 0.01f * mouseSensitivity.y;
#else
            mouseX = Input.GetAxis("Mouse X") * 0.01f * mouseSensitivity.x * 10;
            mouseY = Input.GetAxis("Mouse Y") * 0.01f * mouseSensitivity.y * 10;
#endif
            xRot -= mouseY;
            xRot = Mathf.Clamp(xRot, verticalLookLimit.x, verticalLookLimit.y);

            xRotSmooth = Mathf.SmoothDamp(xRotSmooth, xRot, ref refVelX, smooth);
            yRotSmooth = Mathf.SmoothDamp(yRotSmooth, mouseX, ref refVelY, smooth);

            cam.transform.localEulerAngles = new Vector3(xRotSmooth, 0, 0);
            transform.Rotate(Vector3.up * yRotSmooth);
        }

        void PlayerMove()
        {
#if ENABLE_INPUT_SYSTEM
            speed = Keyboard.current[runKey].isPressed ? runSpeed : walkSpeed;
            forwardMove = Keyboard.current[forwardKey].isPressed;
            backwardMove = Keyboard.current[backwardKey].isPressed;
            leftMove = Keyboard.current[strafeLeftKey].isPressed;
            rightMove = Keyboard.current[strafeRightKey].isPressed;
#else
            speed = Input.GetKey(run) ? runSpeed : walkSpeed;
            forwardMove = Input.GetKey(forward);
            backwardMove = Input.GetKey(backward);
            leftMove = Input.GetKey(strafeLeft);
            rightMove = Input.GetKey(strafeRight);
#endif
            if (forwardMove || backwardMove || leftMove || rightMove)
                curAnim = Mathf.Approximately(speed, runSpeed) ? "CamShakeRun" : "CamShakeWalk";
            else
                curAnim = "CamShakeIdle";

            if (curAnim != lastAnim)
                camAnimator.CrossFadeInFixedTime(curAnim, 0.3f);

            lastAnim = curAnim;
        }

        private void FixedUpdate()
        {
            if (forwardMove)
                controller.Move(controller.transform.forward * (speed * 0.01f));
            if (backwardMove)
                controller.Move(controller.transform.forward * (-speed * 0.01f));
            if (leftMove)
                controller.Move(controller.transform.right * (-speed * 0.01f));
            if (rightMove)
                controller.Move(controller.transform.right * (speed * 0.01f));

            if (!forwardMove && !backwardMove && !leftMove && !rightMove)
                controller.velocity.Set(0, 0, 0);

            if (controller.isGrounded) return;

            if (Physics.SphereCast(transform.position, controller.radius, -transform.up, out RaycastHit hitInfo, 50, -1, QueryTriggerInteraction.Ignore))
                transform.position = new Vector3(transform.position.x, hitInfo.point.y + controller.height / 2 + controller.skinWidth, transform.position.z);
        }
    }
}
