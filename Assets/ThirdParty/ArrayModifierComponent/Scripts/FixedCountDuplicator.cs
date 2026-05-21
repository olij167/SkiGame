using System.Collections.Generic;

namespace UnityArrayModifier
{
    public class FixedCountDuplicator : Duplicator
    {
        public FixedCountDuplicator(ArrayModifier arrayModifier)
            : base(arrayModifier)
        {
        }

        public override List<Duplicate> CreateDuplicates(List<Duplicate> sourceDuplicates)
        {
            var translation = arrayModifier.GetTranslation();
            if (translation.magnitude < ArrayModifier.MIN_TRANSLATION_MAGNITUDE)
            {
                return null;
            }

            var duplicates = new List<Duplicate>();
            var newDuplicates = new List<Duplicate>(sourceDuplicates.Count);

            for (int i = 0; i < arrayModifier.Count - 1; ++i)
            {
                newDuplicates.Clear();
                newDuplicates.AddRange(DuplicatorUtility.CloneDuplicates(sourceDuplicates));

                for (int j = 0; j < newDuplicates.Count; ++j)
                {
                    newDuplicates[j].position += translation * (i + 1f);
                }

                duplicates.AddRange(newDuplicates);
            }

            return duplicates;
        }
    }
}