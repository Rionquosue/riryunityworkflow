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
    /// <summary>
    /// One planned move: the entry at From goes into the submenu To, at Index (-1 is the end). With NewMenu set it adds
    /// instead: a new, empty submenu of that name at the end of To (From is "").
    /// </summary>
    [Serializable]
    internal sealed class PlannedMove
    {
        public string From = "";
        public string To = "";
        public int Index = -1;
        // The edit the user chose when ownership is ambiguous ("" until chosen).
        public string Choice = "";
        // Add menu: the new submenu's name ("" for a move).
        public string NewMenu = "";
        // Add New Control: the new control's name, type (ControlType), parameter, value and icon (texture GUID).
        public string NewControl = "";
        public int ControlType;
        public string Parameter = "";
        public float Value = 1f;
        public string Icon = "";
        // Assign Menu Icon: the entry at From gets the texture Icon.
        public bool AssignIcon;

        internal bool IsAdd
        {
            get { return !string.IsNullOrEmpty(NewMenu); }
        }

        internal bool IsAddControl
        {
            get { return !string.IsNullOrEmpty(NewControl); }
        }

        internal Texture2D IconTexture
        {
            get { return string.IsNullOrEmpty(Icon) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(Icon)); }
        }
    }

    /// <summary>
    /// The planned menu layout of one avatar: moves recorded by Move to... and drag and drop, and menus added by Add menu,
    /// kept in a JSON file
    /// (UserSettings/RiryTools/MenuTool/&lt;avatar key&gt;.json, the key from MenuToolAvatarKeys) and applied to nothing
    /// until Apply. The sources are only read while planning.
    /// </summary>
    [Serializable]
    internal sealed class MenuPlan
    {
        public string Avatar = "";
        // The avatar's key (MenuToolAvatarKeys): the plan is always saved to its own avatar's file.
        public string Key = "";
        public List<PlannedMove> Moves = new List<PlannedMove>();

        internal static string Folder
        {
            get { return Path.Combine(RiryToolsPaths.ProjectRoot, "UserSettings", "RiryTools", "MenuTool"); }
        }

        private string FilePath
        {
            get { return Path.Combine(Folder, Key + ".json"); }
        }

        internal static MenuPlan Load(GameObject avatar)
        {
            var plan = new MenuPlan { Avatar = avatar != null ? avatar.name : "" };
            if (avatar == null) return plan;
            plan.Key = MenuToolAvatarKeys.KeyFor(avatar);
            string path = plan.FilePath;
            if (!File.Exists(path)) return plan;
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), plan);
                plan.Key = MenuToolAvatarKeys.KeyFor(avatar);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Reading the menu plan " + path, exception, avatar);
            }
            return plan;
        }

        /// <summary>What Save writes, to tell whether the plan changed.</summary>
        internal string Json
        {
            get { return JsonUtility.ToJson(this, true); }
        }

        internal void Save()
        {
            if (string.IsNullOrEmpty(Key)) return;
            string path = FilePath;
            try
            {
                if (Moves.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                Directory.CreateDirectory(Folder);
                File.WriteAllText(path, Json);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Saving the menu plan " + path, exception);
            }
        }
    }

    /// <summary>
    /// A GUID stamped on each avatar the Menu Tool plans for, kept in UserSettings (never on the avatar): avatars.json maps
    /// each avatar's GlobalObjectId to its key. An avatar in a scene not saved yet has no lasting id, so its key is kept
    /// for the session by instance id and moved into avatars.json when its scene is saved.
    /// </summary>
    [InitializeOnLoad]
    internal static class MenuToolAvatarKeys
    {
        private const string UnsavedKey = "RiryTools.MenuTool.UnsavedAvatars";

        [Serializable]
        private sealed class Entry
        {
            public string Object = "";
            public string Key = "";
        }

        [Serializable]
        private sealed class Registry
        {
            public List<Entry> Avatars = new List<Entry>();
        }

        static MenuToolAvatarKeys()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static string RegistryPath
        {
            get { return Path.Combine(MenuPlan.Folder, "avatars.json"); }
        }

        internal static string KeyFor(GameObject avatar)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(avatar);
            if (Lasting(id))
            {
                Registry registry = Read();
                string text = id.ToString();
                Entry entry = registry.Avatars.FirstOrDefault(e => e.Object == text);
                if (entry == null)
                {
                    entry = new Entry { Object = text, Key = NewKey() };
                    registry.Avatars.Add(entry);
                    Write(registry);
                }
                return entry.Key;
            }
            Dictionary<int, string> unsaved = Unsaved();
            string key;
            if (!unsaved.TryGetValue(avatar.GetInstanceID(), out key))
            {
                key = NewKey();
                unsaved[avatar.GetInstanceID()] = key;
                WriteUnsaved(unsaved);
            }
            return key;
        }

        // An object in a saved scene or a prefab asset has a GlobalObjectId that lasts; one in an unsaved scene has none.
        private static bool Lasting(GlobalObjectId id)
        {
            return id.identifierType != 0 && !id.assetGUID.Empty();
        }

        private static string NewKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            Dictionary<int, string> unsaved = Unsaved();
            if (unsaved.Count == 0) return;
            Registry registry = null;
            foreach (KeyValuePair<int, string> pair in unsaved.ToList())
            {
                var avatar = EditorUtility.InstanceIDToObject(pair.Key) as GameObject;
                if (avatar == null)
                {
                    unsaved.Remove(pair.Key);
                    continue;
                }
                if (avatar.scene != scene) continue;
                GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(avatar);
                if (!Lasting(id)) continue;
                if (registry == null) registry = Read();
                registry.Avatars.RemoveAll(e => e.Object == id.ToString());
                registry.Avatars.Add(new Entry { Object = id.ToString(), Key = pair.Value });
                unsaved.Remove(pair.Key);
            }
            if (registry != null) Write(registry);
            WriteUnsaved(unsaved);
        }

        private static Registry Read()
        {
            var registry = new Registry();
            try
            {
                if (File.Exists(RegistryPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(RegistryPath), registry);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Reading " + RegistryPath, exception);
            }
            return registry;
        }

        private static void Write(Registry registry)
        {
            try
            {
                Directory.CreateDirectory(MenuPlan.Folder);
                File.WriteAllText(RegistryPath, JsonUtility.ToJson(registry, true));
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Saving " + RegistryPath, exception);
            }
        }

        // "instance id=key" pairs, one per line.
        private static Dictionary<int, string> Unsaved()
        {
            var map = new Dictionary<int, string>();
            foreach (string line in SessionState.GetString(UnsavedKey, "").Split('\n'))
            {
                int equals = line.IndexOf('=');
                int id;
                if (equals > 0 && int.TryParse(line.Substring(0, equals), out id)) map[id] = line.Substring(equals + 1);
            }
            return map;
        }

        private static void WriteUnsaved(Dictionary<int, string> map)
        {
            SessionState.SetString(UnsavedKey, string.Join("\n", map.Select(pair => pair.Key + "=" + pair.Value)));
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
        // Add menu: a new menu asset, linked by a SubMenu control in the parent's menu asset (copying a shared one first).
        NewMenuAsset,
        // Add menu: a new object with a Modular Avatar Menu Item (SubMenu, children from its own object), under the parent's
        // MA children object, or under the avatar with an MA Menu Installer targeting the parent menu.
        NewMaItem,
        // Add New Control: a control in the parent's menu asset, an MA Menu Item object (as NewMaItem), or a VRCFury Toggle.
        NewControlAsset,
        NewControlMa,
        NewControlVrcfury,
        // Assign Menu Icon: the control's icon in its menu asset, a VRCFury Set Icon rule, or the MA Menu Item's icon.
        IconAsset,
        IconVrcfury,
        IconMa,
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
                if (move.IsAdd || move.IsAddControl)
                {
                    MoveCheck added = CheckAdd(scan, root, move);
                    checks.Add(added);
                    if (added.Errors.Count > 0) continue;
                    var made = new MenuEntry
                    {
                        Name = (move.IsAdd ? move.NewMenu : move.NewControl).Trim(),
                        Type = move.IsAdd ? VRCExpressionsMenu.Control.ControlType.SubMenu : (VRCExpressionsMenu.Control.ControlType)move.ControlType,
                        Origin = added.Edit == MoveEdit.NewMaItem || added.Edit == MoveEdit.NewControlMa ? MenuOrigin.ModularAvatar
                            : added.Edit == MoveEdit.NewControlVrcfury ? MenuOrigin.VRCFury : MenuOrigin.Native,
                        Added = added.Edit
                    };
                    if (move.IsAddControl) ControlOf(move, made);
                    added.Destination.Add(made);
                    added.Entry = made;
                    continue;
                }
                if (move.AssignIcon)
                {
                    MoveCheck icon = CheckIcon(scan, root, move);
                    checks.Add(icon);
                    if (icon.Errors.Count == 0) icon.Entry.Icon = move.IconTexture;
                    continue;
                }
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

        /// <summary>A new control's fields from its planned add: puppets take the parameter as their first sub-parameter.</summary>
        internal static void ControlOf(PlannedMove move, MenuEntry entry)
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
        private static MoveCheck CheckIcon(MenuScan scan, MenuEntry root, PlannedMove move)
        {
            Texture2D texture = move.IconTexture;
            var check = new MoveCheck { Move = move, Before = move.From, After = move.From + " (icon " + (texture != null ? texture.name : "?") + ")" };
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
            MenuEntry entry = check.Entry;
            bool asset = entry.Origin == MenuOrigin.Native && (entry.OwnerMenu != null || entry.Added == MoveEdit.NewControlAsset || entry.Added == MoveEdit.NewMenuAsset);
            bool ma = entry.Origin == MenuOrigin.ModularAvatar && (entry.MaItem != null && !EditorUtility.IsPersistent(entry.MaItem) || entry.Added == MoveEdit.NewControlMa || entry.Added == MoveEdit.NewMaItem);
            if (asset) check.Candidates.Add(MoveEdit.IconAsset);
            if (ma) check.Candidates.Add(MoveEdit.IconMa);
            if (scan.VrcfuryInstalled && scan.Avatar != null) check.Candidates.Add(MoveEdit.IconVrcfury);
            Decide(check, entry.Origin == MenuOrigin.Native && asset ? MoveEdit.IconAsset : entry.Origin == MenuOrigin.ModularAvatar && ma ? MoveEdit.IconMa
                : entry.Origin == MenuOrigin.VRCFury && check.Candidates.Contains(MoveEdit.IconVrcfury) ? MoveEdit.IconVrcfury : MoveEdit.Ambiguous,
                "No supported edit sets the icon of " + entry.Path + ".");
            if (check.Edit == MoveEdit.IconAsset && entry.OwnerMenu != null) check.Affected.Add(entry.OwnerMenu);
            if (check.Edit == MoveEdit.IconMa && entry.MaItem != null) check.Affected.Add(entry.MaItem);
            if (check.Edit == MoveEdit.IconVrcfury) check.Affected.Add(scan.Avatar.gameObject);
            return check;
        }

        // The edit: the user's Choice when it fits, else preferred (the only candidate when there is one); none is Invalid.
        private static void Decide(MoveCheck check, MoveEdit preferred, string none)
        {
            if (check.Candidates.Count == 0)
            {
                check.Edit = MoveEdit.Invalid;
                check.Errors.Add(none);
                return;
            }
            if (check.Candidates.Count == 1) preferred = check.Candidates[0];
            MoveEdit chosen;
            if (!string.IsNullOrEmpty(check.Move.Choice) && Enum.TryParse(check.Move.Choice, out chosen) && check.Candidates.Contains(chosen)) check.Edit = chosen;
            else check.Edit = preferred;
            if (check.Edit == MoveEdit.Ambiguous) check.Warnings.Add("More than one source could make this: choose one.");
        }

        // Add menu / Add New Control: the parent must still be a submenu with room and no item of that name; then who owns
        // the parent picks the edit.
        private static MoveCheck CheckAdd(MenuScan scan, MenuEntry root, PlannedMove move)
        {
            if (move.IsAddControl) return CheckAddControl(scan, root, move);
            string name = move.NewMenu.Trim();
            string parentName = string.IsNullOrEmpty(move.To) ? "the root menu" : move.To;
            var check = new MoveCheck { Move = move, Before = "New menu" };
            check.Destination = root.Find(move.To ?? "");
            check.After = (string.IsNullOrEmpty(move.To) ? "" : move.To + "/") + name;
            if (name.Length == 0 || name.Contains("/"))
            {
                check.Errors.Add("A menu name cannot be empty or contain /.");
                return check;
            }
            if (check.Destination == null || !check.Destination.IsSubMenu)
            {
                check.Errors.Add("The parent " + parentName + " is no longer a submenu.");
                return check;
            }
            if (check.Destination.Children.Any(child => child.Name == name))
            {
                check.Errors.Add(parentName + " already has an item named " + name + ".");
                return check;
            }
            int count = check.Destination.Children.Count + 1;
            if (count > MenuToolScanner.MaxControls)
            {
                if (scan.VrcfuryInstalled) check.Warnings.Add(parentName + " will hold " + count + " controls: VRCFury pages the rest into a Next submenu.");
                else check.Errors.Add(parentName + " would hold " + count + " controls; VRChat allows " + MenuToolScanner.MaxControls + ".");
            }
            MenuEntry parent = check.Destination;
            if (scan.Avatar != null && NativeAsset(scan, parent)) check.Candidates.Add(MoveEdit.NewMenuAsset);
            if (scan.ModularAvatarInstalled && scan.Avatar != null && !EditorUtility.IsPersistent(scan.Avatar) &&
                (MaChildren(parent) || parent.Parent == null || parent.SubMenuAsset != null || parent.Added == MoveEdit.NewMenuAsset))
                check.Candidates.Add(MoveEdit.NewMaItem);
            if (check.Candidates.Count == 0)
            {
                check.Edit = MoveEdit.Invalid;
                check.Errors.Add("No supported edit adds a menu to " + parentName + " (it needs a menu asset, or Modular Avatar; VRCFury cannot make an empty submenu).");
                return check;
            }
            // Who owns the parent: a native menu gets a menu asset, an MA children submenu an MA item; the user chooses otherwise.
            MoveEdit preferred = check.Candidates.Count == 1 ? check.Candidates[0]
                : parent.Origin == MenuOrigin.Native && check.Candidates.Contains(MoveEdit.NewMenuAsset) ? MoveEdit.NewMenuAsset
                : MaChildren(parent) ? MoveEdit.NewMaItem : MoveEdit.Ambiguous;
            MoveEdit chosen;
            if (!string.IsNullOrEmpty(move.Choice) && Enum.TryParse(move.Choice, out chosen) && check.Candidates.Contains(chosen)) check.Edit = chosen;
            else check.Edit = preferred;
            if (check.Edit == MoveEdit.Ambiguous) check.Warnings.Add("More than one source could add this menu: choose one.");
            Affected(scan, check);
            return check;
        }

        private static MoveCheck CheckAddControl(MenuScan scan, MenuEntry root, PlannedMove move)
        {
            string name = move.NewControl.Trim();
            var type = (VRCExpressionsMenu.Control.ControlType)move.ControlType;
            string parentName = string.IsNullOrEmpty(move.To) ? "the root menu" : move.To;
            var check = new MoveCheck { Move = move, Before = "New control" };
            check.Destination = root.Find(move.To ?? "");
            check.After = (string.IsNullOrEmpty(move.To) ? "" : move.To + "/") + name;
            if (name.Length == 0 || name.Contains("/")) check.Errors.Add("A control name cannot be empty or contain /.");
            else if (check.Destination == null || !check.Destination.IsSubMenu) check.Errors.Add("The parent " + parentName + " is no longer a submenu.");
            else if (check.Destination.Children.Any(child => child.Name == name)) check.Errors.Add(parentName + " already has an item named " + name + ".");
            if (check.Errors.Count > 0) return check;
            int count = check.Destination.Children.Count + 1;
            if (count > MenuToolScanner.MaxControls)
            {
                if (scan.VrcfuryInstalled) check.Warnings.Add(parentName + " will hold " + count + " controls: VRCFury pages the rest into a Next submenu.");
                else check.Errors.Add(parentName + " would hold " + count + " controls; VRChat allows " + MenuToolScanner.MaxControls + ".");
            }
            MenuEntry parent = check.Destination;
            if (scan.Avatar != null && NativeAsset(scan, parent)) check.Candidates.Add(MoveEdit.NewControlAsset);
            if (scan.ModularAvatarInstalled && scan.Avatar != null && !EditorUtility.IsPersistent(scan.Avatar) &&
                (MaChildren(parent) || parent.Parent == null || parent.SubMenuAsset != null || parent.Added == MoveEdit.NewMenuAsset))
                check.Candidates.Add(MoveEdit.NewControlMa);
            if (scan.VrcfuryInstalled && scan.Avatar != null && type == VRCExpressionsMenu.Control.ControlType.Toggle)
                check.Candidates.Add(MoveEdit.NewControlVrcfury);
            Decide(check, parent.Origin == MenuOrigin.Native && check.Candidates.Contains(MoveEdit.NewControlAsset) ? MoveEdit.NewControlAsset
                : MaChildren(parent) && check.Candidates.Contains(MoveEdit.NewControlMa) ? MoveEdit.NewControlMa : MoveEdit.Ambiguous,
                "No supported edit adds a control to " + parentName + ".");
            if (check.Errors.Count > 0) return check;
            // VRCFury makes its toggle's parameter (a global one when named); the others need a declared one.
            string parameter = (move.Parameter ?? "").Trim();
            if (check.Edit != MoveEdit.NewControlVrcfury && parameter.Length > 0 && scan.Param(parameter) == null)
                check.Warnings.Add(parameter + " is not a declared parameter: add it to the avatar's parameters.");
            if (Axes(type) > 1) check.Warnings.Add("Only the first axis parameter is set; set the others in the source after Apply.");
            if (check.Edit == MoveEdit.NewControlAsset)
            {
                UnityEngine.Object parentMenu = parent.Parent == null ? scan.Avatar.expressionsMenu : parent.SubMenuAsset;
                if (parentMenu != null) check.Affected.Add(parentMenu);
            }
            else if (check.Edit == MoveEdit.NewControlMa) check.Affected.Add(parent.MaChildrenSource != null ? (UnityEngine.Object)parent.MaChildrenSource : scan.Avatar.gameObject);
            else if (check.Edit == MoveEdit.NewControlVrcfury) check.Affected.Add(scan.Avatar.gameObject);
            return check;
        }

        // A native submenu with its own menu asset (the root: the descriptor's menu), or one the plan adds as a menu asset.
        private static bool NativeAsset(MenuScan scan, MenuEntry menu)
        {
            if (menu.Origin != MenuOrigin.Native) return false;
            if (menu.Parent == null) return scan.Avatar != null && scan.Avatar.expressionsMenu != null;
            return menu.SubMenuAsset != null || menu.Added == MoveEdit.NewMenuAsset;
        }

        // A Modular Avatar submenu whose items are an object's children, or one the plan adds as an MA item.
        private static bool MaChildren(MenuEntry menu)
        {
            return menu.MaChildrenSource != null || menu.Added == MoveEdit.NewMaItem;
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
                    // A menu the plan adds counts as made: Apply makes it before the moves after it.
                    if (NativeAsset(scan, destination) && (entry.OwnerMenu != null || entry.Added == MoveEdit.NewMenuAsset || entry.Added == MoveEdit.NewControlAsset))
                        check.Candidates.Add(MoveEdit.NativeAssets);
                    if (vrcfuryRule) check.Candidates.Add(MoveEdit.VrcfuryRule);
                    break;
                case MenuOrigin.ModularAvatar:
                    if ((entry.MaItem != null && CanReparent(entry.MaItem.gameObject) || entry.Added == MoveEdit.NewMaItem || entry.Added == MoveEdit.NewControlMa) && MaChildren(destination))
                        check.Candidates.Add(MoveEdit.MaReparent);
                    if (entry.MaInstaller != null && (entry.MaItem == null || entry.MaItem.gameObject == entry.MaInstaller.gameObject) && entry.Parent != null &&
                        IsInstallRoot(entry) && !MaChildren(destination) &&
                        (destination.Parent == null || destination.SubMenuAsset != null || destination.Added == MoveEdit.NewMenuAsset) && !sameMenu)
                        check.Candidates.Add(MoveEdit.MaRetarget);
                    if (vrcfuryRule) check.Candidates.Add(MoveEdit.VrcfuryRule);
                    break;
            }
            if (sameMenu && entry.Origin == MenuOrigin.ModularAvatar && entry.Parent != null && MaChildren(entry.Parent) &&
                (entry.MaItem != null && CanReparent(entry.MaItem.gameObject) || entry.Added == MoveEdit.NewMaItem) && !check.Candidates.Contains(MoveEdit.MaReparent))
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
                case MoveEdit.NewMenuAsset:
                    UnityEngine.Object parentMenu = check.Destination.Parent == null ? scan.Avatar.expressionsMenu : check.Destination.SubMenuAsset;
                    if (parentMenu != null) check.Affected.Add(parentMenu);
                    break;
                case MoveEdit.NewMaItem:
                    if (check.Destination.MaChildrenSource != null) check.Affected.Add(check.Destination.MaChildrenSource);
                    else if (scan.Avatar != null) check.Affected.Add(scan.Avatar.gameObject);
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
                case MoveEdit.NewMenuAsset: return "New menu asset";
                case MoveEdit.NewMaItem: return "New MA Menu Item";
                case MoveEdit.NewControlAsset: return "New control in the menu asset";
                case MoveEdit.NewControlMa: return "New MA Menu Item";
                case MoveEdit.NewControlVrcfury: return "New VRCFury Toggle";
                case MoveEdit.IconAsset: return "Icon in the menu asset";
                case MoveEdit.IconVrcfury: return "Set Icon (VRCFury)";
                case MoveEdit.IconMa: return "Icon on the MA Menu Item";
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
// PlannedMove                From (path), To (submenu path, "" root), Index (-1 end), Choice (the chosen MoveEdit when several fit),
//                            NewMenu (Add menu: the new submenu's name, added at the end of To; IsAdd); NewControl, ControlType,
//                            Parameter, Value, Icon (Add New Control, IsAddControl; Icon a texture GUID, IconTexture); AssignIcon
//                            (Assign Menu Icon: From gets Icon).
// MenuPlan (this file)       The pathing JSON: UserSettings/RiryTools/MenuTool/<Key>.json; Key is the avatar's stamped GUID
//                            (MenuToolAvatarKeys), kept in the plan so it is only ever saved to its own avatar's file. Load / Save
//                            (an empty plan deletes the file); Json is what Save writes. Only Apply changes the sources.
// MenuToolAvatarKeys         A GUID per avatar, in UserSettings/RiryTools/MenuTool/avatars.json (GlobalObjectId -> key); an avatar
//                            in an unsaved scene keeps its key for the session (SessionState RiryTools.MenuTool.UnsavedAvatars,
//                            by instance id) until its scene is saved (sceneSaved), then it joins avatars.json.
// MoveEdit                   NativeAssets (move the control between menu assets), VrcfuryRule (avatar-level Move / Reorder Menu
//                            Item), MaReparent (MA Menu Item object), MaRetarget (MA installer's target menu), NewMenuAsset and
//                            NewMaItem (Add menu), NewControlAsset / NewControlMa / NewControlVrcfury (Add New Control),
//                            IconAsset / IconVrcfury / IconMa (Assign Menu Icon), Ambiguous, Invalid.
// MoveCheck                  A move checked against the menu: Entry, Destination, Before/After paths, Edit, Candidates, Errors,
//                            Warnings (paging, same name), Affected sources; Ok when it can be applied.
// MenuToolPlanner
//   Planned(scan, plan)      A clone of the assembled menu with each move applied in order (the preview), plus every check; an
//                            Add menu puts a new empty submenu (MenuEntry.Added) at the end of its parent.
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
//   CheckIcon                Assign Menu Icon: the entry and texture exist, not a pagination page; IconAsset (native control),
//                            IconMa (MA item), IconVrcfury (VRCFury installed, any entry); the owner's edit is preferred.
//   Decide                   The user's Choice, else the preferred edit (the only one when single); none: Invalid.
//   NativeAsset / MaChildren Whether a submenu takes native moves and adds (its own asset) or MA reparenting (a children object),
//                            counting menus the plan adds.
