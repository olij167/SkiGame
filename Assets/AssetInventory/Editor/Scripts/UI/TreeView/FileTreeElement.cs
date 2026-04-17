using System;
using System.Collections.Generic;

namespace AssetInventory
{
    [Serializable]
    internal class FileTreeElement : TreeElement
    {
        public string Path;
        public bool IsFolder;
        public bool IsSelected = true;
        public List<string> Usages;

        public FileTreeElement(string name, int depth, int id) : base(name, depth, id)
        {
        }
    }
}

