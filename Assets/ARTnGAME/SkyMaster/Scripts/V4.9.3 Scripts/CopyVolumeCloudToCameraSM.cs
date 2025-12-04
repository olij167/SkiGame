using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Artngame.SKYMASTER
{
    //[ExecuteAlways]
    public class CopyVolumeCloudToCameraSM : MonoBehaviour
    {
        public Transform sun;
        public Camera targetCamera;
        public bool configured = true;
        public FullVolumeCloudsSkyMaster sourceClouds;
        FullVolumeCloudsSkyMaster cloudsScript;
        void Start()
        {
            
        }

        void Update()
        {
            if (!configured && targetCamera != null && sourceClouds != null)
            {
                //ADD CLOUDS
                cloudsScript = targetCamera.gameObject.GetComponent<FullVolumeCloudsSkyMaster>();
                if (cloudsScript != null)
                {
                    cloudsScript.Sun = sun;                   
                    cloudsScript.initVariablesScatter();
                    //cloudsScript.SkyManager = SkyManager;
                    //SkyManager.volumeClouds = cloudsScript;
                }
                else
                {
                    cloudsScript = targetCamera.gameObject.AddComponent<FullVolumeCloudsSkyMaster>();
                    cloudsScript.Sun = sun;                   
                    cloudsScript.initVariablesA();
                    cloudsScript.initVariablesScatter();
                    //cloudsScript.SkyManager = SkyManager;
                    //SkyManager.volumeClouds = cloudsScript;
                }

                PassInitVariablesScatter();
                PassInitVariablesA();

                configured = true;
            }
        }

        public void PassInitVariablesScatter()
        {
            cloudsScript.heightDensity = sourceClouds.heightDensity;
            cloudsScript.height = sourceClouds.height;
            cloudsScript.startDistance = sourceClouds.startDistance;
            cloudsScript.windMultiply = sourceClouds.windMultiply;

            cloudsScript.useTOD = false;
            cloudsScript.unity2018 = true;
            cloudsScript.useWeather = false;
            cloudsScript.adjustNightLigting = false;
           
            cloudsScript.updateShadows = sourceClouds.updateShadows;
            //cloudsScript.shadowsUpdate();
            cloudsScript.updateReflectionCamera = false;
            cloudsScript._HorizonYAdjust = sourceClouds._HorizonYAdjust;
            cloudsScript._NoiseFreq1 = sourceClouds._NoiseFreq1;
            cloudsScript._NoiseFreq2 = sourceClouds._NoiseFreq2;
            cloudsScript._NoiseAmp1 = sourceClouds._NoiseAmp1;
            cloudsScript._NoiseAmp2 = sourceClouds._NoiseAmp2;
            if (!cloudsScript.useWeather)
            {
                cloudsScript._NoiseBias = sourceClouds._NoiseBias;
            }
            cloudsScript._Altitude0 = sourceClouds._Altitude0;
            cloudsScript._Altitude1 = sourceClouds._Altitude1;
            cloudsScript._Scatter = sourceClouds._Scatter;
            cloudsScript._Extinct = sourceClouds._Extinct;
            cloudsScript._ExposureUnder = sourceClouds._ExposureUnder;
            cloudsScript._HGCoeff = sourceClouds._HGCoeff;
            cloudsScript._GroundColor = sourceClouds._GroundColor;
            cloudsScript._SunSize = sourceClouds._SunSize;
            cloudsScript._BackShade = sourceClouds._BackShade;
            cloudsScript._UndersideCurveFactor = sourceClouds._UndersideCurveFactor;

            cloudsScript.luminance = sourceClouds.luminance;
            cloudsScript.lumFac = sourceClouds.lumFac;
            cloudsScript.ScatterFac = sourceClouds.ScatterFac;
            cloudsScript.TurbFac = sourceClouds.TurbFac;
            cloudsScript.HorizFac = sourceClouds.HorizFac;
            cloudsScript.turbidity = sourceClouds.turbidity;
            cloudsScript.reileigh = sourceClouds.reileigh;
            cloudsScript.mieCoefficient = sourceClouds.mieCoefficient;
            cloudsScript.mieDirectionalG = sourceClouds.mieDirectionalG;
            cloudsScript.bias = sourceClouds.bias;
            cloudsScript.contrast = sourceClouds.contrast;
            cloudsScript.TintColor = sourceClouds.TintColor;
        }

        //v4.8
        public void PassInitVariablesA()
        {  
            cloudsScript._SampleCount0 = sourceClouds._SampleCount0;
            cloudsScript._SampleCount1 = sourceClouds._SampleCount1 ;
            cloudsScript._SampleCountL = sourceClouds._SampleCountL;

            cloudsScript.splitPerFrames = sourceClouds.splitPerFrames;
            cloudsScript.downScale = sourceClouds.downScale;
            cloudsScript.downScaleFactor = sourceClouds.downScaleFactor;

            cloudsScript._FarDist = sourceClouds._FarDist;
            cloudsScript.distanceFog = sourceClouds.distanceFog;

            cloudsScript._Scroll1 = sourceClouds._Scroll1;
            cloudsScript._Scroll2 = sourceClouds._Scroll2;

            cloudsScript._InteractTexturePos = sourceClouds._InteractTexturePos;
            cloudsScript._InteractTextureAtr = sourceClouds._InteractTextureAtr;
            cloudsScript._InteractTextureOffset = sourceClouds._InteractTextureOffset;          
        }
    }
}