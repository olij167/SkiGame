using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RopeToolkit
{
    public class SimpleRopeInteraction : MonoBehaviour
    {
        [Tooltip("The mesh to show on the picked particle position. May be empty.")]
        public Mesh pickedMesh;

        [Tooltip("The mesh to show on the target position. May be empty.")]
        public Mesh targetMesh;

        [Tooltip("The material to use for the picked mesh")]
        public Material pickedMaterial;

        [Tooltip("The material to use for the target mesh")]
        public Material targetMaterial;

        [Tooltip("The maximum distance a rope can be picked from")]
        public float maxPickDistance = 2.0f;

        [Tooltip("The max allowable impulse strength to use. If zero, no limit is applied.")]
        public float maxImpulseStrength = 3.0f;

        [Tooltip("The mass multiplier to apply to the pulled rope particle. Increasing the mass multiplier for a particle increases its influence on neighboring particles. As this script pulls a single particle at a time only, it is beneficial to set the mass multiplier above 1 to improve the stability of the overall rope simulation.")]
        public float leverage = 10.0f;

        [Tooltip("The keyboard key to use to split a picked rope. May be set to None to disable this feature.")]
        public KeyCode splitPickedRopeOnKey = KeyCode.Space;

        [Tooltip("The list of ropes that may be picked")]
        public List<Rope> ropes;

        protected bool ready;
        protected Rope rope;
        protected int particle;
        protected float distance;
        protected float3 pickedPosition;
        protected float3 targetPosition;

        public void SplitPickedRope()
        {
            if (rope == null)
            {
                return;
            }

            ropes.Remove(rope);

            var newRopes = new Rope[2];
            rope.SplitAt(particle, newRopes);
            if (newRopes[0] != null) ropes.Add(newRopes[0]);
            if (newRopes[1] != null) ropes.Add(newRopes[1]);

            rope = null;
        }

        protected Rope GetClosestRope(Ray ray, out int closestParticleIndex, out float closestDistanceAlongRay)
        {
            closestParticleIndex = -1;
            closestDistanceAlongRay = 0.0f;

            var closestRopeIndex = -1;
            var closestDistance = 0.0f;
            for (int i = 0; i < ropes.Count; i++)
            {
                ropes[i].GetClosestParticle(ray, out int particleIndex, out float distance, out float distanceAlongRay);

                if (distance > maxPickDistance)
                {
                    continue;
                }

                if (closestRopeIndex != -1 && distance > closestDistance)
                {
                    continue;
                }
                
                closestRopeIndex = i;
                closestParticleIndex = particleIndex;
                closestDistance = distance;
                closestDistanceAlongRay = distanceAlongRay;
            }

            return closestRopeIndex != -1 ? ropes[closestRopeIndex] : null;
        }
        
        public void FixedUpdate()
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            if (Input.GetMouseButton(0))
            {
                // Mouse down
                if (ready && rope == null)
                {
                    // Not pulling a rope, find the closest one to the mouse
                    var closestRope = GetClosestRope(ray, out int closestParticleIndex, out float closestDistanceAlongRay);

                    if (closestRope != null && closestParticleIndex != -1 && closestRope.GetMassMultiplierAt(closestParticleIndex) > 0.0f)
                    {
                        // Found a rope and particle on the rope, start pulling that particle!
                        rope = closestRope;
                        particle = closestParticleIndex;
                        distance = closestDistanceAlongRay;

                        ready = false;
                    }
                }
            }
            else
            {
                // Mouse up
                if (rope != null)
                {
                    // Stop pulling the rope
                    rope.SetMassMultiplierAt(particle, 1.0f);
                    rope = null;
                }
            }

            if (rope != null)
            {
                // We are pulling the rope

                // Move the rope particle to the mouse position on the grab-plane
                pickedPosition = rope.GetPositionAt(particle);
                targetPosition = ray.GetPoint(distance);

                if (maxImpulseStrength == 0.0f)
                {
                    rope.SetMassMultiplierAt(particle, 0.0f);
                }
                else
                {
                    rope.SetMassMultiplierAt(particle, leverage);
                }

                rope.SetPositionAt(particle, targetPosition, maxImpulseStrength);

                // Split the rope on key if keybind is set
                if (Input.GetKey(splitPickedRopeOnKey))
                {
                    SplitPickedRope();
                }
            }
        }

        public void Update()
        {
            if (!Input.GetMouseButton(0))
            {
                ready = true;
            }

            if (rope != null)
            {
                if (pickedMesh != null && pickedMaterial != null)
                {
                    Graphics.DrawMesh(pickedMesh, Matrix4x4.TRS(pickedPosition, Quaternion.identity, Vector3.one * 0.25f), pickedMaterial, 0);
                }
                if (targetMesh != null && targetMaterial != null)
                {
                    Graphics.DrawMesh(targetMesh, Matrix4x4.TRS(targetPosition, Quaternion.identity, Vector3.one * 0.25f), targetMaterial, 0);
                }
            }
        }
    }
}
