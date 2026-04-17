using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#pragma warning disable CS0618 // Type or member is obsolete

namespace AssetInventory
{
    internal sealed class TagTreeViewControl : TreeViewWithTreeModel<TagTreeElement>
    {
        public event Action<Tag> OnRenameTag;
        public event Action<Tag> OnSetHotkey;
        public event Action<Tag> OnDeleteTag;
        public event Action OnHierarchyChanged;

        public TagTreeViewControl(TreeViewState state, TreeModel<TagTreeElement> model) : base(state, model)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = 22f;

            Reload();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            TagTreeElement item = (TagTreeElement)((TreeViewItem<TagTreeElement>)args.item).Data;
            if (item?.Tag == null) return;

            Rect rowRect = args.rowRect;
            float indent = GetContentIndent(args.item);
            rowRect.x += indent;
            rowRect.width -= indent;

            float x = rowRect.x;

            // Color field
            Rect colorRect = new Rect(x, rowRect.y + 2, 20, rowRect.height - 4);
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUI.ColorField(colorRect, GUIContent.none, item.Tag.GetColor(), false, false, false);
            if (EditorGUI.EndChangeCheck())
            {
                item.Tag.Color = "#" + ColorUtility.ToHtmlStringRGB(newColor);
                Tagging.SaveTag(item.Tag);
            }
            x += 25;

            // Tag name
            float nameWidth = rowRect.width - 180;
            Rect nameRect = new Rect(x, rowRect.y, nameWidth, rowRect.height);
            string tooltip = item.Tag.FromAssetStore ? "From Asset Store" : "Local Tag";
            EditorGUI.LabelField(nameRect, new GUIContent(item.Tag.Name, tooltip));
            x += nameWidth + 5;

            // Rename button
            Rect renameRect = new Rect(x, rowRect.y + 2, 24, rowRect.height - 4);
            if (GUI.Button(renameRect, EditorGUIUtility.IconContent("editicon.sml", "|Rename tag"), EditorStyles.miniButton))
            {
                OnRenameTag?.Invoke(item.Tag);
            }
            x += 28;

            // Hotkey button
            string hotkeyText = string.IsNullOrEmpty(item.Tag.Hotkey) ? "Key" : $"Alt+{item.Tag.Hotkey}";
            Rect hotkeyRect = new Rect(x, rowRect.y + 2, 60, rowRect.height - 4);
            if (GUI.Button(hotkeyRect, hotkeyText, EditorStyles.miniButton))
            {
                OnSetHotkey?.Invoke(item.Tag);
            }
            x += 64;

            // Delete button
            Rect deleteRect = new Rect(x, rowRect.y + 2, 24, rowRect.height - 4);
            if (GUI.Button(deleteRect, EditorGUIUtility.IconContent("TreeEditor.Trash", "|Remove tag completely"), EditorStyles.miniButton))
            {
                OnDeleteTag?.Invoke(item.Tag);
            }
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            // Allow dragging any tag
            return true;
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            List<TreeViewItem> draggedRows = DragAndDrop.GetGenericData("GenericDragColumnDragging") as List<TreeViewItem>;
            if (draggedRows == null) return DragAndDropVisualMode.None;

            switch (args.dragAndDropPosition)
            {
                case DragAndDropPosition.UponItem:
                case DragAndDropPosition.BetweenItems:
                {
                    bool validDrag = IsValidDrag(args.parentItem, draggedRows);
                    if (args.performDrop && validDrag)
                    {
                        TagTreeElement parentData = ((TreeViewItem<TagTreeElement>)args.parentItem).Data;
                        OnDropDraggedElementsAtIndex(draggedRows, parentData, args.insertAtIndex == -1 ? 0 : args.insertAtIndex);
                    }
                    return validDrag ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
                }

                case DragAndDropPosition.OutsideItems:
                {
                    if (args.performDrop)
                    {
                        OnDropDraggedElementsAtIndex(draggedRows, TreeModel.Root, TreeModel.Root.Children?.Count ?? 0);
                    }
                    return DragAndDropVisualMode.Move;
                }

                default:
                    return DragAndDropVisualMode.None;
            }
        }

        private bool IsValidDrag(TreeViewItem parent, List<TreeViewItem> draggedItems)
        {
            // Prevent dropping an item onto itself or its descendants
            TreeViewItem currentParent = parent;
            while (currentParent != null)
            {
                if (draggedItems.Contains(currentParent)) return false;
                currentParent = currentParent.parent;
            }
            return true;
        }

        protected override void OnDropDraggedElementsAtIndex(List<TreeViewItem> draggedRows, TagTreeElement parent, int insertIndex)
        {
            // First, let the base class handle the tree structure update
            base.OnDropDraggedElementsAtIndex(draggedRows, parent, insertIndex);

            // Then persist the hierarchy changes to the database
            foreach (TreeViewItem row in draggedRows)
            {
                TagTreeElement tagElement = ((TreeViewItem<TagTreeElement>)row).Data;
                if (tagElement?.Tag == null) continue;

                // Set the new parent ID (null if dropped at root level)
                tagElement.Tag.ParentId = parent?.Tag?.Id;
                Tagging.SaveTag(tagElement.Tag);
            }

            OnHierarchyChanged?.Invoke();
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return false; // We use custom rename via popup
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            // Not used - we handle renaming via custom popup
        }

        public void SetData(List<TagTreeElement> data)
        {
            TreeModel.SetData(data);
            Reload();
        }
    }
}
