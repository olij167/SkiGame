using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class URP_RendererSetter
{
    public static int SetWeatheradeRenderer(ScriptableRendererData rendererAsset)
    {
        int srsRendererId = -1;

        if (UniversalRenderPipeline.asset)
        {
            Type urpAsset = typeof(UniversalRenderPipelineAsset);
            FieldInfo renderersListField = urpAsset.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])renderersListField.GetValue(UniversalRenderPipeline.asset);

            for (int i = 0; i < rendererDataList.Length; i++)
            {
                if (rendererDataList[i] == rendererAsset)
                {
                    //Debug.Log("Renderer is set already. Skip.");
                    srsRendererId = i;
                    return srsRendererId;
                }
            }

            List<ScriptableRendererData> rendDataList = rendererDataList.ToList();
            rendDataList.Add(rendererAsset);

            rendererDataList = rendDataList.ToArray();

            //Set renderers list back
            renderersListField.SetValue(UniversalRenderPipeline.asset, rendererDataList);

            srsRendererId = rendererDataList.Length - 1;
        }
        else
        {
            Debug.LogError("No Universal Render Pipeline is currently active.");
        }

        return srsRendererId;
    }
}
