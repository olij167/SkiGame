using System;
using UnityEngine;

namespace Artngame.SKYMASTER
{
    [ExecuteInEditMode]
    public class WaterTileSM : MonoBehaviour
    {
        public PlanarReflectionSM reflection;
        public WaterBaseSM waterBase;
        public bool allowDebugInSceneWindow = false; //v4.9.3

        public void Start()
        {
            AcquireComponents();
        }


        void AcquireComponents()
        {
            if (!reflection)
            {
                if (transform.parent)
                {
					reflection = transform.parent.GetComponent<PlanarReflectionSM>();
                }
                else
                {
					reflection = transform.GetComponent<PlanarReflectionSM>();
                }
            }

            if (!waterBase)
            {
                if (transform.parent)
                {
					waterBase = transform.parent.GetComponent<WaterBaseSM>();
                }
                else
                {
					waterBase = transform.GetComponent<WaterBaseSM>();
                }
            }
        }


#if UNITY_EDITOR
        public void Update()
        {
            AcquireComponents();
        }
#endif


        public void OnWillRenderObject()
        {
            if (reflection)
            {
                //v3.2
                // && Camera.current.transform.eulerAngles != Vector3.zero){
                if (Camera.current != null && ((Camera.main != null && Camera.current == Camera.main) || allowDebugInSceneWindow)){ //v4.2 //v4.9.3

                    if (Camera.current.stereoEnabled)//v5.0.2
                    {
                        if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
                        {
                           // reflection.WaterTileBeingRendered(transform, Camera.current, 0);
                           // Debug.Log("eye LEFT");
                        }
                        else
                        {
                           // reflection.WaterTileBeingRendered(transform, Camera.current, 1);
                           // Debug.Log("eye RIGHT");
                        }
                    }
                    else
                    {


                        reflection.WaterTileBeingRendered(transform, Camera.current, 0);
                    }
				}
            }
            if (waterBase)
            {
                //v3.2 - //v4.2
                //if (Camera.current != null && Camera.current.transform.eulerAngles != Vector3.zero) {
                    //waterBase.WaterTileBeingRendered (transform, Camera.current); //v4.2
                //}
            }
        }
    }
}