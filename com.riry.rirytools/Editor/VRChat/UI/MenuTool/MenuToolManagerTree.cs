using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Menu manager list: the planned layout, searchable, each row with a drag handle, its source system's icon, its
    /// name and the submenu it is in. Dragging a row by its handle or choosing Move to... records a planned move; nothing
    /// changes until Apply.
    /// </summary>
    internal sealed class MenuToolManagerTree : TreeView
    {
        private const string DragKey = "RiryTools.MenuTool.Entry";
        private const float Handle = 14f;

        private sealed class Row : TreeViewItem
        {
            internal MenuToolEntry Entry;
        }

        internal Action<string, string, int> Move;
        internal Action<MenuToolEntry> Clicked;
        internal Action<MenuToolEntry> ContextMove;
        // Rename Menu Entry (path, new name) and Delete Menu Entry (path), planned by the window.
        internal Action<string, string> Rename;
        internal Action<string> Delete;
        private MenuToolEntry root;
        private int handleDown = -1;
        private readonly Dictionary<MenuToolEntry, int> idOf = new Dictionary<MenuToolEntry, int>();

        internal MenuToolManagerTree(TreeViewState state) : base(state)
        {
            showAlternatingRowBackgrounds = true;
            rowHeight = 20f;
        }

        internal void Set(MenuToolEntry planned)
        {
            root = planned;
            Reload();
        }

        internal MenuToolEntry Selected
        {
            get
            {
                IList<int> selection = GetSelection();
                var row = selection.Count > 0 ? FindItem(selection[0], rootItem) as Row : null;
                return row != null ? row.Entry : null;
            }
        }

        /// <summary>Rename Menu Entry from the radial: selects the entry's row and opens its name field.</summary>
        internal void BeginRename(MenuToolEntry entry)
        {
            int id;
            if (entry == null || !idOf.TryGetValue(entry, out id)) return;
            searchString = "";
            Select(entry);
            TreeViewItem item = FindItem(id, rootItem);
            if (item != null) BeginRename(item);
        }

        internal void Select(MenuToolEntry entry)
        {
            int id;
            if (entry == null || !idOf.TryGetValue(entry, out id)) return;
            SetSelection(new[] { id });
            FrameItem(id);
        }

        protected override TreeViewItem BuildRoot()
        {
            idOf.Clear();
            var top = new Row { id = 0, depth = -1, displayName = "root" };
            var items = new List<TreeViewItem>();
            int id = 1;
            if (root != null) Add(items, root, 0, ref id);
            SetupParentsAndChildrenFromDepths(top, items);
            return top;
        }

        private void Add(List<TreeViewItem> items, MenuToolEntry menu, int depth, ref int id)
        {
            foreach (MenuToolEntry entry in menu.Children)
            {
                idOf[entry] = id;
                items.Add(new Row { id = id++, depth = depth, displayName = entry.Name, Entry = entry });
                if (entry.IsSubMenu) Add(items, entry, depth + 1, ref id);
            }
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var row = item as Row;
            return row != null && (row.Entry.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                row.Entry.Parameters().Any(p => p.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var row = (Row)args.item;
            Rect rect = args.rowRect;
            // A triple-click opens the name field (Rename Menu Entry).
            Event click = Event.current;
            if (click.type == EventType.MouseDown && click.button == 0 && click.clickCount == 3 && rect.Contains(click.mousePosition) && CanRename(row))
            {
                click.Use();
                BeginRename(row);
            }
            float x = rect.x + GetContentIndent(row);
            var handle = new Rect(x, rect.y + 2f, Handle, rect.height - 4f);
            if (!hasSearch)
            {
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.Pan);
                GUI.Label(handle, "≡", EditorStyles.centeredGreyMiniLabel);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0) handleDown = handle.Contains(Event.current.mousePosition) ? row.id : -1;
            }
            x += Handle + 2f;
            Texture origin = MenuToolIcons.Origin(row.Entry);
            if (origin != null) GUI.DrawTexture(new Rect(x, rect.y + 2f, 16f, 16f), origin, ScaleMode.ScaleToFit);
            x += 18f;
            if (row.Entry.Icon != null) GUI.DrawTexture(new Rect(x, rect.y + 2f, 16f, 16f), row.Entry.Icon, ScaleMode.ScaleToFit);
            x += 18f;
            string tip = MenuToolIcons.TypeName(row.Entry.Type) + " from " + row.Entry.Origin + (row.Entry.Estimated ? " (estimated)" : "") +
                (row.Entry.Parameters().Any() ? "\nParameters: " + string.Join(", ", row.Entry.Parameters()) : "");
            string folder = row.Entry.Parent != null && row.Entry.Parent.Parent != null ? row.Entry.Parent.Path : "Root";
            // The folder column gives way when the row is narrow or deeply nested, so the name never runs under it.
            float free = rect.xMax - x;
            float folderWidth = free > 260f ? Mathf.Min(220f, rect.width * 0.35f) : 0f;
            if (folderWidth == 0f) tip += "\nIn: " + folder;
            EditorGUI.LabelField(new Rect(x, rect.y, Mathf.Max(0f, free - folderWidth - 4f), rect.height),
                new GUIContent(row.Entry.Name + (row.Entry.IsSubMenu ? "  ▸" : ""), tip), row.Entry.IsSubMenu ? EditorStyles.boldLabel : EditorStyles.label);
            if (folderWidth > 0f) EditorGUI.LabelField(new Rect(rect.xMax - folderWidth, rect.y, folderWidth, rect.height), new GUIContent(folder, folder), EditorStyles.miniLabel);
        }

        protected override bool CanRename(TreeViewItem item)
        {
            var row = item as Row;
            return row != null && row.Entry != null && !row.Entry.Generated;
        }

        protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            float x = rowRect.x + GetContentIndent(item) + Handle + 2f + 36f;
            return new Rect(x, rowRect.y, Mathf.Max(40f, rowRect.xMax - x), rowRect.height);
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            var row = FindItem(args.itemID, rootItem) as Row;
            string name = (args.newName ?? "").Trim();
            if (!args.acceptedRename || row == null || name.Length == 0 || name == row.Entry.Name || Rename == null) return;
            Rename(row.Entry.Path, name);
        }

        // Delete on a selected row: Delete Menu Entry.
        protected override void KeyEvent()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.Delete || Delete == null) return;
            MenuToolEntry entry = Selected;
            if (entry == null || entry.Generated) return;
            current.Use();
            Delete(entry.Path);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (Clicked != null) Clicked(Selected);
        }

        protected override void ContextClickedItem(int id)
        {
            var row = FindItem(id, rootItem) as Row;
            if (row != null && ContextMove != null) ContextMove(row.Entry);
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            var row = args.draggedItem as Row;
            return !hasSearch && row != null && row.id == handleDown && !row.Entry.Generated;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            var row = FindItem(args.draggedItemIDs[0], rootItem) as Row;
            if (row == null) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(DragKey, row.Entry.Path);
            DragAndDrop.objectReferences = new UnityEngine.Object[0];
            DragAndDrop.StartDrag(row.Entry.Name);
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            var from = DragAndDrop.GetGenericData(DragKey) as string;
            if (from == null || hasSearch) return DragAndDropVisualMode.None;
            MenuToolEntry destination;
            int index;
            var target = args.parentItem as Row;
            switch (args.dragAndDropPosition)
            {
                case DragAndDropPosition.UponItem:
                    if (target == null || target.Entry == null) return DragAndDropVisualMode.Rejected;
                    if (target.Entry.IsSubMenu)
                    {
                        destination = target.Entry;
                        index = -1;
                    }
                    else
                    {
                        destination = target.Entry.Parent;
                        index = destination.Children.IndexOf(target.Entry);
                    }
                    break;
                case DragAndDropPosition.BetweenItems:
                    destination = target != null && target.Entry != null ? target.Entry : root;
                    index = args.insertAtIndex;
                    break;
                default:
                    destination = root;
                    index = -1;
                    break;
            }
            if (destination == null || !destination.IsSubMenu) return DragAndDropVisualMode.Rejected;
            MenuToolEntry moving = root.Find(from);
            if (moving == null || destination == moving || MenuToolScanner.IsUnder(destination, moving)) return DragAndDropVisualMode.Rejected;
            if (args.performDrop && Move != null) Move(from, destination.Path, index);
            return DragAndDropVisualMode.Move;
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool manager tree
// ============================================================================
//
// MenuToolManagerTree (this file)               Menu manager tab: the planned layout. Rows: drag handle (≡, only it starts a drag; none while
//                            searching), the source's component icon, the control's icon, name, and the folder (submenu) it is
//                            in (left out, and put in the tooltip, when the row is narrow). Search matches path and parameters. Drag onto a submenu (into it), onto a control (before it)
//                            or between rows: Move(from, to, index). Right-click: ContextMove (Move to...). Selection: Clicked.
