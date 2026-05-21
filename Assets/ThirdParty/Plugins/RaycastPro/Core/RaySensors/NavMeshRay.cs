using System;
using System.Collections.Generic;
using UnityEngine.AI;

namespace RaycastPro.RaySensors
{
#if UNITY_EDITOR
    using UnityEditor;
    using System.Threading.Tasks;
#endif

    using UnityEngine;
    
    public sealed class NavMeshRay : RaySensor
    {
        private NavMeshHit navHit;
        public int areaMask;
        public override bool Performed => navHit.hit;

        protected override void OnCast()
        {
// #if UNITY_EDITOR
//             GizmoGate = null;
// #endif
//
//             navHit = default;
//            NavMesh.Raycast(transform.position, Tip, out navHit, areaMask);
//
// #if UNITY_EDITOR
//             GizmoGate = () =>
//             {
//                 DrawBlockLine(transform.position, Tip, navHit);
//                 DrawNormal(navHit.position,Vector3.up);
//             };
// #endif
        }


#if UNITY_EDITOR
        internal override string Info =>
            "" + HAccurate +
            HDirectional;
        

        internal override void OnGizmos() => EditorUpdate();

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                DirectionField(_so);
                StaticUtilities.Draw(_so, nameof(areaMask), "NavMesh Areas");
            }

            if (hasGeneral) GeneralField(_so);
            if (hasEvents) EventField(_so);
            if (hasInfo) InformationField();
        }
#endif
        public override Vector3 Tip => transform.position + Direction;

        public override Vector3 RawTip => Tip;
        public override float RayLength => direction.magnitude;
        public override Vector3 Base => transform.position;
    }
}