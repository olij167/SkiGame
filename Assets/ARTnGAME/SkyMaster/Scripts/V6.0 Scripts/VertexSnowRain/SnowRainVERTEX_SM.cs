using UnityEngine;
using System.Collections;

namespace Artngame.SKYMASTER
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SnowRainVERTEX_SM : MonoBehaviour
    {
        public float Range = 1;
        public int SNOW_NUM = 16000;
        private Vector3[] vertices_;
        private int[] triangles_;
        private Vector2[] uvs_;
        private float range_;
        private float rangeR_;
        private Vector3 move_ = Vector3.zero;


        public Vector3 rain_speed = new Vector3(0, -1, 0);
        public bool isRain = false;
        public float rainSnowsize = 1;
        public float rainLength = 1;

        public bool controlVortex = false;
        public float vortexRadius = 0;
        public float vorticity = 0;
        public float vortexSpeed = 0;
        public float vortexDepth = 0;
        public Vector3 vortexPosition = new Vector3(0,0,0);

        //v0.1 - plane collisions
        public float collisionplaneY = 0;
        public float collideThreashold = 0;
        public float collidePower = 0;

        void Start()
        {
            
                range_ = 16f * Range;
                rangeR_ = 1.0f / range_;
                vertices_ = new Vector3[SNOW_NUM * 4];
                for (var i = 0; i < SNOW_NUM; ++i)
                {
                    float x = Random.Range(-range_, range_);
                    float y = Random.Range(-range_, range_);
                    float z = Random.Range(-range_, range_);
                    var point = new Vector3(x, y, z);
                    vertices_[i * 4 + 0] = point;
                    vertices_[i * 4 + 1] = point;
                    vertices_[i * 4 + 2] = point;
                    vertices_[i * 4 + 3] = point;
                }

                triangles_ = new int[SNOW_NUM * 6];
                for (int i = 0; i < SNOW_NUM; ++i)
                {
                    triangles_[i * 6 + 0] = i * 4 + 0;
                    triangles_[i * 6 + 1] = i * 4 + 1;
                    triangles_[i * 6 + 2] = i * 4 + 2;
                    triangles_[i * 6 + 3] = i * 4 + 2;
                    triangles_[i * 6 + 4] = i * 4 + 1;
                    triangles_[i * 6 + 5] = i * 4 + 3;
                }

                uvs_ = new Vector2[SNOW_NUM * 4];
                for (var i = 0; i < SNOW_NUM; ++i)
                {
                    uvs_[i * 4 + 0] = new Vector2(0f, 0f);
                    uvs_[i * 4 + 1] = new Vector2(1f, 0f);
                    uvs_[i * 4 + 2] = new Vector2(0f, 1f);
                    uvs_[i * 4 + 3] = new Vector2(1f, 1f);
                }
                Mesh mesh = new Mesh();
                mesh.name = "MeshSnowFlakes";
                mesh.vertices = vertices_;
                mesh.triangles = triangles_;
                mesh.uv = uvs_;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);
                var mf = GetComponent<MeshFilter>();
                mf.sharedMesh = mesh;
            
           
        }

        void LateUpdate()
        {
            var target_position = Camera.main.transform.TransformPoint(Vector3.forward * range_);
            var mr = GetComponent<Renderer>();
            mr.material.SetFloat("_Range", range_);
            mr.material.SetFloat("_RangeR", rangeR_);
            mr.material.SetFloat("_Size", 0.1f * rainSnowsize);
            mr.material.SetVector("_MoveTotal", move_);
            mr.material.SetVector("_CamUp", Camera.main.transform.up);
            mr.material.SetVector("_TargetPosition", target_position);
            float x = (Mathf.PerlinNoise(0f, Time.time * 0.1f) - 0.5f) * 10f;
            float y = -2f;
            float z = (Mathf.PerlinNoise(Time.time * 0.1f, 0f) - 0.5f) * 10f;

            if (controlVortex) {
                mr.material.SetVector("vortexControl", new Vector4(vorticity, vortexSpeed, vortexDepth, vortexRadius));
                mr.material.SetVector("vortexPosRadius", new Vector4(vortexPosition.x, vortexPosition.y, vortexPosition.z,0));
            }

            if (isRain)
            {
                
                mr.material.SetVector("_MoveR", rain_speed);
                mr.material.SetFloat("isRain", 1);
                mr.material.SetFloat("rainLength", rainLength);
                move_ += rain_speed * 0.1f;
                move_.x = Mathf.Repeat(move_.x, range_ * 2f);
                move_.y = Mathf.Repeat(move_.y, range_ * 2f);
                move_.z = Mathf.Repeat(move_.z, range_ * 2f);
            }
            else
            {
                mr.material.SetFloat("isRain", 0);
                mr.material.SetFloat("rainLength", rainLength);
                move_ += new Vector3(x, y, z) * Time.deltaTime;
                move_.x = Mathf.Repeat(move_.x, range_ * 2f);
                move_.y = Mathf.Repeat(move_.y, range_ * 2f);
                move_.z = Mathf.Repeat(move_.z, range_ * 2f);
            }

            //v0.1
            mr.material.SetFloat("planeY", collisionplaneY);
            mr.material.SetFloat("hitThres", collideThreashold);
            mr.material.SetFloat("hitPower", collidePower);

        }
    }
}