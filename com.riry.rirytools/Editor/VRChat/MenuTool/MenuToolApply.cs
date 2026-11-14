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
        private const string MaInstallerType = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MaItemType = "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        // Modular Avatar's SubmenuSource.Children.
        private const int MaChildrenSource = 1;
        private const string UndoName = "Apply menu layout";

        internal sealed class Report
        {
            internal int Applied;
            internal readonly List<string> Problems = new List<string>();
            internal readonly List<string> CopiedAssets = new List<string>();
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
            ReferenceIndex references = null;
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
                        if (check.Edit == MenuToolMoveEdit.NewMenuAsset) NewMenuAsset(scan, destination, move.NewMenu.Trim(), Index(scan, ref references), report);
                        else NewMaItem(scan, destination, move.NewMenu.Trim());
                        report.Applied++;
                        continue;
                    }
                    if (move.IsAddControl)
                    {
                        var control = new MenuToolEntry { Name = move.NewControl.Trim(), Type = (VRCExpressionsMenu.Control.ControlType)move.ControlType };
                        MenuToolPlanner.ControlOf(move, control);
                        expected.Add(new Expected { Path = check.After, Type = control.Type, Icon = control.Icon,
                            Parameters = check.Edit == MenuToolMoveEdit.NewControlVrcfury ? null : control.Parameters().ToArray() });
                        if (check.Edit == MenuToolMoveEdit.NewControlAsset) NewControlAsset(scan, destination, control, Index(scan, ref references), report);
                        else if (check.Edit == MenuToolMoveEdit.NewControlMa) NewControlMa(scan, destination, control);
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
                    if (move.Remove)
                    {
                        if (check.Edit == MenuToolMoveEdit.RemoveAsset) RemoveNative(scan, entry, Index(scan, ref references), report);
                        else AddRule(scan, MenuToolVrcfuryMenu.PathOf(entry), "", -1, false);
                        report.Applied++;
                        continue;
                    }
                    if (move.AssignIcon)
                    {
                        Texture2D icon = move.IconTexture;
                        expected.Add(new Expected { Path = entry.Path, Type = entry.Type, Icon = icon, Parameters = entry.Parameters().ToArray() });
                        SetIcon(scan, entry, icon, check.Edit, Index(scan, ref references), report);
                        report.Applied++;
                        continue;
                    }
                    expected.Add(new Expected { Path = check.After, Type = entry.Type, Icon = entry.Icon, Parameters = entry.Parameters().ToArray() });
                    switch (check.Edit)
                    {
                        case MenuToolMoveEdit.NativeAssets:
                            MoveNative(scan, entry, destination, move.Index, Index(scan, ref references), report);
                            break;
                        case MenuToolMoveEdit.VrcfuryRule:
                            // Paths as VRCFury reads them: a slash inside a name written "\/".
                            string from = MenuToolVrcfuryMenu.PathOf(entry), into = MenuToolVrcfuryMenu.PathOf(destination);
                            AddRule(scan, from, (into.Length == 0 ? "" : into + "/") + MenuToolVrcfuryMenu.Escape(entry.Name), move.Index, entry.Parent == destination);
                            break;
                        case MenuToolMoveEdit.MaReparent: Reparent(entry, destination, move.Index); break;
                        case MenuToolMoveEdit.DestructiveAsset: MoveInPlace(entry, destination, move.Index); break;
                        case MenuToolMoveEdit.DestructivePath: Repath(entry, destination); break;
                        case MenuToolMoveEdit.MaRetarget: Retarget(scan, entry, destination); break;
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
                (report.CopiedAssets.Count > 0 ? "; copied shared menus to " + string.Join(", ", report.CopiedAssets) : "") +
                (report.CreatedAssets.Count > 0 ? "; new menus at " + string.Join(", ", report.CreatedAssets) : "") + ".", target);
            foreach (string problem in report.Problems) RiryLog.Warn(RiryLog.MenuTool, problem, target);
            return report;
        }

        // The project search, once per Apply, for every native menu of the avatar.
        private static ReferenceIndex Index(MenuToolScan scan, ref ReferenceIndex references)
        {
            if (references == null) references = new ReferenceIndex(new[] { scan.Root }.Concat(scan.Root.Descendants())
                .Where(e => e.Origin == MenuToolOrigin.Native).SelectMany(e => new[] { e.OwnerMenu, e.SubMenuAsset })
                .Concat(new[] { scan.Avatar.expressionsMenu }).Where(m => m != null).Select(m => AssetDatabase.GetAssetPath(m)));
            return references;
        }

        // ---- Native ----------------------------------------------------------------

        private static void MoveNative(MenuToolScan scan, MenuToolEntry entry, MenuToolEntry destination, int at, ReferenceIndex index, Report report)
        {
            var uses = new MenuUses(scan, index);
            // Each menu through the slot on the moved entry's path (its submenu, or the root), so a menu shown twice only
            // changes in that one place.
            VRCExpressionsMenu from = Owned(scan, uses, entry.Parent, report);
            VRCExpressionsMenu to = Owned(scan, uses, destination, report);
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
        private static void NewMenuAsset(MenuToolScan scan, MenuToolEntry parent, string name, ReferenceIndex index, Report report)
        {
            VRCExpressionsMenu holder = Owned(scan, new MenuUses(scan, index), parent, report);
            string folder = "Assets/RiryTools/MenuTool/" + Safe(scan.Avatar.name);
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Safe(name) + ".asset");
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, path);
            index.Made.Add(path);
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
        private static void NewControlAsset(MenuToolScan scan, MenuToolEntry parent, MenuToolEntry control, ReferenceIndex index, Report report)
        {
            VRCExpressionsMenu holder = Owned(scan, new MenuUses(scan, index), parent, report);
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
        private static void RemoveNative(MenuToolScan scan, MenuToolEntry entry, ReferenceIndex index, Report report)
        {
            VRCExpressionsMenu holder = Owned(scan, new MenuUses(scan, index), entry.Parent, report);
            Undo.RecordObject(holder, UndoName);
            holder.controls.RemoveAt(entry.OwnerIndex);
            EditorUtility.SetDirty(holder);
        }

        // Assign Menu Icon through the chosen owner.
        private static void SetIcon(MenuToolScan scan, MenuToolEntry entry, Texture2D icon, MenuToolMoveEdit edit, ReferenceIndex index, Report report)
        {
            switch (edit)
            {
                case MenuToolMoveEdit.IconAsset:
                    VRCExpressionsMenu holder = Owned(scan, new MenuUses(scan, index), entry.Parent, report);
                    Undo.RecordObject(holder, UndoName);
                    holder.controls[entry.OwnerIndex].icon = icon;
                    EditorUtility.SetDirty(holder);
                    break;
                case MenuToolMoveEdit.IconMa:
                    var item = new SerializedObject(entry.MaItem);
                    item.FindProperty("Control.icon").objectReferenceValue = icon;
                    item.ApplyModifiedProperties();
                    break;
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
        /// The menu a native slot shows (slot: a submenu entry, or the root), made safe to change: the menu itself, or a
        /// copy when anything else uses it (MenuUses.Shared), relinked in that slot only (its holding menu made safe the
        /// same way first).
        /// </summary>
        private static VRCExpressionsMenu Owned(MenuToolScan scan, MenuUses uses, MenuToolEntry slot, Report report)
        {
            VRCExpressionsMenu menu = slot.Parent == null ? scan.Avatar.expressionsMenu : slot.SubMenuAsset;
            if (!uses.Shared(menu, slot)) return menu;
            string folder = "Assets/RiryTools/MenuTool/" + Safe(scan.Avatar.name);
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Safe(menu.name) + ".asset");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(menu), path)) throw new IOException("Could not copy " + menu.name + " to " + path);
            var copy = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(path);
            report.CopiedAssets.Add(path);
            if (slot.Parent == null)
            {
                Undo.RecordObject(scan.Avatar, UndoName);
                scan.Avatar.expressionsMenu = copy;
                EditorUtility.SetDirty(scan.Avatar);
                scan.Root.SubMenuAsset = copy;
            }
            else
            {
                VRCExpressionsMenu holder = Owned(scan, uses, slot.Parent, report);
                Undo.RecordObject(holder, UndoName);
                holder.controls[slot.OwnerIndex].subMenu = copy;
                EditorUtility.SetDirty(holder);
                slot.SubMenuAsset = copy;
                slot.OwnerMenu = holder;
            }
            // The controls of this slot now live in the copy; the same menu shown elsewhere keeps the original.
            foreach (MenuToolEntry child in slot.Children.Where(e => e.OwnerMenu == menu)) child.OwnerMenu = copy;
            uses.Copied(menu, copy, slot);
            return copy;
        }

        /// <summary>
        /// Which project files reference each of a set of menu assets, read once per Apply: a text search for each GUID in
        /// every .asset, .prefab and .unity file (AssetDatabase.GetDependencies for a binary-serialized one). Apply only ever
        /// relinks to its own copies (Made), so an index taken at the start can only over-report a use, never miss one.
        /// </summary>
        internal sealed class ReferenceIndex
        {
            private readonly Dictionary<string, List<string>> byMenu = new Dictionary<string, List<string>>();
            internal readonly HashSet<string> Made = new HashSet<string>();

            internal ReferenceIndex(IEnumerable<string> menuPaths)
            {
                Dictionary<string, string> guids = menuPaths.Where(p => !string.IsNullOrEmpty(p)).Distinct()
                    .ToDictionary(p => p, p => AssetDatabase.AssetPathToGUID(p));
                foreach (string path in guids.Keys) byMenu[path] = new List<string>();
                if (guids.Count == 0) return;
                string[] files = AssetDatabase.GetAllAssetPaths().Where(f => f.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)).ToArray();
                try
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (i % 200 == 0) EditorUtility.DisplayProgressBar("Menu Tool", "Looking for other uses of the menus", (float)i / files.Length);
                        string text = Text(files[i]);
                        string[] dependencies = text == null ? AssetDatabase.GetDependencies(files[i], false) : null;
                        foreach (KeyValuePair<string, string> menu in guids)
                        {
                            if (files[i] == menu.Key) continue;
                            if (text != null ? text.Contains(menu.Value) : dependencies.Contains(menu.Key)) byMenu[menu.Key].Add(files[i]);
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            internal List<string> Referencing(string menuPath)
            {
                List<string> files;
                // A menu not in the index (made during this Apply): only its own references, which are ours.
                return byMenu.TryGetValue(menuPath, out files) ? files : new List<string>();
            }

            // The file's text when it is a text-serialized asset, else null.
            private static string Text(string file)
            {
                try
                {
                    // Files of packages in Unity's package cache are not under the project folder: those use the fallback.
                    string physical = Path.GetFullPath(file);
                    if (!File.Exists(physical)) return null;
                    using (var reader = new StreamReader(physical))
                    {
                        var head = new char[5];
                        if (reader.Read(head, 0, 5) != 5 || new string(head) != "%YAML") return null;
                        return reader.ReadToEnd();
                    }
                }
                catch (IOException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Who else uses a menu asset: another slot of this avatar's menu, a Modular Avatar or VRCFury contribution, a
        /// package (read-only), or any other asset, prefab or scene file in the project (a text search for its GUID, falling
        /// back to the asset database for binary files), apart from the file holding this avatar and this avatar's own
        /// native menus. Built once per move.
        /// </summary>
        private sealed class MenuUses
        {
            private readonly MenuToolScan scan;
            private readonly HashSet<string> own = new HashSet<string>();
            private readonly ReferenceIndex index;
            private readonly HashSet<MenuToolEntry> copiedSlots = new HashSet<MenuToolEntry>();

            internal MenuUses(MenuToolScan scan, ReferenceIndex index)
            {
                this.scan = scan;
                this.index = index;
                string holder = HolderFile(scan.Avatar);
                if (holder.Length > 0) own.Add(holder);
                foreach (MenuToolEntry entry in new[] { scan.Root }.Concat(scan.Root.Descendants()).Where(e => e.Origin == MenuToolOrigin.Native))
                {
                    if (entry.OwnerMenu != null) own.Add(AssetDatabase.GetAssetPath(entry.OwnerMenu));
                    if (entry.SubMenuAsset != null) own.Add(AssetDatabase.GetAssetPath(entry.SubMenuAsset));
                }
                if (scan.Avatar.expressionsMenu != null) own.Add(AssetDatabase.GetAssetPath(scan.Avatar.expressionsMenu));
            }

            internal bool Shared(VRCExpressionsMenu menu, MenuToolEntry slot)
            {
                string path = AssetDatabase.GetAssetPath(menu);
                if (path.StartsWith("Packages/", StringComparison.Ordinal)) return true;
                // Another slot of this avatar shows the same menu (and was not given its own copy yet).
                MenuToolEntry[] slots = new[] { scan.Root }.Concat(scan.Root.Descendants())
                    .Where(e => e.IsSubMenu && (e.Parent == null ? scan.Avatar.expressionsMenu == menu : e.SubMenuAsset == menu) && !copiedSlots.Contains(e)).ToArray();
                if (slots.Any(e => e != slot)) return true;
                // A Modular Avatar or VRCFury contribution uses it (an installer's menu, an item's submenu, a Full Controller menu).
                if (scan.Root.Descendants().Any(e => e.Origin != MenuToolOrigin.Native && (e.OwnerMenu == menu || e.SubMenuAsset == menu))) return true;
                // Another avatar in an open scene reaches it.
                foreach (VRCAvatarDescriptor other in Object.FindObjectsOfType<VRCAvatarDescriptor>(true))
                {
                    if (other == scan.Avatar || other.expressionsMenu == null) continue;
                    if (Reaches(other.expressionsMenu, menu, new HashSet<VRCExpressionsMenu>())) return true;
                }
                // Any other file in the project: other prefabs, variants, scenes, menus.
                return index.Referencing(path).Any(file => !own.Contains(file) && !index.Made.Contains(file));
            }

            internal void Copied(VRCExpressionsMenu menu, VRCExpressionsMenu copy, MenuToolEntry slot)
            {
                copiedSlots.Add(slot);
                index.Made.Add(AssetDatabase.GetAssetPath(copy));
            }

            // The scene or prefab file the avatar is saved in ("" for an unsaved scene).
            private static string HolderFile(VRCAvatarDescriptor avatar)
            {
                if (EditorUtility.IsPersistent(avatar)) return AssetDatabase.GetAssetPath(avatar);
                UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(avatar.gameObject);
                if (stage != null) return stage.assetPath;
                return avatar.gameObject.scene.path ?? "";
            }
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
