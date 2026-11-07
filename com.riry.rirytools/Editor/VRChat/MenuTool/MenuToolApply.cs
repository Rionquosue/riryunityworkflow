using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Applies a menu plan to its sources as one undo step: native controls move between menu assets (a menu asset other
    /// places also use is copied first and this avatar relinked to the copy), VRCFury entries get an avatar-level Move Menu
    /// Item rule (an existing rule that put the entry where it is now is updated instead), Modular Avatar items are
    /// reparented or their installer retargeted. Each move is checked again on a fresh scan just before it is made, and the
    /// result is verified on a scan afterwards. Ctrl+Z undoes every change; copied menu assets stay as files.
    /// </summary>
    internal static class MenuToolApply
    {
        private const string VrcfuryType = "VF.Model.VRCFury";
        private const string MoveType = "VF.Model.Feature.MoveMenuItem";
        private const string ReorderType = "VF.Model.Feature.ReorderMenuItem";
        private const string UndoName = "Apply menu layout";

        internal sealed class Report
        {
            internal int Applied;
            internal readonly List<string> Problems = new List<string>();
            internal readonly List<string> CopiedAssets = new List<string>();
        }

        private sealed class Expected
        {
            internal string Path;
            internal VRCExpressionsMenu.Control.ControlType Type;
            internal Texture2D Icon;
            internal string[] Parameters;
        }

        internal static Report Apply(GameObject target, MenuPlan plan)
        {
            var report = new Report();
            var expected = new List<Expected>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                foreach (PlannedMove move in plan.Moves)
                {
                    MenuScan scan = MenuToolScanner.Scan(target);
                    var single = new MenuPlan { Moves = new List<PlannedMove> { move } };
                    MenuToolPlanner.Planned(scan, single, out List<MoveCheck> checks);
                    MoveCheck check = checks[0];
                    if (!check.Ok)
                    {
                        report.Problems.Add(move.From + ": " + string.Join(" ", check.Errors.Concat(check.Edit == MoveEdit.Ambiguous ? new[] { "No source chosen." } : new string[0])));
                        continue;
                    }
                    // The scan's own entries (the planner works on a copy) for the ownership references.
                    MenuEntry entry = scan.Root.Find(move.From);
                    MenuEntry destination = scan.Root.Find(move.To ?? "");
                    expected.Add(new Expected { Path = check.After, Type = entry.Type, Icon = entry.Icon, Parameters = entry.Parameters().ToArray() });
                    switch (check.Edit)
                    {
                        case MoveEdit.NativeAssets: MoveNative(scan, entry, destination, move.Index, report); break;
                        case MoveEdit.VrcfuryRule: AddRule(scan, check.Before, check.After, move.Index, entry.Parent == destination); break;
                        case MoveEdit.MaReparent: Reparent(entry, destination, move.Index); break;
                        case MoveEdit.MaRetarget: Retarget(scan, entry, destination); break;
                    }
                    report.Applied++;
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            MenuScan after = MenuToolScanner.Scan(target);
            foreach (Expected item in expected)
            {
                MenuEntry found = after.Root.Find(item.Path);
                if (found == null) report.Problems.Add(item.Path + " is not where it was moved to.");
                else if (found.Type != item.Type || found.Icon != item.Icon || !found.Parameters().SequenceEqual(item.Parameters))
                    report.Problems.Add(item.Path + " changed type, icon or parameters while moving.");
            }
            RiryLog.Info(RiryLog.MenuTool, "Applied " + report.Applied + " of " + plan.Moves.Count + " menu moves to " + target.name + " (one undo step)" +
                (report.CopiedAssets.Count > 0 ? "; copied shared menus to " + string.Join(", ", report.CopiedAssets) : "") + ".", target);
            foreach (string problem in report.Problems) RiryLog.Warn(RiryLog.MenuTool, problem, target);
            return report;
        }

        // ---- Native ----------------------------------------------------------------

        private static void MoveNative(MenuScan scan, MenuEntry entry, MenuEntry destination, int index, Report report)
        {
            VRCExpressionsMenu from = Owned(scan, entry.OwnerMenu, report);
            VRCExpressionsMenu to = Owned(scan, destination.Parent == null ? scan.Avatar.expressionsMenu : destination.SubMenuAsset, report);
            // Owning may have copied the source menu: find the control in the menu now used.
            VRCExpressionsMenu.Control control = from.controls[entry.OwnerIndex];
            Undo.RecordObject(from, UndoName);
            if (to != from) Undo.RecordObject(to, UndoName);
            from.controls.RemoveAt(entry.OwnerIndex);
            if (index > to.controls.Count) index = -1;
            if (to == from && index > entry.OwnerIndex) index--;
            if (index < 0) to.controls.Add(control);
            else to.controls.Insert(index, control);
            EditorUtility.SetDirty(from);
            EditorUtility.SetDirty(to);
        }

        /// <summary>
        /// The menu this avatar can change: the menu itself, or (when other places use it too, or it is inside a package) a
        /// copy, with every reference to it in this avatar's menu tree relinked to the copy (copying shared parents too).
        /// </summary>
        private static VRCExpressionsMenu Owned(MenuScan scan, VRCExpressionsMenu menu, Report report)
        {
            if (!Shared(scan, menu)) return menu;
            string folder = "Assets/RiryTools/MenuTool/" + Safe(scan.Avatar.name);
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Safe(menu.name) + ".asset");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(menu), path)) throw new IOException("Could not copy " + menu.name + " to " + path);
            var copy = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(path);
            report.CopiedAssets.Add(path);
            if (scan.Avatar.expressionsMenu == menu)
            {
                Undo.RecordObject(scan.Avatar, UndoName);
                scan.Avatar.expressionsMenu = copy;
                EditorUtility.SetDirty(scan.Avatar);
            }
            foreach (MenuEntry parent in scan.Root.Descendants().Where(e => e.Origin == MenuOrigin.Native && e.IsSubMenu && e.SubMenuAsset == menu && e.OwnerMenu != null).ToList())
            {
                VRCExpressionsMenu holder = Owned(scan, parent.OwnerMenu, report);
                Undo.RecordObject(holder, UndoName);
                holder.controls[parent.OwnerIndex].subMenu = copy;
                EditorUtility.SetDirty(holder);
                parent.SubMenuAsset = copy;
            }
            foreach (MenuEntry child in scan.Root.Descendants().Where(e => e.OwnerMenu == menu)) child.OwnerMenu = copy;
            if (scan.Root.SubMenuAsset == menu) scan.Root.SubMenuAsset = copy;
            return copy;
        }

        // Used by more than one menu slot of this avatar, by another avatar in an open scene, or inside a package (read-only).
        private static bool Shared(MenuScan scan, VRCExpressionsMenu menu)
        {
            string path = AssetDatabase.GetAssetPath(menu);
            if (path.StartsWith("Packages/", StringComparison.Ordinal)) return true;
            int uses = scan.Root.Descendants().Count(e => e.IsSubMenu && e.SubMenuAsset == menu) + (scan.Avatar != null && scan.Avatar.expressionsMenu == menu ? 1 : 0);
            if (uses > 1) return true;
            foreach (VRCAvatarDescriptor other in Object.FindObjectsOfType<VRCAvatarDescriptor>(true))
            {
                if (other == scan.Avatar || other.expressionsMenu == null) continue;
                if (Reaches(other.expressionsMenu, menu, new HashSet<VRCExpressionsMenu>())) return true;
            }
            return false;
        }

        private static bool Reaches(VRCExpressionsMenu from, VRCExpressionsMenu menu, HashSet<VRCExpressionsMenu> seen)
        {
            if (from == null || !seen.Add(from)) return false;
            if (from == menu) return true;
            return from.controls != null && from.controls.Any(c => c != null && c.subMenu != null && Reaches(c.subMenu, menu, seen));
        }

        private static string Safe(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---- VRCFury -----------------------------------------------------------------

        private static void AddRule(MenuScan scan, string from, string to, int index, bool sameMenu)
        {
            GameObject avatar = scan.Avatar.gameObject;
            Type vrcfury = MenuToolScanner.TypeNamed(VrcfuryType);
            if (!sameMenu)
            {
                // A rule that already moved this entry here is updated, so rules don't stack up.
                SerializedObject existing = null;
                foreach (Component component in avatar.GetComponents(vrcfury))
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty content = serialized.FindProperty("content");
                    if (content == null || MenuToolScanner.FeatureName(content) != "MoveMenuItem") continue;
                    if (content.FindPropertyRelative("toPath").stringValue == from) existing = serialized;
                }
                if (existing != null)
                {
                    existing.FindProperty("content.toPath").stringValue = to;
                    existing.ApplyModifiedProperties();
                }
                else NewRule(avatar, vrcfury, MoveType, rule =>
                {
                    rule.FindPropertyRelative("fromPath").stringValue = from;
                    rule.FindPropertyRelative("toPath").stringValue = to;
                });
            }
            if (index < 0) return;
            NewRule(avatar, vrcfury, ReorderType, rule =>
            {
                rule.FindPropertyRelative("path").stringValue = to;
                rule.FindPropertyRelative("position").intValue = index;
            });
        }

        private static void NewRule(GameObject avatar, Type vrcfury, string featureType, Action<SerializedProperty> set)
        {
            Type feature = MenuToolScanner.TypeNamed(featureType);
            if (feature == null) throw new InvalidOperationException("VRCFury's " + featureType + " was not found; VRCFury may have changed.");
            Component component = Undo.AddComponent(avatar, vrcfury);
            var serialized = new SerializedObject(component);
            SerializedProperty content = serialized.FindProperty("content");
            content.managedReferenceValue = Activator.CreateInstance(feature, true);
            set(content);
            serialized.ApplyModifiedProperties();
        }

        // ---- Modular Avatar ------------------------------------------------------------

        private static void Reparent(MenuEntry entry, MenuEntry destination, int index)
        {
            Transform item = entry.MaItem.transform;
            Transform parent = destination.MaChildrenSource.transform;
            // The sibling to land before: the item now at that index in the destination submenu.
            Transform before = index >= 0 && index < destination.Children.Count && destination.Children[index].MaItem != null
                ? destination.Children[index].MaItem.transform : null;
            if (item.parent != parent) Undo.SetTransformParent(item, parent, UndoName);
            else Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, UndoName);
            if (before != null && before != item && before.parent == parent) item.SetSiblingIndex(before.GetSiblingIndex());
            else if (index < 0) item.SetAsLastSibling();
        }

        private static void Retarget(MenuScan scan, MenuEntry entry, MenuEntry destination)
        {
            var serialized = new SerializedObject(entry.MaInstaller);
            SerializedProperty target = serialized.FindProperty("installTargetMenu");
            // The root menu: no target (Modular Avatar installs into the avatar's root menu).
            target.objectReferenceValue = destination.Parent == null ? null : destination.SubMenuAsset;
            serialized.ApplyModifiedProperties();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool apply
// ============================================================================
//
// MenuToolApply (this file)
//   Apply(target, plan)      One undo group ("Apply menu layout", CollapseUndoOperations). For each planned move: a fresh scan,
//                            the move checked alone (MenuToolPlanner), then its edit; afterwards a scan verifies each moved entry's
//                            path, type, icon and parameters (Report.Problems). Logged under Menu Tool.
//   MoveNative               Removes the control from its menu asset and inserts it in the destination's (RecordObject both).
//   Owned / Shared           A menu other slots of this avatar, another open avatar or a package also use is copied to
//                            Assets/RiryTools/MenuTool/<avatar>/ (Report.CopiedAssets) and this avatar relinked (descriptor,
//                            parent controls, copying shared parents too). Copies are files: Ctrl+Z leaves them.
//   AddRule / NewRule        VRCFury on the avatar object: updates a Move Menu Item whose toPath is the entry's path, else adds
//                            one (content set through SerializedObject); a set index adds a Reorder Menu Item.
//   Reparent                 Undo.SetTransformParent into the destination's MA children object, then the sibling index.
//   Retarget                 The MA installer's installTargetMenu (none for the root menu).
