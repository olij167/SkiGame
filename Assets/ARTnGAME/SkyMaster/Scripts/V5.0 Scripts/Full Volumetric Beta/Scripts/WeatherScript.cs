using UnityEngine;
using System.Collections;

namespace Artngame.SKYMASTER
{

    [ExecuteInEditMode]
    public class WeatherScript : MonoBehaviour
    {
        //v0.1
        public bool useRandSeed = false;
        public int randomSeed = 1;

        public CloudScript clouds;
        public CloudScript cloudsREFL; //reflections
        public int size = 512;
        public bool useCustomTexture = false;
        public Texture2D customWeatherTexture;
        public GameObject weatherVisualiser;
        public float blendTime = 30f;

        private MeshRenderer weatherVisualiserRenderer;

        public RenderTexture rt; // weather texture at the moment //v5.1.0
        private bool isChangingWeather = false;

        private RenderTexture prevWeatherTexture; // previous weather texture
        private RenderTexture nextWeatherTexture; // next weather texture

        private bool _useUserWeatherTexture;

        public bool useCustomWeatherTexture
        {
            get { return _useUserWeatherTexture; }
            set
            {
                if (value == _useUserWeatherTexture)
                    return;

                _useUserWeatherTexture = value;
                if (_useUserWeatherTexture && customWeatherTexture != null)
                {
                    //Debug.Log("Lerp1");
                    Graphics.Blit(rt, prevWeatherTexture);
                    Graphics.Blit(customWeatherTexture, nextWeatherTexture);
                    StartWeatherTextureChange();
                }
                else
                {
                    //Debug.Log("Lerp2");
                    GenerateAndChangeWeatherTexture();
                }
            }
        }

