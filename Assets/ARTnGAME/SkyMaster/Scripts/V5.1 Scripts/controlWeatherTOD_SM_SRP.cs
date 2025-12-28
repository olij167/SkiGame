using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Artngame.SKYMASTER;
//using ARTNGAME.Skymaster;
//using BrunetonsAtmosphere;

//NOTES
//https://forum.unity.com/threads/hdrp-material-sorting-priority-c-scripting.1034344/ - Cloud sorting priority
//material.renderQueue = 3000 + sortingPriority;
//    material.SetFloat("_TransparentSortPriority", sortingPriority);
//https://www.turiyaware.com/blog/unity-ui-blur-in-hdrp - HDRP blur

namespace Artngame.SKYMASTER
{
    [ExecuteInEditMode]
    public class controlWeatherTOD_SM_SRP : MonoBehaviour
    {
        [Header("------------------------------------------------------")]
        [Header("Script Setup - Lightning Control")]
        [Header("------------------------------------------------------")]
        public bool activateGUI = true;
        public bool enableGUI = false;

        //v0.2
        public Transform water;
        //public PlanarRefractionsSM_LWRP refraction;
        public PlanarReflectionSM reflection; // PlanarReflectionsSM_LWRP reflection;
        public TerrainDepthSM depthRenderer; //DepthRendererSM_LWRP depthRenderer;

        //v0.1
        public CloudHandlerSM shaderVolClouds; //CloudHandlerSM_SRP shaderVolClouds;

        // public Sky skyDome;
        public bool forceLightning = false;
        public lightningCameraVolumeCloudsSM_SRP lightningController;// lightningCameraVolumeCloudsSM_URP lightningController;

        //If sky manager exsit, control weather and TOD
        public SkyMasterManager skyManager;

        //Control TOD coloratino for full volume clouds
        public CloudScript fullVolumeClouds; // connectSuntoFullVolumeCloudsURP fullVolumeClouds;
        public CloudScript fullVolumeCloudsREFL;

        public GlobalFogSkyMaster etherealVolFog; // connectSuntoVolumeFogURP etherealVolFog;

        //Control water TODparams
        public WaterHandlerSM waterManager;

        [Header("------------------------------------------------------")]
        [Header("Cloud Density - Color Controls")]
        [Header("------------------------------------------------------")]
        //v0.3
        public bool affectCloudDensity = true;
        public bool affectCloudColor = true;
        public bool affectCloudTopColor = false; //v0.4

        //choose density per weather type
        public List<float> weatherCloudDensities = new List<float>();
        public float densitiesOffset = 0;
        public float densityThicknessOffset = 0;

        public bool ApplyPresetDensities = true;
        public int cloudType = 0;
        //0 = full volume clouds, 1=background volume clouds, 2=dome background vol clouds, 3= InfiniCLOUD shader volume clouds 

