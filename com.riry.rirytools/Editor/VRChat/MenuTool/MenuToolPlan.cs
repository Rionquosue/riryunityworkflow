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
    internal sealed class MenuToolPlannedMove
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
        // Remove Menu Item: the entry at From is removed.
        public bool Remove;
        // Destructive Move: edits the item's own source (its VRCFury feature's path, or the menu asset or MA item it comes
        // from) instead of adding an override, inside its menu root.
        public bool Destructive;

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
    internal sealed class MenuToolPlan
    {
        public string Avatar = "";
        // The avatar's key (MenuToolAvatarKeys): the plan is always saved to its own avatar's file.
        public string Key = "";
        public List<MenuToolPlannedMove> Moves = new List<MenuToolPlannedMove>();

        internal static string Folder
        {
            get { return Path.Combine(RiryToolsPaths.ProjectRoot, "UserSettings", "RiryTools", "MenuTool"); }
        }

        private string FilePath
        {
            get { return Path.Combine(Folder, Key + ".json"); }
        }

        internal static MenuToolPlan Load(GameObject avatar)
        {
            var plan = new MenuToolPlan { Avatar = avatar != null ? avatar.name : "" };
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

    /// <summary>How a planned move would be applied, from who owns the entry and the destination.</summary>
    internal enum MenuToolMoveEdit
    {
        // Move the control between menu assets (copying shared ones first).
        NativeAssets,
        // Add or update an avatar-level VRCFury Move Menu Item (or Reorder Menu Item) rule.
        VrcfuryRule,
#if RIRYTOOLS_MENUTOOL_MA
        // Reparent the Modular Avatar menu item's object.
        MaReparent,
        // Retarget the Modular Avatar installer of the whole installed block.
        MaRetarget,
#endif
        // Add menu: a new menu asset, linked by a SubMenu control in the parent's menu asset (copying a shared one first).
        NewMenuAsset,
#if RIRYTOOLS_MENUTOOL_MA
        // Add menu: a new object with a Modular Avatar Menu Item (SubMenu, children from its own object), under the parent's
        // MA children object, or under the avatar with an MA Menu Installer targeting the parent menu.
        NewMaItem,
#endif
        // Add New Control: a control in the parent's menu asset, an MA Menu Item object (as NewMaItem), or a VRCFury Toggle.
        NewControlAsset,
#if RIRYTOOLS_MENUTOOL_MA
        NewControlMa,
#endif
        NewControlVrcfury,
        // Assign Menu Icon: the control's icon in its menu asset, a VRCFury Set Icon rule, or the MA Menu Item's icon.
        IconAsset,
        IconVrcfury,
#if RIRYTOOLS_MENUTOOL_MA
        IconMa,
#endif
        // Remove Menu Item: the control deleted from its menu asset (copying a shared one first), or a VRCFury Move Menu Item
        // rule with a blank destination.
        RemoveAsset,
        RemoveVrcfury,
        // Destructive Move: the control moved between its own source's menu assets, in place (no copy), or a VRCFury Toggle or
        // Puppet's menu path rewritten. (An MA Menu Item object is moved with MaReparent.)
        DestructiveAsset,
        DestructivePath,
        // Ownership unclear or unsupported: the user picks one of the candidates.
        Ambiguous,
        Invalid
    }

    /// <summary>A planned move checked against the current menu.</summary>
    internal sealed class MenuToolMoveCheck
    {
        internal MenuToolPlannedMove Move;
        internal MenuToolEntry Entry;
        internal MenuToolEntry Destination;
        internal string Before = "";
        internal string After = "";
        internal MenuToolMoveEdit Edit = MenuToolMoveEdit.Invalid;
        internal readonly List<MenuToolMoveEdit> Candidates = new List<MenuToolMoveEdit>();
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<UnityEngine.Object> Affected = new List<UnityEngine.Object>();

        internal bool Ok
        {
            get { return Errors.Count == 0 && Edit != MenuToolMoveEdit.Ambiguous && Edit != MenuToolMoveEdit.Invalid; }
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool plan
// The checks are in MenuToolPlanner.cs; avatar keys in MenuToolAvatarKeys.cs.
// ============================================================================
//
// MenuToolPlannedMove                From (path), To (submenu path, "" root), Index (-1 end), Choice (the chosen MenuToolMoveEdit when several fit),
//                            NewMenu (Add menu: the new submenu's name, added at the end of To; IsAdd); NewControl, ControlType,
//                            Parameter, Value, Icon (Add New Control, IsAddControl; Icon a texture GUID, IconTexture); AssignIcon
//                            (Assign Menu Icon: From gets Icon).
// MenuToolPlan (this file)       The pathing JSON: UserSettings/RiryTools/MenuTool/<Key>.json; Key is the avatar's stamped GUID
//                            (MenuToolAvatarKeys), kept in the plan so it is only ever saved to its own avatar's file. Load / Save
//                            (an empty plan deletes the file); Json is what Save writes. Only Apply changes the sources.
// MenuToolMoveEdit                   NativeAssets (move the control between menu assets), VrcfuryRule (avatar-level Move / Reorder Menu
//                            Item), MaReparent (MA Menu Item object), MaRetarget (MA installer's target menu), NewMenuAsset and
//                            NewMaItem (Add menu), NewControlAsset / NewControlMa / NewControlVrcfury (Add New Control),
//                            IconAsset / IconVrcfury / IconMa (Assign Menu Icon), Ambiguous, Invalid.
// MenuToolMoveCheck                  A move checked against the menu: Entry, Destination, Before/After paths, Edit, Candidates, Errors,
//                            Warnings (paging, same name), Affected sources; Ok when it can be applied.