        public Material BlendMaterial
        {
            get
            {
                if (!_BlendMaterial)
                {
                    _BlendMaterial = new Material(Shader.Find("Hidden/WeatherBlender"));
                    _BlendMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return _BlendMaterial;
            }
        }
        private Material _BlendMaterial;

        public Material SystemMaterial
        {
            get
            {
                if (!_SystemMaterial)
                {
                    _SystemMaterial = new Material(Shader.Find("Hidden/WeatherSystem"));
                    _SystemMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return _SystemMaterial;
            }
        }
        private Material _SystemMaterial;

        public void Awake()
        {
            if (weatherVisualiser != null)
            {
                weatherVisualiserRenderer = weatherVisualiser.GetComponent<MeshRenderer>();
            }
        }

        // sets weather textures on clouds and visualiser object
        private void setWeatherTexture()
        {
            clouds.CloudMaterial.SetTexture("_WeatherTexture", rt);
            //v5.0
            if (cloudsREFL != null)
            {
                cloudsREFL.CloudMaterial.SetTexture("_WeatherTexture", rt);
            }
            if (weatherVisualiser != null)
            {
                weatherVisualiserRenderer.sharedMaterial.SetTexture("_MainTex", rt);
            }

        }

        // starts weather texture change routine
        public void StartWeatherTextureChange()
        {
            if (isChangingWeather)
            {
                StopCoroutine("LerpWeatherTexture");
            }
            StartCoroutine("LerpWeatherTexture");
        }

        // generates new weather texture
        public void GenerateWeatherTexture()
        {
            //v0.1
            if (useRandSeed)
            {
                //Random.seed = randomSeed;
                Random.InitState(randomSeed);
            }

            SystemMaterial.SetVector("_Randomness", new Vector3(Random.Range(-1000, 1000), Random.Range(-1000, 1000), Random.value * 1.5f - 0.2f));
            Graphics.Blit(rt, prevWeatherTexture);
            Graphics.Blit(null, rt, SystemMaterial, 0);
            Graphics.Blit(rt, nextWeatherTexture);
        }

        //v0.1
        bool setToReset = false;
        private void LateUpdate()
        {
            if (setToReset && !useCustomTexture)
            {
                GenerateAndChangeWeatherTexture();
                setToReset = false;
            }
            if (setToReset && useCustomTexture)
            {
                GenerateAndChangeWeatherTexture();
                GenerateWeatherTexture();
                
                _useUserWeatherTexture = true;
                Graphics.Blit(customWeatherTexture, prevWeatherTexture);
                Graphics.Blit(prevWeatherTexture, rt);
                //Debug.Log("AA");
                setToReset = false;
            }
        }
        private void OnEnable()
        {
            if (!Application.isPlaying && !useCustomTexture)
            {
                setToReset = true;
                //Graphics.Blit(rt, prevWeatherTexture);
                //Graphics.Blit(customWeatherTexture, nextWeatherTexture);
                //StartWeatherTextureChange();
                //BlendMaterial.SetTexture("_PrevWeather", prevWeatherTexture);
                //BlendMaterial.SetTexture("_NextWeather", nextWeatherTexture);
                //BlendMaterial.SetFloat("_Alpha", 0 / blendTime);
                //Graphics.Blit(null, rt, BlendMaterial, 0);
                //setWeatherTexture();
            }
            //v0.1
            if (!Application.isPlaying && useCustomTexture)
            {
                setToReset = true;
                //Graphics.Blit(rt, prevWeatherTexture);
                //Graphics.Blit(customWeatherTexture, nextWeatherTexture);
                //StartWeatherTextureChange();
                //BlendMaterial.SetTexture("_PrevWeather", prevWeatherTexture);
                //BlendMaterial.SetTexture("_NextWeather", nextWeatherTexture);
                //BlendMaterial.SetFloat("_Alpha", 0 / blendTime);
                //Graphics.Blit(null, rt, BlendMaterial, 0);
                //setWeatherTexture();
                //Debug.Log("AA");
            }

            if (1==0 && !Application.isPlaying)
            {
                //GenerateWeatherTexture();
                Debug.Log("Enabled");
                //Graphics.Blit(nextWeatherTexture, prevWeatherTexture);
                //Graphics.Blit(nextWeatherTexture, rt);
                //setWeatherTexture();
                //StartWeatherTextureChange();
                Graphics.Blit(rt, prevWeatherTexture);
                Graphics.Blit(customWeatherTexture, nextWeatherTexture);

                // StartWeatherTextureChange();
                BlendMaterial.SetTexture("_PrevWeather", prevWeatherTexture);
                BlendMaterial.SetTexture("_NextWeather", nextWeatherTexture);
                BlendMaterial.SetFloat("_Alpha", 1);
                Graphics.Blit(null, rt, BlendMaterial, 0);
                setWeatherTexture();

                //GenerateAndChangeWeatherTexture();
                useCustomWeatherTexture = false;
                //GenerateWeatherTexture();
                //Graphics.Blit(nextWeatherTexture, prevWeatherTexture);
                //Graphics.Blit(nextWeatherTexture, rt);
                //setWeatherTexture();
            }
        }

        // calls StartWeatherTextureChange() and GenerateWeatherTexture()
        public void GenerateAndChangeWeatherTexture()
        {
            GenerateWeatherTexture();
            if (!useCustomWeatherTexture)
            { 
                //v0.1
                if (!Application.isPlaying)
                {
                    Graphics.Blit(nextWeatherTexture, prevWeatherTexture);
                    Graphics.Blit( nextWeatherTexture, rt);
                    setWeatherTexture();
                }
                else
                {
                    StartWeatherTextureChange();
                }
            }

            //v0.1
            if ( useCustomWeatherTexture)
            {
                _useUserWeatherTexture = true;
                Graphics.Blit(customWeatherTexture, prevWeatherTexture);
                Graphics.Blit( prevWeatherTexture, rt);
                //StartWeatherTextureChange();
                //GenerateWeatherTexture();
               // StartWeatherTextureChange();
                setWeatherTexture();
            }
        }

        // lerps between previous and next weather texture
        IEnumerator LerpWeatherTexture()
        {
            isChangingWeather = true;
            for (float t = 0f; t <= blendTime; t += Time.deltaTime * (clouds.globalMultiplier == 0.0 ? blendTime : Mathf.Abs(clouds.globalMultiplier)))
            {
                BlendMaterial.SetTexture("_PrevWeather", prevWeatherTexture);
                BlendMaterial.SetTexture("_NextWeather", nextWeatherTexture);
                BlendMaterial.SetFloat("_Alpha", t / blendTime);
                Graphics.Blit(null, rt, BlendMaterial, 0);
                setWeatherTexture();
                //Debug.Log("Lerp A " + t);
                yield return null;
            }
            //Debug.Log("Lerp B ");
            Graphics.Blit(nextWeatherTexture, rt);
            setWeatherTexture();
            isChangingWeather = false;
        }

        void Start()
        {
            if (_BlendMaterial)
                DestroyImmediate(_BlendMaterial);
            if (_SystemMaterial)
                DestroyImmediate(_SystemMaterial);

            rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            rt.wrapMode = TextureWrapMode.Mirror;
            rt.Create();

            prevWeatherTexture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            prevWeatherTexture.wrapMode = TextureWrapMode.Mirror;
            prevWeatherTexture.Create();
            nextWeatherTexture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            nextWeatherTexture.wrapMode = TextureWrapMode.Mirror;
            nextWeatherTexture.Create();

            clouds.CloudMaterial.SetTexture("_WeatherTexture", rt);

            //v5.0
            if (cloudsREFL != null)
            {
                cloudsREFL.CloudMaterial.SetTexture("_WeatherTexture", rt);
            }

            useCustomWeatherTexture = useCustomTexture;
            if (useCustomWeatherTexture && customWeatherTexture != null)
            {
                if (isChangingWeather)
                {
                    StopCoroutine("LerpWeatherTexture");
                }
                Graphics.Blit(rt, prevWeatherTexture);
                Graphics.Blit(customWeatherTexture, nextWeatherTexture);
                setWeatherTexture();
            }
            else
            {
                GenerateWeatherTexture();
                setWeatherTexture();
            }

            //v0.1            
            if (Application.isPlaying)
            {
                GenerateAndChangeWeatherTexture();
                //runonstart = false;
            }
        }

        //v0.1
        //bool runonstart = true;

        void Update()
        {
            useCustomWeatherTexture = useCustomTexture;

            //v0.1            
            if (!Application.isPlaying)
            {
                setWeatherTexture();
            }
        }
    }
}