//#define URP

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Artngame.SKYMASTER
{
    public class biomesSkyMaster : MonoBehaviour
    {
        public SkyMasterManager skyManager;

        //v5.2.3c
        public bool changeByTerrainLayers = false; //use detection of layers to change biome
        public List<TerrainLayer> biomesTerrainLayer = new List<TerrainLayer>();
        public bool playMusicPerBiome = false;
        public List<AudioClip> biomesMusic = new List<AudioClip>();
        public List<AudioSource> biomeAudioSources = new List<AudioSource>();//v0.4 
        public List<float> biomeMaxVolume = new List<float>();//v0.4
        Vector3 prevPlayerPos = new Vector3(0, 0, 0);
        float lastTerrainLayerChangeTime = 0;
        public float TerrainLayerChangeDelay = 2;
        public float updateTerrainLayerDist = 1f;
        public float updateRate = 2;
        public float offsetRaycastDown = 0.5f;
        public float maxRaycastDist = 100;
        [SerializeField]
        private LayerMask FloorLayer;
        public TerrainLayer currentTerrainLayer;

        public List<Transform> biomesCenters = new List<Transform>();
        public List<float> biomesRadius = new List<float>();
        public Transform Player;
        public bool enableDebug = false;

        public Material shaderCloudsScriptlessMat;
        public bool useShaderCloudsVolcano = false;
        //public bool shaderCloudScriptles;
        // public Material shaderCloudsMat;
        //public bool shaderCloudScriptles;

        public Gradient initialSkyGrad;
        public Gradient initialSkyUnderGrad;

        public List<Gradient> biomesSkyGradients = new List<Gradient>();
        public List<Gradient> biomesSkyUnderGradients = new List<Gradient>();
        public List<SkyMasterManager.Volume_Weather_types> biomesWeather = new List<SkyMasterManager.Volume_Weather_types>();

        public int cloudType = 0; //0 = full volumes, 1 = background, 2=shader volumes, 3=particles 

        //v0.1
        public bool useMultiTypeClouds = false;
        public List<int> biomeCloudType = new List<int>();
        public bool scriptlessShaderVolClouds = true;

        public bool updateFullVolumeClouds = true;

#if URP
        public connectSuntoFullVolumeCloudsURP fullVolumeClouds;
        public connectSuntoVolumeCloudsURP volumeClouds; //v0.1
#else
        public CloudScript fullVolumeClouds;
        public FullVolumeCloudsSkyMaster volumeClouds; //v0.1
#endif

        public float cloudLerpSpeed = 1;

        [Tooltip("Sunny, Foggy,Heavy Fog,Tornado,Snow storm,Freeze storm,Flat cloud,Ligthtning,Heavy Storm,Heavy Storm Dark,Cloudy,Rolling Fog,Volcano,Rain ")]
        public List<float> cloudDensities = new List<float>();//0 to 1 normalized
        public bool assignSampleCloudDensities = false;
        public float fullVolumeCloudDensityModifier = 2;
        //Sunny
        //Foggy
        //Heavy Fog
        //Tornado
        //Snow storm
        //Freeze Storm
        //Flat clouds
        //Ligthtning Storm
        //Heavy Storm
        //Heavy Storm Dark
        //Cloudy
        //Rolling Fog
        //Volcano Erupt
        //Rain

        public bool useSunColoration = false;
        public bool useTimeOfDay = false;
        //biomesTimeOfDay biomesSunColors
        public List<Color> biomesSunColors = new List<Color>();
        public List<float> biomesTimeOfDay = new List<float>();

        public bool useCloudColoration = false;

        [Tooltip("Alpha in base = ambient, in sun is sun power and in top is cloud transparency - density")]
        public bool useAlphaForCloudLightPower = false;//Alpha in base = ambient, in sun is sun power and in top is cloud transparency - density
        public List<Color> cloudTopColor = new List<Color>();  // new Color(0.65f, 0.65f, 0.65f);
        public List<Color> cloudBaseColor = new List<Color>(); // = new Color(0.2f, 0.2f, 0.2f);
        public List<Color> cloudSunColor = new List<Color>();  // = new Color(0.9f, 0.9f, 0.9f);

        public bool controlFog = true;
        public bool controlFogHeight = false;//v6.3a - 5.5.2a
        public bool controlVolumetricLighting = false;

        //v0.1
        public List<float> biomeVortexPower = new List<float>();

#if URP
        public connectSuntoVolumeFogURP fog;
#else
        public GlobalFogSkyMaster fog;
#endif
        public List<float> biomesFogHeight = new List<float>();//v6.3a - 5.5.2a
        public List<float> biomesFogIntensity = new List<float>();
        public List<float> biomesGradientDistance = new List<float>();
        public List<Gradient> biomesFogGradients = new List<Gradient>();

        //v0.2
        public List<Color> biomesFogColor = new List<Color>();
        public List<float> biomesVolumeLightPower = new List<float>();
        public List<float> biomesLocalVolumeLightsPower = new List<float>();
        public bool controlCloudShafts = false;
        public List<int> biomesCloudShaftsType = new List<int>();
        public List<float> biomesCloudShaftsPower = new List<float>();

        //Water - OCEANIS #if OCEANIS

        public bool controlSunIntensity = true;
        public List<float> biomesSunIntensity = new List<float>();
        public bool controlAmbientIntensity = true;
        public List<float> biomesAmbientIntensity = new List<float>();

        //v0.3
#if !URP
        public bool regulateReflections = false;
        public PlanarReflectionSM reflectionScript;
#endif

        //v5.2.3c 
        private IEnumerator CheckGround()
        {
            while (true)
            {
                if ((Player.position - prevPlayerPos).magnitude > updateTerrainLayerDist && //Controller.isGrounded && Controller.velocity != Vector3.zero &&
                                                                                            //Physics.Raycast(transform.position - new Vector3(0, 0.5f * Controller.height + 0.5f * Controller.radius, 0),
                    Physics.Raycast(Player.position - new Vector3(0, offsetRaycastDown, 0),
                        Vector3.down,
                        out RaycastHit hit,
                        maxRaycastDist,
                        FloorLayer)
                    )
                {

                    prevPlayerPos = Player.position;

                    //Debug.Log("prevPlayerPos = "+prevPlayerPos);

                    if (hit.collider.TryGetComponent<Terrain>(out Terrain terrain))
                    {
                        yield return StartCoroutine(findTerrainLayer(terrain, hit.point));
                    }
                    //else if (hit.collider.TryGetComponent<Renderer>(out Renderer renderer))
                    //{
                    //    yield return StartCoroutine(PlayFootstepSoundFromRenderer(renderer));
                    //}
                }

                yield return null;
            }
        }
        private IEnumerator findTerrainLayer(Terrain Terrain, Vector3 HitPoint)
        {
            //Debug.Log("Terrain.name = "+Terrain.name);

            Vector3 terrainPosition = HitPoint - Terrain.transform.position;
            Vector3 splatMapPosition = new Vector3(
                terrainPosition.x / Terrain.terrainData.size.x,
                0,
                terrainPosition.z / Terrain.terrainData.size.z
            );

            int x = Mathf.FloorToInt(splatMapPosition.x * Terrain.terrainData.alphamapWidth);
            int z = Mathf.FloorToInt(splatMapPosition.z * Terrain.terrainData.alphamapHeight);

            float[,,] alphaMap = Terrain.terrainData.GetAlphamaps(x, z, 1, 1);

            if (1 == 1)
            {
                int primaryIndex = 0;
                for (int i = 1; i < alphaMap.Length; i++)
                {
                    if (alphaMap[0, 0, i] > alphaMap[0, 0, primaryIndex])
                    {
                        primaryIndex = i;
                    }
                }

                currentTerrainLayer = Terrain.terrainData.terrainLayers[primaryIndex];

                yield return new WaitForSeconds(updateRate);

                //foreach (TextureSound textureSound in TextureSounds)
                //{
                //    if (textureSound.Albedo == Terrain.terrainData.terrainLayers[primaryIndex].diffuseTexture)
                //    {
                //        AudioClip clip = GetClipFromTextureSound(textureSound);
                //        AudioSource.PlayOneShot(clip);
                //        yield return new WaitForSeconds(clip.length);
                //        break;
                //    }
                //}
            }
            /*
            else
            {
                List<AudioClip> clips = new List<AudioClip>();
                int clipIndex = 0;
                for (int i = 0; i < alphaMap.Length; i++)
                {
                    if (alphaMap[0, 0, i] > 0)
                    {
                        //foreach (TextureSound textureSound in TextureSounds)
                        //{
                        //    if (textureSound.Albedo == Terrain.terrainData.terrainLayers[i].diffuseTexture)
                        //    {
                        //        AudioClip clip = GetClipFromTextureSound(textureSound);
                        //        AudioSource.PlayOneShot(clip, alphaMap[0, 0, i]);
                        //        clips.Add(clip);
                        //        clipIndex++;
                        //        break;
                        //    }
                        //}
                    }
                }

                //float longestClip = clips.Max(clip => clip.length);

                yield return new WaitForSeconds(15);
            }
            */
        }


        // Start is called before the first frame update
        void Start()
        {
            //v5.2.3c
            if (changeByTerrainLayers)
            {
                StartCoroutine(CheckGround());
            }

            if (assignSampleCloudDensities)
            {
                cloudDensities.Clear();
                cloudDensities.Add(0.0f); //Sunny 0
                cloudDensities.Add(0.2f); //Foggy 1
                cloudDensities.Add(0.3f); //Heavy Fog 2
                cloudDensities.Add(0.4f); //Tornado 3
                cloudDensities.Add(0.7f); //Snow storm 4
                cloudDensities.Add(0.8f); //Freeze Storm 5
                cloudDensities.Add(0.1f); //Flat clouds 6
                cloudDensities.Add(0.85f);//Ligthtning Storm 7
                cloudDensities.Add(1.0f); //Heavy Storm 8
                cloudDensities.Add(0.9f); //Heavy Storm Dark 9
                cloudDensities.Add(0.5f); //Cloudy 10
                cloudDensities.Add(0.1f); //Rolling Fog 11
                cloudDensities.Add(0.0f); //Volcano Erupt 12
                cloudDensities.Add(0.5f); //Rain 13
            }

            if (skyManager != null)
            {
                skyManager.SkyColorGrad = biomesSkyGradients[0];
                skyManager.SkyTintGrad = biomesSkyUnderGradients[0];

                //initialSkyGrad = skyManager.SkyColorGrad;
                //initialSkyUnderGrad = skyManager.SkyTintGrad;

                //Gradient copyG = new Gradient();
                //copyG.alphaKeys = initialSkyGrad.alphaKeys;
                //copyG.colorKeys = initialSkyGrad.colorKeys;
                //copyG.mode = initialSkyGrad.mode;

                //biomesSkyGradients.Insert(0, copyG);     //START REGION must coincide with chosen gradient

                //Gradient copyGA = new Gradient();
                //copyGA.alphaKeys = initialSkyUnderGrad.alphaKeys;
                //copyGA.colorKeys = initialSkyUnderGrad.colorKeys;
                //copyGA.mode = initialSkyUnderGrad.mode;

                //biomesSkyUnderGradients.Insert(0, copyGA);
            }

            if (Player == null)
            {
                Player = Camera.main.transform;
            }
        }

        //v0.1
        public int currentBiomeID = 0;

        // Update is called once per frame
        void Update()
        {            

            if (skyManager != null && updateFullVolumeClouds && fullVolumeClouds != null)
            {

                if (!useMultiTypeClouds || (useMultiTypeClouds && biomeCloudType[currentBiomeID] == 0)) //CLOUD TYPE 0 = FULLY VOLUMETRIC with FLY THROUGH
                {

                    if (volumeClouds == null || (volumeClouds != null && !volumeClouds.enabled))
                    {
                        //ENABLE CLOUDS if were disabled
                        if (!fullVolumeClouds.enabled)
                        {
                            fullVolumeClouds.enabled = true;//v0.3
                        }
                        //#if URP
                        //                    if (!fullVolumeClouds.enableFog)
                        //                    {
                        //                        fullVolumeClouds.enableFog = true;
                        //                    }
                        //#else
                        //                    if (!fullVolumeClouds.enabled)
                        //                    {
                        //                        fullVolumeClouds.enabled = true;
                        //                    }
                        //#endif

                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Sunny)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[0] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[0] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.FlatClouds)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[6] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[6] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStormDark)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[9] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[9] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Rain)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[13] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[13] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Tornado)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[3] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[3] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }


                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Cloudy)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[10] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[10] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[7] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[7] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[8] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[8] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.SnowStorm)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[4] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[4] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                        }

                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.VolcanoErupt)
                        {
                            fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, cloudDensities[0] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, cloudDensities[0] * fullVolumeCloudDensityModifier, Time.deltaTime * cloudLerpSpeed);
                            if (useShaderCloudsVolcano)
                            {
                                if (scriptlessShaderVolClouds)
                                {
                                    float cover = shaderCloudsScriptlessMat.GetFloat("_Coverage");
                                    shaderCloudsScriptlessMat.SetFloat("_Coverage", Mathf.Lerp(cover, 1, Time.deltaTime * cloudLerpSpeed * 0.14f));
                                }
                                else
                                {

                                }
                            }
                        }
                        else
                        {
                            if (useShaderCloudsVolcano)
                            {
                                if (scriptlessShaderVolClouds)
                                {
                                    float cover = shaderCloudsScriptlessMat.GetFloat("_Coverage");
                                    shaderCloudsScriptlessMat.SetFloat("_Coverage", Mathf.Lerp(cover, 0f, Time.deltaTime * cloudLerpSpeed * 0.14f));
                                }
                                else
                                {

                                }
                            }
                        }

                    }

                    //DECREASE and disable BACKGROUND CLOUDS
                    if (volumeClouds != null && volumeClouds.enabled) //v0.3
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 0, Time.deltaTime * cloudLerpSpeed);
                        if (volumeClouds._NoiseAmp1 < 6f)
                        {
                            volumeClouds.enabled = false;//v0.3
                            Camera.main.clearFlags = CameraClearFlags.Skybox;

//v0.3                      //v0.3
#if !URP
                            if (regulateReflections && reflectionScript != null)
                            {
                                if(reflectionScript.m_ReflectionCameraOut != null)
                                {
                                    FullVolumeCloudsSkyMaster volClouds = reflectionScript.m_ReflectionCameraOut.GetComponent<FullVolumeCloudsSkyMaster>();
                                    if(volClouds != null)
                                    {
                                        volClouds.enabled = false;
                                    }
                                    CloudScript fullvolClouds = reflectionScript.m_ReflectionCameraOut.GetComponent<CloudScript>();
                                    if (fullvolClouds != null)
                                    {
                                        fullvolClouds.enabled = true;
                                    }
                                }
                            }
#endif
                        }
                    }
                }
                else //CLOUD TYPE 1 - BACKGROUND VOLUME CLOUDS
                {

                    if (fullVolumeClouds == null || (fullVolumeClouds != null && !fullVolumeClouds.enabled))
                    {
                        //ENABLE CLOUDS if were disabled
                        //if (!volumeClouds.enableFog)
                        //{
                        //    volumeClouds.enableFog = true;
                        //}
                        if (!volumeClouds.enabled)
                        {
                            volumeClouds.enabled = true;//v0.3
                        }
                        //#if URP
                        //                    if (!volumeClouds.enableFog)
                        //                    {
                        //                        volumeClouds.enableFog = true;
                        //                    }
                        //#else
                        //                    if (!volumeClouds.enabled)
                        //                    {
                        //                        volumeClouds.enabled = true;
                        //                    }
                        //#endif

                        //remap 0-1 to 5 to 15
                        float cloudType2ModifierMult = 10;
                        float cloudType2ModifierAdd = 5;
                        float mutl = fullVolumeCloudDensityModifier * cloudType2ModifierMult + cloudType2ModifierAdd;
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Sunny)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[0] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.FlatClouds)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[6] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStormDark)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[9] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Rain)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[13] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Tornado)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[3] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.Cloudy)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[10] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.LightningStorm)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[7] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[8] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }
                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.SnowStorm)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[4] * mutl, Time.deltaTime * cloudLerpSpeed);
                        }

                        if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.VolcanoErupt)
                        {
                            volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, cloudDensities[0] * mutl, Time.deltaTime * cloudLerpSpeed);
                            if (useShaderCloudsVolcano)
                            {
                                if (scriptlessShaderVolClouds)
                                {
                                    float cover = shaderCloudsScriptlessMat.GetFloat("_Coverage");
                                    shaderCloudsScriptlessMat.SetFloat("_Coverage", Mathf.Lerp(cover, 1, Time.deltaTime * cloudLerpSpeed * 0.14f));
                                }
                                else
                                {

                                }
                            }
                        }
                        else
                        {
                            if (useShaderCloudsVolcano)
                            {
                                if (scriptlessShaderVolClouds)
                                {
                                    float cover = shaderCloudsScriptlessMat.GetFloat("_Coverage");
                                    shaderCloudsScriptlessMat.SetFloat("_Coverage", Mathf.Lerp(cover, 0f, Time.deltaTime * cloudLerpSpeed * 0.14f));
                                }
                                else
                                {

                                }
                            }
                        }

                    }

                    //DECREASE  and disable FULL VOLUME CLOUDS
                    if (fullVolumeClouds.enabled) //v0.3
                    {
                        fullVolumeClouds.coverage = Mathf.Lerp(fullVolumeClouds.coverage, 0, Time.deltaTime * cloudLerpSpeed);
                        fullVolumeClouds.coverageHigh = Mathf.Lerp(fullVolumeClouds.coverageHigh, 0, Time.deltaTime * cloudLerpSpeed);
                        if (fullVolumeClouds.coverage < 0.01f)
                        {
                            fullVolumeClouds.enabled = false;//v0.3

//v0.3                      //v0.3
#if !URP
                            if (regulateReflections && reflectionScript != null)
                            {
                                if (reflectionScript.m_ReflectionCameraOut != null)
                                {
                                    FullVolumeCloudsSkyMaster volClouds = reflectionScript.m_ReflectionCameraOut.GetComponent<FullVolumeCloudsSkyMaster>();
                                    if (volClouds != null)
                                    {
                                        volClouds.enabled = true;
                                    }
                                    CloudScript fullvolClouds = reflectionScript.m_ReflectionCameraOut.GetComponent<CloudScript>();
                                    if (fullvolClouds != null)
                                    {
                                        fullvolClouds.enabled = false;
                                    }
                                }
                            }
#endif
                        }
                    }
                }
            }


            //find which biome
            if (Player != null)
            {
                for (int i = 0; i < biomesCenters.Count; i++)
                {
                    //v5.2.3c
                    //if((biomesCenters[i].position - Player.position).magnitude < biomesRadius[i])
                    if ((!changeByTerrainLayers && (biomesCenters[i].position - Player.position).magnitude < biomesRadius[i])
                        || (changeByTerrainLayers && currentTerrainLayer == biomesTerrainLayer[i] && (Time.fixedTime - lastTerrainLayerChangeTime) > TerrainLayerChangeDelay)
                    )
                    {

                        lastTerrainLayerChangeTime = Time.fixedTime;

                        //v0.1
                        currentBiomeID = i;

                        //SKY
                        skyManager.SkyColorGrad = LerpGradient(skyManager.SkyColorGrad, biomesSkyGradients[i], Time.deltaTime * cloudLerpSpeed, true, true);
                        skyManager.SkyTintGrad = LerpGradient(skyManager.SkyTintGrad, biomesSkyUnderGradients[i], Time.deltaTime * cloudLerpSpeed, true, true);

                        skyManager.currentWeatherName = biomesWeather[i];

                        //FOG
                        if (controlFog && fog != null)
                        {
#if URP
                            fog._FogColor = Color.Lerp(fog._FogColor, biomesFogColor[i], Time.deltaTime * cloudLerpSpeed);

                            //v0.2
                            if (controlVolumetricLighting)
                            {
                                fog.lightControlA.x = Mathf.Lerp(fog.lightControlA.x, biomesVolumeLightPower[i], Time.deltaTime * cloudLerpSpeed);
                                fog.lightControlA.y = Mathf.Lerp(fog.lightControlA.y, biomesLocalVolumeLightsPower[i], Time.deltaTime * cloudLerpSpeed);
                            }

                            fog._fogDensity = Mathf.Lerp(fog._fogDensity, biomesFogIntensity[i] / 10000, Time.deltaTime * cloudLerpSpeed);
#else
                            fog.DistGradient = LerpGradient(fog.DistGradient, biomesFogGradients[i], Time.deltaTime * 2, true, true);
                            fog.heightDensity = Mathf.Lerp(fog.heightDensity, biomesFogIntensity[i] / 10000, Time.deltaTime * cloudLerpSpeed);
                            fog.GradientBounds.y = Mathf.Lerp(fog.GradientBounds.y, biomesGradientDistance[i], Time.deltaTime * cloudLerpSpeed);
                            if (controlFogHeight)
                            {
                                fog.height = Mathf.Lerp(fog.height, biomesFogHeight[i], Time.deltaTime * cloudLerpSpeed);//v6.3a - 5.5.2a
                            }
#endif
                        }

                        //SUN COLOR
                        if (useSunColoration)
                        {
                            Color tmpColSun = skyManager.SUN_LIGHT.GetComponent<Light>().color;
                            skyManager.SUN_LIGHT.GetComponent<Light>().color = Color.Lerp(tmpColSun, biomesSunColors[i], Time.deltaTime * cloudLerpSpeed);
                        }
                        //TIME OF DAY
                        if (useTimeOfDay)
                        {
                            skyManager.Current_Time = Mathf.Lerp(skyManager.Current_Time, biomesTimeOfDay[i], Time.deltaTime * cloudLerpSpeed);
                        }

                        //SUN
                        if (controlSunIntensity)
                        {
                            skyManager.Max_sun_intensity = Mathf.Lerp(skyManager.Max_sun_intensity, biomesSunIntensity[i], Time.deltaTime * cloudLerpSpeed);
                        }

                        //AMBIENT
                        if (controlAmbientIntensity)
                        {
                            skyManager.AmbientIntensity = Mathf.Lerp(skyManager.AmbientIntensity, biomesAmbientIntensity[i], Time.deltaTime * cloudLerpSpeed);
                        }

                        //v0.4
                        if (playMusicPerBiome)
                        {
                            if (biomeAudioSources.Count > i)
                            {
                                //fade out all else
                                for(int j =0;j< biomeAudioSources.Count; j++)
                                {
                                    //assign clip if not there
                                    if(biomeAudioSources[j].clip == null || biomeAudioSources[j].clip != biomesMusic[j])
                                    {
                                        biomeAudioSources[j].clip = biomesMusic[j];
                                    }
                                    else
                                    {
                                        if (j == i) //if current biome found, fade it in
                                        {
                                            //Debug.Log("biomeAudioSources[j].volume "+i+" = " +biomeAudioSources[j].volume);
                                            if (biomeAudioSources[j].volume < biomeMaxVolume[i])
                                            {
                                                if (!biomeAudioSources[j].isPlaying)
                                                {
                                                    biomeAudioSources[j].Play(0);// PlayOneShot(biomeAudioSources[j].clip);
                                                }
                                                StartCoroutine(FadeAudio(biomeAudioSources[j],4, biomeMaxVolume[i]));
                                            }
                                        }
                                        else
                                        {
                                            if (biomeAudioSources[j].volume > 0)
                                            {
                                                StartCoroutine(FadeAudio(biomeAudioSources[j], 2, 0));
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //CLOUD COLOR
                        if (useCloudColoration)
                        {
                            if (!useMultiTypeClouds || (useMultiTypeClouds && biomeCloudType[currentBiomeID] == 0)) //CLOUD TYPE 0 = FULLY VOLUMETRIC with FLY THROUGH
                            {
                                fullVolumeClouds.cloudTopColor = Color.Lerp(fullVolumeClouds.cloudTopColor, cloudTopColor[i], Time.deltaTime * cloudLerpSpeed);
                                fullVolumeClouds.cloudBaseColor = Color.Lerp(fullVolumeClouds.cloudBaseColor, cloudBaseColor[i], Time.deltaTime * cloudLerpSpeed);
                                fullVolumeClouds.highSunColor = Color.Lerp(fullVolumeClouds.highSunColor, cloudSunColor[i], Time.deltaTime * cloudLerpSpeed);

                                //v0.2
                                if (useAlphaForCloudLightPower)
                                {
                                    fullVolumeClouds.ambientLightFactor = Mathf.Lerp(fullVolumeClouds.ambientLightFactor, (cloudBaseColor[i].a / 1.0f), Time.deltaTime * cloudLerpSpeed);
                                    fullVolumeClouds.sunLightFactor = Mathf.Lerp(fullVolumeClouds.sunLightFactor, (cloudSunColor[i].a / 1.0f) * 5, Time.deltaTime * cloudLerpSpeed);
                                    fullVolumeClouds.cloudSampleMultiplier = Mathf.Lerp(fullVolumeClouds.cloudSampleMultiplier, (cloudTopColor[i].a / 1.0f) * 2, Time.deltaTime * cloudLerpSpeed);
                                }

#if URP
                                if (biomeVortexPower.Count > i && biomeVortexPower[i] != 0)
                                {
                                    fullVolumeClouds.enableVORTEX = true;
                                    fullVolumeClouds.vortexRadius = Mathf.Lerp(fullVolumeClouds.vortexRadius, biomeVortexPower[i] * 100 * 450, Time.deltaTime * cloudLerpSpeed);
                                }
                                else
                                {
                                    fullVolumeClouds.vortexRadius = Mathf.Lerp(fullVolumeClouds.vortexRadius, 0, Time.deltaTime * cloudLerpSpeed);
                                    if (fullVolumeClouds.vortexRadius < 20)
                                    {
                                        fullVolumeClouds.enableVORTEX = false;
                                    }
                                }
#endif
                            }
                            else
                            {
                                volumeClouds._SkyTint = Vector3.Lerp(volumeClouds._SkyTint, new Vector3(cloudTopColor[i].r, cloudTopColor[i].g, cloudTopColor[i].b) * 5, Time.deltaTime * cloudLerpSpeed);
                                volumeClouds._GroundColor = Vector3.Lerp(volumeClouds._GroundColor, new Vector3(cloudBaseColor[i].r, cloudBaseColor[i].g, cloudBaseColor[i].b), Time.deltaTime * cloudLerpSpeed);
                            }
                        }

                        //v0.2
#if URP
                        if (controlCloudShafts)
                        {
                            if (biomesCloudShaftsType[i] != 0)
                            {
                                //set type and power
                                fullVolumeClouds.sunRaysOn = biomesCloudShaftsType[i];
                                fullVolumeClouds.sunRaysPower = Mathf.Lerp(fullVolumeClouds.sunRaysPower, biomesCloudShaftsPower[i], Time.deltaTime * cloudLerpSpeed);
                            }
                            else
                            {
                                //reset power to zero
                                fullVolumeClouds.sunRaysPower = Mathf.Lerp(fullVolumeClouds.sunRaysPower, 0, Time.deltaTime * cloudLerpSpeed);
                                if (Mathf.Abs(fullVolumeClouds.sunRaysPower) < 0.005f)
                                {
                                    fullVolumeClouds.sunRaysPower = 0;
                                    fullVolumeClouds.sunRaysOn = 0;
                                }
                            }
                        }
#endif

                        //if within the biome, change state and lerp
                        if (enableDebug)
                        {
                            Debug.Log("Inside Biome: " + i);
                        }

                        break;
                    }
                }
            }

        }


        //GRAD LERP
        Gradient outGrad;
        //Gradient outGradUnder;
        Gradient LerpGradient(Gradient gradA, Gradient gradB, float lerpTime, bool doColor, bool doAlpha)
        {
            List<float> keyTimes = new List<float>();

            if (doColor)
            {
                for (int i = 0; i < gradA.colorKeys.Length; i++)
                {
                    float timeK = gradA.colorKeys[i].time;
                    if (!keyTimes.Contains(timeK) && keyTimes.Count < 8)
                    {
                        keyTimes.Add(timeK);
                    }
                }
            }

            if (doAlpha)
            {
                for (int i = 0; i < gradA.alphaKeys.Length; i++)
                {
                    float timeK = gradA.alphaKeys[i].time;
                    if (!keyTimes.Contains(timeK) && keyTimes.Count < 8)
                    {
                        keyTimes.Add(timeK);
                    }
                }
            }

            GradientColorKey[] colorValues = new GradientColorKey[keyTimes.Count];
            GradientAlphaKey[] alphaValues = new GradientAlphaKey[keyTimes.Count];

            for (int i = 0; i < keyTimes.Count; i++)
            {
                float timeKey = keyTimes[i];
                Color lerpedColor = Color.Lerp(gradA.Evaluate(timeKey), gradB.Evaluate(timeKey), lerpTime);
                colorValues[i] = new GradientColorKey(lerpedColor, timeKey);
                alphaValues[i] = new GradientAlphaKey(lerpedColor.a, timeKey);
            }

            if (enableDebug)
            {
                Debug.Log("keysTimes.Count: " + keyTimes.Count);
            }
            outGrad = new Gradient();
            outGrad.SetKeys(colorValues, alphaValues);
            return outGrad;
            //gradA.colorKeys = outGrad.colorKeys;
            //gradA.alphaKeys = outGrad.alphaKeys;
            //gradA.mode = outGrad.mode;
        }

        //v0.4
        public IEnumerator FadeAudio(AudioSource audioSource, float timer, float volume)
        {
            float time = 0;
            float begindVolume = audioSource.volume;
            while (time < timer)
            {
                time = time + Time.deltaTime;
                audioSource.volume = Mathf.Lerp(begindVolume, volume, time / timer);
                yield return null;
            }
            yield break;
        }       

    }

}

