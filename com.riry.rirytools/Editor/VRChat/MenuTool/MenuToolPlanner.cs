using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>Applies a plan to a copy of the assembled menu (the preview), and checks each move.</summary>
    internal static class MenuToolPlanner
    {
        internal const string MoveRule = "Move Menu Item (VRCFury)";

        /// <summary>The planned layout: a copy of the scan's menu with every move applied in order; checks says how each went.</summary>
        internal static MenuToolEntry Planned(MenuToolScan scan, MenuToolPlan plan, out List<MenuToolMoveCheck> checks)
        {
            checks = new List<MenuToolMoveCheck>();
            MenuToolEntry root = scan.Root.Clone();
            foreach (MenuToolPlannedMove move in plan.Moves)
            {
                if (move.IsAdd || move.IsAddControl)
                {
                    MenuToolMoveCheck added = CheckAdd(scan, root, move);
                    checks.Add(added);
                    if (added.Errors.Count > 0) continue;
                    var made = new MenuToolEntry
                    {
                        Name = (move.IsAdd ? move.NewMenu : move.NewControl).Trim(),
                        Type = move.IsAdd ? VRCExpressionsMenu.Control.ControlType.SubMenu : (VRCExpressionsMenu.Control.ControlType)move.ControlType,
#if RIRYTOOLS_MENUTOOL_MA
                        Origin = added.Edit == MenuToolMoveEdit.NewMaItem || added.Edit == MenuToolMoveEdit.NewControlMa ? MenuToolOrigin.ModularAvatar
                            : added.Edit == MenuToolMoveEdit.NewControlVrcfury ? MenuToolOrigin.VRCFury : MenuToolOrigin.Native,
#else
                        Origin = added.Edit == MenuToolMoveEdit.NewControlVrcfury ? MenuToolOrigin.VRCFury : MenuToolOrigin.Native,
#endif
                        Added = added.Edit
                    };
                    if (move.IsAddControl) ControlOf(move, made);
                    added.Destination.Add(made);
                    added.Entry = made;
                    continue;
                }
                if (move.IsRename)
                {
                    MenuToolMoveCheck renamed = CheckRename(scan, root, move);
                    checks.Add(renamed);
                    if (renamed.Errors.Count == 0) renamed.Entry.Name = move.Rename.Trim();
                    continue;
                }
                if (move.Remove || move.ToNull)
                {
                    MenuToolMoveCheck removed = CheckRemove(scan, root, move);
                    checks.Add(removed);
                    if (removed.Errors.Count == 0) removed.Entry.Parent.Children.Remove(removed.Entry);
                    continue;
                }
                if (move.AssignIcon)
                {
                    MenuToolMoveCheck icon = CheckIcon(scan, root, move);
                    checks.Add(icon);
                    if (icon.Errors.Count == 0) icon.Entry.Icon = move.IconTexture;
                    continue;
                }
                MenuToolMoveCheck check = Check(scan, root, move);
                checks.Add(check);
                if (check.Entry == null || check.Destination == null || check.Errors.Count > 0) continue;
                int index = move.Index;
                if (check.Entry.Parent == check.Destination && index > check.Destination.Children.IndexOf(check.Entry)) index--;
                check.Destination.Add(check.Entry, index);
                check.After = check.Entry.Path;
            }
            return root;
        }

        private static MenuToolMoveCheck Check(MenuToolScan scan, MenuToolEntry root, MenuToolPlannedMove move)
        {
            var check = new MenuToolMoveCheck { Move = move, Before = move.From };
            check.Entry = string.IsNullOrEmpty(move.From) ? null : root.Find(move.From);
            check.Destination = root.Find(move.To ?? "");
            if (check.Entry == null)
            {
                check.Errors.Add(move.From + " is no longer in the menu.");
                return check;
            }
            if (check.Destination == null || !check.Destination.IsSubMenu)
            {
                check.Errors.Add("The destination " + (string.IsNullOrEmpty(move.To) ? "root menu" : move.To) + " is no longer a submenu.");
                return check;
            }
            if (check.Entry.Parent != null && check.Entry.Parent.Children.Count(child => child.Name == check.Entry.Name) > 1)
            {
                check.Errors.Add(check.Entry.Parent.Path + " has more than one item named " + check.Entry.Name + ", so the path is ambiguous; rename one first.");
                return check;
            }
            if (check.Destination == check.Entry || MenuToolScanner.IsUnder(check.Destination, check.Entry))
            {
                check.Errors.Add("A submenu cannot go inside itself (" + move.From + " into " + move.To + ").");
                return check;
            }
            bool sameMenu = check.Entry.Parent == check.Destination;
            int count = check.Destination.Children.Count + (sameMenu ? 0 : 1);
            CheckCapacity(scan, check, string.IsNullOrEmpty(move.To) ? "The root menu" : move.To, count);
            if (!sameMenu && check.Destination.Children.Any(child => child.Name == check.Entry.Name))
                check.Warnings.Add(check.Destination.Path + " already has an item named " + check.Entry.Name +
                    (check.Entry.IsSubMenu && scan.VrcfuryInstalled ? "; VRCFury merges submenus of the same name." : "."));
            check.After = (check.Destination.Path.Length == 0 ? "" : check.Destination.Path + "/") + MenuToolVrcfuryMenu.Escape(check.Entry.Name);
            if (move.Destructive) Destructive(scan, check);
            else Ownership(scan, check, sameMenu);
            return check;
        }

        /// <summary>A new control's fields from its planned add: puppets take the parameter as their first sub-parameter.</summary>
        internal static void ControlOf(MenuToolPlannedMove move, MenuToolEntry entry)
        {
            entry.Icon = move.IconTexture;
            entry.Value = move.Value;
            int axes = Axes(entry.Type);
            if (axes == 0) entry.Parameter = move.Parameter ?? "";
            else
            {
                entry.SubParameters = new string[axes];
                entry.SubParameters[0] = move.Parameter ?? "";
                for (int i = 1; i < axes; i++) entry.SubParameters[i] = "";
            }
        }

        // Sub-parameters a control type uses (0 for Toggle and Button).
        internal static int Axes(VRCExpressionsMenu.Control.ControlType type)
        {
            switch (type)
            {
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet: return 1;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet: return 2;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet: return 4;
                default: return 0;
            }
        }

        // Assign Menu Icon: the entry and texture must exist; who owns the entry picks the edit.
        private static MenuToolMoveCheck CheckIcon(MenuToolScan scan, MenuToolEntry root, MenuToolPlannedMove move)
        {
            Texture2D texture = move.IconTexture;
            var check = new MenuToolMoveCheck { Move = move, Before = move.From, After = move.From + " (icon " + (texture != null ? texture.name : "?") + ")" };
            check.Entry = root.Find(move.From ?? "");
            if (check.Entry == null || check.Entry.Parent == null)
            {
                check.Errors.Add(move.From + " is no longer in the menu.");
                return check;
            }
            if (texture == null)
            {
                check.Errors.Add("The icon texture is no longer in the project.");
                return check;
            }
            if (check.Entry.Generated)
            {
                check.Errors.Add(check.Entry.Name + " is made by VRCFury's pagination and cannot take an icon.");
                return check;
            }
            MenuToolEntry entry = check.Entry;
            bool asset = entry.Origin == MenuToolOrigin.Native && (entry.OwnerMenu != null || entry.Added == MenuToolMoveEdit.NewControlAsset || entry.Added == MenuToolMoveEdit.NewMenuAsset);
#if RIRYTOOLS_MENUTOOL_MA
            bool ma = entry.Origin == MenuToolOrigin.ModularAvatar && (entry.MaItem != null && !EditorUtility.IsPersistent(entry.MaItem) || entry.Added == MenuToolMoveEdit.NewControlMa || entry.Added == MenuToolMoveEdit.NewMaItem);
#endif
            if (asset) check.Candidates.Add(MenuToolMoveEdit.IconAsset);
#if RIRYTOOLS_MENUTOOL_MA
            if (ma) check.Candidates.Add(MenuToolMoveEdit.IconMa);
#endif
            if (scan.VrcfuryInstalled && scan.Avatar != null) check.Candidates.Add(MenuToolMoveEdit.IconVrcfury);
#if RIRYTOOLS_MENUTOOL_MA
            Decide(check, entry.Origin == MenuToolOrigin.Native && asset ? MenuToolMoveEdit.IconAsset : entry.Origin == MenuToolOrigin.ModularAvatar && ma ? MenuToolMoveEdit.IconMa
                : entry.Origin == MenuToolOrigin.VRCFury && check.Candidates.Contains(MenuToolMoveEdit.IconVrcfury) ? MenuToolMoveEdit.IconVrcfury : MenuToolMoveEdit.Ambiguous,
                "No supported edit sets the icon of " + entry.Path + ".");
#else
            Decide(check, entry.Origin == MenuToolOrigin.Native && asset ? MenuToolMoveEdit.IconAsset
                : entry.Origin == MenuToolOrigin.VRCFury && check.Candidates.Contains(MenuToolMoveEdit.IconVrcfury) ? MenuToolMoveEdit.IconVrcfury : MenuToolMoveEdit.Ambiguous,
                "No supported edit sets the icon of " + entry.Path + ".");
#endif
            if (check.Edit == MenuToolMoveEdit.IconAsset && entry.OwnerMenu != null) check.Affected.Add(entry.OwnerMenu);
#if RIRYTOOLS_MENUTOOL_MA
            if (check.Edit == MenuToolMoveEdit.IconMa && entry.MaItem != null) check.Affected.Add(entry.MaItem);
#endif
            if (check.Edit == MenuToolMoveEdit.IconVrcfury) check.Affected.Add(scan.Avatar.gameObject);
            return check;
        }

        // Delete Menu Entry: the entry must exist and be a control in a menu asset, which it is deleted from. Move to… > <NULL>:
        // any entry, through a VRCFury Move Menu Item rule with a blank destination.
        private static MenuToolMoveCheck CheckRemove(MenuToolScan scan, MenuToolEntry root, MenuToolPlannedMove move)
        {
            var check = new MenuToolMoveCheck { Move = move, Before = move.From, After = move.ToNull ? "<NULL>" : move.From + " (deleted)" };
            check.Entry = root.Find(move.From ?? "");
            if (check.Entry == null || check.Entry.Parent == null)
            {
                check.Errors.Add(move.From + " is no longer in the menu.");
                return check;
            }
            MenuToolEntry entry = check.Entry;
            if (entry.Generated)
            {
                check.Errors.Add(entry.Name + " is made by VRCFury's pagination and cannot be removed.");
                return check;
            }
            if (move.ToNull)
            {
                if (scan.VrcfuryInstalled && scan.Avatar != null) check.Candidates.Add(MenuToolMoveEdit.RemoveVrcfury);
                Decide(check, MenuToolMoveEdit.RemoveVrcfury, "Move to <NULL> needs VRCFury and an avatar descriptor.");
            }
            else
            {
                if (entry.Origin == MenuToolOrigin.Native && entry.OwnerMenu != null) check.Candidates.Add(MenuToolMoveEdit.RemoveAsset);
                Decide(check, MenuToolMoveEdit.RemoveAsset, entry.Path + " is not in a menu asset: use Move to… > <NULL> for VRCFury items.");
            }
            if (check.Edit == MenuToolMoveEdit.RemoveAsset) check.Affected.Add(entry.OwnerMenu);
            if (check.Edit == MenuToolMoveEdit.RemoveVrcfury) check.Affected.Add(scan.Avatar.gameObject);
            return check;
        }

        // Rename Menu Entry: the entry must exist, the name not be empty or taken in its submenu; a menu-asset control is renamed
        // in its asset, otherwise (or by choice) a VRCFury Move Menu Item rule renames it.
        private static MenuToolMoveCheck CheckRename(MenuToolScan scan, MenuToolEntry root, MenuToolPlannedMove move)
        {
            string name = move.Rename.Trim();
            var check = new MenuToolMoveCheck { Move = move, Before = move.From };
            check.Entry = root.Find(move.From ?? "");
            if (check.Entry == null || check.Entry.Parent == null)
            {
                check.Errors.Add(move.From + " is no longer in the menu.");
                return check;
            }
            MenuToolEntry entry = check.Entry;
            check.After = (entry.Parent.Path.Length == 0 ? "" : entry.Parent.Path + "/") + MenuToolVrcfuryMenu.Escape(name);
            if (entry.Generated)
            {
                check.Errors.Add(entry.Name + " is made by VRCFury's pagination and cannot be renamed.");
                return check;
            }
            if (name.Length == 0) check.Errors.Add("A name cannot be empty.");
            else if (entry.Parent.Children.Any(child => child != entry && child.Name == name)) check.Errors.Add((entry.Parent.Parent == null ? "The root menu" : entry.Parent.Path) + " already has an item named " + name + ".");
            if (check.Errors.Count > 0) return check;
            bool asset = entry.Origin == MenuToolOrigin.Native && entry.OwnerMenu != null;
            if (asset) check.Candidates.Add(MenuToolMoveEdit.RenameAsset);
            if (scan.VrcfuryInstalled && scan.Avatar != null) check.Candidates.Add(MenuToolMoveEdit.RenameVrcfury);
            Decide(check, asset ? MenuToolMoveEdit.RenameAsset : entry.Origin == MenuToolOrigin.VRCFury ? MenuToolMoveEdit.RenameVrcfury : MenuToolMoveEdit.Ambiguous,
                "No supported edit renames " + entry.Path + ".");
            if (check.Edit == MenuToolMoveEdit.RenameAsset) check.Affected.Add(entry.OwnerMenu);
            if (check.Edit == MenuToolMoveEdit.RenameVrcfury) check.Affected.Add(scan.Avatar.gameObject);
            return check;
        }

        // The edit: the user's Choice when it fits, else preferred (the only candidate when there is one); none is Invalid.
        private static void Decide(MenuToolMoveCheck check, MenuToolMoveEdit preferred, string none)
        {
            if (check.Candidates.Count == 0)
            {
                check.Edit = MenuToolMoveEdit.Invalid;
                check.Errors.Add(none);
                return;
            }
            if (check.Candidates.Count == 1) preferred = check.Candidates[0];
            MenuToolMoveEdit chosen;
            if (!string.IsNullOrEmpty(check.Move.Choice) && Enum.TryParse(check.Move.Choice, out chosen) && check.Candidates.Contains(chosen)) check.Edit = chosen;
            else check.Edit = preferred;
            if (check.Edit == MenuToolMoveEdit.Ambiguous) check.Warnings.Add("More than one source could make this: choose one.");
        }

        // Add menu / Add New Control: the parent must still be a submenu with room and no item of that name; then who owns
        // the parent picks the edit.
        private static MenuToolMoveCheck CheckAdd(MenuToolScan scan, MenuToolEntry root, MenuToolPlannedMove move)
        {
            if (move.IsAddControl) return CheckAddControl(scan, root, move);
            string name = move.NewMenu.Trim();
            string parentName = string.IsNullOrEmpty(move.To) ? "the root menu" : move.To;
            var check = new MenuToolMoveCheck { Move = move, Before = "New menu" };
            check.Destination = root.Find(move.To ?? "");
            check.After = (string.IsNullOrEmpty(move.To) ? "" : move.To + "/") + name;
            if (!CheckNewItem(check, "menu", name, parentName)) return check;
            int count = check.Destination.Children.Count + 1;
            CheckCapacity(scan, check, parentName, count);
            MenuToolEntry parent = check.Destination;
            if (CanAddNative(scan, parent)) check.Candidates.Add(MenuToolMoveEdit.NewMenuAsset);
#if RIRYTOOLS_MENUTOOL_MA
            if (CanAddMa(scan, parent)) check.Candidates.Add(MenuToolMoveEdit.NewMaItem);
#endif
            if (check.Candidates.Count == 0)
            {
                check.Edit = MenuToolMoveEdit.Invalid;
#if RIRYTOOLS_MENUTOOL_MA
                check.Errors.Add("No supported edit adds a menu to " + parentName + " (it needs a menu asset, or Modular Avatar; VRCFury cannot make an empty submenu).");
#else
                check.Errors.Add("No supported edit adds a menu to " + parentName + " (it needs a menu asset; VRCFury cannot make an empty submenu).");
#endif
                return check;
            }
            // Who owns the parent: a native menu gets a menu asset, an MA children submenu an MA item; the user chooses otherwise.
            MenuToolMoveEdit preferred = check.Candidates.Count == 1 ? check.Candidates[0]
                : parent.Origin == MenuToolOrigin.Native && check.Candidates.Contains(MenuToolMoveEdit.NewMenuAsset) ? MenuToolMoveEdit.NewMenuAsset
#if RIRYTOOLS_MENUTOOL_MA
                : MaChildren(parent) ? MenuToolMoveEdit.NewMaItem : MenuToolMoveEdit.Ambiguous;
#else
                : MenuToolMoveEdit.Ambiguous;
#endif
            MenuToolMoveEdit chosen;
            if (!string.IsNullOrEmpty(move.Choice) && Enum.TryParse(move.Choice, out chosen) && check.Candidates.Contains(chosen)) check.Edit = chosen;
            else check.Edit = preferred;
            if (check.Edit == MenuToolMoveEdit.Ambiguous) check.Warnings.Add("More than one source could add this menu: choose one.");
            Affected(scan, check);
            return check;
        }

        private static MenuToolMoveCheck CheckAddControl(MenuToolScan scan, MenuToolEntry root, MenuToolPlannedMove move)
        {
            string name = move.NewControl.Trim();
            var type = (VRCExpressionsMenu.Control.ControlType)move.ControlType;
            string parentName = string.IsNullOrEmpty(move.To) ? "the root menu" : move.To;
            var check = new MenuToolMoveCheck { Move = move, Before = "New control" };
            check.Destination = root.Find(move.To ?? "");
            check.After = (string.IsNullOrEmpty(move.To) ? "" : move.To + "/") + name;
            if (!CheckNewItem(check, "control", name, parentName)) return check;
            int count = check.Destination.Children.Count + 1;
            CheckCapacity(scan, check, parentName, count);
            MenuToolEntry parent = check.Destination;
            if (CanAddNative(scan, parent)) check.Candidates.Add(MenuToolMoveEdit.NewControlAsset);
#if RIRYTOOLS_MENUTOOL_MA
            if (CanAddMa(scan, parent)) check.Candidates.Add(MenuToolMoveEdit.NewControlMa);
#endif
            if (scan.VrcfuryInstalled && scan.Avatar != null && type == VRCExpressionsMenu.Control.ControlType.Toggle)
                check.Candidates.Add(MenuToolMoveEdit.NewControlVrcfury);
            Decide(check, parent.Origin == MenuToolOrigin.Native && check.Candidates.Contains(MenuToolMoveEdit.NewControlAsset) ? MenuToolMoveEdit.NewControlAsset
#if RIRYTOOLS_MENUTOOL_MA
                : MaChildren(parent) && check.Candidates.Contains(MenuToolMoveEdit.NewControlMa) ? MenuToolMoveEdit.NewControlMa : MenuToolMoveEdit.Ambiguous,
#else
                : MenuToolMoveEdit.Ambiguous,
#endif
                "No supported edit adds a control to " + parentName + ".");
            if (check.Errors.Count > 0) return check;
            // VRCFury makes its toggle's parameter (a global one when named); the others need a declared one.
            string parameter = (move.Parameter ?? "").Trim();
            if (check.Edit != MenuToolMoveEdit.NewControlVrcfury && parameter.Length > 0 && scan.Param(parameter) == null)
                check.Warnings.Add(parameter + " is not a declared parameter: add it to the avatar's parameters.");
            if (Axes(type) > 1) check.Warnings.Add("Only the first axis parameter is set; set the others in the source after Apply.");
            if (check.Edit == MenuToolMoveEdit.NewControlAsset)
            {
                UnityEngine.Object parentMenu = parent.Parent == null ? scan.Avatar.expressionsMenu : parent.SubMenuAsset;
                if (parentMenu != null) check.Affected.Add(parentMenu);
            }
#if RIRYTOOLS_MENUTOOL_MA
            else if (check.Edit == MenuToolMoveEdit.NewControlMa) check.Affected.Add(parent.MaChildrenSource != null ? (UnityEngine.Object)parent.MaChildrenSource : scan.Avatar.gameObject);
#endif
            else if (check.Edit == MenuToolMoveEdit.NewControlVrcfury) check.Affected.Add(scan.Avatar.gameObject);
            return check;
        }

        // Over MaxControls: a warning when VRCFury pages the rest, else an error. menuName starts the message.
        private static void CheckCapacity(MenuToolScan scan, MenuToolMoveCheck check, string menuName, int count)
        {
            if (count <= MenuToolScanner.MaxControls) return;
            if (scan.VrcfuryInstalled) check.Warnings.Add(menuName + " will hold " + count + " controls: VRCFury pages the rest into a Next submenu.");
            else check.Errors.Add(menuName + " would hold " + count + " controls; VRChat allows " + MenuToolScanner.MaxControls + ".");
        }

        // Add menu / Add New Control: a usable name, the parent (check.Destination) still a submenu, and no item of that name
        // there. kind ("menu" or "control") names the item in the message. False with the first problem in check.Errors.
        private static bool CheckNewItem(MenuToolMoveCheck check, string kind, string name, string parentName)
        {
            if (name.Length == 0 || name.Contains("/")) check.Errors.Add("A " + kind + " name cannot be empty or contain /.");
            else if (check.Destination == null || !check.Destination.IsSubMenu) check.Errors.Add("The parent " + parentName + " is no longer a submenu.");
            else if (check.Destination.Children.Any(child => child.Name == name)) check.Errors.Add(parentName + " already has an item named " + name + ".");
            return check.Errors.Count == 0;
        }

        // Add menu / Add New Control: the parent takes a new item in its menu asset.
        private static bool CanAddNative(MenuToolScan scan, MenuToolEntry parent)
        {
            return scan.Avatar != null && NativeAsset(scan, parent);
        }

#if RIRYTOOLS_MENUTOOL_MA
        // Add menu / Add New Control: the parent takes a new Modular Avatar item (the avatar is in a scene, and the parent is an
        // MA children submenu, the root, a native submenu with an asset, or a menu asset the plan adds).
        private static bool CanAddMa(MenuToolScan scan, MenuToolEntry parent)
        {
            return scan.ModularAvatarInstalled && scan.Avatar != null && !EditorUtility.IsPersistent(scan.Avatar) &&
                (MaChildren(parent) || parent.Parent == null || parent.SubMenuAsset != null || parent.Added == MenuToolMoveEdit.NewMenuAsset);
        }

#endif
        // A native submenu with its own menu asset (the root: the descriptor's menu), or one the plan adds as a menu asset.
        private static bool NativeAsset(MenuToolScan scan, MenuToolEntry menu)
        {
            if (menu.Origin != MenuToolOrigin.Native) return false;
            if (menu.Parent == null) return scan.Avatar != null && scan.Avatar.expressionsMenu != null;
            return menu.SubMenuAsset != null || menu.Added == MenuToolMoveEdit.NewMenuAsset;
        }

#if RIRYTOOLS_MENUTOOL_MA
        // A Modular Avatar submenu whose items are an object's children, or one the plan adds as an MA item.
        private static bool MaChildren(MenuToolEntry menu)
        {
            return menu.MaChildrenSource != null || menu.Added == MenuToolMoveEdit.NewMaItem;
        }

#endif
        // Chooses the edit from the origin of the entry and the destination (the spec's table).
        private static void Ownership(MenuToolScan scan, MenuToolMoveCheck check, bool sameMenu)
        {
            MenuToolEntry entry = check.Entry, destination = check.Destination;
            bool vrcfuryRule = scan.VrcfuryInstalled && scan.Avatar != null;
            if (entry.Generated)
            {
                check.Errors.Add(entry.Name + " is made by VRCFury's pagination and cannot be moved.");
                return;
            }
            switch (entry.Origin)
            {
                case MenuToolOrigin.VRCFury:
                    check.Candidates.Add(MenuToolMoveEdit.VrcfuryRule);
                    if (!vrcfuryRule) check.Errors.Add("VRCFury rules need VRCFury and an avatar descriptor.");
                    break;
                case MenuToolOrigin.Native:
                    // A menu the plan adds counts as made: Apply makes it before the moves after it.
                    if (NativeAsset(scan, destination) && (entry.OwnerMenu != null || entry.Added == MenuToolMoveEdit.NewMenuAsset || entry.Added == MenuToolMoveEdit.NewControlAsset))
                        check.Candidates.Add(MenuToolMoveEdit.NativeAssets);
                    if (vrcfuryRule) check.Candidates.Add(MenuToolMoveEdit.VrcfuryRule);
                    break;
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolOrigin.ModularAvatar:
                    if ((entry.MaItem != null && CanReparent(entry.MaItem.gameObject) || entry.Added == MenuToolMoveEdit.NewMaItem || entry.Added == MenuToolMoveEdit.NewControlMa) && MaChildren(destination))
                        check.Candidates.Add(MenuToolMoveEdit.MaReparent);
                    if (entry.MaInstaller != null && (entry.MaItem == null || entry.MaItem.gameObject == entry.MaInstaller.gameObject) && entry.Parent != null &&
                        IsInstallRoot(entry) && !MaChildren(destination) &&
                        (destination.Parent == null || destination.SubMenuAsset != null || destination.Added == MenuToolMoveEdit.NewMenuAsset) && !sameMenu)
                        check.Candidates.Add(MenuToolMoveEdit.MaRetarget);
                    if (vrcfuryRule) check.Candidates.Add(MenuToolMoveEdit.VrcfuryRule);
                    break;
#endif
            }
#if RIRYTOOLS_MENUTOOL_MA
            if (sameMenu && entry.Origin == MenuToolOrigin.ModularAvatar && entry.Parent != null && MaChildren(entry.Parent) &&
                (entry.MaItem != null && CanReparent(entry.MaItem.gameObject) || entry.Added == MenuToolMoveEdit.NewMaItem) && !check.Candidates.Contains(MenuToolMoveEdit.MaReparent))
                check.Candidates.Insert(0, MenuToolMoveEdit.MaReparent);
#endif
            if (check.Candidates.Count == 0)
            {
                check.Edit = MenuToolMoveEdit.Invalid;
                check.Errors.Add("No supported edit moves " + entry.Path + " there" + (scan.VrcfuryInstalled ? "." : " (installing VRCFury would allow a Move Menu Item rule)."));
                return;
            }
            // The table's edit when it is clear; several candidates need the user's choice unless one was chosen.
            MenuToolMoveEdit preferred = Preferred(entry, check.Candidates);
            MenuToolMoveEdit chosen;
            if (!string.IsNullOrEmpty(check.Move.Choice) && Enum.TryParse(check.Move.Choice, out chosen) && check.Candidates.Contains(chosen)) check.Edit = chosen;
            else if (preferred != MenuToolMoveEdit.Ambiguous) check.Edit = preferred;
            else check.Edit = MenuToolMoveEdit.Ambiguous;
            if (check.Edit == MenuToolMoveEdit.Ambiguous) check.Warnings.Add("More than one source could make this move: choose one.");
            Affected(scan, check);
        }

        /// <summary>
        /// Where a Modular Avatar or VRCFury item can go with Destructive Move: its menu root, the menu its source's top level
        /// lands in (a Full Controller's menu path, an MA installer's target), and the menu asset that top level is, if any.
        /// A VRCFury Toggle or Puppet has the whole menu as its root. Null for other items.
        /// </summary>
        internal static MenuToolEntry MenuRoot(MenuToolEntry entry, out VRCExpressionsMenu rootAsset)
        {
            rootAsset = null;
            if (entry.Parent == null || entry.Generated) return null;
            if (entry.Origin == MenuToolOrigin.VRCFury && entry.OwnerMenu == null)
            {
                if (entry.VrcfComponent == null || string.IsNullOrEmpty(entry.VrcfPath)) return null;
                MenuToolEntry top = entry;
                while (top.Parent != null) top = top.Parent;
                return top;
            }
            Func<MenuToolEntry, bool> same;
            if (entry.Origin == MenuToolOrigin.VRCFury) same = e => e.VrcfComponent == entry.VrcfComponent && e.OwnerMenu != null;
#if RIRYTOOLS_MENUTOOL_MA
            else if (entry.Origin == MenuToolOrigin.ModularAvatar) same = e => e.MaInstaller == entry.MaInstaller && (e.MaItem != null || e.OwnerMenu != null);
#endif
            else return null;
            MenuToolEntry first = entry;
            while (first.Parent != null && first.Parent.Parent != null && same(first.Parent)) first = first.Parent;
            rootAsset = first.OwnerMenu;
            return first.Parent;
        }

        // Destructive Move's check: the destination must be inside the item's menu root; the edit follows the item's source.
        private static void Destructive(MenuToolScan scan, MenuToolMoveCheck check)
        {
            MenuToolEntry entry = check.Entry, destination = check.Destination;
            MenuToolEntry root = MenuRoot(entry, out VRCExpressionsMenu rootAsset);
            if (root == null)
            {
#if RIRYTOOLS_MENUTOOL_MA
                check.Errors.Add("Destructive Move is for items Modular Avatar or VRCFury add.");
#else
                check.Errors.Add("Destructive Move is for items VRCFury adds.");
#endif
                return;
            }
            if (destination != root && !MenuToolScanner.IsUnder(destination, root))
            {
                check.Errors.Add(check.Move.To + " is outside " + entry.Name + "'s menu root" + (root.Path.Length > 0 ? " (" + root.Path + ")" : "") + ".");
                return;
            }
            if (entry.Origin == MenuToolOrigin.VRCFury && entry.OwnerMenu == null) check.Edit = MenuToolMoveEdit.DestructivePath;
            else if (entry.OwnerMenu != null && (destination == root ? rootAsset : destination.SubMenuAsset) != null) check.Edit = MenuToolMoveEdit.DestructiveAsset;
#if RIRYTOOLS_MENUTOOL_MA
            else if (entry.MaItem != null && CanReparent(entry.MaItem.gameObject) && MaChildren(destination)) check.Edit = MenuToolMoveEdit.MaReparent;
#endif
            else
            {
                check.Errors.Add("No edit of " + entry.Name + "'s source moves it there.");
                return;
            }
            check.Candidates.Add(check.Edit);
            switch (check.Edit)
            {
                case MenuToolMoveEdit.DestructiveAsset:
                    check.Affected.Add(entry.OwnerMenu);
                    VRCExpressionsMenu into = destination == root ? rootAsset : destination.SubMenuAsset;
                    if (into != entry.OwnerMenu) check.Affected.Add(into);
                    check.Warnings.Add("Edits " + AssetDatabase.GetAssetPath(entry.OwnerMenu) + " in place: everything that uses it changes.");
                    break;
                case MenuToolMoveEdit.DestructivePath:
                    check.Affected.Add(entry.VrcfComponent);
                    break;
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.MaReparent:
                    check.Affected.Add(entry.MaItem);
                    break;
#endif
            }
        }

        private static MenuToolMoveEdit Preferred(MenuToolEntry entry, List<MenuToolMoveEdit> candidates)
        {
            if (candidates.Count == 1) return candidates[0];
            if (entry.Origin == MenuToolOrigin.VRCFury) return MenuToolMoveEdit.VrcfuryRule;
            if (entry.Origin == MenuToolOrigin.Native && candidates.Contains(MenuToolMoveEdit.NativeAssets)) return MenuToolMoveEdit.NativeAssets;
#if RIRYTOOLS_MENUTOOL_MA
            if (entry.Origin == MenuToolOrigin.ModularAvatar)
            {
                if (candidates.Contains(MenuToolMoveEdit.MaReparent)) return MenuToolMoveEdit.MaReparent;
                if (candidates.Contains(MenuToolMoveEdit.MaRetarget)) return MenuToolMoveEdit.MaRetarget;
            }
#endif
            return MenuToolMoveEdit.Ambiguous;
        }

        // Unity does not move or reorder an object inside a prefab instance (only the instance root), nor in a prefab asset.
        internal static bool CanReparent(GameObject gameObject)
        {
            if (EditorUtility.IsPersistent(gameObject)) return false;
            return !PrefabUtility.IsPartOfPrefabInstance(gameObject) || PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject);
        }

