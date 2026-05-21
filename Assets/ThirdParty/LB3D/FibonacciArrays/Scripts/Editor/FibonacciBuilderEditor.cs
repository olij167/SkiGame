using UnityEngine;
using System.Collections;

#if UNITY_EDITOR

using UnityEditor;

[CustomEditor(typeof(FibonacciBuilder))]
public class FibonacciBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FibonacciBuilder fibonacciBuilder = (FibonacciBuilder)target;
        if (GUILayout.Button("Build"))
        {
            fibonacciBuilder.GenerateLattice(create:true);
            
        }
        if (GUILayout.Button("Place"))
        {
            fibonacciBuilder.Place();
        }

        if (GUILayout.Button("Delete"))
        {
            fibonacciBuilder.DeleteObject();
        }
    }
}

#endif