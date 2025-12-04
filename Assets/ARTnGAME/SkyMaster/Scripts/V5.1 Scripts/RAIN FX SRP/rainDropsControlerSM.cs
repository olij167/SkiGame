using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Artngame.SKYMASTER
{
    public class rainDropsControlerSM : MonoBehaviour
    {
        //Shader variables
        // _EraseCenterRadius("_EraseCenterRadius", Vector) = (0, 0, 1, 1)
        //erasePower("ErasePower", float) = 1
        ////MASKED
        //maskPower("maskPower", float) = 1
        //mainTexTilingOffset("main Texture Tiling and Offset", Vector) = (1, 1, 0,0)
        ////v0.7
        //interactPointRadius("Interact Point and Radius", Vector) = (0, 0, 0, 1)
        //radialControls("radial Controls", Vector) = (1, 1, 1, 1)
        //directionControls("direction Controls", Vector) = (0, 0, 1, 1)
        //wipeControls("Wipe Center, Radius and falloff", Vector) = (0, 0, 1, 1)

        public Transform airplane;
        public Material rainDropsMat;

        public Vector3 prevPos;
        public Vector3 prevForward;
        public Vector3 prevEuler;

        public float motionPushPower = 0.1f;
        public float diffPushPower = 1.0f;
        public float angleDifferenceOffset = 0.1f;
        public float returnSpeed = 1;

        // Update is called once per frame
        void Update()
        {
            if (rainDropsMat != null && airplane != null)
            {
                float dotMe = Vector3.Dot(prevForward, airplane.forward);
                float diff = prevEuler.y - airplane.eulerAngles.y;
                if (Mathf.Abs(diff) > angleDifferenceOffset)//   dotMe > 0.5f)
                {
                    //-0.25 to 0.55
                    float setPoint = -0.25f * motionPushPower + (-0.25f * Mathf.Abs(diff)) * 0.1f * diffPushPower;
                    if (diff > 0)
                    {
                        setPoint = 0.55f * motionPushPower + (0.55f * Mathf.Abs(diff)) * 0.1f * diffPushPower;
                    }
                    //rainDropsMat.SetVector("directionControls", new Vector4(dotMe * motionPushPower, 0,0,0));
                    //rainDropsMat.SetVector("directionControls", Vector4.Lerp(rainDropsMat.GetVector("directionControls"),
                    //    new Vector4(diff * motionPushPower, 0, 0, 0), Time.deltaTime*111f));

                    rainDropsMat.SetVector("directionControls", Vector4.Lerp(rainDropsMat.GetVector("directionControls"),
                           new Vector4(setPoint, 0, 0, 0), Time.deltaTime * 50f * Mathf.Abs(diff)));
                }
                else
                {
                    float retSpeed = returnSpeed;
                    if (diff == 0)
                    {
                        //retSpeed = 4;
                    }
                    rainDropsMat.SetVector("directionControls", Vector4.Lerp(rainDropsMat.GetVector("directionControls"),
                        new Vector4(0, 0, 0, 0), Time.deltaTime * 0.1f * retSpeed));

                    //rainDropsMat.SetVector("directionControls", new Vector4(0, 0, 0, 0));

                }

                prevPos = airplane.position;
                prevForward = airplane.forward;
                prevEuler = airplane.eulerAngles;
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            prevPos = airplane.position;
            prevForward = airplane.forward;
            prevEuler = airplane.eulerAngles;
            rainDropsMat.SetVector("directionControls", new Vector4(0, 0, 0, 0));
        }
    }
}