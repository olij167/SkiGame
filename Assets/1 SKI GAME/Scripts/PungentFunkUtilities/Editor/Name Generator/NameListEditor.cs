using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Content;

namespace PungentFunk.Utilities.Editor.Content
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(NameList))]
    public sealed class NameListEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NameList list = (NameList)target;
            if (list == null)
                return;

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Name List Utilities", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Entries: {list.Count}", EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Normalize"))
                    {
                        Undo.RecordObject(list, "Normalize Name List");
                        int changed = list.NormalizeNames(true, true, false);
                        EditorUtility.SetDirty(list);
                        Debug.Log($"Normalized {changed} entr{(changed == 1 ? "y" : "ies")} in {list.name}.", list);
                    }

                    if (GUILayout.Button("Normalize + Sort"))
                    {
                        Undo.RecordObject(list, "Normalize And Sort Name List");
                        int changed = list.NormalizeNames(true, true, true);
                        EditorUtility.SetDirty(list);
                        Debug.Log($"Normalized/sorted {changed} entr{(changed == 1 ? "y" : "ies")} in {list.name}.", list);
                    }

                    if (GUILayout.Button("Remove Duplicates"))
                    {
                        Undo.RecordObject(list, "Remove Duplicate Names");
                        int removed = list.RemoveDuplicateNames(true, true);
                        EditorUtility.SetDirty(list);
                        Debug.Log($"Removed {removed} duplicate entr{(removed == 1 ? "y" : "ies")} from {list.name}.", list);
                    }
                }

                if (GUILayout.Button("Open Name Generator Window"))
                    NameGeneratorWindow.Open();
            }
        }
    }

    [CustomEditor(typeof(MadLibNameList))]
    public sealed class MadLibNameListEditor : Editor
    {
        private int _numOfNames = 25;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MadLibNameList list = (MadLibNameList)target;
            if (list == null)
                return;

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Mad-Lib Utilities", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _numOfNames = Mathf.Max(1, EditorGUILayout.IntField("Count", _numOfNames));
                    if (GUILayout.Button("Generate Into Results"))
                    {
                        Undo.RecordObject(list, "Generate Mad-Lib Names");
                        list.GenerateNamesList(_numOfNames);
                        EditorUtility.SetDirty(list);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Normalize All"))
                    {
                        Undo.RecordObject(list, "Normalize Mad-Lib Name List");
                        int changed = list.NormalizeAll(false);
                        EditorUtility.SetDirty(list);
                        Debug.Log($"Normalized {changed} entr{(changed == 1 ? "y" : "ies")} in {list.name}.", list);
                    }

                    if (GUILayout.Button("Normalize + Sort"))
                    {
                        Undo.RecordObject(list, "Normalize And Sort Mad-Lib Name List");
                        int changed = list.NormalizeAll(true);
                        EditorUtility.SetDirty(list);
                        Debug.Log($"Normalized/sorted {changed} entr{(changed == 1 ? "y" : "ies")} in {list.name}.", list);
                    }

                    if (GUILayout.Button("Remove Duplicates"))
                    {
                        Undo.RecordObject(list, "Remove Duplicate Mad-Lib Names");
                        int removed = list.RemoveDuplicateNames();
                        EditorUtility.SetDirty(list);
                        Debug.Log($"Removed {removed} duplicate entr{(removed == 1 ? "y" : "ies")} from {list.name}.", list);
                    }
                }

                if (GUILayout.Button("Open Name Generator Window"))
                    NameGeneratorWindow.Open();
            }
        }
    }
    #endif

}