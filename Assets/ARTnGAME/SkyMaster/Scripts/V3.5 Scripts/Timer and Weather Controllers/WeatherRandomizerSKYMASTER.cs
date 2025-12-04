using UnityEngine;
using System.Collections;
//using Artngame.SKYMASTER;

namespace Artngame.SKYMASTER {
public class WeatherRandomizerSKYMASTER : MonoBehaviour {

		SkyMasterManager skyManager;
		WaterHandlerSM waterManager;

		public bool enableGUI = false;
		public bool affectWater = false;
		public bool affectFog = false;
		public bool affectFogParams = false;

        //v0.1
        public FullVolumeCloudsSkyMaster volumeClouds;

        //v0.2
        public CloudScript volumeClouds2SCRIPT;
        public WeatherScript WeatherPatternSCRIPT;
        public CloudScript volumeClouds2SCRIPT_REFL;
        public bool tempBased; //toggles full volume clouds with fly through (Clouds script on camera) and other types (shader, volume clouds without fly through)
        public float humidity = 30;
        public float temperature = 18;
        public float HighHumidity = 55;
        public float highTemperature = 10;
        public bool changedWeather = false;
        public float changedWeatherTime = 0;
        public float weatherChangeDelay = 14;

        // Use this for initialization
        void Start () {
				skyManager = this.GetComponent<SkyMasterManager> ();

				if(skyManager.water != null){
					waterManager = skyManager.water.GetComponent<WaterHandlerSM> ();
					prevFresnelBias = waterManager.FresnelBias;
					prevShoreBlendOffset =waterManager.ShoreBlendOffset;
				}
		//			float hour = System.DateTime.Now.Hour * 3600;
		//			float minutes = System.DateTime.Now.Minute * 60;
		//			float secs = System.DateTime.Now.Second;
		//			float seconds = hour + minutes + secs;

		}	

	
		float change_time;
		public float changeWeatherInterval = 300; //change every 5 minutes
		public string currentWeather;//for debug purposes

		public float cloudDensityChangeSpeed = 1;

		int weatherChoice=-1;

		float prevFresnelBias;
		float prevShoreBlendOffset;

        // Update is called once per frame
        void Update()
        {


            if (Time.fixedTime - change_time > changeWeatherInterval)
            {

                //do change based on randomizer
                weatherChoice = Random.Range(0, 8);//for integers min is included, max exluded (0 up to 6)						

                change_time = Time.fixedTime;
            }

            //NOTE removed +0.1 modifier in shader volume clouds script (v3.4.8) for slower changes in cloud coloration transitions

            float Speed1 = 0.1f;

            if (affectWater && waterManager != null)
            {
                if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.HeavyStorm)
                {
                    waterManager.waterScaleOffset.y = Mathf.Lerp(waterManager.waterScaleOffset.y, 3.5f, Speed1 * Time.deltaTime);
                }
                else
                {
                    waterManager.waterScaleOffset.y = Mathf.Lerp(waterManager.waterScaleOffset.y, 0, Speed1 * Time.deltaTime);
                }


                if (skyManager.currentWeatherName == SkyMasterManager.Volume_Weather_types.SnowStorm)
                {
                    waterManager.FresnelBias = Mathf.Lerp(waterManager.FresnelBias, -67, Speed1 * Time.deltaTime);

                    if (waterManager.FresnelBias < -64)
                    {
                        waterManager.ReflectColor = Color.Lerp(waterManager.ReflectColor, new Color(155f / 255f, 220f / 255f, 220f / 255f), Speed1 * Time.deltaTime);
                        waterManager.UseSkyGradient = false;
                    }
                    waterManager.ShoreBlendOffset = Mathf.Lerp(waterManager.ShoreBlendOffset, 0.1f, Speed1 * Time.deltaTime);
                }
                else
                {
                    waterManager.FresnelBias = Mathf.Lerp(waterManager.FresnelBias, prevFresnelBias, Speed1 * Time.deltaTime);
                    waterManager.UseSkyGradient = true;
                    waterManager.ShoreBlendOffset = Mathf.Lerp(waterManager.ShoreBlendOffset, prevShoreBlendOffset, Speed1 * Time.deltaTime);
                }
            }

