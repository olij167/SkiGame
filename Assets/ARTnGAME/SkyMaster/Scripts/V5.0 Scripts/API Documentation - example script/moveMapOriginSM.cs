using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Artngame.SKYMASTER
{
    [ExecuteInEditMode]
    public class moveMapOriginSM : MonoBehaviour
    {
        public Transform MapToMove;
        public float moveAfterDist = 1000;
        public Transform player;
        public CloudScript fullvolumeClouds;
        public Material shadowsMat;
        public bool operateOnlyXZ = true;// do the move only in X,Z

        Vector3 initPlayerPos;

        // Start is called before the first frame update
        void Start()
        {
            if (shadowsMat != null && shadowsMat.HasProperty("cameraWSOffset"))
            {
                //Vector3 currentOffset = shadowsMat.GetVector("cameraWSOffset");
                shadowsMat.SetVector("cameraWSOffset", Vector3.zero);
            }
            if (fullvolumeClouds != null)
            {
                fullvolumeClouds._CameraWSOffset = Vector3.zero;
            }
            initPlayerPos = player.transform.position;
        }

        // Update is called once per frame
        void Update()
        {
            float dist = Vector3.Distance(player.transform.position, initPlayerPos);
            if (operateOnlyXZ)
            {
                dist = Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z), 
                    new Vector2(initPlayerPos.x, initPlayerPos.z));
            }

            if (shadowsMat != null && shadowsMat.HasProperty("cameraWSOffset"))
            {
                if (Application.isPlaying)
                {
                    //Vector3 currentOffset = shadowsMat.GetVector("cameraWSOffset");
                    shadowsMat.SetVector("cameraWSOffset", fullvolumeClouds._CameraWSOffset);// currentOffset + player.transform.position);
                }
                else
                {
                    shadowsMat.SetVector("cameraWSOffset", Vector3.zero);
                }
            }

            if (MapToMove != null && dist > moveAfterDist && Application.isPlaying)
            {
                //move all by the current offset
                if(fullvolumeClouds != null)
                {
                    if (operateOnlyXZ)
                    {
                        fullvolumeClouds._CameraWSOffset += new Vector3(player.transform.position.x, 0, player.transform.position.z);
                    }
                    else
                    {
                        fullvolumeClouds._CameraWSOffset += player.transform.position;
                    }
                }
                //player.transform.position = Vector3.zero;
                if (operateOnlyXZ)
                {
                    MapToMove.transform.position -= new Vector3(player.transform.position.x,0, player.transform.position.z);
                }
                else
                {
                    MapToMove.transform.position -= player.transform.position;
                }

               

                //NOW reset player to zero
                if (operateOnlyXZ)
                {
                    player.transform.position = new Vector3(0, player.transform.position.y, 0);
                }
                else
                {
                    player.transform.position = Vector3.zero;
                }

                //v0.1
                initPlayerPos = player.transform.position;

            }
        }
    }
}