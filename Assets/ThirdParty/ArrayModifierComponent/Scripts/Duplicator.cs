using System.Collections.Generic;

namespace UnityArrayModifier
{
    public abstract class Duplicator
    {
        protected ArrayModifier arrayModifier;

        public Duplicator(ArrayModifier arrayModifier)
        {
            this.arrayModifier = arrayModifier;
        }

        public abstract List<Duplicate> CreateDuplicates(List<Duplicate> sourceDuplicates);
    }
}