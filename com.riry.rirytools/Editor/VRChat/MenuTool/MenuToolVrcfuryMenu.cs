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
    /// <summary>
    /// VRCFury's menu path rules (its MenuManager), applied to the assembled menu. In a path, "\/" is a slash inside a
    /// name. A name matches a control's exactly; failing that, ignoring case, rich-text tags, line breaks and repeated
    /// spaces; failing that, any rich-text-separated part of the control's name. Folders match submenus only, and
    /// "Name.dup.N" picks the Nth match. Move, reorder and set icon act on every matching control.
    /// </summary>
    internal static class MenuToolVrcfuryMenu
    {
        private const string RealSlash = "REALSLASH";

        /// <summary>VRCFury's SplitPath: the names in a path ("\/" kept as a slash in a name); none for a blank path.</summary>
        internal static List<string> Split(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return new List<string>();
            return path.Replace("\\/", RealSlash).Split('/').Select(part => part.Replace(RealSlash, "/")).ToList();
        }

        /// <summary>A name as a VRCFury path part: its slashes written "\/".</summary>
        internal static string Escape(string name)
        {
            return (name ?? "").Replace("/", "\\/");
        }

        /// <summary>An entry's path as VRCFury reads it ("" for the root).</summary>
        internal static string PathOf(MenuToolEntry entry)
        {
            if (entry == null || entry.Parent == null) return "";
            string parent = PathOf(entry.Parent);
            return parent.Length == 0 ? Escape(entry.Name) : parent + "/" + Escape(entry.Name);
        }

        /// <summary>
        /// The path with its leading from rewritten to to, when it starts with from (each name matching as SamePath); null
        /// when it does not.
        /// </summary>
        internal static string Rebase(string path, string from, string to)
        {
            List<string> parts = Split(path), prefix = Split(from);
            if (prefix.Count == 0 || parts.Count < prefix.Count) return null;
            if (!SamePath(string.Join("/", parts.Take(prefix.Count).Select(Escape)), from)) return null;
            return string.Join("/", Split(to).Concat(parts.Skip(prefix.Count)).Select(Escape));
        }

        /// <summary>Whether two VRCFury paths name the same place: the same number of names, each matching the other's.</summary>
        internal static bool SamePath(string a, string b)
        {
            List<string> first = Split(a), second = Split(b);
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                if (first[i] != second[i] && (string.IsNullOrWhiteSpace(Normalize(first[i])) || Normalize(first[i]) != Normalize(second[i]))) return false;
            }
            return true;
        }

        private static string Normalize(string name)
        {
            string text = Regex.Replace((name ?? "").ToLower(), "<.*?>", "").Replace("\n", " ").Replace("\\n", " ");
            return Regex.Replace(text, @"\s\s+", " ").Trim();
        }

        private static IEnumerable<string> Slugs(string name)
        {
            return Regex.Replace(name ?? "", "<.*?>", "`").Split('`').Select(Normalize).Where(slug => !string.IsNullOrWhiteSpace(slug));
        }

        /// <summary>VRCFury's FindControlsWithName: the controls of a menu a name matches, by the first rule that matches any.</summary>
        internal static List<MenuToolEntry> Matching(MenuToolEntry menu, string name, Func<MenuToolEntry, bool> predicate = null)
        {
            name = name ?? "";
            var rules = new Func<string, string, bool>[]
            {
                (a, b) => a == b,
                (a, b) => !string.IsNullOrWhiteSpace(Normalize(a)) && Normalize(a) == Normalize(b),
                (a, b) => !string.IsNullOrWhiteSpace(Normalize(a)) && Slugs(b).Contains(Normalize(a))
            };
            foreach (Func<string, string, bool> rule in rules)
            {
                List<MenuToolEntry> matches = menu.Children.Where(child => (predicate == null || predicate(child)) && rule(name, child.Name)).ToList();
                if (matches.Count > 0) return matches;
            }
            return new List<MenuToolEntry>();
        }

        /// <summary>
        /// VRCFury's GetSubmenu: the submenu at a path under root, made where missing when create (else null). A submenu
        /// made for the last name is createFrom, when given, renamed.
        /// </summary>
        internal static MenuToolEntry Folder(MenuToolEntry root, IList<string> path, bool create, Func<string, MenuToolEntry> make, MenuToolEntry createFrom = null)
        {
            MenuToolEntry current = root;
            for (int i = 0; i < path.Count; i++)
            {
                string name = path[i];
                int offset = 0;
                int duplicate = name.IndexOf(".dup.", StringComparison.Ordinal);
                if (duplicate >= 0)
                {
                    int.TryParse(name.Substring(duplicate + 5), out offset);
                    name = name.Substring(0, duplicate);
                }
                List<MenuToolEntry> folders = Matching(current, name, child => child.IsSubMenu);
                MenuToolEntry folder = offset < folders.Count ? folders[offset] : null;
                if (folder == null)
                {
                    if (!create) return null;
                    folder = createFrom != null && i == path.Count - 1 ? createFrom : make(name);
                    folder.Name = name;
                    folder.Type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                    current.Add(folder);
                }
                current = folder;
            }
            return current;
        }

        /// <summary>
        /// VRCFury's MergeMenu: each submenu goes into the Nth submenu already there that matches it (N counting earlier
        /// submenus of its name among these), its controls following; with none, it is added. Other controls are appended.
        /// </summary>
        internal static void Merge(MenuToolEntry into, IEnumerable<MenuToolEntry> controls)
        {
            var seen = new Dictionary<string, int>();
            foreach (MenuToolEntry control in controls.ToList())
            {
                if (!control.IsSubMenu)
                {
                    into.Add(control);
                    continue;
                }
                int count;
                int duplicate = seen[control.Name] = seen.TryGetValue(control.Name, out count) ? count + 1 : 0;
                List<MenuToolEntry> folders = Matching(into, control.Name, child => child.IsSubMenu);
                List<MenuToolEntry> below = control.Children.ToList();
                MenuToolEntry folder = duplicate < folders.Count ? folders[duplicate] : null;
                if (folder == null)
                {
                    control.Children.Clear();
                    into.Add(control);
                    folder = control;
                }
                Merge(folder, below);
            }
        }

        private static List<string> Prefix(List<string> path)
        {
            return path.Take(Math.Max(0, path.Count - 1)).ToList();
        }

        private static string Leaf(List<string> path)
        {
            return path.Count > 0 ? path[path.Count - 1] : "";
        }

        // The controls a path names: in the submenu its folders lead to (none made), those its last name matches.
        private static List<MenuToolEntry> Named(MenuToolEntry root, string path, Func<string, MenuToolEntry> make, out MenuToolEntry menu)
        {
            List<string> split = Split(path);
            menu = Folder(root, Prefix(split), false, make);
            return menu != null ? Matching(menu, Leaf(split)) : new List<MenuToolEntry>();
        }

        private static void Detach(MenuToolEntry entry)
        {
            if (entry.Parent != null) entry.Parent.Children.Remove(entry);
            entry.Parent = null;
        }

        /// <summary>
        /// VRCFury's Move: every control the path names leaves its menu; a blank destination deletes them. A submenu goes to
        /// the destination folder (made from it if missing) and its controls merge there; another control takes the
        /// destination's last name and joins the folder above it. False when the path names nothing.
        /// </summary>
        internal static bool Move(MenuToolEntry root, string from, string to, Func<string, MenuToolEntry> make)
        {
            MenuToolEntry menu;
            List<MenuToolEntry> controls = Named(root, from, make, out menu);
            if (controls.Count == 0) return false;
            foreach (MenuToolEntry control in controls) Detach(control);
            if (string.IsNullOrWhiteSpace(to)) return true;
            List<string> destination = Split(to);
            MenuToolEntry folder = Folder(root, Prefix(destination), true, make);
            foreach (MenuToolEntry control in controls)
            {
                if (control.IsSubMenu)
                {
                    List<MenuToolEntry> below = control.Children.ToList();
                    control.Children.Clear();
                    Merge(Folder(root, destination, true, make, control), below);
                }
                else
                {
                    control.Name = Leaf(destination);
                    Merge(folder, new[] { control });
                }
            }
            return true;
        }

        /// <summary>VRCFury's Reorder: the controls a path names move together to a position (from the end when negative), clamped.</summary>
        internal static bool Reorder(MenuToolEntry root, string path, int position, Func<string, MenuToolEntry> make)
        {
            MenuToolEntry menu;
            List<MenuToolEntry> controls = Named(root, path, make, out menu);
            if (controls.Count == 0) return false;
            foreach (MenuToolEntry control in controls) menu.Children.Remove(control);
            if (position < 0) position = menu.Children.Count + position;
            menu.Children.InsertRange(Mathf.Clamp(position, 0, menu.Children.Count), controls);
            return true;
        }

        /// <summary>VRCFury's SetIcon: every control the path names gets the icon; true when one changed.</summary>
        internal static bool SetIcon(MenuToolEntry root, string path, Texture2D icon, Func<string, MenuToolEntry> make)
        {
            MenuToolEntry menu;
            bool changed = false;
            foreach (MenuToolEntry control in Named(root, path, make, out menu))
            {
                if (control.Icon == icon) continue;
                control.Icon = icon;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// The rules in VRCFury's order: icons are tried first and again after each move that happened (each applied once);
        /// moves in order; reorders last, after VRCFury's sort. Rules naming nothing are noted.
        /// </summary>
        internal static void Apply(MenuToolScan scan, List<MenuToolVrcfuryMenuRule> rules, Func<MenuToolVrcfuryMenuRule, Func<string, MenuToolEntry>> maker)
        {
            List<MenuToolVrcfuryMenuRule> icons = rules.Where(rule => rule.Kind == "SetIcon").ToList();
            Action betweenSteps = () =>
            {
                foreach (MenuToolVrcfuryMenuRule icon in icons.ToList())
                {
                    if (SetIcon(scan.Root, icon.Path, icon.Icon, maker(icon))) icons.Remove(icon);
                }
            };
            betweenSteps();
            foreach (MenuToolVrcfuryMenuRule move in rules.Where(rule => rule.Kind == "MoveMenuItem"))
            {
                if (Move(scan.Root, move.Path, move.To, maker(move))) betweenSteps();
                else scan.Note("VRCFury Move Menu Item on " + move.Component.gameObject.name + ": " + move.Path + " is not in the menu.");
            }
            foreach (MenuToolVrcfuryMenuRule reorder in rules.Where(rule => rule.Kind == "ReorderMenuItem"))
            {
                if (!Reorder(scan.Root, reorder.Path, reorder.Position, maker(reorder)))
                    scan.Note("VRCFury Reorder Menu Item on " + reorder.Component.gameObject.name + ": " + reorder.Path + " is not in the menu.");
            }
            foreach (MenuToolVrcfuryMenuRule icon in icons)
            {
                MenuToolEntry menu;
                if (Named(scan.Root, icon.Path, maker(icon), out menu).Count == 0)
                    scan.Note("VRCFury Set Icon on " + icon.Component.gameObject.name + ": " + icon.Path + " is not in the menu.");
            }
        }
    }
}

// ============================================================================
// GLOSSARY: VRCFury menu path rules
// ============================================================================
//
// MenuToolVrcfuryMenu        VRCFury's MenuManager path rules on the assembled menu: Split ("\/" a slash in a name), Escape /
//                            PathOf (an entry's path as VRCFury reads it), SamePath, Matching (exact, then ignoring case,
//                            rich text, line breaks and spaces, then any rich-text-separated part), Folder (GetSubmenu: submenus
//                            only, "Name.dup.N", made where missing), Merge (MergeMenu), Move (every match; blank destination
//                            deletes), Reorder (every match; negative from the end; clamped), SetIcon (every match), Apply
//                            (icons first and after each move, moves in order, reorders last; misses noted).
