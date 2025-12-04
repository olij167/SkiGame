using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

namespace RopeToolkit
{
    [CustomEditor(typeof(SimpleRopeInteraction))]
    public class SimpleRopeInteractionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Find ropes in current scene"))
            {
                ((SimpleRopeInteraction)target).ropes = StageUtility.GetMainStageHandle().FindComponentsOfType<Rope>().ToList();
            }
        }
    }
}
