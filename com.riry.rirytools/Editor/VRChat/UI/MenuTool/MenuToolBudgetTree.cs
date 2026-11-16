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
    /// The Parameter budget tree: the assembled menu with each submenu's and control's synced bits (each parameter once),
    /// shaded darker red the more synced Floats it holds; then the Animator only block: synced parameters no menu uses,
    /// under the controllers and layers that use them. Double-click selects the source.
    /// </summary>
    internal sealed class MenuToolBudgetTree : TreeView
    {
        private sealed class Row : TreeViewItem
        {
            internal MenuToolEntry Entry;
            internal MenuToolParamEntry Param;
            internal UnityEngine.Object Source;
            internal int Bits = -1;
            internal int Floats;
            internal string Note = "";
            internal bool Header;
        }

        private const float BitsWidth = 70f;
        private const float NoteWidth = 80f;
        // The narrowest the name gets before a column gives way.
        private const float MinName = 80f;

        private MenuToolScan scan;
        private int maxFloats = 1;

        internal MenuToolBudgetTree(TreeViewState state) : base(state)
        {
            showAlternatingRowBackgrounds = true;
            rowHeight = 18f;
        }

        /// <summary>Rebuilds the rows; expandAll opens every row (a new target), otherwise rows keep how the user left them.</summary>
        internal void Set(MenuToolScan value, bool expandAll)
        {
            scan = value;
            Reload();
            if (expandAll) ExpandAll();
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
            MenuToolEntry menu = scan.Root;
            var menuRow = new Row { id = id++, depth = 1, displayName = "Root Menu", Entry = menu, Source = menu.Source, Bits = MenuToolBudget.Bits(scan, menu), Floats = MenuToolBudget.SyncedFloats(scan, menu) };
            items.Add(menuRow);
            maxFloats = Math.Max(1, menuRow.Floats);
            AddEntries(items, menu, 2, ref id);
            items.Add(new Row { id = id++, depth = 0, displayName = "Animator only (synced, no menu uses them)", Header = true });
            List<MenuToolParamEntry> animatorOnly = MenuToolBudget.AnimatorOnly(scan);
            var placed = new HashSet<string>();
            foreach (MenuToolControllerUsage controller in scan.Controllers)
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
                        MenuToolParamEntry param = scan.Param(name);
                        placed.Add(name);
                        items.Add(new Row { id = id++, depth = 3, displayName = name, Param = param, Source = param.Sources.FirstOrDefault(), Bits = param.Bits, Note = param.Type.ToString() });
                    }
                }
            }
            List<MenuToolParamEntry> unused = animatorOnly.Where(p => !placed.Contains(p.Name)).ToList();
            if (unused.Count > 0)
            {
                items.Add(new Row { id = id++, depth = 1, displayName = "Not used by any animator layer", Bits = unused.Sum(p => p.Bits) });
                foreach (MenuToolParamEntry param in unused)
                    items.Add(new Row { id = id++, depth = 2, displayName = param.Name, Param = param, Source = param.Sources.FirstOrDefault(), Bits = param.Bits, Note = param.Type.ToString() });
            }
            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }

        private void AddEntries(List<TreeViewItem> items, MenuToolEntry menu, int depth, ref int id)
        {
            foreach (MenuToolEntry entry in menu.Children)
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
        private bool InFilter(MenuToolEntry entry)
        {
            if (scan.InFilter(entry.Source)) return true;
            return entry.IsSubMenu && entry.Descendants().Any(child => scan.InFilter(child.Source));
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var row = (Row)args.item;
            Rect rect = args.rowRect;
            // The Root Menu row is not shaded.
            if (Event.current.type == EventType.Repaint && row.Floats > 0 && row.Entry != scan.Root)
            {
                // The more synced Floats, the darker the red.
                float t = Mathf.Clamp01((float)row.Floats / maxFloats);
                EditorGUI.DrawRect(rect, Color.Lerp(new Color(0.85f, 0.35f, 0.35f, 0.25f), new Color(0.45f, 0f, 0f, 0.75f), t));
            }
            float indent = GetContentIndent(row);
            // The columns give way when the row is narrow or deeply nested, so the name never runs under them.
            float free = rect.width - indent;
            bool showNote = row.Note.Length > 0 && free > NoteWidth + BitsWidth + MinName;
            bool showBits = row.Bits >= 0 && free > BitsWidth + MinName;
            float columns = (showNote ? NoteWidth : 0f) + (showBits ? BitsWidth : 0f);
            var label = new Rect(rect.x + indent, rect.y, Mathf.Max(0f, free - columns), rect.height);
            string name = row.displayName;
            if (row.Entry != null && row.Entry.Estimated) name += "  (estimated)";
            if (row.Param != null && row.Param.TypeConflict) name += "  (types differ)";
            string tip = row.Entry != null ? row.Entry.Path : row.displayName;
            if (!showBits && row.Bits >= 0) tip += "\n" + row.Bits + (row.Bits == 1 ? " bit" : " bits");
            if (!showNote && row.Note.Length > 0) tip += "\n" + row.Note;
            EditorGUI.LabelField(label, new GUIContent(name, tip), row.Header ? EditorStyles.boldLabel : EditorStyles.label);
            if (showBits)
                EditorGUI.LabelField(new Rect(rect.xMax - columns, rect.y, BitsWidth, rect.height), row.Bits + (row.Bits == 1 ? " bit" : " bits"), EditorStyles.miniLabel);
            if (showNote)
                EditorGUI.LabelField(new Rect(rect.xMax - NoteWidth, rect.y, NoteWidth, rect.height), new GUIContent(row.Note, row.Note), EditorStyles.miniLabel);
        }

        protected override void DoubleClickedItem(int id)
        {
            var row = FindItem(id, rootItem) as Row;
            if (row == null || row.Source == null) return;
            Selection.activeObject = row.Source;
            EditorGUIUtility.PingObject(row.Source);
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool budget tree
// ============================================================================
//
// MenuToolBudgetTree (this file)     Parameter budget tab. Main Menu: every submenu and control with its synced bits (each parameter
//                            once, MenuToolBudget.Bits), parameters as the note (both columns give way to the name below
//                            MinName, their values then in the tooltip), shaded red by synced Floats (darker = more,
//                            relative to maxFloats; the Root Menu row itself unshaded); entries outside the target are hidden
//                            (InFilter). Animator only: per controller and layer, synced parameters no menu uses, then those
//                            no layer uses. Double-click selects and pings the source. Set(scan, expandAll): every row opened
//                            only when expandAll (a new target); otherwise collapsed rows stay collapsed.
