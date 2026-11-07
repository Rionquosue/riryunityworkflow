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
    /// <summary>Icons for menu rows: the source system's component icon, and a type icon for each control.</summary>
    internal static class MenuToolIcons
    {
        private static readonly Dictionary<string, Texture> ByType = new Dictionary<string, Texture>();

        /// <summary>The icon of the component that contributes the entry, as the Inspector shows it (VRCFury and MA only).</summary>
        internal static Texture Origin(MenuEntry entry)
        {
            if (entry.Origin == MenuOrigin.Native) return null;
            Component component = entry.Origin == MenuOrigin.VRCFury ? entry.VrcfComponent ?? entry.Source as Component : entry.MaItem ?? entry.MaInstaller ?? entry.Source as Component;
            if (component == null) return null;
            string key = component.GetType().FullName;
            Texture icon;
            if (!ByType.TryGetValue(key, out icon))
            {
                // The installed package's own component icon; nothing is copied from it.
                icon = AssetPreview.GetMiniTypeThumbnail(component.GetType()) ?? EditorGUIUtility.ObjectContent(component, component.GetType()).image;
                ByType[key] = icon;
            }
            return icon;
        }

        internal static string TypeName(ControlType type)
        {
            switch (type)
            {
                case ControlType.SubMenu: return "Submenu";
                case ControlType.Button: return "Button";
                case ControlType.Toggle: return "Toggle";
                case ControlType.RadialPuppet: return "Radial puppet";
                case ControlType.TwoAxisPuppet: return "Two-axis puppet";
                case ControlType.FourAxisPuppet: return "Four-axis puppet";
                default: return type.ToString();
            }
        }
    }

    /// <summary>
    /// The Parameter budget tree: the assembled menu with each submenu's and control's synced bits (each parameter once),
    /// shaded darker red the more synced Floats it holds; then the Animator only block: synced parameters no menu uses,
    /// under the controllers and layers that use them. Double-click selects the source.
    /// </summary>
    internal sealed class BudgetTree : TreeView
    {
        private sealed class Row : TreeViewItem
        {
            internal MenuEntry Entry;
            internal ParamEntry Param;
            internal UnityEngine.Object Source;
            internal int Bits = -1;
            internal int Floats;
            internal string Note = "";
            internal bool Header;
        }

        private MenuScan scan;
        private int maxFloats = 1;

        internal BudgetTree(TreeViewState state) : base(state)
        {
            showAlternatingRowBackgrounds = true;
            rowHeight = 18f;
        }

        internal void Set(MenuScan value)
        {
            scan = value;
            Reload();
            ExpandAll();
        }

        protected override TreeViewItem BuildRoot()
        {
            int id = 1;
            var root = new Row { id = 0, depth = -1, displayName = "root" };
            var items = new List<TreeViewItem>();
            if (scan == null)
            {
                root.children = new List<TreeViewItem>();
                return root;
            }
            items.Add(new Row { id = id++, depth = 0, displayName = "Main Menu", Header = true });
            MenuEntry menu = scan.Root;
            var menuRow = new Row { id = id++, depth = 1, displayName = "Root Menu", Entry = menu, Source = menu.Source, Bits = MenuToolBudget.Bits(scan, menu), Floats = MenuToolBudget.SyncedFloats(scan, menu) };
            items.Add(menuRow);
            maxFloats = Math.Max(1, menuRow.Floats);
            AddEntries(items, menu, 2, ref id);
            items.Add(new Row { id = id++, depth = 0, displayName = "Animator only (synced, no menu uses them)", Header = true });
            List<ParamEntry> animatorOnly = MenuToolBudget.AnimatorOnly(scan);
            var placed = new HashSet<string>();
            foreach (ControllerUsage controller in scan.Controllers)
            {
                var layers = controller.Layers.Select(layer => (layer, names: layer.Parameters.Where(n => animatorOnly.Any(p => p.Name == n)).ToList()))
                    .Where(pair => pair.names.Count > 0).ToList();
                if (layers.Count == 0) continue;
                var names = new HashSet<string>(layers.SelectMany(pair => pair.names));
                items.Add(new Row { id = id++, depth = 1, displayName = controller.Name, Source = controller.Controller, Bits = names.Sum(n => scan.Param(n).Bits) });
                foreach (var (layer, layerNames) in layers)
                {
                    items.Add(new Row { id = id++, depth = 2, displayName = layer.Name, Source = controller.Controller, Bits = layerNames.Sum(n => scan.Param(n).Bits) });
                    foreach (string name in layerNames)
                    {
                        ParamEntry param = scan.Param(name);
                        placed.Add(name);
                        items.Add(new Row { id = id++, depth = 3, displayName = name, Param = param, Source = param.Sources.FirstOrDefault(), Bits = param.Bits, Note = param.Type.ToString() });
                    }
                }
            }
            List<ParamEntry> unused = animatorOnly.Where(p => !placed.Contains(p.Name)).ToList();
            if (unused.Count > 0)
            {
                items.Add(new Row { id = id++, depth = 1, displayName = "Not used by any animator layer", Bits = unused.Sum(p => p.Bits) });
                foreach (ParamEntry param in unused)
                    items.Add(new Row { id = id++, depth = 2, displayName = param.Name, Param = param, Source = param.Sources.FirstOrDefault(), Bits = param.Bits, Note = param.Type.ToString() });
            }
            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }

        private void AddEntries(List<TreeViewItem> items, MenuEntry menu, int depth, ref int id)
        {
            foreach (MenuEntry entry in menu.Children)
            {
                if (!InFilter(entry)) continue;
                int floats = MenuToolBudget.SyncedFloats(scan, entry);
                maxFloats = Math.Max(maxFloats, floats);
                string note = entry.IsSubMenu ? "" : string.Join(", ", entry.Parameters());
                items.Add(new Row { id = id++, depth = depth, displayName = entry.Name, Entry = entry, Source = entry.Source, Bits = MenuToolBudget.Bits(scan, entry), Floats = floats, Note = note });
                if (entry.IsSubMenu) AddEntries(items, entry, depth + 1, ref id);
            }
        }

        // Entries from outside the target (when the target is part of an avatar) are hidden, keeping submenus that lead to some.
        private bool InFilter(MenuEntry entry)
        {
            if (scan.InFilter(entry.Source)) return true;
            return entry.IsSubMenu && entry.Descendants().Any(child => scan.InFilter(child.Source));
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var row = (Row)args.item;
            Rect rect = args.rowRect;
            if (Event.current.type == EventType.Repaint && row.Floats > 0)
            {
                // The more synced Floats, the darker the red.
                float t = Mathf.Clamp01((float)row.Floats / maxFloats);
                EditorGUI.DrawRect(rect, Color.Lerp(new Color(0.85f, 0.35f, 0.35f, 0.25f), new Color(0.45f, 0f, 0f, 0.75f), t));
            }
            float indent = GetContentIndent(row);
            var label = new Rect(rect.x + indent, rect.y, rect.width - indent - 150f, rect.height);
            string name = row.displayName;
            if (row.Entry != null && row.Entry.Estimated) name += "  (estimated)";
            if (row.Param != null && row.Param.TypeConflict) name += "  (types differ)";
            EditorGUI.LabelField(label, new GUIContent(name, row.Entry != null ? row.Entry.Path : null), row.Header ? EditorStyles.boldLabel : EditorStyles.label);
            if (row.Bits >= 0)
                EditorGUI.LabelField(new Rect(rect.xMax - 150f, rect.y, 70f, rect.height), row.Bits + (row.Bits == 1 ? " bit" : " bits"), EditorStyles.miniLabel);
            if (row.Note.Length > 0)
                EditorGUI.LabelField(new Rect(rect.xMax - 80f, rect.y, 80f, rect.height), new GUIContent(row.Note, row.Note), EditorStyles.miniLabel);
        }

        protected override void DoubleClickedItem(int id)
        {
            var row = FindItem(id, rootItem) as Row;
            if (row == null || row.Source == null) return;
            Selection.activeObject = row.Source;
            EditorGUIUtility.PingObject(row.Source);
        }
    }

    /// <summary>
    /// The Menu manager list: the planned layout, searchable, each row with a drag handle, its source system's icon, its
    /// name and the submenu it is in. Dragging a row by its handle or choosing Move to... records a planned move; nothing
    /// changes until Apply.
    /// </summary>
    internal sealed class ManagerTree : TreeView
    {
        private const string DragKey = "RiryTools.MenuTool.Entry";
        private const float Handle = 14f;

        private sealed class Row : TreeViewItem
        {
            internal MenuEntry Entry;
        }

        internal Action<string, string, int> Move;
        internal Action<MenuEntry> Clicked;
        internal Action<MenuEntry> ContextMove;
        private MenuEntry root;
        private int handleDown = -1;
        private readonly Dictionary<MenuEntry, int> idOf = new Dictionary<MenuEntry, int>();

        internal ManagerTree(TreeViewState state) : base(state)
        {
            showAlternatingRowBackgrounds = true;
            rowHeight = 20f;
        }

        internal void Set(MenuEntry planned)
        {
            root = planned;
            Reload();
        }

        internal MenuEntry Selected
        {
            get
            {
                IList<int> selection = GetSelection();
                var row = selection.Count > 0 ? FindItem(selection[0], rootItem) as Row : null;
                return row != null ? row.Entry : null;
            }
        }

        internal void Select(MenuEntry entry)
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

        private void Add(List<TreeViewItem> items, MenuEntry menu, int depth, ref int id)
        {
            foreach (MenuEntry entry in menu.Children)
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
            float folderWidth = Mathf.Min(220f, rect.width * 0.35f);
            EditorGUI.LabelField(new Rect(x, rect.y, rect.xMax - x - folderWidth - 4f, rect.height),
                new GUIContent(row.Entry.Name + (row.Entry.IsSubMenu ? "  ▸" : ""), tip), row.Entry.IsSubMenu ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUI.LabelField(new Rect(rect.xMax - folderWidth, rect.y, folderWidth, rect.height), new GUIContent(folder, folder), EditorStyles.miniLabel);
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
            MenuEntry destination;
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
            MenuEntry moving = root.Find(from);
            if (moving == null || destination == moving || MenuToolScanner.IsUnder(destination, moving)) return DragAndDropVisualMode.Rejected;
            if (args.performDrop && Move != null) Move(from, destination.Path, index);
            return DragAndDropVisualMode.Move;
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool trees
// ============================================================================
//
// MenuToolIcons              Origin(entry): the contributing VRCFury or Modular Avatar component's own icon (the installed
//                            package's, looked up per type); TypeName(control type).
// BudgetTree (this file)     Parameter budget tab. Main Menu: every submenu and control with its synced bits (each parameter
//                            once, MenuToolBudget.Bits), parameters as the note, shaded red by synced Floats (darker = more,
//                            relative to maxFloats); entries outside the target are hidden (InFilter). Animator only: per
//                            controller and layer, synced parameters no menu uses, then those no layer uses. Double-click
//                            selects and pings the source.
// ManagerTree                Menu manager tab: the planned layout. Rows: drag handle (≡, only it starts a drag; none while
//                            searching), the source's component icon, the control's icon, name, and the folder (submenu) it is
//                            in. Search matches path and parameters. Drag onto a submenu (into it), onto a control (before it)
//                            or between rows: Move(from, to, index). Right-click: ContextMove (Move to...). Selection: Clicked.
