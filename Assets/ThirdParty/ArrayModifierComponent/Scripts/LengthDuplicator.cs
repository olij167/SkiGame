using System.Collections.Generic;
using UnityEngine;

namespace UnityArrayModifier
{
    public class LengthDuplicator : Duplicator
    {
        public LengthDuplicator(ArrayModifier arrayModifier)
            : base(arrayModifier)
        { 
        }

        public override List<Duplicate> CreateDuplicates(List<Duplicate> sourceDuplicates)
        {
            var duplicates = new List<Duplicate>();

            var translation = arrayModifier.GetTranslation();
            if (translation.magnitude < ArrayModifier.MIN_TRANSLATION_MAGNITUDE)
            {
                return null;
            }

            Debug.DrawRay(Vector3.zero, translation * 5f, Color.red);
            var duplicationCount = Mathf.FloorToInt(arrayModifier.FitLength / translation.magnitude);

            for(int i = 0; i < duplicationCount - 1; ++i)
            {
                var copies = DuplicatorUtility.CloneDuplicates(sourceDuplicates);
                for (int j = 0; j < copies.Count; ++j)
                {
                    copies[j].position += translation * (i + 1f);
                }

                duplicates.AddRange(copies);
            }

            return duplicates;
        }
    }
}