        // Start is called before the first frame update
        void Start()
        {
            if (temporalSunMoon == null)//if (Application.isPlaying && temporalSunMoon == null)
            {
                temporalSunMoon = new GameObject();
                temporalSunMoon.name = "SunMoonTransformLerp";
                temporalSunMoon.transform.rotation = skyManager.SUN_LIGHT.transform.rotation;
                temporalSunMoon.transform.position = skyManager.SUN_LIGHT.transform.position;
                Light temp = temporalSunMoon.AddComponent<Light>();
                temp = fullVolumeClouds.sunLight;
                fullVolumeClouds.sunLight = temporalSunMoon.GetComponent<Light>();
                if(fullVolumeCloudsREFL != null)
                {
                    fullVolumeCloudsREFL.sunLight = temporalSunMoon.GetComponent<Light>();
                }
            }
            else
            {
                temporalSunMoon.transform.rotation = skyManager.SUN_LIGHT.transform.rotation;
                temporalSunMoon.transform.position = skyManager.SUN_LIGHT.transform.position;
            }

            //set mooon based on time of day - X = vertical (minus goes up, 180 to 360), Y = horizontal
            //      Vector4 moonSettings = skyDome.m_skyMaterial.GetVector("_moonPos");
            //       skyDome.m_skyMaterial.SetVector("_moonPos", new Vector4(270 + Mathf.Cos(skyManager.Current_Time * 2) * 90,180+Mathf.Cos(Time.fixedTime*2)*180, moonSettings.z, moonSettings.w));

            //set cloud density at game start based on weather
            if (affectCloudDensity && fullVolumeClouds != null)
            {
                setFullVolumeCloudsDensity(false);
                //setFullVolumeCloudsColors();
                if (fullVolumeCloudsREFL != null)
                {
                    fullVolumeCloudsREFL.density = fullVolumeClouds.density;
                    fullVolumeCloudsREFL.coverage = fullVolumeClouds.coverage;

                    fullVolumeCloudsREFL.scale = fullVolumeClouds.scale;
                    fullVolumeCloudsREFL.detailScale = fullVolumeClouds.detailScale;
                    fullVolumeCloudsREFL.lowFreqMin = fullVolumeClouds.lowFreqMin;
                    fullVolumeCloudsREFL.lowFreqMax = fullVolumeClouds.lowFreqMax;
                    fullVolumeCloudsREFL.highFreqModifier = fullVolumeClouds.highFreqModifier;
                    fullVolumeCloudsREFL.weatherScale = fullVolumeClouds.weatherScale;
                    fullVolumeCloudsREFL.startHeight = fullVolumeClouds.startHeight;
                    fullVolumeCloudsREFL.thickness = fullVolumeClouds.thickness;
                    fullVolumeCloudsREFL.planetSize = fullVolumeClouds.planetSize;
                    fullVolumeCloudsREFL.cloudSampleMultiplier = fullVolumeClouds.cloudSampleMultiplier;
                    fullVolumeCloudsREFL.globalMultiplier = fullVolumeClouds.globalMultiplier;
                    fullVolumeCloudsREFL.windSpeed = fullVolumeClouds.windSpeed;
                    fullVolumeCloudsREFL.windDirection = fullVolumeClouds.windDirection;
                    fullVolumeCloudsREFL.coverageWindSpeed = fullVolumeClouds.coverageWindSpeed;
                    fullVolumeCloudsREFL.coverageWindDirection = fullVolumeClouds.coverageWindDirection;
                }
            }
            if (affectCloudColor && fullVolumeClouds != null)
            {
                //setFullVolumeCloudsDensity(false);
                setFullVolumeCloudsColors();
                 if (fullVolumeCloudsREFL != null)
                {
                    fullVolumeCloudsREFL.highSunColor = fullVolumeClouds.highSunColor;
                    fullVolumeCloudsREFL.cloudBaseColor = fullVolumeClouds.cloudBaseColor;
                    fullVolumeCloudsREFL._SkyTint = fullVolumeClouds._SkyTint;

                    if (affectCloudTopColor) //v0.4
                    {
                        fullVolumeCloudsREFL.cloudTopColor = fullVolumeClouds.cloudTopColor;
                    }
                }
            }


            //URP
            //v3.3 - init galaxy
            //v3.3 - shader based stars fade
            if (skyManager.skyboxMat != null)
            {
                //Color StarsCol = StarsMaterial.GetColor ("_Color");
                if (
                    (!skyManager.AutoSunPosition && (skyManager.Current_Time >= (9 + skyManager.Shift_dawn) & skyManager.Current_Time <= (skyManager.NightTimeMax + skyManager.Shift_dawn)))
                    |
                    (skyManager.AutoSunPosition && skyManager.Rot_Sun_X > 0))
                {
                    if (skyManager.Current_Time < 1f)
                    { //initialize at game start
                        skyManager.skyboxMat.SetColor("_Color", new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.y));
                    }
                    else
                    {
                        skyManager.skyboxMat.SetColor("_Color", new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.y));
                    }
                }
                else
                {
                    if (skyManager.Current_Time < 1f)
                    { //initialize at game start
                        skyManager.skyboxMat.SetColor("_Color", new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.x));
                    }
                    else
                    {
                        skyManager.skyboxMat.SetColor("_Color", new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.x));
                    }
                }
                //float StarsCover = StarsMaterial.GetFloat ("_Light");
                skyManager.skyboxMat.SetFloat("_Light", skyManager.StarsIntensity);
            }


            //v0.3 - Apply GUI
            if (activateGUI && enableGUI && Application.isPlaying)
            {
                //URP
                /*
                volumeLightPower = etherealVolFog.blendVolumeLighting;
                volumeFogColor = etherealVolFog._FogColor;
                lightNoiseControl = etherealVolFog.lightNoiseControl;
                volumeFogNoise = etherealVolFog.noiseThickness;
                volumeFogHeight = etherealVolFog._fogHeight;
                volumeFogDensity = etherealVolFog._fogDensity;
                heigthNoiseDensities.x = etherealVolFog.heightDensity;
                heigthNoiseDensities.y = etherealVolFog.noiseDensity;
                volumeFogNoisePower = etherealVolFog.stepsControl.w;
                volumeNoiseScale = etherealVolFog.noiseScale; //etherealVolFog.noiseScale = volumeNoiseScale; //v0.3a
                noiseSpeed = etherealVolFog.noiseSpeed.x;
                */
                //clearskyFactor = etherealVolFog.ClearSkyFac;
            }

        }

        //Lighting
        public GameObject temporalSunMoon;

        //v0.3
        public void OnGUI()
        {
            if (!activateGUI)
            {
                return;
            }

            if (GUI.Button(new Rect(10 + 0 * 100, 10 + 1 * 30, 100, 30), "GUI Toggle"))
            {
                if (enableGUI)
                {
                    enableGUI = false;
                }
                else
                {
                    enableGUI = true;
                }
            }
            if (enableGUI && Application.isPlaying)
            {
                if (GUI.Button(new Rect(10 + 0 * 100, 10 + 0 * 30, 100, 30), "Dust Storm"))
                {
                    volumeLightPower = 0.65f;
                    volumeFogNoise = 0.1f;
                    volumeFogHeight = 60;
                    volumeFogColor = new Color(67.0f / 255.0f, 34.0f / 255.0f, 17.0f / 255.0f); //Color.red * 0.5f + Color.yellow * 0.25f;
                    lightNoiseControl = new Vector4(0.7f, 0, 0.5f, 6);
                    volumeFogDensity = 0.3f;
                    heigthNoiseDensities = new Vector2(15, 0.5f);
                    volumeFogNoisePower = 0.25f;
                    volumeNoiseScale = 0.002f;
                    noiseSpeed = 100;
                    clearskyFactor = 0.07f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 1 * 100, 10 + 0 * 30, 100, 30), "Dust Light"))
                {
                    volumeLightPower = 0.01f;
                    volumeFogNoise = 0.12f; //noiseThickness
                    volumeFogHeight = 105;
                    volumeFogColor = new Color(248.0f / 255.0f, 117.0f / 255.0f, 50.0f / 255.0f); //Color.white * 0.12f;
                    lightNoiseControl = new Vector4(0.7f, 0, 0.5f, 6);
                    volumeFogDensity = 0.056f; //_fogDensity
                    heigthNoiseDensities = new Vector2(8f, 1.0f);
                    volumeFogNoisePower = 0.55f; //stepsControl.w 
                    volumeNoiseScale = 0.18f;//noiseScale
                    noiseSpeed = 40;
                    clearskyFactor = 0.07f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 2 * 100, 10 + 0 * 30, 100, 30), "Heavy Fog"))
                {
                    volumeLightPower = 1.05f;
                    volumeFogNoise = 0.1f;
                    volumeFogHeight = 60;
                    volumeFogColor = Color.white * 0.15f;
                    lightNoiseControl = new Vector4(0.75f, 0, 0.5f, 3);
                    volumeFogDensity = 0.3f;
                    heigthNoiseDensities = new Vector2(15, 0.5f);
                    volumeFogNoisePower = 0.25f; volumeNoiseScale = 0.002f;
                    noiseSpeed = 100;
                    clearskyFactor = 0.07f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 3 * 100, 10 + 0 * 30, 100, 30), "Light Fog"))
                {
                    volumeLightPower = 1.05f;
                    volumeFogNoise = 0.1f;
                    volumeFogHeight = 60;
                    volumeFogColor = Color.white * 0.55f;
                    lightNoiseControl = new Vector4(0.75f, 0, 0.5f, 3);
                    volumeFogDensity = 0.12f;
                    heigthNoiseDensities = new Vector2(15, 0.5f);
                    volumeFogNoisePower = 0.55f; volumeNoiseScale = 0.15f;
                    noiseSpeed = 70;
                    clearskyFactor = 0.07f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 4 * 100, 10 + 0 * 30, 100, 30), "Snow Fog"))
                {
                    volumeLightPower = 0.05f;
                    volumeFogNoise = 0.1f;
                    volumeFogHeight = 460;
                    volumeFogColor = new Color(148.0f / 255.0f, 148.0f / 255.0f, 148.0f / 255.0f); //Color.white * 0.12f;
                    lightNoiseControl = new Vector4(1.0f, 0, 0.5f, 3);
                    volumeFogDensity = 0.01f;
                    heigthNoiseDensities = new Vector2(5.5f, 1.35f);
                    volumeFogNoisePower = 14f; volumeNoiseScale = 0.01f;
                    noiseSpeed = 100;
                    clearskyFactor = 0.07f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 5 * 100, 10 + 0 * 30, 100, 30), "Snow Heavy"))
                {
                    volumeLightPower = 0.05f;
                    volumeFogNoise = 0.1f; //noiseThickness
                    volumeFogHeight = 620;
                    volumeFogColor = new Color(250.0f / 255.0f, 250.0f / 255.0f, 250.0f / 255.0f); //Color.white * 0.12f;
                    lightNoiseControl = new Vector4(1.0f, -12, 0.35f, 2);
                    volumeFogDensity = 0.015f; //_fogDensity
                    heigthNoiseDensities = new Vector2(5.5f, 1.35f);
                    volumeFogNoisePower = 14f; //stepsControl.w 
                    volumeNoiseScale = 0.18f;//noiseScale
                    noiseSpeed = 30;
                    clearskyFactor = 0.07f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 6 * 100, 10 + 0 * 30, 100, 30), "Sun Shafts"))
                {
                    volumeLightPower = 0.75f;
                    volumeFogNoise = 0.01f;
                    volumeFogHeight = 0;
                    volumeFogColor = new Color(29.0f / 255.0f, 29.0f / 255.0f, 29.0f / 255.0f); //Color.white * 0.05f;
                    lightNoiseControl = new Vector4(0.75f, 0, 0.5f, 3);
                    volumeFogDensity = 0.01f;
                    heigthNoiseDensities = new Vector2(0, 0);
                    volumeFogNoisePower = 0.05f;
                    volumeNoiseScale = 0;
                    noiseSpeed = 0;
                    clearskyFactor = 0.43f;
                    water.gameObject.SetActive(false);
                    //refraction.enabled = false;
                    reflection.enabled = false;
                    depthRenderer.enabled = false;
                }
                if (GUI.Button(new Rect(10 + 7 * 100, 10 + 0 * 30, 100, 30), "Clear Fog"))
                {
                    volumeLightPower = 0.5f;
                    volumeFogNoise = 0.01f;
                    volumeFogHeight = 0;
                    volumeFogColor = new Color(29.0f / 255.0f, 29.0f / 255.0f, 29.0f / 255.0f); //Color.white * 0.05f;
                    lightNoiseControl = new Vector4(0.75f, 0, 0.5f, 3);
                    volumeFogDensity = 0.01f;
                    heigthNoiseDensities = new Vector2(0, 0);
                    volumeFogNoisePower = 0.05f;
                    volumeNoiseScale = 0;
                    noiseSpeed = 0;
                    clearskyFactor = 0.43f;
                    water.gameObject.SetActive(true);
                    //refraction.enabled = true;
                    reflection.enabled = true;
                    depthRenderer.enabled = true;
                }



                if (GUI.Button(new Rect(10 + 0 * 100, 10 + 2 * 30, 100, 30), "Sunny"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Sunny;
                    skyManager.WeatherSeverity = 1;
                }
                if (GUI.Button(new Rect(10 + 1 * 100, 10 + 2 * 30, 100, 30), "Cloudy"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Cloudy;
                    skyManager.WeatherSeverity = 1;
                }
                if (GUI.Button(new Rect(10 + 2 * 100, 10 + 2 * 30, 100, 30), "Light Rain"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Rain;
                    skyManager.WeatherSeverity = 10;
                }
                if (GUI.Button(new Rect(10 + 3 * 100, 10 + 2 * 30, 100, 30), "Heavy Rain"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Rain;
                    skyManager.WeatherSeverity = 20;
                }
                if (GUI.Button(new Rect(10 + 4 * 100, 10 + 2 * 30, 100, 30), "Light Snow"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.SnowStorm;
                    skyManager.WeatherSeverity = 10;
                }
                if (GUI.Button(new Rect(10 + 5 * 100, 10 + 2 * 30, 100, 30), "Heavy Snow"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.SnowStorm;
                    skyManager.WeatherSeverity = 20;
                }
                if (GUI.Button(new Rect(10 + 6 * 100, 10 + 2 * 30, 100, 30), "Lightning"))
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.LightningStorm;
                    skyManager.WeatherSeverity = 20;
                }

                //volumeLightPower = 1;
                //public float volumeFogPower = 1;
                // public float volumeFogNoise = 0;
                //public Color volumeFogColor = Color.white * 0.15f;
            }
        }

      
        

        private void OnEnable()
        {
            //v0.4          
           
            //if (advancedSkyOffsets)
            //{
            skyPreset = skyManager.Preset;
                fExposureOffset = skyManager.fExposureOffset;
                fWaveLengthOffset = skyManager.fWaveLengthOffset;
                gOffset = skyManager.gOffset;
                scaleDifOffset = skyManager.scaleDifOffset;
                tintColorOffset = skyManager.tintColorOffset;
                sunRingFactorOffset = skyManager.sunRingFactorOffset;
                Sun_halo_factor = skyManager.Sun_halo_factor;
                Sun_eclipse_factor = skyManager.Sun_eclipse_factor;
                Glob_scale = skyManager.Glob_scale;
            //}
            Esun = skyManager.m_ESun;
            Kr = skyManager.m_Kr;
            Km = skyManager.m_Km;
            fsamples = skyManager.m_fSamples;
            fRayleightScaleDepth = skyManager.m_fRayleighScaleDepth;
            coloration = skyManager.m_Coloration;
        }


        // Update is called once per frame
        void Update()
        {

            //v0.4
            if (passSettingsToEclipse)
            {
                fExposureOffsetS = skyManager.fExposureOffset;
                fWaveLengthOffsetS = skyManager.fWaveLengthOffset;
                gOffsetS = skyManager.gOffset;
                scaleDifOffsetS = skyManager.scaleDifOffset;
                tintColorOffsetS = skyManager.tintColorOffset;
                sunRingFactorOffsetS = skyManager.sunRingFactorOffset;
                Sun_halo_factorS = skyManager.Sun_halo_factor;
                Sun_eclipse_factorS = skyManager.Sun_eclipse_factor;
                Glob_scaleS = skyManager.Glob_scale;
                passSettingsToEclipse = false;
            }

            if (useEclipseSystem && (Application.isPlaying || eclipseInEditor))
            {
                if (!eclipseByAngle)
                {
                    if (activateEclipse)
                    {
                        if (!eclipseActivated)
                        {
                            eclipseActivated = true;
                        }
                        //lerp settings
                        if (skyManager.fExposureOffset != fExposureOffsetS)
                        {
                            skyManager.fExposureOffset = Mathf.Lerp(skyManager.fExposureOffset, fExposureOffsetS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.fWaveLengthOffset != fWaveLengthOffsetS)
                        {
                            skyManager.fWaveLengthOffset = Vector3.Lerp(skyManager.fWaveLengthOffset, fWaveLengthOffsetS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.gOffset != gOffsetS)
                        {
                            skyManager.gOffset = Mathf.Lerp(skyManager.gOffset, gOffsetS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.scaleDifOffset != scaleDifOffsetS)
                        {
                            skyManager.scaleDifOffset = Mathf.Lerp(skyManager.scaleDifOffset, scaleDifOffsetS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.tintColorOffset != tintColorOffsetS)
                        {
                            skyManager.tintColorOffset = Vector3.Lerp(skyManager.tintColorOffset, tintColorOffsetS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.sunRingFactorOffset != sunRingFactorOffsetS)
                        {
                            skyManager.sunRingFactorOffset = Mathf.Lerp(skyManager.sunRingFactorOffset, sunRingFactorOffsetS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.Sun_halo_factor != Sun_halo_factorS)
                        {
                            skyManager.Sun_halo_factor = Mathf.Lerp(skyManager.Sun_halo_factor, Sun_halo_factorS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.Sun_eclipse_factor != Sun_eclipse_factorS)
                        {
                            skyManager.Sun_eclipse_factor = Mathf.Lerp(skyManager.Sun_eclipse_factor, Sun_eclipse_factorS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.Glob_scale != Glob_scaleS)
                        {
                            skyManager.Glob_scale = Mathf.Lerp(skyManager.Glob_scale, Glob_scaleS, Time.deltaTime * 2 * eclipseSpeed);
                        }
                    }
                    else
                    {
                        //go back
                        if (skyManager.fExposureOffset != fExposureOffset)
                        {
                            skyManager.fExposureOffset = Mathf.Lerp(skyManager.fExposureOffset, fExposureOffset, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.fWaveLengthOffset != fWaveLengthOffset)
                        {
                            skyManager.fWaveLengthOffset = Vector3.Lerp(skyManager.fWaveLengthOffset, fWaveLengthOffset, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.gOffset != gOffset)
                        {
                            skyManager.gOffset = Mathf.Lerp(skyManager.gOffset, gOffset, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.scaleDifOffset != scaleDifOffset)
                        {
                            skyManager.scaleDifOffset = Mathf.Lerp(skyManager.scaleDifOffset, scaleDifOffset, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.tintColorOffset != tintColorOffset)
                        {
                            skyManager.tintColorOffset = Vector3.Lerp(skyManager.tintColorOffset, tintColorOffset, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.sunRingFactorOffset != sunRingFactorOffset)
                        {
                            skyManager.sunRingFactorOffset = Mathf.Lerp(skyManager.sunRingFactorOffset, sunRingFactorOffset, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.Sun_halo_factor != Sun_halo_factor)
                        {
                            skyManager.Sun_halo_factor = Mathf.Lerp(skyManager.Sun_halo_factor, Sun_halo_factor, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.Sun_eclipse_factor != Sun_eclipse_factor)
                        {
                            skyManager.Sun_eclipse_factor = Mathf.Lerp(skyManager.Sun_eclipse_factor, Sun_eclipse_factor, Time.deltaTime * 2 * eclipseSpeed);
                        }
                        if (skyManager.Glob_scale != Glob_scale)
                        {
                            skyManager.Glob_scale = Mathf.Lerp(skyManager.Glob_scale, Glob_scale, Time.deltaTime * 2 * eclipseSpeed);
                        }
                    }
                }
                else
                {
                    //by angle - 0.965 to 0.999, lerp 0 to 1
                    float angleMoonSun = Vector3.Dot((skyManager.MoonObj.transform.position - Camera.main.transform.position).normalized,
                        (skyManager.SUN_LIGHT.transform.forward).normalized);
                    float percent = (Mathf.Abs(angleMoonSun) - 0.965f) / (0.9999f - 0.965f);
                    //if (skyManager.fExposureOffset != fExposureOffset)
                    {
                        skyManager.fExposureOffset = Mathf.Lerp(fExposureOffset, fExposureOffsetS, percent);
                    }
                    //if (skyManager.fWaveLengthOffset != fWaveLengthOffset)
                    {
                        skyManager.fWaveLengthOffset = Vector3.Lerp(fWaveLengthOffset, fWaveLengthOffsetS, percent);
                    }
                    //if (skyManager.gOffset != gOffset)
                    {
                        skyManager.gOffset = Mathf.Lerp(gOffset, gOffsetS, percent);
                    }
                    //if (skyManager.scaleDifOffset != scaleDifOffset)
                    {
                        skyManager.scaleDifOffset = Mathf.Lerp(scaleDifOffset, scaleDifOffsetS, percent);
                    }
                    //if (skyManager.tintColorOffset != tintColorOffset)
                    {
                        skyManager.tintColorOffset = Vector3.Lerp(tintColorOffset, tintColorOffsetS, percent);
                    }
                    //if (skyManager.sunRingFactorOffset != sunRingFactorOffset)
                    {
                        skyManager.sunRingFactorOffset = Mathf.Lerp(sunRingFactorOffset, sunRingFactorOffsetS, percent);
                    }
                    //if (skyManager.Sun_halo_factor != Sun_halo_factor)
                    {
                        skyManager.Sun_halo_factor = Mathf.Lerp(Sun_halo_factor, Sun_halo_factorS, percent);
                    }
                    //if (skyManager.Sun_eclipse_factor != Sun_eclipse_factor)
                    {
                        skyManager.Sun_eclipse_factor = Mathf.Lerp(Sun_eclipse_factor, Sun_eclipse_factorS, percent);
                    }
                    //if (skyManager.Glob_scale != Glob_scale)
                    {
                        skyManager.Glob_scale = Mathf.Lerp(Glob_scale, Glob_scaleS, percent);
                    }
                    //Debug.Log(angleMoonSun);
                }
            }

            skyManager.Preset = skyPreset;
            if (advancedSkyOffsets && !eclipseByAngle && !activateEclipse && (!useEclipseSystem || (useEclipseSystem && !Application.isPlaying && !eclipseInEditor) )   )
            {                
                skyManager.fExposureOffset = fExposureOffset;
                skyManager.fWaveLengthOffset = fWaveLengthOffset;
                skyManager.gOffset = gOffset;
                skyManager.scaleDifOffset = scaleDifOffset;
                skyManager.tintColorOffset = tintColorOffset;
                skyManager.sunRingFactorOffset = sunRingFactorOffset;
                skyManager.Sun_halo_factor = Sun_halo_factor;
                skyManager.Sun_eclipse_factor = Sun_eclipse_factor;
                skyManager.Glob_scale = Glob_scale;
            }
            if (advancedSkyOptions)
            {
                 skyManager.m_ESun = Esun;
                 skyManager.m_Kr = Kr;
                 skyManager.m_Km = Km;
                 skyManager.m_fSamples = fsamples;
                 skyManager.m_fRayleighScaleDepth = fRayleightScaleDepth;
                 skyManager.m_Coloration = coloration;
            }
            if (autoMoonPhase)
            {
                skyManager.AutoMoonLighting = true;
            }
            else
            {
                skyManager.AutoMoonLighting = false;
                if (!activateEclipse)
                {
                    //if (controlMoonPhase && MoonA != null)
                    //{
                    //    MoonA.SetVector("_SunDir", moonLightDirection);
                    //    MoonA.SetColor("_Color", MoonSunLight);
                    //    MoonA.SetColor("_Ambient", MoonAmbientLight);
                    //}
                    //if (controlMoonPhase && MoonB != null)
                    //{
                    //    MoonB.SetVector("_SunDir", moonLightDirection);
                    //    MoonB.SetColor("_Color", MoonSunLight);
                    //    MoonB.SetColor("_Ambient", MoonAmbientLight);
                    //}
                    //Debug.Log(moonLightDirection);
                    if (eclipseByAngle)
                    {
                        float angleMoonSun = Vector3.Dot((skyManager.MoonObj.transform.position - Camera.main.transform.position).normalized,
                        (skyManager.SUN_LIGHT.transform.forward).normalized);
                        float percent = (Mathf.Abs(angleMoonSun) - 0.965f) / (0.9999f - 0.965f);

                        if (controlMoonPhase && MoonA != null)
                        {
                            //Vector3 sunDir = MoonA.GetVector("_SunDir");
                            MoonA.SetVector("_SunDir", Vector3.Lerp(moonLightDirection, new Vector3(0, 0, 50000), percent));
                            //Color sunCol = MoonA.GetColor("_Color");
                            MoonA.SetColor("_Color", Color.Lerp(MoonSunLight, MoonSunLightS, percent));
                            //Color ambCol = MoonA.GetColor("_Ambient");
                            MoonA.SetColor("_Ambient", Color.Lerp(MoonAmbientLight, MoonAmbientLightS, percent));

                            //Color skyCol = MoonA.GetColor("_ColorTint");
                            MoonA.SetColor("_ColorTint", Color.Lerp(MoonSkyLight, MoonSkyLightS, percent));
                        }
                        if (controlMoonPhase && MoonB != null)
                        {
                            //Vector3 sunDir = MoonB.GetVector("_SunDir");
                            MoonB.SetVector("_SunDir", Vector3.Lerp(moonLightDirection, new Vector3(0, 0, 50000), percent));
                            //Color sunCol = MoonB.GetColor("_Color");
                            MoonB.SetColor("_Color", Color.Lerp(MoonSunLight, MoonSunLightS, percent));
                            //Color ambCol = MoonB.GetColor("_Ambient");
                            MoonB.SetColor("_Ambient", Color.Lerp(MoonAmbientLight, MoonAmbientLightS, percent));

                            //Color skyCol = MoonB.GetColor("_ColorTint");
                            MoonB.SetColor("_ColorTint", Color.Lerp(MoonSkyLight, MoonSkyLightS, percent));
                        }
                    }
                    else
                    {
                        if (controlMoonPhase && MoonA != null)
                        {
                            Vector3 sunDir = MoonA.GetVector("_SunDir");
                            MoonA.SetVector("_SunDir", Vector3.Lerp(sunDir, moonLightDirection, Time.deltaTime * 2 * eclipseSpeed));
                            Color sunCol = MoonA.GetColor("_Color");
                            MoonA.SetColor("_Color", Color.Lerp(sunCol, MoonSunLight, Time.deltaTime * 2 * eclipseSpeed));
                            Color ambCol = MoonA.GetColor("_Ambient");
                            MoonA.SetColor("_Ambient", Color.Lerp(ambCol, MoonAmbientLight, Time.deltaTime * 2 * eclipseSpeed));

                            if (tintMoonBySkyGrad)
                            {
                                Color skyCol = MoonA.GetColor("_ColorTint");//skyManager.gradSkyColor //m_fWaveLength
                                MoonA.SetColor("_ColorTint", Color.Lerp(skyCol, skyManager.gradSkyColor, Time.deltaTime * 2 * eclipseSpeed));
                            }
                            else
                            {
                                Color skyCol = MoonA.GetColor("_ColorTint");
                                MoonA.SetColor("_ColorTint", Color.Lerp(skyCol, MoonSkyLight, Time.deltaTime * 2 * eclipseSpeed));
                            }
                        }
                        if (controlMoonPhase && MoonB != null)
                        {
                            Vector3 sunDir = MoonB.GetVector("_SunDir");
                            MoonB.SetVector("_SunDir", Vector3.Lerp(sunDir, moonLightDirection, Time.deltaTime * 2 * eclipseSpeed));
                            Color sunCol = MoonB.GetColor("_Color");
                            MoonB.SetColor("_Color", Color.Lerp(sunCol, MoonSunLight, Time.deltaTime * 2 * eclipseSpeed));
                            Color ambCol = MoonB.GetColor("_Ambient");
                            MoonB.SetColor("_Ambient", Color.Lerp(ambCol, MoonAmbientLight, Time.deltaTime * 2 * eclipseSpeed));

                            if (tintMoonBySkyGrad)
                            {
                                Color skyCol = MoonB.GetColor("_ColorTint");//skyManager.gradSkyColor //m_fWaveLength
                                MoonB.SetColor("_ColorTint", Color.Lerp(skyCol, skyManager.gradSkyColor, Time.deltaTime * 2 * eclipseSpeed));
                            }
                            else
                            {
                                Color skyCol = MoonB.GetColor("_ColorTint");
                                MoonB.SetColor("_ColorTint", Color.Lerp(skyCol, MoonSkyLight, Time.deltaTime * 2 * eclipseSpeed));
                            }
                        }
                    }
                }
                else
                {
                    //Debug.Log(moonLightDirection);
                    if (controlMoonPhase && MoonA != null)
                    {
                        Vector3 sunDir = MoonA.GetVector("_SunDir");
                        MoonA.SetVector("_SunDir", Vector3.Lerp(sunDir, new Vector3(0, 0, 50000), Time.deltaTime * 2 * eclipseSpeed));
                        Color sunCol = MoonA.GetColor("_Color");
                        MoonA.SetColor("_Color", Color.Lerp(sunCol, MoonSunLightS, Time.deltaTime * 2 * eclipseSpeed));
                        Color ambCol = MoonA.GetColor("_Ambient");
                        MoonA.SetColor("_Ambient", Color.Lerp(ambCol, MoonAmbientLightS, Time.deltaTime * 2 * eclipseSpeed));

                        Color skyCol = MoonA.GetColor("_ColorTint");
                        MoonA.SetColor("_ColorTint", Color.Lerp(skyCol, MoonSkyLightS, Time.deltaTime * 2 * eclipseSpeed));
                    }
                    if (controlMoonPhase && MoonB != null)
                    {
                        Vector3 sunDir = MoonB.GetVector("_SunDir");
                        MoonB.SetVector("_SunDir", Vector3.Lerp(sunDir, new Vector3(0, 0, 50000), Time.deltaTime * 2 * eclipseSpeed));
                        Color sunCol = MoonB.GetColor("_Color");
                        MoonB.SetColor("_Color", Color.Lerp(sunCol, MoonSunLightS, Time.deltaTime * 2 * eclipseSpeed));
                        Color ambCol = MoonB.GetColor("_Ambient");
                        MoonB.SetColor("_Ambient", Color.Lerp(ambCol, MoonAmbientLightS, Time.deltaTime * 2 * eclipseSpeed));

                        Color skyCol = MoonB.GetColor("_ColorTint");
                        MoonB.SetColor("_ColorTint", Color.Lerp(skyCol, MoonSkyLightS, Time.deltaTime * 2 * eclipseSpeed));
                    }
                }
            }

            //v0.3 - Apply GUI
            if (activateGUI && enableGUI && Application.isPlaying)
            {
                //URP
                /*
                etherealVolFog.blendVolumeLighting = volumeLightPower;
                etherealVolFog._FogColor = volumeFogColor;
                etherealVolFog.lightNoiseControl = lightNoiseControl;
                etherealVolFog.noiseThickness = volumeFogNoise;
                etherealVolFog._fogHeight = volumeFogHeight;
                etherealVolFog._fogDensity = volumeFogDensity;
                etherealVolFog.heightDensity = heigthNoiseDensities.x;
                etherealVolFog.noiseDensity = heigthNoiseDensities.y;
                etherealVolFog.stepsControl.w = volumeFogNoisePower;
                etherealVolFog.noiseScale = volumeNoiseScale;
                etherealVolFog.noiseSpeed.x = noiseSpeed;
                */
                //etherealVolFog.ClearSkyFac = clearskyFactor;
            }

            //URP
            //v3.3 - shader based stars fade
            if (skyManager.skyboxMat != null)
            {
                if (skyManager.skyboxMat.HasProperty("_Color"))
                {
                    Color StarsCol = skyManager.skyboxMat.GetColor("_Color");
                    if (
                        (!skyManager.AutoSunPosition && (skyManager.Current_Time >= (9 + skyManager.Shift_dawn) & skyManager.Current_Time <= (skyManager.NightTimeMax + skyManager.Shift_dawn)))
                        |
                        (skyManager.AutoSunPosition && skyManager.Rot_Sun_X > 0))
                    {
                        if (skyManager.Current_Time < 1f)
                        { //initialize at game start
                            skyManager.skyboxMat.SetColor("_Color", new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.y));
                        }
                        else
                        {
                            skyManager.skyboxMat.SetColor("_Color", Color.Lerp(StarsCol,
                                new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.y), Time.deltaTime));
                        }
                    }
                    else
                    {
                        if (skyManager.Current_Time < 1f)
                        { //initialize at game start
                            skyManager.skyboxMat.SetColor("_Color", new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.x));
                        }
                        else
                        {
                            skyManager.skyboxMat.SetColor("_Color", Color.Lerp(StarsCol,
                                new Color(skyManager.StarsColor.r, skyManager.StarsColor.g, skyManager.StarsColor.b, 1 - skyManager.MinMaxStarsTransp.x), Time.deltaTime));
                        }
                    }
                }
                if (skyManager.skyboxMat.HasProperty("_Light"))
                {
                    float StarsCover = skyManager.skyboxMat.GetFloat("_Light");
                    skyManager.skyboxMat.SetFloat("_Light", Mathf.Lerp(StarsCover, skyManager.StarsIntensity, Time.deltaTime));
                }
            }




            if (temporalSunMoon == null)//if (Application.isPlaying && temporalSunMoon == null)
            {
                temporalSunMoon = new GameObject();
                temporalSunMoon.name = "SunMoonTransformLerp";
                temporalSunMoon.transform.rotation = skyManager.SUN_LIGHT.transform.rotation;
                temporalSunMoon.transform.position = skyManager.SUN_LIGHT.transform.position;
                Light temp = temporalSunMoon.AddComponent<Light>();
                temp = fullVolumeClouds.sunLight;
                fullVolumeClouds.sunLight = temporalSunMoon.GetComponent<Light>();
            }

            //Play mode only
            if (Application.isPlaying)
            {
                //set mooon based on time of day - X = vertical (minus goes up, 180 to 360), Y = horizontal
                //          Vector4 moonSettings = skyDome.m_skyMaterial.GetVector("_moonPos");
                //         skyDome.m_skyMaterial.SetVector("_moonPos", new Vector4(270 + Mathf.Cos(skyManager.Current_Time * 0.2f) * 90,0.1f* (Time.fixedTime % 360), moonSettings.z, moonSettings.w));
            }

            //v0.1
            if (shaderVolClouds != null)
            {
                //Pass time from sky master
                //shaderVolClouds.Current_Time = skyManager.Current_Time; //URP
                //shaderVolClouds.WeatherSeverity = skyManager.WeatherSeverity;//URP
                shaderVolClouds.WeatherDensity = true;
            }


            //set clouds based on weather
            //set cloud density at game start based on weather
            if (affectCloudDensity)
            {
                if (Application.isPlaying)
                {
                    setFullVolumeCloudsDensity(true);
                }
                else
                {
                    setFullVolumeCloudsDensity(false);
                }
                if (fullVolumeCloudsREFL != null)
                {
                    fullVolumeCloudsREFL.density = fullVolumeClouds.density;
                    fullVolumeCloudsREFL.coverage = fullVolumeClouds.coverage;

                    fullVolumeCloudsREFL.scale = fullVolumeClouds.scale;
                    fullVolumeCloudsREFL.detailScale = fullVolumeClouds.detailScale;
                    fullVolumeCloudsREFL.lowFreqMin = fullVolumeClouds.lowFreqMin;
                    fullVolumeCloudsREFL.lowFreqMax = fullVolumeClouds.lowFreqMax;
                    fullVolumeCloudsREFL.highFreqModifier = fullVolumeClouds.highFreqModifier;
                    fullVolumeCloudsREFL.weatherScale = fullVolumeClouds.weatherScale;
                    fullVolumeCloudsREFL.startHeight = fullVolumeClouds.startHeight;
                    fullVolumeCloudsREFL.thickness = fullVolumeClouds.thickness;
                    fullVolumeCloudsREFL.planetSize = fullVolumeClouds.planetSize;
                    fullVolumeCloudsREFL.cloudSampleMultiplier = fullVolumeClouds.cloudSampleMultiplier;
                    fullVolumeCloudsREFL.globalMultiplier = fullVolumeClouds.globalMultiplier;
                    fullVolumeCloudsREFL.windSpeed = fullVolumeClouds.windSpeed;
                    fullVolumeCloudsREFL.windDirection = fullVolumeClouds.windDirection;
                    fullVolumeCloudsREFL.coverageWindSpeed = fullVolumeClouds.coverageWindSpeed;
                    fullVolumeCloudsREFL.coverageWindDirection = fullVolumeClouds.coverageWindDirection;
                }
            }
            setFullVolumeCloudsColors();

            //apply a set of predefined densities
            if (ApplyPresetDensities)
            {
                applyPresetDens();
                ApplyPresetDensities = false;
            }
        }

        public void applyPresetDens()
        {

            //Full volume clouds with fly through, fog of war - planetary - vortex cloud options
            weatherCloudDensities.Clear();

            if (cloudType == 0)
            {
                weatherCloudDensities.Add(0);
                weatherCloudDensities.Add(0.4f);
                weatherCloudDensities.Add(1.2f);
                weatherCloudDensities.Add(1.4f);//tornado (to do - change material here to faciliate vortex)
                weatherCloudDensities.Add(1.5f);
                weatherCloudDensities.Add(1.5f);
                weatherCloudDensities.Add(1.1f);//flat clouds - also regulate darness density
                weatherCloudDensities.Add(2f);//lightning
                weatherCloudDensities.Add(2f);//heavy storm - also regulate darkness density
                weatherCloudDensities.Add(2f);
                weatherCloudDensities.Add(1f);//CLOUDY
                weatherCloudDensities.Add(0.4f);
                weatherCloudDensities.Add(0.4f);
                weatherCloudDensities.Add(0.9f);
            }
        }

        //0 Sunny
        //1 Foggy
        //2 Heavy fog
        //3 tornado
        //4 snow storm
        //5 freeze storm
        //6 flat clouds
        //7 lightning storm
        //8 heavy storm
        //9 heavy storm dark
        //10 cloudy
        //11 rolling fog
        //12 volcano erupt
        //13 Rain
        public float cloudTransitionSpeed = 1;
        public void setFullVolumeCloudsDensity(bool lerp)
        {
            if (weatherCloudDensities.Count < 14)
            {
                applyPresetDens();//fill with preset if nothing is declared 
            }

            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Sunny)//0
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[0] + densitiesOffset, 0, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Foggy)//1
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[1] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyFog)//2
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[2] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Tornado)//3
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[3] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.SnowStorm)//4
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[4] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.FreezeStorm)//5
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[5] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.FlatClouds)//6
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[6] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm)//8
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[8] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStormDark)//9
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[9] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.RollingFog)//11
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[11] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.VolcanoErupt)//12
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[12] + densitiesOffset, 1, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Rain)//13
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[13] + densitiesOffset, 1, lerp);
            }

            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Cloudy)//10
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[10] + densitiesOffset, 1.32f, lerp);
            }
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm)//7
            {
                setFullVolumeCloudsDensityA(weatherCloudDensities[7] + densitiesOffset, 1.65f, lerp);
                //fullVolumeClouds.lightning.enable
                lightningController.EnableLightning = true;
            }
            else
            {
                lightningController.EnableLightning = forceLightning;
            }
        }
        public void setFullVolumeCloudsDensityA(float density, float darkness, bool lerp)
        {
            if (lerp)
            {
                fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, density, cloudTransitionSpeed * Time.deltaTime * 0.05f);
                fullVolumeClouds.density = Mathf.Lerp(fullVolumeClouds.density, darkness + densityThicknessOffset, cloudTransitionSpeed * Time.deltaTime * 0.05f);

                //v0.2 //URP
                //fullVolumeClouds._NoiseAmp1 = Mathf.Lerp(fullVolumeClouds._NoiseAmp1, density, cloudTransitionSpeed * Time.deltaTime * 0.05f);
                //fullVolumeClouds._NoiseBias = Mathf.Lerp(fullVolumeClouds._NoiseBias, darkness, cloudTransitionSpeed * Time.deltaTime * 0.05f);
            }
            else
            {
                fullVolumeClouds.coverage = density;
                fullVolumeClouds.density = darkness + densityThicknessOffset;

                //v0.2 //URP
                //fullVolumeClouds._NoiseAmp1 = density;
                //fullVolumeClouds._NoiseBias = darkness;
            }
        }

        //Set cloud colors
        public void setFullVolumeCloudsColors()
        {
            setFullVolumeCloudsColorsA();
        }
        public Color cloudColor;
        public Color cloudsAmbientColor;
        public float cloudColorSpeed = 1;
        public float cloudColorSpeedD = 1;
        public float cloudColorSpeedN = 1;
        public Color Day_Sun_Color = new Color(0.933f, 0.933f, 0.933f, 1);
        public Color Day_Ambient_Color = new Color(0.4f, 0.4f, 0.4f, 1);//
        public Color Day_Tint_Color = new Color(0.2f, 0.2f, 0.2f, 1);//
        public Color Dusk_Sun_Color = new Color(0.933f, 0.596f, 0.443f, 1);
        public Color Dusk_Ambient_Color = new Color(0.2f, 0.2f, 0.2f, 0);
        public Color Dusk_Tint_Color = new Color(0.386f, 0, 0, 0);
        public Color Dawn_Sun_Color = new Color(0.933f, 0.933f, 0.933f, 1);
        public Color Dawn_Ambient_Color = new Color(0.35f, 0.35f, 0.35f, 1);//
        public Color Dawn_Tint_Color = new Color(0.2f, 0.05f, 0.1f, 0);//
        public Color Night_Sun_Color = new Color(0.01f, 0.01f, 0.01f, 1);
        public Color Night_Ambient_Color = new Color(0.03f, 0.03f, 0.03f, 0);
        public Color Night_Tint_Color = new Color(0, 0, 0, 0);
        public Color cloudStormColor = new Color(0.13f, 0.12f, 0.11f, 0);
        public Color cloudStormAmbientColor = new Color(0.13f, 0.12f, 0.11f, 0);
        public void setFullVolumeCloudsColorsA()
        {
            if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm
                || skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStormDark
                || skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm
                )
            {
                if (Application.isPlaying)
                {
                    cloudColor = Color.Lerp(cloudColor, cloudStormColor, 0.05f * Time.deltaTime * cloudColorSpeed);
                    cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, cloudStormAmbientColor, 0.05f * cloudColorSpeed * Time.deltaTime);//v1.2.1
                }
                else
                {
                    if (cloudColor != cloudStormColor)
                    {
                        cloudColor = cloudStormColor;
                    }
                    if (cloudsAmbientColor != cloudStormAmbientColor)
                    {
                        cloudsAmbientColor = cloudStormAmbientColor;
                    }
                }
            }
            else
            {
                //v3.0
                if (// IF DAY TIME
                    (!skyManager.AutoSunPosition && (skyManager.Current_Time < (18 + skyManager.Shift_dawn)
                    && skyManager.Current_Time > (9 + skyManager.Shift_dawn)))
                        ||
                    (skyManager.AutoSunPosition && skyManager.Rot_Sun_X > 0)
                )
                {
                    //if(fullVolumeClouds.sunLight.transform != skyManager.SUN_LIGHT.transform)
                    //{
                    //    fullVolumeClouds.sunLight = skyManager.SUN_LIGHT.GetComponent<Light>();
                    //}
                    if (temporalSunMoon != null)
                    {
                        temporalSunMoon.transform.rotation = Quaternion.Lerp(temporalSunMoon.transform.rotation, skyManager.SUN_LIGHT.transform.rotation, cloudColorSpeedD * 0.1f * Time.deltaTime);
                        temporalSunMoon.transform.position = Vector3.Lerp(temporalSunMoon.transform.position, skyManager.SUN_LIGHT.transform.position, cloudColorSpeedD * 0.1f * Time.deltaTime);
                    }

                    if ((skyManager.AutoSunPosition && skyManager.Rot_Sun_X < 25)
                        || (!skyManager.AutoSunPosition && skyManager.Current_Time > (17 + skyManager.Shift_dawn)))
                    { //v3.0

                        float UP_rate = 11f;

                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Dusk_Sun_Color, 0.05f * UP_rate * Time.deltaTime * cloudColorSpeed);
                            cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, Dusk_Ambient_Color, 0.05f * UP_rate * Time.deltaTime);//v1.2.1
                        }
                        else
                        {
                            if (cloudColor != Dusk_Sun_Color)
                            {
                                cloudColor = Dusk_Sun_Color;
                            }
                            if (cloudsAmbientColor != Dusk_Ambient_Color)
                            {
                                cloudsAmbientColor = Dusk_Ambient_Color;//v1.5
                            }
                        }
                    }
                    else if ((skyManager.AutoSunPosition && skyManager.Rot_Sun_X < 65) ||
                        (!skyManager.AutoSunPosition && skyManager.Current_Time > (12 + skyManager.Shift_dawn)))
                    {
                        //raise to max
                        float UP_rate = 3;
                        UP_rate = 0.7f;

                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Day_Sun_Color, 0.05f * UP_rate * Time.deltaTime * cloudColorSpeed);
                            cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, Day_Ambient_Color, 0.5f * UP_rate * Time.deltaTime);//v1.2.1
                        }
                        else
                        {
                            if (cloudColor != Day_Sun_Color)
                            {
                                cloudColor = Day_Sun_Color;
                            }
                            if (cloudsAmbientColor != Day_Ambient_Color)
                            {
                                cloudsAmbientColor = Day_Ambient_Color;//v1.5
                            }
                        }
                    }
                    else
                    {
                        //raise to max
                        float UP_rate = 3;

                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Dawn_Sun_Color, 0.05f * UP_rate * Time.deltaTime * cloudColorSpeed);
                            cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, Dawn_Ambient_Color, 0.5f * UP_rate * Time.deltaTime);//v1.2.1
                        }
                        else
                        {
                            if (cloudColor != Dawn_Sun_Color)
                            {
                                cloudColor = Dawn_Sun_Color;
                            }
                            if (cloudsAmbientColor != Dawn_Ambient_Color)
                            {
                                cloudsAmbientColor = Dawn_Ambient_Color;//v1.5
                            }
                        }
                    }
                }
                else if ((skyManager.AutoSunPosition && skyManager.Rot_Sun_X < 15)//NIGHT TIME
                    || (!skyManager.AutoSunPosition && skyManager.Current_Time <= (9 + skyManager.Shift_dawn)
                    && skyManager.Current_Time > (1 + skyManager.Shift_dawn)))
                {
                    float UP_rate = 0.5f;
                    //Debug.Log("Night Time " + fullVolumeClouds.SunLight.gameObject.name +"," + skyManager.MOON_LIGHT.name);
                    //if (fullVolumeClouds.sunLight.gameObject != skyManager.MOON_LIGHT)
                    //{
                    //    fullVolumeClouds.sunLight = skyManager.MOON_LIGHT.GetComponent<Light>();
                    //    //Debug.Log("Night Time Assign Loon Light");
                    //}
                    if (temporalSunMoon != null)
                    {
                        temporalSunMoon.transform.rotation = Quaternion.Lerp(temporalSunMoon.transform.rotation, skyManager.MOON_LIGHT.transform.rotation, cloudColorSpeedD * 0.1f * Time.deltaTime);
                        temporalSunMoon.transform.position = Vector3.Lerp(temporalSunMoon.transform.position, skyManager.MOON_LIGHT.transform.position, cloudColorSpeedD * 0.1f * Time.deltaTime);
                    }

                    //v2.0.1
                    if ((skyManager.AutoSunPosition && skyManager.Rot_Sun_X < 5)
                        || (!skyManager.AutoSunPosition && skyManager.Current_Time < (8.7f + skyManager.Shift_dawn)))
                    {
                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Night_Sun_Color, 1.5f * UP_rate * Time.deltaTime * cloudColorSpeed);
                        }
                        else
                        {
                            if (cloudColor != Night_Sun_Color)
                            {
                                cloudColor = Night_Sun_Color;
                            }
                        }
                    }
                    else
                    {
                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Dawn_Sun_Color, UP_rate * Time.deltaTime * cloudColorSpeed);
                        }
                        else
                        {
                            if (cloudColor != Dawn_Sun_Color)
                            {
                                cloudColor = Dawn_Sun_Color;
                            }
                        }
                    }
                    if (Application.isPlaying)
                    {
                        cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, Night_Ambient_Color, 2f * UP_rate * Time.deltaTime);//v1.2.1
                    }
                    else
                    {
                        if (cloudsAmbientColor != Night_Ambient_Color)
                        {
                            cloudsAmbientColor = Night_Ambient_Color;//v1.5
                        }
                    }
                }
                else
                { //18 evening up to 1 at night 
                  //if (fullVolumeClouds.sunLight.transform != skyManager.SUN_LIGHT.transform)
                  //{
                  //    fullVolumeClouds.sunLight = skyManager.SUN_LIGHT.GetComponent<Light>();
                  //}


                    float UP_rate = 0.5f;
                    //GLOBAL TINT
                    if ((skyManager.AutoSunPosition && skyManager.Rot_Sun_X < 0)
                        || (!skyManager.AutoSunPosition && skyManager.Current_Time > (22 + skyManager.Shift_dawn)
                        || (skyManager.Current_Time >= 0 & skyManager.Current_Time <= (1 + skyManager.Shift_dawn))))
                    {//v2.0.1

                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Night_Sun_Color, 1.5f * UP_rate * Time.deltaTime * cloudColorSpeed); //v2.0.1
                            cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, Night_Ambient_Color, 2f * UP_rate * Time.deltaTime);//v1.2.1
                        }
                        else
                        {
                            if (cloudColor != Night_Sun_Color)
                            {
                                cloudColor = Night_Sun_Color;
                            }
                            if (cloudsAmbientColor != Night_Ambient_Color)
                            {
                                cloudsAmbientColor = Night_Ambient_Color;//v1.5
                            }
                        }

                        if (skyManager.Current_Time >= 0.45f)
                        {
                            temporalSunMoon.transform.rotation = Quaternion.Lerp(temporalSunMoon.transform.rotation, skyManager.MOON_LIGHT.transform.rotation, cloudColorSpeedN * 0.1f * Time.deltaTime);
                            temporalSunMoon.transform.position = Vector3.Lerp(temporalSunMoon.transform.position, skyManager.MOON_LIGHT.transform.position, cloudColorSpeedN * 0.1f * Time.deltaTime);
                        }
                        else
                        {
                            temporalSunMoon.transform.rotation = Quaternion.Lerp(temporalSunMoon.transform.rotation, skyManager.SUN_LIGHT.transform.rotation, cloudColorSpeedN * 0.1f * Time.deltaTime);
                            temporalSunMoon.transform.position = Vector3.Lerp(temporalSunMoon.transform.position, skyManager.SUN_LIGHT.transform.position, cloudColorSpeedN * 0.1f * Time.deltaTime);
                        }

                    }
                    else
                    {

                        if (temporalSunMoon != null)
                        {
                            temporalSunMoon.transform.rotation = Quaternion.Lerp(temporalSunMoon.transform.rotation, skyManager.SUN_LIGHT.transform.rotation, cloudColorSpeedD * 0.1f * Time.deltaTime);
                            temporalSunMoon.transform.position = Vector3.Lerp(temporalSunMoon.transform.position, skyManager.SUN_LIGHT.transform.position, cloudColorSpeedD * 0.1f * Time.deltaTime);
                        }

                        if (Application.isPlaying)
                        {
                            cloudColor = Color.Lerp(cloudColor, Dusk_Sun_Color, 0.5f * UP_rate * Time.deltaTime * cloudColorSpeed);
                            cloudsAmbientColor = Color.Lerp(cloudsAmbientColor, Dusk_Ambient_Color, 0.5f * UP_rate * Time.deltaTime);//v1.2.1
                        }
                        else
                        {
                            if (cloudColor != Dusk_Sun_Color)
                            {
                                cloudColor = Dusk_Sun_Color;
                            }
                            if (cloudsAmbientColor != Dusk_Ambient_Color)
                            {
                                cloudsAmbientColor = Dusk_Ambient_Color;//v1.5
                            }
                        }
                    }
                }//END COLOR SET
            }//END STORM CHECK

            if (affectCloudColor && fullVolumeClouds != null)
            {
                fullVolumeClouds.highSunColor = cloudColor;
                fullVolumeClouds.cloudBaseColor = cloudsAmbientColor;
                //fullVolumeClouds.sunColor = cloudColor; //URP

                if (affectCloudTopColor) //v0.4
                {
                    fullVolumeClouds.cloudTopColor = cloudsAmbientColor;
                }

                //v0.2
                //fullVolumeClouds._GroundColor = new Vector3(cloudsAmbientColor.r, cloudsAmbientColor.g, cloudsAmbientColor.b);//URP
                fullVolumeClouds._SkyTint = new Vector3(cloudColor.r, cloudColor.g, cloudColor.b);

                if (fullVolumeCloudsREFL != null)
                {
                    fullVolumeCloudsREFL.highSunColor = fullVolumeClouds.highSunColor;
                    fullVolumeCloudsREFL.cloudBaseColor = fullVolumeClouds.cloudBaseColor;
                    fullVolumeCloudsREFL._SkyTint = fullVolumeClouds._SkyTint;

                    if (affectCloudTopColor) //v0.4
                    {
                        fullVolumeCloudsREFL.cloudTopColor = fullVolumeClouds.cloudTopColor;
                    }
                }
            }
        }//END SET CLOUD COLOR Function

        //[Header("------------------------------------------------------")]
        //[Header("Fog Parameters")]
        //[Header("------------------------------------------------------")]
        //v0.3 - GUI
        [HideInInspector]
        public Vector2 heigthNoiseDensities = new Vector2(0, 0);
        [HideInInspector]
        public float clearskyFactor = 0.43f;
        [HideInInspector]
        public float noiseSpeed = 0;
        [HideInInspector]
        public float volumeFogHeight = 0;
        [HideInInspector]
        public float volumeNoiseScale = 0;
        [HideInInspector]
        public float volumeFogDensity = 0;
        [HideInInspector]
        public float volumeLightPower = 1;
        [HideInInspector]
        public float volumeFogPower = 1;
        [HideInInspector]
        public float volumeFogNoise = 0;
        [HideInInspector]
        public float volumeFogNoisePower = 1;
        [HideInInspector]
        public Color volumeFogColor = Color.white * 0.15f;
        [HideInInspector]
        public Vector4 lightNoiseControl = new Vector4(1, 1, 1, 1);

        [Header("------------------------------------------------------")]
        [Header("Eclipse System")]
        [Header("------------------------------------------------------")]
        //v0.4a - Advanced Sky Coloration - Eclipse
        [Tooltip("Active eclipse system")]
        public bool useEclipseSystem = false;
        [Tooltip("Lerp to eclipse, if deactivated lerp back to previous state")]
        public bool activateEclipse = false; //when activated will lerp to eclipse, when deactivated lerps back to previous state, assume stationary moon
        public bool eclipseByAngle = false;
        [Tooltip("Enable eclipse transition in editor. Advanced option, use with caution, preferably edit eclipse in play mode and pass the settings to editor.")]
        public bool eclipseInEditor = false;

        public float fExposureOffsetS = 0;
        public Vector3 fWaveLengthOffsetS = Vector3.zero; //
        public float gOffsetS = 0;
        public float scaleDifOffsetS = 0;
        public Vector3 tintColorOffsetS = Vector3.zero;  //
        public float sunRingFactorOffsetS = 0;
        public float Sun_halo_factorS = 0; //0 - 0.015 range   0.78
        public float Sun_eclipse_factorS = 0;//-0.53
        public float Glob_scaleS = 1;
        public bool eclipseActivated = false;
        public bool passSettingsToEclipse = false;
        public float eclipseSpeed = 1;

        [Header("------------------------------------------------------")]
        [Header("Sky Parameters")]
        [Header("------------------------------------------------------")]
        [Tooltip("Offset sky Gradient coloration and curve controls")]
        public bool advancedSkyOffsets = false;//enable control of sky be below offsets
        //v5.3.1
        [Tooltip("Choose sky style Preset (1-11), choose 12 to enable Advanced Sky Options below")]
        public int skyPreset = 9;
        public float fExposureOffset = 0;
        public Vector3 fWaveLengthOffset = Vector3.zero; //
        public float gOffset = 0;
        public float scaleDifOffset = 0;
        public Vector3 tintColorOffset = Vector3.zero;  //
        public float sunRingFactorOffset = 0;
        public float Sun_halo_factor = 0; //0 - 0.015 range   0.78
        public float Sun_eclipse_factor = 0;//			-0.53
        public float Glob_scale = 1;

        public bool advancedSkyOptions = false;
        //Control further:
        [Tooltip("Advanced Sky Options below")]
        public float Esun = 0.66f;
        public float Kr = 0.005510659f;
        public float Km = 0.0004f;//only when preset is 12 plus 
        public float fsamples = 0.02f;
        public float fRayleightScaleDepth = 0.06f;
        public float coloration = -0.04399999f;

        [Header("------------------------------------------------------")]
        [Header("Moon Controls")]
        [Header("------------------------------------------------------")]
        //v0.4 - Moon phases
        [Tooltip("Manually control moon lighting")]
        public bool controlMoonPhase = false;
        [Tooltip("Moon lighting left-right (X), up-down (Y), front-back (Z)")]
        public Vector3 moonLightDirection = Vector3.one * 50000;
        public Color MoonSunLight = new Color(150 / 255, 150 / 255, 150 / 255, 1);
        public Color MoonAmbientLight = new Color(100 / 255, 100 / 255, 100 / 255, 1);
        public Color MoonSunLightS = new Color(150 / 255, 100 / 255, 60 / 255, 1);
        public Color MoonAmbientLightS = new Color(100 / 255, 60 / 255, 40 / 255, 1);
        public bool tintMoonBySkyGrad = false;
        public Color MoonSkyLight = new Color(0 / 255, 0 / 255, 0 / 255, 0);
        public Color MoonSkyLightS = new Color(0 / 255, 0 / 255, 0 / 255, 0);
        public Material MoonA;
        public Material MoonB;
        //enable the automatic moon shading based on the sun postion with AutoMoonLighting option
        //if this option is selected in URP, assing same properties in MoonB material, that is used to render moon behind clouds in water reflctions
        [Tooltip("enable the automatic moon shading based on the sun postion with AutoMoonLighting option")]
        public bool autoMoonPhase = false;

    }
}