#if RIRYTOOLS_MENUTOOL_MA
        // Whether the entry is the whole installed block of its installer (the first thing it installs at that level).
        private static bool IsInstallRoot(MenuToolEntry entry)
        {
            return entry.Parent == null || entry.Parent.MaInstaller != entry.MaInstaller;
        }

#endif
        private static void Affected(MenuToolScan scan, MenuToolMoveCheck check)
        {
            switch (check.Edit)
            {
                case MenuToolMoveEdit.NativeAssets:
                    if (check.Entry.OwnerMenu != null) check.Affected.Add(check.Entry.OwnerMenu);
                    UnityEngine.Object destination = check.Destination.Parent == null ? (scan.Avatar != null ? scan.Avatar.expressionsMenu : null) : check.Destination.SubMenuAsset;
                    if (destination != null && !check.Affected.Contains(destination)) check.Affected.Add(destination);
                    break;
                case MenuToolMoveEdit.VrcfuryRule:
                    if (scan.Avatar != null) check.Affected.Add(scan.Avatar.gameObject);
                    break;
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.MaReparent:
                    check.Affected.Add(check.Entry.MaItem);
                    break;
                case MenuToolMoveEdit.MaRetarget:
                    check.Affected.Add(check.Entry.MaInstaller);
                    break;
#endif
                case MenuToolMoveEdit.NewMenuAsset:
                    UnityEngine.Object parentMenu = check.Destination.Parent == null ? scan.Avatar.expressionsMenu : check.Destination.SubMenuAsset;
                    if (parentMenu != null) check.Affected.Add(parentMenu);
                    break;
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.NewMaItem:
                    if (check.Destination.MaChildrenSource != null) check.Affected.Add(check.Destination.MaChildrenSource);
                    else if (scan.Avatar != null) check.Affected.Add(scan.Avatar.gameObject);
                    break;
#endif
            }
        }

        internal static string Describe(MenuToolMoveEdit edit)
        {
            switch (edit)
            {
                case MenuToolMoveEdit.NativeAssets: return "Move between menu assets";
                case MenuToolMoveEdit.VrcfuryRule: return MoveRule;
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.MaReparent: return "Move the MA Menu Item object";
                case MenuToolMoveEdit.MaRetarget: return "Retarget the MA Menu Installer";
#endif
                case MenuToolMoveEdit.NewMenuAsset: return "New menu asset";
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.NewMaItem: return "New MA Menu Item";
#endif
                case MenuToolMoveEdit.NewControlAsset: return "New control in the menu asset";
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.NewControlMa: return "New MA Menu Item";
#endif
                case MenuToolMoveEdit.NewControlVrcfury: return "New VRCFury Toggle";
                case MenuToolMoveEdit.IconAsset: return "Icon in the menu asset";
                case MenuToolMoveEdit.IconVrcfury: return "Set Icon (VRCFury)";
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.IconMa: return "Icon on the MA Menu Item";
#endif
                case MenuToolMoveEdit.RemoveAsset: return "Delete from the menu asset";
                case MenuToolMoveEdit.RenameAsset: return "Rename in the menu asset";
                case MenuToolMoveEdit.RenameVrcfury: return "Rename with Move Menu Item (VRCFury)";
                case MenuToolMoveEdit.RemoveVrcfury: return "Move Menu Item to nothing (VRCFury)";
                case MenuToolMoveEdit.DestructiveAsset: return "Destructive Move in the menu asset";
                case MenuToolMoveEdit.DestructivePath: return "Destructive Move of the VRCFury menu path";
                case MenuToolMoveEdit.Ambiguous: return "Choose a source";
                default: return "Cannot move";
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool planner
// The plan it checks is in MenuToolPlan.cs.
// ============================================================================
//
// MenuToolPlanner (this file)
//   Planned(scan, plan)      A clone of the assembled menu with each move applied in order (the preview), plus every check; an
//                            Add menu puts a new empty submenu (MenuToolEntry.Added) at the end of its parent.
//   Check                    Missing entry or destination, submenu into itself (cycle), capacity (more than 8: VRCFury pages,
//                            otherwise an error), a name already there, then Ownership.
//   Ownership                VRCFury entries: VRCFury rule. Native: between assets when both ends are native assets, else a
//                            VRCFury rule. MA: reparent into an MA children submenu, retarget the installer for a whole
//                            installed block going to an asset menu, else a VRCFury rule. Several fitting: Preferred, or the
//                            user's Choice; none: Invalid. A menu the plan adds counts as made (NativeAsset, MaChildren).
//   CheckAdd                 Add menu: the name (not empty, no /), the parent still a submenu, no item of that name there,
//                            capacity, then the edit by who owns the parent: NewMenuAsset for a native menu with an asset (or the
//                            descriptor's root menu), NewMaItem with Modular Avatar for an MA children submenu, the root or any
//                            submenu with an asset (never on a prefab asset). Several: native parent NewMenuAsset, MA children
//                            NewMaItem, else the user's Choice. VRCFury has no feature that makes an empty submenu.
//   CheckAddControl          Add New Control: as CheckAdd; edits NewControlAsset (native parent with an asset), NewControlMa
//                            (with MA, as NewMaItem), NewControlVrcfury (VRCFury installed, Toggle only). Warns on an undeclared
//                            parameter (not for VRCFury, which makes its own) and on puppets with more than one axis (only the
//                            first is set). ControlOf / Axes: the planned entry's fields.
//   CheckCapacity            Shared by moves and both adds: over MaxControls warns when VRCFury pages the rest, else errors.
//   CheckNewItem / CanAddNative / CanAddMa  Shared by both adds: name, parent and duplicate checks; whether the parent
//                            takes a menu-asset item or a Modular Avatar item. The VRCFury toggle stays in CheckAddControl.
//   CheckIcon                Assign Menu Icon: the entry and texture exist, not a pagination page; IconAsset (native control),
//                            IconMa (MA item), IconVrcfury (VRCFury installed, any entry); the owner's edit is preferred.
//   Decide                   The user's Choice, else the preferred edit (the only one when single); none: Invalid.
//   NativeAsset / MaChildren Whether a submenu takes native moves and adds (its own asset) or MA reparenting (a children object),
//                            counting menus the plan adds.