            if (affectFog)
            {

                SeasonalTerrainSKYMASTER terrainControl = skyManager.Terrain_controller;
                if (terrainControl == null)
                {
                    terrainControl = skyManager.Mesh_Terrain_controller;
                }

                if (terrainControl != null)
                {

                    terrainControl.UseFogCurves = true;
                    terrainControl.SkyFogOn = true;
                    terrainControl.HeightFogOn = true;

                    if (affectFogParams)
                    {
                        terrainControl.fogGradientDistance = 0; //v3.4.9
                        terrainControl.VFogDistance = 12;
                    }

                    if (weatherChoice == 0)
                    {
                        terrainControl.fogDensity = Mathf.Lerp(terrainControl.fogDensity, 0.1f, Speed1 * Time.deltaTime); //for light rain, light snow
                        terrainControl.AddFogHeightOffset = Mathf.Lerp(terrainControl.AddFogHeightOffset, 700, Speed1 * Time.deltaTime);
                    }
                    if (weatherChoice == 1 || weatherChoice == 5)
                    {
                        terrainControl.fogDensity = Mathf.Lerp(terrainControl.fogDensity, 0.5f, Speed1 * Time.deltaTime); //for  rain,  snow
                        terrainControl.AddFogHeightOffset = Mathf.Lerp(terrainControl.AddFogHeightOffset, 700, Speed1 * Time.deltaTime);
                    }
                    if (weatherChoice == 2 || weatherChoice == 3 || weatherChoice == 4 || weatherChoice == 6)
                    {
                        terrainControl.fogDensity = Mathf.Lerp(terrainControl.fogDensity, 2, Speed1 * Time.deltaTime); //for heavy weather
                        terrainControl.AddFogHeightOffset = Mathf.Lerp(terrainControl.AddFogHeightOffset, 700, Speed1 * Time.deltaTime);
                    }
                }
            }

            float speed = 0.05f * cloudDensityChangeSpeed * Time.deltaTime;
            if (!tempBased)
            {
                if (weatherChoice == 0)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Cloudy;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.85f, speed); // -0.55f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 3, speed);
                    }

