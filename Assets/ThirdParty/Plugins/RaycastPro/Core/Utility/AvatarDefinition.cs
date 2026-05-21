using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using RaycastPro.Editor;
#endif

namespace RaycastPro.Sensor
{
    [RequireComponent(typeof(Animator))]
    public sealed class AvatarDefinition : BaseUtility
    {
        public Animator animator;

        public List<HumanBodyBones> bodyBones = new List<HumanBodyBones>();
        public List<Transform> bones = new List<Transform>();
    void Start()
    {
        SyncBones();
    }

    public void SyncBones()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        bones.Clear();
        foreach (var humanBodyBones in bodyBones)
        {
            var t = animator.GetBoneTransform(humanBodyBones);
            if (!t) continue;
            bones.Add(t);
        }
    }

    public override bool Performed { get; protected set; }
    protected override void OnCast() { }

#if UNITY_EDITOR
        internal override string Info => "A data component that defines the physical presence of an avatar for perception sensors. It holds a collection of transforms representing key body parts (e.g., bones), enabling other systems to perform granular, bone-level line-of-sight and visibility assessments instead of relying on a single pivot point."
                                         + HUtility + HPreview;
    internal override void OnGizmos()
        {
            foreach (var bone in bones)
            {
                Gizmos.DrawWireSphere(bone.position, DotSize);
            }
        }

    internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true, bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                if (GUILayout.Button("Human Preset"))
                {
                    bodyBones.Clear();
                    bodyBones.Add(HumanBodyBones.Head);
                    bodyBones.Add(HumanBodyBones.Chest);
                    bodyBones.Add(HumanBodyBones.LeftHand);
                    bodyBones.Add(HumanBodyBones.RightHand);
                    bodyBones.Add(HumanBodyBones.LeftFoot);
                    bodyBones.Add(HumanBodyBones.RightFoot);
                    
                    SyncBones();
                }
                RCProEditor.DrawSerializedList(_so.FindProperty(nameof(bodyBones)));
            }

            if (hasGeneral)
            {
                BaseField(_so);
            }
        }
#endif
}
}

