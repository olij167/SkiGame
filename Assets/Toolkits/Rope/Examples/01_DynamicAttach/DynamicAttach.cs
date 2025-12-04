using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RopeToolkit.Example
{
    public class DynamicAttach : MonoBehaviour
    {
        public Material ropeMaterial;

        public Vector3 attachPoint;
        public Transform target;
        public Vector3 targetAttachPoint;

        public SimpleRopeInteraction ropeInteraction;

        protected GameObject ropeObject;

        protected Rect guiRect = new Rect(Screen.width - 210, 10, 200, 0);

        public void Detach()
        {
            if (ropeObject)
            {
                Destroy(ropeObject);
            }
            ropeObject = null;

            if (ropeInteraction)
            {
                ropeInteraction.ropes.Clear();
            }
        }

        public void Attach()
        {
            Detach();

            ropeObject = new GameObject();
            ropeObject.name = "Rope";

            var start = transform.TransformPoint(attachPoint);
            var end = target.TransformPoint(targetAttachPoint);

            var rope = ropeObject.AddComponent<Rope>();
            rope.material = ropeMaterial;
            rope.spawnPoints.Add(ropeObject.transform.InverseTransformPoint(start));
            rope.spawnPoints.Add(ropeObject.transform.InverseTransformPoint(end));

            var conn0 = ropeObject.AddComponent<RopeConnection>();
            conn0.type = RopeConnectionType.PinRopeToTransform;
            conn0.ropeLocation = 0.0f;
            conn0.transformSettings.transform = transform;
            conn0.localConnectionPoint = attachPoint;

            var conn1 = ropeObject.AddComponent<RopeConnection>();
            conn1.type = RopeConnectionType.PinRopeToTransform;
            conn1.ropeLocation = 1.0f;
            conn1.transformSettings.transform = target;
            conn1.localConnectionPoint = targetAttachPoint;

            if (ropeInteraction)
            {
                ropeInteraction.ropes.Add(rope);
            }
        }

        protected void Window(int id)
        {
            if (GUILayout.Button("Attach"))
            {
                Attach();
            }
            if (GUILayout.Button("Detach"))
            {
                Detach();
            }

            GUI.enabled = false;
            GUILayout.Label("Instructions: Use the buttons above to dynamically attach and detach a rope from the scene.");
            GUI.enabled = true;
        }

        public void OnGUI()
        {
            guiRect = GUILayout.Window(1, guiRect, Window, "Dynamic attach");
        }
    }
}