                    skyManager.WeatherSeverity = 0;
                    currentWeather = "Sunny";
                }
                if (weatherChoice == 1)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Rain;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.25f, speed * 2); // -0.25f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 14, speed);
                    }

                    skyManager.WeatherSeverity = 4;
                    currentWeather = "Rain";
                }
                if (weatherChoice == 2)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Rain;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.20f, speed * 4.5f); //-0.20f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 20, speed);
                    }

                    skyManager.WeatherSeverity = 10;
                    currentWeather = "Heavy Rain";
                }
                if (weatherChoice == 3)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.HeavyStorm;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.15f, speed * 5); // -0.15f;
                        skyManager.VolShaderCloudsH.StormCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.17f, speed * 5); // -0.17f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 20, speed);
                    }

                    skyManager.WeatherSeverity = 4;
                    currentWeather = "Storm";
                }
                if (weatherChoice == 4)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.HeavyStorm;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.10f, speed * 5); // -0.10f;
                        skyManager.VolShaderCloudsH.StormCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.10f, speed * 5); // -0.10f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 20, speed);
                    }

                    skyManager.WeatherSeverity = 10;
                    currentWeather = "Heavy Storm";
                }
                if (weatherChoice == 5)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.SnowStorm;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.25f, speed * 2); // -0.10f;
                        skyManager.VolShaderCloudsH.StormCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.25f, speed * 2); // -0.10f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 17, speed);
                    }

                    skyManager.WeatherSeverity = 2;
                    currentWeather = "Snow Storm";
                }
                if (weatherChoice == 6)
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.SnowStorm;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.15f, speed); // -0.10f;
                        skyManager.VolShaderCloudsH.StormCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.15f, speed); // -0.10f;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 19, speed);
                    }

                    skyManager.WeatherSeverity = 10;
                    currentWeather = "Heavy Snow Storm";
                }
                if (weatherChoice == 7) //LIGHTNING
                {
                    skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.LightningStorm;
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.ClearDayCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.15f, speed); // -0.10f;
                        skyManager.VolShaderCloudsH.StormCoverage = Mathf.Lerp(skyManager.VolShaderCloudsH.ClearDayCoverage, -0.15f, speed); // -0.10f;
                        skyManager.VolShaderCloudsH.EnableLightning = true;
                    }

                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds._NoiseAmp1 = Mathf.Lerp(volumeClouds._NoiseAmp1, 20, speed);
                        volumeClouds.EnableLightning = true;
                    }

                    skyManager.WeatherSeverity = 10;
                    currentWeather = "Lightning Storm";
                }
                else
                {
                    if (skyManager.VolShaderCloudsH != null)
                    {
                        skyManager.VolShaderCloudsH.EnableLightning = false; //Disable lighning for any other weather type
                    }
                    //v0.1
                    if (volumeClouds != null)
                    {
                        volumeClouds.EnableLightning = false;
                    }
                }
            }



            /////////////// v0.2 FULL VOLUME FLY THROUGH CLOUDS CONTROL ////////////////////////////// TEMPRATURE BASED
            
            //5.0.2
            if (tempBased)
            {
                humidity = UnityEngine.Random.Range(0, 100);//
                temperature = UnityEngine.Random.Range(-20, 50);//

                bool enableCumulus = true;
                bool enableCirrus = false;

                //RESET weather change time
                if (changedWeather && Time.fixedTime - changedWeatherTime > weatherChangeDelay)
                {
                    changedWeather = false;
                }

                //MIDDLE
                if (enableCumulus)
                {                   

                    //lower cumulus, incsrease cirrus
                    volumeClouds2SCRIPT.scale = 0.14f;
                    volumeClouds2SCRIPT.coverage = 0.55f + humidity * 0.008f;// 1.1f;
                    volumeClouds2SCRIPT.coverageHigh = 0;

                    if (temperature < highTemperature) //HEAVY or LIGHT RAIN based on severity
                    {
                        if (!changedWeather && skyManager.currentWeatherName != SkyMasterManager.Volume_Weather_types.Rain)
                        { //change only if not same
                            skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Rain;
                            changedWeather = true;
                            changedWeatherTime = Time.fixedTime;
                            Debug.Log("Changing weather to Heavy or Light Rain");
                        }

                        skyManager.WeatherSeverity = humidity - HighHumidity; //the more humidity, more rain
                    }
                    else
                    {
                        if (!changedWeather && skyManager.currentWeatherName != SkyMasterManager.Volume_Weather_types.Cloudy)
                        {
                            skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Cloudy;
                            changedWeather = true;
                            changedWeatherTime = Time.fixedTime;
                            Debug.Log("Changing weather to Cloudy");
                        }
                        skyManager.WeatherSeverity = 0; //the more humidity, more rain
                    }
                }
                //HIGH
                if (enableCirrus)
                {                   
                    //lower cumulus, incsrease cirrus
                    volumeClouds2SCRIPT.coverage = 0;
                    volumeClouds2SCRIPT.coverageHigh = 3 * (humidity / 100);
                    volumeClouds2SCRIPT.highCloudsScale = 0.008f;

                    //NO RAIN OR SNOW
                    if (!changedWeather && skyManager.currentWeatherName != SkyMasterManager.Volume_Weather_types.Cloudy)
                    {
                        skyManager.currentWeatherName = SkyMasterManager.Volume_Weather_types.Cloudy;
                        changedWeather = true;
                        changedWeatherTime = Time.fixedTime;
                        Debug.Log("Changing weather to Cloudy");
                    }
                    skyManager.WeatherSeverity = 0; //the more humidity, more rain
                }               
            }

            /////////////// END v0.2 ///////////////////

        }

		void OnGUI(){
			if (enableGUI && currentWeather != "") {
				GUI.TextField (new Rect (500+20, 400-205, 400-40, 22), "Random Weather = "+currentWeather +" ,Time to Next:" +(changeWeatherInterval - (Time.fixedTime - change_time)).ToString("F1"));
				//GUI.TextField (new Rect (500, 400+22, 400, 22), "Game Time ="+currentGameTime.ToLongTimeString());
			}
		}
}
}
