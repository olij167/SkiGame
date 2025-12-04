using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RopeToolkit.Example
{
    [RequireComponent(typeof(SimpleRopeInteraction))]
    public class SimpleRopeInteractionGUI : MonoBehaviour
    {
        protected float desiredMaxImpulseStrength;

        protected Rect guiRect = new Rect(10, 10, 200, 0);

        public void Start()
        {
            desiredMaxImpulseStrength = GetComponent<SimpleRopeInteraction>().maxImpulseStrength;
        }

        public void Window(int id)
        {
            var interact = GetComponent<SimpleRopeInteraction>();

            bool isLimiting = interact.maxImpulseStrength > 0.0f;
            bool shouldLimit = GUILayout.Toggle(isLimiting, "Limit max impulse strength");

            if (isLimiting != shouldLimit)
            {
                Event.current.Use();
                
                if (shouldLimit)
                {
                    interact.maxImpulseStrength = desiredMaxImpulseStrength;
                }
                else
                {
                    desiredMaxImpulseStrength = interact.maxImpulseStrength;

                    interact.maxImpulseStrength = 0.0f;
                }
            }

            var limitString = shouldLimit ? interact.maxImpulseStrength.ToString("0.0") + " Ns" : "Infinite";
            GUILayout.Label("Max impulse strength: " + limitString);

            interact.maxImpulseStrength = GUILayout.HorizontalSlider(interact.maxImpulseStrength, 0.0f, 10.0f);

            GUI.enabled = false;
            GUILayout.Label("Instructions: Use the left mouse button to interact with the ropes in the scene. While holding on to a rope using the mouse, press <SPACE> on the keyboard to cut it at the held position.");
            GUI.enabled = true;
        }

        public void OnGUI()
        {
            guiRect = GUILayout.Window(0, guiRect, Window, "Interaction settings");
        }
    }
}
