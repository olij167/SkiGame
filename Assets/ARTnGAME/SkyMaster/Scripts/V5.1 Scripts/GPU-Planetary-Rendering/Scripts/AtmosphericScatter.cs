using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
namespace Artngame.SKYMASTER.PlanetCreator.Atmosphere
{
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class AtmosphericScatter : MonoBehaviour
    {
        //v5.2.2
        public bool enableVR = false;

        [Range(0, 1)]
        public float imageFXBlend = 1;

        public int downscale;
        public float SCALE = 1000.0f;

        const int TRANSMITTANCE_WIDTH = 256;
        const int TRANSMITTANCE_HEIGHT = 64;
        const int TRANSMITTANCE_CHANNELS = 3;

        const int IRRADIANCE_WIDTH = 64;
        const int IRRADIANCE_HEIGHT = 16;
        const int IRRADIANCE_CHANNELS = 3;

        const int INSCATTER_WIDTH = 256;
        const int INSCATTER_HEIGHT = 128;
        const int INSCATTER_DEPTH = 32;
        const int INSCATTER_CHANNELS = 4;

        public GameObject m_sun;
        //public RenderTexture m_skyMap;
        public Vector3 m_betaR = new Vector3(0.0058f, 0.0135f, 0.0331f);
        public float m_mieG = 0.75f;
        public float m_sunIntensity = 100.0f;
        public ComputeShader m_writeData;
        public Vector3 EarthPosition;
        public float RG = 6360.0f, RT = 6420.0f, RL = 6421.0f;
        public float HR = 8;
        public float HM = 1.2f;

        private CommandBuffer lightingBuffer;
        private new Camera camera;

        public float MinViewDistance = 3000;

        public RenderTexture m_transmittance;
        public RenderTexture m_inscatter;
        public RenderTexture m_irradiance;
        public Material m_atmosphereImageEffect;
        void Start()
        {
            Application.runInBackground = false;
            lightingBuffer = new CommandBuffer();
            camera = GetComponent<Camera>();

            if (imageFXBlend < 1)
            {
                camera.AddCommandBuffer(CameraEvent.AfterImageEffects, lightingBuffer);
            }
            else
            {
                camera.AddCommandBuffer(CameraEvent.BeforeLighting, lightingBuffer);
            }

            camera.depthTextureMode = DepthTextureMode.DepthNormals;
            //m_skyMap.format = RenderTextureFormat.ARGBHalf; //must be floating point format

            CreateTextures();
            CopyDataToTextures();
            InitMaterial(m_atmosphereImageEffect);
            UpdateMaterialTextures(m_atmosphereImageEffect);
        }

        private void CreateTextures()
        {
            m_transmittance = new RenderTexture(TRANSMITTANCE_WIDTH, TRANSMITTANCE_HEIGHT, 0, RenderTextureFormat.ARGBHalf)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };
            m_transmittance.Create();

            m_inscatter = new RenderTexture(INSCATTER_WIDTH, INSCATTER_HEIGHT, 0, RenderTextureFormat.ARGBHalf)
            {
                volumeDepth = INSCATTER_DEPTH,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true
            };
            m_inscatter.Create();

            m_irradiance = new RenderTexture(IRRADIANCE_WIDTH, IRRADIANCE_HEIGHT, 0, RenderTextureFormat.ARGBHalf)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };
            m_irradiance.Create();
        }

        private void CopyDataToTextures()
        {
            //Transmittance is responsible for the change in the sun color as it moves
            //The raw file is a 2D array of 32 bit floats with a range of 0 to 1
            string path = Application.streamingAssetsPath + "/Textures/transmittance.raw";
            ComputeBuffer buffer = new ComputeBuffer(TRANSMITTANCE_WIDTH * TRANSMITTANCE_HEIGHT, sizeof(float) * TRANSMITTANCE_CHANNELS);
            CBUtility.WriteIntoRenderTexture(m_transmittance, TRANSMITTANCE_CHANNELS, path, buffer, m_writeData);
            buffer.Release();

            //Inscatter is responsible for the change in the sky color as the sun moves
            //The raw file is a 4D array of 32 bit floats with a range of 0 to 1.589844
            //As there is not such thing as a 4D texture the data is packed into a 3D texture 
            //and the shader manually performs the sample for the 4th dimension
            path = Application.streamingAssetsPath + "/Textures/inscatter.raw";
            buffer = new ComputeBuffer(INSCATTER_WIDTH * INSCATTER_HEIGHT * INSCATTER_DEPTH, sizeof(float) * INSCATTER_CHANNELS);
            CBUtility.WriteIntoRenderTexture(m_inscatter, INSCATTER_CHANNELS, path, buffer, m_writeData);
            buffer.Release();

            //The raw file is a 2D array of 32 bit floats with a range of 0 to 1
            path = Application.streamingAssetsPath + "/Textures/irradiance.raw";
            buffer = new ComputeBuffer(IRRADIANCE_WIDTH * IRRADIANCE_HEIGHT, sizeof(float) * IRRADIANCE_CHANNELS);
            CBUtility.WriteIntoRenderTexture(m_irradiance, IRRADIANCE_CHANNELS, path, buffer, m_writeData);
            buffer.Release();
        }

        void LateUpdate() //v6.1
        {
            UpdateMat(m_atmosphereImageEffect);
            camera.farClipPlane = Mathf.Max(MinViewDistance, (transform.position - transform.position.normalized * (RG * SCALE / 3f)).magnitude);

            UpdateRenderBuffer();
        }

        //v5.2.2
        Matrix4x4 left_world_from_view;
        Matrix4x4 right_world_from_view;
        // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
        Matrix4x4 left_screen_from_view;
        Matrix4x4 right_screen_from_view;
        Matrix4x4 left_view_from_screen;
        Matrix4x4 right_view_from_screen;


        private void UpdateRenderBuffer()
        {
            lightingBuffer.Clear();

            lightingBuffer.SetGlobalFloat("imageFXBlend", imageFXBlend);

            Matrix4x4 P = camera.projectionMatrix;
            Vector4 CamScreenDir = new Vector4(1f / P[0], 1f / P[5], 1f, 1f);
            lightingBuffer.SetGlobalVector("_CamScreenDir", CamScreenDir);
            Matrix4x4 viewProjInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
            lightingBuffer.SetGlobalMatrix("_ViewProjectInverse", viewProjInverse);
            Matrix4x4 CameraInv = camera.cameraToWorldMatrix.inverse;
            lightingBuffer.SetGlobalMatrix("_CameraInv", CameraInv);
            lightingBuffer.SetGlobalMatrix("_ViewMatrix", camera.worldToCameraMatrix);
            lightingBuffer.SetGlobalFloat("_FarPlane", camera.farClipPlane);
            lightingBuffer.SetGlobalTexture("_CameraDepthNormalsTexture", BuiltinRenderTextureType.GBuffer2);


            //v5.2.2
            if (camera.stereoEnabled)
            {
                left_world_from_view = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                right_world_from_view = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;

                // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                left_screen_from_view = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                right_screen_from_view = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;
                right_view_from_screen = GL.GetGPUProjectionMatrix(right_screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                left_view_from_screen[1, 1] *= -1;
                right_view_from_screen[1, 1] *= -1;

                // Store matrices
                lightingBuffer.SetGlobalMatrix("_LeftWorldFromView", left_world_from_view);
                lightingBuffer.SetGlobalMatrix("_RightWorldFromView", right_world_from_view);
                lightingBuffer.SetGlobalMatrix("_LeftViewFromScreen", left_view_from_screen);
                lightingBuffer.SetGlobalMatrix("_RightViewFromScreen", right_view_from_screen);

            }
            else
            {
                // Main eye inverse view matrix
                left_world_from_view = camera.cameraToWorldMatrix;
                left_screen_from_view = camera.projectionMatrix;
                left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                left_view_from_screen[1, 1] *= -1;

                // Store matrices
                lightingBuffer.SetGlobalMatrix("_LeftWorldFromView", left_world_from_view);
                lightingBuffer.SetGlobalMatrix("_LeftViewFromScreen", left_view_from_screen);
            }


            RenderTargetIdentifier active = new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer0);
            if (imageFXBlend < 1)
            {
                active = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            }

            RenderTargetIdentifier target = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            int downScaleTex = Shader.PropertyToID("_DownsampledTarget");
            lightingBuffer.GetTemporaryRT(downScaleTex, camera.pixelWidth / Mathf.Max(1, downscale), camera.pixelHeight / Mathf.Max(1, downscale), 0, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);

            //v5.2.1
            if (imageFXBlend < 1)
            {
                lightingBuffer.SetGlobalFloat("invertY", 1);
                if (enableVR)//v5.2.2
                {
                    lightingBuffer.Blit(active, downScaleTex);//v5.2.2
                }
            }
            else
            {
                lightingBuffer.SetGlobalFloat("invertY", 0);
                if (enableVR)//v5.2.2
                {
                    lightingBuffer.Blit(active, downScaleTex, m_atmosphereImageEffect);//v5.2.2
                }
            }

            if (!enableVR)//v5.2.2
            {
                lightingBuffer.Blit(active, downScaleTex, m_atmosphereImageEffect);//v5.2.2
            }

            if (imageFXBlend < 1)
            {
                if (!enableVR)//v5.2.2
                {
                    lightingBuffer.SetGlobalFloat("invertY", 0); //v5.2.1 //v5.2.2
                }
                lightingBuffer.Blit(downScaleTex, target, m_atmosphereImageEffect);
            }
            else
            {
                lightingBuffer.Blit(downScaleTex, target);
            }

            lightingBuffer.ReleaseTemporaryRT(downScaleTex);
        }

        void UpdateMat(Material mat)
        {
            mat.SetVector("betaR", m_betaR / SCALE);
            mat.SetFloat("mieG", m_mieG);
            mat.SetVector("SUN_DIR", m_sun.transform.forward * -1);
            mat.SetFloat("SUN_INTENSITY", m_sunIntensity);
            mat.SetVector("EARTH_POS", EarthPosition);
            mat.SetVector("CAMERA_POS", transform.position);
            mat.SetVector("betaMSca", (Vector4.one * 4e-3f) / SCALE);
            mat.SetVector("betaMEx", (Vector4.one * 4e-3f * 0.9f) / SCALE);

            mat.SetFloat("SCALE", SCALE);
            mat.SetFloat("Rg", RG * SCALE);
            mat.SetFloat("Rt", RT * SCALE);
            mat.SetFloat("Rl", RL * SCALE);
            mat.SetFloat("HR", HR * SCALE);
            mat.SetFloat("HM", HM * SCALE);
        }

        void UpdateMaterialTextures(Material mat)
        {
            mat.SetTexture("_Transmittance", m_transmittance);
            mat.SetTexture("_Inscatter", m_inscatter);
            mat.SetTexture("_Irradiance", m_irradiance);
        }

        void InitMaterial(Material mat)
        {
            //Consts, best leave these alone
            mat.SetFloat("M_PI", Mathf.PI);
            mat.SetFloat("SCALE", SCALE);
            mat.SetFloat("Rg", 6360.0f * SCALE);
            mat.SetFloat("Rt", 6420.0f * SCALE);
            mat.SetFloat("Rl", 6421.0f * SCALE);
            mat.SetFloat("RES_R", 32.0f);
            mat.SetFloat("RES_MU", 128.0f);
            mat.SetFloat("RES_MU_S", 32.0f);
            mat.SetFloat("RES_NU", 8.0f);
            mat.SetFloat("SUN_INTENSITY", m_sunIntensity);
            mat.SetVector("SUN_DIR", m_sun.transform.forward * -1.0f);
        }

        void OnDestroy()
        {
            if (m_transmittance) m_irradiance.Release();
            if (m_transmittance) m_transmittance.Release();
            if (m_inscatter) m_inscatter.Release();
        }
    }
}