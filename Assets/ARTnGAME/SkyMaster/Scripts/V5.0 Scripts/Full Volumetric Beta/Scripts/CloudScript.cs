// https://github.com/Flafla2/Generic-Raymarch-Unity
using UnityEngine;

namespace Artngame.SKYMASTER
{

    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera), typeof(CloudTemporalAntiAliasing))]
    [AddComponentMenu("Effects/Clouds")]
    public class CloudScript : SceneViewFilter
    {
        //v5.4.3
        public bool changeOnHeight = false;//change to clouds above transparent mode
        public float changeAboveTranspHeight = 200;

        //v0.9 - v5.2.1
        public Vector4 specialFX = new Vector4(0, 0, 1, 0);

        //v0.7
        public bool enableEdgeControl = false;
        public int debugClouds = 0;
        //v0.6
        public Vector4 edgeControl = new Vector4(1, 1, 1, 1);//v5.1.1
                                                             //public Vector4 edgeControlP = new Vector4(1, 1, 1, 1);
        public bool autoRegulateEdgeMode = true; //the new edges mode is only for view below clouds, regulate out of the mode if inside or above
        public float autoRegulateCutoffDistance = 1000;//stop new edge blend mode when near or above clouds
        public float controlBackAlphaPower = 1;//2
        public float controlCloudAlphaPower = 0.001f;//1
        float controlBackAlphaPowerP = 1;
        float controlCloudAlphaPowerP = 0.001f;
        public Vector4 controlCloudEdgeA = new Vector4(1, 1, 1, 1);//0.65, 1.22, 1.14, 1.125
        public Vector4 controlCloudEdgeAP = new Vector4(1, 1, 1, 1);
        public float depthDilation = 0;//2204
        float depthDilationP = 0;

        //v0.5
        public bool useCustomTexture = false;
        public Texture2D weatherTexture;

        //v0.4
        public Vector3 _CameraWSOffset = new Vector3(0, 0, 0);

        //v0.3
        public int scatterOn = 1;
        public int sunRaysOn = 0; //v5.3.0
        public float zeroCountSteps = 0;
        public int sunShaftSteps = 5;

        //v0.1
        public int renderInFront = 0;

        public enum RandomJitter
        {
            Off,
            Random,
            BlueNoise
        }

        //v0.2
        //v3.5.3
        public Texture2D _InteractTexture;
        public Vector4 _InteractTexturePos = new Vector4(1, 1, 0, 0);
        public Vector4 _InteractTextureAtr = new Vector4(1, 1, 0, 0);//multipliers and offsets
        public Vector4 _InteractTextureOffset = new Vector4(0, 0, 0, 0); //v4.0


        ///SCATTER
        ///
        [Tooltip("Distance fog is based on radial distance from camera when checked")]
        public bool useRadialDistance = false;
        [Tooltip("Fog top Y coordinate")]
        public float height = 1.0f;
        [Range(0.00001f, 10.0f)]
        public float heightDensity = 2.0f;
        [Tooltip("Push fog away from the camera by this amount")]
        public float startDistance = 0.0f;

        public Light localLight;
        public float localLightFalloff = 2;
        public float localLightIntensity = 1;
        public float currentLocalLightIntensity = 0;

        public float _Scatter = 0.008f;
        public float _HGCoeff = 0.5f;
        public float _Extinct = 0.01f;

        //public float _Exposure = 0.00001f;
        //public float _ExposureUnder = 3f; //v4.0
        //public Vector3 _GroundColor = new Vector3(0.369f, 0.349f, 0.341f);//
        // public float _SunSize = 0.04f;
        public Vector3 _SkyTint = new Vector3(0.5f, 0.5f, 0.5f);
        public float _BackShade = 1.0f;

        public float luminance = 0.8f;
        public float lumFac = 0.9f;
        public float ScatterFac = 24.4f;//scatter factor
        public float TurbFac = 324.74f;//turbidity scale
        public float HorizFac = 1;//sun horizon multiplier
        public float turbidity = 10f;
        public float reileigh = 0.8f;
        public float mieCoefficient = 0.1f;
        public float mieDirectionalG = 0.7f;
        public float bias = 0.6f;
        public float contrast = 4.12f;
        public bool FogSky = false;
        public Vector3 TintColor = new Vector3(68, 155, 345); //68, 155, 345
        public float ClearSkyFac = 1;

        /// <summary>
        /// / END SCATTER
        /// </summary>

        [HeaderAttribute("Vortex")]
        public bool enableVORTEX = false;//public bool debugNoLowFreqNoise = false;
        public Vector3 vortexPosition = new Vector3(0, 0, 0);
        public float vortexRadius = 1000;
        public Vector4 vortexControls = new Vector4(1, 1, 1, 1);

        [HeaderAttribute("Debugging")]
        public bool debugNoLowFreqNoise = false;
        public bool debugNoHighFreqNoise = false;
        public bool debugNoCurlNoise = false;

        [HeaderAttribute("Performance")]
        [Range(1, 2048)] //v5.2.2
        public int steps = 256;//128; v5.3.0
        public bool adjustDensity = true;
        public AnimationCurve stepDensityAdjustmentCurve = new AnimationCurve(new Keyframe(0.0f, 3.019f), new Keyframe(0.25f, 1.233f), new Keyframe(0.5f, 1.0f), new Keyframe(1.0f, 0.892f));
        public bool allowFlyingInClouds = false;
        [Range(1, 8)]
        public int downSample = 2;
        public Texture2D blueNoiseTexture;
        public RandomJitter randomJitterNoise = RandomJitter.BlueNoise;
        public bool temporalAntiAliasing = true;

        [HeaderAttribute("Cloud modeling")]
        public Gradient gradientLow;
        public Gradient gradientMed;
        public Gradient gradientHigh;
        public Texture2D curlNoise;
        public TextAsset lowFreqNoise;
        public TextAsset highFreqNoise;
        public float startHeight = 1500.0f;
        public float thickness = 4000.0f;
        public float planetSize = 35000.0f;
        public Vector3 planetZeroCoordinate = new Vector3(0.0f, 0.0f, 0.0f);
        [Range(0.0f, 1.0f)]
        public float scale = 0.3f;
        [Range(0.0f, 32.0f)]
        public float detailScale = 13.9f;
        [Range(0.0f, 1.0f)]
        public float lowFreqMin = 0.366f;
        [Range(0.0f, 1.0f)]
        public float lowFreqMax = 0.8f;
        [Range(0.0f, 1.0f)]
        public float highFreqModifier = 0.21f;
        [Range(0.0f, 10.0f)]
        public float curlDistortScale = 7.44f;
        [Range(0.0f, 1000.0f)]
        public float curlDistortAmount = 407.0f;
        [Range(0.0f, 1.0f)]
        public float weatherScale = 0.1f;
        [Range(0.0f, 2.0f)]
        public float coverage = 1.06f;
        [Range(0.0f, 2.0f)]
        public float cloudSampleMultiplier = 1.0f;

        [HeaderAttribute("High altitude clouds")]
        public Texture2D cloudsHighTexture;
        [Range(0.0f, 2.0f)]
        public float coverageHigh = 2.0f;//v5.3.0 was 1
        [Range(0.0f, 2.0f)]
        public float highCoverageScale = 1.0f;
        [Range(0.0f, 1.0f)]
        public float highCloudsScale = 0.02f;//v5.3.0 was 0.5

        [HeaderAttribute("Cloud Lighting")]
        public Light sunLight;
        public Color cloudBaseColor = new Color32(199, 220, 255, 0); //v5.3.0
        public Color cloudTopColor = new Color32(255, 255, 255, 255);
        [Range(0.0f, 1.0f)]
        public float ambientLightFactor = 0.551f;
        [Range(0.0f, 5f)]//1.5
        public float sunLightFactor = 3.79f; //v5.3.0
        public Color highSunColor = new Color32(255, 252, 210, 255);
        public Color lowSunColor = new Color32(255, 174, 0, 255);
        [Range(0.0f, 1.0f)]
        public float henyeyGreensteinGForward = 0.4f;
        [Range(0.0f, 1.0f)]
        public float henyeyGreensteinGBackward = 0.179f;
        [Range(0.0f, 200.0f)]
        public float lightStepLength = 64.0f;
        [Range(0.0f, 1.0f)]
        public float lightConeRadius = 0.4f;
        public bool randomUnitSphere = true;
        [Range(0.0f, 4.0f)]
        public float density = 1.0f;
        public bool aLotMoreLightSamples = false;

        [HeaderAttribute("Animating")]
        public float globalMultiplier = 1.0f;
        public float windSpeed = 15.9f;
        public float windDirection = -22.4f;
        public float coverageWindSpeed = 25.0f;
        public float coverageWindDirection = 5.0f;
        public float highCloudsWindSpeed = 49.2f;
        public float highCloudsWindDirection = 77.8f;

        private Vector3 _windOffset;
        private Vector2 _coverageWindOffset;
        private Vector2 _highCloudsWindOffset;
        private Vector3 _windDirectionVector;
        private float _multipliedWindSpeed;

        private Texture3D _cloudShapeTexture;
        private Texture3D _cloudDetailTexture;

        private CloudTemporalAntiAliasing _temporalAntiAliasing;

        public Shader CloudsShader;
        public Shader CloudsBlendShader;

        public Material CloudMaterial
        {
            get
            {
                if (!_CloudMaterial)
                {
                    if (CloudsShader != null)
                    {
                        _CloudMaterial = new Material(CloudsShader);
                    }
                    else
                    {
                        _CloudMaterial = new Material(Shader.Find("Hidden/Clouds"));
                    }
                    _CloudMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return _CloudMaterial;
            }
        }
        private Material _CloudMaterial;

        public Material UpscaleMaterial
        {
            get
            {
                if (!_UpscaleMaterial)
                {
                    if (CloudsBlendShader != null)
                    {
                        _UpscaleMaterial = new Material(CloudsBlendShader);
                    }
                    else
                    {
                        _UpscaleMaterial = new Material(Shader.Find("Hidden/CloudBlender"));
                    }
                    _UpscaleMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return _UpscaleMaterial;
            }
        }
        private Material _UpscaleMaterial;

        void Reset()
        {
            _temporalAntiAliasing = GetComponent<CloudTemporalAntiAliasing>();
            _temporalAntiAliasing.SetCamera(CurrentCamera);
        }

        void Awake()
        {
            Reset();

            //v5.1.0
            if (useCustomTexture)
            {
                CloudMaterial.SetTexture("_WeatherTexture", weatherTexture);
            }
        }

        void Start()
        {
            //v0.6
            controlBackAlphaPowerP = controlBackAlphaPower;
            controlCloudAlphaPowerP = controlCloudAlphaPower;
            depthDilationP = depthDilation;
            //edgeControlP = edgeControl;
            controlCloudEdgeAP = controlCloudEdgeA;


            if (_CloudMaterial)
                DestroyImmediate(_CloudMaterial);
            if (_UpscaleMaterial)
                DestroyImmediate(_UpscaleMaterial);
            _windOffset = new Vector3(0.0f, 0.0f, 0.0f);
            _coverageWindOffset = new Vector3(0.5f / (weatherScale * 0.00025f), 0.5f / (weatherScale * 0.00025f));
            _highCloudsWindOffset = new Vector3(1500.0f, -900.0f);

            //v5.1.0
            if (useCustomTexture)
            {
                CloudMaterial.SetTexture("_WeatherTexture", weatherTexture);
            }
        }

        private void Update()
        {
            //v5.3.0
            if(gradientLow == null)
            {
                createGradient(ref gradientLow);
            }
            if (gradientMed == null)
            {
                createGradient(ref gradientMed);
            }
            if (gradientHigh == null)
            {
                createGradient(ref gradientHigh);
            }
            //v0.9
            if (sunLight == null && !Application.isPlaying)
            {
                //bool assignedLight = false;
                controlWeatherTOD_SM_SRP weathercontroller = transform.gameObject.GetComponent<controlWeatherTOD_SM_SRP>();
                if (weathercontroller != null && weathercontroller.temporalSunMoon != null)
                {
                    Light temporalSunMoon = weathercontroller.temporalSunMoon.GetComponent<Light>();
                    if (temporalSunMoon != null)
                    {
                        sunLight = temporalSunMoon;
                        //sun = temporalSunMoon.transform; //URP
                        //assignedLight = true;
                    }
                }
                if (sunLight == null)
                {
                    //if did not find in controller, check for light with Sun in name
                    Light[] lightsAll = FindObjectsOfType(typeof(Light)) as Light[];
                    foreach (Light light in lightsAll)
                    {
                        if (light.type == LightType.Directional && light.gameObject.name.Contains("Sun") && light.enabled && light.gameObject.activeInHierarchy)
                        {
                            sunLight = light;
                            //sun = light.transform; //URP
                            break;
                        }
                        // light.intensity = 0;
                        //Debug.Log(light);
                    }
                    if (sunLight == null)
                    {
                        //if not found Sun from sky master, try find any other active directional
                        foreach (Light light in lightsAll)
                        {
                            if (light.type == LightType.Directional && light.gameObject.activeInHierarchy)
                            {
                                sunLight = light;
                               // sun = light.transform; //URP
                                break;
                            }
                        }
                        if (sunLight == null)
                        {
                            //if not found, create one
                            GameObject SunLight = new GameObject();
                            SunLight.name = "Sky Master Sun";
                            Light tmpL = SunLight.AddComponent<Light>();
                            sunLight = tmpL;
                            //sun = SunLight.transform;//URP
                        }
                    }
                }
            }

            //v0.6
            //put controlBackAlphaPower = 1, controlCloudAlphaPower = 0.001 if inside or above clouds
            if (autoRegulateEdgeMode && Application.isPlaying)
            { //the new edges mode is only for view below clouds, regulate out of the mode if inside or above
                if (Camera.main.transform.position.y > autoRegulateCutoffDistance)//stop new edge blend mode when near or above clouds
                {
                    controlBackAlphaPower = 1;
                    controlCloudAlphaPower = 0.001f;
                    depthDilation = 0;
                    //edgeControlP = new Vector4(1,1,1,1);
                    //controlCloudEdgeA = new Vector4(1, 1, 1, 1);
                    controlCloudEdgeA = Vector4.Lerp(controlCloudEdgeA, new Vector4(1, 1, 1, 1), Time.deltaTime * 0.4f);
                }
                else
                {
                    controlBackAlphaPower = controlBackAlphaPowerP;
                    controlCloudAlphaPower = controlCloudAlphaPowerP;
                    depthDilation = depthDilationP;
                    //edgeControl = edgeControlP;
                    //controlCloudEdgeA = controlCloudEdgeAP;
                    controlCloudEdgeA = Vector4.Lerp(controlCloudEdgeA, controlCloudEdgeAP, Time.deltaTime * 0.4f);
                }
            }


            // updates wind offsets
            _multipliedWindSpeed = windSpeed * globalMultiplier;
            float angleWind = windDirection * Mathf.Deg2Rad;
            _windDirectionVector = new Vector3(Mathf.Cos(angleWind), -0.25f, Mathf.Sin(angleWind));
            _windOffset += _multipliedWindSpeed * _windDirectionVector * Time.deltaTime;

            float angleCoverage = coverageWindDirection * Mathf.Deg2Rad;
            Vector2 coverageDirecton = new Vector2(Mathf.Cos(angleCoverage), Mathf.Sin(angleCoverage));
            _coverageWindOffset += coverageWindSpeed * globalMultiplier * coverageDirecton * Time.deltaTime;

            float angleHighClodus = highCloudsWindDirection * Mathf.Deg2Rad;
            Vector2 highCloudsDirection = new Vector2(Mathf.Cos(angleHighClodus), Mathf.Sin(angleHighClodus));
            _highCloudsWindOffset += highCloudsWindSpeed * globalMultiplier * highCloudsDirection * Time.deltaTime;
        }

        private void OnDestroy()
        {
            if (_CloudMaterial)
                DestroyImmediate(_CloudMaterial);
        }

        public Camera CurrentCamera
        {
            get
            {
                if (!_CurrentCamera)
                    _CurrentCamera = GetComponent<Camera>();
                return _CurrentCamera;
            }
        }
        public Camera _CurrentCamera;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;

            Matrix4x4 corners = GetFrustumCorners(CurrentCamera);
            Vector3 pos = CurrentCamera.transform.position;

            for (int x = 0; x < 4; x++)
            {
                corners.SetRow(x, CurrentCamera.cameraToWorldMatrix * corners.GetRow(x));
                Gizmos.DrawLine(pos, pos + (Vector3)(corners.GetRow(x)));
            }


            // UNCOMMENT TO DEBUG RAY DIRECTIONS
            Gizmos.color = Color.red;
            int n = 10; // # of intervals
            for (int x = 1; x < n; x++)
            {
                float i_x = (float)x / (float)n;

                var w_top = Vector3.Lerp(corners.GetRow(0), corners.GetRow(1), i_x);
                var w_bot = Vector3.Lerp(corners.GetRow(3), corners.GetRow(2), i_x);
                for (int y = 1; y < n; y++)
                {
                    float i_y = (float)y / (float)n;

                    var w = Vector3.Lerp(w_top, w_bot, i_y).normalized;
                    Gizmos.DrawLine(pos + (Vector3)w, pos + (Vector3)w * 1.2f);
                }
            }

        }

        private Vector4 gradientToVector4(Gradient gradient)
        {
            if (gradient.colorKeys.Length != 4)
            {
                return new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            }
            float x = gradient.colorKeys[0].time;
            float y = gradient.colorKeys[1].time;
            float z = gradient.colorKeys[2].time;
            float w = gradient.colorKeys[3].time;
            return new Vector4(x, y, z, w);
        }

        [ImageEffectOpaque]
        public void OnRenderImage(RenderTexture source, RenderTexture destination)
        {

            //v5.4.3
            if(changeOnHeight && Camera.main != null && Camera.main.transform.position.y > changeAboveTranspHeight)
            {
                Graphics.Blit(source, destination); // do nothing
                return;
            }

            if (CloudMaterial == null || curlNoise == null || blueNoiseTexture == null || lowFreqNoise == null || highFreqNoise == null) // if some script public parameters are missing, do nothing
            {
                Graphics.Blit(source, destination); // do nothing
                return;
            }

            if (_cloudShapeTexture == null) // if shape texture is missing load it in
            {
                _cloudShapeTexture = TGALoader.load3DFromTGASlices(lowFreqNoise);
            }

            if (_cloudDetailTexture == null) // if detail texture is missing load it in
            {
                _cloudDetailTexture = TGALoader.load3DFromTGASlices(highFreqNoise);
            }
            Vector3 cameraPos = CurrentCamera.transform.position;
            // sunLight.rotation.x 364 -> 339, 175 -> 201

            float sunLightFactorUpdated = sunLightFactor;
            float ambientLightFactorUpdated = ambientLightFactor;
            float sunAngle = sunLight.transform.eulerAngles.x;
            Color sunColor = highSunColor;
            float henyeyGreensteinGBackwardLerp = henyeyGreensteinGBackward;

            float noiseScale = 0.00001f + scale * 0.0004f;

            if (sunAngle > 170.0f) // change sunlight color based on sun's height.
            {
                float gradient = Mathf.Max(0.0f, (sunAngle - 330.0f) / 30.0f);
                float gradient2 = gradient * gradient;
                sunLightFactorUpdated *= gradient;
                ambientLightFactorUpdated *= gradient;
                henyeyGreensteinGBackwardLerp *= gradient2 * gradient;
                ambientLightFactorUpdated = Mathf.Max(0.02f, ambientLightFactorUpdated);
                sunColor = Color.Lerp(lowSunColor, highSunColor, gradient2);
            }

            updateMaterialKeyword(enableVORTEX, "DEBUG_NO_LOW_FREQ_NOISE");//updateMaterialKeyword(debugNoLowFreqNoise, "DEBUG_NO_LOW_FREQ_NOISE");
            //v0.7
            CloudMaterial.SetVector("vortexPosRadius", new Vector4(vortexPosition.x, vortexPosition.y, vortexPosition.z, vortexRadius));
            CloudMaterial.SetVector("vortexControlsA", vortexControls);

            updateMaterialKeyword(debugNoHighFreqNoise, "DEBUG_NO_HIGH_FREQ_NOISE");
            updateMaterialKeyword(debugNoCurlNoise, "DEBUG_NO_CURL");
            updateMaterialKeyword(allowFlyingInClouds, "ALLOW_IN_CLOUDS");
            updateMaterialKeyword(randomUnitSphere, "RANDOM_UNIT_SPHERE");
            updateMaterialKeyword(aLotMoreLightSamples, "SLOW_LIGHTING");

            switch (randomJitterNoise)
            {
                case RandomJitter.Off:
                    updateMaterialKeyword(false, "RANDOM_JITTER_WHITE");
                    updateMaterialKeyword(false, "RANDOM_JITTER_BLUE");
                    break;
                case RandomJitter.Random:
                    updateMaterialKeyword(true, "RANDOM_JITTER_WHITE");
                    updateMaterialKeyword(false, "RANDOM_JITTER_BLUE");
                    break;
                case RandomJitter.BlueNoise:
                    updateMaterialKeyword(false, "RANDOM_JITTER_WHITE");
                    updateMaterialKeyword(true, "RANDOM_JITTER_BLUE");
                    break;
            }

            // send uniforms to shader
            CloudMaterial.SetVector("_SunDir", sunLight.transform ? (-sunLight.transform.forward).normalized : Vector3.up);
            CloudMaterial.SetVector("_PlanetCenter", planetZeroCoordinate - new Vector3(0, planetSize, 0));
            CloudMaterial.SetVector("_ZeroPoint", planetZeroCoordinate);
            CloudMaterial.SetColor("_SunColor", sunColor);
            //CloudMaterial.SetColor("_SunColor", sunLight.color);

            CloudMaterial.SetColor("_CloudBaseColor", cloudBaseColor);
            CloudMaterial.SetColor("_CloudTopColor", cloudTopColor);
            CloudMaterial.SetFloat("_AmbientLightFactor", ambientLightFactorUpdated);
            CloudMaterial.SetFloat("_SunLightFactor", sunLightFactorUpdated);
            //CloudMaterial.SetFloat("_AmbientLightFactor", sunLight.intensity * ambientLightFactor * 0.3f);
            //CloudMaterial.SetFloat("_SunLightFactor", sunLight.intensity * sunLightFactor);

            CloudMaterial.SetTexture("_ShapeTexture", _cloudShapeTexture);
            CloudMaterial.SetTexture("_DetailTexture", _cloudDetailTexture);
            CloudMaterial.SetTexture("_CurlNoise", curlNoise);
            CloudMaterial.SetTexture("_BlueNoise", blueNoiseTexture);
            CloudMaterial.SetVector("_Randomness", new Vector4(Random.value, Random.value, Random.value, Random.value));
            CloudMaterial.SetTexture("_AltoClouds", cloudsHighTexture);

            CloudMaterial.SetFloat("_CoverageHigh", 1.0f - coverageHigh);
            CloudMaterial.SetFloat("_CoverageHighScale", highCoverageScale * weatherScale * 0.001f);
            CloudMaterial.SetFloat("_HighCloudsScale", highCloudsScale * 0.002f);

            CloudMaterial.SetFloat("_CurlDistortAmount", 150.0f + curlDistortAmount);
            CloudMaterial.SetFloat("_CurlDistortScale", curlDistortScale * noiseScale);

            CloudMaterial.SetFloat("_LightConeRadius", lightConeRadius);
            CloudMaterial.SetFloat("_LightStepLength", lightStepLength);
            CloudMaterial.SetFloat("_SphereSize", planetSize);
            CloudMaterial.SetVector("_CloudHeightMinMax", new Vector2(startHeight, startHeight + thickness));
            CloudMaterial.SetFloat("_Thickness", thickness);
            CloudMaterial.SetFloat("_Scale", noiseScale);
            CloudMaterial.SetFloat("_DetailScale", detailScale * noiseScale);
            CloudMaterial.SetVector("_LowFreqMinMax", new Vector4(lowFreqMin, lowFreqMax));
            CloudMaterial.SetFloat("_HighFreqModifier", highFreqModifier);
            CloudMaterial.SetFloat("_WeatherScale", weatherScale * 0.00025f);
            CloudMaterial.SetFloat("_Coverage", 1.0f - coverage);
            CloudMaterial.SetFloat("_HenyeyGreensteinGForward", henyeyGreensteinGForward);
            CloudMaterial.SetFloat("_HenyeyGreensteinGBackward", -henyeyGreensteinGBackwardLerp);
            if (adjustDensity)
            {
                CloudMaterial.SetFloat("_SampleMultiplier", cloudSampleMultiplier * stepDensityAdjustmentCurve.Evaluate(steps / 256.0f));
            }
            else
            {
                CloudMaterial.SetFloat("_SampleMultiplier", cloudSampleMultiplier);
            }


            CloudMaterial.SetFloat("_Density", density);

            CloudMaterial.SetFloat("_WindSpeed", _multipliedWindSpeed);
            CloudMaterial.SetVector("_WindDirection", _windDirectionVector);
            CloudMaterial.SetVector("_WindOffset", _windOffset);
            CloudMaterial.SetVector("_CoverageWindOffset", _coverageWindOffset);
            CloudMaterial.SetVector("_HighCloudsWindOffset", _highCloudsWindOffset);

            //v5.3.0
            if (gradientLow == null)
            {
                createGradient(ref gradientLow);
            }
            if (gradientMed == null)
            {
                createGradient(ref gradientMed);
            }
            if (gradientHigh == null)
            {
                createGradient(ref gradientHigh);
            }

            CloudMaterial.SetVector("_Gradient1", gradientToVector4(gradientLow));
            CloudMaterial.SetVector("_Gradient2", gradientToVector4(gradientMed));
            CloudMaterial.SetVector("_Gradient3", gradientToVector4(gradientHigh));

            CloudMaterial.SetInt("_Steps", steps);
            CloudMaterial.SetInt("_renderInFront", renderInFront);//v0.1 choose to render in front of objects for reflections

            CloudMaterial.SetMatrix("_FrustumCornersES", GetFrustumCorners(CurrentCamera));


            //v5.2.2
            if (CurrentCamera.stereoEnabled)
            {
                left_world_from_view = CurrentCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                right_world_from_view = CurrentCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;

                // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                left_screen_from_view = CurrentCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                right_screen_from_view = CurrentCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;
                right_view_from_screen = GL.GetGPUProjectionMatrix(right_screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                left_view_from_screen[1, 1] *= -1;
                right_view_from_screen[1, 1] *= -1;

                // Store matrices
                CloudMaterial.SetMatrix("_LeftWorldFromView", left_world_from_view);
                CloudMaterial.SetMatrix("_RightWorldFromView", right_world_from_view);
                CloudMaterial.SetMatrix("_LeftViewFromScreen", left_view_from_screen);
                CloudMaterial.SetMatrix("_RightViewFromScreen", right_view_from_screen);

            }
            else
            {
                // Main eye inverse view matrix
                left_world_from_view = CurrentCamera.cameraToWorldMatrix;
                left_screen_from_view = CurrentCamera.projectionMatrix;
                left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                left_view_from_screen[1, 1] *= -1;

                // Store matrices
                CloudMaterial.SetMatrix("_LeftWorldFromView", left_world_from_view);
                CloudMaterial.SetMatrix("_LeftViewFromScreen", left_view_from_screen);
            }


            CloudMaterial.SetMatrix("_CameraInvViewMatrix", CurrentCamera.cameraToWorldMatrix);
            CloudMaterial.SetVector("_CameraWS", cameraPos);
            CloudMaterial.SetVector("_CameraWSOffset", _CameraWSOffset);
            CloudMaterial.SetFloat("_FarPlane", CurrentCamera.farClipPlane);



            //v0.2
            //v3.5.3			
            CloudMaterial.SetTexture("_InteractTexture", _InteractTexture);
            CloudMaterial.SetVector("_InteractTexturePos", _InteractTexturePos);
            CloudMaterial.SetVector("_InteractTextureAtr", _InteractTextureAtr);
            CloudMaterial.SetVector("_InteractTextureOffset", _InteractTextureOffset); //v4.0


            //////// SCATTER
            //Camera cam = GetComponent<Camera>(); //v2.1.15
            //Transform camtr = cam.transform;
            //float camNear = cam.nearClipPlane;
            //float camFar = cam.farClipPlane;
            //float camFov = cam.fieldOfView;
            //float camAspect = cam.aspect;

            //Matrix4x4 frustumCorners = Matrix4x4.identity;

            //float fovWHalf = camFov * 0.5f;

            //Vector3 toRight = camtr.right * camNear * Mathf.Tan(fovWHalf * Mathf.Deg2Rad) * camAspect;
            //Vector3 toTop = camtr.up * camNear * Mathf.Tan(fovWHalf * Mathf.Deg2Rad);

            //Vector3 topLeft = (camtr.forward * camNear - toRight + toTop);
            //float camScale = topLeft.magnitude * camFar / camNear;

            //topLeft.Normalize();
            //topLeft *= camScale;

            //Vector3 topRight = (camtr.forward * camNear + toRight + toTop);
            //topRight.Normalize();
            //topRight *= camScale;

            //Vector3 bottomRight = (camtr.forward * camNear + toRight - toTop);
            //bottomRight.Normalize();
            //bottomRight *= camScale;

            //Vector3 bottomLeft = (camtr.forward * camNear - toRight - toTop);
            //bottomLeft.Normalize();
            //bottomLeft *= camScale;

            //frustumCorners.SetRow(0, topLeft);
            //frustumCorners.SetRow(1, topRight);
            //frustumCorners.SetRow(2, bottomRight);
            //frustumCorners.SetRow(3, bottomLeft);

            CloudMaterial.SetInt("scatterOn", scatterOn);//v0.3
            CloudMaterial.SetInt("sunRaysOn", sunRaysOn);//v0.3
            CloudMaterial.SetFloat("zeroCountSteps", zeroCountSteps);//v0.3
            CloudMaterial.SetInt("sunShaftSteps", sunShaftSteps);//v0.3

            //var camPos = camtr.position;
            float FdotC = CurrentCamera.transform.position.y - height;
            float paramK = (FdotC <= 0.0f ? 1.0f : 0.0f);
            //fogMaterial.SetMatrix("_FrustumCornersWS", frustumCorners);
            //fogMaterial.SetVector("_CameraWS", camPos);
            CloudMaterial.SetVector("_HeightParams", new Vector4(height, FdotC, paramK, heightDensity * 0.5f));
            CloudMaterial.SetVector("_DistanceParams", new Vector4(-Mathf.Max(startDistance, 0.0f), 0, 0, 0));


            //v3.5.1
            //fogMaterial.SetFloat("_NearZCutoff", _NearZCutoff);
            //fogMaterial.SetFloat("_HorizonYAdjust", _HorizonYAdjust);
            //fogMaterial.SetFloat("_HorizonZAdjust", _HorizonZAdjust);//v2.1.24
            //fogMaterial.SetFloat("_FadeThreshold", _FadeThreshold);

            //v3.5
            //fogMaterial.SetFloat("_SampleCount0", _SampleCount0);
            //fogMaterial.SetFloat("_SampleCount1", _SampleCount1);
            //fogMaterial.SetInt("_SampleCountL", _SampleCountL);

            //fogMaterial.SetFloat("_NoiseFreq1", _NoiseFreq1);
            //fogMaterial.SetFloat("_NoiseFreq2", _NoiseFreq2);
            //fogMaterial.SetFloat("_NoiseAmp1", _NoiseAmp1);
            //fogMaterial.SetFloat("_NoiseAmp2", _NoiseAmp2);
            //fogMaterial.SetFloat("_NoiseBias", _NoiseBias);

            //fogMaterial.SetVector("_Scroll1", _Scroll1);
            //fogMaterial.SetVector("_Scroll2", _Scroll2);

            //fogMaterial.SetFloat("_Altitude0", _Altitude0);
            //fogMaterial.SetFloat("_Altitude1", _Altitude1);
            //fogMaterial.SetFloat("_FarDist", _FarDist);

            CloudMaterial.SetFloat("_Scatter", _Scatter);
            CloudMaterial.SetFloat("_HGCoeff", _HGCoeff);
            CloudMaterial.SetFloat("_Extinct", _Extinct);


            // CloudMaterial.SetFloat("_Exposure", _ExposureUnder); //v4.0
            //  CloudMaterial.SetVector("_GroundColor", _GroundColor);//
            //  CloudMaterial.SetFloat("_SunSize", _SunSize);
            CloudMaterial.SetVector("_SkyTint", _SkyTint);
            //fogMaterial.SetFloat("_AtmosphereThickness", _AtmosphereThickness);
            //v3.5
            CloudMaterial.SetFloat("_BackShade", _BackShade);
            //fogMaterial.SetFloat("_UndersideCurveFactor", _UndersideCurveFactor);

            //v2.1.19
            if (localLight != null)
            {
                Vector3 localLightPos = localLight.transform.position;
                //float intensity = Mathf.Pow(10, 3 + (localLightFalloff - 3) * 3);
                currentLocalLightIntensity = Mathf.Pow(10, 3 + (localLightFalloff - 3) * 3);
                //fogMaterial.SetVector ("_LocalLightPos", new Vector4 (localLightPos.x, localLightPos.y, localLightPos.z, localLight.intensity * localLightIntensity * intensity));
                CloudMaterial.SetVector("_LocalLightPos", new Vector4(localLightPos.x, localLightPos.y, localLightPos.z, localLight.intensity * localLightIntensity * currentLocalLightIntensity));
                CloudMaterial.SetVector("_LocalLightColor", new Vector4(localLight.color.r, localLight.color.g, localLight.color.b, localLightFalloff));
            }
            else
            {
                if (currentLocalLightIntensity > 0)
                {
                    currentLocalLightIntensity = 0;
                    //fogMaterial.SetVector ("_LocalLightPos", new Vector4 (localLightPos.x, localLightPos.y, localLightPos.z, localLight.intensity * localLightIntensity * intensity));
                    CloudMaterial.SetVector("_LocalLightColor", Vector4.zero);
                }
            }

            //SM v1.7
            CloudMaterial.SetFloat("luminance", luminance);
            CloudMaterial.SetFloat("lumFac", lumFac);
            CloudMaterial.SetFloat("Multiplier1", ScatterFac);
            CloudMaterial.SetFloat("Multiplier2", TurbFac);
            CloudMaterial.SetFloat("Multiplier3", HorizFac);
            CloudMaterial.SetFloat("turbidity", turbidity);
            CloudMaterial.SetFloat("reileigh", reileigh);
            CloudMaterial.SetFloat("mieCoefficient", mieCoefficient);
            CloudMaterial.SetFloat("mieDirectionalG", mieDirectionalG);
            CloudMaterial.SetFloat("bias", bias);
            CloudMaterial.SetFloat("contrast", contrast);
            CloudMaterial.SetVector("v3LightDir", -sunLight.transform.forward);
            CloudMaterial.SetVector("_TintColor", new Vector4(TintColor.x, TintColor.y, TintColor.z, 1));//68, 155, 345

            float Foggy = 0;
            if (FogSky)
            {
                Foggy = 1;
            }
            CloudMaterial.SetFloat("FogSky", Foggy);
            CloudMaterial.SetFloat("ClearSkyFac", ClearSkyFac);

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
            CloudMaterial.SetVector("_SceneFogParams", sceneParams);
            CloudMaterial.SetVector("_SceneFogMode", new Vector4((int)sceneMode, useRadialDistance ? 1 : 0, 0, 0));


            ////////// END SCATTER

            //v0.9 - v5.2.1
            CloudMaterial.SetVector("specialFX", specialFX);

            //v0.6
            UpscaleMaterial.SetFloat("controlBackAlphaPower", controlBackAlphaPower);
            UpscaleMaterial.SetFloat("controlCloudAlphaPower", controlCloudAlphaPower);
            UpscaleMaterial.SetVector("controlCloudEdgeA", controlCloudEdgeA);
            CloudMaterial.SetFloat("depthDilation", depthDilation);
            UpscaleMaterial.SetFloat("depthDilation", depthDilation);

            // get cloud render texture and render clouds to it
            RenderTexture rtClouds = RenderTexture.GetTemporary((int)(source.width / ((float)downSample)), (int)(source.height / ((float)downSample)), 0, source.format, RenderTextureReadWrite.Default, source.antiAliasing);
            CustomGraphicsBlit(source, rtClouds, CloudMaterial, 0);

            //v5.1.1
            if (enableEdgeControl)
            {
                //UpscaleMaterial.SetTexture("_Clouds", rtClouds);
                UpscaleMaterial.SetVector("edgeControl", edgeControl);
                //UpscaleMaterial.SetInt("enableEdges", 1);
                if (debugClouds > 0)
                {
                    UpscaleMaterial.SetInt("enableEdges", debugClouds - 1);
                }
                else
                {
                    UpscaleMaterial.SetInt("enableEdges", 1);
                }
                RenderTexture rtTemporalA = RenderTexture.GetTemporary(rtClouds.width, rtClouds.height, 0, rtClouds.format, RenderTextureReadWrite.Default);
                //v5.1.1
                // CustomGraphicsBlit(rtClouds, rtTemporalA, UpscaleMaterial, 1);
                Graphics.Blit(rtClouds, rtTemporalA, UpscaleMaterial, 1);

                if (temporalAntiAliasing && Application.isPlaying) // if TAA is enabled, then apply it to cloud render texture //v5.1.0
                {
                    RenderTexture rtTemporal = RenderTexture.GetTemporary(rtClouds.width, rtClouds.height, 0, rtClouds.format, RenderTextureReadWrite.Default, source.antiAliasing);
                    _temporalAntiAliasing.TemporalAntiAliasing(rtTemporalA, rtTemporal);
                    UpscaleMaterial.SetTexture("_Clouds", rtTemporal);

                    // Apply clouds to background
                    if (debugClouds > 0)
                    {
                        Graphics.Blit(rtTemporalA, destination, UpscaleMaterial, 0);
                    }
                    else
                    {
                        Graphics.Blit(source, destination, UpscaleMaterial, 0);
                    }

                    RenderTexture.ReleaseTemporary(rtTemporal);
                }
                else
                {
                    UpscaleMaterial.SetTexture("_Clouds", rtTemporalA); //

                    // Apply clouds to background
                    if (debugClouds > 0)
                    {
                        Graphics.Blit(rtTemporalA, destination, UpscaleMaterial, 0);
                    }
                    else
                    {
                        Graphics.Blit(source, destination, UpscaleMaterial, 0);
                    }
                }

                RenderTexture.ReleaseTemporary(rtClouds); RenderTexture.ReleaseTemporary(rtTemporalA);
            }
            else
            {
                UpscaleMaterial.SetInt("enableEdges", 0);
                if (temporalAntiAliasing && Application.isPlaying) // if TAA is enabled, then apply it to cloud render texture //v5.1.0
                {
                    RenderTexture rtTemporal = RenderTexture.GetTemporary(rtClouds.width, rtClouds.height, 0, rtClouds.format, RenderTextureReadWrite.Default, source.antiAliasing);

                    _temporalAntiAliasing.TemporalAntiAliasing(rtClouds, rtTemporal);
                    UpscaleMaterial.SetTexture("_Clouds", rtTemporal);
                    RenderTexture.ReleaseTemporary(rtTemporal);
                }
                else
                {
                    UpscaleMaterial.SetTexture("_Clouds", rtClouds); //
                }
                // Apply clouds to background
                Graphics.Blit(source, destination, UpscaleMaterial, 0);
                RenderTexture.ReleaseTemporary(rtClouds);
            }

        }

        private void updateMaterialKeyword(bool b, string keyword)
        {
            if (b != CloudMaterial.IsKeywordEnabled(keyword))
            {
                if (b)
                {
                    CloudMaterial.EnableKeyword(keyword);
                }
                else
                {
                    CloudMaterial.DisableKeyword(keyword);
                }
            }
        }

        //v5.2.2
        Matrix4x4 left_world_from_view;
        Matrix4x4 right_world_from_view;
        // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
        Matrix4x4 left_screen_from_view;
        Matrix4x4 right_screen_from_view;
        Matrix4x4 left_view_from_screen;
        Matrix4x4 right_view_from_screen;

        /// \brief Stores the normalized rays representing the camera frustum in a 4x4 matrix.  Each row is a vector.
        /// 
        /// The following rays are stored in each row (in eyespace, not worldspace):
        /// Top Left corner:     row=0
        /// Top Right corner:    row=1
        /// Bottom Right corner: row=2
        /// Bottom Left corner:  row=3
        private Matrix4x4 GetFrustumCorners(Camera cam)
        {
            float camFov = cam.fieldOfView;
            float camAspect = cam.aspect;

            Matrix4x4 frustumCorners = Matrix4x4.identity;

            float fovWHalf = camFov * 0.5f;

            float tan_fov = Mathf.Tan(fovWHalf * Mathf.Deg2Rad);

            Vector3 toRight = Vector3.right * tan_fov * camAspect;
            Vector3 toTop = Vector3.up * tan_fov;

            Vector3 topLeft = (-Vector3.forward - toRight + toTop);
            Vector3 topRight = (-Vector3.forward + toRight + toTop);
            Vector3 bottomRight = (-Vector3.forward + toRight - toTop);
            Vector3 bottomLeft = (-Vector3.forward - toRight - toTop);

            frustumCorners.SetRow(0, topLeft);
            frustumCorners.SetRow(1, topRight);
            frustumCorners.SetRow(2, bottomRight);
            frustumCorners.SetRow(3, bottomLeft);

            return frustumCorners;
        }

        /// \brief Custom version of Graphics.Blit that encodes frustum corner indices into the input vertices.
        /// 
        /// In a shader you can expect the following frustum cornder index information to get passed to the z coordinate:
        /// Top Left vertex:     z=0, u=0, v=0
        /// Top Right vertex:    z=1, u=1, v=0
        /// Bottom Right vertex: z=2, u=1, v=1
        /// Bottom Left vertex:  z=3, u=1, v=0
        /// 
        /// \warning You may need to account for flipped UVs on DirectX machines due to differing UV semantics
        ///          between OpenGL and DirectX.  Use the shader define UNITY_UV_STARTS_AT_TOP to account for this.
        static void CustomGraphicsBlit(RenderTexture source, RenderTexture dest, Material fxMaterial, int passNr)
        {
            RenderTexture.active = dest;

            //fxMaterial.SetTexture("_MainTex", source);

            GL.PushMatrix();
            GL.LoadOrtho(); // Note: z value of vertices don't make a difference because we are using ortho projection

            fxMaterial.SetPass(passNr);

            GL.Begin(GL.QUADS);

            // Here, GL.MultitexCoord2(0, x, y) assigns the value (x, y) to the TEXCOORD0 slot in the shader.
            // GL.Vertex3(x,y,z) queues up a vertex at position (x, y, z) to be drawn.  Note that we are storing
            // our own custom frustum information in the z coordinate.
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

        //v5.3.0
        GradientColorKey[] colorKey;
        GradientAlphaKey[] alphaKey;
        void createGradient(ref Gradient gradient)
        {
            gradient = new Gradient();

            // Populate the color keys at the relative time 0 and 1 (0 and 100%)
            colorKey = new GradientColorKey[4];
            colorKey[0].color = Color.black;
            colorKey[0].time = 0.0f;
            colorKey[1].color = Color.white;
            colorKey[1].time = 0.1f;
            colorKey[2].color = Color.white;
            colorKey[2].time = 0.9f;
            colorKey[3].color = Color.black;
            colorKey[3].time = 1.0f;

            // Populate the alpha  keys at relative time 0 and 1  (0 and 100%)
            alphaKey = new GradientAlphaKey[4];
            alphaKey[0].alpha = 1.0f;
            alphaKey[0].time = 0.0f;
            alphaKey[1].alpha = 1.0f;
            alphaKey[1].time = 0.1f;
            alphaKey[2].alpha = 1.0f;
            alphaKey[2].time = 0.9f;
            alphaKey[3].alpha = 1.0f;
            alphaKey[3].time = 1.0f;

            gradient.SetKeys(colorKey, alphaKey);

            // What's the color at the relative time 0.25 (25 %) ?
            //Debug.Log(gradient.Evaluate(0.25f));
        }
    }
}