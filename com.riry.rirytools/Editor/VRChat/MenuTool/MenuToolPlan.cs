using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>One planned move: the entry at From goes into the submenu To, at Index (-1 is the end).</summary>
    [Serializable]
    internal sealed class PlannedMove
    {
        public string From = "";
        public string To = "";
        public int Index = -1;
        // The edit the user chose when ownership is ambiguous ("" until chosen).
        public string Choice = "";
    }

    /// <summary>
    /// The planned menu layout of one avatar: moves recorded by Move to... and drag and drop, kept in a JSON file
    /// (UserSettings/RiryTools/MenuTool/&lt;avatar key&gt;.json) and applied to nothing until Apply. The sources are only
    /// read while planning.
    /// </summary>
    [Serializable]
    internal sealed class MenuPlan
    {
        public string Avatar = "";
        public List<PlannedMove> Moves = new List<PlannedMove>();

        /// <summary>The plan file of an avatar (or standalone target): keyed by its GlobalObjectId, so per scene or prefab.</summary>
        internal static string FileFor(GameObject avatar)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
            using (MD5 md5 = MD5.Create())
            {
                string key = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").ToLowerInvariant();
                return Path.Combine(RiryToolsPaths.ProjectRoot, "UserSettings", "RiryTools", "MenuTool", key + ".json");
            }
        }

        internal static MenuPlan Load(GameObject avatar)
        {
            var plan = new MenuPlan { Avatar = avatar != null ? avatar.name : "" };
            if (avatar == null) return plan;
            string path = FileFor(avatar);
            if (!File.Exists(path)) return plan;
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), plan);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Reading the menu plan " + path, exception, avatar);
            }
            return plan;
        }

        internal void Save(GameObject avatar)
        {
            if (avatar == null) return;
            string path = FileFor(avatar);
            try
            {
                if (Moves.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(this, true));
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Saving the menu plan " + path, exception, avatar);
            }
        }
    }

    /// <summary>How a planned move would be applied, from who owns the entry and the destination.</summary>
    internal enum MoveEdit
    {
        // Move the control between menu assets (copying shared ones first).
        NativeAssets,
        // Add or update an avatar-level VRCFury Move Menu Item (or Reorder Menu Item) rule.
        VrcfuryRule,
        // Reparent the Modular Avatar menu item's object.
        MaReparent,
        // Retarget the Modular Avatar installer of the whole installed block.
        MaRetarget,
        // Ownership unclear or unsupported: the user picks one of the candidates.
        Ambiguous,
        Invalid
    }

    /// <summary>A planned move checked against the current menu.</summary>
    internal sealed class MoveCheck
    {
        internal PlannedMove Move;
        internal MenuEntry Entry;
        internal MenuEntry Destination;
        internal string Before = "";
        internal string After = "";
        internal MoveEdit Edit = MoveEdit.Invalid;
        internal readonly List<MoveEdit> Candidates = new List<MoveEdit>();
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<UnityEngine.Object> Affected = new List<UnityEngine.Object>();

        internal bool Ok
        {
            get { return Errors.Count == 0 && Edit != MoveEdit.Ambiguous && Edit != MoveEdit.Invalid; }
        }
    }

    /// <summary>Applies a plan to a copy of the assembled menu (the preview), and checks each move.</summary>
    internal static class MenuToolPlanner
    {
        internal const string MoveRule = "Move Menu Item (VRCFury)";

        /// <summary>The planned layout: a copy of the scan's menu with every move applied in order; checks says how each went.</summary>
        internal static MenuEntry Planned(MenuScan scan, MenuPlan plan, out List<MoveCheck> checks)
        {
            checks = new List<MoveCheck>();
            MenuEntry root = scan.Root.Clone();
            foreach (PlannedMove move in plan.Moves)
            {
                MoveCheck check = Check(scan, root, move);
                checks.Add(check);
                if (check.Entry == null || check.Destination == null || check.Errors.Count > 0) continue;
                int index = move.Index;
                if (check.Entry.Parent == check.Destination && index > check.Destination.Children.IndexOf(check.Entry)) index--;
                check.Destination.Add(check.Entry, index);
                check.After = check.Entry.Path;
            }
            return root;
        }

        private static MoveCheck Check(MenuScan scan, MenuEntry root, PlannedMove move)
        {
            var check = new MoveCheck { Move = move, Before = move.From };
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
            if (count > MenuToolScanner.MaxControls)
            {
                if (scan.VrcfuryInstalled) check.Warnings.Add((string.IsNullOrEmpty(move.To) ? "The root menu" : move.To) + " will hold " + count +
                    " controls: VRCFury pages the rest into a Next submenu.");
                else check.Errors.Add((string.IsNullOrEmpty(move.To) ? "The root menu" : move.To) + " would hold " + count + " controls; VRChat allows " +
                    MenuToolScanner.MaxControls + ".");
            }
            if (!sameMenu && check.Destination.Children.Any(child => child.Name == check.Entry.Name))
                check.Warnings.Add(check.Destination.Path + " already has an item named " + check.Entry.Name +
                    (check.Entry.IsSubMenu && scan.VrcfuryInstalled ? "; VRCFury merges submenus of the same name." : "."));
            check.After = (check.Destination.Path.Length == 0 ? "" : check.Destination.Path + "/") + check.Entry.Name;
            Ownership(scan, check, sameMenu);
            return check;
        }

        // Chooses the edit from the origin of the entry and the destination (the spec's table).
        private static void Ownership(MenuScan scan, MoveCheck check, bool sameMenu)
        {
            MenuEntry entry = check.Entry, destination = check.Destination;
            bool vrcfuryRule = scan.VrcfuryInstalled && scan.Avatar != null;
            if (entry.Generated)
            {
                check.Errors.Add(entry.Name + " is made by VRCFury's pagination and cannot be moved.");
                return;
            }
            switch (entry.Origin)
            {
                case MenuOrigin.VRCFury:
                    check.Candidates.Add(MoveEdit.VrcfuryRule);
                    if (!vrcfuryRule) check.Errors.Add("VRCFury rules need VRCFury and an avatar descriptor.");
                    break;
                case MenuOrigin.Native:
                    if (destination.Origin == MenuOrigin.Native && (destination.Parent == null ? scan.Avatar != null && scan.Avatar.expressionsMenu != null : destination.SubMenuAsset != null)
                        && entry.OwnerMenu != null)
                        check.Candidates.Add(MoveEdit.NativeAssets);
                    if (vrcfuryRule) check.Candidates.Add(MoveEdit.VrcfuryRule);
                    break;
                case MenuOrigin.ModularAvatar:
                    if (entry.MaItem != null && destination.MaChildrenSource != null && CanReparent(entry.MaItem.gameObject))
                        check.Candidates.Add(MoveEdit.MaReparent);
                    if (entry.MaInstaller != null && (entry.MaItem == null || entry.MaItem.gameObject == entry.MaInstaller.gameObject) && entry.Parent != null &&
                        IsInstallRoot(entry) && destination.MaChildrenSource == null && (destination.Parent == null || destination.SubMenuAsset != null) && !sameMenu)
                        check.Candidates.Add(MoveEdit.MaRetarget);
                    if (vrcfuryRule) check.Candidates.Add(MoveEdit.VrcfuryRule);
                    break;
            }
            if (sameMenu && entry.Origin == MenuOrigin.ModularAvatar && entry.MaItem != null && entry.Parent != null && entry.Parent.MaChildrenSource != null &&
                CanReparent(entry.MaItem.gameObject) && !check.Candidates.Contains(MoveEdit.MaReparent))
                check.Candidates.Insert(0, MoveEdit.MaReparent);
            if (check.Candidates.Count == 0)
            {
                check.Edit = MoveEdit.Invalid;
                check.Errors.Add("No supported edit moves " + entry.Path + " there" + (scan.VrcfuryInstalled ? "." : " (installing VRCFury would allow a Move Menu Item rule)."));
                return;
            }
            // The table's edit when it is clear; several candidates need the user's choice unless one was chosen.
            MoveEdit preferred = Preferred(entry, check.Candidates);
            MoveEdit chosen;
            if (!string.IsNullOrEmpty(check.Move.Choice) && Enum.TryParse(check.Move.Choice, out chosen) && check.Candidates.Contains(chosen)) check.Edit = chosen;
            else if (preferred != MoveEdit.Ambiguous) check.Edit = preferred;
            else check.Edit = MoveEdit.Ambiguous;
            if (check.Edit == MoveEdit.Ambiguous) check.Warnings.Add("More than one source could make this move: choose one.");
            Affected(scan, check);
        }

        private static MoveEdit Preferred(MenuEntry entry, List<MoveEdit> candidates)
        {
            if (candidates.Count == 1) return candidates[0];
            if (entry.Origin == MenuOrigin.VRCFury) return MoveEdit.VrcfuryRule;
            if (entry.Origin == MenuOrigin.Native && candidates.Contains(MoveEdit.NativeAssets)) return MoveEdit.NativeAssets;
            if (entry.Origin == MenuOrigin.ModularAvatar)
            {
                if (candidates.Contains(MoveEdit.MaReparent)) return MoveEdit.MaReparent;
                if (candidates.Contains(MoveEdit.MaRetarget)) return MoveEdit.MaRetarget;
            }
            return MoveEdit.Ambiguous;
        }

        // Unity does not move or reorder an object inside a prefab instance (only the instance root), nor in a prefab asset.
        internal static bool CanReparent(GameObject gameObject)
        {
            if (EditorUtility.IsPersistent(gameObject)) return false;
            return !PrefabUtility.IsPartOfPrefabInstance(gameObject) || PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject);
        }

        // Whether the entry is the whole installed block of its installer (the first thing it installs at that level).
        private static bool IsInstallRoot(MenuEntry entry)
        {
            return entry.Parent == null || entry.Parent.MaInstaller != entry.MaInstaller;
        }

        private static void Affected(MenuScan scan, MoveCheck check)
        {
            switch (check.Edit)
            {
                case MoveEdit.NativeAssets:
                    if (check.Entry.OwnerMenu != null) check.Affected.Add(check.Entry.OwnerMenu);
                    UnityEngine.Object destination = check.Destination.Parent == null ? (scan.Avatar != null ? scan.Avatar.expressionsMenu : null) : check.Destination.SubMenuAsset;
                    if (destination != null && !check.Affected.Contains(destination)) check.Affected.Add(destination);
                    break;
                case MoveEdit.VrcfuryRule:
                    if (scan.Avatar != null) check.Affected.Add(scan.Avatar.gameObject);
                    break;
                case MoveEdit.MaReparent:
                    check.Affected.Add(check.Entry.MaItem);
                    break;
                case MoveEdit.MaRetarget:
                    check.Affected.Add(check.Entry.MaInstaller);
                    break;
            }
        }

        internal static string Describe(MoveEdit edit)
        {
            switch (edit)
            {
                case MoveEdit.NativeAssets: return "Move between menu assets";
                case MoveEdit.VrcfuryRule: return MoveRule;
                case MoveEdit.MaReparent: return "Move the MA Menu Item object";
                case MoveEdit.MaRetarget: return "Retarget the MA Menu Installer";
                case MoveEdit.Ambiguous: return "Choose a source";
                default: return "Cannot move";
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool plan
// ============================================================================
//
// PlannedMove                From (path), To (submenu path, "" root), Index (-1 end), Choice (the chosen MoveEdit when several fit).
// MenuPlan (this file)       The pathing JSON: UserSettings/RiryTools/MenuTool/<MD5 of the avatar's GlobalObjectId>.json (FileFor);
//                            Load / Save (an empty plan deletes the file). Only Apply changes the sources.
// MoveEdit                   NativeAssets (move the control between menu assets), VrcfuryRule (avatar-level Move / Reorder Menu
//                            Item), MaReparent (MA Menu Item object), MaRetarget (MA installer's target menu), Ambiguous, Invalid.
// MoveCheck                  A move checked against the menu: Entry, Destination, Before/After paths, Edit, Candidates, Errors,
//                            Warnings (paging, same name), Affected sources; Ok when it can be applied.
// MenuToolPlanner
//   Planned(scan, plan)      A clone of the assembled menu with each move applied in order (the preview), plus every check.
//   Check                    Missing entry or destination, submenu into itself (cycle), capacity (more than 8: VRCFury pages,
//                            otherwise an error), a name already there, then Ownership.
//   Ownership                VRCFury entries: VRCFury rule. Native: between assets when both ends are native assets, else a
//                            VRCFury rule. MA: reparent into an MA children submenu, retarget the installer for a whole
//                            installed block going to an asset menu, else a VRCFury rule. Several fitting: Preferred, or the
//                            user's Choice; none: Invalid.
