using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Changes to the flat block list.</summary>
    internal static class MenuMockupModel
    {
        internal static List<MenuMockupBlock> Children(List<MenuMockupBlock> blocks, int parent)
        {
            return blocks.FindAll(block => block.Parent == parent && block.Id != parent);
        }

        internal static MenuMockupBlock Find(List<MenuMockupBlock> blocks, int id)
        {
            return blocks.Find(block => block.Id == id);
        }

        /// <summary>Puts a block (already in the list or new) at index among the parent's other children.</summary>
        internal static void Place(List<MenuMockupBlock> blocks, MenuMockupBlock block, int parent, int index)
        {
            blocks.Remove(block);
            block.Parent = parent;
            List<MenuMockupBlock> siblings = Children(blocks, parent);
            if (index >= 0 && index < siblings.Count) blocks.Insert(blocks.IndexOf(siblings[index]), block);
            else blocks.Add(block);
        }

        /// <summary>
        /// Place for a drop in Edit, where index counts only the blocks shown: in a Tabs block those on its shown tab.
        /// The block joins that tab; elsewhere its tab is cleared.
        /// </summary>
        internal static void PlaceShown(List<MenuMockupBlock> blocks, MenuMockupBlock block, int parent, int index)
        {
            MenuMockupBlock owner = Find(blocks, parent);
            if (owner == null || owner.Type != MenuMockupTypes.Tabs)
            {
                block.Tab = 0;
                Place(blocks, block, parent, index);
                return;
            }
            int tab = Math.Max(0, Math.Min(owner.ActiveTab, MenuMockupTypes.TabNames(owner).Count - 1));
            List<MenuMockupBlock> siblings = Children(blocks, parent);
            siblings.Remove(block);
            List<MenuMockupBlock> shown = siblings.FindAll(child => MenuMockupTypes.TabOf(owner, child) == tab);
            block.Tab = tab;
            int at;
            if (index >= 0 && index < shown.Count) at = siblings.IndexOf(shown[index]);
            else if (shown.Count > 0) at = siblings.IndexOf(shown[shown.Count - 1]) + 1;
            else at = siblings.Count;
            Place(blocks, block, parent, at);
        }

        /// <summary>Removes a block and every block inside it.</summary>
        internal static void Delete(List<MenuMockupBlock> blocks, MenuMockupBlock block)
        {
            foreach (MenuMockupBlock child in Children(blocks, block.Id)) Delete(blocks, child);
            blocks.Remove(block);
        }

        internal static MenuMockupBlock Create(List<MenuMockupBlock> blocks, string type)
        {
            int id = 0;
            foreach (MenuMockupBlock block in blocks) id = Math.Max(id, block.Id);
            MenuMockupTypes.Kind kind = MenuMockupTypes.Find(type);
            return new MenuMockupBlock { Id = id + 1, Type = kind.Name, Content = kind.Default ?? "" };
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup model
// Menu Mockup (a Debug tool): the windows are in Editor/UI/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupModel               Children, Find, Place (move or insert at an index), PlaceShown (an index among the shown
//                           blocks: in a Tabs block, those on its shown tab, which the block joins), Delete, Create.
