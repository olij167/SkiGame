using System.Collections.Generic;

namespace RaycastPro.RaySensors
{
#if UNITY_EDITOR
    using UnityEditor;
    using System.Threading.Tasks;
#endif

    using UnityEngine;

    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(BasicRay))]
    public sealed class BasicRay : RaySensor
    {
        /// <summary>
        /// Cast Reverse
        /// </summary>
        /// <param name="reverseHit"></param>
        /// <returns></returns>
        public bool ReverseCast(out RaycastHit _hit)
        {
            return Physics.Linecast(Tip, Base, out _hit, detectLayer.value, triggerInteraction);
        }

        /// <summary>
        /// This method will Update and calculate ray depth by flip flop casting. 
        /// </summary>
        /// <returns></returns>
        public float GetDepth()
        {
            var _b = Base;
            var _t = Tip;
            return Vector3.Distance(
                Physics.Linecast(_t, _b, out var _hit, detectLayer.value, triggerInteraction) ? hit.point : _b,
                (Physics.Linecast(_t, _b, out _hit, detectLayer.value, triggerInteraction) &&
                 _hit.transform == hit.transform)
                    ? _hit.point
                    : _t);
        }

        protected override void OnCast() => Physics.Raycast(transform.position, Direction,
            out hit,
            DirectionLength, detectLayer.value, triggerInteraction);

        #region Plus Methods

        public override int AllCast(
            ref List<RaycastHit> raycastHits,
            out RaycastHit finalHit)
        {
            finalHit = default;
            raycastHits.Clear();

            var rayHits = Physics.RaycastAll(
                transform.position,
                Direction,
                DirectionLength,
                detectLayer.value,
                triggerInteraction
            );

            if (rayHits == null || rayHits.Length == 0)
                return -1;

            float minDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < rayHits.Length; i++)
            {
                var hit = rayHits[i];

                if (!hit.collider)
                    continue;

                raycastHits.Add(hit);

                // انتخاب برخورد نهایی: نزدیک‌ترین
                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    finalHit = hit;
                    found = true;
                }
            }

            // Raycast مدل single-segment است → index = 0
            return found ? 0 : -1;
        }

        #endregion
      

#if UNITY_EDITOR
        internal override string Info =>
            "Emits a single ray in a specific direction and returns the corresponding hit information." + HAccurate +
            HDirectional;

        /// <summary>
        /// Hint: This command will make your references missing.
        /// </summary>
        [ContextMenu("Convert To PipeRay")]
        private async void ConvertToPipeRay()
        {
            var _ray = Undo.AddComponent<PipeRay>(gameObject);
            _ray.direction = direction;
            await Task.Delay(1);
            Undo.DestroyObjectImmediate(this);
        }

        [ContextMenu("Convert To BoxRay")]
        private async void ConvertToBoxRay()
        {
            var _ray = Undo.AddComponent<BoxRay>(gameObject);
            _ray.direction = direction;
            await Task.Delay(1);
            Undo.DestroyObjectImmediate(this);
        }

        private Vector3 _p;

        internal override void OnGizmos()
        {
            EditorUpdate();

            _p = transform.position;
            Gizmos.color = Performed ? DetectColor : DefaultColor;


            if (IsManuelMode)
            {
                GizmoColor = DefaultColor;
                Gizmos.DrawRay(transform.position, Direction);
            }
            else
            {
                DrawBlockLine(_p, _p + Direction, hit);
            }

            DrawNormal(hit);
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain) DirectionField(_so);
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