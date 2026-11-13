using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace Riry.RiryTools.Editor
{
    /// <summary>Menu Tool scan: the avatar's own expressions menu and the menu assets it links (native menus).</summary>
    internal static partial class MenuToolScanner
    {
        // ---- Native --------------------------------------------------------------

        private static void AddNative(Context context)
        {
            VRCAvatarDescriptor avatar = context.Scan.Avatar;
            if (avatar == null) return;
            Block(context.Scan, BaseBlock);
            if (!avatar.customExpressions)
            {
                context.Scan.Note(avatar.name + " uses the default expressions (Customize Expressions is off): no menu or parameters of its own.");
                return;
            }
            if (avatar.expressionsMenu == null) context.Scan.Note(avatar.name + " has no Expressions Menu set.");
            else
            {
                context.Scan.Root.SubMenuAsset = avatar.expressionsMenu;
                context.Scan.Root.Source = avatar.expressionsMenu;
                AddMenuAsset(context, context.Scan.Root, avatar.expressionsMenu, MenuToolOrigin.Native, avatar.expressionsMenu, BaseBlock, null,
                    new HashSet<VRCExpressionsMenu>());
            }
            if (avatar.expressionParameters == null) context.Scan.Note(avatar.name + " has no Expression Parameters set.");
            else
            {
                foreach (VRCExpressionParameters.Parameter parameter in avatar.expressionParameters.parameters ?? new VRCExpressionParameters.Parameter[0])
                {
                    if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
                    Declare(context.Scan, parameter.name, parameter.valueType, parameter.networkSynced, parameter.saved, parameter.defaultValue,
                        BaseBlock, avatar.expressionParameters, false, WidthOf(parameter), FlippedOf(parameter));
                }
            }
        }

        /// <summary>
        /// Adds the controls of a menu asset (and its submenus) under parent. edit adjusts each new entry (ownership,
        /// parameter renames); a menu already being expanded is a cycle and is not expanded again.
        /// </summary>
        private static void AddMenuAsset(Context context, MenuToolEntry parent, VRCExpressionsMenu menu, MenuToolOrigin origin, UnityEngine.Object source,
            string block, Action<MenuToolEntry> edit, HashSet<VRCExpressionsMenu> expanding)
        {
            if (menu == null) return;
            if (!expanding.Add(menu))
            {
                context.Scan.Note("The menu " + menu.name + " contains itself through a submenu; it is shown once.");
                return;
            }
            List<VRCExpressionsMenu.Control> controls = menu.controls ?? new List<VRCExpressionsMenu.Control>();
            // VRCFury merges the Nth submenu of a name into the Nth matching submenu already there.
            var submenus = new Dictionary<string, int>();
            for (int i = 0; i < controls.Count; i++)
            {
                VRCExpressionsMenu.Control control = controls[i];
                if (control == null) continue;
                MenuToolEntry entry = FromControl(control, control.name);
                entry.Origin = origin;
                entry.Source = source;
                entry.Block = block;
                entry.OwnerMenu = menu;
                entry.OwnerIndex = i;
                if (edit != null) edit(entry);
                MenuToolEntry placed = origin == MenuToolOrigin.VRCFury && control.type == ControlType.SubMenu && control.subMenu != null
                    ? MergeInto(parent, entry, submenus) : AddTo(parent, entry);
                if (control.type != ControlType.SubMenu) continue;
                if (placed == entry) entry.SubMenuAsset = control.subMenu;
                if (control.subMenu == null) context.Scan.Note("The submenu " + placed.Path + " in " + menu.name + " has no menu asset.");
                else AddMenuAsset(context, placed, control.subMenu, origin, source, block, edit, expanding);
            }
            expanding.Remove(menu);
        }

        internal static MenuToolEntry FromControl(VRCExpressionsMenu.Control control, string name)
        {
            return new MenuToolEntry
            {
                Name = name ?? "",
                Type = control.type,
                Icon = control.icon,
                Parameter = control.parameter != null ? control.parameter.name ?? "" : "",
                Value = control.value,
                SubParameters = control.subParameters != null ? control.subParameters.Select(p => p != null ? p.name ?? "" : "").ToArray() : new string[0],
                Labels = control.labels != null ? control.labels.ToArray() : new VRCExpressionsMenu.Control.Label[0]
            };
        }

        private static MenuToolEntry AddTo(MenuToolEntry parent, MenuToolEntry entry)
        {
            parent.Add(entry);
            return entry;
        }

        // VRCFury's MergeMenu: the Nth submenu of a name (counted in its own menu) goes into the Nth submenu already there
        // that matches it (VRCFury's name matching), its controls following; with none, it is added. Returned: where its
        // controls go.
        private static MenuToolEntry MergeInto(MenuToolEntry parent, MenuToolEntry entry, Dictionary<string, int> submenus)
        {
            int count;
            int duplicate = submenus[entry.Name] = submenus.TryGetValue(entry.Name, out count) ? count + 1 : 0;
            List<MenuToolEntry> existing = MenuToolVrcfuryMenu.Matching(parent, entry.Name, child => child.IsSubMenu);
            if (duplicate < existing.Count) return existing[duplicate];
            parent.Add(entry);
            return entry;
        }

        // Makes the submenus VRCFury creates for a component's paths.
        private static Func<string, MenuToolEntry> Maker(Component component, string block)
        {
            return name => new MenuToolEntry { Name = name, Type = ControlType.SubMenu, Origin = MenuToolOrigin.VRCFury, Source = component, Block = block };
        }

        // VRCFury's NewMenuItem: the submenu a feature's path puts its control in (made where missing), and the control's name.
        private static MenuToolEntry ParentOf(Context context, Component component, string block, string path, out string leaf)
        {
            List<string> split = MenuToolVrcfuryMenu.Split(path);
            if (split.Count == 0) split.Add("");
            leaf = split[split.Count - 1];
            return MenuToolVrcfuryMenu.Folder(context.Scan.Root, split.Take(split.Count - 1).ToList(), true, Maker(component, block));
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool scanner: native menus
// MenuToolScanner is one partial class: MenuToolScanner.cs (the scan, references, parameters and controllers) and MenuToolScanner.Native.cs, .ModularAvatar.cs and .VRCFury.cs. It builds a MenuToolScan (MenuToolModel.cs) and is read-only.
// ============================================================================
//
// MenuToolScanner (this part)
//   AddMenuAsset             A menu asset's controls (and submenus), owned (OwnerMenu/OwnerIndex) by that asset; cycles noted.
//   MergeInto / AddTo        VRCFury's MergeMenu: the Nth submenu of a name goes into the Nth matching submenu already there
//                            (MenuToolVrcfuryMenu.Matching); everything else is appended.
//   Maker / ParentOf         The submenus VRCFury makes for a path; NewMenuItem's parent and control name.
