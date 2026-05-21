using UnityEngine;
using UnityEditor;

namespace UnityArrayModifier
{
    [CustomEditor(typeof(ArrayModifier))]
    public class ArrayModifierEditor : Editor
    {
        private ArrayModifier arrayModifier = null;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh"))
            {
                arrayModifier.Refresh();
            }

            GUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();

            var fitType = (FitType)EditorGUILayout.EnumPopup(new GUIContent("Fit Type", "Determines which method to use for creating duplicates."), arrayModifier.fitType);

            var count = arrayModifier.count;
            var fitLength = arrayModifier.fitLength;
            if (arrayModifier.fitType == FitType.FixedCount)
            {
                count = EditorGUILayout.DelayedIntField(new GUIContent("Count", "Determines how many duplicates will get created."), arrayModifier.count);
            }
            else if (arrayModifier.fitType == FitType.Length)
            {
                fitLength = EditorGUILayout.DelayedFloatField(new GUIContent("Length", "Determines at which length to duplicate to."), arrayModifier.fitLength);
            }

            var constantOffset = EditorGUILayout.Vector3Field(new GUIContent("Constant Offset", "Determines the offset between each object in global space."), arrayModifier.constantOffset);
            var relativeOffset = EditorGUILayout.Vector3Field(new GUIContent("Relative Offset", "Determines the offset between each object in local space."), arrayModifier.relativeOffset);

            if (EditorGUI.EndChangeCheck())
            {
                ClearDuplicates();

                if (fitType != arrayModifier.fitType)
                {
                    Undo.RecordObject(arrayModifier, "Changed fit type");
                    arrayModifier.fitType = fitType;
                    arrayModifier.InitializeDuplicator();
                }

                if (count != arrayModifier.count)
                {
                    Undo.RecordObject(arrayModifier, "Changed count");
                    arrayModifier.count = count;
                }

                if (fitLength != arrayModifier.fitLength)
                {
                    Undo.RecordObject(arrayModifier, "Changed fit length");
                    arrayModifier.fitLength = fitLength;
                }

                if (constantOffset != arrayModifier.constantOffset)
                {
                    Undo.RecordObject(arrayModifier, "Changed constant offset");
                    arrayModifier.constantOffset = constantOffset;
                }

                if (relativeOffset != arrayModifier.relativeOffset)
                {
                    Undo.RecordObject(arrayModifier, "Changed relative offset");
                    arrayModifier.relativeOffset = relativeOffset;
                }

                arrayModifier.Refresh();
                RegisterUdnoDuplicateGameObjects();
            }
        }

        private void Awake()
        {
            arrayModifier = target as ArrayModifier;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo() 
        {
            arrayModifier.Refresh();
            RemoveOldDuplicates();
        }

        private void ClearDuplicates() 
        {
            for (int i = arrayModifier.InstantiatedDuplicates.Count - 1; i >= 0; --i)
            {
                Undo.DestroyObjectImmediate(arrayModifier.InstantiatedDuplicates[i]);
            }
        }

        private void RegisterUdnoDuplicateGameObjects() 
        {
            foreach (var gameObject in arrayModifier.InstantiatedDuplicates)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, "Created duplicate gameobject");
            }
        }

        private void RemoveOldDuplicates() 
        {
            var duplicateBehaviours = FindObjectsOfType<DuplicateBehaviour>();
            for (int i = duplicateBehaviours.Length - 1; i >= 0; --i)
            {
                if (duplicateBehaviours[i].IsOld)
                {
                    DestroyImmediate(duplicateBehaviours[i].gameObject);
                }
            }
        }
    }
}