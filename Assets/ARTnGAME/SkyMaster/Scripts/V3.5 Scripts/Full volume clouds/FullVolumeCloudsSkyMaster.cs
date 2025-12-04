using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

namespace Artngame.SKYMASTER
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    //[AddComponentMenu ("Image Effects/Rendering/Global Fog Sky Master")] //v4.6
    public class FullVolumeCloudsSkyMaster : PostEffectsBaseSkyMaster
    {
        //v5.0.3
        public bool reflCamBlendBackground = false;
        public float adjustReflPos = 8;
        public float adjustReflPos2 = 0;
        public float adjustReflPos3 = 0;

        //v5.0.1
        public bool adjustVR = false;
        public float shiftLeftEye = 0;

        //v4.9.4
        public float cameraFarDistanceAdjust = 30000; //same as camera far view distance, or tweaked for scaling of the effect

        //v4.8.6
        public bool adjustNightLigting = true;
        public float backShadeNight = 0.5f; //use this at night for more dense clouds
        public float turbidityNight = 2f;
        public float extinctionNight = 0.01f;
        public float shift_dawn = 0; //add shift to when cloud lighting changes vs the TOD of sky master
        public float shift_dusk = 0;

        //v4.8.4
        //public bool adjustNightLigting = true;
        public Vector3 groundColorNight = new Vector3(0.5f, 0.5f, 0.5f);
        public float scatterNight = 0.12f; //use this at night
        public float reflectFogHeight = 1;

        //v4.8
        //public bool enableReproject = false;
        public bool isForReflections = false;
        public bool windByWindZone = false;
        public bool windByWeather = false;
        public float windSunny = 0.01f;
        public float windCloudy = 0.06f;
        public float windStorm = 0.85f;
        public float downscaleReflectNoiseSteps = 1;
        public bool adjustNightTimeSun = true; //replace sun with moon in Sun slot
        public float varianceAltitude1 = 0;

        //v4.6
        public bool useWeather = false; //adjust clouds by weather in sky master - optional
        public SkyMasterManager.Volume_Weather_types prevWeather;
        bool weatherTransition = true;
        float weatherTransTimeStart = 0;
        public float weatherTransitionDuration = 45;
        public float sunnyDensity = 0;
        public float cloudyDensity = 2.5f;
        public float stormyDensity = 4.5f;
        public float weatherTransitionSpeed = 1.2f;
        public bool heavyStorm = false;
        float localLightFalloffP;
        float localLightIntensityP;
        float sinSizeP;
        float backShadeP;
        float underSideP;

        public bool unity2018 = true; //v4.1

        [Tooltip("Apply distance-based fog?")]
        public bool distanceFog = true;
        [Tooltip("Distance fog is based on radial distance from camera when checked")]
        public bool useRadialDistance = false;
        [Tooltip("Apply height-based fog?")]
        public bool heightFog = true;
        [Tooltip("Fog top Y coordinate")]
        public float height = 1.0f;
        [Range(0.00001f, 10.0f)]
        public float heightDensity = 0.00001f; //2.0f; //v4.8
        [Tooltip("Push fog away from the camera by this amount")]
        public float startDistance = 0.0f;

        //v4.1f
        public float _mobileFactor = 1;
        public float _alphaFactor = 0.96f;

        //v2.1.24
        public float _HorizonZAdjust = 1;

        //v2.1.24
        public bool updateReflectionCamera = false; //add reflection of clouds to reflect camera of water module or update it if already exists
        public bool updateReflectionCameraLocalLights = false;
        public bool updateShadows = true;//update shadows material parameters based on cloud params
        public Material cloudsShadowMat;//material for cloud shadows
        public bool adaptLocalToLightning = false;//adapt local light from lightning module
                                                  //public 

        //v2.1.21
        public bool useFluidTexture = false;
        public Material fluidFlow;

        //v2.1.20
        public bool WebGL = false;
        public bool blendBackground = false;
        public Camera backgroundCam;
        public Material backgroundMat; //blend layer between sky and clouds - https://answers.unity.com/questions/330892/why-is-the-second-camera-interfering-with-image-ef.html

        //v2.1.19
        public bool fastest = false;
        public Light localLight;
        public float localLightFalloff = 2;
        public float localLightIntensity = 1;
        public float currentLocalLightIntensity = 0; //public only for debug purposes

        //v3.5.3
        public Texture2D _InteractTexture;
        public Vector4 _InteractTexturePos = new Vector4(1, 1, 0, 0);
        public Vector4 _InteractTextureAtr = new Vector4(1, 1, 0, 0);//multipliers and offsets
        public Vector4 _InteractTextureOffset = new Vector4(0, 0, 0, 0); //v4.0

        //v3.5.1
        public float _NearZCutoff = -2;
        public float _HorizonYAdjust = 0;
        public float _FadeThreshold = 0;

        //v3.5
        public Texture3D texture3Dnoise1;
        public Texture3D texture3Dnoise2;

        public float _SampleCount0 = 42;

        public float _SampleCount1 = 3;
        public int _SampleCountL = 4;

        public float _NoiseFreq1 = 3.1f;
        public float _NoiseFreq2 = 35.1f;
        public float _NoiseAmp1 = 5;
        public float _NoiseAmp2 = 1;
        public float _NoiseBias = -0.2f;

        public float windMultiply = 1;//v4.8 use with weather or only when set above one
        public Vector3 _Scroll1 = new Vector3(0.01f, 0.08f, 0.06f);
        public Vector3 _Scroll2 = new Vector3(0.01f, 0.05f, 0.03f);

        public float _Altitude0 = 1500;
        public float _Altitude1 = 3500;
        public float _FarDist = 30000;
        public float _FarDistNight = 300000000;
        public float _FarDistDay = 400000;//save day distance here to restore it

        public float _Scatter = 0.008f;
        public float _HGCoeff = 0.5f;
        public float _Extinct = 0.01f;

        public float _Exposure = 0.00001f;
        public float _ExposureUnder = 3f; //v4.0
        public Vector3 _GroundColor = new Vector3(0.8f, 0.8f, 0.8f);//new Vector3(0.369f, 0.349f, 0.341f);//
        public float _SunSize = 0.04f;
        public Vector3 _SkyTint = new Vector3(0.5f, 0.5f, 0.5f);
        public float _AtmosphereThickness = 1.0f;

        public float _BackShade = 1.0f;
        public float _UndersideCurveFactor = 0.0f;




        //SM v1.7
        public Gradient DistGradient = new Gradient();
        public Vector2 GradientBounds = Vector2.zero;
        public float luminance = 0.05f;//0.8f;
        public float lumFac = 0.2f;//0.9f; 
        public float ScatterFac = 500;//3500;//24.4f;//scatter factor //v4.8.4
        public float TurbFac = 35;//324.74f;//turbidity scale
        public float HorizFac = 62.4f;//1;//sun horizon multiplier
        public float turbidity = 30;//10f;//v4.8.4
        public float reileigh = 170;//0.8f;
        public float mieCoefficient = 9.5f;//0.1f;
        public float mieDirectionalG = 0.9f;//0.7f; 		
        public float bias = 0.3f;//0.6f;
        public float contrast = 6.5f;//4.12f;
        public Transform Sun;
        public bool FogSky = false;
        public Vector3 TintColor = new Vector3(68, 155, 345); //68, 155, 345
        public float ClearSkyFac = 1;

        public Shader fogShader = null;
        private Material fogMaterial = null;
        public SkyMasterManager SkyManager;
        Texture2D colourPalette;
        bool Made_texture = false;

        //v2.1.24 - setup shadows-reflections-depth camera
        //BACK LAYER SETUP
        public bool useTOD = false;
        public bool tintUnder = false;
        public Vector3 colorMultiplier = Vector3.one;
        public Vector3 UnderColorMultiplier = Vector3.one;
        public bool adjustNightTimeDensity = true;//false;//v4.2 //v4.8.5
        public bool followPlayer = false;

        public Transform Galaxy;
        public Transform ShadowLayer;
        public GameObject shadowDome;

        public GameObject backCamPrefab;
        public GameObject shadowDomePrefab;
        public GameObject LightningBoxPrefab;

        public bool setupDepth = false;
        public bool setupShadows = false;
        public bool setupLightning = false;

        //v4.6
        void Start1()//new void Start() //v4.8
        {
            localLightFalloffP = localLightFalloff;//2
            localLightIntensityP = localLightIntensity;//1
            sinSizeP = _SunSize;//12
            backShadeP = _BackShade;//0.3
            underSideP = _UndersideCurveFactor;//0.2
        }

        //void Start()
        //{
            //Start1();
           // Update();
        //}

        //v4.8
        public void initVariablesScatter()
        {
            heightDensity = 0.003f;
            height = 40;// 60; //v4.8.4
            startDistance = 0;
            windMultiply = 0.05f;

            useTOD = true;
            unity2018 = true;
            useWeather = true;
            //_ExposureUnder = 1;
            //_GroundColor = new Vector3(1, 1, 1) * 1.8f;
            //_SunSize = 5;// 35;
            //_BackShade = 0.18f;
            updateShadows = true;
            shadowsUpdate();
            updateReflectionCamera = true;
            _HorizonYAdjust = -700;
            _NoiseFreq1 = 3.9f;
            _NoiseFreq2 = 29f;
            _NoiseAmp1 = 4.82f;
            _NoiseAmp2 = 4.04f;
            if (!useWeather)
            {
                _NoiseBias = -3.63f;
            }
            _Altitude0 = 2300;
            _Altitude1 = 4500;
            _Scatter = 0.015f;//0.025f;// 0.015f; // 0.029f;    //v4.8.4
            _Extinct = 0.0095f; //0.004f;                       //v4.8.4
            _ExposureUnder = 1;
            _HGCoeff = 0.19f;
            _GroundColor = Vector3.one * 1.8f; // Vector3.one //new Vector3(1.82f, 1.8f, 1.8f);
            _SunSize = 2;// 1;// 5;
            _BackShade = 0.12f;// 0.18f;
            _UndersideCurveFactor = 0.5f;// 0.79f;

            luminance = 0.9f;
            lumFac = 0.9f;// 1.2f;// 0.35f;// 0.65f;
            ScatterFac = 800;// 500;// 3330;               //v4.8.4
            TurbFac = 35;
            HorizFac = 62.4f;
            turbidity = 80;// 40;// 130;// 27;            //v4.8.4
            reileigh = 171;
            mieCoefficient = 2;// 19;
            mieDirectionalG = 0.74f;
            bias = 3.6f;// 3.1f;// 1.28f;
            contrast = 7.59f;// 6.74f;
            TintColor = new Vector3(74, 78, 345);
        }

        //v4.8
        public void initVariablesA()
        {
            _Altitude0 = 2300;
            _Altitude1 = 4200;

            _SampleCount0 = 1;
            _SampleCount1 = 140;
            _SampleCountL = 9;

            _NoiseFreq1 = 5.1f;
            _NoiseFreq2 = 49;

            _NoiseAmp1 = 5.32f;
            _NoiseAmp2 = 2.34f;
            if (!useWeather)
            {
                _NoiseBias = -3.8f;
            }

            splitPerFrames = 1;
            downScale = true;
            downScaleFactor = 2;
            _Scatter = 0.02f;

            _Extinct = 0.01f;
            _HGCoeff = -0.05f;

            _BackShade = 0.4f;
            _UndersideCurveFactor = 0.2f;
            _FarDist = 442000;

            distanceFog = false;
            _SunSize = 20;

            _Scroll1 = new Vector3(-0.25f, -0.14f, -0.25f);
            _Scroll2 = new Vector3(0.01f, 0.05f, 0.03f);

            _InteractTexturePos = new Vector4(0.06f, 0.05f, -1222f, -1222f);
            _InteractTextureAtr = new Vector4(0.34f, 0.991f, 0f, 1);
            _InteractTextureOffset = new Vector4(1100, 1100, 0, 0);

            _HorizonYAdjust = 2000;
        }

        public void createDepthSetup()
        {

            if (setupDepth)
            {

                //check layer exists and if not return and message to add
                var layerToCheck = LayerMask.NameToLayer("Background");
                if (layerToCheck > -1)
                {

                    //change layer for new background camera
                    SkyManager.SunObj.layer = layerToCheck;
                    SkyManager.MoonObj.layer = layerToCheck;
                    SkyManager.CloudDomeL1.layer = layerToCheck;
                    SkyManager.CloudDomeL2.layer = layerToCheck;
                    SkyManager.StarDome.layer = layerToCheck;
                    SkyManager.Star_particles_OBJ.layer = layerToCheck;

                    Transform[] transforms = transform.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        if (transforms[i].name.Contains("STAR_GALAXY_DOME"))
                        {
                            transforms[i].gameObject.layer = layerToCheck;
                            Galaxy = transforms[i];
                            break;
                        }
                    }

                    //if not find in camera, search otherwise
                    if (Galaxy == null)
                    {
                        transforms = SkyManager.transform.GetComponentsInChildren<Transform>(true);
                        for (int i = 0; i < transforms.Length; i++)
                        {
                            if (transforms[i].name.Contains("STAR_GALAXY_DOME"))
                            {
                                transforms[i].gameObject.layer = layerToCheck;
                                Galaxy = transforms[i];
                                break;
                            }
                        }
                    }

                    //do stars
                    ParticleSystem[] transformsStars = SkyManager.Star_particles_OBJ.transform.GetComponentsInChildren<ParticleSystem>(true);
                    for (int i = 0; i < transformsStars.Length; i++)
                    {
                        //if (transforms[i].name.Contains("STAR_GALAXY_DOME")){
                        transformsStars[i].gameObject.layer = layerToCheck;
                        //}
                    }

                    //Back Camera
                    if (backgroundCam == null)
                    {
                        GameObject backCam = Instantiate(backCamPrefab);
                        backgroundCam = backCam.GetComponent<Camera>();
                    }

                    //Material

                    //Change cameras layers
                    //make 9 visible  - var newMask = oldMask | (1 << 9);
                    //hide 9 - var newMask = oldMask & ~(1 << 9);

                    // Turn on the bit using an OR operation:
                    //private void Show() {
                    // camera.cullingMask |= 1 << LayerMask.NameToLayer("SomeLayer");
                    //}

                    // Turn off the bit using an AND operation with the complement of the shifted int:
                    //private void Hide() {
                    // camera.cullingMask &=  ~(1 << LayerMask.NameToLayer("SomeLayer"));
                    //}

                    // Toggle the bit using a XOR operation:
                    //private void Toggle() {
                    // camera.cullingMask ^= 1 << LayerMask.NameToLayer("SomeLayer");
                    //}

                    backgroundCam.cullingMask = 1 << layerToCheck;
                    Camera.main.cullingMask &= ~(1 << layerToCheck);

                    //Change view distance to match main camera
                    backgroundCam.farClipPlane = Camera.main.farClipPlane;

                    //Add same to reflection script

                    //v4.1 - untag camera from MainCamera
                    backgroundCam.tag = "Untagged";

                    //Follow player option
                    if (followPlayer)
                    {
                        Vector3 CamPos = Camera.main.transform.position;
                        SkyManager.SunObj.layer = layerToCheck;
                        SkyManager.MoonObj.layer = layerToCheck;
                        SkyManager.CloudDomeL1.layer = layerToCheck;
                        SkyManager.CloudDomeL2.layer = layerToCheck;
                        SkyManager.StarDome.layer = layerToCheck;
                        SkyManager.Star_particles_OBJ.layer = layerToCheck;
                        Galaxy.position = new Vector3(Galaxy.position.x, Galaxy.position.z);
                        shadowDome.transform.position = new Vector3(CamPos.x, shadowDome.transform.position.y, CamPos.z);
                    }
                }
                else
                {
                    Debug.Log("Please add the Background layer to proceed with the setup");
                }
                setupDepth = false;
            }
        }

        public void createShadowDome()
        {
            if (setupShadows)
            {
                if (shadowDome == null)
                {
                    GameObject shadowDome1 = Instantiate(shadowDomePrefab);
                    shadowDome = shadowDome1;   //v4.1e
                }
                setupShadows = false;
            }
        }

        public void createLightningBox()
        {
            if (setupLightning)
            {
                if (LightningBox == null)
                {
                    GameObject lightningDome = Instantiate(LightningBoxPrefab);
                    LightningBox = lightningDome.transform;
                }
                setupLightning = false;
            }
        }


        //v2.1.24
        public void shadowsUpdate()
        {
            if (updateShadows && !isForReflections)
            {

                if (cloudsShadowMat)
                {
                    cloudsShadowMat.SetVector("_InteractTextureAtr", _InteractTextureAtr);
                    cloudsShadowMat.SetVector("_InteractTextureOffset", _InteractTextureOffset);
                    cloudsShadowMat.SetVector("_InteractTexturePos", _InteractTexturePos);
                    cloudsShadowMat.SetTexture("_InteractTexture", _InteractTexture);
                    cloudsShadowMat.SetFloat("_Altitude0", _Altitude0);
                    cloudsShadowMat.SetFloat("_Altitude1", _Altitude1);

                    cloudsShadowMat.SetFloat("_NoiseBias", _NoiseBias);
                    cloudsShadowMat.SetFloat("_UndersideCurveFactor", _UndersideCurveFactor);
                    cloudsShadowMat.SetFloat("_BackShade", _BackShade);

                    //v4.8.6
                    if (Application.isPlaying)
                    {
                        cloudsShadowMat.SetVector("_Scroll1", _Scroll1);
                        cloudsShadowMat.SetVector("_Scroll2", _Scroll2);
                    }
                    else
                    {
                        cloudsShadowMat.SetVector("_Scroll1", Vector4.zero);
                        cloudsShadowMat.SetVector("_Scroll2", Vector4.zero);
                    }

                    cloudsShadowMat.SetFloat("_NoiseAmp1", _NoiseAmp1);
                    cloudsShadowMat.SetFloat("_NoiseAmp2", _NoiseAmp2);
                    cloudsShadowMat.SetFloat("_NoiseFreq1", _NoiseFreq1);
                    cloudsShadowMat.SetFloat("_NoiseFreq2", _NoiseFreq2);
                }

                updateShadows = false;
            }
        }

        //v2.1.24 - reflections
        //bool reflectInitDone = false;//v4.8
        public FullVolumeCloudsSkyMaster reflectClouds;
        public void updateReflections()
        {

            if (Application.isPlaying)
            {
                //update local lights
                if (reflectClouds != null && updateReflectionCameraLocalLights)
                {
                    reflectClouds.localLight = localLight;
                }
            }

            if (updateReflectionCamera)
            {

                if (SkyManager)
                {
                    if (SkyManager.water)
                    {
                        PlanarReflectionSM waterScript = SkyManager.water.GetComponentInChildren<PlanarReflectionSM>();
                        if (waterScript.m_ReflectionCameraOut != null && !isForReflections)
                        {
                            reflectClouds = waterScript.m_ReflectionCameraOut.GetComponent<FullVolumeCloudsSkyMaster>();
                            if (reflectClouds == null)
                            {
                                reflectClouds = waterScript.m_ReflectionCameraOut.gameObject.AddComponent<FullVolumeCloudsSkyMaster>();
                            }
                            //update params

                            //reflectClouds.startDistance = 10000000000; //v4.8
                            //v4.8
                            reflectClouds.isForReflections = true;
                            //remove back layer from refections
                            int layerToCheck = LayerMask.NameToLayer("Background");
                            //backgroundCam.cullingMask = 1 << layerToCheck;
                            SkyManager.water.GetComponent<PlanarReflectionSM>().m_ReflectionCameraOut.cullingMask &= ~(1 << layerToCheck);
                            SkyManager.water.GetComponent<PlanarReflectionSM>().reflectionMask &= ~(1 << layerToCheck);
                            reflectClouds.distanceFog = false;
                            reflectClouds.windMultiply = windMultiply;

                            //vary height based on camera orizon distance
                            Vector3 camView = Camera.main.transform.forward;
                            float camHor = Vector3.SignedAngle(camView, new Vector3(camView.x, 0, camView.z), Camera.main.transform.right);
                            //Debug.Log("hor="+camHor);
                            float adjuster = 22;
                            if (camHor > 0f)
                            {
                                adjuster = adjuster - camHor;//2f;
                                camHor = 0.3f * camHor;
                            }
                            reflectClouds.height = 1f * (height + camHor * adjuster) * reflectFogHeight; //v4.8.4
                            //reflectClouds.height = height;


                            reflectClouds.heightDensity = heightDensity;
                            reflectClouds.startDistance = startDistance;

                            reflectClouds.downScaleFactor = reflectDownScale;//2; //v5.0.3
                            reflectClouds.adjustReflPos = adjustReflPos;//v5.0.3
                            reflectClouds.adjustReflPos2 = adjustReflPos2;//v5.0.3
                            reflectClouds.adjustReflPos3 = adjustReflPos3;//v5.0.3

                            reflectClouds.downScale = true;
                            reflectClouds.splitPerFrames = 0;// splitPerFrames ;// 0; //v5.0.3
                            reflectClouds._needsReset = true;
                            reflectClouds._SampleCount0 = _SampleCount0;
                            reflectClouds._SampleCount1 = _SampleCount1 / downscaleReflectNoiseSteps; //v4.8.1
                            reflectClouds._SampleCountL = (int)(_SampleCountL / downscaleReflectNoiseSteps); //v4.9.6

                            reflectClouds.localLightFalloff = localLightFalloff;
                            reflectClouds.localLightIntensity = localLightIntensity;
                            reflectClouds.updateReflectionCameraLocalLights = updateReflectionCameraLocalLights;

                            //materials
                            reflectClouds.cloudsShadowMat = cloudsShadowMat;
                            reflectClouds.backgroundCam = backgroundCam; //v4.8
                            reflectClouds.backgroundMat = backgroundMat; //v4.8
                            reflectClouds.blendBackground = reflCamBlendBackground;// blendBackground; //v5.0.3
                            reflectClouds.WebGL = WebGL;
                            //reflectClouds._InteractTexture = _InteractTexture;
                            reflectClouds.texture3Dnoise1 = texture3Dnoise1;
                            reflectClouds.texture3Dnoise2 = texture3Dnoise2;
                            reflectClouds.Sun = Sun;//v4.8
                            reflectClouds.fogShader = fogShader;
                            reflectClouds.SkyManager = SkyManager;
                            reflectClouds.LightningPrefab = LightningPrefab;
                            reflectClouds.LightningBox = LightningBox;
                            reflectClouds.cameraMotionCompensate = cameraMotionCompensate;
                            reflectClouds.lightning_every = lightning_every;
                            reflectClouds.max_lightning_time = max_lightning_time;
                            reflectClouds.lightning_rate_offset = lightning_rate_offset;

                            reflectClouds._FarDist = _FarDist / (reduceReflectfarDist * 1.4f);//v4.9.6 //v5.0.3
                            reflectClouds._FarDistDay = _FarDistDay / (reduceReflectfarDist * 1.4f);
                            reflectClouds._FarDistDay = _FarDistDay / (reduceReflectfarDist * 1.4f);

                            //reflectClouds.ScatterFac = ScatterFac;
                            reflectClouds._Scatter = _Scatter;
                            reflectClouds._HGCoeff = _HGCoeff;
                            reflectClouds._Extinct = _Extinct;
                            reflectClouds._SunSize = _SunSize;
                            reflectClouds._SkyTint = _SkyTint;
                            reflectClouds._ExposureUnder = _ExposureUnder;
                            reflectClouds._HorizonYAdjust = _HorizonYAdjust;
                            reflectClouds._HorizonZAdjust = _HorizonZAdjust; //v2.1.24
                            reflectClouds._GroundColor = _GroundColor;

                            //v4.1f                            
                            reflectClouds._mobileFactor = _mobileFactor;
                            reflectClouds._alphaFactor = _alphaFactor;

                            reflectClouds.luminance = luminance;
                            reflectClouds.lumFac = lumFac;
                            reflectClouds.ScatterFac = ScatterFac;
                            reflectClouds.TurbFac = TurbFac;
                            reflectClouds.turbidity = turbidity;
                            reflectClouds.HorizFac = HorizFac;
                            reflectClouds.reileigh = reileigh;
                            reflectClouds.mieCoefficient = mieCoefficient;
                            reflectClouds.mieDirectionalG = mieDirectionalG;
                            reflectClouds.bias = bias;
                            reflectClouds.contrast = contrast;
                            reflectClouds.TintColor = TintColor;

                            //same as shadows
                            reflectClouds._InteractTextureAtr = _InteractTextureAtr;
                            reflectClouds._InteractTextureOffset = _InteractTextureOffset;
                            reflectClouds._InteractTexturePos = _InteractTexturePos;
                            reflectClouds._InteractTexture = _InteractTexture;
                            reflectClouds._Altitude0 = _Altitude0;
                            reflectClouds._Altitude1 = _Altitude1;

                            reflectClouds._NoiseBias = _NoiseBias;
                            reflectClouds._UndersideCurveFactor = _UndersideCurveFactor;
                            reflectClouds._BackShade = _BackShade;

                            reflectClouds._Scroll1 = _Scroll1;
                            reflectClouds._Scroll2 = _Scroll2;

                            reflectClouds._NoiseAmp1 = _NoiseAmp1;
                            reflectClouds._NoiseAmp2 = _NoiseAmp2;
                            reflectClouds._NoiseFreq1 = _NoiseFreq1;
                            reflectClouds._NoiseFreq2 = _NoiseFreq2;

                            //v4.8
                            reflectClouds.useTOD = useTOD;
                            reflectClouds.useWeather = useWeather;

                        }
                    }
                }

                updateReflectionCamera = false;
            }
        }

        //v2.1.24 - lightning
        public GameObject LightningPrefab; //Prefab to instantiate for lighting, use only 1-2 prefabs and move them around
        public bool EnableLightning = false;
        public bool useLocalLightLightn = false;
        float last_lightning_time = 0;
        public float lightning_every = 15;
        public float max_lightning_time = 2;
        public float lightning_rate_offset = 5;
        Transform LightningOne;
        Transform LightningTwo;
        public Transform LightningBox;
        Light LightA;
        Light LightB; //keep lights here and update them in the script local light as needed
        public void LightningUpdate()
        {

            if (Application.isPlaying)
            {
                if (EnableLightning)
                {
                    if (LightningOne == null)
                    {
                        LightningOne = Instantiate(LightningPrefab).transform;
                        LightA = LightningOne.GetComponentInChildren<ChainLightning_SKYMASTER>().startLight;
                        LightningOne.gameObject.SetActive(false);
                    }
                    if (LightningTwo == null)
                    {
                        LightningTwo = Instantiate(LightningPrefab).transform;
                        LightB = LightningTwo.GetComponentInChildren<ChainLightning_SKYMASTER>().startLight;//v4.0 - 2.1.24 IG
                        LightningTwo.gameObject.SetActive(false);
                    }

                    //move around
                    if (LightningBox != null)
                    {
                        if (Time.fixedTime - last_lightning_time > lightning_every - UnityEngine.Random.Range(-lightning_rate_offset, lightning_rate_offset))
                        {

                            Vector2 MinMaXLRangeX = LightningBox.position.x * Vector2.one + (LightningBox.localScale.x / 2) * new Vector2(-1, 1);
                            Vector2 MinMaXLRangeY = LightningBox.position.y * Vector2.one + (LightningBox.localScale.y / 2) * new Vector2(-1, 1);
                            Vector2 MinMaXLRangeZ = LightningBox.position.z * Vector2.one + (LightningBox.localScale.z / 2) * new Vector2(-1, 1);

                            LightningOne.position = new Vector3(UnityEngine.Random.Range(MinMaXLRangeX.x, MinMaXLRangeX.y),
                                UnityEngine.Random.Range(MinMaXLRangeY.x, MinMaXLRangeY.y), UnityEngine.Random.Range(MinMaXLRangeZ.x, MinMaXLRangeZ.y));
                            if (UnityEngine.Random.Range(0, SkyManager.WeatherSeverity + 1) == 1)
                            {
                                //do nothing
                            }
                            else
                            {
                                LightningOne.gameObject.SetActive(true);
                                localLight = LightA;
                            }

                            LightningTwo.position = new Vector3(UnityEngine.Random.Range(MinMaXLRangeX.x, MinMaXLRangeX.y),
                                UnityEngine.Random.Range(MinMaXLRangeY.x, MinMaXLRangeY.y), UnityEngine.Random.Range(MinMaXLRangeZ.x, MinMaXLRangeZ.y));
                            if (UnityEngine.Random.Range(0, SkyManager.WeatherSeverity + 1) == 1)
                            {
                                //do nothing
                            }
                            else
                            {
                                LightningTwo.gameObject.SetActive(true);
                                localLight = LightB;
                            }

                            last_lightning_time = Time.fixedTime;
                        }
                        else
                        {
                            if (Time.fixedTime - last_lightning_time > max_lightning_time)
                            {
                                if (LightningOne != null)
                                {
                                    if (LightningOne.gameObject.activeInHierarchy)
                                    {
                                        LightningOne.gameObject.SetActive(false); localLight = null; //v4.0
                                    }
                                }
                                if (LightningTwo != null)
                                {
                                    if (LightningTwo.gameObject.activeInHierarchy)
                                    {
                                        LightningTwo.gameObject.SetActive(false); localLight = null; //v4.0
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (LightningOne != null)
                    {
                        if (LightningOne.gameObject.activeInHierarchy)
                        {
                            LightningOne.gameObject.SetActive(false); localLight = null; //v4.0
                        }
                    }
                    if (LightningTwo != null)
                    {
                        if (LightningTwo.gameObject.activeInHierarchy)
                        {
                            LightningTwo.gameObject.SetActive(false); localLight = null; //v4.0
                        }
                    }
                }
            }

        }

        bool init_values = true;

        void Update()
        {

            //v4.8
            //if (windByWindZone)
            //{
            //
            //}

            //v4.8
            if (init_values)
            {
                Start1();
                init_values = false;
            }

            //v2.1.24 - setup shadows-backlayer camera
            createDepthSetup();
            createShadowDome();
            createLightningBox();

            //v2.1.24
            if (Application.isPlaying) { updateShadows = true; }//v4.8
            shadowsUpdate();
            if (Application.isPlaying) { updateReflectionCamera = true; }//v4.8
            updateReflections();
            if (Application.isPlaying)
            {
                LightningUpdate();
            }


            if (SkyManager != null)
            {
                //				if(SkyManager.Current_Time > 8 & SkyManager.Current_Time <20){
                //					height = 166;
                //				}else{
                //					height = 100;
                //				}
                if (useTOD)
                {

                    //v4.6
                    bool enter = true;
                    if (heavyStorm && (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm
                        || SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm))
                    {
                        _GroundColor = Vector3.Lerp(_GroundColor, new Vector3(0, 0, 0), Time.deltaTime * weatherTransitionSpeed * 0.12f);
                        enter = false;
                    }

                    if (SkyManager.UseGradients && enter)
                    {
                        //_SkyTint = new Vector3 (SkyManager.gradSkyColor.r * colorMultiplier.x, SkyManager.gradSkyColor.g * colorMultiplier.y, SkyManager.gradSkyColor.b * colorMultiplier.z);
                        Vector3 _SkyTintF = new Vector3(SkyManager.gradSkyColor.r * colorMultiplier.x, SkyManager.gradSkyColor.g * colorMultiplier.y, SkyManager.gradSkyColor.b * colorMultiplier.z);
                        _SkyTint = Vector3.Lerp(_SkyTint, _SkyTintF, Time.deltaTime * weatherTransitionSpeed * 0.12f);
                        if (tintUnder)
                        {
                            //_GroundColor = new Vector3 (SkyManager.gradSkyColor.r * UnderColorMultiplier.x, SkyManager.gradSkyColor.g * UnderColorMultiplier.y, SkyManager.gradSkyColor.b * UnderColorMultiplier.z);
                            Vector3 _GroundColorF = new Vector3(SkyManager.gradSkyColor.r * UnderColorMultiplier.x, SkyManager.gradSkyColor.g * UnderColorMultiplier.y, SkyManager.gradSkyColor.b * UnderColorMultiplier.z);
                            _GroundColor = Vector3.Lerp(_GroundColor, _GroundColorF, Time.deltaTime * weatherTransitionSpeed * 0.12f);
                        }
                    }
                }

                //v4.6
                if (useWeather && !isForReflections)
                {
                    if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Sunny)
                    {
                        //_NoiseBias = Mathf.Lerp(_NoiseBias, sunnyDensity - 7.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        if (windByWeather)
                        {
                            windMultiply = Mathf.Lerp(windMultiply, windSunny, Time.deltaTime * 0.005f);
                        }
                    }
                    if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Cloudy)
                    {
                        //_NoiseBias = Mathf.Lerp(_NoiseBias, cloudyDensity - 6.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        if (windByWeather)
                        {
                            windMultiply = Mathf.Lerp(windMultiply, windCloudy, Time.deltaTime * 0.005f);
                        }
                    }
                    if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm)
                    {
                        //_NoiseBias = Mathf.Lerp(_NoiseBias, stormyDensity - 6.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        if (windByWeather)
                        {
                            windMultiply = Mathf.Lerp(windMultiply, windStorm, Time.deltaTime * 0.005f);
                        }
                    }
                    if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm)
                    {
                        //_NoiseBias = Mathf.Lerp(_NoiseBias, stormyDensity - 6.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        //EnableLightning = true;
                        if (windByWeather)
                        {
                            windMultiply = Mathf.Lerp(windMultiply, windStorm, Time.deltaTime * 0.005f);
                        }
                    }

                    if (SkyManager.currentWeatherName != prevWeather)
                    {
                        prevWeather = SkyManager.currentWeatherName;
                        weatherTransition = true;
                        weatherTransTimeStart = Time.fixedTime;
                    }
                    if (weatherTransition)
                    {
                        // Lerp to values based on chosen weather
                        // -6.5f sunny, -4.0f cloudy, -2.0f stormy
                        if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Sunny)
                        {
                            _NoiseBias = Mathf.Lerp(_NoiseBias, sunnyDensity - 7.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        }
                        if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Cloudy)
                        {
                            _NoiseBias = Mathf.Lerp(_NoiseBias, cloudyDensity - 6.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        }
                        if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm)
                        {
                            _NoiseBias = Mathf.Lerp(_NoiseBias, stormyDensity - 6.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        }
                        if (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm)
                        {
                            _NoiseBias = Mathf.Lerp(_NoiseBias, stormyDensity - 6.5f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            EnableLightning = true;
                        }
                        else
                        {
                            EnableLightning = false;
                        }

                        if (heavyStorm && (SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm
                        || SkyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm))
                        {
                            localLightFalloff = Mathf.Lerp(localLightFalloff, 2.6f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            localLightIntensity = Mathf.Lerp(localLightIntensity, 6.0f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            _SunSize = Mathf.Lerp(_SunSize, -1.5f, Time.deltaTime * weatherTransitionSpeed * 0.16f);
                            _BackShade = Mathf.Lerp(_BackShade, 0.73f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            _UndersideCurveFactor = Mathf.Lerp(_UndersideCurveFactor, 0.1f, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        }
                        else
                        {
                            localLightFalloff = Mathf.Lerp(localLightFalloff, localLightFalloffP, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            localLightIntensity = Mathf.Lerp(localLightIntensity, localLightIntensityP, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            _SunSize = Mathf.Lerp(_SunSize, sinSizeP, Time.deltaTime * weatherTransitionSpeed * 0.16f);
                            _BackShade = Mathf.Lerp(_BackShade, backShadeP, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                            _UndersideCurveFactor = Mathf.Lerp(_UndersideCurveFactor, underSideP, Time.deltaTime * weatherTransitionSpeed * 0.02f);
                        }

                        if (Time.fixedTime - weatherTransTimeStart > weatherTransitionDuration)
                        {
                            weatherTransition = false;
                        }
                    }

                    //v4.8
                    if (SkyManager.windZone != null)
                    {
                        if (windByWindZone)
                        {
                            windMultiply = Mathf.Lerp(windMultiply, SkyManager.windZone.windMain + 0.0f, Time.deltaTime * 0.02f);
                        }

                        //assign cloud speeds based on windzone direction and windMultiplier
                        _Scroll1.x = -windMultiply * SkyManager.windZone.transform.forward.x; //v4.8.2 fix wind opposite of cloude motion
                        _Scroll1.z = -windMultiply * SkyManager.windZone.transform.forward.z;
                        _Scroll2.x = _Scroll1.x;
                        _Scroll2.z = _Scroll1.z;
                    }

                }//END if useWeather

                //v4.2
                if (adjustNightTimeDensity)
                {
                    //if ((!SkyManager.AutoSunPosition && (SkyManager.Current_Time >= (9 + SkyManager.Shift_dawn) & SkyManager.Current_Time <= (SkyManager.NightTimeMax + SkyManager.Shift_dawn)))
                    if ( //v4.8.6
                    (!SkyManager.AutoSunPosition && (SkyManager.Current_Time >= (9 + SkyManager.Shift_dawn + shift_dawn)
                    && SkyManager.Current_Time <= (SkyManager.NightTimeMax + SkyManager.Shift_dawn + shift_dusk)))
                    ||
                    (SkyManager.AutoSunPosition && SkyManager.Rot_Sun_X > 0))
                    {
                        _FarDist = _FarDistDay;
                    }
                    else
                    {
                        _FarDist = _FarDistNight;
                    }
                }//
                //v4.8.2
                if (adjustNightTimeSun) //put moon in sun slot
                {
                    if ((!SkyManager.AutoSunPosition && (SkyManager.Current_Time >= (9 + SkyManager.Shift_dawn) & SkyManager.Current_Time <= (SkyManager.NightTimeMax + SkyManager.Shift_dawn)))
                    ||
                    (SkyManager.AutoSunPosition && SkyManager.Rot_Sun_X > 0))
                    {
                        if (Sun != SkyManager.SunObj.transform)
                        {
                            Sun = SkyManager.SunObj.transform;
                        }
                    }
                    else
                    {
                        if (Sun != SkyManager.MoonObj.transform)
                        {
                            Sun = SkyManager.MoonObj.transform;
                        }
                    }
                }
            }
            if (colourArray == null)
            {
                colourArray = new Color[gradientResolution];
            }
            //if (prevGradient != DistGradient) {
            //if (1 == 0) //v4.1e
            //{
            //    for (int i = 0; i < colourArray.Length; i++)
            //    {
            //        colourArray[i] = DistGradient.Evaluate((float)i / (float)colourArray.Length);
            //    }
            //    if (!Made_texture)
            //    {
            //        colourPalette = new Texture2D(gradientResolution, 10, TextureFormat.ARGB32, false);
            //        colourPalette.filterMode = FilterMode.Point;
            //        colourPalette.wrapMode = TextureWrapMode.Clamp;
            //        Made_texture = true;
            //    }
            //    for (int x = 0; x < gradientResolution; x++)
            //    {
            //        for (int y = 0; y < 10; y++)
            //        {
            //            colourPalette.SetPixel(x, y, colourArray[x]);
            //        }
            //    }
            //    colourPalette.Apply();
            //}
            //prevGradient = DistGradient;
            //}

        }

        //v3.4.3
        public int gradientResolution = 256;
        //Gradient prevGradient;
        Color[] colourArray;

        //void Start(){
        void Awake()
        {

            Start1(); //v4.8


            _needsReset = true; //v4.0

            //SM v3.4.3
            //if (colourPalette == null) 
            {
                colourArray = new Color[gradientResolution];    //Color[] colourArray = new Color[gradientResolution];			
                for (int i = 0; i < colourArray.Length; i++)
                {
                    colourArray[i] = DistGradient.Evaluate((float)i / (float)colourArray.Length);
                }

                if (!Made_texture)
                {
                    colourPalette = new Texture2D(gradientResolution, 10, TextureFormat.ARGB32, false);
                    colourPalette.filterMode = FilterMode.Point;
                    colourPalette.wrapMode = TextureWrapMode.Clamp;
                    Made_texture = true;
                }

                for (int x = 0; x < gradientResolution; x++)
                {
                    for (int y = 0; y < 10; y++)
                    {
                        colourPalette.SetPixel(x, y, colourArray[x]);
                    }
                }

                colourPalette.Apply();
                //prevGradient = DistGradient;
            }

            //v3.5
            //fogMaterial.SetTexture("_NoiseTex1",texture3Dnoise1);
            //fogMaterial.SetTexture("_NoiseTex2",texture3Dnoise2);

            cam = GetComponent<Camera>(); //v2.1.15

        }

        public override bool CheckResources()
        {
            CheckSupport(true);

            fogMaterial = CheckShaderAndCreateMaterial(fogShader, fogMaterial);

            if (!isSupported)
                ReportAutoDisable();
            return isSupported;
        }


        //v2.1.20
        CameraClearFlags previousFlag;

        //v5.0.2
        Matrix4x4 left_world_from_view;
        Matrix4x4 right_world_from_view;
        // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
        Matrix4x4 left_screen_from_view;
        Matrix4x4 right_screen_from_view;
        Matrix4x4 left_view_from_screen;
        Matrix4x4 right_view_from_screen;

        //v2.1.15
        Camera cam;
        void OnPreRender()
        {
            if (!fastest)
            {
                cam = GetComponent<Camera>(); //v4.2

                previousFlag = cam.clearFlags; //v2.1.20
                cam.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0);
                cam.clearFlags = CameraClearFlags.SolidColor;

                //				if (blendBackground) { //v2.1.20
                //					backgroundCam.transform.rotation = cam.transform.rotation;
                //					backgroundCam.transform.position = cam.transform.position;
                //				}
            }


            //v5.0.2
            //update this every frame in editor 
#if UNITY_EDITOR
            m_single_pass_stereo = UnityEditor.PlayerSettings.stereoRenderingPath == UnityEditor.StereoRenderingPath.SinglePass;
#endif

            //camera must at least be in depth mode
            cam.depthTextureMode = DepthTextureMode.DepthNormals;
            Camera temp = cam;
            if (isForReflections && Camera.main != null) //GRAB CORRECT CAMERA
            {
                // temp = Camera.main;
                // Debug.Log("AAAAAAAAAAAAAAAAAAAAAAAaas   0000");
            }

            //cache matrices so they can be used in render image step
            if (fogMaterial != null && !isForReflections)
            {
                if (temp.stereoEnabled && (toggleCounter != 2 && toggleCounter != 3))
                {
                    // Debug.Log("AAAAAAAAAAAAAAAAAAAAAAAaas   ");
                    // Both stereo eye inverse view matrices
                    left_world_from_view = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                    right_world_from_view = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;

                    // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                    left_screen_from_view = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                    right_screen_from_view = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                    left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;
                    right_view_from_screen = GL.GetGPUProjectionMatrix(right_screen_from_view, true).inverse;

                    // Negate [1,1] to reflect Unity's CBuffer state
                    left_view_from_screen[1, 1] *= -1;
                    right_view_from_screen[1, 1] *= -1;

                    //Debug.Log(cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left));
                    //Debug.Log(cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right));
                    //Debug.Log(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
                    //Debug.Log(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right));
                    //if(backgroundCam != null)
                    //{
                    //    Debug.Log(backgroundCam.worldToCameraMatrix);
                    //    Debug.Log(backgroundCam.GetProjectionMatrix());
                    //}

                    // Store matrices
                    fogMaterial.SetMatrix("_LeftWorldFromView", left_world_from_view);
                    fogMaterial.SetMatrix("_RightWorldFromView", right_world_from_view);
                    fogMaterial.SetMatrix("_LeftViewFromScreen", left_view_from_screen);
                    fogMaterial.SetMatrix("_RightViewFromScreen", right_view_from_screen);

                    if (isForReflections)
                    {
                        //Debug.Log(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right));
                        //fogMaterial.SetMatrix("_LeftWorldFromView", left_world_from_view);
                        //fogMaterial.SetMatrix("_RightWorldFromView", left_world_from_view);
                        //fogMaterial.SetMatrix("_LeftViewFromScreen", left_view_from_screen);
                        //fogMaterial.SetMatrix("_RightViewFromScreen", left_view_from_screen);
                    }
                }
                else
                {
                    // Main eye inverse view matrix
                    left_world_from_view = cam.cameraToWorldMatrix;

                    // Inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                    //screen_from_view = cam.projectionMatrix;
                    //Matrix4x4 left_view_from_screen = GL.GetGPUProjectionMatrix(screen_from_view, true).inverse;
                    left_screen_from_view = cam.projectionMatrix;
                    left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;

                    // Negate [1,1] to reflect Unity's CBuffer state
                    left_view_from_screen[1, 1] *= -1;

                    // Store matrices
                    fogMaterial.SetMatrix("_LeftWorldFromView", left_world_from_view);
                    fogMaterial.SetMatrix("_LeftViewFromScreen", left_view_from_screen);


                    //if (isForReflections)
                    //{
                    //    if (Camera.main.stereoEnabled)
                    //    {
                    //        if (Camera.main.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
                    //        {
                    //            Debug.Log("BBBBBBBBBBBBBBBBBBBBBBBBb");
                    //            right_world_from_view = Camera.main.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
                    //            right_screen_from_view = Camera.main.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                    //            fogMaterial.SetMatrix("_LeftWorldFromView", right_world_from_view);
                    //            fogMaterial.SetMatrix("_LeftViewFromScreen", right_screen_from_view);
                    //        }
                    //    }
                    //}
                }
            }
            //END v5.0.2

        }
        void OnPostRender()
        {
            if (!fastest)
            {
                cam = GetComponent<Camera>(); //v4.2

                //cam.clearFlags = CameraClearFlags.Skybox;
                cam.clearFlags = previousFlag; //v2.1.20
            }
        }


        int toggleCounter = 0;
        public int splitPerFrames = 1; //v2.1.19
        public bool cameraMotionCompensate = true;//v2.1.19


        //v5.0.1
        //bool TryGetCenterEyeFeature(out Quaternion rotation)
        //{
        //    InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        //    if (device.isValid)
        //    {
        //        if (device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation))
        //            return true;
        //    }
        //    // This is the fail case, where there was no center eye was available.
        //    rotation = Quaternion.identity;
        //    return false;
        //}
        //bool TryGetLeftEyePos(out Vector3 position)
        //{
        //    InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.LeftEye);
        //    if (device.isValid)
        //    {
        //        if (device.TryGetFeatureValue(CommonUsages.leftEyePosition, out position))
        //            return true;
        //    }
        //    else
        //    {
        //        Debug.Log("Device LEFT EYE not valid");
        //    }
        //    // This is the fail case, where there was no center eye was available.
        //    position = Vector3.zero;
        //    return false;
        //}
        //bool TryGetRightEyePos(out Vector3 position)
        //{
        //    InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightEye);
        //    if (device.isValid)
        //    {
        //        if (device.TryGetFeatureValue(CommonUsages.rightEyePosition, out position))
        //            return true;
        //    }
        //    else
        //    {
        //        Debug.Log("Device RIGHT EYE not valid");
        //    }
        //    // This is the fail case, where there was no center eye was available.
        //    position = Vector3.zero;
        //    return false;
        //}
        // This is so you don't create garbage on every frame, reuse the same list
        List<XRNodeState> nodeStatesCache = new List<XRNodeState>();
        bool TryGetCenterEyeNodeStateRotation(out Quaternion rotation)
        {
            InputTracking.GetNodeStates(nodeStatesCache);
            for (int i = 0; i < nodeStatesCache.Count; i++)
            {
                XRNodeState nodeState = nodeStatesCache[i];
                if (nodeState.nodeType == XRNode.CenterEye)
                {
                    if (nodeState.TryGetRotation(out rotation))
                        return true;
                }
            }
            // This is the fail case, where there was no center eye was available.
            rotation = Quaternion.identity;
            return false;
        }
        bool TryGetLeftEyeNodeStatePosition(out Vector3 position)
        {
            InputTracking.GetNodeStates(nodeStatesCache);
            for (int i = 0; i < nodeStatesCache.Count; i++)
            {
                XRNodeState nodeState = nodeStatesCache[i];
                if (nodeState.nodeType == XRNode.LeftEye)
                {
                    if (nodeState.TryGetPosition(out position))
                        return true;
                }
            }
            // This is the fail case, where there was no center eye was available.
            position = Vector3.zero;
            return false;
        }
        Vector3 prevCenterPos;

        //v5.0.2
        //stores whether this is a single pass stereo build (not required but useful to be able to access)
        public bool m_single_pass_stereo = false;
        //on validate just reads stereo mode
        private void OnValidate()
        {
            //update the stereo flag in editor
#if UNITY_EDITOR
            m_single_pass_stereo = UnityEditor.PlayerSettings.stereoRenderingPath == UnityEditor.StereoRenderingPath.SinglePass;
#endif
            //Debug.Log(UnityEditor.PlayerSettings.stereoRenderingPath);
        }


        [ImageEffectOpaque]
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (CheckResources() == false || (!distanceFog && !heightFog))
            {
                Graphics.Blit(source, destination);
                return;
            }

            //Camera cam = GetComponent<Camera>(); //v2.1.15
            Transform camtr = cam.transform;


            //v5.0.3 - VR refelctions
            if (isForReflections) //if reflect camera, dont apply full VR
            {
                left_world_from_view = cam.cameraToWorldMatrix;
                left_screen_from_view = cam.projectionMatrix;
                Matrix4x4 left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;
                left_view_from_screen[1, 1] *= -1;
                left_view_from_screen[3, 3] += 3; //v5.0.4 - fix reflections not appear in low clip plane of 0.07
                //if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                {
                    fogMaterial.SetMatrix("_LeftWorldFromView", left_world_from_view);
                    fogMaterial.SetMatrix("_LeftViewFromScreen", left_view_from_screen);
                }
                // if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
                {
                    fogMaterial.SetMatrix("_RightWorldFromView", left_world_from_view);     //THE eye is set to 1 (right) even in camera not stereo enabled !!!!!!!!!!!!!
                    fogMaterial.SetMatrix("_RightViewFromScreen", left_view_from_screen);
                }
                //RIGHT EYE SOMEHOW
                //left_world_from_view = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                // right_world_from_view = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
                // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                //left_screen_from_view = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                // right_screen_from_view = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                //left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;
                //Matrix4x4 right_view_from_screen = GL.GetGPUProjectionMatrix(right_screen_from_view, true).inverse;
                //left_view_from_screen[1, 1] *= -1;
                //right_view_from_screen[1, 1] *= -1;
                //fogMaterial.SetMatrix("_LeftWorldFromView", right_world_from_view);
                //fogMaterial.SetMatrix("_LeftViewFromScreen", right_view_from_screen);
            }


            //Debug.Log(UnityEditor.PlayerSettings.stereoRenderingPath);
            //v5.0.1
            if (adjustVR)
            {
                //https://forum.unity.com/threads/updating-from-getlocalrotation-to-getnodestates.695527/
                prevCenterPos = cam.transform.position; //SAVE PREVIOUS, alter and restore
                if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                {
                    camtr.position = camtr.position + new Vector3(shiftLeftEye, 0, 0);
                    //Debug.Log("left eye" + camtr.transform.position);
                    //Debug.Log("left eye" + camtr.transform.eulerAngles);
                }
            }


            float camNear = cam.nearClipPlane;
            float camFar = cam.farClipPlane;
            float camFov = cam.fieldOfView;
            float camAspect = cam.aspect;

            Matrix4x4 frustumCorners = Matrix4x4.identity;

            float fovWHalf = camFov * 0.5f;

            Vector3 toRight = camtr.right * camNear * Mathf.Tan(fovWHalf * Mathf.Deg2Rad) * camAspect;
            Vector3 toTop = camtr.up * camNear * Mathf.Tan(fovWHalf * Mathf.Deg2Rad);

            Vector3 topLeft = (camtr.forward * camNear - toRight + toTop);
            float camScale = topLeft.magnitude * camFar / camNear;

            topLeft.Normalize();
            topLeft *= camScale;

            Vector3 topRight = (camtr.forward * camNear + toRight + toTop);
            topRight.Normalize();
            topRight *= camScale;

            Vector3 bottomRight = (camtr.forward * camNear + toRight - toTop);
            bottomRight.Normalize();
            bottomRight *= camScale;

            Vector3 bottomLeft = (camtr.forward * camNear - toRight - toTop);
            bottomLeft.Normalize();
            bottomLeft *= camScale;

            frustumCorners.SetRow(0, topLeft);
            frustumCorners.SetRow(1, topRight);
            frustumCorners.SetRow(2, bottomRight);
            frustumCorners.SetRow(3, bottomLeft);

            var camPos = camtr.position;
            float FdotC = camPos.y - height;
            float paramK = (FdotC <= 0.0f ? 1.0f : 0.0f);
            fogMaterial.SetMatrix("_FrustumCornersWS", frustumCorners);
            fogMaterial.SetVector("_CameraWS", camPos);
            fogMaterial.SetVector("_HeightParams", new Vector4(height, FdotC, paramK, heightDensity * 0.5f));
            fogMaterial.SetVector("_DistanceParams", new Vector4(-Mathf.Max(startDistance, 0.0f), 0, 0, 0));


            //v5.0.2



            //v3.5.3
            if (!useFluidTexture)
            {
                fogMaterial.SetTexture("_InteractTexture", _InteractTexture);
            }
            else
            {
                fogMaterial.SetTexture("_InteractTexture", fluidFlow.GetTexture("_MainTex")); //v4.0
            }
            fogMaterial.SetVector("_InteractTexturePos", _InteractTexturePos);
            fogMaterial.SetVector("_InteractTextureAtr", _InteractTextureAtr);
            fogMaterial.SetVector("_InteractTextureOffset", _InteractTextureOffset); //v4.0

            //v3.5.1
            fogMaterial.SetFloat("_NearZCutoff", _NearZCutoff);
            fogMaterial.SetFloat("_HorizonYAdjust", _HorizonYAdjust);
            fogMaterial.SetFloat("_HorizonZAdjust", _HorizonZAdjust);//v2.1.24
            fogMaterial.SetFloat("_FadeThreshold", _FadeThreshold);

            //v4.1f
            fogMaterial.SetFloat("_mobileFactor", _mobileFactor); //v4.1f
            fogMaterial.SetFloat("_alphaFactor", _alphaFactor);

            //v3.5
            fogMaterial.SetFloat("_SampleCount0", _SampleCount0);
            fogMaterial.SetFloat("_SampleCount1", _SampleCount1);
            fogMaterial.SetInt("_SampleCountL", _SampleCountL);

            fogMaterial.SetFloat("_NoiseFreq1", _NoiseFreq1);
            fogMaterial.SetFloat("_NoiseFreq2", _NoiseFreq2);
            fogMaterial.SetFloat("_NoiseAmp1", _NoiseAmp1);
            fogMaterial.SetFloat("_NoiseAmp2", _NoiseAmp2);
            fogMaterial.SetFloat("_NoiseBias", _NoiseBias);

            //v4.8.6
            if (Application.isPlaying)
            {
                fogMaterial.SetVector("_Scroll1", _Scroll1);
                fogMaterial.SetVector("_Scroll2", _Scroll2);
            }
            else
            {
                fogMaterial.SetVector("_Scroll1", Vector4.zero);
                fogMaterial.SetVector("_Scroll2", Vector4.zero);
            }

            fogMaterial.SetFloat("_Altitude0", _Altitude0);
            fogMaterial.SetFloat("_Altitude1", _Altitude1);
            fogMaterial.SetFloat("_FarDist", _FarDist);

            //fogMaterial.SetFloat("_Scatter",_Scatter);
            fogMaterial.SetFloat("_HGCoeff", _HGCoeff);
            //fogMaterial.SetFloat("_Extinct",_Extinct); //v4.8.6


            fogMaterial.SetFloat("_Exposure", _ExposureUnder); //v4.0

            //v4.8.4
            if (!adjustNightLigting)
            {
                fogMaterial.SetFloat("_Scatter", _Scatter);
                fogMaterial.SetVector("_GroundColor", _GroundColor);//
                fogMaterial.SetFloat("_BackShade", _BackShade);
                fogMaterial.SetFloat("turbidity", turbidity);
                fogMaterial.SetFloat("_Extinct", _Extinct);
            }
            else
            {
                if ((!SkyManager.AutoSunPosition && (SkyManager.Current_Time >= (9 + SkyManager.Shift_dawn) & SkyManager.Current_Time <= (SkyManager.NightTimeMax + SkyManager.Shift_dawn)))
                    ||
                    (SkyManager.AutoSunPosition && SkyManager.Rot_Sun_X > 0))
                {
                    //DAY
                    float currentScatter = fogMaterial.GetFloat("_Scatter");
                    Vector3 current_GroundColor = fogMaterial.GetVector("_GroundColor");
                    float currentBackShade = fogMaterial.GetFloat("_BackShade");
                    float currentTurbidity = fogMaterial.GetFloat("turbidity");//
                    float currentExtinct = fogMaterial.GetFloat("_Extinct");
                    if (Time.fixedTime < 1)
                    {
                        fogMaterial.SetFloat("_Scatter", _Scatter);
                        fogMaterial.SetVector("_GroundColor", _GroundColor);//
                        fogMaterial.SetFloat("_BackShade", _BackShade);
                        fogMaterial.SetFloat("turbidity", turbidity);
                        fogMaterial.SetFloat("_Extinct", _Extinct);
                    }
                    else
                    {
                        fogMaterial.SetFloat("_Scatter", Mathf.Lerp(currentScatter, _Scatter, Time.deltaTime * 0.2f));
                        fogMaterial.SetVector("_GroundColor", Vector3.Lerp(current_GroundColor, _GroundColor, Time.deltaTime * 0.2f));//
                        fogMaterial.SetFloat("_BackShade", Mathf.Lerp(currentBackShade, _BackShade, Time.deltaTime * 0.2f));
                        fogMaterial.SetFloat("turbidity", Mathf.Lerp(currentTurbidity, turbidity, Time.deltaTime * 0.2f));
                        fogMaterial.SetFloat("_Extinct", Mathf.Lerp(currentExtinct, _Extinct, Time.deltaTime * 0.2f));
                    }
                }
                else
                {
                    //NIGHT
                    float currentScatter = fogMaterial.GetFloat("_Scatter");
                    Vector3 current_GroundColor = fogMaterial.GetVector("_GroundColor");
                    float currentBackShade = fogMaterial.GetFloat("_BackShade");
                    float currentTurbidity = fogMaterial.GetFloat("turbidity");
                    float currentExtinct = fogMaterial.GetFloat("_Extinct");
                    if (Time.fixedTime < 1)
                    {
                        fogMaterial.SetFloat("_Scatter", scatterNight);
                        fogMaterial.SetVector("_GroundColor", groundColorNight);//
                        fogMaterial.SetFloat("_BackShade", _BackShade);
                        fogMaterial.SetFloat("turbidity", turbidity);
                        fogMaterial.SetFloat("_Extinct", _Extinct);
                    }
                    else
                    {
                        fogMaterial.SetFloat("_Scatter", Mathf.Lerp(currentScatter, scatterNight, Time.deltaTime * 0.2f));
                        fogMaterial.SetVector("_GroundColor", Vector3.Lerp(current_GroundColor, groundColorNight, Time.deltaTime * 0.2f));//
                        fogMaterial.SetFloat("_BackShade", Mathf.Lerp(currentBackShade, backShadeNight, Time.deltaTime * 0.2f));
                        fogMaterial.SetFloat("turbidity", Mathf.Lerp(currentTurbidity, turbidityNight, Time.deltaTime * 0.2f));
                        fogMaterial.SetFloat("_Extinct", Mathf.Lerp(currentExtinct, extinctionNight, Time.deltaTime * 0.2f));
                    }
                }
            }


            fogMaterial.SetFloat("_SunSize", _SunSize);
            fogMaterial.SetVector("_SkyTint", _SkyTint);
            fogMaterial.SetFloat("_AtmosphereThickness", _AtmosphereThickness);


            //v3.5
            //fogMaterial.SetFloat("_BackShade",_BackShade); //v4.8.6 moved up for night time change
            fogMaterial.SetFloat("_UndersideCurveFactor", _UndersideCurveFactor);

            //v2.1.19
            if (localLight != null)
            {
                Vector3 localLightPos = localLight.transform.position;
                //float intensity = Mathf.Pow(10, 3 + (localLightFalloff - 3) * 3);
                currentLocalLightIntensity = Mathf.Pow(10, 3 + (localLightFalloff - 3) * 3);
                //fogMaterial.SetVector ("_LocalLightPos", new Vector4 (localLightPos.x, localLightPos.y, localLightPos.z, localLight.intensity * localLightIntensity * intensity));
                fogMaterial.SetVector("_LocalLightPos", new Vector4(localLightPos.x, localLightPos.y, localLightPos.z, localLight.intensity * localLightIntensity * currentLocalLightIntensity));
                fogMaterial.SetVector("_LocalLightColor", new Vector4(localLight.color.r, localLight.color.g, localLight.color.b, localLightFalloff));
            }
            else
            {
                if (currentLocalLightIntensity > 0)
                {
                    currentLocalLightIntensity = 0;
                    //fogMaterial.SetVector ("_LocalLightPos", new Vector4 (localLightPos.x, localLightPos.y, localLightPos.z, localLight.intensity * localLightIntensity * intensity));
                    fogMaterial.SetVector("_LocalLightColor", Vector4.zero);
                }
            }

            //VFOG
            //fogMaterial.SetMatrix ("_WorldClip", cam.cameraToWorldMatrix * cam.projectionMatrix.inverse); 

            //			fogMaterial.SetFloat("_SampleCount0",42);
            //			fogMaterial.SetFloat("_SampleCount1",43);
            //			fogMaterial.SetInt("_SampleCount",44);
            //
            //			fogMaterial.SetFloat("_NoiseFreq1",3.1f);
            //			fogMaterial.SetFloat("_NoiseFreq2",35.1f);
            //			fogMaterial.SetFloat("_NoiseAmp1",5f);
            //			fogMaterial.SetFloat("_NoiseAmp2",1f);
            //			fogMaterial.SetFloat("_NoiseBias",-0.2f);
            //
            //			fogMaterial.SetVector ("_Scroll1",new Vector3 (0.01f, 0.08f, 0.06f));
            //			fogMaterial.SetVector ("_Scroll2", new Vector3 (0.01f, 0.05f, 0.03f));
            //
            //			fogMaterial.SetFloat("_Altitude0",1500f);
            //			fogMaterial.SetFloat("_Altitude1",3500f);
            //			fogMaterial.SetFloat("_FarDist",30000f);
            //
            //			fogMaterial.SetFloat("_Scatter",0.008f);
            //			fogMaterial.SetFloat("_HGCoeff",0.5f);
            //			fogMaterial.SetFloat("_Extinct",0.01f);
            //
            //
            //			fogMaterial.SetFloat("_Exposure",1.3f);
            //			fogMaterial.SetVector ("_GroundColor", new Vector3 (0.369f, 0.349f, 0.341f));//
            //			fogMaterial.SetFloat("_SunSize",0.04f);
            //			fogMaterial.SetVector ("_SkyTint", new Vector3 (0.5f, 0.5f, 0.5f));
            //			fogMaterial.SetFloat("_AtmosphereThickness",1.0f);




            //SM v1.7
            fogMaterial.SetFloat("luminance", luminance);
            fogMaterial.SetFloat("lumFac", lumFac);
            fogMaterial.SetFloat("Multiplier1", ScatterFac);
            fogMaterial.SetFloat("Multiplier2", TurbFac);
            fogMaterial.SetFloat("Multiplier3", HorizFac);
            //fogMaterial.SetFloat("turbidity",turbidity); //v4.8.6
            fogMaterial.SetFloat("reileigh", reileigh);
            fogMaterial.SetFloat("mieCoefficient", mieCoefficient);
            fogMaterial.SetFloat("mieDirectionalG", mieDirectionalG);
            fogMaterial.SetFloat("bias", bias);
            fogMaterial.SetFloat("contrast", contrast);

            //v4.8
            fogMaterial.SetFloat("varianceAltitude1", varianceAltitude1);
            if (isForReflections)
            {
                //v4.8
                if (adjustNightTimeSun && Application.isPlaying && Time.fixedTime > 5)
                {
                    Vector3 getDir = fogMaterial.GetVector("v3LightDir");
                    fogMaterial.SetVector("v3LightDir", Vector3.Lerp(getDir, new Vector3(-Sun.forward.x, Sun.forward.y, -Sun.forward.z), Time.deltaTime * 0.2f));
                }
                else
                {
                    fogMaterial.SetVector("v3LightDir", new Vector3(-Sun.forward.x, Sun.forward.y, -Sun.forward.z));
                }
            }
            else
            {
                //v4.8
                if (adjustNightTimeSun && Application.isPlaying && Time.fixedTime > 5)
                {
                    Vector3 getDir = fogMaterial.GetVector("v3LightDir");
                    fogMaterial.SetVector("v3LightDir", Vector3.Lerp(getDir, -Sun.forward, Time.deltaTime * 0.2f));
                }
                else
                {
                    fogMaterial.SetVector("v3LightDir", -Sun.forward);
                }
            }


            fogMaterial.SetVector("_TintColor", new Vector4(TintColor.x, TintColor.y, TintColor.z, 1));//68, 155, 345

            float Foggy = 0;
            if (FogSky)
            {
                Foggy = 1;
            }
            fogMaterial.SetFloat("FogSky", Foggy);
            fogMaterial.SetFloat("ClearSkyFac", ClearSkyFac);

            //v4.9.4
            fogMaterial.SetFloat("_cameraFarDistanceAdjust", cameraFarDistanceAdjust);

            var sceneMode = RenderSettings.fogMode;
            var sceneDensity = 0.01f; //RenderSettings.fogDensity;//v3.0
            var sceneStart = RenderSettings.fogStartDistance;
            var sceneEnd = RenderSettings.fogEndDistance;
            Vector4 sceneParams;
            bool linear = (sceneMode == FogMode.Linear);
            float diff = linear ? sceneEnd - sceneStart : 0.0f;
            float invDiff = Mathf.Abs(diff) > 0.0001f ? 1.0f / diff : 0.0f;
            sceneParams.x = sceneDensity * 1.2011224087f; // density / sqrt(ln(2)), used by Exp2 fog mode
            sceneParams.y = sceneDensity * 1.4426950408f; // density / ln(2), used by Exp fog mode
            sceneParams.z = linear ? -invDiff : 0.0f;
            sceneParams.w = linear ? sceneEnd * invDiff : 0.0f;
            fogMaterial.SetVector("_SceneFogParams", sceneParams);
            fogMaterial.SetVector("_SceneFogMode", new Vector4((int)sceneMode, useRadialDistance ? 1 : 0, 0, 0));

            int pass = 0;
            if (distanceFog && heightFog)
                pass = 0; // distance + height
            else if (distanceFog)
                pass = 1; // distance only
            else
                pass = 2; // height only


            //v4.8
            if (isForReflections)
            {
                fogMaterial.SetFloat("_invertX", 1);
            }
            else
            {
                fogMaterial.SetFloat("_invertX", 0);
            }
            if (isForReflections)
            {
                fogMaterial.SetFloat("_invertRay", -1);
            }
            else
            {
                fogMaterial.SetFloat("_invertRay", 1);
            }
            if (isForReflections)
            {
                //Vector3 camPos = Camera.main.transform.position;
                //fogMaterial.SetVector("_WorldSpaceCameraPosC", new Vector3(-camPos.x, -camPos.y, camPos.z));
                fogMaterial.SetVector("_WorldSpaceCameraPosC", camPos);
            }
            else
            {
                fogMaterial.SetVector("_WorldSpaceCameraPosC", camPos);
            }
            //SM v1.7
            //if (colourPalette == null)  //v3.4.3
            //			{
            //				Color[] colourArray = new Color[256];			
            //				for(int i=0;i<colourArray.Length;i++){
            //					colourArray[i] = DistGradient.Evaluate((float)i/(float)colourArray.Length);
            //				}	
            //				
            //				if(!Made_texture){
            //					colourPalette = new Texture2D (256, 10, TextureFormat.ARGB32, false);
            //					colourPalette.filterMode = FilterMode.Point;
            //					colourPalette.wrapMode = TextureWrapMode.Clamp;
            //					Made_texture=true;
            //				}
            //				
            //				
            //				for(int x = 0; x < 256; x++){
            //					for(int y = 0; y < 10; y++){
            //						colourPalette.SetPixel(x,y,colourArray[x]);
            //					}
            //				}
            //				
            //				colourPalette.Apply();
            //				
            //				
            //			}

            //			if(colourArray == null){
            //				colourArray = new Color[gradientResolution];
            //			}
            //			if (prevGradient != DistGradient) {
            //				for(int i=0;i<colourArray.Length;i++){
            //					colourArray[i] = DistGradient.Evaluate((float)i/(float)colourArray.Length);
            //				}	
            //				if(!Made_texture){
            //					colourPalette = new Texture2D (gradientResolution, 10, TextureFormat.ARGB32, false);
            //					colourPalette.filterMode = FilterMode.Point;
            //					colourPalette.wrapMode = TextureWrapMode.Clamp;
            //					Made_texture=true;
            //				}
            //				for(int x = 0; x < gradientResolution; x++){
            //					for(int y = 0; y < 10; y++){
            //						colourPalette.SetPixel(x,y,colourArray[x]);
            //					}
            //				}
            //				colourPalette.Apply();
            //				prevGradient = DistGradient;
            //			}

            if (!downScale)
            {
                CustomGraphicsBlit(source, destination, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, WebGL);
            }
            else
            {
                //if (Time.fixedTime - LastUpdateTime > updateRate) {
                // LastUpdateTime = Time.fixedTime; //v4.1e


                //v2.1.15
                if (!fastest)
                {
                    if (tmpBuffer == null || tmpBufferL == null || tmpBufferR == null)
                    {
                        var format = cam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                        //tmpBuffer = RenderTexture.GetTemporary(source.width+125, source.height+125, 0, format);//v2.1.19 - add extra pixels to cover for frame displacement //v5.0.2

                        //v5.0.2
                        //https://forum.unity.com/threads/creating-image-effects-for-singlepass-stereo-rendering-with-multiple-shader-passes.710609/
                        RenderTextureDescriptor desc;
                        if (XRSettings.enabled)
                        {
                            desc = XRSettings.eyeTextureDesc;
                            //Debug.Log("Created temp texture for VR with W:" + desc.width + " H:" + desc.height);
                            desc = new RenderTextureDescriptor(source.width + 125, source.height + 125, format, 0);
                            // Debug.Log("Created temp texture for VR with W2:" + desc.width + " H2:" + desc.height);
                        }
                        else
                        {
                            desc = new RenderTextureDescriptor(source.width + 125, source.height + 125, format, 0);// (Screen.width, Screen.height); // Not XR
                                                                                                                   // Debug.Log("Created temp texture for non VR");
                        }
                        //RenderTexture rt = RenderTexture.GetTemporary(desc);
                        tmpBuffer = RenderTexture.GetTemporary(desc);

                        //v5.0.3
                        tmpBufferL = RenderTexture.GetTemporary(desc);
                        tmpBufferR = RenderTexture.GetTemporary(desc);

                        if (cam.stereoEnabled)
                        {
                            if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                            {
                                RenderTexture.active = tmpBufferL;
                            }
                            else
                            {
                                RenderTexture.active = tmpBufferR;
                            }
                        }
                        else
                        {
                            RenderTexture.active = tmpBuffer;
                        }
                        GL.ClearWithSkybox(false, cam);
                        if (blendBackground)
                        { //v2.1.20

                            //v4.8
                            if (isForReflections)
                            {
                                backgroundCam.transform.rotation = cam.transform.rotation;
                                backgroundCam.transform.position = cam.transform.position;

                                if (Camera.main.stereoEnabled) //v5.0.5 - fix issue with black background at game start
                                {
                                    //https://answers.unity.com/questions/1359718/what-do-the-values-in-the-matrix4x4-for-cameraproj.html                                   

                                    Matrix4x4 projectionMatrix = cam.projectionMatrix;
                                    //projectionMatrix[0, 0] *= -1;
                                    float adjuster = adjustReflPos;
                                    float adjuster2 = adjustReflPos2;
                                    float adjuster3 = adjustReflPos3;

                                    //FIX background rotation -  30 degrees 5 adjuster, -30 -5, 15 2.5
                                    float angle = cam.transform.eulerAngles.x;
                                    if (angle > 180)
                                    {
                                        angle = -(360 - angle);
                                    }
                                    // Debug.Log(angle);
                                    adjuster3 = -angle / 6;

                                    if (backgroundCam.nearClipPlane == 1)
                                    {
                                        adjuster = -adjustReflPos;
                                        adjuster2 = -adjustReflPos2;
                                        //adjuster3= -adjustReflPos3;
                                        adjuster3 = -adjuster3;
                                    }
                                    Matrix4x4 rot = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster, Vector3.up));
                                    //Matrix4x4 rot2 = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster2, Vector3.forward));
                                    Matrix4x4 rot3 = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster3, Vector3.forward));
                                    //backgroundCam.projectionMatrix = projectionMatrix * rot;
                                    backgroundCam.projectionMatrix = projectionMatrix * rot * rot3;
                                    // backgroundCam.worldToCameraMatrix = cam.cameraToWorldMatrix.inverse;// cam.worldToCameraMatrix;
                                    //backgroundCam.transform.RotateAroundLocal(Vector3.down, adjuster);

                                    Matrix4x4 rotA = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster2, Vector3.up));
                                    Matrix4x4 viewM = cam.cameraToWorldMatrix * rotA * rot3;
                                    //viewM[0, 0] *= -1;
                                    backgroundCam.worldToCameraMatrix = viewM.inverse;
                                }
                            }
                            else
                            {
                                if (cam.stereoEnabled)
                                {
                                    if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                    {
                                        // Debug.Log("eye LEFT A");
                                        backgroundCam.worldToCameraMatrix = left_world_from_view; //view matrix
                                        backgroundCam.projectionMatrix = left_screen_from_view;   //projection matrix

                                        //v5.0.3 - VR reflections
                                        if (SkyManager != null && SkyManager.water != null)
                                        {
                                            PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                            //waterR.RenderHelpCameras(cam);
                                            if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                            {
                                                waterR.WaterTileBeingRenderedA(transform, cam, 0); //Debug.Log("eye LEFT");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Debug.Log("eye RIGHT A");
                                        backgroundCam.worldToCameraMatrix = right_world_from_view; //view matrix
                                        backgroundCam.projectionMatrix = right_screen_from_view;  //projection matrix

                                        //v5.0.3 - VR reflections
                                        if (SkyManager != null && SkyManager.water != null)
                                        {
                                            PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                            if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                            {
                                                waterR.WaterTileBeingRenderedA(transform, cam, 1);// Debug.Log("eye RIGHT");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    backgroundCam.transform.rotation = cam.transform.rotation;
                                    backgroundCam.transform.position = cam.transform.position;

                                    //v5.0.5
                                    backgroundCam.worldToCameraMatrix = Camera.main.worldToCameraMatrix;
                                    backgroundCam.projectionMatrix = Camera.main.projectionMatrix;
                                }
                            }
                            //v2.1.23
                            //float ratio = (float)Screen.height / (float)Screen.width;						
                            //backgroundCam.rect = new Rect (backgroundCam.rect.position, new Vector2 (backgroundCam.rect.size.x, ratio)); //v4.1
                            if (!unity2018)
                            {
                                //backgroundCam.rect = new Rect(backgroundCam.rect.position, new Vector2(backgroundCam.rect.size.x, ratio)); //v4.8
                            }
                            else
                            {
                                //v4.1f
                                //backgroundCam.rect = new Rect(backgroundCam.rect.position, new Vector2(1, 1));
                                //if (backgroundCam.targetTexture != null)
                                //{
                                //    backgroundCam.targetTexture.Release();
                                //}
                                //backgroundCam.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);

                                //v5.0.8
                                if (Screen.width > 0 &&
                                    (backgroundCam.targetTexture == null || backgroundCam.targetTexture.width != Screen.width || backgroundCam.targetTexture.height != Screen.height)
                                    )
                                {
                                    backgroundCam.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
                                }
                            }
                            //v4.8
                            if (isForReflections)
                            {
                                backgroundCam.transform.eulerAngles = new Vector3(backgroundCam.transform.eulerAngles.x, backgroundCam.transform.eulerAngles.y, 180);
                                SkyManager.setMoonShader(backgroundCam.transform, 0.5f);
                                backgroundCam.Render();
                                tmpBuffer.DiscardContents();//v4.1f
                                Graphics.Blit(backgroundCam.targetTexture, tmpBuffer);
                                SkyManager.setMoonShader(Camera.main.transform, 1f);
                            }
                            else
                            {
                                backgroundCam.Render();
                                Graphics.Blit(backgroundCam.targetTexture, tmpBuffer, backgroundMat);
                            }
                        }

                        if (cam.stereoEnabled)
                        {
                            if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                            {
                                CustomGraphicsBlitOpt(source, destination, tmpBufferL, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, 0);
                                CustomGraphicsBlitOpt(source, destination, tmpBufferL, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, 0);
                            }
                            else
                            {
                                CustomGraphicsBlitOpt(source, destination, tmpBufferR, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, 1);
                                CustomGraphicsBlitOpt(source, destination, tmpBufferR, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, 1);
                            }
                        }
                        else
                        {
                            CustomGraphicsBlitOpt(source, destination, tmpBuffer, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, -1);
                            CustomGraphicsBlitOpt(source, destination, tmpBuffer, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, -1);
                        }
                    }
                    else
                    {
                        //v2.1.19
                        if (((!cam.stereoEnabled && toggleCounter != 0) || (cam.stereoEnabled && (toggleCounter == 2 || toggleCounter == 3))) && Application.isPlaying && splitPerFrames > 0)
                        {

                            //v4.8.3
                            //if (splitPerFrames > 0 && toggleCounter != splitPerFrames) {
                            if (enableReproject)
                            {
                                //fogMaterial.SetTexture("_CloudTex", tmpBuffer);
                                //fogMaterial.SetTexture("_CloudTexP", _cloudBufferP);
                                //float frameFraction = (float)toggleCounter / (float)splitPerFrames;
                                //fogMaterial.SetFloat("frameFraction", frameFraction);
                                //Graphics.Blit(source, tmpBuffer, fogMaterial, 5);//v4.8.3
                                //fogMaterial.SetTexture("_CloudTex", _cloudBuffer);
                                int signer = 1;
                                if (WebGL)
                                {
                                    signer = -1;
                                }
                                //v4.0
                                Camera cam1 = cam;
                                if (Camera.main != null)
                                {
                                    cam1 = Camera.main;
                                }
                                Matrix4x4 scaler = cam.cameraToWorldMatrix * Matrix4x4.Scale(new Vector3(2, 1 * signer, -1)) * cam1.projectionMatrix.inverse * Matrix4x4.Scale(new Vector3(-1, 1, 1));//v2.1.19


                                float frameFraction = (float)toggleCounter / (float)splitPerFrames;

                                float Xdisp = cam.transform.eulerAngles.y - prevCameraRotP.y;
                                float Ydisp = -(cam.transform.eulerAngles.x - prevCameraRotP.x);
                                //Debug.Log(Xdisp);
                                if (Xdisp > 122f || Xdisp < -122f)
                                {
                                    //Debug.Log("Xdisp = " + Xdisp);
                                    //Debug.Log("tmpBuffer width=" + tmpBuffer.width);
                                    // Debug.Log("tmpBuffer height=" + tmpBuffer.height);
                                    Xdisp = 0;
                                }
                                if (Ydisp > 122f || Ydisp < -122f)
                                {
                                    //Debug.Log("Ydisp = " + Ydisp);
                                    Ydisp = 0;
                                }

                                //Debug.Log("in " + (cam.transform.eulerAngles.x - prevCameraRot.x).ToString());
                                //Debug.Log("inA " + (cam.transform.eulerAngles.y - prevCameraRot.y).ToString());
                                //scaler[0, 3] = scaler[0, 3] - 1110.71f * (splitPerFrames - toggleCounter);//move in X, depending on camera motion and frames skipped
                                scaler[0, 3] = 1 * Xdisp;//0.0075f * Xdisp * 1;//0.005f * Xdisp;//0.009f * Xdisp;
                                scaler[1, 3] = 1 * Ydisp;//0.0062f * Ydisp * 1;// 0.0062f * Ydisp * 1;//0.012f * Ydisp;
                                scaler[2, 3] = 1;// 1f - 0.005f * (Xdisp+Ydisp); // 0.95f+ 0.005f*(splitPerFrames - toggleCounter); //image scaler
                                fogMaterial.SetMatrix("_WorldClip", scaler);
                                //Debug.Log("Xdisp =" + Xdisp + " scaleX="+ scaler[0, 3] + " Ydisp =" + Ydisp + " scaleY=" + scaler[1, 3]);

                                //v4.8.3
                                fogMaterial.SetTexture("_CloudTexP", _cloudBuffer);
                                fogMaterial.SetTexture("_CloudTex", _cloudBufferP);

                                fogMaterial.SetFloat("frameFraction", frameFraction);
                                //Graphics.Blit(_cloudBufferP, source, fogMaterial, 5);//v4.8.3
                                Graphics.Blit(_cloudBufferP, _cloudBufferP1, fogMaterial, 5);//v4.8.3
                                //Graphics.Blit(_cloudBuffer, _cloudBufferP1);//v4.8.3

                                //prevCameraRotP = cam.transform.eulerAngles; 
                            }

                            //Debug.Log("in1 =  "+splitPerFrames);


                            if (cam.stereoEnabled)
                            {
                                if (toggleCounter == 2)
                                {
                                    toggleCounter = 3;
                                }
                                else if (toggleCounter == 3)
                                {
                                    toggleCounter = 0;
                                }
                                //Debug.Log(toggleCounter);
                            }
                            else
                            {
                                toggleCounter--;
                            }
                            //if (!enableReproject)
                            //{
                            if (cam.stereoEnabled)
                            {
                                if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                {
                                    CustomGraphicsBlitOpt(source, destination, tmpBufferL, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, true, WebGL, 0);
                                }
                                else
                                {
                                    CustomGraphicsBlitOpt(source, destination, tmpBufferR, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, true, WebGL, 1);
                                }
                            }
                            else
                            {
                                CustomGraphicsBlitOpt(source, destination, tmpBuffer, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, true, WebGL, -1);
                            }

                            //}

                            //////////////v5.0.3
                            if (cam.stereoEnabled)
                            {
                                if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                {
                                    //v5.0.3 - VR reflections
                                    if (SkyManager != null && SkyManager.water != null)
                                    {
                                        PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                        if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                        {
                                            waterR.WaterTileBeingRenderedA(transform, cam, 0);
                                            backgroundCam.nearClipPlane = 1; //MARK LEFT EYE
                                        }
                                    }
                                }
                                else
                                {
                                    //v5.0.3 - VR reflections
                                    if (SkyManager != null && SkyManager.water != null)
                                    {
                                        PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                        if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                        {
                                            waterR.WaterTileBeingRenderedA(transform, cam, 1);
                                            backgroundCam.nearClipPlane = 2; //MARK RIGHT EYE
                                        }
                                    }
                                }
                                backgroundCam.transform.position = Camera.main.transform.position;
                                backgroundCam.worldToCameraMatrix = Camera.main.worldToCameraMatrix;
                                backgroundCam.projectionMatrix = Camera.main.projectionMatrix;
                                backgroundCam.transform.rotation = Camera.main.transform.parent.rotation;
                            }
                            ///////////// END v5.0.3

                        }
                        else
                        {
                            //Graphics.Blit(_cloudBuffer, _cloudBufferP);//v4.8.3


                            if (cam.stereoEnabled)
                            {
                                toggleCounter = toggleCounter + 1;
                                //Debug.Log(toggleCounter);
                            }
                            else
                            {
                                toggleCounter = splitPerFrames;
                            }

                            if (cam.stereoEnabled)
                            {
                                if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                {
                                    RenderTexture.active = tmpBufferL;
                                }
                                else
                                {
                                    RenderTexture.active = tmpBufferR;
                                }
                            }
                            else
                            {
                                RenderTexture.active = tmpBuffer;
                            }
                            GL.ClearWithSkybox(false, cam);
                            if (blendBackground)
                            { //v2.1.20

                                //v4.8
                                if (isForReflections)
                                {
                                    backgroundCam.transform.rotation = cam.transform.rotation;
                                    backgroundCam.transform.position = cam.transform.position;

                                    //https://answers.unity.com/questions/1359718/what-do-the-values-in-the-matrix4x4-for-cameraproj.html                                   

                                    Matrix4x4 projectionMatrix = cam.projectionMatrix;
                                    //projectionMatrix[0, 0] *= -1;
                                    float adjuster = adjustReflPos;
                                    float adjuster2 = adjustReflPos2;
                                    float adjuster3 = adjustReflPos3;

                                    //FIX background rotation -  30 degrees 5 adjuster, -30 -5, 15 2.5
                                    float angle = cam.transform.eulerAngles.x;
                                    if (angle > 180)
                                    {
                                        angle = -(360 - angle);
                                    }
                                    // Debug.Log(angle);
                                    adjuster3 = -angle / 6;

                                    if (backgroundCam.nearClipPlane == 1)
                                    {
                                        adjuster = -adjustReflPos;
                                        adjuster2 = -adjustReflPos2;
                                        //adjuster3= -adjustReflPos3;
                                        adjuster3 = -adjuster3;
                                    }
                                    Matrix4x4 rot = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster, Vector3.up));
                                  //  Matrix4x4 rot2 = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster2, Vector3.forward));
                                    Matrix4x4 rot3 = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster3, Vector3.forward));
                                    //backgroundCam.projectionMatrix = projectionMatrix * rot;
                                    backgroundCam.projectionMatrix = projectionMatrix * rot * rot3;
                                    // backgroundCam.worldToCameraMatrix = cam.cameraToWorldMatrix.inverse;// cam.worldToCameraMatrix;
                                    //backgroundCam.transform.RotateAroundLocal(Vector3.down, adjuster);

                                    Matrix4x4 rotA = Matrix4x4.Rotate(Quaternion.AngleAxis(adjuster2, Vector3.up));
                                    Matrix4x4 viewM = cam.cameraToWorldMatrix * rotA * rot3;
                                    //viewM[0, 0] *= -1;
                                    backgroundCam.worldToCameraMatrix = viewM.inverse;
                                }
                                else
                                {
                                    //v5.0.2
                                    if (cam.stereoEnabled)
                                    {

                                        if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                        {
                                           // Matrix4x4 left_world_from_viewA = left_world_from_view;
                                            // left_world_from_viewA[1, 1] *= -1;
                                            //     backgroundCam.worldToCameraMatrix = left_world_from_viewA; //view matrix
                                           // Matrix4x4 left_screen_from_viewA = left_screen_from_view;
                                            // left_screen_from_viewA[1, 1] *= -1;
                                            //     backgroundCam.projectionMatrix = left_screen_from_viewA;   //projection matrix - right_view_from_screen, left_view_from_screen, not left_screen_from_view
                                            // right_view_from_screen[1, 1] *= -1;


                                            //v5.0.3 - VR reflections
                                            if (SkyManager != null && SkyManager.water != null)
                                            {
                                                PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                                //waterR.RenderHelpCameras(cam);
                                                if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                                {
                                                    waterR.WaterTileBeingRenderedA(transform, cam, 0); //Debug.Log("eye LEFT1");
                                                    backgroundCam.nearClipPlane = 1; //MARK LEFT EYE
                                                }
                                            }
                                            if (!isForReflections)
                                            {
                                                //Debug.Log("eye LEFT1 REFLECTIONS !!!");
                                            }
                                        }
                                        else
                                        {
                                           // Matrix4x4 right_world_from_viewA = right_world_from_view;
                                            // right_world_from_viewA[1, 1] *= -1;
                                            //    backgroundCam.worldToCameraMatrix = right_world_from_viewA; //view matrix
                                            //right_screen_from_view[0, 0] *= -1;
                                          //  Matrix4x4 right_screen_from_viewA = right_screen_from_view;
                                            // right_screen_from_viewA[1, 1] *= -1;
                                            //    backgroundCam.projectionMatrix = right_screen_from_viewA;  //projection matrix

                                            //v5.0.3 - VR reflections
                                            if (SkyManager != null && SkyManager.water != null)
                                            {
                                                PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                                //waterR.RenderHelpCameras(cam);
                                                if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                                {
                                                    waterR.WaterTileBeingRenderedA(transform, cam, 1); //Debug.Log("eye RIGHT1");
                                                    backgroundCam.nearClipPlane = 2; //MARK RIGHT EYE
                                                }
                                            }

                                            if (!isForReflections)
                                            {
                                                //Debug.Log("eye RIGHT1 REFLECTIONS !!!");
                                            }
                                        }

                                        backgroundCam.transform.position = Camera.main.transform.position;
                                        backgroundCam.worldToCameraMatrix = Camera.main.worldToCameraMatrix;
                                        backgroundCam.projectionMatrix = Camera.main.projectionMatrix;
                                        backgroundCam.transform.rotation = Camera.main.transform.parent.rotation;
                                        //backgroundCam.transform.eulerAngles =
                                        //    new Vector3(-backgroundCam.transform.eulerAngles.x, backgroundCam.transform.eulerAngles.y, backgroundCam.transform.eulerAngles.z);
                                        // backgroundCam.transform.eulerAngles =
                                        //    new Vector3(0,0,0);
                                    }
                                    else
                                    {
                                        backgroundCam.transform.rotation = cam.transform.rotation;
                                        backgroundCam.transform.position = cam.transform.position;

                                        //v5.0.4
                                        backgroundCam.worldToCameraMatrix = Camera.main.worldToCameraMatrix;
                                        backgroundCam.projectionMatrix = Camera.main.projectionMatrix;
                                    }
                                }

                                //v2.1.23
                                //float ratio = (float)Screen.height / (float)Screen.width;						
                                //backgroundCam.rect = new Rect (backgroundCam.rect.position, new Vector2 (backgroundCam.rect.size.x, ratio));//v4.1
                                if (!unity2018)
                                {
                                    //backgroundCam.rect = new Rect(backgroundCam.rect.position, new Vector2(backgroundCam.rect.size.x, ratio)); //v4.8
                                }
                                else
                                {
                                    //v4.1f
                                    //backgroundCam.rect = new Rect(backgroundCam.rect.position, new Vector2(1, 1));
                                    //if (backgroundCam.targetTexture != null)
                                    //{
                                    //    backgroundCam.targetTexture.Release();
                                    //}
                                    //backgroundCam.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
                                    if (backgroundCam.targetTexture == null || backgroundCam.targetTexture.width != Screen.width || backgroundCam.targetTexture.height != Screen.height)
                                    {
                                        backgroundCam.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
                                    }
                                }

                                //v4.8
                                if (isForReflections)
                                {
                                    backgroundCam.transform.eulerAngles = new Vector3(backgroundCam.transform.eulerAngles.x, backgroundCam.transform.eulerAngles.y, 180);
                                    //SkyManager.AutoMoonLighting = true;
                                    SkyManager.setMoonShader(backgroundCam.transform, 0.5f);
                                    backgroundCam.Render();

                                    if (cam.stereoEnabled)
                                    {
                                        if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                        {
                                            tmpBufferL.DiscardContents();
                                            Graphics.Blit(backgroundCam.targetTexture, tmpBufferL, fogMaterial, 6);
                                        }
                                        else
                                        {
                                            tmpBufferR.DiscardContents();
                                            Graphics.Blit(backgroundCam.targetTexture, tmpBufferR, fogMaterial, 6);
                                        }
                                    }
                                    else
                                    {
                                        tmpBuffer.DiscardContents();//v4.1f
                                        //Graphics.Blit(backgroundCam.targetTexture, tmpBuffer);
                                        Graphics.Blit(backgroundCam.targetTexture, tmpBuffer, fogMaterial, 6);
                                    }
                                    //SkyManager.AutoMoonLighting = false;
                                    SkyManager.setMoonShader(Camera.main.transform, 1f);
                                }
                                else
                                {
                                    backgroundCam.Render();

                                    if (cam.stereoEnabled)
                                    {
                                        if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                        {
                                            tmpBufferL.DiscardContents();
                                            Graphics.Blit(backgroundCam.targetTexture, tmpBufferL, backgroundMat);
                                        }
                                        else
                                        {
                                            RenderTexture.active = tmpBufferR;
                                            Graphics.Blit(backgroundCam.targetTexture, tmpBufferR, backgroundMat);
                                        }
                                    }
                                    else
                                    {
                                        tmpBuffer.DiscardContents();//v4.1f
                                        Graphics.Blit(backgroundCam.targetTexture, tmpBuffer, backgroundMat);
                                    }
                                }
                            }
                            else if (1 == 1)
                            {
                                //v5.0.2- VR
                                if (cam.stereoEnabled)
                                {
                                    if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                    {
                                        //v5.0.3 - VR reflections
                                        if (SkyManager != null && SkyManager.water != null)
                                        {
                                            PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                            if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                            {
                                                waterR.WaterTileBeingRenderedA(transform, cam, 0); //Debug.Log("eye LEFT1");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //v5.0.3 - VR reflections
                                        if (SkyManager != null && SkyManager.water != null)
                                        {
                                            PlanarReflectionSM waterR = SkyManager.water.GetComponent<PlanarReflectionSM>();
                                            if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main)))
                                            {
                                                waterR.WaterTileBeingRenderedA(transform, cam, 1); //Debug.Log("eye RIGHT1");
                                            }
                                        }
                                    }
                                }

                            }



                            if (cam.stereoEnabled)
                            {
                                if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                                {
                                    CustomGraphicsBlitOpt(source, destination, tmpBufferL, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, 0);
                                    destination.DiscardContents();//v4.1f
                                    tmpBufferL.DiscardContents();
                                    CustomGraphicsBlitOpt(source, destination, tmpBufferL, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, 0);
                                }
                                else
                                {
                                    CustomGraphicsBlitOpt(source, destination, tmpBufferR, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, 1);
                                    destination.DiscardContents();//v4.1f
                                    tmpBufferR.DiscardContents();
                                    CustomGraphicsBlitOpt(source, destination, tmpBufferR, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, 1);
                                }
                            }
                            else
                            {
                                CustomGraphicsBlitOpt(source, destination, tmpBuffer, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, -1);
                                destination.DiscardContents();//v4.1f
                                tmpBuffer.DiscardContents();//v4.1f
                                                            //Graphics.Blit(_cloudBufferP1, destination);
                                CustomGraphicsBlitOpt(source, destination, tmpBuffer, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, -1);
                            }
                        }
                    }

                }
                else
                {
                    CustomGraphicsBlitOpt(source, destination, null, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, true, false, WebGL, -1);
                    CustomGraphicsBlitOpt(source, destination, null, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false, false, WebGL, -1);
                }
                //} else {
                //Graphics.Blit (source, destination);
                //CustomGraphicsBlitOpt (source, destination, fogMaterial, pass, DistGradient, GradientBounds, colourPalette, texture3Dnoise1, texture3Dnoise2, cam, false);
                //}
            }


            //v5.0.1
            //Restore camera state
            if (adjustVR)
            {
                cam.transform.position = prevCenterPos;
            }

        }

        RenderTexture tmpBuffer;//v2.1.15
        RenderTexture tmpBufferL;//v5.0.3
        RenderTexture tmpBufferR;//v5.0.3
        RenderTexture tmpCloudBufferL;//v5.0.3
        RenderTexture tmpCloudBufferR;//v5.0.3

        static void CustomGraphicsBlit(RenderTexture source, RenderTexture dest, Material fxMaterial, int passNr, Gradient DistGradient,
            Vector2 GradientBounds, Texture2D colourPalette, Texture3D tex3D1, Texture3D tex3D2, Camera cam, bool WebGL)
        {
            RenderTexture.active = dest;

            fxMaterial.SetTexture("_MainTex", source);


            //v3.5
            fxMaterial.SetTexture("_NoiseTex1", tex3D1);
            fxMaterial.SetTexture("_NoiseTex2", tex3D2);
            //VFOG

            //v2.1.20
            int signer = 1;
            if (WebGL)
            {
                signer = -1;
            }

            //v4.8
            //if (isForReflections)
            //{
            //}

            //v4.0
            Camera cam1 = cam;
            if (Camera.main != null)
            {
                cam1 = Camera.main;
            }

            fxMaterial.SetMatrix("_WorldClip", cam.cameraToWorldMatrix * Matrix4x4.Scale(new Vector3(1, 1 * signer, -1)) * cam1.projectionMatrix.inverse * Matrix4x4.Scale(new Vector3(-1, 1, 1)));
            //fxMaterial.SetMatrix ("_WorldClip",  cam.cameraToWorldMatrix* cam.projectionMatrix.inverse  ); 


            fxMaterial.SetTexture("_ColorRamp", colourPalette);

            if (GradientBounds != Vector2.zero)
            {
                fxMaterial.SetFloat("_Close", GradientBounds.x);
                fxMaterial.SetFloat("_Far", GradientBounds.y);
            }

            GL.PushMatrix();
            GL.LoadOrtho();

            fxMaterial.SetPass(passNr);

            GL.Begin(GL.QUADS);

            GL.MultiTexCoord2(0, 0.0f, 0.0f);
            GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

            GL.MultiTexCoord2(0, 1.0f, 0.0f);
            GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

            GL.MultiTexCoord2(0, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

            GL.MultiTexCoord2(0, 0.0f, 1.0f);
            GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

            GL.End();
            GL.PopMatrix();
        }





        //float LastUpdateTime; //v4.1e
        public float updateRate = 0.3f;
        public int resolution = 256;
        public int downScaleFactor = 1;
        public int reflectDownScale = 4;//v5.0.3
        public float reduceReflectfarDist = 1;
        public bool downScale = false;

        //K
        RenderTexture CreateBuffer()
        {
            //var width = (_columns + 1) * 2;
            //var height = _totalRows + 1;

            //v5.0.2
            //RenderTexture buffer = new RenderTexture(1280/downScaleFactor, 720/downScaleFactor, 0, RenderTextureFormat.ARGB32); //SM v4.0

            //v5.0.2 - VR
            RenderTextureDescriptor desc;
            if (XRSettings.enabled)
            {
                desc = XRSettings.eyeTextureDesc;
                //Debug.Log("Created render textures for VR with W:" + desc.width + " H:" + desc.height);
                desc.width = 1280 / downScaleFactor;
                desc.height = 720 / downScaleFactor;
                //desc = new RenderTextureDescriptor(1280 / downScaleFactor, 720 / downScaleFactor, RenderTextureFormat.ARGB32, 0);
                //Debug.Log("Created render textures for VR with W1:" + desc.width + " H1:" + desc.height);
            }
            else
            {
                desc = new RenderTextureDescriptor(1280 / downScaleFactor, 720 / downScaleFactor, RenderTextureFormat.ARGB32, 0); // Not XR
                                                                                                                                  // Debug.Log("Created render textures for Non VR");
            }
            RenderTexture buffer = new RenderTexture(desc);


            //buffer.hideFlags = HideFlags.DontSave;
            //buffer.hideFlags = HideFlags.None;
            buffer.filterMode = FilterMode.Bilinear; //FilterMode.Point; //v4.8.8
            buffer.wrapMode = TextureWrapMode.Repeat;
            //	buffer.c
            //	buffer.Create()
            return buffer;
        }

        RenderTexture _cloudBuffer;
        RenderTexture _cloudBufferP;
        RenderTexture _cloudBufferP1;
        //RenderTexture _prevcloudBuffer;
        void ResetResources()
        {
            if (_cloudBuffer) DestroyImmediate(_cloudBuffer);
            _cloudBuffer = CreateBuffer();

            if (_cloudBufferP) DestroyImmediate(_cloudBufferP);
            _cloudBufferP = CreateBuffer();

            if (_cloudBufferP1) DestroyImmediate(_cloudBufferP1);
            _cloudBufferP1 = CreateBuffer();

            //v5.0.3
            if (tmpCloudBufferL) DestroyImmediate(tmpCloudBufferL);
            tmpCloudBufferL = CreateBuffer();
            if (tmpCloudBufferR) DestroyImmediate(tmpCloudBufferR);
            tmpCloudBufferR = CreateBuffer();

            //v4.5
            if (new1) DestroyImmediate(new1);
            new1 = CreateBuffer();

            _needsReset = false;
        }
        void LateUpdate()
        {
            if (_needsReset) ResetResources();
        }
        void Reset()
        {
            _needsReset = true;
        }
        public bool _needsReset = true;

        //v5.0.3
        Vector3 prevCameraRotL;
        Vector3 prevCameraRotR;

        Vector3 prevCameraRot;
        Vector3 prevCameraRotP;//v4.8.3 previous position when frame grabbed

        RenderTexture new1; //v4.5

        //v4.8.3
        int countCameraSteady = 0;
        //v4.8
        public bool enableReproject = false;
        public bool autoReproject = false;

        //void CustomGraphicsBlitOpt (RenderTexture source, RenderTexture dest, Material fxMaterial, int passNr,Gradient DistGradient, Vector2 GradientBounds, Texture2D colourPalette, Texture3D tex3D1, Texture3D tex3D2, Camera cam, bool toggle )
        void CustomGraphicsBlitOpt(RenderTexture source, RenderTexture dest, RenderTexture skysource, Material fxMaterial, int passNr, Gradient DistGradient,
            Vector2 GradientBounds, Texture2D colourPalette, Texture3D tex3D1, Texture3D tex3D2, Camera cam, bool toggle, bool splitPerFrame, bool WebGL, int eye)
        {

            if (!fastest)
            {
                fxMaterial.SetInt("_fastest", 0);
            }
            else
            {
                fxMaterial.SetInt("_fastest", 1);
            }

            if (toggle)
            {

                //v4.8.3
                //Graphics.Blit(_cloudBuffer, _cloudBufferP);
                if (enableReproject || countCameraSteady > 1)
                {
                    Graphics.Blit(new1, _cloudBufferP);
                }

                //v2.1.15
                if (!fastest)
                {
                    fxMaterial.SetTexture("_SkyTex", skysource);
                }

                fxMaterial.SetTexture("_CloudTex", _cloudBuffer);

                //v4.1f
                //RenderTexture.active = _cloudBuffer;
                //RenderTexture new1 = CreateBuffer(); //v4.5
                //v4.5
                if (new1 == null)
                {
                    new1 = CreateBuffer();
                }

                RenderTexture.active = new1;

                //fxMaterial.SetTexture ("_MainTex", source);
                fxMaterial.SetTexture("_MainTex", null);

                //v3.5
                fxMaterial.SetTexture("_NoiseTex1", tex3D1);
                fxMaterial.SetTexture("_NoiseTex2", tex3D2);

                //VFOG (v3.4.9c - put -1 in second 1 for WebGL)
                //v2.1.20
                int signer = 1;
                if (WebGL)
                {
                    signer = -1;
                }
                //v4.0
                Camera cam1 = cam;
                if (Camera.main != null)
                {
                    cam1 = Camera.main;
                }
                Matrix4x4 scaler = cam.cameraToWorldMatrix * Matrix4x4.Scale(new Vector3(1, 1 * signer, -1)) * cam1.projectionMatrix.inverse * Matrix4x4.Scale(new Vector3(-1, 1, 1));//v2.1.19
                                                                                                                                                                                      //scaler[0,3] = scaler[0,3] + 0.1f;
                fxMaterial.SetMatrix("_WorldClip", scaler);
                //fxMaterial.SetMatrix ("_WorldClip",  cam.cameraToWorldMatrix* cam.projectionMatrix.inverse  ); 


                fxMaterial.SetTexture("_ColorRamp", colourPalette);

                if (GradientBounds != Vector2.zero)
                {
                    fxMaterial.SetFloat("_Close", GradientBounds.x);
                    fxMaterial.SetFloat("_Far", GradientBounds.y);
                }




                GL.PushMatrix();
                GL.LoadOrtho();

                fxMaterial.SetPass(passNr);

                GL.Begin(GL.QUADS);

                GL.MultiTexCoord2(0, 0.0f, 0.0f);
                GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

                GL.MultiTexCoord2(0, 1.0f, 0.0f);
                GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

                GL.MultiTexCoord2(0, 1.0f, 1.0f);
                GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

                GL.MultiTexCoord2(0, 0.0f, 1.0f);
                GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

                GL.End();
                GL.PopMatrix();

                // _cloudBuffer = new1;// RenderTexture.active; //v4.1f //v4.8.3
                //_prevcloudBuffer = RenderTexture.active;

                Graphics.Blit(new1, _cloudBuffer);//v4.8.3

                //v5.0.3
                if(eye == 0)
                {
                    Graphics.Blit(new1, tmpCloudBufferL);
                }
                if (eye == 1)
                {
                    Graphics.Blit(new1, tmpCloudBufferR);
                }

                prevCameraRotP = cam.transform.eulerAngles;
            }
            else
            {
                fxMaterial.SetTexture("_MainTex", source);
                if (!fastest)
                {
                    fxMaterial.SetTexture("_SkyTex", skysource);//v2.1.15
                }

                if (splitPerFrames > 0 && enableReproject && Application.isPlaying)
                {
                    if (splitPerFrame)
                    {
                        fxMaterial.SetTexture("_CloudTex", _cloudBufferP1);
                    }
                    else
                    {
                        //v4.8.3
                        //Graphics.Blit(_cloudBuffer, _cloudBufferP);
                        if (autoReproject)
                        {
                            if (autoReproject && enableReproject && countCameraSteady > 2)
                            {
                                fxMaterial.SetTexture("_CloudTex", _cloudBufferP1);
                            }
                            else
                            {
                                fxMaterial.SetTexture("_CloudTex", _cloudBuffer);
                            }
                        }
                        else
                        {
                            fxMaterial.SetTexture("_CloudTex", _cloudBufferP1);
                        }
                    }
                }
                else
                {
                    fxMaterial.SetTexture("_CloudTex", _cloudBuffer); //v4.8.3

                    //v5.0.3
                    if (eye == 0)
                    {
                        fxMaterial.SetTexture("_CloudTex", tmpCloudBufferL);
                    }
                    if (eye == 1)
                    {
                        fxMaterial.SetTexture("_CloudTex", tmpCloudBufferR);
                    }
                }
                //fxMaterial.SetTexture("_CloudTex", _cloudBuffer); //v4.8.3

                RenderTexture.active = dest;

                //v2.1.19
                //v2.1.20
                int signer = 1;
                if (WebGL)
                {
                    signer = -1;
                }
                //v4.0
                Camera cam1 = cam;
                if (Camera.main != null)
                {
                    cam1 = Camera.main;
                }
                Matrix4x4 scaler = cam.cameraToWorldMatrix * Matrix4x4.Scale(new Vector3(2, 1 * signer, -1)) * cam1.projectionMatrix.inverse * Matrix4x4.Scale(new Vector3(-1, 1, 1));//v2.1.19



                //if (splitPerFrames > 0 && toggleCounter != splitPerFrames) {
                if (splitPerFrame)
                {

                    float Xdisp = cam.transform.eulerAngles.y - prevCameraRot.y;
                    float Ydisp = -(cam.transform.eulerAngles.x - prevCameraRot.x);
                    
                    //v5.0.3
                    if (eye == 0)
                    {
                        Xdisp = cam.transform.eulerAngles.y - prevCameraRotL.y;
                        Ydisp = -(cam.transform.eulerAngles.x - prevCameraRotL.x);
                    }
                    if (eye == 1)
                    {
                        Xdisp = cam.transform.eulerAngles.y - prevCameraRotR.y;
                        Ydisp = -(cam.transform.eulerAngles.x - prevCameraRotR.x);
                    }

                    //v4.8.3
                    if (autoReproject)
                    {
                        if (Mathf.Abs(Xdisp) == 0.000000f && Mathf.Abs(Ydisp) == 0.000000f)
                        {
                            if (countCameraSteady > 8)
                            {
                                enableReproject = true;
                                //cameraMotionCompensate = false;
                            }
                            else { countCameraSteady++; }
                        }
                        else
                        {
                            //float frameFraction = (float)toggleCounter / (float)splitPerFrames;
                            //if (toggleCounter == splitPerFrames-1 && countCameraSteady > 0)
                            if (toggleCounter == 1 && countCameraSteady > 0)
                            {
                                //Graphics.Blit(_cloudBufferP, _cloudBuffer);//v4.8.3
                                //Graphics.Blit(new1, _cloudBufferP);//v4.8.3
                                //Graphics.Blit(_cloudBufferP, _cloudBufferP1);//v4.8.3
                                //Graphics.Blit(_cloudBufferP1, _cloudBuffer);
                                enableReproject = false;
                                //cameraMotionCompensate = true;
                                countCameraSteady = 0;
                                //return;
                            }
                        }
                    }

                    if (Xdisp > 122f || Xdisp < -122f)
                    {
                        //Debug.Log("Xdisp = " + Xdisp);
                        //Debug.Log("tmpBuffer width=" + tmpBuffer.width);
                        // Debug.Log("tmpBuffer height=" + tmpBuffer.height);
                        Xdisp = 0;
                    }
                    if (Ydisp > 122f || Ydisp < -122f)
                    {
                        //Debug.Log("Ydisp = " + Ydisp);
                        Ydisp = 0;
                    }

                    //Debug.Log("in " + (cam.transform.eulerAngles.x - prevCameraRot.x).ToString());
                    //Debug.Log("inA " + (cam.transform.eulerAngles.y - prevCameraRot.y).ToString());
                    //scaler[0, 3] = scaler[0, 3] - 1110.71f * (splitPerFrames - toggleCounter);//move in X, depending on camera motion and frames skipped
                    scaler[0, 3] = 0.009f * Xdisp;//0.005f * Xdisp;
                    scaler[1, 3] = 0.012f * Ydisp;
                    scaler[2, 3] = 1;// 1f - 0.005f * (Xdisp+Ydisp); // 0.95f+ 0.005f*(splitPerFrames - toggleCounter); //image scaler

                    //v4.8.3
                    //fxMaterial.SetTexture("_CloudTexP", _cloudBufferP);
                    //float frameFraction = (float)toggleCounter / (float)splitPerFrames;
                    //fxMaterial.SetFloat("frameFraction", frameFraction);
                    // Graphics.Blit(new1, _cloudBuffer, fxMaterial,5);//v4.8.3
                    // Graphics.Blit(new1, _cloudBuffer);//v4.8.3
                    //fxMaterial.SetTexture("_CloudTex", _cloudBuffer);

                    //v4.8.3
                    //fxMaterial.SetTexture("_CloudTexP", _cloudBufferP);
                    //fxMaterial.SetTexture("_CloudTex", _cloudBufferP);
                    //float frameFraction = (float)toggleCounter / (float)splitPerFrames;
                    //fxMaterial.SetFloat("frameFraction", frameFraction);
                    //Graphics.Blit(_cloudBufferP, _cloudBuffer, fxMaterial, 5);//v4.8.3
                }
                else
                {
                    prevCameraRot = cam.transform.eulerAngles;

                    //v5.0.3
                    if (eye == 0)
                    {
                        prevCameraRotL = cam.transform.eulerAngles;
                        //Debug.Log("Left eye =" + prevCameraRotL);
                        //Debug.Log("Left eye M =" + cam.projectionMatrix);
                    }
                    if (eye == 1)
                    {
                        prevCameraRotR = cam.transform.eulerAngles;
                        //Debug.Log("Right eye =" + prevCameraRotR);
                        //Debug.Log("Right eye M =" + cam.projectionMatrix);
                    }

                }
                fxMaterial.SetMatrix("_WorldClip", scaler);

                GL.PushMatrix();
                GL.LoadOrtho();

                if (splitPerFrame && cameraMotionCompensate)
                {
                    fxMaterial.SetPass(4);
                }
                else
                {
                    fxMaterial.SetPass(3);
                }

                GL.Begin(GL.QUADS);

                GL.MultiTexCoord2(0, 0.0f, 0.0f);
                GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

                GL.MultiTexCoord2(0, 1.0f, 0.0f);
                GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

                GL.MultiTexCoord2(0, 1.0f, 1.0f);
                GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

                GL.MultiTexCoord2(0, 0.0f, 1.0f);
                GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

                GL.End();
                GL.PopMatrix();

            }

        }



    }
}

//Part of the code is based on MIT licensed code 
//MIT License
//Copyright(c) 2016 Unity Technologies
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files 
//(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
//publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
// subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
//MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
//ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
//THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.