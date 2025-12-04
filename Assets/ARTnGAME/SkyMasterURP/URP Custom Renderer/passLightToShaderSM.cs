using UnityEngine;

namespace Artngame.SKYMASTER
{
    public class passLightToShader : MonoBehaviour
    {
        public Transform sun;
        public Material materialTopass;
        public string variableName = "_LightDirection";

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (materialTopass != null && sun != null)
            {
                materialTopass.SetVector(variableName, sun.forward);
            }
        }
    }
}