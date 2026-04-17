using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AssetInventory
{
    internal sealed class ReorderableMultiColumnHeader : MultiColumnHeader
    {
        public event Action<int[]> columnOrderChanged;

        public ReorderableMultiColumnHeader(MultiColumnHeaderState state) : base(state)
        {
        }

        protected override void AddColumnHeaderContextMenuItems(GenericMenu menu)
        {
            // Find which column was right-clicked based on mouse position
            int clickedColumnIndex = GetColumnIndexAtMousePosition();

            if (clickedColumnIndex >= 0)
            {
                int visibleIndex = GetVisibleColumnIndexFor(clickedColumnIndex);

                // Don't allow moving the first column (Name) - it should stay locked
                bool isFirstColumn = visibleIndex == 0;
                bool isLastColumn = visibleIndex == state.visibleColumns.Length - 1;

                if (!isFirstColumn && visibleIndex > 1) // Can't move to position 0 (Name's spot)
                {
                    menu.AddItem(new GUIContent("Move Left"), false, () => MoveColumn(clickedColumnIndex, -1));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move Left"));
                }

                if (!isFirstColumn && !isLastColumn)
                {
                    menu.AddItem(new GUIContent("Move Right"), false, () => MoveColumn(clickedColumnIndex, 1));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move Right"));
                }
            }

            // Add default items (show/hide columns, resize to fit)
            base.AddColumnHeaderContextMenuItems(menu);
        }

        private int GetColumnIndexAtMousePosition()
        {
            Vector2 mousePos = Event.current.mousePosition;

            for (int i = 0; i < state.visibleColumns.Length; i++)
            {
                Rect columnRect = GetColumnRect(i);
                if (columnRect.Contains(mousePos))
                {
                    return state.visibleColumns[i];
                }
            }
            return -1;
        }

        private int GetVisibleColumnIndexFor(int columnIndex)
        {
            for (int i = 0; i < state.visibleColumns.Length; i++)
            {
                if (state.visibleColumns[i] == columnIndex) return i;
            }
            return -1;
        }

        private void MoveColumn(int columnIndex, int direction)
        {
            int currentVisibleIndex = GetVisibleColumnIndexFor(columnIndex);
            if (currentVisibleIndex < 0) return;

            int newVisibleIndex = currentVisibleIndex + direction;

            // Don't allow moving to position 0 (reserved for Name column)
            if (newVisibleIndex < 1) return;
            if (newVisibleIndex >= state.visibleColumns.Length) return;

            // Swap columns in visibleColumns array
            List<int> cols = new List<int>(state.visibleColumns);
            int temp = cols[currentVisibleIndex];
            cols[currentVisibleIndex] = cols[newVisibleIndex];
            cols[newVisibleIndex] = temp;

            state.visibleColumns = cols.ToArray();

            // Notify listeners
            columnOrderChanged?.Invoke(state.visibleColumns);

            Repaint();
        }
    }
}
