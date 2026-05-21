using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentMultiSelectionState
    {
        private readonly HashSet<int> _indices = new HashSet<int>();

        public int anchorIndex = -1;
        public bool isDragging;
        public int dragStartIndex = -1;
        public int dragEndIndex = -1;

        public IReadOnlyList<int> Indices => _indices.OrderBy(index => index).ToList();
        public int Count => _indices.Count;
        public bool HasSelection => _indices.Count > 0;

        public bool Contains(int index)
        {
            return _indices.Contains(index);
        }

        public void Clear()
        {
            _indices.Clear();
            anchorIndex = -1;
            isDragging = false;
            dragStartIndex = -1;
            dragEndIndex = -1;
        }

        public void SetSingle(int index)
        {
            _indices.Clear();
            if (index >= 0)
                _indices.Add(index);
            anchorIndex = index;
        }

        public void Toggle(int index)
        {
            if (index < 0)
                return;

            if (!_indices.Add(index))
                _indices.Remove(index);
            anchorIndex = index;
        }

        public void SetRange(int start, int end)
        {
            _indices.Clear();
            int min = Math.Min(start, end);
            int max = Math.Max(start, end);
            for (int i = min; i <= max; i++)
                if (i >= 0)
                    _indices.Add(i);
            anchorIndex = start;
        }

        public void BeginDrag(int start)
        {
            isDragging = start >= 0;
            dragStartIndex = start;
            dragEndIndex = start;
            if (isDragging)
                SetSingle(start);
        }

        public void UpdateDrag(int end)
        {
            if (!isDragging || dragStartIndex < 0)
                return;

            dragEndIndex = Math.Max(0, end);
            SetRange(dragStartIndex, dragEndIndex);
        }

        public void EndDrag()
        {
            isDragging = false;
        }
    }
#endif
}
