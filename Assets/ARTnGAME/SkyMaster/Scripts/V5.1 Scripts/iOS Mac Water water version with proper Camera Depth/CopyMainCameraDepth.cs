using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

namespace Artngame.SKYMASTER
{
    [ExecuteInEditMode]
    public class CopyMainCameraDepth : MonoBehaviour
    {
        public Material waterMat;

        public RenderTexture m_CamDepth = null;
        private CommandBuffer m_CmdDepthGrab = null;
        private Material m_DepthGrabMat = null;

        private int m_pixelWidth = 0;
        private int m_pixelHeight = 0;

        // Use this for initialization
        void Start()
        {
            m_DepthGrabMat = new Material(Shader.Find("SkyMaster/iOS_Depth_Grab"));
            m_CmdDepthGrab = new CommandBuffer();

            Camera.main.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, m_CmdDepthGrab);
            Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        }

        // Update is called once per frame
        void Update()
        {
            if (m_CmdDepthGrab == null)
            {
                m_DepthGrabMat = new Material(Shader.Find("SkyMaster/iOS_Depth_Grab"));
                m_CmdDepthGrab = new CommandBuffer();
                Camera.main.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, m_CmdDepthGrab);
                Camera.main.depthTextureMode |= DepthTextureMode.Depth;
            }


            if (m_CamDepth == null ||
                m_CamDepth.IsCreated() == false ||
                Camera.main.pixelWidth != m_pixelWidth ||
                Camera.main.pixelHeight != m_pixelHeight)
            {
                m_pixelWidth = Camera.main.pixelWidth;
                m_pixelHeight = Camera.main.pixelHeight;
                m_CamDepth = new RenderTexture(m_pixelWidth,
                                                m_pixelHeight, 0, RenderTextureFormat.RFloat);
                m_CamDepth.Create();

                if (waterMat != null)
                {
                    waterMat.SetTexture("_camDepthTex", m_CamDepth);
                }
            }

            m_CmdDepthGrab.Clear();
            m_CmdDepthGrab.name = "Grab depth";
            // m_CmdDepthGrab.Blit((Texture)m_CamDepth, m_CamDepth, m_DepthGrabMat);

            m_CmdDepthGrab.Blit((Texture)m_CamDepth, m_CamDepth, m_DepthGrabMat);

            //Graphics.Blit((Texture)m_CamDepth, m_CamDepth, m_DepthGrabMat);
            //if (waterMat != null)
            //{
            //    waterMat.SetTexture("_camDepthTex", m_CamDepth);
            //}
        }

        //private void OnRenderImage(RenderTexture source, RenderTexture destination)
        //{
        //    Graphics.Blit(null, m_CamDepth, m_DepthGrabMat);
        //    if (waterMat != null)
        //    {
        //        waterMat.SetTexture("_camDepthTex", m_CamDepth);
        //    }
        //}
    }
}