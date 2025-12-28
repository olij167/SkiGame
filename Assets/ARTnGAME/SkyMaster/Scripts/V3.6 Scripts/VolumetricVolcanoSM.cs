using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Artngame.SKYMASTER;
namespace Artngame.SKYMASTER
{
    public class VolumetricVolcanoSM : MonoBehaviour
    {

        public FullVolumeCloudsSkyMaster volumeClouds;
        public bool gui_on = false;
        public bool growSmoke = false;
        public float growWidthMax = 3.5f;
        public float growHeightMax = 5200;
        public float growWidthMin;
        public float growHeightMin;
        //public float growSpeed = 1;
        // Use this for initialization
        void Start()
        {
            //init volcano
            //volumeClouds

            FullVolumeCloudsSkyMaster[] cloudScripts = transform.GetComponents<FullVolumeCloudsSkyMaster>();
            for (int i = 0; i < cloudScripts.Length; i++)
            {
                if (cloudScripts[i].enabled)
                {
                    volumeClouds = cloudScripts[i];
                }
            }

            growWidthMin = volumeClouds._InteractTextureAtr.w;
            growHeightMin = volumeClouds._Altitude1;
        }

        float lerp, lerp2 = 0f;
        public float growthWidthDuration = 35f;
        public float growthHeightDuration = 25f;

        // Update is called once per frame
        void Update()
        {
            if (growSmoke && volumeClouds)
            {
                lerp += Time.deltaTime / growthWidthDuration;
                lerp2 += Time.deltaTime / growthHeightDuration;

                if (volumeClouds._InteractTextureAtr.w < growWidthMax)
                {
                    //volumeClouds._InteractTextureAtr.w += volumeClouds._InteractTextureAtr.w + Time.deltaTime * growSpeed * 0.001f;
                    volumeClouds._InteractTextureAtr.w = Mathf.Lerp(growWidthMin, growWidthMax, lerp);
                }
                if (volumeClouds._Altitude1 < growHeightMax)
                {
                    //volumeClouds._Altitude1 += volumeClouds._Altitude1 + Time.deltaTime * growSpeed * 0.1f;
                    volumeClouds._Altitude1 = Mathf.Lerp(growHeightMin, growHeightMax, lerp2);
                }
            }
        }

        void onGUI()
        {

            if (gui_on && volumeClouds)
            {

            }

        }
    }
}