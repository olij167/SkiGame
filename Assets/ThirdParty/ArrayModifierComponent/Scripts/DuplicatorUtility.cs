using System.Collections.Generic;
using UnityEngine;

namespace UnityArrayModifier
{
    public static class DuplicatorUtility
    {
        public static void RemoveArrayModifiersFrom(GameObject gameObject)
        {
            var arrayModifiers = gameObject.GetComponents<ArrayModifier>();
            for (int i = arrayModifiers.Length - 1; i >= 0; --i)
            {
                Object.DestroyImmediate(arrayModifiers[i]);
            }
        }

        public static List<Duplicate> CreateDuplicates(GameObject gameObject) 
        {
            var arrayModifiers = gameObject.GetComponents<ArrayModifier>();

            var duplicates = new List<Duplicate>();
            var hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
            duplicates.Add(new Duplicate(gameObject.transform.position, gameObject.transform.rotation, hideFlags));

            foreach (var am in arrayModifiers)
            {
                if (am.IsScheduledToBeDestroyed)
                {
                    continue;
                }

                var newDuplicates = am.Duplicator.CreateDuplicates(duplicates);
                if (newDuplicates != null && newDuplicates.Count > 0)
                { 
                    duplicates.AddRange(newDuplicates);
                }
            }

            duplicates.RemoveAt(0); // Remove the duplicate of the original GameObject

            return duplicates;
        }

        public static List<Duplicate> CloneDuplicates(List<Duplicate> duplicate)
        {
            List<Duplicate> clones = new List<Duplicate>();

            for (int i = 0; i < duplicate.Count; ++i)
            {
                clones.Add(new Duplicate(duplicate[i]));
            }

            return clones;
        }
    }
}