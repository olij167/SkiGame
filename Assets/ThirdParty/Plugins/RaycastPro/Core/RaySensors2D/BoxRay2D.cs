using UnityEngine.UIElements;

namespace RaycastPro.RaySensors2D
{
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(BoxRay2D))]
    public sealed class BoxRay2D : RaySensor2D
    {
        public Vector2 size = new Vector2(.1f, .4f);

        private float angle;

        protected override void OnCast()
        {
            angle = local ? -Vector2.Angle(transform.right.To2D(), Vector2.right) : 0;
            hit = Physics2D.BoxCast(transform.position, size, angle,
                Direction, direction.magnitude, detectLayer.value, MinDepth, MaxDepth);
            isDetect = FilterCheck(hit);
        }

#if UNITY_EDITOR
        internal override string Info =>
            "A 2D directional BoxCast sensor that can operate in two distinct modes based on the 'local' flag. When 'local' is disabled, it performs a world-aligned cast, maintaining its orientation regardless of the object's rotation (an AABB-style cast). When enabled, the cast's angle dynamically synchronizes with the object's rotation, effectively creating an Oriented Bounding Box (OBB) cast that matches the object's local orientation. This dual-mode capability makes it highly versatile for 2D scenarios, allowing for both simple, grid-aligned checks and precise, rotation-sensitive detection."
            + HAccurate + HDirectional;

        private Vector3 p1, p2;

        internal override void OnGizmos()
        {
            EditorUpdate();
            p1 = Base;
            p2 = Tip;
            DrawDepthLine(p1, p2);
            GizmoColor = Performed ? DetectColor : DefaultColor;
            DrawBoxRay(transform, transform.position, direction, size, z, local);
            DrawNormal2D(hit, z);
            DrawNormalFilter();
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                DirectionField(_so);
                var prop = _so.FindProperty(nameof(size));
                EditorGUILayout.PropertyField(prop, "Size".ToContent("Size"));
                prop.vector2Value = new Vector2(Mathf.Max(0, prop.vector2Value.x), Mathf.Max(0, prop.vector2Value.y));
            }

            if (hasGeneral) GeneralField(_so);
            if (hasEvents) EventField(_so);
            if (hasInfo) HitInformationField();
        }
#endif
        public override Vector3 Tip => transform.position + (Direction + Direction.normalized * (size.x)).ToDepth();

        public override Vector3 RawTip => transform.position + Direction.ToDepth();
        public override float RayLength => direction.magnitude + size.x;
        public override Vector3 Base => transform.position;
    }
}