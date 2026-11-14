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
    /// reparented or their installer retargeted. A planned new menu (Add menu) becomes a new menu asset linked from its
    /// parent's menu asset, or a new object with a Modular Avatar Menu Item. Each move is checked again on a fresh scan just
    /// before it is made, and the result is verified on a scan afterwards. Ctrl+Z undoes every change; copied and new menu
    /// assets stay as files.
    /// </summary>
    internal static class MenuToolApply
    {
        private const string VrcfuryType = "VF.Model.VRCFury";
        private const string MoveType = "VF.Model.Feature.MoveMenuItem";
        private const string ReorderType = "VF.Model.Feature.ReorderMenuItem";
        private const string ToggleType = "VF.Model.Feature.Toggle";
        private const string SetIconType = "VF.Model.Feature.SetIcon";
#if RIRYTOOLS_MENUTOOL_MA
        private const string MaInstallerType = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MaItemType = "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        // Modular Avatar's SubmenuSource.Children.
        private const int MaChildrenSource = 1;
#endif
        private const string UndoName = "Apply menu layout";

        internal sealed class Report
        {
            internal int Applied;
            internal readonly List<string> Problems = new List<string>();
            internal readonly List<string> CreatedAssets = new List<string>();
        }

        private sealed class Expected
        {
            internal string Path;
            internal VRCExpressionsMenu.Control.ControlType Type;
            internal Texture2D Icon;
            internal string[] Parameters;
        }

        /// <summary>Applies every move of the plan; insideUndoStep runs last, inside the same undo step (the window clears its plan there).</summary>
        internal static Report Apply(GameObject target, MenuToolPlan plan, Action insideUndoStep)
        {
            var report = new Report();
            var expected = new List<Expected>();
            int total = plan.Moves.Count;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                foreach (MenuToolPlannedMove move in plan.Moves.ToList())
                {
                    MenuToolScan scan = MenuToolScanner.Scan(target);
                    var single = new MenuToolPlan { Moves = new List<MenuToolPlannedMove> { move } };
                    MenuToolPlanner.Planned(scan, single, out List<MenuToolMoveCheck> checks);
                    MenuToolMoveCheck check = checks[0];
                    if (!check.Ok)
                    {
                        report.Problems.Add((move.IsAdd ? "New menu " + check.After : move.IsAddControl ? "New control " + check.After : move.From) + ": " +
                            string.Join(" ", check.Errors.Concat(check.Edit == MenuToolMoveEdit.Ambiguous ? new[] { "No source chosen." } : new string[0])));
                        continue;
                    }
                    // The scan's own entries (the planner works on a copy) for the ownership references.
                    MenuToolEntry destination = scan.Root.Find(move.To ?? "");
                    if (move.IsAdd)
                    {
                        expected.Add(new Expected { Path = check.After, Type = VRCExpressionsMenu.Control.ControlType.SubMenu, Parameters = new string[0] });
                        if (check.Edit == MenuToolMoveEdit.NewMenuAsset) NewMenuAsset(scan, destination, move.NewMenu.Trim(), report);
#if RIRYTOOLS_MENUTOOL_MA
                        else NewMaItem(scan, destination, move.NewMenu.Trim());
#endif
                        report.Applied++;
                        continue;
                    }
                    if (move.IsAddControl)
                    {
                        var control = new MenuToolEntry { Name = move.NewControl.Trim(), Type = (VRCExpressionsMenu.Control.ControlType)move.ControlType };
                        MenuToolPlanner.ControlOf(move, control);
                        expected.Add(new Expected { Path = check.After, Type = control.Type, Icon = control.Icon,
                            Parameters = check.Edit == MenuToolMoveEdit.NewControlVrcfury ? null : control.Parameters().ToArray() });
                        if (check.Edit == MenuToolMoveEdit.NewControlAsset) NewControlAsset(scan, destination, control, report);
#if RIRYTOOLS_MENUTOOL_MA
                        else if (check.Edit == MenuToolMoveEdit.NewControlMa) NewControlMa(scan, destination, control);
#endif
                        else NewRule(scan.Avatar.gameObject, RiryToolsTypes.Named(VrcfuryType), ToggleType, rule =>
                        {
                            string into = MenuToolVrcfuryMenu.PathOf(destination);
                            rule.FindPropertyRelative("name").stringValue = (into.Length == 0 ? "" : into + "/") + MenuToolVrcfuryMenu.Escape(control.Name);
                            if (control.Icon != null)
                            {
                                rule.FindPropertyRelative("enableIcon").boolValue = true;
                                rule.FindPropertyRelative("icon.objRef").objectReferenceValue = control.Icon;
                            }
                            if (control.Parameter.Length > 0)
                            {
                                rule.FindPropertyRelative("useGlobalParam").boolValue = true;
                                rule.FindPropertyRelative("globalParam").stringValue = control.Parameter;
                            }
                        });
                        report.Applied++;
                        continue;
                    }
                    MenuToolEntry entry = scan.Root.Find(move.From);
                    if (move.Remove || move.ToNull)
                    {
                        if (check.Edit == MenuToolMoveEdit.RemoveAsset) RemoveNative(scan, entry, report);
                        else AddRule(scan, MenuToolVrcfuryMenu.PathOf(entry), "", -1, false);
                        report.Applied++;
                        continue;
                    }
                    if (move.IsRename)
                    {
                        expected.Add(new Expected { Path = check.After, Type = entry.Type, Icon = entry.Icon, Parameters = entry.Parameters().ToArray() });
                        string old = MenuToolVrcfuryMenu.PathOf(entry), parent = MenuToolVrcfuryMenu.PathOf(entry.Parent);
                        string renamed = (parent.Length == 0 ? "" : parent + "/") + MenuToolVrcfuryMenu.Escape(move.Rename.Trim());
                        if (check.Edit == MenuToolMoveEdit.RenameAsset)
                        {
                            VRCExpressionsMenu holder = Owned(scan, entry.Parent);
                            Undo.RecordObject(holder, UndoName);
                            holder.controls[entry.OwnerIndex].name = move.Rename.Trim();
                            EditorUtility.SetDirty(holder);
                            RepathVrcfury(scan, entry, old, renamed, report);
                        }
                        else AddRule(scan, old, renamed, -1, false);
                        report.Applied++;
                        continue;
                    }
                    if (move.AssignIcon)
                    {
                        Texture2D icon = move.IconTexture;
                        expected.Add(new Expected { Path = entry.Path, Type = entry.Type, Icon = icon, Parameters = entry.Parameters().ToArray() });
                        SetIcon(scan, entry, icon, check.Edit, report);
                        report.Applied++;
                        continue;
                    }
                    expected.Add(new Expected { Path = check.After, Type = entry.Type, Icon = entry.Icon, Parameters = entry.Parameters().ToArray() });
                    switch (check.Edit)
                    {
                        case MenuToolMoveEdit.NativeAssets:
                            MoveNative(scan, entry, destination, move.Index, report);
                            RepathVrcfury(scan, entry, destination, report);
                            break;
                        case MenuToolMoveEdit.VrcfuryRule:
                            // Paths as VRCFury reads them: a slash inside a name written "\/".
                            string from = MenuToolVrcfuryMenu.PathOf(entry), into = MenuToolVrcfuryMenu.PathOf(destination);
                            AddRule(scan, from, (into.Length == 0 ? "" : into + "/") + MenuToolVrcfuryMenu.Escape(entry.Name), move.Index, entry.Parent == destination);
                            break;
#if RIRYTOOLS_MENUTOOL_MA
                        case MenuToolMoveEdit.MaReparent: Reparent(entry, destination, move.Index); break;
#endif
                        case MenuToolMoveEdit.DestructiveAsset:
                            MoveInPlace(entry, destination, move.Index);
                            RepathVrcfury(scan, entry, destination, report);
                            break;
                        case MenuToolMoveEdit.DestructivePath: Repath(entry, destination); break;
#if RIRYTOOLS_MENUTOOL_MA
                        case MenuToolMoveEdit.MaRetarget: Retarget(scan, entry, destination); break;
#endif
                    }
                    report.Applied++;
                }
                if (insideUndoStep != null) insideUndoStep();
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            MenuToolScan after = MenuToolScanner.Scan(target);
            foreach (Expected item in expected)
            {
                MenuToolEntry found = after.Root.Find(item.Path);
                if (found == null) report.Problems.Add(item.Path + " is not where it was moved to.");
                else if (found.Type != item.Type || found.Icon != item.Icon || item.Parameters != null && !found.Parameters().SequenceEqual(item.Parameters))
                    report.Problems.Add(item.Path + " changed type, icon or parameters while moving.");
            }
            RiryLog.Info(RiryLog.MenuTool, "Applied " + report.Applied + " of " + total + " menu moves to " + target.name + " (one undo step)" +
                (report.CreatedAssets.Count > 0 ? "; new menus at " + string.Join(", ", report.CreatedAssets) : "") + ".", target);
            foreach (string problem in report.Problems) RiryLog.Warn(RiryLog.MenuTool, problem, target);
            return report;
        }

        // ---- Native ----------------------------------------------------------------

        private static void MoveNative(MenuToolScan scan, MenuToolEntry entry, MenuToolEntry destination, int at, Report report)
        {
            // Each menu through the slot on the moved entry's path (its submenu, or the root), so a menu shown twice only
            // changes in that one place.
            VRCExpressionsMenu from = Owned(scan, entry.Parent);
            VRCExpressionsMenu to = Owned(scan, destination);
            // Owning may have copied the source menu: find the control in the menu now used.
            VRCExpressionsMenu.Control control = from.controls[entry.OwnerIndex];
            Undo.RecordObject(from, UndoName);
            if (to != from) Undo.RecordObject(to, UndoName);
            from.controls.RemoveAt(entry.OwnerIndex);
            if (at > to.controls.Count) at = -1;
            if (to == from && at > entry.OwnerIndex) at--;
            if (at < 0) to.controls.Add(control);
            else to.controls.Insert(at, control);
            EditorUtility.SetDirty(from);
            EditorUtility.SetDirty(to);
        }

        // Destructive Move in a menu asset: the control moves between its own source's menu assets as they are, no copy.
        private static void MoveInPlace(MenuToolEntry entry, MenuToolEntry destination, int at)
        {
            VRCExpressionsMenu from = entry.OwnerMenu;
            MenuToolEntry root = MenuToolPlanner.MenuRoot(entry, out VRCExpressionsMenu rootAsset);
            VRCExpressionsMenu to = destination == root ? rootAsset : destination.SubMenuAsset;
            VRCExpressionsMenu.Control control = from.controls[entry.OwnerIndex];
            Undo.RecordObject(from, UndoName);
            if (to != from) Undo.RecordObject(to, UndoName);
            from.controls.RemoveAt(entry.OwnerIndex);
            if (at > to.controls.Count) at = -1;
            if (to == from && at > entry.OwnerIndex) at--;
            if (at < 0) to.controls.Add(control);
            else to.controls.Insert(at, control);
            EditorUtility.SetDirty(from);
            EditorUtility.SetDirty(to);
        }

        /// <summary>
        /// A submenu moved in its menu assets: every VRCFury menu path on the avatar inside its old path (Toggle and Puppet
        /// names, Full Controller menu prefixes, Move Menu Item, Reorder Menu Item and Set Icon paths, the SPS menu path) is
        /// rewritten to the new one, so what VRCFury adds there follows it. VRCFury items under it that no path of theirs
        /// places there (a Full Controller menu asset merged higher up, Security Lock) are reported.
        /// </summary>
        private static void RepathVrcfury(MenuToolScan scan, MenuToolEntry entry, MenuToolEntry destination, Report report)
        {
            string into = MenuToolVrcfuryMenu.PathOf(destination);
            RepathVrcfury(scan, entry, MenuToolVrcfuryMenu.PathOf(entry), (into.Length == 0 ? "" : into + "/") + MenuToolVrcfuryMenu.Escape(entry.Name), report);
        }

        // The same for a submenu whose path goes from one VRCFury path to another (a move or a rename).
        private static void RepathVrcfury(MenuToolScan scan, MenuToolEntry entry, string from, string to, Report report)
        {
            Type vrcfury = RiryToolsTypes.Named(VrcfuryType);
            if (!entry.IsSubMenu || scan.Avatar == null || vrcfury == null) return;
            if (MenuToolVrcfuryMenu.SamePath(from, to)) return;
            var changed = new HashSet<Component>();
            foreach (Component component in scan.Avatar.GetComponentsInChildren(vrcfury, true))
            {
                var serialized = new SerializedObject(component);
                bool any = false;
                foreach (SerializedProperty feature in MenuToolScanner.Features(serialized))
                {
                    switch (MenuToolScanner.FeatureName(feature))
                    {
                        case "Toggle":
                        case "Puppet":
                            any |= Rebase(feature.FindPropertyRelative("name"), from, to);
                            break;
                        case "FullController":
                            SerializedProperty menus = feature.FindPropertyRelative("menus");
                            for (int i = 0; menus != null && i < menus.arraySize; i++)
                                any |= Rebase(menus.GetArrayElementAtIndex(i).FindPropertyRelative("prefix"), from, to);
                            break;
                        case "MoveMenuItem":
                            any |= Rebase(feature.FindPropertyRelative("fromPath"), from, to);
                            any |= Rebase(feature.FindPropertyRelative("toPath"), from, to);
                            break;
                        case "ReorderMenuItem":
                        case "SetIcon":
                            any |= Rebase(feature.FindPropertyRelative("path"), from, to);
                            break;
                        case "SpsOptions":
                            any |= Rebase(feature.FindPropertyRelative("menuPath"), from, to);
                            break;
                    }
                }
                if (!any) continue;
                serialized.ApplyModifiedProperties();
                changed.Add(component);
            }
            foreach (Component left in entry.Descendants().Where(e => e.Origin == MenuToolOrigin.VRCFury && e.VrcfComponent != null && !changed.Contains(e.VrcfComponent))
                .Select(e => e.VrcfComponent).Distinct())
                report.Problems.Add(left.name + ": its VRCFury items in " + entry.Path + " are not placed by a path under it and were not repathed.");
        }

        private static bool Rebase(SerializedProperty property, string from, string to)
        {
            if (property == null || property.propertyType != SerializedPropertyType.String) return false;
            string rebased = MenuToolVrcfuryMenu.Rebase(property.stringValue, from, to);
            if (rebased == null || rebased == property.stringValue) return false;
            property.stringValue = rebased;
            return true;
        }

        // Destructive Move of a VRCFury Toggle or Puppet: its feature's menu path becomes the destination's path and its name.
        private static void Repath(MenuToolEntry entry, MenuToolEntry destination)
        {
            var serialized = new SerializedObject(entry.VrcfComponent);
            foreach (SerializedProperty feature in MenuToolScanner.Features(serialized))
            {
                string kind = MenuToolScanner.FeatureName(feature);
                SerializedProperty name = feature.FindPropertyRelative("name");
                if ((kind != "Toggle" && kind != "Puppet") || name == null || name.stringValue != entry.VrcfPath) continue;
                string into = MenuToolVrcfuryMenu.PathOf(destination);
                name.stringValue = (into.Length == 0 ? "" : into + "/") + MenuToolVrcfuryMenu.Escape(entry.Name);
                serialized.ApplyModifiedProperties();
                return;
            }
            throw new InvalidOperationException("The VRCFury feature for " + entry.Path + " was not found on " + entry.VrcfComponent.name + ".");
        }

        // Add menu as a menu asset: a new empty menu in Assets/RiryTools/MenuTool/<avatar>/, linked by a SubMenu control at the end
        // of the parent's menu (made safe to change first).
        private static void NewMenuAsset(MenuToolScan scan, MenuToolEntry parent, string name, Report report)
        {
            VRCExpressionsMenu holder = Owned(scan, parent);
            string folder = "Assets/RiryTools/MenuTool/" + Safe(scan.Avatar.name);
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Safe(name) + ".asset");
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, path);
            report.CreatedAssets.Add(path);
            Undo.RecordObject(holder, UndoName);
            holder.controls.Add(new VRCExpressionsMenu.Control
            {
                name = name,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = menu,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0]
            });
            EditorUtility.SetDirty(holder);
        }

        // Add New Control in a menu asset: the control at the end of the parent's menu (made safe to change first).
        private static void NewControlAsset(MenuToolScan scan, MenuToolEntry parent, MenuToolEntry control, Report report)
        {
            VRCExpressionsMenu holder = Owned(scan, parent);
            Undo.RecordObject(holder, UndoName);
            holder.controls.Add(new VRCExpressionsMenu.Control
            {
                name = control.Name,
                type = control.Type,
                icon = control.Icon,
                value = control.Value,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = control.Parameter },
                subParameters = control.SubParameters.Select(n => new VRCExpressionsMenu.Control.Parameter { name = n }).ToArray(),
                labels = new VRCExpressionsMenu.Control.Label[0]
            });
            EditorUtility.SetDirty(holder);
        }

        // Remove Menu Item from a menu asset: the control leaves the menu its slot shows (a copy when shared).
        private static void RemoveNative(MenuToolScan scan, MenuToolEntry entry, Report report)
        {
            VRCExpressionsMenu holder = Owned(scan, entry.Parent);
            Undo.RecordObject(holder, UndoName);
            holder.controls.RemoveAt(entry.OwnerIndex);
            EditorUtility.SetDirty(holder);
        }

        // Assign Menu Icon through the chosen owner.
        private static void SetIcon(MenuToolScan scan, MenuToolEntry entry, Texture2D icon, MenuToolMoveEdit edit, Report report)
        {
            switch (edit)
            {
                case MenuToolMoveEdit.IconAsset:
                    VRCExpressionsMenu holder = Owned(scan, entry.Parent);
                    Undo.RecordObject(holder, UndoName);
                    holder.controls[entry.OwnerIndex].icon = icon;
                    EditorUtility.SetDirty(holder);
                    break;
#if RIRYTOOLS_MENUTOOL_MA
                case MenuToolMoveEdit.IconMa:
                    var item = new SerializedObject(entry.MaItem);
                    item.FindProperty("Control.icon").objectReferenceValue = icon;
                    item.ApplyModifiedProperties();
                    break;
#endif
                default:
                    NewRule(scan.Avatar.gameObject, RiryToolsTypes.Named(VrcfuryType), SetIconType, rule =>
                    {
                        rule.FindPropertyRelative("path").stringValue = MenuToolVrcfuryMenu.PathOf(entry);
                        rule.FindPropertyRelative("icon.objRef").objectReferenceValue = icon;
                    });
                    break;
            }
        }

        /// <summary>
        /// The menu a native slot shows (slot: a submenu entry, or the root: the descriptor's menu). Edited in place: what is on
        /// the avatar's own menu stays on it, never copied.
        /// </summary>
        private static VRCExpressionsMenu Owned(MenuToolScan scan, MenuToolEntry slot)
        {
            return slot.Parent == null ? scan.Avatar.expressionsMenu : slot.SubMenuAsset;
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

        private static void AddRule(MenuToolScan scan, string from, string to, int index, bool sameMenu)
        {
            GameObject avatar = scan.Avatar.gameObject;
            Type vrcfury = RiryToolsTypes.Named(VrcfuryType);
            if (!sameMenu)
            {
                // A rule that already moved this entry here is updated, so rules don't stack up.
                SerializedObject existing = null;
                foreach (Component component in avatar.GetComponents(vrcfury))
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty content = serialized.FindProperty("content");
                    if (content == null || MenuToolScanner.FeatureName(content) != "MoveMenuItem") continue;
                    if (MenuToolVrcfuryMenu.SamePath(content.FindPropertyRelative("toPath").stringValue, from)) existing = serialized;
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
            Type feature = RiryToolsTypes.Named(featureType);
            if (feature == null) throw new InvalidOperationException("VRCFury's " + featureType + " was not found; VRCFury may have changed.");
            Component component = Undo.AddComponent(avatar, vrcfury);
            var serialized = new SerializedObject(component);
            SerializedProperty content = serialized.FindProperty("content");
            content.managedReferenceValue = Activator.CreateInstance(feature, true);
            set(content);
            serialized.ApplyModifiedProperties();
        }

#if RIRYTOOLS_MENUTOOL_MA
        // ---- Modular Avatar ------------------------------------------------------------

        // Add menu as a Modular Avatar Menu Item: a new object named after the menu with a SubMenu item whose items are its own
        // children, last under the parent's children object; for any other parent, under the avatar with a Menu Installer
        // targeting the parent's menu (none for the root menu).
        private static void NewMaItem(MenuToolScan scan, MenuToolEntry parent, string name)
        {
            NewMaObject(scan, parent, new MenuToolEntry { Name = name });
        }

        // Add New Control as a Modular Avatar Menu Item, placed as NewMaItem places a new menu.
        private static void NewControlMa(MenuToolScan scan, MenuToolEntry parent, MenuToolEntry control)
        {
            NewMaObject(scan, parent, control);
        }

        // The new object for an MA item: SubMenu (children) for a new menu, else the control's fields.
        private static void NewMaObject(MenuToolScan scan, MenuToolEntry parent, MenuToolEntry control)
        {
            string name = control.Name;
            Type itemType = RiryToolsTypes.Named(MaItemType), installerType = RiryToolsTypes.Named(MaInstallerType);
            if (itemType == null || installerType == null) throw new InvalidOperationException("Modular Avatar's menu components were not found; Modular Avatar may have changed.");
            Transform under = parent.MaChildrenSource != null ? parent.MaChildrenSource.transform : scan.Avatar.transform;
            var made = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(made, UndoName);
            Undo.SetTransformParent(made.transform, under, UndoName);
            made.transform.localPosition = Vector3.zero;
            made.transform.localRotation = Quaternion.identity;
            made.transform.localScale = Vector3.one;
            made.transform.SetAsLastSibling();
            if (parent.MaChildrenSource == null)
            {
                var installer = new SerializedObject(Undo.AddComponent(made, installerType));
                installer.FindProperty("installTargetMenu").objectReferenceValue = parent.Parent == null ? null : parent.SubMenuAsset;
                installer.ApplyModifiedProperties();
            }
            var item = new SerializedObject(Undo.AddComponent(made, itemType));
            item.FindProperty("Control.name").stringValue = name;
            item.FindProperty("Control.type").intValue = (int)control.Type;
            if (control.IsSubMenu) item.FindProperty("MenuSource").intValue = MaChildrenSource;
            else
            {
                item.FindProperty("Control.icon").objectReferenceValue = control.Icon;
                item.FindProperty("Control.value").floatValue = control.Value;
                item.FindProperty("Control.parameter.name").stringValue = control.Parameter;
                SerializedProperty subs = item.FindProperty("Control.subParameters");
                subs.arraySize = control.SubParameters.Length;
                for (int i = 0; i < control.SubParameters.Length; i++) subs.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = control.SubParameters[i];
            }
            item.ApplyModifiedProperties();
        }

        private static void Reparent(MenuToolEntry entry, MenuToolEntry destination, int index)
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

        private static void Retarget(MenuToolScan scan, MenuToolEntry entry, MenuToolEntry destination)
        {
            var serialized = new SerializedObject(entry.MaInstaller);
            SerializedProperty target = serialized.FindProperty("installTargetMenu");
            // The root menu: no target (Modular Avatar installs into the avatar's root menu).
            target.objectReferenceValue = destination.Parent == null ? null : destination.SubMenuAsset;
            serialized.ApplyModifiedProperties();
        }
#endif
    }
}

// ============================================================================
// GLOSSARY: Menu Tool apply
// ============================================================================
//
// MenuToolApply (this file)
//   Apply(target, plan)      One undo group ("Apply menu layout", CollapseUndoOperations). For each planned move or new menu: a
//                            fresh scan, the move checked alone (MenuToolPlanner), then its edit; afterwards a scan verifies each
//                            moved entry's path, type, icon and parameters, and each new menu's path (Report.Problems). Logged
//                            under Menu Tool, with copied (CopiedAssets) and new (CreatedAssets) menu assets.
//   Index                    The ReferenceIndex, built on first need once per Apply.
//   MoveNative               Removes the control from its menu asset and inserts it in the destination's (RecordObject both).
//   NewMenuAsset             Add menu: a new empty VRCExpressionsMenu at Assets/RiryTools/MenuTool/<avatar>/<name>.asset (unique
//                            path; a file Ctrl+Z leaves), and a SubMenu control for it at the end of the parent's menu (Owned).
//   Owned(scan, uses, slot)  The menu one slot shows (the moved entry's submenu or the destination; the root's is the
//                            descriptor's), copied to Assets/RiryTools/MenuTool/<avatar>/ (Report.CopiedAssets) when anything else
//                            uses it, and relinked in that slot only (the holding menu made safe first). Copies are files:
//                            Ctrl+Z leaves them.
//   MenuUses.Shared          Anything else using the menu: another slot of this avatar, a Modular Avatar or VRCFury contribution,
//                            a package, another avatar in an open scene, or any other .asset / .prefab / .unity file in the project
//                            (ReferenceIndex, built once per Apply for every native menu of the avatar: a text search for each
//                            GUID, AssetDatabase.GetDependencies for binary files; copies made by this Apply are Made), apart
//                            from the file holding this avatar (HolderFile) and this avatar's own native menus.
//   AddRule / NewRule        VRCFury on the avatar object: updates a Move Menu Item whose toPath is the entry's path
//                            (MenuToolVrcfuryMenu.SamePath), else adds one (content set through SerializedObject); a set index
//                            adds a Reorder Menu Item. Paths are written as VRCFury reads them (PathOf / Escape: "\/" in names).
//   Reparent                 Undo.SetTransformParent into the destination's MA children object, then the sibling index.
//   Retarget                 The MA installer's installTargetMenu (none for the root menu).
//   NewMaItem                Add menu: a new object named after the menu (RegisterCreatedObjectUndo), last under the parent's MA
//                            children object, or under the avatar with an MA Menu Installer (installTargetMenu the parent's
//                            asset, none for the root); an MA Menu Item on it (Control.name, Control.type SubMenu, MenuSource
//                            Children, MaChildrenSource 1). MA types by name (MaInstallerType, MaItemType). NewMaObject makes
//                            it; NewControlMa uses it for a control (type, icon, value, parameter, sub-parameters).
//   NewControlAsset          Add New Control: the control at the end of the parent's menu asset (Owned).
//   New VRCFury Toggle       NewRule ToggleType on the avatar: name (the menu path), enableIcon/icon, useGlobalParam/globalParam
//                            for a named parameter (else VRCFury makes one); its parameters are not verified after Apply.
//   SetIcon                  Assign Menu Icon: the control's icon in its menu asset (Owned), the MA item's Control.icon, or a
//                            VRCFury Set Icon rule (SetIconType: path, icon.objRef).
